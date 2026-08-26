using System.Collections.Generic;
using System.Collections;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using Godmist.Views.Combat;
using Godmist.Views.UI.Workspace;
using UnityEngine;

namespace Godmist.Views.UI
{
    public class CombatFloatingFeedbackController : MonoBehaviour
    {
        [SerializeField] private RectTransform feedbackRoot;
        [SerializeField] private CombatFloatingFeedbackView feedbackTemplate;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private CombatUnitViewSpawner unitViewSpawner;
        [SerializeField] private Vector2 worldOffset = new(0f, 1.05f);
        [SerializeField, Min(0f)] private float minimumWorldOffsetY = 1.05f;
        [SerializeField, Min(0f)] private float entryDelay = 0.4f;
        [Header("Lifetime")]
        [SerializeField, Min(0f)] private float lingerDuration = 3f;
        [SerializeField, Min(0f)] private float fadeDuration = 3f;
        [Header("Intro Drift")]
        [SerializeField, Min(0f)] private float introDriftDuration = 0.1f;
        [SerializeField, Min(0f)] private float introDriftDistance = 18f;
        [Header("Layout")]
        [SerializeField, Min(0f)] private float layoutHorizontalRange = 72f;
        [SerializeField, Min(0f)] private float layoutVerticalRange = 48f;
        [SerializeField, Range(1, 32)] private int layoutCandidateCount = 18;
        [SerializeField, Min(0f)] private float horizontalDrift = 48f;
        [SerializeField, Min(0f)] private float textCollisionPadding = 8f;
        [SerializeField] private bool showResourceFloatingText;
        [SerializeField] private bool showActionPointFloatingText;

        private readonly Dictionary<BattleUnit, Queue<PendingFeedbackEntry>> pendingEntries = new();
        private readonly HashSet<BattleUnit> activeQueues = new();
        private readonly List<CombatFloatingFeedbackView> activeFeedbackViews = new();
        private readonly Dictionary<CombatFloatingFeedbackView, BattleUnit>
            activeFeedbackTargets = new();
        private readonly Dictionary<CombatFloatingFeedbackView, Vector2>
            activeFeedbackSizes = new();
        private CombatFeedbackBus feedbackBus;

        public void Bind(CombatFeedbackBus nextFeedbackBus)
        {
            if (feedbackBus != null) feedbackBus.Published -= HandleFeedback;
            feedbackBus = nextFeedbackBus;
            if (feedbackBus != null) feedbackBus.Published += HandleFeedback;
        }

        private void Awake()
        {
            worldCamera ??= Camera.main;
            feedbackRoot ??= transform as RectTransform;
            unitViewSpawner ??= FindAnyObjectByType<CombatUnitViewSpawner>();
        }

        private void OnDestroy()
        {
            if (feedbackBus != null) feedbackBus.Published -= HandleFeedback;
        }

        private void HandleFeedback(CombatFeedbackEvent feedbackEvent)
        {
            if (!ShouldShowFeedback(feedbackEvent))
            {
                return;
            }

            if (feedbackRoot == null || feedbackTemplate == null || unitViewSpawner == null
                || worldCamera == null) return;

            if (!pendingEntries.TryGetValue(feedbackEvent.Target, out var queue))
            {
                queue = new Queue<PendingFeedbackEntry>();
                pendingEntries.Add(feedbackEvent.Target, queue);
            }

            queue.Enqueue(new PendingFeedbackEntry(feedbackEvent));
            if (activeQueues.Add(feedbackEvent.Target))
            {
                StartCoroutine(ShowQueuedFeedback(feedbackEvent.Target));
            }
        }

        private bool ShouldShowFeedback(CombatFeedbackEvent feedbackEvent)
        {
            return feedbackEvent.Type switch
            {
                CombatFeedbackType.ResourceGain or CombatFeedbackType.ResourceSpend
                    => showResourceFloatingText,
                CombatFeedbackType.ActionPointSpend => showActionPointFloatingText,
                _ => true
            };
        }

        private IEnumerator ShowQueuedFeedback(BattleUnit target)
        {
            while (pendingEntries.TryGetValue(target, out var queue) && queue.Count > 0)
            {
                var pendingEntry = queue.Dequeue();
                var feedbackEvent = pendingEntry.Event;
                if (unitViewSpawner != null
                    && (unitViewSpawner.TryGetWorldPosition(
                            feedbackEvent.Target,
                            out var worldPosition)
                        || unitViewSpawner.TryGetWorldPosition(
                            feedbackEvent.Target?.Position ?? feedbackEvent.Position,
                            out worldPosition)))
                {
                    var offset = worldOffset;
                    offset.y = Mathf.Max(offset.y, minimumWorldOffsetY);
                    var screenPosition = worldCamera.WorldToScreenPoint(
                        worldPosition + (Vector3)offset);
                    var canvas = feedbackRoot.GetComponentInParent<Canvas>();
                    var uiCamera = canvas != null
                        && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                        ? worldCamera
                        : null;
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                            feedbackRoot, screenPosition, uiCamera, out var localPosition))
                    {
                        var emphasized = feedbackEvent.Type == CombatFeedbackType.Critical;
                        var text = GetText(feedbackEvent);
                        var textSize = feedbackTemplate.GetLayoutSize(text, emphasized);
                        var anchorPosition = localPosition;
                        localPosition = FindAvailablePosition(
                            localPosition,
                            feedbackEvent.Target,
                            textSize);
                        var horizontalDirection = Mathf.Sign(
                            localPosition.x - anchorPosition.x);
                        var drift = horizontalDirection == 0f
                            ? Random.Range(-horizontalDrift, horizontalDrift)
                            : horizontalDirection * Random.Range(
                                horizontalDrift * 0.75f,
                                horizontalDrift);
                        var view = Instantiate(feedbackTemplate, feedbackRoot);
                        view.ConfigureLifetime(lingerDuration, fadeDuration);
                        view.ConfigureIntroDrift(introDriftDuration, introDriftDistance);
                        view.Show(text, GetColor(feedbackEvent),
                            localPosition, 0f,
                            emphasized,
                            drift);
                        activeFeedbackViews.Add(view);
                        activeFeedbackTargets[view] = feedbackEvent.Target;
                        activeFeedbackSizes[view] = textSize;
                    }
                }

                yield return new WaitForSecondsRealtime(Mathf.Max(0f, entryDelay));
            }

            pendingEntries.Remove(target);
            activeQueues.Remove(target);
        }

        private Vector2 FindAvailablePosition(
            Vector2 anchor,
            BattleUnit target,
            Vector2 textSize)
        {
            PruneDestroyedFeedbackViews();

            var fallback = anchor;
            var bestScore = float.PositiveInfinity;

            // Sample a bounded cloud around the unit. Unlike fixed slots, the
            // result remains organic; unlike the old vertical stack, a burst can
            // never push a message arbitrarily far above its source.
            for (var candidateIndex = 0;
                 candidateIndex < Mathf.Max(1, layoutCandidateCount);
                 candidateIndex++)
            {
                var candidate = GetRandomLayoutCandidate(anchor);
                var score = GetPlacementScore(candidate, anchor, textSize, target);
                if (score < bestScore)
                {
                    fallback = candidate;
                    bestScore = score;
                }
            }

            return fallback;
        }

        private Vector2 GetRandomLayoutCandidate(Vector2 anchor)
        {
            var x = Random.Range(-layoutHorizontalRange, layoutHorizontalRange);
            var y = Random.Range(-layoutVerticalRange * 0.25f, layoutVerticalRange);
            return anchor + new Vector2(x, y);
        }

        private float GetPlacementScore(
            Vector2 candidate,
            Vector2 anchor,
            Vector2 size,
            BattleUnit target)
        {
            var candidateRect = CreateFeedbackRect(candidate, size);
            var score = (candidate - anchor).sqrMagnitude;

            foreach (var view in activeFeedbackViews)
            {
                if (view == null
                    || !activeFeedbackTargets.TryGetValue(view, out var viewTarget)
                    || viewTarget != target)
                {
                    continue;
                }

                var viewRect = view.GetComponent<RectTransform>();
                if (viewRect == null)
                {
                    continue;
                }

                var viewSize = activeFeedbackSizes.TryGetValue(view, out var knownSize)
                    ? knownSize
                    : viewRect.rect.size;
                var occupiedRect = CreateFeedbackRect(
                    view.LayoutPosition,
                    viewSize);
                score += GetOverlapArea(candidateRect, occupiedRect) * 1000f;
            }

            return score;
        }

        private static float GetOverlapArea(Rect first, Rect second)
        {
            if (!first.Overlaps(second)) return 0f;

            var width = Mathf.Min(first.xMax, second.xMax)
                - Mathf.Max(first.xMin, second.xMin);
            var height = Mathf.Min(first.yMax, second.yMax)
                - Mathf.Max(first.yMin, second.yMin);
            return Mathf.Max(0f, width) * Mathf.Max(0f, height);
        }

        private Rect CreateFeedbackRect(Vector2 center, Vector2 size)
        {
            var paddedSize = size + Vector2.one * (textCollisionPadding * 2f);
            return new Rect(
                center - paddedSize * 0.5f,
                paddedSize);
        }

        private void PruneDestroyedFeedbackViews()
        {
            for (var index = activeFeedbackViews.Count - 1; index >= 0; index--)
            {
                if (activeFeedbackViews[index] == null)
                {
                    var view = activeFeedbackViews[index];
                    activeFeedbackTargets.Remove(view);
                    activeFeedbackSizes.Remove(view);
                    activeFeedbackViews.RemoveAt(index);
                }
            }
        }

        private static string GetText(CombatFeedbackEvent feedbackEvent) => feedbackEvent.Type switch
        {
            CombatFeedbackType.Damage => $"-{feedbackEvent.Amount}",
            CombatFeedbackType.Heal => $"+{feedbackEvent.Amount}",
            CombatFeedbackType.ShieldGain => $"+{feedbackEvent.Amount}",
            CombatFeedbackType.ShieldLoss => $"-{feedbackEvent.Amount}",
            CombatFeedbackType.ResourceGain => $"+{feedbackEvent.Amount}",
            CombatFeedbackType.ResourceSpend => $"-{feedbackEvent.Amount}",
            CombatFeedbackType.ActionPointSpend => $"-{feedbackEvent.Amount} AP",
            CombatFeedbackType.Critical => "CRIT",
            CombatFeedbackType.Miss => "MISS",
            CombatFeedbackType.StatusApplied
                or CombatFeedbackType.BuffApplied
                or CombatFeedbackType.DebuffApplied
                or CombatFeedbackType.ProcTriggered
                or CombatFeedbackType.AuraApplied
                or CombatFeedbackType.ResourceRegenerationBlocked
                => ResolveEffectName(feedbackEvent.EffectName),
            CombatFeedbackType.DamageRedirected =>
                string.IsNullOrWhiteSpace(feedbackEvent.EffectName)
                    ? "REDIRECT"
                    : feedbackEvent.EffectName,
            _ => string.Empty
        };

        private static Color GetColor(CombatFeedbackEvent feedbackEvent) => feedbackEvent.Type switch
        {
            CombatFeedbackType.Damage => GetDamageColor(feedbackEvent.DamageType),
            CombatFeedbackType.Heal => new Color(0.28f, 0.82f, 0.43f),
            CombatFeedbackType.ShieldGain or CombatFeedbackType.ShieldLoss => new Color(0.31f, 0.69f, 0.96f),
            CombatFeedbackType.ResourceGain or CombatFeedbackType.ResourceSpend => CombatResourceColors.Get(feedbackEvent.ResourceType ?? ResourceType.None),
            CombatFeedbackType.ActionPointSpend => new Color(0.86f, 0.88f, 0.92f),
            CombatFeedbackType.Critical => new Color(1f, 0.76f, 0.23f),
            CombatFeedbackType.Miss => new Color(0.78f, 0.69f, 0.54f),
            CombatFeedbackType.StatusApplied => new Color(0.86f, 0.86f, 0.96f),
            CombatFeedbackType.BuffApplied or CombatFeedbackType.AuraApplied
                => new Color(0.32f, 0.93f, 0.62f),
            CombatFeedbackType.DebuffApplied or CombatFeedbackType.ResourceRegenerationBlocked
                => new Color(1f, 0.48f, 0.39f),
            CombatFeedbackType.ProcTriggered => new Color(1f, 0.82f, 0.31f),
            CombatFeedbackType.DamageRedirected => new Color(0.78f, 0.62f, 1f),
            _ => Color.white
        };

        private static string ResolveEffectName(string effectName)
        {
            if (string.IsNullOrWhiteSpace(effectName)) return string.Empty;

            var glossaryTitle = UIWorkspaceTooltipView.ActiveInstance
                ?.GetGlossaryTitle(effectName);
            return string.IsNullOrWhiteSpace(glossaryTitle)
                ? FormatEffectId(effectName)
                : glossaryTitle;
        }

        private static string FormatEffectId(string effectId)
        {
            var words = effectId.Replace('_', ' ').Replace('-', ' ').Split(' ');
            for (var index = 0; index < words.Length; index++)
            {
                if (string.IsNullOrWhiteSpace(words[index])) continue;
                words[index] = char.ToUpperInvariant(words[index][0])
                    + words[index].Substring(1);
            }

            return string.Join(" ", words);
        }

        private static Color GetDamageColor(DamageType? damageType) => damageType switch
        {
            DamageType.Physical => new Color(0.73f, 0.31f, 0.31f),
            DamageType.Inevitable => new Color(0.94f, 0.18f, 0.18f),
            DamageType.Magical => new Color(0.43f, 0.40f, 0.74f),
            DamageType.Direct => new Color(0.46f, 0.28f, 0.92f),
            DamageType.Bleed => new Color(0.48f, 0.09f, 0.16f),
            DamageType.Poison => new Color(0.63f, 0.78f, 0.16f),
            DamageType.Burn => new Color(0.82f, 0.34f, 0.11f),
            _ => Color.white
        };

        private readonly struct PendingFeedbackEntry
        {
            public PendingFeedbackEntry(CombatFeedbackEvent feedbackEvent)
            {
                Event = feedbackEvent;
            }

            public CombatFeedbackEvent Event { get; }
        }
    }
}

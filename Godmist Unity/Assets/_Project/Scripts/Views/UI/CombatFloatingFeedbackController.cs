using System.Collections.Generic;
using System.Collections;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using Godmist.Views.Combat;
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

        private readonly Dictionary<BattleUnit, Queue<CombatFeedbackEvent>> pendingEntries = new();
        private readonly HashSet<BattleUnit> activeQueues = new();
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
            if (feedbackRoot == null || feedbackTemplate == null || unitViewSpawner == null
                || worldCamera == null
                || !unitViewSpawner.TryGetWorldPosition(feedbackEvent.Target, out _)) return;

            if (!pendingEntries.TryGetValue(feedbackEvent.Target, out var queue))
            {
                queue = new Queue<CombatFeedbackEvent>();
                pendingEntries.Add(feedbackEvent.Target, queue);
            }

            queue.Enqueue(feedbackEvent);
            if (activeQueues.Add(feedbackEvent.Target))
            {
                StartCoroutine(ShowQueuedFeedback(feedbackEvent.Target));
            }
        }

        private IEnumerator ShowQueuedFeedback(BattleUnit target)
        {
            while (pendingEntries.TryGetValue(target, out var queue) && queue.Count > 0)
            {
                var feedbackEvent = queue.Dequeue();
                if (unitViewSpawner != null
                    && unitViewSpawner.TryGetWorldPosition(target, out var worldPosition))
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
                        var view = Instantiate(feedbackTemplate, feedbackRoot);
                        view.Show(GetText(feedbackEvent), GetColor(feedbackEvent),
                            localPosition, 0f,
                            feedbackEvent.Type == CombatFeedbackType.Critical);
                    }
                }

                yield return new WaitForSecondsRealtime(Mathf.Max(0.4f, entryDelay));
            }

            pendingEntries.Remove(target);
            activeQueues.Remove(target);
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
            _ => Color.white
        };

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
    }
}

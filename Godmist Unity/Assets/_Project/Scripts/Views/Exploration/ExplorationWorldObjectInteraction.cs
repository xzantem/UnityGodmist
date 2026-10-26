using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Views.Combat;
using Godmist.Views.Dungeons;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using UnityEngine;

namespace Godmist.Views.Exploration
{
    // Explicitly attached by each owning view. No scene searches or prefab wiring.
    [DefaultExecutionOrder(-90)]
    public sealed class ExplorationWorldObjectInteraction : MonoBehaviour
    {
        private MonoBehaviour owner;
        private IExplorationWorldInteraction target;
        private System.Func<bool> canInteractOutsideExploration;
        private SpriteRenderer[] bodies;
        private readonly Dictionary<SpriteRenderer, CombatUnitVisualStateView> outlines = new();
        private bool highlighted;
        public IExplorationWorldInteraction Target => target;
        public ExplorationPartyController Party => target?.InteractionParty;
        public Vector2 Position => transform.position;
        public float InteractionRadius => target?.InteractionRadius ?? 0;

        public static ExplorationWorldObjectInteraction Attach(MonoBehaviour view, IExplorationWorldInteraction interaction,
            System.Func<bool> canInteractOutsideExploration = null)
        {
            var result = view.GetComponent<ExplorationWorldObjectInteraction>()
                ?? view.gameObject.AddComponent<ExplorationWorldObjectInteraction>();
            result.SetInspectionHighlight(false);
            result.owner = view;
            result.target = interaction;
            result.canInteractOutsideExploration = canInteractOutsideExploration;
            result.bodies = null; // Exit artwork is styled after Initialize.
            return result;
        }

        public bool CanRequestInteraction => owner != null && owner.isActiveAndEnabled && isActiveAndEnabled
            && target?.CanRequestInteraction == true
            && (Party?.CanRequestWorldInteraction == true || canInteractOutsideExploration?.Invoke() == true)
            && (target.InteractionFloor == null || ReferenceEquals(target.InteractionFloor, Party.InteractionFloor));

        public void InvalidatePresentation() { SetInspectionHighlight(false); bodies = null; }

        private void EnsureBodies()
        {
            if (bodies != null) return;
            var values = new List<SpriteRenderer>();
            foreach (var renderer in GetComponentsInChildren<SpriteRenderer>(true))
                if (renderer.name != "Visual State Outline") values.Add(renderer);
            bodies = values.ToArray();
        }

        public bool IsPointerHit(Vector2 point)
        {
            if (!CanRequestInteraction) return false;
            EnsureBodies();
            // Own the exit threshold, rather than distant decorations around it.
            var exit = owner is DungeonExitView;
            var threshold = exit && ((Vector2)transform.position-point).sqrMagnitude <= .85f*.85f;
            foreach (var renderer in bodies)
            {
                if (renderer == null || !renderer.enabled || renderer.forceRenderingOff || renderer.sprite == null) continue;
                if (threshold || renderer.bounds.Contains(new Vector3(point.x, point.y, renderer.bounds.center.z))) return true;
            }
            return false;
        }

        public bool CanInteractFrom(Vector2 point) => CanRequestInteraction && target.CanInteractFrom(point);
        public bool CanInteract() => CanRequestInteraction && target.CanInteract();
        public void Interact() { if (CanInteract()) target.Interact(); }

        private static bool PointerBlocked => DevConsoleView.IsOpen || UIWorkspaceInputRouter.IsPointerOverUi()
            || UIWorkspaceInputRouter.HasFocusedTextInput() || UIWorkspaceInputRouter.HasFocusedContextMenu();

        private void Update()
        {
            if (!CanRequestInteraction || PointerBlocked) { SetInspectionHighlight(false); return; }
            if (Input.GetKeyDown(target.InteractionKey) && TryKeyboardInteraction()) return;
            var point = Vector2.zero;
            var hovered = !Party.IsWorldPrimaryClickClaimed && Party.TryGetPointerWorldPosition(out point) && IsPointerHit(point);
            SetInspectionHighlight(hovered);
            if (hovered && Input.GetMouseButtonDown(0)) TryPrimaryInteraction(point);
        }

        public bool TryKeyboardInteraction()
        {
            if (PointerBlocked || !CanInteract() || Party.IsWorldInteractionClaimedThisFrame) return false;
            Party.ClaimWorldPrimaryClick(); Party.CancelMovement(); SetInspectionHighlight(false); Interact(); return true;
        }

        public bool TryPrimaryInteraction(Vector2 point)
        {
            if (PointerBlocked || !IsPointerHit(point) || Party.IsWorldPrimaryClickClaimed) return false;
            Party.ClaimWorldPrimaryClick();
            // Combat doors keep their paid interaction owner. Never queue an
            // exploration approach or fall through to tactical movement.
            if (Party.CanRequestWorldInteraction != true)
            {
                SetInspectionHighlight(false);
                Interact();
                return true;
            }
            if (!Party.RequestWorldObjectInteraction(this))
            {
                var feedback = new GameObject("World Object Interaction Feedback", typeof(TMPro.TextMeshPro), typeof(DungeonLootFloatingText));
                feedback.transform.SetParent(transform, false);
                feedback.transform.position = transform.position + Vector3.up * .6f;
                feedback.GetComponent<DungeonLootFloatingText>().Initialize("Nie można podejść do obiektu.");
            }
            return true; // A blocked approach still owns this click; never fall through to movement.
        }

        public void SetInspectionHighlight(bool value)
        {
            highlighted = value;
            if (!value) foreach (var visual in outlines.Values) if (visual != null) visual.ClearHighlight();
        }

        private void LateUpdate()
        {
            if (!highlighted) return;
            if (!CanRequestInteraction || PointerBlocked) { SetInspectionHighlight(false); return; }
            EnsureBodies();
            foreach (var renderer in bodies)
            {
                if (renderer == null) continue;
                var visible = renderer.enabled && !renderer.forceRenderingOff && renderer.sprite != null;
                var firstHighlight = false;
                if (!outlines.TryGetValue(renderer, out var visual))
                {
                    if (!visible) continue;
                    visual = renderer.GetComponent<CombatUnitVisualStateView>() ?? renderer.gameObject.AddComponent<CombatUnitVisualStateView>();
                    visual.SetPulseSpeed(0);
                    visual.SetOutlineWidth(1f / Mathf.Max(1, renderer.sprite.pixelsPerUnit));
                    outlines.Add(renderer, visual);
                    firstHighlight = true;
                }
                if (visible) visual.Refresh(null, false, true, false, true); else visual.ClearHighlight();
                // Refresh also initializes the existing outline component in
                // EditMode previews; inherit layers after those children exist.
                if (firstHighlight)
                    foreach (var child in renderer.GetComponentsInChildren<SpriteRenderer>(true)) child.gameObject.layer = renderer.gameObject.layer;
            }
        }

        private void OnDisable() => SetInspectionHighlight(false);
        private void OnDestroy() => SetInspectionHighlight(false);
    }
}

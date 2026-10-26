using Godmist.Core.Enums;
using Godmist.Core.Interfaces;
using Godmist.Data.Dungeons;
using Godmist.Views.Exploration;
using Godmist.Views.Game;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    [DefaultExecutionOrder(-90)]
    public class DungeonEntranceView : MonoBehaviour, IInteractable
    {
        [SerializeField] private ExplorationPartyController partyController;
        [SerializeField] private DungeonDefinition dungeonDefinition;
        [SerializeField] private int seed = 12345;
        [SerializeField] private DungeonSelectorWindowView selector;
        [SerializeField] private float interactionRadius = 1.25f;
        [SerializeField] private KeyCode interactionKey = KeyCode.E;
        [SerializeField] private Godmist.Views.Combat.CombatUnitVisualStateView hoverOutline;
        [SerializeField] private UIWorkspaceTooltipView tooltip;
        [Header("City gate exit (world units, relative to entrance)")]
        [SerializeField] private bool useGateExit;
        [SerializeField] private Vector2 gateExitOffset = new Vector2(0f, 1.3f);
        [SerializeField, Min(.1f)] private float gateHalfWidth = 1.1f;
        [SerializeField, Min(.1f)] private float gateReturnMargin = .5f;
        [SerializeField] private SpriteRenderer gateArrow;
        private Collider2D clickArea;
        private bool openedDuringVisit;
        private Sprite arrowSprite;
        private Texture2D arrowTexture;
        public float InteractionRadius => interactionRadius;
        public bool UsesGateExit => useGateExit;
        public Vector2 GateExitPosition => (Vector2)transform.position + (Vector2)(transform.rotation * gateExitOffset);
        public Vector2 GateApproachPosition => GateExitPosition + (Vector2)transform.up * .3f;
        private void Awake()
        {
            clickArea = GetComponent<Collider2D>();
            if (useGateExit && gateArrow != null) CreateGateArrow();
        }
        private void OnEnable() => partyController?.RegisterInteraction(this);
        private void ClearHover()
        {
            if (hoverOutline != null) hoverOutline.ClearHighlight();
            if (tooltip != null) tooltip.InvalidateSource(this);
        }
        private void OnDisable() => ClearHover();
        public bool CanInteract()
        {
            if (!isActiveAndEnabled || partyController == null || !partyController.CanAcceptExplorationInput
                || partyController.ActiveLeaderTransform == null) return false;
            var position = (Vector2)partyController.ActiveLeaderTransform.position;
            if (!useGateExit) return (position - (Vector2)transform.position).sqrMagnitude <= interactionRadius * interactionRadius;
            var offset = position - GateExitPosition;
            return !openedDuringVisit && Vector2.Dot(offset, transform.up) >= 0f
                && Mathf.Abs(Vector2.Dot(offset, transform.right)) <= gateHalfWidth;
        }
        public bool TryInteract()
        {
            if (!CanInteract()) return false;
            var game = GameBootstrap.Instance;
            if (game?.Session == null || game.Session.CurrentMode != GameModeState.Exploration) return false;
            ClearHover();
            partyController.CancelMovement();
            if (selector != null) { openedDuringVisit = true; selector.Open(game, dungeonDefinition); return true; }
            return game.TryEnterDungeon(dungeonDefinition, seed);
        }
        public void Interact() => TryInteract();
        // A small exit margin prevents repeated openings when movement jitters at the boundary.
        private bool CanAutomaticallyOpen()
        {
            if (partyController == null || partyController.ActiveLeaderTransform == null) return false;
            var position = (Vector2)partyController.ActiveLeaderTransform.position;
            if (useGateExit)
            {
                // Only returning to the city rearms the exit, never walking farther outside.
                if (Vector2.Dot(position - GateExitPosition, transform.up) < -gateReturnMargin)
                    openedDuringVisit = false;
            }
            else if ((position - (Vector2)transform.position).sqrMagnitude > (interactionRadius + .25f) * (interactionRadius + .25f))
                openedDuringVisit = false;
            return !openedDuringVisit && CanInteract() && partyController.Session != null && !partyController.Session.HasCombat;
        }
        private void Update()
        {
            var automaticEntry = CanAutomaticallyOpen();
            // Never automatically use the debug fallback that bypasses choosing an offer.
            if (automaticEntry && selector != null) { TryInteract(); return; }
            if (DevConsoleView.IsOpen || UIWorkspaceInputRouter.HasFocusedTextInput()
                || UIWorkspaceInputRouter.IsPointerOverUi() || partyController == null
                || !partyController.CanAcceptExplorationInput || selector != null && selector.IsOpen) { ClearHover(); return; }
            if (Input.GetKeyDown(interactionKey)) { TryInteract(); return; }
            var hovered = !partyController.IsWorldPrimaryClickClaimed && clickArea != null && clickArea.enabled
                && partyController.TryGetPointerWorldPosition(out var point) && clickArea.OverlapPoint(point);
            if (!hovered) { ClearHover(); return; }
            if (!useGateExit && hoverOutline != null) hoverOutline.Refresh(null, false, true, false, true);
            if (tooltip != null) tooltip.ShowCompactText("Wyprawy", string.Empty, this);
            if (!Input.GetMouseButtonDown(0)) return;
            partyController.ClaimWorldPrimaryClick();
            if (!partyController.RequestEntranceInteraction(this) && tooltip != null)
            {
                tooltip.InvalidateSource(this);
                tooltip.ShowCompactText("Wejście do lochu", "Nie można podejść do wejścia.", this);
            }
        }

        private void CreateGateArrow()
        {
            // Native pixel size and hard alpha keep the marker consistent with the world art.
            const int width = 24, height = 32;
            bool Inside(int x, int y) => (y >= 3 && y <= 18 && x >= 9 && x <= 14)
                || (y >= 15 && y <= 28 && Mathf.Abs(x - 11.5f) <= (28 - y) * .75f + .5f);
            arrowTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                { name = "Expedition gate arrow", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[width * height];
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                if (!Inside(x, y)) continue;
                var edge = !Inside(x - 1, y) || !Inside(x + 1, y) || !Inside(x, y - 1) || !Inside(x, y + 1);
                pixels[y * width + x] = edge ? new Color32(40, 33, 22, 255)
                    : x < 12 ? new Color32(229, 207, 131, 255) : new Color32(177, 140, 63, 255);
            }
            arrowTexture.SetPixels32(pixels);
            arrowTexture.Apply();
            arrowSprite = Sprite.Create(arrowTexture, new Rect(0, 0, width, height), Vector2.one * .5f, 32f);
            arrowSprite.name = "Expedition gate arrow";
            gateArrow.sprite = arrowSprite;
        }

        private void OnDestroy()
        {
            partyController?.UnregisterInteraction(this);
            if (Application.isPlaying)
            {
                if (arrowSprite != null) Destroy(arrowSprite);
                if (arrowTexture != null) Destroy(arrowTexture);
            }
            else
            {
                if (arrowSprite != null) DestroyImmediate(arrowSprite);
                if (arrowTexture != null) DestroyImmediate(arrowTexture);
            }
        }
    }
}

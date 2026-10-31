using Godmist.Core.Utilities;
using Godmist.Views.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Godmist.Views.UI.Workspace;

namespace Godmist.Views.UI
{
    public class CombatActionButtonsView : MonoBehaviour
    {
        [SerializeField] private CombatSandboxBootstrap sandboxBootstrap;
        [SerializeField] private Button endTurnButton;
        [SerializeField] private Button escapeButton;
        [SerializeField] private Image escapeIcon;
        [SerializeField] private Material escapeDisabledMaterial;
        [SerializeField] private Button consumablesButton;
        [SerializeField] private CombatConsumableSelectionView consumableSelectionPrefab;
        [SerializeField] private UIWorkspaceTooltipView tooltipView;
        private CombatConsumableSelectionView consumableSelection;
        private Godmist.Runtime.Combat.CombatSession boundSession;
        private Godmist.Runtime.Combat.BattleUnit boundUnit;
        private TextMeshProUGUI endTurnShortcutLabel;
        private TextMeshProUGUI consumablesShortcutLabel;
        private TextMeshProUGUI escapeShortcutLabel;
        private CombatSimpleTooltipTrigger escapeTooltip;
        private Material escapeNormalMaterial;
        private Color escapeNormalIconColor;
        private Color escapeNormalFrameColor;
        private Color escapeNormalShortcutColor;

        private void Start()
        {
            if (endTurnButton != null)
            {
                endTurnButton.onClick.AddListener(OnEndTurnClicked);
            }
            if (escapeButton != null)
            {
                escapeButton.onClick.AddListener(OnEscapeClicked);
            }
            if (consumablesButton != null) consumablesButton.onClick.AddListener(OnConsumablesClicked);
            endTurnShortcutLabel = CreateShortcutLabel(endTurnButton, "E");
            consumablesShortcutLabel = CreateShortcutLabel(consumablesButton, "Q");
            escapeShortcutLabel = CreateShortcutLabel(escapeButton, "X");
            escapeNormalMaterial = escapeIcon != null ? escapeIcon.material : null;
            escapeNormalIconColor = escapeIcon != null ? escapeIcon.color : Color.white;
            escapeNormalFrameColor = escapeButton?.targetGraphic != null ? escapeButton.targetGraphic.color : Color.white;
            escapeNormalShortcutColor = escapeShortcutLabel != null ? escapeShortcutLabel.color : Color.white;
            AddTooltip(endTurnButton, "Zakończ turę", "Kończy turę aktywnej jednostki. Niewydane Punkty Akcji przepadają.");
            AddTooltip(escapeButton, "Ucieknij", string.Empty);
            escapeTooltip = escapeButton != null ? escapeButton.GetComponent<CombatSimpleTooltipTrigger>() : null;
            AddTooltip(consumablesButton, "Przedmioty", string.Empty);
        }

        private void Update()
        {
            if (sandboxBootstrap == null) return;
            if (boundSession != sandboxBootstrap.Session || boundUnit != sandboxBootstrap.Session?.ActiveUnit)
            {
                CloseConsumableSelection();
                boundSession = sandboxBootstrap.Session;
                boundUnit = boundSession?.ActiveUnit;
            }

            var playerTurn = boundSession?.ActiveUnit?.IsPlayerSide == true && !sandboxBootstrap.IsOpeningPreview;
            SetButtonVisible(endTurnButton, playerTurn);
            SetButtonVisible(escapeButton, playerTurn);
            SetButtonVisible(consumablesButton, playerTurn);
            if (!playerTurn) CloseConsumableSelection();

            if (endTurnButton != null)
            {
                endTurnButton.interactable = sandboxBootstrap.CanEndTurn();
            }

            if (escapeButton != null)
            {
                var reason = sandboxBootstrap.GetEscapeBlockReason();
                SetEscapeButtonState(string.IsNullOrEmpty(reason));
                escapeTooltip?.UpdateDescription(!string.IsNullOrEmpty(reason) ? reason
                    : boundSession?.IsSeamless == true ? $"Wycofaj się z walki.\nKoszt: 0 {UiTerms.ActionPointsShort}."
                    : "Próbuje uciec z walki. Nieudana próba zwiększa szansę powodzenia następnej.");
            }
            if (consumablesButton != null)
                consumablesButton.interactable = sandboxBootstrap.CanEndTurn();

            if (!DevConsoleView.IsOpen
                && !UIWorkspaceInputRouter.HasFocusedTextInput()
                && !UIWorkspaceInputRouter.HasFocusedContextMenu()
                && UIWorkspaceWindowManager.Instance?.HasOpenWindows != true
                && Input.GetKeyDown(KeyCode.Q)
                && consumablesButton != null
                && consumablesButton.gameObject.activeInHierarchy
                && consumablesButton.interactable)
            {
                OnConsumablesClicked();
            }
        }

        private void SetEscapeButtonState(bool available)
        {
            if (escapeButton == null) return;
            escapeButton.interactable = available;
            if (escapeIcon != null)
            {
                // Reuse the inventory's neutral pixel-art material. Tint alone
                // only darkens a colored sprite; the material makes it grey.
                escapeIcon.material = !available && escapeDisabledMaterial != null ? escapeDisabledMaterial : escapeNormalMaterial;
                escapeIcon.color = available ? escapeNormalIconColor : new Color(.62f, .62f, .62f, 1f);
            }
            if (escapeButton.targetGraphic != null)
                escapeButton.targetGraphic.color = available ? escapeNormalFrameColor : new Color(.45f, .45f, .45f, 1f);
            if (escapeShortcutLabel != null)
                escapeShortcutLabel.color = available ? escapeNormalShortcutColor : new Color(.5f, .5f, .5f, 1f);
        }

        private static void SetButtonVisible(Button button, bool visible)
        {
            if (button != null) button.gameObject.SetActive(visible);
        }

        private void CloseConsumableSelection()
        {
            if (consumableSelection != null) consumableSelection.Close();
        }

        private void OnDisable()
        {
            CloseConsumableSelection();
        }

        private void OnDestroy()
        {
            if (consumableSelection != null) Destroy(consumableSelection.gameObject);
            if (endTurnShortcutLabel != null) Destroy(endTurnShortcutLabel.gameObject);
            if (consumablesShortcutLabel != null) Destroy(consumablesShortcutLabel.gameObject);
            if (escapeShortcutLabel != null) Destroy(escapeShortcutLabel.gameObject);
            if (endTurnButton != null)
            {
                endTurnButton.onClick.RemoveListener(OnEndTurnClicked);
            }
            if (escapeButton != null)
            {
                escapeButton.onClick.RemoveListener(OnEscapeClicked);
            }
            if (consumablesButton != null) consumablesButton.onClick.RemoveListener(OnConsumablesClicked);
        }

        private void OnEndTurnClicked()
        {
            sandboxBootstrap?.RequestEndTurn();
        }

        private void OnEscapeClicked()
        {
            if (boundSession != sandboxBootstrap?.Session || boundUnit != boundSession?.ActiveUnit) return;
            sandboxBootstrap?.RequestEscape();
        }

        private void OnConsumablesClicked()
        {
            if (sandboxBootstrap?.Session?.ActiveUnit?.IsPlayerSide != true || !sandboxBootstrap.CanEndTurn()) return;
            if (consumableSelection != null && consumableSelection.IsOpen) { consumableSelection.Close(); return; }
            // Debug-created items are stored on the game-session leader. The
            // standalone sandbox uses a throwaway BattleUnit, so synchronize
            // that payload before the selection window performs its first
            // rebuild (and also when the command was entered mid-combat).
            sandboxBootstrap.SyncDebugConsumablesToActiveUnit();
            if (consumableSelectionPrefab == null)
            {
                Debug.LogWarning("CombatActionButtonsView nie ma przypisanego Consumable Selection Prefab.", this);
                return;
            }
            if (consumableSelection == null)
            {
                var parent = tooltipView != null ? tooltipView.transform.parent : GetComponentInParent<Canvas>()?.transform;
                consumableSelection = CombatConsumableSelectionView.Create(parent, consumableSelectionPrefab);
            }
            consumableSelection?.Configure(
                sandboxBootstrap,
                GetComponentInParent<CombatHudView>()?.Targeting,
                tooltipView);
            consumableSelection?.Open(consumablesButton.transform as RectTransform);
        }

        private void AddTooltip(Button button, string title, string description)
        {
            if (button == null) return;
            var trigger = button.GetComponent<CombatSimpleTooltipTrigger>();
            if (trigger == null) return;
            trigger.Initialize(tooltipView, title, description);
        }

        private static TextMeshProUGUI CreateShortcutLabel(Button button, string shortcut)
        {
            if (button == null) return null;

            var existingLabel = button.GetComponentInChildren<TMP_Text>(true);
            var labelObject = new GameObject(
                $"Shortcut ({shortcut})",
                typeof(RectTransform),
                typeof(TextMeshProUGUI),
                typeof(LayoutElement));
            labelObject.transform.SetParent(button.transform, false);
            var label = labelObject.GetComponent<TextMeshProUGUI>();
            labelObject.GetComponent<LayoutElement>().ignoreLayout = true;
            label.font = existingLabel?.font ?? TMP_Settings.defaultFontAsset;
            // Icon-only buttons have no source label. TMP requires the selected
            // font's material; assigning null here breaks its next canvas rebuild.
            label.fontSharedMaterial = existingLabel?.fontSharedMaterial ?? label.font?.material;
            label.fontSize = existingLabel != null ? Mathf.Max(14f, existingLabel.fontSize) : 18f;
            label.fontStyle = FontStyles.Bold;
            label.color = new Color(0.93f, 0.88f, 0.72f, 1f);
            label.alignment = TextAlignmentOptions.Center;
            label.text = shortcut;
            label.raycastTarget = false;

            var rect = (RectTransform)label.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 2f);
            rect.sizeDelta = new Vector2(0f, 20f);
            return label;
        }
    }
}

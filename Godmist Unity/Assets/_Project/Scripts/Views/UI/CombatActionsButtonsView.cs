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
        [SerializeField] private Button consumablesButton;
        [SerializeField] private CombatConsumableSelectionView consumableSelectionPrefab;
        [SerializeField] private UIWorkspaceTooltipView tooltipView;
        private CombatConsumableSelectionView consumableSelection;
        private Godmist.Runtime.Combat.CombatSession boundSession;

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
            AddTooltip(endTurnButton, "Zakończ turę", "Kończy turę aktywnej jednostki. Niewydane Punkty Akcji przepadają.");
            AddTooltip(escapeButton, "Ucieczka", "Próbuje uciec z walki. Nieudana próba zwiększa szansę powodzenia następnej.");
            AddTooltip(consumablesButton, "Consumables", "Otwiera potiony, bomby i przedmioty użytkowe dostępne dla aktywnej postaci.");
        }

        private void Update()
        {
            if (sandboxBootstrap == null) return;
            if (boundSession != sandboxBootstrap.Session)
            {
                if (consumableSelection != null) consumableSelection.gameObject.SetActive(false);
                boundSession = sandboxBootstrap.Session;
            }

            if (endTurnButton != null)
            {
                endTurnButton.interactable = sandboxBootstrap.CanEndTurn();
            }

            if (escapeButton != null)
            {
                escapeButton.interactable = sandboxBootstrap.CanEscape();
            }
            if (consumablesButton != null)
                consumablesButton.interactable = sandboxBootstrap.CanEndTurn();
        }

        private void OnDisable()
        {
            if (consumableSelection != null) consumableSelection.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
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
            sandboxBootstrap?.RequestEscape();
        }

        private void OnConsumablesClicked()
        {
            if (sandboxBootstrap?.Session?.ActiveUnit?.IsPlayerSide != true) return;
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
                var parent = UIWorkspaceTooltipView.ActiveInstance?.transform.parent
                    ?? UIWorkspaceWindowManager.Instance?.WorkspaceRoot
                    ?? GetComponentInParent<Canvas>()?.transform;
                consumableSelection = CombatConsumableSelectionView.Create(parent, consumableSelectionPrefab);
            }
            consumableSelection?.Configure(
                sandboxBootstrap,
                FindAnyObjectByType<CombatTargetSelectionController>(),
                tooltipView);
            consumableSelection?.Open();
        }

        private void AddTooltip(Button button, string title, string description)
        {
            if (button == null) return;
            var trigger = button.GetComponent<CombatSimpleTooltipTrigger>();
            if (trigger == null) trigger = button.gameObject.AddComponent<CombatSimpleTooltipTrigger>();
            trigger.Initialize(tooltipView, title, description);
        }
    }
}

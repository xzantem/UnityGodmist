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
        [SerializeField] private UIWorkspaceTooltipView tooltipView;

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
            AddTooltip(endTurnButton, "Zakończ turę", "Kończy turę aktywnej jednostki. Niewydane Punkty Akcji przepadają.");
            AddTooltip(escapeButton, "Ucieczka", "Próbuje uciec z walki. Nieudana próba zwiększa szansę powodzenia następnej.");
        }

        private void Update()
        {
            if (sandboxBootstrap == null) return;

            if (endTurnButton != null)
            {
                endTurnButton.interactable = sandboxBootstrap.CanEndTurn();
            }

            if (escapeButton != null)
            {
                escapeButton.interactable = sandboxBootstrap.CanEscape();
            }
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
        }

        private void OnEndTurnClicked()
        {
            sandboxBootstrap?.RequestEndTurn();
        }

        private void OnEscapeClicked()
        {
            sandboxBootstrap?.RequestEscape();
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

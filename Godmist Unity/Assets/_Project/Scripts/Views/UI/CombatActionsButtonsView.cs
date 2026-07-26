using Godmist.Views.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI
{
    public class CombatActionButtonsView : MonoBehaviour
    {
        [SerializeField] private CombatSandboxBootstrap sandboxBootstrap;
        [SerializeField] private Button endTurnButton;
        [SerializeField] private Button escapeButton;

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
    }
}
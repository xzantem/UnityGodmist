using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI
{
    public class CombatLogToggleView : MonoBehaviour
    {
        [SerializeField] private Button toggleButton;
        [SerializeField] private GameObject logPanel;
        [SerializeField] private bool startsExpanded;

        private CombatEdgeDrawer edgeDrawer;

        private void Awake()
        {
            edgeDrawer = logPanel != null
                ? logPanel.GetComponent<CombatEdgeDrawer>()
                : null;

            if (edgeDrawer != null)
            {
                edgeDrawer.SetExpanded(startsExpanded, immediate: true);
                return;
            }

            SetExpanded(startsExpanded);
        }

        private void OnEnable()
        {
            if (toggleButton != null)
            {
                toggleButton.onClick.AddListener(Toggle);
            }
        }

        private void OnDisable()
        {
            if (toggleButton != null)
            {
                toggleButton.onClick.RemoveListener(Toggle);
            }
        }

        public void Toggle()
        {
            if (edgeDrawer != null)
            {
                edgeDrawer.Toggle();
                return;
            }

            SetExpanded(logPanel != null && !logPanel.activeSelf);
        }

        private void SetExpanded(bool expanded)
        {
            if (logPanel != null)
            {
                logPanel.SetActive(expanded);
            }
        }
    }
}

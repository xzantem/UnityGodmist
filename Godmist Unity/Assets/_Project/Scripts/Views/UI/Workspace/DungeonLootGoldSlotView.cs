using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Godmist.Views.UI.Workspace
{
    public sealed class DungeonLootGoldSlotView : MonoBehaviour,
        IPointerClickHandler,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        [SerializeField] private TextMeshProUGUI label;

        private DungeonLootWindowView owner;
        private int gold;

        public int Gold => gold;

        public void ConfigureRuntime(TextMeshProUGUI nextLabel)
        {
            label = nextLabel;
        }

        public void Bind(int nextGold, DungeonLootWindowView nextOwner)
        {
            gold = Mathf.Max(0, nextGold);
            owner = nextOwner;
            label ??= GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null) label.text = $"Złoto: {gold}";
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (gold > 0 && eventData != null && eventData.clickCount >= 1)
            {
                owner?.TryClaimGold();
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            UIWorkspaceTooltipView.ActiveInstance?.ShowText(
                "Złoto",
                $"Odbierz {gold} szt. złota aktywną postacią.",
                this);
        }

        public void OnPointerExit(PointerEventData eventData) =>
            UIWorkspaceTooltipView.ActiveInstance?.NotifySourceExit();
    }
}

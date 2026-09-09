using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Godmist.Views.UI.Workspace
{
    public sealed class DungeonLootGoldSlotView : MonoBehaviour,
        IPointerClickHandler,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI countText;

        private DungeonLootWindowView owner;
        private int gold;

        public int Gold => gold;

        public void ConfigureRuntime(Image nextIcon, TextMeshProUGUI nextCountText)
        {
            icon = nextIcon;
            countText = nextCountText;
        }

        public void Bind(int nextGold, DungeonLootWindowView nextOwner)
        {
            gold = Mathf.Max(0, nextGold);
            owner = nextOwner;
            icon ??= transform.Find("Icon")?.GetComponent<Image>();
            countText ??= transform.Find("CountLabel")?.GetComponent<TextMeshProUGUI>();
            if (countText != null) countText.text = gold.ToString();
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
                "Nagroda",
                $"Odbierz nagrodę: {gold}.",
                this);
        }

        public void OnPointerExit(PointerEventData eventData) =>
            UIWorkspaceTooltipView.ActiveInstance?.NotifySourceExit(this);
    }
}

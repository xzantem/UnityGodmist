using Godmist.Runtime.NPC;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Godmist.Views.UI.Workspace
{
    public sealed class NpcLoyaltyHeaderView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private Slider progressSlider;
        [SerializeField] private RectTransform progressRow;
        private NpcInstance npc;
        private NpcLoyaltyTooltipData tooltipData;
        public void Bind(NpcInstance nextNpc, NpcLoyaltyTooltipData nextTooltipData)
        {
            npc = nextNpc;
            tooltipData = nextTooltipData;
        }
        public void SetProgress(int level, long spent, int required, bool isMax)
        {
            levelText.text = level.ToString();
            progressSlider.value = isMax || required <= 0 ? 1f : Mathf.Clamp01((float)(spent / (double)required));
            progressText.text = isMax ? "MAX" : $"{spent} / {required}";
        }
        public void RefreshTooltip()
        {
            if (tooltipData != null) UIWorkspaceTooltipView.ActiveInstance?.RefreshNpcLoyaltyTooltip(tooltipData, this);
        }
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (npc != null) UIWorkspaceTooltipView.ActiveInstance?.ShowNpcLoyalty(tooltipData, this);
        }
        public void OnPointerExit(PointerEventData eventData) =>
            UIWorkspaceTooltipView.ActiveInstance?.NotifySourceExit(this);
    }
}

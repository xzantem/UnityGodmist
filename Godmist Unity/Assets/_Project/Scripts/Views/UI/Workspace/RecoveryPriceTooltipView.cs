using UnityEngine;
using UnityEngine.EventSystems;
namespace Godmist.Views.UI.Workspace
{
    public sealed class RecoveryPriceTooltipView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private string breakdown;
        public void Bind(string value) => breakdown = value;
        public void OnPointerEnter(PointerEventData eventData) =>
            UIWorkspaceTooltipView.ActiveInstance?.ShowText("Cena odzyskania", breakdown, this);
        public void OnPointerExit(PointerEventData eventData) =>
            UIWorkspaceTooltipView.ActiveInstance?.NotifySourceExit(this);
        private void OnDisable() => UIWorkspaceTooltipView.ActiveInstance?.NotifySourceExit(this);
    }
}

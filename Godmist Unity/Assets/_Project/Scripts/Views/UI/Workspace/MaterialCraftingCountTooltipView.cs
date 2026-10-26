using UnityEngine;
using UnityEngine.EventSystems;
namespace Godmist.Views.UI.Workspace
{
    /// <summary>Explains the ingredient fraction without replacing the item tooltip.</summary>
    public sealed class MaterialCraftingCountTooltipView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public void OnPointerEnter(PointerEventData eventData) => UIWorkspaceTooltipView.ActiveInstance?.ShowText(
            "Posiadane / wymagane", "Pierwsza liczba: materiały w ekwipunku.\nDruga liczba: materiały potrzebne na jedno wytworzenie.", this);
        public void OnPointerExit(PointerEventData eventData) => UIWorkspaceTooltipView.ActiveInstance?.NotifySourceExit(this);
        private void OnDisable() => UIWorkspaceTooltipView.ActiveInstance?.NotifySourceExit(this);
    }
}

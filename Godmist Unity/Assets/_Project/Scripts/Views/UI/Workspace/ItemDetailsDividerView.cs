using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI.Workspace
{
    /// <summary>Draws an authored divider at the equipment details' reserved text line.</summary>
    [ExecuteAlways]
    public sealed class ItemDetailsDividerView : MonoBehaviour
    {
        public const string MarkerId = "item-details-divider";
        public const string MarkerLine = "<line-height=12><link=\"" + MarkerId
            + "\">\u200B</link></line-height>";

        [SerializeField] private TMP_Text detailsText;
        [SerializeField] private Image divider;

        private void OnEnable()
        {
            if (detailsText != null) detailsText.OnPreRenderText += Refresh;
        }

        private void OnDisable()
        {
            if (detailsText != null) detailsText.OnPreRenderText -= Refresh;
            if (divider != null) divider.enabled = false;
        }

        private void LateUpdate()
        {
            if (detailsText == null) return;
            if (detailsText.havePropertiesChanged) detailsText.ForceMeshUpdate();
            Refresh(detailsText.textInfo);
        }

        private void Refresh(TMP_TextInfo info)
        {
            if (divider == null || detailsText == null) return;
            // TMP may notify during Canvas rebuild. Defer Image/RectTransform edits
            // to LateUpdate rather than registering another graphic mid-rebuild.
            if (CanvasUpdateRegistry.IsRebuildingGraphics() || CanvasUpdateRegistry.IsRebuildingLayout()) return;
            if (info == null || !detailsText.gameObject.activeInHierarchy)
            {
                divider.enabled = false;
                return;
            }
            for (var i = 0; i < info.linkCount; i++)
            {
                var link = info.linkInfo[i];
                if (link.GetLinkID() != MarkerId || link.linkTextLength == 0) continue;
                var characterIndex = link.linkTextfirstCharacterIndex;
                if (characterIndex < 0 || characterIndex >= info.characterCount) continue;
                var line = info.lineInfo[info.characterInfo[characterIndex].lineNumber];
                var textRect = detailsText.rectTransform;
                var rect = divider.rectTransform;
                rect.anchorMin = rect.anchorMax = textRect.pivot;
                rect.pivot = new Vector2(.5f, .5f);
                rect.sizeDelta = new Vector2(textRect.rect.width, 2);
                rect.anchoredPosition = new Vector2(textRect.rect.center.x, line.baseline + 2);
                divider.enabled = true;
                return;
            }
            divider.enabled = false;
        }
    }
}

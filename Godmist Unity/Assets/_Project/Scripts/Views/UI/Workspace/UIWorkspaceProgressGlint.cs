using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI.Workspace
{
    /// <summary>Optional EXP/loyalty highlight anchored to the actual slider fill.</summary>
    [ExecuteAlways]
    public sealed class UIWorkspaceProgressGlint : MonoBehaviour
    {
        [SerializeField] private Slider source;
        [SerializeField] private Image glint;

        public void Configure(Slider slider, Image image)
        {
            source = slider;
            glint = image;
            Refresh();
        }

        private void LateUpdate() => Refresh();

        public void Refresh()
        {
            if (source == null || glint == null || source.fillRect == null) return;
            var rect = glint.rectTransform;
            var horizontal = source.direction is Slider.Direction.LeftToRight or Slider.Direction.RightToLeft;
            var reversed = source.direction is Slider.Direction.RightToLeft or Slider.Direction.TopToBottom;
            var anchor = horizontal ? new Vector2(reversed ? 0 : 1, .5f) : new Vector2(.5f, reversed ? 0 : 1);
            rect.anchorMin = rect.anchorMax = anchor;
            var extent = horizontal ? source.fillRect.rect.width : source.fillRect.rect.height;
            var halfSize = (horizontal ? rect.rect.width : rect.rect.height) * .5f;
            var inset = Mathf.Min(halfSize, extent * .5f) * (reversed ? 1 : -1);
            rect.anchoredPosition = horizontal ? new Vector2(inset, 0) : new Vector2(0, inset);
            glint.enabled = source.normalizedValue > 0 && extent >= halfSize;
        }
    }
}

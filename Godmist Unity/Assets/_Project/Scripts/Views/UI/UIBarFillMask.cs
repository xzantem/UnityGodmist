using UnityEngine;
using UnityEngine.UI;
namespace Godmist.Views.UI
{
    /// <summary>Background, clipped fill, border. Shared by read-only sliders and image-based bars.</summary>
    [DisallowMultipleComponent]
    public sealed class UIBarFillMask : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private RectTransform fillArea;
        [SerializeField] private Image border;
        [SerializeField] private float authoredPixelsPerUnitMultiplier = 1f;
        [SerializeField] private RectTransform shieldOverlay;
        private bool applying;
        public RectTransform FillArea => fillArea;
        public Image Border => border;
        public Image Background => background;
        public void SetShieldOverlay(RectTransform rect) { shieldOverlay = rect; Apply(); }
        private void OnEnable() => Apply();
        private void OnRectTransformDimensionsChange() { if (isActiveAndEnabled) Apply(); }
        public void Apply()
        {
            if (TryGetComponent<Slider>(out var input) && input.handleRect != null) return;
            if (applying || background == null || border == null || fillArea == null || background.sprite == null) return;
            applying = true;
            try
            {
                var imageRect = background.GetPixelAdjustedRect();
                if (imageRect.height <= 0 || imageRect.width <= 0) return;
                var units = Mathf.Max(.0001f, background.pixelsPerUnit);
                var source = background.sprite.border;
                // A thin bar must retain an interior instead of collapsing both sliced borders together.
                var minimumMultiplier = Mathf.Ceil((source.y + source.w) / units / Mathf.Max(1f, imageRect.height - 2f));
                var multiplier = Mathf.Max(authoredPixelsPerUnitMultiplier, minimumMultiplier);
                background.pixelsPerUnitMultiplier = multiplier;
                border.sprite = background.sprite;
                border.type = Image.Type.Sliced;
                border.fillCenter = false;
                border.pixelsPerUnitMultiplier = multiplier;
                border.color = background.color;
                border.raycastTarget = false;
                border.enabled = true;
                background.type = Image.Type.Sliced;
                background.fillCenter = true;
                var inset = source / (units * multiplier);
                if (inset.x + inset.z > imageRect.width)
                {
                    var scale = imageRect.width / (inset.x + inset.z);
                    inset.x *= scale; inset.z *= scale;
                }
                Align(border.rectTransform, Vector4.zero);
                Align(fillArea, inset);
                if (shieldOverlay != null && shieldOverlay.parent == fillArea)
                {
                    shieldOverlay.anchorMin = new Vector2(0, 1); shieldOverlay.anchorMax = Vector2.one;
                    shieldOverlay.pivot = new Vector2(.5f, 1);
                    shieldOverlay.offsetMin = new Vector2(0, -Mathf.Min(2, fillArea.rect.height));
                    shieldOverlay.offsetMax = Vector2.zero;
                }
                else if (shieldOverlay != null && shieldOverlay.parent == fillArea.parent)
                {
                    shieldOverlay.anchorMin = new Vector2(fillArea.anchorMin.x, fillArea.anchorMax.y);
                    shieldOverlay.anchorMax = fillArea.anchorMax;
                    shieldOverlay.pivot = new Vector2(.5f, 1);
                    shieldOverlay.offsetMin = new Vector2(fillArea.offsetMin.x, fillArea.offsetMax.y - Mathf.Min(2, fillArea.rect.height));
                    shieldOverlay.offsetMax = fillArea.offsetMax;
                }
                border.transform.SetAsLastSibling();
            }
            finally { applying = false; }
        }
        private void Align(RectTransform rect, Vector4 inset)
        {
            if (rect.parent == background.transform)
            {
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.pivot = background.rectTransform.pivot;
                rect.offsetMin = new Vector2(inset.x, inset.y);
                rect.offsetMax = new Vector2(-inset.z, -inset.w);
            }
            else
            {
                var source = background.rectTransform;
                rect.anchorMin = source.anchorMin; rect.anchorMax = source.anchorMax; rect.pivot = source.pivot;
                rect.offsetMin = source.offsetMin + new Vector2(inset.x, inset.y);
                rect.offsetMax = source.offsetMax - new Vector2(inset.z, inset.w);
            }
            rect.localScale = Vector3.one;
        }
        // Called only by explicit authoring, or by a panel which already owns these images.
        public static UIBarFillMask Install(Image track, Image fill, RectTransform area = null, Image frame = null)
        {
            if (track == null || fill == null || track.sprite == null) return null;
            var owner = area != null ? area.parent.gameObject : track.gameObject;
            var presentation = owner.GetComponent<UIBarFillMask>() ?? owner.AddComponent<UIBarFillMask>();
            if (presentation.background != null)
            {
                presentation.authoredPixelsPerUnitMultiplier = Mathf.Max(presentation.authoredPixelsPerUnitMultiplier,
                    track.pixelsPerUnitMultiplier, frame != null ? frame.pixelsPerUnitMultiplier : 0);
                presentation.Apply(); return presentation;
            }
            var multiplier = frame != null ? Mathf.Max(track.pixelsPerUnitMultiplier, frame.pixelsPerUnitMultiplier) : track.pixelsPerUnitMultiplier;
            if (area == null)
            {
                var mask = new GameObject("FillMask", typeof(RectTransform), typeof(RectMask2D));
                mask.layer = track.gameObject.layer; mask.transform.SetParent(track.transform, false);
                area = (RectTransform)mask.transform;
                fill.transform.SetParent(area, false);
                fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = Vector2.one;
                fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            }
            else if (area.GetComponent<RectMask2D>() == null) area.gameObject.AddComponent<RectMask2D>();
            fill.maskable = true;
            if (frame == null)
            {
                var node = new GameObject("BarBorder", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                node.layer = track.gameObject.layer; node.transform.SetParent(area.parent, false);
                frame = node.GetComponent<Image>();
            }
            presentation.background = track; presentation.fillArea = area; presentation.border = frame;
            presentation.authoredPixelsPerUnitMultiplier = multiplier;
            presentation.Apply();
            return presentation;
        }
    }
}

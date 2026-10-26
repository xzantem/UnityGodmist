using Godmist.Core.Enums;
using Godmist.Runtime.Items;
using UnityEngine;

namespace Godmist.Views.UI.Workspace
{
    /// <summary>Shared rarity colors for item accents and text throughout the UI.</summary>
    public static class ItemRarityPresentation
    {
        public static Color Color(RarityTier rarity) => rarity switch
        {
            RarityTier.Trash => new Color32(181, 148, 50, 255),
            RarityTier.Broken => new Color32(182, 69, 47, 255),
            RarityTier.Damaged => new Color32(233, 129, 86, 255),
            RarityTier.Common => new Color32(183, 188, 196, 255),
            RarityTier.Uncommon => new Color32(99, 202, 71, 255),
            RarityTier.Rare => new Color32(66, 138, 245, 255),
            RarityTier.Ancient => new Color32(246, 208, 120, 255),
            RarityTier.Legendary => new Color32(161, 103, 245, 255),
            RarityTier.Mythic => new Color32(240, 53, 191, 255),
            RarityTier.Godly => new Color32(39, 239, 255, 255),
            _ => new Color32(183, 188, 196, 255)
        };


        /// <summary>Owns a non-interactive, maskable accent on an explicitly bound item frame.</summary>
        public static void ApplyBorder(ref ItemRarityBorderGraphic border, RectTransform frame,
            ItemInstance item, float inset = 4f) => ApplyBorder(ref border, frame, item?.CurrentRarity, inset);

        public static void ApplyBorder(ref ItemRarityBorderGraphic border, RectTransform frame,
            RarityTier? rarity, float inset = 4f)
        {
            // Tooltip nesting clones the live hierarchy, including this runtime child,
            // while private owner references are reset. Reuse only our direct child.
            if (border == null && frame != null)
                foreach (Transform child in frame)
                    if (child.TryGetComponent<ItemRarityBorderGraphic>(out var existing))
                    { border = existing; break; }
            if (border == null && (frame == null || !rarity.HasValue)) return;
            if (border == null)
            {
                var node = new GameObject("ItemRarityBorder", typeof(RectTransform),
                    typeof(UnityEngine.UI.LayoutElement), typeof(ItemRarityBorderGraphic));
                node.layer = frame.gameObject.layer;
                node.transform.SetParent(frame, false);
                node.GetComponent<UnityEngine.UI.LayoutElement>().ignoreLayout = true;
                border = node.GetComponent<ItemRarityBorderGraphic>();
                border.raycastTarget = false;
                var rect = border.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.one * inset;
                rect.offsetMax = -Vector2.one * inset;
            }
            border.enabled = rarity.HasValue;
            if (!rarity.HasValue) return;
            border.SetRarity(rarity.Value);
            border.transform.SetAsLastSibling();
        }

        public static string Colorize(RarityTier rarity, string text) =>
            $"<color=#{ColorUtility.ToHtmlStringRGB(Color(rarity))}>{text}</color>";

        public static string Name(ItemInstance item) => item == null
            ? string.Empty : Colorize(item.CurrentRarity, item.DisplayName);
    }
    /// <summary>The inventory's thin outline and bottom marker, without a texture/material dependency.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ItemRarityBorderGraphic : UnityEngine.UI.MaskableGraphic
    {
        private bool diamond;
        private int shineStep = -1;

        public void SetRarity(RarityTier rarity)
        {
            color = ItemRarityPresentation.Color(rarity);
            diamond = rarity == RarityTier.Godly;
            SetVerticesDirty();
        }

        private void Update()
        {
            if (!diamond) return;
            // Match the authored inventory accent's travelling cyan/white highlight.
            var step = Mathf.FloorToInt(Mathf.Repeat(Time.unscaledTime * .22f, 1f) * 100f);
            if (step == shineStep) return;
            shineStep = step;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            var rect = GetPixelAdjustedRect();
            if (rect.width <= 0 || rect.height <= 0) return;
            if (!diamond)
            {
                var pixel = Mathf.Max(1f, Mathf.Round(Mathf.Min(rect.width, rect.height) / 64f));
                AddQuad(mesh, rect.xMin, rect.yMin, rect.xMax, rect.yMin + pixel, color);
                AddQuad(mesh, rect.xMin, rect.yMax - pixel, rect.xMax, rect.yMax, color);
                AddQuad(mesh, rect.xMin, rect.yMin + pixel, rect.xMin + pixel, rect.yMax - pixel, color);
                AddQuad(mesh, rect.xMax - pixel, rect.yMin + pixel, rect.xMax, rect.yMax - pixel, color);
                AddQuad(mesh, rect.xMin + rect.width * 20f / 64f, rect.yMin + pixel,
                    rect.xMin + rect.width * 44f / 64f, rect.yMin + pixel * 3f, color);
                return;
            }
            var phase = Mathf.Repeat(Time.unscaledTime * .22f, 1f);
            for (var y = 0; y < 64; y++)
            for (var x = 0; x < 64; x++)
            {
                if (x != 0 && x != 63 && y != 0 && y != 63
                    && !(x >= 20 && x < 44 && y < 3)) continue;
                var distance = Mathf.Abs(x / 64f + .12f * y / 64f - (phase * 1.6f - .3f));
                var tint = UnityEngine.Color.Lerp(color, new UnityEngine.Color(.96f, 1f, 1f),
                    distance < .04f ? .85f : distance < .09f ? .28f : 0f);
                AddQuad(mesh, rect.xMin + rect.width * x / 64f, rect.yMin + rect.height * y / 64f,
                    rect.xMin + rect.width * (x + 1) / 64f, rect.yMin + rect.height * (y + 1) / 64f, tint);
            }
        }

        private static void AddQuad(UnityEngine.UI.VertexHelper mesh, float left, float bottom,
            float right, float top, UnityEngine.Color tint)
        {
            var start = mesh.currentVertCount;
            mesh.AddVert(new Vector3(left, bottom), tint, Vector2.zero);
            mesh.AddVert(new Vector3(left, top), tint, Vector2.zero);
            mesh.AddVert(new Vector3(right, top), tint, Vector2.zero);
            mesh.AddVert(new Vector3(right, bottom), tint, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start + 2, start + 3, start);
        }
    }

}

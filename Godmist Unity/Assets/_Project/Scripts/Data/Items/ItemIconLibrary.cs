using System;
using System.Collections.Generic;
using UnityEngine;

namespace Godmist.Data.Items
{
    /// <summary>
    /// Editor-generated lookup table for item-instance art. Static item art stays
    /// on ItemDefinition; this asset only contains variants that depend on the
    /// crafted instance (for example jewelry metal + gem combinations).
    /// </summary>
    [CreateAssetMenu(fileName = "ItemIconLibrary", menuName = "Godmist/UI/Item Icon Library")]
    public sealed class ItemIconLibrary : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string Key;
            public Sprite Icon;
        }

        [SerializeField] private List<Entry> entries = new();
        [SerializeField] private Texture2D galduryteBase;
        // Retained for existing authored library/importer compatibility.
        // Current native glyphs use effect identity and their powder pigment.
        [SerializeField] private Texture2D[] galdurytePanels;
        [SerializeField] private Texture2D[] potionWeights;
        [SerializeField] private Texture2D potionShading;
        [SerializeField] private Texture2D potionOverlay;
        [SerializeField] private Texture2D[] bombWeights;
        [SerializeField] private Texture2D bombShading;
        [SerializeField] private Texture2D bombOverlay;
        private readonly Dictionary<string, Sprite> composed = new();

        public IReadOnlyList<Entry> Entries => entries;

        public Sprite ComposeConsumable(bool bomb, Color32[] colors)
        {
            if (colors == null || colors.Length < 1 || colors.Length > 3) return null;
            var key = bomb ? "bomb" : "potion";
            foreach (var color in colors) key += "." + ColorUtility.ToHtmlStringRGB(color);
            if (composed.TryGetValue(key, out var cached)) return cached;
            var masks = bomb ? bombWeights : potionWeights;
            var shading = bomb ? bombShading : potionShading;
            var overlay = bomb ? bombOverlay : potionOverlay;
            if (masks == null || masks.Length != 3 || masks[colors.Length - 1] == null || shading == null || overlay == null) return null;
            var mask = masks[colors.Length - 1];
            var weights = mask.GetPixels32();
            var shade = shading.GetPixels32();
            var glass = overlay.GetPixels32();
            var result = new Color32[weights.Length];
            for (var index = 0; index < result.Length; index++)
            {
                var weight = weights[index];
                var color = (Color)colors[0] * (weight.r / 255f);
                if (colors.Length > 1) color += (Color)colors[1] * (weight.g / 255f);
                if (colors.Length > 2) color += (Color)colors[2] * (weight.b / 255f);
                color.a = weight.a / 255f;
                result[index] = Over(glass[index], Over(shade[index], color));
            }
            return CacheSprite(key, mask.width, mask.height, result);
        }

        private static Color Over(Color foreground, Color background)
        {
            var alpha = foreground.a + background.a * (1 - foreground.a);
            if (alpha <= 0) return Color.clear;
            var result = (foreground * foreground.a + background * background.a * (1 - foreground.a)) / alpha;
            result.a = alpha;
            return result;
        }

        public Sprite Find(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            if (key.StartsWith("galduryte.", StringComparison.Ordinal)) return ComposeGalduryte(key);

            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry != null && string.Equals(entry.Key, key, StringComparison.Ordinal))
                    return entry.Icon;
            }

            return null;
        }

        private Sprite ComposeGalduryte(string key)
        {
            if (composed.TryGetValue(key, out var cached)) return cached;
            if (galduryteBase == null || galduryteBase.width != 32 || galduryteBase.height != 32) return null;
            var values=key.Substring("galduryte.".Length).Split('-');
            if(values.Length<1 || values.Length>6) return null;
            var colors=new int[values.Length];var patterns=new string[values.Length];
            for(var slot=0;slot<values.Length;slot++)
                if(!GalduryteRuneGlyphs.TryParse(values[slot],out colors[slot],out patterns[slot])) return null;
            var pixels=galduryteBase.GetPixels32();
            for(var slot=0;slot<values.Length;slot++)
                GalduryteRuneGlyphs.Draw(pixels,slot,values.Length,colors[slot],patterns[slot]);
            return CacheSprite(key,32,32,pixels);
        }

        private Sprite CacheSprite(string key, int width, int height, Color32[] pixels)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                { name = key, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            texture.SetPixels32(pixels);
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, width, texture.height), new Vector2(.5f, .5f), 32);
            sprite.name = key;
            sprite.hideFlags = HideFlags.DontSave;
            composed.Add(key, sprite);
            return sprite;
        }

        private void OnDisable()
        {
            foreach (var sprite in composed.Values)
            {
                if (sprite == null) continue;
                if (Application.isPlaying) { Destroy(sprite.texture); Destroy(sprite); }
                else { DestroyImmediate(sprite.texture); DestroyImmediate(sprite); }
            }
            composed.Clear();
        }
    }
}

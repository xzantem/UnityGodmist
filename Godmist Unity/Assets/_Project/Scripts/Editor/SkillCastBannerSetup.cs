using System;
using System.IO;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Editor
{
    public static class SkillCastBannerSetup
    {
        public const string HudPath = "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CombatHud.prefab";
        public const string PalettePath = "Assets/_Project/ScriptableObjects/UI/Workspace/UIWorkspaceStylePalette.asset";
        [MenuItem("Godmist/UI/Style skill cast banner")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            var backup = Path.GetFullPath("../LocalHandoffs/skill-cast-banner-20261024/before/" + HudPath);
            Directory.CreateDirectory(Path.GetDirectoryName(backup));
            if (!File.Exists(backup)) File.Copy(HudPath, backup);
            var palette = AssetDatabase.LoadAssetAtPath<UIWorkspaceStylePalette>(PalettePath);
            if (palette == null || palette.Font == null) throw new InvalidOperationException("Missing authored UI palette/font.");
            var root = PrefabUtility.LoadPrefabContents(HudPath);
            try
            {
                var hud = root.GetComponent<CombatHudView>();
                var state = new SerializedObject(hud);
                var banner = (GameObject)state.FindProperty("skillCastBanner").objectReferenceValue;
                var text = (TextMeshProUGUI)state.FindProperty("skillCastText").objectReferenceValue;
                if (banner == null || text == null) throw new InvalidOperationException("Missing authored banner/text.");
                var rect = (RectTransform)banner.transform;
                rect.sizeDelta = new Vector2(240, 60);
                Paint(banner.GetComponent<Image>(), palette.GetArtwork("surface_solid"));
                var opacity = Ensure<CanvasGroup>(banner);
                opacity.alpha = 0; opacity.interactable = false; opacity.blocksRaycasts = false;
                var layout = Ensure<HorizontalLayoutGroup>(banner);
                layout.padding = new RectOffset(16, 16, 8, 8); layout.spacing = 12;
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = layout.childForceExpandHeight = false;
                layout.childScaleWidth = layout.childScaleHeight = false;
                var icon = Node(rect, "SkillCastIcon"); icon.SetAsFirstSibling();
                var image = Ensure<Image>(icon.gameObject); image.sprite = null;
                image.type = Image.Type.Simple; image.preserveAspect = true; image.raycastTarget = false;
                Fixed(Ensure<LayoutElement>(icon.gameObject), 44, 44);
                var iconFrame = Node(icon, "Frame"); Stretch(iconFrame);
                Paint(Ensure<Image>(iconFrame.gameObject), palette.GetArtwork("item_frame"), true);
                icon.gameObject.SetActive(false);
                text.font = palette.Font; text.fontSharedMaterial = palette.Font.material;
                text.fontSize = 28; text.enableAutoSizing = true; text.fontSizeMin = 20; text.fontSizeMax = 28;
                text.alignment = TextAlignmentOptions.MidlineLeft; text.fontStyle = FontStyles.Bold;
                text.color = new Color(.98f, .85f, .56f); text.margin = Vector4.zero;
                text.richText = false; text.raycastTarget = false;
                text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Overflow;
                text.text = string.Empty;
                var nameLayout = Ensure<LayoutElement>(text.gameObject);
                nameLayout.minWidth = 0; nameLayout.preferredWidth = -1; nameLayout.flexibleWidth = 1;
                nameLayout.minHeight = nameLayout.preferredHeight = 44; nameLayout.flexibleHeight = 0;
                var border = Node(rect, "BannerFrame"); Stretch(border); border.SetAsLastSibling();
                Paint(Ensure<Image>(border.gameObject), palette.GetArtwork("tooltip_frame"), true);
                Ensure<LayoutElement>(border.gameObject).ignoreLayout = true;
                state.FindProperty("skillCastIcon").objectReferenceValue = image;
                state.FindProperty("skillCastOpacity").objectReferenceValue = opacity;
                state.FindProperty("skillCastDuration").floatValue = 1.4f;
                state.ApplyModifiedPropertiesWithoutUndo();
                banner.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, HudPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        static T Ensure<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }
        static RectTransform Node(Transform parent, string name)
        {
            var child = parent.Find(name) as RectTransform;
            if (child == null) { child = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); child.SetParent(parent, false); }
            child.gameObject.layer = parent.gameObject.layer;
            return child;
        }
        static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        static void Fixed(LayoutElement layout, float width, float height)
        { layout.minWidth = layout.preferredWidth = width; layout.minHeight = layout.preferredHeight = height; layout.flexibleWidth = layout.flexibleHeight = 0; }
        static void Paint(Image image, Sprite sprite, bool hollow = false)
        {
            if (image == null || sprite == null) throw new InvalidOperationException("Missing UI artwork/image.");
            image.sprite = sprite; image.overrideSprite = null; image.color = Color.white;
            image.type = sprite.border.sqrMagnitude > 0 ? Image.Type.Sliced : Image.Type.Simple;
            image.fillCenter = !hollow; image.raycastTarget = false;
        }
    }
}

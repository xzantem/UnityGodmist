using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Resource metrics in the existing character and development cards.</summary>
public static class ResourcePanelIconSetup
{
    const string Root = "Assets/_Project/Prefabs/UI/Workspace/";
    const string Icons = "Assets/_Project/Art/Icons/ResourceRewards/";

    [MenuItem("Godmist/UI/Assign resource icons to character panels")]
    public static void Configure()
    {
        Save(Root + "PF_UIW_CharacterInfoWindow Variant.prefab", ConfigureCharacter);
        Save(Root + "PF_UIW_SkillTreeWindow Variant.prefab", ConfigureTree);
        AssetDatabase.SaveAssets();
    }

    static void Save(string path, System.Action<GameObject> configure)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try { configure(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static T Ref<T>(Object owner, string field) where T : Object =>
        (T)new SerializedObject(owner).FindProperty(field).objectReferenceValue;

    static void Wire(Object owner, string field, Object value)
    {
        var serialized = new SerializedObject(owner);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static RectTransform Node(Transform parent, string name)
    {
        var child = parent.Find(name);
        if (child == null)
        {
            child = new GameObject(name, typeof(RectTransform)).transform;
            child.SetParent(parent, false);
        }
        child.gameObject.layer = parent.gameObject.layer;
        return (RectTransform)child;
    }

    static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    static TextMeshProUGUI Metric(Transform parent, string name, string iconName, string tooltip,
        TextMeshProUGUI style, Vector2 anchor, Vector2 position, float width)
    {
        var row = Node(parent, name);
        Place(row, anchor, position, new Vector2(width, 26), new Vector2(0, anchor.y));
        var hit = row.GetComponent<Image>() ?? row.gameObject.AddComponent<Image>();
        hit.color = Color.clear;
        hit.raycastTarget = true;
        (row.GetComponent<CombatSimpleTooltipTrigger>() ?? row.gameObject.AddComponent<CombatSimpleTooltipTrigger>())
            .Initialize(null, tooltip, string.Empty);
        var imageRect = Node(row, "Icon");
        Place(imageRect, new Vector2(0, .5f), Vector2.zero, new Vector2(24, 24), new Vector2(0, .5f));
        var image = imageRect.GetComponent<Image>() ?? imageRect.gameObject.AddComponent<Image>();
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Icons + iconName + "-32.png");
        if (image.sprite == null) throw new System.InvalidOperationException("Missing resource icon " + iconName);
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;
        var textRect = Node(row, "Value");
        Place(textRect, new Vector2(0, .5f), new Vector2(32, 0), new Vector2(width - 32, 26), new Vector2(0, .5f));
        var text = textRect.GetComponent<TextMeshProUGUI>() ?? textRect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = style.font;
        text.fontSharedMaterial = style.fontSharedMaterial;
        text.fontSize = style.fontSize;
        text.color = style.color;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.enableAutoSizing = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.margin = Vector4.zero;
        text.raycastTarget = false;
        text.text = "0";
        return text;
    }

    public static void ConfigureCharacter(GameObject root)
    {
        var view = root.GetComponent<CharacterInfoWindowView>();
        var progression = Ref<TextMeshProUGUI>(view, "progressionText");
        Place(progression.rectTransform, Vector2.up, new Vector2(12, -38), new Vector2(256, 22), Vector2.up);
        progression.text = "Poziom 1";
        Wire(view, "experienceValueText", Metric(progression.transform.parent, "ExperienceMetric", "experience",
            "Doświadczenie", progression, Vector2.up, new Vector2(12, -62), 256));
        var state = Ref<TextMeshProUGUI>(view, "honorAvailabilityText");
        Place(state.rectTransform, Vector2.up, new Vector2(12, -674), new Vector2(256, 40), Vector2.up);
        state.text = "Stan: Żywy\nDostępny";
        Wire(view, "honorValueText", Metric(state.transform.parent, "HonorMetric", "honor", "Honor", state,
            Vector2.up, new Vector2(12, -648), 256));
        var footer = Ref<TextMeshProUGUI>(view, "inventoryFooterText");
        Place(footer.rectTransform, Vector2.zero, new Vector2(12, 40), new Vector2(396, 26), Vector2.zero);
        footer.text = "Waga: 0 kg";
        Wire(view, "goldValueText", Metric(footer.transform.parent, "GoldMetric", "gold", "Złoto drużyny", footer,
            Vector2.zero, new Vector2(12, 8), 396));
    }

    public static void ConfigureTree(GameObject root)
    {
        var view = root.GetComponent<SkillTreeProgressionPanelView>();
        var progression = Ref<TextMeshProUGUI>(view, "progressionText");
        Place(progression.rectTransform, Vector2.up, Vector2.zero, new Vector2(116, 26), Vector2.up);
        progression.text = "Poziom 1";
        Wire(view, "experienceValueText", Metric(progression.transform.parent, "ExperienceMetric", "experience",
            "Doświadczenie", progression, Vector2.up, new Vector2(120, 0), 282));
    }
}

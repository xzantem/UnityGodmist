using Godmist.Views.UI.Workspace;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.UI;

public static class EquipmentTooltipUiSetup
{
    public const string SpritePath = "Assets/_Project/UI/ResourceCurrencySprites.asset";
    public const string TooltipPath = "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_Tooltip.prefab";
    public const string CharacterPath = "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CharacterInfoWindow Variant.prefab";

    [MenuItem("Godmist/Content/UI/Configure Equipment Tooltip")]
    public static void Configure()
    {
        var sprites = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(SpritePath);
        if (sprites == null || sprites.spriteGlyphTable.Count == 0)
        {
            var isNew = sprites == null;
            var gold = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Icons/ResourceRewards/gold-32.png");
            if (isNew) sprites = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            sprites.name = "ResourceCurrencySprites";
            var serializedSprites = new SerializedObject(sprites);
            serializedSprites.FindProperty("m_Version").stringValue = "1.1.0";
            serializedSprites.ApplyModifiedPropertiesWithoutUndo();
            sprites.spriteSheet = gold.texture;
            var rect = gold.rect;
            var glyph = new TMP_SpriteGlyph(0, new GlyphMetrics(rect.width, rect.height, 0, rect.height * .82f, rect.width),
                new GlyphRect((int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height), 1, 0);
            glyph.sprite = gold;
            sprites.spriteGlyphTable.Add(glyph);
            sprites.spriteCharacterTable.Add(new TMP_SpriteCharacter(0xFFFE, glyph) { name = "gold", scale = 1 });
            if (sprites.material == null) sprites.material = new Material(Shader.Find("TextMeshPro/Sprite")) { name = "ResourceCurrencySprites Material" };
            sprites.material.mainTexture = gold.texture;
            if (isNew) AssetDatabase.CreateAsset(sprites, SpritePath);
            if (!AssetDatabase.Contains(sprites.material)) AssetDatabase.AddObjectToAsset(sprites.material, sprites);
            sprites.UpdateLookupTables(); EditorUtility.SetDirty(sprites);
        }
        var tooltip = PrefabUtility.LoadPrefabContents(TooltipPath);
        try
        {
            var view = tooltip.GetComponent<UIWorkspaceTooltipView>();
            var body = Ref<TextMeshProUGUI>(view, "bodyText");
            ConfigureColumns(body);
            var meta = Ref<TextMeshProUGUI>(view, "metaText"); meta.spriteAsset = sprites;
            PrefabUtility.SaveAsPrefabAsset(tooltip, TooltipPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(tooltip); }
        var character = PrefabUtility.LoadPrefabContents(CharacterPath);
        try
        {
            var view = character.GetComponent<CharacterInfoWindowView>();
            ConfigureColumns(Ref<TMP_Text>(view, "itemDetailsBody"));
            Ref<TMP_Text>(view, "itemDetailsMeta").spriteAsset = sprites;
            PrefabUtility.SaveAsPrefabAsset(character, CharacterPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(character); }
        AssetDatabase.SaveAssets();
    }
    private static T Ref<T>(Object owner, string field) where T : Object =>
        (T)new SerializedObject(owner).FindProperty(field).objectReferenceValue;
    private static void ConfigureColumns(TMP_Text text)
    {
        var columns = text.GetComponent<ItemStatColumnsView>() ?? text.gameObject.AddComponent<ItemStatColumnsView>();
        var serialized = new SerializedObject(columns);
        serialized.FindProperty("detailsText").objectReferenceValue = text;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        var dividerView = text.GetComponent<ItemDetailsDividerView>();
        if (dividerView != null)
        {
            var divider = Ref<Image>(dividerView, "divider");
            // A sliced frame has a transparent center and leaves only end caps at 2 px.
            // The existing divider Image draws a solid, pixel-aligned rule instead.
            if (divider != null) { divider.sprite = null; divider.type = Image.Type.Simple; }
        }
    }
}

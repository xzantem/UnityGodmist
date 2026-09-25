using Godmist.Core.Enums;
using Godmist.Data.Items;
using Godmist.Data.World;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    public static class CampingKitSetup
    {
        private const string ItemPath = "Assets/_Project/ScriptableObjects/Items/ID_CampingKit.asset";
        private const string CatalogPath = "Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset";

        [MenuItem("Godmist/Content/Configure Camping Kit")]
        public static void Configure()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(CatalogPath);
            if (catalog == null) { Debug.LogError("Brak głównego katalogu zawartości."); return; }
            var definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ItemPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<ItemDefinition>();
                AssetDatabase.CreateAsset(definition, ItemPath);
            }
            Undo.RecordObject(definition, "Configure camping kit");
            var item = new SerializedObject(definition);
            item.FindProperty("<Id>k__BackingField").stringValue = "CampingKit";
            item.FindProperty("<DisplayName>k__BackingField").stringValue = "Zestaw obozowy";
            item.FindProperty("<Description>k__BackingField").stringValue =
                "PPM: rozbij obóz poza walką, przy zebranej drużynie. Odnawia 40% maksymalnego HP i many żywych członków drużyny. Zużywa jeden zestaw. Przy rozpoczęciu wyprawy otrzymujesz jeden zestaw na każde piętro; możesz zużyć je na dowolnym piętrze. Niewykorzystane zestawy znikają przy każdym wyjściu z lochu, również zwojem. Wznowienie wyprawy nie uzupełnia zapasu.";
            item.FindProperty("<ItemType>k__BackingField").intValue = (int)ItemType.Tool;
            item.FindProperty("<BaseValue>k__BackingField").intValue = 0;
            item.FindProperty("<Weight>k__BackingField").floatValue = 0f;
            item.FindProperty("<IsStackable>k__BackingField").boolValue = true;
            item.ApplyModifiedProperties();
            if (!catalog.ItemDefinitions.Contains(definition))
            {
                Undo.RecordObject(catalog, "Register camping kit");
                catalog.ItemDefinitions.Add(definition);
                EditorUtility.SetDirty(catalog);
            }
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();
            Selection.activeObject = definition;
        }
    }
}

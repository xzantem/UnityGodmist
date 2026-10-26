using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Data.Combat;
using Godmist.Data.Dungeons;
using Godmist.Data.Items;
using Godmist.Data.World;
using Godmist.Views.UI.Workspace;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Editor
{
    public static class DungeonPolishSetup
    {
        public const string PatrolId = "encounter.forest.bandit_patrol.balanced.v2";
        public static readonly List<string> Report = new();

        [MenuItem("Godmist/Content/Apply dungeon polish October 24")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            Report.Clear();
            ConfigureLoot();
            ConfigureMissingLootIcons();
            ConfigurePatrol();
            AssetDatabase.SaveAssets();
            var output = Path.GetFullPath("../LocalHandoffs/dungeon-polish-20261024");
            Directory.CreateDirectory(output);
            File.WriteAllLines(Path.Combine(output, "authoring.txt"), Report);
        }

        private static void ConfigureLoot()
        {
            const string path = "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_DungeonLootWindow.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var data = new SerializedObject(root.GetComponent<DungeonLootWindowView>());
                var button = data.FindProperty("collectAllButton").objectReferenceValue as Button;
                if (button == null)
                {
                    var template = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_Button.prefab");
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(template, root.transform);
                    instance.name = "CollectAllButton";
                    button = instance.GetComponent<Button>();
                    button.onClick = new Button.ButtonClickedEvent();
                    data.FindProperty("collectAllButton").objectReferenceValue = button;
                }
                var rect = (RectTransform)button.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 0);
                rect.anchoredPosition = new Vector2(-16, 16); rect.sizeDelta = new Vector2(260, 42);
                var label = button.GetComponentInChildren<TMP_Text>(true);
                label.text = "Zbierz wszystko [B]"; label.fontSize = 20; label.enableAutoSizing = false;
                var empty = data.FindProperty("emptyText").objectReferenceValue as TMP_Text;
                if (empty != null)
                {
                    empty.rectTransform.anchorMax = new Vector2(0, empty.rectTransform.anchorMax.y);
                    empty.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 110);
                }
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Report.Add("Loot button: Zbierz wszystko [B], wired to existing view.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void ConfigureMissingLootIcons()
        {
            const string art = "Assets/_Project/Art/Icons/Items/VS22/";
            var icons = new Dictionary<string,string>
            {
                ["item.material.elf_ear"] = "monster-ingredient-icons-elf-ear-32-v1.png",
                ["item.material.dead_king_ectoplasm"] = "gem-powder-icons-gem-powder-purple-32.png",
                ["item.runic.solid_granite"] = "solid-limestone-32-v2.png",
                ["item.runic.solid_marble"] = "solid-limestone-32-v2.png",
                ["item.runic.galduryte_stone.granite"] = "limestone-galduryte-stone-32-v2.png",
                ["item.runic.galduryte_stone.marble"] = "limestone-galduryte-stone-32-v2.png"
            };
            foreach (var grade in new[]{"good","perfect","strong"})
            {
                icons["item.alchemy."+grade+"_alcohol"] = "alchemy-basics-icons-alcohol-32-v1.png";
                icons["item.alchemy."+grade+"_gunpowder"] = "alchemy-basics-icons-gunpowder-32-v1.png";
            }
            foreach (var guid in AssetDatabase.FindAssets("t:ItemDefinition",new[]{"Assets/_Project/ScriptableObjects"}))
            {
                var item=AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if(item==null || item.Icon!=null || !icons.TryGetValue(item.Id,out var filename)) continue;
                var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(art+filename);
                if(sprite==null) throw new InvalidDataException("Missing authored loot icon: "+art+filename);
                var data=new SerializedObject(item);
                data.FindProperty("<Icon>k__BackingField").objectReferenceValue=sprite;
                data.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssetIfDirty(item);
                Report.Add("Bound missing loot icon: "+item.Id+" -> "+filename);
            }
        }

        private static void ConfigurePatrol()
        {
            const string oldPath = "Assets/_Project/ScriptableObjects/Dungeons/Forest/EncountersBalanced/ED_encounter_forest_bandit_patrol_balanced.asset";
            var old = AssetDatabase.LoadAssetAtPath<EncounterDefinition>(oldPath);
            var nextPath = oldPath.Replace("_balanced.asset", "_balanced_v2.asset");
            var next = AssetDatabase.LoadAssetAtPath<EncounterDefinition>(nextPath);
            if (next == null)
            {
                next = UnityEngine.Object.Instantiate(old);
                next.name = Path.GetFileNameWithoutExtension(nextPath);
                var data = new SerializedObject(next);
                data.FindProperty("<Id>k__BackingField").stringValue = PatrolId;
                data.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.CreateAsset(next, nextPath);
            }
            EncounterBalanceSetup.ApplyRoster(next);
            AssetDatabase.SaveAssetIfDirty(next);
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>("Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
            if (!catalog.EncounterDefinitions.Contains(old)) catalog.EncounterDefinitions.Add(old);
            if (!catalog.EncounterDefinitions.Contains(next)) catalog.EncounterDefinitions.Add(next);
            EditorUtility.SetDirty(catalog);
            foreach (var guid in AssetDatabase.FindAssets("t:DungeonDefinition", new[] { "Assets/_Project/ScriptableObjects" }))
            {
                var dungeon = AssetDatabase.LoadAssetAtPath<DungeonDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                for (var i = 0; i < dungeon.EncounterDefinitions.Count; i++)
                    if (dungeon.EncounterDefinitions[i] == old)
                    { dungeon.EncounterDefinitions[i] = next; EditorUtility.SetDirty(dungeon); }
            }
            Report.Add("New patrol v2: 2 thugs + 1 shooter; old definition retained for saved layouts.");
        }
    }
}

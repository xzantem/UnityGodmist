using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Data.Characters;
using Godmist.Data.Combat;
using Godmist.Data.Dungeons;
using Godmist.Data.World;
using Godmist.Core.Enums;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    public static class EncounterBalanceSetup
    {
        public const string VersionSuffix = ".balanced";
        static readonly Dictionary<string,string[]> ForestRosters = new()
        {
            ["wolf_pair"] = new[]{"rabid_wolf","rabid_wolf","rabid_wolf"},
            ["bandit_pair"] = new[]{"bandit_thug","bandit_thug"},
            ["bandit_patrol"] = new[]{"bandit_thug","bandit_thug","bandit_shooter"},
            ["elf_archers"] = new[]{"elf_poacher","elf_druid","elf_archer"},
            ["elf_druids"] = new[]{"elf_druid","elf_poacher","elf_poacher"},
            ["elf_hunting_party"] = new[]{"elf_poacher","elf_archer","elf_archer"},
            ["infected_trees"] = new[]{"infected_tree","infected_tree"}
        };
        public static bool TryGetRoster(string id,out string[] roster) => ForestRosters.TryGetValue(
            (id ?? "").Replace("encounter.forest.","").Replace(VersionSuffix+".v2","").Replace(VersionSuffix,""),out roster);

        // Used on temporary clones during calibration; production authoring creates
        // versioned definitions so existing dungeon snapshots retain their old rosters.
        public static bool ApplyRoster(EncounterDefinition encounter)
        {
            if(encounter == null || !encounter.Id.StartsWith("encounter.forest.",StringComparison.Ordinal)
                || !TryGetRoster(encounter.Id,out var roster))return false;
            var definitions=AssetDatabase.FindAssets("t:CharacterDefinition",new[]{"Assets/_Project/ScriptableObjects/Dungeons/Forest"})
                .Select(g=>AssetDatabase.LoadAssetAtPath<CharacterDefinition>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(d=>d!=null).ToDictionary(d=>d.Id,StringComparer.Ordinal);
            var data=new SerializedObject(encounter);
            var entries=data.FindProperty("<Enemies>k__BackingField");entries.arraySize=roster.Length;
            for(var i=0;i<roster.Length;i++)
            {
                var definition=definitions["enemy.forest."+roster[i]];
                var entry=entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("<InstanceId>k__BackingField").stringValue=encounter.Id+"."+i;
                entry.FindPropertyRelative("<CharacterDefinition>k__BackingField").objectReferenceValue=definition;
                entry.FindPropertyRelative("<ClassData>k__BackingField").objectReferenceValue=definition.ClassData;
                entry.FindPropertyRelative("<SpawnPosition>k__BackingField").vector2IntValue=new Vector2Int(7+i/3,2+(i%3)*2);
                entry.FindPropertyRelative("<VisionRange>k__BackingField").floatValue=6;
                entry.FindPropertyRelative("<VisionAngle>k__BackingField").floatValue=120;
                entry.FindPropertyRelative("<InitialFacing>k__BackingField").vector2Value=Vector2.left;
            }
            return data.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void ApplyToDungeon(DungeonDefinition dungeon,DungeonRoomTemplateCatalog rooms,GameContentCatalog catalog)
        {
            if(dungeon?.Theme!=DungeonTheme.Forest || catalog==null || rooms==null)return;
            const string folder="Assets/_Project/ScriptableObjects/Dungeons/Forest/EncountersBalanced";
            if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets/_Project/ScriptableObjects/Dungeons/Forest","EncountersBalanced");
            var replacements=new List<EncounterDefinition>();
            foreach(var original in dungeon.EncounterDefinitions)
            {
                if(original==null || !TryGetRoster(original.Id,out _)){replacements.Add(original);continue;}
                var id=original.Id.Contains("bandit_patrol",StringComparison.Ordinal)
                    ? DungeonPolishSetup.PatrolId
                    : original.Id.EndsWith(VersionSuffix,StringComparison.Ordinal)?original.Id:original.Id+VersionSuffix;
                var path=folder+"/ED_"+id.Replace(".","_")+".asset";
                var balanced=AssetDatabase.LoadAssetAtPath<EncounterDefinition>(path);
                if(balanced==null)
                {
                    balanced=UnityEngine.Object.Instantiate(original);balanced.name=System.IO.Path.GetFileNameWithoutExtension(path);
                    var data=new SerializedObject(balanced);data.FindProperty("<Id>k__BackingField").stringValue=id;
                    data.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.CreateAsset(balanced,path);
                }
                if(ApplyRoster(balanced))EditorUtility.SetDirty(balanced);
                AssetDatabase.SaveAssetIfDirty(balanced);
                if(!catalog.EncounterDefinitions.Contains(original))catalog.EncounterDefinitions.Add(original);
                if(!catalog.EncounterDefinitions.Contains(balanced))catalog.EncounterDefinitions.Add(balanced);
                if(!replacements.Contains(balanced))replacements.Add(balanced);
            }
            if(!dungeon.EncounterDefinitions.SequenceEqual(replacements))
            {
                dungeon.EncounterDefinitions.Clear();dungeon.EncounterDefinitions.AddRange(replacements);
                EditorUtility.SetDirty(dungeon);
            }
            EnsureEnemySlots(rooms);
            EditorUtility.SetDirty(catalog);
        }

        static void EnsureEnemySlots(DungeonRoomTemplateCatalog catalog)
        {
            foreach(var room in catalog.Templates.Where(r=>r!=null && !r.ExcludeFromRandomEncounters
                && r.RoomType is DungeonRoomType.Normal or DungeonRoomType.Optional))
            {
                var enemies=room.SemanticAnchors.Where(a=>a.Type==DungeonSemanticAnchorType.Enemy).ToArray();
                var used=new HashSet<Vector2Int>();
                foreach(var anchor in room.SemanticAnchors)
                    foreach(var cell in new RectInt(anchor.LocalCell,anchor.AreaSize).allPositionsWithin)used.Add(cell);
                used.UnionWith(room.StructuralAnchors.Select(a=>a.LocalCell));
                for(var index=0;index<4;index++)
                {
                    if(enemies.Any(a=>a.EncounterEnemyIndex==index))continue;
                    var points=room.SemanticAnchors.Where(a=>a.Type==DungeonSemanticAnchorType.Enemy).Select(a=>a.LocalCell).ToArray();
                    var center=(Vector2)room.SizeInCells*.5f;
                    var cells=room.Footprint.Enumerate(room.SizeInCells).Where(c=>!used.Contains(c) && !room.DifficultTerrainCells.Contains(c))
                        .OrderBy(c=>points.Any(p=>(p-c).sqrMagnitude<4)?1:0)
                        .ThenBy(c=>((Vector2)c-center).sqrMagnitude).ThenBy(c=>c.y).ThenBy(c=>c.x).ToArray();
                    if(cells.Length==0)throw new InvalidOperationException("No legal encounter slot in "+room.Id);
                    var cell=cells[0];used.Add(cell);
                    room.SemanticAnchors.Add(new DungeonSemanticAnchorData("forest.wolf."+index,
                        DungeonSemanticAnchorType.Enemy,cell,Vector2Int.one,encounterEnemyIndex:index));
                    EditorUtility.SetDirty(room);
                }
                DungeonRoomContentPlannerValidation(room);
                AssetDatabase.SaveAssetIfDirty(room);
            }
        }
        static void DungeonRoomContentPlannerValidation(DungeonRoomTemplateDefinition room)
        {
            // Keep validation in the existing Editor validator; no runtime fallback spawns.
            var report=DungeonRoomTemplateEditorValidator.Validate(room);
            if(!report.IsValid)throw new InvalidOperationException(room.Id+": "+string.Join("; ",report.Errors));
        }
        [MenuItem("Godmist/Content/Apply forest encounter balance")]
        public static void ApplyAll()
        {
            var catalog=AssetDatabase.LoadAssetAtPath<GameContentCatalog>(ForestExpeditionsSetup.CatalogPath);
            foreach(var guid in AssetDatabase.FindAssets("t:DungeonDefinition",new[]{"Assets/_Project/ScriptableObjects/Dungeons/Forest"}))
            {
                var dungeon=AssetDatabase.LoadAssetAtPath<DungeonDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                ApplyToDungeon(dungeon,dungeon.RoomTemplateCatalog,catalog);AssetDatabase.SaveAssetIfDirty(dungeon);
            }
            AssetDatabase.SaveAssetIfDirty(catalog);
        }
    }
}

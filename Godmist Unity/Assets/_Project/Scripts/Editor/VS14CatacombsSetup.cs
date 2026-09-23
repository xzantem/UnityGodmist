using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using Godmist.Data.World;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    /// <summary>One-time, repeatable authoring of the VS-14 Catacombs layout assets.</summary>
    public static class VS14CatacombsSetup
    {
        private const string Root = "Assets/_Project/ScriptableObjects/Dungeons/";
        private const string Rooms = Root + "RoomTemplates/";
        private const string ProfilePath = Root + "LayoutProfiles/DLP_Catacombs_Production.asset";
        private const string CatalogPath = Root + "DTC_Catacombs_Production.asset";
        private const string RegularPath = Root + "DD_Catacombs_Expedition.asset";
        private const string BossPath = Root + "DD_Catacombs_BossExpedition.asset";
        private const string ContentCatalogPath = "Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset";
        private const string FieldPrefix = "<";

        private static readonly string[] ExistingRoomNames =
        {
            "Start", "Normal_Small", "Normal_Large", "L", "Tactical_CryptL",
            "Tactical_Pillars", "Tactical_Passages", "Tactical_Ritual",
            "Optional", "Optional_Lever", "Optional_Condition", "Corridor", "Final"
        };

        private static string Field(string name) => FieldPrefix + name + ">k__BackingField";

        private static T Require<T>(string path) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(path)
            ?? throw new InvalidOperationException("VS-14: missing asset " + path);

        private static T GetOrCreate<T>(string path, Func<T> create) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var asset = create();
            asset.name = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void SetString(ScriptableObject target, string name, string value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(Field(name)).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static DungeonSemanticAnchorData Pillar(
            string id, int x, int y, DungeonObjectDefinition definition) =>
            new(id, DungeonSemanticAnchorType.Obstacle, new Vector2Int(x, y),
                Vector2Int.one, isRequired: true, objectDefinition: definition);

        private static DungeonSemanticAnchorData Enemy(int index, int x, int y) =>
            new("enemy." + index, DungeonSemanticAnchorType.Enemy,
                new Vector2Int(x, y), Vector2Int.one, encounterEnemyIndex: index);

        /// <summary>Can be used with an unsaved in-memory asset for validation tests.</summary>
        public static void ConfigureNewNormalRoom(DungeonRoomTemplateDefinition room,
            string id, string displayName, Vector2Int size, bool longHall,
            DungeonObjectDefinition pillar)
        {
            if (room == null || pillar == null) throw new ArgumentNullException();
            var serialized = new SerializedObject(room);
            serialized.FindProperty(Field("Id")).stringValue = id;
            serialized.FindProperty(Field("DisplayName")).stringValue = displayName;
            serialized.FindProperty(Field("RoomType")).enumValueIndex = (int)DungeonRoomType.Normal;
            serialized.FindProperty(Field("SizeInCells")).vector2IntValue = size;
            serialized.FindProperty(Field("BoundaryEdgesConfigured")).boolValue = false;
            serialized.FindProperty(Field("AllowedBoundaryEdges")).ClearArray();
            serialized.ApplyModifiedPropertiesWithoutUndo();

            room.SemanticAnchors.Clear();
            room.ContentGroups.Clear();
            room.StructuralAnchors.Clear();
            room.DifficultTerrainCells.Clear();
            room.Footprint.FloorCellOffsets.Clear();
            room.Surfaces.Clear();
            room.Stairs.Clear();
            if (longHall)
            {
                room.SemanticAnchors.AddRange(new[]
                {
                    Pillar("sarcophagus.west.1", 5, 2, pillar),
                    Pillar("sarcophagus.west.2", 6, 2, pillar),
                    Pillar("sarcophagus.east.1", 11, 5, pillar),
                    Pillar("sarcophagus.east.2", 12, 5, pillar),
                    Enemy(0, 3, 4), Enemy(1, 8, 4),
                    Enemy(2, 14, 3), Enemy(3, 15, 6)
                });
            }
            else
            {
                room.SemanticAnchors.AddRange(new[]
                {
                    Pillar("ossuary.nw", 4, 4, pillar),
                    Pillar("ossuary.ne", 9, 4, pillar),
                    Pillar("ossuary.sw", 4, 9, pillar),
                    Pillar("ossuary.se", 9, 9, pillar),
                    Enemy(0, 2, 6), Enemy(1, 6, 2),
                    Enemy(2, 11, 7), Enemy(3, 7, 11)
                });
            }
            room.MigrateLegacyBoundaryEdges();
            EditorUtility.SetDirty(room);
        }

        private static DungeonRoomTemplateDefinition ConfigureNormal(
            string file, string id, string displayName, Vector2Int size,
            bool longHall, DungeonObjectDefinition pillar)
        {
            var room = GetOrCreate(Rooms + file + ".asset",
                ScriptableObject.CreateInstance<DungeonRoomTemplateDefinition>);
            ConfigureNewNormalRoom(room, id, displayName, size, longHall, pillar);
            return room;
        }

        private static DungeonRoomTemplateDefinition ConfigureCorridor()
        {
            var room = GetOrCreate(Rooms + "DRT_Catacombs_Tactical_Corridor_Production.asset",
                () => UnityEngine.Object.Instantiate(Require<DungeonRoomTemplateDefinition>(
                    Rooms + "DRT_Catacombs_Tactical_Corridor.asset")));
            ConfigureProductionCorridor(room);
            EditorUtility.SetDirty(room);
            return room;
        }

        public static void ConfigureProductionCorridor(DungeonRoomTemplateDefinition room)
        {
            var serialized = new SerializedObject(room);
            serialized.FindProperty(Field("Id")).stringValue = "room.catacombs.corridor.tactical.production";
            serialized.FindProperty(Field("DisplayName")).stringValue = "Korytarz z przeszkodami";
            serialized.FindProperty(Field("RoomType")).enumValueIndex = (int)DungeonRoomType.Corridor;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static DungeonLayoutProfile ConfigureProfile()
        {
            var profile = GetOrCreate(ProfilePath,
                () => UnityEngine.Object.Instantiate(Require<DungeonLayoutProfile>(
                    Root + "LayoutProfiles/DLP_OrganicSpaces.asset")));
            ConfigureProductionProfile(profile);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        public static void ConfigureProductionProfile(DungeonLayoutProfile profile)
        {
            var serialized = new SerializedObject(profile);
            serialized.FindProperty(Field("Id")).stringValue = "layout.catacombs.production";
            serialized.FindProperty(Field("MaxLateralOffsetCells")).intValue = 3;
            serialized.FindProperty(Field("MainPathTurnChance")).floatValue = 0.6f;
            serialized.FindProperty(Field("PreferCompactMainPath")).boolValue = true;
            serialized.FindProperty(Field("MinDoorwayWidth")).intValue = 1;
            serialized.FindProperty(Field("MaxDoorwayWidth")).intValue = 2;
            serialized.FindProperty(Field("CorridorRoomChance")).floatValue = 0.3f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static DungeonDefinition ConfigureExpedition(string path, string id,
            string displayName, DungeonRoomTemplateCatalog rooms,
            DungeonLayoutProfile profile, bool hasBoss)
        {
            var definition = GetOrCreate(path,
                () => UnityEngine.Object.Instantiate(Require<DungeonDefinition>(
                    Root + "DD_Catacombs_Procedural.asset")));
            ConfigureExpeditionData(definition, id, displayName, rooms, profile, hasBoss);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        public static void ConfigureExpeditionData(DungeonDefinition definition, string id,
            string displayName, DungeonRoomTemplateCatalog rooms,
            DungeonLayoutProfile profile, bool hasBoss)
        {
            var serialized = new SerializedObject(definition);
            serialized.FindProperty(Field("Id")).stringValue = id;
            serialized.FindProperty(Field("DisplayName")).stringValue = displayName;
            serialized.FindProperty(Field("IsAvailableAsOffer")).boolValue = true;
            serialized.FindProperty(Field("LayoutProfile")).objectReferenceValue = profile;
            serialized.FindProperty(Field("RoomTemplateCatalog")).objectReferenceValue = rooms;
            serialized.FindProperty(Field("MinFloorCount")).intValue = 2;
            serialized.FindProperty(Field("MaxFloorCount")).intValue = 2;
            serialized.FindProperty(Field("MinRoomCountPerFloor")).intValue = 11;
            serialized.FindProperty(Field("MaxRoomCountPerFloor")).intValue = 14;
            serialized.FindProperty(Field("MinOptionalRoomCount")).intValue = 2;
            serialized.FindProperty(Field("MaxOptionalRoomCount")).intValue = 3;
            serialized.FindProperty(Field("MinLoopConnections")).intValue = 1;
            serialized.FindProperty(Field("MaxLoopConnections")).intValue = 2;
            serialized.FindProperty(Field("MinProgressionGateCount")).intValue = 1;
            serialized.FindProperty(Field("MaxProgressionGateCount")).intValue = 2;
            serialized.FindProperty(Field("MinEncounterCountPerFloor")).intValue = 4;
            serialized.FindProperty(Field("MaxEncounterCountPerFloor")).intValue = 6;
            if (!hasBoss)
                serialized.FindProperty(Field("BossEncounterDefinition")).objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("Godmist/Content/Configure VS-14 Catacombs")]
        public static void Configure()
        {
            var authored = VS14RoomBlueprintAuthoring.Configure();
            var catalog = GetOrCreate(CatalogPath,
                ScriptableObject.CreateInstance<DungeonRoomTemplateCatalog>);
            SetString(catalog, "Id", "dungeon.catacombs.templates.production");
            catalog.Templates.Clear();
            catalog.Templates.AddRange(authored);
            foreach (var room in catalog.Templates)
            {
                var report = DungeonRoomTemplateEditorValidator.Validate(room);
                if (!report.IsValid)
                    throw new InvalidOperationException(room.name + ": " + string.Join("; ", report.Errors));
                if (room.Stairs.Count > 0 || room.EnumerateSurfaces().Any(surface => surface.Level != 0))
                    throw new InvalidOperationException("Elevated room in production catalog: " + room.name);
            }
            if (catalog.Templates.Count < 16 || catalog.Templates.Select(room => room.Id).Distinct().Count() != catalog.Templates.Count)
                throw new InvalidOperationException("VS-14 needs at least 16 distinct room templates.");
            EditorUtility.SetDirty(catalog);

            var profile = ConfigureProfile();
            var regular = ConfigureExpedition(RegularPath, "dungeon.catacombs.expedition",
                "Katakumby - wyprawa", catalog, profile, false);
            var boss = ConfigureExpedition(BossPath, "dungeon.catacombs.boss_expedition",
                "Katakumby - wyprawa z bossem", catalog, profile, true);
            VS14RoomBlueprintAuthoring.AddRequiredObjectDefinitions(regular);
            VS14RoomBlueprintAuthoring.AddRequiredObjectDefinitions(boss);
            var content = Require<GameContentCatalog>(ContentCatalogPath);
            if (!content.DungeonDefinitions.Contains(regular)) content.DungeonDefinitions.Add(regular);
            if (!content.DungeonDefinitions.Contains(boss)) content.DungeonDefinitions.Add(boss);
            EditorUtility.SetDirty(content);
            AssetDatabase.SaveAssets();
            Debug.Log("VS-14 Catacombs configured: " + catalog.Templates.Count
                      + " flat authored rooms, two offerable expeditions.");
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using Godmist.Data.World;
using Godmist.Systems.Dungeons;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Godmist.Editor
{
    public static class ForestPrototypeSetup
    {
        [Serializable]
        private sealed class Blueprints
        {
            public Blueprint[] rooms;
        }

        [Serializable]
        private sealed class Blueprint
        {
            public string id;

            public string name;

            public string type;

            public string[] rows;
        }

        public const string Root = "Assets/_Project/ScriptableObjects/Dungeons/Forest/";

        public const string DefinitionPath = "Assets/_Project/ScriptableObjects/Dungeons/Forest/DD_Forest_Prototype.asset";

        private const string BlueprintPath = "Assets/_Project/Scripts/Editor/ForestRoomBlueprints.json";

        public static DungeonDefinition LoadImportedPrototype()
        {
            AssetDatabase.ImportAsset("Assets/_Project/ScriptableObjects/Dungeons/Forest/DO_Forest_Tree_Prototype.asset", ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset("Assets/_Project/ScriptableObjects/Dungeons/Forest/DD_Forest_Prototype.asset", ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            DungeonDefinition dungeonDefinition = AssetDatabase.LoadAssetAtPath<DungeonDefinition>("Assets/_Project/ScriptableObjects/Dungeons/Forest/DD_Forest_Prototype.asset");
            if (dungeonDefinition == null)
            {
                throw new InvalidOperationException("Brak assetu prototypu lasu.");
            }
            string[] array = new string[] { "DO_Forest_Tree_Prototype", "DO_Forest_Boulder_Prototype", "DO_Forest_Stump_Prototype", "DO_Forest_RuinedPillar_Prototype", "DO_Forest_Gravestone_Prototype", "DO_Forest_ColdCampfire_Prototype", "DO_Forest_BrokenCrate_Prototype", "DO_Forest_Bedroll_Prototype" };
            foreach (string text in array)
            {
                AssetDatabase.ImportAsset("Assets/_Project/ScriptableObjects/Dungeons/Forest/" + text + ".asset", ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                ObstacleDungeonObjectDefinition obstacleDungeonObjectDefinition = AssetDatabase.LoadAssetAtPath<ObstacleDungeonObjectDefinition>("Assets/_Project/ScriptableObjects/Dungeons/Forest/" + text + ".asset");
                if (obstacleDungeonObjectDefinition == null)
                {
                    throw new InvalidOperationException("Brak assetu: " + text);
                }
                if (!dungeonDefinition.ObjectDefinitions.Contains(obstacleDungeonObjectDefinition))
                {
                    dungeonDefinition.ObjectDefinitions.Add(obstacleDungeonObjectDefinition);
                    EditorUtility.SetDirty(dungeonDefinition);
                    AssetDatabase.SaveAssetIfDirty(dungeonDefinition);
                }
            }
            foreach (var path in new[] { ForestContainersSetup.ChestPath, ForestContainersSetup.CachePath })
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                var container = AssetDatabase.LoadAssetAtPath<ChestDungeonObjectDefinition>(path);
                if (container != null && !dungeonDefinition.ObjectDefinitions.Contains(container))
                {
                    dungeonDefinition.ObjectDefinitions.Add(container);
                    EditorUtility.SetDirty(dungeonDefinition);
                    AssetDatabase.SaveAssetIfDirty(dungeonDefinition);
                }
            }
            DungeonObjectPlacementSystem.ValidateContentDefinitions(dungeonDefinition);
            return dungeonDefinition;
        }

        private static void Set(ScriptableObject asset, string property, Action<SerializedProperty> assign)
        {
            SerializedObject serializedObject = new SerializedObject(asset);
            SerializedProperty serializedProperty = serializedObject.FindProperty("<" + property + ">k__BackingField");
            if (serializedProperty == null)
            {
                throw new InvalidOperationException(asset.GetType().Name + "." + property);
            }
            assign(serializedProperty);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T Asset<T>(string file) where T : ScriptableObject
        {
            string text = "Assets/_Project/ScriptableObjects/Dungeons/Forest/" + file + ".asset";
            T val = AssetDatabase.LoadAssetAtPath<T>(text);
            if (val != null)
            {
                return val;
            }
            val = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(val, text);
            return val;
        }

        public static void ConfigureProfile(DungeonLayoutProfile profile)
        {
            Set(profile, "Id", p => p.stringValue = "layout.forest.prototype");
            Set(profile, "DoorPlacementStyle", p => p.enumValueIndex = 2);
            Set(profile, "MaxLateralOffsetCells", p => p.intValue = 5);
            Set(profile, "MainPathTurnChance", p => p.floatValue = 0.8f);
            Set(profile, "PreferCompactMainPath", p => p.boolValue = false);
            Set(profile, "MinDoorwayWidth", p => p.intValue = 2);
            Set(profile, "MaxDoorwayWidth", p => p.intValue = 3);
            Set(profile, "CorridorRoomChance", p => p.floatValue = 0.55f);
        }

        public static void ConfigureDefinition(DungeonDefinition dungeon, DungeonRoomTemplateCatalog catalog, DungeonLayoutProfile profile)
        {
            Set(dungeon, "Id", p => p.stringValue = "dungeon.forest.prototype");
            Set(dungeon, "DisplayName", p => p.stringValue = "Skażony Las — prototyp");
            Set(dungeon, "Theme", p => p.enumValueIndex = 1);
            Set(dungeon, "IsProcedural", p => p.boolValue = true);
            Set(dungeon, "EnvironmentConfigured", p => p.boolValue = true);
            Set(dungeon, "IsIndoor", p => p.boolValue = false);
            Set(dungeon, "IsAvailableAsOffer", p => p.boolValue = false);
            Set(dungeon, "RoomTemplateCatalog", p => p.objectReferenceValue = catalog);
            Set(dungeon, "LayoutProfile", p => p.objectReferenceValue = profile);
            Set(dungeon, "MinRoomCountPerFloor", p => p.intValue = 10);
            Set(dungeon, "MaxRoomCountPerFloor", p => p.intValue = 13);
            Set(dungeon, "MinOptionalRoomCount", p => p.intValue = 3);
            Set(dungeon, "MaxOptionalRoomCount", p => p.intValue = 4);
            Set(dungeon, "MinEncounterCountPerFloor", p => p.intValue = 0);
            Set(dungeon, "MaxEncounterCountPerFloor", p => p.intValue = 0);
            Set(dungeon, "MinObjectCountPerFloor", p => p.intValue = 0);
            Set(dungeon, "MaxObjectCountPerFloor", p => p.intValue = 0);
        }

        public static void ConfigureRoom(DungeonRoomTemplateDefinition room, string id, string displayName, DungeonRoomType type, string[] rows, ObstacleDungeonObjectDefinition tree = null, ObstacleDungeonObjectDefinition[] edgeProps = null)
        {
            if (rows == null || rows.Length == 0 || rows.Any((string r) => r.Length != rows[0].Length))
            {
                throw new ArgumentException("Forest blueprint must be rectangular.");
            }
            if (rows.Any((string r) => r.Any((char c) => c != '.' && c != '#' && c != '~')))
            {
                throw new ArgumentException("Unknown forest blueprint symbol.");
            }
            Set(room, "Id", p => p.stringValue = "room.forest." + id);
            Set(room, "DisplayName", p => p.stringValue = displayName);
            Set(room, "RoomType", p => p.enumValueIndex = (int)type);
            Set(room, "SizeInCells", p => p.vector2IntValue = new Vector2Int(rows[0].Length, rows.Length));
            room.Footprint.FloorCellOffsets.Clear();
            room.AllowedBoundaryEdges.Clear();
            Set(room, "BoundaryEdgesConfigured", p => p.boolValue = false);
            room.DifficultTerrainCells.Clear();
            room.SemanticAnchors.Clear();
            room.StructuralAnchors.Clear();
            room.ContentGroups.Clear();
            room.Surfaces.Clear();
            room.Stairs.Clear();
            for (int num = 0; num < rows.Length; num++)
            {
                for (int num2 = 0; num2 < rows[num].Length; num2++)
                {
                    if (rows[num][num2] != '#')
                    {
                        Vector2Int item = new Vector2Int(num2, rows.Length - 1 - num);
                        room.Footprint.FloorCellOffsets.Add(item);
                        if (rows[num][num2] == '~')
                        {
                            room.DifficultTerrainCells.Add(item);
                        }
                    }
                }
            }
            RemoveInteriorTrees(room, tree);
            if (edgeProps != null && tree != null && (type == DungeonRoomType.Start || type == DungeonRoomType.Normal || type == DungeonRoomType.Optional))
            {
                HashSet<Vector2Int> hashSet2 = room.Footprint.FloorCellOffsets.ToHashSet();
                var crossClearance = id == "blind_glade" ? 1f : 1.25f;
                List<Vector2Int> reserved = room.SemanticAnchors.Select((DungeonSemanticAnchorData a) => a.LocalCell).ToList();
                Vector2Int? propFocus = null;
                var propSpacingSquared = 4;
                bool AvailablePropCell(Vector2Int c)
                {
                    if (Mathf.Abs(c.x - (rows[0].Length - 1) * .5f) < crossClearance
                        || Mathf.Abs(c.y - (rows.Length - 1) * .5f) < crossClearance
                        || reserved.Any(r => (r-c).sqrMagnitude < propSpacingSquared)
                        || room.DifficultTerrainCells.Contains(c)) return false;
                    for (var y=-1;y<=1;y++) for (var x=-1;x<=1;x++)
                        if (!hashSet2.Contains(c + new Vector2Int(x,y))) return false;
                    return true;
                }
                foreach (ObstacleDungeonObjectDefinition obstacleDungeonObjectDefinition in edgeProps)
                {
                    if (obstacleDungeonObjectDefinition == null)
                    {
                        continue;
                    }
                    propSpacingSquared = id == "blind_glade" && obstacleDungeonObjectDefinition.Id == "object.forest.bedroll.prototype" ? 1 : 4;
                    var available = hashSet2.Where(AvailablePropCell).ToArray();
                    foreach (Vector2Int cell2 in from c in available
                        orderby !propFocus.HasValue && edgeProps.Length > 1
                            && !available.Any(n => (n-c).sqrMagnitude >= 4 && (n-c).sqrMagnitude <= 10) ? 1 : 0,
                            propFocus.HasValue ? (c - propFocus.Value).sqrMagnitude : (reserved.Count > 0) ? ((float)reserved.Min((Vector2Int r) => (r - c).sqrMagnitude)) : (0f - (c - new Vector2((float)(rows[0].Length - 1) * 0.5f, (float)(rows.Length - 1) * 0.5f)).sqrMagnitude), c.y, c.x
                        select c)
                    {
                        room.SemanticAnchors.Add(new DungeonSemanticAnchorData("edgeprop." + reserved.Count, DungeonSemanticAnchorType.Obstacle, cell2, Vector2Int.one, 1f, isRequired: true, null, null, -1, obstacleDungeonObjectDefinition));
                        reserved.Add(cell2);
                        propFocus ??= cell2;
                        break;
                    }
                }
            }
            // Keep older tree anchors out of the finished prop composition.
            RemoveInteriorTrees(room, tree);
            room.AllowedBoundaryEdges.Clear();
            room.AllowedBoundaryEdges.AddRange(DungeonRoomBoundaryUtility.EnumerateExternalBoundaryEdges(room));
            Set(room, "BoundaryEdgesConfigured", p => p.boolValue = true);
            DungeonRoomTemplateValidationReport dungeonRoomTemplateValidationReport = DungeonRoomTemplateEditorValidator.Validate(room);
            if (!dungeonRoomTemplateValidationReport.IsValid)
            {
                throw new InvalidOperationException(id + ": " + string.Join("; ", dungeonRoomTemplateValidationReport.Errors));
            }
        }

        private static ObstacleDungeonObjectDefinition ConfigureEdgeProp(string assetName, string id, string displayName, string spriteName, float width, float radius, bool blocksSight)
        {
            ObstacleDungeonObjectDefinition obstacleDungeonObjectDefinition = Asset<ObstacleDungeonObjectDefinition>(assetName);
            Set(obstacleDungeonObjectDefinition, "Id", p => p.stringValue = id);
            Set(obstacleDungeonObjectDefinition, "DisplayName", p => p.stringValue = displayName);
            Set(obstacleDungeonObjectDefinition, "BlocksMovement", p => p.boolValue = true);
            Set(obstacleDungeonObjectDefinition, "BlocksLineOfSight", p => p.boolValue = blocksSight);
            Set(obstacleDungeonObjectDefinition, "PresentationSprite", p => p.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/ForestPrototype/" + spriteName + "-32ppu.png"));
            Set(obstacleDungeonObjectDefinition, "PresentationTint", p => p.colorValue = new Color(0.65f, 0.68f, 0.57f));
            Set(obstacleDungeonObjectDefinition, "NativePixelScale", p => p.boolValue = true);
            Set(obstacleDungeonObjectDefinition, "PresentationWidth", p => p.floatValue = width);
            Set(obstacleDungeonObjectDefinition, "TrunkRadius", p => p.floatValue = radius);
            EditorUtility.SetDirty(obstacleDungeonObjectDefinition);
            return obstacleDungeonObjectDefinition;
        }

        [MenuItem("Godmist/Content/Pass 1b/Configure Forest Prototype")]
        public static void RefreshTreeFootprints()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DungeonRoomTemplateCatalog>(Root + "DTC_Forest_Prototype.asset");
            var tree = AssetDatabase.LoadAssetAtPath<ObstacleDungeonObjectDefinition>(Root + "DO_Forest_Tree_Prototype.asset");
            if (catalog == null || tree == null) throw new InvalidOperationException("Missing forest tree authoring.");
            foreach (var room in catalog.Templates)
            {
                int removed = room.SemanticAnchors.RemoveAll(a => a.ObjectDefinition == tree);
                if (removed == 0) continue;
                EditorUtility.SetDirty(room);
                AssetDatabase.SaveAssetIfDirty(room);
            }
        }

        private static void RemoveInteriorTrees(DungeonRoomTemplateDefinition room, ObstacleDungeonObjectDefinition tree)
        {
            if (tree == null) return;
            // Mature trees belong to the woodland boundary. Interior clearings
            // stay free of their large crowns and navigation/LOS footprints.
            // Reauthoring must also remove anchors left by older content.
            room.SemanticAnchors.RemoveAll(a => a.ObjectDefinition == tree);
        }

        public static void Configure()
        {
            Directory.CreateDirectory("Assets/_Project/ScriptableObjects/Dungeons/Forest/");
            AssetDatabase.Refresh();
            Blueprint[] rooms = JsonUtility.FromJson<Blueprints>(File.ReadAllText("Assets/_Project/Scripts/Editor/ForestRoomBlueprints.json")).rooms;
            if (rooms == null || rooms.Select((Blueprint s) => s.id).Distinct().Count() != rooms.Length)
            {
                throw new InvalidOperationException("Missing or duplicate forest blueprint.");
            }
            DungeonRoomTemplateCatalog dungeonRoomTemplateCatalog = Asset<DungeonRoomTemplateCatalog>("DTC_Forest_Prototype");
            ObstacleDungeonObjectDefinition obstacleDungeonObjectDefinition = Asset<ObstacleDungeonObjectDefinition>("DO_Forest_Tree_Prototype");
            Set(obstacleDungeonObjectDefinition, "Id", p => p.stringValue = "object.forest.tree.prototype");
            Set(obstacleDungeonObjectDefinition, "DisplayName", p => p.stringValue = "Skręcone drzewo");
            Set(obstacleDungeonObjectDefinition, "BlocksMovement", p => p.boolValue = true);
            Set(obstacleDungeonObjectDefinition, "BlocksLineOfSight", p => p.boolValue = true);
            Set(obstacleDungeonObjectDefinition, "PresentationSprite", p => p.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/ForestPrototype/forest-tree-mature-oak-v2-32ppu.png"));
            Set(obstacleDungeonObjectDefinition, "PresentationVariants", delegate(SerializedProperty p)
            {
                p.arraySize = 3;
                p.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/ForestPrototype/forest-tree-mature-elm-v2-32ppu.png");
                p.GetArrayElementAtIndex(1).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/ForestPrototype/forest-tree-mature-spruce-v2-32ppu.png");
                p.GetArrayElementAtIndex(2).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/ForestPrototype/forest-tree-mature-hollow-v2-32ppu.png");
            });
            Set(obstacleDungeonObjectDefinition, "PresentationTint", p => p.colorValue = new Color(0.48f, 0.52f, 0.45f));
            Set(obstacleDungeonObjectDefinition, "NativePixelScale", p => p.boolValue = true);
            Set(obstacleDungeonObjectDefinition, "PresentationWidth", p => p.floatValue = 1.5f);
            Set(obstacleDungeonObjectDefinition, "TrunkRadius", p => p.floatValue = 0.18f);
            ObstacleDungeonObjectDefinition obstacleDungeonObjectDefinition2 = ConfigureEdgeProp("DO_Forest_Boulder_Prototype", "object.forest.boulder.prototype", "Omszały głaz", "forest-boulder-v1", 1.4f, 0.32f, blocksSight: true);
            Set(obstacleDungeonObjectDefinition2, "PresentationVariants", p =>
            {
                p.arraySize = 2;
                p.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/ForestPrototype/forest-boulder-fractured-v1-32ppu.png");
                p.GetArrayElementAtIndex(1).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/ForestPrototype/forest-boulder-lichen-v1-32ppu.png");
            });
            ObstacleDungeonObjectDefinition obstacleDungeonObjectDefinition3 = ConfigureEdgeProp("DO_Forest_Stump_Prototype", "object.forest.stump.prototype", "Spróchniały pniak", "forest-stump-v1", 1.2f, 0.22f, blocksSight: false);
            Set(obstacleDungeonObjectDefinition3, "PresentationVariants", p =>
            {
                p.arraySize = 2;
                p.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/ForestPrototype/forest-stump-rotten-v1-32ppu.png");
                p.GetArrayElementAtIndex(1).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/ForestPrototype/forest-stump-charred-v1-32ppu.png");
            });
            var ruinedPillar = ConfigureEdgeProp("DO_Forest_RuinedPillar_Prototype", "object.forest.ruined_pillar.prototype", "Pochłonięta kolumna", "forest-ruined-pillar-v1", 1.7f, .3f, true);
            var gravestone = ConfigureEdgeProp("DO_Forest_Gravestone_Prototype", "object.forest.gravestone.prototype", "Omszały nagrobek", "forest-gravestone-v1", 1f, .2f, false);
            var campfire = ConfigureEdgeProp("DO_Forest_ColdCampfire_Prototype", "object.forest.cold_campfire.prototype", "Palenisko", "forest-cold-campfire-v1", 1.7f, .26f, false);
            var bedroll = ConfigureEdgeProp("DO_Forest_Bedroll_Prototype", "object.forest.bedroll.prototype", "Podarty śpiwór", "forest-bedroll-v1", 1.25f, 0f, false);
            Set(bedroll, "BlocksMovement", p => p.boolValue = false);
            var crate = ConfigureEdgeProp("DO_Forest_BrokenCrate_Prototype", "object.forest.broken_crate.prototype", "Rozbita skrzynia", "forest-broken-crate-v1", 1.25f, .28f, false);
            Set(dungeonRoomTemplateCatalog, "Id", p => p.stringValue = "dungeon.forest.templates.prototype");
            Set(dungeonRoomTemplateCatalog, "HasVegetationFringe", p => p.boolValue = true);
            dungeonRoomTemplateCatalog.Templates.Clear();
            Blueprint[] array = rooms;
            foreach (Blueprint blueprint in array)
            {
                DungeonRoomTemplateDefinition dungeonRoomTemplateDefinition = Asset<DungeonRoomTemplateDefinition>("DRT_Forest_" + blueprint.id);
                var props = blueprint.id switch
                {
                    "overgrown_ruins" => new[] { ruinedPillar, gravestone, obstacleDungeonObjectDefinition3 },
                    "blind_glade" => new[] { campfire, crate, bedroll },
                    "roots" => new[] { obstacleDungeonObjectDefinition3, obstacleDungeonObjectDefinition3 },
                    "hollow" => new[] { obstacleDungeonObjectDefinition2, obstacleDungeonObjectDefinition2 },
                    _ => new[] { obstacleDungeonObjectDefinition2, obstacleDungeonObjectDefinition3 }
                };
                ConfigureRoom(dungeonRoomTemplateDefinition, blueprint.id, blueprint.id == "blind_glade" ? "Porzucone obozowisko" : blueprint.name, Enum.Parse<DungeonRoomType>(blueprint.type), blueprint.rows, obstacleDungeonObjectDefinition, props);
                dungeonRoomTemplateCatalog.Templates.Add(dungeonRoomTemplateDefinition);
                EditorUtility.SetDirty(dungeonRoomTemplateDefinition);
            }
            DungeonLayoutProfile dungeonLayoutProfile = Asset<DungeonLayoutProfile>("DLP_Forest_Prototype");
            ConfigureProfile(dungeonLayoutProfile);
            DungeonDefinition dungeonDefinition = Asset<DungeonDefinition>("DD_Forest_Prototype");
            ConfigureDefinition(dungeonDefinition, dungeonRoomTemplateCatalog, dungeonLayoutProfile);
            dungeonDefinition.ObjectDefinitions.Clear();
            dungeonDefinition.ObjectDefinitions.Add(obstacleDungeonObjectDefinition);
            dungeonDefinition.ObjectDefinitions.Add(obstacleDungeonObjectDefinition2);
            dungeonDefinition.ObjectDefinitions.Add(obstacleDungeonObjectDefinition3);
            dungeonDefinition.ObjectDefinitions.Add(ruinedPillar);
            dungeonDefinition.ObjectDefinitions.Add(gravestone);
            dungeonDefinition.ObjectDefinitions.Add(campfire);
            dungeonDefinition.ObjectDefinitions.Add(crate);
            dungeonDefinition.ObjectDefinitions.Add(bedroll);
            Tile ground = Asset<Tile>("Tile_Forest_Prototype");
            ground.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/ForestPrototype/forest-floor-v1.png");
            if (ground.sprite == null)
            {
                throw new InvalidOperationException("Missing prototype ground sprite.");
            }
            ground.transform = DungeonVegetationPresentation.CenteredGroundTransform(ground.sprite);
            ground.colliderType = Tile.ColliderType.None;
            DungeonVegetationPresentation presentation = Asset<DungeonVegetationPresentation>("DVP_Forest_Prototype");
            Set(presentation, "SoilSprite", p => p.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/ArungardForgePrototype/dirt-128.png"));
            Set(presentation, "GroundCover", p => p.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/ForestPrototype/grass-32ppu.png"));
            Set(presentation, "DryShrub", p => p.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/ForestPrototype/forest-dry-shrub-v1-32ppu.png"));
            Set(presentation, "LowShrub", p => p.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/ForestPrototype/shrub-32ppu.png"));
            Set(presentation, "Deadwood", p => p.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/ForestPrototype/forest-deadwood-v1-32ppu.png"));
            Set(presentation, "ExposedRoots", p => p.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/ForestPrototype/forest-roots-v1-32ppu.png"));
            Set(presentation, "WoodlandFungi", p => p.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/ForestPrototype/forest-fungi-v1-32ppu.png"));
            Set(presentation, "WoodlandFern", p => p.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/ForestPrototype/forest-fern-v1-32ppu.png"));
            Set(presentation, "FallenLog", p => p.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/ForestPrototype/forest-fallen-log-v1-32ppu.png"));
            Set(presentation, "SmallStone", p => p.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/ForestPrototype/forest-small-stone-32ppu.png"));
            Set(presentation, "WoodlandFungiVariants", p =>
            {
                p.arraySize = 1;
                p.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/ForestPrototype/forest-fungi-shelf-v1-32ppu.png");
            });
            Set(presentation, "WoodlandFernVariants", p =>
            {
                p.arraySize = 1;
                p.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/ForestPrototype/forest-fern-broad-v1-32ppu.png");
            });
            Set(presentation, "ExposedRootsVariants", p =>
            {
                p.arraySize = 1;
                p.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/ForestPrototype/forest-roots-tangled-v1-32ppu.png");
            });
            Set(presentation, "DeadwoodVariants", p =>
            {
                p.arraySize = 1;
                p.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/ForestPrototype/forest-deadwood-forked-v1-32ppu.png");
            });
            Set(presentation, "FallenLogVariants", p =>
            {
                p.arraySize = 1;
                p.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/ForestPrototype/forest-log-hollow-v1-32ppu.png");
            });
            Set(presentation, "SmallStoneVariants", p =>
            {
                p.arraySize = 1;
                p.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/ForestPrototype/forest-stone-angular-v1-32ppu.png");
            });
            Set(presentation, "LowShrubVariants", p =>
            {
                p.arraySize = 1;
                p.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/ForestPrototype/forest-shrub-bramble-v1-32ppu.png");
            });
            Set(presentation, "GroundCoverVariants", p =>
            {
                p.arraySize = 1;
                p.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/ForestPrototype/forest-grass-sedge-v1-32ppu.png");
            });
            Set(presentation, "GroundTile", p => p.objectReferenceValue = ground);
            Set(presentation, "TreeVisualOffsetInCells", p => p.vector2Value = Vector2.zero);
            Set(presentation, "BoundaryTree", p => p.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/ForestPrototype/forest-tree-mature-oak-v2-32ppu.png"));
            Set(presentation, "GroundTint", p => p.colorValue = new Color(0.78f, 0.8f, 0.7f));
            Set(presentation, "TreeVariants", delegate(SerializedProperty p)
            {
                p.arraySize = 6;
                p.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/ForestPrototype/forest-tree-mature-elm-v2-32ppu.png");
                p.GetArrayElementAtIndex(1).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/ForestPrototype/forest-tree-mature-spruce-v2-32ppu.png");
                p.GetArrayElementAtIndex(2).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/ForestPrototype/forest-tree-mature-hollow-v2-32ppu.png");
                p.GetArrayElementAtIndex(3).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/ForestPrototype/forest-tree-mature-oak-v2-32ppu.png");
                p.GetArrayElementAtIndex(4).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/ForestPrototype/forest-tree-mature-hollow-v2-32ppu.png");
                p.GetArrayElementAtIndex(5).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/ForestPrototype/forest-tree-mature-ancient-v2-32ppu.png");
            });
            Set(presentation, "TreeTint", p => p.colorValue = new Color(0.48f, 0.52f, 0.45f));
            Set(presentation, "SmallTreeVariants", p =>
            {
                p.arraySize = 1;
                p.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(ForestMatureTreesSetup.CompactTreePath);
            });
            Set(dungeonDefinition, "VegetationPresentation", p => p.objectReferenceValue = presentation);
            GameContentCatalog gameContentCatalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>("Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
            if (gameContentCatalog == null)
            {
                throw new InvalidOperationException("Missing main content catalog.");
            }
            if (!gameContentCatalog.DungeonDefinitions.Contains(dungeonDefinition))
            {
                gameContentCatalog.DungeonDefinitions.Add(dungeonDefinition);
            }
            ForestEnemiesSetup.WireIfAuthored(dungeonDefinition, dungeonRoomTemplateCatalog, gameContentCatalog);
            ForestContainersSetup.WireIfAuthored(dungeonDefinition);
            ForestExplorationObjectsSetup.WireIfAuthored(dungeonDefinition);
            ScriptableObject[] array2 = new ScriptableObject[7] { dungeonRoomTemplateCatalog, dungeonLayoutProfile, dungeonDefinition, gameContentCatalog, ground, presentation, obstacleDungeonObjectDefinition };
            foreach (ScriptableObject dirty in array2)
            {
                EditorUtility.SetDirty(dirty);
            }
            AssetDatabase.SaveAssets();
            ForestExpeditionsSetup.RefreshIfAuthored();
            Debug.Log("Forest prototype authored: " + dungeonRoomTemplateCatalog.Templates.Count + " glades and paths; offers disabled until content is ready.");
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Dungeons;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    public static partial class BiomeRoomProductionSetup
    {
        private static readonly string TerrainRequest=Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../LocalHandoffs/biome-terrain-20261023/apply.request"));

        // Narrow composition update: terrain, safe rotations and explicitly
        // repositioned furniture. Entrances and encounter budgets stay intact.
        public static void ApplyTerrainOnly()
        {
            RegisterAuthoredRooms();
            var specs=JsonUtility.FromJson<Specs>(AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/_Project/Scripts/Editor/BiomeRoomProductionBlueprints.json").text).rooms;
            var rooms=AssetDatabase.FindAssets("t:DungeonRoomTemplateCatalog")
                .Select(g=>AssetDatabase.LoadAssetAtPath<DungeonRoomTemplateCatalog>(AssetDatabase.GUIDToAssetPath(g)))
                .SelectMany(c=>c.ProductionTemplates).Where(r=>r!=null).Distinct();
            foreach(var room in rooms)
            {
                var spec=specs.SingleOrDefault(s=>room.Id=="room."+s.biome+".production."+s.id);
                if(spec==null)continue;
                // Blueprint rooms start from the same four authored candidates
                // as a full rebuild, then retain those safe for this composition.
                room.AllowedRotationDegrees.Clear();room.AllowedRotationDegrees.AddRange(new[]{0,90,180,270});
                if(spec.repositionFurniture)
                    for(var index=0;index<spec.props.Length;index++)
                    {
                        var anchorIndex=room.SemanticAnchors.FindIndex(a=>a.Id=="furniture."+index&&a.Type==DungeonSemanticAnchorType.Obstacle);
                        if(anchorIndex<0)throw new InvalidOperationException(room.Id+": missing furniture "+index);
                        var a=room.SemanticAnchors[anchorIndex];
                        room.SemanticAnchors[anchorIndex]=new DungeonSemanticAnchorData(a.Id,a.Type,spec.props[index].Cell,a.AreaSize,
                            a.SpawnChance,a.IsRequired,a.RequiresAnchorId,a.ActivationGroupId,a.EncounterEnemyIndex,a.ObjectDefinition,a.DistributionKey,a.PatrolMode);
                    }
                ConfigureTerrain(room,spec);
                var report=DungeonRoomTemplateEditorValidator.Validate(room);
                if(!report.IsValid)throw new InvalidOperationException(room.Id+": "+string.Join("; ",report.Errors));
                EditorUtility.SetDirty(room);AssetDatabase.SaveAssetIfDirty(room);
            }
        }

        // Hand-authored sources are never blueprint rebuild targets. A production
        // copy can constrain rotations to the actual world-oriented prop footprint.
        public static void RegisterAuthoredRooms()
        {
            foreach(var guid in AssetDatabase.FindAssets("t:DungeonRoomTemplateCatalog"))
            {
                var catalog=AssetDatabase.LoadAssetAtPath<DungeonRoomTemplateCatalog>(AssetDatabase.GUIDToAssetPath(guid));
                if(catalog.Id!="dungeon.catacombs.templates.production"&&catalog.Id!="rooms.catacombs.skeleton_slayer")continue;
                if(AppendAuthoredRooms(catalog,"catacombs"))
                {EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssetIfDirty(catalog);}
            }
        }

        private static bool AppendAuthoredRooms(DungeonRoomTemplateCatalog catalog,string biome)
        {
            var specs=JsonUtility.FromJson<Specs>(AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/_Project/Scripts/Editor/BiomeRoomProductionBlueprints.json").text);
            var changed=false;
            foreach(var entry in specs.authoredRooms??Array.Empty<AuthoredRoom>())
            {
                if(entry.biome!=biome)continue;
                var source=AssetDatabase.LoadAssetAtPath<DungeonRoomTemplateDefinition>(entry.path)
                    ??throw new InvalidOperationException("Missing hand-authored room: "+entry.path);
                var room=source;
                if(!string.IsNullOrEmpty(entry.productionPath))
                {
                    room=AssetDatabase.LoadAssetAtPath<DungeonRoomTemplateDefinition>(entry.productionPath);
                    if(room==null){room=UnityEngine.Object.Instantiate(source);AssetDatabase.CreateAsset(room,entry.productionPath);}
                    else EditorUtility.CopySerialized(source,room);
                    room.name=Path.GetFileNameWithoutExtension(entry.productionPath);
                    if(!string.IsNullOrEmpty(entry.function))Set(room,"RoomFunction",p=>p.stringValue=entry.function);
                    LimitUnsafeFurnitureRotations(room);
                    EditorUtility.SetDirty(room);AssetDatabase.SaveAssetIfDirty(room);
                    changed|=catalog.ProductionTemplates.Remove(source);
                }
                var report=DungeonRoomTemplateEditorValidator.Validate(room);
                if(!report.IsValid)throw new InvalidOperationException(room.Id+": "+string.Join("; ",report.Errors));
                if(catalog.ProductionTemplates.Contains(room))continue;
                if(catalog.AllTemplates.Any(r=>r!=null&&r!=room&&r.Id==room.Id))throw new InvalidOperationException("Duplicate room ID: "+room.Id);
                catalog.ProductionTemplates.Add(room);changed=true;
            }
            return changed;
        }

        private static void LimitUnsafeFurnitureRotations(DungeonRoomTemplateDefinition room)
        {
            var rotations=room.AllowedRotationDegrees.Where(rotation=>
            {
                Vector2Int Transform(Vector2Int c)=>DungeonRoomTransformUtility.TransformCell(c,room.SizeInCells,rotation);
                var floor=room.Footprint.Enumerate(room.SizeInCells).Select(Transform).ToHashSet();
                var props=room.SemanticAnchors.Where(a=>a.ObjectDefinition is ObstacleDungeonObjectDefinition p&&p.BlocksMovement)
                    .SelectMany(a=>((ObstacleDungeonObjectDefinition)a.ObjectDefinition).EnumerateOccupiedCells(Transform(a.LocalCell))).ToHashSet();
                if(!props.All(floor.Contains))return false;
                foreach(var anchor in room.StructuralAnchors.Where(a=>a.Type==DungeonStructuralAnchorType.Wall&&a.WallType==DungeonWallType.Low))
                    if(props.Contains(Transform(anchor.LocalCell))||props.Contains(Transform(anchor.LocalCell+anchor.GetDirectionOffset())))return false;
                return true;
            }).ToArray();
            if(rotations.Length==0)throw new InvalidOperationException(room.Id+": no safe furniture rotation.");
            room.AllowedRotationDegrees.Clear();room.AllowedRotationDegrees.AddRange(rotations);
        }

        private static void ConfigureTerrain(DungeonRoomTemplateDefinition room,Spec spec)
        {
            var cells=room.Footprint.Enumerate(room.SizeInCells).ToHashSet();
            room.DifficultTerrainCells.Clear();
            room.StructuralAnchors.RemoveAll(a=>a.Type==DungeonStructuralAnchorType.Wall && a.WallType==DungeonWallType.Low);
            foreach(var point in spec.difficult??Array.Empty<Point>())
            {
                if(!cells.Contains(point.Cell))throw new InvalidOperationException(room.Id+": terrain outside floor.");
                room.DifficultTerrainCells.Add(point.Cell);
            }
            var edges=new HashSet<DungeonCellEdge>();
            foreach(var point in spec.lowWalls??Array.Empty<Port>())
            {
                var direction=point.direction=="U"?DungeonEdgeDirection.Up:DungeonEdgeDirection.Right;
                var anchor=new DungeonStructuralAnchorData(DungeonStructuralAnchorType.Wall,point.Cell,direction,DungeonWallType.Low);
                var other=point.Cell+anchor.GetDirectionOffset();
                if(!cells.Contains(point.Cell)||!cells.Contains(other))throw new InvalidOperationException(room.Id+": low wall lacks floor on both sides.");
                var edge=new DungeonCellEdge(point.Cell,other);
                if(!edges.Add(edge))throw new InvalidOperationException(room.Id+": duplicate low wall.");
                room.StructuralAnchors.Add(anchor);
            }
            // Walking around every low divider must remain possible, even if
            // crossing it were forbidden. Do not put a toll across a bottleneck.
            if(cells.Count==0)return;
            var reached=new HashSet<Vector2Int>();var queue=new Queue<Vector2Int>();queue.Enqueue(cells.First());
            var directions=new[]{Vector2Int.up,Vector2Int.right,Vector2Int.down,Vector2Int.left};
            while(queue.Count>0)
            {
                var cell=queue.Dequeue();if(!reached.Add(cell))continue;
                foreach(var d in directions)
                {
                    var other=cell+d;
                    if(cells.Contains(other)&&!reached.Contains(other)&&!edges.Contains(new DungeonCellEdge(cell,other)))queue.Enqueue(other);
                }
            }
            if(reached.Count!=cells.Count)throw new InvalidOperationException(room.Id+": low divider cuts off a route.");
            LimitUnsafeFurnitureRotations(room);
        }
    }
}

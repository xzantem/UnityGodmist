using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using Godmist.Data.World;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    /// <summary>Explicit, idempotent content import. Never reauthors enemy/loot/length balance.</summary>
    [InitializeOnLoad]
    public static class ForestExplorationObjectsSetup
    {
        public const string Root=ForestPrototypeSetup.Root;
        public const string ArtRoot="Assets/Resources/Godmist/ForestArt/Exploration/";
        public static readonly string[] TrapFiles={"DO_Forest_Trap_Snare","DO_Forest_Trap_Poison","DO_Forest_Trap_Roots"};
        static readonly Vector2Int[] Directions={Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right};
        static readonly string Request=Path.GetFullPath(Path.Combine(Application.dataPath,"../../LocalHandoffs/forest-exploration-objects-20261020/import.request"));
        static ForestExplorationObjectsSetup()=>EditorApplication.update+=Tick;
        static void Tick()
        {
            if(!File.Exists(Request)||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            File.Delete(Request);
            try { Configure();File.WriteAllText(Path.ChangeExtension(Request,".state.txt"),"PASS "+DateTime.UtcNow.ToString("O")); }
            catch(Exception e){File.WriteAllText(Path.ChangeExtension(Request,".state.txt"),"ERROR "+e);Debug.LogException(e);}
        }
        [MenuItem("Godmist/Content/Forest/Import exploration objects and progression")]
        public static void Configure()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode before importing content.");
            ImportArt();
            var native="Assets/_Project/ScriptableObjects/Dungeons/Objects/";
            Clone<SpikeTrapDungeonObjectDefinition>(native+"DO_Trap_Spikes.asset",TrapFiles[0],"object.forest.trap.snare","Wnyki kłusownika");
            Clone<SpikeTrapDungeonObjectDefinition>(native+"DO_Trap_Gas.asset",TrapFiles[1],"object.forest.trap.poison","Trujące purchawki");
            Clone<SpikeTrapDungeonObjectDefinition>(native+"DO_Trap_GraveSeal.asset",TrapFiles[2],"object.forest.trap.roots","Skażone korzenie");
            var lever=Clone<LeverDungeonObjectDefinition>(native+"DO_Lever_Debug.asset","DO_Forest_Lever","object.forest.lever","Dźwignia wyciągarki");
            Set(lever,"IsReversible",p=>p.boolValue=false);
            Clone<ConditionDungeonObjectDefinition>(native+"DO_Condition_Debug.asset","DO_Forest_Condition","object.forest.condition","Skażony znak");
            var prototype=AssetDatabase.LoadAssetAtPath<DungeonDefinition>(ForestPrototypeSetup.DefinitionPath);
            WireIfAuthored(prototype);
            var catalog=AssetDatabase.LoadAssetAtPath<GameContentCatalog>(ForestExpeditionsSetup.CatalogPath);
            foreach(var dungeon in catalog.DungeonDefinitions.Where(d=>d!=null))
            {
                if(dungeon.Theme==DungeonTheme.Forest)WireIfAuthored(dungeon);
                if(dungeon.Theme!=DungeonTheme.Forest && dungeon.Theme!=DungeonTheme.Catacombs)continue;
                foreach(var chest in dungeon.ObjectDefinitions.OfType<ChestDungeonObjectDefinition>())
                {
                    Set(chest,"LockType",p=>p.enumValueIndex=(int)ChestLockType.Unlocked);
                    Set(chest,"RequiredKey",p=>p.objectReferenceValue=null);
                }
            }
            AssetDatabase.SaveAssets();
        }
        static T Clone<T>(string source,string file,string id,string label) where T:ScriptableObject
        {
            var src=AssetDatabase.LoadAssetAtPath<T>(source)??throw new InvalidOperationException("Missing source: "+source);
            var path=Root+file+".asset";
            var value=AssetDatabase.LoadAssetAtPath<T>(path);
            if(value==null){value=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(value,path);}
            EditorUtility.CopySerialized(src,value);value.name=file;
            Set(value,"Id",p=>p.stringValue=id);Set(value,"DisplayName",p=>p.stringValue=label);
            return value;
        }
        static void Set(UnityEngine.Object target,string field,Action<SerializedProperty> write)
        {
            var data=new SerializedObject(target);
            var p=data.FindProperty("<"+field+">k__BackingField")??throw new InvalidOperationException(target.name+"."+field);
            write(p);data.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(target);
        }
        static void ImportArt()
        {
            Directory.CreateDirectory(ArtRoot);
            var sources=new[]{
                ("Assets/_Project/Art/ForestExploration/forest-poacher-snare-v1-64.png","snare",new Vector2(.5f,.5f)),
                ("Assets/_Project/Art/ForestExploration/forest-poison-puffballs-v1-64.png","poison",new Vector2(.5f,.5f)),
                ("Assets/_Project/Art/ForestExploration/forest-cursed-roots-v1-64.png","roots",new Vector2(.5f,.5f)),
                ("Assets/_Project/Resources/CatacombsAudit/catacombs-lever-v4.png","lever",new Vector2(.5f,0)),
                ("Assets/_Project/Resources/CatacombsAudit/catacombs-condition.png","condition",new Vector2(.5f,.5f)),
                ("Assets/_Project/Art/ForestPrototype/forest-fallen-log-v1-32ppu.png","barrier",new Vector2(.5f,.5f))};
            foreach(var (source,name,pivot) in sources)
            {
                var path=ArtRoot+name+".png";
                if(!File.Exists(path)||!File.ReadAllBytes(path).SequenceEqual(File.ReadAllBytes(source)))File.Copy(source,path,true);
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                importer.spritePixelsPerUnit=32;importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.alphaIsTransparency=true;
                importer.wrapMode=TextureWrapMode.Clamp;importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=128;
                var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
                settings.spriteMeshType=SpriteMeshType.FullRect;settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=pivot;
                settings.spriteBorder=name=="barrier"?new Vector4(16,0,16,0):Vector4.zero;
                importer.SetTextureSettings(settings);importer.SaveAndReimport();
            }
        }
        public static void WireIfAuthored(DungeonDefinition dungeon)
        {
            if(dungeon==null)return;
            var traps=TrapFiles.Select(f=>AssetDatabase.LoadAssetAtPath<SpikeTrapDungeonObjectDefinition>(Root+f+".asset")).ToArray();
            var lever=AssetDatabase.LoadAssetAtPath<LeverDungeonObjectDefinition>(Root+"DO_Forest_Lever.asset");
            var condition=AssetDatabase.LoadAssetAtPath<ConditionDungeonObjectDefinition>(Root+"DO_Forest_Condition.asset");
            if(lever==null||condition==null||traps.Any(t=>t==null))return;
            foreach(var obj in traps.Cast<DungeonObjectDefinition>().Concat(new DungeonObjectDefinition[]{lever,condition}))
                if(!dungeon.ObjectDefinitions.Contains(obj))dungeon.ObjectDefinitions.Add(obj);
            var catalog=dungeon.RoomTemplateCatalog;
            AddBranch(catalog,"blind_glade","winch_camp","Porzucona wyciągarka",lever,DungeonSemanticAnchorType.Lever);
            AddBranch(catalog,"overgrown_ruins","cleansing_ruins","Skażone ruiny",condition,DungeonSemanticAnchorType.Condition);
            foreach(var room in catalog.Templates)
            {
                room.SemanticAnchors.RemoveAll(a=>a.Id.StartsWith("forest.trap.",StringComparison.Ordinal));
                if(room.RoomType!=DungeonRoomType.Normal&&room.RoomType!=DungeonRoomType.Corridor)continue;
                var index=room.Id.EndsWith("roots")?2:room.Id.EndsWith("hollow")?1:room.Id.EndsWith("long_glade")?1:0;
                var cell=FreeCell(room,false);
                room.SemanticAnchors.Add(new DungeonSemanticAnchorData("forest.trap."+index,DungeonSemanticAnchorType.Trap,cell,Vector2Int.one,
                    spawnChance:room.RoomType==DungeonRoomType.Corridor?.2f:.35f,objectDefinition:traps[index],
                    distributionKey:"forest.traps."+index+(room.RoomType==DungeonRoomType.Corridor?".path":".glade")));
                EditorUtility.SetDirty(room);
            }
            Set(dungeon,"MinProgressionGateCount",p=>p.intValue=1);Set(dungeon,"MaxProgressionGateCount",p=>p.intValue=2);
            EditorUtility.SetDirty(catalog);EditorUtility.SetDirty(dungeon);
        }
        static void AddBranch(DungeonRoomTemplateCatalog catalog,string source,string suffix,string label,DungeonObjectDefinition control,DungeonSemanticAnchorType type)
        {
            var src=AssetDatabase.LoadAssetAtPath<DungeonRoomTemplateDefinition>(Root+"DRT_Forest_"+source+".asset")
                ??throw new InvalidOperationException("Missing Forest room: "+source);
            var room=Clone<DungeonRoomTemplateDefinition>(AssetDatabase.GetAssetPath(src),"DRT_Forest_"+suffix,"room.forest."+suffix,label);
            Set(room,"CanBeRequiredProgressionBranch",p=>p.boolValue=true);
            room.SemanticAnchors.Add(new DungeonSemanticAnchorData("forest.progression."+suffix,type,FreeCell(room,true),Vector2Int.one,
                spawnChance:1,isRequired:true,objectDefinition:control));
            if(!catalog.Templates.Contains(room))catalog.Templates.Add(room);
            EditorUtility.SetDirty(room);
        }
        static Vector2Int FreeCell(DungeonRoomTemplateDefinition room,bool control)
        {
            var floor=room.Footprint.FloorCellOffsets.ToHashSet();var reserved=new HashSet<Vector2Int>();
            foreach(var a in room.SemanticAnchors)
                if(a.ObjectDefinition is ObstacleDungeonObjectDefinition obstacle)reserved.UnionWith(obstacle.EnumerateOccupiedCells(a.LocalCell));
                else foreach(var p in new RectInt(a.LocalCell,a.AreaSize).allPositionsWithin)reserved.Add(p);
            var center=(Vector2)room.SizeInCells*.5f;
            var candidates=floor.Where(c=>!reserved.Contains(c)&&!room.DifficultTerrainCells.Contains(c)
                &&Directions.All(d=>floor.Contains(c+d))&&Directions.Any(d=>!reserved.Contains(c+d)))
                .OrderBy(c=>((Vector2)c-center).sqrMagnitude).ThenBy(c=>c.y).ThenBy(c=>c.x).ToArray();
            if(candidates.Length==0)throw new InvalidOperationException("No free Forest interaction anchor: "+room.Id);
            return candidates[control?Math.Min(2,candidates.Length-1):0];
        }
    }
}

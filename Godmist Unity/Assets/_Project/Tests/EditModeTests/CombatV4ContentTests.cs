using System.Collections.Generic;
using System.Linq;
using Godmist.Data.Dungeons;
using Godmist.Core.Enums;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Dungeons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
namespace Godmist.Tests.EditMode
{
 public class CombatV4ContentTests
 {
  [System.Serializable] private sealed class RoomBlueprintCatalog
  {
   public RoomBlueprint[] rooms;
  }
  [System.Serializable] private sealed class RoomBlueprint
  {
   public string suffix;
   public string[] rows;
   public string[] structuralEdges;
   public string obstacle;
  }
  const string Root="Assets/_Project/ScriptableObjects/Dungeons/";
  static readonly Vector2Int[] Directions={Vector2Int.up,Vector2Int.right,Vector2Int.down,Vector2Int.left};
  [TestCase("Ossuary")][TestCase("SarcophagusHall")]
  [TestCase("Optional_Condition_Production")][TestCase("Tactical_CryptL_Production")]
  [TestCase("Tactical_Corridor")][TestCase("Tactical_CryptL")][TestCase("Tactical_Passages")]
  public void SarcophagusFootprintsFitWithoutAnchorOverlapInEveryAllowedRotation(string suffix)
  {
   var template=AssetDatabase.LoadAssetAtPath<DungeonRoomTemplateDefinition>(Root+"RoomTemplates/DRT_Catacombs_"+suffix+".asset");
   foreach(var rotation in template.GetAllowedRotationDegrees())
   {
    Vector2Int World(Vector2Int cell)=>DungeonRoomTransformUtility.TransformCell(cell,template.SizeInCells,rotation);
    var floor=template.Footprint.Enumerate(template.SizeInCells).Select(World).ToHashSet();
    var reserved=template.SemanticAnchors.Where(a=>a.ObjectDefinition?.Id!="object.obstacle.sarcophagus")
     .Select(a=>World(a.LocalCell)).ToHashSet();
    foreach(var anchor in template.SemanticAnchors.Where(a=>a.ObjectDefinition?.Id=="object.obstacle.sarcophagus"))
    {
     var occupied=((ObstacleDungeonObjectDefinition)anchor.ObjectDefinition).EnumerateOccupiedCells(World(anchor.LocalCell)).ToArray();
     Assert.That(occupied.Length,Is.EqualTo(2));
     foreach(var cell in occupied)
     {
      Assert.That(floor.Contains(cell),Is.True,template.name+" outside floor at "+rotation+": "+cell);
      Assert.That(reserved.Add(cell),Is.True,template.name+" overlapping anchor at "+rotation+": "+cell);
     }
     foreach(var wall in template.StructuralAnchors.Where(a=>a.Type==DungeonStructuralAnchorType.Wall))
     {
      var first=World(wall.LocalCell);
      var second=first+DungeonRoomTransformUtility.GetDirectionOffset(DungeonRoomTransformUtility.TransformDirection(wall.Direction,rotation));
      Assert.That(occupied.Contains(first)&&occupied.Contains(second),Is.False,template.name+" crosses wall at "+rotation);
     }
    }
   }
  }
  [TestCase("Corridor")][TestCase("Pillars")][TestCase("CryptL")][TestCase("Ritual")][TestCase("Passages")]
  public void ScenarioUsesProductionTemplateAndRemainsTraversable(string name)
  {
   var template=AssetDatabase.LoadAssetAtPath<DungeonRoomTemplateDefinition>(Root+"RoomTemplates/DRT_Catacombs_Tactical_"+name+".asset");
   Assert.That(template,Is.Not.Null);
   var report=Godmist.Editor.DungeonRoomTemplateEditorValidator.Validate(template);
   Assert.That(report.IsValid,Is.True,string.Join("\n",report.Errors));
   var catalog=AssetDatabase.LoadAssetAtPath<DungeonRoomTemplateCatalog>(Root+"DTC_Catacombs_V1.asset");
   Assert.That(catalog.Templates,Does.Contain(template));
   var definition=AssetDatabase.LoadAssetAtPath<DungeonDefinition>(Root+"DD_Catacombs_Tactical_"+name+".asset");
   foreach(var seed in new[]{404,405,406})
   {
    var dungeon=new DungeonGeneratorService().Generate(definition,seed);
    new DungeonEncounterPlacementSystem().Place(dungeon);
    new DungeonObjectPlacementSystem().Place(dungeon);
    var repeat=new DungeonGeneratorService().Generate(definition,seed);new DungeonEncounterPlacementSystem().Place(repeat);new DungeonObjectPlacementSystem().Place(repeat);
    Assert.That(Signature(dungeon),Is.EqualTo(Signature(repeat)),"Repeatable seed");
    var floor=dungeon.Floors[0];
    Assert.That(floor.Encounters.Count,Is.GreaterThanOrEqualTo(2),"Multiple fights on the same floor");
    var rooms=floor.Rooms.Where(r=>r.TemplateId==template.Id).ToList();Assert.That(rooms.Count,Is.GreaterThanOrEqualTo(1));
    foreach(var room in rooms)
    {
     var cells=new HashSet<Vector2Int>(floor.Map.FloorCells.Where(c=>floor.Map.TryGetRoomId(c,out var id)&&id==room.RoomId));
     var obstacles=floor.Objects.OfType<ObstacleDungeonObjectInstance>().Where(o=>o.BlocksMovement).SelectMany(o=>o.EnumerateOccupiedCells()).ToHashSet();
     var traps=floor.Objects.OfType<SpikeTrapDungeonObjectInstance>().Select(o=>o.Position).ToHashSet();
     cells.ExceptWith(obstacles);cells.ExceptWith(traps);
     Assert.That(cells.Count,Is.GreaterThanOrEqualTo(12));
     var visited=new HashSet<Vector2Int>();var queue=new Queue<Vector2Int>();queue.Enqueue(cells.First());visited.Add(cells.First());
     while(queue.Count>0){var c=queue.Dequeue();foreach(var direction in Directions){var next=c+direction;if(cells.Contains(next)&&!floor.Map.HasWallBetween(c,next)&&visited.Add(next))queue.Enqueue(next);}}
     Assert.That(visited.Count,Is.EqualTo(cells.Count),"All safe cells connected: "+room.RoomId);
     foreach(var anchor in template.SemanticAnchors)
     {
      var transformed=DungeonRoomTransformUtility.TransformCell(anchor.LocalCell,template.SizeInCells,room.RotationDegrees)+room.LayoutPosition;
      var present=floor.Objects.Any(o=>o.Position==transformed&&o.Definition==anchor.ObjectDefinition);
      var group=template.ContentGroups.FirstOrDefault(g=>g.AnchorIds.Contains(anchor.Id));
      if ((group?.IsRequired??anchor.IsRequired)||(group?.SpawnChance??anchor.SpawnChance)>=1f)
       Assert.That(present,Is.True,"Required or certain anchor "+anchor.Id);
      if(group!=null)
       foreach(var member in template.SemanticAnchors.Where(a=>group.AnchorIds.Contains(a.Id)))
       {
        var memberCell=DungeonRoomTransformUtility.TransformCell(member.LocalCell,template.SizeInCells,room.RotationDegrees)+room.LayoutPosition;
        Assert.That(floor.Objects.Any(o=>o.Position==memberCell&&o.Definition==member.ObjectDefinition),Is.EqualTo(present),"Complete optional group "+group.Id);
       }
     }
    }
   }
  }
  [TestCase(404)][TestCase(405)][TestCase(406)]
  public void NormalCatacombsGenerateTacticalContent(int seed)
  {
   var definition=AssetDatabase.LoadAssetAtPath<DungeonDefinition>(Root+"DD_Catacombs_Procedural.asset");
   var dungeon=new DungeonGeneratorService().Generate(definition,seed);
   new DungeonEncounterPlacementSystem().Place(dungeon);new DungeonObjectPlacementSystem().Place(dungeon);
   Assert.That(dungeon.Floors.SelectMany(f=>f.Rooms).Any(r=>r.TemplateId.StartsWith("room.catacombs.tactical.")),Is.True);
   Assert.That(dungeon.Floors.SelectMany(f=>f.Objects).OfType<ObstacleDungeonObjectInstance>().Any(),Is.True);
   Assert.That(dungeon.Floors.SelectMany(f=>f.Objects).OfType<SpikeTrapDungeonObjectInstance>().Any(),Is.True);
  }
  [TestCase("Start")][TestCase("Normal_Small")][TestCase("Normal_Large")]
  [TestCase("L")][TestCase("Tactical_CryptL")][TestCase("Tactical_Pillars")]
  [TestCase("Tactical_Passages")][TestCase("Tactical_Ritual")]
  [TestCase("Optional")][TestCase("Optional_Lever")][TestCase("Optional_Condition")]
  [TestCase("Corridor")][TestCase("Tactical_Corridor")][TestCase("Final")]
  public void Vs14CandidateRoomIsFlatAndValid(string suffix)
  {
   var template=AssetDatabase.LoadAssetAtPath<DungeonRoomTemplateDefinition>(
    Root+"RoomTemplates/DRT_Catacombs_"+suffix+".asset");
   Assert.That(template,Is.Not.Null,suffix);
   var report=Godmist.Editor.DungeonRoomTemplateEditorValidator.Validate(template);
   Assert.That(report.IsValid,Is.True,suffix+": "+string.Join("\n",report.Errors));
   Assert.That(template.Stairs,Is.Empty,suffix+" contains stairs");
   Assert.That(template.EnumerateSurfaces().All(surface=>surface.Level==0),Is.True,
    suffix+" contains an elevated surface");
   Assert.That(template.StructuralAnchors.All(anchor=>anchor.Level==0),Is.True,
    suffix+" contains an elevated structural anchor");
  }
  [TestCase(false,14,14)][TestCase(true,18,8)]
  public void Vs14NewNormalRoomPlanValidatesWithoutSavingAssets(bool longHall,int width,int height)
  {
   var pillar=AssetDatabase.LoadAssetAtPath<DungeonObjectDefinition>(
    Root+"Objects/DO_Obstacle_Pillar.asset");
   Assert.That(pillar,Is.Not.Null);
   var room=ScriptableObject.CreateInstance<DungeonRoomTemplateDefinition>();
   try
   {
    Godmist.Editor.VS14CatacombsSetup.ConfigureNewNormalRoom(room,
     longHall?"room.catacombs.sarcophagus_hall":"room.catacombs.ossuary",
     longHall?"Sala sarkofagow":"Kostnica",new Vector2Int(width,height),longHall,pillar);
    var report=Godmist.Editor.DungeonRoomTemplateEditorValidator.Validate(room);
    Assert.That(report.IsValid,Is.True,string.Join("\n",report.Errors));
    Assert.That(room.AllowedBoundaryEdges,Is.Not.Empty);
    Assert.That(room.Stairs,Is.Empty);
    Assert.That(room.SemanticAnchors.Count(anchor=>anchor.Type==DungeonSemanticAnchorType.Enemy),Is.EqualTo(4));
   }
   finally{Object.DestroyImmediate(room);}
  }
  static IEnumerable<object[]> Vs14Seeds()
  {
   foreach(var seed in Enumerable.Range(400,10))
   {
    yield return new object[]{seed,false};
    yield return new object[]{seed,true};
   }
  }
  [Test]
  public void Vs14ProductionExpeditionSeed15KeepsValidDoorways()
  {
   var definition=AssetDatabase.LoadAssetAtPath<DungeonDefinition>(
    Root+"DD_Catacombs_Expedition.asset");
   Assert.That(definition,Is.Not.Null);
   var dungeon=new DungeonGeneratorService().Generate(definition,15);
   Assert.That(dungeon.Floors,Is.Not.Empty);
   foreach(var floor in dungeon.Floors)
   {
    Assert.That(floor.Connections,Is.Not.Empty);
    Assert.That(floor.Connections.All(connection=>connection.HasGeometry),Is.True);
    Assert.That(floor.Map,Is.Not.Null);
   }
  }
  [TestCaseSource(nameof(Vs14Seeds))]
  public void Vs14PlannedExpeditionsGenerateConnectedFloors(int seed,bool hasBoss)
  {
   var owned=new List<Object>();
   try
   {
    var pillar=AssetDatabase.LoadAssetAtPath<DungeonObjectDefinition>(
     Root+"Objects/DO_Obstacle_Pillar.asset");
    var catalog=ScriptableObject.CreateInstance<DungeonRoomTemplateCatalog>();owned.Add(catalog);
    foreach(var suffix in new[]{"Start","Normal_Small","Normal_Large","L","Tactical_CryptL",
     "Tactical_Pillars","Tactical_Passages","Tactical_Ritual","Optional","Optional_Lever",
     "Optional_Condition","Corridor","Final"})
     catalog.Templates.Add(AssetDatabase.LoadAssetAtPath<DungeonRoomTemplateDefinition>(
      Root+"RoomTemplates/DRT_Catacombs_"+suffix+".asset"));
    var ossuary=ScriptableObject.CreateInstance<DungeonRoomTemplateDefinition>();owned.Add(ossuary);
    Godmist.Editor.VS14CatacombsSetup.ConfigureNewNormalRoom(ossuary,"room.catacombs.ossuary",
     "Kostnica",new Vector2Int(14,14),false,pillar);
    var hall=ScriptableObject.CreateInstance<DungeonRoomTemplateDefinition>();owned.Add(hall);
    Godmist.Editor.VS14CatacombsSetup.ConfigureNewNormalRoom(hall,"room.catacombs.sarcophagus_hall",
     "Sala sarkofagow",new Vector2Int(18,8),true,pillar);
    var corridor=Object.Instantiate(AssetDatabase.LoadAssetAtPath<DungeonRoomTemplateDefinition>(
     Root+"RoomTemplates/DRT_Catacombs_Tactical_Corridor.asset"));owned.Add(corridor);
    Godmist.Editor.VS14CatacombsSetup.ConfigureProductionCorridor(corridor);
    catalog.Templates.AddRange(new[]{ossuary,hall,corridor});
    Assert.That(catalog.Templates.Count,Is.EqualTo(16));
    var profile=Object.Instantiate(AssetDatabase.LoadAssetAtPath<DungeonLayoutProfile>(
     Root+"LayoutProfiles/DLP_OrganicSpaces.asset"));owned.Add(profile);
    Godmist.Editor.VS14CatacombsSetup.ConfigureProductionProfile(profile);
    var definition=Object.Instantiate(AssetDatabase.LoadAssetAtPath<DungeonDefinition>(
     Root+"DD_Catacombs_Procedural.asset"));owned.Add(definition);
    Godmist.Editor.VS14CatacombsSetup.ConfigureExpeditionData(definition,
     hasBoss?"dungeon.catacombs.boss_expedition":"dungeon.catacombs.expedition",
     "Katakumby",catalog,profile,hasBoss);
    var dungeon=new DungeonGeneratorService().Generate(definition,seed);
    new DungeonEncounterPlacementSystem().Place(dungeon);
    new DungeonObjectPlacementSystem().Place(dungeon);
    Assert.That(dungeon.Floors.Count,Is.EqualTo(2));
    Assert.That(dungeon.Floors.SelectMany(floor=>floor.Objects)
     .OfType<ChestDungeonObjectInstance>().Any(),Is.True,
     "Expedition without a reachable chest, seed "+seed);
    foreach(var floor in dungeon.Floors)
    {
     Assert.That(floor.Rooms.Count,Is.InRange(11,14));
     Assert.That(floor.Encounters.Count,Is.InRange(4,7));
     var start=floor.Rooms.First(room=>room.RoomType==DungeonRoomType.Start);
     var visited=new HashSet<string>{start.RoomId};
     var queue=new Queue<string>();queue.Enqueue(start.RoomId);
     while(queue.Count>0)
     {
      var current=queue.Dequeue();
      foreach(var connection in floor.Connections)
      {
       var next=connection.FirstRoomId==current?connection.SecondRoomId:
        connection.SecondRoomId==current?connection.FirstRoomId:null;
       if(next!=null&&visited.Add(next))queue.Enqueue(next);
      }
     }
     Assert.That(visited.Count,Is.EqualTo(floor.Rooms.Count),"Disconnected room, seed "+seed);
     Assert.That(floor.Rooms.Any(room=>room.RoomType==DungeonRoomType.Final&&visited.Contains(room.RoomId)),Is.True);
     foreach(var chest in floor.Objects.OfType<ChestDungeonObjectInstance>())
     {
      Assert.That(chest.Gold,Is.GreaterThan(0),"Chest has no encounter-scaled gold, seed "+seed);
      Assert.That(floor.Map.TryGetRoomId(chest.Position,out var roomId),Is.True,
       "Chest outside room, seed "+seed);
      Assert.That(visited.Contains(roomId),Is.True,"Unreachable chest room, seed "+seed);
     }
    }
    var bossCount=dungeon.Floors.SelectMany(floor=>floor.Encounters)
     .Count(encounter=>encounter.Definition==definition.BossEncounterDefinition);
    Assert.That(bossCount,Is.EqualTo(hasBoss?1:0));
   }
   finally{foreach(var asset in owned.AsEnumerable().Reverse())Object.DestroyImmediate(asset);}
  }
  [Test] public void Vs14AuthoredCatalogAndExpeditionsAreProductionOnly()
  {
   var catalog=AssetDatabase.LoadAssetAtPath<DungeonRoomTemplateCatalog>(
    Root+"DTC_Catacombs_Production.asset");
   var regular=AssetDatabase.LoadAssetAtPath<DungeonDefinition>(
    Root+"DD_Catacombs_Expedition.asset");
   var boss=AssetDatabase.LoadAssetAtPath<DungeonDefinition>(
    Root+"DD_Catacombs_BossExpedition.asset");
   Assert.That(catalog,Is.Not.Null);
   Assert.That(regular,Is.Not.Null);
   Assert.That(boss,Is.Not.Null);
   Assert.That(catalog.Templates.Count,Is.GreaterThanOrEqualTo(16));
   Assert.That(catalog.Templates.Count(room=>room.RoomType==DungeonRoomType.Start),Is.GreaterThanOrEqualTo(1));
   Assert.That(catalog.Templates.Count(room=>room.RoomType==DungeonRoomType.Normal),Is.GreaterThanOrEqualTo(9));
   Assert.That(catalog.Templates.Count(room=>room.RoomType==DungeonRoomType.Optional),Is.GreaterThanOrEqualTo(3));
   Assert.That(catalog.Templates.Count(room=>room.RoomType==DungeonRoomType.Corridor),Is.GreaterThanOrEqualTo(2));
   Assert.That(catalog.Templates.Count(room=>room.RoomType==DungeonRoomType.Final),Is.GreaterThanOrEqualTo(1));
   Assert.That(catalog.Templates.Select(room=>room.Id).Distinct().Count(),Is.EqualTo(catalog.Templates.Count));
   Assert.That(catalog.Templates.Any(room=>room.name.Contains("Demo")||
    room.name.Contains("Stairs")||room.name.Contains("Balcony")),Is.False);
   Assert.That(regular.RoomTemplateCatalog,Is.SameAs(catalog));
   Assert.That(boss.RoomTemplateCatalog,Is.SameAs(catalog));
   Assert.That(regular.LayoutProfile,Is.SameAs(boss.LayoutProfile));
   Assert.That(regular.BossEncounterDefinition,Is.Null);
   Assert.That(boss.BossEncounterDefinition,Is.Not.Null);
   Assert.That(regular.IsAvailableAsOffer&&boss.IsAvailableAsOffer,Is.True);
   var world=AssetDatabase.LoadAssetAtPath<Godmist.Data.World.GameContentCatalog>(
    "Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
   Assert.That(world.DungeonDefinitions,Does.Contain(regular));
   Assert.That(world.DungeonDefinitions,Does.Contain(boss));
   CollectionAssert.AreEquivalent(new[]{regular,boss},world.DungeonDefinitions
    .Where(dungeon=>dungeon!=null&&dungeon.IsProcedural&&
     !dungeon.IsStoryDungeon&&dungeon.IsAvailableAsOffer));
  }
  [Test] public void Vs14ProductionRolesKeepDoorsAndRiskRewardIntact()
  {
   var catalog=AssetDatabase.LoadAssetAtPath<DungeonRoomTemplateCatalog>(
    Root+"DTC_Catacombs_Production.asset");
   Assert.That(catalog,Is.Not.Null);
   Assert.That(catalog.Templates.Where(room=>room.ExcludeFromRandomEncounters)
    .Select(room=>room.DisplayName),Is.EquivalentTo(new[]{"Skarbiec z pułapką","Odetchnienie"}));
   Assert.That(catalog.Templates.SelectMany(room=>room.StructuralAnchors)
    .Count(anchor=>anchor.WallType==DungeonWallType.Full),Is.GreaterThan(0));
   Assert.That(catalog.Templates.SelectMany(room=>room.StructuralAnchors)
    .Count(anchor=>anchor.WallType==DungeonWallType.Low),Is.GreaterThan(0));
   Assert.That(catalog.Templates.SelectMany(room=>room.StructuralAnchors)
    .Count(anchor=>anchor.WallType==DungeonWallType.Railing),Is.GreaterThan(0));
   foreach(var room in catalog.Templates)
   {
    var report=Godmist.Editor.DungeonRoomTemplateEditorValidator.Validate(room);
    Assert.That(report.IsValid,Is.True,room.name+": "+string.Join("; ",report.Errors));
    Assert.That(room.SemanticAnchors.Count(a=>a.Type==DungeonSemanticAnchorType.Enemy),
     Is.LessThanOrEqualTo(4),room.name);
    Assert.That(room.AllowedBoundaryEdges,Is.Not.Empty,room.name);
    Assert.That(room.Stairs,Is.Empty,room.name);
    var walkable=room.Footprint.Enumerate(room.SizeInCells).ToHashSet();
    walkable.ExceptWith(room.SemanticAnchors.Where(a=>a.Type==DungeonSemanticAnchorType.Obstacle)
     .SelectMany(a=>a.ObjectDefinition is ObstacleDungeonObjectDefinition obstacle
      ? obstacle.EnumerateOccupiedCells(a.LocalCell) : new[]{a.LocalCell}));
    var seen=new HashSet<Vector2Int>();
    var queue=new Queue<Vector2Int>();
    foreach(var edge in room.AllowedBoundaryEdges)
     Assert.That(walkable.Contains(edge.LocalCell),Is.True,room.name+" has a blocked door");
    var entry=room.AllowedBoundaryEdges[0].LocalCell;
    seen.Add(entry);queue.Enqueue(entry);
    while(queue.Count>0)
    {
     var cell=queue.Dequeue();
     foreach(var direction in Directions)
     {
      var next=cell+direction;
      var blocked=room.StructuralAnchors.Any(anchor=>anchor.Type==DungeonStructuralAnchorType.Wall
       &&anchor.WallType!=DungeonWallType.Low
       &&((anchor.LocalCell==cell&&anchor.LocalCell+anchor.GetDirectionOffset()==next)
        ||(anchor.LocalCell==next&&anchor.LocalCell+anchor.GetDirectionOffset()==cell)));
      if(walkable.Contains(next)&&!blocked&&seen.Add(next))queue.Enqueue(next);
     }
    }
    Assert.That(seen.Count,Is.EqualTo(walkable.Count),room.name+" has isolated floor cells");
   }
   var ring=catalog.Templates.Single(room=>room.DisplayName=="Krąg rytualny");
   Assert.That(ring.AllowedBoundaryEdges.All(edge=>edge.Direction==DungeonEdgeDirection.Left
    ||edge.Direction==DungeonEdgeDirection.Right
    ||edge.Direction==DungeonEdgeDirection.Down),Is.True);
   Assert.That(ring.Footprint.Contains(new Vector2Int(ring.SizeInCells.x/2,
    ring.SizeInCells.y/2),ring.SizeInCells),Is.False);

   var vault=catalog.Templates.Single(room=>room.DisplayName=="Skarbiec z pułapką");
   var floor=vault.Footprint.Enumerate(vault.SizeInCells).ToHashSet();
   var traps=vault.SemanticAnchors.Where(a=>a.Type==DungeonSemanticAnchorType.Trap)
    .Select(a=>a.LocalCell).ToHashSet();
   var chest=vault.SemanticAnchors.Single(a=>a.Type==DungeonSemanticAnchorType.Chest);
   Assert.That(traps.Count,Is.InRange(1,3));
   Assert.That(vault.AllowedBoundaryEdges.All(e=>e.Direction==DungeonEdgeDirection.Down),Is.True);
   Assert.That(chest.IsRequired,Is.True);
   var goals=Directions.Select(direction=>chest.LocalCell+direction).Where(floor.Contains).ToHashSet();
   bool CanReach(bool allowTraps)
   {
    var seen=new HashSet<Vector2Int>();
    var queue=new Queue<Vector2Int>();
    foreach(var edge in vault.AllowedBoundaryEdges)
     if(seen.Add(edge.LocalCell))queue.Enqueue(edge.LocalCell);
    while(queue.Count>0)
    {
     var cell=queue.Dequeue();
     if(goals.Contains(cell))return true;
     foreach(var direction in Directions)
     {
      var next=cell+direction;
      if(!floor.Contains(next)||!allowTraps&&traps.Contains(next)||!seen.Add(next))continue;
      queue.Enqueue(next);
     }
    }
    return false;
   }
   Assert.That(CanReach(true),Is.True,"Treasure must be physically reachable");
   Assert.That(CanReach(false),Is.False,"A door cannot bypass the trapped neck");
  }
  [Test] public void Vs14SavedFootprintsMatchConceptLayouts()
  {
   var source=AssetDatabase.LoadAssetAtPath<TextAsset>(
    "Assets/_Project/Scripts/Editor/VS14RoomBlueprints.json");
   Assert.That(source,Is.Not.Null);
   var blueprints=JsonUtility.FromJson<RoomBlueprintCatalog>(source.text).rooms;
   foreach(var blueprint in blueprints)
   {
    var existing=blueprint.suffix=="Ossuary"||blueprint.suffix=="SarcophagusHall"
     ||blueprint.suffix=="Tactical_Corridor_Production";
    var path=Root+"RoomTemplates/DRT_Catacombs_"+blueprint.suffix
     +(existing?"":"_Production")+".asset";
    var room=AssetDatabase.LoadAssetAtPath<DungeonRoomTemplateDefinition>(path);
    Assert.That(room,Is.Not.Null,path);
    if(new[]{"Normal_Small","Normal_Large","Tactical_CryptL","Tactical_Pillars",
     "Tactical_Passages","Tactical_Ritual","Ossuary","SarcophagusHall",
     "Optional_Lever","Optional_Condition","Final"}.Contains(blueprint.suffix))
     Assert.That(room.SemanticAnchors.Count(anchor=>anchor.Type==DungeonSemanticAnchorType.Enemy),
      Is.EqualTo(4),room.name+" must author all four enemy positions");
    Assert.That(room.StructuralAnchors.Count(anchor=>anchor.Type==DungeonStructuralAnchorType.Wall),
     Is.EqualTo(blueprint.structuralEdges?.Length??0),room.name+" structural edges");
    Assert.That(blueprint.rows.Select(row=>row.Length).Distinct().Count(),Is.EqualTo(1),
     blueprint.suffix+" blueprint rows must have matching widths and valid UTF-8 symbols");
    var sourceRows=blueprint.rows.Select(row=>row.ToCharArray()).ToArray();
    var expandedRows=new List<char[]>();
    for(var y=0;y<sourceRows.Length;y++)
    {
     var row=new List<char>();
     for(var x=0;x<sourceRows[y].Length;x++)
     {
      row.Add(sourceRows[y][x]);
      if(x==2||x==sourceRows[y].Length-3)
       row.Add(("EXWSBC".Contains(sourceRows[y][x]) || blueprint.obstacle=="Sarcophagus"&&sourceRows[y][x]=='■')?'.':sourceRows[y][x]);
     }
     expandedRows.Add(row.ToArray());
     if(y==2||y==sourceRows.Length-3)
      expandedRows.Add(row.Select(c=>("EXWSBC".Contains(c) || blueprint.obstacle=="Sarcophagus"&&c=='■')?'.':c).ToArray());
    }
    Assert.That(room.SizeInCells,
     Is.EqualTo(new Vector2Int(expandedRows[0].Length,expandedRows.Count)),room.name);
    var actual=room.Footprint.Enumerate(room.SizeInCells).ToHashSet();
    var extra=0;
    for(var y=0;y<expandedRows.Count;y++)
    for(var x=0;x<expandedRows[y].Length;x++)
    {
     var c=expandedRows[y][x];
     var cell=new Vector2Int(x,expandedRows.Count-1-y);
     if(c!='#'&&c!='·')
      Assert.That(actual.Contains(cell),Is.True,room.name+" missing concept cell "+cell);
     else if(actual.Contains(cell))
     {
      Assert.That(c,Is.EqualTo('#'),room.name+" opened a concept gap "+cell);
      extra++;
     }
     DungeonSemanticAnchorType? expectedAnchor=c switch
     {
      '■'=>DungeonSemanticAnchorType.Obstacle,
      '!'=>DungeonSemanticAnchorType.Trap,
      'W' or 'B'=>DungeonSemanticAnchorType.Enemy,
      'C'=>DungeonSemanticAnchorType.Chest,
      'S'=>room.RoomType==DungeonRoomType.Optional
       ? blueprint.suffix=="Optional"?DungeonSemanticAnchorType.Chest
        : blueprint.suffix=="Optional_Lever"?DungeonSemanticAnchorType.Lever
        : DungeonSemanticAnchorType.Condition
       : null,
      _=>null
     };
     // Door authoring moves this trap beyond both interaction cells.
     var anchorCell=blueprint.suffix=="Tactical_Corridor_Production"
      &&c=='!'&&cell==new Vector2Int(6,4)?new Vector2Int(6,5):cell;
     if(expectedAnchor.HasValue)
      Assert.That(room.SemanticAnchors.Any(anchor=>anchor.LocalCell==anchorCell
       &&anchor.Type==expectedAnchor.Value),Is.True,
       room.name+" missing concept object "+c+" at "+anchorCell);
    }
    Assert.That(extra,Is.LessThanOrEqualTo(16),room.name+" alters too much wall geometry");
   }
  }
  [TestCase(400,false)][TestCase(401,false)][TestCase(402,false)]
  [TestCase(403,false)][TestCase(404,false)][TestCase(405,false)]
  [TestCase(400,true)][TestCase(401,true)][TestCase(402,true)]
  [TestCase(403,true)][TestCase(404,true)][TestCase(405,true)]
  public void Vs14ProductionExpeditionsPlaceAuthoredRoomsAndObjects(int seed,bool hasBoss)
  {
   var definition=AssetDatabase.LoadAssetAtPath<DungeonDefinition>(Root+
    (hasBoss?"DD_Catacombs_BossExpedition.asset":"DD_Catacombs_Expedition.asset"));
   var dungeon=new DungeonGeneratorService().Generate(definition,seed);
   new DungeonEncounterPlacementSystem().Place(dungeon);
   new DungeonObjectPlacementSystem().Place(dungeon);
   Assert.That(dungeon.Floors.Count,Is.EqualTo(2));
   var templates=definition.RoomTemplateCatalog.Templates.ToDictionary(room=>room.Id);
   foreach(var floor in dungeon.Floors)
   {
    Assert.That(floor.Rooms.Count,Is.InRange(11,14));
    var reached=new HashSet<string>();
    var queue=new Queue<string>();
    var start=floor.Rooms.Single(room=>room.RoomType==DungeonRoomType.Start);
    queue.Enqueue(start.RoomId);reached.Add(start.RoomId);
    while(queue.Count>0)
    {
     var current=queue.Dequeue();
     foreach(var connection in floor.Connections)
     {
      var next=connection.FirstRoomId==current?connection.SecondRoomId:
       connection.SecondRoomId==current?connection.FirstRoomId:null;
      if(next!=null&&reached.Add(next))queue.Enqueue(next);
     }
    }
    Assert.That(reached.Count,Is.EqualTo(floor.Rooms.Count),"Disconnected floor, seed "+seed);
   foreach(var room in floor.Rooms)
   {
    var template=templates[room.TemplateId];
    if(template.ExcludeFromRandomEncounters)
     Assert.That(floor.Encounters.Any(encounter=>encounter.RoomId==room.RoomId),Is.False,
      "Non-combat room contains an encounter: "+template.name);
     foreach(var anchor in template.StructuralAnchors.Where(a=>a.Type==DungeonStructuralAnchorType.Wall))
     {
      var first=DungeonRoomTransformUtility.TransformCell(anchor.LocalCell,
       template.SizeInCells,room.RotationDegrees)+room.LayoutPosition;
      var direction=DungeonRoomTransformUtility.TransformDirection(anchor.Direction,
       room.RotationDegrees);
      var second=first+DungeonRoomTransformUtility.GetDirectionOffset(direction);
      Assert.That(floor.Map.HasWallBetween(first,second),Is.True,
       "Missing structural wall in "+template.name+", seed "+seed);
      Assert.That(floor.Map.GetWallType(new DungeonCellEdge(first,second)),
       Is.EqualTo(anchor.WallType),template.name+", seed "+seed);
     }
     foreach(var anchor in template.SemanticAnchors.Where(a=>a.IsRequired&&a.ObjectDefinition!=null))
     {
      var position=DungeonRoomTransformUtility.TransformCell(anchor.LocalCell,
       template.SizeInCells,room.RotationDegrees)+room.LayoutPosition;
      Assert.That(floor.Objects.Any(o=>o.Position==position&&o.Definition==anchor.ObjectDefinition),
       Is.True,"Required "+anchor.Id+" missing in "+template.name+", seed "+seed);
     }
    }
   }
   var bossCount=dungeon.Floors.SelectMany(floor=>floor.Encounters)
    .Count(encounter=>encounter.Definition==definition.BossEncounterDefinition);
   Assert.That(bossCount,Is.EqualTo(hasBoss?1:0));
  }
  [Test] public void Vs14ProductionFloorsDoNotStretchIntoLongStrips()
  {
   var definition=AssetDatabase.LoadAssetAtPath<DungeonDefinition>(
    Root+"DD_Catacombs_Expedition.asset");
   Assert.That(definition.LayoutProfile.PreferCompactMainPath,Is.True);
   var templates=definition.RoomTemplateCatalog.Templates.ToDictionary(room=>room.Id);
   double aspect=0,occupancy=0,span=0,turns=0;
   var floorCount=0;
   foreach(var seed in new[]{400,401,402})
   foreach(var floor in new DungeonGeneratorService().Generate(definition,seed).Floors)
   {
    var rectangles=floor.Rooms.ToDictionary(room=>room.RoomId,room=>new RectInt(
     room.LayoutPosition,DungeonRoomTransformUtility.GetRotatedSize(
      templates[room.TemplateId].SizeInCells,room.RotationDegrees)));
    var values=rectangles.Values.ToArray();
    var width=values.Max(rect=>rect.xMax)-values.Min(rect=>rect.xMin);
    var height=values.Max(rect=>rect.yMax)-values.Min(rect=>rect.yMin);
    aspect+=(double)System.Math.Max(width,height)/System.Math.Min(width,height);
    occupancy+=values.Sum(rect=>rect.width*rect.height)/(double)(width*height);
    span+=System.Math.Max(width,height);
    var main=floor.Rooms.Where(room=>room.RoomId.Contains(".room."))
     .OrderBy(room=>room.RoomId).ToArray();
    var directions=new List<int>();
    for(var i=1;i<main.Length;i++)
    {
     var previous=rectangles[main[i-1].RoomId];
     var next=rectangles[main[i].RoomId];
     directions.Add(next.xMin>=previous.xMax?0:next.xMax<=previous.xMin?1:
      next.yMin>=previous.yMax?2:3);
    }
    for(var i=1;i<directions.Count;i++)
     if(directions[i]!=directions[i-1])turns++;
    floorCount++;
   }
   Assert.That(aspect/floorCount,Is.LessThan(1.7));
   Assert.That(occupancy/floorCount,Is.GreaterThan(0.4));
   Assert.That(span/floorCount,Is.LessThan(90));
   Assert.That(turns/floorCount,Is.GreaterThan(2.5));
  }
  [Test] public void PinnedDefinitionOutsidePoolFailsInsteadOfSubstituting()
  {
   var original=AssetDatabase.LoadAssetAtPath<DungeonDefinition>(Root+"DD_Catacombs_Tactical_Pillars.asset");
   var definition=Object.Instantiate(original);
   try {
    definition.ObjectDefinitions.RemoveAll(o=>o.name=="DO_Obstacle_Pillar");
    var dungeon=new DungeonGeneratorService().Generate(definition,404);
    Assert.Throws<System.InvalidOperationException>(()=>new DungeonObjectPlacementSystem().Place(dungeon));
   }finally{Object.DestroyImmediate(definition);}
  }
  static string Signature(DungeonInstance d)=>string.Join("|",d.Floors.SelectMany(f=>f.Objects).Select(o=>o.Definition.Id+":"+o.Position));
 }
}

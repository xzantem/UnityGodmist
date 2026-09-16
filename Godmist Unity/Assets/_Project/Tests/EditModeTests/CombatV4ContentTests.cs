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
  const string Root="Assets/_Project/ScriptableObjects/Dungeons/";
  static readonly Vector2Int[] Directions={Vector2Int.up,Vector2Int.right,Vector2Int.down,Vector2Int.left};
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
     var obstacles=floor.Objects.OfType<ObstacleDungeonObjectInstance>().Where(o=>o.BlocksMovement).Select(o=>o.Position).ToHashSet();
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

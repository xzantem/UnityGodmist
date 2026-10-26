using System.Linq;
using System.Reflection;
using Godmist.Data.Dungeons;
using Godmist.Editor;
using Godmist.Systems.Dungeons;
using Godmist.Views.Dungeons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
namespace Godmist.Tests.EditMode
{
    public class EntArenaDecorationTests
    {
        [Test]
        public void ArenaAccentsAreNativeScaleGroundArtWithoutNavigationChangesOrRebuildDuplicates()
        {
            var definition = AssetDatabase.LoadAssetAtPath<DungeonDefinition>(ForestPrototypeSetup.DefinitionPath);
            var dungeon = new DungeonGeneratorService().Generate(definition,42);
            var floor = dungeon.Floors[0]; var root = new GameObject("Ent arena ground");
            try
            {
                var view = root.AddComponent<DungeonFloorMapView>();
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                void Set(string field,object value) => typeof(DungeonFloorMapView).GetField(field,flags).SetValue(view,value);
                Set("boundMap",floor.Map); Set("vegetationPresentation",definition.VegetationPresentation);
                view.ConfigureForestPresentationFloor(floor);
                Assert.That(view.FogFloor,Is.Null,"Presentation must work before the first fog pass.");
                var build = typeof(DungeonFloorMapView).GetMethod("BuildEntArenaDetails",flags);
                var revision = floor.Map.TraversalRevision;
                build.Invoke(view,null);
                var accents = root.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.name.StartsWith("Ent arena")).ToArray();
                Assert.That(accents.Length,Is.GreaterThan(0)); Assert.That(accents.Length,Is.LessThanOrEqualTo(20));
                foreach(var accent in accents)
                { Assert.That(accent.sprite.pixelsPerUnit,Is.EqualTo(32)); Assert.That(accent.transform.localScale,Is.EqualTo(Vector3.one)); }
                Assert.That(root.GetComponentsInChildren<Collider2D>(),Is.Empty);
                Assert.That(floor.Map.TraversalRevision,Is.EqualTo(revision));
                var exitPrefab=AssetDatabase.LoadAssetAtPath<DungeonExitView>("Assets/_Project/Prefabs/Dungeon/PF_DungeonExitView.prefab");
                var exit=Object.Instantiate(exitPrefab,root.transform);
                var colliderCount=exit.GetComponentsInChildren<Collider2D>().Length;
                var gateway = view.ResolveForestTransitionPosition(floor,Vector2.zero,Godmist.Core.Enums.DungeonRoomType.Final);
                var cell = floor.Map.FloorCells.OrderBy(c=>((Vector2)c-gateway).sqrMagnitude).First();
                Assert.That(floor.Map.IsWalkable(cell+Vector2Int.up) != floor.Map.IsWalkable(cell+Vector2Int.down),Is.True);
                exit.transform.position=gateway;
                view.StyleForestTransition(exit);
                var gatewayTrees=exit.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.name=="Forest gateway tree").ToArray();
                Assert.That(gatewayTrees.All(r=>r.sprite.pixelsPerUnit==32 && r.transform.localScale==Vector3.one),Is.True);
                Assert.That(exit.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.name!="Forest gateway tree").All(r=>!r.enabled),Is.True);
                Assert.That(exit.GetComponentsInChildren<Collider2D>().Length,Is.EqualTo(colliderCount));
                Assert.That(exit.GetComponentsInChildren<SpriteRenderer>().Count(r=>r.name=="Forest gateway tree"),Is.EqualTo(2));
                Object.DestroyImmediate(exit.gameObject);
                typeof(DungeonFloorMapView).GetMethod("ClearPresentationDecorations",flags).Invoke(view,null);
                build.Invoke(view,null);
                Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Length,Is.EqualTo(accents.Length));
            }
            finally{Object.DestroyImmediate(root);}
        }
    }
}

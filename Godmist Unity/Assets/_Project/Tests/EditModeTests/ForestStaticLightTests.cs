using System.Linq;
using System.Reflection;
using Godmist.Data.Dungeons;
using Godmist.Editor;
using Godmist.Systems.Dungeons;
using Godmist.Views.Dungeons;
using Godmist.Views.Lighting;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public class ForestStaticLightTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        static void Set(object obj,string field,object value) => obj.GetType().GetField(field,Flags).SetValue(obj,value);
        [Test]
        public void ForestFixtureKeepsGroundDepthWhenAnOverlappingCrownChangesDepth()
        {
            var root=new GameObject("Forest fixture canopy regression");
            try
            {
                var terrain=root.AddComponent<DungeonFloorMapView>();
                terrain.ConfigureVegetationPresentation(AssetDatabase.LoadAssetAtPath<DungeonVegetationPresentation>("Assets/_Project/ScriptableObjects/Dungeons/Forest/DVP_Forest_Prototype.asset"));
                var map=new Godmist.Runtime.Dungeons.DungeonFloorMap();
                for(var y=-5;y<10;y++)for(var x=-5;x<10;x++)map.TryAddFloorCell(new Vector2Int(x,y),"room");
                Set(terrain,"boundMap",map);
                var tree=new GameObject("Overlapping tree").AddComponent<SpriteRenderer>(); tree.transform.SetParent(root.transform,false);
                tree.sprite=AssetDatabase.LoadAssetAtPath<DungeonDefinition>(ForestPrototypeSetup.DefinitionPath).VegetationPresentation.BoundaryTree;
                tree.sortingOrder=1000; terrain.RegisterForestCanopy(tree);
                var fixture=new GameObject("Fixture").AddComponent<SpriteRenderer>(); fixture.transform.SetParent(root.transform,false);
                fixture.sprite=Resources.Load<Sprite>("Godmist/ForestArt/Fixtures/forest-brazier-32ppu");
                var lighting=root.AddComponent<DungeonSilhouetteLighting>(); Set(lighting,"terrain",terrain);
                var type=typeof(DungeonSilhouetteLighting).GetNestedType("Torch",BindingFlags.NonPublic);
                var torch=System.Activator.CreateInstance(type,true);
                type.GetField("forestFixture").SetValue(torch,true); type.GetField("view").SetValue(torch,fixture);
                type.GetField("cell").SetValue(torch,new Godmist.Runtime.Dungeons.DungeonSurfacePosition(terrain.WorldToCell(Vector2.zero)));
                var resolve=typeof(DungeonSilhouetteLighting).GetMethod("ResolveTorchSortingOrder",Flags);
                foreach(var order in new[]{1000,1200,900})
                {
                    tree.sortingOrder=order;
                    Assert.That((int)resolve.Invoke(lighting,new[]{torch}),
                        Is.EqualTo(terrain.GetStandingObjectDepthSortingOrder(0)+1),
                        "A crown cannot promote the fixture out of its ground-depth lane.");
                }
            }
            finally {Object.DestroyImmediate(root);}
        }
        [Test]
        public void MountedTorchStaysImmediatelyInFrontOfItsOwnerAfterDepthChanges()
        {
            var root=new GameObject("mounted torch sorting");
            try
            {
                var lighting=root.AddComponent<DungeonSilhouetteLighting>();
                var group=root.AddComponent<UnityEngine.Rendering.SortingGroup>();
                var type=typeof(DungeonSilhouetteLighting).GetNestedType("Torch",BindingFlags.NonPublic);
                var torch=System.Activator.CreateInstance(type,true);
                type.GetField("mountGroup").SetValue(torch,group);
                var resolve=typeof(DungeonSilhouetteLighting).GetMethod("ResolveTorchSortingOrder",Flags);
                foreach(var order in new[]{12,800,-30})
                {
                    group.sortingOrder=order;
                    Assert.That(resolve.Invoke(lighting,new[]{torch}),Is.EqualTo(order+1));
                }
                type.GetField("mountGroup").SetValue(torch,null);
                var trunk=root.AddComponent<SpriteRenderer>();
                type.GetField("mountSprite").SetValue(torch,trunk);
                trunk.sortingOrder=37;
                Assert.That(resolve.Invoke(lighting,new[]{torch}),Is.EqualTo(38));
            }
            finally{Object.DestroyImmediate(root);}
        }
        [TestCase(42)][TestCase(91)][TestCase(127)]
        public void ExistingForestPropsReceiveSparseDeterministicLightsWithoutChangingObjects(int seed)
        {
            var definition=AssetDatabase.LoadAssetAtPath<DungeonDefinition>(ForestPrototypeSetup.DefinitionPath);
            var dungeon=new DungeonGeneratorService().Generate(definition,seed);
            new DungeonObjectPlacementSystem().Place(dungeon);
            var floor=dungeon.Floors[0];var before=floor.Objects.ToArray();
            var root=new GameObject("forest light test");
            var material=new Material(Shader.Find("Godmist/Dungeon/Pixel Torch"));
            try
            {
                var terrain=root.AddComponent<DungeonFloorMapView>();
                terrain.ConfigureVegetationPresentation(AssetDatabase.LoadAssetAtPath<DungeonVegetationPresentation>("Assets/_Project/ScriptableObjects/Dungeons/Forest/DVP_Forest_Prototype.asset"));
                Set(terrain,"boundMap",floor.Map);Set(terrain,"<FogFloor>k__BackingField",floor);
                var lighting=root.AddComponent<DungeonSilhouetteLighting>();
                Set(lighting,"terrain",terrain);Set(lighting,"flameMaterial",material);
                Set(lighting,"torchSprite",AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Tiles/DungeonTorch32-v3.png"));
                var build=typeof(DungeonSilhouetteLighting).GetMethod("BuildTorches",Flags);
                build.Invoke(lighting,null);
                var fixtures=root.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.transform.parent==root.transform).ToArray();
                Assert.That(fixtures.Length,Is.GreaterThan(0));
                build.Invoke(lighting,null);
                Assert.That(lighting.TorchCount,Is.EqualTo(fixtures.Length),"Repeat refresh must not duplicate fixtures.");
                Assert.That(lighting.ForestStaticLightCount,Is.EqualTo(fixtures.Length));
                var sources=(System.Collections.IEnumerable)typeof(DungeonSilhouetteLighting).GetField("torches",Flags).GetValue(lighting);
                Assert.That(sources.Cast<object>().All(t=>!(bool)t.GetType().GetField("carried").GetValue(t)),Is.True,
                    "Forest lighting must contain map fixtures only, never a light following the leader.");
                Assert.That(root.GetComponentsInChildren<Transform>().Any(t=>t.name=="Party light source"),Is.False);
                Assert.That(floor.Objects,Is.EqualTo(before));
                var positions=fixtures.Select(r=>(Vector2)r.transform.position).ToArray();
                for(int i=0;i<positions.Length;i++)for(int j=i+1;j<positions.Length;j++)
                    Assert.That(Vector2.Distance(positions[i],positions[j]),Is.GreaterThan(DungeonSilhouetteLighting.ForestFixtureSpacing-.5f));
                foreach(var fixture in fixtures)Assert.That(fixture.sprite.pixelsPerUnit,Is.EqualTo(32));
                typeof(DungeonSilhouetteLighting).GetMethod("OnDisable",Flags).Invoke(lighting,null); // EditMode does not run the normal enabled lifecycle
                Assert.That(root.GetComponentsInChildren<SpriteRenderer>(),Is.Empty);
                Assert.That(lighting.TorchCount,Is.Zero);
            }
            finally {Object.DestroyImmediate(root);if(material!=null)Object.DestroyImmediate(material);}
        }
    }
}

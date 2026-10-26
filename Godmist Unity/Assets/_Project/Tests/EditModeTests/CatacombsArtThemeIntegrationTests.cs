using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Dungeons;
using Godmist.Views.Dungeons;
using Godmist.Views.Exploration;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public class CatacombsArtThemeIntegrationTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [TestCase("DD_Catacombs_Expedition.asset")]
        [TestCase("DD_Catacombs_BossExpedition.asset")]
        [TestCase("DD_Catacombs_Procedural.asset")]
        [TestCase("DD_Catacombs_Traps_Demo.asset")]
        public void ShippedCatacombsVariantsUseImportedWallsAndTrapStates(string file)
        {
            var definition = AssetDatabase.LoadAssetAtPath<DungeonDefinition>(
                "Assets/_Project/ScriptableObjects/Dungeons/" + file);
            Assert.That(definition, Is.Not.Null); Assert.That(definition.Theme, Is.EqualTo(DungeonTheme.Catacombs));
            VerifyPresentation(definition, true);
        }

        [Test]
        public void DifferentBiomeDoesNotReceiveCatacombsArtEvenWithLegacyExpeditionId()
        {
            var definition = ScriptableObject.CreateInstance<DungeonDefinition>();
            try
            {
                var state = new SerializedObject(definition);
                state.FindProperty("<Id>k__BackingField").stringValue = "dungeon.catacombs.expedition";
                state.FindProperty("<Theme>k__BackingField").enumValueIndex = (int)DungeonTheme.Forest;
                state.ApplyModifiedPropertiesWithoutUndo();
                VerifyPresentation(definition, false);
            }
            finally { Object.DestroyImmediate(definition); }
        }

        private static void VerifyPresentation(DungeonDefinition definition, bool imported)
        {
            // Use the shipped scene's actual prefab references and component
            // wiring, then exercise the same configuration called by Start.
            var scene = EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/Sandbox/DungeonSandbox.unity");
            try
            {
                T Component<T>() where T : Component => scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();
                var bootstrap = Component<DungeonSandboxBootstrap>();
                var mapView = Component<DungeonFloorMapView>();
                var spawner = Component<DungeonObjectSpawner>();
                var partyObject = new GameObject("Presentation fixture party");
                SceneManager.MoveGameObjectToScene(partyObject, scene);
                var party = partyObject.AddComponent<ExplorationPartyController>();
                var map = new DungeonFloorMap();
                for (var y = 0; y < 6; y++) for (var x = 0; x < 6; x++)
                    map.TryAddFloorCell(new Vector2Int(x,y), "room");
                var lowH = new DungeonCellEdge(new Vector2Int(1,1), new Vector2Int(1,2));
                var lowV = new DungeonCellEdge(new Vector2Int(2,1), new Vector2Int(3,1));
                var railH = new DungeonCellEdge(new Vector2Int(1,3), new Vector2Int(1,4));
                var railV = new DungeonCellEdge(new Vector2Int(3,3), new Vector2Int(4,3));
                map.SetWallType(lowH,DungeonWallType.Low); map.SetWallType(lowV,DungeonWallType.Low);
                map.SetWallType(railH,DungeonWallType.Railing); map.SetWallType(railV,DungeonWallType.Railing);
                var floor = new DungeonFloorInstance(0); Assert.That(floor.TrySetMap(map), Is.True);
                var assets = new[] { "Spikes", "Gas", "GraveSeal" };
                var textures = new[] { "catacombs-spikes", "catacombs-gas-vent", "catacombs-grave-seal" };
                var traps = new SpikeTrapDungeonObjectInstance[assets.Length];
                for (var i = 0; i < assets.Length; i++)
                {
                    var trapDefinition = AssetDatabase.LoadAssetAtPath<SpikeTrapDungeonObjectDefinition>(
                        "Assets/_Project/ScriptableObjects/Dungeons/Objects/DO_Trap_" + assets[i] + ".asset");
                    Assert.That(trapDefinition, Is.Not.Null);
                    traps[i] = new SpikeTrapDungeonObjectInstance("trap."+i,trapDefinition,new Vector2Int(i*2,0));
                    Assert.That(floor.TryAddObject(traps[i]), Is.True);
                }
                var revision = map.TraversalRevision;
                var enabled = (bool)typeof(DungeonSandboxBootstrap).GetMethod("ConfigureFloorPresentation",Private)
                    .Invoke(bootstrap,new object[] { definition,floor,false });
                Assert.That(enabled, Is.EqualTo(imported));
                mapView.Rebuild(map);
                var all = mapView.GetComponentsInChildren<SpriteRenderer>(true);
                var lowProfiles = all.Where(r => r.name == "Catacombs low wall profile").ToArray();
                var railProfiles = all.Where(r => r.name == "Catacombs audit railing visual").ToArray();
                Assert.That(lowProfiles.Length, Is.EqualTo(imported ? 2 : 0));
                Assert.That(railProfiles.Length, Is.EqualTo(imported ? 2 : 0));
                if (imported)
                {
                    Assert.That(lowProfiles.All(r => r.sprite.texture.name == "catacombs-low-wall-sheet-v1"), Is.True);
                    Assert.That(railProfiles.Select(r => r.sprite.texture.name),
                        Is.EquivalentTo(new[] { "catacombs-railing-v3", "catacombs-railing-side-v3" }));
                    foreach (var renderer in lowProfiles.Concat(railProfiles))
                    {
                        Assert.That(renderer.sprite.pixelsPerUnit, Is.EqualTo(32));
                        Assert.That(renderer.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
                        Assert.That(renderer.enabled, Is.True);
                    }
                }
                Assert.That(spawner.Rebuild(floor,party,definition.Theme,definition.Id), Is.True);
                for (var i = 0; i < traps.Length; i++)
                {
                    var trap = spawner.SpawnedObjects.OfType<DungeonSpikeTrapView>()
                        .Single(v => v.ObjectInstanceId == traps[i].InstanceId);
                    var active = trap.GetComponentsInChildren<SpriteRenderer>(true).Where(r => r.enabled).ToArray();
                    Assert.That(active, Has.Length.EqualTo(1), "No old prefab visual underneath the replacement.");
                    var art = active[0];
                    Assert.That(art.sprite.texture.name == textures[i], Is.EqualTo(imported));
                    if (!imported) continue;
                    Assert.That(art.sprite.pixelsPerUnit, Is.EqualTo(32));
                    var size = art.bounds.size;
                    Assert.That(traps[i].TryTrigger(), Is.True);
                    typeof(DungeonSpikeTrapView).GetMethod("RefreshVisual",Private).Invoke(trap,null);
                    Assert.That(art.sprite.texture.name, Is.EqualTo(i == 1 ? "catacombs-gas-base" : textures[i]));
                    Assert.That(art.bounds.size, Is.EqualTo(size));
                }
                Assert.That(map.GetWallType(lowH), Is.EqualTo(DungeonWallType.Low));
                Assert.That(map.GetWallType(railV), Is.EqualTo(DungeonWallType.Railing));
                Assert.That(map.TraversalRevision, Is.EqualTo(revision), "Art activation must not change navigation topology.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Editor;
using Godmist.Core.Enums;
using Godmist.Runtime.Dungeons;
using Godmist.Views.Dungeons;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public class CatacombsMixedWallJunctionTests
    {
        private Scene scene;
        private DungeonFloorMapView authored;
        private readonly List<GameObject> roots = new();
        [OneTimeSetUp] public void LoadProductionSetup()
        {
            scene = EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/Sandbox/DungeonSandbox.unity");
            authored = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DungeonFloorMapView>(true)).Single();
        }
        [TearDown] public void ClearViews() { foreach (var root in roots) Object.DestroyImmediate(root); roots.Clear(); }
        [OneTimeTearDown] public void ClosePreview() => EditorSceneManager.ClosePreviewScene(scene);

        private DungeonFloorMapView Build(string id, out DungeonFloorMap map, int armLength = 1, Action<DungeonFloorMap> configure = null)
        {
            var root = new GameObject(id, typeof(Grid)); roots.Add(root); SceneManager.MoveGameObjectToScene(root, scene);
            var view = root.AddComponent<DungeonFloorMapView>();
            var tiles = new GameObject("Floor", typeof(Tilemap), typeof(TilemapRenderer)); tiles.transform.SetParent(root.transform, false);
            var source = new SerializedObject(authored); var destination = new SerializedObject(view);
            destination.FindProperty("floorTilemap").objectReferenceValue = tiles.GetComponent<Tilemap>();
            foreach (var field in new[] { "floorTile", "wallSegmentPrefab", "lowWallPrefab", "railingPrefab" })
                destination.FindProperty(field).objectReferenceValue = source.FindProperty(field).objectReferenceValue;
            destination.ApplyModifiedPropertiesWithoutUndo(); view.ConfigureCatacombsArtPresentation(true);
            var sample = CatacombsWallRulesetReview.Cases().Single(value => value.id == id);
            map = CatacombsWallRulesetReview.BuildMap(sample, armLength);
            configure?.Invoke(map);
            var revision = map.TraversalRevision;
            view.Rebuild(map);
            Assert.That(map.TraversalRevision, Is.EqualTo(revision));
            Assert.That(view.GetComponentsInChildren<DungeonWallSegmentView>().Length, Is.EqualTo((sample.low + sample.full) * armLength));
            return view;
        }

        [TestCase("F13-A45")] [TestCase("F14-A45")] [TestCase("F15-A45")]
        [TestCase("F15-A19")] [TestCase("F15-A21")] [TestCase("F15-A22")]
        [TestCase("F15-A29")] [TestCase("F15-A33")] [TestCase("F15-A49")]
        public void MixedStepOwnsOneClosedFaceAndCapWithoutMovingLogicalWalls(string id)
        {
            var view = Build(id, out _);
            var renderers = view.GetComponentsInChildren<SpriteRenderer>().Where(r => r.enabled).ToArray();
            var faces = renderers.Where(r => r.name == "Mixed low wall junction face").ToArray();
            var caps = renderers.Where(r => r.name.StartsWith("Mixed low wall cap junction ")).ToArray();
            Assert.That(faces.Length, Is.EqualTo(1), id + ": a single owner closes the standing step.");
            Assert.That(caps.Length, Is.EqualTo(1), id + ": a single owner closes the raised cap.");
            Assert.That(faces[0].bounds.min.y, Is.EqualTo(0).Within(.0001));
            Assert.That(faces[0].bounds.max.y, Is.EqualTo(caps[0].bounds.min.y).Within(.0001));
            Assert.That(caps[0].bounds.max.y, Is.EqualTo(.75f).Within(.0001));
            Assert.That(faces[0].bounds.min.x, Is.EqualTo(-.125f).Within(.0001));
            Assert.That(faces[0].bounds.max.x, Is.EqualTo(.125f).Within(.0001));
        }

        [Test]
        public void NorthFullSouthLowPreservesTheFullHeightSouthFace()
        {
            var view = Build("F15-A63", out _);
            var renderers = view.GetComponentsInChildren<SpriteRenderer>().Where(r => r.enabled).ToArray();
            var full = renderers.Single(r => r.name == "Side strip above mixed low junction");
            var low = renderers.Single(r => r.name == "Cropped native low wall");
            var face = renderers.Single(r => r.name == "Mixed full wall junction face");
            var cap = renderers.Single(r => r.name.StartsWith("Mixed full wall cap "));
            Assert.That(low.bounds.max.y, Is.EqualTo(face.bounds.min.y).Within(.0001));
            Assert.That(face.bounds.size.y, Is.EqualTo(.75f).Within(.0001));
            Assert.That(face.bounds.max.y, Is.EqualTo(cap.bounds.min.y).Within(.0001));
            Assert.That(full.bounds.min.y, Is.EqualTo(DungeonWallSegmentView.NativeFullWallCapCenterHeight).Within(.0001));
            Assert.That(renderers.Any(r => r.name.StartsWith("Mixed low wall cap junction ")), Is.False,
                "The low roof must meet the full face, not erase the height transition with a low cap.");
            var collider = full.GetComponentInParent<DungeonWallSegmentView>().GetComponent<BoxCollider2D>();
            Assert.That(collider.bounds.center.y, Is.EqualTo(.5f).Within(.0001), "Only the visual child is clipped.");
            Assert.That(collider.bounds.size.y, Is.EqualTo(1).Within(.0001));
        }

        [TestCase("F15-A22")] [TestCase("F15-A63")] [TestCase("F05-A19")]
        [TestCase("F14-A14")] [TestCase("F13-A16")]
        [TestCase("F14-A17")] [TestCase("F13-A17")]
        [TestCase("F14-A65")] [TestCase("F13-A69")]
        [TestCase("F14-A47")] [TestCase("F13-A51")]
        [TestCase("F14-A35")] [TestCase("F13-A35")]
        [TestCase("F15-A30")] [TestCase("F15-A28")]
        [TestCase("F15-A10")] [TestCase("F15-A12")] [TestCase("F15-A13")]
        [TestCase("F15-A31")] [TestCase("F15-A37")] [TestCase("F15-A39")]
        [TestCase("F15-A40")] [TestCase("F14-A30")] [TestCase("F13-A28")]
        public void ReconfigurationDoesNotDuplicateJunctionsOrChangeTraversalAndColliders(string id)
        {
            var view = Build(id, out var map);
            var revision = map.TraversalRevision;
            var before = view.GetComponentsInChildren<BoxCollider2D>().Select(c => (c, c.size, c.offset, c.enabled, c.transform.localToWorldMatrix)).ToArray();
            var rendererCount = view.GetComponentsInChildren<SpriteRenderer>(true).Length;
            var configure = typeof(DungeonFloorMapView).GetMethod("ConfigureCatacombsLowWallPresentations", BindingFlags.Instance | BindingFlags.NonPublic);
            configure.Invoke(view, null); configure.Invoke(view, null);
            Assert.That(map.TraversalRevision, Is.EqualTo(revision));
            Assert.That(view.GetComponentsInChildren<SpriteRenderer>(true).Length, Is.EqualTo(rendererCount));
            Assert.That(view.GetComponentsInChildren<BoxCollider2D>().Select(c => (c, c.size, c.offset, c.enabled, c.transform.localToWorldMatrix)), Is.EqualTo(before));
        }

        [TestCase("F14-A17", 5)] [TestCase("F13-A17", 6)]
        public void MixedFullProfilesKeepTheSouthLowRoofVisible(string id, int capArms)
        {
            var view = Build(id, out _, 4);
            var roof = view.GetComponentsInChildren<SpriteRenderer>().Single(r => r.enabled && r.name == "South low wall roof continuation");
            var cap = view.GetComponentsInChildren<SpriteRenderer>().Single(r => r.enabled && r.name == "Mixed low wall cap junction " + capArms);
            Assert.That(roof.bounds.min.y, Is.EqualTo(0).Within(.0001));
            Assert.That(roof.bounds.max.y, Is.EqualTo(cap.bounds.min.y).Within(.0001));
            Assert.That(cap.bounds.max.y, Is.EqualTo(.75f).Within(.0001));
        }

        [TestCase("F06-A17")] [TestCase("F09-A17")]
        public void MixedStandingNorthFaceIsNotCoveredBySouthFloorMask(string id)
        {
            var view = Build(id, out _, 4);
            var face = view.GetComponentsInChildren<DungeonWallSegmentView>()
                .Where(w => w.SurfaceProfile == DungeonWallSurfaceProfile.North)
                .Select(w => w.GetComponent<SpriteRenderer>()).OrderBy(r => Mathf.Abs(r.bounds.center.x)).First();
            foreach (var mask in view.GetComponentsInChildren<SpriteRenderer>().Where(r => r.enabled && r.name == "SouthWallOcclusionMask"))
            {
                var x = Mathf.Max(0, Mathf.Min(face.bounds.max.x, mask.bounds.max.x) - Mathf.Max(face.bounds.min.x, mask.bounds.min.x));
                var y = Mathf.Max(0, Mathf.Min(face.bounds.max.y, mask.bounds.max.y) - Mathf.Max(face.bounds.min.y, mask.bounds.min.y));
                Assert.That(x * y, Is.LessThan(.00001f), "A gap mask must not paint over a neighbouring standing face.");
            }
        }

        private static IEnumerable<string> MultiFullProfileCases => CatacombsWallRulesetReview.Cases()
            .Where(c => c.low > 0 && c.full > 1).Select(c => c.id);

        [TestCaseSource(nameof(MultiFullProfileCases))]
        public void EveryMultiFullProfileJoinCanBeReconfiguredWithoutChangingGameplay(string id)
            => ReconfigurationDoesNotDuplicateJunctionsOrChangeTraversalAndColliders(id);

        [TestCase("F06-A71", 10)] [TestCase("F09-A71", 9)]
        public void ThreeFullProfilesJoinTheirHigherRoofsWithoutClippingTheCap(string id, int capArms)
        {
            var view = Build(id, out _, 4);
            var cap = view.GetComponentsInChildren<SpriteRenderer>()
                .Single(r => r.enabled && r.name == "Mixed full wall cap " + capArms);
            Assert.That(cap.bounds.min.y, Is.EqualTo(.75f).Within(.0001));
            Assert.That(cap.bounds.size.x, Is.EqualTo(.25f).Within(.0001),
                "Standing North roofs on the same plane must share a whole turn, not a clipped half-cap.");
        }

        [TestCase("F15-A30", 10)] [TestCase("F15-A28", 9)]
        [TestCase("F14-A30", 10)] [TestCase("F13-A28", 9)]
        public void LowNorthTurnOwnsItsFaceUnderTheWholeCorner(string id, int arms)
        {
            var view = Build(id, out _, 4);
            var cap = view.GetComponentsInChildren<SpriteRenderer>().Single(r => r.enabled && r.name == "Native low wall cap junction " + arms);
            var wall = cap.GetComponentInParent<DungeonWallSegmentView>();
            var face = wall.GetComponentsInChildren<SpriteRenderer>().Single(r => r.enabled && r.name == "Cropped native low wall");
            Assert.That(face.bounds.size.x, Is.GreaterThan(.3f), "The horizontal face must own the turn, above the vertical low-wall depth lane.");
            Assert.That(face.bounds.min.x, Is.LessThanOrEqualTo(cap.bounds.min.x + .0001f));
            Assert.That(face.bounds.max.x, Is.GreaterThanOrEqualTo(cap.bounds.max.x - .0001f));
        }

        [TestCase("F15-A10")] [TestCase("F15-A12")]
        public void LowFaceDoesNotTreatARailingAsADescendingMasonryRoof(string id)
        {
            var view = Build(id, out _, 1, map => map.SetWallType(
                new DungeonCellEdge(new Vector2Int(-1,-1),new Vector2Int(0,-1)),DungeonWallType.Railing));
            Assert.That(view.GetComponentsInChildren<SpriteRenderer>().Any(r => r.enabled
                && r.name == "Native low wall roof continuation"), Is.False,
                "A shared topology arm is not necessarily a low masonry roof.");
        }

        [TestCase("F14-A65")] [TestCase("F13-A69")]
        [TestCase("F06-A65")] [TestCase("F09-A69")]
        public void SideBranchPreservesTheFullNorthToLowSouthStandingFace(string id)
        {
            var view = Build(id, out _, 4);
            var renderers = view.GetComponentsInChildren<SpriteRenderer>().Where(r => r.enabled).ToArray();
            var face = renderers.Single(r => r.name == "Mixed full wall junction face");
            var cap = renderers.Single(r => r.name == "Mixed full wall cap 8");
            Assert.That(face.bounds.min.y, Is.EqualTo(0).Within(.0001));
            Assert.That(face.bounds.max.y, Is.EqualTo(.75f).Within(.0001));
            Assert.That(cap.bounds.min.y, Is.EqualTo(face.bounds.max.y).Within(.0001));
            Assert.That(renderers.Any(r => r.name == "South low wall roof continuation"), Is.False,
                "The low south roof must meet the foot of the full face, not overwrite it.");
        }

        [TestCase("F14-A47", 9)] [TestCase("F13-A51", 10)]
        [TestCase("F06-A47", 9)] [TestCase("F09-A51", 10)]
        public void SideBranchPreservesTheLowNorthToFullSouthHeightStep(string id, int capArms)
        {
            var view = Build(id, out _, 4);
            var renderers = view.GetComponentsInChildren<SpriteRenderer>().Where(r => r.enabled).ToArray();
            var face = renderers.Single(r => r.name == "Mixed low wall junction face");
            var lower = renderers.Single(r => r.name == "Mixed full wall cap 4");
            var upper = renderers.Single(r => r.name == "Mixed low wall cap junction " + capArms);
            Assert.That(lower.bounds.max.y, Is.EqualTo(face.bounds.min.y).Within(.0001));
            Assert.That(face.bounds.size.y, Is.EqualTo(.5f).Within(.0001));
            Assert.That(face.bounds.max.y, Is.EqualTo(upper.bounds.min.y).Within(.0001));
        }

        [TestCase("F14-A35", 9)] [TestCase("F13-A35", 10)]
        public void MixedProfileNorthTurnKeepsItsNativeCornerBorder(string id, int capArms)
        {
            var view = Build(id, out _, 4);
            var cap = view.GetComponentsInChildren<SpriteRenderer>().Single(r => r.enabled && r.name == "Mixed low wall cap junction " + capArms);
            Assert.That(cap.bounds.min.y, Is.EqualTo(.5f).Within(.0001));
            Assert.That(cap.bounds.max.y, Is.EqualTo(.75f).Within(.0001));
        }

        [TestCase("F14-A14")] [TestCase("F13-A16")]
        public void SouthFacingFullWallMeetsLowArmsWithOneContinuousRoofCorner(string id)
        {
            var view = Build(id, out _, 4);
            var caps = view.GetComponentsInChildren<SpriteRenderer>()
                .Where(r => r.enabled && r.name == "Mixed low wall cap junction 7").ToArray();
            Assert.That(caps.Length, Is.EqualTo(1), "The shared W/E/S roof vertex needs one cap, including the full South-profile arm.");
            Assert.That(caps[0].bounds.center.x, Is.EqualTo(0).Within(.0001));
            Assert.That(caps[0].bounds.min.y, Is.EqualTo(.5f).Within(.0001));
            Assert.That(caps[0].bounds.max.y, Is.EqualTo(.75f).Within(.0001));
            var roof = view.GetComponentsInChildren<SpriteRenderer>()
                .Single(r => r.enabled && r.name == "South low wall roof continuation");
            Assert.That(roof.bounds.min.y, Is.EqualTo(0).Within(.0001));
            Assert.That(roof.bounds.max.y, Is.EqualTo(caps[0].bounds.min.y).Within(.0001));
            Assert.That(roof.bounds.size.x, Is.EqualTo(.25f).Within(.0001));
            var lowStrip = view.GetComponentsInChildren<SpriteRenderer>().Where(r => r.enabled
                    && r.name == "Cropped native low wall" && r.bounds.size.x < .3f && r.bounds.max.y > 0)
                .OrderByDescending(r => r.bounds.max.y).First();
            var expectedSourceY = lowStrip.sprite.rect.y
                + Mathf.Repeat(roof.bounds.min.y - lowStrip.bounds.min.y, lowStrip.sprite.rect.height / lowStrip.sprite.pixelsPerUnit)
                * lowStrip.sprite.pixelsPerUnit;
            Assert.That(roof.sprite.rect.y, Is.EqualTo(expectedSourceY).Within(.001), "Rehoming must preserve the original stone UV phase.");
            Assert.That(roof.GetComponentInParent<DungeonWallSegmentView>(),
                Is.EqualTo(caps[0].GetComponentInParent<DungeonWallSegmentView>()), "The mask owner must also own the overlapping roof.");
            Assert.That(view.GetComponentsInChildren<SpriteRenderer>().Any(r => r.enabled && r.name == "SouthWallOcclusionMask"), Is.True,
                "Closing the roof corner must preserve intentional floor occlusion.");
        }

        [Test]
        public void ACompletelyClippedSideCanRestoreItsSourceOnReconfiguration()
        {
            var view = Build("F15-A63", out _);
            var strip = view.GetComponentsInChildren<SpriteRenderer>().Single(r => r.name == "Side strip above mixed low junction");
            var wall = strip.GetComponentInParent<DungeonWallSegmentView>();
            typeof(DungeonWallSegmentView).GetMethod("ClipSideAtRaisedLowWall", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(wall, new object[] { float.MaxValue });
            Assert.That(strip.enabled, Is.False);
            typeof(DungeonFloorMapView).GetMethod("ConfigureCatacombsLowWallPresentations", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
            Assert.That(strip.enabled, Is.True);
            Assert.That(strip.bounds.min.y, Is.EqualTo(DungeonWallSegmentView.NativeFullWallCapCenterHeight).Within(.0001));
        }

        [Test]
        public void LowNorthFullSouthRetainsTheFullCapEndingBeforeTheNextCap()
        {
            var view = Build("F15-A45", out _, 3);
            var renderers = view.GetComponentsInChildren<SpriteRenderer>().Where(r => r.enabled).ToArray();
            var lowerCap = renderers.Single(r => r.name == "Mixed full wall cap 4");
            var upperCap = renderers.Single(r => r.name.StartsWith("Mixed low wall cap junction "));
            Assert.That(lowerCap.bounds.max.y, Is.EqualTo(0).Within(.0001));
            Assert.That(upperCap.bounds.min.y, Is.EqualTo(.5f).Within(.0001));
            var southWall = view.GetComponentsInChildren<DungeonWallSegmentView>()
                .Single(w => w.GetComponent<BoxCollider2D>().enabled && Mathf.Abs(w.GetComponent<BoxCollider2D>().bounds.center.y + .5f) < .0001f);
            Assert.That(southWall.GetComponent<SpriteRenderer>().sprite.name, Is.Not.EqualTo("RuntimeDungeonMixedSideFace"),
                "The near-junction southern arm is a cap run, not the free face of an artificially short sample.");
        }

        [Test]
        public void OneCellFullWallBetweenTwoLowArmsCombinesCapClosures()
        {
            var view = Build("F15-A36", out _, 3, map =>
                map.SetWallType(new DungeonCellEdge(new Vector2Int(-1, 0), new Vector2Int(0, 0)), DungeonWallType.Full));
            var caps = view.GetComponentsInChildren<SpriteRenderer>().Where(r => r.enabled && r.name.StartsWith("Mixed full wall cap ")).ToArray();
            Assert.That(caps.Length, Is.EqualTo(1), "Coincident top/bottom cap requests have one owner, not two overlapping renderers.");
            Assert.That(caps[0].name, Is.EqualTo("Mixed full wall cap 0"));
            var sprite = caps[0].sprite;
            var rect = sprite.rect;
            Assert.That(sprite.texture.GetPixel((int)rect.x + 3, (int)rect.y + 6),
                Is.Not.EqualTo(sprite.texture.GetPixel((int)rect.x + 3, (int)rect.y + 3)),
                "Zero open arms is a closed cap, not the legacy 'infer a corner' default.");
        }
    }
}

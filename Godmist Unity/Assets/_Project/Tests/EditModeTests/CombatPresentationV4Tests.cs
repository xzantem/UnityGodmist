using Godmist.Core.Enums;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Effects;
using Godmist.Runtime.Dungeons;
using Godmist.Views.Dungeons;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Tests.EditMode
{
    public class CombatPresentationV4Tests
    {
        [Test]
        public void RepeatedFovRenderingReusesSpritesInsteadOfRecreatingObjects()
        {
            var root=new GameObject("fov-render-test");var texture=new Texture2D(2,2);var sprite=Sprite.Create(texture,new Rect(0,0,2,2),Vector2.one*.5f);
            try
            {
                var renderer=root.AddComponent<Godmist.Views.Combat.CombatGridFeedbackRenderer>();var so=new SerializedObject(renderer);
                so.FindProperty("feedbackRoot").objectReferenceValue=root.transform;so.FindProperty("feedbackSprite").objectReferenceValue=sprite;so.ApplyModifiedPropertiesWithoutUndo();
                var cells=new[]{new Godmist.Runtime.Combat.GridPosition(0,0),new Godmist.Runtime.Combat.GridPosition(1,0)};
                renderer.ShowFill(Godmist.Views.Combat.CombatGridFeedbackLayer.EnemyFov,cells,Color.red);
                var first=root.GetComponentsInChildren<SpriteRenderer>();
                for(var i=0;i<20;i++)renderer.ShowFill(Godmist.Views.Combat.CombatGridFeedbackLayer.EnemyFov,cells,Color.red);
                CollectionAssert.AreEquivalent(first,root.GetComponentsInChildren<SpriteRenderer>());
                Assert.That(root.transform.childCount,Is.EqualTo(2));
                renderer.ClearFill(Godmist.Views.Combat.CombatGridFeedbackLayer.EnemyFov);
                Assert.That(root.GetComponentsInChildren<SpriteRenderer>(),Is.Empty);
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(sprite);Object.DestroyImmediate(texture);}
        }

        [Test]
        public void LargeUniformSkillFillUsesOneReusableMesh()
        {
            var root = new GameObject("skill-fill-test");
            var texture = new Texture2D(2, 2);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * .5f);
            try
            {
                var renderer = root.AddComponent<Godmist.Views.Combat.CombatGridFeedbackRenderer>();
                var settings = new SerializedObject(renderer);
                settings.FindProperty("feedbackRoot").objectReferenceValue = root.transform;
                settings.FindProperty("feedbackSprite").objectReferenceValue = sprite;
                settings.ApplyModifiedPropertiesWithoutUndo();
                var cells = new System.Collections.Generic.List<Godmist.Runtime.Combat.GridPosition>();
                for (var x = 0; x < 12; x++) for (var y = 0; y < 12; y++)
                    cells.Add(new Godmist.Runtime.Combat.GridPosition(x, y));
                renderer.ShowFill(Godmist.Views.Combat.CombatGridFeedbackLayer.SkillRange, cells, Color.blue);
                var mesh = root.GetComponentInChildren<MeshFilter>();
                Assert.That(mesh, Is.Not.Null);
                Assert.That(mesh.sharedMesh.vertexCount, Is.EqualTo(cells.Count * 4));
                Assert.That(root.GetComponentsInChildren<SpriteRenderer>(), Is.Empty);
                renderer.ShowFill(Godmist.Views.Combat.CombatGridFeedbackLayer.SkillRange, cells, Color.red);
                Assert.That(root.GetComponentsInChildren<MeshFilter>().Length, Is.EqualTo(1));
                renderer.ClearFill(Godmist.Views.Combat.CombatGridFeedbackLayer.SkillRange);
                Assert.That(mesh.GetComponent<MeshRenderer>().enabled, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void ExplorationDurationUsesRemainingTicksAndCurrentTickProgress()
        {
            var character = new CharacterInstance { ExplorationStatusElapsedSeconds = 2.5f };
            var effect = new PassiveEffectRuntime { RemainingDuration = 3 };
            var status = new CombatStatusPresentation(effect, CombatStatusKind.Status, 0, explorationCharacter: character);
            Assert.That(UIWorkspaceTooltipView.ExplorationStatusDuration(status), Is.EqualTo("Pozostało: 16 s · ticki: 3"));
            effect.RemainingDuration = 0;
            Assert.That(UIWorkspaceTooltipView.ExplorationStatusDuration(status), Is.EqualTo("Bez limitu czasu"));
        }

        [Test]
        public void InspectViewportIsAuthoredWithoutWhiteMaskAndWithRowTemplates()
        {
            var root = PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CombatUnitInspectPanel.prefab");
            try
            {
                var view = root.GetComponent<CombatUnitInspectWindowView>();
                var so = new SerializedObject(view);
                var scroll = (ScrollRect)so.FindProperty("contentScroll").objectReferenceValue;
                Assert.That(scroll.viewport.GetComponent<Mask>(), Is.Null);
                Assert.That(scroll.viewport.GetComponent<RectMask2D>(), Is.Not.Null);
                Assert.That(scroll.viewport.GetComponent<Image>().color.a, Is.Zero);
                foreach (var field in new[] { "statusTemplate", "skillTemplate", "emptyStatuses", "emptySkills", "closeButton", "titleText" })
                    Assert.That(so.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
                Assert.That(((Component)so.FindProperty("statusTemplate").objectReferenceValue).gameObject.activeSelf, Is.False);
                Assert.That(((Component)so.FindProperty("skillTemplate").objectReferenceValue).gameObject.activeSelf, Is.False);
                foreach (var field in new[] { "skillTemplate", "statusTemplate" })
                {
                    var row = (Component)so.FindProperty(field).objectReferenceValue;
                    Assert.That(row.GetComponentsInChildren<Selectable>(true), Is.Empty, "Inspect rows only show information");
                    var icon = (Image)new SerializedObject(row).FindProperty("icon").objectReferenceValue;
                    Assert.That(icon.rectTransform.rect.width, Is.GreaterThanOrEqualTo(40));
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [Test]
        public void ExplorationPortraitHasThreeIconsWithoutCounters()
        {
            var root = PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CharacterPortrait Variant.prefab");
            try
            {
                var owner = root.GetComponent<CharacterInfoPartyPortraitView>();
                var collection = new SerializedObject(owner).FindProperty("statusCollection").objectReferenceValue;
                Assert.That(collection, Is.Not.Null);
                var so = new SerializedObject(collection);
                Assert.That(so.FindProperty("maximumVisibleIcons").intValue, Is.EqualTo(3));
                var icon = (CombatStatusIconView)so.FindProperty("iconTemplate").objectReferenceValue;
                Assert.That(new SerializedObject(icon).FindProperty("showCounters").boolValue, Is.False);
                Assert.That(icon.GetComponent<Graphic>(), Is.Not.Null);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [Test]
        public void TacticalTooltipDoesNotAdvertiseEmptyFloorAndExplainsDifficultTerrain()
        {
            var floor = new DungeonFloorInstance(0);
            var map = new DungeonFloorMap();
            map.TryAddFloorCell(Vector2Int.zero, "room"); floor.TrySetMap(map);
            Assert.That(DungeonTacticalTooltip.Describe(floor, Vector2Int.zero), Is.Empty);
            map.SetDifficultTerrain(Vector2Int.zero);
            Assert.That(DungeonTacticalTooltip.Describe(floor, Vector2Int.zero), Does.Contain("+1 AP"));
            Assert.That(DungeonTacticalTooltip.Describe(floor, Vector2Int.right), Is.Empty);
            map.SetWallType(new DungeonCellEdge(Vector2Int.zero, Vector2Int.right), DungeonWallType.Low);
            Assert.That(DungeonTacticalTooltip.Describe(floor, Vector2Int.zero), Does.Contain("tylko na istniejącą podłogę"));
        }
    }
}

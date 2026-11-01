using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Views;
using Godmist.Views.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class HighlightStandardTests
    {
        static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        static void Call(object target, string method) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

        [TestCase(true, true, true)] [TestCase(true, false, false)]
        [TestCase(false, false, true)] [TestCase(false, true, false)]
        public void TargetColourUsesRelationToCaster(bool caster, bool target, bool friendly) =>
            Assert.That(HighlightStyle.Target(caster, target), Is.EqualTo(friendly ? HighlightStyle.Friendly : HighlightStyle.Hostile));

        [Test]
        public void AreaPreviewSharesRangeArtworkAndKeepsRelationMarkersSeparate()
        {
            var go = new GameObject("area preview");
            try
            {
                var view = go.AddComponent<CombatGridFeedbackRenderer>();
                var cells = new[] { new GridPosition(0, 0), new GridPosition(1, 0) };
                view.ShowOutline(CombatGridFeedbackLayer.SkillRange, cells, HighlightStyle.Range);
                var range = go.GetComponentInChildren<MeshRenderer>();
                view.ShowOutline(CombatGridFeedbackLayer.RelativeRange, cells, HighlightStyle.Range);
                var area = go.GetComponentsInChildren<MeshRenderer>().Single(r => r != range);
                Assert.That(area.sharedMaterial, Is.SameAs(range.sharedMaterial),
                    "Area previews must use the same feathered artwork as skill range.");
                Assert.That(area.sortingOrder, Is.GreaterThan(range.sortingOrder));
                Assert.That(go.GetComponentsInChildren<SpriteRenderer>(), Is.Empty,
                    "Area previews must not fall back to rectangular edge sprites.");
                var mesh = area.GetComponent<MeshFilter>().sharedMesh;
                var texture = (Texture2D)area.sharedMaterial.mainTexture;
                var uv = mesh.uv;
                float Alpha(Vector2 p) => texture.GetPixelBilinear(p.x, p.y).a;
                Assert.That(Alpha(Vector2.Lerp(uv[2], uv[3], .5f)), Is.LessThan(.005f),
                    "Adjacent affected cells must not have internal contour lines.");
                view.ShowOutline(CombatGridFeedbackLayer.AffectedArea, new[] { cells[0] }, HighlightStyle.Hostile);
                var target = go.GetComponentsInChildren<MeshRenderer>().Single(r => r != range && r != area);
                Assert.That(target.sharedMaterial, Is.Not.SameAs(area.sharedMaterial));
                Assert.That(target.GetComponent<MeshFilter>().sharedMesh.colors.All(c => c == HighlightStyle.Hostile), Is.True);
                view.IsPositionVisible = p => p.X == 0;
                view.RefreshVisibility();
                Assert.That(mesh.vertexCount, Is.EqualTo(4));
                view.ClearAll();
                Assert.That(go.GetComponentsInChildren<MeshRenderer>().All(r => !r.enabled), Is.True);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ConfirmedTargetPreservesColourAndAddsAnInsetStroke()
        {
            var go = new GameObject("feedback");
            try
            {
                var view = go.AddComponent<CombatGridFeedbackRenderer>();
                var cells = new[] { new GridPosition(2, 2) };
                view.ShowOutline(CombatGridFeedbackLayer.SkillTarget, cells, HighlightStyle.Hostile);
                view.ShowOutline(CombatGridFeedbackLayer.ChosenSkillTargets, cells, HighlightStyle.Hostile);
                var meshes = go.GetComponentsInChildren<MeshFilter>();
                Assert.That(meshes.Select(m => m.sharedMesh.vertexCount).OrderBy(v => v), Is.EqualTo(new[] { 4, 8 }));
                Assert.That(meshes.All(m => m.sharedMesh.colors.All(c => c == HighlightStyle.Hostile)), Is.True);
                Assert.That(meshes.Single(m => m.sharedMesh.vertexCount == 8).GetComponent<MeshRenderer>().sortingOrder,
                    Is.GreaterThan(meshes.Single(m => m.sharedMesh.vertexCount == 4).GetComponent<MeshRenderer>().sortingOrder));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void RelationColouredConfirmedTargetsFollowEachUnitsFeet()
        {
            var go = new GameObject("feedback");
            try
            {
                var view = go.AddComponent<CombatGridFeedbackRenderer>();
                var offset = Vector2.zero;
                view.ResolveExplorationMarker = cell => new Vector2(cell.X, cell.Y) + offset;
                view.ShowOutline(CombatGridFeedbackLayer.ChosenSkillTargets,
                    new[] { new GridPosition(1, 1), new GridPosition(3, 3) }, Color.white, _ => HighlightStyle.Hostile);
                var mesh = go.GetComponentInChildren<MeshFilter>().sharedMesh;
                var before = mesh.vertices;
                offset = new Vector2(.3f, .7f);
                Call(view, "LateUpdate");
                for (var i = 0; i < before.Length; i++)
                    Assert.That(Vector3.Distance(mesh.vertices[i], before[i] + (Vector3)offset), Is.LessThan(.0001f));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void LeavingResolvableMapClearsMovementPreview()
        {
            var go = new GameObject("movement");
            try
            {
                var view = go.AddComponent<CombatGridFeedbackRenderer>();
                var controller = go.AddComponent<CombatMovementInputController>();
                Set(controller, "feedbackRenderer", view);
                Set(controller, "worldCamera", go.AddComponent<Camera>());
                controller.ResolvePointerPosition = _ => null;
                var destination = new GridPosition(1, 1);
                view.ShowMovementPreview(new[] { destination }, new GridPosition(0, 0), 10, HighlightStyle.Selection, showCost: false);
                Set(controller, "previewedDestination", (GridPosition?)destination);
                Call(controller, "RefreshMovementPreview");
                Assert.That(go.GetComponentsInChildren<SpriteRenderer>().Any(r => r.enabled && r.gameObject.activeSelf), Is.False);
                Assert.That(controller.GetType().GetField("previewedDestination", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller), Is.Null);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void LeavingResolvableMapClearsRelativeAreaAndCanReenterSameCell()
        {
            var go = new GameObject("targeting");
            var skill = ScriptableObject.CreateInstance<SkillData>();
            try
            {
                var mode = new SkillTargetModeData();
                Set(mode, "targetType", TargetType.AreaAroundTile);
                Set(skill, "targetModes", new List<SkillTargetModeData> { mode });
                var view = go.AddComponent<CombatGridFeedbackRenderer>();
                var controller = go.AddComponent<CombatTargetSelectionController>();
                Set(controller, "feedbackRenderer", view);
                Set(controller, "worldCamera", go.AddComponent<Camera>());
                Set(controller, "sandboxBootstrap", go.AddComponent<CombatSandboxBootstrap>());
                Set(controller, "selectedSkill", skill);
                controller.ResolvePointerPosition = _ => null;
                var cell = new GridPosition(1, 1);
                Set(controller, "previewedRelativeCenter", (GridPosition?)cell);
                view.ShowOutline(CombatGridFeedbackLayer.RelativeRange, new[] { cell }, HighlightStyle.Range);
                Call(controller, "UpdateRelativeRangePreview");
                Assert.That(controller.GetType().GetField("previewedRelativeCenter", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller), Is.Null);
                Assert.That(go.GetComponentsInChildren<Renderer>().Any(r => r.enabled), Is.False);
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(skill); }
        }

        [Test]
        public void TurnMarkerUsesPixelGeometryWithoutPaintingTheActorsBody()
        {
            var go = new GameObject("pixel actor");
            var texture = new Texture2D(4, 4);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.one * .5f, 64);
            try
            {
                var body = go.AddComponent<SpriteRenderer>(); body.sprite = sprite;
                var view = go.AddComponent<CombatUnitVisualStateView>();
                var unit = new BattleUnit { IsPlayerSide = true, Character = new CharacterInstance { LifeState = CharacterLifeState.Alive } };
                view.Refresh(unit, true, false, false);
                Assert.That(go.GetComponentsInChildren<SpriteRenderer>().Any(r => r != body && r.enabled), Is.False,
                    "The independent turn marker replaces the permanent neon silhouette.");
                var mesh = go.GetComponentInChildren<MeshFilter>().sharedMesh;
                Assert.That(go.GetComponentInChildren<MeshRenderer>().enabled, Is.True);
                foreach (var vertex in mesh.vertices)
                {
                    Assert.That(vertex.x * 32 + .5f, Is.EqualTo(Mathf.Round(vertex.x * 32 + .5f)).Within(.001));
                    Assert.That(vertex.y * 32 + .5f, Is.EqualTo(Mathf.Round(vertex.y * 32 + .5f)).Within(.001));
                }
                view.Refresh(unit, true, true, false);
                var outline = go.GetComponentsInChildren<SpriteRenderer>().First(r => r.name == "Visual State Outline");
                Assert.That(outline.transform.localPosition.magnitude, Is.EqualTo(1f / 64f).Within(.0001),
                    "Hover tracks the source sprite's pixel size, not a scene-specific world width.");
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); }
        }

        [Test]
        public void HiddenActorCannotLeaveAnOutlineOrTurnMarker()
        {
            var go = new GameObject("hidden actor");
            var texture = new Texture2D(2, 2);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * .5f);
            try
            {
                var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = sprite;
                var view = go.AddComponent<CombatUnitVisualStateView>();
                var unit = new BattleUnit { IsPlayerSide = true, Character = new CharacterInstance { LifeState = CharacterLifeState.Alive } };
                view.Refresh(unit, true, true, false);
                renderer.color = Color.clear;
                Call(view, "LateUpdate");
                Assert.That(go.GetComponentsInChildren<MeshRenderer>().Any(r => r.enabled), Is.False);
                Assert.That(go.GetComponentsInChildren<SpriteRenderer>().Any(r => r != renderer && r.enabled), Is.False);
                renderer.color = Color.white;
                view.Refresh(unit, true, true, false);
                Assert.That(go.GetComponentInChildren<MeshRenderer>().enabled, Is.True);
                view.enabled = false;
                Call(view, "OnDisable"); // EditMode unit test of the message contract.
                view.Refresh(unit, true, true, false);
                Assert.That(go.GetComponentsInChildren<MeshRenderer>().Any(r => r.enabled), Is.False);
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); }
        }

        [Test]
        public void ObjectHoverPreservesItsStateColourAndClearsOnDisable()
        {
            var go = new GameObject("lever");
            var texture = new Texture2D(2, 2);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * .5f);
            try
            {
                var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = sprite;
                var stateColour = new Color(.3f, .7f, .45f, .35f); renderer.color = stateColour;
                var view = go.AddComponent<CombatMapObjectView>();
                Set(view, "targetRenderer", renderer); Set(view, "isHovered", true);
                Call(view, "ApplyInspectTint");
                Assert.That(renderer.color, Is.EqualTo(stateColour));
                Assert.That(go.GetComponentsInChildren<SpriteRenderer>().Count(r => r != renderer && r.enabled), Is.EqualTo(8));
                Call(view, "OnDisable");
                Assert.That(go.GetComponentsInChildren<SpriteRenderer>().Any(r => r != renderer && r.enabled), Is.False);
                Assert.That(renderer.color, Is.EqualTo(stateColour));
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); }
        }

        [Test]
        public void ActiveTurnMarkerSurvivesHoverAndHidesForDownedUnits()
        {
            var go = new GameObject("actor");
            var texture = new Texture2D(2, 2);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * .5f);
            try
            {
                go.AddComponent<SpriteRenderer>().sprite = sprite;
                var view = go.AddComponent<CombatUnitVisualStateView>();
                var unit = new BattleUnit { IsPlayerSide = true, Character = new CharacterInstance { LifeState = CharacterLifeState.Alive } };
                view.Refresh(unit, true, true, false);
                var ring = go.GetComponentsInChildren<MeshRenderer>().Single(r => r.name == "Active turn foot ring");
                Assert.That(ring.enabled, Is.True);
                var outline = go.GetComponentsInChildren<SpriteRenderer>().First(r => r.name == "Visual State Outline");
                var block = new MaterialPropertyBlock(); outline.GetPropertyBlock(block);
                Assert.That(Vector4.Distance(block.GetColor("_OutlineColor"), HighlightStyle.Hover), Is.LessThan(.0001f),
                    "Shader colour round-trips retain the hover pigment within floating-point precision.");
                unit.Character.LifeState = CharacterLifeState.Downed;
                view.Refresh(unit, true, true, false);
                Assert.That(ring.enabled, Is.False);
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); }
        }
    }
}

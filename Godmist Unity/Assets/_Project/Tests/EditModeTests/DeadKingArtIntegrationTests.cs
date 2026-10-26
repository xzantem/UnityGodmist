using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Combat;
using Godmist.Data.Skills;
using Godmist.Editor;
using Godmist.Runtime.Combat;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Views.Characters;
using Godmist.Views.Combat;
using Godmist.Views.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public sealed class DeadKingArtIntegrationTests
    {
        private const string Root = "Assets/_Project/ScriptableObjects/Catacombs/";
        private static readonly string[] Directions = { "s", "se", "e", "ne", "n", "nw", "w", "sw" };
        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static Vector2 Direction(string d) => d switch { "s" => Vector2.down, "se" => new(1,-1), "e" => Vector2.right, "ne" => Vector2.one, "n" => Vector2.up, "nw" => new(-1,1), "w" => Vector2.left, _ => new(-1,-1) };
        private static CharacterDefinition King => AssetDatabase.LoadAssetAtPath<CharacterDefinition>(Root + "Enemies/CD_Catacombs_dead_king.asset");
        private static DeadKingArtSetup.Config Config => JsonUtility.FromJson<DeadKingArtSetup.Config>(File.ReadAllText("Assets/_Project/Scripts/Editor/DeadKingArtBindings.json"));

        [TestCase("enemy.catacombs.dead_king.enraged", 192, 44, .5f, .3f)]
        [TestCase("enemy.catacombs.spectral_knight", 64, 4, .25f, .125f)]
        public void EveryDirectionUsesNativeScaleAndManifestTiming(string id, int size, int pivot, float idle, float walk)
        {
            foreach (var d in Directions)
            foreach (var state in new[] { CharacterAnimationState.Idle, CharacterAnimationState.Walk })
            {
                Assert.That(CharacterAnimationResolver.TryResolveCharacter(id, Direction(d), state, out var clip), Is.True, id + d);
                Assert.That(clip.FrameCount, Is.EqualTo(4));
                for (var i = 0; i < 4; i++)
                {
                    Assert.That(clip.Frames[i].rect.size, Is.EqualTo(new Vector2(size, size)));
                    Assert.That(clip.Frames[i].pivot, Is.EqualTo(new Vector2(size/2f, pivot)));
                    Assert.That(clip.Frames[i].pixelsPerUnit, Is.EqualTo(32));
                    Assert.That(clip.GetFrameDuration(i), Is.EqualTo(state == CharacterAnimationState.Idle ? idle : walk).Within(.0001f));
                }
                Assert.That(CombatPoseResolver.TryResolveAction(id, "hit", Direction(d), out _), Is.True);
                Assert.That(CombatPoseResolver.TryResolveAction(id, "dodge", Direction(d), out _), Is.True);
            }
        }

        [Test]
        public void ImportedBytesProfilesAndAllSerializedBindingsMatchApprovedPack()
        {
            var config = Config;
            Assert.That(config.imports.Length, Is.EqualTo(275));
            foreach (var item in config.imports)
            {
                using var hash = SHA256.Create();
                Assert.That(BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(item.path))).Replace("-", "").ToLowerInvariant(), Is.EqualTo(item.sha256), item.path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(item.path);
                Assert.That(importer.textureType, Is.EqualTo(item.texture ? TextureImporterType.Default : TextureImporterType.Sprite), item.path);
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
                Assert.That(importer.GetPlatformTextureSettings("Standalone").format, Is.EqualTo(TextureImporterFormat.RGBA32));
                Assert.That(importer.mipmapEnabled, Is.False);
                Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None));
                Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(32));
                if (!item.texture)
                {
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(item.path);
                    Assert.That(sprite.pivot.x, Is.EqualTo(sprite.rect.width * item.pivotX).Within(.001f));
                    Assert.That(sprite.pivot.y, Is.EqualTo(sprite.rect.height * item.pivotY).Within(.001f));
                }
            }
            foreach (var binding in config.bindings)
            {
                var data = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(binding.asset));
                var field = data.FindProperty("<" + binding.field + ">k__BackingField") ?? data.FindProperty(binding.field);
                for (var i = 0; i < binding.sprites.Length; i++)
                    Assert.That((field.isArray ? field.GetArrayElementAtIndex(i) : field).objectReferenceValue,
                        Is.SameAs(AssetDatabase.LoadAssetAtPath<Sprite>(binding.sprites[i])), binding.asset + binding.field);
            }
            foreach (var binding in config.floats)
            {
                var data = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(binding.asset));
                Assert.That(data.FindProperty("<" + binding.field + ">k__BackingField").floatValue, Is.EqualTo(binding.value).Within(.0001f));
            }
        }

        [TestCase("dead_king", "enemy.catacombs.dead_king", 128)]
        [TestCase("dead_king_enraged", "enemy.catacombs.dead_king.enraged", 128)]
        [TestCase("spectral_knight", "enemy.catacombs.spectral_knight", 64)]
        public void DeathUsesSeparateNativeCorpseInDefinitionAndResolver(string file, string id, int size)
        {
            var definition = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(Root + "Enemies/CD_Catacombs_" + file + ".asset");
            Assert.That(definition.DeadSprite, Is.Not.Null);
            Assert.That(definition.DeadSprite.name, Does.Contain("corpse"));
            Assert.That(definition.DeadSprite.rect.width, Is.EqualTo(size));
            Assert.That(definition.DeadSprite.pivot, Is.EqualTo(new Vector2(size/2f, size/2f)));
            Assert.That(CharacterAnimationResolver.TryResolveCorpse(id, out var sprite), Is.True);
            Assert.That(sprite.rect.width, Is.EqualTo(size));
            Assert.That(sprite.pivot, Is.EqualTo(new Vector2(size/2f, size/2f)));
        }

        [Test]
        public void SevenSkillsStatusesMarkersAndWaveIconAreWired()
        {
            foreach (var name in new[] { "dead_king", "dead_king_enraged", "spectral_knight" })
            {
                var definition = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(Root + "Enemies/CD_Catacombs_" + name + ".asset");
                foreach (var skill in definition.StartingSkills.Append(definition.BasicAttackSkill).Where(s => s != null).Distinct())
                {
                    Assert.That(skill.Icon.rect.size, Is.EqualTo(new Vector2(32,32)), skill.SkillId);
                    foreach (var d in Directions)
                        Assert.That(CombatPoseResolver.TryResolveSkill(definition.Id, skill.SkillId, Direction(d), AttackType.Ranged, false, out _), Is.True, skill.SkillId+d);
                }
            }
            var glossary = AssetDatabase.LoadAssetAtPath<CombatTooltipGlossary>("Assets/_Project/ScriptableObjects/UI/CombatTooltipGlossary.asset");
            foreach (var id in new[] { "catacombs.dead_king.despair", "catacombs.dead_king.rage" })
            { Assert.That(glossary.TryGet(id, out var entry), Is.True); Assert.That(entry.Icon.rect.width, Is.EqualTo(32)); }
            foreach (var name in new[] { "Safe", "Dark" })
                Assert.That(AssetDatabase.LoadAssetAtPath<SummonedObjectDefinition>(Root + "Objects/SO_DeadKing"+name+".asset").WorldSprite.name, Does.Contain(name.ToLowerInvariant()));
            var texture = new Texture2D(2,2);
            try
            {
                texture.LoadImage(File.ReadAllBytes("Assets/Resources/Godmist/DeadKingArtV2/Icons/dead-king-2-icon-wave-32.png"));
                Assert.That(texture.GetPixels32().Any(p => p.r > p.g*1.4f && p.r > p.b*1.2f && p.r > 30), Is.False, "No red neighbouring-icon background.");
            }
            finally { Object.DestroyImmediate(texture); }
        }

        [Test]
        public void CommandIsTargetHeadOnlyAndBiggerPoseCanvasesRetainTheirFeetPivot()
        {
            Assert.That(CombatPoseResolver.TryResolveSkill(King.Id, DeadKingBossSystem.CommandId, Vector2.up, AttackType.Ranged, false, out var command), Is.True);
            Assert.That(command.Actor.rect.width, Is.EqualTo(192));
            Assert.That(command.Actor.pivot.y, Is.EqualTo(44).Within(.001f));
            Assert.That(command.Effects, Is.Empty);
            Assert.That(command.TargetHeadEffects.Length, Is.EqualTo(1));
            Assert.That(command.Direction, Is.EqualTo("s"));
            Assert.That(CombatPoseResolver.TryResolveSkill(King.SecondPhaseDefinition.Id, DeadKingBossSystem.BladeId, Vector2.up, AttackType.Ranged, false, out var blade), Is.True);
            Assert.That(blade.Actor.pivot.y, Is.EqualTo(44).Within(.001f));
            Assert.That(blade.EffectOffsets[0], Is.Not.EqualTo(Vector2.zero));
            Assert.That(King.TransformationFrames.Count, Is.EqualTo(6));
            Assert.That(King.TransformationFramesPerSecond, Is.EqualTo(5));
        }

        private static CharacterView View(GameObject parent, BattleUnit unit)
        {
            var go = new GameObject("Unit"); go.transform.SetParent(parent.transform);
            var body = go.AddComponent<SpriteRenderer>(); var view = go.AddComponent<CharacterView>();
            var data = new SerializedObject(view); data.FindProperty("bodyRenderer").objectReferenceValue = body; data.ApplyModifiedPropertiesWithoutUndo();
            view.Bind(unit); return view;
        }
        private static BattleUnit Boss() => new() { Character = new CharacterInstanceFactory().Create(King, King.ClassData, 10), Position = new GridPosition(2,2) };

        private sealed class FeedbackForwarder
        {
            private readonly CombatExecutionDriver driver;
            public FeedbackForwarder(CombatExecutionDriver driver) => this.driver = driver;
            public void Handle(CombatFeedbackEvent feedback) => typeof(CombatExecutionDriver)
                .GetMethod("HandlePoseFeedback", Private).Invoke(driver, new object[] { feedback });
        }

        private sealed class TurnEndCounter
        {
            public int Count;
            public void Ended(BattleUnit unit) => Count++;
        }
        [UnityTest]
        public IEnumerator StalledAiWaitEndsTurnAndReleasesActionGateAfterEightSeconds()
        {
            yield return new EnterPlayMode();
            var root=new GameObject("AI timeout regression");
            CombatExecutionDriver driver=null;
            try
            {
                var host=root.AddComponent<CombatSandboxBootstrap>();host.enabled=false;
                driver=new CombatExecutionDriver(host,null,null,null,null,null,null,8,8,0,0,0,null,null,new(),null,null,null);
                var boss=Boss();boss.CurrentActionPoints=10000;
                var session=new CombatSession { Grid=new BattleGrid(8,8),ActiveUnit=boss,
                    Units=new(){boss},MembershipChangePending=true };
                session.ActiveParticipant=new CombatTurnParticipant(boss);
                session.TurnParticipants.Add(session.ActiveParticipant);
                typeof(CombatExecutionDriver).GetProperty("Session").SetValue(driver,session);
                var ended=new TurnEndCounter();
                typeof(CombatExecutionDriver).GetField("turnSystem",Private).SetValue(driver,new CombatTurnSystem(onUnitTurnEnded:ended.Ended));
                typeof(CombatExecutionDriver).GetField("aiSystem",Private).SetValue(driver,new SimpleCombatAiSystem());
                var routine=(IEnumerator)typeof(CombatExecutionDriver).GetMethod("ExecuteAiTurnVisually",Private).Invoke(driver,new object[]{boss});
                LogAssert.Expect(LogType.Warning,new System.Text.RegularExpressions.Regex("AI_TURN_TIMEOUT:"));
                var started=Time.realtimeSinceStartupAsDouble;
                yield return routine;
                Assert.That(Time.realtimeSinceStartupAsDouble-started,Is.InRange(7.9,10));
                Assert.That(ended.Count,Is.EqualTo(1));Assert.That(driver.IsActionInProgress,Is.False);
                Assert.That(typeof(CombatExecutionDriver).GetField("activeAiTurnCoroutine",Private).GetValue(driver),Is.Null);
                Assert.That(session.MembershipChangePending,Is.True,"Membership remains owned by the coordinator.");
            }
            finally { driver?.Dispose();Object.Destroy(root); }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator PhaseEventPlaysAllSixSouthFramesOnceAndRestoresLogicalFacing()
        {
            yield return new EnterPlayMode();
            var root = new GameObject("King phase presentation regression");
            CombatExecutionDriver driver = null; DeadKingBossSystem owner = null;
            try
            {
                var boss = Boss(); var view = View(root, boss);
                var spawner = root.AddComponent<CombatUnitViewSpawner>(); spawner.enabled = false;
                ((Dictionary<BattleUnit,CharacterView>)typeof(CombatUnitViewSpawner).GetField("views",Private).GetValue(spawner)).Add(boss,view);
                var host = root.AddComponent<CombatSandboxBootstrap>(); host.enabled = false;
                driver = new CombatExecutionDriver(host,spawner,null,null,null,null,null,8,8,0,0,0,null,null,new(),null,null,null);
                var session = new CombatSession { Grid = new BattleGrid(8,8), ActiveUnit = boss, Units = new() { boss } };
                typeof(CombatExecutionDriver).GetProperty("Session").SetValue(driver, session);
                var feedback = new CombatFeedbackBus();
                // A method-group receiver survives EnterPlayMode; capturing driver in
                // a lambda hoists its closure before the test runner's domain reload.
                feedback.Published += new FeedbackForwarder(driver).Handle;
                owner = new DeadKingBossSystem(session,new CombatDamageSystem(),new StatusEffectSystem(),feedback:feedback);
                Assert.That(CombatPoseResolver.TryResolveSkill(King.Id,CatacombsBossSystem.GuardSkillId,Vector2.left,AttackType.Ranged,false,out var oldPose), Is.True);
                view.PlayCombatPose(oldPose,Vector2.left,3);
                boss.Character.CurrentHealth = (int)(boss.Character.Stats.MaxHealth.GetValue()*.5f);
                owner.CheckPhases();
                Assert.That(boss.Character.Definition, Is.SameAs(King));
                Assert.That(view.IsTransforming, Is.False);
                owner.BeginTurn(boss);
                Assert.That(boss.Character.Definition, Is.SameAs(King.SecondPhaseDefinition));
                Assert.That(boss.Character.Skills.Select(s=>s.SkillId), Is.EqualTo(King.SecondPhaseDefinition.StartingSkills.Select(s=>s.SkillId)));
                Assert.That(view.BodyRenderer.sprite, Is.SameAs(King.TransformationFrames[0]));
                Assert.That(view.HasCombatPose, Is.False); Assert.That(driver.IsActionInProgress, Is.True);
                view.PlayCombatPose(oldPose,Vector2.right,3);
                var halfway = Time.time + .7f;
                while (Time.time < halfway) yield return null;
                Assert.That(view.IsTransforming, Is.True); Assert.That(driver.IsActionInProgress, Is.True);
                Assert.That(view.FacingDirection, Is.EqualTo(Vector2.left));
                Assert.That(King.TransformationFrames, Does.Contain(view.BodyRenderer.sprite));
                var finish = Time.time + .65f;
                while (Time.time < finish) yield return null;
                Assert.That(view.IsTransforming, Is.False); Assert.That(driver.IsActionInProgress, Is.False);
                Assert.That(view.BodyRenderer.sprite.name, Does.StartWith("dead-king-enraged-w-idle"));
                owner.CheckPhases(); yield return null; Assert.That(view.IsTransforming, Is.False);
            }
            finally { owner?.Dispose(); driver?.Dispose(); Object.Destroy(root); }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator TargetMarkFollowsTargetWithoutFreezingItsBodyAndHidesWithFog()
        {
            yield return new EnterPlayMode();
            var root = new GameObject("Target mark regression");
            try
            {
                var unit = Boss(); var view = View(root,unit);
                Assert.That(CombatPoseResolver.TryResolveSkill(King.Id,DeadKingBossSystem.CommandId,Vector2.down,AttackType.Ranged,false,out var pose), Is.True);
                var player = CombatTargetEffectPlayer.Play(view.BodyRenderer,unit,pose.TargetHeadEffects,.3f);
                var layer = player.GetComponentInChildren<SpriteRenderer>();
                Assert.That(player.transform.parent, Is.SameAs(view.BodyRenderer.transform));
                view.BodyRenderer.enabled = false; yield return null; Assert.That(layer.enabled, Is.False);
                view.BodyRenderer.enabled = true; view.BodyRenderer.forceRenderingOff = true; yield return null; Assert.That(layer.forceRenderingOff, Is.True);
                view.BodyRenderer.forceRenderingOff = false; yield return null;
                Assert.That(layer.enabled, Is.True); Assert.That(view.BodyRenderer.sprite.name, Does.StartWith("dead-king-"));
                var finish = Time.time + .35f;
                while (Time.time < finish) yield return null;
                yield return null; Assert.That(player == null, Is.True);
                Assert.That(view.HasCombatPose, Is.False);
            }
            finally { Object.Destroy(root); }
            yield return new ExitPlayMode();
        }
    }
}

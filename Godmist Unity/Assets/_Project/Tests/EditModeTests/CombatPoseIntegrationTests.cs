using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.NPC;
using Godmist.Systems.Combat;
using Godmist.Views.Characters;
using Godmist.Views.Combat;
using Godmist.Views.NPC;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public class CombatPoseIntegrationTests
    {
        private readonly List<Object> created = new();
        private T Asset<T>() where T : ScriptableObject { var a = ScriptableObject.CreateInstance<T>(); created.Add(a); return a; }
        private static void Set(object obj, string field, object value) => obj.GetType()
            .GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(obj, value);
        private static void Call(object obj, string method, params object[] args) => obj.GetType()
            .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(obj, args);
        [TearDown] public void Cleanup() { foreach (var obj in created) if (obj != null) Object.DestroyImmediate(obj); created.Clear(); }

        [TestCase(true, false)] [TestCase(true, true)]
        [TestCase(false, false)] [TestCase(false, true)]
        public void FirstCastAndHitSurviveInitialLifeSyncAndStationaryMovement(bool borrowed, bool hit)
        {
            var root = new GameObject("First combat pose"); created.Add(root);
            var body = root.AddComponent<SpriteRenderer>();
            var definition = AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/ScriptableObjects/Characters/CD_Jerzy.asset");
            var character = new Godmist.Systems.Characters.CharacterInstanceFactory().Create(definition);
            var unit = new BattleUnit { Character = character, IsPlayerSide = true };
            Assert.That(CombatPoseResolver.TryResolveAction("jerzy", hit ? "hit" : "melee", Vector2.down, out var pose), Is.True);
            if (borrowed)
            {
                var view = root.AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
                Set(view, "bodyRenderer", body); view.Bind(character); view.BindCombat(unit);
                view.PlayCombatPose(pose, Vector2.down, 2);
                Call(view, "Update"); Call(view, "LateUpdate");
                view.Move(Vector2.zero); view.Move(new Vector2(.0001f, 0));
                Assert.That(view.HasCombatPose, Is.True);
                Assert.That(body.sprite, Is.SameAs(pose.Actor));
                view.Move(Vector2.right * .1f);
                Assert.That(view.HasCombatPose, Is.False, "An actual movement still cancels the action pose.");
            }
            else
            {
                var view = root.AddComponent<CharacterView>(); Set(view, "bodyRenderer", body); view.Bind(unit);
                Set(view, "hasDisplayedLifeState", false);
                view.PlayCombatPose(pose, Vector2.down, 2);
                view.SetWorldPosition(root.transform.position); view.Refresh(); Call(view, "Update");
                Assert.That(view.HasCombatPose, Is.True); Assert.That(body.sprite, Is.SameAs(pose.Actor));
            }
        }

        [TestCase(true, false)] [TestCase(false, false)] [TestCase(true, true)]
        public void RevealingAttackPublishesOneVisibleCastAndHudReceivesItBeforeLateUpdate(bool invisible, bool materialize)
        {
            var random = UnityEngine.Random.state;
            var root = new GameObject("Synchronous opening feedback"); created.Add(root);
            var host = root.AddComponent<CombatSandboxBootstrap>(); Set(host, "seamlessDungeon", true);
            var spawner = root.AddComponent<CombatUnitViewSpawner>();
            var definition = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(materialize
                ? "Assets/_Project/ScriptableObjects/Catacombs/Enemies/CD_Catacombs_wraith.asset"
                : "Assets/_Project/ScriptableObjects/Characters/CD_Jerzy.asset");
            BattleUnit Make(bool player, int x)
            {
                var c = new Godmist.Systems.Characters.CharacterInstanceFactory().Create(definition);
                c.CharacterId = Guid.NewGuid().ToString(); c.Stats.MaxHealth = new Stat(5000); c.CurrentHealth = 5000;
                c.Stats.MinAttack = new Stat(100); c.Stats.MaxAttack = new Stat(100);
                c.Stats.Dodge = new Stat(0); c.Stats.CritChance = new Stat(0); c.WeaponAccuracy = 10000; c.ResourcePool.CurrentValue = 200;
                return new BattleUnit { Character = c, IsPlayerSide = player, Position = new GridPosition(x, 2),
                    CurrentActionPoints = 10000, MaxActionPoints = 10000, MaxKnives = 5, AvailableKnives = 5 };
            }
            var actor = Make(false, 1); var target = Make(true, 3);
            if (invisible) actor.Character.PassiveEffects.Add(new Godmist.Runtime.Effects.PassiveEffectRuntime {
                EffectId = Godmist.Core.Constants.StatusEffectIds.Invisibility, Lifetime = PassiveEffectLifetime.Timed, RemainingDuration = 2 });
            var session = new CombatSession { IsSeamless = true, Grid = new BattleGrid(8, 6), ActiveUnit = actor };
            session.Units.AddRange(new[] { actor, target });
            var borrowed = (Dictionary<BattleUnit, Godmist.Views.Exploration.ExplorationCharacterView>)typeof(CombatUnitViewSpawner).GetField("borrowed", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(spawner);
            foreach (var unit in session.Units)
            {
                var node = new GameObject("Borrowed participant", typeof(SpriteRenderer)); node.transform.SetParent(root.transform);
                var view = node.AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>(); Set(view, "bodyRenderer", node.GetComponent<SpriteRenderer>());
                view.Bind(unit.Character); view.BindCombat(unit); borrowed.Add(unit, view);
            }
            var bus = new CombatFeedbackBus();
            using var driver = new CombatExecutionDriver(host, spawner, null, null, null, null, null, 8, 6, 0, 0, 0, null, null, new(), null, null, null);
            typeof(CombatExecutionDriver).GetProperty("Session").SetValue(driver, session); Set(driver, "feedbackBus", bus);
            bus.Published += (Action<CombatFeedbackEvent>)Delegate.CreateDelegate(typeof(Action<CombatFeedbackEvent>), driver,
                typeof(CombatExecutionDriver).GetMethod("HandlePoseFeedback", BindingFlags.NonPublic | BindingFlags.Instance));
            ((Dictionary<CombatSession, CombatExecutionDriver>)typeof(CombatSandboxBootstrap).GetField("drivers", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(host)).Add(session, driver);
            host.Focus(session);
            var hudObject = new GameObject("HUD"); hudObject.transform.SetParent(root.transform);
            var hud = hudObject.AddComponent<Godmist.Views.UI.CombatHudView>(); Set(hud, "sandboxBootstrap", host);
            var banner = new GameObject("Banner", typeof(RectTransform)); banner.transform.SetParent(hudObject.transform); banner.SetActive(false);
            var textObject = new GameObject("Text", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI)); textObject.transform.SetParent(banner.transform);
            var text = textObject.GetComponent<TMPro.TextMeshProUGUI>(); Set(hud, "skillCastBanner", banner); Set(hud, "skillCastText", text);
            // This is the synchronous focus hook used before CommitOpeningSkill executes.
            hud.RefreshSkillFeedbackBinding();
            var uses = 0; var damageEvents = 0;
            bus.Published += feedback => {
                if (feedback.Type == CombatFeedbackType.Damage && feedback.Target == target)
                {
                    damageEvents++;
                    Assert.That(StatusEffectSystem.IsInvisible(actor), Is.EqualTo(invisible), "Damage must keep its original invisibility bonuses/order.");
                }
                if (feedback.Type != CombatFeedbackType.SkillUsed || feedback.EffectName == "old session") return;
                uses++;
                Assert.That(StatusEffectSystem.IsInvisibleToPlayer(actor), Is.False);
                Assert.That(feedback.SkillPresentation.Value.Origin, Is.EqualTo(new GridPosition(1, 2)));
            };
            var skill = AssetDatabase.LoadAssetAtPath<SkillData>(materialize
                ? "Assets/_Project/ScriptableObjects/Catacombs/Skills/SD_Catacombs_materialize.asset"
                : "Assets/_Project/ScriptableObjects/Skills/SD_KnifeToss.asset");
            try
            {
                UnityEngine.Random.InitState(7401);
                Assert.That(new SkillExecutionSystem(statusEffectSystem: new StatusEffectSystem(), feedbackBus: bus).TryExecute(session, actor, skill, materialize ? actor : target), Is.True);
                Assert.That(uses, Is.EqualTo(1));
                Assert.That(damageEvents, materialize ? Is.EqualTo(0) : Is.GreaterThan(0));
                Assert.That(banner.activeSelf, Is.True, "No LateUpdate is needed to receive the opening cast.");
                Assert.That(text.text, Is.EqualTo(skill.DisplayName)); Assert.That(actor.AvailableKnives, Is.EqualTo(materialize ? 5 : 4));
                Call(borrowed[actor], "Update"); Call(borrowed[actor], "LateUpdate");
                Assert.That(borrowed[actor].HasCombatPose, Is.True, "The revealing cast must also show its actual actor pose.");
                host.Focus(null); hud.RefreshSkillFeedbackBinding();
                bus.Publish(new CombatFeedbackEvent(actor, CombatFeedbackType.SkillUsed, effectName: "old session"));
                Assert.That(banner.activeSelf, Is.False, "Old sessions cannot revive a stale banner.");
            }
            finally { UnityEngine.Random.state = random; }
        }

        [TestCase(true, true)] [TestCase(false, true)]
        [TestCase(true, false)] [TestCase(false, false)]
        public void DeathFinishesOnlyThePendingHitReactionThenReleasesPresentation(bool borrowed, bool hit)
        {
            var root = new GameObject("Finishing hit"); created.Add(root); var body = root.AddComponent<SpriteRenderer>();
            var definition = AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/ScriptableObjects/Characters/CD_Jerzy.asset");
            var character = new Godmist.Systems.Characters.CharacterInstanceFactory().Create(definition);
            var unit = new BattleUnit { Character = character };
            Assert.That(CombatPoseResolver.TryResolveAction("jerzy", hit ? "hit" : "melee", Vector2.down, out var pose), Is.True);
            object view;
            if (borrowed)
            {
                var actor = root.AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>(); Set(actor, "bodyRenderer", body);
                actor.Bind(character); actor.BindCombat(unit); actor.PlayCombatPose(pose, Vector2.down, 2); view = actor;
            }
            else
            {
                var actor = root.AddComponent<CharacterView>(); Set(actor, "bodyRenderer", body);
                actor.Bind(unit); actor.PlayCombatPose(pose, Vector2.down, 2); view = actor;
            }
            character.CurrentHealth = 0; character.LifeState = CharacterLifeState.Dead;
            Call(view, "Update"); if (borrowed) Call(view, "LateUpdate");
            var player = (CombatPosePlayer)view.GetType().GetField("combatPose", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);
            Assert.That(player.IsPlaying, Is.EqualTo(hit), "Death should finish an impact, not retain a casting pose.");
            if (hit) Assert.That(body.sprite, Is.SameAs(pose.Actor));
            player.Tick(Time.time + 3); Assert.That(player.IsPlaying, Is.False);
        }

        [TestCase("CD_Jerzy", "skill.knife_toss", false)]
        [TestCase("CD_Mage", "skill.mage.energy_orb", false)]
        [TestCase("CD_Paladin", "skill.paladin.verdict", false)]
        [TestCase("CD_Wladimir_Test", "skill.warrior.cut", false)]
        [TestCase("CD_Mage", "skill.mage.energy_orb", true)]
        public void OpeningActionRendersCastAndHitAfterTheFirstBorrowedViewUpdate(string asset, string skillId, bool lethal)
        {
            var root = new GameObject("Opening pose render"); created.Add(root);
            var spawner = root.AddComponent<CombatUnitViewSpawner>(); var host = root.AddComponent<CombatSandboxBootstrap>();
            var definition = AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/ScriptableObjects/Characters/" + asset + ".asset");
            var enemyDefinition = AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/ScriptableObjects/Catacombs/Enemies/CD_Catacombs_undead.asset");
            var factory = new Godmist.Systems.Characters.CharacterInstanceFactory();
            var actor = new BattleUnit { Character = factory.Create(definition), IsPlayerSide = true, Position = new GridPosition(1, 3) };
            var defender = new BattleUnit { Character = factory.Create(enemyDefinition), Position = new GridPosition(3, 3) };
            var session = new CombatSession { IsSeamless = true, ActiveUnit = actor, Units = new List<BattleUnit> { actor, defender } };
            var borrowed = (Dictionary<BattleUnit, Godmist.Views.Exploration.ExplorationCharacterView>)typeof(CombatUnitViewSpawner).GetField("borrowed", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(spawner);
            foreach (var unit in session.Units)
            {
                var node = new GameObject("Borrowed actor", typeof(SpriteRenderer)); node.transform.SetParent(root.transform);
                node.transform.position = new Vector3(unit == actor ? 0 : 2, 0);
                var view = node.AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
                Set(view, "bodyRenderer", node.GetComponent<SpriteRenderer>()); view.Bind(unit.Character); view.BindCombat(unit); borrowed.Add(unit, view);
            }
            using var driver = new CombatExecutionDriver(host, spawner, null, null, null, null, null, 8, 6, 0, 0, 0, null, null, new(), null, null, null);
            typeof(CombatExecutionDriver).GetProperty("Session").SetValue(driver, session);
            var skill = Asset<SkillData>(); Set(skill, "<SkillId>k__BackingField", skillId);
            var cameraNode = new GameObject("Render camera", typeof(Camera)); cameraNode.transform.SetParent(root.transform);
            var camera = cameraNode.GetComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 1.65f;
            camera.aspect = 2; camera.transform.position = new Vector3(1, 1.2f, -10); camera.cullingMask = 1 << 30;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.08f, .09f, .1f); camera.enabled = false;
            var rt = new RenderTexture(640, 320, 24); var pixels = new Texture2D(640, 320, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            var output = System.IO.Path.GetFullPath("../LocalHandoffs/combat-action-presentation-20261020"); System.IO.Directory.CreateDirectory(output);
            byte[] Capture(string suffix)
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 30;
                UnityEngine.Rendering.SortingGroup.UpdateAllSortingGroups(); camera.targetTexture = rt; camera.Render();
                RenderTexture.active = rt; pixels.ReadPixels(new Rect(0, 0, 640, 320), 0, 0); pixels.Apply();
                var png = pixels.EncodeToPNG(); System.IO.File.WriteAllBytes(System.IO.Path.Combine(output, asset + (lethal ? "-lethal" : "") + "-" + suffix + ".png"), png); return png;
            }
            try
            {
                var idle = Capture("idle");
                Call(driver, "HandlePoseFeedback", new CombatFeedbackEvent(actor, CombatFeedbackType.SkillUsed,
                    skillPresentation: new CombatSkillPresentation(actor, skill, defender.Position, AttackType.Ranged)));
                Call(driver, "HandlePoseFeedback", new CombatFeedbackEvent(defender, CombatFeedbackType.Damage, 10,
                    source: actor));
                Assert.That(CombatPoseResolver.TryResolveSkill(definition.Id, skillId, Vector2.right, AttackType.Ranged, false, out var cast), Is.True);
                Assert.That(CombatPoseResolver.TryResolveAction(enemyDefinition.Id, "hit", Vector2.left, out var hit), Is.True);
                Assert.That(borrowed[actor].GetComponent<SpriteRenderer>().sprite, Is.SameAs(cast.Actor));
                Assert.That(borrowed[defender].GetComponent<SpriteRenderer>().sprite, Is.SameAs(hit.Actor));
                var immediate = Capture("cast-hit");
                if (lethal) { defender.Character.CurrentHealth = 0; defender.Character.LifeState = CharacterLifeState.Dead; }
                foreach (var view in borrowed.Values) { Call(view, "Update"); Call(view, "LateUpdate"); Assert.That(view.HasCombatPose, Is.True); }
                var afterUpdate = Capture("after-update");
                Assert.That(afterUpdate, Is.EqualTo(immediate), "The rendered opening poses must survive the first presentation tick.");
                Assert.That(afterUpdate, Is.Not.EqualTo(idle));
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(pixels);
            }
        }

        #pragma warning disable 0649
        [Serializable] private class Catalog { public Pack[] packs; }
        [Serializable] private class Pack { public string unit; public int actorSize; public int pivotBottomPixels; public Frame[] frames; public Skill[] skills; }
        [Serializable] private class Frame { public string action; public string direction; public Layer[] layers; }
        [Serializable] private class Layer { public bool actor; public int canvasSize; public bool customPivot, targetHead; public float pivotX, pivotY, offsetX, offsetY; }
        [Serializable] private class Skill { public string id; public string action; public string meleeAction; public bool frontOnly; }
        #pragma warning restore 0649
        private static string Id(string unit) => unit switch
        {
            "alison" => "character.mage", "reimond" => "character.paladin", "jerzy" => "jerzy", "wladimir" => "wladimir",
            "skeleton-swordsman" => "enemy.catacombs.swordsman", "skeleton-archer" => "enemy.catacombs.archer",
            "skeleton-defender" => "enemy.catacombs.defender", "skeleton-occultist" => "enemy.catacombs.occultist",
            "undead-fleshy" => "enemy.catacombs.undead", "ghoul-humanoid" => "enemy.catacombs.ghoul",
            "dead-king" => "enemy.catacombs.dead_king",
            "dead-king-enraged" => "enemy.catacombs.dead_king.enraged",
            "spectral-knight" => "enemy.catacombs.spectral_knight",
            "bandit-cutthroat" => "enemy.forest.bandit_thug",
            "bandit-shooter" => "enemy.forest.bandit_shooter",
            "elf-archer" => "enemy.forest.elf_archer",
            "elf-druid" => "enemy.forest.elf_druid",
            "elf-poacher" => "enemy.forest.elf_poacher",
            "rabid-wolf" => "enemy.forest.rabid_wolf",
            "infected-tree" => "enemy.forest.infected_tree",
            "infected-ent-p1" => "enemy.forest.infected_ent",
            "infected-ent-p2" => "enemy.forest.infected_ent.burning",
 _ => "enemy.catacombs.wraith"
        };
        private static Vector2 Direction(string d) => d switch
        { "s" => Vector2.down, "se" => new(1,-1), "e" => Vector2.right, "ne" => Vector2.one,
          "n" => Vector2.up, "nw" => new(-1,1), "w" => Vector2.left, _ => new(-1,-1) };

        [TestCase(false)]
        [TestCase(true)]
        public void AnimationLookupRecreatesFramesAfterUnityDestroysCachedSprites(bool npc)
        {
            bool Resolve(out CharacterAnimationClip clip) => npc
                ? CharacterAnimationResolver.TryResolveNpcIdle("npc.arungard.blacksmith_01", out clip)
                : CharacterAnimationResolver.TryResolveCharacter("jerzy", Vector2.down, CharacterAnimationState.Idle, out clip);
            Assert.That(Resolve(out var before), Is.True);
            var previousFrame = before.Frames[0];
            Object.DestroyImmediate(previousFrame);
            Assert.That(Resolve(out var after), Is.True);
            Assert.That(after.Frames, Has.All.Not.Null);
            Assert.That(after.Frames[0] == null, Is.False);
            Assert.That(after.Frames[0], Is.Not.SameAs(previousFrame));
            Assert.That(after.Frames[0].texture.filterMode, Is.EqualTo(FilterMode.Point));
        }

        [Test]
        public void EveryImportedCharacterTextureUsesPointAndUncompressedNativePixels()
        {
            var paths = AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/Resources/Godmist/CharacterAnimations","Assets/Resources/Godmist/CombatPoses"})
                .Select(AssetDatabase.GUIDToAssetPath).Where(path=>path.EndsWith(".png",StringComparison.OrdinalIgnoreCase)).ToArray();
            // Retired overlays may be removed; every remaining source must still import.
            var sourcePaths = new[]{"Assets/Resources/Godmist/CharacterAnimations", "Assets/Resources/Godmist/CombatPoses"}
                .SelectMany(root => System.IO.Directory.GetFiles(root, "*.png", System.IO.SearchOption.AllDirectories))
                .Select(path => path.Replace('\\', '/')).ToArray();
            Assert.That(paths, Is.Not.Empty);
            Assert.That(paths, Is.EquivalentTo(sourcePaths));
            foreach (var path in paths)
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.filterMode,Is.EqualTo(FilterMode.Point),path);
                Assert.That(importer.textureType,Is.EqualTo(TextureImporterType.Default),path);
                Assert.That(importer.textureCompression,Is.EqualTo(TextureImporterCompression.Uncompressed),path);
                Assert.That(importer.mipmapEnabled,Is.False,path);
                Assert.That(importer.npotScale,Is.EqualTo(TextureImporterNPOTScale.None),path);
                var platform=importer.GetPlatformTextureSettings("Standalone");
                Assert.That(platform.overridden,Is.True,path);
                Assert.That(platform.format,Is.EqualTo(TextureImporterFormat.RGBA32),path);
                Assert.That(platform.textureCompression,Is.EqualTo(TextureImporterCompression.Uncompressed),path);
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.That(texture.filterMode,Is.EqualTo(FilterMode.Point),path);
                Assert.That(texture.mipmapCount,Is.EqualTo(1),path);
                var header=System.IO.File.ReadAllBytes(path);
                var width=(header[16]<<24)|(header[17]<<16)|(header[18]<<8)|header[19];
                var height=(header[20]<<24)|(header[21]<<16)|(header[22]<<8)|header[23];
                Assert.That(texture.width,Is.EqualTo(width),path);Assert.That(texture.height,Is.EqualTo(height),path);
            }
        }

        [TestCase("CombatPoses")][TestCase("CharacterAnimations")]
        public void ExistingWrongImportIsRepairedWithoutChangingGuidAndRepairIsIdempotent(string folder)
        {
            var path="Assets/Resources/Godmist/"+folder+"/ImportProfileRegression-"+Guid.NewGuid().ToString("N")+".png";
            try
            {
                System.IO.File.Copy("Assets/Resources/Godmist/CombatPoses/jerzy/jerzy-melee-s-64.png",path);
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var guid=AssetDatabase.AssetPathToGUID(path);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.filterMode=FilterMode.Bilinear;importer.mipmapEnabled=true;
                importer.textureType=TextureImporterType.Sprite;
                importer.textureCompression=TextureImporterCompression.Compressed;
                var platform=importer.GetPlatformTextureSettings("Standalone");
                platform.overridden=true;platform.format=TextureImporterFormat.DXT5;
                platform.textureCompression=TextureImporterCompression.Compressed;
                importer.SetPlatformTextureSettings(platform);
                // Persist the pre-existing bad meta without triggering OnPreprocessTexture.
                AssetDatabase.WriteImportSettingsIfDirty(path);
                Godmist.Editor.CharacterAnimationTextureImporter.ApplyImportedProfiles();
                importer=(TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.filterMode,Is.EqualTo(FilterMode.Point));
                Assert.That(importer.textureCompression,Is.EqualTo(TextureImporterCompression.Uncompressed));
                Assert.That(importer.textureType,Is.EqualTo(TextureImporterType.Default));
                Assert.That(importer.mipmapEnabled,Is.False);
                Assert.That(importer.GetPlatformTextureSettings("Standalone").format,Is.EqualTo(TextureImporterFormat.RGBA32));
                Assert.That(AssetDatabase.AssetPathToGUID(path),Is.EqualTo(guid));
                var metadata=System.IO.File.ReadAllText(path+".meta");
                Godmist.Editor.CharacterAnimationTextureImporter.ApplyImportedProfiles();
                Assert.That(System.IO.File.ReadAllText(path+".meta"),Is.EqualTo(metadata));
            }
            finally { AssetDatabase.DeleteAsset(path); }
        }

        [Test]
        public void EveryApprovedPoseAndSkillResolvesWithNativeDimensionsAndRegisteredPivots()
        {
            var catalog = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("Godmist/CombatPoses/catalog").text);
            Assert.That(catalog.packs.Length, Is.EqualTo(23));
            Assert.That(catalog.packs.Sum(p => p.frames.Length), Is.EqualTo(976));
            foreach (var pack in catalog.packs)
            {
                foreach (var frame in pack.frames)
                {
                    Assert.That(CombatPoseResolver.TryResolveAction(Id(pack.unit), frame.action, Direction(frame.direction), out var pose), Is.True, pack.unit+":"+frame.action+":"+frame.direction);
                    var size = pack.actorSize;
                    var actorCanvas = frame.layers.Single(layer => layer.actor).canvasSize;
                    Assert.That(pose.Actor.rect.width, Is.EqualTo(actorCanvas));
                    Assert.That(pose.Actor.pixelsPerUnit, Is.EqualTo(32));
                    Assert.That(pose.Actor.texture.filterMode, Is.EqualTo(FilterMode.Point));
                    for (var effectIndex = 0; effectIndex < pose.Effects.Length; effectIndex++)
                    {
                        var fx = pose.Effects[effectIndex];
                        var layer = frame.layers.Where(layer => !layer.actor && !layer.targetHead).ElementAt(effectIndex);
                        var effectCanvas = layer.canvasSize;
                        Assert.That(fx.rect.width, Is.EqualTo(effectCanvas));
                        if (layer.customPivot)
                            Assert.That(fx.pivot, Is.EqualTo(new Vector2(layer.pivotX, layer.pivotY) * effectCanvas));
                        else
                            Assert.That(fx.pivot.y - pose.Actor.pivot.y, Is.EqualTo((effectCanvas - actorCanvas) / 2f).Within(.001f));
                        Assert.That(pose.EffectOffsets[effectIndex], Is.EqualTo(new Vector2(layer.offsetX, layer.offsetY)));
                        Assert.That(fx.texture.filterMode, Is.EqualTo(FilterMode.Point));
                        var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(fx.texture));
                        Assert.That(importer.mipmapEnabled, Is.False);
                        Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None));
                        Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
                    }
                }
                foreach (var skill in pack.skills)
                {
                    Assert.That(CombatPoseResolver.TryResolveSkill(Id(pack.unit), skill.id, Vector2.up, AttackType.Ranged, false, out var pose), Is.True, skill.id);
                    Assert.That(pose.Action, Is.EqualTo(skill.action));
                    Assert.That(pose.Direction, Is.EqualTo(skill.frontOnly ? "s" : "n"));
                    foreach (var direction in new[] { "s", "se", "e", "ne", "n", "nw", "w", "sw" })
                        foreach (var type in new[] { AttackType.Melee, AttackType.Ranged })
                        {
                            Assert.That(CombatPoseResolver.TryResolveSkill(Id(pack.unit), skill.id, Direction(direction), type, false, out var facingPose),
                                Is.True, pack.unit + ":" + skill.id + ":" + direction + ":" + type);
                            Assert.That(facingPose.Action, Is.EqualTo(type == AttackType.Melee && !string.IsNullOrEmpty(skill.meleeAction) ? skill.meleeAction : skill.action));
                        }
                }
            }
        }

        [Test]
        public void HelperAndDownedCatalogResolvesAllDirectionsWithoutReplacingBaseCatalog()
        {
            var special = Resources.Load<TextAsset>("Godmist/CombatPoses/hero-special-catalog");
            Assert.That(special, Is.Not.Null);
            var catalog = JsonUtility.FromJson<Catalog>(special.text);
            Assert.That(catalog.packs.Select(pack => pack.unit), Is.EquivalentTo(new[] { "jerzy", "wladimir", "alison", "reimond" }));

            foreach (var pack in catalog.packs)
            {
                Assert.That(pack.frames, Has.Length.EqualTo(9), pack.unit);
                Assert.That(CombatPoseResolver.TryResolveDowned(Id(pack.unit), out var downed), Is.True, pack.unit);
                Assert.That(downed.Direction, Is.EqualTo("s"));

                foreach (var direction in new[] { "s", "se", "e", "ne", "n", "nw", "w", "sw" })
                {
                    Assert.That(CombatPoseResolver.TryResolveHelpUp(Id(pack.unit), Direction(direction), out var helper), Is.True,
                        pack.unit + ":" + direction);
                    Assert.That(helper.Direction, Is.EqualTo(direction));
                    Assert.That(helper.Actor.texture.filterMode, Is.EqualTo(FilterMode.Point));
                    Assert.That(helper.Actor.pixelsPerUnit, Is.EqualTo(32));
                }
            }

            // Loading the special catalog must leave the existing skill/action packs intact.
            Assert.That(CombatPoseResolver.TryResolveSkill("jerzy", "skill.knife_toss", Vector2.up,
                AttackType.Ranged, false, out var existingSkill), Is.True);
            Assert.That(existingSkill.Action, Is.EqualTo("throw"));
        }

        [Test]
        public void DownedPoseRehydratesAfterCharacterViewDisableAndEnable()
        {
            var root = new GameObject("downed lifecycle");
            created.Add(root);
            var body = root.AddComponent<SpriteRenderer>();
            var view = root.AddComponent<CharacterView>();
            Set(view, "bodyRenderer", body);
            var definition = AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/ScriptableObjects/Characters/CD_Jerzy.asset");
            var character = new Godmist.Systems.Characters.CharacterInstanceFactory().Create(definition);
            character.LifeState = CharacterLifeState.Downed;
            var unit = new BattleUnit { Character = character };

            Assert.That(CombatPoseResolver.TryResolveDowned("jerzy", out var downed), Is.True);
            view.Bind(unit);
            Assert.That(view.HasCombatPose, Is.True);
            Assert.That(body.sprite, Is.SameAs(downed.Actor));

            root.SetActive(false);
            root.SetActive(true);
            Call(view, "Update");

            Assert.That(view.HasCombatPose, Is.True);
            Assert.That(body.sprite, Is.SameAs(downed.Actor));
        }

        [Test]
        public void ExplorationHelperPoseCancelsOnMovementAndRebind()
        {
            var root = new GameObject("helper lifecycle");
            created.Add(root);
            var body = root.AddComponent<SpriteRenderer>();
            var view = root.AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
            Set(view, "bodyRenderer", body);
            var definition = AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/ScriptableObjects/Characters/CD_Jerzy.asset");
            var character = new Godmist.Systems.Characters.CharacterInstanceFactory().Create(definition);
            view.Bind(character);

            view.PlayHelpUpPose(Vector2.right, 5f);
            Assert.That(view.HasCombatPose, Is.True);
            view.Move(Vector2.right);
            Assert.That(view.HasCombatPose, Is.False);

            view.PlayHelpUpPose(Vector2.left, 5f);
            Assert.That(view.HasCombatPose, Is.True);
            view.Bind(character);
            Assert.That(view.HasCombatPose, Is.False);
        }

        [TestCase(AttackType.Melee, false, "melee")]
        [TestCase(AttackType.Ranged, false, "throw")]
        [TestCase(AttackType.Ranged, true, "extract")]
        public void ScoutChoosesActualAttackAndRecoveryPose(AttackType type, bool recovery, string action)
        {
            Assert.That(CombatPoseResolver.TryResolveSkill("jerzy", "skill.knife_toss", new Vector2(-1,1), type, recovery, out var pose), Is.True);
            Assert.That(pose.Action, Is.EqualTo(action)); Assert.That(pose.Direction, Is.EqualTo("nw"));
            if (action == "throw") Assert.That(pose.Effects.Length, Is.EqualTo(2), "air wake and released dagger are separate layers");
        }

        [Test]
        public void TileAimUsesSnapshotEvenAfterTeleportAndNorthIsDistinctFromNorthEast()
        {
            var unit = new BattleUnit { Position = new GridPosition(2,2) };
            var snapshot = new CombatSkillPresentation(unit, null, new GridPosition(1,3));
            unit.Position = new GridPosition(1,3);
            Assert.That(CombatPoseResolver.Facing(snapshot, Vector2.right), Is.EqualTo(new Vector2(-1,1).normalized));
            Assert.That(CombatPoseResolver.TryResolveAction("jerzy", "melee", Vector2.up, out var n), Is.True);
            Assert.That(CombatPoseResolver.TryResolveAction("jerzy", "melee", Vector2.one, out var ne), Is.True);
            Assert.That(n.Actor, Is.Not.SameAs(ne.Actor));
        }

        [Test]
        public void FreezeOverridesIdleAndClearsEffectsOnExpiryAndVisibilityChange()
        {
            var root = new GameObject("pose player"); created.Add(root);
            var body = root.AddComponent<SpriteRenderer>();
            CharacterAnimationResolver.TryResolveCharacter("jerzy", Vector2.down, CharacterAnimationState.Idle, out var idle);
            CombatPoseResolver.TryResolveAction("jerzy", "throw", Vector2.down, out var pose);
            body.sprite = idle.Frames[0];
            using var player = new CombatPosePlayer(body);
            var now = Time.time; player.Play(pose, 2);
            body.sprite = idle.Frames[1]; Assert.That(player.Tick(now+1.9f), Is.True); Assert.That(body.sprite, Is.SameAs(pose.Actor));
            body.forceRenderingOff = true; player.Tick(now+1.9f);
            Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Where(x=>x!=body).All(x=>!x.enabled && x.forceRenderingOff), Is.True);
            body.forceRenderingOff = false; player.Tick(now+1.9f);
            Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Where(x=>x!=body).All(x=>x.enabled && !x.forceRenderingOff), Is.True);
            Assert.That(player.Tick(now+2.01f), Is.False); Assert.That(body.sprite, Is.SameAs(idle.Frames[0]));
            Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Where(x=>x!=body).All(x=>!x.enabled), Is.True);
        }

        [TestCase("blacksmith")][TestCase("alchemist")][TestCase("enchanter")][TestCase("priest")][TestCase("gate-guardian")]
        public void NpcWorkStopsImmediatelyForItsDialogueAndResumesAtEnd(string unit)
        {
            var id = "npc.arungard."+(unit=="gate-guardian" ? "questgiver" : unit)+"_01";
            Assert.That(CharacterAnimationResolver.TryResolveNpcWork(id, out var work), Is.True); Assert.That(work.FrameCount, Is.EqualTo(8));
            Assert.That(CharacterAnimationResolver.TryResolveNpcIdle(id, out var idle), Is.True); Assert.That(idle.FrameCount, Is.EqualTo(4));
            var root = new GameObject("npc"); created.Add(root); var body=root.AddComponent<SpriteRenderer>(); var view=root.AddComponent<NpcView>();
            Set(view,"bodyRenderer",body); var npc=new NpcInstance { NpcId=id };
            view.Initialize(null,npc); Assert.That(body.sprite, Is.SameAs(work.Frames[0]));
            Call(view,"HandleAnimationDialogueStarted",new DialogueSession(new NpcInstance { NpcId="other" },null,null,"player"));
            Assert.That(body.sprite, Is.SameAs(work.Frames[0]));
            Call(view,"HandleAnimationDialogueStarted",new DialogueSession(npc,null,null,"player"));
            Assert.That(body.sprite, Is.SameAs(idle.Frames[0]));
            Call(view,"HandleAnimationDialogueEnded",npc); Assert.That(body.sprite, Is.SameAs(work.Frames[0]));
        }

        [TestCase(false)][TestCase(true)]
        public void MovementCameraFollowsInactiveHiddenEnemyStepsButNotPlayerMovement(bool player)
        {
            var root = new GameObject("movement camera"); created.Add(root);
            var host = root.AddComponent<CombatSandboxBootstrap>(); var spawner = root.AddComponent<CombatUnitViewSpawner>();
            var cameraRoot = new GameObject("camera"); cameraRoot.transform.SetParent(root.transform);
            var camera = cameraRoot.AddComponent<Camera>(); camera.orthographic = true; camera.aspect = 1.6f;
            camera.transform.position = new Vector3(0, 0, -10);
            var framer = cameraRoot.AddComponent<CombatCameraFramer>(); Set(framer, "worldCamera", camera);
            framer.CanPresentUnit = _ => false;
            var unit = new BattleUnit { IsPlayerSide = player, Position = new GridPosition(6, 3) };
            var session = new CombatSession { ActiveUnit = unit }; session.Units.Add(unit);
            var actor = new GameObject("hidden actor"); actor.transform.SetParent(root.transform);
            var view = actor.AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>(); actor.SetActive(false);
            var borrowed = (Dictionary<BattleUnit, Godmist.Views.Exploration.ExplorationCharacterView>)typeof(CombatUnitViewSpawner)
                .GetField("borrowed", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(spawner);
            borrowed.Add(unit, view);
            framer.Focus(session, new Bounds(Vector3.zero, new Vector3(20, 10)), new Bounds(Vector3.zero, new Vector3(200, 100)), _ => Vector3.zero);
            framer.TickTransition(10); var before = camera.transform.position; var zoom = camera.orthographicSize;
            using var driver = new CombatExecutionDriver(host, spawner, null, null, null, null, null, 4, 4, 0, 0, 0, null, null, new(), null, framer, null);
            typeof(CombatExecutionDriver).GetProperty("Session").SetValue(driver, session);
            var iterator = (System.Collections.IEnumerator)typeof(CombatExecutionDriver).GetMethod("PlayMovementPath", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(driver, new object[] { unit, new[] { new GridPosition(5, 3), new GridPosition(6, 3) } });
            try
            {
                Assert.That(iterator.MoveNext(), Is.True);
                if (!player)
                {
                    framer.TickTransition(10);
                    Assert.That(camera.transform.position.x, Is.EqualTo(5).Within(.01));
                }
                Call(spawner, "AdvancePresentationPath", unit, 1f / 60);
                Assert.That(iterator.MoveNext(), Is.True);
                if (!player)
                {
                    framer.TickTransition(10);
                    Assert.That(camera.transform.position.x, Is.EqualTo(6).Within(.01));
                }
                Call(spawner, "AdvancePresentationPath", unit, 1f / 60);
                Assert.That(iterator.MoveNext(), Is.False); framer.TickTransition(10);
                Assert.That(camera.transform.position.x, Is.EqualTo(player ? before.x : 6).Within(.01));
                Assert.That(camera.orthographicSize, Is.EqualTo(zoom).Within(.01));
                Assert.That(actor.activeSelf, Is.False);
                Assert.That(actor.transform.position, Is.EqualTo(Vector3.zero));
                Assert.That(spawner.IsPresentationMovementInProgress(unit), Is.False);
            }
            finally { (iterator as IDisposable)?.Dispose(); }
        }

        [Test]
        public void RangedPresentationFramesActualCasterAndAllTargetsEvenWithoutAnAuthoredPose()
        {
            var root = new GameObject("ranged frame"); created.Add(root);
            var host = root.AddComponent<CombatSandboxBootstrap>();
            var spawner = root.AddComponent<CombatUnitViewSpawner>();
            var cameraRoot = new GameObject("camera"); cameraRoot.transform.SetParent(root.transform);
            var camera = cameraRoot.AddComponent<Camera>(); camera.orthographic = true; camera.aspect = 1.6f;
            camera.transform.position = new Vector3(0, 0, -10);
            var framer = cameraRoot.AddComponent<CombatCameraFramer>(); Set(framer, "worldCamera", camera);
            framer.ActionSafeViewport = _ => new Rect(0, .22f, 1, .43f);
            var definition = Asset<CharacterDefinition>();
            var actor = new BattleUnit { Character = new CharacterInstance { Definition = definition } };
            var target = new BattleUnit { Character = new CharacterInstance { Definition = definition } };
            var other = new BattleUnit { Character = new CharacterInstance { Definition = definition } };
            var session = new CombatSession { ActiveUnit = actor };
            session.Units.AddRange(new[] { actor, target, other });
            var views = (Dictionary<BattleUnit, CharacterView>)typeof(CombatUnitViewSpawner).GetField("views", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(spawner);
            var positions = new[] { new Vector3(0, 0), new Vector3(35, 7), new Vector3(-8, -6) };
            for (var i = 0; i < session.Units.Count; i++)
            {
                var child = new GameObject("participant"); child.transform.SetParent(root.transform); child.transform.position = positions[i];
                var view = child.AddComponent<CharacterView>(); views.Add(session.Units[i], view);
            }
            framer.Focus(session, new Bounds(Vector3.zero, new Vector3(20, 10)), new Bounds(Vector3.zero, new Vector3(200, 100)), _ => positions[0]);
            framer.TickTransition(10);
            using var driver = new CombatExecutionDriver(host, spawner, null, null, null, null, null, 4, 4, 0, 0, 0, null, null, new(), null, framer, null);
            typeof(CombatExecutionDriver).GetProperty("Session").SetValue(driver, session);
            var skill = Asset<SkillData>();
            Call(driver, "HandlePoseFeedback", new CombatFeedbackEvent(actor, CombatFeedbackType.SkillUsed,
                skillPresentation: new CombatSkillPresentation(actor, skill, targets: new[] { target, other })));
            framer.TickTransition(10);
            foreach (var unit in session.Units)
            {
                Assert.That(spawner.TryGetPresentationBounds(unit, out var bounds), Is.True);
                var min = camera.WorldToViewportPoint(bounds.min); var max = camera.WorldToViewportPoint(bounds.max);
                Assert.That(min.x, Is.GreaterThan(0)); Assert.That(max.x, Is.LessThan(1));
                Assert.That(min.y, Is.GreaterThan(.22f)); Assert.That(max.y, Is.LessThan(.65f));
            }
            Assert.That(driver.IsActionInProgress, Is.True);
        }

        [Test]
        public void DriverPresentsActorAndDefenderAndBlocksFurtherActionsUntilPresentationEnds()
        {
            var root = new GameObject("combat presentation"); created.Add(root);
            var host = root.AddComponent<CombatSandboxBootstrap>();
            var spawner = root.AddComponent<CombatUnitViewSpawner>();
            var definition = AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/ScriptableObjects/Characters/CD_Jerzy.asset");
            var actor = new BattleUnit { Character = new Godmist.Systems.Characters.CharacterInstanceFactory().Create(definition), IsPlayerSide = true, Position = new(1,1) };
            var defender = new BattleUnit { Character = new Godmist.Systems.Characters.CharacterInstanceFactory().Create(definition), Position = new(2,2) };
            var session = new CombatSession { IsSeamless = true, ActiveUnit = actor }; session.Units.AddRange(new[]{actor,defender});
            var views = (Dictionary<BattleUnit,CharacterView>)typeof(CombatUnitViewSpawner).GetField("views",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(spawner);
            foreach (var unit in session.Units)
            {
                var child = new GameObject("actor"); child.transform.SetParent(root.transform);
                var body = child.AddComponent<SpriteRenderer>(); var view = child.AddComponent<CharacterView>();
                Set(view,"bodyRenderer",body); view.Bind(unit); views.Add(unit,view);
            }
            using var driver = new CombatExecutionDriver(host,spawner,null,null,null,null,null,4,4,0,0,0,null,null,new(),null,null,null);
            typeof(CombatExecutionDriver).GetProperty("Session").SetValue(driver,session);
            var skill = Asset<SkillData>(); Set(skill,"<SkillId>k__BackingField","skill.knife_toss");
            Call(driver,"HandlePoseFeedback",new CombatFeedbackEvent(actor,CombatFeedbackType.SkillUsed,
                skillPresentation:new CombatSkillPresentation(actor,skill,defender.Position,AttackType.Ranged)));
            Assert.That(driver.IsActionInProgress,Is.True); Assert.That(views[actor].HasCombatPose,Is.True);
            Assert.That(driver.CanEndTurn(),Is.False);
            Call(driver,"HandlePoseFeedback",new CombatFeedbackEvent(defender,CombatFeedbackType.Miss,source:actor));
            Assert.That(views[defender].HasCombatPose,Is.True);
            var bodyRenderer=views[defender].GetComponent<SpriteRenderer>();
            CombatPoseResolver.TryResolveAction("jerzy","dodge",new Vector2(-1,-1),out var dodge);
            Assert.That(bodyRenderer.sprite,Is.SameAs(dodge.Actor));
            Set(driver,"skillPresentationEnds",Time.time-1); driver.Tick(false);
            Assert.That(driver.IsActionInProgress,Is.False);
            driver.Dispose(); Assert.That(views[actor].HasCombatPose,Is.False);Assert.That(views[defender].HasCombatPose,Is.False);
        }

        [Test]
        public void DownedPoseDoesNotPromoteAUnitToTheActiveTurnOutline()
        {
            var root = new GameObject("downed outline state");
            created.Add(root);
            var spawner = root.AddComponent<CombatUnitViewSpawner>();
            var definition = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(
                "Assets/_Project/ScriptableObjects/Characters/CD_Jerzy.asset");
            var downed = new BattleUnit
            {
                IsPlayerSide = true,
                Position = new GridPosition(1, 1),
                Character = new CharacterInstance
                {
                    CharacterId = "jerzy",
                    Definition = definition,
                    CurrentHealth = 0,
                    LifeState = CharacterLifeState.Downed
                }
            };
            var active = new BattleUnit
            {
                IsPlayerSide = false,
                Character = new CharacterInstance
                {
                    CharacterId = "skeleton",
                    Definition = definition,
                    CurrentHealth = 1,
                    LifeState = CharacterLifeState.Alive
                }
            };
            var session = new CombatSession { ActiveUnit = active };
            session.Units.AddRange(new[] { downed, active });

            var actor = new GameObject("downed actor");
            actor.transform.SetParent(root.transform);
            created.Add(actor);
            var body = actor.AddComponent<SpriteRenderer>();
            var view = actor.AddComponent<CharacterView>();
            Set(view, "bodyRenderer", body);
            actor.AddComponent<CombatUnitVisualStateView>();
            view.Bind(downed);
            Assert.That(view.HasCombatPose, Is.True, "The fixture must retain the infinite downed pose.");

            var views = (Dictionary<BattleUnit, CharacterView>)typeof(CombatUnitViewSpawner)
                .GetField("views", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(spawner);
            views.Add(downed, view);
            Call(spawner, "SynchronizeViews", session);

            var outlines = actor.GetComponentsInChildren<SpriteRenderer>(true)
                .Where(renderer => renderer.gameObject.name == "Visual State Outline")
                .ToArray();
            Assert.That(outlines, Is.Not.Empty);
            Assert.That(outlines, Has.All.Property("enabled").False,
                "A persistent downed pose must not be treated as active turn feedback.");

            var visual = actor.GetComponent<CombatUnitVisualStateView>();
            visual.Refresh(downed, true, false, false, true);
            Assert.That(outlines, Has.All.Property("enabled").False,
                "The visual state owner must reject a stale active flag for a downed unit.");
            visual.Refresh(downed, true, true, true, true);
            Assert.That(outlines, Has.All.Property("enabled").True,
                "Target feedback must remain available for a downed revive target.");

            var activeActor = new GameObject("active actor");
            activeActor.transform.SetParent(root.transform);
            created.Add(activeActor);
            var activeBody = activeActor.AddComponent<SpriteRenderer>();
            var activeView = activeActor.AddComponent<CharacterView>();
            Set(activeView, "bodyRenderer", activeBody);
            activeActor.AddComponent<CombatUnitVisualStateView>();
            activeView.Bind(active);
            views.Add(active, activeView);
            Call(spawner, "SynchronizeViews", session);
            var activeOutlines = activeActor.GetComponentsInChildren<SpriteRenderer>(true)
                .Where(renderer => renderer.gameObject.name == "Visual State Outline")
                .ToArray();
            Assert.That(activeOutlines, Is.Not.Empty);
            Assert.That(activeOutlines, Has.All.Property("enabled").True,
                "The living active unit must retain its active-turn outline.");

            var borrowedActor = new GameObject("borrowed downed actor");
            borrowedActor.transform.SetParent(root.transform);
            var borrowedBody = borrowedActor.AddComponent<SpriteRenderer>();
            var borrowedView = borrowedActor.AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
            var borrowedVisual = borrowedActor.AddComponent<CombatUnitVisualStateView>();
            Set(borrowedView, "bodyRenderer", borrowedBody);
            Set(borrowedView, "combatVisualState", borrowedVisual);
            borrowedView.BindCombat(downed);
            Call(borrowedView, "SyncLifeStatePose");
            Assert.That(borrowedView.HasCombatPose, Is.True);
            borrowedView.RefreshCombatPresentation(false, false, false);
            var borrowedOutlines = borrowedActor.GetComponentsInChildren<SpriteRenderer>(true)
                .Where(renderer => renderer.gameObject.name == "Visual State Outline")
                .ToArray();
            Assert.That(borrowedOutlines, Is.Not.Empty);
            Assert.That(borrowedOutlines, Has.All.Property("enabled").False);
        }

        [TestCase("npc.arungard.blacksmith_01")]
        [TestCase("npc.arungard.alchemist_01")]
        public void NpcStationResolverRecreatesDestroyedCacheEntry(string id)
        {
            Assert.That(CharacterAnimationResolver.TryResolveNpcStation(id,out var before,out var offset),Is.True);
            var size=before.rect.size;
            Object.DestroyImmediate(before);
            Assert.That(CharacterAnimationResolver.TryResolveNpcStation(id,out var after,out var restoredOffset),Is.True);
            Assert.That(after != null,Is.True);
            Assert.That(after.rect.size,Is.EqualTo(size));Assert.That(restoredOffset,Is.EqualTo(offset));
        }

        [TestCase("blacksmith",26,28,-.40625f,.46875f)]
        [TestCase("alchemist",28,24,-.5f,.6875f)]
        public void NpcStationMatchesApprovedOffsetsAndForegroundTracksConversation(string unit,int width,int height,float x,float y)
        {
            var id="npc.arungard."+unit+"_01";
            Assert.That(CharacterAnimationResolver.TryResolveNpcStation(id,out var prop,out var offset),Is.True);
            Assert.That(prop.rect.size,Is.EqualTo(new Vector2(width,height))); Assert.That(offset,Is.EqualTo(new Vector2(x,y)));
            Assert.That(prop.pivot,Is.EqualTo(new Vector2(0,height)));
            CharacterAnimationResolver.TryResolveNpcWork(id,out var work);
            var hasForeground = unit == "blacksmith";
            Assert.That(CharacterAnimationResolver.TryResolveNpcForeground(id,true,out var upperWork),Is.EqualTo(hasForeground));
            if (hasForeground)
            {
                Assert.That(upperWork,Is.Not.SameAs(work)); Assert.That(upperWork.FrameCount,Is.EqualTo(8));
            }
            CharacterAnimationResolver.TryResolveNpcForeground(id,false,out var upperTalk);
            var root=new GameObject("station NPC");created.Add(root);var body=root.AddComponent<SpriteRenderer>();var view=root.AddComponent<NpcView>();
            Set(view,"bodyRenderer",body);var npc=new NpcInstance{NpcId=id};view.Initialize(null,npc);Call(view,"LateUpdate");
            var upper=root.transform.Find("NPC Above Workstation").GetComponent<SpriteRenderer>();
            var station=root.transform.Find("NPC Workstation").GetComponent<SpriteRenderer>();
            Assert.That(upper.sprite,Is.SameAs(upperWork?.Frames[0]));Assert.That(station.sortingOrder,Is.GreaterThan(body.sortingOrder));Assert.That(station.sortingOrder,Is.LessThan(upper.sortingOrder));
            Call(view,"HandleAnimationDialogueStarted",new DialogueSession(npc,null,null,"player"));
            Assert.That(upper.sprite,Is.SameAs(upperTalk?.Frames[0]));
            body.forceRenderingOff=true;Call(view,"LateUpdate");Assert.That(station.forceRenderingOff && upper.forceRenderingOff,Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AlchemistCauldronRearRimAndLiquidStayInFrontOfApronAcrossFrames(bool conversation)
        {
            const string id = "npc.arungard.alchemist_01";
            var root = new GameObject("alchemist layer render"); created.Add(root);
            root.transform.position = new Vector3(1000, 1000);
            var body = root.AddComponent<SpriteRenderer>();
            var material = new Material(Shader.Find("Sprites/Default")); created.Add(material);
            body.sharedMaterial = material;
            var view = root.AddComponent<NpcView>(); Set(view, "bodyRenderer", body);
            var npc = new NpcInstance { NpcId = id }; view.Initialize(null, npc);
            if (conversation) Call(view, "HandleAnimationDialogueStarted", new DialogueSession(npc,null,null,"player"));
            var station = root.transform.Find("NPC Workstation").GetComponent<SpriteRenderer>();
            var upper = root.transform.Find("NPC Above Workstation").GetComponent<SpriteRenderer>();
            foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>()) renderer.gameObject.layer = 31;
            var cameraObject = new GameObject("alchemist QA camera"); created.Add(cameraObject);
            var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
            camera.orthographic = true; camera.orthographicSize = 1; camera.aspect = 1;
            camera.cullingMask = 1 << 31; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
            camera.transform.position = root.transform.position + new Vector3(0, 29f/32f, -10);
            var target = new RenderTexture(256, 256, 24); created.Add(target); camera.targetTexture = target;
            var readback = new Texture2D(256,256,TextureFormat.RGBA32,false); created.Add(readback);
            var previous = RenderTexture.active;
            Color32[] Capture()
            {
                camera.Render(); RenderTexture.active = target;
                readback.ReadPixels(new Rect(0,0,256,256),0,0); readback.Apply();
                return readback.GetPixels32();
            }
            try
            {
                Call(view, "LateUpdate"); body.enabled = false; upper.enabled = false;
                var potOnly = Capture();
                // Native cauldron pixel (14, 3) lies on the rear rim, below the
                // stirring hand, where the old full upper-body crop painted apron.
                var sample = camera.WorldToViewportPoint(station.transform.TransformPoint(new Vector3(14.5f/32f, -3.5f/32f)));
                var pixel = Mathf.FloorToInt(sample.y*256)*256 + Mathf.FloorToInt(sample.x*256);
                Assert.That(potOnly[pixel].a, Is.EqualTo(255), "Rear rim sample must be opaque.");
                body.enabled = true;
                var clip = (CharacterAnimationClip)typeof(NpcView).GetField("idleAnimation",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(view);
                for (var frame=0; frame<clip.FrameCount; frame++)
                {
                    body.sprite = clip.Frames[frame]; Set(view,"idleAnimationFrameIndex",frame); Call(view,"LateUpdate");
                    Assert.That(upper.sprite, Is.Null, "The apron must not be restored over the cauldron.");
                    var rendered = Capture();
                    Assert.That(rendered[pixel], Is.EqualTo(potOnly[pixel]), "Cauldron rear rim covered in frame "+frame);
                }
                Assert.That(body.sprite, Is.Not.Null); Assert.That(station.enabled, Is.True);
            }
            finally { RenderTexture.active = previous; camera.targetTexture = null; target.Release(); }
        }

        [Test]
        public void BlacksmithUsesExistingCityAnvilWithoutMovingOrDuplicatingIt()
        {
            var id="npc.arungard.blacksmith_01";
            CharacterAnimationResolver.TryResolveNpcStation(id,out var sprite,out var offset);
            var prop=new GameObject("existing anvil");created.Add(prop);var renderer=prop.AddComponent<SpriteRenderer>();renderer.sprite=sprite;
            prop.transform.position=new Vector3(5,12,0);var original=prop.transform.position;
            var root=new GameObject("blacksmith");created.Add(root);var body=root.AddComponent<SpriteRenderer>();var view=root.AddComponent<NpcView>();
            Set(view,"bodyRenderer",body);Set(view,"workstationRenderer",renderer);
            view.Initialize(null,new NpcInstance{NpcId=id});Call(view,"LateUpdate");
            Assert.That(prop.transform.position,Is.EqualTo(original));Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Length,Is.EqualTo(3));
            Assert.That(renderer.transform.parent,Is.SameAs(body.transform));
            var registered=body.transform.TransformPoint(offset);
            Assert.That(registered.x,Is.EqualTo(renderer.bounds.min.x).Within(.001));Assert.That(registered.y,Is.EqualTo(renderer.bounds.max.y).Within(.001));
            var position=root.transform.position;view.Initialize(null,new NpcInstance{NpcId=id});Assert.That(root.transform.position,Is.EqualTo(position));
        }

        [Test]
        public void SuccessfulSkillPublishesOnePresentationBeforeEffectsAndFailedSkillPublishesNone()
        {
            var definition=Asset<CharacterDefinition>();
            var caster=new BattleUnit { Position=new(1,1), IsPlayerSide=true, CurrentActionPoints=10000,
                Character=new CharacterInstance { Definition=definition, CharacterId="caster", LifeState=CharacterLifeState.Alive, CurrentHealth=50,
                    ResourcePool=new ResourcePool(ResourceType.Momentum,0,0,0), Stats=new CharacterStats { MaxHealth=new Stat(100), Speed=new Stat(100) } } };
            var session=new CombatSession { Grid=new BattleGrid(4,4), ActiveUnit=caster };session.Units.Add(caster);
            var skill=Asset<SkillData>();Set(skill,"<SkillId>k__BackingField","skill.test.presentation");
            var mode=new SkillTargetModeData();Set(mode,"targetType",TargetType.Caster);Set(mode,"targetingType",TargetingType.Allies);Set(mode,"includesCaster",true);
            Set(skill,"targetModes",new List<SkillTargetModeData>{mode}); var heal=new SkillEffectData();Set(heal,"effectType",EffectType.Heal);Set(heal,"baseValue",20);Set(skill,"effects",new List<SkillEffectData>{heal});
            var bus=new CombatFeedbackBus(); var count=0;
            bus.Published+=e=> { if (!e.SkillPresentation.HasValue)return;count++;Assert.That(caster.Character.CurrentHealth, Is.EqualTo(50)); Assert.That(e.SkillPresentation.Value.Skill, Is.SameAs(skill)); };
            var system=new SkillExecutionSystem(feedbackBus:bus);
            Assert.That(system.TryExecute(session,caster,skill,caster),Is.True); Assert.That(count,Is.EqualTo(1));Assert.That(caster.Character.CurrentHealth,Is.EqualTo(70));
            caster.Character.LifeState=CharacterLifeState.Downed;
            Assert.That(system.TryExecute(session,caster,skill,caster),Is.False);Assert.That(count,Is.EqualTo(1));
        }
    }
}

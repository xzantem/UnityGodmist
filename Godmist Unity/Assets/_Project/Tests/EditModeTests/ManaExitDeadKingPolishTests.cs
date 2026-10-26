using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Items;
using Godmist.Data.Skills;
using Godmist.Data.World;
using Godmist.Editor;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Effects;
using Godmist.Runtime.Items;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Systems.Dungeons;
using Godmist.Systems.Items;
using Godmist.Views.Characters;
using Godmist.Views.Dungeons;
using Godmist.Views.Lighting;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public partial class ManaExitDeadKingPolishTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly string Output = Path.GetFullPath("../LocalHandoffs/mana-exits-deadking-20261026");
        static T Read<T>(object o, string field) => (T)o.GetType().GetField(field, Private).GetValue(o);
        static void Write(object o, string field, object value) => o.GetType().GetField(field, Private).SetValue(o, value);
        static DungeonExitView Prefab => AssetDatabase.LoadAssetAtPath<DungeonExitView>("Assets/_Project/Prefabs/Dungeon/PF_DungeonExitView.prefab");
        static BattleUnit Unit(string path) => new() { IsPlayerSide = true,
            Character = new CharacterInstanceFactory().Create(AssetDatabase.LoadAssetAtPath<CharacterDefinition>(path)) };

        [Test, Explicit("Apply only the approved exit art and two glossary entries through Editor API.")]
        public void ApplyApprovedPresentationAssets()
        {
            Directory.CreateDirectory(Output);
            foreach (var path in new[] { "Assets/_Project/Prefabs/Dungeon/PF_DungeonExitView.prefab", "Assets/_Project/ScriptableObjects/UI/CombatTooltipGlossary.asset" })
            {
                var before = Path.Combine(Output, "serialized-before", Path.GetFileName(path));
                Directory.CreateDirectory(Path.GetDirectoryName(before));
                if (!File.Exists(before)) File.Copy(path, before);
            }
            ManaExitDeadKingPolishSetup.Apply();
            Assert.That(Read<Sprite>(Prefab, "upwardSprite"), Is.SameAs(AssetDatabase.LoadAssetAtPath<Sprite>(ManaExitDeadKingPolishSetup.UpPath)));
            Assert.That(Read<Sprite>(Prefab, "closedStairSprite"), Is.Not.Null);
            Assert.That(Read<Sprite>(Prefab, "closedForestSprite"), Is.Not.Null);
        }

        [TestCase("CD_Mage")] [TestCase("CD_Paladin")]
        public void WaitingCannotRefillManaButRealPotionStillWorks(string character)
        {
            var unit = Unit("Assets/_Project/ScriptableObjects/Characters/" + character + ".asset");
            unit.Character.ResourcePool.CurrentValue = 17;
            new ExplorationStatusEffectSystem().Advance(600, new[] { new DungeonTrapOccupant(unit, null, Vector2Int.zero) });
            Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(17));
            var definition = ScriptableObject.CreateInstance<ConsumableItemDefinition>();
            try
            {
                void Set(string name, object value) => typeof(ConsumableItemDefinition).GetField("<" + name + ">k__BackingField", Private)?.SetValue(definition, value);
                typeof(ItemDefinition).GetField("<Id>k__BackingField", Private).SetValue(definition, "test.mana.potion");
                Set("UseType", ConsumableUseType.Potion); Set("MaxCharges", 3);
                var potion = new ConsumableItemInstance(definition);
                potion.RuntimeEffects.Add(new ConsumableEffectRuntime { EffectType = ConsumableRuntimeEffectType.RestoreResource, Value = .5f });
                unit.Character.Inventory.Items.Add(potion);
                Assert.That(new ConsumableService().TryUseInExploration(unit.Character, potion, out _), Is.True);
                Assert.That(unit.Character.ResourcePool.CurrentValue, Is.GreaterThan(17));
                var afterPotion = unit.Character.ResourcePool.CurrentValue;
                new ExplorationStatusEffectSystem().Advance(60, new[] { new DungeonTrapOccupant(unit, null, Vector2Int.zero) });
                Assert.That(unit.Character.ResourcePool.CurrentValue, Is.EqualTo(afterPotion));
            }
            finally { Object.DestroyImmediate(definition); }
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void KnightsShowRemainingHitsWithoutTechnicalHealthBuff(int hits)
        {
            var knight = Unit("Assets/_Project/ScriptableObjects/Catacombs/Enemies/CD_Catacombs_spectral_knight.asset");
            knight.DeadKingKnightHits = hits;
            var technical = new PassiveEffectRuntime { EffectId = "summon.health", Lifetime = PassiveEffectLifetime.CombatScoped };
            knight.Character.PassiveEffects.Add(technical);
            var status = CombatStatusPresentationFactory.Create(knight.Character.PassiveEffects, owner: knight);
            Assert.That(status.Any(s => s.Effect.EffectId == "summon.health"), Is.False);
            var shell = status.Single(s => s.Effect.EffectId == "catacombs.spectral_knight.shell");
            Assert.That(shell.Effect.Stacks, Is.EqualTo(3 - hits));
            Assert.That(shell.Effect.DisplayName, Is.EqualTo("Widmowa powłoka"));
            var root = new GameObject("Knight tooltip");
            try
            {
                var body = root.AddComponent<UIWorkspaceTooltipView>().BuildPresentedStatusPayload(shell);
                Assert.That(body, Does.Contain("1 HP").And.Contain("Pozostałe trafienia: " + (3 - hits)));
                var glossary = AssetDatabase.LoadAssetAtPath<CombatTooltipGlossary>("Assets/_Project/ScriptableObjects/UI/CombatTooltipGlossary.asset");
                Assert.That(glossary.TryGet(shell.Effect.EffectId, out var entry), Is.True);
                Assert.That(entry.Icon, Is.Not.Null);
            }
            finally { Object.DestroyImmediate(root); }
            Assert.That(knight.Character.PassiveEffects, Does.Contain(technical), "Presentation must not change summon HP mechanics.");
        }

        [TestCase(1, "w")] [TestCase(-1, "e")]
        public void KnightSideProfilesAreCorrectedForLocomotionAndAttacks(int x, string assetDirection)
        {
            var id = DeadKingBossSystem.KnightId; var direction = new Vector2(x, 0);
            foreach (var state in new[] { CharacterAnimationState.Idle, CharacterAnimationState.Walk })
            {
                Assert.That(CharacterAnimationResolver.TryResolveCharacter(id, direction, state, out var clip), Is.True);
                Assert.That(clip.Frames.All(f => f.name.StartsWith("spectral-knight-" + assetDirection + "-")), Is.True);
            }
            Assert.That(CharacterAnimationResolver.DirectionFor(direction), Is.EqualTo(x > 0 ? "e" : "w"));
            Assert.That(CombatPoseResolver.TryResolveAction(id, "hit", direction, out _), Is.True);
        }

        [Test]
        public void ShadowDescribesItsRealMechanicInsteadOfZeroActionValue()
        {
            var skill = AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Catacombs/Skills/SD_Catacombs_dead_king_shadow.asset");
            var root = new GameObject("Shadow tooltip");
            try
            {
                var tooltip = root.AddComponent<UIWorkspaceTooltipView>();
                var lines = (IEnumerable<string>)typeof(UIWorkspaceTooltipView).GetMethod("BuildSkillMechanics", Private).Invoke(tooltip, new object[] { skill, null });
                var text = string.Join("\n", lines);
                Assert.That(text, Does.Contain("10 bezpiecznych").And.Contain("2 tur"));
                Assert.That(text, Does.Not.Contain("0%").And.Not.Contain("Przyspiesza"));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(false)] [TestCase(true)]
        public void ExitOpensVisuallyOnlyAfterItsOwnGuardsAreCleared(bool forest)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>("Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
            var definition = catalog.DungeonDefinitions.Single(d => d != null && d.Id == "dungeon." + (forest ? "forest" : "catacombs") + ".expedition");
            var dungeon = new DungeonGeneratorService().Generate(definition, 261026, 10, DungeonLength.Short);
            new DungeonEncounterPlacementSystem().Place(dungeon);
            var floor = dungeon.Floors[0];
            var exit = Object.Instantiate(Prefab);
            try
            {
                exit.InitializeTransition(null, Vector2.zero, () => Assert.Fail("Locked exit must not execute."), requiresClearedExitRoom: true);
                Write(exit, "interactionFloor", floor);
                SpriteRenderer body = Read<SpriteRenderer>(exit, "transitionRenderer");
                if (forest)
                {
                    var go = new GameObject("Forest exit wicket gate", typeof(SpriteRenderer), typeof(UnityEngine.Rendering.SortingGroup));
                    go.transform.SetParent(exit.transform);
                    body = go.GetComponent<SpriteRenderer>();
                    body.sprite = Resources.Load<Sprite>("Godmist/ForestArt/Exploration/exit-gate-v2");
                    exit.ConfigureStandingPresentation(null, go.GetComponent<UnityEngine.Rendering.SortingGroup>());
                }
                var open = forest ? Resources.Load<Sprite>("Godmist/ForestArt/Exploration/exit-gate-v2") : Read<Sprite>(exit, "downwardSprite");
                exit.ConfigureExitState(dungeon);
                Assert.That(exit.IsExitBlocked, Is.True);
                Assert.That(body.sprite, Is.Not.SameAs(open));
                exit.Interact(); // No notice popup or transition while locked.
                var guards = floor.Encounters.Where(e => e.RoomId == floor.Rooms.Single(r => r.RoomType == DungeonRoomType.Final).RoomId).ToArray();
                Assert.That(guards, Is.Not.Empty);
                foreach (var guard in guards) dungeon.TryMarkEncounterCleared(guard.InstanceId);
                typeof(DungeonExitView).GetMethod("LateUpdate", Private).Invoke(exit, null);
                Assert.That(exit.IsExitBlocked, Is.False);
                Assert.That(body.sprite, Is.SameAs(open));
            }
            finally { Object.DestroyImmediate(exit.gameObject); }
        }
    }
}

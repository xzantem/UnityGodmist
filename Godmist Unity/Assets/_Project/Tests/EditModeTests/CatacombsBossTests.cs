using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Combat;
using Godmist.Data.Dungeons;
using Godmist.Data.Effects;
using Godmist.Data.Items;
using Godmist.Data.Skills;
using Godmist.Data.World;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Systems.Dungeons;
using Godmist.Systems.Items;
using Godmist.Views.Characters;
using Godmist.Views.Combat;
using Godmist.Views.Dungeons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class CatacombsBossTests
    {
        private readonly List<UnityEngine.Object> assets = new();

        private T Asset<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            assets.Add(asset);
            return asset;
        }

        private static void Set(object target, string name, object value)
        {
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var field = type.GetField(name, flags)
                    ?? type.GetField("<" + name + ">k__BackingField", flags);
                if (field == null) continue;
                field.SetValue(target, value);
                return;
            }
            throw new MissingFieldException(target.GetType().Name, name);
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (var asset in assets) UnityEngine.Object.DestroyImmediate(asset);
            assets.Clear();
        }

        private BattleUnit Unit(string id, bool player, int hp, GridPosition position,
            SkillData vengeance = null)
        {
            var cls = Asset<CharacterClassData>();
            Set(cls, "Id", "class." + id);
            Set(cls, "ResourceType", ResourceType.Mana);
            var definition = Asset<CharacterDefinition>();
            Set(definition, "Id", id);
            Set(definition, "DisplayName", id);
            Set(definition.BaseStats, "MaxHealth", hp);
            Set(definition.BaseStats, "MinAttack", 40);
            Set(definition.BaseStats, "MaxAttack", 40);
            if (vengeance != null) definition.StartingSkills.Add(vengeance);
            var character = new CharacterInstanceFactory().Create(definition, cls);
            return new BattleUnit
            {
                Character = character,
                IsPlayerSide = player,
                Position = position
            };
        }

        [Test]
        public void HealthGatesQueueOneBossCastEachAndEnteringRuneDisarmsIt()
        {
            var runeDefinition = Asset<SummonedObjectDefinition>();
            Set(runeDefinition, "objectId", CatacombsBossSystem.RuneObjectId);
            Set(runeDefinition, "blocksMovement", false);
            Set(runeDefinition, "defaultDurationTurns", 2);
            var effect = new SkillEffectData();
            Set(effect, "effectType", EffectType.CreateObject);
            Set(effect, "summonedObjectDefinition", runeDefinition);
            Set(effect, "maxActiveObjects", 15);
            var skill = Asset<SkillData>();
            Set(skill, "SkillId", CatacombsBossSystem.VengeanceSkillId);
            Set(skill, "effects", new List<SkillEffectData> { effect });
            var boss = Unit(CatacombsBossSystem.BossId, false, 100,
                new GridPosition(4, 4), skill);
            var hero = Unit("hero", true, 300, new GridPosition(1, 1));
            var session = new CombatSession { Grid = new BattleGrid(10, 10), ActiveUnit = boss };
            session.Units.AddRange(new[] { boss, hero });
            new CombatTurnSystem().InitializeTurnOrder(session);
            session.ActiveUnit = boss;
            boss.CurrentActionPoints = boss.MaxActionPoints = 500;
            var objects = new SummonedObjectSystem();
            var system = new CatacombsBossSystem(objects, new CombatDamageSystem());
            var ai = new SimpleCombatAiSystem(skillExecutionSystem: new SkillExecutionSystem(),
                catacombsBossSystem: system);
            var turn = ai.CreateTurnState();

            boss.Character.CurrentHealth = 70;
            system.OnEnemyHealthChanged(session, boss, 100);
            Assert.That(session.SummonedObjects, Is.Empty,
                "Próg HP tylko udostępnia umiejętność, nie stawia run automatycznie.");
            Assert.That(ai.TryExecuteNextAction(session, boss, turn, out var firstCast), Is.True);
            Assert.That(firstCast.Type, Is.EqualTo(SimpleCombatAiActionType.Skill));
            Assert.That(firstCast.Skill, Is.SameAs(skill));
            Assert.That(session.SummonedObjects.Count, Is.EqualTo(5));
            Assert.That(session.TurnParticipants.Count(p => p.IsSummonedObject), Is.EqualTo(5));
            Assert.That(boss.CurrentActionPoints, Is.EqualTo(500));
            Assert.That(system.TryExecuteVengeance(session, boss, out _), Is.False,
                "Jeden próg nie może dać drugiego użycia.");
            system.OnEnemyHealthChanged(session, boss, 80);
            Assert.That(session.SummonedObjects.Count, Is.EqualTo(5));

            var disarmed = session.SummonedObjects[0];
            hero.Position = disarmed.Position;
            system.OnUnitEnteredCell(session, hero);
            Assert.That(session.SummonedObjects.Contains(disarmed), Is.False);
            Assert.That(session.TurnParticipants.Any(p => p.SummonedObject == disarmed), Is.False);

            boss.Character.CurrentHealth = 49;
            system.OnEnemyHealthChanged(session, boss, 70);
            Assert.That(session.SummonedObjects.Count, Is.EqualTo(4));
            Assert.That(ai.TryExecuteNextAction(session, boss, turn, out var secondCast), Is.True);
            Assert.That(secondCast.Skill, Is.SameAs(skill));
            Assert.That(session.SummonedObjects.Count, Is.EqualTo(9));
        }

        [Test]
        public void VengeancePlacesRunesOnlyInBossCurrentRoomOnFloorWideGrid()
        {
            var map = new DungeonFloorMap();
            for (var x = 0; x < 8; x++)
                for (var y = 0; y < 4; y++)
                    map.TryAddFloorCell(new Vector2Int(x, y), x < 4 ? "left" : "right");
            var context = new DungeonCombatContext("catacombs", 0,
                "boss.encounter", "left", new Vector2Int(1, 1),
                new Vector2Int(6, 2), map.FloorCells,
                Array.Empty<DungeonCellEdge>(),
                Array.Empty<DungeonCombatMapObjectSnapshot>(),
                surfaceMap: map, isFloorWide: true);
            Assert.That(new DungeonBattleGridFactory().TryCreate(context, out var grid), Is.True);
            var runeDefinition = Asset<SummonedObjectDefinition>();
            Set(runeDefinition, "objectId", CatacombsBossSystem.RuneObjectId);
            Set(runeDefinition, "defaultDurationTurns", 2);
            var effect = new SkillEffectData();
            Set(effect, "effectType", EffectType.CreateObject);
            Set(effect, "summonedObjectDefinition", runeDefinition);
            Set(effect, "maxActiveObjects", 15);
            var skill = Asset<SkillData>();
            Set(skill, "SkillId", CatacombsBossSystem.VengeanceSkillId);
            Set(skill, "effects", new List<SkillEffectData> { effect });
            var boss = Unit(CatacombsBossSystem.BossId, false, 100,
                new GridPosition(6, 2), skill);
            var hero = Unit("hero.room.test", true, 100,
                new GridPosition(1, 1));
            var session = new CombatSession
            {
                Grid = grid, DungeonContext = context, ActiveUnit = boss,
                Outcome = CombatOutcomeState.Ongoing
            };
            session.Units.AddRange(new[] { boss, hero });
            var feedback = new CombatFeedbackBus();
            CombatFeedbackEvent? skillUse = null;
            feedback.Published += item => skillUse = item;
            var system = new CatacombsBossSystem(new SummonedObjectSystem(),
                new CombatDamageSystem(), feedbackBus: feedback);
            boss.Character.CurrentHealth = 70;
            system.OnEnemyHealthChanged(session, boss, 100);

            Assert.That(system.TryExecuteVengeance(session, boss, out _), Is.True);
            Assert.That(skillUse?.Type, Is.EqualTo(CombatFeedbackType.SkillUsed));
            Assert.That(skillUse?.Target, Is.SameAs(boss));
            Assert.That(session.SummonedObjects.Count, Is.EqualTo(5));
            foreach (var rune in session.SummonedObjects)
            {
                var world = DungeonBattleGridFactory.ToWorldPosition(context, rune.Position);
                Assert.That(map.TryGetRoomId(world, out var roomId), Is.True);
                Assert.That(roomId, Is.EqualTo("right"));
            }
        }

        [Test]
        public void CombatHudPrefabHasSkillCastBannerWired()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CombatHud.prefab");
            Assert.That(prefab, Is.Not.Null);
            var hud = prefab.GetComponent<Godmist.Views.UI.CombatHudView>();
            Assert.That(hud, Is.Not.Null);
            var banner = prefab.transform.Find("SkillCastBanner");
            var text = banner?.Find("SkillCastText")?.GetComponent("TextMeshProUGUI");
            Assert.That(banner, Is.Not.Null);
            Assert.That(text, Is.Not.Null);
            Assert.That(banner.gameObject.activeSelf, Is.False);
            Assert.That(typeof(Godmist.Views.UI.CombatHudView)
                .GetField("skillCastBanner", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(hud), Is.SameAs(banner.gameObject));
            Assert.That(typeof(Godmist.Views.UI.CombatHudView)
                .GetField("skillCastText", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(hud), Is.SameAs(text));
        }

        [Test]
        public void BossPrioritizesThreeFreeVengeanceCastsAfterOneHitCrossesAllThresholds()
        {
            var definition = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(
                "Assets/_Project/ScriptableObjects/Catacombs/Enemies/CD_Catacombs_dead_king.asset");
            Assert.That(definition, Is.Not.Null);
            var boss = new BattleUnit
            {
                Character = new CharacterInstanceFactory().Create(definition, definition.ClassData, 10),
                Position = new GridPosition(4, 4),
                CurrentActionPoints = 500,
                MaxActionPoints = 500
            };
            var hero = Unit("vengeance.target", true, 1000, new GridPosition(5, 4));
            var session = new CombatSession
            {
                Grid = new BattleGrid(10, 10),
                ActiveUnit = boss,
                Outcome = CombatOutcomeState.Ongoing
            };
            session.Units.AddRange(new[] { boss, hero });
            var system = new CatacombsBossSystem(new SummonedObjectSystem(),
                new CombatDamageSystem());
            var ai = new SimpleCombatAiSystem(skillExecutionSystem: new SkillExecutionSystem(),
                catacombsBossSystem: system);
            var initialHealth = boss.Character.CurrentHealth;
            var initialMana = boss.Character.ResourcePool.CurrentValue;
            boss.Character.CurrentHealth = Mathf.FloorToInt(initialHealth * .2f);
            system.OnEnemyHealthChanged(session, boss, initialHealth);
            Assert.That(session.SummonedObjects, Is.Empty);
            boss.CurrentActionPoints = 0;
            Assert.That(system.CanCastVengeance(session, boss), Is.True);

            var turn = ai.CreateTurnState();
            for (var cast = 1; cast <= 3; cast++)
            {
                Assert.That(ai.TryExecuteNextAction(session, boss, turn, out var action), Is.True);
                Assert.That(action.Type, Is.EqualTo(SimpleCombatAiActionType.Skill));
                Assert.That(action.Skill.SkillId, Is.EqualTo(CatacombsBossSystem.VengeanceSkillId));
                Assert.That(session.SummonedObjects.Count, Is.EqualTo(cast * 5));
            }

            Assert.That(system.TryExecuteVengeance(session, boss, out _), Is.False);
            Assert.That(boss.CurrentActionPoints, Is.Zero);
            Assert.That(boss.Character.ResourcePool.CurrentValue, Is.EqualTo(initialMana));
        }

        [Test]
        public void RuneWarnsOnFirstAvTurnAndHitsNearestHeroOnSecond()
        {
            var runeDefinition = Asset<SummonedObjectDefinition>();
            Set(runeDefinition, "objectId", CatacombsBossSystem.RuneObjectId);
            Set(runeDefinition, "defaultDurationTurns", 2);
            var boss = Unit(CatacombsBossSystem.BossId, false, 100,
                new GridPosition(4, 4));
            var near = Unit("near", true, 300, new GridPosition(1, 2));
            var far = Unit("far", true, 300, new GridPosition(9, 9));
            var session = new CombatSession { Grid = new BattleGrid(10, 10) };
            session.Units.AddRange(new[] { boss, near, far });
            var rune = new SummonedCombatMapObject("rune", new GridPosition(1, 1),
                boss.Character.CharacterId, CatacombsBossSystem.VengeanceSkillId,
                0, runeDefinition);
            session.SummonedObjects.Add(rune);
            var system = new CatacombsBossSystem(new SummonedObjectSystem(),
                new CombatDamageSystem());

            system.ExecuteRuneTurn(session, rune);
            Assert.That(near.Character.CurrentHealth, Is.EqualTo(300));
            rune.AdvanceDuration();
            system.ExecuteRuneTurn(session, rune);
            Assert.That(near.Character.CurrentHealth, Is.LessThan(300));
            Assert.That(far.Character.CurrentHealth, Is.EqualTo(300));
        }

        [Test]
        public void BossOfferUnlocksAtMilestonesAndPersistsUntilCompleted()
        {
            var regular = Asset<DungeonDefinition>();
            Set(regular, "Id", "regular");
            Set(regular, "IsProcedural", true);
            Set(regular, "IsAvailableAsOffer", true);
            var boss = Asset<DungeonDefinition>();
            Set(boss, "Id", "boss");
            Set(boss, "IsProcedural", true);
            Set(boss, "IsAvailableAsOffer", true);
            boss.FixedOfferLevels.AddRange(new[] { 10, 20 });

            GameSession AtLevel(int level)
            {
                var session = new GameSession(campaignId: "boss.offer." + level);
                var character = new Godmist.Runtime.Characters.CharacterInstance
                    { CharacterId = "hero", Level = level };
                session.TryRegisterCharacter(character);
                session.PlayerParty.TryAddActiveMember("hero");
                return session;
            }

            var early = AtLevel(5);
            Assert.That(DungeonOfferSystem.EnsureOffers(early, new[] { regular, boss }), Is.True);
            Assert.That(early.DungeonOffers.Offers.All(offer => offer.DefinitionId == "regular"), Is.True);
            var before = AtLevel(9);
            Assert.That(DungeonOfferSystem.EnsureOffers(before, new[] { regular, boss }), Is.True);
            Assert.That(before.DungeonOffers.Offers.All(offer => offer.DefinitionId == "regular"), Is.True);

            var session = AtLevel(10);
            Assert.That(DungeonOfferSystem.EnsureOffers(session, new[] { regular, boss }), Is.True);
            var first = session.DungeonOffers.Offers.Single(offer => offer.DefinitionId == "boss");
            Assert.That(DungeonOfferSystem.OfferLevel(10, first), Is.EqualTo(10));
            Assert.That(session.DungeonOffers.GetDanger(boss.Theme), Is.EqualTo(100));
            DungeonOfferSystem.CompleteExpedition(session, new DungeonInstance(regular, 41, 10),
                new[] { regular, boss });
            Assert.That(session.DungeonOffers.GetDanger(boss.Theme), Is.EqualTo(70));
            Assert.That(DungeonOfferSystem.EnsureOffers(session, new[] { regular, boss }), Is.True);
            Assert.That(session.DungeonOffers.Offers[0].FixedLevel, Is.EqualTo(10),
                "A normal expedition must not remove an unlocked boss opportunity.");
            session.CharactersById["hero"].Level = 20;
            Assert.That(DungeonOfferSystem.EnsureOffers(session, new[] { regular, boss }), Is.True);
            Assert.That(session.DungeonOffers.BossOpportunities.Select(opportunity => opportunity.Level),
                Is.EquivalentTo(new[] { 10, 20 }));
            Assert.That(DungeonOfferSystem.OfferLevel(20, session.DungeonOffers.Offers[0]), Is.EqualTo(10));

            DungeonOfferSystem.CompleteExpedition(session, new DungeonInstance(boss, 42, 10),
                new[] { regular, boss });
            Assert.That(session.DungeonOffers.GetDanger(boss.Theme), Is.Zero);
            Assert.That(DungeonOfferSystem.EnsureOffers(session, new[] { regular, boss }), Is.True);
            Assert.That(DungeonOfferSystem.OfferLevel(20, session.DungeonOffers.Offers[0]), Is.EqualTo(20));
        }

        [Test]
        public void SummonedGuardUsesVisibleDungeonCharacterSorting()
        {
            var bossDefinition = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(
                "Assets/_Project/ScriptableObjects/Catacombs/Enemies/CD_Catacombs_dead_king.asset");
            var prefab = AssetDatabase.LoadAssetAtPath<CharacterView>(
                "Assets/_Project/Prefabs/Characters/PF_BattleUnit.prefab");
            Assert.That(bossDefinition, Is.Not.Null);
            Assert.That(prefab, Is.Not.Null);
            var guard = bossDefinition.StartingSkills.Single(skill =>
                skill.SkillId == CatacombsBossSystem.GuardSkillId);
            var boss = new BattleUnit
            {
                Character = new CharacterInstanceFactory().Create(bossDefinition,
                    bossDefinition.ClassData, 10),
                Position = new GridPosition(2, 2)
            };
            var session = new CombatSession { Grid = new BattleGrid(5, 5) };
            session.Units.Add(boss);
            Assert.That(new SummonSystem().TrySummon(session, boss, guard,
                guard.Effects[0], 0, new GridPosition(3, 2), out var skeleton), Is.True);
            Assert.That(skeleton.Character.Definition.VisualSprite, Is.Not.Null);

            var root = new GameObject("boss summon view test");
            try
            {
                var spawner = root.AddComponent<CombatUnitViewSpawner>();
                typeof(CombatUnitViewSpawner).GetField("battleUnitPrefab",
                    BindingFlags.Instance | BindingFlags.NonPublic).SetValue(spawner, prefab);
                typeof(CombatUnitViewSpawner).GetField("unitsRoot",
                    BindingFlags.Instance | BindingFlags.NonPublic).SetValue(spawner, root.transform);
                spawner.ConfigureDungeon(new GameSession(campaignId: "boss.summon.view"), null);
                typeof(CombatUnitViewSpawner).GetMethod("EnsureViews",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(spawner, new object[] { session });
                var view = root.GetComponentsInChildren<CharacterView>()
                    .Single(item => item.BoundUnit == skeleton);
                var body = view.GetComponentInChildren<SpriteRenderer>();
                Assert.That(body.sprite, Is.Not.Null);
                Assert.That(body.sortingOrder, Is.GreaterThanOrEqualTo(10));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [TestCase("swordsman")]
        [TestCase("archer")]
        [TestCase("defender")]
        [TestCase("occultist")]
        public void SummonedSkeletonUsesDamageSkillWhenSupportSkillIsUnaffordable(string skeletonType)
        {
            var definition = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(
                $"Assets/_Project/ScriptableObjects/Catacombs/Enemies/CD_Catacombs_{skeletonType}.asset");
            Assert.That(definition, Is.Not.Null);
            var skeleton = new BattleUnit
            {
                Character = new CharacterInstanceFactory().Create(definition,
                    definition.ClassData, 10),
                Position = new GridPosition(2, 2),
                IsAiControlledSummon = true,
                CurrentActionPoints = 500,
                MaxActionPoints = 500
            };
            skeleton.Character.ResourcePool.Drain(85);
            var boss = Unit("summon.owner", false, 1000, new GridPosition(1, 2));
            var hero = Unit("summon.target", true, 1000, new GridPosition(3, 2));
            var session = new CombatSession
            {
                Grid = new BattleGrid(5, 5),
                ActiveUnit = skeleton,
                Outcome = CombatOutcomeState.Ongoing
            };
            session.Units.AddRange(new[] { skeleton, boss, hero });
            session.Summons.Add(new SummonedBattleUnit(skeleton, boss.Character.CharacterId,
                CatacombsBossSystem.GuardSkillId, 0, 99, 1));
            var ai = new SimpleCombatAiSystem(skillExecutionSystem: new SkillExecutionSystem());
            var turn = ai.CreateTurnState();
            var usedDamageSkill = false;
            for (var actionIndex = 0; actionIndex < 5; actionIndex++)
            {
                if (!ai.TryExecuteNextAction(session, skeleton, turn, out var action)) break;
                if (action.Type == SimpleCombatAiActionType.Skill
                    && action.Skill.Effects.Any(effect => effect.EffectType == EffectType.DealDamage))
                {
                    usedDamageSkill = true;
                    break;
                }
            }

            Assert.That(usedDamageSkill, Is.True,
                $"Przywołany {skeletonType} powinien zaatakować przyległego bohatera w swojej turze.");
        }

        [Test]
        public void SummonedEnemyWithoutExplorationViewHasCombatVision()
        {
            var map = new DungeonFloorMap();
            for (var x = 0; x < 5; x++)
                for (var y = 0; y < 5; y++)
                    map.TryAddFloorCell(new Vector2Int(x, y), "boss.room");
            var floor = new DungeonFloorInstance(0);
            Assert.That(floor.TrySetMap(map), Is.True);
            var dungeon = new DungeonInstance("summon.vision", 10);
            Assert.That(dungeon.TryAddFloor(floor), Is.True);
            var encounter = new DungeonEncounterInstance("boss.encounter",
                Asset<EncounterDefinition>(), "boss.room", new RectInt(0, 0, 5, 5));
            Assert.That(new DungeonCombatContextFactory().TryCreate(dungeon, encounter,
                out var context, true), Is.True);

            var owner = Unit("summon.owner", false, 1000, new GridPosition(1, 2));
            var definition = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(
                "Assets/_Project/ScriptableObjects/Catacombs/Enemies/CD_Catacombs_swordsman.asset");
            Assert.That(definition, Is.Not.Null);
            var skeleton = new BattleUnit
            {
                Character = new CharacterInstanceFactory().Create(definition, definition.ClassData, 10),
                Position = new GridPosition(2, 2),
                CurrentActionPoints = 500,
                MaxActionPoints = 500
            };
            skeleton.Character.ResourcePool.Drain(85);
            var unrelatedEnemy = Unit("ordinary.enemy", false, 100, new GridPosition(3, 2));
            var hero = Unit("summon.target", true, 1000, new GridPosition(2, 3));
            var session = new CombatSession
            {
                DungeonContext = context,
                Grid = new BattleGrid(5, 5),
                ActiveUnit = skeleton,
                Outcome = CombatOutcomeState.Ongoing
            };
            session.Units.AddRange(new[] { owner, skeleton, unrelatedEnemy, hero });
            session.Summons.Add(new SummonedBattleUnit(skeleton, owner.Character.CharacterId,
                CatacombsBossSystem.GuardSkillId, 0, 99, 1));
            var root = new GameObject("summon combat vision test");
            try
            {
                var coordinator = root.AddComponent<DungeonCombatCoordinator>();
                Set(coordinator, "floor", floor);
                var canSee = typeof(DungeonCombatCoordinator).GetMethod(
                    "CanEnemySeeCombatTarget", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(canSee, Is.Not.Null);
                session.Contact = new DungeonContactState((enemy, player) =>
                    (bool)canSee.Invoke(coordinator, new object[] { session, enemy, player }));
                session.Contact.Refresh(session);
                Assert.That(session.Contact.Sees(skeleton, hero), Is.True);
                Assert.That(session.Contact.Sees(unrelatedEnemy, hero), Is.False);
                var ai = new SimpleCombatAiSystem(skillExecutionSystem: new SkillExecutionSystem());
                Assert.That(ai.TryExecuteNextAction(session, skeleton,
                    ai.CreateTurnState(), out var action), Is.True);
                Assert.That(action.Type, Is.EqualTo(SimpleCombatAiActionType.Skill));
                Assert.That(action.Skill.SkillId, Is.EqualTo("skill.enemy.catacombs.bone_sword"));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void BossUsesRoyalCleaveAgainstAdjacentHeroInEveryDirection()
        {
            var bossDefinition = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(
                "Assets/_Project/ScriptableObjects/Catacombs/Enemies/CD_Catacombs_dead_king.asset");
            Assert.That(bossDefinition, Is.Not.Null);
            foreach (var offset in new[] { new GridPosition(1, 0), new GridPosition(-1, 0),
                         new GridPosition(0, 1), new GridPosition(0, -1) })
            {
                var boss = new BattleUnit
                {
                    Character = new CharacterInstanceFactory().Create(
                        bossDefinition, bossDefinition.ClassData, 10),
                    Position = new GridPosition(2, 2),
                    CurrentActionPoints = 500,
                    MaxActionPoints = 500
                };
                boss.Character.ResourcePool.Drain(100);
                var hero = Unit("cleave.target", true, 500,
                    new GridPosition(2 + offset.X, 2 + offset.Y));
                var session = new CombatSession
                {
                    Grid = new BattleGrid(5, 5),
                    ActiveUnit = boss,
                    Outcome = CombatOutcomeState.Ongoing
                };
                session.Units.AddRange(new[] { boss, hero });
                var ai = new SimpleCombatAiSystem(
                    skillExecutionSystem: new SkillExecutionSystem());

                Assert.That(ai.TryExecuteNextAction(session, boss,
                    ai.CreateTurnState(), out var action), Is.True, offset.ToString());
                Assert.That(action.Type, Is.EqualTo(SimpleCombatAiActionType.Skill),
                    offset.ToString());
                Assert.That(action.Skill.SkillId, Is.EqualTo(
                    "skill.enemy.catacombs.dead_king_cleave"), offset.ToString());
                Assert.That(boss.CurrentActionPoints, Is.LessThan(500),
                    offset.ToString());
            }
        }

        [Test]
        public void BossAttacksEvenWhenGuardIsAvailable()
        {
            var definition = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(
                "Assets/_Project/ScriptableObjects/Catacombs/Enemies/CD_Catacombs_dead_king.asset");
            Assert.That(definition, Is.Not.Null);
            var boss = new BattleUnit
            {
                Character = new CharacterInstanceFactory().Create(definition,
                    definition.ClassData, 10),
                Position = new GridPosition(2, 2),
                CurrentActionPoints = 500,
                MaxActionPoints = 500
            };
            var hero = Unit("guard.target", true, 500, new GridPosition(1, 2));
            var session = new CombatSession
            {
                Grid = new BattleGrid(5, 5),
                ActiveUnit = boss,
                Outcome = CombatOutcomeState.Ongoing
            };
            session.Units.AddRange(new[] { boss, hero });
            var ai = new SimpleCombatAiSystem(
                skillExecutionSystem: new SkillExecutionSystem());
            Assert.That(ai.TryExecuteNextAction(session, boss,
                ai.CreateTurnState(), out var action), Is.True);
            Assert.That(action.Skill.SkillId, Is.EqualTo(
                "skill.enemy.catacombs.dead_king_cleave"));
            Assert.That(boss.CurrentActionPoints, Is.LessThan(500));
        }

        [Test]
        public void AuthoredBossExpeditionPlacesExactlyOneBossInFinalRoom()
        {
            var dungeon = AssetDatabase.LoadAssetAtPath<DungeonDefinition>(
                "Assets/_Project/ScriptableObjects/Dungeons/DD_Catacombs_BossExpedition.asset");
            Assert.That(dungeon, Is.Not.Null);
            var generator = new DungeonGeneratorService();
            var placement = new DungeonEncounterPlacementSystem();
            foreach (var level in new[] { 10, 20 })
            foreach (var seed in new[] { 17, 219, 3401 })
            {
                var instance = generator.Generate(dungeon, seed, level);
                placement.Place(instance);
                var bosses = instance.Floors.SelectMany(floor => floor.Encounters)
                    .Where(encounter => encounter.Definition == dungeon.BossEncounterDefinition)
                    .ToArray();
                Assert.That(bosses, Has.Length.EqualTo(1), $"level {level}, seed {seed}");
                var finalFloor = instance.Floors.Last();
                var finalRoom = finalFloor.Rooms.Single(room =>
                    room.RoomType == DungeonRoomType.Final);
                Assert.That(bosses[0].RoomId, Is.EqualTo(finalRoom.RoomId));
                Assert.That(finalFloor.Encounters.Count(encounter =>
                    encounter.RoomId == finalRoom.RoomId), Is.EqualTo(1));
            }
        }

        [Test]
        public void AuthoredBossLootUsesBothBracketsAndUniqueEquipment()
        {
            var root = "Assets/_Project/ScriptableObjects/";
            var dungeon = AssetDatabase.LoadAssetAtPath<DungeonDefinition>(
                root + "Dungeons/DD_Catacombs_BossExpedition.asset");
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(
                root + "World/GC_MainContentCatalog.asset");
            Assert.That(dungeon, Is.Not.Null);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(dungeon.BossEncounterDefinition?.Enemies.Count, Is.EqualTo(1));
            Assert.That(dungeon.BossEncounterDefinition.Enemies[0].CharacterDefinition.Id,
                Is.EqualTo(CatacombsBossSystem.BossId));
            CollectionAssert.AreEquivalent(new[] { 10, 20 }, dungeon.FixedOfferLevels);

            foreach (var level in new[] { 10, 20 })
            {
                Assert.That(dungeon.LootTableCatalog.TryGetBossLootTable(
                    DungeonTheme.Catacombs, level, out var table), Is.True);
                var factory = new ItemInstanceFactory();
                var loot = new LootTableSystem(() => 0f, factory,
                    new EquipmentGenerationService(new System.Random(17), factory),
                    catalog.EquipmentPartDefinitions, level);
                var items = loot.RollLoot(table);
                var bag = items.OfType<LootBagItemInstance>().Single(item =>
                    item.LootBagDefinition.Id == "item.lootbag.catacombs.dead_king");
                var unique = bag.Contents.OfType<EquipmentItemInstance>()
                    .Where(item => item.Definition is UniqueEquipmentDefinition).ToArray();
                Assert.That(unique.Length, Is.EqualTo(1));
                Assert.That(unique[0].ItemLevel, Is.EqualTo(level));
                if (level != 20)
                {
                    var missedUnique = new LootTableSystem(() => .99f, factory,
                        new EquipmentGenerationService(new System.Random(18), factory),
                        catalog.EquipmentPartDefinitions, level).RollLoot(table);
                    Assert.That(missedUnique.OfType<LootBagItemInstance>(), Is.Empty);
                    continue;
                }
                Assert.That(bag.Contents.Any(item =>
                    item.Definition.Id == "item.material.dead_king_ectoplasm"), Is.True);
                var stones = bag.Contents.OfType<GalduryteItemInstance>().ToArray();
                Assert.That(stones.Length, Is.EqualTo(3));
                Assert.That(stones.All(item => item.CurrentRarity >= RarityTier.Rare), Is.True);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Combat;
using Godmist.Data.Dungeons;
using Godmist.Data.Items;
using Godmist.Data.Skills;
using Godmist.Data.World;
using Godmist.Editor;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Items;
using Godmist.Runtime.Game;
using Godmist.Systems.SaveLoad;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Systems.Dungeons;
using Godmist.Systems.Items;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
namespace Godmist.Tests.EditMode
{
    public class BanditLeaderBossTests
    {
        static T Asset<T>(string p) where T:UnityEngine.Object=>AssetDatabase.LoadAssetAtPath<T>(p);
        static CharacterDefinition Definition=>Asset<CharacterDefinition>(VS16CatacombsBossSetup.BanditPath);
        UnityEngine.Random.State saved;
        [SetUp]public void Seed(){saved=UnityEngine.Random.state;UnityEngine.Random.InitState(725);}
        [TearDown]public void Restore(){UnityEngine.Random.state=saved;}
        static BattleUnit Unit(string role="boss",int x=7,int y=7)
        {
            var def=role=="boss"?Definition:Asset<CharacterDefinition>(VS16CatacombsBossSetup.BanditRoot+"CD_Forest_BanditThug.asset");
            Assert.That(def,Is.Not.Null,"Configure Bandit Leader first.");
            var c=new CharacterInstanceFactory().Create(def,def.ClassData,10);c.CharacterId+=Guid.NewGuid();
            c.Stats.MaxHealth=new Stat(5000);c.CurrentHealth=5000;c.Stats.MinAttack=c.Stats.MaxAttack=new Stat(100);
            c.Stats.PhysicalDefense=c.Stats.MagicalDefense=c.Stats.Dodge=c.Stats.CritChance=new Stat(0);c.Stats.CritDamage=new Stat(2);
            c.ResourcePool.CurrentValue=100;c.WeaponAccuracy=100;
            c.Stats.Speed=new Stat(400);
            return new BattleUnit {Character=c,IsPlayerSide=role=="hero",Position=new GridPosition(x,y),MaxActionPoints=10000,CurrentActionPoints=10000};
        }
        static CombatSession Session(params BattleUnit[] units)
        {
            var session=new CombatSession {CombatId="bandit.test",Grid=new BattleGrid(16,16),Units=units.ToList(),ActiveUnit=units[0]};
            session.TurnParticipants=units.Select(u=>new CombatTurnParticipant(u)).ToList();
            session.ActiveParticipant=session.TurnParticipants[0];
            foreach(var unit in units)unit.WarriorState.Session=session;return session;
        }
        static BanditLeaderBossSystem Owner(CombatSession s)=>new(s,new CombatDamageSystem(),new StatusEffectSystem(),new CombatRecoverySystem(),new SummonedObjectSystem());
        static SkillData Skill(BattleUnit u,string id)=>u.Character.Skills.Single(s=>s.SkillId==id);
        static SkillExecutionSystem Execution()=>new(statusEffectSystem:new StatusEffectSystem());
        static bool Cast(CombatSession s,BattleUnit u,string id,BattleUnit t)=>Execution().TryExecute(s,u,Skill(u,id),new Dictionary<int,IReadOnlyList<BattleUnit>>{{0,new[]{t}}});
        static void Reveal(BanditLeaderBossSystem owner,BattleUnit boss){owner.CheckPhases();owner.BeginTurn(boss);boss.MaxActionPoints=boss.CurrentActionPoints=10000;}
        static bool Place(CombatSession s,BattleUnit b,string id,GridPosition position)=>Execution().TryExecute(s,b,Skill(b,id),new Dictionary<int,IReadOnlyList<BattleUnit>>{{0,new[]{b}}},null,position);

        [Test]
        public void PhaseWaitsForGuardDeathsAndOwnTurnNotHealthThreshold()
        {
            var b=Unit();var g=Unit("guard",6,7);var h=Unit("hero",5,7);var s=Session(b,g,h);using var owner=Owner(s);
            b.Character.CurrentHealth=1;owner.CheckPhases();owner.BeginTurn(b);
            Assert.That(b.BanditLeaderState.PhaseTwo,Is.False);Assert.That(StatusEffectSystem.IsInvisible(b),Is.True);
            g.Character.CurrentHealth=0;g.Character.LifeState=CharacterLifeState.Dead;
            s.SkillResolutionDepth=1;owner.CheckPhases();Assert.That(b.BanditLeaderState.PhasePending,Is.False);
            s.SkillResolutionDepth=0;owner.CheckPhases();owner.BeginTurn(h);Assert.That(b.Character.Definition,Is.SameAs(Definition));
            owner.BeginTurn(b);Assert.That(b.BanditLeaderState.PhaseTwo,Is.True);
            Assert.That(b.Character.Skills.Select(k=>k.SkillId),Is.EqualTo(new[]{BanditLeaderBossSystem.CutId,BanditLeaderBossSystem.FireId,BanditLeaderBossSystem.StrikeId}));
        }
        [Test]
        public void OtherEncounterEnemiesDoNotCountAsGuardsAndRebindPreservesPhase()
        {
            var b=Unit();var g=Unit("guard");var h=Unit("hero");var s=Session(b,g,h);
            s.EnemySources[b.Character.CharacterId]=("boss.room","boss");s.EnemySources[g.Character.CharacterId]=("other.room","patrol");
            var owner=Owner(s);Assert.That(b.BanditLeaderState.Guards,Is.Empty);owner.CheckPhases();owner.Dispose(true);
            using var rebound=Owner(s);rebound.BeginTurn(b);Assert.That(b.BanditLeaderState.PhaseTwo,Is.True);
        }
        [TestCase(999,true,4501)] [TestCase(1000,false,4000)]
        public void CommanderPassiveChecksActualHealthLossSincePreviousOwnTurn(int amount,bool hides,int expected)
        {
            var b=Unit();var h=Unit("hero");var s=Session(b,h);using var owner=Owner(s);Reveal(owner,b);
            new StatusEffectSystem().RemoveStatus(b,StatusEffectIds.Invisibility);
            new CombatDamageSystem().ApplyDamage(s,h,b,amount,DamageType.Physical);
            owner.BeginTurn(b);Assert.That(b.Character.CurrentHealth,Is.EqualTo(expected));Assert.That(StatusEffectSystem.IsInvisible(b),Is.EqualTo(hides));
            Assert.That(b.BanditLeaderState.DamageSinceTurn,Is.Zero);
        }
        [Test]
        public void ShieldDamageDoesNotBlockCommanderHealing()
        {
            var b=Unit();var h=Unit("hero");var s=Session(b,h);using var owner=Owner(s);Reveal(owner,b);
            b.Character.CurrentHealth=3000;b.Character.ShieldPool ??= new Godmist.Runtime.Characters.ShieldPool();b.Character.ShieldPool.AddLayer(2000);
            new CombatDamageSystem().ApplyDamage(s,h,b,1500,DamageType.Physical);owner.BeginTurn(b);
            Assert.That(b.Character.CurrentHealth,Is.EqualTo(3500));
        }
        [Test]
        public void TrapsAreHiddenAreaEffectsTriggerOnceAndIgnoreAllies()
        {
            var b=Unit();var g=Unit("guard",3,3);var h=Unit("hero",5,5);var near=Unit("hero",5,6);var far=Unit("hero",9,9);var s=Session(b,g,h,near,far);using var owner=Owner(s);
            Assert.That(Place(s,b,BanditLeaderBossSystem.BearId,new GridPosition(6,7)),Is.True);
            var trap=s.SummonedObjects.Single();Assert.That(trap.Definition.HiddenUntilTriggered,Is.True);Assert.That(trap.CanReceiveHit(),Is.False);
            g.Position=trap.Position;s.NotifyUnitEnteredCell(g);Assert.That(s.SummonedObjects.Count,Is.EqualTo(1));
            h.Position=trap.Position;near.Position=new GridPosition(6,6);s.NotifyUnitEnteredCell(h);
            Assert.That(s.SummonedObjects,Is.Empty);Assert.That(StatusEffectSystem.IsImmobilized(h),Is.True);Assert.That(StatusEffectSystem.IsImmobilized(near),Is.True);Assert.That(StatusEffectSystem.IsImmobilized(far),Is.False);
        }
        [Test]
        public void MineDetonatesAllOwnedTrapsOnceOnPhaseAndDeathCleansWithoutExplosion()
        {
            var b=Unit();var g=Unit("guard",2,2);var h=Unit("hero",5,7);var s=Session(b,g,h);using var owner=Owner(s);
            Assert.That(Place(s,b,BanditLeaderBossSystem.MineId,new GridPosition(6,7)),Is.True);
            g.Character.CurrentHealth=0;g.Character.LifeState=CharacterLifeState.Dead;
            owner.CheckPhases();Assert.That(h.Character.CurrentHealth,Is.EqualTo(5000));owner.BeginTurn(b);
            Assert.That(h.Character.CurrentHealth,Is.EqualTo(4800));Assert.That(s.SummonedObjects,Is.Empty);owner.BeginTurn(b);Assert.That(h.Character.CurrentHealth,Is.EqualTo(4800));
        }
        [Test]
        public void OrderBuffsGuardAndAdvancesHalfBaseTurnWithoutRepeatedBuffSpam()
        {
            var b=Unit();var g=Unit("guard",6,7);var h=Unit("hero",3,7);var s=Session(b,g,h);using var owner=Owner(s);
            g.CurrentActionValue=CombatTurnSystem.CalculateBaseActionValue(g);var start=g.CurrentActionValue;
            Assert.That(Cast(s,b,BanditLeaderBossSystem.OrderId,g),Is.True);
            Assert.That(g.CurrentActionValue,Is.EqualTo(start-.5f*CombatTurnSystem.CalculateBaseActionValue(g)).Within(.01f));
            Assert.That(new CombatVariableSystem().GetMultiplier(g.Character,GameplayVariableType.DamageDealtPercent),Is.EqualTo(1.2f).Within(.001));
            Assert.That(BanditLeaderBossSystem.CanOrderTarget(b,g),Is.False);Assert.That(Cast(s,b,BanditLeaderBossSystem.OrderId,g),Is.False);
        }
        [TestCase(0,1000)] [TestCase(1,700)] [TestCase(2,400)] [TestCase(3,100)] [TestCase(4,100)]
        public void HiddenStrikeIsOneCriticalHitWithFormationProtection(int allies,int expected)
        {
            var b=Unit();var h=Unit("hero",7,8);var units=new List<BattleUnit>{b,h};
            foreach(var p in new[]{(6,8),(8,8),(7,9),(7,7)}.Take(allies))units.Add(Unit("hero",p.Item1,p.Item2));
            var s=Session(units.ToArray());using var owner=Owner(s);Reveal(owner,b);
            var events=0;s.DamageApplied+=(a,t,n,type,id)=>{if(t==h)events++;};
            var preview=CombatSkillPreviewCalculator.CalculateTargetPreview(b,h,Skill(b,BanditLeaderBossSystem.StrikeId));
            Assert.That(Cast(s,b,BanditLeaderBossSystem.StrikeId,h),Is.True);Assert.That(5000-h.Character.CurrentHealth,Is.EqualTo(expected));
            Assert.That(events,Is.EqualTo(1));Assert.That(preview.Damage.Minimum,Is.EqualTo(expected));Assert.That(StatusEffectSystem.IsInvisible(b),Is.False);
        }
        [Test]
        public void PreciseCutRepeatsAtMostOnceWithoutExtraCost()
        {
            var repeats=0;
            for(var seed=0;seed<20;seed++)
            {
                var b=Unit();var h=Unit("hero",7,8);var s=Session(b,h);using var owner=Owner(s);Reveal(owner,b);UnityEngine.Random.InitState(seed);
                Assert.That(Cast(s,b,BanditLeaderBossSystem.CutId,h),Is.True);
                var lost=5000-h.Character.CurrentHealth;Assert.That(lost,Is.EqualTo(0).Or.EqualTo(70).Or.EqualTo(100).Or.EqualTo(170));if(lost==170)repeats++;
                Assert.That(b.CurrentActionPoints,Is.EqualTo(4500));
            }
            Assert.That(repeats,Is.GreaterThan(0));
        }
        [Test]
        public void FireConeHasNineCellsFacesSelectedDirectionAndExcludesBehind()
        {
            var b=Unit();var h=Unit("hero",10,9);var behind=Unit("hero",6,7);var s=Session(b,h,behind);
            var skill=Asset<SkillData>(VS16CatacombsBossSetup.BanditRoot+"SD_Forest_BanditLeader_fire.asset");
            var cells=new CombatTargetingService().GetDirectionalRangePositions(s,b,skill.TargetModes[0].AbsoluteRange,new GridPosition(8,7));
            Assert.That(cells.Count,Is.EqualTo(9));Assert.That(cells,Contains.Item(h.Position));CollectionAssert.DoesNotContain(cells,behind.Position);
        }
        [TestCase(1,0)] [TestCase(0,1)] [TestCase(-1,0)] [TestCase(0,-1)]
        public void FireExecutionDamagesOnlyEnemiesInsideSelectedCone(int dx,int dy)
        {
            var b=Unit();var front=Unit("hero",7+dx,7+dy);var edge=Unit("hero",7+3*dx-2*dy,7+3*dy+2*dx);
            var back=Unit("hero",7-dx,7-dy);var ally=Unit("guard",7+2*dx,7+2*dy);
            var s=Session(b,front,edge,back,ally);using var owner=Owner(s);ally.Character.LifeState=CharacterLifeState.Dead;Reveal(owner,b);ally.Character.LifeState=CharacterLifeState.Alive;
            var skill=Skill(b,BanditLeaderBossSystem.FireId);
            var targets=new CombatTargetingService().GetTargets(s,b,skill.TargetModes[0],front.Position);
            Assert.That(targets,Is.EquivalentTo(new[]{front,edge}));
            Assert.That(Execution().TryExecute(s,b,skill,new Dictionary<int,IReadOnlyList<BattleUnit>>{{0,targets}}),Is.True);
            Assert.That(front.Character.CurrentHealth,Is.EqualTo(4900));Assert.That(edge.Character.CurrentHealth,Is.EqualTo(4900));
            Assert.That(back.Character.CurrentHealth,Is.EqualTo(5000));Assert.That(ally.Character.CurrentHealth,Is.EqualTo(5000));
            Assert.That(b.Character.ResourcePool.CurrentValue,Is.EqualTo(60));Assert.That(b.CurrentActionPoints,Is.EqualTo(6500));
            Assert.That(StatusEffectSystem.IsInvisible(b),Is.False);
            Assert.That(skill.Effects[0].StatusDamageMultiplier,Is.EqualTo(.6f));
        }
        [Test]
        public void BossDeathRemovesOwnedHiddenTrapsWithoutDetonating()
        {
            var b=Unit();var guard=Unit("guard",6,7);var h=Unit("hero",9,7);var s=Session(b,guard,h);using var owner=Owner(s);
            Assert.That(Place(s,b,BanditLeaderBossSystem.MineId,new GridPosition(8,7)),Is.True);
            b.Character.CurrentHealth=0;b.Character.LifeState=CharacterLifeState.Dead;owner.OnBossDied(b);owner.CheckPhases();
            Assert.That(s.SummonedObjects,Is.Empty);Assert.That(s.MapObjects.OfType<SummonedCombatMapObject>(),Is.Empty);
            Assert.That(h.Character.CurrentHealth,Is.EqualTo(5000));Assert.That(b.BanditLeaderState.PhasePending,Is.False);
        }
        [TestCase(10,.35f)] [TestCase(20,.40f)]
        public void CommanderArmorReducesAllThreeOrdersButNotAttacks(int level,float reduction)
        {
            var u=Unit("hero");var armor=Asset<UniqueEquipmentDefinition>("Assets/_Project/ScriptableObjects/Equipment/BossUnique/EID_BanditLeader_WarriorArmor.asset");
            u.Character.Equipment.Armor=new EquipmentItemInstance {Definition=armor,ItemLevel=level};
            var catalog=Asset<GameContentCatalog>(ForestExpeditionsSetup.CatalogPath);
            foreach(var id in new[]{"skill.warrior.destruction","skill.warrior.protection","skill.warrior.regroup","skill.warrior.cut"})
            {
                var skill=catalog.SkillDefinitions.Single(s=>s.SkillId==id);
                var full=CombatActionCostCalculator.CalculatePercentageCost(u.MaxActionPoints,skill.ActionCost);
                Assert.That(CombatActionCostCalculator.CalculateSkillCost(u,skill),Is.EqualTo(Mathf.RoundToInt(full*(id=="skill.warrior.cut"?1:1-reduction))));
            }
        }
        [TestCase(10,.30f,.20f)] [TestCase(20,.29f,.23f)]
        public void PoacherArmorRefreshesAfterFiveOwnTurnsWithoutStacking(int level,float threshold,float shieldPercent)
        {
            var u=Unit("hero");var armor=Asset<UniqueEquipmentDefinition>("Assets/_Project/ScriptableObjects/Equipment/BossUnique/EID_BanditLeader_PaladinArmor.asset");
            u.Character.Equipment.Armor=new EquipmentItemInstance {Definition=armor,ItemLevel=level};
            u.Character.ShieldPool ??= new Godmist.Runtime.Characters.ShieldPool();
            u.Character.CurrentHealth=Mathf.RoundToInt(5000*threshold);UniqueEquipmentCombatRules.TryPoacherShield(u);Assert.That(u.Character.ShieldPool.TotalShield,Is.Zero);
            u.Character.CurrentHealth--;UniqueEquipmentCombatRules.TryPoacherShield(u);Assert.That(u.PoacherArmorCooldown,Is.EqualTo(5));
            var expected=Mathf.RoundToInt(5000*shieldPercent);Assert.That(u.Character.ShieldPool.TotalShield,Is.EqualTo(expected));
            u.Character.ShieldPool.AbsorbDamage(expected);UniqueEquipmentCombatRules.TryPoacherShield(u);Assert.That(u.Character.ShieldPool.TotalShield,Is.Zero);
            for(var i=0;i<4;i++)UniqueEquipmentCombatRules.BeginTurn(u);Assert.That(u.Character.ShieldPool.TotalShield,Is.Zero);
            UniqueEquipmentCombatRules.BeginTurn(u);Assert.That(u.Character.ShieldPool.TotalShield,Is.EqualTo(expected));Assert.That(u.PoacherArmorCooldown,Is.EqualTo(5));
            for(var i=0;i<5;i++)UniqueEquipmentCombatRules.BeginTurn(u);Assert.That(u.Character.ShieldPool.TotalShield,Is.EqualTo(expected));
            u.Character.CurrentHealth=5000;for(var i=0;i<5;i++)UniqueEquipmentCombatRules.BeginTurn(u);Assert.That(u.Character.ShieldPool.TotalShield,Is.Zero);
        }
        [TestCase(10)] [TestCase(20)]
        public void FangDaggerCriticalProcsCarryRealBleedDamage(int level)
        {
            var u=Unit("hero");var t=Unit("guard");u.Character.Stats.CritChance=new Stat(1);
            u.Character.Equipment.Weapon=new EquipmentItemInstance {Definition=Asset<UniqueEquipmentDefinition>("Assets/_Project/ScriptableObjects/Equipment/BossUnique/EID_BanditLeader_ScoutDagger.asset"),ItemLevel=level};
            new PassiveEffectReconciliationSystem().Reconcile(u.Character);
            var s=Session(u,t);s.PassiveEffectEventDispatcher=new PassiveEffectCombatEventDispatcher();
            using var executor=new PassiveEffectCombatExecutionSystem(s,new StatusEffectSystem());
            for(var i=0;i<20 && !StatusEffectSystem.HasStatus(t,StatusEffectIds.Bleed);i++)new CombatDamageSystem().ResolveAndApplyDamage(s,u,t,1,DamageType.Physical,true);
            Assert.That(StatusEffectSystem.HasStatus(t,StatusEffectIds.Bleed),Is.True);
            Assert.That(t.Character.PassiveEffects.First(e=>e.EffectId==StatusEffectIds.Bleed).TotalDamage,Is.GreaterThan(0));
        }
        [Test]
        public void ProductionEncounterStatsLootAndOfferRegistrationAreSeparateFromEnt()
        {
            var encounter=Asset<EncounterDefinition>(VS16CatacombsBossSetup.BanditEncounterPath);Assert.That(encounter.Enemies.Count,Is.EqualTo(4));Assert.That(encounter.BossEnemyIndex,Is.Zero);
            Assert.That(encounter.Enemies.Skip(1).Count(e=>e.CharacterDefinition.Id=="enemy.forest.bandit_thug"),Is.EqualTo(2));
            Assert.That(encounter.Enemies.Last().CharacterDefinition.Id,Is.EqualTo("enemy.forest.bandit_shooter"));
            Assert.That(Definition.BaseStats.MaxHealth,Is.EqualTo(280));Assert.That(Definition.GrowthData.MaxHealth,Is.EqualTo(33f));Assert.That(Definition.LootTable,Is.Null); // PT-06.
            var dungeon=Asset<DungeonDefinition>(VS16CatacombsBossSetup.BanditDungeonPath);var ent=Asset<DungeonDefinition>(ForestExpeditionsSetup.BossPath);
            Assert.That(dungeon.BossEncounterDefinition,Is.SameAs(encounter));Assert.That(dungeon.FixedOfferLevels,Is.EqualTo(new[]{10,20}));Assert.That(dungeon.LootTableCatalog,Is.Not.SameAs(ent.LootTableCatalog));
            var bag=Asset<LootBagItemDefinition>(VS16CatacombsBossSetup.BanditBagPath);Assert.That(bag.BossUniqueDrop.EquipmentDefinitions.Count,Is.EqualTo(4));
            Assert.That(bag.BossUniqueDrop.EquipmentDefinitions.All(u=>u.Id.StartsWith("unique.bandit_leader.")),Is.True);
            var catalog=Asset<GameContentCatalog>(ForestExpeditionsSetup.CatalogPath);Assert.That(catalog.DungeonDefinitions.Count(d=>d==dungeon),Is.EqualTo(1));
            var blood=catalog.ItemDefinitions.Single(i=>i!=null && i.Id=="item.material.bandit_leader_blood");
            Assert.That(blood.DisplayName,Is.EqualTo("Krew przywódcy"));Assert.That(blood.BaseValue,Is.EqualTo(6258));Assert.That(blood.BaseRarity,Is.EqualTo(RarityTier.Ancient));
            Assert.That(blood.Icon,Is.Not.Null);Assert.That(catalog.ItemDefinitions.Any(i=>i!=null && i.Id=="item.material.bandit_leader_signet"),Is.False);
            var bloodPool=bag.Brackets.SelectMany(b=>b.Pools).Single(p=>p.Entries.Any(e=>e.ItemDefinition==blood));
            Assert.That(bloodPool.DropChance,Is.EqualTo(.75f));Assert.That(bloodPool.Entries.Single(e=>e.ItemDefinition==blood).Amount.Min,Is.EqualTo(1));
            foreach(var seed in new[]{12,53,145})
            {
                var generated=new DungeonGeneratorService().Generate(dungeon,seed,10);new DungeonEncounterPlacementSystem().Place(generated);
                Assert.That(generated.Floors.SelectMany(f=>f.Encounters).Count(e=>e.Definition==encounter),Is.EqualTo(1));
            }
            foreach(var level in new[]{10,20})for(var seed=0;seed<20;seed++)
            {
                var rolled=new LootTableSystem(new System.Random(seed),catalog.EquipmentPartDefinitions,lootLevel:level).CreateLootBag(bag);
                Assert.That(rolled.Contents.Where(i=>i.Definition is UniqueEquipmentDefinition).All(i=>i.Definition.Id.StartsWith("unique.bandit_leader.")),Is.True);
            }
        }
        [Test]
        public void AiUsesOrderWithoutSpammingSameGuardThenSetsTraps()
        {
            var b=Unit();var guard=Unit("guard",6,7);var hero=Unit("hero",3,7);var s=Session(b,guard,hero);using var owner=Owner(s);
            var ai=new SimpleCombatAiSystem(skillExecutionSystem:Execution());var turn=ai.CreateTurnState();
            Assert.That(ai.TryExecuteNextAction(s,b,turn,out var first),Is.True);Assert.That(first.Skill.SkillId,Is.EqualTo(BanditLeaderBossSystem.OrderId));
            Assert.That(ai.TryExecuteNextAction(s,b,turn,out var next),Is.True);
            Assert.That(next.Type,Is.EqualTo(SimpleCombatAiActionType.Skill),$"AP {b.CurrentActionPoints}/{b.MaxActionPoints}; bear cost {CombatActionCostCalculator.CalculateSkillCost(b,Skill(b,BanditLeaderBossSystem.BearId))}");
            Assert.That(next.Skill.SkillId,Is.Not.EqualTo(BanditLeaderBossSystem.OrderId));
            Assert.That(s.SummonedObjects.Count,Is.EqualTo(1));
        }
        [TestCase(10)] [TestCase(20)]
        public void ForestOffersChooseBothBossesAndKeepChoiceAcrossSave(int level)
        {
            var catalog=Asset<GameContentCatalog>(ForestExpeditionsSetup.CatalogPath);
            var normal=Asset<DungeonDefinition>(ForestExpeditionsSetup.RegularPath);var ent=Asset<DungeonDefinition>(ForestExpeditionsSetup.BossPath);var bandit=Asset<DungeonDefinition>(VS16CatacombsBossSetup.BanditDungeonPath);
            var pool=new[]{normal,ent,bandit};var seen=new HashSet<string>();
            for(var seed=0;seed<32;seed++)
            {
                var game=new GameSession(campaignId:"bandit.offer."+seed);var hero=Unit("hero").Character;hero.Level=level;
                game.TryRegisterCharacter(hero);game.PlayerParty.TryAddActiveMember(hero.CharacterId);if(level==20)game.DungeonOffers.MarkMilestoneProcessed(10);
                game.DungeonOffers.SetDanger(DungeonTheme.Forest,100);Assert.That(DungeonOfferSystem.EnsureOffers(game,pool),Is.True);
                var offer=game.DungeonOffers.Offers[0];Assert.That(offer.FixedLevel,Is.EqualTo(level));seen.Add(offer.DefinitionId);
                var restored=new GameSession(campaignId:game.CampaignId);restored.TryRegisterCharacter(hero);restored.PlayerParty.TryAddActiveMember(hero.CharacterId);
                DungeonOfferPersistence.Restore(DungeonOfferPersistence.Capture(game.DungeonOffers),restored.DungeonOffers);
                DungeonOfferSystem.EnsureOffers(restored,pool.Reverse());Assert.That(restored.DungeonOffers.Offers[0].DefinitionId,Is.EqualTo(offer.DefinitionId));
            }
            Assert.That(seen,Is.EquivalentTo(new[]{ent.Id,bandit.Id}));
        }
        [Test]
        public void RealSessionFactorySpawnsAllFourEnemiesAndThreeGuards()
        {
            var game=new GameSession();var hero=Unit("hero").Character;game.TryRegisterCharacter(hero);game.PlayerParty.TryAddActiveMember(hero.CharacterId);
            Assert.That(new CombatSessionFactory().TryCreate(game,Asset<EncounterDefinition>(VS16CatacombsBossSetup.BanditEncounterPath),out var session),Is.True);
            var boss=session.Units.Single(u=>u.Character.Definition==Definition);using var owner=Owner(session);
            Assert.That(session.Units.Count(u=>!u.IsPlayerSide),Is.EqualTo(4));Assert.That(boss.BanditLeaderState.Guards.Count,Is.EqualTo(3));
            Assert.That(session.Units.Select(u=>u.Position).Distinct().Count(),Is.EqualTo(5));Assert.That(session.Units.All(u=>session.Grid.IsWalkable(u.Position)),Is.True);
        }
    }
}

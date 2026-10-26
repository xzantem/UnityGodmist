using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Editor;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEditor;

namespace Godmist.Tests.EditMode
{
    public sealed class DeadKingV2Tests
    {
        static CharacterDefinition King => AssetDatabase.LoadAssetAtPath<CharacterDefinition>(VS16CatacombsBossSetup.KingPath);
        static BattleUnit Unit(CharacterDefinition definition, bool player, int x, int y, int level=10)
        {
            var unit=new BattleUnit { Character=new CharacterInstanceFactory().Create(definition,definition.ClassData,level),Position=new GridPosition(x,y),IsPlayerSide=player,CurrentActionPoints=10000,MaxActionPoints=10000 };
            unit.Character.CharacterId += Guid.NewGuid();unit.Character.ResourcePool.CurrentValue=unit.Character.ResourcePool.MaxValue;
            unit.Character.Stats.CritChance=new Stat(0);unit.Character.Stats.Dodge=new Stat(0);
            return unit;
        }
        static CombatSession Session(params BattleUnit[] units)
        {
            var s=new CombatSession { CombatId="king.v2",Grid=new BattleGrid(15,15),Units=units.ToList(),ActiveUnit=units[0] };
            foreach(var u in units)u.WarriorState.Session=s;
            return s;
        }
        static DeadKingBossSystem Owner(CombatSession s)=>new(s,new CombatDamageSystem(),new StatusEffectSystem());
        static void Phase(DeadKingBossSystem owner,BattleUnit boss) {boss.Character.CurrentHealth=(int)(boss.Character.Stats.MaxHealth.GetValue()*.5f);owner.CheckPhases();owner.BeginTurn(boss);boss.CurrentActionPoints=boss.MaxActionPoints=10000;}
        static BattleUnit Hero(int x,int y)=>Unit(AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/ScriptableObjects/Catacombs/Enemies/CD_Catacombs_swordsman.asset"),true,x,y);
        [TestCase(10)] [TestCase(20)]
        public void AuthoredPhasesMatchWorkbookAndKeepRewardDefinition(int level)
        {
            var boss=Unit(King,false,7,7,level);var s=Session(boss);using var owner=Owner(s);
            Assert.That(boss.Character.Skills.Select(k=>k.DisplayName),Is.EqualTo(new[]{"Widmowa Fala","Rozkaz Króla","Straż!"}));
            boss.CurrentActionPoints=1000;var hp=boss.Character.CurrentHealth;var ap=boss.CurrentActionPoints;
            boss.Character.CurrentHealth=hp/2;s.SkillResolutionDepth=1;owner.CheckPhases();Assert.That(boss.DeadKingState.PhaseTwo,Is.False);
            s.SkillResolutionDepth=0;owner.CheckPhases();Assert.That(boss.DeadKingState.PhaseTwo,Is.False);
            Assert.That(boss.DeadKingState.PhaseTwoPending,Is.True);owner.BeginTurn(boss);Assert.That(boss.DeadKingState.PhaseTwo,Is.True);
            Assert.That(boss.Character.CurrentHealth,Is.EqualTo(hp/2));Assert.That(boss.CurrentActionPoints,Is.EqualTo(ap));
            Assert.That(boss.Character.Skills.Select(k=>k.DisplayName),Is.EqualTo(new[]{"Widmowe Ostrze","Krzyk Cienia","Cień Destrukcji"}));
            Assert.That(boss.Character.Definition.LootTable,Is.SameAs(King.LootTable));
            owner.CheckPhases();Assert.That(boss.Character.CurrentHealth,Is.EqualTo(hp/2));
        }
        [Test]
        public void PendingPhaseWaitsForOwnTurnSurvivesHealingAndRebindAndRunsOnce()
        {
            var boss=Unit(King,false,7,7);var hero=Hero(1,1);var session=Session(boss,hero);
            var feedback=new CombatFeedbackBus();var transitions=0;
            feedback.Published += e => { if(e.EffectName == "Widmowy Szał") transitions++; };
            var owner=new DeadKingBossSystem(session,new CombatDamageSystem(),new StatusEffectSystem(),feedback:feedback);
            boss.Character.CurrentHealth=(int)(boss.Character.Stats.MaxHealth.GetValue()*.5f);
            owner.CheckPhases();owner.BeginTurn(hero);
            Assert.That(boss.Character.Definition,Is.SameAs(King));Assert.That(transitions,Is.Zero);
            boss.Character.CurrentHealth=(int)boss.Character.Stats.MaxHealth.GetValue();
            owner.Dispose(true);
            using var rebound=new DeadKingBossSystem(session,new CombatDamageSystem(),new StatusEffectSystem(),feedback:feedback);
            rebound.BeginTurn(boss);rebound.BeginTurn(boss);
            Assert.That(boss.DeadKingState.PhaseTwo,Is.True);Assert.That(transitions,Is.EqualTo(1));
        }
        [Test]
        public void DirectDamageLatchesPhaseWithoutTransformingDuringTheAttack()
        {
            var boss=Unit(King,false,7,7);var hero=Hero(1,1);var session=Session(boss,hero);using var owner=Owner(session);
            new CombatDamageSystem().ApplyDamage(session,hero,boss,boss.Character.CurrentHealth*3/4,DamageType.Magical);
            Assert.That(boss.DeadKingState.PhaseTwoPending,Is.True);Assert.That(boss.DeadKingState.PhaseTwo,Is.False);
            owner.BeginTurn(hero);Assert.That(boss.Character.Definition,Is.SameAs(King));
            owner.BeginTurn(boss);Assert.That(boss.DeadKingState.PhaseTwo,Is.True);
        }
        [Test]
        public void TransformationDismissesOnlyTheTransformingKingsKnights()
        {
            var first=Unit(King,false,3,3);var second=Unit(King,false,10,10);var hero=Hero(0,0);
            var session=Session(first,second,hero);using var owner=Owner(session);var execute=new SkillExecutionSystem();
            foreach(var king in new[]{first,second})
            {
                session.ActiveUnit=king;
                Assert.That(execute.TryExecute(session,king,king.Character.Skills[2],
                    new Dictionary<int,IReadOnlyList<BattleUnit>>{{0,new[]{king}}},null,
                    new GridPosition(king.Position.X-1,king.Position.Y)),Is.True);
            }
            Assert.That(session.Summons.Count,Is.EqualTo(6));
            Phase(owner,first);
            Assert.That(session.Summons.Count,Is.EqualTo(3));
            Assert.That(session.Summons.All(s=>s.CreatorCharacterId==second.Character.CharacterId),Is.True);
        }
        [Test]
        public void DeadKingDoesNotConsumePendingTransformation()
        {
            var boss=Unit(King,false,7,7);var session=Session(boss);using var owner=Owner(session);
            boss.Character.CurrentHealth=1;owner.CheckPhases();
            boss.Character.CurrentHealth=0;boss.Character.LifeState=CharacterLifeState.Dead;
            owner.BeginTurn(boss);
            Assert.That(boss.DeadKingState.PhaseTwo,Is.False);Assert.That(boss.DeadKingState.PhaseTwoPending,Is.False);
        }
        [Test]
        public void PhasePassivesAppearWithDedicatedIdsWithoutMutatingCharacterEffects()
        {
            var boss=Unit(King,false,7,7);var session=Session(boss);using var owner=Owner(session);
            var count=boss.Character.PassiveEffects.Count;
            var signature=typeof(Godmist.Views.UI.CombatUnitInspectWindowView).GetMethod("BuildStatusSignature",
                System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            var before=signature.Invoke(null,new object[]{boss.Character.PassiveEffects,null,null,boss});
            var first=Godmist.Views.UI.CombatStatusPresentationFactory.Create(boss.Character.PassiveEffects,owner:boss);
            Assert.That(first.Count(e=>e.Effect.EffectId=="catacombs.dead_king.despair"),Is.EqualTo(1));
            Phase(owner,boss);
            var second=Godmist.Views.UI.CombatStatusPresentationFactory.Create(boss.Character.PassiveEffects,owner:boss);
            Assert.That(second.Count(e=>e.Effect.EffectId=="catacombs.dead_king.rage"),Is.EqualTo(1));
            Assert.That(second.Any(e=>e.Effect.EffectId=="catacombs.dead_king.despair"),Is.False);
            Assert.That(boss.Character.PassiveEffects.Count,Is.EqualTo(count));
            Assert.That(signature.Invoke(null,new object[]{boss.Character.PassiveEffects,null,null,boss}),Is.Not.EqualTo(before));
        }
        [Test]
        public void PhasePassivesApplyOnlyToCorrectSideAndAttackCategory()
        {
            var boss=Unit(King,false,7,7);var ally=Hero(8,7);ally.IsPlayerSide=false;var hero=Hero(1,1);var s=Session(boss,ally,hero);using var owner=Owner(s);
            Assert.That(DeadKingBossSystem.DamageMultiplier(s,hero,ally,new PassiveEffectCombatContext(attackType:AttackType.Ranged)),Is.EqualTo(.75f));
            Assert.That(DeadKingBossSystem.DamageMultiplier(s,hero,ally,new PassiveEffectCombatContext(attackType:AttackType.Melee)),Is.EqualTo(1f));
            Phase(owner,boss);
            Assert.That(DeadKingBossSystem.DamageMultiplier(s,boss,hero,null),Is.EqualTo(1.3f));
            Assert.That(DeadKingBossSystem.DamageMultiplier(s,hero,boss,null),Is.EqualTo(1.5f));
            Assert.That(DeadKingBossSystem.DamageMultiplier(s,hero,ally,new PassiveEffectCombatContext(attackType:AttackType.Ranged)),Is.EqualTo(1f));
        }
        [Test]
        public void KnightsIgnoreDotsAndDieOnThirdDamagingHitEvenAgainstHugeDamage()
        {
            var knight=Unit(AssetDatabase.LoadAssetAtPath<CharacterDefinition>(VS16CatacombsBossSetup.KnightPath),false,2,2);
            var hero=Hero(1,2);var s=Session(hero,knight);var damage=new CombatDamageSystem();
            foreach(var type in new[]{DamageType.Bleed,DamageType.Poison,DamageType.Burn})damage.ApplyDamage(s,hero,knight,9999,type);
            Assert.That(knight.Character.CurrentHealth,Is.EqualTo(3));Assert.That(knight.DeadKingKnightHits,Is.Zero);
            for(var hit=1;hit<=3;hit++)
            {
                damage.ApplyDamage(s,hero,knight,9999,DamageType.Magical);
                Assert.That(knight.DeadKingKnightHits,Is.EqualTo(hit));
                Assert.That(knight.Character.CurrentHealth,Is.EqualTo(3-hit));
            }
            Assert.That(knight.Character.LifeState,Is.EqualTo(CharacterLifeState.Dead));
        }
        [Test]
        public void ShadowHasTenSafeFieldsWaitsTwoBossTurnsAndGuaranteesSelfStun()
        {
            var boss=Unit(King,false,7,7);var hero=Hero(1,1);hero.Character.Stats.MaxHealth=new Stat(100000);hero.Character.CurrentHealth=100000;
            var s=Session(boss,hero);using var owner=Owner(s);Phase(owner,boss);
            Assert.That(new SkillExecutionSystem().TryExecute(s,boss,boss.Character.Skills[2],boss),Is.True);
            Assert.That(boss.DeadKingState.SafeFields.Count,Is.EqualTo(10));Assert.That(boss.DeadKingState.ShadowTurns,Is.EqualTo(2));
            hero.Position=boss.DeadKingState.DarkFields.First();var hp=hero.Character.CurrentHealth;
            owner.BeginTurn(hero);Assert.That(boss.DeadKingState.ShadowTurns,Is.EqualTo(2));
            owner.BeginTurn(boss);Assert.That(hero.Character.CurrentHealth,Is.EqualTo(hp));
            owner.BeginTurn(boss);Assert.That(hero.Character.CurrentHealth,Is.LessThan(hp));
            Assert.That(StatusEffectSystem.IsStunned(boss),Is.True);Assert.That(boss.DeadKingState.Markers,Is.Empty);
            Assert.That(s.SummonedObjects,Is.Empty);
        }
        [Test]
        public void SafeFieldProtectsTargetAndRebindingPreservesCountdown()
        {
            var boss=Unit(King,false,7,7);var hero=Hero(1,1);var s=Session(boss,hero);var owner=Owner(s);Phase(owner,boss);
            Assert.That(new SkillExecutionSystem().TryExecute(s,boss,boss.Character.Skills[2],boss),Is.True);
            hero.Position=boss.DeadKingState.SafeFields.First();var hp=hero.Character.CurrentHealth;owner.Dispose(true);
            using var rebound=Owner(s);rebound.BeginTurn(boss);rebound.BeginTurn(boss);
            Assert.That(hero.Character.CurrentHealth,Is.EqualTo(hp));Assert.That(StatusEffectSystem.IsStunned(boss),Is.True);
        }
        [Test]
        public void CommandIsExclusiveAndLastsThreeTurns()
        {
            var boss=Unit(King,false,7,7);var first=Hero(6,7);var second=Hero(7,6);var s=Session(boss,first,second);using var owner=Owner(s);var execute=new SkillExecutionSystem();
            var skill=boss.Character.Skills[1];Assert.That(execute.TryExecute(s,boss,skill,first),Is.True);Assert.That(DeadKingBossSystem.Marked(first),Is.True);
            Assert.That(first.Character.PassiveEffects.Single(e=>e.Sources.Any(src=>src.SourceId.StartsWith(DeadKingBossSystem.CommandId))).RemainingDuration,Is.EqualTo(3));
            boss.CurrentActionPoints=10000;Assert.That(execute.TryExecute(s,boss,skill,second),Is.True);
            Assert.That(DeadKingBossSystem.Marked(first),Is.False);Assert.That(DeadKingBossSystem.Marked(second),Is.True);
            Assert.That(DeadKingBossSystem.DamageMultiplier(s,boss,second,null),Is.EqualTo(1.2f));
        }
        [Test]
        public void BladeTeleportsBeforeDamageAndGuardFillsToThreeWithUniqueIds()
        {
            var boss=Unit(King,false,7,7);var hero=Hero(11,7);var s=Session(boss,hero);using var owner=Owner(s);
            var guard=boss.Character.Skills[2];var execute=new SkillExecutionSystem();
            Assert.That(execute.TryExecute(s,boss,guard,new Dictionary<int,IReadOnlyList<BattleUnit>>{{0,new[]{boss}}},null,new GridPosition(6,7)),Is.True);
            Assert.That(s.Summons.Count,Is.EqualTo(3));Assert.That(s.Summons.Select(x=>x.Unit.Character.CharacterId).Distinct().Count(),Is.EqualTo(3));
            var knights=s.Summons.Select(x=>x.Unit).ToArray();
            boss.Character.CurrentHealth=(int)(boss.Character.Stats.MaxHealth.GetValue()*.5f);
            owner.CheckPhases();Assert.That(boss.DeadKingState.PhaseTwoPending,Is.True);
            Assert.That(s.Summons.Count,Is.EqualTo(3));
            Phase(owner,boss);Assert.That(s.Summons,Is.Empty);
            Assert.That(knights.All(k=>!s.Units.Contains(k) && !s.TurnOrder.Contains(k)
                && !s.TurnParticipants.Any(p=>p.Unit==k)),Is.True);
            Assert.That(knights.All(k=>k.Character.LifeState==CharacterLifeState.Alive),Is.True);
            var hp=hero.Character.CurrentHealth;
            Assert.That(execute.TryExecute(s,boss,boss.Character.Skills[0],hero),Is.True);
            Assert.That(Math.Max(Math.Abs(boss.Position.X-hero.Position.X),Math.Abs(boss.Position.Y-hero.Position.Y)),Is.EqualTo(1));
            Assert.That(hero.Character.CurrentHealth,Is.LessThan(hp));
        }
        [Test]
        public void ShadowDoesNotSpendResourcesWithoutTenReachableFields()
        {
            var boss=Unit(King,false,1,1);var hero=Hero(0,0);var s=Session(boss,hero);
            s.Grid=new BattleGrid(3,3);using var owner=Owner(s);Phase(owner,boss);
            var mana=boss.Character.ResourcePool.CurrentValue;var ap=boss.CurrentActionPoints;
            Assert.That(new SkillExecutionSystem().TryExecute(s,boss,boss.Character.Skills[2],boss),Is.False);
            Assert.That(boss.Character.ResourcePool.CurrentValue,Is.EqualTo(mana));
            Assert.That(boss.CurrentActionPoints,Is.EqualTo(ap));
            Assert.That(s.SummonedObjects,Is.Empty);
        }
        [Test]
        public void AiUsesAffordableShadowAndDeathClearsArena()
        {
            var boss=Unit(King,false,7,7);var hero=Hero(1,1);var s=Session(boss,hero);
            using var owner=Owner(s);Phase(owner,boss);
            var ai=new SimpleCombatAiSystem(skillExecutionSystem:new SkillExecutionSystem());
            Assert.That(ai.TryExecuteNextAction(s,boss,ai.CreateTurnState(),out var action),Is.True);
            Assert.That(action.Skill.SkillId,Is.EqualTo(DeadKingBossSystem.ShadowId));
            Assert.That(s.SummonedObjects,Is.Not.Empty);
            owner.OnBossDied(boss);
            Assert.That(s.SummonedObjects,Is.Empty);Assert.That(boss.DeadKingState.ShadowTurns,Is.Zero);
        }
    }
}

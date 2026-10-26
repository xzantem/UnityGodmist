using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
namespace Godmist.Tests.EditMode
{
    public class ScoutMechanicsTests
    {
        readonly List<Object> assets = new();
        [TearDown] public void Cleanup() { foreach(var a in assets)Object.DestroyImmediate(a);assets.Clear(); }
        BattleUnit Unit(bool player,int x)
        {
            var d=ScriptableObject.CreateInstance<CharacterDefinition>();assets.Add(d);
            return new BattleUnit { IsPlayerSide=player,Position=new GridPosition(x,1),MaxKnives=5,AvailableKnives=4,
                Character=new CharacterInstance { CharacterId=System.Guid.NewGuid().ToString(),Definition=d,LifeState=CharacterLifeState.Alive,CurrentHealth=1000,
                    Stats=new CharacterStats { MaxHealth=new Stat(1000),MinAttack=new Stat(100),MaxAttack=new Stat(100),PhysicalDefense=new Stat(0),MagicalDefense=new Stat(0),CritChance=new Stat(0),CritDamage=new Stat(1) },
                    SkillTreeProgress=new(),PassiveEffects=new(),Resistances=new ResistanceSet(),BaseResistances=new ResistanceSet() } };
        }
        void Rank(BattleUnit u,string tree,string node,int rank)
        {
            var p=u.Character.SkillTreeProgress.FirstOrDefault(x=>x.TreeId==tree);
            if(p==null){p=new(){TreeId=tree};u.Character.SkillTreeProgress.Add(p);}
            p.NodeInvestments.Add(new(){NodeId="node.scout."+(tree==ScoutCombatSystem.BladeTree?"blade_dancer.":"assassin.")+node,Ranks=rank});
        }

        BattleUnit RecoveryUnit(int resource = 50)
        {
            var unit = Unit(true, 1);
            // The speed-step refresh synchronizes the pool from runtime stats.
            unit.Character.Stats.MaxResource = new Stat(200);
            unit.Character.ResourcePool = new ResourcePool(ResourceType.Momentum, resource, 200, 0);
            unit.MaxActionPoints = unit.CurrentActionPoints = 10000;
            return unit;
        }

        [Test]
        public void GroundRecoveryRewardsOnePickupRatherThanEveryKnifeAndCannotRepeat()
        {
            var owner = RecoveryUnit(); owner.AvailableKnives = 1;
            var session = new CombatSession { Grid = new BattleGrid(10, 6), Units = new() { owner }, ActiveUnit = owner };
            session.MapObjects.Add(new KnifeCombatMapObject("stack", owner.Position, owner.Character.CharacterId, 2));
            session.MapObjects.Add(new KnifeCombatMapObject("single", owner.Position, owner.Character.CharacterId, 1));
            var events = new List<CombatFeedbackEvent>(); var bus = new CombatFeedbackBus(); bus.Published += events.Add;
            var recovery = new CombatRecoverySystem(feedbackBus: bus);
            Assert.That(ScoutKnifeSystem.RecoverAtPosition(session, owner, recovery), Is.EqualTo(3));
            Assert.That(owner.AvailableKnives, Is.EqualTo(4));
            Assert.That(owner.Character.ResourcePool.CurrentValue, Is.EqualTo(58));
            Assert.That(events.Single().Amount, Is.EqualTo(8));
            Assert.That(events[0].Type, Is.EqualTo(CombatFeedbackType.ResourceGain));
            Assert.That(events[0].ResourceType, Is.EqualTo(ResourceType.Momentum));
            Assert.That(ScoutKnifeSystem.RecoverAtPosition(session, owner, recovery), Is.Zero);
            Assert.That(owner.Character.ResourcePool.CurrentValue, Is.EqualTo(58)); Assert.That(events.Count, Is.EqualTo(1));
        }

        [Test]
        public void PartialGroundRecoveryKeepsTheRestOfTheStackAndFullInventoryCannotFarmMomentum()
        {
            var owner = RecoveryUnit();
            var session = new CombatSession();
            var knife = new KnifeCombatMapObject("stack", owner.Position, owner.Character.CharacterId, 3);
            session.MapObjects.Add(knife);
            Assert.That(ScoutKnifeSystem.RecoverAtPosition(session, owner), Is.EqualTo(1));
            Assert.That(knife.KnifeCount, Is.EqualTo(2)); Assert.That(session.MapObjects, Contains.Item(knife));
            Assert.That(owner.Character.ResourcePool.CurrentValue, Is.EqualTo(58));
            Assert.That(ScoutKnifeSystem.RecoverAtPosition(session, owner), Is.Zero);
            Assert.That(knife.KnifeCount, Is.EqualTo(2)); Assert.That(owner.Character.ResourcePool.CurrentValue, Is.EqualTo(58));
        }

        [TestCase(150, 158, 8)]
        [TestCase(197, 200, 3)]
        [TestCase(200, 200, 0)]
        public void RecoveryIsFlatAboveOneHundredButClampedWithAccurateFeedback(int initial, int expected, int feedback)
        {
            var owner = RecoveryUnit(initial); var session = new CombatSession();
            session.MapObjects.Add(new KnifeCombatMapObject("knife", owner.Position, owner.Character.CharacterId, 1));
            var events = new List<CombatFeedbackEvent>(); var bus = new CombatFeedbackBus(); bus.Published += events.Add;
            Assert.That(ScoutKnifeSystem.RecoverAtPosition(session, owner, new CombatRecoverySystem(feedbackBus: bus)), Is.EqualTo(1));
            Assert.That(owner.Character.ResourcePool.CurrentValue, Is.EqualTo(expected));
            Assert.That(events.Sum(e => e.Amount), Is.EqualTo(feedback));
            Assert.That(events.Count, Is.EqualTo(feedback > 0 ? 1 : 0));
        }

        [TestCase("ground")]
        [TestCase("skill")]
        [TestCase("extract")]
        public void EveryRecoveryRouteRespectsResourceGainBlock(string route)
        {
            var owner = RecoveryUnit(); var enemy = Unit(false, 2);
            owner.Character.PassiveEffects.Add(new Godmist.Runtime.Effects.PassiveEffectRuntime { BlocksResourceRegeneration = true });
            var session = new CombatSession { Grid = new BattleGrid(10, 6), Units = new() { owner, enemy }, ActiveUnit = owner };
            if (route == "ground")
            {
                session.MapObjects.Add(new KnifeCombatMapObject("knife", owner.Position, owner.Character.CharacterId, 1));
                Assert.That(ScoutKnifeSystem.RecoverAtPosition(session, owner), Is.EqualTo(1));
            }
            else if (route == "skill")
            {
                var position = new GridPosition(3, 1);
                session.MapObjects.Add(new KnifeCombatMapObject("knife", position, owner.Character.CharacterId, 1));
                var hook = AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Skills/SD_ScoutHook.asset");
                Assert.That(ScoutCombatSystem.RecoverKnife(session, owner, hook, position), Is.True);
            }
            else
            {
                enemy.ScoutState.Knives.Add(new() { Owner = owner, BleedMultiplier = .6f });
                Assert.That(ScoutCombatSystem.TryExtractKnife(session, owner, enemy, new StatusEffectSystem()), Is.True);
            }
            Assert.That(owner.AvailableKnives, Is.EqualTo(5)); Assert.That(owner.Character.ResourcePool.CurrentValue, Is.EqualTo(50));
        }

        [TestCase(ResourceType.Mana)]
        [TestCase(ResourceType.Rage)]
        [TestCase(ResourceType.None)]
        public void KnifeRecoveryDoesNotAwardOtherResourceTypes(ResourceType type)
        {
            var owner = RecoveryUnit(); owner.Character.ResourcePool.ResourceType = type;
            var session = new CombatSession(); session.MapObjects.Add(new KnifeCombatMapObject("knife", owner.Position, owner.Character.CharacterId, 1));
            Assert.That(ScoutKnifeSystem.RecoverAtPosition(session, owner), Is.EqualTo(1));
            Assert.That(owner.Character.ResourcePool.CurrentValue, Is.EqualTo(50));
        }

        [Test]
        public void GroundRecoveryRejectsForeignEmptyAndDistantKnivesWithoutReward()
        {
            var owner = RecoveryUnit(); var session = new CombatSession();
            session.MapObjects.Add(new KnifeCombatMapObject("foreign", owner.Position, "other", 1));
            session.MapObjects.Add(new KnifeCombatMapObject("distant", new GridPosition(4, 1), owner.Character.CharacterId, 1));
            session.MapObjects.Add(new KnifeCombatMapObject("empty", owner.Position, owner.Character.CharacterId, 1) { KnifeCount = 0 });
            Assert.That(ScoutKnifeSystem.RecoverAtPosition(session, owner), Is.Zero);
            Assert.That(owner.AvailableKnives, Is.EqualTo(4)); Assert.That(owner.Character.ResourcePool.CurrentValue, Is.EqualTo(50));
        }

        [Test]
        public void SkillRecoveryRewardsTheWholeStackOnceAndRejectsRepeat()
        {
            var owner = RecoveryUnit(); owner.AvailableKnives = 1;
            var session = new CombatSession { Grid = new BattleGrid(10, 6), Units = new() { owner }, ActiveUnit = owner };
            var position = new GridPosition(3, 1);
            session.MapObjects.Add(new KnifeCombatMapObject("stack", position, owner.Character.CharacterId, 3));
            var hook = AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Skills/SD_ScoutHook.asset");
            Assert.That(ScoutCombatSystem.RecoverKnife(session, owner, hook, position), Is.True);
            Assert.That(owner.AvailableKnives, Is.EqualTo(4)); Assert.That(owner.Character.ResourcePool.CurrentValue, Is.EqualTo(58));
            Assert.That(ScoutCombatSystem.RecoverKnife(session, owner, hook, position), Is.False);
            Assert.That(owner.Character.ResourcePool.CurrentValue, Is.EqualTo(58));
        }

        [Test]
        public void ExtractionRewardsOnlyOneActuallyRemovedKnife()
        {
            var owner = RecoveryUnit(); var enemy = Unit(false, 2);
            var session = new CombatSession { Grid = new BattleGrid(6, 6), Units = new() { owner, enemy }, ActiveUnit = owner };
            enemy.ScoutState.Knives.Add(new() { Owner = owner, BleedMultiplier = .6f });
            Assert.That(ScoutCombatSystem.TryExtractKnife(session, owner, enemy, new StatusEffectSystem()), Is.True);
            Assert.That(owner.Character.ResourcePool.CurrentValue, Is.EqualTo(58));
            Assert.That(ScoutCombatSystem.TryExtractKnife(session, owner, enemy, new StatusEffectSystem()), Is.False);
            Assert.That(owner.Character.ResourcePool.CurrentValue, Is.EqualTo(58));
        }

        [Test]
        public void RealMovementPickupPublishesRewardWithoutChangingActionPointCost()
        {
            var owner = RecoveryUnit(); var session = new CombatSession { Grid = new BattleGrid(6, 6), Units = new() { owner }, ActiveUnit = owner };
            session.MapObjects.Add(new KnifeCombatMapObject("knife", new GridPosition(2, 1), owner.Character.CharacterId, 1));
            var events = new List<CombatFeedbackEvent>(); var bus = new CombatFeedbackBus(); bus.Published += events.Add;
            var expectedAp = owner.CurrentActionPoints - CombatActionCostCalculator.CalculateMovementCost(owner);
            Assert.That(new CombatMovementSystem(feedbackBus: bus).TryMoveOneStep(session, owner, 1, 0), Is.True);
            Assert.That(owner.CurrentActionPoints, Is.EqualTo(expectedAp)); Assert.That(owner.Character.ResourcePool.CurrentValue, Is.EqualTo(58));
            Assert.That(events.Count(e => e.Type == CombatFeedbackType.ResourceGain && e.Amount == 8), Is.EqualTo(1));
        }
        [TestCase(.9f,.9f,3)] [TestCase(.1f,.9f,4)] [TestCase(.9f,.1f,4)] [TestCase(.1f,.1f,5)]
        public void AdditionalHitsAreIndependent(float first,float second,int expected)
        {
            var rolls=new Queue<float>(new[]{first,second});
            Assert.That(CombatHitSequence.RollHitCount(3,2,.5f,()=>rolls.Dequeue()),Is.EqualTo(expected));
            Assert.That(rolls,Is.Empty);
        }
        [Test] public void AdditionalHitLimitCanGrowToFourIndependentTrials()
        {var rolls=new Queue<float>(new[]{.9f,.1f,.9f,.1f});Assert.That(CombatHitSequence.RollHitCount(3,4,.5f,()=>rolls.Dequeue()),Is.EqualTo(5));}
        [Test] public void AngerBuildsPerHitAndBuffsAlliesWithoutCrossTargetLeakage()
        {
            var scout=Unit(true,1);var ally=Unit(true,2);var enemy=Unit(false,3);var other=Unit(false,4);
            Rank(scout,ScoutCombatSystem.BladeTree,"anger",1);
            for(var i=0;i<3;i++)ScoutCombatSystem.RecordHit(scout,enemy,DamageType.Physical);
            Assert.That(enemy.ScoutState.AngerBySource[scout],Is.EqualTo(30));
            Assert.That(ScoutCombatSystem.ReceivedDamageMultiplier(ally,enemy),Is.EqualTo(1.045f).Within(.00001));
            Assert.That(ScoutCombatSystem.ReceivedDamageMultiplier(ally,other),Is.EqualTo(1));
            ScoutCombatSystem.RecordHit(scout,enemy,DamageType.Poison);ScoutCombatSystem.RecordHit(scout,enemy,DamageType.Bleed);ScoutCombatSystem.RecordHit(scout,enemy,DamageType.Burn);
            Assert.That(enemy.ScoutState.AngerBySource[scout],Is.EqualTo(30));
        }
        [TestCase(1,11)] [TestCase(2,13)] [TestCase(3,15)]
        public void MentalControlAppliesToHitsAndRevengeAndCapsAt100(int rank,int gain)
        {
            var scout=Unit(true,1);var enemy=Unit(false,2);
            Rank(scout,ScoutCombatSystem.BladeTree,"anger",1);Rank(scout,ScoutCombatSystem.BladeTree,"mental_control",rank);Rank(scout,ScoutCombatSystem.BladeTree,"revenge",1);
            ScoutCombatSystem.RecordHit(scout,enemy,DamageType.Physical);Assert.That(enemy.ScoutState.AngerBySource[scout],Is.EqualTo(gain));
            ScoutCombatSystem.RecordDamageTaken(enemy,scout,DamageType.Physical);Assert.That(enemy.ScoutState.AngerBySource[scout],Is.EqualTo(gain*2));
            for(var i=0;i<20;i++)ScoutCombatSystem.RecordHit(scout,enemy,DamageType.Physical);
            Assert.That(enemy.ScoutState.AngerBySource[scout],Is.EqualTo(100));
        }
        [Test] public void SpentKnifeCannotBeBothEmbeddedAndDroppedAndExtractionIsFree()
        {
            var scout=Unit(true,1);var enemy=Unit(false,2);Rank(scout,ScoutCombatSystem.AssassinTree,"sharp_knife",2);
            var s=new CombatSession{Grid=new BattleGrid(6,6),Units=new(){scout,enemy},ActiveUnit=scout};
            s.PendingScoutKnifeActions["action"]=new(){Owner=scout,Count=1,DropPosition=enemy.Position};
            Assert.That(ScoutCombatSystem.TryEmbedKnife(s,"action",scout,enemy,AttackType.Ranged,()=>0),Is.True);
            Assert.That(ScoutCombatSystem.TryEmbedKnife(s,"action",scout,enemy,AttackType.Ranged,()=>0),Is.False);
            ScoutCombatSystem.FinishKnifeAction(s,"action");Assert.That(s.MapObjects,Is.Empty);
            scout.CurrentActionPoints=0;
            Assert.That(ScoutCombatSystem.TryExtractKnife(s,scout,enemy,new StatusEffectSystem()),Is.True);
            Assert.That(scout.AvailableKnives,Is.EqualTo(5));Assert.That(scout.CurrentActionPoints,Is.Zero);Assert.That(enemy.ScoutState.Knives,Is.Empty);
            Assert.That(ScoutCombatSystem.TryExtractKnife(s,scout,enemy,new StatusEffectSystem()),Is.False);
        }
        [Test] public void DirectRecoveryUsesRangeAndOwnershipWithoutChanceRoll()
        {
            var scout=Unit(true,1);var s=new CombatSession{Grid=new BattleGrid(10,6),Units=new(){scout},ActiveUnit=scout};
            var skill=ScriptableObject.CreateInstance<SkillData>();assets.Add(skill);
            var serialized = new SerializedObject(skill);
            serialized.FindProperty("<CanRecoverKnives>k__BackingField").boolValue = true;
            var modes = serialized.FindProperty("targetModes");
            modes.arraySize = 1;
            var range = modes.GetArrayElementAtIndex(0).FindPropertyRelative("absoluteRange");
            range.FindPropertyRelative("shape").enumValueIndex = (int)RangeShape.Square;
            range.FindPropertyRelative("minRange").intValue = 1;
            range.FindPropertyRelative("maxRange").intValue = 6;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(skill.CanRecoverKnives,Is.True,"recovery flag");
            Assert.That(skill.TargetModes.Count,Is.EqualTo(1),"target mode");
            Assert.That(skill.TargetModes[0].AbsoluteRange.MaxRange,Is.EqualTo(6),"range");
            var position=new GridPosition(6,1);
            var knife=new KnifeCombatMapObject("knife.test",position,scout.Character.CharacterId,1);s.MapObjects.Add(knife);
            Assert.That(ScoutCombatSystem.RecoverKnife(s,scout,skill,position),Is.True);
            Assert.That(scout.AvailableKnives,Is.EqualTo(5));Assert.That(s.MapObjects,Is.Empty);
            Assert.That(ScoutCombatSystem.RecoverKnife(s,scout,skill,position),Is.False);
            scout.AvailableKnives=4;s.MapObjects.Add(new KnifeCombatMapObject("knife.foreign",position,"someone.else",1));
            Assert.That(ScoutCombatSystem.RecoverKnife(s,scout,skill,position),Is.False);
            s.MapObjects.Add(new KnifeCombatMapObject("knife.far",new GridPosition(9,1),scout.Character.CharacterId,1));
            Assert.That(ScoutCombatSystem.RecoverKnife(s,scout,skill,new GridPosition(9,1)),Is.False);
        }
        [Test] public void ExtractionRequiresOwnersTurnAdjacencyAndCapacity()
        {
            var scout=Unit(true,1);var enemy=Unit(false,2);var s=new CombatSession{Grid=new BattleGrid(6,6),Units=new(){scout,enemy},ActiveUnit=enemy};
            enemy.ScoutState.Knives.Add(new(){Owner=scout,BleedMultiplier=.6f});
            Assert.That(ScoutCombatSystem.CanExtractKnife(s,scout,enemy),Is.False);
            s.ActiveUnit=scout;Assert.That(ScoutCombatSystem.CanExtractKnife(s,scout,enemy),Is.True);
            scout.Position=new GridPosition(0,1);Assert.That(ScoutCombatSystem.CanExtractKnife(s,scout,enemy),Is.False);
            scout.Position=new GridPosition(1,1);scout.AvailableKnives=5;Assert.That(ScoutCombatSystem.CanExtractKnife(s,scout,enemy),Is.False);
        }
        [Test] public void AngerDoesNotCountMissAndActualDamageUsesPartyDebuff()
        {
            var scout=Unit(true,1);var ally=Unit(true,2);var enemy=Unit(false,3);Rank(scout,ScoutCombatSystem.BladeTree,"anger",1);
            var s=new CombatSession{Grid=new BattleGrid(6,6),Units=new(){scout,ally,enemy}};var damage=new CombatDamageSystem();
            damage.ResolveAndApplyDamage(s,scout,enemy,100,DamageType.Physical,wasHit:false);
            Assert.That(enemy.ScoutState.AngerBySource,Is.Empty);
            damage.ResolveAndApplyDamage(s,scout,enemy,100,DamageType.Physical,wasHit:true);
            Assert.That(enemy.ScoutState.AngerBySource[scout],Is.EqualTo(10));
            Assert.That(new CombatVariableSystem().ApplyDamageModifiers(ally,enemy,200),Is.EqualTo(203));
        }
        [Test] public void DeadTargetReleasesEmbeddedKnives()
        {
            var scout=Unit(true,1);var enemy=Unit(false,2);var s=new CombatSession{Grid=new BattleGrid(6,6),Units=new(){scout,enemy}};
            enemy.ScoutState.Knives.Add(new(){Owner=scout,BleedMultiplier=.6f});ScoutCombatSystem.ReleaseKnivesOnDeath(s,enemy);
            Assert.That(s.MapObjects.OfType<KnifeCombatMapObject>().Count(),Is.EqualTo(1));Assert.That(enemy.ScoutState.Knives,Is.Empty);
        }
    }
}

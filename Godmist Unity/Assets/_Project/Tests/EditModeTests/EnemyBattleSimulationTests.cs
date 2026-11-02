using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Combat;
using Godmist.Data.Dungeons;
using Godmist.Data.World;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Game;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Systems.Dungeons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    // A bounded, headless battle, not a second implementation of damage formulas.
    // All actions, costs, AV, resource rules, status ticks, reactions and boss phases
    // go through the production systems. Only player decisions are a test policy.
    public partial class EnemyBattleSimulationTests
    {
        const string Root = "Assets/_Project/ScriptableObjects/";
        static readonly string Folder = Path.GetFullPath("../LocalHandoffs/enemy-scaling-pass-20261016");
        readonly List<UnityEngine.Object> temporary = new();
        readonly Dictionary<CharacterDefinition, CharacterDefinition> clones = new();
        readonly Dictionary<CharacterDefinition,string> originals = new();
        Settings activeSettings;
        // Immutable pre-pass growth: candidates never accumulate changes or touch live assets.
        static readonly Dictionary<string, Vector3> Original = new()
        {
            ["enemy.catacombs.swordsman"] = new(23,2.73f,4.16f), ["enemy.catacombs.undead"] = new(33,1.95f,3.12f),
            ["enemy.catacombs.wraith"] = new(18,1.63f,4.88f), ["enemy.catacombs.occultist"] = new(17,3.58f,5.2f),
            ["enemy.catacombs.archer"] = new(20,2.6f,3.9f), ["enemy.catacombs.ghoul"] = new(26,2.34f,4.29f),
            ["enemy.catacombs.defender"] = new(36,1.63f,2.93f), ["enemy.catacombs.spectral_knight"] = new(0,2.73f,4.16f),
            ["enemy.catacombs.dead_king"] = new(59,3.12f,6.24f), ["enemy.catacombs.skeleton_slayer"] = new(82,3.9f,5.53f),
            ["enemy.forest.bandit_thug"] = new(23,2.28f,3.58f), ["enemy.forest.bandit_shooter"] = new(18,2.93f,4.55f),
            ["enemy.forest.elf_archer"] = new(16,3.25f,4.55f), ["enemy.forest.elf_poacher"] = new(21,2.6f,3.9f),
            ["enemy.forest.rabid_wolf"] = new(13,2.34f,4.68f), ["enemy.forest.elf_druid"] = new(13,1.3f,1.95f),
            ["enemy.forest.infected_tree"] = new(30,1.95f,2.28f), ["enemy.forest.infected_ent"] = new(91,3.58f,4.88f),
            ["enemy.forest.bandit_leader"] = new(33,4.23f,5.85f)
        };
        static string BaseId(string id) => id.Replace(".enraged", "").Replace(".burning", "").Replace(".revealed", "");
        static readonly Dictionary<string,Vector2> PreviousRoleFactors=new()
        {
            ["enemy.catacombs.swordsman"]=new(1.2f,2.25f),["enemy.catacombs.undead"]=new(1.25f,2.1f),
            ["enemy.catacombs.wraith"]=new(1,1.7f),["enemy.catacombs.occultist"]=new(1,1.55f),
            ["enemy.catacombs.archer"]=new(1.1f,2),["enemy.catacombs.ghoul"]=new(1.2f,2.25f),
            ["enemy.catacombs.defender"]=new(1.25f,2),["enemy.catacombs.spectral_knight"]=new(1,2.25f),
            ["enemy.catacombs.dead_king"]=new(1.2f,1.5f),["enemy.catacombs.skeleton_slayer"]=new(1.2f,1.9f),
            ["enemy.forest.bandit_thug"]=new(1.2f,2.25f),["enemy.forest.bandit_shooter"]=new(1.1f,2.15f),
            ["enemy.forest.elf_archer"]=new(1.1f,2),["enemy.forest.elf_poacher"]=new(1.1f,2.1f),
            ["enemy.forest.rabid_wolf"]=new(1.1f,2),["enemy.forest.elf_druid"]=new(1,1.55f),
            ["enemy.forest.infected_tree"]=new(1.15f,2.1f),["enemy.forest.infected_ent"]=new(1.2f,1.8f),
            ["enemy.forest.bandit_leader"]=new(1.5f,1)
        };

        CharacterDefinition Clone(CharacterDefinition source, string candidate, float hp, float attack)
        {
            if (source == null) return null;
            if (clones.TryGetValue(source, out var cached)) return cached;
            originals[source]=EditorJsonUtility.ToJson(source);
            var clone = UnityEngine.Object.Instantiate(source); temporary.Add(clone); clones.Add(source, clone);
            var data = new SerializedObject(clone);
            var lateOffset=Vector3.zero;
            if (Godmist.Editor.EnemyScalingBalanceSetup.TryGetOriginalBase(source.Id,out var historicalBase))
            {
                var basis=data.FindProperty("<BaseStats>k__BackingField");
                basis.FindPropertyRelative("<MaxHealth>k__BackingField").intValue=(int)historicalBase.x;
                basis.FindPropertyRelative("<MinAttack>k__BackingField").floatValue=(int)historicalBase.y;
                basis.FindPropertyRelative("<MaxAttack>k__BackingField").floatValue=(int)historicalBase.z;
            }
            if (Original.TryGetValue(BaseId(source.Id), out var growth))
            {
                var special=candidate=="uniform" ? activeSettings.bosses.FirstOrDefault(b=>b.id==BaseId(source.Id)) : null;
                var healthFactor=special?.health ?? hp; var attackFactor=special?.attack ?? attack;
                if(candidate=="roles")
                {
                    var role=PreviousRoleFactors[BaseId(source.Id)];
                    float Anchored(float start,float increment,float factor)=>Mathf.Round((((start+increment*181)*factor-start)/181)*1000)/1000;
                    growth=new Vector3(Anchored(historicalBase.x,growth.x,role.x),
                        Anchored(historicalBase.y,growth.y,role.y),Anchored(historicalBase.z,growth.z,role.y));
                }
                var value = data.FindProperty("<GrowthData>k__BackingField");
                var ramp=candidate=="uniform" && activeSettings.ramp;
                var full=new Vector3(Mathf.Round(growth.x*healthFactor*1000)/1000,Mathf.Round(growth.y*attackFactor*1000)/1000,Mathf.Round(growth.z*attackFactor*1000)/1000);
                if(candidate=="uniform" && Godmist.Editor.EnemyScalingBalanceSetup.TryGetOriginalBase(source.Id,out var originalBase))
                {
                    var ordinary=Godmist.Editor.EnemyScalingBalanceSetup.UsesEarlyThreatCalibration(source.Id);
                    var anchorBase=new Vector3(BaseId(source.Id)=="enemy.catacombs.spectral_knight"?originalBase.x:Mathf.Max(1,Mathf.RoundToInt(originalBase.x*Godmist.Editor.EnemyScalingBalanceSetup.AnchorBaseHealthFactor)),
                        Mathf.Max(1,Mathf.RoundToInt(originalBase.y*Godmist.Editor.EnemyScalingBalanceSetup.AnchorBaseAttackFactor)),Mathf.Max(1,Mathf.RoundToInt(originalBase.z*Godmist.Editor.EnemyScalingBalanceSetup.AnchorBaseAttackFactor)));
                    var reduced=ordinary?new Vector3(Mathf.Max(1,Mathf.RoundToInt(originalBase.x*activeSettings.baseHealth)),
                        Mathf.Max(1,Mathf.RoundToInt(originalBase.y*activeSettings.baseAttack)),Mathf.Max(1,Mathf.RoundToInt(originalBase.z*activeSettings.baseAttack))):anchorBase;
                    var basis=data.FindProperty("<BaseStats>k__BackingField");
                    var multiplier=ordinary?activeSettings.baseStatMultiplier
                        :Godmist.Editor.EnemyScalingBalanceSetup.UsesBossBaseBoost(source.Id)?activeSettings.bossBaseStatMultiplier:1f;
                    var authoredBase=new Vector3(Mathf.RoundToInt(reduced.x*multiplier),
                        Mathf.RoundToInt(reduced.y*multiplier),Mathf.RoundToInt(reduced.z*multiplier));
                    basis.FindPropertyRelative("<MaxHealth>k__BackingField").intValue=(int)authoredBase.x;
                    basis.FindPropertyRelative("<MinAttack>k__BackingField").floatValue=(int)authoredBase.y;
                    basis.FindPropertyRelative("<MaxAttack>k__BackingField").floatValue=(int)authoredBase.z;
                    full+=(originalBase-(ramp?anchorBase:reduced))/199f;
                    if(ramp)lateOffset=anchorBase-reduced;
                }
                value.FindPropertyRelative("<MaxHealth>k__BackingField").floatValue = ramp?growth.x:full.x;
                value.FindPropertyRelative("<MinAttack>k__BackingField").floatValue = ramp?growth.y:full.y;
                value.FindPropertyRelative("<MaxAttack>k__BackingField").floatValue = ramp?growth.z:full.z;
                value.FindPropertyRelative("<LateMaxHealth>k__BackingField").floatValue = ramp?full.x-growth.x:0;
                value.FindPropertyRelative("<LateMinAttack>k__BackingField").floatValue = ramp?full.y-growth.y:0;
                value.FindPropertyRelative("<LateMaxAttack>k__BackingField").floatValue = ramp?full.z-growth.z:0;
                var early=ramp && special==null;
                var knight=BaseId(source.Id)=="enemy.catacombs.spectral_knight";
                var earlyHealth=knight ? .6f : activeSettings.earlyHealth;
                var earlyAttack=knight ? .6f : activeSettings.earlyAttack;
                value.FindPropertyRelative("<EarlyMaxHealth>k__BackingField").floatValue = early?Mathf.Round(growth.x*earlyHealth*1000)/1000-growth.x:0;
                value.FindPropertyRelative("<EarlyMinAttack>k__BackingField").floatValue = early?Mathf.Round(growth.y*earlyAttack*1000)/1000-growth.y:0;
                value.FindPropertyRelative("<EarlyMaxAttack>k__BackingField").floatValue = early?Mathf.Round(growth.z*earlyAttack*1000)/1000-growth.z:0;
            }
            var growthData=data.FindProperty("<GrowthData>k__BackingField");
            growthData.FindPropertyRelative("<LateMaxHealthOffset>k__BackingField").floatValue=lateOffset.x;
            growthData.FindPropertyRelative("<LateMinAttackOffset>k__BackingField").floatValue=lateOffset.y;
            growthData.FindPropertyRelative("<LateMaxAttackOffset>k__BackingField").floatValue=lateOffset.z;
            data.FindProperty("<SecondPhaseDefinition>k__BackingField").objectReferenceValue = Clone(source.SecondPhaseDefinition, candidate, hp, attack);
            data.ApplyModifiedPropertiesWithoutUndo();
            // Summoned knights/trees must use the same candidate as their summoner.
            SkillData CopySkill(SkillData skill)
            {
                if (skill == null || !skill.Effects.Any(e => e?.SummonedCharacterDefinition != null)) return skill;
                var copy = UnityEngine.Object.Instantiate(skill); temporary.Add(copy);
                var serialized = new SerializedObject(copy);
                var effects = serialized.FindProperty("effects");
                for (var i = 0; i < skill.Effects.Count; i++)
                    if (skill.Effects[i]?.SummonedCharacterDefinition != null)
                        effects.GetArrayElementAtIndex(i).FindPropertyRelative("summonedCharacterDefinition")
                            .objectReferenceValue = Clone(skill.Effects[i].SummonedCharacterDefinition, candidate, hp, attack);
                serialized.ApplyModifiedPropertiesWithoutUndo(); return copy;
            }
            data.Update();
            var skills = data.FindProperty("<StartingSkills>k__BackingField");
            for (var i = 0; i < source.StartingSkills.Count; i++) skills.GetArrayElementAtIndex(i).objectReferenceValue = CopySkill(source.StartingSkills[i]);
            data.FindProperty("<BasicAttackSkill>k__BackingField").objectReferenceValue = CopySkill(source.BasicAttackSkill);
            data.ApplyModifiedPropertiesWithoutUndo();
            return clone;
        }

        [TearDown] public void Cleanup()
        {
            var changed=originals.Where(p=>p.Value!=EditorJsonUtility.ToJson(p.Key)).Select(p=>p.Key.Id).ToArray();
            foreach (var item in temporary) if (item != null) UnityEngine.Object.DestroyImmediate(item);
            temporary.Clear(); clones.Clear(); originals.Clear(); new EnemyScalingBalanceTests().ReleaseReferenceObjects();
            Assert.That(changed,Is.Empty,"Simulation must never modify authored definitions.");
        }

        static readonly string[][] Loadouts =
        {
            new[] {"skill.warrior.cut", "skill.warrior.kick", "skill.warrior.right_hook", "skill.warrior.bandage", "skill.warrior.bleeding_destruction"},
            new[] {"skill.knife_toss", "skill.scout.aimed_throw", "skill.scout.ricochet", "skill.scout.weak_point", "skill.scout.poisoned_dagger"},
            new[] {"skill.mage.energy_orb", "skill.mage.fireball", "skill.mage.shield", "skill.mage.focus", "skill.mage.fire_arrow"},
            new[] {"skill.paladin.verdict", "skill.paladin.heal", "skill.paladin.group_heal", "skill.paladin.holy_regeneration", "skill.paladin.blinding_light"}
        };
        [Serializable] public class SkillCount { public string id; public int count; }
        [Serializable] public class Result
        {
            public string candidate, encounter, outcome, build, stop;
            public int level, seed, turns, actions, downings, deaths, enemyDownings, phaseChanges, hardControlTurns, damage, healing, shields;
            public int enemyCount;
            public int heroDamageTaken,heroHealing,heroShields,heroManaSpent,heroActions,enemyActions;
            public float heroHealthBudget;
            public int heroMoves, enemyMoves;
            public int heroRevives;
            public int heroHealingPreviouslyUnreported,heroFireFeedingHealing,rescuesDeferred,riskRescues,mageRetreats;
            public int runesDisarmed;
            public string[] enemyIds, heroSkills;
            public float remainingHealth, enemyRemainingHealth, seconds;
            public List<SkillCount> skills = new();
        }
        [Serializable] class Report { public string policy, policyVersion="5-healing", configuration; public List<Result> battles = new(); }

        sealed partial class Engine : IDisposable
        {
            public readonly CombatSession Session;
            public readonly Result Result;
            readonly List<BattleUnit> heroes;
            readonly CombatTurnSystem turns;
            readonly ResourceRuleSystem resources;
            readonly CombatOutcomeSystem outcome = new();
            readonly CombatDamageSystem damage;
            readonly StatusEffectSystem status;
            readonly CombatRecoverySystem recovery;
            readonly PassiveEffectLifecycleSystem lifecycle;
            readonly SkillExecutionSystem skills;
            readonly CombatMovementSystem movement;
            readonly PassiveEffectCombatExecutionSystem passive;
            readonly SummonedObjectSystem objects;
            readonly SummonSystem summons = new();
            readonly CatacombsBossSystem catacombs;
            readonly ForestEntBossSystem ent;
            readonly DeadKingBossSystem king;
            readonly SkeletonSlayerBossSystem slayer;
            readonly BanditLeaderBossSystem leader;
            readonly SimpleCombatAiSystem ai;
            readonly Dictionary<BattleUnit, CharacterLifeState> life = new();
            readonly Dictionary<BattleUnit, CharacterDefinition> definitions = new();
            readonly Queue<string> trace = new();
            readonly CombatTargetingService targeting = new();
            readonly CombatLineOfSightService sight = new();
            readonly HashSet<SkillData> zeroCostUsed = new();
            readonly HashSet<string> helpedUp = new();
            // Optional audit policy only; ordinary calibration keeps its policy.
            internal Func<BattleUnit,SkillData,int,IReadOnlyList<BattleUnit>,float> AdditionalAuditScore;
            internal Action<BattleUnit> AuditUnitTurn;
            internal Action<CombatFeedbackEvent> AuditFeedback;
            internal int AuditTurnLimit=240;
            internal double AuditWallClockLimitSeconds=12;
            internal double AuditAiTimeoutSeconds=1;
            internal int AuditAiSliceLimit=2000;
            internal bool AuditLateKnifeExtraction;
            internal bool UseLegacyPlayerPolicy;
            internal bool UsePreviousRescuePolicy;
            bool SmartRescue=>!UseLegacyPlayerPolicy&&!UsePreviousRescuePolicy;
            readonly Dictionary<CharacterInstance,Action<int,string,bool>> healingObservers=new();
            internal bool AuditUseEffectiveActionCost;
            internal bool AuditHoldRangedPosition;
            internal bool AuditRepeatRescues;
            internal Action<BattleUnit,SkillData,IReadOnlyList<BattleUnit>,float> AuditChosenPlan;
            internal Action<BattleUnit,bool,bool> AuditPlayerDecision;
            bool endPlayerTurn;

            readonly KnockdownTrace knockdownTrace;
            public Engine(CombatSession session, Result result, KnockdownTrace diagnostic=null)
            {
                knockdownTrace=diagnostic;diagnostic?.Attach(session,result);
                Session=session; Result=result; heroes=session.Units.Where(u=>u.IsPlayerSide).ToList();
                Result.heroHealthBudget=heroes.Sum(u=>u.Character.Stats.MaxHealth.GetValue());
                foreach(var u in session.Units) { life[u]=u.Character.LifeState; definitions[u]=u.Character.Definition;TrackHealing(u); }
                void Log(string message) { if(trace.Count==30)trace.Dequeue(); trace.Enqueue(message); }
                session.PassiveEffectEventDispatcher=new PassiveEffectCombatEventDispatcher();
                var bus=new CombatFeedbackBus();
                bus.Published += feedback =>
                {
                    if(feedback.Type!=CombatFeedbackType.Heal)knockdownTrace?.Feedback(feedback);
                    AuditFeedback?.Invoke(feedback);
                    // Healing is observed once from CharacterInstance, including bus-less passives.
                    if(feedback.Type==CombatFeedbackType.ShieldGain) Result.shields+=Math.Max(0,feedback.Amount);
                    if(feedback.Target?.IsPlayerSide==true)
                    {
                        
                        if(feedback.Type==CombatFeedbackType.ShieldGain)Result.heroShields+=Math.Max(0,feedback.Amount);
                        if(feedback.Type==CombatFeedbackType.ResourceSpend && feedback.ResourceType==ResourceType.Mana)
                            Result.heroManaSpent+=Math.Max(0,feedback.Amount);
                    }
                };
                resources=new ResourceRuleSystem(Log,bus);
                turns=new CombatTurnSystem(Log, Begin, End, null, ObjectEnd, Died, session.PassiveEffectEventDispatcher,
                    onUnitDownedTurn:u=>status.ProcessDownedTurn(u),resourceRuleSystem:resources);
                damage=new CombatDamageSystem(Log,Evaluate,Remove,new CombatVariableSystem(),bus,session.PassiveEffectEventDispatcher);
                recovery=new CombatRecoverySystem(Log,bus);
                var shield=new CombatShieldSystem(Log,bus);
                lifecycle=new PassiveEffectLifecycleSystem(Log,new IPassiveEffectTurnProcessor[]
                {diagnostic==null ? new DamageOverTimePassiveEffectProcessor(damage) : new TracedDotProcessor(damage,diagnostic),new SleepPassiveEffectProcessor(recovery),new PeriodicRecoveryPassiveEffectProcessor(recovery)},new CharacterStatPipelineSystem());
                status=new StatusEffectSystem(Log,Remove,Evaluate,damage,lifecycle,bus,session.PassiveEffectEventDispatcher);
                var stat=new ModifyStatSystem(Log,lifecycle); var variable=new ModifyVarSystem(Log,lifecycle);
                objects=new SummonedObjectSystem(Log,null,new SummonedObjectEffectExecutor(Log,damage,recovery,shield,status,stat,variable,bus));
                catacombs=new CatacombsBossSystem(objects,damage,Log,bus);
                session.EnemyHealthChanged=(u,h)=>catacombs.OnEnemyHealthChanged(session,u,h);
                session.BossRuneEnteredCell=u=>
                {
                    var before=session.SummonedObjects.Count(CatacombsBossSystem.IsRune);
                    catacombs.OnUnitEnteredCell(session,u);
                    if(u.IsPlayerSide)Result.runesDisarmed+=Math.Max(0,before-session.SummonedObjects.Count(CatacombsBossSystem.IsRune));
                };
                ent=new ForestEntBossSystem(session,damage,status,recovery,null,Log,bus);
                king=new DeadKingBossSystem(session,damage,status,null,Log,bus);
                slayer=new SkeletonSlayerBossSystem(session,damage,status,null,Log,bus);
                leader=new BanditLeaderBossSystem(session,damage,status,recovery,objects,Log,bus);
                skills=new SkillExecutionSystem(Log,Remove,Evaluate,status,damage,recovery,shield,
                    lineOfSightService:sight,modifyStatSystem:stat,modifyVarSystem:variable,summonedObjectSystem:objects,
                    summonSystem:summons,feedbackBus:bus,modifyResistanceSystem:new ModifyResistanceSystem(Log,lifecycle),
                    passiveEffectEventDispatcher:session.PassiveEffectEventDispatcher,resourceRuleSystem:resources);
                passive=new PassiveEffectCombatExecutionSystem(session,status,lifecycle,Log)
                    {ReactionDamageSystem=damage,DuplicateActionExecutor=skills.TryExecutePassiveDuplicate};
                skills.PassiveActionResolved=passive.NotifyActionResolved;
                movement=new CombatMovementSystem(Log,resources,status,bus,session.PassiveEffectEventDispatcher);
                ai=new SimpleCombatAiSystem(Log,null,Evaluate,skills,targeting,movement,sight,new CombatInteractionSystem(Log,null,bus),catacombs);
                session.SkillResolved+=(u,s,id)=>
                { knockdownTrace?.Skill(u,s,id);if(u.IsPlayerSide)result.heroActions++;else result.enemyActions++;
                    var key=(u.IsPlayerSide?"hero:":"enemy:")+s.SkillId; var count=result.skills.Find(x=>x.id==key);
                    if(count==null) {count=new SkillCount {id=key};result.skills.Add(count);} count.count++; Observe(); };
                session.DamageApplied+=(a,t,n,d,id)=>{knockdownTrace?.Applied(a,t,n,d,id);result.damage+=n;if(t.IsPlayerSide)result.heroDamageTaken+=n;Observe();};
                ScoutKnifeSystem.ResetForCombat(session);
                turns.InitializeTurnOrder(session);
            }
            void TrackHealing(BattleUnit unit)
            {
                if(healingObservers.ContainsKey(unit.Character))return;
                Action<int,string,bool> observer=(amount,origin,withFeedback)=>
                {
                    Result.healing+=amount;
                    if(unit.IsPlayerSide)
                    {
                        Result.heroHealing+=amount;
                        if(!withFeedback)Result.heroHealingPreviouslyUnreported+=amount;
                        if(origin=="mage.fire_feeding")Result.heroFireFeedingHealing+=amount;
                        knockdownTrace?.Feedback(new CombatFeedbackEvent(unit,CombatFeedbackType.Heal,amount,effectName:origin));
                    }
                };
                healingObservers.Add(unit.Character,observer);unit.Character.HealingReceived+=observer;
            }
            void Observe()
            {
                knockdownTrace?.Observe();
                foreach(var unit in life.Keys.Concat(Session.Units).Distinct().ToArray())
                {
                    TrackHealing(unit);
                    Assert.That(unit.CurrentActionPoints,Is.GreaterThanOrEqualTo(0),"negative AP");
                    Assert.That(unit.Character.ResourcePool.CurrentValue,Is.InRange(0,unit.Character.ResourcePool.RecoveryLimit),"resource bound including authored mana overflow");
                    Assert.That(unit.Character.CurrentHealth,Is.InRange(0,Mathf.CeilToInt(unit.Character.Stats.MaxHealth.GetValue())),"health bound");
                    var next=unit.Character.LifeState;
                    if(life.TryGetValue(unit,out var before) && before==CharacterLifeState.Alive && next!=before)
                    {if(unit.IsPlayerSide) Result.downings++;else Result.enemyDownings++;}
                    life[unit]=next;
                    if(definitions.TryGetValue(unit,out var definition) && definition!=unit.Character.Definition) Result.phaseChanges++;
                    definitions[unit]=unit.Character.Definition;
                }
            }
            void Evaluate(CombatSession session) {Observe();if(AuditNpcDuelOutcome!=null)AuditNpcDuelOutcome(session);else outcome.EvaluateCombatOutcome(session);}
            void Begin(BattleUnit unit)
            {
                knockdownTrace?.TurnStart(unit);
                resources.ProcessTurnStart(unit); skills.ProcessToggleUpkeep(Session,unit);
                king.BeginTurn(unit);slayer.BeginTurn(unit);leader.BeginTurn(unit);ent.BeginTurn(unit);
                status.ProcessTurnStart(Session,unit);ent.CheckPhases();
                CombatTurnSystem.RefreshMaxActionPoints(Session,unit,true);Observe();
            }
            void End(BattleUnit unit)
            {
                ent.EndTurn(unit);status.ProcessTurnEnd(unit);skills.ProcessScheduledEffects(Session,unit);
                summons.AdvanceDurationAtTurnEnd(Session,unit);CombatTurnSystem.RefreshMaxActionPoints(Session,unit);Observe();
            }
            void ObjectEnd(SummonedCombatMapObject item) { if(item.AdvanceDuration())objects.Remove(Session,item); }
            void Died(BattleUnit unit)
            {
                catacombs.OnBossDied(Session,unit);king.OnBossDied(unit);slayer.OnBossDied(unit);leader.OnBossDied(unit);
                summons.RemoveSummonsCreatedBy(Session,unit.Character.CharacterId);
                skills.CancelScheduledEffectsByCaster(Session,unit.Character.CharacterId);skills.DeactivateAllToggles(Session,unit);
            }
            void Remove(BattleUnit unit)
            {
                Observe(); Died(unit); Session.Units.Remove(unit);
                Session.TurnParticipants.RemoveAll(p=>p.Unit==unit);
                Session.TurnOrder=Session.TurnParticipants.Where(p=>p.Unit!=null).Select(p=>p.Unit).ToList();
                if(Session.ActiveUnit==unit) { Session.ActiveParticipant=null;Session.ActiveUnit=null;Session.ActiveSummonedObject=null; }
            }
            bool Support(BattleUnit actor,bool downedOnly=false)
            {
                if(BudgetSupport)return BudgetMaintenance(actor,downedOnly);
                // Prioritize an endangered ally with a real, affordable, in-range action.
                foreach(var skill in actor.Character.GetActiveSkills().Where(IsSupport)
                    .OrderByDescending(s=>s.Effects.Any(e=>e.EffectType==EffectType.PaladinRegeneration)))
                {
                    var shield=skill.Effects.Any(e=>e.EffectType==EffectType.Shield);
                    var regeneration=skill.Effects.Any(e=>e.EffectType==EffectType.PaladinRegeneration);
                    foreach(var ally in Session.Units.Where(u=>u.IsPlayerSide==actor.IsPlayerSide && (!downedOnly||u.Character.LifeState==CharacterLifeState.Downed))
                        .OrderBy(u=>u.Character.LifeState==CharacterLifeState.Downed?-1f:(float)u.Character.CurrentHealth/u.Character.Stats.MaxHealth.GetValue()))
                    {
                        var fraction=ally.Character.CurrentHealth/ally.Character.Stats.MaxHealth.GetValue();
                        if(ally.Character.LifeState==CharacterLifeState.Dead || fraction>(shield?.6f:.75f)
                            || shield && ally.Character.ShieldPool.TotalShield>0
                            || regeneration && (ally.Character.LifeState!=CharacterLifeState.Alive
                                || ally.Character.PassiveEffects.Any(e=>e.EffectId=="paladin.holy_regeneration" && PassiveEffectLifecycleSystem.IsActive(e))))continue;
                        var targets=new Dictionary<int,IReadOnlyList<BattleUnit>>();
                        for(var m=0;m<skill.TargetModes.Count;m++)
                        {
                            var mode=PassiveEffectCombatExecutionSystem.ResolveMode(actor,skill,skill.TargetModes[m]);
                            targets[m]=targeting.GetTargets(Session,actor,mode,ally.Position,true)
                                .Where(u=>!skill.RequiresLineOfSight||sight.HasLineOfSight(Session,actor.Position,u.Position)).ToArray();
                        }
                        if(targets.Values.All(t=>t.Count>0) && targets.Values.Any(t=>t.Contains(ally)) && skills.TryExecute(Session,actor,skill,targets))return true;
                    }
                }
                return false;
            }
            static bool IsSupport(SkillData skill)=>skill.Effects.Any(e=>e.EffectType is EffectType.Heal or EffectType.HealPercent
                or EffectType.HealMissingHealthPercent or EffectType.HealOverTimePercent or EffectType.PaladinHeal or EffectType.PaladinRegeneration or EffectType.Shield);
            HashSet<GridPosition> DangerousCells()
            {
                var result=new HashSet<GridPosition>();
                foreach(var enemy in Session.Units.Where(u=>!u.IsPlayerSide))
                {
                    if(enemy.ForestEntState is {} fire)result.UnionWith(fire.Fields);
                    if(enemy.DeadKingState is {ShadowTurns:>0} shadow)result.UnionWith(shadow.DarkFields);
                    if(enemy.SkeletonSlayerState is {HammerTurns:>0} hammer)result.UnionWith(hammer.HammerFields);
                }
                return result;
            }
            bool Evade(BattleUnit actor)
            {
                var danger=DangerousCells();if(!danger.Contains(actor.Position))return false;
                var path=movement.CalculateReachablePaths(Session,actor)
                    .Where(p=>p.Value.Count>0 && !danger.Contains(p.Key))
                    .OrderBy(p=>CombatMovementSystem.CalculatePathCost(Session,actor.Position,actor.MovementStepsThisTurn,p.Value))
                    .ThenBy(p=>p.Key.X).ThenBy(p=>p.Key.Y).Select(p=>p.Value).FirstOrDefault();
                return path!=null && movement.TryMoveAlongPath(Session,actor,path);
            }
            float Score(BattleUnit actor,SkillData skill,int mode,IReadOnlyList<BattleUnit> targets)
            {
                float score=0;
                foreach(var target in targets)
                {
                    var opposing=target.IsPlayerSide!=actor.IsPlayerSide;
                    // Hidden units may receive incidental AoE damage, but their
                    // unseen positions must not steer the player's target choice.
                    if(opposing && StatusEffectSystem.IsInvisible(target))continue;
                    // Do not deliberately charge the Slayer's retaliation while
                    // its visible defensive stance stores incoming damage.
                    if(opposing && target.SkeletonSlayerState?.DefenseTurns>0)continue;
                    var preview=CombatSkillPreviewCalculator.CalculateTargetPreview(actor,target,skill,UseLegacyPlayerPolicy?-1:mode);
                    if(opposing && preview.Damage.HasDamage)
                    {
                        var expected=(preview.Damage.Minimum+preview.Damage.Maximum)*.5f*(preview.HasHitChance?preview.HitChance:1);
                        score+=Math.Min(target.Character.CurrentHealth,expected);
                        if(expected>=target.Character.CurrentHealth)score+=UseLegacyPlayerPolicy?1000:Math.Max(1000,target.Character.CurrentHealth+(actor.Character.Stats.MinAttack.GetValue()+actor.Character.Stats.MaxAttack.GetValue())*.5f);
                        if(!UseLegacyPlayerPolicy && target==playerFocus)score+=Math.Min(target.Character.CurrentHealth,expected)*.75f;
                    }
                    foreach(var original in skill.Effects.Where(e=>e.TargetModeIndex==mode))
                    {
                        var effect=PassiveEffectCombatExecutionSystem.ResolveEffect(actor,skill,original);
                        if(!SkillExecutionSystem.CanPlanEffect(Session,effect,actor,target))continue;
                        if(opposing && effect.EffectType==EffectType.ApplyStatus && !StatusEffectSystem.HasStatus(target,effect.StatusId))
                            score+=StatusEffectSystem.CalculateStatusApplicationChance(actor,target,effect.StatusId,effect.BaseChance)
                                *(StatusEffectSystem.IsDamageOverTimeStatus(effect.StatusId)
                                    ? (actor.Character.Stats.MinAttack.GetValue()+actor.Character.Stats.MaxAttack.GetValue())*.5f*effect.StatusDamageMultiplier*effect.DurationTurns
                                    : effect.StatusId==Godmist.Core.Constants.StatusEffectIds.Stun?500:150);
                        if(!opposing && effect.EffectType==EffectType.RestoreResource && target.Character.ResourcePool.CurrentValue<30)
                            score+=(actor.Character.Stats.MinAttack.GetValue()+actor.Character.Stats.MaxAttack.GetValue())*.3f;
                    }
                }
                var value=score+(AdditionalAuditScore?.Invoke(actor,skill,mode,targets)??0f);
                return TeamBurnScore(actor,skill,value-Math.Min(Math.Max(0,value)*.9f,MageBurnRiskPenalty(actor,skill,mode,targets)));
            }
            public bool PlayerAction(BattleUnit actor)
            {
                AuditPlayerDecision?.Invoke(actor,false,false);
                var start=actor.Position;
                var played=PlayerDecision(actor);
                if(actor.Position!=start)Result.heroMoves++;
                AuditPlayerDecision?.Invoke(actor,true,played);
                return played;
            }
            bool PlayerDecision(BattleUnit actor)
            {
                if(Evade(actor))return true;
                if(BuildManageToggles(actor))return true;
                if(TryJointLethalAttack(actor))return true;
                // Extraction is free and does not end the turn. Recover the
                // available dagger before a help-up consumes the entire turn.
                if(SmartRescue&&actor.MaxKnives>0&&actor.AvailableKnives==0)
                    foreach(var target in Session.Units.Where(u=>u.IsPlayerSide||!StatusEffectSystem.IsInvisible(u)).ToArray())
                        if(ScoutCombatSystem.TryExtractKnife(Session,actor,target,status,recovery))return true;
                if(SmartRescue&&TacticalSupport(actor,true,false))return true;
                if(SmartRescue&&NeedsUrgentRescueApproach(actor)&&TrySafeRescue(actor))return true;
                if(SmartRescue&&TacticalSupport(actor))return true;
                if(SmartRescue ? Support(actor) : !UseLegacyPlayerPolicy&&Support(actor,true)||Support(actor))return true;
                if(TryCommittedMageAttack(actor))return true;
                if(TryCommittedTeamAttack(actor))return true;
                if(TeamPolicy&&ProtectRangedPlayer(actor))return true;
                if(SmartRescue && TrySafeRescue(actor))return true;
                if(SmartRescue && TryProtectMagePosition(actor))return true;
                if(!SmartRescue&&(!UseLegacyPlayerPolicy || AuditLateKnifeExtraction && Result.turns>=200) && actor.AvailableKnives==0)
                    foreach(var target in Session.Units.Where(u=>UseLegacyPlayerPolicy || u.IsPlayerSide || !StatusEffectSystem.IsInvisible(u)).ToArray())
                        if(ScoutCombatSystem.TryExtractKnife(Session,actor,target,status,recovery))return true;
                // The real help-up action ends the actor's entire turn. Ignoring
                // it would turn a recoverable downing into permanent expedition loss.
                var rescueDanger=DangerousCells();
                foreach(var fallen in Session.Units.Where(u=>!SmartRescue && u.IsPlayerSide==actor.IsPlayerSide && u.Character.LifeState==CharacterLifeState.Downed
                    && (!UseLegacyPlayerPolicy || AuditRepeatRescues || !helpedUp.Contains(u.Character.CharacterId)))
                    .OrderBy(u=>u.DownedTurnsRemaining))
                {
                    if(new CombatReviveSystem().TryRevive(Session,actor,fallen)) {knockdownTrace?.ManualRevive(actor,fallen);Result.heroRevives++;helpedUp.Add(fallen.Character.CharacterId);endPlayerTurn=true;return true;}
                    int RescueDistance(GridPosition p)=>Math.Abs(p.X-fallen.Position.X)+Math.Abs(p.Y-fallen.Position.Y);
                    var rescue=movement.CalculateReachablePaths(Session,actor).Where(p=>p.Value.Count>0 && RescueDistance(p.Key)<RescueDistance(actor.Position)
                        && !p.Value.Any(rescueDanger.Contains)).OrderBy(p=>RescueDistance(p.Key))
                        .ThenBy(p=>CombatMovementSystem.CalculatePathCost(Session,actor.Position,actor.MovementStepsThisTurn,p.Value)).Select(p=>p.Value).FirstOrDefault();
                    if(rescue!=null && movement.TryMoveAlongPath(Session,actor,rescue))return true;
                }
                var runes=Session.SummonedObjects.Where(CatacombsBossSystem.IsRune).Select(r=>r.Position).ToHashSet();
                if(runes.Count>0)
                {
                    var disarm=movement.CalculateReachablePaths(Session,actor)
                        .Where(p=>p.Value.Count>0 && runes.Contains(p.Key) && !p.Value.Any(rescueDanger.Contains))
                        .OrderBy(p=>CombatMovementSystem.CalculatePathCost(Session,actor.Position,actor.MovementStepsThisTurn,p.Value))
                        .ThenBy(p=>p.Key.X).ThenBy(p=>p.Key.Y).Select(p=>p.Value).FirstOrDefault();
                    if(disarm!=null && movement.TryMoveAlongPath(Session,actor,disarm))return true;
                }
                // Hidden Strike explicitly rewards adjacent allied protection.
                if(Session.Units.Any(u=>u.BanditLeaderState is {PhaseTwo:true} && StatusEffectSystem.IsInvisible(u))
                    && !Session.Units.Any(u=>u!=actor && u.IsPlayerSide==actor.IsPlayerSide && u.Character.LifeState==CharacterLifeState.Alive
                        && Math.Abs(u.Position.X-actor.Position.X)+Math.Abs(u.Position.Y-actor.Position.Y)==1))
                {
                    var allies=Session.Units.Where(u=>u!=actor && u.IsPlayerSide==actor.IsPlayerSide && u.Character.LifeState==CharacterLifeState.Alive).ToArray();
                    var cover=movement.CalculateReachablePaths(Session,actor).Where(p=>p.Value.Count>0 && allies.Any(u=>
                        Math.Abs(u.Position.X-p.Key.X)+Math.Abs(u.Position.Y-p.Key.Y)==1))
                        .OrderBy(p=>CombatMovementSystem.CalculatePathCost(Session,actor.Position,actor.MovementStepsThisTurn,p.Value))
                        .Select(p=>p.Value).FirstOrDefault();
                    if(cover!=null && movement.TryMoveAlongPath(Session,actor,cover))return true;
                }
                if(!UseLegacyPlayerPolicy)UpdatePlayerFocus(actor);
                if(TryJointPressureApproach(actor))return true;
                var plans=BuildPlayerPlans(actor);
                if(TryCoordinatedFocusAttack(actor,plans))return true;
                foreach(var plan in plans.OrderByDescending(p=>p.score))
                    if(skills.TryExecute(Session,actor,plan.skill,plan.targets,null,plan.objectPosition,plan.positions))
                    { AuditChosenPlan?.Invoke(actor,plan.skill,plan.targets.Values.SelectMany(t=>t).Distinct().ToArray(),plan.score);
                        if(CombatActionCostCalculator.CalculateSkillCost(actor,plan.skill)==0)zeroCostUsed.Add(plan.skill);return true; }
                // Movement explicitly approaches the opposite side. Enemy AI's
                // FindClosestPlayerUnit is deliberately not used for heroes.
                var opponents=Session.Units.Where(u=>u.IsPlayerSide!=actor.IsPlayerSide && u.Character.LifeState==CharacterLifeState.Alive
                    && !StatusEffectSystem.IsInvisible(u)).ToArray();
                if(opponents.Length==0)return false;
                // A staged helper stays available for the rescue deadline;
                // a defensive withdrawal is not undone by a no-attack approach.
                if(SmartRescue&&(HoldRescuePosition(actor)||(Mage(actor)||JointPolicy)&&playerRepositioned))return false;
                if(!UseLegacyPlayerPolicy)return MovePlayerTactically(actor,opponents);

                // Diagnostic ablation only: do not spend leftover AP closing to
                // melee when a ranged basic attack already has a legal firing lane.
                var basic=actor.Character.GetBasicAttackSkill();
                if(AuditHoldRangedPosition && basic!=null && basic.TargetModes.Any(m=>m.TargetingType==TargetingType.Enemies
                    && m.AbsoluteRange.MaxRange>1 && opponents.Any(t=>!SkillData.IsPointBlank(t.Position.X-actor.Position.X,t.Position.Y-actor.Position.Y)
                        && CombatTargetingService.IsPositionWithinRange(actor.Position,t.Position,m.AbsoluteRange)
                        && (!basic.RequiresLineOfSight||sight.HasLineOfSight(Session,actor.Position,t.Position)))))return false;

                int Distance(GridPosition cell)=>opponents.Min(u=>Math.Abs(cell.X-u.Position.X)+Math.Abs(cell.Y-u.Position.Y));
                var before=Distance(actor.Position);var dangerCells=DangerousCells();
                // An empty scout picks up a reachable own dagger on the way back
                // into range. Recovery and Momentum rewards stay in movement owner.
                var knives=Session.MapObjects.OfType<KnifeCombatMapObject>()
                    .Where(k=>k.OwnerCharacterId==actor.Character.CharacterId).Select(k=>k.Position).ToHashSet();
                var paths=movement.CalculateReachablePaths(Session,actor).Where(p=>p.Value.Count>0
                    && (Distance(p.Key)<before || actor.MaxKnives>0 && actor.AvailableKnives==0 && knives.Contains(p.Key))
                    && !p.Value.Any(dangerCells.Contains));
                var path=paths.OrderBy(p=>actor.MaxKnives>0 && actor.AvailableKnives==0 && knives.Contains(p.Key)?-1:Distance(p.Key))
                    .ThenBy(p=>CombatMovementSystem.CalculatePathCost(Session,actor.Position,actor.MovementStepsThisTurn,p.Value))
                    .ThenBy(p=>p.Key.X).ThenBy(p=>p.Key.Y).Select(p=>p.Value).FirstOrDefault();
                return path!=null && movement.TryMoveAlongPath(Session,actor,path);
            }
            public void Run()
            {
                var clock=Stopwatch.StartNew();
                while(Session.Outcome==CombatOutcomeState.Ongoing && Result.turns<AuditTurnLimit && Result.actions<1200 && clock.Elapsed.TotalSeconds<AuditWallClockLimitSeconds)
                {
                    Result.turns++;
                    if(Session.ActiveSummonedObject is {} item)
                    { if(CatacombsBossSystem.IsRune(item))catacombs.ExecuteRuneTurn(Session,item);else objects.ExecuteTurnEffects(Session,item);turns.EndActiveTurn(Session);continue; }
                    var actor=Session.ActiveUnit;
                    if(actor?.Character.LifeState!=CharacterLifeState.Alive) {Evaluate(Session);if(Session.Outcome==CombatOutcomeState.Ongoing)turns.AdvanceTurn(Session);continue;}
                    knockdownTrace?.Ready(actor);AuditUnitTurn?.Invoke(actor);
                    if(StatusEffectSystem.IsHardCrowdControlled(actor))Result.hardControlTurns++;
                    else
                    {
                        zeroCostUsed.Clear();
                        playerRepositioned=false;
                        endPlayerTurn=false;
                        var state=ai.CreateTurnState();
                        for(var action=0;action<24 && !endPlayerTurn && Session.Outcome==CombatOutcomeState.Ongoing && Session.ActiveUnit==actor
                            && actor.Character.LifeState==CharacterLifeState.Alive;action++)
                        {
                            if(actor.IsPlayerSide)
                            { if(!PlayerAction(actor))break;knockdownTrace?.Action(actor);Result.actions++;Observe();continue; }
                            using var job=ai.CreateNextActionJob(Session,actor,state,timeoutSeconds:AuditAiTimeoutSeconds);
                            var steps=0;
                            while(!job.IsComplete && steps++<AuditAiSliceLimit)job.AdvanceSlice(2,2000);
                            if(job.HasTimedOut || !job.IsComplete) {Result.stop="AI budget";break;}
                            if(!job.Executed)break;
                            if(job.Action.Type==SimpleCombatAiActionType.Movement)Result.enemyMoves++;
                            Result.actions++;Observe();
                        }
                    }
                    Evaluate(Session);
                    if(Session.Outcome!=CombatOutcomeState.Ongoing)break;
                    if(Session.ActiveParticipant==null)turns.AdvanceTurn(Session);else turns.EndActiveTurn(Session);
                }
                Observe(); Result.seconds=(float)clock.Elapsed.TotalSeconds;Result.outcome=Session.Outcome.ToString();
                if(Session.Outcome==CombatOutcomeState.Ongoing && Result.stop==null)Result.stop="battle budget";
                Result.remainingHealth=heroes.Sum(u=>Math.Max(0,u.Character.CurrentHealth))/heroes.Sum(u=>u.Character.Stats.MaxHealth.GetValue());
                Result.enemyRemainingHealth=Session.Units.Where(u=>!u.IsPlayerSide).Sum(u=>Math.Max(0,u.Character.CurrentHealth));
                Result.deaths=heroes.Count(u=>u.Character.LifeState==CharacterLifeState.Dead);
                if(Result.stop!=null)File.WriteAllLines(Path.Combine(Folder,"simulation-last-budget.log"),trace);
            }
            public void Dispose() {foreach(var pair in healingObservers)pair.Key.HealingReceived-=pair.Value;healingObservers.Clear();leader.Dispose();slayer.Dispose();king.Dispose();ent.Dispose();passive.Dispose();}
            public void LeaveCombat() => lifecycle.RemoveCombatScopedEffects(Session);
        }

        [Test]
        public void UniformCandidateMatchesAuthoredProductionCalibration()
        {
            Godmist.Editor.EnemyScalingBalanceSetup.ApplyAll();
            activeSettings=JsonUtility.FromJson<Settings>("{}");
            Assert.That(activeSettings.bossBaseStatMultiplier,Is.EqualTo(1.4f));
            Assert.That(activeSettings.baseStatMultiplier,Is.EqualTo(1.4f),"Settings written before the new field must inherit the current production base multiplier.");
            foreach(var guid in AssetDatabase.FindAssets("t:CharacterDefinition",new[]{Root+"Catacombs/Enemies",Root+"Dungeons/Forest"}))
            {
                var definition=AssetDatabase.LoadAssetAtPath<CharacterDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if(!Godmist.Editor.EnemyScalingBalanceSetup.TryGetOriginalBase(definition.Id,out _))continue;
                var candidate=Clone(definition,"uniform",activeSettings.uniformHealth,activeSettings.uniformAttack);
                foreach(var level in new[]{1,10,48,50})
                {
                    var actual=new CharacterInstanceFactory().Create(definition,definition.ClassData,level);
                    var predicted=new CharacterInstanceFactory().Create(candidate,candidate.ClassData,level);
                    Assert.That(predicted.Stats.MaxHealth.BaseValue,Is.EqualTo(actual.Stats.MaxHealth.BaseValue).Within(.02f),definition.Id);
                    Assert.That(predicted.Stats.MinAttack.BaseValue,Is.EqualTo(actual.Stats.MinAttack.BaseValue).Within(.02f),definition.Id);
                    Assert.That(predicted.Stats.MaxAttack.BaseValue,Is.EqualTo(actual.Stats.MaxAttack.BaseValue).Within(.02f),definition.Id);
                }
            }
        }

        [TestCase("original",1f,1f)]
        [TestCase("roles",1f,1f)]
        [TestCase("uniform",2f,2.5f)]
        public void CompareFullBattles(string candidate,float hp,float attack)
        {
            var settingsPath=Path.Combine(Folder,"simulation-settings.json");
            var settings=File.Exists(settingsPath)?JsonUtility.FromJson<Settings>(File.ReadAllText(settingsPath)):new Settings();
            RunBattles(candidate,hp,attack,settings);
        }

        [Test]
        public void CompareRegularEncounterProfiles()
        {
            var json=File.ReadAllText(Path.Combine(Folder,"simulation-settings.json"));
            foreach(var roster in JsonUtility.FromJson<Settings>(json).rosterProfiles)
            {
                var settings=JsonUtility.FromJson<Settings>(json);
                settings.regularOnly=true;settings.rosterCandidate=roster;settings.tag+="-"+roster;
                RunBattles("uniform",settings.uniformHealth,settings.uniformAttack,settings);
            }
        }

        void RunBattles(string candidate,float hp,float attack,Settings settings)
        {
            activeSettings=settings;
            if(candidate=="uniform") { hp=settings.uniformHealth;attack=settings.uniformAttack; }
            var report=new Report {configuration=JsonUtility.ToJson(settings),policy="Production enemy AI. Separate hero policy: evade boss markers, respect Slayer defense, protect adjacent allies from Hidden Strike, heal below 75% HP, shield below 60%, choose legal expected impact per AP, approach opponents and recover knives. Legal 5-slot loadouts. No consumables/surprise; full production AV/cost/status/boss resolution. Bounds: 240 turns, 1200 actions, 12 seconds/battle."};
            var authored=AssetDatabase.FindAssets("t:EncounterDefinition",new[]{Root+"Catacombs/Encounters",Root+"Dungeons/Forest"})
                .Select(g=>AssetDatabase.LoadAssetAtPath<EncounterDefinition>(AssetDatabase.GUIDToAssetPath(g)))
                .OrderBy(e=>e.Id).ToArray();
            var definitions=authored.SelectMany(e=>e.Enemies).Select(e=>e.CharacterDefinition).Distinct().ToArray();
            EncounterDefinition Pack(string name, params string[] ids)
            {
                var pack=UnityEngine.Object.Instantiate(authored[0]);temporary.Add(pack);
                var data=new SerializedObject(pack);data.FindProperty("<Id>k__BackingField").stringValue="encounter.simulation.forest_"+name;
                var entries=data.FindProperty("<Enemies>k__BackingField");entries.arraySize=ids.Length;
                for(var i=0;i<ids.Length;i++)
                {
                    var entry=entries.GetArrayElementAtIndex(i);var definition=definitions.Single(d=>d.Id=="enemy.forest."+ids[i]);
                    entry.FindPropertyRelative("<CharacterDefinition>k__BackingField").objectReferenceValue=definition;
                    entry.FindPropertyRelative("<ClassData>k__BackingField").objectReferenceValue=definition.ClassData;
                }
                data.ApplyModifiedPropertiesWithoutUndo();return pack;
            }
            var pool=settings.regularOnly ? authored.Where(e=>e.Id.StartsWith("encounter.catacombs.")
                    ? e.Id is "encounter.catacombs.ambush" or "encounter.catacombs.bone_line" or "encounter.catacombs.haunting"
                        or "encounter.catacombs.occult_escort" or "encounter.catacombs.rotting_pair" or "encounter.catacombs.skeleton_guard" or "encounter.catacombs.undead_pack"
                    : Godmist.Editor.EncounterBalanceSetup.TryGetRoster(e.Id,out _) && !e.Id.EndsWith(Godmist.Editor.EncounterBalanceSetup.VersionSuffix))
                : authored.Where(e=>!Godmist.Editor.EncounterBalanceSetup.TryGetRoster(e.Id,out _)
                    || e.Id.EndsWith(Godmist.Editor.EncounterBalanceSetup.VersionSuffix)
                    || !authored.Any(next=>next.Id==e.Id+Godmist.Editor.EncounterBalanceSetup.VersionSuffix))
                .Concat(new[]{Pack("bandits_four","bandit_thug","bandit_thug","bandit_shooter","bandit_shooter"),
                    Pack("elves_four","elf_archer","elf_archer","elf_poacher","elf_druid")});
            var encounters=pool.Select(e=>
                {
                    if(settings.rosterCandidate!="candidate" || !Godmist.Editor.EncounterBalanceSetup.TryGetRoster(e.Id,out _))return e;
                    var copy=UnityEngine.Object.Instantiate(e);temporary.Add(copy);Godmist.Editor.EncounterBalanceSetup.ApplyRoster(copy);return copy;
                })
                .Where(e=>settings.encounters.Length==0 || settings.encounters.Any(id=>e.Id.Contains(id))).ToArray();
            Assert.That(encounters,Is.Not.Empty);
            var random=UnityEngine.Random.state;
            try
            {
                foreach(var level in settings.levels.Length==0?new[]{settings.level}:settings.levels)
                for(var seed=settings.seedStart;seed<settings.seedStart+settings.seeds;seed++) foreach(var encounter in encounters)
                {
                    var session=new CombatSession {Grid=new BattleGrid(12,10),DungeonTheme=encounter.Id.Contains("forest")?DungeonTheme.Forest:DungeonTheme.Catacombs};
                    for(var i=0;i<4;i++)
                    {
                        var hero=EnemyScalingBalanceTests.Hero(i,level,!settings.starter,settings.starter,settings.material,settings.upgraded);
                        var known=hero.GetKnownSkills().Select(s=>s.SkillId).ToHashSet();
                        Assert.That(hero.ActiveSkillLoadout.TryInitialize(known,hero.GetBasicAttackSkill().SkillId,Loadouts[i].Where(known.Contains)),Is.True);
                        session.Units.Add(new BattleUnit {Character=hero,IsPlayerSide=true,Position=new GridPosition(i==0||i==3?3:2,3+i)});
                    }
                    for(var i=0;i<encounter.Enemies.Count;i++)
                    {
                        var entry=encounter.Enemies[i];var definition=Clone(entry.CharacterDefinition,candidate,hp,attack);
                        var enemy=new CharacterInstanceFactory().Create(definition,entry.ClassData,level);enemy.CharacterId="sim-enemy-"+i;
                        session.Units.Add(new BattleUnit {Character=enemy,Position=new GridPosition(7+i/6,2+i%6),IsBoss=BaseId(definition.Id) is CatacombsBossSystem.BossId or SkeletonSlayerBossSystem.BossId or ForestEntBossSystem.BossId or BanditLeaderBossSystem.BossId});
                    }
                    var result=new Result {candidate=candidate,encounter=encounter.Id,level=level,seed=seed,build=settings.starter?"starter-unspent":settings.upgraded?"upgraded":"baseline",
                        enemyCount=encounter.Enemies.Count,enemyIds=encounter.Enemies.Select(e=>e.CharacterDefinition.Id).ToArray(),
                        heroSkills=session.Units.Where(u=>u.IsPlayerSide).SelectMany(u=>u.Character.GetActiveSkills()).Select(s=>s.SkillId).ToArray()};
                    UnityEngine.Random.InitState(61016+seed);
                    using(var engine=new Engine(session,result))engine.Run();
                    report.battles.Add(result);
                    File.WriteAllText(Path.Combine(Folder,"simulation-"+candidate+settings.tag+".json"),JsonUtility.ToJson(report,true));
                }
            }
            finally {UnityEngine.Random.state=random;}
            Assert.That(report.battles.All(b=>b.stop==null),Is.True,"Simulation budget reached; inspect report rather than counting it as a defeat.");
        }
        [Serializable] class BossFactor {public string id;public float health=1,attack=1;}
        [Serializable] class ExpeditionResult
        {
            public string dungeon, build, outcome;
            public DungeonLength length;
            public bool scouting;
            public int fullScheduled;
            public int seed, level, scheduled, cleared, camps, downings, deaths;
            public int[] endLevels;
            public float remainingHealth, remainingHealthBeforeQuest;
            public List<Result> battles=new();
            public List<PartySnapshot> before=new(), after=new();
        }
        [Serializable] class PartySnapshot
        {
            public int[] health, resource;
            public string[] life;
            public static PartySnapshot Take(CharacterInstance[] party) => new()
            {
                health=party.Select(h=>h.CurrentHealth).ToArray(),resource=party.Select(h=>h.ResourcePool.CurrentValue).ToArray(),
                life=party.Select(h=>h.LifeState.ToString()).ToArray()
            };
        }
        [Serializable] class ExpeditionReport
        {
            public string configuration, policyVersion="5.0-length-progression";
            public string policy="Generated authored dungeon rosters/counts, boss last on final floor; open battle arena. Same CharacterInstances persist. Real combat-scoped cleanup, exploration status/resource ticks, ally revive to 1 HP and DungeonCampService. Hero policy helps each ally up manually once per battle (whole turn), avoiding a doomed 1-HP rescue loop; actual healing can revive repeatedly. No free HP recovery, potions, gear upgrades or surprise. The progression flag enables real victory XP/level growth and the completionQuest flag grants one length-scaled quest only after completion. Earned skillpoints are not automatically invested during a series. Camp at <60% average HP or any living ally <30%; before boss use remaining kit if wounded or short of mana.";
            public List<ExpeditionResult> expeditions=new();
        }
        [Serializable] class RouteFloor {public string[] encounters;public string[] scouted;}
        [Serializable] class Route {public RouteFloor[] floors;}
        DungeonInstance ExpeditionRoute(DungeonDefinition definition,int seed,int level)
        {
            // Reuse the generated roster schedule across paired candidates and levels.
            // Arena geometry is deliberately fixed; regenerating room geometry adds
            // no information to this attrition comparison.
            var suffix=activeSettings.length==DungeonLength.Unspecified?"":"-length"+(int)activeSettings.length;
            // Retained legacy definitions must never leak into a current simulation
            // through an old roster cache, including runs without scouting enabled.
            {
                uint hash=2166136261;
                foreach(var c in string.Join("|",definition.EncounterDefinitions.Select(e=>e.Id+":"+string.Join(",",e.Enemies.Select(n=>n.CharacterDefinition.Id)))))
                    hash=unchecked((hash^c)*16777619);
                suffix+="-encounters-"+hash;
            }
            if(activeSettings.encounterBalanceRoutes)suffix+="-scouting";
            var path=Path.Combine(Folder,"route-"+definition.Id+"-"+seed+suffix+".json");
            if(!File.Exists(path))
            {
                var generated=new DungeonGeneratorService().Generate(definition,71016+seed,level,activeSettings.length);
                new DungeonEncounterPlacementSystem().Place(generated);
                var route=new Route {floors=generated.Floors.Select(f=>
                {
                    var encounters=f.Encounters.OrderBy(e=>e.Definition==definition.BossEncounterDefinition?1:0).ThenBy(e=>e.InstanceId,StringComparer.Ordinal).ToArray();
                    var rooms=activeSettings.encounterBalanceRoutes?EncounterScoutingRoute.SelectRooms(f):null;
                    return new RouteFloor {encounters=encounters.Select(e=>AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(e.Definition))).ToArray(),
                        scouted=rooms==null?null:encounters.Where(e=>rooms.Contains(e.RoomId)).Select(e=>AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(e.Definition))).ToArray()};
                }).ToArray()};
                File.WriteAllText(path,JsonUtility.ToJson(route,true));
            }
            var plan=JsonUtility.FromJson<Route>(File.ReadAllText(path));
            lastFullScheduled=plan.floors.Sum(f=>f.encounters.Length);
            var dungeon=new DungeonInstance(definition,71016+seed,level,activeSettings.length);
            for(var i=0;i<plan.floors.Length;i++)
            {
                var floor=new DungeonFloorInstance(i);Assert.That(dungeon.TryAddFloor(floor),Is.True);
                var schedule=activeSettings.scouting?plan.floors[i].scouted:plan.floors[i].encounters;
                Assert.That(schedule,Is.Not.Null,"Scouting requires a generated graph route.");
                for(var j=0;j<schedule.Length;j++)
                {
                    var encounter=AssetDatabase.LoadAssetAtPath<EncounterDefinition>(AssetDatabase.GUIDToAssetPath(schedule[j]));
                    Assert.That(encounter,Is.Not.Null);
                    Assert.That(floor.TryAddRoom(new DungeonRoomInstance($"room-{j}",DungeonRoomType.Normal)),Is.True);
                    Assert.That(floor.TryAddEncounter(new DungeonEncounterInstance($"route-{i}-{j:D3}",encounter,$"room-{j}",new RectInt(0,0,12,10))),Is.True);
                }
            }
            return dungeon;
        }
        int lastFullScheduled;

        [Test]
        public void RouteCacheUsesCurrentProductionPoolEvenWithoutScouting()
        {
            Godmist.Editor.EncounterBalanceSetup.ApplyAll();
            activeSettings=new Settings {length=DungeonLength.Short,encounterBalanceRoutes=false};
            var catalog=AssetDatabase.LoadAssetAtPath<GameContentCatalog>(Godmist.Editor.ForestExpeditionsSetup.CatalogPath);
            var definition=catalog.DungeonDefinitions.Single(d=>d!=null && d.Id=="dungeon.forest.expedition");
            var legacy=catalog.EncounterDefinitions.First(e=>e!=null && e.Id=="encounter.forest.wolf_pair");
            var seed=171099;
            string oldPath;
            do {oldPath=Path.Combine(Folder,"route-"+definition.Id+"-"+(seed++)+"-length1.json");}while(File.Exists(oldPath));
            seed--;
            var guid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(legacy));
            File.WriteAllText(oldPath,JsonUtility.ToJson(new Route {floors=new[]{new RouteFloor {encounters=new[]{guid}}}}));
            try
            {
                var dungeon=ExpeditionRoute(definition,seed,1);
                Assert.That(dungeon.Floors.Sum(f=>f.Encounters.Count),Is.InRange(9,11));
                Assert.That(dungeon.Floors.SelectMany(f=>f.Encounters).All(e=>e.Definition.Id.EndsWith(Godmist.Editor.EncounterBalanceSetup.VersionSuffix)),Is.True);
                activeSettings.encounterBalanceRoutes=true;activeSettings.scouting=true;
                var scouted=ExpeditionRoute(definition,seed,1);
                Assert.That(scouted.Floors.Sum(f=>f.Encounters.Count),Is.InRange(1,lastFullScheduled));
            }
            finally {File.Delete(oldPath);}
        }

        [Test]
        public void CompareExpeditionAttrition()
        {
            RunExpeditionAttrition(JsonUtility.FromJson<Settings>(File.ReadAllText(Path.Combine(Folder,"simulation-settings.json"))));
        }

        // Bounded per-level cases avoid the test runner's three-minute limit for
        // one giant matrix, without weakening any per-battle safety budget.
        [TestCase(1,false)] [TestCase(1,true)]
        [TestCase(5,false)] [TestCase(5,true)]
        [TestCase(10,false)] [TestCase(10,true)]
        [TestCase(20,false)] [TestCase(20,true)]
        [TestCase(30,false)] [TestCase(30,true)]
        [TestCase(40,false)] [TestCase(40,true)]
        [TestCase(48,false)] [TestCase(48,true)]
        [TestCase(50,false)] [TestCase(50,true)]
        public void CompareEarlyThreatExpeditionCalibration(int level,bool strengthened)
        {
            Godmist.Editor.EnemyScalingBalanceSetup.ApplyAll();
            RunExpeditionAttrition(new Settings
            {
                levels=new[]{level},seeds=level==1?4:2,seedStart=0,
                dungeons=new[]{"dungeon.catacombs.expedition","dungeon.forest.expedition"},
                length=DungeonLength.Short,compareScouting=true,encounterBalanceRoutes=true,
                progression=true,completionQuest=true,highestQuality=true,
                baseHealth=strengthened?Godmist.Editor.EnemyScalingBalanceSetup.BaseHealthFactor:.4f,
                baseAttack=strengthened?Godmist.Editor.EnemyScalingBalanceSetup.BaseAttackFactor:.6f,
                bossBaseStatMultiplier=1f, baseStatMultiplier=1f, // Historical 2026-10-18 comparison, before the later base-only boost.
                earlyHealth=strengthened?Godmist.Editor.EnemyScalingBalanceSetup.EarlyHealthGrowthFactor:.6f,
                earlyAttack=strengthened?Godmist.Editor.EnemyScalingBalanceSetup.EarlyAttackGrowthFactor:.6f,
                tag=(strengthened?"-early-threat-after-20261018":"-early-threat-before-20261018")+"-level"+level
            });
        }

        [TestCase(.44f,.63f)]
        [TestCase(.42f,.63f)]
        [TestCase(.42f,.6f)]
        [TestCase(.4f,.63f)]
        public void CompareLevelOneEarlyThreatCandidates(float baseHealth,float baseAttack)
        {
            // Candidate clones only; this sweep does not author production stats.
            RunExpeditionAttrition(new Settings
            {
                levels=new[]{1},seeds=2,seedStart=0,
                dungeons=new[]{"dungeon.catacombs.expedition","dungeon.forest.expedition"},
                length=DungeonLength.Short,compareScouting=true,encounterBalanceRoutes=true,
                progression=true,completionQuest=true,highestQuality=true,
                baseHealth=baseHealth,baseAttack=baseAttack,earlyHealth=.8f,earlyAttack=.8f,
                tag="-early-threat-grid-"+Mathf.RoundToInt(baseHealth*100)+"-"+Mathf.RoundToInt(baseAttack*100)+"-20261018"
            });
        }

        void RunExpeditionAttrition(Settings input)
        {
            activeSettings=input;
            var settings=activeSettings;
            if(settings.encounterBalanceRoutes)Godmist.Editor.EncounterBalanceSetup.ApplyAll();
            var report=new ExpeditionReport {configuration=JsonUtility.ToJson(settings)};
            var catalog=AssetDatabase.FindAssets("t:GameContentCatalog",new[]{Root}).Select(g=>AssetDatabase.LoadAssetAtPath<GameContentCatalog>(AssetDatabase.GUIDToAssetPath(g))).First(c=>c.ItemDefinitions.Any(i=>i!=null && i.Id==DungeonCampService.KitId));
            var definitions=AssetDatabase.FindAssets("t:DungeonDefinition",new[]{Root+"Dungeons"})
                .Select(g=>AssetDatabase.LoadAssetAtPath<DungeonDefinition>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(d=>d.IsAvailableAsOffer && settings.dungeons.Any(id=>d.Id.Contains(id))).OrderBy(d=>d.Id).ToArray();
            Assert.That(definitions,Is.Not.Empty);
            var random=UnityEngine.Random.state;
            try
            {
                foreach(var scouting in settings.compareScouting?new[]{false,true}:new[]{false})
                foreach(var length in settings.lengths.Length==0?new[]{settings.length}:settings.lengths)
                foreach(var level in settings.levels.Length==0?new[]{settings.level}:settings.levels)
                for(var seed=settings.seedStart;seed<settings.seedStart+settings.seeds;seed++) foreach(var definition in definitions)
                {
                    settings.length=length;
                    settings.scouting=scouting;
                    var dungeon=ExpeditionRoute(definition,seed,level);
                    var game=new GameSession(GameModeState.Exploration);
                    var heroes=Enumerable.Range(0,4).Select(i=>EnemyScalingBalanceTests.Hero(i,level,!settings.starter,settings.starter,settings.material,settings.upgraded,highestQuality:settings.highestQuality)).ToArray();
                    for(var i=0;i<heroes.Length;i++)
                    {
                        var h=heroes[i];h.CharacterId="expedition-hero-"+i;
                        var known=h.GetKnownSkills().Select(s=>s.SkillId).ToHashSet();
                        Assert.That(h.ActiveSkillLoadout.TryInitialize(known,h.GetBasicAttackSkill().SkillId,Loadouts[i].Where(known.Contains)),Is.True);
                        Assert.That(game.TryRegisterCharacter(h),Is.True);
                        Assert.That(game.TryAddCharacterToActiveParty(h.CharacterId),Is.True);
                    }
                    game.TrySetActiveLeader(heroes[0].CharacterId);game.TrySetActiveDungeon(dungeon);
                    var camp=new DungeonCampService();Assert.That(camp.TryIssueSupplies(game,catalog),Is.True);
                    Assert.That(heroes.Sum(h=>h.Inventory.Items.Count(DungeonCampService.IsCampingKit)),Is.EqualTo(dungeon.CampingKitAllowance));
                    var row=new ExpeditionResult {dungeon=definition.Id,length=length,scouting=scouting,fullScheduled=lastFullScheduled,level=level,seed=seed,build=settings.starter?"starter-unspent":settings.upgraded?"upgraded":"baseline",outcome="Completed",
                        scheduled=dungeon.Floors.Sum(f=>f.Encounters.Count)};
                    var exploration=new ExplorationStatusEffectSystem();
                    UnityEngine.Random.InitState(81016+seed);
                    foreach(var floor in dungeon.Floors)
                    {
                        dungeon.TrySetCurrentFloorIndex(floor.FloorIndex);
                        foreach(var encounter in floor.Encounters.OrderBy(e=>e.Definition==definition.BossEncounterDefinition?1:0).ThenBy(e=>e.InstanceId,StringComparer.Ordinal))
                        {
                            if(row.cleared>0)
                            {
                                exploration.Advance(settings.travelSeconds,heroes.Select(h=>new DungeonTrapOccupant(new BattleUnit {Character=h,IsPlayerSide=true},null,Vector2Int.zero)).ToArray());
                                var reviver=heroes.FirstOrDefault(h=>h.LifeState==CharacterLifeState.Alive);
                                if(reviver!=null) foreach(var fallen in heroes.Where(h=>h.LifeState==CharacterLifeState.Downed))
                                    new CombatReviveSystem().TryReviveExploration(reviver,fallen);
                                if(settings.camps && (heroes.Sum(h=>h.CurrentHealth)/heroes.Sum(h=>h.Stats.MaxHealth.GetValue())<.6f
                                    || heroes.Any(h=>h.LifeState==CharacterLifeState.Alive && h.CurrentHealth/h.Stats.MaxHealth.GetValue()<.3f)
                                    || encounter.Definition==definition.BossEncounterDefinition && heroes.Any(h=>h.LifeState==CharacterLifeState.Alive
                                        && (h.CurrentHealth/h.Stats.MaxHealth.GetValue()<.95f || h.ResourcePool.ResourceType==ResourceType.Mana && h.ResourcePool.CurrentValue<h.ResourcePool.MaxValue*.6f))))
                                { if(camp.TryCamp(game,floor,out _))row.camps++; }
                            }
                            if(heroes.All(h=>h.LifeState!=CharacterLifeState.Alive)) {row.outcome="TravelDefeat";break;}
                            row.before.Add(PartySnapshot.Take(heroes));
                            var session=new CombatSession {Grid=new BattleGrid(12,10),DungeonTheme=definition.Theme};
                            for(var i=0;i<heroes.Length;i++) if(heroes[i].LifeState==CharacterLifeState.Alive)
                                session.Units.Add(new BattleUnit {Character=heroes[i],IsPlayerSide=true,Position=new GridPosition(i==0||i==3?3:2,3+i)});
                            for(var i=0;i<encounter.Enemies.Count;i++)
                            {
                                var entry=encounter.Enemies[i];var npcDefinition=Clone(entry.CharacterDefinition,"uniform",settings.uniformHealth,settings.uniformAttack);
                                var npc=new CharacterInstanceFactory().Create(npcDefinition,entry.ClassData,level);npc.CharacterId="chain-enemy-"+row.cleared+"-"+i;
                                if(settings.progression)session.Rewards.EnemyRewards.Add(new CombatEnemyReward{EnemyInstanceId=npc.CharacterId,ExperienceLevel=level});
                                session.Units.Add(new BattleUnit {Character=npc,Position=new GridPosition(7+i/6,2+i%6),IsBoss=BaseId(npcDefinition.Id) is CatacombsBossSystem.BossId or SkeletonSlayerBossSystem.BossId or ForestEntBossSystem.BossId or BanditLeaderBossSystem.BossId});
                            }
                            var result=new Result {candidate="expedition",encounter=encounter.Definition.Id,level=level,seed=seed,build=row.build,enemyCount=encounter.Enemies.Count,enemyIds=encounter.Enemies.Select(e=>e.CharacterDefinition.Id).ToArray()};
                            using(var engine=new Engine(session,result)) {engine.Run();engine.LeaveCombat();}
                            if(settings.progression && result.outcome=="Victory")
                                Assert.That(new CombatRewardSystem().TryGrantVictoryRewards(game,session),Is.True);
                            row.battles.Add(result);row.downings+=result.downings;row.after.Add(PartySnapshot.Take(heroes));
                            if(result.stop!=null) {row.outcome="Budget";break;}
                            if(result.outcome!="Victory") {row.outcome="Defeat";break;}
                            row.cleared++;
                        }
                        if(row.outcome!="Completed")break;
                    }
                    row.remainingHealthBeforeQuest=heroes.Sum(h=>Math.Max(0,h.CurrentHealth))/heroes.Sum(h=>h.Stats.MaxHealth.GetValue());
                    if(settings.progression && row.outcome=="Completed" && settings.completionQuest)
                        new Godmist.Systems.Quests.QuestRewardSystem().GrantRadiantRewards(game,
                            new Godmist.Runtime.Quests.RadiantRewardOffer(level,CharacterProgressionSystem.GetStandardMissionExperience(level)*dungeon.CompletionRewardMultiplier,0,0,null));
                    row.endLevels=heroes.Select(h=>h.Level).ToArray();
                    row.deaths=heroes.Count(h=>h.LifeState==CharacterLifeState.Dead);
                    row.remainingHealth=heroes.Sum(h=>Math.Max(0,h.CurrentHealth))/heroes.Sum(h=>h.Stats.MaxHealth.GetValue());
                    report.expeditions.Add(row);
                    File.WriteAllText(Path.Combine(Folder,"expedition"+settings.tag+".json"),JsonUtility.ToJson(report,true));
                }
            }
            finally {UnityEngine.Random.state=random;}
            Assert.That(report.expeditions.All(e=>e.outcome!="Budget"),Is.True,"Incomplete simulation is not a defeat.");
        }
        [Test]
        public void ExpeditionRecoveryCarriesHealthAndConsumesOnlyFiniteRealSupplies()
        {
            var party=Enumerable.Range(0,4).Select(i=>EnemyScalingBalanceTests.Hero(i,48)).ToArray();
            var game=new GameSession(GameModeState.Exploration);
            for(var i=0;i<party.Length;i++)
            {
                party[i].CharacterId="carry-"+i;party[i].CurrentHealth=100;
                game.TryRegisterCharacter(party[i]);game.TryAddCharacterToActiveParty(party[i].CharacterId);
            }
            game.TrySetActiveLeader(party[0].CharacterId);
            var dungeon=new DungeonInstance("carry-test",1);
            dungeon.TryAddFloor(new DungeonFloorInstance(0));dungeon.TryAddFloor(new DungeonFloorInstance(1));
            game.TrySetActiveDungeon(dungeon);
            party[0].ResourcePool.CurrentValue=40;party[1].ResourcePool.CurrentValue=150;
            party[2].ResourcePool.CurrentValue=0;party[3].LifeState=CharacterLifeState.Dead;party[3].CurrentHealth=0;
            var units=party.Select(h=>new BattleUnit {Character=h,IsPlayerSide=true}).ToArray();
            new ExplorationStatusEffectSystem().Advance(6,units.Select(u=>new DungeonTrapOccupant(u,null,Vector2Int.zero)).ToArray());
            Assert.That(party.Select(h=>h.CurrentHealth),Is.EqualTo(new[]{100,100,100,0}),"Travel grants no free HP reset.");
            // Rage decay and the developed hero's real resource talents both tick.
            Assert.That(party[0].ResourcePool.CurrentValue,Is.GreaterThan(0).And.LessThan(40));
            Assert.That(party[1].ResourcePool.CurrentValue,Is.Zero);
            Assert.That(party[2].ResourcePool.CurrentValue,Is.GreaterThan(0).And.LessThan(party[2].ResourcePool.MaxValue));
            var catalog=AssetDatabase.FindAssets("t:GameContentCatalog",new[]{Root})
                .Select(g=>AssetDatabase.LoadAssetAtPath<GameContentCatalog>(AssetDatabase.GUIDToAssetPath(g)))
                .First(c=>c.ItemDefinitions.Any(i=>i!=null && i.Id==DungeonCampService.KitId));
            var camp=new DungeonCampService();Assert.That(camp.TryIssueSupplies(game,catalog),Is.True);
            Assert.That(camp.TryIssueSupplies(game,catalog),Is.False);
            for(var use=0;use<2;use++)
            {
                var before=party.Select(h=>h.CurrentHealth).ToArray();
                Assert.That(camp.TryCamp(game,dungeon.Floors[0],out _),Is.True);
                for(var i=0;i<3;i++)
                {
                    var maximum=Mathf.RoundToInt(party[i].Stats.MaxHealth.GetValue());
                    Assert.That(party[i].CurrentHealth,Is.EqualTo(Mathf.Min(maximum,before[i]+Mathf.RoundToInt(maximum*.4f))));
                }
                Assert.That(party[3].LifeState,Is.EqualTo(CharacterLifeState.Dead));Assert.That(party[3].CurrentHealth,Is.Zero);
            }
            var final=party.Select(h=>h.CurrentHealth).ToArray();
            Assert.That(camp.TryCamp(game,dungeon.Floors[0],out _),Is.False);
            Assert.That(party.Select(h=>h.CurrentHealth),Is.EqualTo(final));
        }
        [Test]
        public void HeroPolicyUsesRealPaladinHealingAndRegeneration()
        {
            var healer=EnemyScalingBalanceTests.Hero(3,48);healer.CharacterId="healer";
            var known=healer.GetKnownSkills().Select(s=>s.SkillId).ToHashSet();
            Assert.That(healer.ActiveSkillLoadout.TryInitialize(known,healer.GetBasicAttackSkill().SkillId,Loadouts[3].Where(known.Contains)),Is.True);
            var wounded=EnemyScalingBalanceTests.Hero(0,48);wounded.CharacterId="wounded";wounded.CurrentHealth=1;
            var source=AssetDatabase.LoadAssetAtPath<EncounterDefinition>(Root+"Dungeons/Forest/ED_Forest_BanditPatrol.asset").Enemies[0].CharacterDefinition;
            var unit=new BattleUnit {Character=healer,IsPlayerSide=true,Position=new GridPosition(3,4)};
            var session=new CombatSession {Grid=new BattleGrid(12,10),Units=new()
            {
                unit,new BattleUnit {Character=wounded,IsPlayerSide=true,Position=new GridPosition(3,5)},
                new BattleUnit {Character=new CharacterInstanceFactory().Create(source,source.ClassData,1),Position=new GridPosition(9,4)}
            }};
            var random=UnityEngine.Random.state;
            try
            {
                var result=new Result();using var engine=new Engine(session,result);
                session.ActiveUnit=unit;session.ActiveParticipant=session.TurnParticipants.Single(p=>p.Unit==unit);
                CombatTurnSystem.RefreshMaxActionPoints(session,unit,true);
                Assert.That(engine.PlayerAction(unit),Is.True);
                Assert.That(wounded.PassiveEffects.Any(e=>e.EffectId=="paladin.holy_regeneration" && PassiveEffectLifecycleSystem.IsActive(e)),Is.True);
                var before=wounded.CurrentHealth;
                Assert.That(engine.PlayerAction(unit),Is.True);
                Assert.That(wounded.CurrentHealth,Is.GreaterThan(before));
                Assert.That(result.skills.Any(s=>s.id=="hero:skill.paladin.heal"),Is.True);
                Assert.That(result.skills.Any(s=>s.id=="hero:skill.paladin.holy_regeneration"),Is.True);
            }
            finally {UnityEngine.Random.state=random;}
        }
        [Test]
        public void HeroControllerApproachesARealRangedOpponentAndReplayIsDeterministic()
        {
            activeSettings=new Settings();
            var encounter=AssetDatabase.LoadAssetAtPath<EncounterDefinition>(Root+"Dungeons/Forest/ED_Forest_BanditPatrol.asset");
            var source=encounter.Enemies.Select(e=>e.CharacterDefinition).Single(d=>d.Id=="enemy.forest.bandit_shooter");
            var enemy=Clone(source,"original",1,1);
            var random=UnityEngine.Random.state;
            try
            {
                Result Fight()
                {
                    var hero=EnemyScalingBalanceTests.Hero(0,48);hero.CharacterId="replay-hero";
                    var npc=new CharacterInstanceFactory().Create(enemy,enemy.ClassData,48);npc.CharacterId="replay-enemy";
                    var session=new CombatSession {Grid=new BattleGrid(12,10),Units=new()
                    {
                        new BattleUnit {Character=hero,IsPlayerSide=true,Position=new GridPosition(2,4)},
                        new BattleUnit {Character=npc,Position=new GridPosition(8,4)}
                    }};
                    UnityEngine.Random.InitState(6610);var result=new Result();
                    using(var engine=new Engine(session,result))engine.Run();result.seconds=0;return result;
                }
                var first=Fight();var second=Fight();
                Assert.That(first.stop,Is.Null);Assert.That(first.heroMoves,Is.GreaterThan(0));
                Assert.That(first.skills.Any(s=>s.id.StartsWith("hero:",StringComparison.Ordinal)),Is.True);
                Assert.That(JsonUtility.ToJson(first),Is.EqualTo(JsonUtility.ToJson(second)));
            }
            finally {UnityEngine.Random.state=random;}
        }
        [Serializable] class Settings
        {
            public int seeds=2,seedStart,level=48,material;
            public int[] levels=Array.Empty<int>();
            public bool starter,upgraded,highestQuality;
            public DungeonLength length;
            public bool progression,completionQuest;
            public float baseHealth=Godmist.Editor.EnemyScalingBalanceSetup.BaseHealthFactor,
                baseAttack=Godmist.Editor.EnemyScalingBalanceSetup.BaseAttackFactor;
            public float baseStatMultiplier=Godmist.Editor.EnemyScalingBalanceSetup.OrdinaryBaseStatMultiplier;
            public float bossBaseStatMultiplier=Godmist.Editor.EnemyScalingBalanceSetup.BossBaseStatMultiplier;
            public DungeonLength[] lengths=Array.Empty<DungeonLength>();
            public bool regularOnly;
            public string rosterCandidate="legacy";
            public string[] rosterProfiles={"legacy","candidate"};
            public bool encounterBalanceRoutes,compareScouting,scouting;
            public bool ramp=true;
            public float uniformHealth=2f,uniformAttack=2.5f;
            public float earlyHealth=Godmist.Editor.EnemyScalingBalanceSetup.EarlyHealthGrowthFactor,
                earlyAttack=Godmist.Editor.EnemyScalingBalanceSetup.EarlyAttackGrowthFactor;
            public bool camps=true;
            public float travelSeconds=18;
            public string[] dungeons={"catacombs", "forest"};
            public string tag="";
            public BossFactor[] bosses={
                new() {id="enemy.catacombs.dead_king",health=1.25f,attack=1.5f},
                new() {id="enemy.catacombs.skeleton_slayer",health=2.25f,attack=2.5f},
                new() {id="enemy.forest.infected_ent",health=2.5f,attack=2.5f},
                new() {id="enemy.forest.bandit_leader",health=2.5f,attack=1.15f}};
            public string[] encounters={"bone_line","bandit_patrol"};
        }
    }
}

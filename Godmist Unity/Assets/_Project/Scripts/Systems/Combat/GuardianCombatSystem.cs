using System;
using System.Linq;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Data.Skills;
using Godmist.Data.Effects;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public static class GuardianCombatSystem
    {
        public const string Tree="tree.scout.guardian";
        public const string Shield="skill.scout.guardian.t1_3",Taunt="skill.scout.guardian.t1_4",Counter="skill.scout.guardian.t5_4";
        public const string FreeStepMarker="scout.guardian.free_step",CounterMarker=Counter,SureStep="scout.guardian.sure_step";
        public static int Rank(CharacterInstance c,string slot)=>c?.SkillTreeProgress?.FirstOrDefault(p=>p.TreeId==Tree)?.NodeInvestments?
            .Where(n=>n.NodeId=="node.scout.guardian."+slot).Select(n=>Math.Max(0,n.Ranks)).DefaultIfEmpty(0).Max()??0;
        public static int G(BattleUnit u,string slot)=>Rank(u?.Character,slot);
        public static bool Active(string id)=>id is Shield or Taunt or Counter;
        static bool Alive(BattleUnit u)=>u?.Character?.LifeState==CharacterLifeState.Alive;
        static bool Member(BattleUnit u)=>Alive(u)&&!u.IsAiControlledSummon&&!u.IsStationarySummon
            &&u.WarriorState.Session?.Summons?.Any(s=>s.Unit==u)!=true;
        public static bool Direct(DamageType type)=>type is DamageType.Physical or DamageType.Magical;
        public static int Distance(BattleUnit a,BattleUnit b)=>a==null||b==null||a.Position.Level!=b.Position.Level?int.MaxValue:
            Math.Abs(a.Position.X-b.Position.X)+Math.Abs(a.Position.Y-b.Position.Y);
        static CombatSession Session(BattleUnit u,CombatSession session)=>session??u?.WarriorState.Session;
        static bool AdjacentAlly(CombatSession session,BattleUnit u)=>session?.Units?.Any(a=>a!=u&&Member(a)&&a.IsPlayerSide==u.IsPlayerSide&&Distance(a,u)==1)==true;
        static PassiveEffectRuntime TauntEffect(BattleUnit u)=>u?.Character?.PassiveEffects?.LastOrDefault(e=>e.EffectId==StatusEffectIds.Taunt&&PassiveEffectLifecycleSystem.IsActive(e));

        public static void ApplyStats(CharacterInstance c,Action<CharacterStatType,string,StatModifierOperation,float> observe=null)
        {
            if(c?.Stats==null)return;
            var points=c.SkillTreeProgress?.FirstOrDefault(p=>p.TreeId==Tree)?.NodeInvestments?.Sum(n=>Math.Max(0,n.Ranks))??0;
            var global=.005f*points;var magic=Rank(c,"t1_5") switch {1=>.1f,>=2=>.3f,_=>0};var reflex=8*Rank(c,"t1_1");
            c.Stats.PhysicalDefense.SetEffectiveValue(c.Stats.PhysicalDefense.GetValue()*(1+global));
            c.Stats.MagicalDefense.SetEffectiveValue(c.Stats.MagicalDefense.GetValue()*(1+global)*(1+magic));
            c.Stats.Dodge.SetEffectiveValue(c.Stats.Dodge.GetValue()+reflex);
            observe?.Invoke(CharacterStatType.PhysicalDefense,Tree,StatModifierOperation.Percent,global);
            observe?.Invoke(CharacterStatType.MagicalDefense,Tree,StatModifierOperation.Percent,global);
            observe?.Invoke(CharacterStatType.MagicalDefense,"node.scout.guardian.t1_5",StatModifierOperation.Percent,magic);
            observe?.Invoke(CharacterStatType.Dodge,"node.scout.guardian.t1_1",StatModifierOperation.FlatAbsolute,reflex);
        }
        public static float DodgeBonus(BattleUnit target,CombatSession session=null)
        {
            session=Session(target,session);if(!Member(target)||session?.Units==null)return 0;
            float escort=0;
            foreach(var owner in session.Units)
                if(Member(owner)&&owner.IsPlayerSide==target.IsPlayerSide&&G(owner,"t4_3")>0&&Distance(owner,target)<=1&&AdjacentAlly(session,owner))
                    escort=Math.Max(escort,5*G(owner,"t4_3"));
            return escort;
        }
        public static float DefenseMultiplier(BattleUnit target,CombatSession session=null)=>
            Member(target)&&AdjacentAlly(Session(target,session),target)?1+.1f*G(target,"t5_3"):1;
        public static float AccuracyPenalty(BattleUnit source,CombatSession session=null)
        {
            session=Session(source,session);
            if(!Alive(source)||!StatusEffectSystem.HasStatus(source,StatusEffectIds.Taunt))return 0;
            // Query sources without TryGetTaunter's cleanup side effect (also used by previews).
            var effect=TauntEffect(source);
            if(effect?.Sources==null||session?.Units==null)return 0;
            foreach(var origin in effect.Sources)
                if(origin.SourceId==Taunt)
                {
                    var owner=session.Units.FirstOrDefault(u=>Alive(u)&&u.Character.CharacterId==origin.SourceCharacterId);
                    if(owner!=null)return 5*G(owner,"t2_4");
                }
            return 0;
        }
        static bool TauntedBy(BattleUnit attacker,BattleUnit owner)
        {
            var effect=TauntEffect(attacker);
            return effect?.Sources?.Any(s=>s.SourceId==Taunt&&s.SourceCharacterId==owner?.Character?.CharacterId)==true;
        }
        public static float IncomingMultiplier(CombatSession session,BattleUnit source,BattleUnit target,DamageType type,bool? firstHitUsed=null)
        {
            if(!Direct(type)||!Alive(target))return 1;
            session=Session(target,session);float result=1;
            if(!(firstHitUsed??target.GuardianState.FirstHitUsed))result*=1-.1f*G(target,"t1_2");
            if(source!=null&&TauntedBy(source,target))result*=1-.1f*G(target,"t3_4");
            float knife=0;var cover=false;
            foreach(var owner in session?.Units?.AsEnumerable()??Enumerable.Empty<BattleUnit>())
            {
                if(!Member(owner)||owner.IsPlayerSide!=target.IsPlayerSide)continue;
                if(Member(target)&&owner!=target&&Distance(owner,target)==1&&G(owner,"t4_5")>0&&Distance(source,target)>=2&&source!=null)cover=true;
                var rank=G(owner,"t5_2");
                if(rank>0&&session.MapObjects.OfType<KnifeCombatMapObject>().Any(k=>k.OwnerCharacterId==owner.Character.CharacterId&&k.Position.Level==target.Position.Level
                    &&Math.Abs(k.Position.X-target.Position.X)+Math.Abs(k.Position.Y-target.Position.Y)<=1))
                    knife=Math.Max(knife,rank==1?.05f:rank==2?.08f:.1f);
            }
            return result*(1-knife)*(cover?.9f:1f);
        }
        public static void RecordDirectHit(BattleUnit target,DamageType type)
        {if(Direct(type)&&Alive(target))target.GuardianState.FirstHitUsed=true;}
        public static float StatusBaseChance(BattleUnit target,string id,float chance)=>id is StatusEffectIds.Stun or StatusEffectIds.Immobilize or StatusEffectIds.Sleep or StatusEffectIds.Paralysis or StatusEffectIds.Taunt or "fear"
            ?Math.Max(0,chance-.1f*G(target,"t2_5")):chance;
        public static int DiscountedBaseCost(BattleUnit owner,SkillData skill,int cost)=>Active(skill?.SkillId)&&owner?.GuardianState.DiscountUsed==false
            ?Mathf.CeilToInt(cost*(1-.1f*G(owner,"t5_1"))):cost;
        public static void Paid(BattleUnit owner,SkillData skill,int actual)
        {if(Active(skill?.SkillId)){owner.GuardianState.DiscountUsed=true;owner.GuardianState.PaidResourceThisAction=actual;}}
        public sealed class CastScope:IDisposable
        {
            readonly BattleUnit owner;readonly int previous;
            public CastScope(BattleUnit owner,SkillData skill){if(!Active(skill?.SkillId))return;this.owner=owner;previous=owner.GuardianState.PaidResourceThisAction;owner.GuardianState.PaidResourceThisAction=0;}
            public void Dispose(){if(owner!=null)owner.GuardianState.PaidResourceThisAction=previous;}
        }
        public static int ShieldAmount(BattleUnit target)=>Mathf.FloorToInt((target?.Character?.MaximumHealth??0)*.15f);
        public static SkillEffectData ResolveEffect(BattleUnit source,SkillData skill,SkillEffectData effect)=>skill?.SkillId==Shield
            ?effect.WithRuntimeParameters(effect.DamageHitCount,3+G(source,"t2_3"),effect.BaseChance):effect;
        public static bool Execute(BattleUnit caster,BattleUnit target,SkillData skill,StatusEffectSystem statuses,CombatShieldSystem shields,float? statusRoll=null)
        {
            if(!Alive(caster)||!Alive(target))return false;
            switch(skill.SkillId)
            {
                case Shield:
                    var given=shields.GrantShield(target,ShieldAmount(target),Shield,3+G(caster,"t2_3"),true);
                    if(given>0&&caster!=target&&G(caster,"t3_3")>0)
                        shields.GrantShield(caster,Math.Min(Mathf.FloorToInt(caster.Character.MaximumHealth*.1f),Mathf.FloorToInt(given*.5f)),"scout.guardian.front",3,true);
                    return given>0;
                case Taunt:
                    var applied=(statuses??new StatusEffectSystem()).ApplyStatus(caster,target,StatusEffectIds.Taunt,.9f,3,1,0,0,Taunt,sharedRoll:statusRoll);
                    if(!applied&&G(caster,"t4_4")>0)Restore(caster,Mathf.FloorToInt(caster.GuardianState.PaidResourceThisAction*.5f*G(caster,"t4_4")));
                    return applied;
                case Counter:
                    caster.GuardianState.CounterTurns=2;ScoutCombatSystem.Marker(caster,CounterMarker,"Kontratak");return true;
            }
            return false;
        }
        static void Restore(BattleUnit u,int amount)
        {if(amount<=0||ResourceRuleSystem.IsResourceGainBlocked(u))return;var before=u.Character.ResourcePool.CurrentValue;u.Character.ResourcePool.Restore(amount);MomentumResourceRule.RefreshSpeedAfterChange(u.Character,before);}
        public static void TurnStart(BattleUnit u)
        {
            if(u==null)return;var state=u.GuardianState;state.OwnTurn++;state.FreeStepsTaken=0;
            new PassiveEffectLifecycleSystem().RemoveEffect(u.Character,SureStep);
            if(!StatusEffectSystem.HasStatus(u,FreeStepMarker))state.HasFreeStep=false;
            state.DodgeRewardUsed=state.StepAwarded=state.FirstHitUsed=state.PaidMovementUsed=state.DiscountUsed=false;
        }
        public static void TurnEnd(BattleUnit u)
        {
            if(u==null)return;var state=u.GuardianState;
            if(state.CounterTurns>0&&--state.CounterTurns==0)StatusEffectSystem.RemoveStatusAppliedBySource(u,CounterMarker,CounterMarker);
            if(state.HasFreeStep&&state.OwnTurn>=state.FreeStepExpiresAfterTurn){state.HasFreeStep=false;StatusEffectSystem.RemoveStatusAppliedBySource(u,FreeStepMarker,FreeStepMarker);}
        }
        public static bool HasFreeStep(BattleUnit u)=>Alive(u)&&u.GuardianState.HasFreeStep&&StatusEffectSystem.HasStatus(u,FreeStepMarker);
        public static int PaidSteps(BattleUnit u)=>Math.Max(0,(u?.MovementStepsThisTurn??0)-(u?.GuardianState.FreeStepsTaken??0));
        public static void ProjectMove(BattleUnit u,bool guardianFree,int cost)
        {
            // Called only on detached planner units. Do not run lifecycle on shared payloads.
            if(guardianFree)
            {
                u.GuardianState.HasFreeStep=false;u.GuardianState.FreeStepsTaken++;
                u.Character.PassiveEffects.RemoveAll(e=>e.EffectId==FreeStepMarker);
            }
            if(cost>0&&!guardianFree&&!u.GuardianState.PaidMovementUsed)
            {
                u.GuardianState.PaidMovementUsed=true;
                u.Character.Stats.Dodge.SetEffectiveValue(u.Character.Stats.Dodge.GetValue()+4*G(u,"t2_1"));
            }
        }
        public static int PathCost(CombatSession session,BattleUnit u,GridPosition origin,int paidBefore,
            System.Collections.Generic.IReadOnlyList<GridPosition> path,int discount,int warriorFree)
        {
            var free=HasFreeStep(u);var awarded=u.GuardianState.StepAwarded;var inventory=u.AvailableKnives;
            var visitedKnives=new System.Collections.Generic.HashSet<string>();var total=0;var ordinal=paidBefore;
            foreach(var cell in path)
            {
                if(warriorFree>0){warriorFree--;ordinal++;}
                else if(free)free=false;
                else
                {
                    ordinal++;
                    var cost=Math.Max(0,ordinal-discount)*ActionPointUnits.PerPoint;
                    total+=PassiveEffectCombatExecutionSystem.ApplyMovementCost(session,u,cost)+(session.Grid.GetMovementSurcharge(origin,cell)+MageCombatSystem.FrostMovementSurcharge(session,u,origin,cell))*ActionPointUnits.PerPoint;
                }
                if(G(u,"t3_2")>0&&inventory<u.MaxKnives)
                    foreach(var knife in session.MapObjects.OfType<KnifeCombatMapObject>())
                        if(knife.Position==cell&&knife.OwnerCharacterId==u.Character.CharacterId&&visitedKnives.Add(knife.ObjectId))
                        {
                            var count=Math.Min(knife.KnifeCount,u.MaxKnives-inventory);inventory+=count;
                            if(count>0&&!awarded&&!free&&G(u,"t2_2")>0){free=true;awarded=true;}
                        }
                origin=cell;
            }
            return total;
        }
        public static int ProjectRecovery(CombatSession session,BattleUnit u)
        {
            if(G(u,"t3_2")==0)return 0;
            var recovered=0;
            for(var i=session.MapObjects.Count-1;i>=0;i--)
                if(session.MapObjects[i] is KnifeCombatMapObject knife&&knife.Position==u.Position&&knife.OwnerCharacterId==u.Character.CharacterId)
                {
                    var count=Math.Min(knife.KnifeCount,Math.Max(0,u.MaxKnives-u.AvailableKnives));
                    if(count==0)continue;u.AvailableKnives+=count;recovered+=count;
                    if(count==knife.KnifeCount)session.MapObjects.RemoveAt(i);
                    else session.MapObjects[i]=new KnifeCombatMapObject(knife.ObjectId,knife.Position,knife.OwnerCharacterId,knife.KnifeCount-count){LandingOrigin=knife.LandingOrigin};
                }
            if(recovered>0)
            {
                var state=u.GuardianState;
                if(!HasFreeStep(u)&&!state.StepAwarded&&G(u,"t2_2")>0)
                {
                    state.HasFreeStep=state.StepAwarded=true;state.FreeStepExpiresAfterTurn=state.OwnTurn+1;
                    u.Character.PassiveEffects.Add(new PassiveEffectRuntime(FreeStepMarker,new(){PassiveEffectSourceFactory.FromRuntimeSystem(u,FreeStepMarker)},1,0,0,PassiveEffectLifetime.CombatScoped,PassiveEffectTickPhase.None){DisplayName="Taneczny Krok"});
                }
                if(ScoutCombatSystem.BladeRank(u,"t5_2")>0)u.ScoutState.RecoveredStacks=Math.Min(3,u.ScoutState.RecoveredStacks+recovered);
            }
            return recovered;
        }
        static void AwardStep(BattleUnit u)
        {
            var state=u.GuardianState;if(G(u,"t2_2")==0||HasFreeStep(u)||state.StepAwarded)return;
            state.HasFreeStep=state.StepAwarded=true;state.FreeStepExpiresAfterTurn=state.OwnTurn+1;
            ScoutCombatSystem.Marker(u,FreeStepMarker,"Taneczny Krok",0);
        }
        public static void Recovered(BattleUnit u,int count){if(count>0&&G(u,"t3_2")>0)AwardStep(u);}
        public static void Moved(BattleUnit u,bool guardianFree,int paidCost)
        {
            var state=u.GuardianState;
            if(guardianFree){state.HasFreeStep=false;state.FreeStepsTaken++;StatusEffectSystem.RemoveStatusAppliedBySource(u,FreeStepMarker,FreeStepMarker);}
            if(paidCost>0&&!guardianFree&&!state.PaidMovementUsed)
            {
                state.PaidMovementUsed=true;var rank=G(u,"t2_1");
                if(rank>0)
                {
                    var effect=new PassiveEffectRuntime(SureStep,new(){PassiveEffectSourceFactory.FromRuntimeSystem(u,SureStep)},1,0,0,PassiveEffectLifetime.CombatScoped,PassiveEffectTickPhase.None)
                        {DisplayName="Pewny Krok",StatModifiers=new(){new Godmist.Core.Types.StatModifier{StatType=CharacterStatType.Dodge,Operation=StatModifierOperation.FlatAbsolute,Value=4*rank}}};
                    new PassiveEffectLifecycleSystem().TryApplyCombatScopedEffect(u.Character,effect,PassiveEffectStackingMode.ReplaceDurationAndValues,out _);
                }
            }
        }
        public static void Dodged(CombatSession session,BattleUnit defender,BattleUnit attacker,PassiveEffectCombatContext context,
            CombatDamageSystem damage,Action<string> log=null,CombatFeedbackBus feedback=null)
        {
            if(context?.DamageType is DamageType.Poison or DamageType.Bleed or DamageType.Burn)return;
            if(!Alive(defender)||defender.GuardianState.DodgeRewardUsed||context?.IsPassiveReaction==true||attacker?.GuardianState.ReactionDepth>0)return;
            defender.GuardianState.DodgeRewardUsed=true;AwardStep(defender);
            Restore(defender,G(defender,"t4_2") switch {1=>6,>=2=>10,_=>0});
            var rank=G(defender,"t3_1");var skill=defender.Character.ClassData?.BasicAttackSkill;
            if(rank==0||!Alive(attacker)||skill==null||Distance(defender,attacker)>4||Distance(defender,attacker)==int.MaxValue
                ||StatusEffectSystem.IsStunned(defender)||StatusEffectSystem.IsSleeping(defender)||StatusEffectSystem.IsParalyzed(defender)
                ||!ScoutKnifeSystem.CanPay(defender,skill,attacker)||!new CombatLineOfSightService().HasLineOfSight(session,defender.Position,attacker.Position))return;
            var actionId=Guid.NewGuid().ToString("N");
            if(!ScoutKnifeSystem.TrySpendAndDrop(session,defender,skill,new[]{attacker},actionId:actionId))return;
            var state=defender.GuardianState;state.ReactionDepth++;
            var contacted=false;
            try
            {
                log?.Invoke(defender.Character.Definition.DisplayName+" wykonuje Szybki Odwet.");
                feedback?.Publish(new CombatFeedbackEvent(defender,CombatFeedbackType.SkillUsed,effectName:"Szybki Odwet",skillPresentation:new CombatSkillPresentation(defender,skill,attacker.Position,skill.ResolveAttackType(attacker.Position.X-defender.Position.X,attacker.Position.Y-defender.Position.Y),targets:new[]{attacker})));
                var type=skill.ResolveAttackType(attacker.Position.X-defender.Position.X,attacker.Position.Y-defender.Position.Y);
                var ctx=new PassiveEffectCombatContext(session,defender,attacker,skill:skill,actionId:actionId,attackType:type,damageType:DamageType.Physical,isPassiveReaction:true);
                var roll=CombatDamageValueCalculator.CalculateDamageValue(defender,DamageValueSource.AttackRange,rank==1?.4f:.7f);
                var accuracy=PassiveEffectCombatExecutionSystem.ResolveParameter(defender,skill,"accuracy",skill.SkillAccuracy);
                var hit=damage.ResolveAndApplyDamage(session,defender,attacker,roll,DamageType.Physical,accuracy,passiveContext:ctx);
                contacted=hit.Hit;
                if(hit.Hit)ScoutCombatSystem.TryEmbedKnife(session,actionId,defender,attacker,type,()=>UnityEngine.Random.value);
                if(hit.Hit&&Alive(attacker)&&type==AttackType.Melee&&G(defender,"t4_1")>0)
                    new SkillExecutionSystem(battleLog:log,statusEffectSystem:new StatusEffectSystem(),damageSystem:damage,feedbackBus:feedback).TryGuardianPush(session,defender,attacker,G(defender,"t4_1"));
            }
            finally{ScoutCombatSystem.FinishKnifeAction(session,actionId,contacted);state.ReactionDepth--;}
        }
        public static Action PrepareReflection(CombatSession session,BattleUnit source,BattleUnit target,int raw,DamageType type,
            PassiveEffectCombatContext context,CombatDamageSystem damage)
        {
            if(!Alive(target)||!Alive(source)||!Direct(type)||target.GuardianState.CounterTurns==0||!StatusEffectSystem.HasStatus(target,CounterMarker)||context?.IsPassiveReaction==true||source.GuardianState.ReactionDepth>0||source==target)return null;
            target.GuardianState.CounterTurns=0;StatusEffectSystem.RemoveStatusAppliedBySource(target,CounterMarker,CounterMarker);
            var multiplier=1+.75f*G(target,"t5_5");var value=Mathf.RoundToInt(raw*multiplier);
            return ()=>
            {
                if(!Alive(source))return;
                var reflected=CombatDamageSystem.ApplyDefense(target,source,value,type);
                damage.ApplyDamage(session,target,source,reflected,type,passiveContext:new PassiveEffectCombatContext(session,target,source,skillId:Counter,damageType:type,isPassiveReaction:true),allowWarriorProcs:false,preMitigationDamage:value,allowVampirism:false);
            };
        }
    }
}

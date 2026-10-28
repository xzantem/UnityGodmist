using System;
using System.Linq;
using System.Collections.Generic;
using Godmist.Core.Constants;
using Godmist.Core.Types;
using Godmist.Core.Enums;
using Godmist.Data.Skills;
using Godmist.Data.Effects;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Characters;
using UnityEngine;
namespace Godmist.Systems.Combat
{
    public static partial class WarriorCombatSystem
    {
        public const string Commander="tree.warrior.commander", Brawler="tree.warrior.brawler";
        public const string Cut="skill.warrior.cut", DeathWish="skill.warrior.death_wish";
        public static int Rank(CharacterInstance c,string tree,string id) => c?.SkillTreeProgress?
            .FirstOrDefault(p=>p.TreeId==tree)?.NodeInvestments?.FirstOrDefault(n=>n.NodeId=="node.warrior."+(tree==Commander?"commander.":"brawler.")+id)?.Ranks ?? 0;
        public static int C(BattleUnit u,string id)=>Rank(u?.Character,Commander,id);
        public static int B(BattleUnit u,string id)=>Rank(u?.Character,Brawler,id);
        public static int Points(CharacterInstance c,string tree)=>c?.SkillTreeProgress?.FirstOrDefault(p=>p.TreeId==tree)?.NodeInvestments?.Sum(n=>Math.Max(0,n.Ranks))??0;
        public static bool GainBlocked(BattleUnit u)=>u?.Character?.PassiveEffects?.Any(e=>PassiveEffectLifecycleSystem.IsActive(e) && e.GetPayload<ResourceRegenerationBlockPayload>()!=null)==true;
        public static bool IsRage(BattleUnit u)=>u?.Character?.ResourcePool?.ResourceType==ResourceType.Rage;
        public static int Cost(BattleUnit u,SkillData skill,int cost)
        {
            if(!IsRage(u) || skill?.ResourceType!=ResourceType.Rage)return cost;
            if(skill.SkillId=="skill.warrior.shout")return u.Character.ResourcePool.CurrentValue;
            return PassiveEffectCombatExecutionSystem.IsOffensiveSkill(skill)?Mathf.CeilToInt(cost*(1-.1f*B(u,"furious_strikes"))-.00001f):cost;
        }
        public static float PreviewDealtMultiplier(BattleUnit u,SkillData skill,float current)
        {
            var cost=PassiveEffectCombatExecutionSystem.ResolveResourceCost(u,skill);
            if(!CanOverspend(u,skill,cost))return current;
            var penalty=Mathf.Clamp01((cost-u.Character.ResourcePool.CurrentValue)*(B(u,"rage")>0?.01f:.02f));
            return new CombatVariableSystem().GetMultiplier(u.Character,GameplayVariableType.DamageDealtPercent,excludedEffectId:"warrior.shortage")*(1-penalty);
        }
        public static bool CanOverspend(BattleUnit u,SkillData skill,int cost)
        {
            var pool=u?.Character?.ResourcePool;
            return pool!=null && pool.ResourceType==ResourceType.Rage && skill?.ResourceType==ResourceType.Rage
                && pool.MaxValue>0 && cost>pool.MaxValue && pool.CurrentValue==pool.MaxValue;
        }
        public static void Paid(BattleUnit u,SkillData skill,int cost,int spent,CombatRecoverySystem recovery)
        {
            if(!IsRage(u) || skill?.ResourceType!=ResourceType.Rage)return;
            if(cost>spent) Variable(u,u,GameplayVariableType.DamageDealtPercent,-Mathf.Min(1,(cost-spent)*(B(u,"rage")>0?.01f:.02f)),3,"warrior.shortage","Niedobór wściekłości");
            u.WarriorState.HealingRemainder+=spent*.02f*Points(u.Character,Brawler);
            var heal=Mathf.FloorToInt(u.WarriorState.HealingRemainder);u.WarriorState.HealingRemainder-=heal;
            recovery?.Heal(u,heal);Refresh(u);
        }
        public static void Variable(BattleUnit source,BattleUnit target,GameplayVariableType type,float value,int turns,string id,string label)
        {
            var e=new PassiveEffectRuntime(id,new(){PassiveEffectSourceFactory.FromRuntimeSystem(source,id)},1,turns,0,PassiveEffectLifetime.Timed,PassiveEffectTickPhase.TurnEnd){DisplayName=label};
            e.SetPayload(new VariableModifiersPayload{Modifiers=new(){new GameplayVariableModifier{VariableType=type,Value=value,Stacking=id=="warrior.shortage"?StatModifierStacking.Multiplicative:StatModifierStacking.Additive}}});
            new PassiveEffectLifecycleSystem().TryApplyTimedEffect(target.Character,e,PassiveEffectStackingMode.ReplaceDurationAndValues,out _);
        }
        public static void Refresh(BattleUnit u){if(u?.Character!=null && (Points(u.Character,Commander)>0 || Points(u.Character,Brawler)>0))new CharacterStatPipelineSystem().Refresh(u.Character);}
        public static void ApplyStats(CharacterInstance c)
        {
            if(c?.Stats==null)return;
            var commander=Points(c,Commander);var brawler=Points(c,Brawler);
            if(commander+brawler==0)return;
            var s=c.Stats;
            s.MaxResource.SetEffectiveValue((s.MaxResource.GetValue()+10*Rank(c,Brawler,"release_rage"))*(1+.01f*brawler));
            var rage=Mathf.Min(c.ResourcePool?.CurrentValue??0,Mathf.RoundToInt(s.MaxResource.GetValue()));
            s.CritChance.SetEffectiveValue(s.CritChance.GetValue()*(1+.05f*Rank(c,Commander,"weapon_mastery"))+.003f*commander+.0005f*Rank(c,Brawler,"revenge")*rage);
            s.CritDamage.SetEffectiveValue(s.CritDamage.GetValue()+.2f*Rank(c,Commander,"piercing"));
            s.PhysicalDefensePenetration.SetEffectiveValue(s.PhysicalDefensePenetration.GetValue()+.25f*Rank(c,Commander,"armor_shred"));
            var attack=.1f*s.PhysicalDefense.GetValue()*Rank(c,Brawler,"spiked_armor");
            s.MinAttack.SetEffectiveValue(s.MinAttack.GetValue()+attack);s.MaxAttack.SetEffectiveValue(s.MaxAttack.GetValue()+attack);
            s.Speed.SetEffectiveValue(s.Speed.GetValue()+4*(rage/25)*Rank(c,Brawler,"adrenaline"));
        }
        public static bool CanActivate(BattleUnit u,SkillData skill)=>skill?.SkillId == WarriorDischargeRules.SkillId
            ? IsRage(u) && WarriorDischargeRules.Cost(u) >= WarriorDischargeRules.MinimumRage
            : skill?.SkillId!=DeathWish || IsRage(u) && u.Character.ResourcePool.CurrentValue>=ToggleSkillUpkeepCalculator.Calculate(skill,u.Character.ResourcePool);
        public static bool DeathProtected(BattleUnit u)=>u?.ActiveToggleSkillIds?.Contains(DeathWish)==true;
        public static int Adjacent(CombatSession s,BattleUnit u)=>s?.Units?.Count(a=>a!=u && a.IsPlayerSide==u.IsPlayerSide && a.Character.LifeState==CharacterLifeState.Alive
            && Math.Abs(a.Position.X-u.Position.X)+Math.Abs(a.Position.Y-u.Position.Y)==1 && s.Grid.CanMoveBetween(a.Position,u.Position))??0;
        public static float Protection(CombatSession s,BattleUnit target)=>target?.WarriorState.ProtectionTurns>0 ? 1-Mathf.Min(.6f,.3f+.1f*Adjacent(s ?? target.WarriorState.Session,target)):1;
        public static float ProtectionPriority(int adjacentAllies)=>1+Mathf.Min(1.25f,.5f+.25f*adjacentAllies);
        public static float TargetPriority(CombatSession s,BattleUnit target)=>target?.WarriorState.ProtectionTurns>0 && target.WarriorState.Riposte ? ProtectionPriority(Adjacent(s ?? target.WarriorState.Session,target)):1;
        public static float DamageMultiplier(PassiveEffectCombatContext ctx,bool preview=false)
        {
            if(ctx?.Source==null || ctx.Target?.Character==null)return 1;
            if (ctx.SkillId == WarriorDischargeRules.SkillId) return WarriorDischargeRules.Multiplier(preview ? WarriorDischargeRules.Cost(ctx.Source) : ctx.DischargeCast?.RageSpent ?? WarriorDischargeRules.Cost(ctx.Source));
            var bonus=0f;
            var cuts=ctx.Source.WarriorState.ConsecutiveCuts;
            if(preview)cuts=ctx.Source.WarriorState.LastCutTarget==ctx.Target.Character.CharacterId?cuts+1:1;
            if(ctx.SkillId==Cut && cuts%3==0 && cuts>0)
                bonus+=.0025f*B(ctx.Source,"third_time")*(ctx.Source.Character.ResourcePool?.CurrentValue??0);
            return 1+bonus;
        }
        public static float ArmorBonus(BattleUnit source,BattleUnit target)=>target?.Character?.Stats==null?1:1+.15f*C(source,"armor_piercing")*target.Character.CurrentHealth/Mathf.Max(1,target.Character.Stats.MaxHealth.GetValue());
        public static float Received(CombatSession session,BattleUnit source,BattleUnit target,DamageType type)
        {
            if(target==null)return 1;
            var w=target.WarriorState;var value=Protection(session,target)*ArmorBonus(source,target);
            if(w.OrderTurns>0 && w.OrderSource?.IsPlayerSide==source?.IsPlayerSide)value*=1.2f+.05f*C(w.OrderSource,"breakdown");
            if(w.TraumaTurns>0 && (type==DamageType.Bleed || type==DamageType.Poison || type==DamageType.Burn))value*=1+w.TraumaBonus;
            return value;
        }
        public static float DefenseMultiplier(BattleUnit target,CombatSession session)
        {
            var w=target?.WarriorState;
            return w?.OrderTurns>0?Mathf.Max(0,1-.05f*C(w.OrderSource,"flanking")*AdjacentEnemies(session ?? w.Session,target,w.OrderSource)):1;
        }
        static int AdjacentEnemies(CombatSession session,BattleUnit target,BattleUnit source)=>session?.Units?.Count(u=>u.IsPlayerSide==source.IsPlayerSide && u.Character.LifeState==CharacterLifeState.Alive
            && Math.Abs(u.Position.X-target.Position.X)+Math.Abs(u.Position.Y-target.Position.Y)==1 && session.Grid.CanMoveBetween(u.Position,target.Position))??0;
        public static float CriticalMultiplier(BattleUnit source,BattleUnit target)=>target?.WarriorState.OrderTurns>0 && target.WarriorState.OrderSource?.IsPlayerSide==source?.IsPlayerSide?1+.5f*C(target.WarriorState.OrderSource,"marking"):1;
        public static void BeginSkill(BattleUnit caster,SkillData skill,IReadOnlyList<BattleUnit> targets)
        {
            var w=caster.WarriorState;var id=targets?.FirstOrDefault()?.Character?.CharacterId;
            if(skill.SkillId==Cut){w.ConsecutiveCuts=w.LastCutTarget==id?w.ConsecutiveCuts+1:1;w.LastCutTarget=id;}
            else{w.ConsecutiveCuts=0;w.LastCutTarget=null;}
        }
        public static bool RefundAp(BattleUnit u,SkillData skill)=>skill?.SkillId==Cut && UnityEngine.Random.value<.03f*C(u,"finisher");
        public static int FreeSteps(BattleUnit u)=>u?.WarriorState.FreeMovementTurns>0?Math.Max(0,2-u.WarriorState.FreeStepsUsed):0;
        public static bool FreeStep(BattleUnit u)=>u?.WarriorState.FreeMovementTurns>0 && u.WarriorState.FreeStepsUsed<2;
        public static void Moved(BattleUnit u){if(FreeStep(u))u.WarriorState.FreeStepsUsed++;}
        public static void TurnStart(BattleUnit u)
        {
            if(u==null)return;u.WarriorState.FreeStepsUsed=0;
            if(IsRage(u) && !GainBlocked(u) && B(u,"aggressive_nature")>0){u.Character.ResourcePool.Restore(3);u.Character.ResourcePool.HasGainedResourceSinceLastTurn=true;}
            Refresh(u);
        }
        public static void TurnEnd(BattleUnit u)
        {
            if(u==null)return;var w=u.WarriorState;
            w.OrderTurns=Math.Max(0,w.OrderTurns-1);w.ProtectionTurns=Math.Max(0,w.ProtectionTurns-1);w.TraumaTurns=Math.Max(0,w.TraumaTurns-1);w.FreeMovementTurns=Math.Max(0,w.FreeMovementTurns-1);
            Refresh(u);
        }
    }
}

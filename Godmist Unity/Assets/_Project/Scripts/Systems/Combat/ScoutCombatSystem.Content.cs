using System;
using System.Linq;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public static partial class ScoutCombatSystem
    {
        public static bool IsBoss(Godmist.Runtime.Dungeons.DungeonInstance dungeon, Godmist.Data.Combat.EncounterDefinition encounter, string enemyId)
        {
            var boss=dungeon?.Definition?.BossEncounterDefinition;
            return boss!=null && boss==encounter && boss.BossEnemyIndex<boss.Enemies.Count && boss.Enemies[boss.BossEnemyIndex].InstanceId==enemyId;
        }
        public static int Points(BattleUnit unit, string tree) => unit?.Character?.SkillTreeProgress?
            .FirstOrDefault(p => p.TreeId == tree)?.NodeInvestments?.Sum(n => Math.Max(0,n.Ranks)) ?? 0;
        public static void Face(BattleUnit unit, GridPosition target)
        {
            if (unit == null) return;
            var dx=target.X-unit.Position.X; var dy=target.Y-unit.Position.Y;
            if (dx==0 && dy==0) return;
            unit.ScoutState.Facing = Math.Abs(dx)>=Math.Abs(dy) ? new Vector2Int(Math.Sign(dx),0) : new Vector2Int(0,Math.Sign(dy));
        }
        public static bool Behind(BattleUnit source, BattleUnit target) => source != null && target != null
            && (target.ScoutState.Facing.x*(source.Position.X-target.Position.X)+target.ScoutState.Facing.y*(source.Position.Y-target.Position.Y)) < 0;
        public static float DamageMultiplier(PassiveEffectCombatContext context,CombatValueExplanation explanation=null)
        {
            if(context?.Source==null || !context.DamageType.HasValue || !IsDirect(context.DamageType.Value))return 1;
            var source=context.Source;var bonus=0f;
            void Add(string id,float value,bool applies=true)
            {
                if(applies)bonus+=value;
                if(value!=0)explanation?.Add(id,(applies?"+":"Nie uwzględniono: +")+(100*value).ToString("0.###",System.Globalization.CultureInfo.GetCultureInfo("pl-PL"))+"%");
            }
            Add(AssassinTree,Points(source,AssassinTree)*.005f,context.AttackType==AttackType.Melee);
            Add("node.scout.assassin.karambit_style",AssassinRank(source,"karambit_style") switch{1=>.25f,2=>.6f,3=>1f,_=>0},context.AttackType==AttackType.Melee);
            Add("node.scout.assassin.backstab",.1f*AssassinRank(source,"backstab"),context.Target!=null&&Behind(source,context.Target));
            Add("node.scout.assassin.surprise",.3f*AssassinRank(source,"surprise"),StatusEffectSystem.IsInvisible(source));
            Add("node.scout.blade_dancer.accurate_throw",.02f*BladeRank(source,"accurate_throw")*Math.Max(0,context.Distance),context.Target!=null);
            if(context.SkillId=="skill.scout.ricochet")Add("node.scout.assassin.throw_power",ChainIndex(context)*.1f*AssassinRank(source,"throw_power"));
            return 1+bonus;
        }
        static int ChainIndex(PassiveEffectCombatContext context)
        {
            if (context.TargetsByModeIndex == null || !context.TargetsByModeIndex.TryGetValue(0,out var list)) return 0;
            for(var i=0;i<list.Count;i++) if(list[i]==context.Target)return i;
            return 0;
        }
        public static float ChainCriticalChance(int index)=>Math.Max(0,index)*.1f;
        public static float CriticalChance(PassiveEffectCombatContext context)
        {
            if (context?.Source == null) return 0;
            return (context.AttackType==AttackType.Melee ? .05f*AssassinRank(context.Source,"rapid_strikes") : 0)
                +(context.Target?.ScoutState.WeakPointTurns>0 ? .1f : 0)
                +(context.SkillId=="skill.scout.ricochet" ? ChainCriticalChance(ChainIndex(context)) : 0);
        }
        public static float WeakPointCriticalDamage(BattleUnit target) => target?.ScoutState.WeakPointTurns>0 ? target.ScoutState.WeakPointCritDamage : 0f;
        public static float AnalysisPenetration(BattleUnit source) => source?.ScoutState.AnalysisTurns>0 ? source.ScoutState.AnalysisPenetration : 0f;
        public static void AfterCritical(PassiveEffectCombatContext context, bool critical)
        {
            var rank=AssassinRank(context?.Source,"deep_analysis");
            if (!critical || rank==0 || context.Target.ScoutState.WeakPointTurns<=0) return;
            context.Source.ScoutState.AnalysisTurns=3;
            context.Source.ScoutState.AnalysisPenetration=rank==1 ? .2f : .3f;
        }
        public static void TurnStart(BattleUnit unit) { if(unit!=null)unit.ScoutState.RunningAttackUsed=false; }
        public static void TurnEnd(BattleUnit unit)
        {
            if(unit==null)return;
            unit.ScoutState.WeakPointTurns=Math.Max(0,unit.ScoutState.WeakPointTurns-1);
            unit.ScoutState.AnalysisTurns=Math.Max(0,unit.ScoutState.AnalysisTurns-1);
        }
        public static int MovementDiscount(BattleUnit unit) => !StatusEffectSystem.IsInvisible(unit) ? 0
            : AssassinRank(unit,"light_step") switch {1=>1,2=>3,_=>0};
        public static float PoisonMultiplier(BattleUnit source,CombatValueExplanation explanation=null)
        {
            var tree=1f+Points(source,BladeTree)*.01f;
            var master=1f+.1f*BladeRank(source,"poison_master");
            if(tree!=1)explanation?.Add(BladeTree,"×"+tree.ToString("0.####",System.Globalization.CultureInfo.GetCultureInfo("pl-PL")));
            if(master!=1)explanation?.Add("node.scout.blade_dancer.poison_master","×"+master.ToString("0.####",System.Globalization.CultureInfo.GetCultureInfo("pl-PL")));
            return tree*master;
        }
        public static SkillEffectData ResolveEffect(BattleUnit source, SkillData skill, SkillEffectData effect)
        {
            var trials=effect.AdditionalHitTrials;var chance=effect.BonusDamageMultiplierChance;
            var dot=effect.StatusDamageMultiplier;var value=effect.ModifierValue;var turns=effect.DurationTurns;
            switch(skill?.SkillId)
            {
                case "skill.scout.quick_strikes":
                    trials+=BladeRank(source,"series");
                    if(BladeRank(source,"series")>0)trials+=BladeRank(source,"dance_of_doom");
                    chance+=.1f*BladeRank(source,"fine_thrusts");break;
                case "skill.scout.poisoned_dagger":dot*=1f+.1f*BladeRank(source,"venom");if(effect.EffectType==EffectType.ModifyResistance)value=-.15f*BladeRank(source,"viruses");break;
                case "skill.scout.tonic":
                    if(value>0)value+=10*BladeRank(source,"brew_strength");
                    else value-=5*BladeRank(source,"brew_strength");break;
            }
            return effect.WithScoutParameters(trials,chance,dot,value,turns);
        }
        public static bool RollCombo(BattleUnit source, SkillData skill, bool duplicate) => !duplicate && skill?.SkillId=="skill.knife_toss"
            && UnityEngine.Random.value < .05f*BladeRank(source,"combo");
        public static void AfterSkillHit(CombatSession session, BattleUnit source, BattleUnit target, SkillData skill, bool combo, StatusEffectSystem statuses)
        {
            if (target?.Character?.LifeState != CharacterLifeState.Alive || statuses==null)return;
            if (skill.SkillId=="skill.knife_toss" && UnityEngine.Random.value < .1f*BladeRank(source,"knee_throw"))
                TimedStat(source,target,CharacterStatType.Speed,-15,3,"scout.knee_throw");
            if (combo && BladeRank(source,"combo_master")>0)
                statuses.ApplyStatus(source,target,StatusEffectIds.Bleed,.8f,3,1,HeroTalentEffectValues.ComboBleedMultiplier(BladeRank(source,"combo_master")),0,"scout.combo_master");
            if (Behind(source,target) && source.MovementStepsThisTurn>0)
                statuses.ApplyStatus(source,target,StatusEffectIds.Stun,.25f*AssassinRank(source,"leg_sweep"),1,1,0,0,"scout.leg_sweep");
        }
        public static bool TimedStat(BattleUnit source,BattleUnit target,CharacterStatType stat,float value,int turns,string id,
            string displayName = null, string sourceSkillId = null)
        {
            var effect=new PassiveEffectRuntime(id,new(){PassiveEffectSourceFactory.FromRuntimeSystem(source,sourceSkillId??id)},1,turns,0,
                PassiveEffectLifetime.Timed,PassiveEffectTickPhase.TurnEnd)
            { DisplayName=displayName??id,StatModifiers=new(){new StatModifier{StatType=stat,Operation=StatModifierOperation.FlatAbsolute,Value=value}} };
            return new PassiveEffectLifecycleSystem().TryApplyTimedEffect(target.Character,effect,PassiveEffectStackingMode.ReplaceDurationAndValues,out _);
        }
        public static void OnMoved(CombatSession session,BattleUnit source,StatusEffectSystem statuses)
        {
            var rank=AssassinRank(source,"running_attack");
            if(rank==0 || source.ScoutState.RunningAttackUsed || source.Character.LifeState!=CharacterLifeState.Alive)return;
            var targets=session.Units.Where(t=>t.IsPlayerSide!=source.IsPlayerSide && t.Character.LifeState==CharacterLifeState.Alive
                && Math.Abs(t.Position.X-source.Position.X)+Math.Abs(t.Position.Y-source.Position.Y)==1
                && session.Grid.CanMoveBetween(source.Position,t.Position)).ToArray();
            if(targets.Length==0)return;
            source.ScoutState.RunningAttackUsed=true;
            foreach(var target in targets)
                (statuses?.DamageSystem ?? new CombatDamageSystem()).ResolveAndApplyDamage(session,source,target,
                    CombatDamageValueCalculator.CalculateDamageValue(source,DamageValueSource.AttackRange,HeroTalentEffectValues.RunningAttackMultiplier(rank)),DamageType.Physical,77f);
        }
        public static int ResolveThroatCut(CombatSession session,PassiveEffectCombatContext context,int damage,Func<float> roll = null)
        {
            var rank=AssassinRank(context?.Source,"throat_cut");
            if(rank==0 || context.AttackType!=AttackType.Melee || context.Target?.Character==null)return damage;
            var success=(roll?.Invoke() ?? UnityEngine.Random.value) < .01f*(1+rank*3);
            if(success && context.Target.IsBoss)return damage*2;
            var effective=new CombatVariableSystem().ApplyDamageModifiers(context.Source,context.Target,damage);
            if(success && context.Target.Character.CurrentHealth+(context.Target.Character.ShieldPool?.TotalShield??0)<=effective*5)return damage*5;
            var pommel=AssassinRank(context.Source,"pommel_strike");
            if(pommel>0)new StatusEffectSystem().ApplyStatus(context.Source,context.Target,StatusEffectIds.Stun,.15f*pommel,
                AssassinRank(context.Source,"precise_strike")>0?2:1,1,0,0,"scout.pommel_strike");
            return damage;
        }
    }
}

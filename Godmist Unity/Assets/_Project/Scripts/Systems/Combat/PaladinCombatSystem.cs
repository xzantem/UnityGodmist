using System;
using System.Linq;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Core.Constants;
using Godmist.Core.Types;
using Godmist.Data.Skills;
using Godmist.Data.Effects;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using UnityEngine;
namespace Godmist.Systems.Combat
{
    public static partial class PaladinCombatSystem
    {
        public const string Priest="tree.paladin.priest", Crusader="tree.paladin.crusader", FieldId="paladin.judgement_field";
        public static int Rank(CharacterInstance c,string tree,string node)=>Mathf.Clamp(c?.SkillTreeProgress?.FirstOrDefault(p=>p.TreeId==tree)?.NodeInvestments?.FirstOrDefault(n=>n.NodeId=="node.paladin."+(tree==Priest?"priest.":"crusader.")+node)?.Ranks??0,0,3);
        public static int P(BattleUnit u,string n)=>Rank(u?.Character,Priest,n);
        public static int C(BattleUnit u,string n)=>Rank(u?.Character,Crusader,n);
        public static int Points(CharacterInstance c,string tree)=>c?.SkillTreeProgress?.FirstOrDefault(p=>p.TreeId==tree)?.NodeInvestments?.Sum(n=>Math.Max(0,n.Ranks))??0;
        static bool Alive(BattleUnit u)=>u?.Character?.LifeState==CharacterLifeState.Alive;
        static int Distance(GridPosition a,GridPosition b)=>Math.Max(Math.Abs(a.X-b.X),Math.Abs(a.Y-b.Y));
        static float Average(BattleUnit u)=>(u.Character.Stats.MinAttack.GetValue()+u.Character.Stats.MaxAttack.GetValue())*.5f;
        static BattleUnit Creator(CombatSession s,PassiveEffectRuntime e)=>s?.Units.FirstOrDefault(u=>u.Character.CharacterId==e.Sources.FirstOrDefault()?.SourceCharacterId);
        static PassiveEffectRuntime Timed(BattleUnit source,string id,string name,int turns)=>new(id,new(){PassiveEffectSourceFactory.FromRuntimeSystem(source,id)},1,turns,0,PassiveEffectLifetime.Timed,PassiveEffectTickPhase.TurnEnd){DisplayName=name};
        public static int HealCost(BattleUnit source,SkillData skill,BattleUnit target,int cost)
        {
            if(skill?.SkillId!="skill.paladin.heal" || target?.PaladinState.HealingDiscount!=true)return cost;
            var reduction=P(source,"persistent_healer") switch {1=>.25f,2=>.5f,3=>1f,_=>0};
            return Mathf.RoundToInt(cost*(1-reduction));
        }
        public static int PreviewHealCost(CombatSession session,BattleUnit source,SkillData skill,BattleUnit hovered,int cost)
        {
            if(skill?.SkillId!="skill.paladin.heal"||session==null)return cost;
            // Without this passive a target/LOS query cannot change the cost.
            if(P(source,"persistent_healer")==0)return cost;
            if(hovered!=null)return HealCost(source,skill,hovered,cost);
            var targeting=new CombatTargetingService();
            foreach(var target in session.Units)
                if(target.PaladinState.HealingDiscount && targeting.GetTargets(session,source,skill.TargetModes[0],target.Position,true).Contains(target))return HealCost(source,skill,target,cost);
            return cost;
        }
        public static void RecordDamage(BattleUnit target,int amount)
        {
            if(target?.Character?.Stats?.MaxHealth==null || amount<=0)return;
            target.PaladinState.DamageThisTurn+=amount;
            if(target.PaladinState.DamageThisTurn>target.Character.Stats.MaxHealth.GetValue()*.2f)target.PaladinState.HealingDiscount=true;
            AddGrace(target);
        }
        public static void RecordDamageDealt(BattleUnit source,int actualDamage)
        { if(actualDamage>0)AddGrace(source); }
        public static float HealingCritChance(BattleUnit source,bool single)=>Mathf.Clamp01(source.Character.Stats.CritChance.GetValue()*.5f*P(source,"unexpected_grace")*(single?1+.5f*P(source,"critical_focus"):1));
        static void AddGrace(BattleUnit unit)
        {
            foreach(var e in unit?.Character?.PassiveEffects??new())
                if(e.EffectId=="paladin.blessing" && PassiveEffectLifecycleSystem.IsActive(e))
                    e.SetStateValue(PassiveEffectRuntimeStateValue.Integer("grace",Math.Min(6,(e.GetStateValue("grace")?.IntegerValue??0)+1)));
        }
        public static float CalculateHealing(BattleUnit source,SkillData skill,SkillEffectData effect, bool? singleTarget = null,CombatValueExplanation explanation=null)
        {
            explanation?.Add("healing_attack",Average(source));
            explanation?.Add("tree.paladin.priest",1+.01f*Points(source.Character,Priest));
            explanation?.Add("healing_coefficient",effect.DamageMultiplier);
            var amount=Average(source)*(1+.01f*Points(source.Character,Priest))*effect.DamageMultiplier;
            if(skill.SkillId is "skill.paladin.heal" or "skill.paladin.group_heal")
            {
                amount*=1+.15f*P(source,"healing_power");
                if(P(source,"healing_power")>0)explanation?.Add("node.paladin.priest.healing_power",1+.15f*P(source,"healing_power"));
            }
            if(singleTarget ?? skill.SkillId=="skill.paladin.heal")
            {
                amount*=SingleTargetHealingMultiplier(source);
                if(P(source,"healer_focus")>0)explanation?.Add("node.paladin.priest.healer_focus",SingleTargetHealingMultiplier(source));
            }
            else if(P(source,"healer_focus")>0)explanation?.Add("node.paladin.priest.healer_focus","Nie uwzględniono — wiele celów");
            explanation?.Add("healing_power",amount);
            return amount;
        }
        public static float SingleTargetHealingMultiplier(BattleUnit source) => 1 + .15f * P(source,"healer_focus");

        public static bool IsSingleHealingTarget(SkillData skill,
            IReadOnlyDictionary<int,IReadOnlyList<BattleUnit>> targets)
        {
            if (skill == null || targets == null) return skill?.SkillId == "skill.paladin.heal";
            return skill.Effects.Where(e => e != null && e.EffectType is EffectType.PaladinHeal
                    or EffectType.Heal or EffectType.HealPercent or EffectType.HealMissingHealthPercent)
                .Where(e => targets.ContainsKey(e.TargetModeIndex)).SelectMany(e => targets[e.TargetModeIndex])
                .Where(u => u?.Character != null && u.Character.LifeState != CharacterLifeState.Dead)
                .Distinct().Take(2).Count() == 1;
        }

        public static bool IsSingleHealingPreview(CombatSession session,BattleUnit source,BattleUnit target,SkillData skill)
        {
            var modes=skill?.Effects?.Where(e=>e.EffectType is EffectType.PaladinHeal or EffectType.Heal or EffectType.HealPercent or EffectType.HealMissingHealthPercent).Select(e=>e.TargetModeIndex).Distinct().ToArray();
            if(modes==null || modes.Length==0 || skill.TargetModes==null || modes.Any(i=>i<0||i>=skill.TargetModes.Count))return false;
            if(modes.All(i=>skill.TargetModes[i].TargetType==TargetType.Single))return true;
            if(session==null)return false;
            var targeting=new CombatTargetingService();
            return IsSingleHealingTarget(skill,modes.ToDictionary(i=>i,i=>(IReadOnlyList<BattleUnit>)targeting.GetTargets(session,source,skill.TargetModes[i],target?.Position)));
        }

        // Only direct healing from an activated skill. Auras, periodic ticks and
        // secondary returns call recovery without this hook and cannot recurse.
        public static void CompleteDirectSkillHealing(CombatSession session,BattleUnit source,
            BattleUnit target,bool single,int actualHealing)
        {
            if (!single || actualHealing <= 0 || P(source,"encouragement") == 0) return;
            var advance=.2f*CombatTurnSystem.CalculateBaseActionValue(target);
            // Active participants reset AV at turn end; retain this advance in
            // the existing deferred pool so direct self-healing is not lost.
            if(session?.ActiveUnit==target)target.PendingEquipmentActionValueReduction+=advance;
            else target.CurrentActionValue=Mathf.Max(0,target.CurrentActionValue-advance);
            CombatTurnSystem.ReorderParticipants(session);
        }

        public static (int Minimum,int Maximum) HealingPowerRange(BattleUnit source,SkillData skill,SkillEffectData effect,bool? single=null)
        {
            var amount=CalculateHealing(source,skill,effect,single);
            return (Mathf.RoundToInt(amount*(1-HealingVariation)),Mathf.RoundToInt(amount*(1+HealingVariation)));
        }
        public static float LifeStealCoefficient(BattleUnit source,float coefficient,bool single)
            => coefficient*(single?SingleTargetHealingMultiplier(source):1);
        public static float BlessingStrength(BattleUnit source) => .1f+.05f*P(source,"strong_blessing");

        public const float HealingVariation = 1f / 6f;
        public static int CalculateRegenerationUpfront(BattleUnit source,BattleUnit target,bool critical=false)
        {
            var total=RegenerationTotal(source,target,critical);
            var front=P(source,"haste") switch{1=>.15f,2=>.25f,3=>.4f,_=>0};
            return Mathf.FloorToInt(total*front);
        }
        public static bool Execute(CombatSession session,BattleUnit source,BattleUnit target,SkillData skill,SkillEffectData effect,CombatRecoverySystem recovery, bool? singleTarget = null)
        {
            switch(effect.EffectType)
            {
                case EffectType.PaladinHeal:
                    var single=singleTarget ?? skill.SkillId=="skill.paladin.heal";
                    if(skill.SkillId=="skill.paladin.heal")target.PaladinState.HealingDiscount=false;
                    var amount=CalculateHealing(source,skill,effect,single)*UnityEngine.Random.Range(1-HealingVariation,1+HealingVariation);
                    var critical=UnityEngine.Random.value<HealingCritChance(source,single || session.Units.Count(u=>Alive(u)&&u.IsPlayerSide==source.IsPlayerSide)==1);
                    if(critical)amount*=Mathf.Max(1,source.Character.Stats.CritDamage.GetValue());
                    var actualHealing = HealAndShare(source,target,Mathf.RoundToInt(amount),recovery,isCritical:critical);
                    CompleteDirectSkillHealing(session,source,target,single,actualHealing);
                    if(critical && P(source,"anointed_warrior")>0)WarriorCombatSystem.Variable(source,target,GameplayVariableType.DamageDealtPercent,.1f*P(source,"anointed_warrior"),2,"paladin.anointed","Namaszczony Wojownik");
                    if(skill.SkillId=="skill.paladin.group_heal")
                    {
                        var bonus=P(source,"universal_grace") switch{1=>.02f,2=>.05f,3=>.1f,_=>0};
                        if(bonus>0)ApplyStats(source,target,"paladin.universal",2,bonus,0);
                        if(P(source,"battle_zeal")>0)
                        {
                            ScoutCombatSystem.TimedStat(source,target,CharacterStatType.Speed,5,2,"paladin.zeal.base");
                            ScoutCombatSystem.TimedStat(source,target,CharacterStatType.Speed,5,1,"paladin.zeal.extra");
                        }
                    }
                    return true;
                case EffectType.PaladinRegeneration:
                    var regenCritical=UnityEngine.Random.value<HealingCritChance(source,true);
                    if(regenCritical&&P(source,"anointed_warrior")>0)WarriorCombatSystem.Variable(source,target,GameplayVariableType.DamageDealtPercent,.1f*P(source,"anointed_warrior"),2,"paladin.anointed","Namaszczony Wojownik");
                    HealAndShare(source,target,CalculateRegenerationUpfront(source,target,regenCritical),recovery,isCritical:regenCritical);
                    var regen=CreateHolyRegeneration(source,target,regenCritical);
                    new PassiveEffectLifecycleSystem().TryApplyTimedEffect(target.Character,regen,PassiveEffectStackingMode.ReplaceDurationAndValues,out _);
                    var applied=target.Character.PassiveEffects.First(e=>e.EffectId==regen.EffectId);
                    foreach(var state in regen.StateValues.ToArray())applied.SetStateValue(state);
                    applied.RemoveStateValue("tail");
                    return true;
                case EffectType.PaladinBlessing:
                    var blessing=Timed(source,"paladin.blessing","Błogosławieństwo",4);
                    var strength=BlessingStrength(source);
                    blessing.SetPayload(new VariableModifiersPayload{Modifiers=new(){new GameplayVariableModifier{VariableType=GameplayVariableType.DamageDealtPercent,Value=strength},new GameplayVariableModifier{VariableType=GameplayVariableType.DamageReceivedPercent,Value=-strength}}});
                    new PassiveEffectLifecycleSystem().TryApplyTimedEffect(target.Character,blessing,PassiveEffectStackingMode.ReplaceDurationAndValues,out var appliedBlessing);
                    appliedBlessing.SetStateValue(PassiveEffectRuntimeStateValue.Integer("grace",0));
                    return true;
            }
            return false;
        }
        static int HealAndShare(BattleUnit source,BattleUnit target,int amount,CombatRecoverySystem recovery,
            CombatTargetVfxKind? targetVfx = null, bool isCritical = false)
        {
            var actual=recovery.Heal(target,amount,targetVfx:targetVfx,isCritical:isCritical);
            recovery.Heal(source,HealingShareAmount(source,actual),targetVfx:targetVfx);
            return actual;
        }
        static void ApplyStats(BattleUnit source,BattleUnit target,string id,int turns,float defense,int dodge)
        {
            var e=Timed(source,id,id,turns);
            e.StatModifiers=new(){new(){StatType=CharacterStatType.PhysicalDefense,Operation=StatModifierOperation.Percent,Value=defense},new(){StatType=CharacterStatType.MagicalDefense,Operation=StatModifierOperation.Percent,Value=defense},new(){StatType=CharacterStatType.Dodge,Operation=dodge==0?StatModifierOperation.Percent:StatModifierOperation.FlatAbsolute,Value=dodge==0?defense:dodge}};
            new PassiveEffectLifecycleSystem().TryApplyTimedEffect(target.Character,e,PassiveEffectStackingMode.ReplaceDurationAndValues,out _);
        }
        public static void TurnStart(CombatSession session,BattleUnit unit,CombatRecoverySystem recovery)
        {
            unit.PaladinState.DamageThisTurn=0;
            var rank=P(unit,"healing_aura");if(rank==0)return;
            var reached=new HashSet<BattleUnit>{unit};var queue=new Queue<GridPosition>();queue.Enqueue(unit.Position);
            var anchors=new HashSet<GridPosition>();
            while(queue.Count>0)
            {
                var at=queue.Dequeue();
                foreach(var ally in session.Units)
                    if(Alive(ally)&&ally.IsPlayerSide==unit.IsPlayerSide&&!reached.Contains(ally)&&Distance(at,ally.Position)<=1)
                    {reached.Add(ally);if(P(unit,"healing_chain")>0)queue.Enqueue(ally.Position);}
                if(P(unit,"healing_chain")>0 && P(unit,"holy_artifacts")>0)
                    foreach(var obj in session.SummonedObjects)
                        if(session.Units.Any(u=>u.Character.CharacterId==obj.CreatorCharacterId&&u.IsPlayerSide==unit.IsPlayerSide)&&Distance(at,obj.Position)<=1&&anchors.Add(obj.Position))queue.Enqueue(obj.Position);
            }
            foreach(var ally in reached.Where(a=>a!=unit))HealAndShare(unit,ally,HeroTalentEffectValues.HealingAura(unit,rank),recovery);
        }
        public static void BeforeTurnEnd(CombatSession session,BattleUnit unit,CombatRecoverySystem recovery)
        {
            foreach(var e in unit.Character.PassiveEffects.ToArray())
            {
                if(e.EffectId=="paladin.holy_regeneration" && e.RemainingDuration>0)
                {
                    var amount=RegenerationTickAmount(e);
                    var critical=e.GetStateValue("critical")?.BooleanValue==true;
                    var healer=Creator(session,e);if(healer!=null)HealAndShare(healer,unit,amount,recovery,CombatTargetVfxKind.Heal,critical);else recovery.Heal(unit,amount,targetVfx:CombatTargetVfxKind.Heal,isCritical:critical);
                    CompleteRegenerationTick(e,amount);
                }
                if(e.EffectId=="paladin.blessing" && e.RemainingDuration==1)
                {
                    var source=Creator(session,e);var rank=P(source,"grace_collection");var amount=4*rank*(e.GetStateValue("grace")?.IntegerValue??0);
                    var restored=recovery.RestoreResource(unit,amount);
                    if(P(source,"salvation")>0)recovery.RestoreResource(source,Mathf.FloorToInt(restored*.5f));
                }
            }
        }
    }
}

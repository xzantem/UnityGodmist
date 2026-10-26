using System;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Constants;
using Godmist.Core.Types;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using UnityEngine;
namespace Godmist.Systems.Combat
{
    public static partial class PaladinCombatSystem
    {
        static bool Tagged(BattleUnit target,CharacterTag tag)=>target?.Character?.Definition?.Tags?.Contains(tag)==true;
        public static float DamageMultiplier(PassiveEffectCombatContext context)
        {
            var source=context?.Source;var target=context?.Target;if(source==null||target==null)return 1;
            float bonus=0;
            if(Tagged(target,CharacterTag.Undead)||Tagged(target,CharacterTag.Demon))bonus+=.01f*Points(source.Character,Crusader);
            if(Tagged(target,CharacterTag.Undead))bonus+=.15f*C(source,"gravedigger");
            if(Tagged(target,CharacterTag.Demon))bonus+=.15f*C(source,"exorcist");
            if(C(source,"advantage")>0){var hp=target.Character.Stats.MaxHealth.GetValue();if(hp>0)bonus+=Mathf.Clamp((source.Character.Stats.MaxHealth.GetValue()/hp-1)*.5f,0,.5f);}
            if(StatusEffectSystem.HasStatus(target,StatusEffectIds.Stun))bonus+=C(source,"kicking_downed") switch{1=>.15f,2=>.3f,3=>.5f,_=>0};
            return 1+bonus;
        }
        public static float CriticalMultiplier(BattleUnit source,BattleUnit target)=>target!=null&&source?.Character.CurrentHealth>target.Character.CurrentHealth?1+.5f*C(source,"control"):1;
        public static float ConsumeExposure(BattleUnit target,bool directAttack)
        {
            if(!directAttack)return 1;
            var effect=target.Character.PassiveEffects.FirstOrDefault(e=>e.EffectId=="paladin.exposure");if(effect==null)return 1;
            target.Character.PassiveEffects.Remove(effect);
            return 1+(float)(effect.GetStateValue("strength")?.NumberValue??0);
        }
        public static void AfterAttack(CombatSession session,BattleUnit source,BattleUnit target,PassiveEffectCombatContext context,int rawDamage,float penetration,int healthDamage,CombatDamageSystem damage)
        {
            if(source==null||target==null)return;
            var stacks=target.Character.PassiveEffects.FirstOrDefault(e=>e.EffectId=="paladin.torment")?.Stacks??0;
            var rank=C(source,"torment_damage");
            if(stacks>=4 && rank>0 && healthDamage>0 && Alive(target))
            {
                var type=context.DamageType??DamageType.Physical;
                var current=type==DamageType.Physical?target.Character.Stats.PhysicalDefense.GetValue():type==DamageType.Magical?target.Character.Stats.MagicalDefense.GetValue():0;
                var baseDefense=current/Mathf.Max(.01f,1-.02f*stacks);
                var statPen=type==DamageType.Physical?source.Character.Stats.PhysicalDefensePenetration.GetValue():source.Character.Stats.MagicalDefensePenetration.GetValue();
                var after=CombatDamageSystem.ApplyDefense(source,target,rawDamage,type,penetration);
                var before=Mathf.CeilToInt((float)rawDamage*rawDamage/(rawDamage+Mathf.Max(0,baseDefense)/(1+Mathf.Max(0,statPen)+penetration)));
                var extra=Mathf.RoundToInt(Mathf.Max(0,after-before)*(rank==1?.3f:rank==2?.6f:1f));
                damage.ApplyDamage(session,source,target,extra,DamageType.Inevitable,allowWarriorProcs:false);
            }
            if(!Alive(target))return;
            if(C(source,"torment")>0)
            {
                stacks=Math.Min(4+2*C(source,"torment_max"),stacks+1);
                ApplyStats(source,target,"paladin.torment",9999,-.02f*stacks,-2*stacks);
                var debuff=target.Character.PassiveEffects.First(e=>e.EffectId=="paladin.torment");debuff.Stacks=stacks;debuff.Lifetime=PassiveEffectLifetime.CombatScoped;debuff.TickPhase=PassiveEffectTickPhase.None;
            }
            if(C(source,"hatred")>0&&(Tagged(target,CharacterTag.Undead)||Tagged(target,CharacterTag.Demon)))new StatusEffectSystem().ApplyStatus(source,target,StatusEffectIds.Taunt,.5f,1,1,0,0,"paladin.hatred");
            if(context.SkillId=="skill.paladin.blinding_light")
            {
                if(C(source,"expose")>0)
                {var exposure=Timed(source,"paladin.exposure","Naświetlenie",3);exposure.SetStateValue(PassiveEffectRuntimeStateValue.Number("strength",.25f*C(source,"expose")));new PassiveEffectLifecycleSystem().RemoveEffect(target.Character,exposure.EffectId);new PassiveEffectLifecycleSystem().TryApplyTimedEffect(target.Character,exposure,PassiveEffectStackingMode.ReplaceDurationAndValues,out _);}
                if(C(source,"blinding")>0)
                {var targets=context.TargetsByModeIndex?.Values.FirstOrDefault();var index=targets?.ToList().IndexOf(target)??0;new StatusEffectSystem().ApplyStatus(source,target,StatusEffectIds.Stun,Mathf.Max(0,.9f-.2f*Math.Max(0,index)),3,1,0,0,"paladin.blinding");}
            }
            if(context.SkillId=="skill.paladin.verdict")
            {
                var chance=.2f*C(source,"harsh_verdict")+(context.IsCritical==true&&C(source,"severe_judge")>0?.4f:0);
                if(new StatusEffectSystem().PassesMovementEffectChance(target,chance))PushAway(session,source,target,1+(context.IsCritical==true?C(source,"severe_judge"):0));
            }
        }
        static void PushAway(CombatSession session,BattleUnit source,BattleUnit target,int distance)
        {
            for(var i=0;i<distance;i++)
            {
                var dx=target.Position.X-source.Position.X;var dy=target.Position.Y-source.Position.Y;
                var next=new GridPosition(target.Position.X+(Math.Abs(dx)>=Math.Abs(dy)?Math.Sign(dx):0),target.Position.Y+(Math.Abs(dy)>Math.Abs(dx)?Math.Sign(dy):0),target.Position.Level);
                if(next==target.Position||!session.Grid.CanMoveBetween(target.Position,next)||session.IsPositionOccupied(next,target)||session.IsPositionOccupiedByMapObject(next))break;
                target.Position=next;CombatCellEntrySystem.Notify(session, target);
            }
        }
        public static bool IsField(SummonedCombatMapObject field)=>field?.Definition?.ObjectId==FieldId;
        public static bool FieldContains(CombatSession session,SummonedCombatMapObject field,GridPosition position)
        {
            var source=session.Units.FirstOrDefault(u=>u.Character.CharacterId==field.CreatorCharacterId);
            return CombatTargetingService.IsPositionWithinRelativeRange(field.Position,field.Position,position,new SkillRangeData(RangeShape.Square,1,2+C(source,"field_range")))
                &&new CombatLineOfSightService().HasLineOfSight(session,field.Position,position);
        }
        public static void FieldEffects(CombatSession session,SummonedCombatMapObject field,bool tick,CombatDamageSystem damage,StatusEffectSystem statuses,
            CombatFeedbackBus feedback = null)
        {
            var source=session.Units.FirstOrDefault(u=>u.Character.CharacterId==field.CreatorCharacterId);if(source==null)return;
            var radius=2+C(source,"field_range");
            foreach(var target in session.Units.ToArray())
            {
                var id=field.ObjectId+".paladin.slow";
                var within=Alive(target)&&target.IsPlayerSide!=source.IsPlayerSide&&FieldContains(session,field,target.Position);
                if(!within){new PassiveEffectLifecycleSystem().RemoveEffect(target.Character,id);continue;}
                ScoutCombatSystem.TimedStat(source,target,CharacterStatType.Speed,-5,9999,id);
                if(!tick)continue;
                var value=CombatDamageValueCalculator.CalculateDamageValue(source,DamageValueSource.AttackRange,.5f);
                var contact = target.Position;
                damage.ApplyDamage(session,source,target,CombatDamageSystem.ApplyDefense(source,target,value,DamageType.Magical),DamageType.Magical,allowWarriorProcs:false);
                feedback?.Publish(new CombatFeedbackEvent(target, CombatFeedbackType.TargetEffect,
                    position: contact, source: source, targetVfx: CombatTargetVfxKind.HolyImpact));
                if(field.RemainingDurationTurns==2&&C(source,"exhaustion")>0)statuses.ApplyStatus(source,target,StatusEffectIds.Stun,1,C(source,"exhaustion"),1,0,0,"paladin.exhaustion");
                if(field.RemainingDurationTurns==1&&C(source,"judgement_release")>0)
                {var burst=CombatDamageValueCalculator.CalculateDamageValue(source,DamageValueSource.AttackRange,.5f*C(source,"judgement_release"));damage.ApplyDamage(session,source,target,CombatDamageSystem.ApplyDefense(source,target,burst,DamageType.Magical),DamageType.Magical,allowWarriorProcs:false);}
            }
        }
        public static void RemoveFieldEffects(CombatSession session,SummonedCombatMapObject field)
        {foreach(var unit in session.Units)new PassiveEffectLifecycleSystem().RemoveEffect(unit.Character,field.ObjectId+".paladin.slow");}
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Characters;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public static partial class WarriorCombatSystem
    {
        public const string ManAtArms = "tree.warrior.man_at_arms";
        public const string DefensiveStance = "skill.warrior.man_at_arms.t1_2";
        public const string PommelStrike = "skill.warrior.man_at_arms.t2_3";
        public const string Prediction = "skill.warrior.man_at_arms.t4_1";
        public const string EnergyRenewal = "skill.warrior.man_at_arms.t5_3";
        const string OverflowShield = "warrior.man_at_arms.overflow";
        public static int M(BattleUnit u,string slot) => Rank(u?.Character,ManAtArms,slot);

        public static void ApplyArmoredStats(CharacterInstance c,Action<CharacterStatType,string,StatModifierOperation,float> observe)
        {
            var points=Points(c,ManAtArms);if(c?.Stats==null||points==0)return;
            var s=c.Stats;
            float R(string id)=>Rank(c,ManAtArms,id);
            float Defense(float rank)=>rank==1?.15f:rank==2?.25f:rank>=3?.4f:0;
            var hp=15*points;var vitality=.1f*R("t4_3");
            s.MaxHealth.SetEffectiveValue((s.MaxHealth.GetValue()+hp)*(1+vitality));
            var phys=Defense(R("t1_1"));var magic=Defense(R("t1_5"));
            s.PhysicalDefense.SetEffectiveValue(s.PhysicalDefense.GetValue()*(1+phys));
            s.MagicalDefense.SetEffectiveValue(s.MagicalDefense.GetValue()*(1+magic));
            s.Dodge.SetEffectiveValue(s.Dodge.GetValue()+8*R("t3_1"));
            observe?.Invoke(CharacterStatType.MaxHealth,ManAtArms,StatModifierOperation.FlatAbsolute,hp);
            observe?.Invoke(CharacterStatType.MaxHealth,"node.warrior.man_at_arms.t4_3",StatModifierOperation.Percent,vitality);
            observe?.Invoke(CharacterStatType.PhysicalDefense,"node.warrior.man_at_arms.t1_1",StatModifierOperation.Percent,phys);
            observe?.Invoke(CharacterStatType.MagicalDefense,"node.warrior.man_at_arms.t1_5",StatModifierOperation.Percent,magic);
            observe?.Invoke(CharacterStatType.Dodge,"node.warrior.man_at_arms.t3_1",StatModifierOperation.FlatAbsolute,8*R("t3_1"));
        }

        public static void InitializeArmored(BattleUnit u)
        {
            if(u?.Character==null||u.WarriorState.ArmoredInitialized)return;
            u.WarriorState.ArmoredInitialized=true;
            if(M(u,"t5_1")>0)new CombatShieldSystem().GrantShield(u,
                Mathf.FloorToInt(u.Character.MaximumHealth*.1f*M(u,"t5_1")),"warrior.man_at_arms.vigor",5,true);
            if(M(u,"t3_4")>0&&u.Character.ManAtArmsDungeonKills>0)
                ArmoredBuff(u,"warrior.man_at_arms.acquired","Odporność Nabyta",0);
        }

        public static int ArmoredShield(BattleUnit u)=>Mathf.FloorToInt(
            (u?.Character?.MaximumHealth??0)*.2f*(1+.1f*M(u,"t1_3")));
        public static void ArmoredBuff(BattleUnit u,string id,string label,int turns)
        {
            var effect=new PassiveEffectRuntime(id,new(){PassiveEffectSourceFactory.FromRuntimeSystem(u,id)},1,turns,0,
                turns>0?PassiveEffectLifetime.Timed:PassiveEffectLifetime.CombatScoped,
                turns>0?PassiveEffectTickPhase.TurnEnd:PassiveEffectTickPhase.None){DisplayName=label};
            var lifecycle=new PassiveEffectLifecycleSystem();
            if(turns>0)lifecycle.TryApplyTimedEffect(u.Character,effect,PassiveEffectStackingMode.ReplaceDurationAndValues,out _);
            else lifecycle.TryApplyCombatScopedEffect(u.Character,effect,PassiveEffectStackingMode.ReplaceDurationAndValues,out _);
        }
        public static bool IsArmoredActive(string id)=>id is DefensiveStance or PommelStrike or Prediction or EnergyRenewal;
        public static bool ExecuteArmored(BattleUnit caster,BattleUnit target,SkillData skill,StatusEffectSystem statuses,CombatShieldSystem shields=null)
        {
            if(caster?.Character==null||target?.Character==null)return false;
            switch(skill.SkillId)
            {
                case DefensiveStance:
                    ArmoredBuff(caster,DefensiveStance,"Postawa Defensywna",5);
                    return (shields??new CombatShieldSystem()).GrantShield(caster,ArmoredShield(caster),DefensiveStance,5,true)>0;
                case Prediction:ArmoredBuff(caster,Prediction,"Przewidywanie",5);return true;
                case EnergyRenewal:
                    var sleep=new PassiveEffectRuntime(StatusEffectIds.Sleep,
                        new(){PassiveEffectSourceFactory.FromRuntimeSystem(caster,EnergyRenewal)},1,2,0,
                        PassiveEffectLifetime.Timed,PassiveEffectTickPhase.TurnEnd,.2f){DisplayName="Odnowienie Energii"};
                    var applied=new PassiveEffectLifecycleSystem().TryApplyTimedEffect(caster.Character,sleep,PassiveEffectStackingMode.ReplaceDurationAndValues,out _);
                    if(applied)caster.WarriorState.RenewalActive=true;
                    return applied;
                case PommelStrike:
                    if(UnityEngine.Random.value<.9f)ArmoredResistance(caster,target,ResistanceType.Suppression,-.5f,"suppression");
                    var elemental=M(caster,"t3_3");
                    if(elemental>0)foreach(ResistanceType type in Enum.GetValues(typeof(ResistanceType)))
                        if(type!=ResistanceType.Suppression&&type!=ResistanceType.All)
                            ArmoredResistance(caster,target,type,-.08f*elemental*ModifyResistanceSystem.GetEffectiveResistance(target,type),type.ToString());
                    var stunned=(statuses??new StatusEffectSystem()).ApplyStatus(caster,target,StatusEffectIds.Stun,.7f,3,1,0,0,PommelStrike);
                    if(!stunned&&M(caster,"t2_4")>0)
                    {
                        target.WarriorState.DisorientationCap=M(caster,"t2_4")==1?.4f:.2f;
                        target.WarriorState.DisorientationUntil=target.WarriorState.ArmoredTurn+1;
                        ArmoredBuff(target,"warrior.man_at_arms.disorientation","Dezorientacja",
                            target.WarriorState.Session?.ActiveUnit==target?2:1);
                    }
                    return true;
            }
            return false;
        }
        static void ArmoredResistance(BattleUnit source,BattleUnit target,ResistanceType type,float value,string suffix)
        {
            var effect=new PassiveEffectRuntime("warrior.man_at_arms.resistance."+suffix,
                new(){PassiveEffectSourceFactory.FromRuntimeSystem(source,PommelStrike)},1,3,0,
                PassiveEffectLifetime.Timed,PassiveEffectTickPhase.TurnEnd){DisplayName="Cios Rękojeścią"};
            effect.SetPayload(new ResistanceModifierPayload{ResistanceType=type,Value=value});
            new PassiveEffectLifecycleSystem().TryApplyTimedEffect(target.Character,effect,PassiveEffectStackingMode.ReplaceDurationAndValues,out _);
        }
        public static float PredictionDodge(BattleUnit attacker,BattleUnit target)=>StatusEffectSystem.HasStatus(target,Prediction)
            ?5*Distance(attacker,target):0;
        static int Distance(BattleUnit a,BattleUnit b)=>a==null||b==null?0:
            Math.Abs(a.Position.X-b.Position.X)+Math.Abs(a.Position.Y-b.Position.Y);
        public static float ArmoredHitChance(BattleUnit source,float chance)=>source?.WarriorState.DisorientationCap>0
            &&StatusEffectSystem.HasStatus(source,"warrior.man_at_arms.disorientation")?Mathf.Min(chance,source.WarriorState.DisorientationCap):chance;

        public static int ArmoredIncoming(BattleUnit source,BattleUnit target,int amount,DamageType type,string skillId,float? roll=null)
        {
            if(target?.Character==null)return amount;
            var w=target.WarriorState;
            var reduction=M(target,"t3_4")>0?.02f*Mathf.Clamp(target.Character.ManAtArmsDungeonKills,0,25):0;
            if(w.AdaptedSkills.Contains(skillId??""))reduction+=.15f*M(target,"t3_5");
            if(StatusEffectSystem.HasStatus(target,Prediction)&&type==DamageType.Magical)
                reduction+=(M(target,"t4_2")==1?.02f:M(target,"t4_2")>=2?.03f:0)*Distance(source,target);
            if(StatusEffectSystem.HasStatus(target,StatusEffectIds.Sleep)&&w.RenewalActive)
                reduction+=.25f*M(target,"t5_2");
            amount=Mathf.Max(0,Mathf.RoundToInt(amount*(1-Mathf.Clamp01(reduction))));
            if(type==DamageType.Physical)
            {
                amount=Mathf.Max(0,amount-Mathf.RoundToInt(.5f*M(target,"t3_2")*(target.Character.Equipment?.Armor?.RequiredLevel??0)));
            }
            return ArmoredBlock(target,amount,type,roll);
        }
        public static int ArmoredBlock(BattleUnit target,int amount,DamageType type,float? roll=null)
        {
            if(type!=DamageType.Physical)return amount;
            var chance=.1f*M(target,"t2_1");
            if(StatusEffectSystem.HasStatus(target,DefensiveStance))chance+=.1f*M(target,"t2_2");
            return chance>0&&(roll??UnityEngine.Random.value)<chance?Mathf.RoundToInt(amount*.3f):amount;
        }
        public static bool ArmoredAvoidMagic(BattleUnit target,DamageType type,float? roll=null)=>
            type==DamageType.Magical&&M(target,"t2_5")>0&&(roll??UnityEngine.Random.value)<.1f*M(target,"t2_5");

        public static void RecordArmoredMitigation(BattleUnit target,CombatDamageSystem.DamageResolution result)
        {
            var points=Points(target?.Character,ManAtArms);
            if(points>0&&target.Character.LifeState==CharacterLifeState.Alive)
                target.WarriorState.MitigatedBank+=result.Mitigated*.02*points;
        }

        public sealed class ArmoredAttackScope:IDisposable
        {
            readonly BattleUnit unit;readonly double previous;
            public ArmoredAttackScope(BattleUnit u,SkillData skill,bool duplicate)
            {
                unit=u;previous=u.WarriorState.AttackBank;
                u.WarriorState.AttackBank=0;
                if(!duplicate&&PassiveEffectCombatExecutionSystem.IsDamagingOffensiveSkill(skill))
                {
                    if(Points(u.Character,ManAtArms)>0)u.WarriorState.AttackBank=u.WarriorState.MitigatedBank;
                    u.WarriorState.MitigatedBank=0;
                }
            }
            public void Dispose(){unit.WarriorState.AttackBank=previous;}
        }
        public static int ConsumeArmoredAttack(BattleUnit u)
        {
            var bank=u.WarriorState.AttackBank;u.WarriorState.AttackBank=0;
            return (int)Math.Min(int.MaxValue,Math.Floor(Math.Max(0,bank)));
        }
        public static int PreviewArmoredAttack(BattleUnit u)=>Points(u?.Character,ManAtArms)>0
            ?(int)Math.Min(int.MaxValue,Math.Floor(Math.Max(0,u.WarriorState.MitigatedBank))):0;

        public static void ArmoredDamageResolved(CombatSession session,BattleUnit source,BattleUnit target,
            int lostHealth,DamageType type,PassiveEffectCombatContext context,bool reactive)
        {
            if(lostHealth<=0)return;
            var w=target.WarriorState;
            if(!string.IsNullOrEmpty(context?.SkillId))w.PendingAdaptedSkills.Add(context.SkillId);
            if(w.RenewalActive)
            {
                new StatusEffectSystem().RemoveStatus(target,StatusEffectIds.Sleep);
                w.RenewalActive=false;
            }
            if(source==null||source==target||source.IsPlayerSide==target.IsPlayerSide||reactive)return;
            var rank=M(source,"t4_5");
            if(rank>0&&source.Character.LifeState==CharacterLifeState.Alive)
            {
                var multiplier=1f;
                if(source.Character.CurrentHealth<source.Character.MaximumHealth*.5f)multiplier+=.5f*M(source,"t5_5");
                source.WarriorState.VampirismRemainder+=lostHealth*.05*rank*multiplier;
                var heal=(int)Math.Min(int.MaxValue,Math.Floor(source.WarriorState.VampirismRemainder));
                source.WarriorState.VampirismRemainder-=heal;
                var result=new CombatRecoverySystem().HealWithResult(source,heal,reportingSourceId:"warrior.man_at_arms.vampirism");
                if(result.Overflow>0&&M(source,"t4_4")>0)
                {
                    var pool=source.Character.ShieldPool??=new ShieldPool();
                    var previous=pool.Layers.Where(l=>l.SourceId==OverflowShield).Sum(l=>l.CurrentValue);
                    new CombatShieldSystem().GrantShield(source,Math.Min(Mathf.FloorToInt(source.Character.MaximumHealth*.1f),previous+result.Overflow),OverflowShield,5,true);
                }
            }
            if(target.Character.CurrentHealth<=0&&M(source,"t3_4")>0&&(session?.DungeonContext!=null||session?.DungeonDefinition!=null))
            {
                source.Character.ManAtArmsDungeonKills=Math.Min(25,source.Character.ManAtArmsDungeonKills+1);
                ArmoredBuff(source,"warrior.man_at_arms.acquired","Odporność Nabyta",0);
            }
        }
        public static bool TryArmoredRevive(BattleUnit target,float? roll=null,CombatRecoverySystem recovery=null)
        {
            if(target?.Character==null||target.Character.LifeState!=CharacterLifeState.Alive||target.Character.CurrentHealth>0)return false;
            if(M(target,"t5_4")==0||target.WarriorState.ReviveCooldown>0||(roll??UnityEngine.Random.value)>=.2f)return false;
            target.WarriorState.ReviveCooldown=5;
            ArmoredBuff(target,"warrior.man_at_arms.revive_cooldown","Nieugiętość — odnowienie",0);
            (recovery??new CombatRecoverySystem()).Heal(target,Math.Max(1,Mathf.CeilToInt(target.Character.MaximumHealth*.25f)),reportingSourceId:"node.warrior.man_at_arms.t5_4");
            return true;
        }
        public static void ArmoredTurnStart(BattleUnit u)
        {
            if(u==null)return;InitializeArmored(u);var w=u.WarriorState;
            w.ArmoredTurn++;w.AdaptedSkills.Clear();w.AdaptedSkills.UnionWith(w.PendingAdaptedSkills);w.PendingAdaptedSkills.Clear();
        }
        public static void ArmoredTurnEnd(BattleUnit u)
        {
            if(u==null)return;var w=u.WarriorState;
            if(w.ReviveCooldown>0&&--w.ReviveCooldown==0)
                new PassiveEffectLifecycleSystem().RemoveEffect(u.Character,"warrior.man_at_arms.revive_cooldown");
            if(w.ArmoredTurn>=w.DisorientationUntil)w.DisorientationCap=0;
            if(!StatusEffectSystem.HasStatus(u,StatusEffectIds.Sleep))w.RenewalActive=false;
        }
    }
}

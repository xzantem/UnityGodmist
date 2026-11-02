using System;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Constants;
using Godmist.Core.Types;
using Godmist.Data.Skills;
using Godmist.Data.Effects;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Characters;
using UnityEngine;
namespace Godmist.Systems.Combat
{
    public static partial class MageCombatSystem
    {
        public const string Frost="tree.mage.frost", Icicle="skill.mage.frost.t1_4", FrostAura="skill.mage.frost.t3_4", IceField="skill.mage.frost.t5_4";
        public const string FrostAuraMarker="mage.frost.aura", EmergencyShield="mage.frost.emergency", BrokenIce="mage.frost.broken";
        public static int I(BattleUnit u,string node)=>Rank(u?.Character,Frost,node);
        static bool Living(BattleUnit u)=>u?.Character?.LifeState==CharacterLifeState.Alive;
        static int StatusTurns(BattleUnit u,string id)=>u?.Character?.PassiveEffects?.FirstOrDefault(e=>e.EffectId==id&&PassiveEffectLifecycleSystem.IsActive(e))?.RemainingDuration??0;
        public static void BindFrostSession(CombatSession session)
        {
            if(session==null)return;
            if(!session.FrostPlanning)foreach(var unit in session.Units)unit.MageState.Frost.Session=session;
            if(session.FrostCallbacksBound)return;
            session.FrostCallbacksBound=true;session.UnitEnteredCell+=_=>RefreshFrostFields(session);
        }
        public static float FrozenCriticalChance(BattleUnit source,BattleUnit target,bool? wasFrozen=null)
            =>(wasFrozen??StatusEffectSystem.IsFrozen(target)) ? .015f*Points(source?.Character,Frost) : 0;
        public static float FrostHealingMultiplier(BattleUnit target,int turnsAhead=0)
        {
            if(StatusTurns(target,StatusEffectIds.Chill)<=turnsAhead&&StatusTurns(target,StatusEffectIds.Freeze)<=turnsAhead)return 1;
            var session=target?.MageState.Frost.Session;
            var rank=session?.Units.Where(u=>Living(u)&&u.IsPlayerSide!=target.IsPlayerSide).Select(u=>I(u,"t1_2")).DefaultIfEmpty(0).Max()??0;
            return session?.Outcome==CombatOutcomeState.Ongoing ? (rank>=2?.25f:rank==1?.65f:1) : 1;
        }
        public static SkillEffectData FrostEffect(BattleUnit source,SkillData skill,SkillEffectData effect)
        {
            if(skill?.SkillId==Icicle)
            {
                if(effect.EffectType==EffectType.DealDamage)effect=effect.WithDamageMultiplier(I(source,"t2_3") switch{1=>1.35f,2=>1.45f,>=3=>1.6f,_=>1.25f});
                if(effect.StatusId==StatusEffectIds.Freeze&&I(source,"t2_4")>0)effect=effect.WithRuntimeParameters(effect.DamageHitCount,3,effect.BaseChance);
            }
            if(skill?.SkillId==FrostAura)effect=effect.WithRuntimeParameters(effect.DamageHitCount,I(source,"t3_3")>0?5:3,effect.BaseChance);
            return effect;
        }
        public static SkillTargetModeData FrostMode(BattleUnit source,SkillData skill,SkillTargetModeData mode)
            =>skill?.SkillId==IceField?mode.WithRuntimeRanges(I(source,"t5_5") switch{1=>9,>=2=>11,_=>8},mode.RelativeRange.MaxRange):mode;
        public static MageFrostContact FrostContact(BattleUnit source,BattleUnit target,PassiveEffectCombatContext context)
        {
            var frozen=StatusTurns(target,StatusEffectIds.Freeze);var stun=StatusTurns(target,StatusEffectIds.Stun);
            var eligible=source!=null&&target!=null&&source.IsPlayerSide!=target.IsPlayerSide&&context?.Skill!=null
                &&!context.IsPassiveDuplicate&&!context.IsPassiveReaction&&source.GuardianState.ReactionDepth==0
                &&context.DamageType is not (DamageType.Bleed or DamageType.Poison or DamageType.Burn);
            return new MageFrostContact(frozen,stun,target?.Character?.CurrentHealth??0,eligible&&Points(source?.Character,Frost)>0,context?.ActionId);
        }
        static bool FrostAttempt(BattleUnit source,BattleUnit target,string action,string kind)
        {
            var key=action+"|"+kind;
            if(!source.MageState.Frost.Contacts.TryGetValue(key,out var targets))source.MageState.Frost.Contacts[key]=targets=new();
            return targets.Add(target.Character.CharacterId);
        }
        public static void CompleteFrostAction(BattleUnit source,string action)
        {
            if(source==null)return;
            foreach(var key in source.MageState.Frost.Contacts.Keys.Where(k=>k.StartsWith(action+"|",StringComparison.Ordinal)).ToArray())source.MageState.Frost.Contacts.Remove(key);
        }
        public readonly struct FrostActionScope:IDisposable
        {
            readonly CombatSession session;readonly BattleUnit source;readonly string action;
            public FrostActionScope(CombatSession session,BattleUnit source,string action){this.session=session;this.source=source;this.action=action;}
            public void Dispose()
            {
                if(source==null)return;
                if(session!=null)foreach(var pending in session.ScheduledEffects)
                    if(pending.ActionId==action&&pending.CasterCharacterId==source.Character.CharacterId)return;
                CompleteFrostAction(source,action);
            }
        }
        public static float ConsumeFrostFreeze(CombatSession session,BattleUnit attacker,BattleUnit target,int turns)
        {
            if(turns<=0)return 1;
            var chance=I(attacker,"t2_5") switch{1=>.1f,>=2=>.2f,_=>0};
            if(I(attacker,"t3_5")>0)chance=.3f;
            if(session!=null&&session.Units.Any(u=>Living(u)&&u.IsPlayerSide==attacker.IsPlayerSide&&I(u,"t3_5")>0&&Distance(u.Position,attacker.Position)<=3))chance=.3f;
            if(chance>0&&UnityEngine.Random.value<chance)
            {
                var effect=target.Character.PassiveEffects.First(e=>e.EffectId==StatusEffectIds.Freeze&&PassiveEffectLifecycleSystem.IsActive(e));
                if(--effect.RemainingDuration<=0)new StatusEffectSystem().RemoveStatus(target,StatusEffectIds.Freeze);
            }
            else StatusEffectSystem.TryBreakFreeze(target,out _);
            return 1+turns*Godmist.Data.Configuration.GameDefinesRuntime.GetFloat(Godmist.Data.Configuration.GameDefineIds.FreezeDamagePerRemainingTurn,.5f);
        }
        public static int FrostExecution(BattleUnit source,BattleUnit target,MageFrostContact contact,int damage,float? roll=null)
        {
            var rank=I(source,"t4_3");
            if(!contact.CanProc||contact.FrozenTurns<=0||rank==0||target.IsBoss&&contact.Health*5L>=target.Character.MaximumHealth)return damage;
            if(!FrostAttempt(source,target,contact.ActionId,"execution"))return damage;
            var chance=.03f*rank+.02f*I(source,"t5_3")*contact.FrozenTurns;
            if((roll??UnityEngine.Random.value)>=Mathf.Clamp01(chance))return damage;
            // Use the normal damage/death owner, retaining boss phase and immortality rules.
            return (int)Math.Max(damage,Math.Min(int.MaxValue/4,((long)target.Character.CurrentHealth+target.Character.ShieldPool.TotalShield+1)*1000));
        }
        public static void FrostAfterContact(CombatSession session,BattleUnit source,BattleUnit target,PassiveEffectCombatContext context,MageFrostContact contact,StatusEffectSystem statuses)
        {
            if(!contact.CanProc||!Living(target))return;
            var rank=I(source,"t1_1");
            if(rank>0&&FrostAttempt(source,target,contact.ActionId,"force")&&UnityEngine.Random.value<(rank==1?.1f:rank==2?.15f:.2f))
            {
                if(I(source,"t2_2")>0&&UnityEngine.Random.value<.5f)statuses.ApplyStatus(source,target,StatusEffectIds.Chill,1,2,1,0,0,"mage.frost.force");
                else statuses.ApplyStatus(source,target,StatusEffectIds.Slow,1,2,1,0,0,"mage.frost.force",flatSlow:I(source,"t2_1")>0?15:10);
            }
            rank=I(source,"t1_5");
            if(rank>0&&contact.StunTurns>0&&FrostAttempt(source,target,contact.ActionId,"conversion")&&statuses.ApplyStatus(source,target,StatusEffectIds.Freeze,rank==1?.5f:1f,contact.StunTurns,1,0,0,"mage.frost.conversion"))
                statuses.RemoveStatus(target,StatusEffectIds.Stun);
            rank=I(source,"t1_3");
            if(context.Skill?.SkillId==Icicle&&rank>0&&FrostAttempt(source,target,contact.ActionId,"bleed"))statuses.ApplyStatus(source,target,StatusEffectIds.Bleed,.5f,2,1,rank==1?.4f:rank==2?.6f:.9f,0,"mage.frost.icicle.bleed");
        }
        public static void FrostStats(CharacterInstance c,Action<CharacterStatType,string,StatModifierOperation,float> observe=null)
        {
            if(Rank(c,Frost,"t3_1")==0||c.ShieldPool?.Layers.Any(l=>l.SourceId==EmergencyShield&&l.CurrentValue>0)!=true)return;
            var bonus=.3f+.1f*Rank(c,Frost,"t4_1");
            c.Stats.PhysicalDefense.SetEffectiveValue(c.Stats.PhysicalDefense.GetValue()*(1+bonus));
            c.Stats.MagicalDefense.SetEffectiveValue(c.Stats.MagicalDefense.GetValue()*(1+bonus));
            observe?.Invoke(CharacterStatType.PhysicalDefense,EmergencyShield,StatModifierOperation.Percent,bonus);
            observe?.Invoke(CharacterStatType.MagicalDefense,EmergencyShield,StatModifierOperation.Percent,bonus);
        }
        public static void FrostShieldResolved(BattleUnit target,int healthLost)
        {
            if(target?.Character?.ShieldPool==null)return;var state=target.MageState.Frost;
            var shield=target.Character.ShieldPool.Layers.Any(l=>l.SourceId==EmergencyShield&&l.CurrentValue>0);
            if(state.HadEmergencyShield&&!shield&&Living(target))
            {
                var penalty=new PassiveEffectRuntime(BrokenIce,new(){PassiveEffectSourceFactory.FromRuntimeSystem(target,BrokenIce)},1,3,0,PassiveEffectLifetime.Timed,PassiveEffectTickPhase.TurnEnd)
                    {DisplayName="Rozbita Soplowa Tarcza",StatModifiers=new(){new StatModifier{StatType=CharacterStatType.PhysicalDefense,Operation=StatModifierOperation.Percent,Value=-.3f},new StatModifier{StatType=CharacterStatType.MagicalDefense,Operation=StatModifierOperation.Percent,Value=-.3f}}};
                new PassiveEffectLifecycleSystem().TryApplyTimedEffect(target.Character,penalty,PassiveEffectStackingMode.ReplaceDurationAndValues,out _);
                new PassiveEffectLifecycleSystem().RemoveEffect(target.Character,EmergencyShield);
            }
            state.HadEmergencyShield=shield;state.HealthLost+=Math.Max(0,healthLost);
            if(!Living(target)||target.Character.CurrentHealth<=0||I(target,"t3_1")==0||state.ShieldCooldown>0
                ||state.HealthLost*100d<target.Character.MaximumHealth*(I(target,"t3_2")>0?15:25))return;
            var amount=Mathf.CeilToInt(target.Character.MaximumHealth*.3f);
            new CombatShieldSystem().GrantShield(target,amount,EmergencyShield,replaceSameSource:true);
            state.HadEmergencyShield=true;state.ShieldCooldown=10-I(target,"t4_2");
            ScoutCombatSystem.Marker(target,EmergencyShield,"Soplowa Tarcza Awaryjna");
            ScoutCombatSystem.Marker(target,"mage.frost.shield_cooldown","Soplowa Tarcza — odnowienie");
            new CharacterStatPipelineSystem().Refresh(target.Character);
        }
        public static int Distance(GridPosition a,GridPosition b)=>Math.Max(Math.Abs(a.X-b.X),Math.Abs(a.Y-b.Y));
    }
}

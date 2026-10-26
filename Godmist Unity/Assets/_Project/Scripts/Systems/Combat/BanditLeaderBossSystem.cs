using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using UnityEngine;
namespace Godmist.Systems.Combat
{
    public sealed class BanditLeaderBossSystem : IDisposable
    {
        public const string BossId="enemy.forest.bandit_leader", PhaseTwoId=BossId+".revealed";
        public const string Prefix="skill.enemy.forest.bandit_leader_";
        public const string BearId=Prefix+"bear", MineId=Prefix+"mine", OrderId=Prefix+"order";
        public const string CutId=Prefix+"cut", FireId=Prefix+"fire", StrikeId=Prefix+"strike";
        public const string ConcealCue=Prefix+"conceal";
        readonly CombatSession session;
        readonly CombatRecoverySystem recovery;
        readonly SummonedObjectSystem objects;
        readonly SummonedObjectEffectExecutor traps;
        readonly CombatFeedbackBus feedback;
        readonly Action<string> log;
        readonly Func<BattleUnit,SkillData,bool> previousGate;
        public BanditLeaderBossSystem(CombatSession session,CombatDamageSystem damage,StatusEffectSystem status,
            CombatRecoverySystem recovery,SummonedObjectSystem objects,Action<string> log=null,CombatFeedbackBus feedback=null)
        {
            this.session=session;this.recovery=recovery;this.objects=objects;this.log=log;this.feedback=feedback;
            traps=new SummonedObjectEffectExecutor(log,damage,statusEffectSystem:status,feedbackBus:feedback);
            Discover();previousGate=session.CanUseBossSkill;session.CanUseBossSkill=CanUseSkill;
            foreach(var boss in session.Units.Where(u=>u?.BanditLeaderState!=null))boss.BanditLeaderState.Session=session;
            session.SkillsFinished+=CheckPhases;session.DamageApplied+=OnDamage;
        }
        static bool Alive(BattleUnit unit)=>unit?.Character?.LifeState==CharacterLifeState.Alive && unit.Character.CurrentHealth>0;
        void Discover()
        {
            foreach(var boss in session.Units.Where(u=>u?.Character?.Definition?.Id==BossId && u.BanditLeaderState==null))
            {
                var s=boss.BanditLeaderState=new BanditLeaderCombatState {OriginalDefinition=boss.Character.Definition,Session=session};
                boss.IsBoss=true;
                session.EnemySources.TryGetValue(boss.Character.CharacterId,out var origin);
                foreach(var ally in session.Units.Where(u=>u!=boss && Alive(u) && u.IsPlayerSide==boss.IsPlayerSide))
                    if(string.IsNullOrEmpty(origin.EncounterId) || session.EnemySources.TryGetValue(ally.Character.CharacterId,out var other) && other.EncounterId==origin.EncounterId)
                        s.Guards.Add(ally);
            }
        }
        bool CanUseSkill(BattleUnit unit,SkillData skill)
        {
            if(previousGate?.Invoke(unit,skill)==false)return false;
            var s=unit?.BanditLeaderState;
            if(s==null)return true;
            if(skill?.SkillId==OrderId)return !s.PhaseTwo && s.Guards.Any(ally=>CanOrderTarget(unit,ally));
            return skill?.SkillId!=StrikeId || s.PhaseTwo && StatusEffectSystem.IsInvisible(unit);
        }
        // Phase-I traps have a second opportunity: the existing phase owner
        // detonates all of them when the guards die, even without an entry.
        public static bool HasPendingTrapDetonation(BattleUnit boss) => Alive(boss) && boss.BanditLeaderState is {PhaseTwo:false};
        public static bool PermanentlyInvisible(BattleUnit unit)=>Alive(unit)
            && (unit.BanditLeaderState is {PhaseTwo:false} || unit.Character.Definition?.Id==BossId);
        public static bool CanOrderTarget(BattleUnit boss,BattleUnit ally)=>Alive(ally) && ally!=boss
            && boss?.BanditLeaderState?.Guards.Contains(ally)==true
            && !ally.Character.PassiveEffects.Any(e=>PassiveEffectLifecycleSystem.IsActive(e)
                && e.Sources?.Any(source=>source.SourceId.StartsWith(OrderId+".",StringComparison.Ordinal))==true);
        public static float StrikeMultiplier(CombatSession session,BattleUnit target,SkillData skill)
        {
            if(skill?.SkillId!=StrikeId || target==null)return 1;
            var count=session?.Units.Count(u=>Alive(u) && u!=target && u.IsPlayerSide==target.IsPlayerSide
                && u.Position.Level==target.Position.Level && Math.Abs(u.Position.X-target.Position.X)+Math.Abs(u.Position.Y-target.Position.Y)==1) ?? 0;
            return 1-Math.Min(3,count)*.3f;
        }
        public static void RecordHealthDamage(BattleUnit target,int amount)
        {
            if(target?.BanditLeaderState is {PhaseTwo:true} s && amount>0)
                s.DamageSinceTurn=(int)Math.Min(int.MaxValue,(long)s.DamageSinceTurn+amount);
        }
        void OnDamage(BattleUnit source,BattleUnit target,int amount,DamageType type,string action)
        {if(session.SkillResolutionDepth==0)CheckPhases();}
        public void CheckPhases()
        {
            Discover();if(session.SkillResolutionDepth>0)return;
            foreach(var boss in session.Units.Where(u=>u?.BanditLeaderState!=null).ToArray())
            {
                if(!Alive(boss)){OnBossDied(boss);continue;}
                var s=boss.BanditLeaderState;
                if(!s.PhaseTwo && !s.Guards.Any(Alive))s.PhasePending=true;
            }
        }
        public void BeginTurn(BattleUnit boss)
        {
            CheckPhases();var s=boss?.BanditLeaderState;if(s==null || !Alive(boss))return;
            var transitioning=s.PhasePending && !s.PhaseTwo;
            if(s.PhasePending && !s.PhaseTwo && s.OriginalDefinition.SecondPhaseDefinition!=null)
            {
                s.PhasePending=false;s.PhaseTwo=true;s.DamageSinceTurn=0;
                boss.Character.Definition=s.OriginalDefinition.SecondPhaseDefinition;
                boss.Character.Skills=new List<SkillData>(boss.Character.Definition.StartingSkills);
                new Godmist.Systems.Characters.SkillLoadoutSystem().TryInitialize(boss.Character);
                foreach(var trap in OwnedTraps(boss))
                {traps.ExecuteDetonation(session,trap);objects.Remove(session,trap);}
                log?.Invoke("Przywódca Bandytów — Nieugięty Dowódca!");
                feedback?.Publish(new CombatFeedbackEvent(boss,CombatFeedbackType.ProcTriggered,effectName:"Nieugięty Dowódca"));
            }
            if(!s.PhaseTwo)return;
            if(s.DamageSinceTurn<Mathf.CeilToInt(boss.Character.Stats.MaxHealth.GetValue()*.2f))
            {
                recovery.Heal(boss,Mathf.RoundToInt(boss.Character.Stats.MaxHealth.GetValue()*.1f));
                if(!StatusEffectSystem.HasStatus(boss,StatusEffectIds.Invisibility))
                {
                    // Brief cue at the last visible position, never a persistent locator.
                    if(!transitioning)feedback?.Publish(new CombatFeedbackEvent(boss,CombatFeedbackType.VisualCue,effectName:ConcealCue));
                    boss.Character.PassiveEffects.Add(new PassiveEffectRuntime {EffectId=StatusEffectIds.Invisibility,
                        DisplayName="Niewidzialność",Sources=new(){PassiveEffectSourceFactory.FromRuntimeSystem(boss,BossId+".passive")}});
                }
            }
            s.DamageSinceTurn=0;
        }
        SummonedCombatMapObject[] OwnedTraps(BattleUnit boss)=>session.SummonedObjects.Where(t=>t.CreatorCharacterId==boss.Character.CharacterId
            && (t.SourceSkillId==BearId || t.SourceSkillId==MineId)).ToArray();
        public void OnBossDied(BattleUnit boss)
        {if(boss?.BanditLeaderState==null)return;boss.BanditLeaderState.PhasePending=false;foreach(var trap in OwnedTraps(boss))objects.Remove(session,trap);}
        public void Dispose()=>Dispose(false);
        public void Dispose(bool preserve)
        {
            session.CanUseBossSkill=previousGate;session.SkillsFinished-=CheckPhases;session.DamageApplied-=OnDamage;
            if(preserve)return;
            foreach(var boss in session.Units.Where(u=>u?.BanditLeaderState!=null))
            {
                OnBossDied(boss);var original=boss.BanditLeaderState.OriginalDefinition;
                boss.Character.PassiveEffects.RemoveAll(e=>e.Sources?.Any(source=>source.SourceId==BossId+".passive")==true);
                boss.Character.Definition=original;boss.Character.Skills=new List<SkillData>(original.StartingSkills);
                new Godmist.Systems.Characters.SkillLoadoutSystem().TryInitialize(boss.Character);boss.BanditLeaderState=null;
            }
        }
    }
}

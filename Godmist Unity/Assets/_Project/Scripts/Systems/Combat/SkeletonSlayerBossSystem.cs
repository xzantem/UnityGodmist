using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    /// <summary>Workbook mechanics for the skeletal executioner; one owner per combat.</summary>
    public sealed class SkeletonSlayerBossSystem : IDisposable
    {
        public const string BossId = "enemy.catacombs.skeleton_slayer";
        public const string PhaseTwoId = BossId + ".enraged";
        public const string SkillPrefix = "skill.enemy.catacombs.skeleton_slayer_";
        public const string CrushingId = SkillPrefix + "crushing";
        public const string TremorsId = SkillPrefix + "tremors";
        public const string DefenseId = SkillPrefix + "defense";
        public const string BoneCrushId = SkillPrefix + "bone_crush";
        public const string HammerId = SkillPrefix + "hammer";
        public const string TaskId = SkillPrefix + "task";
        public const string ArmorStatusId = "catacombs.skeleton_slayer.armor";
        public const string RevengeStatusId = "catacombs.skeleton_slayer.revenge";
        readonly CombatSession session;
        readonly CombatDamageSystem damage;
        readonly StatusEffectSystem status;
        readonly Action<CombatMapObject> changed;
        readonly Action<string> log;
        readonly CombatFeedbackBus feedback;
        readonly Func<BattleUnit, SkillData, bool> previousGate;

        public SkeletonSlayerBossSystem(CombatSession session, CombatDamageSystem damage, StatusEffectSystem status,
            Action<CombatMapObject> changed = null, Action<string> log = null, CombatFeedbackBus feedback = null)
        {
            this.session=session;this.damage=damage;this.status=status;this.changed=changed;this.log=log;this.feedback=feedback;
            Discover();previousGate=session.CanUseBossSkill;session.CanUseBossSkill=CanUseSkill;
            session.SkillsFinished+=CheckPhases;session.SkillResolved+=OnSkill;session.DamageApplied+=OnDamage;
        }
        static bool Alive(BattleUnit unit) => unit?.Character?.LifeState==CharacterLifeState.Alive && unit.Character.CurrentHealth>0;
        void Discover()
        {
            foreach(var unit in session.Units)
                if(unit?.Character?.Definition?.Id==BossId && unit.Character.Definition.SecondPhaseDefinition!=null)
                    unit.SkeletonSlayerState ??= new SkeletonSlayerCombatState {OriginalDefinition=unit.Character.Definition};
        }
        public void CheckPhases()
        {
            Discover();if(session.SkillResolutionDepth>0)return;
            foreach(var boss in session.Units.Where(u=>u?.SkeletonSlayerState!=null).ToArray())
            {
                var state=boss.SkeletonSlayerState;
                if(!Alive(boss)){OnBossDied(boss);continue;}
                if(!state.PhaseTwo && boss.Character.CurrentHealth<=boss.Character.Stats.MaxHealth.GetValue()*.5f)
                    state.PhaseTwoPending=true;
            }
        }
        bool CanUseSkill(BattleUnit unit, SkillData skill)
        {
            if(previousGate?.Invoke(unit,skill)==false)return false;
            var s=unit?.SkeletonSlayerState;
            if(s==null)return true;
            if(skill?.SkillId==DefenseId)return !s.PhaseTwo && s.DefenseTurns==0 && s.NextAttackBonus==0;
            if(skill?.SkillId==TaskId)return s.PhaseTwo && !s.TaskUsedThisTurn && !s.AccelerateNextTurn;
            return skill?.SkillId!=HammerId || s.PhaseTwo && s.HammerTurns==0;
        }
        public static bool BlocksMovement(BattleUnit unit)=>unit?.SkeletonSlayerState?.HammerTurns>0;
        public static float IncomingMultiplier(BattleUnit target,DamageType type)
        {
            var s=target?.SkeletonSlayerState;if(s==null)return 1f;
            return (!s.PhaseTwo && type==DamageType.Physical?.65f:1f)*(s.DefenseTurns>0?.5f:1f);
        }
        // Called by the damage owner AFTER hit/prevention checks, with pre-defense damage.
        public static void RecordPreMitigation(BattleUnit target,int amount)
        {
            var s=target?.SkeletonSlayerState;
            if(s?.DefenseTurns>0 && amount>0)s.DefenseDamage=(int)Math.Min(int.MaxValue,(long)s.DefenseDamage+amount);
        }
        public static void BeforeSkill(BattleUnit caster,SkillData skill,IReadOnlyDictionary<int,IReadOnlyList<BattleUnit>> targets)
        {
            var s=caster?.SkeletonSlayerState;
            if(s==null)return;
            if(skill.SkillId==HammerId)
                s.SelectedHammerCenter=targets!=null && targets.TryGetValue(0,out var selected) && selected.Count>0 ? selected[0].Position : null;
            if(!skill.Effects.Any(e=>e.EffectType==EffectType.DealDamage))return;
            s.ActiveAttackBonus=s.NextAttackBonus;s.NextAttackBonus=0;
        }
        public static int AttackBonus(BattleUnit caster)=>caster?.SkeletonSlayerState?.ActiveAttackBonus??0;
        public static float RefillMultiplier(BattleUnit unit)=>unit?.SkeletonSlayerState?.PhaseTwo==true
            ?1f+.03f*Math.Min(20,unit.SkeletonSlayerState.RevengeHits):1f;
        public static float ApplyTurnDelay(BattleUnit unit,float delay,bool consume)
        {
            if(unit?.SkeletonSlayerState?.AccelerateNextTurn!=true)return delay;
            if(consume)
            {
                unit.SkeletonSlayerState.AccelerateNextTurn=false;
            }
            return 0f;
        }
        void OnDamage(BattleUnit source,BattleUnit target,int amount,DamageType type,string action)
        {
            var s=target?.SkeletonSlayerState;
            if(s!=null && amount>0)
            {
                if(s.PhaseTwo)s.RevengeHits=Math.Min(20,s.RevengeHits+1);
                if(s.HammerTurns>0)
                {
                    s.HammerDamage=(int)Math.Min(int.MaxValue,(long)s.HammerDamage+amount);
                    // Damage is integral. Materialize the threshold once, avoiding
                    // a floating-point comparison just above an exact 10% boundary.
                    var interruptAt=Mathf.CeilToInt(target.Character.Stats.MaxHealth.GetValue()*.1f);
                    if(s.HammerDamage>=interruptAt)
                    {ClearHammer(s);log?.Invoke("Przygotowanie Młota Zniszczenia przerwane.");}
                }
            }
            if(session.SkillResolutionDepth==0)CheckPhases();
        }
        void OnSkill(BattleUnit caster,SkillData skill,string action)
        {
            var s=caster?.SkeletonSlayerState;if(s==null)return;
            s.ActiveAttackBonus=0;
            if(!Alive(caster))return;
            if(skill.SkillId==DefenseId){s.DefenseTurns=2;s.DefenseDamage=0;CombatBossVfxFeedback.PublishState(feedback,caster,skill.SkillId,action);}
            if(skill.SkillId==TaskId){s.TaskUsedThisTurn=true;s.AccelerateNextTurn=true;CombatBossVfxFeedback.PublishState(feedback,caster,skill.SkillId,action);}
            if(skill.SkillId!=HammerId)return;
            // Freeze the chosen cell, not a fresh nearest-target lookup after effects.
            if(s.SelectedHammerCenter.HasValue)
            {StartHammer(caster,s.SelectedHammerCenter.Value);CombatBossVfxFeedback.PublishState(feedback,caster,skill.SkillId,action);}
            s.SelectedHammerCenter=null;
        }
        public void StartHammer(BattleUnit caster,GridPosition center)
        {
            var s=caster.SkeletonSlayerState;ClearHammer(s);s.HammerTurns=2;s.HammerCenter=center;s.HammerDamage=0;
            for(var dx=-1;dx<=1;dx++)for(var dy=-1;dy<=1;dy++)
            {
                var cell=session.Grid.GetOffsetPosition(center,dx,dy);
                if(!session.Grid.IsWalkable(cell)||session.Grid.IsBlocked(cell)||session.IsPositionOccupiedByMapObject(cell))continue;
                s.HammerFields.Add(cell);var definition=caster.Character.Definition.SanctuaryWarning;if(definition==null)continue;
                var marker=new SummonedCombatMapObject("slayer.hammer."+Guid.NewGuid().ToString("N"),cell,caster.Character.CharacterId,HammerId,0,definition);
                s.Markers.Add(marker);session.SummonedObjects.Add(marker);session.MapObjects.Add(marker);changed?.Invoke(marker);
            }
            log?.Invoke("Młot Zniszczenia — Pole Impetu, uderzenie za dwie tury bossa.");
        }
        public void BeginTurn(BattleUnit boss)
        {
            CheckPhases();var s=boss?.SkeletonSlayerState;if(s==null || !Alive(boss))return;
            s.TaskUsedThisTurn=false;
            if(s.PhaseTwoPending && !s.PhaseTwo)
            {
                s.PhaseTwoPending=false;s.PhaseTwo=true;s.DefenseTurns=s.DefenseDamage=s.NextAttackBonus=s.ActiveAttackBonus=0;
                ClearHammer(s);
                boss.Character.Definition=s.OriginalDefinition.SecondPhaseDefinition;
                boss.Character.Skills=new List<SkillData>(boss.Character.Definition.StartingSkills);
                new Godmist.Systems.Characters.SkillLoadoutSystem().TryInitialize(boss.Character);
                log?.Invoke("Szkielet Zabójca — Zemsta Zabójcy!");
                feedback?.Publish(new CombatFeedbackEvent(boss,CombatFeedbackType.ProcTriggered,effectName:"Zemsta Zabójcy"));
            }
            if(s.DefenseTurns>0 && --s.DefenseTurns==0)
            {
                if(s.DefenseDamage==0)status.ApplyGuaranteedStun(boss,boss,null,-1,2);
                else s.NextAttackBonus=s.DefenseDamage;
                s.DefenseDamage=0;
            }
            if(s.HammerTurns<=0 || --s.HammerTurns>0)return;
            var fields=new HashSet<GridPosition>(s.HammerFields);ClearHammer(s);
            var presentation=Guid.NewGuid().ToString("N");
            feedback?.Publish(new CombatFeedbackEvent(boss,CombatFeedbackType.ProcTriggered,effectName:"Uderzenie Młota Zniszczenia",actionId:presentation));
            foreach(var target in session.Units.Where(u=>Alive(u) && u.IsPlayerSide!=boss.IsPlayerSide && fields.Contains(u.Position)).ToArray())
                for(var hit=0;hit<5 && Alive(target) && Alive(boss);hit++)
                {
                    var impactPosition=target.Position;
                    var result=damage.ResolveAndApplyDamage(session,boss,target,CombatDamageValueCalculator.CalculateDamageValue(boss,DamageValueSource.AttackRange,1f),DamageType.Physical,85f,ignoreDefense:true);
                    CombatBossVfxFeedback.PublishContact(feedback,boss,target,result,CombatTargetVfxKind.Blunt,hit,presentation,impactPosition);
                }
            CombatBossVfxFeedback.PublishResolutionArea(feedback,boss,fields,CombatTargetVfxKind.Tremor,presentation);
        }
        void ClearHammer(SkeletonSlayerCombatState s)
        {
            s.HammerTurns=0;s.HammerDamage=0;s.HammerFields.Clear();
            foreach(var marker in s.Markers){session.SummonedObjects.Remove(marker);session.MapObjects.Remove(marker);changed?.Invoke(marker);}
            s.Markers.Clear();
        }
        public void OnBossDied(BattleUnit boss)
        {
            var s=boss?.SkeletonSlayerState;if(s==null)return;
            s.PhaseTwoPending=false;s.DefenseTurns=s.NextAttackBonus=s.ActiveAttackBonus=0;s.AccelerateNextTurn=false;ClearHammer(s);
        }
        public void Dispose()=>Dispose(false);
        public void Dispose(bool preserve)
        {
            session.CanUseBossSkill=previousGate;session.SkillsFinished-=CheckPhases;session.SkillResolved-=OnSkill;session.DamageApplied-=OnDamage;
            if(preserve)return;
            foreach(var boss in session.Units.Where(u=>u?.SkeletonSlayerState!=null))
            {
                var s=boss.SkeletonSlayerState;ClearHammer(s);boss.Character.Definition=s.OriginalDefinition;
                boss.Character.Skills=new List<SkillData>(s.OriginalDefinition.StartingSkills);
                new Godmist.Systems.Characters.SkillLoadoutSystem().TryInitialize(boss.Character);boss.SkeletonSlayerState=null;
            }
        }
    }
}

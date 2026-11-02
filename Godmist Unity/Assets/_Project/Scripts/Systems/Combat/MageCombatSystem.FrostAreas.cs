using System;
using System.Linq;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Core.Constants;
using Godmist.Data.Skills;
using Godmist.Data.Effects;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Characters;
using UnityEngine;
namespace Godmist.Systems.Combat
{
    public static partial class MageCombatSystem
    {
        public static bool FrostAreaContains(CombatSession session,MageFrostArea area,GridPosition position)
            =>area.Turns>0&&Distance(area.Center,position)<=1&&new CombatLineOfSightService().HasLineOfSight(session,area.Center,position);
        public static IEnumerable<GridPosition> FrostAreaCells(CombatSession session,MageFrostArea area)
        {
            for(int x=Math.Max(0,area.Center.X-1);x<=Math.Min(session.Grid.Width-1,area.Center.X+1);x++)
                for(int y=Math.Max(0,area.Center.Y-1);y<=Math.Min(session.Grid.Height-1,area.Center.Y+1);y++)
                    foreach(var p in session.Grid.GetPositionsAt(x,y))if(session.Grid.IsWalkable(p)&&FrostAreaContains(session,area,p))yield return p;
        }
        public static bool CreateIceField(CombatSession session,BattleUnit source,GridPosition? center)
        {
            if(session?.Grid==null||!center.HasValue||!Living(source)||!session.Grid.IsWalkable(center.Value))return false;
            BindFrostSession(session);
            source.MageState.Frost.Areas.Add(new MageFrostArea{Center=center.Value,Turns=6});
            RefreshFrostFields(session);return true;
        }
        public static bool ApplyFrostAura(CombatSession session,BattleUnit source,BattleUnit target,int turns,StatusEffectSystem statuses)
        {
            if(!Living(target))return false;BindFrostSession(session);
            if(!ScoutCombatSystem.TimedStat(source,target,CharacterStatType.Dodge,20,turns,FrostAuraMarker,"Aura Mrozu",FrostAura))return false;
            target.MageState.Frost.AuraSourceId=source.Character.CharacterId;
            PulseFrostAura(session,target,statuses);return true;
        }
        public static void PulseFrostAura(CombatSession session,BattleUnit recipient,StatusEffectSystem statuses)
        {
            if(!Living(recipient)||!StatusEffectSystem.HasStatus(recipient,FrostAuraMarker))return;
            var sourceId=recipient.MageState.Frost.AuraSourceId??recipient.Character.PassiveEffects.FirstOrDefault(e=>e.EffectId==FrostAuraMarker)?.Sources.FirstOrDefault()?.SourceCharacterId;
            var source=session?.Units.FirstOrDefault(u=>u.Character.CharacterId==sourceId);
            if(source==null)return;
            foreach(var enemy in session.Units.Where(u=>Living(u)&&u.IsPlayerSide!=recipient.IsPlayerSide&&Distance(u.Position,recipient.Position)<=1).ToArray())
            {
                var chilled=StatusEffectSystem.HasStatus(enemy,StatusEffectIds.Chill);
                if(chilled&&I(source,"t4_4")>0)
                    statuses.ApplyStatus(source,enemy,StatusEffectIds.Freeze,.2f*I(source,"t4_4"),1+I(source,"t4_5"),1,0,0,"mage.frost.aura.freeze");
                else statuses.ApplyStatus(source,enemy,StatusEffectIds.Chill,.4f,1,1,0,0,"mage.frost.aura.chill");
            }
        }
        public static void FrostEnemyDied(CombatSession session,BattleUnit target)
        {
            if(session==null||target?.Character==null)return;BindFrostSession(session);
            foreach(var source in session.Units.Where(u=>Living(u)&&u.IsPlayerSide!=target.IsPlayerSide&&I(u,"t5_1")>0).ToArray())
            {
                if(!source.MageState.Frost.ObservedDeaths.Add(target.Character.CharacterId))continue;
                source.MageState.Frost.Areas.Add(new MageFrostArea{Center=target.Position,Turns=4,Snowstorm=true,DamageRank=I(source,"t5_1"),
                    Attack=CombatDamageValueCalculator.CalculateDamageValue(source,DamageValueSource.AverageAttack,1),MovementPenalty=I(source,"t5_2")>0});
            }
            RefreshFrostFields(session);
        }
        public static void AdvanceFrostShieldClock(BattleUnit unit)
        {
            if(unit==null)return;
            unit.MageState.Frost.HealthLost=0;
            unit.MageState.Frost.ShieldCooldown=Math.Max(0,unit.MageState.Frost.ShieldCooldown-1);
            if(unit.MageState.Frost.ShieldCooldown==0)new PassiveEffectLifecycleSystem().RemoveEffect(unit.Character,"mage.frost.shield_cooldown");
        }
        public static void FrostTurnStart(CombatSession session,BattleUnit unit,StatusEffectSystem statuses)
        {
            if(unit==null)return;BindFrostSession(session);
            AdvanceFrostShieldClock(unit);
            PulseFrostAura(session,unit,statuses);RefreshFrostFields(session);
        }
        public static void FrostTurnEnd(CombatSession session,BattleUnit unit,CombatDamageSystem damage)
        {
            if(session==null||unit==null)return;
            foreach(var source in session.Units.Where(u=>u.Character?.LifeState!=CharacterLifeState.Dead&&u.IsPlayerSide!=unit.IsPlayerSide).ToArray())
            {
                var active=source.MageState.Frost.Areas.Where(a=>a.Snowstorm&&FrostAreaContains(session,a,unit.Position)).ToArray();
                if(active.Length==0||!Living(unit))continue;
                var amount=active.Max(a=>Mathf.FloorToInt(a.Attack*(a.DamageRank==1?.2f:.3f)
                    +(unit.Character.MaximumHealth-unit.Character.CurrentHealth)*(a.DamageRank==1?.03f:.06f)));
                var defended=CombatDamageSystem.ApplyDefense(source,unit,amount,DamageType.Magical);
                damage.ApplyDamage(session,source,unit,defended,DamageType.Magical,allowWarriorProcs:false);
            }
            foreach(var area in unit.MageState.Frost.Areas)area.Turns--;
            unit.MageState.Frost.Areas.RemoveAll(a=>a.Turns<=0);
            RefreshFrostFields(session);
        }
        static bool EnemyArea(CombatSession session,BattleUnit target,bool snow,GridPosition position,bool penalty=false)
            =>session?.Units.Any(source=>source.Character?.LifeState!=CharacterLifeState.Dead&&source.IsPlayerSide!=target.IsPlayerSide
                &&source.MageState.Frost.Areas.Any(a=>a.Snowstorm==snow&&(!penalty||a.MovementPenalty)&&FrostAreaContains(session,a,position)))==true;
        public static int FrostMovementSurcharge(CombatSession session,BattleUnit target,GridPosition from,GridPosition to)
            =>target!=null&&(EnemyArea(session,target,true,from,true)||EnemyArea(session,target,true,to,true))?1:0;
        public static float FrostAccuracyPenalty(BattleUnit target)
            =>target!=null&&EnemyArea(target.MageState.Frost.Session,target,false,target.Position)?30:0;
        public static void RefreshFrostFields(CombatSession session)
        {
            if(session==null)return;
            foreach(var source in session.Units)if(source.Character?.LifeState==CharacterLifeState.Dead&&(!session.FrostPlanning||source.MageState.Frost.Session==session))source.MageState.Frost.Areas.Clear();
            foreach(var target in session.Units)
            {
                if(!Living(target)||session.FrostPlanning&&target.MageState.Frost.Session!=session)continue;
                UpdateSlow("mage.frost.snow.slow",EnemyArea(session,target,true,target.Position,true)?-10:0);
                UpdateSlow("mage.frost.ice.slow",EnemyArea(session,target,false,target.Position)?-15:0);
                void UpdateSlow(string id,int value)
                {
                    var current=target.Character.PassiveEffects.FirstOrDefault(e=>e.EffectId==id);
                    if(value==0){if(current!=null)new PassiveEffectLifecycleSystem().RemoveEffect(target.Character,id);return;}
                    if(current==null)
                    {
                        ScoutCombatSystem.TimedStat(target,target,CharacterStatType.Speed,value,9999,id,value==-15?"Lodowisko":"Wieczna Śnieżyca");
                        current=target.Character.PassiveEffects.FirstOrDefault(e=>e.EffectId==id);
                        if(current!=null){current.Lifetime=PassiveEffectLifetime.CombatScoped;current.RemainingDuration=0;}
                    }
                }
            }
        }
    }
}

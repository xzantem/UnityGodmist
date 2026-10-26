using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;

namespace Godmist.Tests.EditMode
{
    public partial class EnemyBattleSimulationTests
    {
        const int JointDefaultLoadout=3;
        const int JointAdvancedLoadout=2;
        static bool JointHasReplacementTools(CharacterInstance hero,int profile)
        {
            var known=hero.GetKnownSkills().Select(s=>s.SkillId).ToHashSet();
            return hero.ClassData.Id switch
            {
                "class.warrior" when profile==0=>known.Contains("skill.warrior.whirlwind"),
                "class.scout" when profile==1=>known.Contains("skill.scout.weak_point"),
                "class.mage" when profile==0=>MageCombatSystem.Rank(hero,MageCombatSystem.Warlock,"mass_sacrifice")>0,
                "class.paladin" when profile==1=>known.Contains("skill.paladin.group_heal"),
                _=>false
            };
        }
        static void SetJointLoadout(CharacterInstance hero,int profile,int variant)
        {
            if(variant==3)variant=JointHasReplacementTools(hero,profile)?JointAdvancedLoadout:0;
            if(variant==0){SetBuildLoadout(hero,profile);return;}
            var known=hero.GetKnownSkills().Select(s=>s.SkillId).ToHashSet();
            string[] preferred=hero.ClassData.Id switch
            {
                "class.warrior" when profile==0=>variant==1
                    ?new[]{"skill.warrior.destruction","skill.warrior.whirlwind","skill.warrior.shout","skill.warrior.protection","skill.warrior.regroup","skill.warrior.immobilize"}
                    :new[]{"skill.warrior.destruction","skill.warrior.protection","skill.warrior.regroup","skill.warrior.whirlwind","skill.warrior.shout","skill.warrior.immobilize"},
                "class.scout" when profile==1&&variant==1=>new[]{"skill.scout.poisoned_dagger","skill.scout.quick_strikes","skill.scout.weak_point","skill.scout.hook","skill.scout.ricochet","skill.scout.tonic"},
                "class.mage" when profile==0=>variant==1
                    ?new[]{"skill.mage.shield","skill.mage.fireball","skill.mage.energy_split","skill.mage.sacrifice","skill.mage.offering","skill.mage.magic_focus","skill.mage.ritual"}
                    :new[]{"skill.mage.shield","skill.mage.fireball","skill.mage.sacrifice","skill.mage.ritual","skill.mage.energy_split","skill.mage.offering","skill.mage.magic_focus"},
                "class.paladin" when profile==1=>variant==1
                    ?new[]{"skill.paladin.heal","skill.paladin.group_heal","skill.paladin.judgement_field","skill.paladin.holy_regeneration","skill.paladin.blinding_light","skill.paladin.grounding_wave"}
                    :new[]{"skill.paladin.heal","skill.paladin.holy_regeneration","skill.paladin.group_heal","skill.paladin.blinding_light","skill.paladin.judgement_field","skill.paladin.grounding_wave"},
                _=>Array.Empty<string>()
            };
            var ids=new[]{hero.GetBasicAttackSkill().SkillId}.Concat(preferred).Concat(BuildLoadout(hero,profile))
                .Where(known.Contains).Distinct().Take(hero.ActiveSkillLoadout.Capacity).ToArray();
            AssertBuildLoadout(hero,ids);
        }

        sealed partial class Engine
        {
            internal bool UsePreviousPlanningPolicy;
            bool JointPolicy=>BuildAware&&!UsePreviousPlanningPolicy;
            bool JointSetupMove(BattleUnit actor,IReadOnlyList<GridPosition> path)
            {
                var moved=movement.TryMoveAlongPath(Session,actor,path);
                if(moved&&JointPolicy)playerRepositioned=true;
                return moved;
            }
            HashSet<BattleUnit> JointLethalTargets(BattleUnit actor,PlayerPlan plan)
            {
                var killed=new HashSet<BattleUnit>();
                foreach(var mode in plan.targets)
                foreach(var enemy in mode.Value.Where(u=>u.IsPlayerSide!=actor.IsPlayerSide&&u.Character.LifeState==CharacterLifeState.Alive))
                {
                    if(!plan.skill.Effects.Any(e=>e.TargetModeIndex==mode.Key&&e.EffectType==EffectType.DealDamage&&e.DelayCasterTurns==0))continue;
                    var preview=CombatSkillPreviewCalculator.CalculateTargetPreview(actor,enemy,plan.skill,mode.Key);
                    // No assumed crit/proc or perfect hit: retain a conservative
                    // direct minimum and a high real accuracy requirement.
                    if(preview.Damage.HasDamage&&preview.Damage.Minimum>=enemy.Character.CurrentHealth+enemy.Character.ShieldPool.TotalShield
                        &&(!preview.HasHitChance||preview.HitChance>=.85f))killed.Add(enemy);
                }
                return killed;
            }
            float JointRemainingRisk(BattleUnit ally,HashSet<BattleUnit> removed)
                =>NextPlayerDot(ally)+VisiblePlayerOpponents(ally).Where(e=>!removed.Contains(e)&&NextPlayerTurn(e)<=NextPlayerTurn(ally)
                    +(MageRecoveryPolicy&&Mage(ally)&&StatusEffectSystem.IsHardCrowdControlled(ally)?CombatTurnSystem.CalculateBaseActionValue(ally):0))
                    .Sum(e=>EnemyContact(e,ally,ally.Position));
            bool TryJointLethalAttack(BattleUnit actor)
            {
                if(!JointPolicy||Session.Units.Any(u=>u.IsPlayerSide==actor.IsPlayerSide&&u.Character.LifeState==CharacterLifeState.Downed&&RescueEmergency(actor,u)))return false;
                var allies=Session.Units.Where(u=>u.IsPlayerSide==actor.IsPlayerSide&&u.Character.LifeState==CharacterLifeState.Alive).ToArray();
                var threatened=allies.Where(u=>PlayerNeedsProtection(u)).ToArray();
                // Ordinary plan selection already handles unpressured finishes.
                if(threatened.Length==0)return false;
                foreach(var plan in BuildPlayerPlans(actor).OrderByDescending(p=>p.score))
                {
                    var killed=JointLethalTargets(actor,plan);if(killed.Count==0)continue;
                    if(!threatened.All(u=>JointRemainingRisk(u,killed)*1.15f<u.Character.CurrentHealth+u.Character.ShieldPool.TotalShield))continue;
                    if(JointRemainingRisk(actor,killed)*1.15f>=actor.Character.CurrentHealth+actor.Character.ShieldPool.TotalShield)continue;
                    if(!killed.Any(e=>threatened.Any(u=>EnemyContact(e,u,u.Position)>0)))continue;
                    if(!skills.TryExecute(Session,actor,plan.skill,plan.targets,null,plan.objectPosition,plan.positions))continue;
                    AuditChosenPlan?.Invoke(actor,plan.skill,plan.targets.Values.SelectMany(t=>t).Distinct().ToArray(),plan.score);
                    if(CombatActionCostCalculator.CalculateSkillCost(actor,plan.skill)==0)zeroCostUsed.Add(plan.skill);
                    return true;
                }
                return false;
            }
            bool TryJointPressureApproach(BattleUnit actor)
            {
                if(!JointPolicy||playerRepositioned||CriticalPatient(actor)||HoldRescuePosition(actor)
                    ||Session.Units.Any(u=>u.IsPlayerSide==actor.IsPlayerSide&&u.Character.LifeState==CharacterLifeState.Downed&&RescueEmergency(actor,u)))return false;
                UpdatePlayerFocus(actor);
                var current=BuildPlayerPlans(actor);
                if(current.Count==0||current.Any(p=>p.skill.Effects.Any(e=>e.EffectType==EffectType.DealDamage)
                    ||CombatActionCostCalculator.CalculateSkillCost(actor,p.skill)==0))return false;
                // Compare a complete affordable move+attack with preparing a
                // buff out of contact. The committed attack still revalidates.
                return TryCoordinatedFocusAttack(actor,new List<PlayerPlan>());
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public partial class EnemyBattleSimulationTests
    {
        // Fixed legal role loadouts, not an optimizer with knowledge of a route.
        // Investment paths, capacity, equipment and production costs stay intact.
        static string[] BuildLoadout(CharacterInstance hero,int profile)
        {
            var known=hero.GetKnownSkills().Select(s=>s.SkillId).ToHashSet();
            string[] preferred=hero.ClassData.Id switch
            {
                "class.warrior" when profile==0=>new[]{"skill.warrior.destruction","skill.warrior.immobilize","skill.warrior.protection","skill.warrior.regroup","skill.warrior.shout","skill.warrior.bleeding_destruction","skill.warrior.kick"},
                "class.warrior"=>new[]{"skill.warrior.shout","skill.warrior.whirlwind","skill.warrior.death_wish","skill.warrior.bleeding_destruction","skill.warrior.protection","skill.warrior.destruction","skill.warrior.kick"},
                "class.scout" when profile==1=>new[]{"skill.scout.poisoned_dagger","skill.scout.quick_strikes","skill.scout.tonic","skill.scout.ricochet","skill.scout.weak_point","skill.scout.aimed_throw"},
                "class.scout"=>new[]{"skill.scout.ricochet","skill.scout.shadow","skill.scout.weak_point","skill.scout.quick_strikes","skill.scout.poisoned_dagger","skill.scout.tonic"},
                "class.mage" when profile==0=>new[]{"skill.mage.shield","skill.mage.fireball","skill.mage.energy_split","skill.mage.magic_focus","skill.mage.offering","skill.mage.ritual","skill.mage.sacrifice","skill.mage.focus"},
                "class.mage" when profile==2=>new[]{"skill.mage.shield","skill.mage.fireball","skill.mage.energy_split","skill.mage.fire_arrow","skill.mage.magic_focus","skill.mage.ritual","skill.mage.offering","skill.mage.fire_aura","skill.mage.focus"},
                "class.mage"=>new[]{"skill.mage.shield","skill.mage.fireball","skill.mage.fire_arrow","skill.mage.focus","skill.mage.fire_aura","skill.mage.magic_focus","skill.mage.offering"},
                "class.paladin" when profile==1=>new[]{"skill.paladin.heal","skill.paladin.judgement_field","skill.paladin.blinding_light","skill.paladin.holy_regeneration","skill.paladin.group_heal","skill.paladin.blessing","skill.paladin.grounding_wave"},
                "class.paladin" when profile==2=>new[]{"skill.paladin.heal","skill.paladin.holy_regeneration","skill.paladin.judgement_field","skill.paladin.group_heal","skill.paladin.blinding_light","skill.paladin.blessing"},
                "class.paladin"=>new[]{"skill.paladin.heal","skill.paladin.holy_regeneration","skill.paladin.group_heal","skill.paladin.blessing","skill.paladin.judgement_field","skill.paladin.blinding_light"},
                _=>Array.Empty<string>()
            };
            return new[]{hero.GetBasicAttackSkill().SkillId}.Concat(preferred)
                .Concat(hero.Definition.StartingSkills.Select(s=>s.SkillId)).Concat(known.OrderBy(s=>s,StringComparer.Ordinal))
                .Where(known.Contains).Distinct().Take(hero.ActiveSkillLoadout.Capacity).ToArray();
        }
        static void SetBuildLoadout(CharacterInstance hero,int profile)
        {
            var ids=BuildLoadout(hero,profile);
            AssertBuildLoadout(hero,ids);
        }
        static void AssertBuildLoadout(CharacterInstance hero,string[] ids)
        {
            NUnit.Framework.Assert.That(hero.ActiveSkillLoadout.TryInitialize(hero.GetKnownSkills().Select(s=>s.SkillId),
                hero.GetBasicAttackSkill().SkillId,ids,hero.ActiveSkillLoadout.CopyCapacityModifiers()),NUnit.Framework.Is.True);
        }

        sealed partial class Engine
        {
            internal bool UsePreviousBuildPolicy;
            internal bool AuditExhaustiveDirections;
            bool BuildAware=>CoordinatedPolicy&&!UsePreviousBuildPolicy;
            IEnumerable<GridPosition> BuildDirections(BattleUnit actor,SkillTargetModeData mode)
            {
                var all=targeting.GetAbsoluteRangePositions(Session,actor,mode);
                // Production resolves these references by signed direction.
                // Keep its first representative and avoid scoring it 10+ times.
                return AuditExhaustiveDirections?all:all.GroupBy(p=>(Math.Sign(p.X-actor.Position.X),Math.Sign(p.Y-actor.Position.Y))).Select(g=>g.First());
            }
            bool BuildEffectActive(BattleUnit unit,string skill)=>unit.Character.PassiveEffects.Any(e=>PassiveEffectLifecycleSystem.IsActive(e)
                &&e.Sources.Any(s=>s.SourceId.StartsWith(skill+".effect.",StringComparison.Ordinal)));
            float BuildAttackValue(BattleUnit actor)
            {
                if(actor.Character.LifeState!=CharacterLifeState.Alive||StatusEffectSystem.IsHardCrowdControlled(actor))return 0;
                var values=VisiblePlayerOpponents(actor).Select(t=>EnemyContact(actor,t,t.Position)).ToArray();
                return values.DefaultIfEmpty(0).Max();
            }
            float BuildPlanCost(BattleUnit actor,SkillData skill)
            {
                var ap=CombatActionCostCalculator.CalculateSkillCost(actor,skill);
                if(!BuildAware||actor.Character.ResourcePool.ResourceType!=ResourceType.Mana)return Math.Max(20,ap);
                var pool=actor.Character.ResourcePool;
                var resource=Math.Max(0,PassiveEffectCombatExecutionSystem.ResolveResourceCost(actor,skill));
                return Math.Max(20,ap+ForecastPoints(actor)*resource/(float)Math.Max(1,pool.MaxValue));
            }
            internal float BuildUtility(BattleUnit actor,SkillData skill,int mode,IReadOnlyList<BattleUnit> targets,HashSet<string> used,bool late)
            {
                if(!BuildAware||late)return ExpeditionUtilityScore(Session,actor,skill,mode,targets,used,late);
                if(used.Contains(actor.Character.CharacterId+":"+skill.SkillId))return 0;
                var attack=(actor.Character.Stats.MinAttack.GetValue()+actor.Character.Stats.MaxAttack.GetValue())*.5f;
                var offense=BuildAttackValue(actor);float value=0;
                foreach(var target in targets.Distinct())
                foreach(var original in skill.Effects.Where(e=>e.TargetModeIndex==mode))
                {
                    var e=PassiveEffectCombatExecutionSystem.ResolveEffect(actor,skill,original);
                    if(!SkillExecutionSystem.CanPlanEffect(Session,e,actor,target))continue;
                    var ally=actor.IsPlayerSide==target.IsPlayerSide;
                    var incoming=ally?PlayerIncoming(target,target.Position):0;
                    switch(e.EffectType)
                    {
                        case EffectType.MarkForDestruction when !ally&&target.WarriorState.OrderTurns==0:
                            var team=Session.Units.Where(u=>u.IsPlayerSide==actor.IsPlayerSide&&u.Character.LifeState==CharacterLifeState.Alive)
                                .Sum(u=>EnemyContact(u,target,target.Position));
                            value+=Math.Min(target.Character.CurrentHealth,team)*(.2f+.05f*WarriorCombatSystem.C(actor,"breakdown"))*1.5f;break;
                        case EffectType.MarkWeakPoint when !ally&&target.ScoutState.WeakPointTurns==0:
                            value+=Math.Min(target.Character.CurrentHealth,Math.Max(attack,offense))*.4f;break;
                        case EffectType.ProtectAlly when ally&&target.WarriorState.ProtectionTurns==0:
                            value+=incoming*Math.Min(.6f,.3f+.1f*WarriorCombatSystem.Adjacent(Session,target))*1.5f;break;
                        case EffectType.ModifyActionValue when ally&&target!=actor&&!StatusEffectSystem.IsHardCrowdControlled(target):
                            value+=BuildAttackValue(target)*Mathf.Clamp01(NextPlayerTurn(target)/Math.Max(1,CombatTurnSystem.CalculateBaseActionValue(target)));break;
                        case EffectType.GrantFreeMovement when ally&&target.WarriorState.FreeMovementTurns==0:
                            if(VisiblePlayerOpponents(target).Any()&&BuildAttackValue(target)==0)value+=attack*.3f;break;
                        case EffectType.PaladinBlessing when ally&&!target.Character.PassiveEffects.Any(p=>p.EffectId=="paladin.blessing"&&PassiveEffectLifecycleSystem.IsActive(p)):
                            value+=(BuildAttackValue(target)+incoming)*(.1f+.05f*PaladinCombatSystem.P(actor,"strong_blessing"))*1.5f;break;
                        case EffectType.ApplyUniqueBuff when ally&&!CombatDamageSystem.HasUniqueBuff(target,e.UniqueBuffType):
                            if(e.UniqueBuffType==UniqueBuffType.MageOffering)
                            {
                                var bonus=MageCombatSystem.OfferingBonus(actor);var penalty=Math.Max(0,bonus-.1f*MageCombatSystem.W(actor,"calm"));
                                if(!(incoming>0&&actor.Character.CurrentHealth<actor.Character.Stats.MaxHealth.GetValue()*.3f)
                                    &&(incoming*(1+penalty)*1.2f+NextPlayerDot(actor))<actor.Character.CurrentHealth+actor.Character.ShieldPool.TotalShield)
                                    value+=Math.Max(0,offense*bonus*1.5f-incoming*penalty);
                            }
                            else if(e.UniqueBuffType==UniqueBuffType.MageMaximumDamage)value+=Math.Max(0,actor.Character.Stats.MaxAttack.GetValue()-attack)*2;
                            else value+=Math.Max(attack*.3f,offense*.3f);
                            break;
                        case EffectType.MageSacrifice when ally&&target!=actor:
                            if(!actor.Character.PassiveEffects.Any(p=>p.EffectId.StartsWith("mage.sacrifice.gain.",StringComparison.Ordinal)&&PassiveEffectLifecycleSystem.IsActive(p)))
                                value+=Math.Max(0,.2f*(offense-BuildAttackValue(target))*1.5f);
                            break;
                        case EffectType.MageRitual when ally&&target.Character.LifeState==CharacterLifeState.Alive:
                            if(BuildEffectActive(target,skill.SkillId))break;
                            if(incoming>0&&CriticalPatient(target))return 0;
                            var max=target.Character.Stats.MaxHealth.GetValue();var cost=Math.Min(max*.1f,Math.Max(0,target.Character.CurrentHealth-1));
                            if(target.Character.CurrentHealth-cost<=NextPlayerDot(target)+incoming*1.2f)return 0;
                            value+=Math.Max(0,Math.Min(max-target.Character.CurrentHealth+cost,max*e.PercentageValue*.6f)-cost);
                            break;
                        case EffectType.MageFireAura when ally:
                            if(!target.ActiveAuras.Any(a=>a.Creator==actor&&a.RemainingTurns>0))
                                value+=attack*.8f*VisiblePlayerOpponents(actor).Count(u=>PlayerDistance(u.Position,target.Position)<=1);
                            break;
                        case EffectType.ModifyStat when !BuildEffectActive(target,skill.SkillId):
                            if(ally&&e.ModifierValue>0)value+=Math.Max(attack*.3f,BuildAttackValue(target)*.4f);
                            if(!ally&&e.ModifierValue<0)value+=attack*.3f;break;
                        case EffectType.SacrificeHealthForResource when target==actor:
                            var pool=actor.Character.ResourcePool;
                            var sacrificed=Math.Min(actor.Character.CurrentHealth-1,Mathf.FloorToInt(actor.Character.Stats.MaxHealth.GetValue()*e.HealthSacrificePercent));
                            var gain=Math.Min(pool.RecoveryLimit-pool.CurrentValue,sacrificed*e.ResourceRestorePerSacrificedHealth);
                            if(pool.CurrentValue<pool.MaxValue*.3f&&sacrificed>0&&gain>0
                                &&actor.Character.CurrentHealth-sacrificed>NextPlayerDot(actor)+incoming*1.2f
                                &&!(incoming>0&&CriticalPatient(actor)))
                                value+=Math.Max(0,attack*2*gain/Math.Max(1,pool.MaxValue)-sacrificed);
                            break;
                        case EffectType.ApplyStatus when ally&&e.StatusId=="invisibility"&&!StatusEffectSystem.IsInvisible(target):
                            value+=incoming*.6f+offense*(ScoutCombatSystem.AssassinRank(actor,"surprise")>0?.6f:.15f);break;
                    }
                }
                if(skill.SkillId==WarriorCombatSystem.DeathWish&&!actor.ActiveToggleSkillIds.Contains(skill.SkillId))value+=attack*1.5f;
                return value;
            }

            float BuildShieldPriority(BattleUnit actor,BattleUnit ally,SkillData skill,Dictionary<int,IReadOnlyList<BattleUnit>> targets,bool urgentOnly)
            {
                if(ally.Character.LifeState!=CharacterLifeState.Alive||ally.Character.ShieldPool.TotalShield>0)return 0;
                var risk=NextPlayerDot(ally)+PlayerIncoming(ally,ally.Position);
                if(risk<=0)return 0;
                var gain=targets.Where(t=>t.Value.Contains(ally)).Sum(t=>ImmediateRecovery(actor,ally,skill,t.Key));
                var avoidsDown=risk>=ally.Character.CurrentHealth&&ally.Character.CurrentHealth+gain>risk;
                if(urgentOnly&&!avoidsDown)return 0;
                // Low HP alone is not a reason to repeatedly shield instead of
                // removing attackers; protection is not restoration of that HP.
                return Math.Min(gain,risk)*(avoidsDown?4:1)+(avoidsDown?ally.Character.Stats.MaxHealth.GetValue():0);
            }

            bool BuildManageToggles(BattleUnit actor)
            {
                if(!BuildAware||!actor.ActiveToggleSkillIds.Contains("skill.mage.offering"))return false;
                var incoming=PlayerIncoming(actor,actor.Position);
                if(!(incoming>0&&actor.Character.CurrentHealth<actor.Character.Stats.MaxHealth.GetValue()*.3f)
                    &&NextPlayerDot(actor)+incoming*1.2f<actor.Character.CurrentHealth+actor.Character.ShieldPool.TotalShield)return false;
                var skill=actor.Character.GetActiveSkills().FirstOrDefault(s=>s.SkillId=="skill.mage.offering");
                if(skill==null||!skills.TryExecute(Session,actor,skill,new Dictionary<int,IReadOnlyList<BattleUnit>>{{0,new[]{actor}}}))return false;
                zeroCostUsed.Add(skill);return true;
            }

            // Placement is a separate production parameter, not a target tile.
            void BuildObjectPlans(BattleUnit actor,SkillData skill,List<PlayerPlan> plans)
            {
                var effect=skill.Effects.SingleOrDefault(e=>e.EffectType==EffectType.CreateObject);
                if(effect?.SummonedObjectDefinition?.ObjectId!=PaladinCombatSystem.FieldId)return;
                var active=Session.SummonedObjects.FirstOrDefault(o=>PaladinCombatSystem.IsField(o)&&o.CreatorCharacterId==actor.Character.CharacterId);
                var enemies=VisiblePlayerOpponents(actor).ToArray();
                var oldCoverage=active==null?0:enemies.Count(t=>PaladinCombatSystem.FieldContains(Session,active,t.Position));
                if(active!=null&&oldCoverage>0)return;
                var targets=new Dictionary<int,IReadOnlyList<BattleUnit>>();
                foreach(var position in targeting.GetPositionsInRange(Session,actor.Position,effect.ObjectPlacementRange))
                {
                    var radius=2+PaladinCombatSystem.C(actor,"field_range");
                    var group=enemies.Where(t=>CombatTargetingService.IsPositionWithinRelativeRange(position,position,t.Position,new SkillRangeData(RangeShape.Square,1,radius))
                        &&sight.HasLineOfSight(Session,position,t.Position)).ToArray();
                    if(group.Length==0||active!=null&&(oldCoverage>0||group.Length<2))continue;
                    if(!skills.CanPreview(Session,actor,skill,targets,null,position,null))continue;
                    var attack=(actor.Character.Stats.MinAttack.GetValue()+actor.Character.Stats.MaxAttack.GetValue())*.5f;
                    var benefit=group.Sum(t=>Math.Min(t.Character.CurrentHealth,CombatDamageSystem.ApplyDefense(actor,t,Mathf.RoundToInt(attack*.5f),DamageType.Magical)*2f)
                        +attack*.15f*PaladinCombatSystem.C(actor,"exhaustion"));
                    plans.Add(new PlayerPlan{skill=skill,targets=targets,positions=new(),objectPosition=position,score=benefit/BuildPlanCost(actor,skill)});
                }
            }

            bool BuildPreservesHealing(BattleUnit actor,SkillData skill,int reserve)
            {
                var cost=PassiveEffectCombatExecutionSystem.ResolveResourceCost(actor,skill);
                if(cost<=0||actor.Character.ResourcePool.CurrentValue-cost>=reserve)return true;
                // An actual in-range lethal hit may remove pressure immediately.
                return skill.Effects.Any(e=>e.EffectType==EffectType.DealDamage)&&VisiblePlayerOpponents(actor).Any(t=>
                    skill.TargetModes.Any(m=>CombatTargetingService.IsPositionWithinRange(actor.Position,t.Position,PassiveEffectCombatExecutionSystem.ResolveMode(actor,skill,m).AbsoluteRange))
                    &&(!skill.RequiresLineOfSight||sight.HasLineOfSight(Session,actor.Position,t.Position))
                    &&CombatSkillPreviewCalculator.CalculateTargetPreview(actor,t,skill).Damage.Minimum>=t.Character.CurrentHealth);
            }
        }
    }
}

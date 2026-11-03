using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public partial class EnemyBattleSimulationTests
    {
        [Serializable] sealed class PartyResistanceRow {public string hero;public int hp;public float bleed,poison,fire,move,suppression,weakening;}
        [Serializable] sealed class BleedThreatRow
        {
            public string hero,enemy,enemyName,skill,skillName;public bool ordinary,conditional;
            public int enemyLevel,heroHp,turns,applications,rawTick,tick,potentialDamage,normalDirectMax;
            public float floor,resistance,statusChance,damageFraction;
        }
        [Serializable] sealed class PlayerBleedReport {public List<PartyResistanceRow> party=new();public List<BleedThreatRow> threats=new();public string validatedEnemy,validatedSkill;public int validatedRequested,validatedHpLost;}
        [Test,Explicit("Read-only L1 party resistance and largest authored enemy bleed audit, plus detached common-floor comparisons.")]
        public void AuditPlayerLevelOneBleeding()
        {
            var folder=Path.GetFullPath("../LocalHandoffs/player-bleed-resistance-20261103");Directory.CreateDirectory(folder);
            var previous=UnityEngine.Random.state;var report=new PlayerBleedReport();
            var definitions=PointCatalog.CharacterDefinitions.Where(d=>d!=null && d.ClassData!=null && d.Id.StartsWith("enemy.") && d.name!="CD_Enemy_Test").ToArray();
            foreach(var d in definitions.Concat(Enumerable.Range(0,4).Select(PointHeroDefinition)))originals[d]=EditorJsonUtility.ToJson(d);
            try
            {
                for(var h=0;h<4;h++)
                {
                    var party=PointUnit(HealthComparisonHero(h,1,0),true);
                    report.party.Add(new PartyResistanceRow{hero=party.Character.Definition.DisplayName,hp=party.Character.MaximumHealth,
                        bleed=ModifyResistanceSystem.GetEffectiveResistance(party,ResistanceType.Bleed),poison=ModifyResistanceSystem.GetEffectiveResistance(party,ResistanceType.Poison),
                        fire=ModifyResistanceSystem.GetEffectiveResistance(party,ResistanceType.Fire),weakening=ModifyResistanceSystem.GetEffectiveResistance(party,ResistanceType.Weakening),move=ModifyResistanceSystem.GetEffectiveResistance(party,ResistanceType.Move),suppression=ModifyResistanceSystem.GetEffectiveResistance(party,ResistanceType.Suppression)});
                    foreach(var definition in definitions)foreach(var level in HistoricalEnemyBalanceReference.UsesBossBaseBoost(definition.Id)?new[]{1,10}:new[]{1})
                    foreach(var skill in definition.StartingSkills.Append(definition.BasicAttackSkill).Where(s=>s!=null).Distinct())
                    foreach(var original in skill.Effects.Where(e=>e.EffectType==EffectType.ApplyStatus && e.StatusId=="bleed" || e.EffectType==EffectType.DealDamage && e.OnHitStatusId=="bleed"))
                    foreach(var floor in new[]{-1f,.15f,.25f,.4f,.5f})
                    {
                        var caster=PointUnit(new CharacterInstanceFactory().Create(definition,definition.ClassData,level),false);
                        var target=PointUnit(HealthComparisonHero(h,1,0),true);caster.Position=new GridPosition(3,2);target.Position=new GridPosition(2,2);target.ScoutState.Facing=Vector2Int.right;
                        if(floor>=0)
                        {
                            target.Character.BaseResistances=JsonUtility.FromJson<ResistanceSet>(JsonUtility.ToJson(target.Character.BaseResistances));
                            target.Character.BaseResistances.BleedResistance=Math.Max(target.Character.BaseResistances.BleedResistance,floor);
                            new CharacterStatPipelineSystem().Refresh(target.Character);
                        }
                        var effect=PassiveEffectCombatExecutionSystem.ResolveEffect(caster,skill,original);
                        var turns=effect.EffectType==EffectType.DealDamage?effect.OnHitStatusDurationTurns:effect.DurationTurns;
                        var applications=effect.EffectType==EffectType.DealDamage?Math.Max(1,effect.DamageHitCount)+effect.AdditionalHitTrials:1;
                        var raw=StatusEffectSystem.CalculateStatusDamagePerTurn(caster,"bleed",effect.StatusDamageMultiplier)*applications;
                        var tick=DamageOverTimePassiveEffectProcessor.CalculateEffectiveDamage(target,"bleed",raw);
                        var baseChance=effect.EffectType==EffectType.DealDamage?effect.OnHitStatusChance:effect.BaseChance;
                        var perApplication=StatusEffectSystem.CalculateStatusApplicationChance(caster,target,"bleed",baseChance);
                        // Probability shown is conditional on the required hit/state; not full cast availability.
                        var normal=CombatSkillPreviewCalculator.CalculateTargetPreview(caster,target,skill);
                        report.threats.Add(new BleedThreatRow{hero=target.Character.Definition.DisplayName,enemy=definition.Id,enemyName=definition.DisplayName,skill=skill.SkillId,skillName=skill.DisplayName,
                            ordinary=SkillPointEnemyIds.Contains(definition.Id),enemyLevel=level,heroHp=target.Character.MaximumHealth,turns=turns,applications=applications,rawTick=raw,tick=tick,
                            potentialDamage=tick*turns,normalDirectMax=normal.Damage.Maximum,floor=floor,resistance=ModifyResistanceSystem.GetEffectiveResistance(target,ResistanceType.Bleed),
                            statusChance=1-Mathf.Pow(1-perApplication,applications),damageFraction=tick*turns/(float)target.Character.MaximumHealth,
                            conditional=effect.Conditions?.Any(c=>c!=null && c.ConditionType!=EffectConditionType.Always)==true});
                    }
                }
                var mage=report.party[2];var largest=report.threats.Where(r=>r.hero==mage.hero && r.ordinary && r.enemyLevel==1 && r.floor<0).OrderByDescending(r=>r.potentialDamage).First();
                var def=definitions.Single(d=>d.Id==largest.enemy);var source=PointUnit(new CharacterInstanceFactory().Create(def,def.ClassData,1),false);var victim=PointUnit(HealthComparisonHero(2,1,0),true);
                var session=new CombatSession{Grid=new BattleGrid(8,8),Units=new(){source,victim}};var status=new StatusEffectSystem();
                Assert.That(status.ApplyStatus(source,victim,"bleed",1,largest.turns,1,1,0,"audit.conditional",publishFeedback:false,statusDamagePerTurnOverride:largest.rawTick,sharedRoll:0f),Is.True);
                var before=victim.Character.CurrentHealth;
                for(var t=0;t<largest.turns && victim.Character.LifeState==CharacterLifeState.Alive;t++){status.ProcessTurnStart(session,victim);status.ProcessTurnEnd(victim);}
                report.validatedEnemy=largest.enemy;report.validatedSkill=largest.skill;report.validatedRequested=largest.potentialDamage;report.validatedHpLost=before-victim.Character.CurrentHealth;
                Assert.That(report.validatedHpLost,Is.EqualTo(Math.Min(before,largest.potentialDamage)),"Real forced-onset ticks must reproduce the conditional packet. No direct attack included.");
                File.WriteAllText(Path.Combine(folder,"audit.json"),JsonUtility.ToJson(report,true));
            }
            finally{UnityEngine.Random.state=previous;}
        }
    }
}

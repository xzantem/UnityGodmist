using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Combat;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public partial class EnemyBattleSimulationTests
    {
        static string HealthAttackFolder=>Path.GetFullPath("../LocalHandoffs/health-attack-comparison-20261103");
        [Serializable] sealed class HealthAttackBattle
        {
            public int level,party,seed,attackBonus,hpCut,enemyCount;
            public string encounter,opening;public string[] enemyIds;
            public Result result;
            public bool downedBeforeOwnTurn;public float damageBeforeFirstPartyTurn,maxSingleHitFraction,shieldAbsorbed;
            public int endMana,startMana,healthDamageTaken;
        }
        [Serializable] sealed class HealthAttackStatic
        {
            public int level,hero,attackBonus,hpCut,enemyHp,heroHp,min,max,criticalMax;
            public string enemy,name,heroName;
            public float attackMin,attackMax,expected,hit;
        }
        [Serializable] sealed class HealthAttackReport
        {
            public string method="Current authored ordinary encounters and current production Common Normal gear, legal earned SP, no upgrades/jewelry/unique items. Production turn/damage/AI/skill/status systems; deterministic test player policy (not human play). Fresh full-health fights, natural starting resources, no supplies. Flat open grid, frontal formation, ordinary non-summoning encounters only. NPC HP and base/growth ATK precomputed on detached definitions, no runtime multiplier or asset writes. Paired seeds reset per battle; action branches can diverge. Turns count individual scheduled unit turns, not rounds or seconds. Before own turn = before that hero first becomes ready, not a proof that no teammate could help.";
            public List<HealthAttackBattle> battles=new();public List<HealthAttackStatic> stats=new();
        }
        CharacterDefinition HealthAttackDefinition(CharacterDefinition definition,int level,int cut,int bonus)
        {
            var original=new CharacterInstanceFactory().Create(definition,definition.ClassData,level);
            var clone=HealthProbeDefinition(definition,Mathf.Max(1,Mathf.RoundToInt(original.MaximumHealth*(1-cut/100f))));
            var data=new SerializedObject(clone);var basis=data.FindProperty("<BaseStats>k__BackingField");var growth=data.FindProperty("<GrowthData>k__BackingField");
            foreach(var field in new[]{"MinAttack","MaxAttack"})basis.FindPropertyRelative("<"+field+">k__BackingField").floatValue*=1+bonus/100f;
            foreach(var field in new[]{"MinAttack","MaxAttack","EarlyMinAttack","EarlyMaxAttack","LateMinAttack","LateMaxAttack","LateMinAttackOffset","LateMaxAttackOffset"})
                growth.FindPropertyRelative("<"+field+">k__BackingField").floatValue*=1+bonus/100f;
            data.ApplyModifiedPropertiesWithoutUndo();
            var actual=new CharacterInstanceFactory().Create(clone,clone.ClassData,level);
            Assert.That(actual.Stats.MinAttack.GetValue(),Is.EqualTo(original.Stats.MinAttack.GetValue()*(1+bonus/100f)).Within(.001f));
            Assert.That(actual.Stats.MaxAttack.GetValue(),Is.EqualTo(original.Stats.MaxAttack.GetValue()*(1+bonus/100f)).Within(.001f));
            Assert.That(actual.Stats.PhysicalDefense.GetValue(),Is.EqualTo(original.Stats.PhysicalDefense.GetValue()));
            Assert.That(actual.Stats.MagicalDefense.GetValue(),Is.EqualTo(original.Stats.MagicalDefense.GetValue()));
            Assert.That(actual.Stats.Speed.GetValue(),Is.EqualTo(original.Stats.Speed.GetValue()));
            return clone;
        }
        static EncounterDefinition[] HealthAttackEncounters()
        {
            bool Eligible(EncounterDefinition e)=>e!=null && e.Enemies.Count>=3 && e.Enemies.Count<=5
                && e.Enemies.All(n=>n.CharacterDefinition!=null && SkillPointEnemyIds.Contains(n.CharacterDefinition.Id)
                    && !n.CharacterDefinition.StartingSkills.Append(n.CharacterDefinition.BasicAttackSkill).Where(s=>s!=null)
                        .SelectMany(s=>s.Effects).Any(f=>f.EffectType==EffectType.Summon));
            var forest=PointCatalog.DungeonDefinitions.Single(d=>d!=null && d.Id=="dungeon.forest.expedition").EncounterDefinitions.Where(Eligible).ToArray();
            var cat=PointCatalog.DungeonDefinitions.Single(d=>d!=null && d.Id=="dungeon.catacombs.expedition").EncounterDefinitions.Where(Eligible).ToArray();
            var wolves=forest.Where(e=>e.Enemies.Any(n=>n.CharacterDefinition.Id=="enemy.forest.rabid_wolf"))
                .OrderByDescending(e=>e.Enemies.Count(n=>n.CharacterDefinition.Id=="enemy.forest.rabid_wolf")).ThenBy(e=>e.Enemies.Count).ThenBy(e=>e.Id).First();
            var ranged=forest.Where(e=>e!=wolves && e.Enemies.Any(n=>n.CharacterDefinition.Id.Contains("shooter")||n.CharacterDefinition.Id.Contains("archer")))
                .OrderByDescending(e=>e.Enemies.Select(n=>n.CharacterDefinition.Id).Distinct().Count()).ThenBy(e=>e.Enemies.Count).ThenBy(e=>e.Id).First();
            var mixed=cat.OrderByDescending(e=>e.Enemies.Select(n=>n.CharacterDefinition.Id).Distinct().Count()).ThenBy(e=>e.Enemies.Count).ThenBy(e=>e.Id).First();
            return new[]{wolves,ranged,mixed};
        }
        [Test,Explicit("Pilot of HP/ATK comparison; real full battles without authoring.")]
        [Timeout(600000)]
        public void AuditHealthAttackPilot()=>RunHealthAttackComparison(new[]{1},1,true);
        [TestCase(1),TestCase(3),TestCase(5),Explicit("Read-only HP -20% with ATK +0/+5/+10/+15/+25 comparison.")]
        [Timeout(900000)]
        public void AuditHealthAttackComparison(int level)=>RunHealthAttackComparison(new[]{level},4,false);

        [TestCase(2),TestCase(4),Explicit("L1 close-contact wolf opening stress; same balance candidates, no gameplay edits.")]
        [Timeout(600000)]
        public void AuditHealthAttackCloseContact(int partySize)=>RunHealthAttackComparison(new[]{1},8,false,true,partySize);

        void RunHealthAttackComparison(int[] levels,int seeds,bool pilot,bool close=false,int closeParty=4)
        {
            Directory.CreateDirectory(HealthAttackFolder);Directory.CreateDirectory(Folder);
            var random=UnityEngine.Random.state;var report=new HealthAttackReport();
            var variants=pilot?new[]{(cut:0,bonus:0),(cut:20,bonus:25)}:new[]{(cut:0,bonus:0),(cut:20,bonus:0),(cut:20,bonus:5),(cut:20,bonus:10),(cut:20,bonus:15),(cut:20,bonus:25)};
            var output=Path.Combine(HealthAttackFolder,pilot?"pilot.json":close?"close-party-"+closeParty+".json":"level-"+levels[0]+".json");
            try
            {
                var encounters=HealthAttackEncounters();if(close)encounters=encounters.Take(1).ToArray();
                var definitions=PointEnemyDefinitions.ToArray();
                foreach(var definition in definitions)originals[definition]=EditorJsonUtility.ToJson(definition);
                foreach(var level in levels)
                {
                    var clonesByVariant=new Dictionary<(string,int,int),CharacterDefinition>();
                    foreach(var variant in variants)foreach(var definition in definitions)
                    {
                        var candidate=HealthAttackDefinition(definition,level,variant.cut,variant.bonus);
                        clonesByVariant[(definition.Id,variant.cut,variant.bonus)]=candidate;
                        if(pilot||close)continue;
                        foreach(var heroIndex in Enumerable.Range(0,4))
                        {
                            var hero=PointUnit(HealthComparisonHero(heroIndex,level,1),true);
                            var enemy=PointUnit(new CharacterInstanceFactory().Create(candidate,candidate.ClassData,level),false);
                            var skill=enemy.Character.GetBasicAttackSkill();
                            var range=skill.TargetModes.Select(m=>PassiveEffectCombatExecutionSystem.ResolveMode(enemy,skill,m).AbsoluteRange.MaxRange).DefaultIfEmpty(1).Max();
                            enemy.Position=new GridPosition(5,2);hero.Position=new GridPosition(5-Math.Min(4,Math.Max(1,range)),2);hero.ScoutState.Facing=Vector2Int.right;
                            var session=new CombatSession{Grid=new BattleGrid(8,8),Units=new List<BattleUnit>{hero,enemy}};hero.WarriorState.Session=session;enemy.WarriorState.Session=session;
                            var rng=UnityEngine.Random.state;var normal=CombatSkillPreviewCalculator.CalculateTargetPreview(enemy,hero,skill);
                            var expected=skill.Effects.Select(e=>e.TargetModeIndex).Distinct().Sum(mode=>CombatSkillPreviewCalculator.CalculateExpectedTargetDamage(session,enemy,hero,skill,mode));
                            var guarantee=new PassiveEffectRuntime{EffectId="audit.critical",IsUniqueCombatBuff=true,UniqueBuffType=UniqueBuffType.GuaranteedCriticalHit,Stacks=1};
                            Assert.That(new PassiveEffectLifecycleSystem().TryApplyCombatScopedEffect(enemy.Character,guarantee,PassiveEffectStackingMode.ReplaceDurationAndValues,out _),Is.True);
                            var crit=CombatSkillPreviewCalculator.CalculateTargetPreview(enemy,hero,skill);
                            Assert.That(UnityEngine.Random.state,Is.EqualTo(rng));
                            report.stats.Add(new HealthAttackStatic{level=level,hero=heroIndex,hpCut=variant.cut,attackBonus=variant.bonus,enemyHp=enemy.Character.MaximumHealth,heroHp=hero.Character.MaximumHealth,
                                enemy=definition.Id,name=definition.DisplayName,heroName=hero.Character.Definition.DisplayName,attackMin=enemy.Character.Stats.MinAttack.GetValue(),attackMax=enemy.Character.Stats.MaxAttack.GetValue(),
                                min=normal.Damage.Minimum,max=normal.Damage.Maximum,criticalMax=crit.Damage.Maximum,expected=expected,hit=normal.HitChance});
                        }
                    }
                    foreach(var partySize in close?new[]{closeParty}:level==1&&!pilot?new[]{4,2}:new[]{4})foreach(var encounter in encounters)foreach(var seed in Enumerable.Range(0,seeds))foreach(var variant in variants)
                    {
                        UnityEngine.Random.InitState(771100+seed);
                        var session=new CombatSession{Grid=new BattleGrid(8,8),DungeonTheme=encounter.Enemies[0].CharacterDefinition.Id.Contains("forest")?DungeonTheme.Forest:DungeonTheme.Catacombs};
                        foreach(var index in Enumerable.Range(0,partySize))
                        {
                            var character=HealthComparisonHero(index,level,1);character.CharacterId="audit.hero."+index;
                            SetJointLoadout(character,index==3?1:0,JointDefaultLoadout);
                            session.Units.Add(new BattleUnit{Character=character,IsPlayerSide=true,Position=new GridPosition(index is 0 or 3?3:2,2+index)});
                        }
                        for(var index=0;index<encounter.Enemies.Count;index++)
                        {
                            var entry=encounter.Enemies[index];var definition=clonesByVariant[(entry.CharacterDefinition.Id,variant.cut,variant.bonus)];
                            var character=new CharacterInstanceFactory().Create(definition,entry.ClassData??definition.ClassData,level);character.CharacterId="enemy.audit."+index;
                            session.Units.Add(new BattleUnit{Character=character,Position=new GridPosition(close?4:6,1+index)});
                        }
                        var result=new Result{candidate="hp-"+variant.cut+"-atk+"+variant.bonus,encounter=encounter.Id,level=level,seed=seed,enemyCount=encounter.Enemies.Count};
                        var row=new HealthAttackBattle{opening=close?"close":"normal",level=level,party=partySize,seed=seed,hpCut=variant.cut,attackBonus=variant.bonus,enemyCount=encounter.Enemies.Count,encounter=encounter.Id,
                            enemyIds=encounter.Enemies.Select(e=>e.CharacterDefinition.Id).ToArray(),result=result,startMana=session.Units.Where(u=>u.IsPlayerSide&&u.Character.ResourcePool.ResourceType==ResourceType.Mana).Sum(u=>u.Character.ResourcePool.CurrentValue)};
                        var ready=new HashSet<BattleUnit>();var partyReady=false;var pendingShieldDamage=new Dictionary<BattleUnit,int>();
                        session.DamageApplied+=(source,target,amount,type,id)=>
                        {
                            if(!target.IsPlayerSide)return;
                            var shield=pendingShieldDamage.TryGetValue(target,out var absorbed)?absorbed:0;
                            pendingShieldDamage.Remove(target);
                            var hpDamage=Math.Max(0,amount-shield);row.healthDamageTaken+=hpDamage;
                            row.maxSingleHitFraction=Math.Max(row.maxSingleHitFraction,hpDamage/(float)target.Character.MaximumHealth);
                            if(!partyReady)row.damageBeforeFirstPartyTurn+=hpDamage;
                            // DamageApplied is published before LifeState changes to Downed.
                            if(!ready.Contains(target)&&target.Character.CurrentHealth<=0)row.downedBeforeOwnTurn=true;
                        };
                        using(var engine=new Engine(session,result))
                        {
                            engine.AuditWallClockLimitSeconds=30;engine.AuditAiTimeoutSeconds=2;engine.AuditTurnLimit=300;
                            engine.AuditUseEffectiveActionCost=true;
                            engine.AuditUnitTurn=actor=>{if(actor.IsPlayerSide){ready.Add(actor);partyReady=true;}};
                            engine.AuditFeedback=e=>
                            {
                                if(e.Target?.IsPlayerSide==true&&e.Type==CombatFeedbackType.ShieldLoss&&e.DamageType.HasValue)
                                {
                                    row.shieldAbsorbed+=e.Amount;
                                    pendingShieldDamage[e.Target]=(pendingShieldDamage.TryGetValue(e.Target,out var previous)?previous:0)+e.Amount;
                                }
                            };
                            engine.Run();
                        }
                        row.endMana=session.Units.Where(u=>u.IsPlayerSide&&u.Character.ResourcePool.ResourceType==ResourceType.Mana).Sum(u=>u.Character.ResourcePool.CurrentValue);
                        report.battles.Add(row);File.WriteAllText(output,JsonUtility.ToJson(report,true));
                        File.AppendAllText(Path.Combine(HealthAttackFolder,"progress.txt"),$"L{level} party={partySize} {encounter.Id} seed={seed} hp-{variant.cut} atk+{variant.bonus} {result.outcome} turns={result.turns} seconds={result.seconds:0.###} stop={result.stop}\n");
                        Assert.That(result.stop,Is.Null,"No truncated fight may enter the comparison.");
                    }
                }
                File.WriteAllText(output,JsonUtility.ToJson(report,true));
            }
            finally{UnityEngine.Random.state=random;}
        }
    }
}

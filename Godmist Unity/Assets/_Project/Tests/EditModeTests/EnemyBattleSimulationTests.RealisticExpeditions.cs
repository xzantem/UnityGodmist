using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Combat;
using Godmist.Data.Configuration;
using Godmist.Data.Dungeons;
using Godmist.Data.Items;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Systems.Dungeons;
using Godmist.Systems.Items;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public partial class EnemyBattleSimulationTests
    {
        static string EquipmentAuditRoot=>Path.GetFullPath("../LocalHandoffs/equipment-balance-expeditions-20261018");
        static string RealisticFolder=>Path.Combine(EquipmentAuditRoot,File.ReadAllText(Path.Combine(EquipmentAuditRoot,"phase.txt")).Trim());
        [Serializable] sealed class HistoricalDrop {public int bossLevel,hero,slot,material,requiredLevel;public string definition,rarity="Common";}
        [Serializable] sealed class ExpeditionState
        {
            public int[] level,hp,maxHp,resource,maxResource,points;public string[] life,weapon,armor,statuses,weaponRarity,armorRarity;
            public static ExpeditionState Take(CharacterInstance[] party)=>new(){
                level=party.Select(h=>h.Level).ToArray(),hp=party.Select(h=>h.CurrentHealth).ToArray(),maxHp=party.Select(h=>Mathf.RoundToInt(h.Stats.MaxHealth.GetValue())).ToArray(),
                resource=party.Select(h=>h.ResourcePool.CurrentValue).ToArray(),maxResource=party.Select(h=>h.ResourcePool.MaxValue).ToArray(),
                points=party.Select(h=>h.SkillTreeProgress.Sum(t=>t.NodeInvestments.Sum(n=>n.Ranks))).ToArray(),life=party.Select(h=>h.LifeState.ToString()).ToArray(),
                weapon=party.Select(h=>h.Equipment.GetItem(EquipmentSlot.Weapon).Definition.Id).ToArray(),armor=party.Select(h=>h.Equipment.GetItem(EquipmentSlot.Armor).Definition.Id).ToArray(),
                weaponRarity=party.Select(h=>h.Equipment.GetItem(EquipmentSlot.Weapon).CurrentRarity.ToString()).ToArray(),armorRarity=party.Select(h=>h.Equipment.GetItem(EquipmentSlot.Armor).CurrentRarity.ToString()).ToArray(),
                statuses=party.Select(h=>string.Join("|",h.PassiveEffects.Where(PassiveEffectLifecycleSystem.IsActive).Select(e=>e.EffectId))).ToArray()};
        }
        [Serializable] sealed class MageSpellUse {public string skill;public int count;}
        [Serializable] sealed class RealisticExpedition
        {
            public int level,profile,seed,length,scheduled,fullScheduled,cleared,camps,downings,deaths,shieldAbsorbed,enemyBurnDamage,fallbackBattles;
            public string ordinaryRarity="Common";public int mageDamage,mageBurnDamage,mageManaSpent,mageManaGained,mageTurns;public List<MageSpellUse> mageSpells=new();
            public bool scouting,rolledUniqueRarity;public float upgrade,remainingHealthBeforeQuest,remainingHealth;public string dungeon,outcome;
            public List<HistoricalDrop> drops=new();public List<Result> battles=new();
            public List<ExpeditionState> before=new(),after=new();public ExpeditionState start,end;
            public List<string> campAt=new();
        }
        [Serializable] sealed class RealisticReport {public string policy,playerPolicyVersion;public List<RealisticExpedition> expeditions=new();}
        [Serializable] sealed class RealisticPoolRow {public string id;public string[] items;}
        [Serializable] sealed class RealisticHistoryRow {public int seed;public List<HistoricalDrop> drops;}
        [Serializable] sealed class RealisticInputReport {public RealisticPoolRow[] pools;public List<string> dungeons=new();public RealisticHistoryRow[] histories,rolledHistories;}
        [Serializable] sealed class RealisticConfigurationReport {public int entries;public List<string> differentIds=new(),differences=new(),balanceRelevantDifferences=new();}
        static LootBagItemDefinition[] realisticPools;
        static LootBagItemDefinition[] RealisticPools=>realisticPools??=PointCatalog.ItemDefinitions.OfType<LootBagItemDefinition>()
            .Where(b=>b.BossUniqueDrop?.EquipmentDefinitions?.Count==4 && b.BossUniqueDrop.EquipmentDefinitions.All(d=>d is UniqueEquipmentDefinition))
            .GroupBy(b=>string.Join("|",b.BossUniqueDrop.EquipmentDefinitions.Select(d=>d.Id).OrderBy(id=>id)))
            .Select(g=>g.OrderBy(b=>b.Id).First()).OrderBy(b=>b.Id).ToArray();
        static int RealisticProfile(int hero,int profile)
        {
            var primary=new[]{1,0,1,0};
            var owner=profile switch{4=>2,5=>0,6=>1,7=>3,_=>-1};
            if(owner>=0)return hero==owner?1-primary[hero]:primary[hero];
            return profile==0||profile==3?primary[hero]:profile==1?1-primary[hero]:2;
        }

        static List<HistoricalDrop> RealisticDropHistory(int level,int seed,bool rolledRarity=false)
        {
            var random=new System.Random(701018+seed);var history=new List<HistoricalDrop>();
            for(var boss=4;boss<=level;boss+=4)
            {
                var pool=RealisticPools[(boss/4+seed)%RealisticPools.Length];
                Assert.That(pool.BossUniqueDrop.DropChance,Is.EqualTo(.3f).Within(.00001f));
                if(random.NextDouble()>=pool.BossUniqueDrop.DropChance)continue;
                var item=pool.BossUniqueDrop.EquipmentDefinitions[random.Next(4)];
                var hero=Enumerable.Range(0,4).Single(i=>PointHeroDefinition(i).ClassData==item.RequiredClass);
                var tier=Math.Min(45,((boss-1)/10)*10+5);
                var rarity=RarityTier.Common;
                if(rolledRarity)
                {
                    // Production rarity roll, on a separate stream so counts,
                    // classes and boss histories remain paired with Common.
                    var generated=new EquipmentGenerationService(new System.Random(891018+seed*100+boss)).GenerateUnique(item,
                        PointCatalog.EquipmentPartDefinitions.Where(p=>p!=null&&(int)p.LevelBracket==5).ToArray());
                    Assert.That(generated,Is.Not.Null);rarity=generated.CurrentRarity;
                }
                history.Add(new HistoricalDrop{bossLevel=boss,hero=hero,slot=item.ItemType==ItemType.Weapon?0:1,material=tier,requiredLevel=tier+5,definition=item.Id,rarity=rarity.ToString()});
            }
            return history;
        }
        static PointGearStat[] HistoricalValues(int hero,int material,int slot)
        {
            var input=PointInput.rows.First(r=>r.level==material+5 && r.hero==hero && (slot==0?r.weaponTier:r.armorTier)==material);
            return slot==0?input.weapon:input.armor;
        }
        static void EquipHistoricalDrops(CharacterInstance hero,int index,HistoricalDrop[] history,float upgrade,BattleUnit[] enemies)
        {
            var savedHp=hero.CurrentHealth;var savedResource=hero.ResourcePool.CurrentValue;
            foreach(var slot in new[]{EquipmentSlot.Weapon,EquipmentSlot.Armor})
            {
                var best=hero.Equipment.GetItem(slot);
                new CharacterStatPipelineSystem().Refresh(hero);hero.CurrentHealth=Mathf.RoundToInt(hero.Stats.MaxHealth.GetValue());
                var dummy=new PointGearCase{hero=index,level=hero.Level};
                var initial=PointSnapshot(hero,dummy,0,0,upgrade,enemies);
                double Score(PointRow row)=>slot==EquipmentSlot.Weapon?-row.enemy:row.heroPhysical+row.heroMagical;
                var score=Score(initial);
                foreach(var drop in history.Where(h=>h.hero==index && h.slot==(slot==EquipmentSlot.Weapon?0:1) && h.requiredLevel<=hero.Level))
                {
                    var definition=PointCatalog.ItemDefinitions.OfType<UniqueEquipmentDefinition>().Single(d=>d.Id==drop.definition);
                    var candidate=PointItem(hero,HistoricalValues(index,drop.material,drop.slot),slot,upgrade,drop.material,true,definition);
                    candidate.CurrentRarity=(RarityTier)Enum.Parse(typeof(RarityTier),drop.rarity);candidate.RecalculateStatModifiers();
                    hero.Equipment.SetItem(slot,candidate);new CharacterStatPipelineSystem().Refresh(hero);
                    hero.CurrentHealth=Mathf.RoundToInt(hero.Stats.MaxHealth.GetValue());
                    var value=Score(PointSnapshot(hero,dummy,0,0,upgrade,enemies));
                    if(value>score){best=candidate;score=value;}
                }
                hero.Equipment.SetItem(slot,best);new CharacterStatPipelineSystem().Refresh(hero);
            }
            hero.CurrentHealth=Math.Min(savedHp,Mathf.RoundToInt(hero.Stats.MaxHealth.GetValue()));
            hero.ResourcePool.CurrentValue=Math.Min(savedResource,hero.ResourcePool.RecoveryLimit);
        }
        static void InvestExpeditionPoints(CharacterInstance hero,int profile)
        {
            var progression=new SkillTreeProgressionSystem();var trees=hero.ClassData.SkillTrees.Where(t=>t!=null).ToArray();
            int Rank(string tree,string node)=>hero.SkillTreeProgress.FirstOrDefault(t=>t.TreeId==tree)?.NodeInvestments.FirstOrDefault(n=>n.NodeId==node)?.Ranks??0;
            while(hero.AvailableSkillPoints>0)
            {
                var ordered=profile==2?trees.OrderBy(t=>SkillTreeProgressionSystem.GetInvestedRanks(hero,t.Id)):trees.OrderBy(t=>t==trees[profile]?0:1);
                var changed=false;
                foreach(var tree in ordered)
                {
                    foreach(var node in tree.Nodes.OrderBy(n=>Rank(tree.Id,n.Id)).ThenBy(n=>n.Tier).ThenBy(n=>n.Column))
                        if(progression.TryInvest(hero,tree.Id,node.Id)){changed=true;break;}
                    if(changed)break;
                }
                Assert.That(changed,Is.True,"Real earned points must be invested legally without resetting prior ranks.");
            }
        }
        static void SetExpeditionLoadout(CharacterInstance hero,int profile,bool defensiveMage=false)
        {
            // Reconfigure only outside combat, preserving legitimate capacity talents.
            var loadout=PointLoadout(hero,profile);
            if(hero.ClassData.Id=="class.mage")
            {
                // A mixed build retains its starting damage spell instead of
                // filling every slot with newly granted utility abilities.
                if(!loadout.Contains("skill.mage.fireball"))loadout[^1]="skill.mage.fireball";
                if(defensiveMage&&!loadout.Contains("skill.mage.shield"))
                {
                    var replace=Array.IndexOf(loadout,"skill.mage.fire_aura");
                    if(replace<0)replace=Array.IndexOf(loadout,"skill.mage.focus");
                    Assert.That(replace,Is.GreaterThan(0),"Controlled ablation replaces one utility slot, preserving basic and Fireball.");
                    loadout[replace]="skill.mage.shield";
                }
            }
            Assert.That(hero.ActiveSkillLoadout.TryInitialize(hero.GetKnownSkills().Select(s=>s.SkillId),hero.GetBasicAttackSkill().SkillId,
                loadout,hero.ActiveSkillLoadout.CopyCapacityModifiers()),Is.True);
        }
        static float ExpeditionUtilityScore(CombatSession session,BattleUnit actor,SkillData skill,int mode,IReadOnlyList<BattleUnit> targets,HashSet<string> used,bool lateFallback)
        {
            if(lateFallback)
            {
                // A prolonged healer/summon stalemate switches to damage rather
                // than perpetually valuing the old heuristic's flat control score.
                // This affects decisions only; production execution/costs stay intact.
                float correction=0;
                foreach(var target in targets.Where(t=>t.IsPlayerSide!=actor.IsPlayerSide && !StatusEffectSystem.IsInvisible(t)))
                    foreach(var original in skill.Effects.Where(e=>e.TargetModeIndex==mode))
                    {
                        var effect=PassiveEffectCombatExecutionSystem.ResolveEffect(actor,skill,original);
                        if(effect.EffectType==EffectType.ApplyStatus && !StatusEffectSystem.IsDamageOverTimeStatus(effect.StatusId)
                            && !StatusEffectSystem.HasStatus(target,effect.StatusId) && SkillExecutionSystem.CanPlanEffect(session,effect,actor,target))
                            correction-=StatusEffectSystem.CalculateStatusApplicationChance(actor,target,effect.StatusId,effect.BaseChance)
                                *(effect.StatusId==Godmist.Core.Constants.StatusEffectIds.Stun?500:150);
                    }
                return correction;
            }
            // Duration checks permit renewing expired buffs. Already active
            // unique buffs must not be recast or toggled off by the heuristic.
            if(skill.Effects.Where(e=>e.TargetModeIndex==mode).Any(e=>e.EffectType==EffectType.ApplyUniqueBuff
                && targets.Any(t=>t.IsPlayerSide==actor.IsPlayerSide && CombatDamageSystem.HasUniqueBuff(t,e.UniqueBuffType))))return 0;
            return PointUtilityScore(session,actor,skill,mode,targets,used);
        }

        [Test,Explicit("Read-only local expedition analysis; requires prepared equipment reference.")]
        public void AuditRealisticExpeditionInputs()
        {
            Directory.CreateDirectory(RealisticFolder);
            var settings=AssetDatabase.LoadAssetAtPath<GameDefines>("Assets/_Project/Resources/GameDefines.asset");
            Assert.That(settings,Is.Not.Null);
            var authored=new GameDefinesService(settings);var configProof=new RealisticConfigurationReport();
            object Read(GameDefinesService service,GameDefineEntry entry)=>entry.ValueType switch
            {
                GameDefineValueType.Integer=>service.GetInt(entry.Id),GameDefineValueType.Float=>service.GetFloat(entry.Id),
                GameDefineValueType.Boolean=>service.GetBool(entry.Id),GameDefineValueType.String=>service.GetString(entry.Id),_=>throw new ArgumentOutOfRangeException()
            };
            foreach(var entry in settings.Entries)
            {
                configProof.entries++;var actual=Read(GameDefinesRuntime.Current,entry);var expected=Read(authored,entry);
                if(!Equals(actual,expected)){configProof.differentIds.Add(entry.Id);configProof.differences.Add($"{entry.Id}: runtime={actual}, authored={expected}");}
            }
            bool OutsideModel(string id)=>id==GameDefineIds.EscapeContactLossTurns
                ||id=="upgrade_reforge.upgrade.failure_progress_loss_min"||id=="upgrade_reforge.upgrade.failure_progress_loss_max"
                ||id.StartsWith("economy.honor.tier_",StringComparison.Ordinal)&&id.EndsWith(".discount_percent",StringComparison.Ordinal);
            // Generic zero-fallback reads differ for some shop/upgrade entries.
            // This model does not buy gear, attempt upgrades or perform escape.
            configProof.balanceRelevantDifferences=configProof.differentIds.Where(id=>!OutsideModel(id)).ToList();
            File.WriteAllText(Path.Combine(RealisticFolder,"configuration.json"),JsonUtility.ToJson(configProof,true));
            Assert.That(configProof.balanceRelevantDifferences,Is.Empty,
                "Combat/growth/loot/XP configuration must match the authored game.");
            Assert.That(RealisticPools,Has.Length.EqualTo(4),"Use the four real boss unique pools, without legacy duplicate weighting.");
            foreach(var pool in RealisticPools)
            {
                Assert.That(pool.BossUniqueDrop.EquipmentDefinitions.Select(d=>d.RequiredClass.Id).Distinct().Count(),Is.EqualTo(4));
                Assert.That(pool.BossUniqueDrop.DropChance,Is.EqualTo(.3f).Within(.00001f));
            }
            var report=new RealisticInputReport{pools=RealisticPools.Select(p=>new RealisticPoolRow{id=p.Id,items=p.BossUniqueDrop.EquipmentDefinitions.Select(d=>d.Id).ToArray()}).ToArray(),
                histories=Enumerable.Range(0,4).Select(seed=>new RealisticHistoryRow{seed=seed,drops=RealisticDropHistory(48,seed)}).ToArray(),
                rolledHistories=Enumerable.Range(0,4).Select(seed=>new RealisticHistoryRow{seed=seed,drops=RealisticDropHistory(48,seed,true)}).ToArray()};
            foreach(var biome in new[]{"catacombs","forest"})
            {
                var definition=PointCatalog.DungeonDefinitions.Single(d=>d!=null&&d.Id=="dungeon."+biome+".expedition");
                Assert.That(definition.BossEncounterDefinition,Is.Null,"Regular expedition and boss expeditions are reported separately.");report.dungeons.Add(definition.Id);
            }
            File.WriteAllText(Path.Combine(RealisticFolder,"inputs.json"),JsonUtility.ToJson(report,true));
        }

        void RunRealisticExpeditions(int level,float upgrade,string biome,DungeonLength length,int profile,int seeds,string tag,bool rolledUniqueRarity=false,RarityTier ordinaryRarity=RarityTier.Common,int firstSeed=0,string outputFolder=null,bool holdRanged=false,bool fullOnly=false,int enemyStartX=7,bool repeatRescues=false,bool legacyPolicy=false,bool traceKnockdowns=false,bool previousRescuePolicy=false,Action<Engine,int> auditEngine=null,bool previousSupportPolicy=false,bool previousTeamPolicy=false,bool previousMagePolicy=false,bool previousCoordinationPolicy=false,bool previousBuildPolicy=false,bool previousPlanningPolicy=false,int planningLoadout=JointDefaultLoadout,Func<int,BattleUnit[]> equipmentReferenceFactory=null,Action<CharacterInstance> prepareHero=null)
        {
            var folder=outputFolder??RealisticFolder;
            var oldBuild=previousBuildPolicy||legacyPolicy||previousRescuePolicy||previousSupportPolicy||previousTeamPolicy||previousMagePolicy||previousCoordinationPolicy;
            Directory.CreateDirectory(folder);
            activeSettings=new Settings{length=length,encounterBalanceRoutes=true,progression=true,completionQuest=true};
            var definition=PointCatalog.DungeonDefinitions.Single(d=>d!=null&&d.Id=="dungeon."+biome+".expedition");
            var report=new RealisticReport{playerPolicyVersion=legacyPolicy?"legacy-v1":previousRescuePolicy?"party-v2":previousSupportPolicy?"party-v3-rescue":previousTeamPolicy?"party-v4-support-budget":previousMagePolicy?"party-v5-team":previousCoordinationPolicy?"party-v6-mage-recovery":oldBuild?"party-v7-coordination":previousPlanningPolicy?"party-v8-builds":"party-v9-joint-planning",policy="Current authored regular rosters and generated graph routes; full clear and scouting. Same character instances carry HP/resources/statuses; 18s real exploration between fights. Kits0/1/2, production40% recovery, no potions or free resets. Real XP plus length-scaled completion quest, earned SP invested legally after victories; starting gear stays fixed during expedition, no crafting or ordinary loot replacement. Mean weapon/armor; ordinary rarity recorded per expedition (Common baseline, controlled party-wide rarity sensitivity; reforge economy not simulated), unique rarity recorded per historical drop; normal quality plus historical boss uniques30% every4 levels, uniform class from four rotating actual pools, real unique passives. Unique selection conservative by first-contact attack/survival score, not optimal valuation of conditional perks. Future high-tier unique materials projected from worksheet (production unique generator currently5/15 only). Separate heuristic player policy, expired buffs renewable; production AI/combat, open12x10 battle arena (no map geometry/traps), no surprise. Budget exhaustion is failure, not defeat."};
            var randomState=UnityEngine.Random.state;
            try
            {
                foreach(var scouting in fullOnly?new[]{false}:new[]{false,true})for(var seed=firstSeed;seed<seeds;seed++)
                {
                    activeSettings.scouting=scouting;var dungeon=ExpeditionRoute(definition,seed,level);
                    var history=RealisticDropHistory(level,seed,rolledUniqueRarity).ToArray();var enemies=equipmentReferenceFactory?.Invoke(level)??PointEnemies(level);
                    var game=new GameSession(GameModeState.Exploration);
                    var party=Enumerable.Range(0,4).Select(index=>
                    {
                        var gear=PointInput.rows.Single(r=>r.level==level&&r.hero==index&&r.weaponTier==0&&r.armorTier==0);
                        var hero=PointHero(gear,RealisticProfile(index,profile),PointBudget(level),upgrade,enemies);
                        hero.CharacterId="real-exp-hero-"+index;
                        foreach(var item in hero.Equipment.GetEquippedItems()){item.CurrentRarity=ordinaryRarity;item.RecalculateStatModifiers();}
                        PointRefresh(hero);
                        EquipHistoricalDrops(hero,index,history,upgrade,enemies);PointRefresh(hero);
                        if(oldBuild)SetExpeditionLoadout(hero,RealisticProfile(index,profile),profile==3);else if(previousPlanningPolicy)SetBuildLoadout(hero,RealisticProfile(index,profile));else SetJointLoadout(hero,RealisticProfile(index,profile),planningLoadout);
                        prepareHero?.Invoke(hero);return hero;
                    }).ToArray();
                    foreach(var hero in party){Assert.That(game.TryRegisterCharacter(hero),Is.True);Assert.That(game.TryAddCharacterToActiveParty(hero.CharacterId),Is.True);}
                    game.TrySetActiveLeader(party[0].CharacterId);game.TrySetActiveDungeon(dungeon);
                    var camp=new DungeonCampService();Assert.That(camp.TryIssueSupplies(game,PointCatalog),Is.True);
                    Assert.That(party.Sum(h=>h.Inventory.Items.Count(DungeonCampService.IsCampingKit)),Is.EqualTo(dungeon.CampingKitAllowance));
                    var row=new RealisticExpedition{level=level,upgrade=upgrade,dungeon=definition.Id,profile=profile,seed=seed,length=(int)length,
                        scouting=scouting,rolledUniqueRarity=rolledUniqueRarity,ordinaryRarity=ordinaryRarity.ToString(),fullScheduled=lastFullScheduled,scheduled=dungeon.Floors.Sum(f=>f.Encounters.Count),outcome="Completed",drops=history.ToList(),start=ExpeditionState.Take(party)};
                    var exploration=new ExplorationStatusEffectSystem();UnityEngine.Random.InitState(81016+seed);
                    foreach(var floor in dungeon.Floors)
                    {
                        dungeon.TrySetCurrentFloorIndex(floor.FloorIndex);
                        foreach(var encounter in floor.Encounters.OrderBy(e=>e.Definition==definition.BossEncounterDefinition?1:0).ThenBy(e=>e.InstanceId,StringComparer.Ordinal))
                        {
                            if(row.cleared>0)
                            {
                                exploration.Advance(18,party.Select(h=>new DungeonTrapOccupant(new BattleUnit{Character=h,IsPlayerSide=true},null,Vector2Int.zero)).ToArray());
                                var helper=party.FirstOrDefault(h=>h.LifeState==CharacterLifeState.Alive);
                                if(helper!=null)foreach(var fallen in party.Where(h=>h.LifeState==CharacterLifeState.Downed))new CombatReviveSystem().TryReviveExploration(helper,fallen);
                                if(party.Sum(h=>h.CurrentHealth)/party.Sum(h=>h.Stats.MaxHealth.GetValue())<.6f
                                    ||party.Any(h=>h.LifeState==CharacterLifeState.Alive&&h.CurrentHealth/h.Stats.MaxHealth.GetValue()<.3f))
                                    if(camp.TryCamp(game,floor,out _)){row.camps++;row.campAt.Add(encounter.InstanceId);}
                            }
                            if(party.All(h=>h.LifeState!=CharacterLifeState.Alive)){row.outcome="TravelDefeat";break;}
                            row.before.Add(ExpeditionState.Take(party));
                            var session=new CombatSession{Grid=new BattleGrid(12,10),DungeonTheme=definition.Theme};
                            for(var index=0;index<4;index++)if(party[index].LifeState==CharacterLifeState.Alive)
                                session.Units.Add(new BattleUnit{Character=party[index],IsPlayerSide=true,Position=new GridPosition(index==0||index==3?3:2,3+index)});
                            for(var index=0;index<encounter.Enemies.Count;index++)
                            {
                                var entry=encounter.Enemies[index];var npc=new CharacterInstanceFactory().Create(entry.CharacterDefinition,entry.ClassData,level);npc.CharacterId="real-exp-enemy-"+row.cleared+"-"+index;
                                session.Rewards.EnemyRewards.Add(new CombatEnemyReward{EnemyInstanceId=npc.CharacterId,ExperienceLevel=level});
                                session.Units.Add(new BattleUnit{Character=npc,Position=new GridPosition(enemyStartX+index/6,2+index%6)});
                            }
                            var result=new Result{candidate="realistic-expedition",encounter=encounter.Definition.Id,level=level,seed=seed,build="profile-"+profile,
                                enemyCount=encounter.Enemies.Count,enemyIds=encounter.Enemies.Select(e=>e.CharacterDefinition.Id).ToArray(),
                                heroSkills=party.SelectMany(h=>h.GetActiveSkills()).Select(s=>s.SkillId).ToArray()};
                            var used=new HashSet<string>();
                            session.SkillResolved+=(unit,skill,id)=>{if(unit.IsPlayerSide)used.Add(unit.Character.CharacterId+":"+skill.SkillId);
                                if(unit.Character==party[2]){var count=row.mageSpells.FirstOrDefault(s=>s.skill==skill.SkillId);if(count==null){count=new MageSpellUse{skill=skill.SkillId};row.mageSpells.Add(count);}count.count++;}};
                            session.DamageApplied+=(source,target,amount,type,id)=>{if(!target.IsPlayerSide&&type==DamageType.Burn)row.enemyBurnDamage+=amount;
                                if(!target.IsPlayerSide&&source?.Character==party[2]){row.mageDamage+=amount;if(type==DamageType.Burn)row.mageBurnDamage+=amount;}};
                            var knockdowns=traceKnockdowns?new KnockdownTrace():null;
                            using(var engine=new Engine(session,result,knockdowns))
                            {
                                engine.AuditTurnLimit=600;engine.AuditLateKnifeExtraction=true;engine.UseLegacyPlayerPolicy=legacyPolicy;engine.UsePreviousRescuePolicy=previousRescuePolicy;engine.UsePreviousSupportPolicy=previousSupportPolicy;engine.UsePreviousTeamPolicy=previousTeamPolicy;engine.UsePreviousMagePolicy=previousMagePolicy;engine.UsePreviousCoordinationPolicy=previousCoordinationPolicy;engine.UsePreviousBuildPolicy=oldBuild;engine.UsePreviousPlanningPolicy=previousPlanningPolicy;engine.AuditHoldRangedPosition=holdRanged;engine.AuditRepeatRescues=repeatRescues;
                                var usedFallback=false;
                                engine.AdditionalAuditScore=(actor,skill,mode,targets)=>engine.BuildUtility(actor,skill,mode,targets,used,result.turns>=200);
                                engine.AuditUnitTurn=actor=>{if(actor.Character==party[2])row.mageTurns++;used.RemoveWhere(id=>id.StartsWith(actor.Character.CharacterId+":",StringComparison.Ordinal));
                                    if(result.turns>=200&&!usedFallback){usedFallback=true;row.fallbackBattles++;}};
                                engine.AuditFeedback=feedback=>{if(feedback.Target?.IsPlayerSide==true&&feedback.Type==CombatFeedbackType.ShieldLoss)row.shieldAbsorbed+=feedback.Amount;
                                    if(feedback.Target?.Character==party[2]){if(feedback.Type==CombatFeedbackType.ResourceSpend)row.mageManaSpent+=feedback.Amount;
                                        if(feedback.Type==CombatFeedbackType.ResourceGain)row.mageManaGained+=feedback.Amount;}};
                                auditEngine?.Invoke(engine,row.cleared);
                                engine.Run();
                                if(knockdowns!=null)
                                {
                                    knockdowns.Verify(result);
                                    File.WriteAllText(Path.Combine(folder,$"trace-{biome}-S{seed}-B{row.cleared}.json"),JsonUtility.ToJson(knockdowns,true));
                                }
                                engine.LeaveCombat();
                            }
                            if(result.outcome=="Victory")
                            {
                                var previousLevels=party.Select(h=>h.Level).ToArray();
                                Assert.That(new CombatRewardSystem().TryGrantVictoryRewards(game,session),Is.True);
                                for(var index=0;index<4;index++)if(party[index].LifeState!=CharacterLifeState.Dead)
                                {
                                    InvestExpeditionPoints(party[index],RealisticProfile(index,profile));
                                    if(party[index].Level!=previousLevels[index])
                                        EquipHistoricalDrops(party[index],index,history,upgrade,equipmentReferenceFactory?.Invoke(party[index].Level)??PointEnemies(party[index].Level));
                                    if(oldBuild)SetExpeditionLoadout(party[index],RealisticProfile(index,profile),profile==3);else if(previousPlanningPolicy)SetBuildLoadout(party[index],RealisticProfile(index,profile));else SetJointLoadout(party[index],RealisticProfile(index,profile),planningLoadout);
                                }
                            }
                            row.battles.Add(result);row.downings+=result.downings;row.after.Add(ExpeditionState.Take(party));
                            if(result.stop!=null){row.outcome="Budget";break;}
                            if(result.outcome!="Victory"){row.outcome="Defeat";break;}row.cleared++;
                        }
                        if(row.outcome!="Completed")break;
                    }
                    row.remainingHealthBeforeQuest=party.Where(h=>h.LifeState==CharacterLifeState.Alive).Sum(h=>Math.Max(0,h.CurrentHealth))/party.Sum(h=>h.Stats.MaxHealth.GetValue());
                    if(row.outcome=="Completed")new Godmist.Systems.Quests.QuestRewardSystem().GrantRadiantRewards(game,
                        new Godmist.Runtime.Quests.RadiantRewardOffer(level,CharacterProgressionSystem.GetStandardMissionExperience(level)*dungeon.CompletionRewardMultiplier,0,0,null));
                    row.end=ExpeditionState.Take(party);row.deaths=party.Count(h=>h.LifeState==CharacterLifeState.Dead);
                    row.remainingHealth=party.Where(h=>h.LifeState==CharacterLifeState.Alive).Sum(h=>Math.Max(0,h.CurrentHealth))/party.Sum(h=>h.Stats.MaxHealth.GetValue());
                    report.expeditions.Add(row);
                    File.WriteAllText(Path.Combine(folder,"expeditions-"+tag+".json"),JsonUtility.ToJson(report,true));
                }
            }
            finally{UnityEngine.Random.state=randomState;}
            Assert.That(report.expeditions.All(e=>e.outcome!="Budget"),Is.True,"Incomplete simulations cannot be reported as defeats.");
        }

        [TestCaseSource(nameof(RealisticMainCases)),Explicit("Opt-in local full expedition balance matrix.")]
        public void AuditRealisticExpeditionMain(int level,float upgrade,string biome)
            =>RunRealisticExpeditions(level,upgrade,biome,DungeonLength.Normal,0,level==48?4:2,$"main-L{level}-U{Mathf.RoundToInt(upgrade*100)}-{biome}");
        static IEnumerable<TestCaseData> RealisticMainCases()
        {
            foreach(var level in new[]{1,5,10,20,30,40,48,50})foreach(var upgrade in new[]{0f,.5f,1f})foreach(var biome in new[]{"catacombs","forest"})
                yield return new TestCaseData(level,upgrade,biome);
        }
        [TestCaseSource(nameof(RealisticLengthCases)),Explicit("Opt-in L48 short/long expedition depth check.")]
        public void AuditRealisticExpeditionLengths(int level,float upgrade,string biome,DungeonLength length)
            =>RunRealisticExpeditions(level,upgrade,biome,length,0,2,$"length-L{level}-U{Mathf.RoundToInt(upgrade*100)}-{biome}-{(int)length}");
        static IEnumerable<TestCaseData> RealisticLengthCases()
        {
            foreach(var level in new[]{1,5,10,20,30,40,48,50})foreach(var upgrade in new[]{0f,.5f,1f})foreach(var biome in new[]{"catacombs","forest"})foreach(var length in new[]{DungeonLength.Short,DungeonLength.Long})
                if(level==1||level==48||length==DungeonLength.Short&&upgrade==0)
                    yield return new TestCaseData(level,upgrade,biome,length);
        }
        [TestCaseSource(nameof(RealisticBuildCases)),Explicit("Opt-in alternate-build L48 expedition check.")]
        public void AuditRealisticExpeditionBuilds(float upgrade,string biome,int profile)
            =>RunRealisticExpeditions(48,upgrade,biome,DungeonLength.Normal,profile,2,$"build-L48-U{Mathf.RoundToInt(upgrade*100)}-{biome}-P{profile}");
        static IEnumerable<TestCaseData> RealisticBuildCases()
        {
            foreach(var upgrade in new[]{0f,.5f,1f})foreach(var biome in new[]{"catacombs","forest"})foreach(var profile in new[]{1,2})
                yield return new TestCaseData(upgrade,biome,profile);
        }
        [TestCaseSource(nameof(RealisticFinalCases)),Explicit("Opt-in controlled Mage Shield loadout ablation.")]
        public void AuditRealisticDefensiveMageAt48(float upgrade,string biome)
            =>RunRealisticExpeditions(48,upgrade,biome,DungeonLength.Normal,3,4,$"defense-L48-U{Mathf.RoundToInt(upgrade*100)}-{biome}");
        [TestCaseSource(nameof(RealisticFinalCases)),Explicit("Opt-in production boss-unique rarity sensitivity.")]
        public void AuditRealisticUniqueRarityAt48(float upgrade,string biome)
            =>RunRealisticExpeditions(48,upgrade,biome,DungeonLength.Normal,3,4,$"rarity-L48-U{Mathf.RoundToInt(upgrade*100)}-{biome}",true);
        [TestCaseSource(nameof(ReforgedExpeditionCases)),Explicit("Paired L48 full expeditions with ordinary reforge sensitivity, real trees and Mage resources.")]
        public void AuditReforgedExpeditionsAt48(float upgrade,string biome,RarityTier rarity,int profile)
            =>RunRealisticExpeditions(48,upgrade,biome,DungeonLength.Normal,profile,2,$"reforge-L48-U{Mathf.RoundToInt(upgrade*100)}-{biome}-{rarity}-P{profile}",true,rarity);
        static IEnumerable<TestCaseData> ReforgedExpeditionCases()
        {
            foreach(var upgrade in new[]{0f,.5f,1f})foreach(var biome in new[]{"catacombs","forest"})foreach(var rarity in new[]{RarityTier.Rare,RarityTier.Legendary,RarityTier.Godly})
                yield return new TestCaseData(upgrade,biome,rarity,0);
            foreach(var upgrade in new[]{0f,1f})foreach(var biome in new[]{"catacombs","forest"})foreach(var rarity in new[]{RarityTier.Legendary,RarityTier.Godly})
                yield return new TestCaseData(upgrade,biome,rarity,1);
        }
        [TestCaseSource(nameof(MageOnlyExpeditionCases)),Explicit("L48 Mage-only tree control: other three hero trees unchanged from profile0.")]
        public void AuditMageOnlyTreeAt48(float upgrade,string biome,RarityTier rarity)
            =>RunRealisticExpeditions(48,upgrade,biome,DungeonLength.Normal,4,2,$"mage-L48-U{Mathf.RoundToInt(upgrade*100)}-{biome}-{rarity}",rarity!=RarityTier.Common,rarity);
        static IEnumerable<TestCaseData> MageOnlyExpeditionCases()
        {
            foreach(var upgrade in new[]{0f,.5f,1f})foreach(var biome in new[]{"catacombs","forest"})
                yield return new TestCaseData(upgrade,biome,RarityTier.Common);
            foreach(var upgrade in new[]{0f,1f})foreach(var biome in new[]{"catacombs","forest"})foreach(var rarity in new[]{RarityTier.Legendary,RarityTier.Godly})
                yield return new TestCaseData(upgrade,biome,rarity);
        }
        [TestCaseSource(nameof(ExpandedL48Cases)),Explicit("Additional L48 seeds4-11 to check the nonmonotone small-sample upgrade result.")]
        public void AuditExpandedEquipmentAt48(float upgrade,string biome)
            =>RunRealisticExpeditions(48,upgrade,biome,DungeonLength.Normal,0,12,$"expanded-L48-U{Mathf.RoundToInt(upgrade*100)}-{biome}",false,RarityTier.Common,4);
        static IEnumerable<TestCaseData> ExpandedL48Cases()
        {
            foreach(var upgrade in new[]{0f,.5f,1f})foreach(var biome in new[]{"catacombs","forest"})
                yield return new TestCaseData(upgrade,biome);
        }
        static IEnumerable<TestCaseData> RealisticFinalCases()
        {
            foreach(var upgrade in new[]{0f,.5f,1f})foreach(var biome in new[]{"catacombs","forest"})yield return new TestCaseData(upgrade,biome);
        }
    }
}

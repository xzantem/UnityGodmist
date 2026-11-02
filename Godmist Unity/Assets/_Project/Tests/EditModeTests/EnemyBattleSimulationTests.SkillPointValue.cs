using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Items;
using Godmist.Data.Skills;
using Godmist.Data.World;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Items;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Systems.Items;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public partial class EnemyBattleSimulationTests
    {
        static string SkillPointFolder => Path.GetFullPath("../LocalHandoffs/skill-point-value-20261018");
        static GameContentCatalog PointCatalog => AssetDatabase.LoadAssetAtPath<GameContentCatalog>(Root+"World/GC_MainContentCatalog.asset");
        static readonly string[] SkillPointHeroes={"CD_Wladimir_Test","CD_Jerzy","CD_Mage","CD_Paladin"};
        static readonly string[] SkillPointEnemyIds={"enemy.catacombs.swordsman","enemy.catacombs.undead","enemy.catacombs.wraith","enemy.catacombs.occultist","enemy.catacombs.archer","enemy.catacombs.ghoul","enemy.catacombs.defender","enemy.forest.bandit_thug","enemy.forest.bandit_shooter","enemy.forest.elf_archer","enemy.forest.elf_poacher","enemy.forest.rabid_wolf","enemy.forest.elf_druid","enemy.forest.infected_tree"};
        [Serializable] sealed class PointGearStat { public string name;public float value; }
        [Serializable] sealed class PointGearCase
        {
            public int level,hero,weaponTier,armorTier;public double probability;
            public PointGearStat[] weapon,armor,normalWeapon,normalArmor;
        }
        [Serializable] sealed class PointGearInput {public PointGearCase[] rows;}
        [Serializable] sealed class PointNode {public string id,name,description,skill;public int tier,maxRanks;}
        [Serializable] sealed class PointTree {public string id,name,global;public List<PointNode> nodes=new();}
        [Serializable] sealed class PointInventory {public List<PointTree> trees=new();public string[] excludedLegacyTrees;}
        [Serializable] sealed class PointRow
        {
            public int level,hero,profile,points,weaponTier,armorTier;public float upgrade;public double probability;
            public double heroPhysical,heroMagical,enemy,enemyMelee;
            public float hp,min,max,physicalDefense,magicalDefense,critChance,critDamage,speed;
            public List<SkillTreeProgress> investments;
        }
        [Serializable] sealed class PointReport {public string policy;public List<PointRow> rows=new();}
        [Serializable] sealed class PointBattle
        {
            public int profile,seed,owner;public float upgrade;public string roster;public Result result;
            public float totalEnemyHealth;public int damageToEnemies;public string[][] loadouts;
            public int[] points;public string[] chosenNodes;
        }
        [Serializable] sealed class PointBattles {public string policy;public List<PointBattle> battles=new();}

        static PointGearInput pointGearInput;
        static CharacterDefinition[] pointHeroes,pointEnemies;
        static readonly Dictionary<(int,int,int),List<SkillTreeProgress>> pointPaths=new();
        static readonly Dictionary<(int,int,float,float,float,float,float,float,float),double> pointDamageCache=new();
        static PointGearInput PointInput => pointGearInput??=JsonUtility.FromJson<PointGearInput>(File.ReadAllText(Path.Combine(SkillPointFolder,"gear-input.json")));
        static CharacterDefinition PointHeroDefinition(int index) => (pointHeroes??=SkillPointHeroes.Select(id=>AssetDatabase.LoadAssetAtPath<CharacterDefinition>(Root+"Characters/"+id+".asset")).ToArray())[index];
        static CharacterDefinition[] PointEnemyDefinitions => pointEnemies??=SkillPointEnemyIds.Select(id=>PointCatalog.CharacterDefinitions.Single(d=>d.Id==id)).ToArray();
        static int PointBudget(int level)=>Enumerable.Range(2,Math.Max(0,level-1)).Sum(CharacterProgressionSystem.GetSkillPointsGrantedOnLevelUp);
        static List<SkillTreeProgress> CopyPointProgress(IEnumerable<SkillTreeProgress> source)=>source.Select(t=>new SkillTreeProgress{TreeId=t.TreeId,NodeInvestments=t.NodeInvestments.Select(n=>new SkillTreeNodeInvestment{NodeId=n.NodeId,Ranks=n.Ranks}).ToList()}).ToList();

        static List<SkillTreeProgress> PointProgress(int index,int budget,int profile)
        {
            if(profile<0 || budget==0)return new();
            if(pointPaths.TryGetValue((index,budget,profile),out var cached))return CopyPointProgress(cached);
            var definition=PointHeroDefinition(index);
            var hero=new CharacterInstanceFactory().Create(definition,definition.ClassData,50);
            hero.AvailableSkillPoints=budget;
            var trees=hero.ClassData.SkillTrees.Where(t=>t!=null).ToArray();
            Assert.That(trees,Has.Length.EqualTo(2),"Audit covers every currently attached tree, not legacy prototypes.");
            var progression=new SkillTreeProgressionSystem();
            int Rank(string t,string n)=>hero.SkillTreeProgress.FirstOrDefault(p=>p.TreeId==t)?.NodeInvestments.FirstOrDefault(p=>p.NodeId==n)?.Ranks??0;
            while(hero.AvailableSkillPoints>0)
            {
                var ordered=profile==2 ? trees.OrderBy(t=>SkillTreeProgressionSystem.GetInvestedRanks(hero,t.Id))
                    : trees.OrderBy(t=>t==trees[profile]?0:1);
                var invested=false;
                foreach(var tree in ordered)
                {
                    foreach(var node in tree.Nodes.OrderBy(n=>Rank(tree.Id,n.Id)).ThenBy(n=>n.Tier).ThenBy(n=>n.Column))
                        if(progression.TryInvest(hero,tree.Id,node.Id)){invested=true;break;}
                    if(invested)break;
                }
                Assert.That(invested,Is.True,"Budget must be spent legally; no manual node injection.");
            }
            cached=CopyPointProgress(hero.SkillTreeProgress);pointPaths[(index,budget,profile)]=cached;
            return CopyPointProgress(cached);
        }

        static EquipmentItemInstance PointItem(CharacterInstance hero,PointGearStat[] values,EquipmentSlot slot,float upgrade,int material,bool masterpiece,EquipmentItemDefinition definitionOverride=null)
        {
            var itemType=slot==EquipmentSlot.Weapon?ItemType.Weapon:ItemType.Armor;
            var definition=definitionOverride??PointCatalog.ItemDefinitions.OfType<EquipmentItemDefinition>().Where(d=>d.RequiredClass==hero.ClassData
                && d.ItemType==itemType && !(d is UniqueEquipmentDefinition)).OrderBy(d=>d.Id).First();
            var item=(EquipmentItemInstance)new ItemInstanceFactory().Create(definition);
            item.CurrentRarity=RarityTier.Common;item.UpgradeProgress=upgrade;
            item.ItemLevel=item.RequiredLevel=material+(masterpiece?5:0);item.Quality=masterpiece?EquipmentQuality.Masterpiece:EquipmentQuality.Normal;
            Assert.That(item.RequiredLevel,Is.LessThanOrEqualTo(hero.Level));
            item.BaseStatModifiers=values.Select(v=>new StatModifier{StatType=(CharacterStatType)Enum.Parse(typeof(CharacterStatType),v.name),Operation=StatModifierOperation.FlatBase,Value=v.value}).ToList();
            item.RecalculateStatModifiers();return item;
        }
        static int PointMaterial(int level)=>level<5?1:level<15?5:level<25?15:level<35?25:level<45?35:45;
        static void PointRefresh(CharacterInstance hero)
        {
            new CharacterStatPipelineSystem().Refresh(hero);
            hero.ResourcePool.CurrentValue=hero.ResourcePool.ResourceType==ResourceType.Mana?hero.ResourcePool.MaxValue:0;
            new CharacterStatPipelineSystem().Refresh(hero);
            hero.CurrentHealth=Mathf.RoundToInt(hero.Stats.MaxHealth.GetValue());
        }
        static BattleUnit PointUnit(CharacterInstance hero,bool player)=>new(){Character=hero,IsPlayerSide=player,Position=new GridPosition(player?2:6,2)};
        static DamageType PointDamageType(CharacterInstance c)=>c.GetBasicAttackSkill().Effects.First(e=>e.EffectType==EffectType.DealDamage).DamageType;

        // Fractional, first-contact x1 ATK index. Uses production stat pipeline,
        // hit/crit/multiplier/penetration resolvers. On-hit extra attacks, DoT,
        // healing, control and active rotation are measured separately below.
        static double PointExpected(BattleUnit source,BattleUnit target,DamageType kind,bool melee=false,string skillOverride=null)
        {
            var skill=source.Character.GetBasicAttackSkill();
            var attack=melee?AttackType.Melee:skill.ResolveAttackType(4);
            target.ScoutState.Facing=new Vector2Int(Math.Sign(source.Position.X-target.Position.X),Math.Sign(source.Position.Y-target.Position.Y));
            var session=new CombatSession();session.Units.Add(source);session.Units.Add(target);
            var ctx=new PassiveEffectCombatContext(session,source,target,skillId:skillOverride??skill.SkillId,skill:skill,
                targetsByModeIndex:new Dictionary<int,IReadOnlyList<BattleUnit>>{{0,new[]{target}}},attackType:attack,attackPosition:AttackPosition.Front,damageType:kind,distance:melee?1:4);
            var s=source.Character.Stats;var t=target.Character.Stats;
            var hit=kind==DamageType.Physical?CombatDamageSystem.ApplyHitChanceBonus(source.Character,CombatDamageSystem.CalculateHitChance(source.Character.WeaponAccuracy,100,t.Dodge.GetValue())):1f;
            var crit=kind==DamageType.Magical?MageCombatSystem.CalculateMagicCriticalChance(ctx):kind==DamageType.Physical?Mathf.Clamp01((PassiveEffectCombatExecutionSystem.ApplyCriticalChance(session,ctx,s.CritChance.GetValue())+ScoutCombatSystem.CriticalChance(ctx))
                *WarriorCombatSystem.CriticalMultiplier(source,target)*PaladinCombatSystem.CriticalMultiplier(source,target)):0f;
            var critical=Mathf.Max(1,s.CritDamage.GetValue()+ScoutCombatSystem.WeakPointCriticalDamage(target));
            var pre=ScoutCombatSystem.DamageMultiplier(ctx)*WarriorCombatSystem.DamageMultiplier(ctx)*MageCombatSystem.DamageMultiplier(ctx)*PaladinCombatSystem.DamageMultiplier(ctx);
            var vars=new CombatVariableSystem();
            var post=vars.GetMultiplier(source.Character,GameplayVariableType.DamageDealtPercent)*vars.GetMultiplier(target.Character,GameplayVariableType.DamageReceivedPercent)
                *ScoutCombatSystem.ReceivedDamageMultiplier(source,target)*WarriorCombatSystem.Received(session,source,target,kind)*MageCombatSystem.FireOutgoing(source,session);
            var penetration=(kind==DamageType.Physical?s.PhysicalDefensePenetration.GetValue():s.MagicalDefensePenetration.GetValue())
                +PassiveEffectCombatExecutionSystem.ResolveConditionalPenetration(source,target,kind);
            var defense=(kind==DamageType.Physical?t.PhysicalDefense.GetValue():t.MagicalDefense.GetValue())*WarriorCombatSystem.DefenseMultiplier(target,session)/(1+penetration);
            var low=Mathf.Max(0,Mathf.RoundToInt(s.MinAttack.GetValue()));var high=Mathf.Max(low,Mathf.RoundToInt(s.MaxAttack.GetValue()));
            var key=(low,high,hit,crit,critical,pre,post,defense,0f);
            if(pointDamageCache.TryGetValue(key,out var cached))return cached;
            int Mitigate(int raw)=>defense<=0?raw:Mathf.CeilToInt((float)raw*raw/(raw+defense));
            double total=0;
            for(var raw=low;raw<=high;raw++)
            {
                var value=Mathf.RoundToInt(raw*pre);
                total+=(1-crit)*Mathf.RoundToInt(Mitigate(value)*post)+crit*Mathf.RoundToInt(Mitigate(Mathf.CeilToInt(value*critical))*post);
            }
            cached=hit*total/(high-low+1);pointDamageCache[key]=cached;Assert.That(cached,Is.GreaterThan(0));return cached;
        }
        static PointRow PointSnapshot(CharacterInstance character,PointGearCase gear,int profile,int points,float upgrade,BattleUnit[] enemies)
        {
            var hero=PointUnit(character,true);var physical=new List<double>();var magical=new List<double>();
            foreach(var enemy in enemies)
            {
                var kind=PointDamageType(enemy.Character);
                (kind==DamageType.Physical?physical:magical).Add(character.CurrentHealth/PointExpected(enemy,hero,kind));
            }
            var ownKind=PointDamageType(character);var s=character.Stats;
            return new PointRow{level=gear.level,hero=gear.hero,profile=profile,points=points,upgrade=upgrade,probability=gear.probability,
                weaponTier=gear.weaponTier,armorTier=gear.armorTier,heroPhysical=physical.Average(),heroMagical=magical.Average(),
                enemy=enemies.Average(e=>e.Character.CurrentHealth/PointExpected(hero,e,ownKind)),
                enemyMelee=enemies.Average(e=>e.Character.CurrentHealth/PointExpected(hero,e,ownKind,true)),
                hp=s.MaxHealth.GetValue(),min=s.MinAttack.GetValue(),max=s.MaxAttack.GetValue(),physicalDefense=s.PhysicalDefense.GetValue(),magicalDefense=s.MagicalDefense.GetValue(),
                critChance=s.CritChance.GetValue(),critDamage=s.CritDamage.GetValue(),speed=s.Speed.GetValue(),investments=CopyPointProgress(character.SkillTreeProgress)};
        }
        static CharacterInstance PointHero(PointGearCase gear,int profile,int budget,float upgrade,BattleUnit[] enemies)
        {
            var definition=PointHeroDefinition(gear.hero);var hero=new CharacterInstanceFactory().Create(definition,definition.ClassData,gear.level);
            hero.SkillTreeProgress=PointProgress(gear.hero,budget,profile);
            var material=PointMaterial(gear.level);
            var normalWeapon=PointItem(hero,gear.normalWeapon,EquipmentSlot.Weapon,upgrade,material,false);
            var normalArmor=PointItem(hero,gear.normalArmor,EquipmentSlot.Armor,upgrade,material,false);
            hero.Equipment.SetItem(EquipmentSlot.Weapon,normalWeapon);hero.Equipment.SetItem(EquipmentSlot.Armor,normalArmor);PointRefresh(hero);
            if(gear.weaponTier>0)
            {
                var before=PointSnapshot(hero,gear,profile,budget,upgrade,enemies).enemy;
                hero.Equipment.SetItem(EquipmentSlot.Weapon,PointItem(hero,gear.weapon,EquipmentSlot.Weapon,upgrade,gear.weaponTier,true));PointRefresh(hero);
                if(PointSnapshot(hero,gear,profile,budget,upgrade,enemies).enemy>=before){hero.Equipment.SetItem(EquipmentSlot.Weapon,normalWeapon);PointRefresh(hero);}
            }
            if(gear.armorTier>0)
            {
                var before=PointSnapshot(hero,gear,profile,budget,upgrade,enemies);
                hero.Equipment.SetItem(EquipmentSlot.Armor,PointItem(hero,gear.armor,EquipmentSlot.Armor,upgrade,gear.armorTier,true));PointRefresh(hero);
                var after=PointSnapshot(hero,gear,profile,budget,upgrade,enemies);
                if(after.heroPhysical+after.heroMagical<=before.heroPhysical+before.heroMagical){hero.Equipment.SetItem(EquipmentSlot.Armor,normalArmor);PointRefresh(hero);}
            }
            return hero;
        }
        static BattleUnit[] PointEnemies(int level)=>PointEnemyDefinitions.Select(d=>PointUnit(new CharacterInstanceFactory().Create(d,d.ClassData,level),false)).ToArray();

        [Test]
        [Explicit("Local analysis; requires prepared read-only equipment reference and writes LocalHandoffs reports.")]
        public void AuditCompleteAttachedSkillTrees()
        {
            Directory.CreateDirectory(SkillPointFolder);var inventory=new PointInventory();
            foreach(var hero in Enumerable.Range(0,4).Select(PointHeroDefinition))
            foreach(var tree in hero.ClassData.SkillTrees)
                inventory.trees.Add(new PointTree{id=tree.Id,name=tree.DisplayName,global=tree.GlobalPassive.Description,
                    nodes=tree.Nodes.Select(n=>new PointNode{id=n.Id,name=n.DisplayName,description=n.Description,skill=n.GrantedSkill?.SkillId,tier=n.Tier,maxRanks=n.EffectiveMaxRanks}).ToList()});
            inventory.excludedLegacyTrees=AssetDatabase.FindAssets("t:SkillTreeDefinition").Select(g=>AssetDatabase.LoadAssetAtPath<SkillTreeDefinition>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(t=>inventory.trees.All(p=>p.id!=t.Id)).Select(t=>t.Id).ToArray();
            Assert.That(inventory.trees,Has.Count.EqualTo(8));Assert.That(inventory.trees.Sum(t=>t.nodes.Count),Is.EqualTo(160));
            foreach(var index in Enumerable.Range(0,4))foreach(var profile in new[]{0,1})
            {
                var progress=PointProgress(index,50,profile);var tree=PointHeroDefinition(index).ClassData.SkillTrees[profile];
                Assert.That(progress.Single(t=>t.TreeId==tree.Id).NodeInvestments.Count,Is.EqualTo(tree.Nodes.Count),tree.Id+" all nodes represented");
            }
            File.WriteAllText(Path.Combine(SkillPointFolder,"tree-inventory.json"),JsonUtility.ToJson(inventory,true));
        }

        [TestCase(1)][TestCase(5)][TestCase(10)][TestCase(15)][TestCase(20)][TestCase(25)]
        [TestCase(30)][TestCase(35)][TestCase(40)][TestCase(45)][TestCase(48)][TestCase(50)]
        [Explicit("Local analysis; requires prepared read-only equipment reference and writes LocalHandoffs reports.")]
        public void AuditSkillPointDurability(int level)
        {
            Directory.CreateDirectory(SkillPointFolder);var report=new PointReport{policy="Exact weighted rare-drop histories, legal budgets, all eight attached trees. Normalized first contact x1 ATK, skill accuracy100, front/ranged baseline; melee separately. Full mana, zero rage/momentum. No temporary buffs, DoT, on-hit extras, healing or control counted in direct indices. Same means of legal material combinations as previous analysis."};
            var enemies=PointEnemies(level);
            foreach(var gear in PointInput.rows.Where(r=>r.level==level))foreach(var upgrade in new[]{0f,.5f,1f})foreach(var profile in new[]{-1,0,1,2})
            {
                var budget=profile<0?0:PointBudget(level);var hero=PointHero(gear,profile,budget,upgrade,enemies);
                report.rows.Add(PointSnapshot(hero,gear,profile,budget,upgrade,enemies));
            }
            File.WriteAllText(Path.Combine(SkillPointFolder,$"durability-level{level}.json"),JsonUtility.ToJson(report,true));
            Assert.That(report.rows.All(r=>double.IsFinite(r.heroPhysical)&&double.IsFinite(r.heroMagical)&&double.IsFinite(r.enemy)),Is.True);
        }

        [Test]
        [Explicit("Local analysis; requires prepared read-only equipment reference and writes LocalHandoffs reports.")]
        public void AuditEveryPointAtFixedLevel48()
        {
            var report=new PointReport{policy="Ceteris paribus L48 ordinary mean gear, zero upgrade. Every legal point prefix for every class and primary/balanced path. Not a claim of constant per-point combat power."};
            var enemies=PointEnemies(48);
            foreach(var gear in PointInput.rows.Where(r=>r.level==48&&r.weaponTier==0&&r.armorTier==0))foreach(var profile in new[]{0,1,2})
            for(var budget=0;budget<=PointBudget(48);budget++)
            {
                var hero=PointHero(gear,profile,budget,0,enemies);
                report.rows.Add(PointSnapshot(hero,gear,profile,budget,0,enemies));
            }
            File.WriteAllText(Path.Combine(SkillPointFolder,"point-prefixes48.json"),JsonUtility.ToJson(report,true));
            Assert.That(report.rows,Has.Count.EqualTo(4*3*(PointBudget(48)+1)));
        }

        static string[] PointLoadout(CharacterInstance hero,int profile)
        {
            var known=hero.GetKnownSkills().ToArray();var basic=hero.GetBasicAttackSkill().SkillId;
            var chosen=new List<string>{basic};
            if(hero.ClassData.Id=="class.paladin")chosen.Add("skill.paladin.heal");
            var trees=hero.ClassData.SkillTrees;
            var ordered=profile<0?Array.Empty<SkillTreeDefinition>():profile==2?trees.ToArray():new[]{trees[profile]}.Concat(trees.Where(t=>t!=trees[profile])).ToArray();
            var grants=ordered.Select(tree=>tree.Nodes.OrderBy(n=>n.Tier).ThenBy(n=>n.Column)
                .Where(n=>n.GrantedSkill!=null && known.Any(s=>s.SkillId==n.GrantedSkill.SkillId)).Select(n=>n.GrantedSkill.SkillId).ToArray()).ToArray();
            if(profile==2)
            {
                for(var depth=0;depth<grants.Select(g=>g.Length).DefaultIfEmpty(0).Max();depth++)
                    foreach(var list in grants)if(depth<list.Length)chosen.Add(list[depth]);
            }
            else if(grants.Length>0)chosen.AddRange(grants[0]);
            chosen.AddRange(hero.Definition.StartingSkills.Select(s=>s.SkillId));
            foreach(var list in grants.Skip(profile==2?grants.Length:1))chosen.AddRange(list);
            chosen.AddRange(known.Select(s=>s.SkillId));
            return chosen.Where(id=>known.Any(s=>s.SkillId==id)).Distinct().Take(hero.ActiveSkillLoadout.Capacity).ToArray();
        }
        static float PointUtilityScore(CombatSession session,BattleUnit actor,SkillData skill,int mode,IReadOnlyList<BattleUnit> targets,HashSet<string> once)
        {
            var atk=(actor.Character.Stats.MinAttack.GetValue()+actor.Character.Stats.MaxAttack.GetValue())*.5f;
            if(once.Contains(actor.Character.CharacterId+":"+skill.SkillId))return 0;
            if(skill.SkillId==WarriorCombatSystem.DeathWish && actor.ActiveToggleSkillIds?.Contains(skill.SkillId)!=true)return atk*1.5f;
            float score=0;
            foreach(var target in targets)
            foreach(var effect in skill.Effects.Where(e=>e.TargetModeIndex==mode))
            {
                var ally=actor.IsPlayerSide==target.IsPlayerSide;
                if(effect.EffectType==EffectType.MarkForDestruction && !ally && target.WarriorState.OrderTurns==0)score+=atk*1.3f;
                if(effect.EffectType==EffectType.MarkWeakPoint && !ally && target.ScoutState.WeakPointTurns==0)score+=atk*.8f;
                if(effect.EffectType==EffectType.ProtectAlly && ally && target.WarriorState.ProtectionTurns==0)score+=atk*.8f;
                if(effect.EffectType==EffectType.PaladinBlessing && ally && !target.Character.PassiveEffects.Any(e=>e.EffectId=="paladin.blessing"))score+=atk;
                if(effect.EffectType==EffectType.ApplyUniqueBuff && ally && target==actor)score+=atk*.9f;
                if(effect.EffectType==EffectType.MageSacrifice && ally && target!=actor && target.Character.ClassData.Id!="class.mage")score+=atk*.7f;
                if(effect.EffectType==EffectType.MageRitual && ally && target.Character.CurrentHealth<target.Character.Stats.MaxHealth.GetValue()*.65f)score+=atk*.6f;
                if(effect.EffectType==EffectType.MageFireAura && ally)score+=atk*.8f*session.Units.Count(u=>u.IsPlayerSide!=actor.IsPlayerSide && Math.Abs(u.Position.X-target.Position.X)+Math.Abs(u.Position.Y-target.Position.Y)<=1);
                if(effect.EffectType==EffectType.ModifyStat && ally && effect.ModifierValue>0)score+=atk*.6f;
                if(effect.EffectType==EffectType.ApplyStatus && ally && effect.StatusId=="invisibility" && !StatusEffectSystem.IsInvisible(target))score+=atk;
            }
            return score;
        }

        [TestCase(0f)][TestCase(.5f)][TestCase(1f)]
        [Explicit("Bounded local balance benchmark, not a routine regression suite.")]
        public void AuditWholeTreeCombatAt48(float upgrade)
        {
            Directory.CreateDirectory(SkillPointFolder);
            var report=new PointBattles{policy="Paired L48 4v3 encounters, same two global loot histories and RNG seeds, all eight attached trees (primary0/primary1/balanced) vs no points. Owner=-1 develops the whole party; owner0-3 develops only that one hero, isolating the value of its48 points. Common mean weapon/armor, no jewelry, no potions or surprise. Production combat/AI and costs; legal loadouts with granted actives. Additional separate hero policy values buffs and control; utility casts at most once per actor/skill in a battle. Not optimized play, not full expeditions, not a universal per-point coefficient. Bounds inherited from Engine, and budget exhaustion is a failure, not a loss."};
            var randomState=UnityEngine.Random.state;
            try
            {
                for(var seed=0;seed<2;seed++)
                {
                    var tiers=new int[4,2];var loot=new System.Random(701018+seed);
                    for(var boss=4;boss<=48;boss+=4)
                    {
                        if(loot.NextDouble()>=.3)continue;
                        var cls=loot.Next(4);var slot=loot.Next(2);var tier=Math.Min(45,((boss-1)/10)*10+5);
                        if(tier+5<=48)tiers[cls,slot]=Math.Max(tiers[cls,slot],tier);
                    }
                    foreach(var roster in new[]{new[]{"enemy.catacombs.swordsman","enemy.catacombs.swordsman","enemy.catacombs.archer"},
                        new[]{"enemy.catacombs.occultist","enemy.catacombs.occultist","enemy.catacombs.wraith"}})
                    foreach(var profile in new[]{-1,0,1,2})
                    foreach(var owner in profile<0?new[]{-1}:new[]{-1,0,1,2,3})
                    {
                        var session=new CombatSession{Grid=new BattleGrid(10,9),DungeonTheme=DungeonTheme.Catacombs};
                        var audit=new PointBattle{profile=profile,seed=seed,owner=owner,upgrade=upgrade,roster=roster[0],loadouts=new string[4][],points=new int[4]};
                        for(var index=0;index<4;index++)
                        {
                            var gear=PointInput.rows.Single(r=>r.level==48 && r.hero==index && r.weaponTier==tiers[index,0] && r.armorTier==tiers[index,1]);
                            var ownProfile=owner>=0 && owner!=index?-1:profile;
                            var hero=PointHero(gear,ownProfile,ownProfile<0?0:PointBudget(48),upgrade,PointEnemies(48));hero.CharacterId="point-hero-"+index;
                            var loadout=PointLoadout(hero,ownProfile);
                            Assert.That(hero.ActiveSkillLoadout.TryInitialize(hero.GetKnownSkills().Select(s=>s.SkillId),hero.GetBasicAttackSkill().SkillId,loadout,hero.ActiveSkillLoadout.CopyCapacityModifiers()),Is.True);
                            audit.loadouts[index]=loadout;audit.points[index]=hero.SkillTreeProgress.Sum(t=>t.NodeInvestments.Sum(n=>n.Ranks));
                            session.Units.Add(new BattleUnit{Character=hero,IsPlayerSide=true,Position=new GridPosition(index==0||index==3?3:2,2+index)});
                        }
                        for(var index=0;index<roster.Length;index++)
                        {
                            var definition=PointEnemyDefinitions.Single(d=>d.Id==roster[index]);
                            var enemy=new CharacterInstanceFactory().Create(definition,definition.ClassData,48);enemy.CharacterId="point-enemy-"+index;
                            audit.totalEnemyHealth+=enemy.CurrentHealth;
                            session.Units.Add(new BattleUnit{Character=enemy,Position=new GridPosition(6,2+index)});
                        }
                        var result=audit.result=new Result{level=48,seed=seed,build="skill-point-"+profile,enemyCount=3};
                        var once=new HashSet<string>();
                        session.SkillResolved+=(u,s,id)=>{if(u.IsPlayerSide)once.Add(u.Character.CharacterId+":"+s.SkillId);};
                        session.DamageApplied+=(a,t,n,type,id)=>{if(!t.IsPlayerSide)audit.damageToEnemies+=n;};
                        UnityEngine.Random.InitState(81018+seed);
                        using(var engine=new Engine(session,result))
                        {engine.AdditionalAuditScore=(actor,skill,mode,targets)=>PointUtilityScore(session,actor,skill,mode,targets,once);engine.Run();}
                        report.battles.Add(audit);
                        File.WriteAllText(Path.Combine(SkillPointFolder,"combat48-"+Mathf.RoundToInt(upgrade*100)+".json"),JsonUtility.ToJson(report,true));
                    }
                }
            }
            finally{UnityEngine.Random.state=randomState;}
            Assert.That(report.battles.All(b=>b.result.stop==null),Is.True,"Audit budget exhaustion is not a gameplay result.");
        }

        [TestCase(0)][TestCase(1)][TestCase(2)][TestCase(3)]
        [Explicit("Local analysis; requires prepared read-only equipment reference and uses seeded damage samples.")]
        public void SkillPointProjectionAgreesWithProductionFirstContact(int index)
        {
            var gear=PointInput.rows.Single(r=>r.level==48&&r.hero==index&&r.weaponTier==0&&r.armorTier==0);
            var enemyDefinition=PointEnemyDefinitions.First(d=>d.Id=="enemy.catacombs.swordsman");
            var samples=new List<string>();var random=UnityEngine.Random.state;
            try
            {
                foreach(var profile in new[]{-1,0,1,2})
                {
                    var hero=PointHero(gear,profile,profile<0?0:48,0,PointEnemies(48));
                    var enemy=new CharacterInstanceFactory().Create(enemyDefinition,enemyDefinition.ClassData,48);
                    var source=PointUnit(hero,true);var target=PointUnit(enemy,false);
                    var kind=PointDamageType(hero);var expected=PointExpected(source,target,kind);
                    var damage=new CombatDamageSystem();double sum=0,squares=0;const int count=2000;
                    var skill=hero.GetBasicAttackSkill();UnityEngine.Random.InitState(901018+index*10+profile);
                    for(var sample=0;sample<count;sample++)
                    {
                        hero.PassiveEffects.Clear();enemy.PassiveEffects.Clear();PointRefresh(hero);PointRefresh(enemy);
                        source=PointUnit(hero,true);target=PointUnit(enemy,false);target.ScoutState.Facing=new Vector2Int(-1,0);
                        var session=new CombatSession{Grid=new BattleGrid(9,5)};session.Units.Add(source);session.Units.Add(target);
                        var ctx=new PassiveEffectCombatContext(session,source,target,skillId:skill.SkillId,skill:skill,
                            targetsByModeIndex:new Dictionary<int,IReadOnlyList<BattleUnit>>{{0,new[]{target}}},attackType:skill.ResolveAttackType(4),attackPosition:AttackPosition.Front,damageType:kind,distance:4);
                        var raw=UnityEngine.Random.Range(Mathf.RoundToInt(hero.Stats.MinAttack.GetValue()),Mathf.RoundToInt(hero.Stats.MaxAttack.GetValue())+1);
                        var actual=damage.ResolveAndApplyDamage(session,source,target,raw,kind,100f,passiveContext:ctx).HealthDamage;
                        sum+=actual;squares+=(double)actual*actual;
                    }
                    var mean=sum/count;var variance=Math.Max(0,squares/count-mean*mean);var tolerance=Math.Max(2,6*Math.Sqrt(variance/count));
                    samples.Add($"{index},{profile},{expected:R},{mean:R},{tolerance:R}");
                    Assert.That(mean,Is.EqualTo(expected).Within(tolerance),"Independent real damage resolution must agree with first-contact projection; subsequent procs excluded.");
                }
                File.WriteAllLines(Path.Combine(SkillPointFolder,"projection-check-"+index+".csv"),samples);
            }
            finally{UnityEngine.Random.state=random;}
        }
    }
}

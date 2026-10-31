using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Data.Quests;
using Godmist.Data.Items;
using Godmist.Data.World;
using Godmist.Runtime.Quests;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Systems.SaveLoad;
using Godmist.Systems.Quests;
using Godmist.Systems.Items;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
namespace Godmist.Tests.EditMode
{
    public class StableContentIdentityTests
    {
        private readonly List<UnityEngine.Object> objects=new();
        private T Create<T>() where T:ScriptableObject {var value=ScriptableObject.CreateInstance<T>();objects.Add(value);return value;}
        private static void Set(object instance,string name,object value) => instance.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(instance,value);
        private static void Property(object instance,string name,object value)
        {
            var type=instance.GetType();
            while(type!=null)
            {
                var property=type.GetProperty(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly);
                if(property!=null) {property.SetValue(instance,value);return;}
                type=type.BaseType;
            }
            throw new MissingMemberException(name);
        }
        [TearDown] public void TearDown(){foreach(var value in objects)UnityEngine.Object.DestroyImmediate(value);objects.Clear();}
        private static string Root => Path.GetFullPath(Path.Combine(Application.dataPath,"../.."));
        [Test,Explicit("Scoped serialized identity migration through Editor API, authorized C02/F03.")]
        public void AuthorIdentities()
        {
            Godmist.Editor.StableContentIdentitySetup.Apply(Path.Combine(Application.dataPath,"../Tools/ContentMigration/legacy-authoring-v0.1.0.json"),
                Path.Combine(Root,"LocalHandoffs/repair-batch-12-25-20261030/C02/authoring"));
            EveryPublishedIdentityAndConditionIsStable();
        }
        [Test] public void EveryPublishedIdentityAndConditionIsStable()
        {
            var count=0;
            foreach(var guid in AssetDatabase.FindAssets("t:SkillData",new[]{"Assets/_Project"}))
            {
                var skill=AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.That(skill.HasStableEffectIds,Is.True,skill.SkillId);
                Assert.DoesNotThrow(()=>SkillEffectIdentity.ExecutionOrder(skill),skill.SkillId);count++;
                foreach(var effect in skill.Effects)
                foreach(var condition in effect.Conditions.Where(SkillEffectIdentity.IsReference))
                    Assert.That(condition.ReferencedEffectId,Is.Not.Empty,skill.SkillId);
            }
            Assert.That(count,Is.GreaterThan(100));
            foreach(var guid in AssetDatabase.FindAssets("t:QuestDefinition",new[]{"Assets/_Project"}))
            {
                var quest=AssetDatabase.LoadAssetAtPath<QuestDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.That(quest.HasStableRewardIds,Is.True,quest.Id);
                Assert.DoesNotThrow(()=>Godmist.Editor.StableContentIdentitySetup.ValidateQuest(quest));
            }
        }
        [Test] public void InitializedAuthoringIsIdempotentAndDoesNotRewriteAssets()
        {
            EveryPublishedIdentityAndConditionIsStable();
            var paths=AssetDatabase.FindAssets("t:SkillData",new[]{"Assets/_Project"})
                .Concat(AssetDatabase.FindAssets("t:QuestDefinition",new[]{"Assets/_Project"})).Select(AssetDatabase.GUIDToAssetPath).Distinct();
            var before=paths.ToDictionary(path=>path,path=>File.ReadAllBytes(path));
            Godmist.Editor.StableContentIdentitySetup.Apply(Path.Combine(Application.dataPath,"../Tools/ContentMigration/legacy-authoring-v0.1.0.json"),
                Path.Combine(Root,"LocalHandoffs/repair-batch-12-25-20261030/C02/idempotence"));
            foreach(var pair in before)Assert.That(File.ReadAllBytes(pair.Key),Is.EqualTo(pair.Value),pair.Key);
        }
        [Test] public void ReorderPreservesIdentityReferencesAndDependencyExecutionOrder()
        {
            var skill=Create<SkillData>();Property(skill,"SkillId","test.identity");
            var first=new SkillEffectData();var second=new SkillEffectData();
            var condition=new SkillEffectConditionData();Set(condition,"conditionType",EffectConditionType.ReferencedEffectSucceeded);
            Set(condition,"referencedEffectId",first.StableId);Set(second,"conditions",new List<SkillEffectConditionData>{condition});
            Set(skill,"stableEffectIdsInitialized",true);Set(skill,"effects",new List<SkillEffectData>{first,second});
            var id=SkillEffectIdentity.RuntimeId(skill,0,"modify_stat");
            Assert.That(SkillEffectIdentity.ExecutionOrder(skill),Is.EqualTo(new[]{0,1}));
            Set(skill,"effects",new List<SkillEffectData>{second,new(),first});
            Assert.That(SkillEffectIdentity.RuntimeId(skill,2,"modify_stat"),Is.EqualTo(id));
            Assert.That(SkillEffectIdentity.ReferenceIndex(skill,condition),Is.EqualTo(2));
            Assert.That(SkillEffectIdentity.ExecutionOrder(skill),Is.EqualTo(new[]{2,0,1}));
        }
        [Test] public void MissingOrCyclicReferencesAndDuplicateIdsAreRejected()
        {
            var skill=Create<SkillData>();var effect=new SkillEffectData();Set(skill,"effects",new List<SkillEffectData>{effect});
            var condition=new SkillEffectConditionData();Set(condition,"conditionType",EffectConditionType.ReferencedEffectSucceeded);
            Set(condition,"referencedEffectId",effect.StableId);Set(effect,"conditions",new List<SkillEffectConditionData>{condition});
            Assert.Throws<InvalidOperationException>(()=>SkillEffectIdentity.ExecutionOrder(skill));
            Set(condition,"referencedEffectId","removed");Assert.Throws<InvalidOperationException>(()=>SkillEffectIdentity.ExecutionOrder(skill));
            Set(effect,"conditions",new List<SkillEffectConditionData>());Set(skill,"effects",new List<SkillEffectData>{effect,effect.DeepCopy()});
            Assert.Throws<InvalidOperationException>(()=>SkillEffectIdentity.ExecutionOrder(skill));
        }
        [Test] public void CrushingBlowUsesFrozenHistoricalIdentityNotCurrentSlot()
        {
            var skill=AssetDatabase.FindAssets("t:SkillData",new[]{"Assets/_Project"}).Select(g=>AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(g)))
                .Single(s=>s.SkillId=="skill.paladin.crushing_blow");
            var index=skill.Effects.ToList().FindIndex(e=>e.EffectType==EffectType.ModifyResistance);
            var canonical=LegacyContentIdentities.Effect("skill.paladin.crushing_blow.modify_resistance.2");
            Assert.That(canonical,Is.EqualTo(SkillEffectIdentity.RuntimeId(skill,index,"modify_resistance")));
            Assert.That(LegacyContentIdentities.Effect(canonical),Is.EqualTo(canonical));
        }
        [Test] public void RewardReorderInsertionRemovalAndRestoredClaimCannotDuplicate()
        {
            var definition=Create<QuestDefinition>();Property(definition,"Id","test.rewards");Property(definition,"StartStageId","stage");
            var stage=new QuestStageDefinition("stage","test",new QuestObjectiveDefinition());definition.Stages.Add(stage);
            QuestRewardDefinition Gold(int amount){var reward=new QuestRewardDefinition();Set(reward,"rewardType",QuestRewardType.Gold);Set(reward,"amount",amount);return reward;}
            var first=Gold(7);var second=Gold(11);stage.CompletionRewards.Add(first);
            var quest=new QuestInstance(definition);var game=new GameSession();var system=new QuestRewardSystem();
            Assert.That(system.GrantStageCompletionRewards(game,quest,stage).Gold,Is.EqualTo(7));
            stage.CompletionRewards.Insert(0,second);stage.CompletionRewards.Reverse();
            Assert.That(system.GrantStageCompletionRewards(game,quest,stage).Gold,Is.EqualTo(11));
            stage.CompletionRewards.Remove(first);Assert.That(system.GrantStageCompletionRewards(game,quest,stage).Gold,Is.Zero);
            var restored=new QuestInstance(definition);
            Assert.That(restored.TryRestoreState(quest.Status,quest.CurrentStageId,quest.IsHidden,new Dictionary<string,int>(),new Dictionary<string,string>(),quest.ChosenBranchIds,quest.ClaimedRewardKeys),Is.True);
            Assert.That(system.GrantStageCompletionRewards(game,restored,stage).Gold,Is.Zero);
        }
        [Test] public void LegacyCrushingBlowEffectAndSourceSurviveRestoreAndReapplicationWithoutDuplicating()
        {
            var characterDefinition=AssetDatabase.LoadAssetAtPath<Godmist.Data.Characters.CharacterDefinition>("Assets/_Project/ScriptableObjects/Characters/CD_Wladimir_Test.asset");
            var character=new Godmist.Systems.Characters.CharacterInstanceFactory().Create(characterDefinition,characterDefinition.ClassData,1);
            var skill=Catalog.SkillDefinitions.Single(value=>value.SkillId=="skill.paladin.crushing_blow");
            var old=new Godmist.Runtime.Effects.PassiveEffectRuntime("skill.paladin.crushing_blow.modify_resistance.2",
                new List<Godmist.Runtime.Effects.PassiveEffectSourceRuntime>{new(){SourceType=PassiveEffectSourceType.Skill,SourceId="skill.paladin.crushing_blow.effect.1",SourceCharacterId=character.CharacterId}},1,2,0,
                PassiveEffectLifetime.Timed,PassiveEffectTickPhase.TurnEnd);
            old.SetPayload(new Godmist.Runtime.Effects.ResistanceModifierPayload{ResistanceType=ResistanceType.Suppression,Value=-.3f});
            character.PassiveEffects.Add(old);var game=new GameSession();Assert.That(game.TryRegisterCharacter(character),Is.True);
            var index=skill.Effects.ToList().FindIndex(effect=>effect.EffectType==EffectType.ModifyResistance);
            for(var cycle=0;cycle<2;cycle++)
            {
                var snapshot=new GameSessionSaveSnapshotFactory().CreateSnapshot(game,"legacy effect identity");
                Assert.That(new GameSessionRestoreFactory().TryCreate(snapshot,Catalog,out var restored,out var diagnostic),Is.True,diagnostic?.ToString());game=restored;
                character=game.CharactersById[character.CharacterId];
                var expected=SkillEffectIdentity.RuntimeId(skill,index,"modify_resistance");
                var runtime=character.PassiveEffects.Single(effect=>effect.EffectId==expected);
                Assert.That(runtime.Sources.Single().SourceId,Is.EqualTo(SkillEffectIdentity.RuntimeId(skill,index,"effect")));
                var unit=new Godmist.Runtime.Combat.BattleUnit{Character=character};
                Assert.That(new Godmist.Systems.Combat.ModifyResistanceSystem().Apply(unit,unit,skill,skill.Effects[index],index),Is.True);
                Assert.That(character.PassiveEffects.Count(effect=>effect.EffectId==expected),Is.EqualTo(1));
            }
        }
        [Test] public void HistoricalRewardClaimSurvivesTwoFullSaveLoadCycles()
        {
            var definition=Catalog.QuestDefinitions.First(quest=>quest.Stages.Any(stage=>stage.CompletionRewards.Count>0));
            var stage=definition.Stages.First(value=>value.CompletionRewards.Count>0);
            var quest=new QuestInstance(definition);Assert.That(quest.TryClaimReward(stage.StageId+":0"),Is.True);
            var game=new GameSession();Assert.That(game.TryRegisterQuest(quest),Is.True);
            for(var cycle=0;cycle<2;cycle++)
            {
                var snapshot=new GameSessionSaveSnapshotFactory().CreateSnapshot(game,"historical quest claim");
                Assert.That(new GameSessionRestoreFactory().TryCreate(snapshot,Catalog,out var restored,out var diagnostic),Is.True,diagnostic?.ToString());game=restored;
                Assert.That(game.TryGetQuest(definition.Id,out quest),Is.True);
                Assert.That(quest.ClaimedRewardKeys,Does.Contain("reward:"+stage.CompletionRewards[0].RewardId));
                Assert.That(quest.ClaimedRewardKeys.Any(key=>key==stage.StageId+":0"),Is.False);
            }
        }
        [Test] public void DuplicateRewardIdentityFailsBeforePayment()
        {
            var definition=Create<QuestDefinition>();Property(definition,"Id","test.duplicate");Property(definition,"StartStageId","stage");
            var stage=new QuestStageDefinition();Property(stage,"StageId","stage");definition.Stages.Add(stage);
            var reward=new QuestRewardDefinition();Set(reward,"rewardType",QuestRewardType.Gold);Set(reward,"amount",10);
            stage.CompletionRewards.Add(reward);stage.CompletionRewards.Add(reward);
            var quest=new QuestInstance(definition);var game=new GameSession();
            Assert.Throws<InvalidOperationException>(()=>new QuestRewardSystem().GrantStageCompletionRewards(game,quest,stage));
            Assert.That(game.Gold,Is.Zero);Assert.That(quest.ClaimedRewardKeys,Is.Empty);
        }
        [Test] public void AllFrozenReleaseAliasesMigrateOnceIncludingAuraSourceSuffix()
        {
            var manifest=JsonUtility.FromJson<Godmist.Editor.StableContentIdentitySetup.Manifest>(File.ReadAllText(Path.Combine(Application.dataPath,"../Tools/ContentMigration/legacy-authoring-v0.1.0.json")));
            Assert.That(manifest.aliases.Length,Is.EqualTo(201));
            foreach(var alias in manifest.aliases)
            {
                var suffix=alias.oldId.EndsWith(".",StringComparison.Ordinal)?"caster.stable.id":"";
                Assert.That(LegacyContentIdentities.Effect(alias.oldId+suffix),Is.EqualTo(alias.newId+suffix),alias.oldId);
                Assert.That(LegacyContentIdentities.Effect(alias.newId+suffix),Is.EqualTo(alias.newId+suffix));
            }
        }
        [Test] public void HistoricalRewardMigrationFailsClosedForUnknownOldIndexAndIsIdempotent()
        {
            var json=File.ReadAllText(Path.Combine(Application.dataPath,"../Tools/ContentMigration/legacy-authoring-v0.1.0.json"));
            var manifest=JsonUtility.FromJson<Godmist.Editor.StableContentIdentitySetup.Manifest>(json);
            foreach(var quest in manifest.quests)
            foreach(var stage in quest.stages)
            foreach(var reward in stage.rewards)
            {
                Assert.That(LegacyContentIdentities.TryRewards(quest.questId,new[]{stage.stageId+":"+reward.index},out var keys),Is.True);
                Assert.That(keys,Is.EqualTo(new[]{"reward:"+reward.id}));
                Assert.That(LegacyContentIdentities.TryRewards(quest.questId,keys,out var twice),Is.True);Assert.That(twice,Is.EqualTo(keys));
            }
            Assert.That(LegacyContentIdentities.TryRewards("unknown",new[]{"stage:0"},out _),Is.False);
        }
        private GameContentCatalog Catalog => AssetDatabase.LoadAssetAtPath<GameContentCatalog>("Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
        private QuestRewardDefinition ItemReward(ItemDefinition definition,int amount)
        {var reward=new QuestRewardDefinition();Set(reward,"rewardType",QuestRewardType.Item);Set(reward,"itemDefinition",definition);Set(reward,"amount",amount);return reward;}
        [TestCase(1)][TestCase(3)] public void ItemAmountCreatesDistinctInstancesAndPaysOnlyOnce(int amount)
        {
            var definition=Create<QuestDefinition>();Property(definition,"Id","test.amount");Property(definition,"StartStageId","stage");
            var stage=new QuestStageDefinition("stage","test",new QuestObjectiveDefinition());definition.Stages.Add(stage);
            var material=Catalog.ItemDefinitions.First(item=>item.ItemType==ItemType.Material);stage.CompletionRewards.Add(ItemReward(material,amount));
            var quest=new QuestInstance(definition);var session=new GameSession();var system=new QuestRewardSystem(nextContentCatalog:Catalog,nextRandom:new System.Random(1));
            var result=system.GrantStageCompletionRewards(session,quest,stage);
            Assert.That(result.Items.Count,Is.EqualTo(amount));Assert.That(result.Items.Select(item=>item.InstanceId).Distinct().Count(),Is.EqualTo(amount));
            Assert.That(system.GrantStageCompletionRewards(session,quest,stage).Items,Is.Empty);
        }
        [TestCase(1)][TestCase(8)][TestCase(14)][TestCase(50)] public void EquipmentUsesNearestCompleteTierToQuestLevel(int level)
        {
            var definition=Catalog.ItemDefinitions.OfType<EquipmentItemDefinition>().First(e=>e.ItemType==ItemType.Weapon && e is not UniqueEquipmentDefinition && !e.IsStarterEquipment);
            var kinds=EquipmentGenerationService.GetRequiredKinds(definition);
            var expected=Catalog.EquipmentPartDefinitions.Where(part=>part.RequiredClass==definition.RequiredClass&&!part.IsStarterOnly)
                .GroupBy(part=>part.LevelBracket).Where(group=>kinds.All(kind=>group.Any(part=>part.Kind==kind)))
                .OrderBy(group=>Math.Abs((int)group.Key-level)).ThenBy(group=>(int)group.Key).First().Key;
            var result=QuestRewardItemGenerator.Generate(ItemReward(definition,2),level,Catalog,new System.Random(1),new ItemInstanceFactory());
            foreach(var item in result.Cast<EquipmentItemInstance>())
            {Assert.That(item.ItemLevel,Is.EqualTo((int)expected));Assert.That(item.PartIds.Count,Is.EqualTo(3));Assert.That(item.BaseStatModifiers,Is.Not.Empty);}
        }
        [Test] public void GeneratedBagContentsAndClaimSurviveTwoCompleteSaveLoadCyclesWithoutReroll()
        {
            var catalog=UnityEngine.Object.Instantiate(Catalog);objects.Add(catalog);
            var bag=Create<LootBagItemDefinition>();Property(bag,"Id","test.reward.bag");Property(bag,"ItemType",ItemType.Lootbag);
            var bracket=new LootBagBracketData();Property(bracket,"Bracket",LootLevelBracketResolver.Resolve(14));
            var pool=new LootBagPoolData();var entry=new LootBagPoolEntryData();
            Property(entry,"ItemDefinition",catalog.ItemDefinitions.First(item=>item.ItemType==ItemType.Material));
            Property(entry,"Amount",new Godmist.Core.Types.IntRange(3,3));pool.Entries.Add(entry);bracket.Pools.Add(pool);bag.Brackets.Add(bracket);
            catalog.ItemDefinitions.Add(bag);
            var definition=Create<QuestDefinition>();Property(definition,"Id","test.reward.save");Property(definition,"StartStageId","stage");Set(definition,"level",14);
            var stage=new QuestStageDefinition();Property(stage,"StageId","stage");definition.Stages.Add(stage);catalog.QuestDefinitions.Add(definition);
            stage.CompletionRewards.Add(ItemReward(bag,2));
            var quest=new QuestInstance(definition);var game=new GameSession();Assert.That(game.TryRegisterQuest(quest),Is.True);
            var system=new QuestRewardSystem(nextContentCatalog:catalog,nextRandom:new System.Random(123));
            Assert.That(system.GrantStageCompletionRewards(game,quest,stage).Items.Count,Is.EqualTo(2));
            var ids=game.PendingLoot.Cast<LootBagItemInstance>().SelectMany(item=>item.Contents).Select(item=>item.InstanceId).ToArray();
            Assert.That(ids.Length,Is.EqualTo(6));
            for(var cycle=0;cycle<2;cycle++)
            {
                var snapshot=new GameSessionSaveSnapshotFactory().CreateSnapshot(game,"generated reward cycle");
                Assert.That(new GameSessionRestoreFactory().TryCreate(snapshot,catalog,out var restored,out var diagnostic),Is.True,diagnostic?.ToString());
                game=restored;
                Assert.That(game.PendingLoot.Cast<LootBagItemInstance>().Select(item=>item.ItemLevel),Is.All.EqualTo(14));
                Assert.That(game.PendingLoot.Cast<LootBagItemInstance>().SelectMany(item=>item.Contents).Select(item=>item.InstanceId),Is.EqualTo(ids));
                Assert.That(game.TryGetQuest(definition.Id,out quest),Is.True);
                Assert.That(system.GrantStageCompletionRewards(game,quest,stage).Items,Is.Empty);
            }
        }
        [Test] public void InvalidItemGenerationCannotClaimTheReward()
        {
            var definition=Create<QuestDefinition>();Property(definition,"Id","test.invalid");Property(definition,"StartStageId","stage");
            var stage=new QuestStageDefinition("stage","test",new QuestObjectiveDefinition());definition.Stages.Add(stage);
            var equipment=Catalog.ItemDefinitions.OfType<EquipmentItemDefinition>().First(e=>!e.IsStarterEquipment && e is not UniqueEquipmentDefinition);
            stage.CompletionRewards.Add(ItemReward(equipment,2));var quest=new QuestInstance(definition);
            Assert.Throws<InvalidOperationException>(()=>new QuestRewardSystem().GrantStageCompletionRewards(new GameSession(),quest,stage));
            Assert.That(quest.ClaimedRewardKeys,Is.Empty);
        }
    }
}

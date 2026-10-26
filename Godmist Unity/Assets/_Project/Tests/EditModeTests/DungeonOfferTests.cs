using System;
using System.Linq;
using System.Reflection;
using Godmist.Core.Serialization;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using Godmist.Data.World;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Systems.Dungeons;
using Godmist.Systems.Characters;
using Godmist.Systems.Debugging;
using Godmist.Systems.SaveLoad;
using Godmist.Views.Dungeons;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
namespace Godmist.Tests.EditMode
{
 public class DungeonOfferTests
 {
  DungeonDefinition definition;
  [SetUp] public void Setup(){definition=ScriptableObject.CreateInstance<DungeonDefinition>();Set(definition,"Id","test.dungeon");Set(definition,"IsProcedural",true);Set(definition,"IsAvailableAsOffer",true);}
  [TearDown] public void Cleanup()=>UnityEngine.Object.DestroyImmediate(definition);
  static void Set(object obj,string property,object value)=>obj.GetType().GetField("<"+property+">k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(obj,value);
  GameSession Session()=>new(campaignId:"offers.campaign");
  [Test] public void SetDangerCommandUnlocksBossOfferWithoutChangingCharacterLevel()
  {
   var boss=ScriptableObject.CreateInstance<DungeonDefinition>();
   var catalog=ScriptableObject.CreateInstance<GameContentCatalog>();
   try {
    Set(boss,"Id","boss.dungeon");Set(boss,"IsProcedural",true);Set(boss,"IsAvailableAsOffer",true);
    boss.FixedOfferLevels.Add(10);
    catalog.DungeonDefinitions.Add(definition);catalog.DungeonDefinitions.Add(boss);
    var session=Session();session.TryRegisterCharacter(new CharacterInstance{CharacterId="hero",Level=8});
    session.PlayerParty.TryAddActiveMember("hero");
    Assert.That(DungeonOfferSystem.EnsureOffers(session,catalog.DungeonDefinitions),Is.True);
    var service=new DevConsoleCommandService();
    var result=service.Execute("setdanger catacombs 100",session,null,catalog);
    Assert.That(result.Success,Is.True,result.Message);
    Assert.That(session.CharactersById["hero"].Level,Is.EqualTo(8));
    Assert.That(session.DungeonOffers.GetDanger(DungeonTheme.Catacombs),Is.EqualTo(100));
    Assert.That(session.DungeonOffers.Offers[0].DefinitionId,Is.EqualTo("boss.dungeon"));
    Assert.That(session.DungeonOffers.Offers[0].FixedLevel,Is.EqualTo(10));
    var invalid=service.Execute("setdanger catacombs 101",session,null,catalog);
    Assert.That(invalid.Success,Is.False);
    Assert.That(session.DungeonOffers.GetDanger(DungeonTheme.Catacombs),Is.EqualTo(100));
   } finally {UnityEngine.Object.DestroyImmediate(catalog);UnityEngine.Object.DestroyImmediate(boss);}
  }
  [TestCase(1)][TestCase(2)][TestCase(10)][TestCase(49)][TestCase(50)]
  public void SixOffersRespectAverageBoundsAndBothDirections(int average)
  {
   var session=Session();Assert.That(DungeonOfferSystem.EnsureOffers(session,new[]{definition}),Is.True);
   var offers=session.DungeonOffers.Offers;Assert.That(offers.Count,Is.EqualTo(6));Assert.That(offers.Select(o=>o.Seed).Distinct().Count(),Is.EqualTo(6));
   var levels=offers.Select(o=>DungeonOfferSystem.OfferLevel(average,o.LevelOffset)).ToArray();
   Assert.That(levels[0],Is.EqualTo(average));Assert.That(levels.Count(l=>l==average),Is.EqualTo(1));
   foreach(var level in levels){Assert.That(level,Is.InRange(1,50));Assert.That(Math.Abs(level-average),Is.LessThanOrEqualTo(3));}
   if(average>1)Assert.That(levels.Any(l=>l<average),Is.True);
   if(average<50)Assert.That(levels.Any(l=>l>average),Is.True);
  }
  [Test] public void AverageUsesActiveMembersAndRoundsHalfUp()
  {
   var s=Session();s.TryRegisterCharacter(new CharacterInstance{CharacterId="a",Level=10});s.TryRegisterCharacter(new CharacterInstance{CharacterId="b",Level=11});s.TryRegisterCharacter(new CharacterInstance{CharacterId="bench",Level=50});
   s.PlayerParty.TryAddActiveMember("a");s.PlayerParty.TryAddActiveMember("b");s.PlayerParty.TryAddBenchMember("bench");
   Assert.That(DungeonOfferSystem.PartyLevel(s),Is.EqualTo(11));
   DungeonOfferSystem.EnsureOffers(s,new[]{definition});var seeds=s.DungeonOffers.Offers.Select(o=>o.Seed).ToArray();s.CharactersById["a"].Level=20;
   DungeonOfferSystem.EnsureOffers(s,new[]{definition});CollectionAssert.AreEqual(seeds,s.DungeonOffers.Offers.Select(o=>o.Seed));Assert.That(DungeonOfferSystem.PartyLevel(s),Is.EqualTo(16));
  }
  [Test] public void DangerMilestoneUsesHighestCharacterIncludingBench()
  {
   var boss=ScriptableObject.CreateInstance<DungeonDefinition>();
   try {
    Set(boss,"Id","boss.dungeon");Set(boss,"IsProcedural",true);Set(boss,"IsAvailableAsOffer",true);
    boss.FixedOfferLevels.AddRange(new[]{10,20});
    var session=Session();
    session.TryRegisterCharacter(new CharacterInstance{CharacterId="active",Level=4});
    session.TryRegisterCharacter(new CharacterInstance{CharacterId="bench",Level=10});
    session.PlayerParty.TryAddActiveMember("active");session.PlayerParty.TryAddBenchMember("bench");
    Assert.That(DungeonOfferSystem.EnsureOffers(session,new[]{definition,boss}),Is.True);
    Assert.That(session.DungeonOffers.GetDanger(DungeonTheme.Catacombs),Is.EqualTo(100));
    Assert.That(session.DungeonOffers.Offers[0].FixedLevel,Is.EqualTo(10));
    Assert.That(DungeonOfferSystem.OfferLevel(DungeonOfferSystem.PartyLevel(session),session.DungeonOffers.Offers[0]),Is.EqualTo(10));
    Assert.That(session.DungeonOffers.Offers[1].LevelOffset,Is.Zero);
   }finally{UnityEngine.Object.DestroyImmediate(boss);}
  }
  [Test] public void CompletedOtherThemeRaisesDangerAndUnlocksBossAtMaximum()
  {
   var forest=ScriptableObject.CreateInstance<DungeonDefinition>();
   var boss=ScriptableObject.CreateInstance<DungeonDefinition>();
   try {
    Set(forest,"Id","forest");Set(forest,"IsProcedural",true);Set(forest,"IsAvailableAsOffer",true);
    Set(forest,"Theme",DungeonTheme.Forest);
    Set(boss,"Id","forest.boss");Set(boss,"IsProcedural",true);Set(boss,"IsAvailableAsOffer",true);
    Set(boss,"Theme",DungeonTheme.Forest);boss.FixedOfferLevels.Add(10);
    var session=Session();session.TryRegisterCharacter(new CharacterInstance{CharacterId="hero",Level=8});
    session.PlayerParty.TryAddActiveMember("hero");
    session.DungeonOffers.SetDanger(DungeonTheme.Forest,90);
    DungeonOfferSystem.CompleteExpedition(session,new DungeonInstance(definition,42,8),new[]{definition,forest,boss});
    Assert.That(session.DungeonOffers.GetDanger(DungeonTheme.Forest),Is.EqualTo(100));
    Assert.That(session.DungeonOffers.BossOpportunities.Single().DefinitionId,Is.EqualTo("forest.boss"));
    Assert.That(session.DungeonOffers.BossOpportunities.Single().Level,Is.EqualTo(10));
   }finally{UnityEngine.Object.DestroyImmediate(forest);UnityEngine.Object.DestroyImmediate(boss);}
  }
  [Test] public void DangerDoesNotGrowBeforeLevelEight()
  {
   var forest=ScriptableObject.CreateInstance<DungeonDefinition>();
   try {
    Set(forest,"Id","forest");Set(forest,"IsProcedural",true);Set(forest,"IsAvailableAsOffer",true);
    Set(forest,"Theme",DungeonTheme.Forest);
    var session=Session();session.TryRegisterCharacter(new CharacterInstance{CharacterId="hero",Level=7});
    session.PlayerParty.TryAddActiveMember("hero");
    DungeonOfferSystem.CompleteExpedition(session,new DungeonInstance(definition,42,7),new[]{definition,forest});
    Assert.That(session.DungeonOffers.GetDanger(DungeonTheme.Forest),Is.Zero);
    session.CharactersById["hero"].Level=8;
    DungeonOfferSystem.CompleteExpedition(session,new DungeonInstance(definition,43,8),new[]{definition,forest});
    Assert.That(session.DungeonOffers.GetDanger(DungeonTheme.Forest),Is.InRange(10,20));
   }finally{UnityEngine.Object.DestroyImmediate(forest);}
  }
  [Test] public void BossStartedInOldSaveDoesNotUnlockItselfAgainOnExit()
  {
   var boss=ScriptableObject.CreateInstance<DungeonDefinition>();
   try {
    Set(boss,"Id","boss.dungeon");Set(boss,"IsProcedural",true);Set(boss,"IsAvailableAsOffer",true);
    boss.FixedOfferLevels.Add(10);
    var session=Session();session.TryRegisterCharacter(new CharacterInstance{CharacterId="hero",Level=10});
    session.PlayerParty.TryAddActiveMember("hero");
    DungeonOfferSystem.CompleteExpedition(session,new DungeonInstance(boss,42,10),new[]{definition,boss});
    Assert.That(session.DungeonOffers.HighestMilestoneProcessed,Is.EqualTo(10));
    Assert.That(session.DungeonOffers.GetDanger(DungeonTheme.Catacombs),Is.Zero);
    Assert.That(DungeonOfferSystem.EnsureOffers(session,new[]{definition,boss}),Is.True);
    Assert.That(session.DungeonOffers.Offers.All(offer=>offer.DefinitionId==definition.Id),Is.True);
   }finally{UnityEngine.Object.DestroyImmediate(boss);}
  }
  [TestCase(0,"Low")][TestCase(39,"Low")][TestCase(40,"Medium")]
  [TestCase(69,"Medium")][TestCase(70,"High")][TestCase(99,"High")][TestCase(100,"Max")]
  public void DangerBandsUseApprovedThresholds(int value,string expected) =>
   Assert.That(DungeonOfferSystem.DangerBand(value),Is.EqualTo(expected));
  [Test] public void ReopenAndJsonRoundTripKeepSeedsOffsetsAndSelection()
  {
   var s=Session();DungeonOfferSystem.EnsureOffers(s,new[]{definition});s.DungeonOffers.TrySelect(4);
   s.DungeonOffers.SetDanger(DungeonTheme.Forest,75);s.DungeonOffers.MarkMilestoneProcessed(10);
   var catalog=ScriptableObject.CreateInstance<GameContentCatalog>();catalog.DungeonDefinitions.Add(definition);
   try {
    var snapshot=new GameSessionSaveSnapshotFactory().CreateSnapshot(s,"offers");
    var json=new GameSaveJsonSerializer();Assert.That(json.TryDeserialize(json.Serialize(snapshot),out var decoded),Is.True);
    Assert.That(new GameSessionRestoreFactory().TryCreate(decoded,catalog,out var loaded),Is.True);
    Assert.That(loaded.DungeonOffers.SelectedIndex,Is.EqualTo(4));
    Assert.That(loaded.DungeonOffers.GetDanger(DungeonTheme.Forest),Is.EqualTo(75));
    Assert.That(loaded.DungeonOffers.HighestMilestoneProcessed,Is.EqualTo(10));
    CollectionAssert.AreEqual(s.DungeonOffers.Offers.Select(o=>(o.DefinitionId,o.Seed,o.LevelOffset,o.FixedLevel)),loaded.DungeonOffers.Offers.Select(o=>(o.DefinitionId,o.Seed,o.LevelOffset,o.FixedLevel)));
    DungeonOfferSystem.EnsureOffers(loaded,new[]{definition});CollectionAssert.AreEqual(s.DungeonOffers.Offers.Select(o=>o.Seed),loaded.DungeonOffers.Offers.Select(o=>o.Seed));
   } finally {UnityEngine.Object.DestroyImmediate(catalog);}
  }
  [Test] public void OldSavesWithoutOffersRestoreAndGenerateOnDemand()
  {
   var catalog=ScriptableObject.CreateInstance<GameContentCatalog>();try{
    var data=GameSaveData.CreateNew("legacy","legacy.campaign");data.Party.MaxActiveMembers=5;
    Assert.That(new GameSessionRestoreFactory().TryCreate(data,catalog,out var loaded),Is.True);Assert.That(loaded.DungeonOffers.Offers,Is.Empty);
    Assert.That(DungeonOfferSystem.EnsureOffers(loaded,new[]{definition}),Is.True);
   }finally{UnityEngine.Object.DestroyImmediate(catalog);}
  }
  [Test] public void CompleteChangesBoardButClearingFailedGenerationDoesNot()
  {
   var s=Session();DungeonOfferSystem.EnsureOffers(s,new[]{definition});var old=s.DungeonOffers.Offers.Select(o=>o.Seed).ToArray();
   s.TrySetActiveDungeon(new DungeonInstance(definition,42,15));Assert.That(DungeonOfferSystem.EnsureOffers(s,new[]{definition}),Is.False);
   Assert.That(s.ActiveDungeon.DungeonLevel,Is.EqualTo(15));s.TryClearActiveDungeon();
   CollectionAssert.AreEqual(old,s.DungeonOffers.Offers.Select(o=>o.Seed));
   s.DungeonOffers.CompleteExpedition();Assert.That(s.DungeonOffers.Offers,Is.Empty);DungeonOfferSystem.EnsureOffers(s,new[]{definition});
   Assert.That(old.SequenceEqual(s.DungeonOffers.Offers.Select(o=>o.Seed)),Is.False);
  }
  [Test] public void InvalidSavedBoardIsRejectedWithoutPartialMutation()
  {
   var s=Session();DungeonOfferSystem.EnsureOffers(s,new[]{definition});var seeds=s.DungeonOffers.Offers.Select(o=>o.Seed).ToArray();
   var data=DungeonOfferPersistence.Capture(s.DungeonOffers);data.Offers[1].Seed=data.Offers[0].Seed;
   Assert.That(DungeonOfferPersistence.Restore(data,s.DungeonOffers),Is.False);CollectionAssert.AreEqual(seeds,s.DungeonOffers.Offers.Select(o=>o.Seed));
  }
  [Test] public void ActualGeneratorUsesOfferedLevelWithoutChangingDefinition()
  {
   var source=AssetDatabase.LoadAssetAtPath<DungeonDefinition>("Assets/_Project/ScriptableObjects/Dungeons/DD_Catacombs_Procedural.asset");
   Assert.That(source,Is.Not.Null);var original=source.DungeonLevel;
   var generated=new DungeonGeneratorService().Generate(source,431,17);
   Assert.That(generated.DungeonLevel,Is.EqualTo(17));Assert.That(generated.Seed,Is.EqualTo(431));Assert.That(source.DungeonLevel,Is.EqualTo(original));
  }
  [Test] public void StoryAndNonProceduralDefinitionsDoNotEnterOfferPool()
  {
   Set(definition,"IsStoryDungeon",true);Assert.That(DungeonOfferSystem.EnsureOffers(Session(),new[]{definition}),Is.False);
   Set(definition,"IsStoryDungeon",false);Set(definition,"IsProcedural",false);Assert.That(DungeonOfferSystem.EnsureOffers(Session(),new[]{definition}),Is.False);
  }
  [Test] public void OldOfferBoardWithoutDangerFieldsRestores()
  {
   var session=Session();Assert.That(DungeonOfferSystem.EnsureOffers(session,new[]{definition}),Is.True);
   var legacy=DungeonOfferPersistence.Capture(session.DungeonOffers);
   legacy.Danger=null;legacy.BossOpportunities=null;
   var restored=new DungeonOfferBoard();
   Assert.That(DungeonOfferPersistence.Restore(legacy,restored),Is.True);
   Assert.That(restored.GetDanger(DungeonTheme.Catacombs),Is.Zero);
   Assert.That(restored.BossOpportunities,Is.Empty);
   CollectionAssert.AreEqual(session.DungeonOffers.Offers.Select(offer=>offer.Seed),
    restored.Offers.Select(offer=>offer.Seed));
  }
  [Test] public void PendingBossAndItsFixedLevelSurviveSaveAndLoad()
  {
   var profile=AssetDatabase.LoadAssetAtPath<NewCampaignDefinition>(
    "Assets/_Project/ScriptableObjects/StartingCampaign/NC_Arungard.asset");
   var catalog=AssetDatabase.LoadAssetAtPath<GameContentCatalog>(
    "Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
   Assert.That(new NewCampaignService().TryCreate(profile,out var session,out var error),Is.True,error);
   var hero=session.CharactersById.Values.First();
   Assert.That(new CharacterProgressionSystem().TrySetLevel(hero,10,out _),Is.True);
   Assert.That(DungeonOfferSystem.EnsureOffers(session,catalog.DungeonDefinitions),Is.True);
    var pending=session.DungeonOffers.BossOpportunities.Single();
    var snapshot=new GameSessionSaveSnapshotFactory().CreateSnapshot(session,"danger");
    var json=new GameSaveJsonSerializer();
    Assert.That(json.TryDeserialize(json.Serialize(snapshot),out var decoded),Is.True);
    Assert.That(new GameSessionRestoreFactory().TryCreate(decoded,catalog,out var loaded),Is.True);
    Assert.That(loaded.DungeonOffers.GetDanger(pending.Theme),Is.EqualTo(100));
    Assert.That(loaded.DungeonOffers.BossOpportunities.Single().Level,Is.EqualTo(10));
    Assert.That(loaded.DungeonOffers.BossOpportunities.Single().DefinitionId,Is.EqualTo(pending.DefinitionId));
    var seed=loaded.DungeonOffers.Offers[0].Seed;
    Assert.That(DungeonOfferSystem.EnsureOffers(loaded,catalog.DungeonDefinitions),Is.True);
    Assert.That(loaded.DungeonOffers.Offers[0].Seed,Is.EqualTo(seed));
    Assert.That(loaded.DungeonOffers.Offers[0].FixedLevel,Is.EqualTo(10));
  }
  [Test] public void DemoDefinitionsDoNotEnterOfferPool()
  {
   var demo=ScriptableObject.CreateInstance<DungeonDefinition>();
   try{
    Set(demo,"Id","demo.dungeon");Set(demo,"IsProcedural",true);
    var session=Session();
    Assert.That(DungeonOfferSystem.EnsureOffers(session,new[]{demo}),Is.False);
    Assert.That(DungeonOfferSystem.EnsureOffers(session,new[]{demo,definition}),Is.True);
    Assert.That(session.DungeonOffers.Offers.All(offer=>offer.DefinitionId==definition.Id),Is.True);
   }finally{UnityEngine.Object.DestroyImmediate(demo);}
  }
  [Test] public void TwoProductionVariantsBothAppearOnTheOfferBoard()
  {
   var bossless=ScriptableObject.CreateInstance<DungeonDefinition>();
   try{
    Set(bossless,"Id","catacombs.bossless");Set(bossless,"IsProcedural",true);
    Set(bossless,"IsAvailableAsOffer",true);
    var session=Session();
    Assert.That(DungeonOfferSystem.EnsureOffers(session,new[]{definition,bossless}),Is.True);
    var ids=session.DungeonOffers.Offers.Select(offer=>offer.DefinitionId).ToArray();
    Assert.That(ids,Does.Contain(definition.Id));
    Assert.That(ids,Does.Contain(bossless.Id));
   }finally{UnityEngine.Object.DestroyImmediate(bossless);}
  }
  [Test] public void PrefabAndCityHaveExplicitSixOfferWiring()
  {
   var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_DungeonSelector.prefab");Assert.That(prefab,Is.Not.Null);
   Assert.That(prefab.GetComponentInChildren<RawImage>(true).texture,Is.InstanceOf<Texture2D>());
   var view=prefab.GetComponent<DungeonSelectorWindowView>();var so=new SerializedObject(view);
   foreach(var field in new[]{"window","details","partySummary","depart","abandon","partyRoot","partyTemplate"})Assert.That(so.FindProperty(field).objectReferenceValue,Is.Not.Null,field);
   Assert.That(so.FindProperty("offerButtons").arraySize,Is.EqualTo(6));
   for(int i=0;i<6;i++)Assert.That(so.FindProperty("offerButtons").GetArrayElementAtIndex(i).objectReferenceValue,Is.Not.Null);
   var setup=EditorSceneManager.GetSceneManagerSetup();try{
    var scene=EditorSceneManager.OpenScene("Assets/_Project/Scenes/City/Arungard.unity");
    var entries=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<DungeonEntranceView>(true)).ToArray();Assert.That(entries,Is.Not.Empty);
    foreach(var entry in entries)Assert.That(new SerializedObject(entry).FindProperty("selector").objectReferenceValue,Is.Not.Null);
   }finally{if(setup.Any(s=>s.isLoaded&&s.isActive))EditorSceneManager.RestoreSceneManagerSetup(setup);else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);}
  }
  [Test] public void CityEntranceOffersCatalogExpeditionsWhenItsLegacyDefinitionIsNotOfferable()
  {
   var catalog=AssetDatabase.LoadAssetAtPath<GameContentCatalog>(
    "Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
   var legacy=AssetDatabase.LoadAssetAtPath<DungeonDefinition>(
    "Assets/_Project/ScriptableObjects/Dungeons/DD_Catacombs_Procedural.asset");
   var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(
    "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_DungeonSelector.prefab");
   Assert.That(catalog,Is.Not.Null);Assert.That(legacy,Is.Not.Null);
   Assert.That(legacy.IsAvailableAsOffer,Is.False);
   var ownerObject=new GameObject("Offer test bootstrap");
   ownerObject.SetActive(false);
   var selectorObject=UnityEngine.Object.Instantiate(prefab);
   try
   {
    var owner=ownerObject.AddComponent<Godmist.Views.Game.GameBootstrap>();
    typeof(Godmist.Views.Game.GameBootstrap).GetField("contentCatalog",
     BindingFlags.Instance|BindingFlags.NonPublic).SetValue(owner,catalog);
    var session=Session();Set(owner,"Session",session);
    selectorObject.GetComponent<DungeonSelectorWindowView>().Open(owner,legacy);
    Assert.That(session.DungeonOffers.Offers.Count,Is.EqualTo(6));
    var ids=session.DungeonOffers.Offers.Select(offer=>offer.DefinitionId).ToArray();
    Assert.That(ids,Does.Contain("dungeon.catacombs.expedition"));
    Assert.That(ids,Does.Not.Contain("dungeon.catacombs.boss_expedition"),
     "Boss expedition requires maximum Catacombs Danger.");
   }
   finally
   {
    UnityEngine.Object.DestroyImmediate(selectorObject);
    UnityEngine.Object.DestroyImmediate(ownerObject);
   }
  }
 }
}

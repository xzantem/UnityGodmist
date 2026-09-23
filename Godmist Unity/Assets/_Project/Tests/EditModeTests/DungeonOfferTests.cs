using System;
using System.Linq;
using System.Reflection;
using Godmist.Core.Serialization;
using Godmist.Data.Dungeons;
using Godmist.Data.World;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Systems.Dungeons;
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
  [Test] public void ReopenAndJsonRoundTripKeepSeedsOffsetsAndSelection()
  {
   var s=Session();DungeonOfferSystem.EnsureOffers(s,new[]{definition});s.DungeonOffers.TrySelect(4);
   var catalog=ScriptableObject.CreateInstance<GameContentCatalog>();catalog.DungeonDefinitions.Add(definition);
   try {
    var snapshot=new GameSessionSaveSnapshotFactory().CreateSnapshot(s,"offers");
    var json=new GameSaveJsonSerializer();Assert.That(json.TryDeserialize(json.Serialize(snapshot),out var decoded),Is.True);
    Assert.That(new GameSessionRestoreFactory().TryCreate(decoded,catalog,out var loaded),Is.True);
    Assert.That(loaded.DungeonOffers.SelectedIndex,Is.EqualTo(4));
    CollectionAssert.AreEqual(s.DungeonOffers.Offers.Select(o=>(o.DefinitionId,o.Seed,o.LevelOffset)),loaded.DungeonOffers.Offers.Select(o=>(o.DefinitionId,o.Seed,o.LevelOffset)));
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
    Assert.That(ids,Does.Contain("dungeon.catacombs.boss_expedition"));
   }
   finally
   {
    UnityEngine.Object.DestroyImmediate(selectorObject);
    UnityEngine.Object.DestroyImmediate(ownerObject);
   }
  }
 }
}

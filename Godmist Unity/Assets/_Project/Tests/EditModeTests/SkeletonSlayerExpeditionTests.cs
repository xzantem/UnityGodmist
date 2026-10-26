using System;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Dungeons;
using Godmist.Data.Items;
using Godmist.Data.World;
using Godmist.Editor;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Systems.Dungeons;
using Godmist.Systems.Items;
using Godmist.Systems.Quests;
using Godmist.Systems.SaveLoad;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class SkeletonSlayerExpeditionTests
    {
        const string Root="Assets/_Project/ScriptableObjects/";
        static T Asset<T>(string path) where T:UnityEngine.Object=>AssetDatabase.LoadAssetAtPath<T>(path);
        static DungeonDefinition King=>Asset<DungeonDefinition>(Root+"Dungeons/DD_Catacombs_BossExpedition.asset");
        static DungeonDefinition Slayer=>Asset<DungeonDefinition>(VS16CatacombsBossSetup.SlayerDungeonPath);
        static LootBagItemDefinition KingBag=>Asset<LootBagItemDefinition>(Root+"Catacombs/Loot/ID_Catacombs_DeadKingBag.asset");
        static LootBagItemDefinition SlayerBag=>Asset<LootBagItemDefinition>(VS16CatacombsBossSetup.SlayerBagPath);
        static ItemDefinition Bone=>Asset<ItemDefinition>(VS16CatacombsBossSetup.SlayerBonePath);
        static ItemDefinition Ectoplasm=>Asset<ItemDefinition>(Root+"Catacombs/Loot/ID_DeadKingEctoplasm.asset");
        static GameContentCatalog Catalog=>Asset<GameContentCatalog>(Root+"World/GC_MainContentCatalog.asset");
        static DungeonDefinition Regular=>Catalog.DungeonDefinitions.First(d=>d!=null && d.IsAvailableAsOffer && d.Theme==DungeonTheme.Catacombs && d.FixedOfferLevels.Count==0);
        static DungeonDefinition[] Pool=>new[]{Regular,King,Slayer};
        static GameSession Session(string campaign,int level)
        {
            var s=new GameSession(campaignId:campaign);s.TryRegisterCharacter(new CharacterInstance{CharacterId="hero",Level=level});s.PlayerParty.TryAddActiveMember("hero");
            if(level>=20)s.DungeonOffers.MarkMilestoneProcessed(10);
            return s;
        }
        [Test]
        public void OrdinaryPoolsMatchKingWithMaterialReplacedAndOwnUniquePool()
        {
            Assert.That(SlayerBag,Is.Not.Null);Assert.That(Bone,Is.Not.Null);Assert.That(SlayerBag.Id,Is.EqualTo(VS16CatacombsBossSetup.SlayerBagId));
            Assert.That(SlayerBag.Brackets.Count,Is.EqualTo(KingBag.Brackets.Count));var replacements=0;
            for(var i=0;i<KingBag.Brackets.Count;i++)
            {
                var original=KingBag.Brackets[i];var actual=SlayerBag.Brackets[i];Assert.That(actual.Bracket,Is.EqualTo(original.Bracket));Assert.That(actual.Pools.Count,Is.EqualTo(original.Pools.Count));
                for(var j=0;j<original.Pools.Count;j++)
                {
                    var a=actual.Pools[j];var e=original.Pools[j];Assert.That(a.DropChance,Is.EqualTo(e.DropChance));Assert.That(a.DistinctSelectionGroup,Is.EqualTo(e.DistinctSelectionGroup));Assert.That(a.Entries.Count,Is.EqualTo(e.Entries.Count));
                    for(var k=0;k<e.Entries.Count;k++)
                    {
                        var target=e.Entries[k].ItemDefinition==Ectoplasm?Bone:e.Entries[k].ItemDefinition;if(target==Bone)replacements++;
                        Assert.That(a.Entries[k].ItemDefinition,Is.SameAs(target));
                        var json=JsonUtility.ToJson(a.Entries[k]).Replace("\"instanceID\":"+Bone.GetInstanceID(),"\"instanceID\":"+Ectoplasm.GetInstanceID());
                        Assert.That(json,Is.EqualTo(JsonUtility.ToJson(e.Entries[k])),"All amount/quality/galduryte fields must match.");
                    }
                }
            }
            Assert.That(replacements,Is.GreaterThan(0));
            Assert.That(SlayerBag.BossUniqueDrop.DropChance,Is.EqualTo(KingBag.BossUniqueDrop.DropChance));
            Assert.That(SlayerBag.BossUniqueDrop.EquipmentDefinitions.Select(u=>u.Id),Is.EquivalentTo(new[]{
                "unique.skeleton_slayer.armor.warrior", "unique.skeleton_slayer.armor.scout",
                "unique.skeleton_slayer.armor.mage", "unique.skeleton_slayer.hammer.paladin"}));
            Assert.That(SlayerBag.BossUniqueDrop.EquipmentDefinitions.Intersect(KingBag.BossUniqueDrop.EquipmentDefinitions),Is.Empty);
            Assert.That(SlayerBag.Icon,Is.SameAs(KingBag.Icon));
        }
        [TestCase(10)] [TestCase(20)]
        public void SeededOrdinaryBagRollsMatchKingAndUniquesBelongToSlayer(int level)
        {
            for(var seed=0;seed<32;seed++)
            {
                var king=new LootTableSystem(new System.Random(seed),Catalog.EquipmentPartDefinitions,lootLevel:level).CreateLootBag(KingBag);
                var slayer=new LootTableSystem(new System.Random(seed),Catalog.EquipmentPartDefinitions,lootLevel:level).CreateLootBag(SlayerBag);
                Assert.That(slayer.Contents.Where(i=>i.Definition is not UniqueEquipmentDefinition).Select(i=>i.Definition.Id==Bone.Id?Ectoplasm.Id:i.Definition.Id),
                    Is.EqualTo(king.Contents.Where(i=>i.Definition is not UniqueEquipmentDefinition).Select(i=>i.Definition.Id)),"seed "+seed);
                var uniqueItems=slayer.Contents.Where(i=>i.Definition is UniqueEquipmentDefinition).ToArray();
                Assert.That(uniqueItems.Length,Is.EqualTo(king.Contents.Count(i=>i.Definition is UniqueEquipmentDefinition)));
                Assert.That(uniqueItems.All(i=>SlayerBag.BossUniqueDrop.EquipmentDefinitions.Contains(i.Definition)),Is.True);
                Assert.That(slayer.Contents.All(i=>i.Definition!=Ectoplasm),Is.True);
            }
        }
        [Test]
        public void NewMaterialAndBagAreRegisteredAndKeepTheReplacedMaterialEconomy()
        {
            Assert.That(Catalog.ItemDefinitions.Count(i=>i==Bone),Is.EqualTo(1));Assert.That(Catalog.ItemDefinitions.Count(i=>i==SlayerBag),Is.EqualTo(1));Assert.That(Catalog.DungeonDefinitions.Count(i=>i==Slayer),Is.EqualTo(1));
            Assert.That(Bone.Id,Is.EqualTo(VS16CatacombsBossSetup.SlayerBoneId));Assert.That(Bone.DisplayName,Is.EqualTo("Kość Zabójcy"));Assert.That(Bone.Icon,Is.Not.Null);
            Assert.That(Bone.BaseRarity,Is.EqualTo(Ectoplasm.BaseRarity));Assert.That(Bone.BaseValue,Is.EqualTo(Ectoplasm.BaseValue));Assert.That(Bone.Weight,Is.EqualTo(Ectoplasm.Weight));Assert.That(Bone.MaterialDiscipline,Is.EqualTo(Ectoplasm.MaterialDiscipline));
        }
        [TestCase(10)] [TestCase(20)]
        public void SeparateBossTablesReplaceBagWithoutChangingOtherDrops(int level)
        {
            Assert.That(Slayer.LootTableCatalog,Is.Not.SameAs(King.LootTableCatalog));
            Assert.That(Slayer.LootTableCatalog.TryGetBossLootTable(DungeonTheme.Catacombs,level,out var actual),Is.True);
            Assert.That(King.LootTableCatalog.TryGetBossLootTable(DungeonTheme.Catacombs,level,out var original),Is.True);Assert.That(actual,Is.Not.SameAs(original));
            Assert.That(actual.Entries.Count,Is.EqualTo(original.Entries.Count));
            for(var i=0;i<original.Entries.Count;i++)
            {
                var e=original.Entries[i];var a=actual.Entries[i];Assert.That(a.ItemDefinition,Is.SameAs(e.ItemDefinition==KingBag?SlayerBag:e.ItemDefinition));
                Assert.That(a.DropChance,Is.EqualTo(e.DropChance));Assert.That(a.Amount.Min,Is.EqualTo(e.Amount.Min));Assert.That(a.Amount.Max,Is.EqualTo(e.Amount.Max));
            }
            Assert.That(Slayer.LootTableCatalog.TryGetEnemyLootTable(DungeonTheme.Catacombs,level,out var enemies),Is.True);
            King.LootTableCatalog.TryGetEnemyLootTable(DungeonTheme.Catacombs,level,out var kingEnemies);Assert.That(enemies,Is.SameAs(kingEnemies));
        }
        [TestCase(10)] [TestCase(20)]
        public void ActualDungeonPlacesOneSlayerInOwnFinalRoom(int level)
        {
            Assert.That(Slayer.IsAvailableAsOffer,Is.True);Assert.That(Slayer.FixedOfferLevels,Is.EqualTo(new[]{10,20}));
            Assert.That(Slayer.RoomTemplateCatalog,Is.Not.SameAs(King.RoomTemplateCatalog));
            var room=Slayer.RoomTemplateCatalog.Templates.First(t=>t.RoomType==DungeonRoomType.Final);Assert.That(room.DisplayName,Is.EqualTo("Komnata Szkieleta Zabójcy"));
            Assert.That(King.RoomTemplateCatalog.Templates.First(t=>t.RoomType==DungeonRoomType.Final).DisplayName,Is.EqualTo("Komnata Umarłego Króla"));
            foreach(var seed in new[]{17,219,3401})
            {
                var dungeon=new DungeonGeneratorService().Generate(Slayer,seed,level);new DungeonEncounterPlacementSystem().Place(dungeon);
                var bosses=dungeon.Floors.SelectMany(f=>f.Encounters).Where(e=>e.Definition==Slayer.BossEncounterDefinition).ToArray();Assert.That(bosses.Length,Is.EqualTo(1));
                Assert.That(bosses[0].RoomId,Is.EqualTo(dungeon.Floors.Last().Rooms.Single(r=>r.RoomType==DungeonRoomType.Final).RoomId));
                Assert.That(bosses[0].Definition.Enemies[0].CharacterDefinition.Id,Is.EqualTo(Godmist.Systems.Combat.SkeletonSlayerBossSystem.BossId));
            }
        }
        [TestCase(10)] [TestCase(20)]
        public void PreparedBossRewardDoesNotDuplicateBagOrReroll(int level)
        {
            var withBag=0;
            for(var seed=0;seed<16;seed++)
            {
                var dungeon=new DungeonInstance(Slayer,seed,level);var floor=new DungeonFloorInstance(0);
                floor.TryAddRoom(new DungeonRoomInstance("final",DungeonRoomType.Final));
                var encounter=new DungeonEncounterInstance("slayer.final",Slayer.BossEncounterDefinition,"final",new RectInt(0,0,15,15));floor.TryAddEncounter(encounter);dungeon.TryAddFloor(floor);
                var prepare=new DungeonEncounterRewardPreparationSystem();prepare.Prepare(dungeon,Catalog.EquipmentPartDefinitions);
                Assert.That(dungeon.TryGetPreRolledRewards(encounter.InstanceId,out var rewards),Is.True);
                var bags=rewards.EnemyRewards.SelectMany(e=>e.Items).OfType<LootBagItemInstance>().Where(b=>b.Definition==SlayerBag).ToArray();Assert.That(bags.Length,Is.LessThanOrEqualTo(1));withBag+=bags.Length;
                Assert.That(rewards.EnemyRewards.SelectMany(e=>e.Items).All(i=>i.Definition!=KingBag),Is.True);
                prepare.Prepare(dungeon,Catalog.EquipmentPartDefinitions);dungeon.TryGetPreRolledRewards(encounter.InstanceId,out var again);Assert.That(again,Is.SameAs(rewards));
            }
            Assert.That(withBag,Is.GreaterThan(0));
        }
        [TestCase(8)] [TestCase(10)] [TestCase(20)]
        public void BothBossesCanBeChosenAndChoiceSurvivesRefreshPoolOrderAndSave(int level)
        {
            var seen=new System.Collections.Generic.HashSet<string>();
            for(var i=0;i<32;i++)
            {
                var campaign="slayer.offer."+i;var s=Session(campaign,level);s.DungeonOffers.SetDanger(DungeonTheme.Catacombs,100);
                Assert.That(DungeonOfferSystem.EnsureOffers(s,Pool),Is.True);var first=s.DungeonOffers.Offers[0];seen.Add(first.DefinitionId);
                Assert.That(s.DungeonOffers.Offers.Count,Is.EqualTo(6));Assert.That(first.FixedLevel,Is.EqualTo(level<10?10:level));
                Assert.That(new[]{King.Id,Slayer.Id},Contains.Item(first.DefinitionId));
                var same=Session(campaign,level);same.DungeonOffers.SetDanger(DungeonTheme.Catacombs,100);DungeonOfferSystem.EnsureOffers(same,Pool.Reverse());Assert.That(same.DungeonOffers.Offers[0].DefinitionId,Is.EqualTo(first.DefinitionId));
                s.DungeonOffers.InvalidateOffers();DungeonOfferSystem.EnsureOffers(s,Pool);Assert.That(s.DungeonOffers.Offers[0].DefinitionId,Is.EqualTo(first.DefinitionId));
                var restored=Session(campaign,level);Assert.That(DungeonOfferPersistence.Restore(DungeonOfferPersistence.Capture(s.DungeonOffers),restored.DungeonOffers),Is.True);
                DungeonOfferSystem.EnsureOffers(restored,Pool);Assert.That(restored.DungeonOffers.Offers[0].DefinitionId,Is.EqualTo(first.DefinitionId));
            }
            Assert.That(seen,Is.EquivalentTo(new[]{King.Id,Slayer.Id}));
        }
        [Test]
        public void CompletingSlayerClearsOpportunityAndDangerWithoutUnlockingKingsMission()
        {
            var s=Session("slayer.complete",8);s.DungeonOffers.SetDanger(DungeonTheme.Catacombs,100);s.DungeonOffers.QueueBoss(DungeonTheme.Catacombs,Slayer.Id,10);
            DungeonOfferSystem.EnsureOffers(s,Pool);Assert.That(s.DungeonOffers.Offers[0].DefinitionId,Is.EqualTo(Slayer.Id));Assert.That(CatacombsMissionBoard.HasBossOffer(s),Is.False);
            DungeonOfferSystem.CompleteExpedition(s,new DungeonInstance(Slayer,123,10),Pool);Assert.That(s.DungeonOffers.GetDanger(DungeonTheme.Catacombs),Is.Zero);Assert.That(s.DungeonOffers.BossOpportunities,Is.Empty);
            DungeonOfferSystem.EnsureOffers(s,Pool);Assert.That(s.DungeonOffers.Offers.All(o=>o.FixedLevel==0),Is.True);
        }
    }
}

using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.World;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Game;
using Godmist.Systems.Characters;
using Godmist.Systems.Items;
using Godmist.Systems.NPC;
using Godmist.Systems.SaveLoad;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class NewCampaignTests
    {
        private NewCampaignDefinition Profile => AssetDatabase.LoadAssetAtPath<NewCampaignDefinition>(
            "Assets/_Project/ScriptableObjects/StartingCampaign/NC_Arungard.asset");
        private GameSession Create()
        {
            Assert.That(new NewCampaignService().TryCreate(Profile, out var session, out var error), Is.True, error);
            return session;
        }
        [Test]
        public void NewGameStartsFourHealthyCharactersWith500GoldAndNoConsumables()
        {
            var session = Create();
            Assert.That(session.Gold, Is.EqualTo(500));
            Assert.That(session.PlayerParty.ActiveMemberIds.Count, Is.EqualTo(4));
            Assert.That(session.Stash.Items, Is.Empty);
            foreach (var character in session.CharactersById.Values)
            {
                Assert.That(character.Level, Is.EqualTo(1));
                Assert.That(character.Inventory.Items, Is.Empty);
                Assert.That(character.CurrentHealth, Is.EqualTo(Mathf.RoundToInt(character.Stats.MaxHealth.GetValue())));
                foreach (var slot in new[] { EquipmentSlot.Weapon, EquipmentSlot.Armor })
                {
                    var item = character.Equipment.GetItem(slot);
                    Assert.That(item, Is.Not.Null);
                    Assert.That(item.RequiredLevel, Is.EqualTo(1));
                    Assert.That(item.ItemLevel, Is.EqualTo(1));
                    Assert.That(item.CurrentRarity, Is.EqualTo(RarityTier.Common));
                    Assert.That(item.Quality, Is.EqualTo(EquipmentQuality.Normal));
                    Assert.That(item.RequiredClassId, Is.EqualTo(character.ClassData.Id));
                }
            }
        }
        [TestCase(0,3,15,1,.07f,80,4,0,50,10)]
        [TestCase(1,7,14,.5f,.03f,78,6,2,75,8)]
        [TestCase(2,5,10,.6f,0,0,2,6,30,4)]
        [TestCase(3,7,10,.5f,.02f,75,8,8,100,6)]
        public void ImportedStarterPartsMatchApprovedValues(int index,float min,float max,float crit,float chance,float accuracy,float physical,float magical,float hp,float dodge)
        {
            var entry=Profile.Characters[index];
            Assert.That(entry.WeaponParts.Count,Is.EqualTo(3));Assert.That(entry.ArmorParts.Count,Is.EqualTo(3));
            var head=entry.WeaponParts.Single(p=>p.Kind==EquipmentPartKind.WeaponHead).Stats;
            Assert.That(head.MinAttackBase,Is.EqualTo(min));Assert.That(head.MaxAttackBase,Is.EqualTo(max));Assert.That(head.CritDamageBase,Is.EqualTo(crit));
            Assert.That(entry.WeaponParts.Single(p=>p.Kind==EquipmentPartKind.WeaponBinder).Stats.CritChanceBase,Is.EqualTo(chance));
            Assert.That(entry.WeaponParts.Single(p=>p.Kind==EquipmentPartKind.WeaponHandle).Stats.WeaponAccuracyBase,Is.EqualTo(accuracy));
            var plate=entry.ArmorParts.Single(p=>p.Kind==EquipmentPartKind.ArmorPlate).Stats;
            Assert.That(plate.PhysicalDefenseBase,Is.EqualTo(physical));Assert.That(plate.MagicalDefenseBase,Is.EqualTo(magical));
            Assert.That(entry.ArmorParts.Single(p=>p.Kind==EquipmentPartKind.ArmorBinder).Stats.MaxHealthBase,Is.EqualTo(hp));
            Assert.That(entry.ArmorParts.Single(p=>p.Kind==EquipmentPartKind.ArmorBase).Stats.DodgeBase,Is.EqualTo(dodge));
            Assert.That(entry.WeaponParts.Concat(entry.ArmorParts).All(p=>p.IsStarterOnly && p.CraftingMaterials.Count==0),Is.True);
        }

        [Test]
        public void SaveRoundTripKeepsSpentGoldAndEquipmentWithoutGrantingAnotherPack()
        {
            var session = Create(); session.TrySpendGold(123);
            var snapshot = new GameSessionSaveSnapshotFactory().CreateSnapshot(session,"start");
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>("Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
            Assert.That(new GameSessionRestoreFactory().TryCreate(snapshot,catalog,out var restored), Is.True);
            Assert.That(restored.Gold, Is.EqualTo(377));
            Assert.That(restored.WorldState.HasFlag(NewCampaignService.InitializedFlag), Is.True);
            foreach(var character in session.CharactersById.Values)
                Assert.That(restored.CharactersById[character.CharacterId].Equipment.GetItem(EquipmentSlot.Weapon).InstanceId,
                    Is.EqualTo(character.Equipment.GetItem(EquipmentSlot.Weapon).InstanceId));
        }
        [Test]
        public void NewCampaignHasIndependentIdsAndDoesNotChangeExistingSession()
        {
            var first=Create();first.TrySpendGold(100);var second=Create();
            Assert.That(second.CampaignId,Is.Not.EqualTo(first.CampaignId));
            Assert.That(first.Gold,Is.EqualTo(400));Assert.That(second.Gold,Is.EqualTo(500));
        }
        [Test]
        public void InvalidProfileReturnsNoPartialCampaign()
        {
            var invalid=ScriptableObject.Instantiate(Profile);
            try
            {
                invalid.Characters[3]=invalid.Characters[0];
                Assert.That(new NewCampaignService().TryCreate(invalid,out var session,out _),Is.False);
                Assert.That(session,Is.Null);
            }
            finally { Object.DestroyImmediate(invalid); }
        }
        [Test]
        public void StarterPartsAreExcludedFromRandomEquipment()
        {
            var entry=Profile.Characters[0];
            Assert.That(new EquipmentGenerationService().Generate(entry.Weapon,entry.WeaponParts),Is.Null);
        }
        [TestCase(10,false)]
        [TestCase(11,true)]
        public void GaldurytesUnlockForReserveCharacterAtEleven(int level,bool expected)
        {
            var session=new GameSession();
            session.TryRegisterCharacter(new CharacterInstance { CharacterId="reserve",Level=level });
            Assert.That(session.PlayerParty.ActiveMemberIds,Is.Empty);
            Assert.That(GalduryteServiceAccess.IsUnlocked(session),Is.EqualTo(expected));
        }
        [Test]
        public void StarterEquipmentCannotBeUpgradedOrReforgedAndSellsForZero()
        {
            var session=Create();var owner=session.CharactersById.Values.First();var item=owner.Equipment.Weapon;
            var catalog=AssetDatabase.LoadAssetAtPath<GameContentCatalog>("Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
            var npc=new Godmist.Runtime.NPC.NpcInstance
            {
                Definition=catalog.NpcDefinitions.First(n=>n?.GetRole<Godmist.Data.NPC.BlacksmithRoleData>()!=null),
                ShopInventory=new Godmist.Runtime.NPC.ShopInventory()
            };
            var upgrade=new EquipmentUpgradeService();
            Assert.That(upgrade.TryUpgrade(session,npc,owner,item,.2f,.8f,catalog,out _,out var paid,out var reason),Is.False);
            Assert.That(reason,Does.Contain("startowego"));Assert.That(paid,Is.Zero);
            Assert.That(upgrade.TryReforge(session,npc,owner,item,catalog,out _,out paid,out reason),Is.False);
            Assert.That(reason,Does.Contain("startowego"));Assert.That(paid,Is.Zero);
            Assert.That(new EquipmentService().TryUnequip(owner,EquipmentSlot.Weapon),Is.True);
            var trade=new TradeService();
            Assert.That(trade.TryGetSellQuote(npc,item,out var normal,out var payable,out reason),Is.True,reason);
            Assert.That(normal,Is.Zero);Assert.That(payable,Is.Zero);
            Assert.That(trade.TrySellToTrader(session,npc,owner,item.InstanceId,out var received),Is.True);
            Assert.That(received,Is.Zero);Assert.That(session.Gold,Is.EqualTo(500));
            Assert.That(trade.TryBuyFromBuyback(session,npc,owner,item.InstanceId,out paid),Is.True);
            Assert.That(paid,Is.Zero);Assert.That(owner.Inventory.Items.Count(i=>i.InstanceId==item.InstanceId),Is.EqualTo(1));
            Assert.That(npc.ShopInventory.BuybackStock,Is.Empty);
        }

        [Test]
        public void GuardOfferDisappearsAfterAcceptingQuest()
        {
            var session=Create();
            var quest=AssetDatabase.LoadAssetAtPath<Godmist.Data.Quests.QuestDefinition>("Assets/_Project/ScriptableObjects/Quests/QD_InvestigateCatacombs.asset");
            var service=new Godmist.Systems.Quests.QuestService(session);
            service.TryRegisterQuest(quest);
            var catalog=AssetDatabase.LoadAssetAtPath<GameContentCatalog>("Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
            var dialogue=catalog.NpcDefinitions.Select(n=>n?.BaseDialogue).FirstOrDefault(d=>QuestOfferAvailability.HasOffer(session,d));
            Assert.That(dialogue,Is.Not.Null,"Guard must expose the first quest in its dialogue.");
            service.TryMakeQuestAvailable(quest.Id);service.TryStartQuest(quest.Id);
            Assert.That(QuestOfferAvailability.HasOffer(session,dialogue),Is.False);
        }

        [Test]
        public void LockedGalduryteServicesRejectActionsWithoutSpendingAnything()
        {
            var session=Create();
            Assert.That(new GalduryteCraftingService().CreateCraftPreview(session,null,null).FailureReason,Is.EqualTo(GalduryteServiceAccess.LockedMessage));
            Assert.That(new GalduryteCraftingService().CreateIdentificationPreview(session,null,null).FailureReason,Is.EqualTo(GalduryteServiceAccess.LockedMessage));
            Assert.That(new GalduryteSocketingService().CreatePreview(session,null,null,null).FailureReason,Is.EqualTo(GalduryteServiceAccess.LockedMessage));
            Assert.That(new GalduryteExtractionService().CreatePreview(session,null,null,null).FailureReason,Is.EqualTo(GalduryteServiceAccess.LockedMessage));
            Assert.That(session.Gold,Is.EqualTo(500));
        }
    }
}

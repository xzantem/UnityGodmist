using System;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Configuration;
using Godmist.Data.NPC;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Game;
using Godmist.Runtime.NPC;
using Godmist.Systems.Combat;
using Godmist.Systems.NPC;
using Godmist.Systems.SaveLoad;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Godmist.Tests.EditMode
{
    public sealed class CampaignDefeatAndRecoveryTests
    {
        private GameSession game;
        private NpcDefinition priestDefinition;
        private PriestRoleData priestRole;
        private CharacterDefinition mainDefinition;
        private NpcInstance priest;
        private CharacterInstance main, companion;
        [SetUp] public void SetUp()
        {
            GameDefinesRuntime.Configure(null);
            game=new GameSession(GameModeState.Exploration);
            priestDefinition=ScriptableObject.CreateInstance<NpcDefinition>();
            priestRole=ScriptableObject.CreateInstance<PriestRoleData>();
            priestDefinition.Roles.Add(priestRole);
            priest=new NpcInstance {NpcId="priest",Definition=priestDefinition};game.TryRegisterNpc(priest);
            mainDefinition=ScriptableObject.CreateInstance<CharacterDefinition>();
            typeof(CharacterDefinition).GetField("<IsMainCharacter>k__BackingField",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(mainDefinition,true);
            main=Character("main");main.Definition=mainDefinition;
            companion=Character("companion");companion.LifeState=CharacterLifeState.Dead;
            game.TryRegisterCharacter(main);game.TryRegisterCharacter(companion);
            game.PlayerParty.TryAddActiveMember(main.CharacterId);game.PlayerParty.TryAddActiveMember(companion.CharacterId);
            game.PlayerParty.TrySetActiveLeader(main.CharacterId);game.TryAddGold(1000);
        }
        private static CharacterInstance Character(string id)=>new CharacterInstance
        {
            CharacterId=id,Level=1,CurrentHealth=50,LifeState=CharacterLifeState.Alive,
            AvailabilityState=CharacterAvailabilityState.Available,
            Stats=new CharacterStats {MaxHealth=new Stat(100),MaxResource=new Stat(80)},
            ResourcePool=new ResourcePool(ResourceType.Mana,0,80,0)
        };
        [TearDown] public void TearDown(){Object.DestroyImmediate(priestDefinition);Object.DestroyImmediate(priestRole);Object.DestroyImmediate(mainDefinition);GameDefinesRuntime.Configure(null);}
        [TestCase(1,10,300)] [TestCase(11,40,1200)] [TestCase(21,160,4800)] [TestCase(31,640,19200)] [TestCase(41,2560,76800)]
        public void GoldAndRecoveryShareGrowth(int level,int gold,int recovery)
        { Assert.That(CombatEconomyScaling.Scale(10,level),Is.EqualTo(gold));Assert.That(CombatEconomyScaling.Scale(300,level),Is.EqualTo(recovery)); }
        [TestCase(1,0d,5)] [TestCase(1,.5d,10)] [TestCase(1,1d,15)]
        [TestCase(11,0d,20)] [TestCase(11,1d,60)]
        public void EnemyGoldNoiseUsesApprovedRange(int level,double sample,int expected)
        {
            Assert.That(CombatEconomyScaling.RollEnemyGold(level,sample),Is.EqualTo(expected));
        }
        [Test] public void ZeroGoldRemainsZeroAtExtremeLevel()=>Assert.That(CombatEconomyScaling.Scale(0,int.MaxValue),Is.Zero);
        [Test] public void HonorDiscountUsesConfiguredTierAndNeverLoyalty()
        {
            var defines=ScriptableObject.CreateInstance<GameDefines>();
            try
            {
                defines.TryGet("economy.honor.tier_count", out var count); count.CurrentIntegerValue = 2;
                defines.TryGet("economy.honor.tier_1.minimum", out var minimum); minimum.CurrentIntegerValue = 50;
                defines.TryGet("economy.honor.tier_0.discount_percent", out var first); first.CurrentFloatValue = 5;
                defines.TryGet("economy.honor.tier_1.discount_percent", out var second); second.CurrentFloatValue = 15;
                GameDefinesRuntime.Configure(defines);
                Assert.That(HonorPriceSystem.GetDiscountPercent(49),Is.EqualTo(5));
                Assert.That(HonorPriceSystem.GetDiscountPercent(50),Is.EqualTo(15));
                main.Honor=60;priest.LoyaltyLevel=15;
                Assert.That(new CompanionRecoveryService().GetCost(companion,main),Is.EqualTo(255));
            }
            finally{GameDefinesRuntime.Configure(null);Object.DestroyImmediate(defines);}
        }
        [TestCase(int.MinValue,0)] [TestCase(-101,0)] [TestCase(-100,1)] [TestCase(-76,1)]
        [TestCase(-75,2)] [TestCase(-51,2)] [TestCase(-50,3)] [TestCase(-21,3)]
        [TestCase(-20,4)] [TestCase(39,4)] [TestCase(40,5)] [TestCase(99,5)]
        [TestCase(100,6)] [TestCase(149,6)] [TestCase(150,7)] [TestCase(199,7)]
        [TestCase(200,8)] [TestCase(int.MaxValue,8)]
        public void HonorUsesApprovedUnboundedBands(int honor,int tier)
        {
            Assert.That(HonorPriceSystem.GetTierIndex(honor),Is.EqualTo(tier));
            Assert.That(HonorPriceSystem.GetDiscountPercent(honor),Is.Zero);
        }
        [Test] public void StoringDeadCompanionPreservesStoryLock()
        {
            companion.AvailabilityState=CharacterAvailabilityState.StoryLocked;
            CampaignDefeatSystem.StoreRecoverableCompanions(game);
            Assert.That(companion.AvailabilityState,Is.EqualTo(CharacterAvailabilityState.StoryLocked));
            Assert.That(new CompanionRecoveryService().CanRecover(companion),Is.False);
        }
        [Test] public void ExcessiveLevelSaturatesInsteadOfOverflowing()=>Assert.That(CombatEconomyScaling.Scale(300,int.MaxValue),Is.EqualTo(int.MaxValue));
        [Test] public void DeadCompanionDoesNotEndCampaignAndBecomesRecoverable()
        {
            Assert.That(CampaignDefeatSystem.ShouldDefeat(game),Is.False);
            CampaignDefeatSystem.StoreRecoverableCompanions(game);
            Assert.That(companion.AvailabilityState,Is.EqualTo(CharacterAvailabilityState.Recoverable));
            Assert.That(game.PlayerParty.BenchMemberIds,Does.Contain("companion"));
            Assert.That(game.PlayerParty.ActiveLeaderId,Is.EqualTo("main"));
        }
        [Test] public void MainDeathEndsCampaignEvenWithAnotherLivingMember()
        {main.LifeState=CharacterLifeState.Dead;companion.LifeState=CharacterLifeState.Alive;Assert.That(CampaignDefeatSystem.ShouldDefeat(game),Is.True);}
        [Test] public void DownedPartyEndsCampaignOnlyWithoutLivingRescuer()
        {main.LifeState=companion.LifeState=CharacterLifeState.Downed;Assert.That(CampaignDefeatSystem.ShouldDefeat(game),Is.True);main.LifeState=CharacterLifeState.Alive;Assert.That(CampaignDefeatSystem.ShouldDefeat(game),Is.False);}
        [Test] public void DefeatReasonIdentifiesDeadMainCharacter()
        {
            main.LifeState=CharacterLifeState.Dead;
            Assert.That(CampaignDefeatSystem.GetDefeatReason(game,out var deadMain),Is.EqualTo(CampaignDefeatReason.MainCharacterDead));
            Assert.That(deadMain,Is.SameAs(main));
        }
        [Test] public void DefeatReasonDistinguishesDownedPartyFromDeadParty()
        {
            main.LifeState=CharacterLifeState.Downed;
            Assert.That(CampaignDefeatSystem.GetDefeatReason(game,out _),Is.EqualTo(CampaignDefeatReason.NoLivingRescuer));
            main.Definition=null;main.LifeState=CharacterLifeState.Dead;
            Assert.That(CampaignDefeatSystem.GetDefeatReason(game,out _),Is.EqualTo(CampaignDefeatReason.EntirePartyDead));
        }
        [Test] public void LivingRescuerHasNoDefeatReason()
        {
            companion.LifeState=CharacterLifeState.Downed;
            Assert.That(CampaignDefeatSystem.GetDefeatReason(game,out var deadMain),Is.EqualTo(CampaignDefeatReason.None));
            Assert.That(deadMain,Is.Null);
        }
        [Test] public void DefeatedCampaignCannotResumeOrSave()
        {game.MarkDefeated();Assert.That(game.TrySetMode(GameModeState.Exploration),Is.False);Assert.Throws<InvalidOperationException>(()=>new GameSessionSaveSnapshotFactory().CreateSnapshot(game,"defeat"));}
        [Test] public void RecoveryPreservesIdentityProgressAndEquipment_ChargesOnce_NoLoyalty()
        {
            var inventory=companion.Inventory;var equipment=companion.Equipment;companion.CurrentExperience=42;
            CampaignDefeatSystem.StoreRecoverableCompanions(game);
            var service=new CompanionRecoveryService();
            Assert.That(service.TryRecover(game,priest,"main","companion",out _),Is.True);
            Assert.That(game.Gold,Is.EqualTo(700));Assert.That(priest.GoldBalance,Is.EqualTo(300));Assert.That(priest.SpentGoldTotal,Is.Zero);
            Assert.That(companion.LifeState,Is.EqualTo(CharacterLifeState.Alive));Assert.That(companion.CurrentHealth,Is.EqualTo(100));Assert.That(companion.ResourcePool.CurrentValue,Is.EqualTo(80));
            Assert.That(companion.Inventory,Is.SameAs(inventory));Assert.That(companion.Equipment,Is.SameAs(equipment));Assert.That(companion.CurrentExperience,Is.EqualTo(42));
            Assert.That(game.PlayerParty.IsActiveMember("companion"),Is.True);
            Assert.That(service.TryRecover(game,priest,"main","companion",out _),Is.False);Assert.That(game.Gold,Is.EqualTo(700));
        }
        [Test] public void RecoveryWithFullPartyKeepsCompanionOnBench()
        {
            CampaignDefeatSystem.StoreRecoverableCompanions(game);
            for(var i=1;i<game.PlayerParty.MaxActiveMembers;i++){var c=Character("extra"+i);game.TryRegisterCharacter(c);game.PlayerParty.TryAddActiveMember(c.CharacterId);}
            Assert.That(new CompanionRecoveryService().TryRecover(game,priest,"main","companion",out _),Is.True);
            Assert.That(game.PlayerParty.BenchMemberIds,Does.Contain("companion"));Assert.That(companion.AvailabilityState,Is.EqualTo(CharacterAvailabilityState.Available));
        }
        [Test] public void FailedPaymentDoesNotChangeCharacterOrTreasury()
        {
            game.TrySpendGold(999);
            Assert.That(new CompanionRecoveryService().TryRecover(game,priest,"main","companion",out _),Is.False);
            Assert.That(game.Gold,Is.EqualTo(1));Assert.That(priest.GoldBalance,Is.Zero);Assert.That(companion.LifeState,Is.EqualTo(CharacterLifeState.Dead));
        }
        [Test] public void StoryLockedAndMainCharacterAreNotRecoveryCandidates()
        {
            var service=new CompanionRecoveryService();companion.AvailabilityState=CharacterAvailabilityState.StoryLocked;
            Assert.That(service.CanRecover(companion),Is.False);main.LifeState=CharacterLifeState.Dead;Assert.That(service.CanRecover(main),Is.False);
        }
    }
}

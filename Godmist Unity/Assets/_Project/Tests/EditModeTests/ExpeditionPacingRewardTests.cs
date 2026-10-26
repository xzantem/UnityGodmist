using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Serialization;
using Godmist.Data.Dungeons;
using Godmist.Data.World;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Systems.Dungeons;
using Godmist.Systems.Items;
using Godmist.Systems.SaveLoad;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class ExpeditionPacingRewardTests
    {
        static GameContentCatalog Catalog=>AssetDatabase.LoadAssetAtPath<GameContentCatalog>("Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
        static void Set(object target,string name,object value)=>target.GetType().GetField("<"+name+">k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);

        [TestCase(1,1)] [TestCase(10,12)] [TestCase(30,40)] [TestCase(50,50)]
        public void OrdinaryExperienceDoublesAfterLevelPenaltyButBossAndMissionCurveStayUnchanged(int enemyLevel,int heroLevel)
        {
            int before=CharacterProgressionSystem.GetEnemyExperience(enemyLevel,heroLevel);
            Assert.That(CombatRewardSystem.CalculateEnemyExperience(enemyLevel,heroLevel,false),Is.EqualTo(2*before));
            Assert.That(CombatRewardSystem.CalculateEnemyExperience(enemyLevel,heroLevel,true),Is.EqualTo(before));
            Assert.That(CharacterProgressionSystem.GetStandardMissionExperience(enemyLevel),
                Is.EqualTo((int)Math.Round(20d*CharacterProgressionSystem.GetEnemyExperienceBase(enemyLevel))));
        }

        [TestCase("dungeon.catacombs.boss_expedition")]
        [TestCase("dungeon.catacombs.skeleton_slayer_expedition")]
        [TestCase("dungeon.forest.boss_expedition")]
        [TestCase("dungeon.forest.bandit_leader_expedition")]
        public void ProductionBossKeepsExperienceWhileItsGuardsReceiveDoubleWithoutExtraGoldOrLoot(string id)
        {
            var definition=Catalog.DungeonDefinitions.Single(d=>d!=null&&d.Id==id);
            var encounter=definition.BossEncounterDefinition;var dungeon=new DungeonInstance(definition,171023,10,DungeonLength.Short);
            var rewards=new CombatRewardSystem(lootTableSystem:new LootTableSystem(new System.Random(123),null),goldRandom:()=>.4)
                .RollEncounterRewards(encounter,null,10,dungeon);
            var reference=new CombatRewardSystem(lootTableSystem:new LootTableSystem(new System.Random(123),null),goldRandom:()=>.4)
                .RollEncounterRewards(encounter,null,10);
            Assert.That(rewards.EnemyRewards.Count,Is.EqualTo(encounter.Enemies.Count));
            foreach(var reward in rewards.EnemyRewards)
            {
                bool boss=encounter.Enemies[encounter.BossEnemyIndex].InstanceId==reward.EnemyInstanceId;
                Assert.That(reward.IsBoss,Is.EqualTo(boss));
                Assert.That(reward.ExperiencePerActiveCharacter,Is.EqualTo(CharacterProgressionSystem.GetEnemyExperience(10,10)*(boss?1:2)));
                var baseline=reference.EnemyRewards.Single(r=>r.EnemyInstanceId==reward.EnemyInstanceId);
                Assert.That(reward.Gold,Is.EqualTo(baseline.Gold));
                Assert.That(reward.Items.Select(i=>i.Definition.Id),Is.EqualTo(baseline.Items.Select(i=>i.Definition.Id)));
            }
        }

        [Test]
        public void NamespacedRewardsPreserveBossRoleAndAwardMixedBattleOnce()
        {
            var hero=new CharacterInstance{CharacterId="hero",Level=1,LifeState=CharacterLifeState.Alive};
            var game=new GameSession();game.TryRegisterCharacter(hero);game.TryAddCharacterToActiveParty(hero.CharacterId);
            var combat=new CombatSession{Outcome=CombatOutcomeState.Victory};
            foreach(bool boss in new[]{false,true})
            {
                var reward=new CombatRewardResult();reward.EnemyRewards.Add(new CombatEnemyReward
                    {EnemyInstanceId="same-id",ExperienceLevel=1,IsBoss=boss});
                var unit=new BattleUnit{Character=new CharacterInstance{CharacterId="same-id"}};
                DungeonCombatMembershipSystem.RegisterEnemy(combat,boss?"boss encounter":"normal encounter","same-id",reward,unit);
                combat.TryMarkEnemyDefeated(unit.Character.CharacterId);
            }
            Assert.That(combat.Rewards.EnemyRewards.Select(r=>r.IsBoss),Is.EqualTo(new[]{false,true}));
            var system=new CombatRewardSystem();Assert.That(system.TryGrantVictoryRewards(game,combat),Is.True);
            Assert.That(hero.CurrentExperience,Is.EqualTo(180));
            Assert.That(system.TryGrantVictoryRewards(game,combat),Is.False);Assert.That(hero.CurrentExperience,Is.EqualTo(180));
        }

        [TestCase("dungeon.catacombs.boss_expedition")]
        [TestCase("dungeon.forest.bandit_leader_expedition")]
        public void PreparedRewardsRestoreBossRoleWithoutSaveFlagAndCachedRewardsCorrectStaleRole(string id)
        {
            var definition=Catalog.DungeonDefinitions.Single(d=>d!=null&&d.Id==id);
            var dungeon=new DungeonGeneratorService().Generate(definition,171023,10,DungeonLength.Short);
            new DungeonEncounterPlacementSystem().Place(dungeon);
            new DungeonEncounterRewardPreparationSystem().Prepare(dungeon,Catalog.EquipmentPartDefinitions);
            var game=Party();game.TrySetActiveDungeon(dungeon);
            var save=new GameSessionSaveSnapshotFactory().CreateSnapshot(game,"pacing-rewards");
            string json=JsonUtility.ToJson(save);
            Assert.That(json,Does.Not.Contain("\"IsBoss\""),"Boss identity is derived, not a new save-schema flag.");
            Assert.That(new GameSessionRestoreFactory().TryCreate(JsonUtility.FromJson<GameSaveData>(json),Catalog,out var restored),Is.True);
            foreach(var encounter in restored.ActiveDungeon.Floors.SelectMany(f=>f.Encounters))
            {
                Assert.That(restored.ActiveDungeon.TryGetPreRolledRewards(encounter.InstanceId,out var rewards),Is.True);
                foreach(var reward in rewards.EnemyRewards)
                {
                    bool boss=ScoutCombatSystem.IsBoss(restored.ActiveDungeon,encounter.Definition,reward.EnemyInstanceId);
                    Assert.That(reward.IsBoss,Is.EqualTo(boss));
                    Assert.That(reward.ExperiencePerActiveCharacter,Is.EqualTo(CombatRewardSystem.CalculateEnemyExperience(10,10,boss)));
                    reward.IsBoss=!boss;
                }
                var cached=(CombatRewardResult)typeof(CombatSessionFactory).GetMethod("ResolveRewards",BindingFlags.Instance|BindingFlags.NonPublic)
                    .Invoke(new CombatSessionFactory(),new object[]{restored,encounter.Definition,encounter.InstanceId});
                Assert.That(cached,Is.SameAs(rewards));
                foreach(var reward in cached.EnemyRewards)
                    Assert.That(reward.IsBoss,Is.EqualTo(ScoutCombatSystem.IsBoss(restored.ActiveDungeon,encounter.Definition,reward.EnemyInstanceId)));
            }
        }

        [Test]
        public void OldLargeActiveFloorRestoresItsSnapshotAfterProfilesAreShortened()
        {
            var definition=Catalog.DungeonDefinitions.Single(d=>d!=null&&d.Id=="dungeon.forest.expedition");
            var old=UnityEngine.Object.Instantiate(definition);
            try
            {
                var profile=old.GetLengthProfile(DungeonLength.Normal);
                Set(profile,"MinRoomsPerFloor",56);Set(profile,"MaxRoomsPerFloor",68);
                Set(profile,"MinEncountersPerFloor",18);Set(profile,"MaxEncountersPerFloor",22);
                Set(profile,"MinBranchTransitRoomCount",0);Set(profile,"MaxBranchTransitRoomCount",0);
                var dungeon=new DungeonGeneratorService().Generate(old,171024,10,DungeonLength.Normal);
                var game=Party();game.TrySetActiveDungeon(dungeon);
                var saved=new GameSessionSaveSnapshotFactory().CreateSnapshot(game,"old-large-floor");
                Assert.That(new GameSessionRestoreFactory().TryCreate(JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(saved)),Catalog,out var restored),Is.True);
                Assert.That(restored.ActiveDungeon.Floors[0].Rooms.Count,Is.InRange(56,68));
                Assert.That(new HashSet<Vector2Int>(restored.ActiveDungeon.Floors[0].Map.FloorCells)
                    .SetEquals(dungeon.Floors[0].Map.FloorCells),Is.True);
                Assert.That(new HashSet<DungeonSurfaceEdge>(restored.ActiveDungeon.Floors[0].Map.SurfaceWallEdges)
                    .SetEquals(dungeon.Floors[0].Map.SurfaceWallEdges),Is.True);
                Assert.That(definition.GetLengthProfile(DungeonLength.Normal).MinRoomsPerFloor,Is.EqualTo(28));
            }
            finally{UnityEngine.Object.DestroyImmediate(old);}
        }

        static GameSession Party()
        {
            var game=new GameSession(GameModeState.Exploration);var hero=EnemyScalingBalanceTests.Hero(0,1,false,true);
            game.TryRegisterCharacter(hero);game.TryAddCharacterToActiveParty(hero.CharacterId);game.TrySetActiveLeader(hero.CharacterId);return game;
        }
    }
}

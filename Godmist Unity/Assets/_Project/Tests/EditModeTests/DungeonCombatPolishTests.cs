using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Skills;
using Godmist.Data.World;
using Godmist.Editor;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Views.Exploration;
using Godmist.Views.Dungeons;
using TMPro;
using UnityEngine.Tilemaps;
using Object=UnityEngine.Object;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Views.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class DungeonCombatPolishTests
    {
        const string Output = "../LocalHandoffs/dungeon-combat-polish-20261025";
        static BattleUnit Unit(bool player = true) => new()
        {
            IsPlayerSide = player, CurrentActionPoints = 1000, MaxActionPoints = 1000,
            Character = new CharacterInstanceFactory().Create(AssetDatabase.LoadAssetAtPath<CharacterDefinition>(
                "Assets/_Project/ScriptableObjects/Characters/CD_Jerzy.asset"))
        };
        static string Text(CombatFeedbackEvent e) => (string)typeof(CombatFloatingFeedbackController)
            .GetMethod("GetText", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] {e});
        [Test, Explicit("Approved local authoring via Editor API only.")]
        public void ApplyApprovedContent() => DungeonCombatPolishSetup.Apply();
        [Test]
        public void ZeroKnivesAllowsOnlyPointBlankFreeKnifeToss()
        {
            var caster=Unit(); caster.Position=new GridPosition(0,0); caster.AvailableKnives=0;
            var target=Unit(false); target.Position=new GridPosition(1,0);
            var skill=AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Skills/SD_KnifeToss.asset");
            Assert.That(skill.FreeKnivesInMelee,Is.True);
            Assert.That(ScoutKnifeSystem.CanPay(caster,skill,target),Is.True);
            var session=new CombatSession {Grid=new BattleGrid(5,1),ActiveUnit=caster};
            session.Units.Add(caster);session.Units.Add(target);
            var groups=new Dictionary<int,IReadOnlyList<BattleUnit>> { [0]=new[]{target} };
            Assert.That(new SkillExecutionSystem().CanPreview(session,caster,skill,groups,null,null,null),Is.True);
            Assert.That(ScoutKnifeSystem.TrySpendAndDrop(session,caster,skill,new[]{target}),Is.True);
            Assert.That(caster.AvailableKnives,Is.Zero); Assert.That(session.MapObjects,Is.Empty);
            target.Position=new GridPosition(3,0);
            Assert.That(ScoutKnifeSystem.CanPay(caster,skill,target),Is.False);
            Assert.That(ScoutKnifeSystem.TrySpendAndDrop(session,caster,skill,new[]{target}),Is.False);
            Assert.That(caster.AvailableKnives,Is.Zero);
        }
        [Test]
        public void HiddenEnemyCannotCutAHoleInPlayerMovementPreviewButStillBlocksExecution()
        {
            var caster=Unit(); caster.Position=new GridPosition(0,0);
            var enemy=Unit(false); enemy.Position=new GridPosition(1,0);
            enemy.Character.PassiveEffects.Add(new PassiveEffectRuntime {
                EffectId=Godmist.Core.Constants.StatusEffectIds.Invisibility,
                Lifetime=PassiveEffectLifetime.Timed, RemainingDuration=2 });
            var session=new CombatSession {Grid=new BattleGrid(4,1)};
            session.Units.Add(caster);session.Units.Add(enemy);
            var movement=new CombatMovementSystem();
            Assert.That(movement.CalculateReachablePaths(session,caster),Does.Not.ContainKey(enemy.Position));
            var preview=movement.CalculateReachablePaths(session,caster,forPlayerPreview:true);
            Assert.That(preview,Does.ContainKey(enemy.Position));
            Assert.That(preview,Does.ContainKey(new GridPosition(3,0)));
            Assert.That(session.IsPositionOccupied(enemy.Position,caster),Is.True);
            enemy.Character.PassiveEffects.Clear();
            Assert.That(movement.CalculateReachablePaths(session,caster,forPlayerPreview:true),Does.Not.ContainKey(enemy.Position));
            enemy.IsPlayerSide=true;
            Assert.That(movement.CalculateReachablePaths(session,caster,forPlayerPreview:true),Does.Not.ContainKey(enemy.Position));
        }
        [Test]
        public void OtherCombatOccupancyIsFilteredOnlyForPreview()
        {
            var caster=Unit();caster.Position=new GridPosition(0,0);
            var session=new CombatSession {Grid=new BattleGrid(4,1),
                ExternalPositionOccupied=p=>p==new GridPosition(1,0),
                ExternalPreviewPositionOccupied=p=>false};
            session.Units.Add(caster);
            var movement=new CombatMovementSystem();
            Assert.That(movement.CalculateReachablePaths(session,caster),Is.Empty);
            Assert.That(movement.CalculateReachablePaths(session,caster,forPlayerPreview:true),Does.ContainKey(new GridPosition(3,0)));
        }
        [TestCase(DamageType.Bleed,"krwawienie")]
        [TestCase(DamageType.Poison,"trucizna")]
        [TestCase(DamageType.Burn,"podpalenie")]
        public void ReviveKeepsDotAndLethalTickExplainsItsCause(DamageType type,string name)
        {
            var target=Unit();target.Character.CurrentHealth=0;target.Character.LifeState=CharacterLifeState.Downed;
            var effect=new PassiveEffectRuntime {
                EffectId=type==DamageType.Bleed?Godmist.Core.Constants.StatusEffectIds.Bleed:type==DamageType.Poison?Godmist.Core.Constants.StatusEffectIds.Poison:Godmist.Core.Constants.StatusEffectIds.Burn,
                Lifetime=PassiveEffectLifetime.Timed,RemainingDuration=3,TickPhase=PassiveEffectTickPhase.TurnStart,TotalDamage=300 };
            target.Character.PassiveEffects.Add(effect);
            var bus=new CombatFeedbackBus();var entries=new List<CombatFeedbackEvent>();bus.Published+=entries.Add;
            var recovery=new CombatRecoverySystem(feedbackBus:bus);
            Assert.That(recovery.Heal(target,1),Is.EqualTo(1));
            Assert.That(target.Character.LifeState,Is.EqualTo(CharacterLifeState.Alive));
            Assert.That(target.Character.PassiveEffects,Does.Contain(effect));
            var session=new CombatSession();session.Units.Add(target);
            var damage=new CombatDamageSystem(feedbackBus:bus);
            var lifecycle=new PassiveEffectLifecycleSystem(turnProcessors:new IPassiveEffectTurnProcessor[]{new DamageOverTimePassiveEffectProcessor(damage)});
            new StatusEffectSystem(damageSystem:damage,passiveEffectLifecycleSystem:lifecycle,feedbackBus:bus).ProcessTurnStart(session,target);
            Assert.That(target.Character.LifeState,Is.EqualTo(CharacterLifeState.Downed));
            var damageEntry=entries.Single(e=>e.Type==CombatFeedbackType.Damage);
            Assert.That(Text(damageEntry),Is.EqualTo($"-{damageEntry.Amount}"));
            Assert.That(entries.Any(e=>e.Type==CombatFeedbackType.Downed && Text(e)==string.Empty),Is.True);
        }
        [TestCase(false)] [TestCase(true)]
        public void ActualHealCriticalIsPresentationMetadataNotAnotherRoll(bool critical)
        {
            var target=Unit();target.Character.CurrentHealth=1;
            var events=new List<CombatFeedbackEvent>();var bus=new CombatFeedbackBus();bus.Published+=events.Add;
            var amount=new CombatRecoverySystem(feedbackBus:bus).Heal(target,10,isCritical:critical);
            Assert.That(amount,Is.EqualTo(10));Assert.That(target.Character.CurrentHealth,Is.EqualTo(11));
            var entry=events.Single(e=>e.Type==CombatFeedbackType.Heal);
            Assert.That(entry.IsCritical,Is.EqualTo(critical));
            Assert.That(Text(entry),Is.EqualTo(critical?"+10 (kryt)":"+10"));
        }
        [TestCase(false)] [TestCase(true)]
        public void PaladinHealCarriesItsActualCriticalResultIntoFloatingFeedback(bool critical)
        {
            var caster=new BattleUnit {IsPlayerSide=true,Position=new GridPosition(0,0),CurrentActionPoints=10000,
                Character=new CharacterInstanceFactory().Create(AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/ScriptableObjects/Characters/CD_Paladin.asset"))};
            caster.Character.Stats.CritChance=new Godmist.Core.Types.Stat(critical?1:0);
            caster.Character.Stats.CritDamage=new Godmist.Core.Types.Stat(2);
            caster.Character.ResourcePool.CurrentValue=caster.Character.ResourcePool.MaxValue;
            var progress=new Godmist.Runtime.Characters.SkillTreeProgress {TreeId=PaladinCombatSystem.Priest};
            caster.Character.SkillTreeProgress.Add(progress);
            progress.NodeInvestments.Add(new Godmist.Runtime.Characters.SkillTreeNodeInvestment {NodeId="node.paladin.priest.unexpected_grace",Ranks=2});
            var target=Unit();target.Position=new GridPosition(1,0);target.Character.CurrentHealth=1;
            var session=new CombatSession {Grid=new BattleGrid(3,1),ActiveUnit=caster};session.Units.Add(caster);session.Units.Add(target);
            caster.WarriorState.Session=session;target.WarriorState.Session=session;
            var entries=new List<CombatFeedbackEvent>();var bus=new CombatFeedbackBus();bus.Published+=entries.Add;
            var execution=new SkillExecutionSystem(statusEffectSystem:new StatusEffectSystem(),recoverySystem:new CombatRecoverySystem(feedbackBus:bus),feedbackBus:bus);
            var skill=AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Skills/SD_PaladinHeal.asset");
            Assert.That(execution.TryExecute(session,caster,skill,target),Is.True);
            var heal=entries.Single(e=>e.Type==CombatFeedbackType.Heal && e.Target==target);
            Assert.That(heal.IsCritical,Is.EqualTo(critical));
            Assert.That(Text(heal).Contains("kryt"),Is.EqualTo(critical));
        }
        [Test]
        public void LoadInCombatReachesSaveReaderAndSaveRemainsForbidden()
        {
            var root=new GameObject("Load in combat fixture");root.SetActive(false);
            var directory=Path.GetFullPath(Output+"/isolated-save-fixture");
            var catalog=ScriptableObject.CreateInstance<GameContentCatalog>();
            try
            {
                var save=new Godmist.Systems.SaveLoad.SaveManager(directory);
                var game=new GameSession(GameModeState.Exploration,"polish-fixture");
                Assert.That(save.TrySaveQuick(game,out var error),Is.True,error);
                Assert.That(save.TrySaveManual(game,"polish-fixture",out var summary,out error),Is.True,error);
                Assert.That(game.TrySetActiveCombat(new CombatSession {CombatId="polish-combat"}),Is.True);
                Assert.That(game.HasCombat,Is.True);
                var bootstrap=root.AddComponent<Godmist.Views.Game.GameBootstrap>();
                typeof(Godmist.Views.Game.GameBootstrap).GetProperty("Session").SetValue(bootstrap,game);
                typeof(Godmist.Views.Game.GameBootstrap).GetProperty("SaveManager").SetValue(bootstrap,save);
                Set(bootstrap,"contentCatalog",catalog);
                // Leave loader deliberately unbound: reads must succeed, then stop
                // at replacement without changing the user's active scene/session.
                Assert.That(bootstrap.TryLoadQuick(out error),Is.False);
                Assert.That(error,Is.EqualTo("Nie udało się przygotować odtworzonej sesji."));
                Assert.That(bootstrap.TryLoadSave(summary,out error),Is.False);
                Assert.That(error,Is.EqualTo("Nie udało się przygotować odtworzonej sesji."));
                Assert.That(save.TrySaveQuick(game,out error),Is.False);
                Assert.That(error,Does.Contain("podczas walki"));
                Assert.That(save.TrySaveManual(game,"blocked",out _,out error),Is.False);
                Assert.That(bootstrap.Session,Is.SameAs(game));
            }
            finally
            {
                Object.DestroyImmediate(root);Object.DestroyImmediate(catalog);
                if(Directory.Exists(directory)) Directory.Delete(directory,true);
            }
        }
        static void Set(object owner,string name,object value) => owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(owner,value);
        [TestCase(0,"0/-2")] [TestCase(1,"-1/-2")] [TestCase(2,"-2/-2")]
        public void MapTitleShowsActualDungeonDepth(int index,string label)
        {
            var root=new GameObject("Map depth fixture");root.SetActive(false);
            try
            {
                var party=root.AddComponent<ExplorationPartyController>();
                var game=new GameSession();var dungeon=new DungeonInstance("test",1);
                for(var n=0;n<3;n++) dungeon.TryAddFloor(new DungeonFloorInstance(n));
                game.TrySetActiveDungeon(dungeon);Set(party,"gameSession",game);
                var titleRoot=new GameObject("title",typeof(RectTransform),typeof(TextMeshProUGUI));titleRoot.transform.SetParent(root.transform);
                var view=root.AddComponent<DungeonMinimapView>();var text=titleRoot.GetComponent<TextMeshProUGUI>();Set(view,"title",text);
                view.Bind(dungeon.Floors[index],null,party,null);
                Assert.That(text.text,Is.EqualTo("Mapa - poziom "+label));
            }
            finally {Object.DestroyImmediate(root);}
        }
        [TestCase(false)] [TestCase(true)]
        public void ExitAndItsOutlineCannotRenderThroughUnexploredFog(bool locked)
        {
            var root=new GameObject("Exit fog fixture",typeof(Grid));root.SetActive(false);
            try
            {
                var tiles=new GameObject("Floor",typeof(Tilemap),typeof(TilemapRenderer));tiles.transform.SetParent(root.transform);
                var map=root.AddComponent<DungeonFloorMapView>();Set(map,"floorTilemap",tiles.GetComponent<Tilemap>());
                Set(map,"wallSegmentPrefab",AssetDatabase.LoadAssetAtPath<DungeonWallSegmentView>("Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab"));
                Set(map,"floorTile",AssetDatabase.LoadAssetAtPath<TileBase>("Assets/_Project/Art/Tiles/T_DungeonFloor_0x72.asset"));
                var floor=new DungeonFloorInstance(0);var geometry=new DungeonFloorMap();geometry.TryAddFloorCell(Vector2Int.zero,"room");floor.TrySetMap(geometry);map.Rebuild(geometry);
                var party=root.AddComponent<ExplorationPartyController>();Set(party,"traversalFloor",floor);Set(party,"traversalMapView",map);
                var exit=Object.Instantiate(AssetDatabase.LoadAssetAtPath<DungeonExitView>("Assets/_Project/Prefabs/Dungeon/PF_DungeonExitView.prefab"),root.transform);
                exit.InitializeTransition(party,map.CellToWorldCenter(Vector2Int.zero),()=>{}, requiresClearedExitRoom: locked);exit.ConfigureFogPresentation();
                Assert.That(exit.IsExitBlocked, Is.EqualTo(locked));
                var renderers=exit.GetComponentsInChildren<SpriteRenderer>(true);
                Assert.That(renderers,Is.Not.Empty);Assert.That(renderers.All(r=>r.forceRenderingOff),Is.True);
                var node=new DungeonTraversalPosition(new DungeonSurfacePosition(Vector2Int.zero));floor.Fog.SetVisible(new[]{node});
                typeof(DungeonExitView).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(exit,null);
                Assert.That(renderers.All(r=>!r.forceRenderingOff),Is.True);
                floor.Fog.SetVisible(Array.Empty<DungeonTraversalPosition>());
                typeof(DungeonExitView).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(exit,null);
                Assert.That(renderers.All(r=>!r.forceRenderingOff),Is.True,"Remembered exit remains under the terrain fog material.");
                floor.Fog.SetMapDiscovered(false);
                typeof(DungeonExitView).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(exit,null);
                Assert.That(renderers.All(r=>r.forceRenderingOff),Is.True);
            }
            finally {Object.DestroyImmediate(root);}
        }
        [Test]
        public void ExportPlayerSkillCosts()
        {
            var catalog=AssetDatabase.LoadAssetAtPath<GameContentCatalog>("Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
            var skills=catalog.SkillDefinitions.Where(s=>s!=null && !s.SkillId.StartsWith("skill.enemy.") && !s.SkillId.Contains("boss"))
                .Distinct().OrderByDescending(s=>s.ResourceCost).ThenBy(s=>s.SkillId).ToArray();
            Directory.CreateDirectory(Output);
            File.WriteAllLines(Output+"/skill-costs.tsv",new[]{"id\tname\tresourceCost\tactionCost\tdescription\tasset"}.Concat(skills.Select(s=>$"{s.SkillId}\t{s.DisplayName}\t{s.ResourceCost}\t{s.ActionCost}\t{s.TooltipDescription.Replace("\n"," ")}\t{AssetDatabase.GetAssetPath(s)}")));
            Assert.That(skills.Single(s=>s.SkillId=="skill.scout.weak_point").ResourceCost,Is.EqualTo(50));
        }
    }
}

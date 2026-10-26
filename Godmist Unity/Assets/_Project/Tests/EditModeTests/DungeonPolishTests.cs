using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Combat;
using Godmist.Data.Items;
using Godmist.Editor;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Systems.Dungeons;
using Godmist.Views.Combat;
using Godmist.Views.Dungeons;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public sealed partial class DungeonPolishTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target,value);
        static T Get<T>(object target,string name) => (T)target.GetType().GetField(name,Flags).GetValue(target);
        static DungeonTraversalPosition At(int x,int y=0) => new(new DungeonSurfacePosition(new Vector2Int(x,y)));

        [Test, Explicit("User-authorized prefab/content authoring, outside Play Mode only.")]
        public void ApplyContentAndValidateReferences()
        {
            DungeonPolishSetup.Apply();
            var path="Assets/_Project/Prefabs/UI/Workspace/PF_UIW_DungeonLootWindow.prefab";
            var loot=AssetDatabase.LoadAssetAtPath<DungeonLootWindowView>(path);
            var button=Get<Button>(loot,"collectAllButton");
            Assert.That(button,Is.Not.Null);
            Assert.That(button.GetComponentInChildren<TMP_Text>(true).text,Is.EqualTo("Zbierz wszystko [B]"));
            var ear=AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/ScriptableObjects/ContentPass1/item.vs10.ucho_elfa.asset");
            Assert.That(ear.Icon,Is.Not.Null);
            var old=AssetDatabase.LoadAssetAtPath<EncounterDefinition>("Assets/_Project/ScriptableObjects/Dungeons/Forest/EncountersBalanced/ED_encounter_forest_bandit_patrol_balanced.asset");
            var next=AssetDatabase.LoadAssetAtPath<EncounterDefinition>("Assets/_Project/ScriptableObjects/Dungeons/Forest/EncountersBalanced/ED_encounter_forest_bandit_patrol_balanced_v2.asset");
            Assert.That(old.Enemies.Count,Is.EqualTo(4),"Old layouts must still restore their original identities.");
            Assert.That(next.Enemies.Select(e=>e.CharacterDefinition.Id),Is.EqualTo(new[]{"enemy.forest.bandit_thug","enemy.forest.bandit_thug","enemy.forest.bandit_shooter"}));
        }

        [Test]
        public void ProductionItemsHaveResolvableIcons()
        {
            var catalog=AssetDatabase.LoadAssetAtPath<Godmist.Data.World.GameContentCatalog>("Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
            var production=catalog.ItemDefinitions.Where(d=>d!=null&&!d.Id.Contains(".test.")).ToArray();
            var missing=production.Where(d=>d.Icon==null).Select(d=>d.Id).ToArray();
            System.IO.Directory.CreateDirectory(Output);
            System.IO.File.WriteAllText(System.IO.Path.Combine(Output,"catalog-icons.txt"),
                $"Production items: {production.Length}; missing icons: {missing.Length}; excluded test definitions and empty legacy catalog slots.\n"+string.Join("\n",missing));
            Assert.That(missing,Is.Empty);
        }

        [Test]
        public void SharedVisibilityAndDiscoveryHaveTwentyCellBoundaryAndRememberSeenTerrain()
        {
            var floor=new DungeonFloorInstance(0);var map=new DungeonFloorMap();
            for(var x=0;x<50;x++)map.TryAddFloorCell(new Vector2Int(x,0),"room");
            floor.TrySetMap(map);var fog=new DungeonFogSystem();
            fog.Update(floor,new[]{At(0)});
            Assert.That(floor.Fog.IsVisible(At(9)),Is.True,"Long-range skills retain observable targets.");
            Assert.That(floor.Fog.IsVisible(At(20)),Is.True);
            Assert.That(floor.Fog.IsVisible(At(21)),Is.False);
            Assert.That(floor.Fog.Discovered,Is.EquivalentTo(floor.Fog.Visible));
            fog.Update(floor,new[]{At(40)});
            Assert.That(floor.Fog.IsVisible(At(0)),Is.False);
            Assert.That(floor.Fog.Discovered.Contains(At(0)),Is.True);
        }

        [TestCase(DungeonWallType.Low,true)]
        [TestCase(DungeonWallType.Full,false)]
        [TestCase(DungeonWallType.Railing,true)]
        [TestCase(DungeonWallType.TreeTrunk,true)]
        public void LowBarriersNeverBlockObservationOrCombatOpeningMembership(DungeonWallType type,bool expected)
        {
            var floor=new DungeonFloorInstance(0);var map=new DungeonFloorMap();
            for(var x=0;x<4;x++)map.TryAddFloorCell(new Vector2Int(x,0),"room");
            map.SetWallType(new DungeonCellEdge(new Vector2Int(1,0),new Vector2Int(2,0)),type);floor.TrySetMap(map);
            var definition=AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/ScriptableObjects/Characters/CD_Wladimir_Test.asset");
            var factory=new CharacterInstanceFactory();
            BattleUnit Unit(int x,bool player) => new(){Character=factory.Create(definition),Position=new GridPosition(x,0),IsPlayerSide=player};
            var actor=Unit(3,true);var ally=Unit(0,true);var enemy=Unit(2,false);
            var session=new CombatSession();session.Units.AddRange(new[]{actor,ally,enemy});
            var visibility=new DungeonVisibilitySystem();
            bool Observe(BattleUnit from,BattleUnit to) => visibility.CanObserveCombat(floor,At(from.Position.X),6,At(to.Position.X));
            Assert.That(Observe(enemy,ally),Is.EqualTo(expected));
            var included=DungeonCombatOpeningSystem.CollectParticipants(session,actor,new[]{enemy},enemy,_=>Array.Empty<BattleUnit>(),Observe);
            Assert.That(included.Contains(ally),Is.EqualTo(expected));
        }

        [Test]
        public void HoverRangeDoesNotSelectOrSpendAndClearsOnExit()
        {
            var root=new GameObject("hover regression");
            try
            {
                var feedback=root.AddComponent<CombatGridFeedbackRenderer>();
                var targeting=root.AddComponent<CombatTargetSelectionController>();Set(targeting,"feedbackRenderer",feedback);
                targeting.PresentSkillHover(new[]{new GridPosition(1,1),new GridPosition(2,1)});
                Assert.That(targeting.SelectedSkill,Is.Null);
                Assert.That(Get<Dictionary<CombatGridFeedbackLayer,(GridPosition[] Cells,Color Color,Func<GridPosition,Color> Tint)>>(feedback,"outlineSources")[CombatGridFeedbackLayer.SkillHover].Cells.Length,Is.EqualTo(2));
                targeting.SetHoveredSkill(null);
                Assert.That(Get<Dictionary<CombatGridFeedbackLayer,(GridPosition[] Cells,Color Color,Func<GridPosition,Color> Tint)>>(feedback,"outlineSources").ContainsKey(CombatGridFeedbackLayer.SkillHover),Is.False);
            }
            finally{Object.DestroyImmediate(root);}
        }
    }
}

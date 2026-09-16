using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Dungeons;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEngine;
namespace Godmist.Tests.EditMode
{
    public class DestructibleObstacleTests
    {
        readonly List<Object> assets = new();
        [TearDown] public void Cleanup() { foreach (var a in assets) Object.DestroyImmediate(a); }
        T Asset<T>() where T : ScriptableObject { var a = ScriptableObject.CreateInstance<T>(); assets.Add(a); return a; }
        static void Set(object obj, string field, object value) => obj.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(obj, value);
        ObstacleCombatMapObject Obstacle(int hits = 3, int x = 2)
        {
            var definition = Asset<ObstacleDungeonObjectDefinition>();
            var source = new ObstacleDungeonObjectInstance("obstacle" + x, definition, new Vector2Int(x,0));
            source.RestoreDurability(true, hits, hits);
            return new ObstacleCombatMapObject(source.InstanceId, new GridPosition(x,0), true, true, source);
        }
        CombatSession Session(ObstacleCombatMapObject obstacle)
        {
            var session = new CombatSession { Grid = new BattleGrid(8, 3) };
            session.MapObjects.Add(obstacle);
            session.Grid.DynamicBlocked = p => session.MapObjects.Any(o => o.Position == p && o.BlocksMovement);
            var def = Asset<CharacterDefinition>(); Set(def,"<DisplayName>k__BackingField","test");
            var unit = new BattleUnit { Position = new GridPosition(0,0), CurrentActionPoints = 100, IsPlayerSide = true,
                Character = new CharacterInstance { Definition = def, CharacterId = "player", LifeState = CharacterLifeState.Alive,
                    CurrentHealth = 100, Stats = new CharacterStats { MaxHealth = new Stat(100), Speed = new Stat(100), MinAttack = new Stat(10), MaxAttack = new Stat(10) } } };
            session.Units.Add(unit); session.ActiveUnit = unit; return session;
        }
        SkillData Skill(EffectType type = EffectType.DealDamage, int hits = 1)
        {
            var skill = Asset<SkillData>(); Set(skill,"<SkillId>k__BackingField","break"); Set(skill,"<DisplayName>k__BackingField","break");
            Set(skill,"<RequiresLineOfSight>k__BackingField",true);
            var mode = new SkillTargetModeData(); Set(mode,"absoluteRange",new SkillRangeData(RangeShape.Square,0,6));
            Set(skill,"targetModes",new List<SkillTargetModeData>{mode});
            var effect = new SkillEffectData(); Set(effect,"effectType",type); Set(effect,"damageHitCount",hits); Set(effect,"displacementDistance",5);
            Set(skill,"effects",new List<SkillEffectData>{effect}); return skill;
        }
        [Test]
        public void DefaultIsIndestructibleAndHitDamageIsClamped()
        {
            var source = new ObstacleDungeonObjectInstance("old",Asset<ObstacleDungeonObjectDefinition>(),Vector2Int.zero);
            Assert.That(source.ApplyHits(10), Is.False); Assert.That(source.IsActive, Is.True);
            source.RestoreDurability(true,3,3); Assert.That(source.ApplyHits(2), Is.True);
            Assert.That(source.RemainingHits, Is.EqualTo(1)); source.ApplyHits(9);
            Assert.That(source.RemainingHits, Is.Zero); Assert.That(source.IsActive, Is.False); Assert.That(source.ApplyHits(1), Is.False);
        }
        [TestCase(true)] [TestCase(false)]
        public void CollisionAndDamageCountOnlyOneSharedHitRegardlessOfOrder(bool collisionFirst)
        {
            var obj = Obstacle(5); var session = Session(obj);
            using (new CombatObjectDamageAction(session))
            {
                if (collisionFirst) CombatObjectDamageSystem.Impact(session,new GridPosition(1,0),obj.Position);
                CombatObjectDamageSystem.ApplyDamage(session,obj,2);
                if (!collisionFirst) CombatObjectDamageSystem.Impact(session,new GridPosition(1,0),obj.Position);
                Assert.That(obj.Source.RemainingHits, Is.EqualTo(3));
            }
            Assert.That(session.ObjectDamageAction, Is.Null);
        }
        [Test]
        public void DestroyedSharedSourceUnblocksAllSessionsAndSight()
        {
            var a = Obstacle(1); var first = Session(a);
            var b = new ObstacleCombatMapObject(a.ObjectId,a.Position,true,true,a.Source); var second = Session(b);
            Assert.That(second.Grid.IsBlocked(a.Position), Is.True);
            Assert.That(new CombatLineOfSightService().HasLineOfSight(second,new GridPosition(0,0),new GridPosition(3,0)), Is.False);
            CombatObjectDamageSystem.ApplyDamage(first,a,1);
            Assert.That(second.Grid.IsBlocked(a.Position), Is.False);
            Assert.That(second.GetMapObjectAt(a.Position), Is.Null);
            Assert.That(new CombatLineOfSightService().HasLineOfSight(second,new GridPosition(0,0),new GridPosition(3,0)), Is.True);
        }
        [TestCase(EffectType.DealDamage)] [TestCase(EffectType.Charge)]
        public void SkillCanTargetOnlyAnObstacleAndChargeStopsBeforeDestroyedCell(EffectType type)
        {
            var obj = Obstacle(1); var session = Session(obj); var skill = Skill(type);
            var groups = new Dictionary<int,IReadOnlyList<BattleUnit>> { [0] = new List<BattleUnit>() };
            Assert.That(new SkillExecutionSystem().TryExecute(session,session.ActiveUnit,skill,groups,null,null,
                new Dictionary<int,GridPosition?> { [0] = obj.Position }), Is.True);
            Assert.That(obj.Source.IsActive, Is.False);
            Assert.That(session.ActiveUnit.Position, Is.EqualTo(type == EffectType.Charge ? new GridPosition(1,0) : new GridPosition(0,0)));
        }
        [Test]
        public void DamageThenChargeStopsAtOriginalBarrierWithoutAnotherHit()
        {
            var obj = Obstacle(1); var session = Session(obj); var skill = Skill();
            var charge = new SkillEffectData(); Set(charge,"effectType",EffectType.Charge); Set(charge,"displacementDistance",5);
            Set(skill,"effects", new List<SkillEffectData> { skill.Effects[0], charge });
            Assert.That(new SkillExecutionSystem().TryExecute(session, session.ActiveUnit, skill,
                new Dictionary<int,IReadOnlyList<BattleUnit>> { [0] = new List<BattleUnit>() },null,null,
                new Dictionary<int,GridPosition?> { [0] = obj.Position }), Is.True);
            Assert.That(obj.Source.IsActive, Is.False);
            Assert.That(session.ActiveUnit.Position, Is.EqualTo(new GridPosition(1,0)));
        }
        [Test]
        public void PushedEnemyStopsEvenWhenCollisionDestroysObstacle()
        {
            var obj = Obstacle(1); var session = Session(obj); var skill = Skill(EffectType.Push);
            var enemy = Session(Obstacle(3,6)).ActiveUnit;
            enemy.IsPlayerSide = false; enemy.Character.CharacterId = "enemy"; enemy.Position = new GridPosition(1,0);
            session.Units.Add(enemy);
            Assert.That(new SkillExecutionSystem().TryExecute(session, session.ActiveUnit, skill,
                new Dictionary<int,IReadOnlyList<BattleUnit>> { [0] = new List<BattleUnit> { enemy } },null,null,
                new Dictionary<int,GridPosition?> { [0] = enemy.Position }), Is.True);
            Assert.That(obj.Source.IsActive, Is.False);
            Assert.That(enemy.Position, Is.EqualTo(new GridPosition(1,0)));
        }
        [TestCase(false)] [TestCase(true)]
        public void ExplorationApproachesStrikesAndMovementCancelsFurtherHits(bool noclip)
        {
            var root = new GameObject("destruction test");
            try
            {
                var grid = new GameObject("grid", typeof(Grid)); grid.transform.SetParent(root.transform);
                var tiles = new GameObject("tiles", typeof(UnityEngine.Tilemaps.Tilemap)); tiles.transform.SetParent(grid.transform);
                var mapView = root.AddComponent<Godmist.Views.Dungeons.DungeonFloorMapView>();
                Set(mapView,"floorTilemap",tiles.GetComponent<UnityEngine.Tilemaps.Tilemap>());
                var map = new DungeonFloorMap();
                for (int x = 0; x < 4; x++) map.TryAddFloorCell(new Vector2Int(x,0),"room");
                var floor = new DungeonFloorInstance(0); floor.TrySetMap(map);
                var obstacle = Obstacle(); floor.TryAddObject(obstacle.Source);
                var character = Session(obstacle).ActiveUnit.Character;
                var session = new Godmist.Runtime.Game.GameSession(GameModeState.Exploration); session.TryRegisterCharacter(character);
                session.PlayerParty.TryAddActiveMember(character.CharacterId); session.PlayerParty.TrySetActiveLeader(character.CharacterId);
                var actor = new GameObject("actor").AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>(); actor.transform.SetParent(root.transform);
                actor.Bind(character); actor.SetPosition(new Vector2(.5f,.5f)); actor.ConfigureTerrain(map,mapView,floor);
                var controller = root.AddComponent<Godmist.Views.Exploration.ExplorationPartyController>();
                controller.Initialize(session,new[] { actor }); controller.ConfigureTraversal(floor,mapView,null); controller.SetNoclip(noclip);
                Assert.That(controller.BeginDestroyObstacle(obstacle.Source), Is.True);
                var tick = controller.GetType().GetMethod("TickDestroyObstacle",BindingFlags.Instance | BindingFlags.NonPublic);
                Set(controller,"nextDestroyHit",-1f); tick.Invoke(controller,new object[] { actor });
                Assert.That(obstacle.Source.RemainingHits, Is.EqualTo(3), "Must approach before hitting.");
                Assert.That(controller.GetType().GetField("destroyTarget",BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller), Is.Not.Null, "Order retained while approaching.");
                var fixedTick = controller.GetType().GetMethod("FixedUpdate",BindingFlags.Instance | BindingFlags.NonPublic);
                for (var i=0; i<100 && obstacle.Source.RemainingHits==3; i++)
                {
                    Set(controller,"nextDestroyHit",-1f);
                    fixedTick.Invoke(controller,null);
                }
                Assert.That(obstacle.Source.RemainingHits, Is.EqualTo(2));
                controller.CancelMovement(); Set(controller,"nextDestroyHit",-1f);
                tick.Invoke(controller,new object[] { actor });
                Assert.That(obstacle.Source.RemainingHits, Is.EqualTo(2));
            }
            finally { Object.DestroyImmediate(root); }
        }
        [Test]
        public void ObstacleUiHasAuthoredHealthBarAndSceneReferences()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_ObstacleHealth.prefab");
            Assert.That(prefab, Is.Not.Null);
            var slider = prefab.GetComponent<UnityEngine.UI.Slider>();
            Assert.That(slider, Is.Not.Null); Assert.That(slider.fillRect, Is.Not.Null);
            Assert.That(slider.interactable, Is.False);
            Assert.That(prefab.GetComponentsInChildren<Component>(true).Any(c => c != null && c.GetType().Name == "TextMeshProUGUI"), Is.True);
            Assert.That(prefab.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).All(g => !g.raycastTarget), Is.True);
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox/DungeonSandbox.unity",UnityEditor.SceneManagement.OpenSceneMode.Additive);
            try
            {
                var owner = scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Godmist.Views.Dungeons.DungeonTacticalTooltip>(true)).Single();
                var data = new UnityEditor.SerializedObject(owner);
                foreach (var name in new[] { "partyController", "obstacleMenu", "obstacleHealthRoot", "obstacleHealthSlider", "obstacleHealthText" })
                    Assert.That(data.FindProperty(name).objectReferenceValue, Is.Not.Null, name);
                var root = (RectTransform)data.FindProperty("obstacleHealthRoot").objectReferenceValue;
                Assert.That(root.gameObject.activeSelf, Is.False);
                var menu = (Godmist.Views.UI.Workspace.PartyRailMenuView)data.FindProperty("obstacleMenu").objectReferenceValue;
                Assert.That(menu.gameObject.activeSelf, Is.False);
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true); }
        }
        [Test]
        public void WallOccludesDamageAndPreventsCollisionBehindIt()
        {
            var obj = Obstacle(); var session = Session(obj); var skill = Skill();
            session.MapObjects.Add(new ObstacleCombatMapObject("wall",new GridPosition(1,0),true,true));
            Assert.That(CombatObjectDamageSystem.Targets(session,session.ActiveUnit,skill,0,obj.Position), Is.Empty);
            session.Grid.SetBlockedEdge(new GridPosition(1,0),obj.Position,true);
            Assert.That(CombatObjectDamageSystem.Impact(session,new GridPosition(1,0),obj.Position), Is.False);
            Assert.That(obj.Source.RemainingHits, Is.EqualTo(3));
        }
    }
}

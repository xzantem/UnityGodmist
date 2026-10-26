using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.World;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Dungeons;
using Godmist.Views.Dungeons;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public class DungeonExitTests
    {
        [Test,Explicit("One-off cleanup of orphaned DontSave NavMeshData left by aborted EditMode previews.")]
        public void ReleaseOrphanedEditorNavigationData()
        {
            Assert.That(Application.isPlaying,Is.False);
            var owned=new System.Collections.Generic.HashSet<UnityEngine.AI.NavMeshData>();
            const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            foreach(var controller in Resources.FindObjectsOfTypeAll<Godmist.Views.Exploration.ExplorationPartyController>())
            {
                var meshes=(System.Collections.IDictionary)controller.GetType().GetField("navigationMeshes",flags).GetValue(controller);
                foreach(var mesh in meshes.Values)
                {
                    var data=(UnityEngine.AI.NavMeshData)mesh.GetType().GetField("data",flags).GetValue(mesh);
                    if(data!=null)owned.Add(data);
                }
            }
            var orphaned=Resources.FindObjectsOfTypeAll<UnityEngine.AI.NavMeshData>()
                .Where(d=>(d.hideFlags&HideFlags.DontSave)==HideFlags.DontSave && !owned.Contains(d) && !EditorUtility.IsPersistent(d)).ToArray();
            var output=System.IO.Path.GetFullPath("../LocalHandoffs/dungeon-exit-20261023/navigation-cleanup.txt");
            System.IO.File.WriteAllText(output,"live owned data="+owned.Count+"; orphaned transient data="+orphaned.Length);
            foreach(var data in orphaned)Object.DestroyImmediate(data);
            Assert.That(owned.All(d=>d!=null),Is.True,"Cleanup cannot remove a live controller's data.");
        }

        [Test]
        public void AssetAuditExitKeepsItsExistingBypass()
        {
            var dungeon=new DungeonInstance("audit",1);
            var floor=new DungeonFloorInstance(0);dungeon.TryAddFloor(floor);
            Assert.That(DungeonExitRules.IsExitRoomCleared(dungeon,floor),Is.False);
            dungeon.MarkAssetAuditRoom();
            Assert.That(DungeonExitRules.IsExitRoomCleared(dungeon,floor),Is.True);
        }

        [TestCase("forest",DungeonLength.Short)] [TestCase("forest",DungeonLength.Normal)] [TestCase("forest",DungeonLength.Long)]
        [TestCase("catacombs",DungeonLength.Short)] [TestCase("catacombs",DungeonLength.Normal)] [TestCase("catacombs",DungeonLength.Long)]
        public void EveryExitHasOneEncounterWithinExistingBudgetAndClearingIsRequired(string theme,DungeonLength length)
        {
            var catalog=AssetDatabase.LoadAssetAtPath<GameContentCatalog>("Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
            var definition=catalog.DungeonDefinitions.Single(d=>d!=null&&d.Id=="dungeon."+theme+".expedition");
            for(var seed=231023;seed<231031;seed++)
            {
                var dungeon=new DungeonGeneratorService().Generate(definition,seed,10,length);
                new DungeonEncounterPlacementSystem().Place(dungeon);
                foreach(var floor in dungeon.Floors)
                {
                    var final=floor.Rooms.Single(r=>r.RoomType==DungeonRoomType.Final);
                    var exit=floor.Encounters.Where(e=>e.RoomId==final.RoomId).ToArray();
                    Assert.That(exit.Length,Is.EqualTo(1));
                    var budgetRooms=floor.Rooms.Count(r=>!r.IsBranchTransitRoom);
                    var min=Mathf.Max(dungeon.LengthProfile.MinEncountersPerFloor,Mathf.CeilToInt(budgetRooms*.3f));
                    var max=Mathf.Min(dungeon.LengthProfile.MaxEncountersPerFloor,Mathf.FloorToInt(budgetRooms*.4f));
                    Assert.That(floor.Encounters.Count,Is.InRange(min,max));
                    Assert.That(DungeonExitRules.IsExitRoomCleared(dungeon,floor),Is.False);
                    dungeon.TryMarkEncounterCleared(exit[0].InstanceId);
                    Assert.That(DungeonExitRules.IsExitRoomCleared(dungeon,floor),Is.True);
                    var count=floor.Encounters.Count;new DungeonEncounterPlacementSystem().Place(dungeon);
                    Assert.That(floor.Encounters.Count,Is.EqualTo(count),"Repeated placement must not duplicate final guards.");
                }
            }
        }

        [Test]
        public void PrefabUsesExistingConfirmationAndSuppressesLegacyLabelsOnEveryInitialization()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<DungeonExitView>("Assets/_Project/Prefabs/Dungeon/PF_DungeonExitView.prefab");
            Assert.That(new SerializedObject(prefab).FindProperty("confirmationPrefab").objectReferenceValue,Is.TypeOf<ItemActionPopupView>());
            Assert.That(prefab.GetComponentsInChildren<TMPro.TMP_Text>(true),Is.Empty);
            var exit=Object.Instantiate(prefab);
            try
            {
                var label=new GameObject("Legacy exit label",typeof(TMPro.TextMeshPro));label.transform.SetParent(exit.transform);
                label.GetComponent<TMPro.TMP_Text>().text="WYJŚCIE [E]";
                exit.Initialize(null,Vector2.zero);Assert.That(label.activeSelf,Is.False);
                label.SetActive(true);exit.InitializeTransition(null,Vector2.zero,()=>{});Assert.That(label.activeSelf,Is.False);
                var rootLabel=exit.gameObject.AddComponent<TMPro.TextMeshPro>();rootLabel.text="WYJŚCIE [E]";
                exit.Initialize(null,Vector2.zero);Assert.That(rootLabel.enabled,Is.False);Assert.That(exit.gameObject.activeSelf,Is.True);
            }
            finally{Object.DestroyImmediate(exit.gameObject);}
        }
    }
}

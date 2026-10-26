using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Data.Characters;
using Godmist.Runtime.Combat;
using Godmist.Systems.Characters;
using Godmist.Views.Combat;
using Godmist.Views.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public class ScoutKnifeHudTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        GameObject canvasObject, runtime, markerRoot;
        CombatHudView hud;
        CombatActiveUnitView active;
        CombatSkillBarView bar;
        CombatUnitViewSpawner spawner;
        BattleUnit scout;
        CombatSession session;
        static T Get<T>(object owner, string name) => (T)owner.GetType().GetField(name, Flags).GetValue(owner);
        static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Flags | BindingFlags.Public).SetValue(owner, value);
        static void Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, Flags).Invoke(owner, args);
        CombatScoutKnifeCounterView Counter => active.GetComponentInChildren<CombatScoutKnifeCounterView>(true);
        TMP_Text Count => Counter.transform.Find("Count").GetComponent<TMP_Text>();
        BattleUnit Unit(string asset) => new BattleUnit { IsPlayerSide = true, MaxKnives = 7, AvailableKnives = 3,
            MaxActionPoints = 1000, CurrentActionPoints = 1000,
            Character = new CharacterInstanceFactory().Create(AssetDatabase.LoadAssetAtPath<CharacterDefinition>(
                "Assets/_Project/ScriptableObjects/Characters/" + asset)) };

        [SetUp] public void Setup()
        {
            canvasObject = new GameObject("Knife HUD canvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.GetComponent<RectTransform>().sizeDelta = new Vector2(1280, 900);
            hud = Object.Instantiate(AssetDatabase.LoadAssetAtPath<CombatHudView>(
                "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CombatHud.prefab"), canvasObject.transform);
            active = hud.GetComponentInChildren<CombatActiveUnitView>(true);
            bar = hud.GetComponentInChildren<CombatSkillBarView>(true);
            runtime = new GameObject("Knife HUD runtime"); runtime.SetActive(false);
            var bootstrap = runtime.AddComponent<CombatSandboxBootstrap>();
            markerRoot = new GameObject("Knife HUD markers"); spawner = markerRoot.AddComponent<CombatUnitViewSpawner>();
            Set(spawner, "unitsRoot", markerRoot.transform); Set(spawner, "hud", hud);
            Set(bootstrap, "unitViewSpawner", spawner); Set(hud, "sandboxBootstrap", bootstrap); Set(bar, "sandboxBootstrap", bootstrap);
            var driver = (CombatExecutionDriver)typeof(CombatSandboxBootstrap).GetMethod("CreateDriver", Flags).Invoke(bootstrap, null);
            scout = Unit("CD_Jerzy.asset"); session = new CombatSession(); session.Units.Add(scout); session.ActiveUnit = scout;
            typeof(CombatExecutionDriver).GetProperty("Session").SetValue(driver, session); Set(bootstrap, "focused", driver);
            hud.gameObject.SetActive(true);
            active.Bind(scout, true);
        }
        [TearDown] public void Cleanup()
        {
            foreach (var visual in markerRoot.GetComponentsInChildren<CombatUnitVisualStateView>(true)) Call(visual, "OnDestroy");
            Object.DestroyImmediate(markerRoot); Object.DestroyImmediate(canvasObject); Object.DestroyImmediate(runtime);
        }
        KnifeCombatMapObject AddKnife(string owner = null, int x = 1)
        {
            var knife = new KnifeCombatMapObject("knife." + session.MapObjects.Count, new GridPosition(x, 1), owner ?? scout.Character.CharacterId, 1);
            session.MapObjects.Add(knife); return knife;
        }
        void Sync() => Call(spawner, "SynchronizeKnifeViews", session, true);
        SpriteRenderer Renderer(KnifeCombatMapObject knife) => Get<Dictionary<KnifeCombatMapObject, SpriteRenderer>>(spawner, "knifeViews")[knife];
        bool Outlined(KnifeCombatMapObject knife) => Renderer(knife).GetComponentsInChildren<SpriteRenderer>().Any(r => r != Renderer(knife) && r.enabled);

        [Test] public void CurrentInventoryAndCapacityUpdateWithoutDuplicateCounterOrLayoutMutation()
        {
            var width = active.GetComponent<LayoutElement>().preferredWidth;
            Assert.That(Count.text, Is.EqualTo("3/7"));
            scout.AvailableKnives = 0; scout.MaxKnives = 9; active.Bind(scout, true);
            Assert.That(Count.text, Is.EqualTo("0/9")); Assert.That(Count.color.r, Is.GreaterThan(Count.color.g));
            scout.AvailableKnives = 6; active.Bind(scout, true);
            Assert.That(Count.text, Is.EqualTo("6/9"));
            Assert.That(active.GetComponentsInChildren<CombatScoutKnifeCounterView>(true).Length, Is.EqualTo(1));
            Assert.That(active.GetComponent<LayoutElement>().preferredWidth, Is.EqualTo(width));
            Assert.That(Counter.GetComponent<LayoutElement>().ignoreLayout, Is.True);
            Assert.That(Count.font, Is.SameAs(Get<TMP_Text>(active, "unitNameText").font));
            Assert.That(Count.fontSharedMaterial, Is.Not.Null);
        }
        [TestCase("CD_Mage.asset", true, true)]
        [TestCase("CD_Jerzy.asset", false, true)]
        [TestCase("CD_Jerzy.asset", true, false)]
        public void CounterAndHoverConcealOnOtherClassEnemyAndUnknownUnit(string asset, bool player, bool known)
        {
            Counter.OnPointerEnter(null); Assert.That(hud.KnifeHighlightOwner, Is.SameAs(scout));
            var next = Unit(asset); next.IsPlayerSide = player; active.Bind(next, true, known: known);
            Assert.That(Counter.gameObject.activeSelf, Is.False); Assert.That(hud.KnifeHighlightOwner, Is.Null);
            active.Bind(scout, true); Assert.That(Counter.gameObject.activeSelf, Is.True); Assert.That(hud.KnifeHighlightOwner, Is.Null);
        }
        [Test] public void RebindExitDisableAndHiddenHudClearHover()
        {
            Counter.OnPointerEnter(null); Counter.OnPointerExit(null); Assert.That(hud.KnifeHighlightOwner, Is.Null);
            Counter.OnPointerEnter(null); active.Bind(Unit("CD_Jerzy.asset"), true); Assert.That(hud.KnifeHighlightOwner, Is.Null);
            Counter.OnPointerEnter(null); Call(Counter, "OnDisable"); Assert.That(hud.KnifeHighlightOwner, Is.Null);
            Counter.OnPointerEnter(null); hud.enabled = false; Assert.That(hud.KnifeHighlightOwner, Is.Null);
        }
        [Test] public void CounterHoverHighlightsOnlyOwnersGroundKnivesAndDoesNotChangeGameplay()
        {
            var first = AddKnife(); var second = AddKnife(x: 2); var foreign = AddKnife("another.scout", 3);
            Sync(); Assert.That(Outlined(first), Is.False);
            Counter.OnPointerEnter(null); Sync();
            Assert.That(Outlined(first), Is.True); Assert.That(Outlined(second), Is.True); Assert.That(Outlined(foreign), Is.False);
            Assert.That(scout.AvailableKnives, Is.EqualTo(3)); Assert.That(scout.CurrentActionPoints, Is.EqualTo(1000));
            Assert.That(session.MapObjects.Count, Is.EqualTo(3));
            Counter.OnPointerExit(null); Sync(); Assert.That(Outlined(first), Is.False); Assert.That(Outlined(second), Is.False);
        }
        [TestCase(false)] [TestCase(true)] public void HiddenOrDisabledKnifeNeverGetsOutline(bool disabled)
        {
            var knife = AddKnife(); Sync(); Counter.OnPointerEnter(null); Sync();
            Assert.That(Outlined(knife), Is.True);
            if (disabled) Renderer(knife).enabled = false; else Renderer(knife).forceRenderingOff = true;
            Call(spawner, "RefreshKnifeHighlight", knife); Assert.That(Outlined(knife), Is.False);
            spawner.SetKnifeHover(knife, true); Assert.That(Outlined(knife), Is.False);
        }
        [Test] public void PointerAndCounterHighlightComposeAndRemainReusable()
        {
            var knife = AddKnife(); Sync(); Counter.OnPointerEnter(null); Sync();
            var rendererCount = markerRoot.GetComponentsInChildren<SpriteRenderer>(true).Length;
            spawner.SetKnifeHover(knife, true); spawner.SetKnifeHover(knife, false);
            Assert.That(Outlined(knife), Is.True, "World hover cleanup must not cancel the counter's independent hover.");
            spawner.SetKnifeHover(knife, true); Counter.OnPointerExit(null); Sync(); Assert.That(Outlined(knife), Is.True);
            spawner.SetKnifeHover(knife, false); Assert.That(Outlined(knife), Is.False);
            for (var i = 0; i < 5; i++) { Counter.OnPointerEnter(null); Sync(); Counter.OnPointerExit(null); Sync(); }
            Assert.That(markerRoot.GetComponentsInChildren<SpriteRenderer>(true).Length, Is.EqualTo(rendererCount));
        }
        [Test] public void NewActiveUnitOrOtherCombatCannotInheritCounterHighlight()
        {
            var knife = AddKnife(); Sync(); Counter.OnPointerEnter(null); Sync(); Assert.That(Outlined(knife), Is.True);
            session.ActiveUnit = Unit("CD_Jerzy.asset"); active.Bind(session.ActiveUnit, true); Sync(); Assert.That(Outlined(knife), Is.False);
        }
        [Test] public void ExplorationWrapperHighlightsSameCharactersGroundKnives()
        {
            var knife = AddKnife(); Sync();
            session.ActiveUnit = null;
            Counter.OnPointerEnter(null); Sync();
            Assert.That(Outlined(knife), Is.True);
            Assert.That(Renderer(knife).GetComponent<UnityEngine.Rendering.SortingGroup>(), Is.Not.Null);
        }
        [Test] public void RemovedKnifeReleasesViewAndHoverState()
        {
            var knife = AddKnife(); Sync(); Counter.OnPointerEnter(null); Sync(); spawner.SetKnifeHover(knife, true);
            // Explicit lifecycle cleanup is required for resource owners in synchronous EditMode.
            foreach (var visual in Renderer(knife).GetComponentsInChildren<CombatUnitVisualStateView>()) Call(visual, "OnDestroy");
            session.MapObjects.Remove(knife); Sync();
            Assert.That(Get<Dictionary<KnifeCombatMapObject, SpriteRenderer>>(spawner, "knifeViews"), Is.Empty);
            Assert.That(Get<HashSet<KnifeCombatMapObject>>(spawner, "pointerHoveredKnives"), Is.Empty);
            Assert.That(Get<HashSet<KnifeCombatMapObject>>(spawner, "counterHighlightedKnives"), Is.Empty);
        }
        [Test] public void SkillSlotsStopRepeatingInventoryButStillEnforceKnifeCostsAndDescribeThem()
        {
            Call(bar, "Update"); var slots = Get<List<CombatSkillSlotView>>(bar, "spawnedSlots");
            Assert.That(slots.Count, Is.GreaterThan(0)); var knifeSlots = slots.Where(s => Get<Godmist.Data.Skills.SkillData>(s, "skill").KnifeCost > 0).ToList();
            Assert.That(knifeSlots, Is.Not.Empty);
            scout.Character.ResourcePool.CurrentValue = scout.Character.ResourcePool.MaxValue;
            scout.AvailableKnives = 0; Call(bar, "Update");
            foreach (var slot in slots) Assert.That(Get<TMP_Text>(slot, "hotkeyText").enabled, Is.False);
            foreach (var slot in knifeSlots)
            {
                var skill = Get<Godmist.Data.Skills.SkillData>(slot, "skill");
                Assert.That(Get<Button>(slot, "button").interactable, Is.EqualTo(skill.FreeKnivesInMelee));
                if (!skill.FreeKnivesInMelee) Assert.That(Get<string>(slot, "unavailableReason"), Does.Contain("noży"));
            }
        }
        [Test] public void RenderCounterInProductionHudAndCheckItFitsBelowPortrait()
        {
            Call(bar, "Update"); Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)Counter.transform;
            Assert.That(rect.anchoredPosition.x + rect.rect.width, Is.LessThanOrEqualTo(98f));
            Assert.That(-rect.anchoredPosition.y + rect.rect.height, Is.LessThanOrEqualTo(168f));
            RenderHud("dungeon-combat-polish-20261025", "scout-knife-hud.png");
        }
        [Test] public void RenderExplorationSkillsWithoutCombatControlsOrTurnOrder()
        {
            foreach(var drawer in hud.GetComponentsInChildren<CombatEdgeDrawer>(true)) Call(drawer,"Awake");
            foreach(var tooltip in hud.GetComponentsInChildren<Godmist.Views.UI.Workspace.UIWorkspaceTooltipView>(true)) tooltip.Hide();
            foreach(var preview in hud.GetComponentsInChildren<CombatTargetPreviewView>(true)) preview.Hide();
            var bootstrap=Get<CombatSandboxBootstrap>(hud,"sandboxBootstrap");
            Set(bootstrap,"focused",null);
            bar.BindExploration(()=>scout,_=>{});
            var actions=hud.GetComponentInChildren<CombatActionButtonsView>(true);
            Set(actions,"sandboxBootstrap",bootstrap);
            Call(bar,"Update");Call(hud,"LateUpdate");Call(actions,"Update");
            Assert.That(bootstrap.Session,Is.Null);Assert.That(active.gameObject.activeSelf,Is.True);
            Assert.That(Count.text, Is.EqualTo("3/7"));
            Assert.That(bar.GetComponentsInChildren<CombatSkillSlotView>().Length,Is.GreaterThan(0));
            foreach(var name in new[]{"endTurnButton","escapeButton","consumablesButton"})
                Assert.That(Get<Button>(actions,name).gameObject.activeSelf,Is.False);
            Assert.That(hud.GetComponentsInChildren<CombatTurnPortraitView>(),Is.Empty);
            Assert.That(hud.transform.Find("TopBar").gameObject.activeSelf,Is.False);
            Assert.That(hud.transform.Find("TimelineDrawerHandle").gameObject.activeSelf,Is.False);
            RenderHud("dungeon-combat-polish-20261025", "exploration-hud.png");
        }
        [TestCase(false)] [TestCase(true)]
        public void TimelineRestoresPortraitsAfterExplorationAndOpeningPreview(bool collapsed)
        {
            foreach(var component in hud.GetComponentsInChildren<CombatEdgeDrawer>(true)) Call(component,"Awake");
            var container=Get<Transform>(hud,"turnOrderContainer");
            var drawer=container.GetComponentInParent<CombatEdgeDrawer>(true);
            Assert.That(drawer,Is.Not.Null);
            drawer.SetExpanded(!collapsed,immediate:true);
            var bootstrap=Get<CombatSandboxBootstrap>(hud,"sandboxBootstrap");
            var driver=Get<CombatExecutionDriver>(bootstrap,"focused");
            var enemy=Unit("CD_Mage.asset");enemy.IsPlayerSide=false;session.Units.Add(enemy);
            new Godmist.Systems.Combat.CombatTurnSystem().InitializeTurnOrder(session,scout);
            bar.BindExploration(()=>scout,_=>{});
            Set(bootstrap,"seamlessDungeon",true);
            CombatTurnPortraitView[] first=null;
            for(var cycle=0;cycle<3;cycle++)
            {
                Set(bootstrap,"focused",null);Call(hud,"LateUpdate");
                Assert.That(container.gameObject.activeSelf,Is.False);
                Assert.That(active.gameObject.activeInHierarchy,Is.True,"Exploration keeps the controlled hero portrait.");
                var preview=new CombatSession {ActiveUnit=scout};preview.Units.Add(scout);
                Assert.That(bootstrap.BeginOpeningPreview(preview,(s,p,t,o,r,c)=>false,_=>true),Is.True);
                Call(hud,"LateUpdate");Assert.That(container.gameObject.activeSelf,Is.False);
                bootstrap.CancelOpeningPreview();Set(bootstrap,"focused",driver);
                Call(hud,"LateUpdate");Canvas.ForceUpdateCanvases();
                Assert.That(container.gameObject.activeInHierarchy,Is.True,"A previous exploration frame must not leave timeline content disabled.");
                var portraits=container.GetComponentsInChildren<CombatTurnPortraitView>();
                Assert.That(portraits.Length,Is.EqualTo(8));
                Assert.That(portraits.Any(p=>p.BoundInputUnit==scout),Is.True);
                Assert.That(portraits.Any(p=>p.BoundInputUnit==enemy),Is.True);
                Assert.That(drawer.IsExpanded,Is.EqualTo(!collapsed),"Restoring content must preserve the user's drawer state.");
                if(first==null)first=portraits;else CollectionAssert.AreEqual(first,portraits,"Reuse existing entries across focus changes.");
            }
            drawer.SetExpanded(true,immediate:true);
            Assert.That(container.GetComponentInParent<CanvasGroup>().alpha,Is.EqualTo(1));
            Assert.That(container.GetComponentsInChildren<CombatTurnPortraitView>().Length,Is.EqualTo(8));
            Call(bar,"Update");
            foreach(var tooltip in hud.GetComponentsInChildren<Godmist.Views.UI.Workspace.UIWorkspaceTooltipView>(true)) tooltip.Hide();
            foreach(var preview in hud.GetComponentsInChildren<CombatTargetPreviewView>(true)) preview.Hide();
            RenderHud("turn-order-visibility-20261025",collapsed?"timeline-restored-from-collapsed.png":"timeline-restored.png");
        }

        private void RenderHud(string directory,string filename)
        {
            var cameraObject = new GameObject("Knife HUD render camera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>(); camera.enabled = false; camera.orthographic = true;
            camera.transform.position = new Vector3(0, 0, -10); camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.035f, .035f, .04f);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            var target = new RenderTexture(1280, 900, 24); var pixels = new Texture2D(1280, 900, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                foreach (var child in canvasObject.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
                target.Create(); camera.targetTexture = target; Canvas.ForceUpdateCanvases(); camera.Render();
                RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 1280, 900), 0, 0); pixels.Apply();
                var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../LocalHandoffs",directory));
                Directory.CreateDirectory(output); File.WriteAllBytes(Path.Combine(output, filename), pixels.EncodeToPNG());
                Assert.That(pixels.GetPixels32().Count(c => c.r > 100 && c.g > 80), Is.GreaterThan(1000));
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous; target.Release();
                Object.DestroyImmediate(target); Object.DestroyImmediate(pixels); Object.DestroyImmediate(cameraObject);
            }
        }
    }
}

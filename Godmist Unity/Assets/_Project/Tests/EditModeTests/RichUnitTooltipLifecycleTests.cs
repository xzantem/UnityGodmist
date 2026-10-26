using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Views.Combat;
using Godmist.Views.Exploration;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Godmist.Tests.EditMode
{
    public sealed class RichUnitTooltipLifecycleTests
    {
        private const string TooltipPrefabPath =
            "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_Tooltip.prefab";
        private const string PartyPortraitPrefabPath =
            "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_PartyMember.prefab";
        private const string TurnPortraitPrefabPath =
            "Assets/_Project/Prefabs/UI/CombatHUD/PF_CombatTurnPortrait.prefab";
        private const BindingFlags InstancePrivate =
            BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void ExplicitPinSurvivesSourceExitAndRepeatedInsideRightClick()
        {
            var canvas = new GameObject("Rich tooltip lifecycle canvas", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            try
            {
                var tooltip = CreateTooltip(canvas.transform);
                var source = new object();
                ShowPendingText(tooltip, source);

                tooltip.PinAtCurrentPosition();
                Assert.That(tooltip.IsPinned, Is.True);

                tooltip.NotifySourceExit(source);
                Assert.That(tooltip.IsPinned, Is.True);
                Assert.That(Get<bool>(tooltip, "visible"), Is.True);

                // The source can finish its hover lifecycle while a manual pin is
                // active. A second right-click inside the card is idempotent.
                tooltip.PinAtCurrentPosition();
                Assert.That(tooltip.IsPinned, Is.True);
                Assert.That(Get<bool>(tooltip, "sourceHovered"), Is.False,
                    "Pinning after source exit must not create a synthetic hover.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void SourceExitBeforeAutoLockOpensEnterableGracePeriod()
        {
            var canvas = new GameObject("Rich tooltip pre-lock grace canvas", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            try
            {
                var tooltip = CreateTooltip(canvas.transform);
                var source = new object();
                ShowPendingText(tooltip, source);
                Assert.That(Get<bool>(tooltip, "isLocked"), Is.False);

                tooltip.NotifySourceExit(source);
                Assert.That(Get<bool>(tooltip, "isLocked"), Is.True,
                    "A visible tooltip must become enterable as soon as its source exits.");
                Assert.That(Get<CanvasGroup>(tooltip, "canvasGroup").blocksRaycasts, Is.True);

                Invoke(tooltip, "Update");
                Assert.That(Get<bool>(tooltip, "visible"), Is.True,
                    "The short exit grace must allow crossing the gap into the card.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void RichHealthTracksShieldGainAbsorptionAndCharacterRebinding()
        {
            var canvas = new GameObject("Rich tooltip shield canvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                var tooltip = CreateTooltip(canvas.transform);
                var unit = CreateUnit("shield-target");
                unit.Character.ShieldPool = new Godmist.Runtime.Characters.ShieldPool();
                unit.Character.ShieldPool.AddLayer(40);
                tooltip.ShowUnit(unit, new object());
                ShowPending(tooltip);
                var rich = Get<UIWorkspaceRichUnitTooltipView>(tooltip, "richUnitContent");
                var healthText = Get<TMPro.TextMeshProUGUI>(rich, "healthText");
                var health = Get<Slider>(rich, "healthSlider");
                var overlay = Get<Image>(rich, "shieldOverlay");
                Assert.That(healthText.text, Is.EqualTo($"HP  {CombatHealthPresentation.Format(unit.Character)}"));
                Assert.That(healthText.text, Does.Contain("(+40)"));
                Assert.That(overlay.gameObject.activeInHierarchy, Is.True);
                Assert.That(overlay.rectTransform.parent, Is.SameAs(health.fillRect.parent));
                unit.Character.ShieldPool.AbsorbDamage(1000);
                Invoke(rich, "Refresh");
                Assert.That(healthText.text, Does.Not.Contain("(+"));
                Assert.That(overlay.gameObject.activeSelf, Is.False);
                unit.Character.ShieldPool.AddLayer(20);
                Invoke(rich, "Refresh");
                Assert.That(Get<Image>(rich, "shieldOverlay"), Is.SameAs(overlay));
                Assert.That(healthText.text, Does.Contain("(+20)"));
                rich.Bind(CreateUnit("no-shield"), tooltip);
                Assert.That(overlay.gameObject.activeSelf, Is.False);
                Assert.That(healthText.text, Does.Not.Contain("(+"));
            }
            finally { UnityEngine.Object.DestroyImmediate(canvas); }
        }

        [Test]
        public void PinnedTooltipPreservesInsideButtonsAndClosesWholeChainOutside()
        {
            var canvas = new GameObject("Rich tooltip pointer canvas", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            try
            {
                var tooltip = CreateTooltip(canvas.transform);
                ShowPendingText(tooltip, new object());
                tooltip.PinAtCurrentPosition();

                var rect = Get<RectTransform>(tooltip, "tooltipRect");
                var inside = RectTransformUtility.WorldToScreenPoint(
                    null,
                    rect.TransformPoint(rect.rect.center));
                Assert.That(UIWorkspaceTooltipView.HandlePointerClick(inside, false), Is.True);
                Assert.That(tooltip.IsPinned, Is.True,
                    "A left-click inside a pinned card must leave the card open.");
                Assert.That(UIWorkspaceTooltipView.HandlePointerClick(inside, true), Is.True);
                Assert.That(tooltip.IsPinned, Is.True,
                    "A repeated right-click inside a pinned card must not unpin it.");

                var create = typeof(UIWorkspaceTooltipView).GetMethod(
                    "CreateNestedTooltip",
                    InstancePrivate,
                    binder: null,
                    types: new[] { typeof(object), typeof(Action), typeof(bool), typeof(string) },
                    modifiers: null);
                create.Invoke(tooltip, new object[] { new object(), (Action)(() => { }), true, null });
                var nested = (List<UIWorkspaceTooltipView>)Get(tooltip, "nested");
                Assert.That(nested, Has.Count.EqualTo(1));
                nested[0].PinAtCurrentPosition();

                // An outside press closes the complete root/child chain. Returning
                // false allows the world or UI action beneath it to continue.
                // The production chain uses deferred Destroy; EditMode reports
                // that call, and fixture teardown destroys the clone immediately.
                LogAssert.Expect(LogType.Error, "Destroy may not be called from edit mode! Use DestroyImmediate instead.\nDestroying an object in edit mode destroys it permanently.");
                Assert.That(UIWorkspaceTooltipView.HandlePointerClick(new Vector2(-10000f, -10000f), false), Is.False);
                Assert.That(Get<bool>(tooltip, "visible"), Is.False);
                Assert.That(nested, Is.Empty);

                // The same outside-chain rule applies to a context click.
                ShowPendingText(tooltip, new object());
                tooltip.PinAtCurrentPosition();
                Assert.That(UIWorkspaceTooltipView.HandlePointerClick(new Vector2(-10000f, -10000f), true), Is.False);
                Assert.That(Get<bool>(tooltip, "visible"), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void TTogglesActiveTooltipBeforeLeavingSourceAndOldRightClickDoesNotPin()
        {
            var canvas = new GameObject("Rich tooltip keyboard pin canvas", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            try
            {
                var tooltip = CreateTooltip(canvas.transform);
                ShowPendingText(tooltip, new object());
                var border = tooltip.transform.Find("Border")?.GetComponent<Graphic>();
                Assert.That(border, Is.Not.Null);
                var defaultBorderColor = border.color;
                var rect = Get<RectTransform>(tooltip, "tooltipRect");
                var inside = RectTransformUtility.WorldToScreenPoint(
                    null,
                    rect.TransformPoint(rect.rect.center));

                // The old manual right-click gesture is now an ordinary content
                // press and must not pin a tooltip that is merely under the pointer.
                Assert.That(UIWorkspaceTooltipView.HandlePointerClick(inside, true), Is.True);
                Assert.That(tooltip.IsPinned, Is.False);

                // T is evaluated against the tooltip under the pointer first. The
                // fallback below models pressing it while the pointer is still on
                // the source, before the card is entered.
                Assert.That(UIWorkspaceTooltipView.TryPinAtCurrentPointer(inside), Is.True);
                Assert.That(tooltip.IsPinned, Is.True);
                Assert.That(UIWorkspaceTooltipView.TryPinAtCurrentPointer(inside), Is.True);
                Assert.That(tooltip.IsPinned, Is.False);

                Assert.That(UIWorkspaceTooltipView.TryPinAtCurrentPointer(
                    new Vector2(-10000f, -10000f)), Is.True);
                Assert.That(tooltip.IsPinned, Is.True);
                Assert.That(border.color, Is.EqualTo(new Color(126f / 255f, 219f / 255f, 1f, 1f)));
                Assert.That(UIWorkspaceTooltipView.TryPinAtCurrentPointer(
                    new Vector2(-10000f, -10000f)), Is.True,
                    "Repeating T must toggle the existing manual pin off.");
                Assert.That(tooltip.IsPinned, Is.False);
                Assert.That(Get<bool>(tooltip, "isLocked"), Is.True,
                    "Unpinning must restore the normal enterable grace lock.");
                Assert.That(Get<CanvasGroup>(tooltip, "canvasGroup").blocksRaycasts, Is.True,
                    "The unpinned card must remain enterable during its grace window.");
                Assert.That(border.color, Is.EqualTo(defaultBorderColor));
                Assert.That(UIWorkspaceTooltipView.TryPinAtCurrentPointer(
                    new Vector2(-10000f, -10000f)), Is.True);
                Assert.That(tooltip.IsPinned, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void TUnpinsNearestManualOwnerWhenNestedTooltipInheritsPinnedChain()
        {
            var canvas = new GameObject("Rich tooltip inherited pin canvas", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            try
            {
                var tooltip = CreateTooltip(canvas.transform);
                ShowPendingText(tooltip, new object());
                tooltip.PinAtCurrentPosition();

                var create = typeof(UIWorkspaceTooltipView).GetMethod(
                    "CreateNestedTooltip",
                    InstancePrivate,
                    binder: null,
                    types: new[] { typeof(object), typeof(Action), typeof(bool), typeof(string) },
                    modifiers: null);
                create.Invoke(tooltip, new object[] { new object(), (Action)(() => { }), true, null });
                var nested = (List<UIWorkspaceTooltipView>)Get(tooltip, "nested");
                Assert.That(nested, Has.Count.EqualTo(1));
                var child = nested[0];
                var childBorder = child.transform.Find("Border")?.GetComponent<Graphic>();
                Assert.That(childBorder, Is.Not.Null);
                Assert.That(childBorder.color, Is.EqualTo(new Color(126f / 255f, 219f / 255f, 1f, 1f)));

                child.TogglePinned();

                Assert.That(tooltip.IsPinned, Is.False,
                    "T over an inherited child must toggle the ancestor that owns the pin.");
                Assert.That(child.IsPinned, Is.False,
                    "T must not create a child pin while unpinning the ancestor.");
                Assert.That(Get<bool>(tooltip, "isLocked"), Is.True);
                Assert.That(Get<bool>(child, "isLocked"), Is.True);
                Assert.That(Get<CanvasGroup>(tooltip, "canvasGroup").blocksRaycasts, Is.True);
                Assert.That(Get<CanvasGroup>(child, "canvasGroup").blocksRaycasts, Is.True);
                Assert.That(childBorder.color, Is.Not.EqualTo(new Color(126f / 255f, 219f / 255f, 1f, 1f)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void TooltipFooterIsSharedAcrossRichAndNestedViewsWithoutDuplicates()
        {
            var canvas = new GameObject("Rich tooltip footer canvas", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            try
            {
                var tooltip = CreateTooltip(canvas.transform);
                AssertTooltipFooter(tooltip);
                Invoke(tooltip, "EnsureTooltipFooter");
                AssertTooltipFooter(tooltip);

                var source = new GameObject("Rich tooltip footer source");
                source.transform.SetParent(canvas.transform, false);
                tooltip.ShowUnit(CreateUnit("footer-rich"), source);
                ShowPending(tooltip);
                AssertTooltipFooter(tooltip);

                var create = typeof(UIWorkspaceTooltipView).GetMethod(
                    "CreateNestedTooltip",
                    InstancePrivate,
                    binder: null,
                    types: new[] { typeof(object), typeof(Action), typeof(bool), typeof(string) },
                    modifiers: null);
                create.Invoke(tooltip, new object[] { new object(), (Action)(() => { }), false, null });
                var nested = (List<UIWorkspaceTooltipView>)Get(tooltip, "nested");
                Assert.That(nested, Has.Count.EqualTo(1));
                Invoke(nested[0], "EnsureTooltipFooter");
                AssertTooltipFooter(nested[0]);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void RichStatusHoverStaysOnCardAndQueuesNestedStatusTooltip()
        {
            var canvas = new GameObject("Rich tooltip status lifecycle canvas", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            try
            {
                var tooltip = CreateTooltip(canvas.transform);
                var source = new GameObject("World mob hover source");
                source.transform.SetParent(canvas.transform, false);
                var unit = CreateUnit("status-target");
                unit.Character.PassiveEffects.Add(new PassiveEffectRuntime
                {
                    EffectId = StatusEffectIds.Bleed,
                    DisplayName = "Krwawienie",
                    Stacks = 2,
                    Lifetime = PassiveEffectLifetime.Timed,
                    RemainingDuration = 2
                });

                tooltip.ShowUnit(unit, source);
                ShowPending(tooltip);

                var rich = Get<UIWorkspaceRichUnitTooltipView>(tooltip, "richUnitContent");
                var statusRoot = Get<RectTransform>(rich, "statusRoot");
                Assert.That(statusRoot.gameObject.activeInHierarchy, Is.True);
                var status = statusRoot.Find("Status") as RectTransform;
                Assert.That(status, Is.Not.Null);

                var statusPoint = RectTransformUtility.WorldToScreenPoint(
                    null,
                    statusRoot.TransformPoint(statusRoot.rect.center));
                Assert.That((bool)Invoke(tooltip, "IsPointerOverTooltip", statusPoint), Is.True,
                    "The complete runtime rich card, including the Statusy row, belongs to the tooltip chain.");

                var statusHandler = status.GetComponents<MonoBehaviour>()
                    .OfType<IPointerEnterHandler>()
                    .Single();
                statusHandler.OnPointerEnter(new PointerEventData(null) { position = statusPoint });

                Assert.That(Get<bool>(tooltip, "visible"), Is.True);
                Assert.That(Get<BattleUnit>(rich, "boundUnit"), Is.SameAs(unit),
                    "Entering a status must not replace or invalidate the rich unit source.");
                Assert.That(Get<Action>(tooltip, "pendingNestedShow"), Is.Not.Null,
                    "Status hover must use the nested tooltip lifecycle instead of replacing the root card.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void PinnedNestedChildKeepsParentAliveAfterParentSourceExit()
        {
            var canvas = new GameObject("Rich tooltip nested pin canvas", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            try
            {
                var tooltip = CreateTooltip(canvas.transform);
                ShowPendingText(tooltip, new object());
                var create = typeof(UIWorkspaceTooltipView).GetMethod(
                    "CreateNestedTooltip",
                    InstancePrivate,
                    binder: null,
                    types: new[] { typeof(object), typeof(Action), typeof(bool), typeof(string) },
                    modifiers: null);
                create.Invoke(tooltip, new object[] { new object(), (Action)(() => { }), true, null });
                var nested = (List<UIWorkspaceTooltipView>)Get(tooltip, "nested");
                Assert.That(nested, Has.Count.EqualTo(1));
                nested[0].PinAtCurrentPosition();
                Assert.That(tooltip.IsPinned, Is.False);

                tooltip.NotifySourceExit(Get<object>(tooltip, "sourceIdentity"));
                Invoke(tooltip, "Update");
                Assert.That(Get<bool>(tooltip, "visible"), Is.True,
                    "A pinned child must keep its parent card alive after parent source exit.");
                Assert.That(nested, Has.Count.EqualTo(1));
                Assert.That(nested[0].IsPinned, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void RootPinProtectsFiveLevelNestedBranchAcrossSiblingCleanupAndSourceExit()
        {
            var canvas = new GameObject("Rich tooltip deep root pin canvas", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            try
            {
                var tooltip = CreateTooltip(canvas.transform);
                var rootSource = new object();
                ShowPendingText(tooltip, rootSource);
                tooltip.PinAtCurrentPosition();

                var branch = new List<UIWorkspaceTooltipView> { tooltip };
                var owner = tooltip;
                for (var level = 0; level < 5; level++)
                {
                    owner = CreateNested(owner, new object(), $"deep.root.{level}");
                    branch.Add(owner);
                }

                foreach (var view in branch)
                {
                    Assert.That(view.transform.Find("Border")?.GetComponent<Graphic>()?.color,
                        Is.EqualTo(new Color(126f / 255f, 219f / 255f, 1f, 1f)),
                        $"Level {branch.IndexOf(view)} must stay visibly pinned through the inherited chain.");
                }

                // A link change on the root is a soft sibling cleanup. The entire
                // inherited-blue branch must survive it, and a new sibling may be
                // opened beside the retained branch.
                Invoke(tooltip, "ClearNested", true);
                Assert.That(Get<List<UIWorkspaceTooltipView>>(tooltip, "nested"), Has.Count.EqualTo(1));
                CreateNested(tooltip, new object(), "deep.root.sibling");
                Assert.That(Get<List<UIWorkspaceTooltipView>>(tooltip, "nested"), Has.Count.EqualTo(2));

                tooltip.NotifySourceExit(rootSource);
                Invoke(tooltip, "Update");
                Assert.That(Get<bool>(tooltip, "visible"), Is.True,
                    "A pinned root must survive its source exit while nested cards remain open.");
                Assert.That(Get<List<UIWorkspaceTooltipView>>(tooltip, "nested"), Has.Count.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void DeepPinProtectsAncestorsAndUnpinRestoresSoftCloseState()
        {
            var canvas = new GameObject("Rich tooltip deep descendant pin canvas", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            try
            {
                var tooltip = CreateTooltip(canvas.transform);
                ShowPendingText(tooltip, new object());

                var branch = new List<UIWorkspaceTooltipView> { tooltip };
                var owner = tooltip;
                for (var level = 0; level < 4; level++)
                {
                    owner = CreateNested(owner, new object(), $"deep.child.{level}");
                    branch.Add(owner);
                }

                var deepest = branch[^1];
                deepest.PinAtCurrentPosition();
                Assert.That((bool)Invoke(tooltip, "HasPinnedTooltipChain"), Is.True,
                    "A pinned descendant must protect every required ancestor.");

                Invoke(tooltip, "ClearNested", true);
                Assert.That(Get<List<UIWorkspaceTooltipView>>(tooltip, "nested"), Has.Count.EqualTo(1));
                Assert.That(Get<List<UIWorkspaceTooltipView>>(branch[^2], "nested"), Has.Count.EqualTo(1));

                deepest.TogglePinned();
                Assert.That(deepest.IsPinned, Is.False);
                Assert.That(Get<bool>(deepest, "isLocked"), Is.True,
                    "Unpinning must restore the normal enterable lock before soft close.");
                Assert.That((bool)Invoke(tooltip, "HasPinnedTooltipChain"), Is.False,
                    "After unpinning the deepest card, ancestors must no longer be protected by a pin.");
                Assert.That(deepest.transform.Find("Border")?.GetComponent<Graphic>()?.color,
                    Is.Not.EqualTo(new Color(126f / 255f, 219f / 255f, 1f, 1f)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void HardInvalidationClearsPinnedNestedTreeIncludingDeepDescendants()
        {
            var canvas = new GameObject("Rich tooltip hard invalidation canvas", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            try
            {
                var tooltip = CreateTooltip(canvas.transform);
                var rootSource = new object();
                ShowPendingText(tooltip, rootSource);
                var owner = tooltip;
                for (var level = 0; level < 3; level++)
                    owner = CreateNested(owner, new object(), $"invalidate.{level}");
                owner.PinAtCurrentPosition();

                // Destroy is deferred in production and reports one EditMode
                // warning per clone in this fixture.
                for (var i = 0; i < 3; i++)
                    LogAssert.Expect(LogType.Error, "Destroy may not be called from edit mode! Use DestroyImmediate instead.\nDestroying an object in edit mode destroys it permanently.");
                tooltip.InvalidateSource(rootSource);

                Assert.That(Get<List<UIWorkspaceTooltipView>>(tooltip, "nested"), Is.Empty,
                    "Hard invalidation of a root source clears the complete pinned nested tree.");
                Assert.That(Get<bool>(tooltip, "visible"), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void OutsideClickClearsDeepPinnedChainAsOneRootAction()
        {
            var canvas = new GameObject("Rich tooltip deep outside click canvas", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            try
            {
                var tooltip = CreateTooltip(canvas.transform);
                ShowPendingText(tooltip, new object());
                var owner = tooltip;
                for (var level = 0; level < 4; level++)
                    owner = CreateNested(owner, new object(), $"outside.{level}");
                owner.PinAtCurrentPosition();

                for (var i = 0; i < 4; i++)
                    LogAssert.Expect(LogType.Error, "Destroy may not be called from edit mode! Use DestroyImmediate instead.\nDestroying an object in edit mode destroys it permanently.");
                Assert.That(UIWorkspaceTooltipView.HandlePointerClick(
                    new Vector2(-10000f, -10000f), false), Is.False);
                Assert.That(Get<bool>(tooltip, "visible"), Is.False);
                Assert.That(Get<List<UIWorkspaceTooltipView>>(tooltip, "nested"), Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void PinnedTooltipKeepsPlacementWhenLayoutResizes()
        {
            var canvas = new GameObject("Rich tooltip placement canvas", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            try
            {
                var tooltip = CreateTooltip(canvas.transform);
                ShowPendingText(tooltip, new object());
                var rect = Get<RectTransform>(tooltip, "tooltipRect");
                var initialPosition = rect.anchoredPosition;
                tooltip.PinAtCurrentPosition();
                Set(tooltip, "resizeAfterLayout", true);
                Invoke(tooltip, "LateUpdate");
                Assert.That(rect.anchoredPosition, Is.EqualTo(initialPosition),
                    "A pinned tooltip must not be repositioned by a later layout pass.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void NestedTooltipCloneDisablesInheritedRuntimeRichContent()
        {
            var canvas = new GameObject("Rich tooltip clone canvas", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            try
            {
                var tooltip = CreateTooltip(canvas.transform);
                var richObject = new GameObject(
                    "RichUnitContent",
                    typeof(RectTransform),
                    typeof(UIWorkspaceRichUnitTooltipView));
                richObject.transform.SetParent(tooltip.transform, false);
                Set(tooltip, "richUnitContent", richObject.GetComponent<UIWorkspaceRichUnitTooltipView>());

                var create = typeof(UIWorkspaceTooltipView).GetMethod(
                    "CreateNestedTooltip",
                    InstancePrivate,
                    binder: null,
                    types: new[] { typeof(object), typeof(Action), typeof(bool), typeof(string) },
                    modifiers: null);
                Assert.That(create, Is.Not.Null);
                create.Invoke(tooltip, new object[] { new object(), (Action)(() => { }), false, null });

                var nested = (List<UIWorkspaceTooltipView>)Get(tooltip, "nested");
                Assert.That(nested, Has.Count.EqualTo(1));
                var clone = nested[0];
                Assert.That(
                    clone.GetComponentsInChildren<UIWorkspaceRichUnitTooltipView>(true)
                        .Any(view => view.gameObject.activeSelf),
                    Is.False,
                    "A nested clone must not display runtime-composed rich content copied from its parent.");
                Assert.That(Get<UIWorkspaceRichUnitTooltipView>(clone, "richUnitContent"), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void PinnedWorldCharacterValidationUsesStableViewAfterHoverReset()
        {
            var controllerObject = new GameObject("World tooltip controller");
            var viewObject = new GameObject("World tooltip target");
            try
            {
                var controller = controllerObject.AddComponent<CombatUnitInspectController>();
                var view = viewObject.AddComponent<ExplorationCharacterView>();
                var character = new CharacterInstance
                {
                    CharacterId = "world-target",
                    CurrentHealth = 100,
                    LifeState = CharacterLifeState.Alive,
                    Stats = new CharacterStats { MaxHealth = new Stat(100) }
                };
                view.Bind(character);

                Set(controller, "tooltipTarget", character);
                Set(controller, "tooltipTargetView", view);
                Set(controller, "hoveredView", null);

                var valid = (bool)Invoke(controller, "IsTooltipTargetValid");
                Assert.That(valid, Is.True,
                    "Pinned world inspection must validate the retained target view, not the transient hover field.");

                character.LifeState = CharacterLifeState.Dead;
                Assert.That((bool)Invoke(controller, "IsTooltipTargetValid"), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(viewObject);
                UnityEngine.Object.DestroyImmediate(controllerObject);
            }
        }

        [Test]
        public void WorldSoftHoverTransitionPreservesDeepPinnedTooltipBranch()
        {
            var canvas = new GameObject("World tooltip branch canvas", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var controllerObject = new GameObject("World tooltip branch controller");
            try
            {
                var tooltip = CreateTooltip(canvas.transform);
                var rootSource = new object();
                ShowPendingText(tooltip, rootSource);

                var owner = tooltip;
                for (var depth = 0; depth < 4; depth++)
                    owner = CreateNested(owner, new object(), $"world-branch-{depth}");
                owner.PinAtCurrentPosition();

                Assert.That(tooltip.HasPinnedBranch, Is.True,
                    "The world controller must see a deep descendant pin through the root view.");

                var controller = controllerObject.AddComponent<CombatUnitInspectController>();
                Set(controller, "worldTooltip", tooltip);
                Set(controller, "tooltipTarget", rootSource);
                Set(controller, "tooltipSource", rootSource);

                // A new world hover is a soft transition. It must notify the old
                // source while retaining the complete root-to-deep-pin branch.
                Invoke(controller, "ShowHover", CreateCharacter("new-world-target"), null);
                Assert.That(Get<bool>(tooltip, "visible"), Is.True);
                Assert.That(Get<List<UIWorkspaceTooltipView>>(tooltip, "nested"), Has.Count.EqualTo(1));
                Assert.That(tooltip.HasPinnedBranch, Is.True);

                // The controller's source-exit path is shared by UI/menu exits;
                // it must also retain the branch and its original source state.
                Invoke(controller, "NotifyTooltipSourceExit");
                Assert.That(Get<bool>(tooltip, "visible"), Is.True);
                Assert.That(Get<object>(controller, "tooltipTarget"), Is.SameAs(rootSource));
                Assert.That(Get<object>(controller, "tooltipSource"), Is.SameAs(rootSource));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(controllerObject);
                UnityEngine.Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void SourceExitThenDeepPinStillHardClosesWhenWorldTargetDies()
        {
            var canvas = new GameObject("World tooltip validation canvas", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var controllerObject = new GameObject("World tooltip validation controller");
            var viewObject = new GameObject("World tooltip validation target");
            try
            {
                var tooltip = CreateTooltip(canvas.transform);
                var character = CreateCharacter("world-validation-target");
                var sourceView = viewObject.AddComponent<ExplorationCharacterView>();
                sourceView.Bind(character);
                ShowPendingText(tooltip, character);

                var controller = controllerObject.AddComponent<CombatUnitInspectController>();
                Set(controller, "worldTooltip", tooltip);
                Set(controller, "tooltipTarget", character);
                Set(controller, "tooltipSource", character);
                Set(controller, "tooltipTargetView", sourceView);

                // The source leaves before the pointer reaches the glossary
                // branch. The target must remain available for hard validation.
                Invoke(controller, "NotifyTooltipSourceExit");
                var owner = tooltip;
                for (var depth = 0; depth < 3; depth++)
                    owner = CreateNested(owner, new object(), $"world-validation-{depth}");
                owner.PinAtCurrentPosition();

                character.LifeState = CharacterLifeState.Dead;
                Assert.That((bool)Invoke(controller, "IsTooltipTargetValid"), Is.False,
                    "A deep pin must not hide the original world target from fog/death validation.");

                for (var depth = 0; depth < 3; depth++)
                    LogAssert.Expect(LogType.Error, "Destroy may not be called from edit mode! Use DestroyImmediate instead.\nDestroying an object in edit mode destroys it permanently.");
                Invoke(controller, "ClearTooltip");
                Assert.That(Get<bool>(tooltip, "visible"), Is.False);
                Assert.That(Get<List<UIWorkspaceTooltipView>>(tooltip, "nested"), Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(viewObject);
                UnityEngine.Object.DestroyImmediate(controllerObject);
                UnityEngine.Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void RepeatedWorldHoverKeepsPendingShowUntilHoverDelayCompletes()
        {
            var canvas = new GameObject("World tooltip pending canvas", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var controllerObject = new GameObject("World tooltip pending controller");
            var viewObject = new GameObject("World tooltip pending target");
            try
            {
                var tooltip = CreateTooltip(canvas.transform);
                var character = CreateCharacter("world-pending-target");
                var sourceView = viewObject.AddComponent<ExplorationCharacterView>();
                sourceView.Bind(character);
                var controller = controllerObject.AddComponent<CombatUnitInspectController>();
                Set(controller, "worldTooltip", tooltip);
                Set(controller, "hoveredView", sourceView);

                // This is the same production ShowHover call made every frame
                // while the pointer remains over one world character. The first
                // call queues the delay; the repeats must not invalidate it.
                Invoke(controller, "ShowHover", character, null);
                var pendingSource = Get<object>(tooltip, "pendingSource");
                Assert.That(Get<Action>(tooltip, "pendingShow"), Is.Not.Null);
                Assert.That(pendingSource, Is.SameAs(sourceView));

                Invoke(controller, "ShowHover", character, null);
                Invoke(controller, "ShowHover", character, null);
                Assert.That(Get<object>(tooltip, "pendingSource"), Is.SameAs(pendingSource));
                Assert.That(Get<Action>(tooltip, "pendingShow"), Is.Not.Null,
                    "Repeated world hover must not reset the pending show before hoverDelay.");

                ShowPending(tooltip);
                Assert.That(Get<bool>(tooltip, "visible"), Is.True,
                    "The retained pending hover must eventually display its world tooltip.");

                // Source exit followed by a natural hide must release the
                // dedup state so re-entering the same world character queues a
                // fresh presentation rather than treating the hidden card as
                // still active.
                Invoke(controller, "NotifyTooltipSourceExit");
                tooltip.Hide();
                Invoke(controller, "ShowHover", character, null);
                Assert.That(Get<Action>(tooltip, "pendingShow"), Is.Not.Null,
                    "The same world target must be allowed to reopen after a natural hide.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(viewObject);
                UnityEngine.Object.DestroyImmediate(controllerObject);
                UnityEngine.Object.DestroyImmediate(canvas);
            }
        }

        [UnityTest]
        public IEnumerator TurnPortraitOwnerEmitsOneRichHoverAndDisablesLegacyTrigger()
        {
            var canvas = new GameObject("Turn tooltip wiring canvas", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            try
            {
                var tooltip = CreateTooltip(canvas.transform);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TurnPortraitPrefabPath);
                Assert.That(prefab, Is.Not.Null);
                var portraitRoot = UnityEngine.Object.Instantiate(prefab, canvas.transform);
                var portrait = portraitRoot.GetComponentInChildren<CombatTurnPortraitView>(true);
                Assert.That(portrait, Is.Not.Null);
                Invoke(portrait, "Awake");
                var rootSurface = portrait.GetComponent<Graphic>();
                Assert.That(rootSurface, Is.Not.Null);
                Assert.That(rootSurface.enabled, Is.True);
                Assert.That(rootSurface.raycastTarget, Is.True,
                    "The real turn portrait prefab must expose its authored root surface to GraphicRaycaster.");

                canvas.GetComponent<RectTransform>().sizeDelta = new Vector2(1920f, 1080f);
                var raycaster = canvas.AddComponent<GraphicRaycaster>();
                rootSurface.SetAllDirty();
                yield return null;
                Canvas.ForceUpdateCanvases();
                var pointer = new PointerEventData(null)
                {
                    position = RectTransformUtility.WorldToScreenPoint(null,
                        rootSurface.rectTransform.TransformPoint(rootSurface.rectTransform.rect.center))
                };
                var raycastResults = new List<RaycastResult>();
                raycaster.Raycast(pointer, raycastResults);
                Assert.That(raycastResults.Any(result => result.gameObject == portrait.gameObject), Is.True,
                    "The portrait root must be the actual GraphicRaycaster hit before pointer dispatch.");

                var unit = CreateUnit("turn-target");
                var hudRoot = new GameObject("Combat HUD", typeof(RectTransform), typeof(CombatHudView));
                hudRoot.transform.SetParent(canvas.transform, false);
                var hud = hudRoot.GetComponent<CombatHudView>();
                var oldPanel = new GameObject("Retired name HP panel");
                oldPanel.transform.SetParent(hudRoot.transform, false);
                var bootstrapRoot = new GameObject("Inactive combat runtime");
                bootstrapRoot.transform.SetParent(canvas.transform, false);
                bootstrapRoot.SetActive(false);
                var bootstrap = bootstrapRoot.AddComponent<CombatSandboxBootstrap>();
                var driver = (CombatExecutionDriver)typeof(CombatSandboxBootstrap).GetMethod("CreateDriver", InstancePrivate).Invoke(bootstrap, null);
                var session = new CombatSession { ActiveUnit = unit };
                session.Units.Add(unit);
                typeof(CombatExecutionDriver).GetProperty("Session").SetValue(driver, session);
                typeof(CombatSandboxBootstrap).GetField("focused", InstancePrivate).SetValue(bootstrap, driver);
                var hudState = new SerializedObject(hud);
                hudState.FindProperty("sandboxBootstrap").objectReferenceValue = bootstrap;
                hudState.FindProperty("targetPanel").objectReferenceValue = oldPanel;
                hudState.ApplyModifiedPropertiesWithoutUndo();
                Invoke(hud, "Awake");
                Assert.That(oldPanel.activeSelf, Is.False, "Authored legacy panels must be hidden at HUD startup.");
                portrait.Bind(unit, false, source: hud, known: true);
                portrait.OnPointerEnter(new PointerEventData(null));
                Invoke(hud, "LateUpdate");
                Assert.That(hud.HighlightedUnit, Is.SameAs(unit), "Retiring the panel must preserve timeline/world highlighting.");
                Assert.That(oldPanel.activeSelf, Is.False, "Timeline hover must not enable a second name/HP tooltip.");
                oldPanel.SetActive(true);
                Invoke(hud, "LateUpdate");
                Assert.That(oldPanel.activeSelf, Is.False, "The retired panel must stay hidden throughout the HUD refresh loop.");
                ShowPending(tooltip);

                var rich = Get<UIWorkspaceRichUnitTooltipView>(tooltip, "richUnitContent");
                Assert.That(rich, Is.Not.Null);
                Assert.That(Get<BattleUnit>(rich, "boundUnit"), Is.SameAs(unit));
                var legacy = portraitRoot.GetComponent<CombatSimpleTooltipTrigger>();
                Assert.That(legacy, Is.Not.Null);
                Assert.That(legacy.enabled, Is.False,
                    "The owner must be the only timeline hover source; the serialized trigger is compatibility data only.");
                portrait.OnPointerExit(new PointerEventData(null));
                Assert.That(hud.HighlightedUnit, Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void TurnPortraitUnknownRebindInvalidatesPreviousOwnerTooltip()
        {
            var canvas = new GameObject("Unknown turn tooltip canvas", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            try
            {
                var tooltip = CreateTooltip(canvas.transform);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TurnPortraitPrefabPath);
                Assert.That(prefab, Is.Not.Null);
                var portraitRoot = UnityEngine.Object.Instantiate(prefab, canvas.transform);
                var portrait = portraitRoot.GetComponentInChildren<CombatTurnPortraitView>(true);
                Assert.That(portrait, Is.Not.Null);

                portrait.Bind(CreateUnit("fog-a"), false, source: null, known: false);
                portrait.OnPointerEnter(new PointerEventData(null));
                ShowPending(tooltip);
                Assert.That(Get<bool>(tooltip, "visible"), Is.True);

                // Timeline entry objects are reused. A new hidden unit must not
                // inherit the previous owner's pinned/visible text card.
                portrait.Bind(CreateUnit("fog-b"), false, source: null, known: false);
                Assert.That(Get<bool>(tooltip, "visible"), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void PartyRailBattleRowUsesBattleWrapperForRichStatusesAndAuthoredBars()
        {
            var canvas = new GameObject("Party rail tooltip wiring canvas", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            try
            {
                var tooltip = CreateTooltip(canvas.transform);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PartyPortraitPrefabPath);
                Assert.That(prefab, Is.Not.Null);
                var rowRoot = UnityEngine.Object.Instantiate(prefab, canvas.transform);
                var row = rowRoot.GetComponentInChildren<CharacterInfoPartyPortraitView>(true);
                Assert.That(row, Is.Not.Null);

                var character = CreateCharacter("rail-target");
                var unit = new BattleUnit { Character = character, IsPlayerSide = true };
                row.Bind(character, null);
                row.RefreshPresentation(unit, false, false);
                Set(row, "portraitHovered", true);
                Set(row, "tooltipView", tooltip);
                Invoke(row, "ShowPortraitTooltip");
                ShowPending(tooltip);

                var rich = Get<UIWorkspaceRichUnitTooltipView>(tooltip, "richUnitContent");
                Assert.That(rich, Is.Not.Null);
                Assert.That(Get<BattleUnit>(rich, "boundUnit"), Is.SameAs(unit),
                    "Combat rows must retain auras/redirects from BattleUnit for status presentation.");

                var health = rich.transform.Find("Identity/Vitals/HealthBar")?.GetComponent<Slider>();
                var resource = rich.transform.Find("Identity/Vitals/ResourceBar")?.GetComponent<Slider>();
                Assert.That(health, Is.Not.Null);
                Assert.That(resource, Is.Not.Null);
                var healthStyle = health.GetComponent<UIWorkspaceSliderStyleApplier>();
                var resourceStyle = resource.GetComponent<UIWorkspaceSliderStyleApplier>();
                Assert.That(healthStyle, Is.Not.Null);
                Assert.That(resourceStyle, Is.Not.Null);
                Assert.That(Get<UIWorkspaceSliderStyle>(healthStyle, "sliderStyle"),
                    Is.EqualTo(UIWorkspaceSliderStyle.Health));
                Assert.That(Get<UIWorkspaceSliderStyle>(resourceStyle, "sliderStyle"),
                    Is.EqualTo(UIWorkspaceSliderStyle.Resource));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void RichStatusRowKeepsCountersInsideEachStatusSlot()
        {
            var canvas = new GameObject("Rich tooltip status geometry canvas", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            try
            {
                var tooltip = CreateTooltip(canvas.transform);
                var richObject = new GameObject(
                    "RichUnitContent",
                    typeof(RectTransform),
                    typeof(UIWorkspaceRichUnitTooltipView));
                richObject.transform.SetParent(tooltip.transform, false);
                var rich = richObject.GetComponent<UIWorkspaceRichUnitTooltipView>();
                var bodyText = Get<TMPro.TextMeshProUGUI>(tooltip, "bodyText");
                rich.Configure(bodyText, null, tooltip);

                Invoke(rich, "EnsureStatusIcons", 1);
                var statusRoot = Get<RectTransform>(rich, "statusRoot");
                statusRoot.gameObject.SetActive(true);
                Get<TMPro.TextMeshProUGUI>(rich, "statusHeading").gameObject.SetActive(true);

                var status = statusRoot.Find("Status") as RectTransform;
                Assert.That(status, Is.Not.Null);

                var icon = status.GetComponent<Image>().rectTransform;
                var stack = status.Find("Stack") as RectTransform;
                var duration = status.Find("Duration") as RectTransform;
                Assert.That(stack, Is.Not.Null);
                Assert.That(duration, Is.Not.Null);

                foreach (var child in new[] { icon, stack, duration })
                {
                    Assert.That(child.anchorMin, Is.EqualTo(Vector2.zero), child.name);
                    Assert.That(child.anchorMax, Is.EqualTo(Vector2.one), child.name);
                    Assert.That(child.offsetMin, Is.EqualTo(Vector2.zero), child.name);
                    Assert.That(child.offsetMax, Is.EqualTo(Vector2.zero), child.name);
                }

                var preferredHeight = rich.GetPreferredHeight(336f);
                Assert.That(preferredHeight, Is.GreaterThanOrEqualTo(154f),
                    "The preferred rich content height must include identity, Statusy and the status row so tooltipRect covers the visible row.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvas);
            }
        }

        private static UIWorkspaceTooltipView CreateTooltip(Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TooltipPrefabPath);
            Assert.That(prefab, Is.Not.Null);
            var root = UnityEngine.Object.Instantiate(prefab, parent);
            root.SetActive(true);
            var tooltip = root.GetComponentInChildren<UIWorkspaceTooltipView>(true);
            Assert.That(tooltip, Is.Not.Null);
            // EditMode instantiation does not run the MonoBehaviour lifecycle.
            Invoke(tooltip, "Awake");
            Invoke(tooltip, "OnEnable");
            return tooltip;
        }

        private static void ShowPendingText(UIWorkspaceTooltipView tooltip, object source)
        {
            tooltip.ShowText("Tytuł", "Treść", source);
            ShowPending(tooltip);
        }

        private static void ShowPending(UIWorkspaceTooltipView tooltip)
        {
            var show = (Action)Get(tooltip, "pendingShow");
            Assert.That(show, Is.Not.Null);
            // Match Update: consume the callback before displaying its content.
            Set(tooltip, "pendingShow", null);
            show();
        }

        private static UIWorkspaceTooltipView CreateNested(
            UIWorkspaceTooltipView owner,
            object source,
            string glossaryId,
            bool pinnedContent = false)
        {
            var create = typeof(UIWorkspaceTooltipView).GetMethod(
                "CreateNestedTooltip",
                InstancePrivate,
                binder: null,
                types: new[] { typeof(object), typeof(Action), typeof(bool), typeof(string) },
                modifiers: null);
            Assert.That(create, Is.Not.Null);
            create.Invoke(owner, new object[]
            {
                source,
                (Action)(() => Invoke(owner, "SetContent", null, glossaryId, "Treść", string.Empty, false)),
                pinnedContent,
                glossaryId
            });

            var nested = Get<List<UIWorkspaceTooltipView>>(owner, "nested");
            Assert.That(nested, Is.Not.Empty);
            return nested[^1];
        }

        private static void AssertTooltipFooter(UIWorkspaceTooltipView tooltip)
        {
            var footers = tooltip.GetComponentsInChildren<RectTransform>(true)
                .Where(rect => rect.name == "TooltipFooter")
                .ToArray();
            Assert.That(footers, Has.Length.EqualTo(1));

            var footer = footers[0];
            Assert.That(footer.GetSiblingIndex(), Is.EqualTo(footer.parent.childCount - 1));
            var shortcut = footer.Find("Shortcut")?.GetComponent<TMPro.TextMeshProUGUI>();
            Assert.That(shortcut, Is.Not.Null);
            Assert.That(shortcut.text, Is.EqualTo("T —"));
            Assert.That(footer.Find("LockGlyph")?.GetComponent<UIWorkspaceTooltipLockGlyph>(), Is.Not.Null);
            Assert.That(LayoutUtility.GetPreferredHeight(footer), Is.GreaterThan(0f));
        }

        private static BattleUnit CreateUnit(string id)
        {
            return new BattleUnit { Character = CreateCharacter(id), IsPlayerSide = false };
        }

        private static CharacterInstance CreateCharacter(string id)
        {
            return new CharacterInstance
            {
                CharacterId = id,
                CurrentHealth = 80,
                LifeState = CharacterLifeState.Alive,
                Stats = new CharacterStats { MaxHealth = new Stat(100) }
            };
        }

        private static object Invoke(object target, string method)
        {
            return target.GetType().GetMethod(method, InstancePrivate).Invoke(target, null);
        }

        private static object Invoke(object target, string method, params object[] arguments)
        {
            var methodInfo = target.GetType()
                .GetMethods(InstancePrivate)
                .Single(candidate => candidate.Name == method
                    && candidate.GetParameters().Length == arguments.Length);
            return methodInfo.Invoke(target, arguments);
        }

        private static object Get(object target, string field)
        {
            return target.GetType().GetField(field, InstancePrivate).GetValue(target);
        }

        private static T Get<T>(object target, string field)
        {
            return (T)Get(target, field);
        }

        private static void Set(object target, string field, object value)
        {
            target.GetType().GetField(field, InstancePrivate).SetValue(target, value);
        }
    }
}

using System.Reflection;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Runtime.Characters;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Godmist.Runtime.Game;

namespace Godmist.Tests.EditMode
{
    public class PartyRailPresentationTests
    {
        const string Path = "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_PartyMember.prefab";
        static T Ref<T>(Object owner, string field) where T : Object
            => owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner) as T;
        static CharacterInstance Character(CharacterLifeState state = CharacterLifeState.Alive, int hp = 350)
            => new() { CharacterId = "Jerzy", Stats = new CharacterStats { MaxHealth = new Stat(350) },
                CurrentHealth = hp, LifeState = state, ResourcePool = new ResourcePool(ResourceType.Rage, 0, 200, 0) };

        [TestCase(CharacterLifeState.Alive, 350, "")]
        [TestCase(CharacterLifeState.Downed, 0, "Powalony")]
        [TestCase(CharacterLifeState.Dead, 0, "Martwy")]
        public void LifeStateHasReadableLabelAndRestoresPortraitTint(CharacterLifeState state, int hp, string label)
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Path));
            try
            {
                var view = root.GetComponent<CharacterInfoPartyPortraitView>();
                var character = Character(state, hp); view.Bind(character, null); view.RefreshPresentation(null, true, false);
                var text = Ref<TextMeshProUGUI>(view, "lifeStateText");
                Assert.That(text.text, Is.EqualTo(label));
                Assert.That(text.gameObject.activeSelf, Is.EqualTo(state != CharacterLifeState.Alive));
                Assert.That(text.transform.GetSiblingIndex(), Is.GreaterThan(Ref<Image>(root.GetComponent<CombatPartyMemberView>(), "lifeStateMarker").transform.GetSiblingIndex()));
                Assert.That(Ref<Image>(view, "selectedMarker").enabled, Is.True);
                var tint = Ref<Image>(view, "portrait").color;
                Assert.That(tint, Is.EqualTo(state == CharacterLifeState.Alive ? Color.white
                    : state == CharacterLifeState.Downed ? new Color(.65f, .5f, .5f) : new Color(.4f, .4f, .4f)));
                character.LifeState = CharacterLifeState.Alive; character.CurrentHealth = 350;
                view.RefreshPresentation(null, false, false);
                Assert.That(Ref<Image>(view, "portrait").color, Is.EqualTo(Color.white));
                Assert.That(text.gameObject.activeSelf, Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void CriticalHealthChangesFillAndZeroResourceStillShowsExactValue()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Path));
            try
            {
                var view = root.GetComponent<CharacterInfoPartyPortraitView>(); var c = Character();
                view.Bind(c, null); view.RefreshPresentation(null, false, false);
                var healthy = Ref<Image>(view, "healthFill").color;
                c.CurrentHealth = 50; view.RefreshPresentation(null, false, false);
                Assert.That(Ref<Image>(view, "healthFill").color, Is.Not.EqualTo(healthy));
                var presenter = root.GetComponent<CombatPartyMemberView>();
                Assert.That(Ref<TextMeshProUGUI>(presenter, "healthValueText").text, Is.EqualTo("50/350"));
                Assert.That(Ref<TextMeshProUGUI>(presenter, "resourceValueText").text, Is.EqualTo("0/200"));
                Assert.That(Ref<Slider>(presenter, "resourceSlider").gameObject.activeSelf, Is.True);
                c.ResourcePool = null; view.RefreshPresentation(null, false, false);
                Assert.That(Ref<Slider>(presenter, "resourceSlider").gameObject.activeSelf, Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(ResourceType.Rage, "Furia")]
        [TestCase(ResourceType.Momentum, "Impet")]
        [TestCase(ResourceType.Mana, "Mana")]
        public void TooltipNamesResourceAndReportsHealthAndLifeState(ResourceType type, string name)
        {
            var c = Character(CharacterLifeState.Downed, 0); c.ResourcePool.ResourceType = type;
            var body = (string)typeof(CharacterInfoPartyPortraitView).GetMethod("BuildPortraitTooltipBody", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { c });
            Assert.That(body, Does.Contain("Zdrowie: 0/350"));
            Assert.That(body, Does.Contain(name + ": 0/200"));
            Assert.That(body, Does.Contain("Powalony"));
        }

        [Test]
        public void AuthoredCardKeepsStatusesOutsidePortraitAndBarsAndUsesGoldSelection()
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
            var view = root.GetComponent<CharacterInfoPartyPortraitView>();
            Assert.That(Ref<TextMeshProUGUI>(view, "nameText"), Is.Not.Null);
            var status = Ref<CombatStatusCollectionView>(view, "statusCollection");
            var resource = Ref<Slider>(root.GetComponent<CombatPartyMemberView>(), "resourceSlider");
            var corners = new Vector3[4];
            Rect Bounds(RectTransform rect)
            {
                rect.GetWorldCorners(corners);
                return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
            }
            var statusBounds = Bounds((RectTransform)status.transform);
            Assert.That(statusBounds.Overlaps(Bounds(Ref<Image>(view, "portrait").rectTransform)), Is.False);
            foreach (var slider in root.GetComponentsInChildren<Slider>(true))
                Assert.That(statusBounds.Overlaps(Bounds((RectTransform)slider.transform)), Is.False);
            Assert.That(Ref<RectTransform>(view, "resourceRect"), Is.SameAs(resource.transform));
            Assert.That(status.GetComponentInChildren<VerticalLayoutGroup>(true).enabled, Is.True);
            Assert.That(status.GetComponent<HorizontalLayoutGroup>().enabled, Is.False);
            Assert.That(Ref<Image>(view, "selectedMarker").sprite.name, Is.EqualTo("slot_selected"));
            foreach (var slider in root.GetComponentsInChildren<Slider>(true))
                Assert.That(slider.interactable, Is.False);
        }

        [TestCase(ResourceType.Rage, "Furia")]
        [TestCase(ResourceType.Momentum, "Impet")]
        [TestCase(ResourceType.Mana, "Mana")]
        public void HoveringResourceBarShowsUnitVitalsWithoutCoveringStatusTooltip(ResourceType type, string name)
        {
            var canvas = new GameObject("canvas", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            try
            {
                var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Path), canvas.transform);
                root.SetActive(true);
                var view = root.GetComponent<CharacterInfoPartyPortraitView>();
                var c = Character(); c.ResourcePool.ResourceType = type;
                view.Bind(c, null); view.RefreshPresentation(null, false, false);
                var tooltip = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_Tooltip.prefab"), canvas.transform).GetComponentInChildren<UIWorkspaceTooltipView>(true);
                var state = new SerializedObject(view); state.FindProperty("tooltipView").objectReferenceValue = tooltip; state.ApplyModifiedPropertiesWithoutUndo();
                Canvas.ForceUpdateCanvases();
                var resource = Ref<RectTransform>(view, "resourceRect");
                view.OnPointerMove(new PointerEventData(null) { position = RectTransformUtility.WorldToScreenPoint(null, resource.TransformPoint(resource.rect.center)) });
                var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                Assert.That(typeof(UIWorkspaceTooltipView).GetField("pendingSource", flags).GetValue(tooltip), Is.SameAs(view));
                ((System.Action)typeof(UIWorkspaceTooltipView).GetField("pendingShow", flags).GetValue(tooltip)).Invoke();
                Assert.That(Ref<TextMeshProUGUI>(tooltip, "titleText").text, Is.EqualTo(c.CharacterId));
                var rich = Ref<UIWorkspaceRichUnitTooltipView>(tooltip, "richUnitContent");
                Assert.That(Ref<TextMeshProUGUI>(rich, "resourceText").text, Does.StartWith(name));
                var other = new object(); tooltip.ShowText("Status", "Efekt", other);
                var statusRect = (RectTransform)Ref<CombatStatusCollectionView>(view, "statusCollection").transform;
                view.OnPointerMove(new PointerEventData(null) { position = RectTransformUtility.WorldToScreenPoint(null, statusRect.TransformPoint(statusRect.rect.center)) });
                Assert.That(typeof(UIWorkspaceTooltipView).GetField("pendingSource", flags).GetValue(tooltip), Is.SameAs(other));
            }
            finally { Object.DestroyImmediate(canvas); }
        }

        [Test]
        public void PortraitHoverUsesSharedUnitCardWithoutControlInstructions()
        {
            var canvas = new GameObject("canvas", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            try
            {
                var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Path), canvas.transform);
                var view = root.GetComponent<CharacterInfoPartyPortraitView>(); view.Bind(Character(), null);
                var tooltip = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_Tooltip.prefab"), canvas.transform).GetComponentInChildren<UIWorkspaceTooltipView>(true);
                var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                typeof(UIWorkspaceTooltipView).GetMethod("Awake", flags).Invoke(tooltip, null);
                var defaultWidth = Ref<RectTransform>(tooltip, "tooltipRect").rect.width;
                var state = new SerializedObject(view); state.FindProperty("tooltipView").objectReferenceValue = tooltip; state.ApplyModifiedPropertiesWithoutUndo();
                Canvas.ForceUpdateCanvases();
                var portrait = Ref<Image>(view, "portrait");
                view.OnPointerEnter(new PointerEventData(null) { position = RectTransformUtility.WorldToScreenPoint(null, portrait.rectTransform.TransformPoint(portrait.rectTransform.rect.center)) });
                ((System.Action)typeof(UIWorkspaceTooltipView).GetField("pendingShow", flags).GetValue(tooltip)).Invoke();
                var rich = Ref<UIWorkspaceRichUnitTooltipView>(tooltip, "richUnitContent");
                var body = string.Join("\n", rich.GetComponentsInChildren<TextMeshProUGUI>().Select(text => text.text));
                Assert.That(body, Does.Contain("350/350")); Assert.That(body, Does.Contain("0/200"));
                foreach (var instruction in new[] { "LPM", "PPM", "Tab", "Przeciągnij" }) Assert.That(body, Does.Not.Contain(instruction));
                Assert.That(rich.gameObject.activeSelf, Is.True);
            }
            finally { Object.DestroyImmediate(canvas); }
        }

        [Test]
        public void PortraitExitDoesNotCancelAnotherTooltipAndRebindInvalidatesOwnHover()
        {
            var canvas = new GameObject("canvas", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            try
            {
                var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Path), canvas.transform);
                root.SetActive(true); var view = root.GetComponent<CharacterInfoPartyPortraitView>(); view.Bind(Character(), null);
                var tooltip = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_Tooltip.prefab"), canvas.transform).GetComponentInChildren<UIWorkspaceTooltipView>(true);
                var state = new SerializedObject(view); state.FindProperty("tooltipView").objectReferenceValue = tooltip; state.ApplyModifiedPropertiesWithoutUndo();
                Canvas.ForceUpdateCanvases();
                var portrait = Ref<Image>(view, "portrait");
                var e = new PointerEventData(null) { position = RectTransformUtility.WorldToScreenPoint(null, portrait.rectTransform.TransformPoint(portrait.rectTransform.rect.center)) };
                view.OnPointerEnter(e);
                var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                Assert.That(typeof(UIWorkspaceTooltipView).GetField("pendingSource", flags).GetValue(tooltip), Is.SameAs(view));
                view.Bind(Character(), null);
                Assert.That(typeof(UIWorkspaceTooltipView).GetField("pendingSource", flags).GetValue(tooltip), Is.Null);
                var other = new object(); tooltip.ShowText("Status", "Efekt", other);
                view.OnPointerExit(e);
                Assert.That(typeof(UIWorkspaceTooltipView).GetField("pendingSource", flags).GetValue(tooltip), Is.SameAs(other));
                Assert.That(typeof(UIWorkspaceTooltipView).GetField("sourceHovered", flags).GetValue(tooltip), Is.True);
            }
            finally { Object.DestroyImmediate(canvas); }
        }

        [Test]
        public void GroupBracketShowsMembershipAndDropTargetWithoutCoveringCards()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_PartyGroup.prefab"));
            try
            {
                var group = root.GetComponent<PartyRailGroupView>();
                group.ConfigurePresentation(2, false);
                var line = Ref<Image>(group, "connector");
                var normalColor = line.color;
                Assert.That(line.enabled, Is.True);
                Assert.That(Ref<Image>(group, "connectorTop").enabled, Is.True);
                Assert.That(Ref<Image>(group, "connectorBottom").enabled, Is.True);
                Assert.That(Ref<Image>(group, "separator").enabled, Is.False);
                Assert.That(Ref<Image>(group, "dropHighlight").enabled, Is.False);
                group.Highlight(true);
                Assert.That(line.color, Is.Not.EqualTo(normalColor));
                Assert.That(Ref<Image>(group, "dropHighlight").enabled, Is.False);
                group.Highlight(false); Assert.That(line.color, Is.EqualTo(normalColor));
                group.ConfigurePresentation(1, true);
                Assert.That(line.enabled, Is.False);
                Assert.That(Ref<Image>(group, "separator").enabled, Is.True);
                group.Highlight(true); Assert.That(line.enabled, Is.True);
                group.Highlight(false); Assert.That(line.enabled, Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void CancelledRailDragReleasesTooltipSuppression()
        {
            var canvas = new GameObject("canvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                var tooltip = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_Tooltip.prefab"), canvas.transform).GetComponentInChildren<UIWorkspaceTooltipView>(true);
                tooltip.gameObject.SetActive(true);
                var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                typeof(UIWorkspaceTooltipView).GetMethod("Awake", flags).Invoke(tooltip, null);
                typeof(UIWorkspaceTooltipView).GetMethod("OnEnable", flags).Invoke(tooltip, null);
                var railRoot = new GameObject("rail", typeof(RectTransform)); railRoot.transform.SetParent(canvas.transform, false);
                var rail = railRoot.AddComponent<CharacterInfoPartyRailView>();
                typeof(CharacterInfoPartyRailView).GetField("session", flags).SetValue(rail, new GameSession());
                var row = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Path), railRoot.transform).GetComponent<CharacterInfoPartyPortraitView>();
                rail.BeginDrag(row, new PointerEventData(null));
                Assert.That(tooltip.IsSkillTooltipSuppressed, Is.True);
                tooltip.ShowText("Status", "Efekt", new object());
                rail.Drag(new PointerEventData(null));
                Assert.That(typeof(UIWorkspaceTooltipView).GetField("pendingSource", flags).GetValue(tooltip), Is.Null);
                rail.CancelDrag(); Assert.That(tooltip.IsSkillTooltipSuppressed, Is.False);
            }
            finally { Object.DestroyImmediate(canvas); }
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Skills;
using Godmist.Runtime.Game;
using Godmist.Systems.Characters;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Godmist.Tests.EditMode
{
    public class SkillLoadoutPresentationTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        static object Field(object owner, string name) => owner.GetType().GetField(name, Flags).GetValue(owner);
        static void Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, Flags).Invoke(owner, args);
        static void SetSkill(SkillData skill, string name, object value) => typeof(SkillData).GetField("<" + name + ">k__BackingField", Flags).SetValue(skill, value);
        static string CardLabel(object card) => ((TMP_Text)typeof(SkillLoadoutCardView).GetField("slotStateText", Flags).GetValue(card)).text;

        [TestCase("SD_KnifeToss")]
        [TestCase("SD_ScoutSmoke")]
        [TestCase("SD_ScoutSwap")]
        public void CombatSkillTooltipUsesReviewedCalculatedSummaryWithoutRepeatingAuthoredCopy(string asset)
        {
            var node = new GameObject("Combat tooltip", typeof(RectTransform), typeof(UIWorkspaceTooltipView));
            try
            {
                var skill = AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Skills/" + asset + ".asset");
                Assert.That(skill, Is.Not.Null);
                var body = (string)typeof(UIWorkspaceTooltipView).GetMethod("BuildSkillDescription", Flags)
                    .Invoke(node.GetComponent<UIWorkspaceTooltipView>(), new object[] { skill, null });
                body = System.Text.RegularExpressions.Regex.Replace(body, "<[^>]*>", "");
                Assert.That(body, Does.Not.Contain(skill.TooltipDescription));
                if (asset == "SD_KnifeToss")
                {
                    Assert.That(body, Does.Contain("W zwarciu: 60%"));
                    Assert.That(body.Split(new[] { "Zadaje" }, System.StringSplitOptions.None).Length, Is.EqualTo(2));
                    var meta = (string)typeof(UIWorkspaceTooltipView).GetMethod("BuildSkillMeta", BindingFlags.Static | BindingFlags.NonPublic)
                        .Invoke(null, new object[] { skill, 825, null });
                    Assert.That(meta, Does.Contain("SZTYLETY 1 na dystans").And.Contain("W zwarciu: bez zużycia"));
                }
                else if (asset == "SD_ScoutSmoke") Assert.That(body.Split(new[] { "80%" }, System.StringSplitOptions.None).Length, Is.EqualTo(2));
            }
            finally { Object.DestroyImmediate(node); }
        }

        [Test]
        public void SkillCardIconsFillMaskedInteriorAndKeepBadgesAboveThem()
        {
            var root=PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_SkillLoadoutWindow Variant.prefab");
            try
            {
                var cards=root.GetComponentsInChildren<SkillLoadoutCardView>(true);Assert.That(cards.Length,Is.EqualTo(3));
                foreach(var card in cards)
                {
                    var fields=typeof(SkillLoadoutCardView);
                    var icon=(Image)fields.GetField("icon",Flags).GetValue(card);
                    var mask=(Godmist.Views.UI.Workspace.SkillIconShapeMask)fields.GetField("iconMask",Flags).GetValue(card);
                    Assert.That(mask,Is.Not.Null);Assert.That(icon.transform.parent,Is.SameAs(mask.transform));
                    Assert.That(mask.GetComponent<Mask>().showMaskGraphic,Is.False);
                    Assert.That(icon.rectTransform.anchorMin,Is.EqualTo(Vector2.zero));Assert.That(icon.rectTransform.anchorMax,Is.EqualTo(Vector2.one));
                    Assert.That(icon.rectTransform.offsetMin,Is.EqualTo(Vector2.zero));Assert.That(icon.rectTransform.offsetMax,Is.EqualTo(Vector2.zero));
                    card.Configure(null,false,false,true,null,()=>{});Assert.That(icon.preserveAspect,Is.False);
                    var badge=(TMP_Text)fields.GetField("slotStateText",Flags).GetValue(card);
                    Assert.That(badge.transform.GetSiblingIndex(),Is.GreaterThan(mask.transform.GetSiblingIndex()));
                }
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }

        [Test]
        public void SmokeSummaryKeepsChanceDurationAndTargetInOneBullet()
        {
            var node = PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_Tooltip.prefab");
            try
            {
                var skill = AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Skills/SD_ScoutSmoke.asset");
                var body = node.GetComponent<UIWorkspaceTooltipView>().BuildLoadoutSkillDetails(skill, null);
                body = System.Text.RegularExpressions.Regex.Replace(body, "<[^>]*>", "");
                Assert.That(body, Does.Contain("80%").And.Contain("-20").And.Contain("3 tury").And.Contain("wszyscy wrogowie"));
                Assert.That(body.Split(new[] { "80%" }, System.StringSplitOptions.None).Length, Is.EqualTo(2), "The chance must be stated once.");
                Assert.That(body.Count(c => c == '•'), Is.EqualTo(1));
                Assert.That(skill.TooltipDescription, Does.Contain("80%"), "Presentation must not modify authored assets.");
            }
            finally { PrefabUtility.UnloadPrefabContents(node); }
        }

        [Test]
        public void SwapSummaryNamesTheActionAndShowsItsChanceOnce()
        {
            var node = new GameObject("Tooltip", typeof(RectTransform), typeof(UIWorkspaceTooltipView));
            var skill = Object.Instantiate(AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Skills/SD_ScoutSwap.asset"));
            try
            {
                string Body() => System.Text.RegularExpressions.Regex.Replace(node.GetComponent<UIWorkspaceTooltipView>().BuildLoadoutSkillDetails(skill, null), "<[^>]*>", "");
                var body = Body();
                Assert.That(body, Does.Contain("80% szansy na zamianę miejsc rzucającego z celem").And.Contain("pojedynczy wróg"));
                Assert.That(body, Does.Not.Contain("dodatkowy efekt"));
                Assert.That(body.Split(new[] { "80%" }, System.StringSplitOptions.None).Length, Is.EqualTo(2));
                typeof(Godmist.Data.Effects.SkillEffectData).GetField("baseChance", Flags).SetValue(skill.Effects[0], 1f);
                Assert.That(Body(), Does.Contain("Zamienia miejscami rzucającego i cel").And.Not.Contain("80%"));
            }
            finally { Object.DestroyImmediate(node); Object.DestroyImmediate(skill); }
        }

        [Test]
        public void AimedThrowSummaryKeepsMeleePenaltyCriticalAndKnifeRules()
        {
            var node = new GameObject("Tooltip", typeof(RectTransform), typeof(UIWorkspaceTooltipView));
            try
            {
                var skill = AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Skills/SD_ScoutAimedThrow.asset");
                var body = node.GetComponent<UIWorkspaceTooltipView>().BuildLoadoutSkillDetails(skill, null);
                body = System.Text.RegularExpressions.Regex.Replace(body, "<[^>]*>", "");
                Assert.That(body, Does.Contain("• W zwarciu: 60% obrażeń").And.Contain("zawsze krytyczne"));
                Assert.That(body, Does.Not.Contain("Zakres bazowy"));
                Assert.That(UIWorkspaceTooltipView.BuildLoadoutSkillCost(skill, null), Does.Contain("Sztylety: 1 na dystans").And.Contain("W zwarciu: bez zużycia"));
            }
            finally { Object.DestroyImmediate(node); }
        }

        [Test]
        public void CustomSkillRulesRetainAuthoredDescription()
        {
            var node = new GameObject("Tooltip", typeof(RectTransform), typeof(UIWorkspaceTooltipView));
            var skill = ScriptableObject.CreateInstance<SkillData>();
            try
            {
                SetSkill(skill, "TooltipDescription", "Specjalna reguła, której nie opisuje ogólny efekt.");
                var effect = new Godmist.Data.Effects.SkillEffectData();
                typeof(Godmist.Data.Effects.SkillEffectData).GetField("effectType", Flags).SetValue(effect, EffectType.MageSacrifice);
                typeof(SkillData).GetField("effects", Flags).SetValue(skill, new List<Godmist.Data.Effects.SkillEffectData> { effect });
                Assert.That(node.GetComponent<UIWorkspaceTooltipView>().BuildLoadoutSkillDetails(skill, null), Does.Contain(skill.TooltipDescription));
                typeof(Godmist.Data.Effects.SkillEffectData).GetField("effectType", Flags).SetValue(effect, EffectType.DealDamage);
                SetSkill(skill, "GuaranteedCritical", true); SetSkill(skill, "PointBlankDamageMultiplier", .6f);
                var body = node.GetComponent<UIWorkspaceTooltipView>().BuildLoadoutSkillDetails(skill, null);
                Assert.That(body, Does.Contain("zawsze krytyczne").And.Contain("W zwarciu: 60%"));
            }
            finally { Object.DestroyImmediate(node); Object.DestroyImmediate(skill); }
        }

        [Test]
        public void DropFeedbackTakesPriorityOverHoverAndKeepsSelection()
        {
            var node = PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_SkillLoadoutWindow Variant.prefab");
            try
            {
                var card = node.transform.Find("Content/EquippedSection/Content/ActiveColumn/ActiveScroll/Viewport/ActiveSlotsRoot/ActiveSlotTemplate").GetComponent<SkillLoadoutCardView>();
                var fieldType = typeof(SkillLoadoutCardView);
                var feedback = (Image)fieldType.GetField("feedbackBorder", Flags).GetValue(card);
                var selection = (Image)fieldType.GetField("selectionBorder", Flags).GetValue(card);
                card.Configure(null, false, false, true, null, () => { }); card.SetSelected(true);
                card.OnPointerEnter(null); var hoverColor = feedback.color;
                Assert.That(feedback.enabled, Is.True, "Empty slots still respond to hover.");
                card.SetDropPreview(true, true); var dropColor = feedback.color;
                Assert.That(dropColor, Is.Not.EqualTo(hoverColor));
                card.OnPointerExit(null);
                Assert.That(feedback.enabled, Is.True); Assert.That(feedback.color, Is.EqualTo(dropColor));
                card.SetDropPreview(true, false); Assert.That(feedback.color, Is.Not.EqualTo(dropColor));
                card.SetDropPreview(false, false);
                Assert.That(feedback.enabled, Is.False); Assert.That(selection.enabled, Is.True);
            }
            finally { PrefabUtility.UnloadPrefabContents(node); }
        }

        [Test]
        public void CombatShortcutsPreserveEmptySlotsAndStopAfterZero()
        {
            var root = PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CombatActionPanel.prefab");
            try
            {
                var character = new CharacterInstanceFactory().Create(AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/ScriptableObjects/Characters/CD_Jerzy.asset"));
                var session = new GameSession(GameModeState.Exploration);
                session.TryRegisterCharacter(character); session.TryAddCharacterToActiveParty(character.CharacterId);
                var system = new SkillLoadoutSystem();
                var first = character.ActiveSkillLoadout.ActiveSkillSlotIds[1];
                var last = character.ActiveSkillLoadout.ActiveSkillSlotIds[4];
                Assert.That(system.TryRemoveSkill(session, character.CharacterId, first), Is.True);
                Assert.That(system.TryRemoveSkill(session, character.CharacterId, last), Is.True);
                Assert.That(system.TrySetCapacityModifier(session, character.CharacterId, "test.hotkeys", 9), Is.True);
                Assert.That(system.TryAddSkillAtSlot(session, character.CharacterId, last, 9), Is.True);
                Assert.That(system.TryAddSkillAtSlot(session, character.CharacterId, first, 13), Is.True);
                var bar = root.GetComponentInChildren<CombatSkillBarView>(true);
                Call(bar, "RebuildSlots", new Godmist.Runtime.Combat.BattleUnit { Character = character }, character.GetActiveSkills(), "test");
                var keys = (Dictionary<int, CombatSkillSlotView>)Field(bar, "shortcutSlots");
                Assert.That(keys.Keys, Is.EquivalentTo(new[] { 0, 2, 3, 9 }), "An empty slot must not shift later shortcuts; bonus slots beyond 0 have no numeric key.");
                var cards = ((IEnumerable<CombatSkillSlotView>)Field(bar, "spawnedSlots")).ToArray();
                var labels = cards.Select(card => ((TMP_Text)new SerializedObject(card).FindProperty("shortcutText").objectReferenceValue).text).ToArray();
                Assert.That(labels, Is.EqualTo(new[] { "1", "3", "4", "0", "" }));
                Assert.That(keys[9], Is.SameAs(cards[3]), "0 must activate the skill in loadout slot 10.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void KnifeCost_StatesMeleeExceptionOnlyWhenAuthored(bool freeInMelee)
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            try
            {
                SetSkill(skill, "ActionCost", 70); SetSkill(skill, "KnifeCost", 1); SetSkill(skill, "FreeKnivesInMelee", freeInMelee);
                var text = UIWorkspaceTooltipView.BuildLoadoutSkillCost(skill, null);
                Assert.That(text, Does.Contain("70% AP")); Assert.That(text, Does.Contain("Sztylety: 1"));
                Assert.That(text.Contains("W zwarciu: bez zużycia"), Is.EqualTo(freeInMelee));
            }
            finally { Object.DestroyImmediate(skill); }
        }

        [Test]
        public void ToggleCost_ShowsMaximumOfFlatAndPercentUpkeep()
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            try
            {
                SetSkill(skill, "IsToggle", true); SetSkill(skill, "ToggleUpkeepFlatResourceCost", 8); SetSkill(skill, "ToggleUpkeepMaxResourcePercent", .1f);
                Assert.That(UIWorkspaceTooltipView.BuildLoadoutSkillCost(skill, null), Does.Contain("max(8, 10% maks.)"));
            }
            finally { Object.DestroyImmediate(skill); }
        }

        [UnityTest]
        public IEnumerator SelectionAssignmentReplacementDragAndBonusCapacity_UseNativePrefab()
        {
            yield return new EnterPlayMode();
            var objects = new List<Object>();
            try
            {
                var root = new GameObject("Workspace", typeof(RectTransform), typeof(Canvas)); objects.Add(root);
                var eventsObject = new GameObject("Loadout events", typeof(UnityEngine.EventSystems.EventSystem)); objects.Add(eventsObject);
                var events = eventsObject.GetComponent<UnityEngine.EventSystems.EventSystem>();
                UnityEngine.EventSystems.EventSystem.current = events;
                ((RectTransform)root.transform).sizeDelta = new Vector2(1920, 1080); root.AddComponent<UIWorkspaceWindowManager>();
                var tooltip = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_Tooltip.prefab"), root.transform).GetComponent<UIWorkspaceTooltipView>();
                var node = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_SkillLoadoutWindow Variant.prefab"), root.transform);
                var view = node.GetComponent<SkillLoadoutModalView>();
                var so = new SerializedObject(view); so.FindProperty("tooltipView").objectReferenceValue = tooltip; so.ApplyModifiedPropertiesWithoutUndo();
                var character = new CharacterInstanceFactory().Create(AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/ScriptableObjects/Characters/CD_Jerzy.asset"));
                var session = new GameSession(GameModeState.Exploration); Assert.That(session.TryRegisterCharacter(character), Is.True); Assert.That(session.TryAddCharacterToActiveParty(character.CharacterId), Is.True);
                var system = new SkillLoadoutSystem(); var basic = character.GetBasicAttackSkill().SkillId; var known = character.GetKnownSkills(); var chosen = known.Last();
                Assert.That(system.TryRemoveSkill(session, character.CharacterId, chosen.SkillId), Is.True);
                view.Open(session, character.CharacterId);
                Assert.That(CardLabel(((Button)Field(view, "basicSlotCard")).GetComponent<SkillLoadoutCardView>()), Is.EqualTo("1"));
                Assert.That(((IList)Field(view, "activeCards")).Cast<object>().Select(CardLabel), Is.EqualTo(new[] { "2", "3", "4", "5" }));
                var slots = character.ActiveSkillLoadout.ActiveSkillSlotIds.ToArray();
                Call(view, "SelectSkill", chosen.SkillId);
                Assert.That(character.ActiveSkillLoadout.ActiveSkillSlotIds, Is.EqualTo(slots), "LPM selection must not change the loadout.");
                Assert.That(((IList)Field(view, "knownCards")).Count, Is.EqualTo(known.Count), "Equipped skills remain in the library.");
                Assert.That(((TMP_Text)Field(view, "detailsTitle")).text, Is.EqualTo(chosen.DisplayName));
                Assert.That(((TMP_Text)Field(view, "detailsBody")).text, Does.Contain("• ")); Assert.That(((TMP_Text)Field(view, "detailsCost")).text, Does.Contain("W zwarciu: bez zużycia"));
                ((Button)Field(view, "assignButton")).onClick.Invoke();
                Assert.That(character.ActiveSkillLoadout.ActiveSkillSlotIds[4], Is.EqualTo(chosen.SkillId));
                var focusedCard = ((IList)Field(view, "activeCards"))[3] as SkillLoadoutCardView;
                events.SetSelectedGameObject(focusedCard.gameObject);
                ((Button)Field(view, "removeButton")).onClick.Invoke();
                Assert.That(character.ActiveSkillLoadout.ActiveSkillSlotIds[4], Is.Null.Or.Empty);
                Assert.That(ReferenceEquals(events.currentSelectedGameObject, null), Is.True,
                    "The focused card must be deselected before its GameObject is destroyed.");
                yield return null;
                Assert.That(UIWorkspaceInputRouter.HasFocusedContextMenu(), Is.False);
                Assert.That(UIWorkspaceInputRouter.HasFocusedTextInput(), Is.False);
                var libraryCard = ((IList)Field(view, "knownCards"))[0] as SkillLoadoutCardView;
                events.SetSelectedGameObject(libraryCard.gameObject);
                Call(view, "Refresh");
                Assert.That(ReferenceEquals(events.currentSelectedGameObject, null), Is.True,
                    "Rebuilding the library must also clear focus on removed cards.");
                var closeButton = (Button)Field(view, "closeButton");
                events.SetSelectedGameObject(closeButton.gameObject);
                Call(view, "Refresh");
                Assert.That(events.currentSelectedGameObject, Is.SameAs(closeButton.gameObject),
                    "Rebuilding cards must preserve focus outside their subtree.");
                var displaced = character.ActiveSkillLoadout.ActiveSkillSlotIds[1];
                Call(view, "SelectActiveSlot", 1, displaced); Call(view, "SelectSkill", chosen.SkillId);
                Assert.That(((TMP_Text)Field(view, "assignButtonText")).text, Is.EqualTo("Zastąp slot 2"));
                ((Button)Field(view, "assignButton")).onClick.Invoke();
                Assert.That(character.ActiveSkillLoadout.ActiveSkillSlotIds[1], Is.EqualTo(chosen.SkillId));
                Call(view, "BeginDrag", chosen.SkillId, true, null); Call(view, "DropOnActiveSlot", 4, null);
                Assert.That(character.ActiveSkillLoadout.ActiveSkillSlotIds[1], Is.Null.Or.Empty); Assert.That(character.ActiveSkillLoadout.ActiveSkillSlotIds[4], Is.EqualTo(chosen.SkillId));
                Call(view, "BeginDrag", chosen.SkillId, true, null); Call(view, "DropOnAvailableArea", (object)null);
                Assert.That(character.ActiveSkillLoadout.ActiveSkillSlotIds[4], Is.Null.Or.Empty);
                Call(view, "SelectSkill", basic); Call(view, "RemoveSelectedSkill"); Call(view, "AssignSelectedSkill");
                Assert.That(character.ActiveSkillLoadout.ActiveSkillSlotIds[0], Is.EqualTo(basic));
                Assert.That(((Button)Field(view, "removeButton")).gameObject.activeSelf, Is.False);
                Assert.That(system.TrySetCapacityModifier(session, character.CharacterId, "test.equipment-or-tree", 9), Is.True);
                session.NotifyCharacterProgressionChanged(character.CharacterId);
                Assert.That(((IList)Field(view, "activeCards")).Count, Is.EqualTo(13));
                Assert.That(CardLabel(((IList)Field(view, "activeCards"))[8]), Is.EqualTo("0"));
                Assert.That(CardLabel(((IList)Field(view, "activeCards"))[12]), Is.EqualTo("14"));
                Assert.That(((TMP_Text)Field(view, "capacityText")).text, Does.EndWith("/ 13"));
                yield return null; Canvas.ForceUpdateCanvases();
                var scroll = (ScrollRect)Field(view, "activeSlotsScrollRect"); LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content); Canvas.ForceUpdateCanvases();
                Assert.That(scroll.content.rect.width, Is.GreaterThan(scroll.viewport.rect.width));
                scroll.horizontalNormalizedPosition = 1; Assert.That(scroll.horizontalNormalizedPosition, Is.EqualTo(1).Within(.001));
                Call(view, "SelectActiveSlot", 13, "");
                Assert.That((string)Field(view, "selectedSkillId"), Is.Empty);
                var armed = ((IList)Field(view, "activeCards"))[12];
                Assert.That(((Image)typeof(SkillLoadoutCardView).GetField("selectionBorder", Flags).GetValue(armed)).enabled, Is.True);
                Call(view, "SelectSkill", chosen.SkillId); ((Button)Field(view, "assignButton")).onClick.Invoke();
                Assert.That(character.ActiveSkillLoadout.ActiveSkillSlotIds[13], Is.EqualTo(chosen.SkillId));
                Assert.That(character.ActiveSkillLoadout.ActiveSkillSlotIds[4], Is.Null.Or.Empty);
                slots = character.ActiveSkillLoadout.ActiveSkillSlotIds.ToArray();
                ((IList)Field(session, "combats")).Add(new Godmist.Runtime.Combat.CombatSession());
                Call(view, "SelectSkill", displaced); Call(view, "AssignSelectedSkill");
                Call(view, "SelectSkill", chosen.SkillId); Call(view, "RemoveSelectedSkill");
                Assert.That(character.ActiveSkillLoadout.ActiveSkillSlotIds, Is.EqualTo(slots), "Changes remain blocked even if a button callback arrives after combat starts.");
                Call(view, "Update"); Assert.That(view.IsOpen, Is.False, "Entering combat closes the loadout.");
                view.Close(); Assert.That(view.IsOpen, Is.False);
            }
            finally { foreach (var obj in objects) Object.DestroyImmediate(obj); }
            yield return new ExitPlayMode();
        }
    }
}

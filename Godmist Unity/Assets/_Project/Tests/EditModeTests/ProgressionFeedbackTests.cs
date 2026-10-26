using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Game;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public class ProgressionFeedbackTests
    {
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
        const string Ui = "Assets/_Project/Prefabs/UI/Workspace/";
        GameObject canvasObject, eventsObject, railObject;
        CharacterInfoPartyRailView rail;
        CharacterInfoPartyPortraitView portrait;
        CharacterInstance character;
        GameSession session;
        CharacterProgressionSystem progression;
        static T Get<T>(object owner, string field) => (T)owner.GetType().GetField(field, Fields).GetValue(owner);
        static void Set(object owner, string field, object value) => owner.GetType().GetField(field, Fields).SetValue(owner, value);
        static void Call(object owner, string method, params object[] args) => owner.GetType().GetMethod(method, Fields).Invoke(owner, args);
        CharacterInstance Hero(string asset = "CD_Jerzy.asset") => new CharacterInstanceFactory().Create(
            AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/ScriptableObjects/Characters/" + asset));
        CharacterProgressionBadgeView Badge => portrait.GetComponentInChildren<CharacterProgressionBadgeView>(true);
        [SetUp] public void Setup()
        {
            canvasObject = new GameObject("Progression canvas", typeof(RectTransform), typeof(Canvas), typeof(UIWorkspaceWindowManager));
            canvasObject.GetComponent<RectTransform>().sizeDelta = new Vector2(1280, 900);
            eventsObject = new GameObject("Progression events", typeof(EventSystem)); Call(eventsObject.GetComponent<EventSystem>(), "OnEnable");
            railObject = new GameObject("Progression rail", typeof(RectTransform)); railObject.transform.SetParent(canvasObject.transform, false);
            rail = railObject.AddComponent<CharacterInfoPartyRailView>();
            Set(rail, "workspaceManager", canvasObject.GetComponent<UIWorkspaceWindowManager>());
            Set(rail, "characterInfoWindowPrefab", AssetDatabase.LoadAssetAtPath<CharacterInfoWindowView>(Ui + "PF_UIW_CharacterInfoWindow Variant.prefab"));
            session = new GameSession(GameModeState.Exploration); character = Hero();
            session.TryRegisterCharacter(character); session.TryAddCharacterToActiveParty(character.CharacterId);
            Set(rail, "session", session);
            portrait = Object.Instantiate(AssetDatabase.LoadAssetAtPath<CharacterInfoPartyPortraitView>(Ui + "PF_UIW_PartyMember.prefab"), canvasObject.transform);
            portrait.gameObject.SetActive(true); portrait.Bind(character, null); portrait.ConfigureRail(rail, true, true);
            progression = new CharacterProgressionSystem();
        }
        [TearDown] public void Cleanup()
        {
            foreach (var row in canvasObject.GetComponentsInChildren<CharacterInfoPartyPortraitView>(true)) Call(row, "OnDisable");
            foreach (var view in canvasObject.GetComponentsInChildren<CombatFloatingFeedbackView>(true)) { Call(view, "OnDisable"); Call(view, "OnDestroy"); }
            foreach (var card in canvasObject.GetComponentsInChildren<CharacterInfoWindowView>(true)) Call(card, "OnDisable");
            foreach (var tree in canvasObject.GetComponentsInChildren<SkillTreeProgressionPanelView>(true)) Call(tree, "OnDisable");
            Call(eventsObject.GetComponent<EventSystem>(), "OnDisable");
            // The rail's production cleanup destroys owned windows at scene teardown.
            Set(rail, "developmentWindow", null);
            Object.DestroyImmediate(canvasObject); Object.DestroyImmediate(eventsObject);
        }
        [Test] public void AwardPublishesOnceAfterLevelStatsAndPointsAreApplied()
        {
            var calls = 0; var amount = 0; var levels = 0; var observedPoints = 0; var observedLevel = 0;
            character.ExperienceGained += (xp, gained) => { calls++; amount = xp; levels = gained; observedPoints = character.AvailableSkillPoints; observedLevel = character.Level; };
            var reward = progression.GetExperienceRequiredForNextLevel(1) + progression.GetExperienceRequiredForNextLevel(2);
            Assert.That(progression.AddExperience(character, reward), Is.EqualTo(2));
            Assert.That(calls, Is.EqualTo(1)); Assert.That(amount, Is.EqualTo(reward)); Assert.That(levels, Is.EqualTo(2));
            Assert.That(observedLevel, Is.EqualTo(3)); Assert.That(observedPoints, Is.EqualTo(3));
            Assert.That(Badge.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("+3"));
        }
        [TestCase(0, 1)] [TestCase(-10, 1)] [TestCase(50, 50)]
        public void ZeroNegativeAndMaximumLevelDoNotPublishPhantomExperience(int amount, int level)
        {
            character.Level = level; var calls = 0; character.ExperienceGained += (_, _) => calls++;
            progression.AddExperience(character, amount); Assert.That(calls, Is.Zero);
            Call(portrait, "LateUpdate"); Assert.That(canvasObject.GetComponentsInChildren<CombatFloatingFeedbackView>(true), Is.Empty);
        }
        [Test] public void ExperienceEventIsRuntimeOnlyAndBindingExistingProgressDoesNotAnnounceIt()
        {
            var field = typeof(CharacterInstance).GetField("ExperienceGained", Fields);
            Assert.That(field.GetCustomAttribute<NonSerializedAttribute>(), Is.Not.Null);
            character.Level = 10; character.AvailableSkillPoints = 8;
            portrait.Bind(character, null); portrait.RefreshPresentation(null, false, false); Call(portrait, "LateUpdate");
            Assert.That(Badge.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("+8"));
            Assert.That(canvasObject.GetComponentsInChildren<CombatFloatingFeedbackView>(true), Is.Empty);
        }
        [Test] public void SameFrameAwardsCoalesceAndPlainXpDoesNotShowLevelMessage()
        {
            progression.AddExperience(character, 60); progression.AddExperience(character, 40);
            Call(portrait, "LateUpdate");
            var views = canvasObject.GetComponentsInChildren<CombatFloatingFeedbackView>(true);
            Assert.That(views.Length, Is.EqualTo(1)); Assert.That(Get<TMP_Text>(views[0], "label").text, Is.EqualTo("+100 XP"));
            Assert.That(Get<CanvasGroup>(views[0], "canvasGroup").blocksRaycasts, Is.False);
            Call(portrait, "LateUpdate"); Assert.That(canvasObject.GetComponentsInChildren<CombatFloatingFeedbackView>(true).Length, Is.EqualTo(1));
        }
        [Test] public void RebuildingPartyRailDoesNotDiscardAlreadyAwardedExperience()
        {
            Get<System.Collections.Generic.List<CharacterInfoPartyPortraitView>>(rail, "portraits").Add(portrait);
            progression.AddExperience(character, 25); Call(rail, "Rebuild");
            var views = canvasObject.GetComponentsInChildren<CombatFloatingFeedbackView>(true);
            Assert.That(views.Length, Is.EqualTo(1)); Assert.That(Get<TMP_Text>(views[0], "label").text, Is.EqualTo("+25 XP"));
            Call(portrait, "LateUpdate"); Assert.That(canvasObject.GetComponentsInChildren<CombatFloatingFeedbackView>(true).Length, Is.EqualTo(1));
        }
        [Test] public void MultilevelAwardUsesOnePopupWithXpAndLevelCount()
        {
            var xp = progression.GetExperienceRequiredForNextLevel(1) + progression.GetExperienceRequiredForNextLevel(2);
            progression.AddExperience(character, xp); Call(portrait, "LateUpdate");
            var view = canvasObject.GetComponentInChildren<CombatFloatingFeedbackView>();
            Assert.That(Get<TMP_Text>(view, "label").text, Is.EqualTo($"+{xp} XP\nZdobyto poziom! ×2"));
        }
        [Test] public void RebindAndDisableUnsubscribeOldCharacterAndClearPendingPopup()
        {
            progression.AddExperience(character, 30);
            var next = Hero("CD_Mage.asset"); session.TryRegisterCharacter(next);
            portrait.Bind(next, null); progression.AddExperience(character, 30); Call(portrait, "LateUpdate");
            Assert.That(canvasObject.GetComponentsInChildren<CombatFloatingFeedbackView>(true), Is.Empty);
            Call(portrait, "OnDisable"); progression.AddExperience(next, 20); Call(portrait, "OnEnable"); Call(portrait, "LateUpdate");
            Assert.That(canvasObject.GetComponentsInChildren<CombatFloatingFeedbackView>(true), Is.Empty);
            progression.AddExperience(next, 15); Call(portrait, "LateUpdate");
            Assert.That(Get<TMP_Text>(canvasObject.GetComponentInChildren<CombatFloatingFeedbackView>(), "label").text, Is.EqualTo("+15 XP"));
        }
        [Test] public void BadgeReflectsActualPointsAndDisappearsAfterInvestmentWithoutChangingPortraitLayout()
        {
            var rect = Get<Image>(portrait, "portrait").rectTransform; var size = rect.sizeDelta; var position = rect.anchoredPosition;
            character.AvailableSkillPoints = 12; portrait.RefreshPresentation(null, false, false);
            Assert.That(Badge.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("+12"));
            Assert.That(Badge.GetComponent<Button>().interactable, Is.True);
            character.AvailableSkillPoints = 0; portrait.RefreshPresentation(null, false, false);
            Assert.That(Badge.gameObject.activeSelf, Is.False);
            Assert.That(rect.sizeDelta, Is.EqualTo(size)); Assert.That(rect.anchoredPosition, Is.EqualTo(position));
        }
        [Test] public void BadgeClickOpensCorrectDevelopmentDirectlyAndReusesWindow()
        {
            character.AvailableSkillPoints = 2; portrait.RefreshPresentation(null, false, false);
            Badge.GetComponent<Button>().onClick.Invoke();
            var tree = Get<SkillTreeProgressionPanelView>(rail, "developmentWindow");
            Assert.That(tree, Is.Not.Null); Assert.That(tree.IsOpen, Is.True);
            Assert.That(Get<CharacterInstance>(tree, "character"), Is.SameAs(character));
            Assert.That(canvasObject.GetComponentsInChildren<CharacterInfoWindowView>(true), Is.Empty, "The badge should open development directly, without an extra equipment card.");
            Badge.GetComponent<Button>().onClick.Invoke(); Assert.That(Get<SkillTreeProgressionPanelView>(rail, "developmentWindow"), Is.SameAs(tree));
            var other = Hero("CD_Mage.asset"); other.AvailableSkillPoints = 1; session.TryRegisterCharacter(other);
            portrait.Bind(other, null); portrait.ConfigureRail(rail, true, true); Badge.GetComponent<Button>().onClick.Invoke();
            Assert.That(Get<CharacterInstance>(tree, "character"), Is.SameAs(other));
        }
        [Test] public void CombatAndSpentPointsPreventBadgeActionEvenWithStaleButtonClick()
        {
            character.AvailableSkillPoints = 2; portrait.RefreshPresentation(null, false, false);
            var combat = new CombatSession { CombatId = "pt08-combat", IsSeamless = true }; combat.Units.Add(new BattleUnit { Character = character, IsPlayerSide = true });
            Assert.That(session.TrySetActiveCombat(combat), Is.True); portrait.RefreshPresentation(null, false, false);
            Assert.That(Badge.gameObject.activeSelf, Is.True); Assert.That(Badge.GetComponent<Button>().interactable, Is.False);
            Badge.GetComponent<Button>().onClick.Invoke(); Assert.That(Get<SkillTreeProgressionPanelView>(rail, "developmentWindow"), Is.Null);
            session.TryRemoveCombat(combat); portrait.RefreshPresentation(null, false, false); character.AvailableSkillPoints = 0;
            Badge.GetComponent<Button>().onClick.Invoke(); Assert.That(Get<SkillTreeProgressionPanelView>(rail, "developmentWindow"), Is.Null);
        }
        [Test] public void CardUpdatesXpAndDevelopmentCountOnRealAwardAndPointChange()
        {
            var card = Object.Instantiate(AssetDatabase.LoadAssetAtPath<CharacterInfoWindowView>(Ui + "PF_UIW_CharacterInfoWindow Variant.prefab"), canvasObject.transform);
            card.gameObject.SetActive(true); Call(card.GetComponent<UIWorkspaceWindow>(), "Awake"); Call(card, "Awake"); card.Initialize(session, character.CharacterId);
            progression.AddExperience(character, progression.GetExperienceRequiredForNextLevel(1));
            Assert.That(Get<Button>(card, "skillTreeButton").GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Rozwój (+2)"));
            Assert.That(Get<TMP_Text>(card, "progressionText").text, Does.Contain("Poziom 2"));
            character.AvailableSkillPoints = 1; session.NotifyCharacterProgressionChanged(character.CharacterId);
            Assert.That(Get<Button>(card, "skillTreeButton").GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Rozwój (+1)"));
            character.AvailableSkillPoints = 0; Call(card, "LateUpdate");
            Assert.That(Get<Button>(card, "skillTreeButton").GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Rozwój"));
        }
        [Test] public void VictoryAwardTriggersFeedbackOnceAndCannotDuplicateReward()
        {
            var combat = new CombatSession { Outcome = CombatOutcomeState.Victory };
            combat.Rewards.EnemyRewards.Add(new CombatEnemyReward { EnemyInstanceId = "enemy", ExperienceLevel = 1, ExperiencePerActiveCharacter = 60 });
            combat.TryMarkEnemyDefeated("enemy"); var rewards = new CombatRewardSystem();
            Assert.That(rewards.TryGrantVictoryRewards(session, combat), Is.True);
            Assert.That(rewards.TryGrantVictoryRewards(session, combat), Is.False); Call(portrait, "LateUpdate");
            var views = canvasObject.GetComponentsInChildren<CombatFloatingFeedbackView>(true);
            Assert.That(views.Length, Is.EqualTo(1)); Assert.That(Get<TMP_Text>(views[0], "label").text, Is.EqualTo("+120 XP"));
        }
        [Test] public void FeedbackExpiresAndReleasesItsMaterialWithoutChangingSharedFont()
        {
            progression.AddExperience(character, 50); Call(portrait, "LateUpdate");
            var view = canvasObject.GetComponentInChildren<CombatFloatingFeedbackView>(); var label = Get<TMP_Text>(view, "label");
            var shared = label.font.material; Assert.That(label.fontSharedMaterial, Is.Not.SameAs(shared));
            Call(view, "OnDestroy"); Set(view, "startTime", Time.unscaledTime - 20); Call(view, "Update");
            Assert.That(view == null, Is.True); Assert.That(shared, Is.Not.Null);
        }
        [Test] public void RenderBadgeAndLevelFeedbackWithGodmistTypography()
        {
            var rect = (RectTransform)portrait.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(-400, 0);
            progression.AddExperience(character, progression.GetExperienceRequiredForNextLevel(1)); Call(portrait, "LateUpdate");
            var view = canvasObject.GetComponentInChildren<CombatFloatingFeedbackView>(); Set(view, "startTime", Time.unscaledTime - .5f); Call(view, "Update");
            var cameraObject = new GameObject("Progression render camera", typeof(Camera)); var camera = cameraObject.GetComponent<Camera>();
            camera.enabled = false; camera.orthographic = true; camera.transform.position = new Vector3(0, 0, -10);
            camera.cullingMask = 1 << 31; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.035f, .035f, .04f);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            var target = new RenderTexture(1280, 900, 24); var pixels = new Texture2D(1280, 900, TextureFormat.RGBA32, false); var previous = RenderTexture.active;
            try
            {
                foreach (var child in canvasObject.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
                target.Create(); camera.targetTexture = target; Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1280, 900), 0, 0); pixels.Apply();
                var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../LocalHandoffs/progression-feedback-pass-20261015")); Directory.CreateDirectory(output);
                File.WriteAllBytes(Path.Combine(output, "progression-feedback.png"), pixels.EncodeToPNG());
                Assert.That(pixels.GetPixels32().Count(c => c.r > 100 && c.g > 80), Is.GreaterThan(1000));
            }
            finally { camera.targetTexture = null; RenderTexture.active = previous; target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(pixels); Object.DestroyImmediate(cameraObject); }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Skills;
using Godmist.Editor;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Views.Combat;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public sealed class SkillCastBannerTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        const string Output = "../LocalHandoffs/dungeon-combat-polish-20261025";
        GameObject root;
        CombatHudView hud;
        CombatSandboxBootstrap host;
        CombatExecutionDriver driver;
        CombatFeedbackBus bus;
        CombatSession session;
        BattleUnit actor;
        static T Get<T>(object o, string field) => (T)o.GetType().GetField(field, Flags).GetValue(o);
        static void Set(object o, string field, object value) => o.GetType().GetField(field, Flags).SetValue(o, value);
        static void Call(object o, string method, params object[] args) => o.GetType().GetMethod(method, Flags).Invoke(o, args);
        static SkillData Skill(string name) => AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Skills/SD_" + name + ".asset");
        GameObject Banner => Get<GameObject>(hud, "skillCastBanner");
        Image Icon => Get<Image>(hud, "skillCastIcon");
        TextMeshProUGUI Text => Get<TextMeshProUGUI>(hud, "skillCastText");
        CanvasGroup Opacity => Get<CanvasGroup>(hud, "skillCastOpacity");
        CombatFeedbackEvent Cast(SkillData skill, BattleUnit caster = null) => new(caster ?? actor, CombatFeedbackType.SkillUsed,
            effectName: skill.DisplayName, skillPresentation: new CombatSkillPresentation(caster ?? actor, skill));
        [SetUp]
        public void Before()
        {
            root = new GameObject("Banner fixture", typeof(RectTransform)); root.SetActive(false);
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(1280, 720);
            var hostRoot = new GameObject("Combat host"); hostRoot.SetActive(false); hostRoot.transform.SetParent(root.transform);
            host = hostRoot.AddComponent<CombatSandboxBootstrap>();
            actor = new BattleUnit { IsPlayerSide = true, Character = new CharacterInstanceFactory().Create(
                AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/ScriptableObjects/Characters/CD_Mage.asset")) };
            session = new CombatSession { ActiveUnit = actor }; session.Units.Add(actor);
            driver = new CombatExecutionDriver(host, null, null, null, null, null, null, 8, 6, 0, 0, 0, null, null, new(), null, null, null);
            typeof(CombatExecutionDriver).GetProperty("Session").SetValue(driver, session);
            bus = new CombatFeedbackBus(); Set(driver, "feedbackBus", bus); Set(host, "focused", driver);
            Get<Dictionary<CombatSession, CombatExecutionDriver>>(host, "drivers").Add(session, driver);
            hud = Object.Instantiate(AssetDatabase.LoadAssetAtPath<CombatHudView>(SkillCastBannerSetup.HudPath), root.transform);
            Set(hud, "sandboxBootstrap", host); hud.RefreshSkillFeedbackBinding();
        }
        [TearDown]
        public void After()
        {
            if (hud != null) Call(hud, "OnDisable");
            if (host != null) { Get<Dictionary<CombatSession, CombatExecutionDriver>>(host, "drivers").Clear(); Set(host, "focused", null); }
            driver?.Dispose(); if (root != null) Object.DestroyImmediate(root);
        }
        [Test, Explicit("Apply approved banner prefab through Editor API.")]
        public void ApplyContent() => SkillCastBannerSetup.Apply();
        [Test]
        public void AuthoredStyleUsesExistingArtworkFontAndNonBlockingIconLayout()
        {
            var palette = AssetDatabase.LoadAssetAtPath<UIWorkspaceStylePalette>(SkillCastBannerSetup.PalettePath);
            Assert.That(Banner.activeSelf, Is.False); Assert.That(Opacity.alpha, Is.Zero);
            Assert.That(Opacity.blocksRaycasts, Is.False); Assert.That(Opacity.interactable, Is.False);
            Assert.That(Banner.GetComponent<Image>().sprite, Is.SameAs(palette.GetArtwork("surface_solid")));
            Assert.That(Banner.transform.Find("BannerFrame").GetComponent<Image>().sprite, Is.SameAs(palette.GetArtwork("tooltip_frame")));
            Assert.That(Text.font, Is.SameAs(palette.Font)); Assert.That(Text.richText, Is.False);
            Assert.That(Get<float>(hud, "skillCastDuration"), Is.EqualTo(1.4f));
            foreach (var graphic in Banner.GetComponentsInChildren<Graphic>(true)) Assert.That(graphic.raycastTarget, Is.False);
        }
        [Test]
        public void CastShowsSkillIconAndNameAndReplacingDuringFadeRestartsFullOpacity()
        {
            var first = Skill("MageFireball"); var second = Skill("MageShield"); bus.Publish(Cast(first));
            Assert.That(Banner.activeSelf, Is.True); Assert.That(Text.text, Is.EqualTo(first.DisplayName)); Assert.That(Icon.sprite, Is.SameAs(first.Icon));
            var oldEnd = Time.unscaledTime + .1f; Set(hud, "skillCastExpiresAt", oldEnd);
            Call(hud, "TickSkillCast", oldEnd - .125f); Assert.That(Opacity.alpha, Is.EqualTo(.5f).Within(.001));
            bus.Publish(Cast(second)); Assert.That(Text.text, Is.EqualTo(second.DisplayName)); Assert.That(Icon.sprite, Is.SameAs(second.Icon));
            Assert.That(Opacity.alpha, Is.EqualTo(1)); Assert.That(Get<float>(hud, "skillCastExpiresAt"), Is.GreaterThan(oldEnd));
            Call(hud, "TickSkillCast", oldEnd); Assert.That(Banner.activeSelf, Is.True);
            Call(hud, "TickSkillCast", Get<float>(hud, "skillCastExpiresAt"));
            Assert.That(Banner.activeSelf, Is.False); Assert.That(Opacity.alpha, Is.Zero); Assert.That(Icon.sprite, Is.Null); Assert.That(Text.text, Is.Empty);
        }
        [Test]
        public void BannerFitsContentAndUsesLargerFontWithoutFixedEmptyWidth()
        {
            bus.Publish(new CombatFeedbackEvent(actor, CombatFeedbackType.SkillUsed, effectName: "Cios"));
            var shortWidth=((RectTransform)Banner.transform).rect.width;
            bus.Publish(new CombatFeedbackEvent(actor, CombatFeedbackType.SkillUsed, effectName: "Wycieńczające Zaklęcia"));
            Assert.That(((RectTransform)Banner.transform).rect.width, Is.GreaterThan(shortWidth));
            Assert.That(Text.fontSizeMax,Is.EqualTo(28));
        }
        [Test]
        public void LegacyNameOnlyEventClearsPreviousIconWithoutAPlaceholder()
        {
            bus.Publish(Cast(Skill("MageFireball")));
            bus.Publish(new CombatFeedbackEvent(actor, CombatFeedbackType.SkillUsed, effectName: "Kopnięcie"));
            Assert.That(Text.text, Is.EqualTo("Kopnięcie")); Assert.That(Icon.gameObject.activeSelf, Is.False); Assert.That(Icon.sprite, Is.Null);
        }
        [TestCase(true)] [TestCase(false)]
        public void InvisibleAndForeignUnitsCannotRevealNames(bool invisible)
        {
            var enemy = new BattleUnit { Character = actor.Character, IsPlayerSide = false };
            if (invisible)
            {
                session.Units.Add(enemy);
                enemy.Character.PassiveEffects.Add(new PassiveEffectRuntime { EffectId = Godmist.Core.Constants.StatusEffectIds.Invisibility,
                    Lifetime = PassiveEffectLifetime.Timed, RemainingDuration = 2 });
            }
            bus.Publish(Cast(Skill("MageFireball"), enemy)); Assert.That(Banner.activeSelf, Is.False); Assert.That(Text.text, Is.Empty);
        }
        [Test]
        public void BusSwitchSessionEndAndDisableClearPresentationAndSubscriptions()
        {
            bus.Publish(Cast(Skill("MageFireball"))); var previous = bus;
            var next = new CombatFeedbackBus(); Call(hud, "BindSkillFeedback", next);
            Assert.That(Banner.activeSelf, Is.False); previous.Publish(Cast(Skill("MageShield"))); Assert.That(Banner.activeSelf, Is.False);
            next.Publish(Cast(Skill("MageShield"))); Assert.That(Banner.activeSelf, Is.True);
            Call(hud, "OnDisable"); next.Publish(Cast(Skill("MageFireball"))); Assert.That(Banner.activeSelf, Is.False); Assert.That(Icon.sprite, Is.Null);
            hud.RefreshSkillFeedbackBinding(); bus.Publish(Cast(Skill("MageFireball"))); Assert.That(Banner.activeSelf, Is.True);
            Set(host, "focused", null); hud.RefreshSkillFeedbackBinding(); Assert.That(Banner.activeSelf, Is.False); Assert.That(Text.text, Is.Empty);
        }
        [Test, Explicit("Native render of authored banner; no scene edits.")]
        public void RenderExamples()
        {
            var cameraRoot = new GameObject("Banner camera", typeof(Camera));
            var canvasRoot = new GameObject("Preview canvas", typeof(RectTransform), typeof(Canvas));
            var camera = cameraRoot.GetComponent<Camera>(); camera.enabled = false; camera.orthographic = true;
            camera.transform.position = new Vector3(0, 0, -10); camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.045f, .05f, .045f);
            var canvas = canvasRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            var target = new RenderTexture(640, 280, 24); var pixels = new Texture2D(640, 280, TextureFormat.RGBA32, false); var previous = RenderTexture.active;
            try
            {
                var names = new[] { "MageFireball", "MageMagicFocus", "WarriorDischarge" };
                for (var n = 0; n < names.Length; n++)
                {
                    var skill = Skill(names[n]); Assert.That(skill, Is.Not.Null);
                    bus.Publish(Cast(skill));
                    var copy = Object.Instantiate(Banner, canvasRoot.transform); copy.SetActive(true);
                    var rect = (RectTransform)copy.transform; rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                    rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = new Vector2(0, 82 - 82 * n);
                    var group = copy.GetComponent<CanvasGroup>(); group.alpha = 1;
                    var icon = copy.transform.Find("SkillCastIcon").GetComponent<Image>(); icon.gameObject.SetActive(true); icon.sprite = skill.Icon;
                    var text = copy.transform.Find("SkillCastText").GetComponent<TextMeshProUGUI>(); text.text = skill.DisplayName;
                }
                foreach (var child in canvasRoot.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
                target.Create(); camera.targetTexture = target; Canvas.ForceUpdateCanvases();
                foreach (var text in canvasRoot.GetComponentsInChildren<TextMeshProUGUI>())
                {
                    text.ForceMeshUpdate(); Assert.That(text.isTextOverflowing, Is.False, text.text);
                }
                camera.Render(); RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 640, 280), 0, 0); pixels.Apply();
                Directory.CreateDirectory(Output); File.WriteAllBytes(Output + "/banner-examples.png", pixels.EncodeToPNG());
                Assert.That(new FileInfo(Output + "/banner-examples.png").Length, Is.GreaterThan(1000));
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous; target.Release();
                Object.DestroyImmediate(target); Object.DestroyImmediate(pixels); Object.DestroyImmediate(canvasRoot); Object.DestroyImmediate(cameraRoot);
            }
        }
    }
}

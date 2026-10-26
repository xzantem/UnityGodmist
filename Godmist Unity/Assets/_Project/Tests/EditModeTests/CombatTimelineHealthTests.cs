using System.Linq;
using Godmist.Core.Types;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Views.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Tests.EditMode
{
    public sealed class CombatTimelineHealthTests
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/UI/CombatHUD/PF_CombatTurnPortrait.prefab";

        [Test]
        public void ShieldTracksAbsorptionAndRebindingWithoutDependingOnRemainingHealth()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var view = root.GetComponent<CombatTurnPortraitView>();
                var health = (Slider)new SerializedObject(view).FindProperty("healthSlider").objectReferenceValue;
                var character = new CharacterInstance { CurrentHealth = 20,
                    Stats = new CharacterStats { MaxHealth = new Stat(100) }, ShieldPool = new ShieldPool() };
                var unit = new BattleUnit { Character = character, IsPlayerSide = true };
                character.ShieldPool.AddLayer(50);
                view.Bind(unit, false, occurrence: 2);
                var overlay = health.fillRect.parent.GetComponentsInChildren<Image>(true)
                    .Single(image => image.name == "ShieldOverlay");
                Assert.That(overlay.gameObject.activeSelf, Is.True);
                Assert.That(overlay.rectTransform.anchorMax.x, Is.EqualTo(.5f));
                Assert.That(overlay.color.a, Is.EqualTo(.35f));
                Assert.That(overlay.raycastTarget, Is.False);
                Assert.That(health.normalizedValue, Is.EqualTo(.2f).Within(.001f));
                Assert.That(CombatHealthPresentation.Format(character), Is.EqualTo("20/100 (+50)"));

                character.ShieldPool.AbsorbDamage(30);
                view.Bind(unit, false);
                Assert.That(overlay.rectTransform.anchorMax.x, Is.EqualTo(.2f));
                Assert.That(overlay.color.a, Is.EqualTo(1f));
                character.ShieldPool.AddLayer(180);
                view.Bind(unit, false);
                Assert.That(overlay.rectTransform.anchorMax.x, Is.EqualTo(1f));

                view.Bind(unit, false, known: false);
                Assert.That(overlay.gameObject.activeInHierarchy, Is.False);
                Assert.That(overlay.gameObject.activeSelf, Is.False);
                view.Bind(unit, false);
                Assert.That(overlay.gameObject.activeSelf, Is.True);
                character.ShieldPool.AbsorbDamage(1000);
                view.Bind(unit, false);
                Assert.That(overlay.gameObject.activeSelf, Is.False);
                Assert.That(CombatHealthPresentation.Format(character), Is.EqualTo("20/100"));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [TestCase("Assets/_Project/Prefabs/UI/CombatHUD/PF_CombatPartyMember.prefab")]
        [TestCase("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_PartyMember.prefab")]
        public void PartyHealthIncludesShieldInCombatAndExploration(string path)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<CombatPartyMemberView>();
                var state = new SerializedObject(view);
                var health = (Slider)state.FindProperty("healthSlider").objectReferenceValue;
                var text = (TMPro.TMP_Text)state.FindProperty("healthValueText").objectReferenceValue;
                var character = new CharacterInstance { CurrentHealth = 70,
                    Stats = new CharacterStats { MaxHealth = new Stat(100) }, ShieldPool = new ShieldPool() };
                character.ShieldPool.AddLayer(25);
                view.Bind(new BattleUnit { Character = character, IsPlayerSide = true });
                Assert.That(text.text, Is.EqualTo("70/100 (+25)"));
                var overlay = health.fillRect.parent.GetComponentsInChildren<Image>(true)
                    .Single(image => image.name == "ShieldOverlay");
                Assert.That(overlay.rectTransform.anchorMax.x, Is.EqualTo(.25f));
                character.ShieldPool.AbsorbDamage(25);
                view.BindCharacter(character);
                Assert.That(text.text, Is.EqualTo("70/100"));
                Assert.That(overlay.gameObject.activeSelf, Is.False);
                view.BindCharacter(new CharacterInstance { CurrentHealth = 40,
                    Stats = new CharacterStats { MaxHealth = new Stat(100) } });
                Assert.That(overlay.gameObject.activeSelf, Is.False);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [Test]
        public void CharacterCardShowsShieldAndClearsItsVisualAfterAbsorption()
        {
            var root = PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CharacterInfoWindow Variant.prefab");
            try
            {
                var view = root.GetComponent<Godmist.Views.UI.Workspace.CharacterInfoWindowView>();
                const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var character = new CharacterInstance { CurrentHealth = 50,
                    Stats = new CharacterStats { MaxHealth = new Stat(100) }, ShieldPool = new ShieldPool() };
                character.ShieldPool.AddLayer(30);
                view.GetType().GetField("character", flags).SetValue(view, character);
                var refresh = view.GetType().GetMethod("RefreshVitals", flags);
                refresh.Invoke(view, null);
                var state = new SerializedObject(view);
                var text = (TMPro.TMP_Text)state.FindProperty("healthResourceText").objectReferenceValue;
                var fill = (Image)state.FindProperty("healthFill").objectReferenceValue;
                var overlay = fill.transform.parent.GetComponentsInChildren<Image>(true)
                    .Single(image => image.name == "ShieldOverlay");
                Assert.That(text.text, Does.Contain("50/100 (+30)"));
                Assert.That(overlay.rectTransform.anchorMax.x, Is.EqualTo(.3f));
                Assert.That(fill.fillAmount, Is.EqualTo(.5f));
                character.ShieldPool.AbsorbDamage(30);
                refresh.Invoke(view, null);
                Assert.That(overlay.gameObject.activeSelf, Is.False);
                Assert.That(text.text, Does.Not.Contain("(+"));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void HealthTracksDamageHealingAndRepeatedTurnsWithoutLeakingUnknownUnits(bool player)
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var view = root.GetComponent<CombatTurnPortraitView>();
                var slider = (Slider)new SerializedObject(view).FindProperty("healthSlider").objectReferenceValue;
                var unit = new BattleUnit { IsPlayerSide = player, Character = new CharacterInstance
                { CurrentHealth = 100, Stats = new CharacterStats { MaxHealth = new Stat(100) } } };
                var fill = slider.fillRect.GetComponent<Image>();
                view.Bind(unit, false);
                Assert.That(slider.gameObject.activeSelf, Is.True);
                Assert.That(slider.normalizedValue, Is.EqualTo(1));
                Assert.That(fill.color, Is.EqualTo(new Color(.7f, .12f, .1f)));
                unit.Character.CurrentHealth = 20;
                view.Bind(unit, false, occurrence: 2);
                Assert.That(slider.normalizedValue, Is.EqualTo(.2f).Within(.001f));
                Assert.That(fill.color, Is.EqualTo(new Color(1f, .4f, .23f, .35f)));
                view.Bind(unit, false, occurrence: 2, highlighted: true);
                Assert.That(fill.color.a, Is.EqualTo(1));
                unit.Character.CurrentHealth = 75;
                view.Bind(unit, false);
                Assert.That(slider.normalizedValue, Is.EqualTo(.75f).Within(.001f));
                Assert.That(fill.color, Is.EqualTo(new Color(.7f, .12f, .1f)));
                view.Bind(unit, false, known: false);
                Assert.That(slider.gameObject.activeSelf, Is.False);
                view.Bind(unit, false);
                Assert.That(slider.gameObject.activeSelf, Is.True);
                Assert.That(slider.normalizedValue, Is.EqualTo(.75f).Within(.001f));
                unit.Character.CurrentHealth = -1;
                view.Bind(unit, false);
                Assert.That(slider.value, Is.Zero);
                unit.Character.Stats.MaxHealth = new Stat(0);
                view.Bind(unit, false);
                Assert.That(slider.maxValue, Is.EqualTo(1));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [Test]
        public void HudReservesSpaceForEveryStripAboveItsOpaqueBottomBorder()
        {
            var root = PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CombatHud.prefab");
            try
            {
                var hud = root.GetComponent<CombatHudView>();
                var state = new SerializedObject(hud);
                var container = (RectTransform)state.FindProperty("turnOrderContainer").objectReferenceValue;
                var prefab = (CombatTurnPortraitView)state.FindProperty("turnOrderEntryPrefab").objectReferenceValue;
                var top = (RectTransform)root.transform.Find("TopBar");
                root.SetActive(true);
                top.gameObject.SetActive(true);
                for (var i = 0; i < 8; i++)
                {
                    var view = Object.Instantiate(prefab, container);
                    view.Bind(new BattleUnit { IsPlayerSide = true, Character = new CharacterInstance
                    { CurrentHealth = 50, Stats = new CharacterStats { MaxHealth = new Stat(100) } } }, i == 0);
                }
                LayoutRebuilder.ForceRebuildLayoutImmediate(top);
                Canvas.ForceUpdateCanvases();
                Assert.That(top.rect.height, Is.EqualTo(108));
                foreach (var view in container.GetComponentsInChildren<CombatTurnPortraitView>(true))
                {
                    var slider = (Slider)new SerializedObject(view).FindProperty("healthSlider").objectReferenceValue;
                    var corners = new Vector3[4];
                    ((RectTransform)slider.transform).GetWorldCorners(corners);
                    Assert.That(top.InverseTransformPoint(corners[0]).y - top.rect.yMin,
                        Is.GreaterThanOrEqualTo(18f - .01f), "The whole HP strip must clear the HUD border.");
                    Assert.That(((RectTransform)view.transform).rect.size, Is.EqualTo(new Vector2(68, 68)));
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [Test]
        public void StripFitsBelowSquarePortraitWithoutLabelsOrClickableGraphics()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var view = root.GetComponent<CombatTurnPortraitView>();
                var slider = (Slider)new SerializedObject(view).FindProperty("healthSlider").objectReferenceValue;
                Canvas.ForceUpdateCanvases();
                var rect = (RectTransform)slider.transform;
                Assert.That(((RectTransform)root.transform).rect.size, Is.EqualTo(new Vector2(68, 68)));
                Assert.That(rect.rect.size, Is.EqualTo(new Vector2(56, 6)));
                Assert.That(rect.anchoredPosition.y, Is.EqualTo(-2));
                Assert.That(rect.pivot.y, Is.EqualTo(1));
                Assert.That(slider.fillRect.rect.height, Is.EqualTo(3).Within(.01f));
                Assert.That(slider.interactable, Is.False);
                Assert.That(slider.navigation.mode, Is.EqualTo(Navigation.Mode.None));
                Assert.That(slider.GetComponentsInChildren<TMPro.TMP_Text>(true), Is.Empty);
                Assert.That(slider.transform.Find("GothicBarFrame").gameObject.activeSelf, Is.False);
                foreach (var image in slider.GetComponentsInChildren<Image>())
                {
                    Assert.That(image.raycastTarget, Is.False);
                }
                var vitals=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CombatVitals.prefab").GetComponent<CombatVitalsView>();
                var source=(Slider)new SerializedObject(vitals).FindProperty("healthSlider").objectReferenceValue;
                var background=slider.transform.Find("Background").GetComponent<Image>();
                var sourceBackground=source.transform.Find("Background").GetComponent<Image>();
                Assert.That(background.sprite, Is.SameAs(sourceBackground.sprite));
                Assert.That(background.type, Is.EqualTo(sourceBackground.type));
                Assert.That(background.color, Is.EqualTo(sourceBackground.color));
                Assert.That(background.pixelsPerUnitMultiplier, Is.EqualTo(sourceBackground.pixelsPerUnitMultiplier*2));
                Assert.That(slider.fillRect.GetComponent<Image>().sprite, Is.SameAs(source.fillRect.GetComponent<Image>().sprite));
                Assert.That(slider.fillRect.GetComponent<Image>().color, Is.EqualTo(source.fillRect.GetComponent<Image>().color));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}

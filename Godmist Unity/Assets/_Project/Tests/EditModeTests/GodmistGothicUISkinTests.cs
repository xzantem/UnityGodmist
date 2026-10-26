using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Items;
using Godmist.Runtime.Items;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Tests.EditMode
{
    public sealed class GodmistGothicUISkinTests
    {
        private const string Prefabs="Assets/_Project/Prefabs/UI/Workspace/";
        private static UIWorkspaceStylePalette Palette => AssetDatabase.LoadAssetAtPath<UIWorkspaceStylePalette>("Assets/_Project/ScriptableObjects/UI/Workspace/UIWorkspaceStylePalette.asset");

        [Test]
        public void UnskinnedPalettePreservesLegacyTintBehaviour()
        {
            var palette=ScriptableObject.CreateInstance<UIWorkspaceStylePalette>();
            var root=new GameObject("Legacy",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(Button),typeof(UIWorkspaceStyleApplier));
            try
            {
                root.GetComponent<UIWorkspaceStyleApplier>().ConfigureRuntime(palette,UIWorkspaceSurfaceStyle.Raised,UIWorkspaceAccentStyle.None,UIWorkspaceTextStyle.Body,UIWorkspaceAccentStyle.None,root.GetComponent<Image>(),nextSelectable:root.GetComponent<Button>());
                Assert.That(root.GetComponent<Image>().color,Is.EqualTo(palette.GetSurfaceColor(UIWorkspaceSurfaceStyle.Raised)));
                Assert.That(root.GetComponent<Button>().colors.highlightedColor,Is.EqualTo(palette.GetAccentColor(UIWorkspaceAccentStyle.Hover)));
            }
            finally {Object.DestroyImmediate(root);Object.DestroyImmediate(palette);}
        }

        [TestCase("PF_UIW_Button.prefab","button")]
        [TestCase("PF_UIW_ListRow.prefab","list_row")]
        [TestCase("PF_UIW_ItemSlot.prefab","icon_button")]
        public void SkinSurvivesStyleReapplyAndHasAllInteractiveStates(string file,string family)
        {
            var root=PrefabUtility.LoadPrefabContents(Prefabs+file);
            try
            {
                var button=root.GetComponent<Button>();var image=(Image)button.targetGraphic;
                root.GetComponent<UIWorkspaceStyleApplier>().Apply();
                Assert.That(image.sprite,Is.EqualTo(Palette.GetArtwork(family+"_normal")));
                Assert.That(image.color,Is.EqualTo(Color.white));
                Assert.That(button.transition,Is.EqualTo(Selectable.Transition.SpriteSwap));
                Assert.That(button.spriteState.highlightedSprite,Is.EqualTo(Palette.GetArtwork(family+"_hover")));
                Assert.That(button.spriteState.pressedSprite,Is.EqualTo(Palette.GetArtwork(family+"_pressed")));
                Assert.That(button.spriteState.selectedSprite,Is.EqualTo(Palette.GetArtwork(family+"_selected")));
                Assert.That(button.spriteState.disabledSprite,Is.EqualTo(Palette.GetArtwork(family+"_disabled")));
                Assert.That(image.type,Is.EqualTo(Image.Type.Sliced));
            }
            finally {PrefabUtility.UnloadPrefabContents(root);}
        }

        [Test]
        public void SemanticFrameCanReturnFromInvalidAndSelectedToNeutralWithoutDisablingButton()
        {
            var root=PrefabUtility.LoadPrefabContents(Prefabs+"PF_UIW_ItemSlot.prefab");
            try
            {
                var border=root.transform.Find("Border").GetComponent<Image>();var button=root.GetComponent<Button>();
                button.interactable=true;
                Palette.ApplyFrameAccent(border,UIWorkspaceAccentStyle.Negative);
                Assert.That(border.sprite.name,Is.EqualTo("slot_invalid"));Assert.That(border.enabled,Is.True);
                Palette.ApplyFrameAccent(border,UIWorkspaceAccentStyle.Selected);
                Assert.That(border.sprite.name,Is.EqualTo("slot_selected"));Assert.That(border.color,Is.EqualTo(Color.white));
                Palette.ApplyFrameAccent(border,UIWorkspaceAccentStyle.Neutral);
                Assert.That(border.sprite.name,Is.EqualTo("item_frame"));Assert.That(border.enabled,Is.False);
                Assert.That(button.interactable,Is.True);
            }
            finally {PrefabUtility.UnloadPrefabContents(root);}
        }

        [Test]
        public void InventoryLogicalSelectionUpdatesBaseSpriteIndependentlyOfPointerOverride()
        {
            var root=PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/UI/PF_InventoryItemButton.prefab");
            var definition=ScriptableObject.CreateInstance<ItemDefinition>();
            try
            {
                var data=new SerializedObject(definition);data.FindProperty("<Id>k__BackingField").stringValue="ui.skin.test.item";data.ApplyModifiedPropertiesWithoutUndo();
                var view=root.GetComponent<InventoryItemButtonView>();
                var image=new SerializedObject(view).FindProperty("backgroundImage").objectReferenceValue as Image;
                var item=new ItemInstance(definition);
                view.Bind(item,1,true,_=>{});
                Assert.That(image.sprite.name,Is.EqualTo("list_row_selected"));
                image.overrideSprite=Palette.GetArtwork("list_row_hover");
                view.Bind(item,1,false,_=>{});
                image.overrideSprite=null;
                Assert.That(image.sprite.name,Is.EqualTo("list_row_normal"));Assert.That(image.color,Is.EqualTo(Color.white));
            }
            finally {PrefabUtility.UnloadPrefabContents(root);Object.DestroyImmediate(definition);}
        }

        [Test]
        public void CraftingInputAndDropdownKeepTheirReferencesAndUseFocusedDisabledArtwork()
        {
            var root=PrefabUtility.LoadPrefabContents(Prefabs+"PF_UIW_BlacksmithCraftingWindow.prefab");
            try
            {
                var input=root.GetComponentInChildren<TMP_InputField>(true);
                var dropdown=root.GetComponentInChildren<TMP_Dropdown>(true);
                Assert.That(((Image)input.targetGraphic).sprite.name,Is.EqualTo("input_normal"));
                Assert.That(input.spriteState.selectedSprite.name,Is.EqualTo("input_focused"));
                Assert.That(input.spriteState.disabledSprite.name,Is.EqualTo("input_disabled"));
                Assert.That(input.textComponent,Is.Not.Null);
                Assert.That(((Image)dropdown.targetGraphic).sprite.name,Is.EqualTo("dropdown_normal"));
                Assert.That(dropdown.template.GetComponent<Image>().sprite.name,Is.EqualTo("dropdown_popup"));
                Assert.That(dropdown.spriteState.disabledSprite.name,Is.EqualTo("dropdown_disabled"));
                Assert.That(dropdown.captionText,Is.Not.Null);
                var toggle=dropdown.template.GetComponentInChildren<Toggle>(true);
                var wasOn=toggle.isOn;toggle.isOn=!wasOn;
                Assert.That(toggle.isOn,Is.EqualTo(!wasOn));
                Assert.That(((Image)toggle.graphic).sprite.name,Is.EqualTo("glyph_confirm"));
            }
            finally {PrefabUtility.UnloadPrefabContents(root);}
        }

        [Test]
        public void PartyLogicalSelectionKeepsWhiteTintAndReturnsToNormalArtwork()
        {
            var root=PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/UI/PF_PartyMemberButton.prefab");
            try
            {
                var view=root.GetComponent<PartyMemberButtonView>();
                var image=new SerializedObject(view).FindProperty("backgroundImage").objectReferenceValue as Image;
                view.Bind(null,true,_=>{});
                Assert.That(image.sprite.name,Is.EqualTo("list_row_selected"));
                view.Bind(null,false,_=>{});
                Assert.That(image.sprite.name,Is.EqualTo("list_row_normal"));
                Assert.That(image.color,Is.EqualTo(Color.white));
            }
            finally {PrefabUtility.UnloadPrefabContents(root);}
        }

        [Test]
        public void RuntimeResourceTintAndFractionalSliderValueRemainAuthoritative()
        {
            var root=PrefabUtility.LoadPrefabContents(Prefabs+"PF_UIW_CombatVitals.prefab");
            try
            {
                var slider=(Slider)new SerializedObject(root.GetComponent<CombatVitalsView>()).FindProperty("resourceSlider").objectReferenceValue;
                slider.minValue=0;slider.maxValue=100;slider.SetValueWithoutNotify(42.5f);
                CombatResourceColors.ApplyToFill(slider,ResourceType.Momentum);
                Assert.That(slider.fillRect.GetComponent<Image>().color,Is.EqualTo(CombatResourceColors.Get(ResourceType.Momentum)));
                Assert.That(slider.fillRect.GetComponent<Image>().sprite.name,Is.EqualTo("fill_runtime"));
                Assert.That(slider.normalizedValue,Is.EqualTo(.425f).Within(.00001f));
                Assert.That(slider.GetComponentsInChildren<UIWorkspaceProgressGlint>(true),Is.Empty);
            }
            finally {PrefabUtility.UnloadPrefabContents(root);}
        }

        [Test]
        public void GlintFollowsReversedFillAndHidesWhenProgressIsZero()
        {
            var root=new GameObject("Progress",typeof(RectTransform),typeof(Slider));
            var fill=new GameObject("Fill",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
            var glint=new GameObject("Glint",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(UIWorkspaceProgressGlint));
            try
            {
                ((RectTransform)root.transform).sizeDelta=new Vector2(100,20);
                fill.transform.SetParent(root.transform,false);var fillRect=(RectTransform)fill.transform;
                fillRect.anchorMin=Vector2.zero;fillRect.anchorMax=Vector2.one;fillRect.offsetMin=fillRect.offsetMax=Vector2.zero;
                glint.transform.SetParent(fill.transform,false);((RectTransform)glint.transform).sizeDelta=new Vector2(24,8);
                var slider=root.GetComponent<Slider>();slider.fillRect=fillRect;slider.direction=Slider.Direction.RightToLeft;slider.SetValueWithoutNotify(.5f);
                var image=glint.GetComponent<Image>();var follower=glint.GetComponent<UIWorkspaceProgressGlint>();follower.Configure(slider,image);
                Assert.That(image.rectTransform.anchorMin,Is.EqualTo(new Vector2(0,.5f)));Assert.That(image.enabled,Is.True);
                slider.SetValueWithoutNotify(0);follower.Refresh();Assert.That(image.enabled,Is.False);
                Assert.That(slider.value,Is.Zero);
            }
            finally {Object.DestroyImmediate(root);}
        }

        [Test]
        public void AllAuthoredUiPrefabsHaveNoMissingScripts()
        {
            foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/_Project/Prefabs/UI"}))
            {
                var path=AssetDatabase.GUIDToAssetPath(guid);var root=PrefabUtility.LoadPrefabContents(path);
                try {Assert.That(root.GetComponentsInChildren<MonoBehaviour>(true).All(behaviour=>behaviour!=null),Is.True,path);}
                finally {PrefabUtility.UnloadPrefabContents(root);}
            }
        }
    }
}

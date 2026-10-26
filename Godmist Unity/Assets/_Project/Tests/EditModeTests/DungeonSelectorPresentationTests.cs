using Godmist.Data.Characters;
using Godmist.Systems.Characters;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.World;
using System.IO;

namespace Godmist.Tests.EditMode
{
    public class DungeonSelectorPresentationTests
    {
        private static T Ref<T>(Object owner, string field) where T : Object =>
            new SerializedObject(owner).FindProperty(field).objectReferenceValue as T;

        [Test]
        public void EveryProductionOfferFitsBothLinesAtMaximumLevel()
        {
            GodmistMainMenuSetup.ConfigureDungeonOfferCaptions();
            var canvasObject = new GameObject("Offer caption bounds", typeof(Canvas));
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(GodmistMainMenuSetup.DungeonSelectorPath),canvasObject.transform);
            try
            {
                root.SetActive(true);
                var data = new SerializedObject(root.GetComponent<DungeonSelectorWindowView>());
                var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>("Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
                var definitions = catalog.DungeonDefinitions.Where(d=>d!=null && d.IsAvailableAsOffer && !d.IsStoryDungeon).ToArray();
                Assert.That(definitions.Length, Is.GreaterThanOrEqualTo(6));
                for (var i=0;i<data.FindProperty("offerLabels").arraySize;i++)
                {
                    var label=(TMP_Text)data.FindProperty("offerLabels").GetArrayElementAtIndex(i).objectReferenceValue;
                    label.transform.parent.gameObject.SetActive(true);
                    foreach (var definition in definitions)
                    foreach (var length in new[]{DungeonLength.Short,DungeonLength.Normal,DungeonLength.Long})
                    {
                        label.text=DungeonSelectorWindowView.OfferCaption(definition,length,50);
                        Canvas.ForceUpdateCanvases(); label.ForceMeshUpdate(true,true);
                        Assert.That(label.textInfo.lineCount,Is.EqualTo(2),label.text);
                        Assert.That(label.isTextOverflowing,Is.False,label.text);
                        Assert.That(label.isTextTruncated,Is.False,label.text);
                        Assert.That(label.textBounds.size.x,Is.LessThanOrEqualTo(label.rectTransform.rect.width+.1f),label.text);
                        Assert.That(label.textBounds.size.y,Is.LessThanOrEqualTo(label.rectTransform.rect.height+.1f),label.text);
                    }
                }
            }
            finally { Object.DestroyImmediate(canvasObject); }
        }

        [Test]
        public void RenderSixOffersWithLengthAndLevel()
        {
            var output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../LocalHandoffs/dungeon-entry-pass-20261016/selector.png"));
            var canvasObject=new GameObject("Expedition selector preview",typeof(Canvas));
            var cameraObject=new GameObject("UI camera",typeof(Camera));
            var target=new RenderTexture(1100,700,24);
            Texture2D capture=null;
            var previous=RenderTexture.active;
            try
            {
                var canvas=canvasObject.GetComponent<Canvas>(); canvas.renderMode=RenderMode.WorldSpace;
                ((RectTransform)canvas.transform).sizeDelta=new Vector2(1100,700);
                var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(GodmistMainMenuSetup.DungeonSelectorPath),canvas.transform);
                root.SetActive(true); ((RectTransform)root.transform).anchoredPosition=Vector2.zero;
                var data=new SerializedObject(root.GetComponent<DungeonSelectorWindowView>());
                var catalog=AssetDatabase.LoadAssetAtPath<GameContentCatalog>("Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
                var definitions=catalog.DungeonDefinitions.Where(d=>d!=null && d.IsAvailableAsOffer && !d.IsStoryDungeon).Take(6).ToArray();
                for(var i=0;i<6;i++)
                {
                    var label=(TMP_Text)data.FindProperty("offerLabels").GetArrayElementAtIndex(i).objectReferenceValue;
                    label.transform.parent.gameObject.SetActive(true);
                    label.text=DungeonSelectorWindowView.OfferCaption(definitions[i],(DungeonLength)(i%3+1),50);
                }
                Ref<TMP_Text>(root.GetComponent<DungeonSelectorWindowView>(),"selectionHeading").text="Katakumby";
                Ref<TMP_Text>(root.GetComponent<DungeonSelectorWindowView>(),"details").text="Poziom: 50\nDrużyna: 50 · różnica 0\nNormalna · Zestawy obozowe: 1 · Quest ukończenia ×2\n\nZagrożenie: 100%";
                var camera=cameraObject.GetComponent<Camera>(); camera.enabled=false;
                camera.transform.position=new Vector3(0,0,-1000); camera.orthographic=true; camera.orthographicSize=350;
                camera.nearClipPlane=.1f; camera.farClipPlane=2000; camera.targetTexture=target;
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active=target;
                capture=new Texture2D(1100,700,TextureFormat.RGBA32,false); capture.ReadPixels(new Rect(0,0,1100,700),0,0); capture.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(output)); File.WriteAllBytes(output,capture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active=previous;
                Object.DestroyImmediate(canvasObject); Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(target); if(capture!=null)Object.DestroyImmediate(capture);
            }
        }

        [Test]
        public void ExpeditionCardRefreshesNameLevelAndVitalsFromBoundCharacter()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_DungeonSelector.prefab");
            var template = Ref<CombatPartyMemberView>(prefab.GetComponent<DungeonSelectorWindowView>(), "partyTemplate");
            var card = Object.Instantiate(template);
            try
            {
                var definition = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(
                    "Assets/_Project/ScriptableObjects/Characters/CD_Jerzy.asset");
                var character = new CharacterInstanceFactory().Create(definition);
                card.BindCharacter(character, true);
                Assert.That(Ref<TMP_Text>(card, "characterNameText").text, Does.Contain(definition.DisplayName).And.Contain("Poz. 1"));
                character.Level = 7; character.CurrentHealth = 10;
                card.BindCharacter(character, false);
                Assert.That(Ref<TMP_Text>(card, "characterNameText").text, Does.Contain("Poz. 7"));
                Assert.That(Ref<TMP_Text>(card, "healthValueText").text, Does.StartWith("10/"));
                Assert.That(Ref<Slider>(card, "healthSlider").value, Is.EqualTo(10));
                Assert.That(Ref<Image>(card, "selectedMarker").enabled, Is.False);
            }
            finally { Object.DestroyImmediate(card.gameObject); }
        }

        [Test]
        public void SharedCombatCardStillBindsWithoutOptionalExpeditionCaption()
        {
            var template = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/UI/CombatHUD/PF_CombatPartyMember.prefab").GetComponent<CombatPartyMemberView>();
            var card = Object.Instantiate(template);
            try
            {
                Assert.That(Ref<TMP_Text>(card, "characterNameText"), Is.Null);
                var definition = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(
                    "Assets/_Project/ScriptableObjects/Characters/CD_Jerzy.asset");
                var character = new CharacterInstanceFactory().Create(definition);
                card.BindCharacter(character, true);
                Assert.That(card.gameObject.activeSelf, Is.True);
                Assert.That(Ref<Image>(card, "portrait").sprite, Is.EqualTo(definition.UiPortraitSprite));
                Assert.That(Ref<Image>(card, "selectedMarker").enabled, Is.True);
                card.BindCharacter(null);
                Assert.That(card.gameObject.activeSelf, Is.False);
            }
            finally { Object.DestroyImmediate(card.gameObject); }
        }
    }
}

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Configuration;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Views.Exploration;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public sealed class CombatDownedIndicatorTests
    {
        private readonly List<Object> owned = new();
        private static UIWorkspaceStylePalette Palette => AssetDatabase.LoadAssetAtPath<UIWorkspaceStylePalette>(
            "Assets/_Project/ScriptableObjects/UI/Workspace/UIWorkspaceStylePalette.asset");
        private static CharacterDefinition Hero => AssetDatabase.LoadAssetAtPath<CharacterDefinition>(
            "Assets/_Project/ScriptableObjects/Characters/CD_Jerzy.asset");
        private static BattleUnit Downed(int remaining = 3) => new()
        {
            Character = new CharacterInstanceFactory().Create(Hero, Hero.ClassData, 1),
            IsPlayerSide = true, DownedTurnsRemaining = remaining
        };
        private BattleUnit CreateDowned(int remaining = 3)
        {
            var unit = Downed(remaining);
            unit.Character.LifeState = CharacterLifeState.Downed;
            unit.Character.CurrentHealth = 0;
            return unit;
        }
        private GameObject Node(string name)
        {
            var node = new GameObject(name); owned.Add(node); return node;
        }
        [TearDown] public void Cleanup()
        {
            foreach (var obj in owned) if (obj != null) Object.DestroyImmediate(obj);
            owned.Clear(); GameDefinesRuntime.Configure(null);
        }
        private static void Tick(CombatDownedIndicatorView view) => typeof(CombatDownedIndicatorView)
            .GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
        private static Image[] Segments(CombatDownedIndicatorView view) => view.GetComponentsInChildren<Image>(true)
            .Where(i => i.name.StartsWith("Turn")).OrderBy(i => i.name).ToArray();

        [Test] public void SkullIsNativeGodmistUiSpriteAndPackedInTheSharedAtlas()
        {
            var sprite = Palette.GetArtwork("status_downed_skull"); Assert.That(sprite, Is.Not.Null);
            Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(32, 32)));
            Assert.That(sprite.pixelsPerUnit, Is.EqualTo(100));
            var path = AssetDatabase.GetAssetPath(sprite);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point)); Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            var atlas = AssetDatabase.LoadAssetAtPath<UnityEngine.U2D.SpriteAtlas>("Assets/_Project/UI/Sprites/GodmistGothic/GodmistGothicUI.spriteatlasv2");
            Assert.That(atlas.GetSprite("status_downed_skull"), Is.Not.Null);
        }

        [Test] public void AuthoredLimitAndDefaultBothUseThreeOwnTurns()
        {
            var definitions = AssetDatabase.LoadAssetAtPath<GameDefines>("Assets/_Project/Resources/GameDefines.asset");
            Assert.That(definitions.TryGet(GameDefineIds.DownedTurnLimit, out var entry), Is.True);
            Assert.That(entry.CurrentIntegerValue, Is.EqualTo(3)); Assert.That(entry.DefaultIntegerValue, Is.EqualTo(3));
            GameDefinesRuntime.Configure(definitions);
            Assert.That(BattleUnit.DownedTurnLimit, Is.EqualTo(3));
            GameDefinesRuntime.Configure(null); Assert.That(BattleUnit.DownedTurnLimit, Is.EqualTo(3));
        }

        [Test] public void ThreeTwoOneThenDeathMatchesTheRealTurnOwner()
        {
            var body = Node("Downed actor").AddComponent<SpriteRenderer>(); body.sprite = Hero.VisualSprite;
            var unit = CreateDowned(); var session = new CombatSession { Units = new() { unit } };
            session.TurnParticipants.Add(new CombatTurnParticipant(unit));
            CombatDownedIndicatorView view = null;
            CombatDownedIndicatorView.UpdateWorld(ref view, body, unit, Palette);
            var process = typeof(CombatTurnSystem).GetMethod("ProcessDownedTurn", BindingFlags.Instance | BindingFlags.NonPublic);
            for (var remaining = 3; remaining > 0; remaining--)
            {
                Assert.That(view.RemainingTurns, Is.EqualTo(remaining)); Assert.That(view.IsVisible, Is.True);
                Assert.That(Segments(view), Has.Length.EqualTo(3));
                Assert.That(Segments(view).Count(i => i.color.r > .2f), Is.EqualTo(remaining));
                process.Invoke(new CombatTurnSystem(), new object[] { session, unit }); Tick(view);
            }
            Assert.That(unit.Character.LifeState, Is.EqualTo(CharacterLifeState.Dead));
            Assert.That(session.Units, Is.Empty); Assert.That(view.IsVisible, Is.False);
        }

        [Test] public void DamageShortensCounterAndRevivalHidesTheIndicator()
        {
            var body = Node("Downed actor").AddComponent<SpriteRenderer>(); body.sprite = Hero.VisualSprite;
            var unit = CreateDowned(); var session = new CombatSession { Units = new() { unit } };
            CombatDownedIndicatorView view = null;
            CombatDownedIndicatorView.UpdateWorld(ref view, body, unit, Palette);
            new CombatDamageSystem().ApplyDamage(session, null, unit, 1, DamageType.Physical); Tick(view);
            Assert.That(view.RemainingTurns, Is.EqualTo(2));
            unit.Character.LifeState = CharacterLifeState.Alive; unit.DownedTurnsRemaining = 0; Tick(view);
            Assert.That(view.IsVisible, Is.False);
            unit.IsPlayerSide = false; unit.Character.LifeState = CharacterLifeState.Downed; unit.DownedTurnsRemaining = 3;
            Tick(view); Assert.That(view.IsVisible, Is.False);
        }

        [Test] public void WorldMarkRespectsFogUsesNoRaycastsAndStaysAboveTheActualPose()
        {
            var body = Node("Downed actor").AddComponent<SpriteRenderer>(); body.sprite = Hero.VisualSprite;
            body.transform.position = new Vector3(4, 6, 0); body.transform.localScale = Vector3.one * 2;
            CombatDownedIndicatorView view = null;
            CombatDownedIndicatorView.UpdateWorld(ref view, body, CreateDowned(), Palette); Tick(view);
            Assert.That(view.transform.position.y, Is.LessThan(body.bounds.max.y+.04f),"Transparent canvas must not add headroom.");
            Assert.That(view.transform.lossyScale.x, Is.EqualTo(1f / CombatDownedIndicatorView.WorldPixelsPerUnit).Within(.001));
            Assert.That(((RectTransform)view.transform).rect.width*view.transform.lossyScale.x, Is.LessThan(.7f));
            Assert.That(view.GetComponentsInChildren<Image>().Single(i=>i.name=="ReadabilityMatte").color.a, Is.EqualTo(1));
            Assert.That(view.GetComponentsInChildren<Graphic>(true).All(g => !g.raycastTarget), Is.True);
            Assert.That(view.GetComponent<GraphicRaycaster>(), Is.Null);
            body.forceRenderingOff = true; Tick(view); Assert.That(view.IsVisible, Is.False);
            body.forceRenderingOff = false; Tick(view); Assert.That(view.IsVisible, Is.True);
            body.enabled = false; Tick(view); Assert.That(view.IsVisible, Is.False);
        }

        [TestCase(false,0,.5f)] [TestCase(true,0,-.5f)]
        [TestCase(false,90,-.09375f)] [TestCase(true,90,-.09375f)]
        public void WorldAnchorUsesCroppedPoseWithScaleRotationAndFlip(bool flip,float rotation,float x)
        {
            var texture=new Texture2D(128,128,TextureFormat.RGBA32,false);owned.Add(texture);
            texture.SetPixels32(new Color32[128*128]);
            for(var y=58;y<74;y++)for(var px=68;px<76;px++)texture.SetPixel(px,y,Color.white);
            texture.Apply();
            var sprite=Sprite.Create(texture,new Rect(0,0,128,128),Vector2.one*.5f,32,0,SpriteMeshType.FullRect,Vector4.zero,true);owned.Add(sprite);
            Assert.That(sprite.GetPhysicsShapeCount(),Is.EqualTo(1));
            var body=Node("Padded downed pose").AddComponent<SpriteRenderer>();body.sprite=sprite;body.flipX=flip;
            body.transform.position=new Vector3(3,4,0);body.transform.localScale=new Vector3(2,1.5f,1);
            body.transform.localRotation=Quaternion.Euler(0,0,rotation);
            CombatDownedIndicatorView mark=null;
            CombatDownedIndicatorView.UpdateWorld(ref mark,body,CreateDowned(),Palette);Tick(mark);
            // Native fallback contours include their own simplification margin.
            // Project actual contour vertices, independently of the view's AABB.
            var points=new List<Vector2>();sprite.GetPhysicsShape(0,points);
            var top=points.Max(p=>body.transform.TransformPoint(new Vector3(flip ? -p.x:p.x,p.y,0)).y);
            Assert.That(mark.transform.position.x,Is.EqualTo(3+x).Within(.04));
            Assert.That(mark.transform.position.y,Is.EqualTo(top+1f/32).Within(.002));
            Assert.That(mark.transform.position.y,Is.LessThan(body.bounds.max.y-.5f));
        }

        [TestCase("jerzy")] [TestCase("wladimir")] [TestCase("character.mage")] [TestCase("character.paladin")]
        public void RealDownedPoseBoundsMatchPngAlphaWithoutChangingCanvasOrPivot(string hero)
        {
            Assert.That(Godmist.Views.Characters.CombatPoseResolver.TryResolveDowned(hero,out var pose),Is.True);
            var sprite=pose.Actor;
            Assert.That(Godmist.Views.Characters.CombatPoseResolver.TryGetVisibleBounds(sprite,out var bounds),Is.True);
            Assert.That(sprite.pixelsPerUnit,Is.EqualTo(32));Assert.That(sprite.texture.isReadable,Is.False);
            var decoded=new Texture2D(2,2,TextureFormat.RGBA32,false);owned.Add(decoded);
            ImageConversion.LoadImage(decoded,File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite.texture)),false);
            var pixels=decoded.GetPixels32();var minX=decoded.width;var minY=decoded.height;var maxX=0;var maxY=0;
            for(var y=0;y<decoded.height;y++)for(var x=0;x<decoded.width;x++)
                if(pixels[y*decoded.width+x].a>0){minX=Mathf.Min(x,minX);minY=Mathf.Min(y,minY);maxX=Mathf.Max(x,maxX);maxY=Mathf.Max(y,maxY);}
            Assert.That(bounds.min.x,Is.EqualTo((minX-sprite.pivot.x)/32).Within(.0001));
            Assert.That(bounds.max.y,Is.EqualTo((maxY+1-sprite.pivot.y)/32).Within(.0001));
            var body=Node(hero+" real downed").AddComponent<SpriteRenderer>();body.sprite=sprite;
            CombatDownedIndicatorView mark=null;CombatDownedIndicatorView.UpdateWorld(ref mark,body,CreateDowned(),Palette);Tick(mark);
            Assert.That(mark.transform.position.y,Is.EqualTo(bounds.max.y+1f/32).Within(.0001));
            Assert.That(mark.transform.position.y,Is.LessThan(body.bounds.max.y-.5f),"Runtime pose canvas must not lift its countdown.");
            Assert.That(sprite.rect.size,Is.EqualTo(new Vector2(decoded.width,decoded.height)));
        }

        [Test]
        public void ImportedPartyArtProvidesACroppedOutlineInsteadOfCanvasHeight()
        {
            var sprite=Hero.VisualSprite;var points=new List<Vector2>();var top=float.NegativeInfinity;
            Assert.That(sprite.GetPhysicsShapeCount(),Is.GreaterThan(0));
            Assert.That(sprite.texture.isReadable,Is.False,"Imported outline works without CPU-readable textures.");
            for(var i=0;i<sprite.GetPhysicsShapeCount();i++)
            {sprite.GetPhysicsShape(i,points);foreach(var p in points)top=Mathf.Max(top,p.y);}
            Assert.That(top,Is.LessThan(sprite.bounds.max.y-.05f),"Shipped actor's transparent top margin must be excluded.");
        }

        [Test]
        public void WorkspaceCountdownHasOneHoverOwnerRefreshesCounterAndPreservesPinnedCards()
        {
            var canvas=Node("Downed tooltip canvas").AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var tooltipRoot=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_Tooltip.prefab"),canvas.transform,false);
            var tooltip=tooltipRoot.GetComponentInChildren<UIWorkspaceTooltipView>(true);
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            void Invoke(string method)=>typeof(UIWorkspaceTooltipView).GetMethod(method,flags).Invoke(tooltip,null);
            object Get(string name)=>typeof(UIWorkspaceTooltipView).GetField(name,flags).GetValue(tooltip);
            void Show()
            {
                var action=(System.Action)Get("pendingShow");Assert.That(action,Is.Not.Null);
                typeof(UIWorkspaceTooltipView).GetField("pendingShow",flags).SetValue(tooltip,null);action();
            }
            Invoke("Awake");Invoke("OnEnable");
            var card=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_PartyMember.prefab"),canvas.transform,false);
            var row=card.GetComponent<CharacterInfoPartyPortraitView>();
            typeof(CharacterInfoPartyPortraitView).GetField("tooltipView",flags).SetValue(row,tooltip);
            var unit=CreateDowned();row.Bind(unit.Character,null);row.RefreshPresentation(unit,false,false);
            Canvas.ForceUpdateCanvases();
            var mark=card.GetComponentInChildren<CombatDownedIndicatorView>(true);
            Assert.That(mark.GetComponentInChildren<CombatSimpleTooltipTrigger>(true),Is.Null,
                "Countdown must not compete with the parent row's rich unit source.");
            var eventSystem=Node("Tooltip events").AddComponent<EventSystem>();
            var e=new PointerEventData(eventSystem);
            var portrait=((RectTransform)mark.transform.parent);
            e.position=RectTransformUtility.WorldToScreenPoint(null,portrait.TransformPoint(new Vector3(0,50,0)));
            row.OnPointerEnter(e);Show();Assert.That((bool)Get("isRichUnitContent"),Is.True);
            e.position=RectTransformUtility.WorldToScreenPoint(null,mark.transform.TransformPoint(new Vector3(0,25,0)));
            row.OnPointerMove(e);Show();
            Assert.That(Get("sourceIdentity"),Is.SameAs(row));Assert.That((bool)Get("isRichUnitContent"),Is.False);
            Assert.That(((TMPro.TextMeshProUGUI)Get("titleText")).text,Is.EqualTo("Powalony"));
            Assert.That(((TMPro.TextMeshProUGUI)Get("bodyText")).text,Does.Contain("3/3"));
            Assert.That((System.Collections.ICollection)Get("nested"),Is.Empty);Assert.That(Get("pendingNestedShow"),Is.Null);
            unit.DownedTurnsRemaining=2;row.RefreshPresentation(unit,false,false);Show();
            Assert.That(((TMPro.TextMeshProUGUI)Get("bodyText")).text,Does.Contain("2/3"));
            var camera=Node("Compact tooltip preview camera").AddComponent<Camera>();
            camera.orthographic=true;camera.orthographicSize=180;camera.transform.position=new Vector3(0,0,-10);
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.09f,.07f);
            var target=new RenderTexture(768,360,24);owned.Add(target);camera.targetTexture=target;
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
            ((RectTransform)card.transform).anchoredPosition=new Vector2(-220,0);
            var tooltipRect=(RectTransform)Get("tooltipRect");
            tooltipRect.anchorMin=tooltipRect.anchorMax=tooltipRect.pivot=Vector2.one*.5f;
            tooltipRect.anchoredPosition=new Vector2(120,0);Canvas.ForceUpdateCanvases();camera.Render();
            var previous=RenderTexture.active;RenderTexture.active=target;
            var png=new Texture2D(768,360,TextureFormat.RGBA32,false);owned.Add(png);
            png.ReadPixels(new Rect(0,0,768,360),0,0);png.Apply();RenderTexture.active=previous;
            var folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Godmist/LocalHandoffs/downed-tooltip-anchor-20261014"));
            Directory.CreateDirectory(folder);File.WriteAllBytes(Path.Combine(folder,"tooltip-preview.png"),png.EncodeToPNG());
            canvas.renderMode=RenderMode.ScreenSpaceOverlay;Canvas.ForceUpdateCanvases();
            e.position=RectTransformUtility.WorldToScreenPoint(null,portrait.TransformPoint(new Vector3(0,50,0)));
            row.OnPointerMove(e);Show();tooltip.PinAtCurrentPosition();
            e.position=RectTransformUtility.WorldToScreenPoint(null,mark.transform.TransformPoint(new Vector3(0,25,0)));
            row.OnPointerMove(e);
            Assert.That(tooltip.IsPinned,Is.True);Assert.That((bool)Get("isRichUnitContent"),Is.True);
            Invoke("OnDisable");
        }

        [Test] public void ExistingPartyPrefabUsesTheSameSkinAndClearsOnExplorationRebind()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/CombatHUD/PF_CombatPartyMember.prefab");
            var node = Object.Instantiate(prefab); owned.Add(node);
            var member = node.GetComponent<CombatPartyMemberView>(); member.Bind(CreateDowned());
            var indicator = node.GetComponentInChildren<CombatDownedIndicatorView>(true);
            Assert.That(indicator, Is.Not.Null); Assert.That(indicator.IsVisible, Is.True);
            var frame = indicator.GetComponentsInChildren<Image>(true).Single(i => i.name == "GodmistBarFrame");
            Assert.That(frame.sprite, Is.SameAs(Palette.GetArtwork("slider_track")));
            var skull = indicator.GetComponentsInChildren<Image>(true).Single(i => i.name == "Skull");
            Assert.That(skull.sprite, Is.SameAs(Palette.GetArtwork("status_downed_skull")));
            Assert.That(skull.sprite, Is.Not.Null); Assert.That(skull.preserveAspect, Is.True);
            member.BindCharacter(new CharacterInstanceFactory().Create(Hero, Hero.ClassData, 1));
            Assert.That(indicator.IsVisible, Is.False);
        }

        [Test] public void BorrowedExplorationActorCreatesAndClearsWorldCountdown()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Characters/PF_ExplorationCharacter.prefab");
            var node = Object.Instantiate(prefab); owned.Add(node);
            var actor = node.GetComponent<ExplorationCharacterView>();
            actor.BindCombat(CreateDowned()); actor.RefreshCombatPresentation(false, false, false);
            var indicator = node.GetComponentInChildren<CombatDownedIndicatorView>(true);
            Assert.That(indicator, Is.Not.Null); Assert.That(indicator.IsVisible, Is.True);
            actor.ReleaseCombat(); Assert.That(indicator.IsVisible, Is.False);
        }

        [TestCase("Assets/_Project/Prefabs/UI/CombatHUD/PF_CombatPartyMember.prefab")]
        [TestCase("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_PartyMember.prefab")]
        public void PortraitCountdownFitsTheAuthoredCardAndHasOpaqueContrast(string path)
        {
            var node = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path)); owned.Add(node);
            var member = node.GetComponent<CombatPartyMemberView>();
            var unit = CreateDowned(1);
            member.Bind(unit);
            var mark = node.GetComponentInChildren<CombatDownedIndicatorView>(true);
            Canvas.ForceUpdateCanvases(); Tick(mark);
            var portrait = (RectTransform)mark.transform.parent;
            var panel = (RectTransform)mark.transform;
            Assert.That(panel.rect.width, Is.LessThanOrEqualTo(portrait.rect.width - 8));
            Assert.That(panel.rect.height + panel.anchoredPosition.y, Is.LessThanOrEqualTo(portrait.rect.height));
            var images = mark.GetComponentsInChildren<Image>(true);
            Assert.That(images.Single(i => i.name == "ReadabilityMatte").color.a, Is.EqualTo(1));
            var skull = images.Single(i => i.name == "Skull");
            Assert.That(skull.color, Is.EqualTo(Color.white));
            Assert.That(skull.rectTransform.rect.width, Is.GreaterThanOrEqualTo(32));
            var segments = Segments(mark);
            Assert.That(segments.Count(i => i.color.r > .2f), Is.EqualTo(1));
            foreach (var segment in segments)
                Assert.That(Mathf.Abs(segment.rectTransform.anchoredPosition.x) + segment.rectTransform.rect.width * .5f,
                    Is.LessThan(segment.rectTransform.parent.GetComponent<RectTransform>().rect.width * .5f));
            var marker = (Image)new SerializedObject(member).FindProperty("lifeStateMarker").objectReferenceValue;
            Assert.That(marker.enabled, Is.False, "Old life-state strip must not cover the countdown.");
            unit.Character.LifeState = CharacterLifeState.Dead;
            member.Bind(unit);
            Assert.That(mark.IsVisible, Is.False);
            Assert.That(marker.enabled, Is.True, "Permanent death retains the existing marker.");
        }

        [Test] public void RenderThreeTwoOneAndRevivedPortraits()
        {
            var camera = Node("Preview camera").AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 120; camera.transform.position = new Vector3(0, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.07f, .07f, .085f);
            var target = new RenderTexture(848, 240, 24); owned.Add(target); camera.targetTexture = target;
            var canvasNode = Node("Preview canvas"); var canvas = canvasNode.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            for (var index = 0; index < 4; index++)
            {
                var card = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_PartyMember.prefab"), canvas.transform, false);
                ((RectTransform)card.transform).anchoredPosition = new Vector2(-318 + index * 212, 0);
                var unit = CreateDowned(3 - index);
                if (index == 3) { unit.Character.LifeState = CharacterLifeState.Alive; unit.Character.CurrentHealth = 1; }
                var row = card.GetComponent<CharacterInfoPartyPortraitView>();
                row.RefreshPresentation(unit, false, false);
                var label = (TMPro.TextMeshProUGUI)new SerializedObject(row).FindProperty("lifeStateText").objectReferenceValue;
                Assert.That(label.gameObject.activeSelf, Is.False, "Production rail label must not cover segments.");
            }
            Canvas.ForceUpdateCanvases(); camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = target;
            var png = new Texture2D(848, 240, TextureFormat.RGBA32, false); owned.Add(png);
            png.ReadPixels(new Rect(0, 0, 848, 240), 0, 0); png.Apply(); RenderTexture.active = previous;
            var pixels = png.GetPixels32();
            Assert.That(pixels.Count(p => p.r > 150 && p.g > 100), Is.GreaterThan(100), "Skin and bone geometry must be rendered.");
            var folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Godmist/LocalHandoffs/downed-tooltip-anchor-20261014"));
            Directory.CreateDirectory(folder); File.WriteAllBytes(Path.Combine(folder, "portrait-countdown-preview.png"), png.EncodeToPNG());
        }

        [Test] public void RenderCompactWorldCountdowns()
        {
            var camera = Node("World countdown preview").AddComponent<Camera>();
            camera.orthographic=true; camera.orthographicSize=1.8f; camera.transform.position=new Vector3(0,.7f,-10);
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.12f,.14f,.10f);
            var target=new RenderTexture(600,300,24); owned.Add(target); camera.targetTexture=target;
            for(var i=0;i<3;i++)
            {
                var body=Node("Downed actor "+i).AddComponent<SpriteRenderer>();
                Assert.That(Godmist.Views.Characters.CombatPoseResolver.TryResolveDowned("jerzy",out var pose),Is.True);
                body.sprite=pose.Actor;
                body.transform.position=new Vector3((i-1)*2,0,0);
                CombatDownedIndicatorView mark=null;
                CombatDownedIndicatorView.UpdateWorld(ref mark,body,CreateDowned(3-i),Palette); Tick(mark);
                mark.GetComponent<Canvas>().worldCamera=camera;
            }
            Canvas.ForceUpdateCanvases(); camera.Render();
            var previous=RenderTexture.active; RenderTexture.active=target;
            var png=new Texture2D(600,300,TextureFormat.RGBA32,false); owned.Add(png);
            png.ReadPixels(new Rect(0,0,600,300),0,0); png.Apply(); RenderTexture.active=previous;
            var folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Godmist/LocalHandoffs/downed-tooltip-anchor-20261014"));
            Directory.CreateDirectory(folder); File.WriteAllBytes(Path.Combine(folder,"world-countdown-preview.png"),png.EncodeToPNG());
            Assert.That(png.GetPixels32().Count(p=>p.r>150 && p.g>100),Is.GreaterThan(30));
        }
    }
}

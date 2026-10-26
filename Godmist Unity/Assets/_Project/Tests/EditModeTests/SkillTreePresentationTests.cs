using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Game;
using Godmist.Systems.Characters;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Godmist.Tests.EditMode
{
    public class SkillTreePresentationTests
    {
        const string Prefab = "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_SkillTreeWindow Variant.prefab";
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        static object Field(object owner, string name) => owner.GetType().GetField(name, Flags).GetValue(owner);
        static void Call(object owner,string method,params object[] values) => owner.GetType().GetMethod(method,Flags).Invoke(owner,values);
        static void Set(object owner, string name, object value) => owner.GetType().GetField("<"+name+">k__BackingField", Flags).SetValue(owner, value);
        static CharacterInstance Jerzy() => new CharacterInstanceFactory().Create(AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/ScriptableObjects/Characters/CD_Jerzy.asset"));
        static GameSession Session(CharacterInstance character)
        {
            var session = new GameSession(GameModeState.Exploration);
            Assert.That(session.TryRegisterCharacter(character), Is.True);
            Assert.That(session.TryAddCharacterToActiveParty(character.CharacterId), Is.True);
            return session;
        }
        static GameObject Workspace()
        {
            var root = new GameObject("Tree test workspace", typeof(RectTransform), typeof(Canvas));
            ((RectTransform)root.transform).sizeDelta = new Vector2(1920,1080);
            root.AddComponent<UIWorkspaceWindowManager>();
            return root;
        }
        static SkillTreeNodeView Card(SkillTreeProgressionPanelView view, string id) => view.GetComponentsInChildren<SkillTreeNodeView>().Single(card => card.NodeId == id);

        [UnityTest]
        public IEnumerator NodeSelectionDoesNotSpendAndInvestmentPreservesSelectionAndSafeFocus()
        {
            yield return new EnterPlayMode();
            var root = Workspace(); var eventsNode = new GameObject("Tree events",typeof(EventSystem));
            try
            {
                var character = Jerzy(); character.AvailableSkillPoints=2; var session=Session(character);
                var tree=character.ClassData.SkillTrees[0]; var node=tree.Nodes.First(n=>n.Tier==1 && n.EffectiveMaxRanks==2 && n.Requirements.Count==0);
                var view=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab),root.transform).GetComponent<SkillTreeProgressionPanelView>();
                view.Open(session,character.CharacterId);
                var card=Card(view,node.Id); EventSystem.current.SetSelectedGameObject(card.gameObject);
                card.GetComponent<Button>().onClick.Invoke();
                Assert.That(character.AvailableSkillPoints,Is.EqualTo(2));
                Assert.That(SkillTreeProgressionSystem.GetInvestedRanks(character,tree.Id),Is.Zero);
                Assert.That(((TMP_Text)Field(view,"detailsTitle")).text,Is.EqualTo(node.DisplayName));
                var notifications=0;session.CharacterProgressionChanged+=_=>notifications++;
                var invest=(Button)Field(view,"investButton");Assert.That(invest.interactable,Is.True);invest.onClick.Invoke();
                Assert.That(notifications,Is.EqualTo(1)); Assert.That(character.AvailableSkillPoints,Is.EqualTo(1));
                Assert.That(Field(view,"selectedNodeId"),Is.EqualTo(node.Id)); Assert.That(((TMP_Text)Field(view,"detailsRank")).text,Is.EqualTo("Ranga: 1/2"));
                Assert.That(ReferenceEquals(EventSystem.current.currentSelectedGameObject,null),Is.True,"Rebuilding cards must not leave focus on a destroyed card.");
                invest.onClick.Invoke();Assert.That(character.AvailableSkillPoints,Is.Zero);Assert.That(invest.interactable,Is.False);
                Assert.That(invest.gameObject.activeSelf,Is.False,"A completed node must not offer a purchase.");
                Assert.That(((TMP_Text)Field(view,"investmentBlockText")).text,Is.EqualTo("Maksymalna ranga"));
                invest.onClick.Invoke();Assert.That(notifications,Is.EqualTo(2),"A stale or disabled action must not spend again.");
                var nextNode=tree.Nodes.First(n=>n.Id!=node.Id && n.Tier==1 && n.Requirements.Count==0);
                Card(view,nextNode.Id).GetComponent<Button>().onClick.Invoke();
                Assert.That(invest.gameObject.activeSelf,Is.True,"Selecting an incomplete node must restore the purchase button.");
                view.Close();((TMP_Text)Field(view,"detailsTitle")).text="closed";session.NotifyCharacterProgressionChanged(character.CharacterId);
                Assert.That(((TMP_Text)Field(view,"detailsTitle")).text,Is.EqualTo("closed"));
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(eventsNode);}
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator LockedNodesRemainInspectableAndTabSwitchDoesNotSpend()
        {
            yield return new EnterPlayMode();
            var root=Workspace();
            try
            {
                var character=Jerzy();character.AvailableSkillPoints=0;var session=Session(character);
                var view=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab),root.transform).GetComponent<SkillTreeProgressionPanelView>();view.Open(session,character.CharacterId);
                var tree=character.ClassData.SkillTrees[0];var node=tree.Nodes.First(n=>n.Tier==3);
                var card=Card(view,node.Id);Assert.That(card.GetComponent<Button>().interactable,Is.True);card.GetComponent<Button>().onClick.Invoke();
                Assert.That(((TMP_Text)Field(view,"detailsTitle")).text,Is.EqualTo(node.DisplayName));
                Assert.That(((TMP_Text)Field(view,"investmentBlockText")).text,Does.Contain("Wymaga 10 pkt"));
                Assert.That(((Button)Field(view,"investButton")).interactable,Is.False);
                Assert.That(((Image)Field(card,"icon")).color.a,Is.EqualTo(1),"Structural locking uses one overlay, not double fading.");
                Assert.That(((Image)Field(card,"disabledOverlay")).enabled,Is.True);
                var available=tree.Nodes.First(n=>n.Tier==1 && n.Requirements.Count==0);var availableCard=Card(view,available.Id);availableCard.GetComponent<Button>().onClick.Invoke();
                Assert.That(((Image)Field(availableCard,"disabledOverlay")).enabled,Is.False,"No points must not look like a structural lock.");
                Assert.That(((TMP_Text)Field(view,"investmentBlockText")).text,Does.Contain("Brak dostępnych punktów"));
                var tabs=(Transform)Field(view,"treeTabRoot");var second=tabs.GetComponentsInChildren<Button>().Last();second.onClick.Invoke();
                Assert.That(((SkillTreeDefinition)Field(view,"selectedTree")).Id,Is.EqualTo(character.ClassData.SkillTrees[1].Id));
                Assert.That(character.AvailableSkillPoints,Is.Zero);
                Assert.That(tabs.GetComponentsInChildren<Button>().Last().GetComponentInChildren<TMP_Text>().text,Does.Contain(character.ClassData.SkillTrees[1].DisplayName));
                yield return null;yield return null;yield return null;
                var detailScroll=(ScrollRect)Field(view,"detailsScroll");
                Assert.That(detailScroll.verticalScrollbar.gameObject.activeSelf,Is.False,"Short details must not show a scrollbar.");
                Assert.That(((ScrollRect)Field(view,"treeScroll")).verticalScrollbar.gameObject.activeSelf,Is.False,"Four tiers fit the default window.");
            }
            finally{Object.DestroyImmediate(root);}
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ActiveShapeKeepsRankSelectionHoverHoldAndRaycastIndependent()
        {
            yield return new EnterPlayMode();var root=Workspace();var events=new GameObject("Shape events",typeof(EventSystem));
            try
            {
                var character=Jerzy();var session=Session(character);var tree=character.ClassData.SkillTrees[0];
                var view=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab),root.transform).GetComponent<SkillTreeProgressionPanelView>();view.Open(session,character.CharacterId);
                var active=tree.Nodes.First(n=>n.GrantedSkill!=null);var passive=tree.Nodes.First(n=>n.GrantedSkill==null);
                var card=Card(view,active.Id);var passiveCard=Card(view,passive.Id);
                var surface=(Image)Field(card,"nodeSurface");var frame=(SkillTreeActiveNodeFrame)Field(card,"activeSurface");var accent=(SkillTreeActiveNodeFrame)Field(card,"activeRankAccent");
                var selected=(SkillTreeActiveNodeFrame)Field(card,"activeSelectionBorder");var hover=(SkillTreeActiveNodeFrame)Field(card,"activeHoverBorder");var hold=(SkillTreeActiveNodeFrame)Field(card,"activeHoldBorder");
                Assert.That(frame.enabled,Is.True);Assert.That(((SkillTreeActiveNodeFrame)Field(passiveCard,"activeSurface")).enabled,Is.False);
                Assert.That(surface.enabled && surface.raycastTarget,Is.True);Assert.That(surface.color.a,Is.Zero);Assert.That(surface.Raycast(Vector2.zero,null),Is.True);
                Assert.That(((Image)Field(passiveCard,"nodeSurface")).color.a,Is.EqualTo(1));Assert.That(((Image)Field(card,"rankAccent")).enabled,Is.False);
                Assert.That(((TMP_Text)Field(card,"rankText")).enabled,Is.False);
                Assert.That(((RectTransform)Field(card,"rankBackground")).gameObject.activeSelf,Is.False);
                var markers=(SkillTreeRankMarkers)Field(card,"rankMarkers");
                Assert.That(markers.rectTransform.anchorMin.x,Is.EqualTo(.5f));
                Assert.That(markers.rectTransform.anchoredPosition,Is.EqualTo(((SkillTreeRankMarkers)Field(passiveCard,"rankMarkers")).rectTransform.anchoredPosition));
                Assert.That(markers.rectTransform.anchoredPosition.y,Is.Zero);
                Assert.That(markers.rectTransform.pivot.y,Is.EqualTo(.5f));
                var mask=(SkillIconShapeMask)Field(card,"iconMask");var maskedIcon=(Image)Field(card,"icon");
                Assert.That(maskedIcon.transform.parent,Is.SameAs(mask.transform));
                Assert.That(mask.GetComponent<Mask>().showMaskGraphic,Is.False);
                Assert.That(maskedIcon.rectTransform.anchorMin,Is.EqualTo(Vector2.zero));Assert.That(maskedIcon.rectTransform.anchorMax,Is.EqualTo(Vector2.one));
                Assert.That(maskedIcon.rectTransform.offsetMin,Is.EqualTo(Vector2.zero));Assert.That(maskedIcon.rectTransform.offsetMax,Is.EqualTo(Vector2.zero));
                Assert.That(maskedIcon.preserveAspect,Is.False);Assert.That((float)Field(mask,"cornerCut"),Is.EqualTo(14));
                Assert.That(((Image)Field(card,"disabledOverlay")).transform.parent,Is.SameAs(mask.transform));
                var purchases=0;card.Configure(active,0,true,true,"",()=>{},()=>purchases++,character);Canvas.ForceUpdateCanvases();
                var zeroColor=accent.color;card.SetSelected(true);Assert.That(selected.enabled,Is.True);Assert.That(((Image)Field(card,"selectionBorder")).gameObject.activeSelf,Is.False);
                var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,pointerId=-1};card.OnPointerEnter(pointer);
                Assert.That(hover.enabled,Is.True);Assert.That(((Image)Field(card,"hoverBorder")).enabled,Is.False);
                card.OnPointerDown(pointer);Call(card,"AdvanceHold",.35f);
                Assert.That(hold.enabled,Is.True);Assert.That(((Image)Field(card,"holdFillBorder")).enabled,Is.False);
                var clip=(RectTransform)Field(card,"holdFillViewport");Assert.That(clip.rect.height,Is.EqualTo(hold.rectTransform.rect.height*.5f).Within(.01f));
                Assert.That(clip.GetSiblingIndex(),Is.GreaterThan(accent.transform.GetSiblingIndex()),"Hold must render above the rank frame.");
                Assert.That(markers.transform.GetSiblingIndex(),Is.GreaterThan(clip.GetSiblingIndex()),"Gems must render above the hold overlay.");
                card.OnPointerUp(pointer);Assert.That(hold.enabled,Is.False);Assert.That(purchases,Is.Zero);
                card.OnPointerDown(pointer);Call(card,"AdvanceHold",.71f);Call(card,"AdvanceHold",2f);Assert.That(purchases,Is.EqualTo(1));
                card.Configure(active,active.EffectiveMaxRanks,true,false,"Brak punktów",()=>{},null,character);card.SetSelected(true);card.OnPointerEnter(pointer);
                Assert.That(accent.color,Is.Not.EqualTo(zeroColor));Assert.That(accent.color.g,Is.GreaterThan(accent.color.r));Assert.That(selected.enabled && hover.enabled,Is.True);
                card.Configure(passive,0,true,false,"",()=>{},null,character);card.SetSelected(true);
                Assert.That(frame.enabled || accent.enabled || selected.enabled || hover.enabled || hold.enabled,Is.False);
                Assert.That(surface.color,Is.EqualTo(Color.white));Assert.That(((Image)Field(card,"rankAccent")).enabled,Is.True);Assert.That(((Image)Field(card,"selectionBorder")).gameObject.activeSelf,Is.True);
                Assert.That(markers.rectTransform.anchorMin.x,Is.EqualTo(.5f));
                Assert.That(((TMP_Text)Field(card,"rankText")).enabled,Is.False);
                Assert.That(markers.color.g,Is.GreaterThan(markers.color.r));
                Assert.That((Color)Field(markers,"emptyColor"),Is.EqualTo(Color.white));
                Assert.That((float)Field(mask,"cornerCut"),Is.EqualTo(2));
                Assert.That(mask.rectTransform.offsetMin,Is.EqualTo(new Vector2(4,4)));
                yield return null;
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(events);}
            yield return new ExitPlayMode();
        }

        [Test]
        public void ActiveFrameMeshHasSteppedCutCornersAndHollowCenter()
        {
            var root=new GameObject("Shape",typeof(RectTransform),typeof(SkillTreeActiveNodeFrame));var mesh=new Mesh();
            try
            {
                var shape=root.GetComponent<SkillTreeActiveNodeFrame>();shape.rectTransform.sizeDelta=new Vector2(96,96);shape.Configure(true,false,4);
                var populate=typeof(SkillTreeActiveNodeFrame).GetMethod("OnPopulateMesh",Flags|BindingFlags.DeclaredOnly);
                using(var vertices=new VertexHelper())
                {
                    populate.Invoke(shape,new object[]{vertices});vertices.FillMesh(mesh);
                    var top=mesh.vertices.Where(v=>Mathf.Approximately(v.y,48)).ToArray();
                    Assert.That(top.Length,Is.GreaterThan(0));Assert.That(top.Min(v=>v.x),Is.GreaterThanOrEqualTo(-32));Assert.That(top.Max(v=>v.x),Is.LessThanOrEqualTo(32));
                    Assert.That(mesh.vertices.Any(v=>Mathf.Approximately(v.x,-48)&&Mathf.Abs(v.y)<20),Is.True);
                }
                shape.Configure(false,false,2);
                using(var vertices=new VertexHelper())
                {
                    populate.Invoke(shape,new object[]{vertices});vertices.FillMesh(mesh);
                    for(var i=0;i<mesh.vertices.Length;i+=4)
                    {
                        var quad=mesh.vertices.Skip(i).Take(4).ToArray();
                        Assert.That(quad.Max(v=>v.x)-quad.Min(v=>v.x)<=2.01f || Mathf.Abs(quad[0].y)>=46,Is.True,"An accent must not cover the skill icon.");
                    }
                }
            }
            finally{Object.DestroyImmediate(mesh);Object.DestroyImmediate(root);}
        }

        [TestCase(0,1)]
        [TestCase(1,1)]
        [TestCase(0,2)]
        [TestCase(1,2)]
        [TestCase(2,2)]
        [TestCase(0,3)]
        [TestCase(2,3)]
        [TestCase(3,3)]
        public void RankGemsStayCenteredAndOnlyInvestedRanksReceiveGreenTint(int invested,int maximum)
        {
            var root=new GameObject("Ranks",typeof(RectTransform),typeof(SkillTreeRankMarkers));var mesh=new Mesh();
            try
            {
                var markers=root.GetComponent<SkillTreeRankMarkers>();markers.rectTransform.sizeDelta=new Vector2(80,14);
                var filled=Color.green;var empty=Color.white;
                markers.SetRanks(invested,maximum,filled,empty);
                using(var vertices=new VertexHelper())
                {
                    typeof(SkillTreeRankMarkers).GetMethod("OnPopulateMesh",Flags|BindingFlags.DeclaredOnly).Invoke(markers,new object[]{vertices});vertices.FillMesh(mesh);
                }
                var positions=mesh.vertices;var colors=mesh.colors32;var area=0f;
                for(var i=0;i<positions.Length;i+=4)
                {
                    var center=(positions[i]+positions[i+2])*.5f;
                    var rank=Mathf.RoundToInt((center.x+(maximum-1)*9f)/18f);
                    if(colors[i].r==0 && colors[i].g==0 && colors[i].b==0)
                    {
                        Assert.That(colors[i].a,Is.GreaterThan(0).And.LessThan(255));
                        continue;
                    }
                    if(rank<invested)
                    {
                        Assert.That(colors[i].r,Is.Zero);Assert.That(colors[i].b,Is.Zero);Assert.That(colors[i].g,Is.GreaterThan(0));
                    }
                    else
                    {
                        Assert.That(colors[i].r,Is.EqualTo(colors[i].g));Assert.That(colors[i].g,Is.EqualTo(colors[i].b));
                    }
                    area+=(positions[i+2].x-positions[i].x)*(positions[i+1].y-positions[i].y);
                }
                Assert.That(area,Is.EqualTo(maximum*100),"Each rank uses the same gem silhouette.");
                Assert.That(colors.Select(c=>c.g).Distinct().Count(),Is.GreaterThan(2),"Facets must preserve the white gem's shading after tinting.");
                Assert.That((positions.Min(v=>v.x)+positions.Max(v=>v.x))*.5f,Is.Zero.Within(.01f));
                Assert.That(positions.Max(v=>v.x)-positions.Min(v=>v.x),Is.EqualTo(maximum*14+(maximum-1)*4+2));
                Assert.That(markers.raycastTarget,Is.False);
            }
            finally{Object.DestroyImmediate(mesh);Object.DestroyImmediate(root);}
        }

        [TestCase(14,92,32)]
        [TestCase(2,88,42)]
        public void SkillIconMaskFillsInteriorAndClipsSteppedCorners(float cut,float size,float topExtent)
        {
            var root=new GameObject("Icon mask",typeof(RectTransform),typeof(SkillIconShapeMask));var mesh=new Mesh();
            try
            {
                var shape=root.GetComponent<SkillIconShapeMask>();shape.rectTransform.sizeDelta=new Vector2(size,size);shape.SetCornerCut(cut);
                using(var vertices=new VertexHelper())
                {
                    typeof(SkillIconShapeMask).GetMethod("OnPopulateMesh",Flags|BindingFlags.DeclaredOnly).Invoke(shape,new object[]{vertices});vertices.FillMesh(mesh);
                }
                var top=mesh.vertices.Where(v=>Mathf.Approximately(v.y,size*.5f));
                Assert.That(top.Max(v=>v.x),Is.EqualTo(topExtent));Assert.That(top.Min(v=>v.x),Is.EqualTo(-topExtent));
                Assert.That(mesh.vertices.Any(v=>Mathf.Approximately(v.x,-size*.5f)&&Mathf.Abs(v.y)<4),Is.True);
                Assert.That(mesh.colors32.All(c=>c.a==255),Is.True,"Stencil silhouette must be opaque.");
                Assert.That(shape.raycastTarget,Is.False);
            }
            finally{Object.DestroyImmediate(mesh);Object.DestroyImmediate(root);}
        }

        [UnityTest]
        public IEnumerator TierGateProgressTracksInvestmentAndDisappearsWhenUnlocked()
        {
            yield return new EnterPlayMode();
            var root=Workspace();
            try
            {
                var character=Jerzy();character.AvailableSkillPoints=10;var session=Session(character);var tree=character.ClassData.SkillTrees[0];
                var view=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab),root.transform).GetComponent<SkillTreeProgressionPanelView>();view.Open(session,character.CharacterId);
                yield return null;yield return null;yield return null;
                var gates=(RectTransform[])Field(view,"tierGateRoots");var texts=(TextMeshProUGUI[])Field(view,"tierGateTexts");var fills=(Image[])Field(view,"tierGateFills");
                Assert.That(gates[0].gameObject.activeSelf,Is.True);Assert.That(texts[0].text,Is.EqualTo("0/10"));Assert.That(fills[0].fillAmount,Is.Zero);
                var progression=new SkillTreeProgressionSystem();
                for(var rank=1;rank<=10;rank++)
                {
                    var node=tree.Nodes.First(n=>string.IsNullOrWhiteSpace(progression.GetInvestmentBlockReason(character,tree.Id,n.Id)));
                    Assert.That(progression.TryInvest(character,tree.Id,node.Id),Is.True);session.NotifyCharacterProgressionChanged(character.CharacterId);
                    if(rank==5)
                    {
                        // Gate refresh follows the deferred rebuild of the tree's layout.
                        yield return null;yield return null;yield return null;
                        Assert.That(texts[0].text,Is.EqualTo("5/10"));Assert.That(fills[0].fillAmount,Is.EqualTo(.5f).Within(.001f));
                        Assert.That(fills[0].type,Is.EqualTo(Image.Type.Filled));Assert.That(fills[0].fillMethod,Is.EqualTo(Image.FillMethod.Horizontal));
                        Assert.That(gates[0].GetComponentsInChildren<Graphic>(true).All(g=>!g.raycastTarget),Is.True);
                    }
                }
                yield return null;yield return null;yield return null;
                Assert.That(gates[0].gameObject.activeSelf,Is.False);
                yield return null;
            }
            finally{Object.DestroyImmediate(root);}
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator SevenTiersScrollWithThinGatesAndConnectionsStayAttachedAfterResize()
        {
            yield return new EnterPlayMode();
            var root=Workspace(); var character=Jerzy();var tree=Object.Instantiate(character.ClassData.SkillTrees[0]);var classData=Object.Instantiate(character.ClassData);
            try
            {
                for(var tier=5;tier<=7;tier++)
                {
                    var node=new SkillTreeNodeDefinition();Set(node,"Id","test.tier."+tier);Set(node,"Tier",tier);Set(node,"Column",3);Set(node,"DisplayName","Tier "+tier);Set(node,"Icon",tree.Nodes[0].Icon);tree.Nodes.Add(node);
                }
                classData.SkillTrees.Clear();classData.SkillTrees.Add(tree);character.ClassData=classData;
                var view=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab),root.transform).GetComponent<SkillTreeProgressionPanelView>();view.Open(Session(character),character.CharacterId);
                yield return null;yield return null;yield return null;Canvas.ForceUpdateCanvases();
                var scroll=(ScrollRect)Field(view,"treeScroll");Assert.That(scroll.content.rect.height,Is.GreaterThan(scroll.viewport.rect.height));
                var gates=(RectTransform[])Field(view,"tierGateRoots");
                foreach(var gate in gates)
                {
                    Assert.That(gate.gameObject.activeSelf,Is.True,gate.name);
                    Assert.That(gate.rect.height,Is.LessThanOrEqualTo(28.1f),gate.name);
                }
                scroll.verticalNormalizedPosition=0;Assert.That(scroll.verticalNormalizedPosition,Is.EqualTo(0).Within(.01));
                var window=view.GetComponent<UIWorkspaceWindow>();window.RectTransform.sizeDelta=new Vector2(1060,700);
                yield return null;yield return null;yield return null;Canvas.ForceUpdateCanvases();
                var connections=(RectTransform)Field(view,"prerequisiteConnectionRoot");var line=connections.GetComponentsInChildren<Image>().First();
                Assert.That(gates.All(gate=>gate.GetSiblingIndex()>connections.GetSiblingIndex()),Is.True,"Gate labels must cover connections behind their text.");
                var targetNode=tree.Nodes.First(node=>node.Requirements.Count>0);var sourceNode=tree.Nodes.Single(node=>node.Id==targetNode.Requirements[0].NodeId);
                var source=(RectTransform)Card(view,sourceNode.Id).transform;var corners=new Vector3[4];source.GetWorldCorners(corners);
                var sameTier=sourceNode.Tier==targetNode.Tier;
                var right=targetNode.Column>sourceNode.Column;
                var expected=sameTier ? (right ? (corners[2]+corners[3])*.5f : (corners[0]+corners[1])*.5f) : (corners[0]+corners[3])*.5f;
                var lineCorners=new Vector3[4];line.rectTransform.GetWorldCorners(lineCorners);
                Assert.That(Vector3.Distance((lineCorners[0]+lineCorners[1])*.5f,expected),Is.LessThan(.1f));
                Assert.That(line.color,Is.Not.EqualTo(Color.black));
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(tree);Object.DestroyImmediate(classData);}
            yield return new ExitPlayMode();
        }

        [TestCase("Zwiększa obrażenia o 10%/20%",2,1,"Zwiększa obrażenia o 10%")]
        [TestCase("Zwiększa obrażenia o 10%/20%",2,2,"Zwiększa obrażenia o 20%")]
        [TestCase("Premia 5/10% na 1/2 tury",2,2,"Premia 10% na 2 tury")]
        [TestCase("Premia 10/17.5/25%",3,2,"Premia 17.5%")]
        [TestCase("Premia 10/17.5/25%",3,3,"Premia 25%")]
        public void RankDescriptionsResolveOnlyTheRequestedRankAndPreserveSharedUnits(string description,int ranks,int rank,string expected)
        {
            var node=new SkillTreeNodeDefinition();Set(node,"MaxRanks",ranks);Set(node,"Description",description);
            Assert.That(SkillTreeNodeView.BuildRankDescription(node,rank),Is.EqualTo(expected));
        }

        [TestCase("10%/20%",3)]
        [TestCase("10%/20% oraz 1/2/3",2)]
        [TestCase("Koszt 0/200",1)]
        public void AmbiguousOrIncompleteRankDescriptionsStayAuthored(string description,int ranks)
        {
            var node=new SkillTreeNodeDefinition();Set(node,"MaxRanks",ranks);Set(node,"Description",description);
            Assert.That(SkillTreeNodeView.BuildRankDescription(node,1),Is.EqualTo(description));
        }

        [TestCase(0,false,true)]
        [TestCase(1,true,true)]
        [TestCase(2,true,false)]
        public void PassiveDetailsShowOnlyCurrentAndNextRank(int rank,bool current,bool next)
        {
            var node=new SkillTreeNodeDefinition();Set(node,"MaxRanks",2);Set(node,"Description","Premia 10%/20%");
            var body=SkillTreeNodeView.BuildDetails(node,rank);
            if (current && next)
                Assert.That(body, Is.EqualTo("<b>Aktualne → następne:</b>\nPremia 10% → 20%"));
            else
            {
                Assert.That(body.Contains("Aktualne:"),Is.EqualTo(current));Assert.That(body.Contains("Następne:"),Is.EqualTo(next));
                if(current)Assert.That(body,Does.Contain("Aktualne:</b>\nPremia 20%"));
                if(next)Assert.That(body,Does.Contain("Następne:</b>\nPremia 10%"));
            }
            Assert.That(body,Does.Not.Contain("Ranga 1:").And.Not.Contain("Ranga 2:").And.Not.Contain("10%/20%"));
        }

        [Test]
        public void CompletedTooltipPrioritizesMaximumRankOverMissingPoints()
        {
            var node=new SkillTreeNodeDefinition();Set(node,"MaxRanks",2);Set(node,"Description","Premia 10%/20%");
            var body=SkillTreeNodeView.BuildTooltip(node,2,"Brak dostępnych punktów umiejętności.");
            Assert.That(body,Does.Contain("Maksymalna ranga").And.Not.Contain("Brak dostępnych punktów").And.Not.Contain("Następne:"));
        }

        [TestCase("SD_ScoutShadow")]
        [TestCase("SD_ScoutAimedThrow")]
        public void ActiveNodeUsesFullSkillSummaryCostsAndCharacterPreview(string assetName)
        {
            var tooltipRoot=PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_Tooltip.prefab");
            try
            {
                var character=Jerzy();var skill=AssetDatabase.LoadAssetAtPath<SkillData>($"Assets/_Project/ScriptableObjects/Skills/{assetName}.asset");
                var node=new SkillTreeNodeDefinition();Set(node,"GrantedSkill",skill);Set(node,"Description","Daje niewidzialność na 3 tury, Szarża 1");
                var tooltip=tooltipRoot.GetComponent<UIWorkspaceTooltipView>();
                foreach(var rank in new[]{0,1})
                {
                    var body=SkillTreeNodeView.BuildDetails(node,rank,tooltip,character);
                    Assert.That(body,Does.Contain(tooltip.BuildLoadoutSkillDetails(skill,character)));
                    Assert.That(body,Does.Contain(UIWorkspaceTooltipView.BuildLoadoutSkillCost(skill,character)));
                    Assert.That(body,Does.Contain("<b>Efekty</b>").And.Contain("• ").And.Not.Contain("Odblokowuje umiejętność").And.Not.Contain("Szarża 1"));
                }
                tooltip.ShowLoadoutSkillDetails(skill,character,node,"Ranga: 0/1");
                ((System.Action)Field(tooltip,"pendingShow"))();
                Assert.That(((Image)Field(tooltip,"icon")).sprite,Is.EqualTo(skill.Icon));
                Assert.That(((TMP_Text)Field(tooltip,"titleText")).text,Is.EqualTo(skill.DisplayName));
                Assert.That(((TMP_Text)Field(tooltip,"bodyText")).text,Is.EqualTo(tooltip.BuildLoadoutSkillDetails(skill,character)));
                var visibleMeta = System.Text.RegularExpressions.Regex.Replace(((TMP_Text)Field(tooltip,"metaText")).text,"<[^>]*>","");
                Assert.That(visibleMeta,Does.Contain(UIWorkspaceTooltipView.BuildLoadoutSkillCost(skill,character)).And.Contain("Ranga: 0/1"));
            }
            finally{PrefabUtility.UnloadPrefabContents(tooltipRoot);}
        }

        [UnityTest]
        public IEnumerator HoldPurchaseCancelsSafelySpendsOnceAndKeepsRankColorsWhenSelected()
        {
            yield return new EnterPlayMode();
            var root=Workspace();var events=new GameObject("Hold events",typeof(EventSystem));
            try
            {
                var character=Jerzy();character.AvailableSkillPoints=2;character.CurrentExperience=600;
                var session=Session(character);var tree=character.ClassData.SkillTrees[0];
                var node=tree.Nodes.First(n=>n.Tier==1 && n.EffectiveMaxRanks==2 && n.Requirements.Count==0);
                var view=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab),root.transform).GetComponent<SkillTreeProgressionPanelView>();view.Open(session,character.CharacterId);
                Canvas.ForceUpdateCanvases();
                Assert.That(((Image)Field(view,"experienceFill")).fillAmount,Is.EqualTo(.25f).Within(.001f));
                var passive=((TMP_Text)Field(view,"globalPassiveText")).text;
                Assert.That(passive,Does.Contain("Premia za punkt"));Assert.That(passive,Does.Contain("Aktualnie:"));Assert.That(passive,Does.Contain("szansy krytycznej"));
                var tab=(Transform)Field(view,"treeTabRoot");foreach(var button in tab.GetComponentsInChildren<Button>())
                    Assert.That(button.transform.Find("Label").GetSiblingIndex(),Is.EqualTo(button.transform.childCount-1),"State frames must not cover tab text.");
                var card=Card(view,node.Id);var neutral=((Image)Field(card,"rankAccent")).color;
                var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,pointerId=-1,position=Vector2.zero};
                card.OnPointerDown(pointer);Call(card,"AdvanceHold",.35f);
                Assert.That(character.AvailableSkillPoints,Is.EqualTo(2));
                var clip=(RectTransform)Field(card,"holdFillViewport");var full=((RectTransform)card.transform).rect.height;
                Assert.That(clip.rect.height/full,Is.EqualTo(.5f).Within(.01f));
                Assert.That(((Image)Field(card,"holdFillBorder")).rectTransform.rect.height,Is.EqualTo(full));
                card.OnPointerUp(pointer);Call(card,"AdvanceHold",1f);Assert.That(character.AvailableSkillPoints,Is.EqualTo(2));Assert.That(clip.rect.height,Is.Zero);
                card.OnPointerDown(pointer);Call(card,"AdvanceHold",.35f);card.OnPointerExit(pointer);Call(card,"AdvanceHold",1f);Assert.That(character.AvailableSkillPoints,Is.EqualTo(2));
                card.OnPointerDown(pointer);pointer.dragging=true;Call(card,"AdvanceHold",1f);pointer.dragging=false;Assert.That(character.AvailableSkillPoints,Is.EqualTo(2));
                card.OnPointerDown(pointer);pointer.scrollDelta=Vector2.up;ExecuteEvents.ExecuteHierarchy(card.gameObject,pointer,ExecuteEvents.scrollHandler);Call(card,"AdvanceHold",1f);pointer.scrollDelta=Vector2.zero;Assert.That(character.AvailableSkillPoints,Is.EqualTo(2));
                card.OnPointerDown(pointer);pointer.position=new Vector2(100,0);Call(card,"AdvanceHold",1f);pointer.position=Vector2.zero;Assert.That(character.AvailableSkillPoints,Is.EqualTo(2));
                card.OnPointerDown(pointer);Call(card,"OnApplicationFocus",false);Call(card,"AdvanceHold",1f);Assert.That(character.AvailableSkillPoints,Is.EqualTo(2));
                pointer.button=PointerEventData.InputButton.Right;card.OnPointerDown(pointer);Call(card,"AdvanceHold",1f);Assert.That(character.AvailableSkillPoints,Is.EqualTo(2));pointer.button=PointerEventData.InputButton.Left;
                var notifications=0;session.CharacterProgressionChanged+=_=>notifications++;
                card.OnPointerDown(pointer);Call(card,"AdvanceHold",.71f);Call(card,"AdvanceHold",2f);card.GetComponent<Button>().onClick.Invoke();
                Assert.That(character.AvailableSkillPoints,Is.EqualTo(1));Assert.That(notifications,Is.EqualTo(1));
                card=Card(view,node.Id);var partial=((Image)Field(card,"rankAccent")).color;
                Assert.That(partial,Is.Not.EqualTo(neutral));Assert.That(((Image)Field(card,"selectionBorder")).gameObject.activeSelf,Is.True);
                Assert.That(((TMP_Text)Field(view,"globalPassiveText")).text,Does.Contain($"Aktualnie: {SkillTreeProgressionPanelView.GetGlobalPassiveSummary(tree,1)}"));
                card.OnPointerDown(pointer);Call(card,"AdvanceHold",.71f);
                Assert.That(character.AvailableSkillPoints,Is.Zero);Assert.That(notifications,Is.EqualTo(2));
                card=Card(view,node.Id);var max=((Image)Field(card,"rankAccent")).color;
                Assert.That(max,Is.Not.EqualTo(partial));Assert.That(max.g,Is.GreaterThan(max.r));
                card.SetSelected(false);Assert.That(((Image)Field(card,"rankAccent")).color,Is.EqualTo(max));
                card.SetSelected(true);Assert.That(((Image)Field(card,"rankAccent")).color,Is.EqualTo(max));
                card.OnPointerDown(pointer);Call(card,"AdvanceHold",2f);Assert.That(notifications,Is.EqualTo(2));
                character.Level=CharacterProgressionSystem.MaximumLevel;session.NotifyCharacterProgressionChanged(character.CharacterId);
                Assert.That(((Image)Field(view,"experienceFill")).fillAmount,Is.EqualTo(1));Assert.That(((TMP_Text)Field(view,"experienceValueText")).text,Is.EqualTo("maksimum"));
                yield return null;
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(events);}
            yield return new ExitPlayMode();
        }

        private static SkillTreeNodeView HoldTestCard(Transform parent, CharacterInstance character,
            SkillTreeNodeDefinition node, System.Action onInvest)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab).GetComponent<SkillTreeProgressionPanelView>();
            var template = (Button)Field(prefab, "nodeTemplate");
            var card = Object.Instantiate(template, parent).GetComponent<SkillTreeNodeView>();
            card.gameObject.SetActive(true);
            card.Configure(node, 0, true, true, "", () => card.SetSelected(true), onInvest, character);
            return card;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void HoldAfterPreviousScrollWorksForPassiveAndActiveNodes(bool active)
        {
            var root = new GameObject("Hold canvas", typeof(RectTransform), typeof(Canvas));
            var events = new GameObject("Scrolled hold events", typeof(EventSystem));
            try
            {
                var character = Jerzy(); character.AvailableSkillPoints = 2;
                var tree = character.ClassData.SkillTrees[0];
                var node = tree.Nodes.First(n => n.Tier == 1 && n.Requirements.Count == 0
                    && (n.GrantedSkill != null) == active);
                var progression = new SkillTreeProgressionSystem();
                var card = HoldTestCard(root.transform, character, node,
                    () => progression.TryInvest(character, tree.Id, node.Id));
                Canvas.ForceUpdateCanvases();
                // InputSystemUIInputModule retains the previous scrollDelta even after scrolling stops.
                var pointer = new PointerEventData(EventSystem.current)
                {
                    button = PointerEventData.InputButton.Left, pointerId = -1, scrollDelta = Vector2.up
                };
                var hit = ((Image)Field(card, "icon")).gameObject;
                Assert.That(ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerDownHandler),
                    Is.EqualTo(card.gameObject));
                Call(card, "AdvanceHold", .35f);
                var clip = (RectTransform)Field(card, "holdFillViewport");
                Assert.That(clip.rect.height / ((RectTransform)card.transform).rect.height,
                    Is.EqualTo(.5f).Within(.01f), "An old scroll event must not cancel a new press.");
                Assert.That(((Image)Field(card, "holdFillBorder")).enabled, Is.EqualTo(!active));
                Assert.That(((SkillTreeActiveNodeFrame)Field(card, "activeHoldBorder")).enabled, Is.EqualTo(active));
                Assert.That(character.AvailableSkillPoints, Is.EqualTo(2));
                Call(card, "AdvanceHold", .36f);
                Call(card, "AdvanceHold", 2f);
                Assert.That(character.AvailableSkillPoints, Is.EqualTo(1));
                Assert.That(SkillTreeProgressionSystem.GetInvestedRanks(character, tree.Id), Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(events); }
        }

        [Test]
        public void ScrollingOverNodeCancelsHoldAndStillScrollsTree()
        {
            var root = new GameObject("Scroll hold canvas", typeof(RectTransform), typeof(Canvas));
            var events = new GameObject("Live scroll hold events", typeof(EventSystem));
            try
            {
                var character = Jerzy(); character.AvailableSkillPoints = 2;
                var tree = character.ClassData.SkillTrees[0];
                var node = tree.Nodes.First(n => n.Tier == 1 && n.Requirements.Count == 0);
                var scroll = new GameObject("Tree scroll", typeof(RectTransform), typeof(ScrollRect)).GetComponent<ScrollRect>();
                scroll.transform.SetParent(root.transform, false);
                var viewport = new GameObject("Viewport", typeof(RectTransform)).GetComponent<RectTransform>();
                viewport.SetParent(scroll.transform, false); viewport.sizeDelta = new Vector2(400, 300);
                var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
                content.SetParent(viewport, false); content.sizeDelta = new Vector2(400, 1300);
                scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
                var progression = new SkillTreeProgressionSystem();
                var card = HoldTestCard(content, character, node,
                    () => progression.TryInvest(character, tree.Id, node.Id));
                Canvas.ForceUpdateCanvases();
                scroll.Rebuild(CanvasUpdate.PostLayout);
                scroll.verticalNormalizedPosition = .5f;
                var pointer = new PointerEventData(EventSystem.current)
                    { button = PointerEventData.InputButton.Left, pointerId = -1 };
                card.OnPointerDown(pointer);
                Call(card, "AdvanceHold", .35f);
                var previousPosition = content.anchoredPosition;
                pointer.scrollDelta = Vector2.up;
                ExecuteEvents.ExecuteHierarchy(card.gameObject, pointer, ExecuteEvents.scrollHandler);
                Assert.That(content.anchoredPosition, Is.Not.EqualTo(previousPosition),
                    "Handling scroll on the node must preserve the parent ScrollRect's wheel input.");
                Call(card, "AdvanceHold", 1f);
                Assert.That(character.AvailableSkillPoints, Is.EqualTo(2));
                Assert.That(((RectTransform)Field(card, "holdFillViewport")).rect.height, Is.Zero);
                card.OnPointerUp(pointer);
                // Starting again must work without mutating the input module's retained event data.
                card.OnPointerDown(pointer);
                Call(card, "AdvanceHold", .71f);
                Assert.That(character.AvailableSkillPoints, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(events); }
        }

        [UnityTest]
        public IEnumerator HoldCompletionRechecksPointsAndClosingCancelsPurchase()
        {
            yield return new EnterPlayMode();var root=Workspace();var events=new GameObject("Cancel events",typeof(EventSystem));
            try
            {
                var character=Jerzy();character.AvailableSkillPoints=1;var session=Session(character);var tree=character.ClassData.SkillTrees[0];
                var node=tree.Nodes.First(n=>n.Tier==1 && n.Requirements.Count==0);
                var view=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab),root.transform).GetComponent<SkillTreeProgressionPanelView>();view.Open(session,character.CharacterId);
                var card=Card(view,node.Id);var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,pointerId=-1};
                card.OnPointerDown(pointer);character.AvailableSkillPoints=0;Call(card,"AdvanceHold",1f);
                Assert.That(SkillTreeProgressionSystem.GetInvestedRanks(character,tree.Id),Is.Zero);
                character.AvailableSkillPoints=1;session.NotifyCharacterProgressionChanged(character.CharacterId);card=Card(view,node.Id);
                card.OnPointerDown(pointer);Call(card,"AdvanceHold",.2f);view.Close();Call(card,"AdvanceHold",1f);
                Assert.That(character.AvailableSkillPoints,Is.EqualTo(1));Assert.That(SkillTreeProgressionSystem.GetInvestedRanks(character,tree.Id),Is.Zero);
                yield return null;
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(events);}
            yield return new ExitPlayMode();
        }
    }
}

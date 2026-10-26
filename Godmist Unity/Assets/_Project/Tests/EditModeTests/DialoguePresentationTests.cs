using System.Linq;
using System.Reflection;
using Godmist.Data.NPC;
using Godmist.Runtime.Game;
using Godmist.Runtime.NPC;
using Godmist.Systems.NPC;
using Godmist.Views.NPC;
using Godmist.Views.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Tests.EditMode
{
    public class DialoguePresentationTests
    {
        const string PanelPath="Assets/_Project/Prefabs/UI/Workspace/PF_UIW_Dialogue.prefab";
        const string ChoicePath="Assets/_Project/Prefabs/UI/PF_DialogueChoiceButton.prefab";
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static T Ref<T>(UnityEngine.Object owner,string field) where T:UnityEngine.Object
            =>new SerializedObject(owner).FindProperty(field).objectReferenceValue as T;
        static void Set(UnityEngine.Object owner,string field,UnityEngine.Object value)
        {var state=new SerializedObject(owner);state.FindProperty(field).objectReferenceValue=value;state.ApplyModifiedPropertiesWithoutUndo();}
        static GameObject CanvasRoot()
        {var root=new GameObject("Dialogue test canvas",typeof(RectTransform),typeof(Canvas));root.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;((RectTransform)root.transform).sizeDelta=new Vector2(1920,1080);return root;}
        static void Layout(NpcDialoguePanelView view)
        {for(var i=0;i<4;i++){Canvas.ForceUpdateCanvases();view.RebuildLayout();}}
        static NpcDefinition Guardian()=>AssetDatabase.LoadAssetAtPath<NpcDefinition>("Assets/_Project/ScriptableObjects/NPC/ND_Arungard_QuestGiver.asset");

        [TestCase("Priest", true)][TestCase("Blacksmith", true)][TestCase("Alchemist", true)]
        [TestCase("Enchanter", true)][TestCase("QuestGiver", false)]
        public void NpcActionsAreFirstAnswerWithoutChangingAuthoredChoiceRouting(string npcName, bool hasServices)
        {
            var root = CanvasRoot();
            try
            {
                var panel = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath), root.transform);
                var presentation = panel.GetComponent<NpcDialoguePanelView>();
                var frame = Ref<RectTransform>(presentation, "panelFrame");
                var content = Ref<RectTransform>(presentation, "conversationContent");
                var choices = content.Find("ChoicesContent");
                var owner = new GameObject("Controller"); owner.transform.SetParent(root.transform);
                var controller = owner.AddComponent<NpcDialogueController>();
                var legacy = frame.Find("TextColumn/ServicesRow/ServicesButton").GetComponent<Button>();
                var legacyMissions = frame.Find("TextColumn/ServicesRow/MissionBoardButton").GetComponent<Button>();
                legacyMissions.gameObject.SetActive(true);
                Set(controller, "panelRoot", panel); Set(controller, "dialoguePanel", presentation);
                Set(controller, "speakerText", frame.Find("TextColumn/SpeakerText").GetComponent<TextMeshProUGUI>());
                Set(controller, "dialogueText", content.Find("DialogueText").GetComponent<TextMeshProUGUI>());
                Set(controller, "choicesContent", choices);
                Set(controller, "choiceButtonPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(ChoicePath).GetComponent<DialogueChoiceButtonView>());
                Set(controller, "servicesButton", legacy);
                Set(controller, "missionBoardButton", legacyMissions);
                var definition = AssetDatabase.LoadAssetAtPath<NpcDefinition>("Assets/_Project/ScriptableObjects/NPC/ND_Arungard_" + npcName + ".asset");
                var service = new DialogueService();
                Assert.That(service.TryStartDialogue(new NpcInstance { NpcId = definition.Id, Definition = definition }, "initiator"), Is.True);
                var dialogue = service.ActiveDialogue; var original = dialogue.CurrentNode.Choices.ToArray();
                typeof(NpcDialogueController).GetField("gameSession", Private).SetValue(controller, new GameSession());
                typeof(NpcDialogueController).GetField("dialogueService", Private).SetValue(controller, service);
                typeof(NpcDialogueController).GetMethod("SetPanelVisible", Private).Invoke(controller, new object[] { true });
                typeof(NpcDialogueController).GetMethod("RefreshDialogue", Private).Invoke(controller, new object[] { dialogue });
                Layout(presentation);
                Assert.That(legacy.gameObject.activeSelf, Is.False);
                Assert.That(legacyMissions.gameObject.activeSelf, Is.False);
                Assert.That(legacy.transform.parent.gameObject.activeSelf, Is.False, "No reserved empty footer row.");
                var rows = choices.GetComponentsInChildren<DialogueChoiceButtonView>();
                var hasMissions = npcName == "QuestGiver";
                var actionCount = hasServices || hasMissions ? 1 : 0;
                Assert.That(rows.Length, Is.EqualTo(original.Length + actionCount));
                if (hasServices) Assert.That(Ref<TextMeshProUGUI>(rows[0], "label").text, Is.EqualTo("Chcę skorzystać z twoich usług"));
                if (hasMissions) Assert.That(Ref<TextMeshProUGUI>(rows[0], "label").text, Is.EqualTo("Masz dla mnie jakieś zlecenia?"));
                for (var i = 0; i < original.Length; i++)
                    Assert.That(Ref<TextMeshProUGUI>(rows[i + actionCount], "label").text, Is.EqualTo(original[i].DisplayText));
                Assert.That(dialogue.CurrentNode.Choices, Is.EqualTo(original), "Presentation must not insert into shared dialogue data.");
                DialogueChoiceData selected = null;
                service.DialogueChoiceSelected += (_, choice) => selected = choice;
                var authoredRow = rows[actionCount];
                typeof(DialogueChoiceButtonView).GetMethod("Awake", Private).Invoke(authoredRow, null);
                Ref<Button>(authoredRow, "button").onClick.Invoke();
                Assert.That(selected, Is.SameAs(original[0]), "Prepended service row must not shift authored choice indices.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test] public void ActionAnswerUsesSharedButtonAndRebindingReplacesItsCallback()
        {
            var root = CanvasRoot();
            try
            {
                var row = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ChoicePath), root.transform);
                var view = row.GetComponent<DialogueChoiceButtonView>();
                typeof(DialogueChoiceButtonView).GetMethod("Awake", Private).Invoke(view, null);
                var calls = 0; view.BindAction("Chcę skorzystać z twoich usług", () => calls++);
                Ref<Button>(view, "button").onClick.Invoke(); Assert.That(calls, Is.EqualTo(1));
                string selected = null; var choice = Guardian().BaseDialogue.FindNode(Guardian().BaseDialogue.StartNodeId).Choices[0];
                view.Bind(choice, id => selected = id); Ref<Button>(view, "button").onClick.Invoke();
                Assert.That(selected, Is.EqualTo(choice.ChoiceId)); Assert.That(calls, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase("QuestGiver")][TestCase("Alchemist")][TestCase("Blacksmith")][TestCase("Enchanter")][TestCase("Priest")]
        public void DialogueIllustrationIsSeparateFromWorldSpriteAndNativelyCropped(string npc)
        {
            var definition=AssetDatabase.LoadAssetAtPath<NpcDefinition>("Assets/_Project/ScriptableObjects/NPC/ND_Arungard_"+npc+".asset");
            Assert.That(definition.DialoguePortrait,Is.Not.Null);Assert.That(definition.Portrait,Is.Not.Null);
            Assert.That(definition.DialoguePortrait,Is.Not.SameAs(definition.Portrait));
            Assert.That(AssetDatabase.GetAssetPath(definition.Portrait),Does.Contain("NPC/VS22/"));
            Assert.That(definition.DialoguePortrait.rect.height,Is.InRange(200,600));
            Assert.That(definition.DialoguePortrait.rect.width,Is.LessThan(definition.DialoguePortrait.texture.width));
            Assert.That(definition.DialoguePortrait.texture.filterMode,Is.EqualTo(FilterMode.Point));
        }

        [Test] public void ShortDialogueFitsAndLongDialogueHasBoundedScrollableContent()
        {
            var root=CanvasRoot();
            try
            {
                var panel=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath),root.transform);panel.SetActive(true);
                var view=panel.GetComponent<NpcDialoguePanelView>();view.BindNpc(Guardian());
                var frame=Ref<RectTransform>(view,"panelFrame");var content=Ref<RectTransform>(view,"conversationContent");var scroll=Ref<ScrollRect>(view,"conversationScroll");
                var body=content.Find("DialogueText").GetComponent<TextMeshProUGUI>();body.text="Katakumby nie wybaczają nieostrożności.";
                var choices=content.Find("ChoicesContent");var template=AssetDatabase.LoadAssetAtPath<GameObject>(ChoicePath);
                var node=Guardian().BaseDialogue.FindNode(Guardian().BaseDialogue.StartNodeId);
                foreach(var choice in node.Choices){var row=Object.Instantiate(template,choices);row.GetComponent<DialogueChoiceButtonView>().Bind(choice,null);}
                view.RefreshLayout();Layout(view);var shortHeight=frame.rect.height;
                Assert.That(scroll.vertical,Is.False);Assert.That(body.isTextOverflowing,Is.False);
                var illustration=Ref<Image>(view,"npcIllustration");Assert.That(illustration.sprite,Is.SameAs(Guardian().DialoguePortrait));
                Assert.That(illustration.transform.IsChildOf(frame),Is.True,"The bust belongs inside the dialogue window.");
                var portraitCorners=new Vector3[4];var frameCorners=new Vector3[4];
                illustration.rectTransform.GetWorldCorners(portraitCorners);frame.GetWorldCorners(frameCorners);
                Assert.That(portraitCorners[1].y,Is.GreaterThan(frameCorners[1].y),"The portrait must protrude above the frame.");
                Assert.That(portraitCorners[0].y,Is.EqualTo(frameCorners[0].y+14).Within(.01f),"The portrait must meet the inside of the bottom border.");
                body.text=string.Join("\n\n",Enumerable.Repeat("Katakumby nie wybaczają nieostrożności. Trzymaj się światła i uważaj na każde przejście.",25));
                for(var i=0;i<8;i++)Object.Instantiate(template,choices).GetComponent<DialogueChoiceButtonView>().Bind(node.Choices[0],null);
                view.RefreshLayout();Layout(view);
                Assert.That(scroll.vertical,Is.True);Assert.That(frame.rect.height,Is.GreaterThan(shortHeight));
                Assert.That(frame.rect.height,Is.LessThanOrEqualTo(1080*.72f+1));
                Assert.That(content.rect.height,Is.GreaterThan(scroll.viewport.rect.height));
                scroll.verticalNormalizedPosition=0;view.RefreshLayout(false);Layout(view);
                Assert.That(scroll.verticalNormalizedPosition,Is.LessThan(.01f),"A routine layout refresh must not return the player to the top.");
            }finally{Object.DestroyImmediate(root);}
        }

        [Test] public void WrappedAnswerGrowsAndHoverAndKeyboardFocusUseReadableColors()
        {
            var root=CanvasRoot();
            try
            {
                var parent=new GameObject("Answers",typeof(RectTransform),typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();parent.SetParent(root.transform,false);parent.sizeDelta=new Vector2(330,600);
                var group=parent.GetComponent<VerticalLayoutGroup>();group.childControlHeight=group.childControlWidth=true;group.childForceExpandHeight=false;
                var row=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ChoicePath),parent);var view=row.GetComponent<DialogueChoiceButtonView>();
                var choice=new DialogueChoiceData();typeof(DialogueChoiceData).GetProperty("ChoiceId").SetValue(choice,"answer-id");
                typeof(DialogueChoiceData).GetProperty("DisplayText").SetValue(choice,"Chcę dowiedzieć się więcej o Katakumbach, zanim zdecyduję, czy moja drużyna powinna wyruszyć na tę wyprawę.");
                string selected=null;view.Bind(choice,id=>selected=id);
                for(var i=0;i<4;i++){Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate(parent);}
                var label=Ref<TextMeshProUGUI>(view,"label");var normal=label.color;
                Assert.That(((RectTransform)row.transform).rect.height,Is.GreaterThan(60));Assert.That(label.isTextOverflowing,Is.False);
                view.OnPointerEnter(null);Assert.That(label.color.r,Is.GreaterThan(normal.r));view.OnPointerExit(null);Assert.That(label.color,Is.EqualTo(normal));
                view.OnSelect(null);Assert.That(label.color,Is.Not.EqualTo(normal));view.OnDeselect(null);Assert.That(label.color,Is.EqualTo(normal));
                typeof(DialogueChoiceButtonView).GetMethod("Awake",Private).Invoke(view,null);Ref<Button>(view,"button").onClick.Invoke();Assert.That(selected,Is.EqualTo("answer-id"));
            }finally{Object.DestroyImmediate(root);}
        }

        [TestCase(true)][TestCase(false)]
        public void DialogueRestoresPreviousDrawerStateOnCloseAndDisable(bool expanded)
        {
            var root=CanvasRoot();
            try
            {
                var rail=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_PartyRail.prefab"),root.transform);
                var drawer=rail.GetComponentInChildren<CombatEdgeDrawer>(true);drawer.SetExpanded(expanded,true);var toggle=Ref<RectTransform>(drawer,"toggleRect").gameObject;
                var panel=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath),root.transform);
                var owner=new GameObject("Dialogue controller");owner.transform.SetParent(root.transform);var controller=owner.AddComponent<NpcDialogueController>();
                Set(controller,"panelRoot",panel);Set(controller,"partyDrawer",drawer);Set(controller,"partyDrawerToggle",toggle);
                var visibility=typeof(NpcDialogueController).GetMethod("SetPanelVisible",Private);
                visibility.Invoke(controller,new object[]{true});Assert.That(drawer.IsExpanded,Is.False);Assert.That(toggle.activeSelf,Is.False);
                visibility.Invoke(controller,new object[]{false});Assert.That(drawer.IsExpanded,Is.EqualTo(expanded));Assert.That(toggle.activeSelf,Is.True);
                visibility.Invoke(controller,new object[]{true});typeof(NpcDialogueController).GetMethod("OnDisable",Private).Invoke(controller,null);
                Assert.That(drawer.IsExpanded,Is.EqualTo(expanded));Assert.That(toggle.activeSelf,Is.True);
            }finally{Object.DestroyImmediate(root);}
        }

        [Test] public void SpeakerChangesToPartyMemberButLargeIllustrationRemainsNpc()
        {
            var root=CanvasRoot();
            try
            {
                var panel=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath),root.transform);panel.SetActive(true);
                var owner=new GameObject("Controller");owner.transform.SetParent(root.transform);var controller=owner.AddComponent<NpcDialogueController>();
                var definition=Guardian();var character=new Godmist.Systems.Characters.CharacterInstanceFactory().Create(AssetDatabase.LoadAssetAtPath<Godmist.Data.Characters.CharacterDefinition>("Assets/_Project/ScriptableObjects/Characters/CD_Jerzy.asset"));
                var game=new GameSession();game.TryRegisterCharacter(character);
                typeof(NpcDialogueController).GetField("gameSession",Private).SetValue(controller,game);
                var node=new DialogueNodeData();typeof(DialogueNodeData).GetProperty("SpeakerId").SetValue(node,character.CharacterId);
                var session=new DialogueSession(new NpcInstance{Definition=definition},definition.BaseDialogue,node,character.CharacterId);
                Assert.That(typeof(NpcDialogueController).GetMethod("GetSpeakerName",Private).Invoke(controller,new object[]{session}),Is.EqualTo(character.Definition.DisplayName));
                var view=panel.GetComponent<NpcDialoguePanelView>();view.BindNpc(session.Npc.Definition);
                Assert.That(Ref<Image>(view,"npcIllustration").sprite,Is.SameAs(definition.DialoguePortrait));
            }finally{Object.DestroyImmediate(root);}
        }
    }
}

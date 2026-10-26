using System;
using System.Linq;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Godmist.Editor
{
    /// <summary>Authors the shared journal from the same window, list and reward artwork as missions.</summary>
    public static class QuestJournalUiSetup
    {
        const string Folder = "Assets/_Project/Prefabs/UI/Workspace/";
        public const string WindowPath = Folder + "PF_UIW_QuestJournal.prefab";
        public const string RowPath = Folder + "PF_UIW_QuestJournalRow.prefab";
        public const string ObjectivePath = Folder + "PF_UIW_QuestJournalObjective.prefab";
        static UIWorkspaceStylePalette palette;
        static readonly string[] Scenes = { "Assets/_Project/Scenes/City/Arungard.unity", "Assets/_Project/Scenes/Sandbox/DungeonSandbox.unity" };

        [MenuItem("Godmist/Content/UI/Rebuild Quest Journal")]
        public static void Configure()
        {
            palette = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "PF_UIW_Window.prefab").GetComponent<UIWorkspaceStyleApplier>().Palette;
            BuildRow(); BuildObjective(); BuildWindow();
            foreach (var path in Scenes)
            {
                var scene = SceneManager.GetSceneByPath(path);
                var openedHere = !scene.isLoaded;
                if (openedHere) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                if (scene.isDirty) throw new InvalidOperationException("Dziennik: niezapisana scena " + path);
                try
                {
                    var owner = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<QuestJournalPanelView>(true)).Single();
                    var old = new SerializedObject(owner).FindProperty("panelRoot").objectReferenceValue as GameObject;
                    var parent = old.transform.parent;
                    var window = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(WindowPath), parent);
                    window.name = "QuestJournalPanel";
                    WireController(owner, window);
                    UnityEngine.Object.DestroyImmediate(old);
                    window.SetActive(false);
                    EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                }
                finally { if (openedHere) EditorSceneManager.CloseScene(scene, true); }
            }
            AssetDatabase.SaveAssets();
        }

        public static void WireController(QuestJournalPanelView owner, GameObject root)
        {
            var t = root.transform;
            void Ref(string field, string path, Type type) => Wire(owner, field, t.Find(path).GetComponent(type));
            Wire(owner, "panelRoot", root); Wire(owner, "window", root.GetComponent<UIWorkspaceWindow>());
            Wire(owner, "visibility", root.GetComponent<QuestJournalWindowVisibility>());
            Ref("closeButton", "Header/CloseButton", typeof(Button));
            Ref("activeTab", "Content/QuestList/Tabs/Active", typeof(Button)); Ref("completedTab", "Content/QuestList/Tabs/Completed", typeof(Button));
            Ref("activeTabText", "Content/QuestList/Tabs/Active/Label", typeof(TextMeshProUGUI)); Ref("completedTabText", "Content/QuestList/Tabs/Completed/Label", typeof(TextMeshProUGUI));
            foreach (var name in new[] { "Active", "Completed" })
            {
                Wire(owner, name == "Active" ? "activeListRoot" : "completedListRoot", t.Find("Content/QuestList/" + name + "Scroll").gameObject);
                Ref(name == "Active" ? "activeQuestsContent" : "completedQuestsContent", "Content/QuestList/" + name + "Scroll/Viewport/Content", typeof(RectTransform));
            }
            Ref("emptyListText", "Content/QuestList/Empty", typeof(TextMeshProUGUI));
            const string body = "Content/QuestDetails/DetailScroll/Viewport/Content/";
            Ref("titleText", body + "Title", typeof(TextMeshProUGUI)); Ref("metadataText", body + "Metadata", typeof(TextMeshProUGUI));
            Ref("descriptionText", body + "Description", typeof(TextMeshProUGUI)); Ref("stageText", body + "Stage/Description", typeof(TextMeshProUGUI));
            Wire(owner, "stageRoot", t.Find(body + "Stage").gameObject); Ref("objectivesText", body + "ObjectivesHeading", typeof(TextMeshProUGUI));
            Ref("objectivesContent", body + "Objectives", typeof(RectTransform));
            Ref("detailsScroll", "Content/QuestDetails/DetailScroll", typeof(ScrollRect));
            Wire(owner, "detailsContent", t.Find(body.TrimEnd('/')).gameObject);
            Wire(owner, "rewardsRoot", t.Find(body + "Rewards").gameObject);
            foreach (var name in new[] { "Experience", "Gold", "Honor" })
                Ref(name.ToLowerInvariant() + "Text", body + "Rewards/RewardAmounts/" + name + "/Amount", typeof(TextMeshProUGUI));
            Ref("rewardItem", body + "Rewards/RewardItem", typeof(MissionRewardItemView));
            Ref("trackButton", "Content/QuestDetails/TrackButton", typeof(Button)); Ref("trackButtonText", "Content/QuestDetails/TrackButton/Label", typeof(TextMeshProUGUI));
            Wire(owner, "questButtonPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(RowPath).GetComponent<QuestJournalQuestButtonView>());
            Wire(owner, "objectivePrefab", AssetDatabase.LoadAssetAtPath<GameObject>(ObjectivePath).GetComponent<QuestJournalObjectiveView>());
        }

        static void Wire(UnityEngine.Object owner, string field, UnityEngine.Object value)
        { var so = new SerializedObject(owner); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        static RectTransform Node(Transform parent, string name, params Type[] components)
        {
            var go = new GameObject(name, new[] { typeof(RectTransform) }.Concat(components).ToArray()); go.layer = 5; go.transform.SetParent(parent, false); return (RectTransform)go.transform;
        }
        static void Rect(Transform t, Vector2 min, Vector2 max, Vector2 low, Vector2 high)
        { var r = (RectTransform)t; r.anchorMin = min; r.anchorMax = max; r.offsetMin = low; r.offsetMax = high; }
        static void Height(GameObject go, float value)
        { var l = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>(); l.minHeight = l.preferredHeight = value; l.flexibleHeight = 0; }
        static TextMeshProUGUI Text(Transform parent, string name, int size, string value = "")
        {
            var t = Node(parent, name, typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            t.font = palette.Font; t.fontSize = size; t.color = QuestJournalPresentation.Cream; t.text = value; t.raycastTarget = false; t.enableAutoSizing = false; return t;
        }
        static void Group(GameObject go, int spacing)
        { var g = go.GetComponent<VerticalLayoutGroup>() ?? go.AddComponent<VerticalLayoutGroup>(); g.spacing = spacing; g.childControlHeight = g.childControlWidth = g.childForceExpandWidth = true; g.childForceExpandHeight = false; }
        static GameObject Instance(string name, Transform parent = null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + name + ".prefab"), parent);
            foreach (var s in go.GetComponentsInChildren<UIWorkspaceStyleApplier>(true)) s.enabled = false; return go;
        }
        static void ButtonArt(Button button, string prefix)
        {
            var image = button.GetComponent<Image>(); image.sprite = palette.GetArtwork(prefix + "_normal"); image.type = Image.Type.Sliced; image.color = Color.white;
            button.targetGraphic = image; button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState { highlightedSprite = palette.GetArtwork(prefix + "_hover"), pressedSprite = palette.GetArtwork(prefix + "_pressed"), selectedSprite = palette.GetArtwork(prefix + "_selected"), disabledSprite = palette.GetArtwork(prefix + "_disabled") };
        }
        static ScrollRect Scroll(Transform parent, string name)
        {
            var go = Instance("PF_UIW_ScrollView", parent); go.name = name; foreach (var image in go.GetComponents<Image>()) image.enabled = false;
            go.transform.Find("Border").gameObject.SetActive(false);
            var s = go.GetComponent<ScrollRect>(); s.horizontal = false; s.vertical = true; s.movementType = ScrollRect.MovementType.Clamped; s.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            Rect(s.content, new Vector2(0, 1), Vector2.one, Vector2.zero, Vector2.zero); s.content.pivot = new Vector2(.5f, 1);
            Group(s.content.gameObject, 14); var f = s.content.GetComponent<ContentSizeFitter>() ?? s.content.gameObject.AddComponent<ContentSizeFitter>(); f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return s;
        }
        [MenuItem("Godmist/Content/UI/Rebuild Quest Journal Rows")]
        public static void BuildRow()
        {
            palette = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "PF_UIW_Window.prefab").GetComponent<UIWorkspaceStyleApplier>().Palette;
            var root = Instance("PF_UIW_ListRow");
            try
            {
                var view = root.AddComponent<QuestJournalQuestButtonView>(); var button = root.GetComponent<Button>(); ButtonArt(button, "tab"); Height(root, 96);
                root.transform.Find("Icon").gameObject.SetActive(false);
                var level = Text(root.transform, "Level", 40, "1");
                Rect(level.transform, Vector2.zero, new Vector2(0, 1), new Vector2(10, 8), new Vector2(66, -8));
                level.alignment = TextAlignmentOptions.Center;
                Rect(root.transform.Find("TextRoot"), Vector2.zero, Vector2.one, new Vector2(76, 8), new Vector2(-28, -8));
                var title = root.transform.Find("TextRoot/Title").GetComponent<TextMeshProUGUI>(); title.fontSize = 21; title.color = QuestJournalPresentation.Cream;
                var detail = root.transform.Find("TextRoot/Detail").GetComponent<TextMeshProUGUI>(); detail.fontSize = 17; detail.color = QuestJournalPresentation.Cream;
                Rect(title.transform, new Vector2(0, .35f), Vector2.one, Vector2.zero, Vector2.zero); Rect(detail.transform, Vector2.zero, new Vector2(1, .35f), Vector2.zero, Vector2.zero);
                var tracked = Node(root.transform, "Tracked", typeof(SkillTreeRankMarkers)); Rect(tracked, new Vector2(1, 1), Vector2.one, new Vector2(-30, -30), new Vector2(-10, -10));
                tracked.GetComponent<SkillTreeRankMarkers>().SetRanks(1, 1, new Color32(225, 182, 61, 255), Color.white); tracked.gameObject.SetActive(false);
                Wire(view, "button", button); Wire(view, "background", root.GetComponent<Image>()); Wire(view, "titleText", title); Wire(view, "statusText", detail); Wire(view, "levelText", level); Wire(view, "selectionFrame", root.transform.Find("Border").GetComponent<Image>()); Wire(view, "palette", palette); Wire(view, "trackedMarker", tracked.gameObject);
                PrefabUtility.SaveAsPrefabAsset(root, RowPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        static void BuildObjective()
        {
            var root = Node(null, "QuestObjective", typeof(QuestJournalObjectiveView), typeof(LayoutElement));
            try
            {
                Height(root.gameObject, 52);
                var text = Text(root, "Description", 21); Rect(text.transform, Vector2.zero, Vector2.one, new Vector2(26, 25), new Vector2(-90, 0)); text.alignment = TextAlignmentOptions.TopLeft;
                var counter = Text(root, "Counter", 19); Rect(counter.transform, new Vector2(1, 0), Vector2.one, new Vector2(-82, 25), Vector2.zero); counter.alignment = TextAlignmentOptions.TopRight;
                var marker = Node(root, "Complete", typeof(SkillTreeRankMarkers)); Rect(marker, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -24), new Vector2(18, -6));
                var track = Node(root, "Track", typeof(Image)); Rect(track, Vector2.zero, new Vector2(1, 0), new Vector2(26, 0), new Vector2(0, 10)); track.GetComponent<Image>().color = new Color(.14f, .14f, .16f, 1); track.GetComponent<Image>().raycastTarget = false;
                var fill = Node(track, "Fill", typeof(Image)); Rect(fill, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); var img = fill.GetComponent<Image>(); img.sprite = palette.GetArtwork("fill_runtime"); img.type = Image.Type.Filled; img.fillMethod = Image.FillMethod.Horizontal; img.raycastTarget = false;
                var view = root.GetComponent<QuestJournalObjectiveView>(); Wire(view, "description", text); Wire(view, "counter", counter); Wire(view, "progressFill", img); Wire(view, "completionMarker", marker.GetComponent<SkillTreeRankMarkers>()); Wire(view, "layout", root.GetComponent<LayoutElement>());
                PrefabUtility.SaveAsPrefabAsset(root.gameObject, ObjectivePath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root.gameObject); }
        }
        static void BuildWindow()
        {
            var root = Instance("PF_UIW_Window");
            try
            {
                root.AddComponent<QuestJournalWindowVisibility>(); var r = (RectTransform)root.transform; r.sizeDelta = new Vector2(1080, 680);
                var ws = new SerializedObject(root.GetComponent<UIWorkspaceWindow>()); ws.FindProperty("minimumSize").vector2Value = new Vector2(850, 570); ws.FindProperty("maximumSize").vector2Value = new Vector2(1400, 900); ws.ApplyModifiedPropertiesWithoutUndo();
                var header = root.transform.Find("Header"); var title = header.Find("Title").GetComponent<TextMeshProUGUI>(); title.text = "Dziennik zadań"; title.fontSize = 28; title.color = QuestJournalPresentation.Cream; Rect(title.transform, Vector2.zero, Vector2.one, new Vector2(22, 6), new Vector2(-66, -6));
                var close = header.GetComponentInChildren<Button>(true); close.name = "CloseButton"; ButtonArt(close, "icon_button"); var cr = (RectTransform)close.transform; cr.anchorMin = cr.anchorMax = Vector2.one; cr.pivot = Vector2.one; cr.sizeDelta = new Vector2(44, 44); cr.anchoredPosition = new Vector2(-7, -6);
                var content = root.transform.Find("Content"); var left = Node(content, "QuestList"); Rect(left, Vector2.zero, new Vector2(.30f, 1), new Vector2(12, 14), new Vector2(-14, -10));
                var tabs = Node(left, "Tabs", typeof(HorizontalLayoutGroup)); Rect(tabs, new Vector2(0, 1), Vector2.one, new Vector2(0, -50), Vector2.zero); var hg = tabs.GetComponent<HorizontalLayoutGroup>(); hg.spacing = 6; hg.childControlHeight = hg.childControlWidth = hg.childForceExpandWidth = hg.childForceExpandHeight = true;
                foreach (var pair in new[] { ("Active", "Aktywne"), ("Completed", "Zakończone") })
                { var b = Instance("PF_UIW_Button", tabs); b.name = pair.Item1; ButtonArt(b.GetComponent<Button>(), "tab"); b.AddComponent<LayoutElement>().flexibleWidth = 1; var label = b.transform.Find("Label").GetComponent<TextMeshProUGUI>(); label.text = pair.Item2; label.fontSize = 18; label.color = QuestJournalPresentation.Cream; label.enableAutoSizing = false; Rect(label.transform, Vector2.zero, Vector2.one, new Vector2(4, 6), new Vector2(-4, -6)); b.transform.Find("Border").gameObject.SetActive(false); }
                foreach (var name in new[] { "Active", "Completed" }) { var s = Scroll(left, name + "Scroll"); Rect(s.transform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -66)); }
                var empty = Text(left, "Empty", 18); Rect(empty.transform, Vector2.zero, Vector2.one, new Vector2(8, 0), new Vector2(-8, -80));
                var divider = Node(content, "Separator", typeof(Image)); Rect(divider, new Vector2(.31f, 0), new Vector2(.31f, 1), new Vector2(0, 16), new Vector2(1, -12)); divider.GetComponent<Image>().color = new Color(.6f, .59f, .47f, .4f); divider.GetComponent<Image>().raycastTarget = false;
                var right = Node(content, "QuestDetails"); Rect(right, new Vector2(.32f, 0), Vector2.one, new Vector2(12, 14), new Vector2(-16, -10));
                var scroll = Scroll(right, "DetailScroll"); Rect(scroll.transform, Vector2.zero, Vector2.one, new Vector2(0, 94), Vector2.zero); var body = scroll.content;
                Text(body, "Title", 30); Text(body, "Metadata", 19); var line = Node(body, "TitleDivider", typeof(Image)); Height(line.gameObject, 1); line.GetComponent<Image>().color = new Color(.6f, .59f, .47f, .4f); line.GetComponent<Image>().raycastTarget = false;
                Text(body, "Description", 18); var stage = Node(body, "Stage"); Group(stage.gameObject, 8); Text(stage, "Heading", 23, "Aktualny etap"); Text(stage, "Description", 21);
                Text(body, "ObjectivesHeading", 23, "Cele"); var objectives = Node(body, "Objectives"); Group(objectives.gameObject, 16);
                var mission = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "PF_UIW_CatacombsMissions.prefab");
                var rewards = UnityEngine.Object.Instantiate(mission.transform.Find("Content/MissionDetailsSection/Content/MissionDetailScroll/Viewport/Content/MissionRewards").gameObject, body, false); rewards.name = "Rewards";
                var button = Instance("PF_UIW_Button", right); button.name = "TrackButton"; ButtonArt(button.GetComponent<Button>(), "button"); var br = (RectTransform)button.transform; br.anchorMin = br.anchorMax = new Vector2(1, 0); br.pivot = new Vector2(1, 0); br.sizeDelta = new Vector2(320, 50); br.anchoredPosition = Vector2.zero;
                var labelTrack = button.transform.Find("Label").GetComponent<TextMeshProUGUI>(); labelTrack.fontSize = 23; labelTrack.color = QuestJournalPresentation.Cream; labelTrack.text = "Śledź zadanie"; button.transform.Find("Border").gameObject.SetActive(false);
                root.SetActive(false); PrefabUtility.SaveAsPrefabAsset(root, WindowPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}

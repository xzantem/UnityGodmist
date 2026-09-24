using System;
using Godmist.Views.NPC;
using Godmist.Views.UI.Workspace;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Godmist.Editor
{
    /// <summary>Creates the mission board from the shared UI Workspace prefabs.</summary>
    public static class VS17CatacombsMissionUiSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/City/Arungard.unity";
        private const string PrefabPath = "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CatacombsMissions.prefab";
        private const string WindowPath = "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_Window.prefab";
        private const string SectionPath = "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_Section.prefab";
        private const string ScrollPath = "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_ScrollView.prefab";
        private const string RowPath = "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_ListRow.prefab";
        private const string ButtonPath = "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_Button.prefab";

        [MenuItem("Godmist/Content/VS-17/Configure Catacombs Mission UI")]
        public static void Configure()
        {
            var prefab = BuildPrefab();
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            if (scene.isDirty)
                throw new InvalidOperationException("VS-17: scena Arungard ma niezapisane zmiany.");

            try
            {
                var dialogue = FindInScene<NpcDialogueController>(scene);
                if (dialogue == null)
                    throw new InvalidOperationException("VS-17: brak kontrolera dialogu w Arungard.");
                var so = new SerializedObject(dialogue);
                var source = so.FindProperty("servicesButton").objectReferenceValue as Button;
                if (source == null)
                    throw new InvalidOperationException("VS-17: brak przycisku usług.");
                var missionButton = so.FindProperty("missionBoardButton").objectReferenceValue as Button;
                if (missionButton == null)
                {
                    var buttonObject = UnityEngine.Object.Instantiate(source.gameObject, source.transform.parent);
                    buttonObject.name = "MissionBoardButton";
                    missionButton = buttonObject.GetComponent<Button>();
                    missionButton.onClick.RemoveAllListeners();
                    var rect = buttonObject.GetComponent<RectTransform>();
                    rect.anchoredPosition = source.GetComponent<RectTransform>().anchoredPosition
                        + new Vector2(-380f, 0f);
                    foreach (var label in buttonObject.GetComponentsInChildren<TextMeshProUGUI>(true))
                        label.text = "Zlecenia";
                }
                so.FindProperty("missionBoardButton").objectReferenceValue = missionButton;
                so.FindProperty("missionBoardPrefab").objectReferenceValue = prefab;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(dialogue);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("VS-17: zapisano okno UIW zleceń i przycisk u strażnika.");
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [MenuItem("Godmist/Content/VS-17/Rebuild Mission Window UIW")]
        public static void RepairLayout()
        {
            BuildPrefab();
            Debug.Log("VS-17: przebudowano okno zleceń z prefabów UIW.");
        }

        private static CatacombsMissionBoardView BuildPrefab()
        {
            var root = Instance(WindowPath);
            root.name = "PF_UIW_CatacombsMissions";
            try
            {
                var rootRect = root.GetComponent<RectTransform>();
                rootRect.sizeDelta = new Vector2(900f, 560f);
                var window = root.GetComponent<UIWorkspaceWindow>();
                var windowSo = new SerializedObject(window);
                windowSo.FindProperty("minimumSize").vector2Value = new Vector2(700f, 450f);
                windowSo.FindProperty("maximumSize").vector2Value = new Vector2(1200f, 820f);
                windowSo.ApplyModifiedPropertiesWithoutUndo();

                var header = RequiredChild(root.transform, "Header");
                RequiredChild(header, "Title").GetComponent<TextMeshProUGUI>().text = "Zlecenia Katakumb";
                var content = RequiredChild(root.transform, "Content");

                var listSection = Instance(SectionPath, content);
                listSection.name = "MissionListSection";
                SetRect(listSection.transform, 0f, 0f, .32f, 1f);
                RequiredChild(listSection.transform, "SectionTitle")
                    .GetComponent<TextMeshProUGUI>().text = "Zlecenia";
                var listContent = RequiredChild(listSection.transform, "Content");
                var scroll = Instance(ScrollPath, listContent);
                scroll.name = "MissionListScroll";
                SetRect(scroll.transform, 0f, 0f, 1f, 1f);
                var scrollRect = scroll.GetComponent<ScrollRect>();
                var list = scrollRect.content;
                var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.childAlignment = TextAnchor.UpperLeft;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
                layout.spacing = 6f;
                layout.padding = new RectOffset(2, 2, 2, 2);
                var fitter = list.gameObject.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                var row = Instance(RowPath, list);
                row.name = "MissionRowTemplate";
                RequiredChild(row.transform, "Icon").gameObject.SetActive(false);
                var rowText = RequiredChild(row.transform, "TextRoot").GetComponent<RectTransform>();
                rowText.anchoredPosition = Vector2.zero;
                rowText.sizeDelta = new Vector2(-24f, -12f);
                foreach (var label in row.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    label.enableWordWrapping = false;
                    label.overflowMode = TextOverflowModes.Ellipsis;
                }
                row.SetActive(false);

                var detailsSection = Instance(SectionPath, content);
                detailsSection.name = "MissionDetailsSection";
                SetRect(detailsSection.transform, .34f, 0f, 1f, 1f);
                RequiredChild(detailsSection.transform, "SectionTitle")
                    .GetComponent<TextMeshProUGUI>().text = "Szczegóły zlecenia";
                var detailContent = RequiredChild(detailsSection.transform, "Content");
                var palette = root.GetComponent<UIWorkspaceStyleApplier>().Palette;
                var title = CreateText(detailContent, "MissionTitle", .02f, .83f, .98f, .99f,
                    UIWorkspaceTextStyle.Heading, palette);
                var description = CreateText(detailContent, "MissionDescription", .02f, .51f, .98f, .80f,
                    UIWorkspaceTextStyle.Body, palette);
                var reward = CreateText(detailContent, "MissionReward", .02f, .22f, .98f, .48f,
                    UIWorkspaceTextStyle.Body, palette);
                var message = CreateText(detailContent, "MissionMessage", .02f, .15f, .98f, .20f,
                    UIWorkspaceTextStyle.Detail, palette);
                var actionObject = Instance(ButtonPath, detailContent);
                actionObject.name = "MissionActionButton";
                SetRect(actionObject.transform, .24f, .015f, .76f, .125f);
                var action = actionObject.GetComponent<Button>();
                var actionLabel = RequiredChild(actionObject.transform, "Label")
                    .GetComponent<TextMeshProUGUI>();
                actionLabel.text = "Przyjmij zlecenie";

                var board = root.AddComponent<CatacombsMissionBoardView>();
                var so = new SerializedObject(board);
                Set(so, "window", window);
                Set(so, "questList", list);
                Set(so, "questButtonTemplate", row.GetComponent<Button>());
                Set(so, "titleText", title);
                Set(so, "detailsText", description);
                Set(so, "rewardText", reward);
                Set(so, "messageText", message);
                Set(so, "actionButton", action);
                Set(so, "actionButtonText", actionLabel);
                so.ApplyModifiedPropertiesWithoutUndo();
                root.SetActive(false);

                var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                return saved != null ? saved.GetComponent<CatacombsMissionBoardView>()
                    : throw new InvalidOperationException("VS-17: nie zapisano prefabu UIW zleceń.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name,
            float left, float bottom, float right, float top,
            UIWorkspaceTextStyle style, UIWorkspaceStylePalette palette)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(TextMeshProUGUI), typeof(UIWorkspaceStyleApplier));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            SetRect(go.transform, left, bottom, right, top);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.text = string.Empty;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            go.GetComponent<UIWorkspaceStyleApplier>().ConfigureRuntime(
                palette, UIWorkspaceSurfaceStyle.None, UIWorkspaceAccentStyle.None,
                style, UIWorkspaceAccentStyle.None, nextText: text);
            return text;
        }

        private static GameObject Instance(string path, Transform parent = null)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) throw new InvalidOperationException("VS-17: brak prefabu " + path);
            return (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        }

        private static Transform RequiredChild(Transform parent, string name) =>
            parent.Find(name) ?? throw new InvalidOperationException("VS-17: brak " + name);

        private static void SetRect(Transform transform, float left, float bottom, float right, float top)
        {
            var rect = transform.GetComponent<RectTransform>();
            if (rect == null) throw new InvalidOperationException("VS-17: brak RectTransform na " + transform.name);
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Set(SerializedObject target, string field, UnityEngine.Object value)
        {
            var property = target.FindProperty(field)
                ?? throw new InvalidOperationException("VS-17: brak pola " + field);
            property.objectReferenceValue = value;
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }
    }
}

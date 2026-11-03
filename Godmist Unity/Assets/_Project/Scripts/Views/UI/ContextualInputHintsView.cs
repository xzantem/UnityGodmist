using Godmist.Core.Enums;
using Godmist.Views.Game;
using Godmist.Views.UI.Workspace;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI
{
    /// <summary>Only the persistent keyboard shortcuts for opening player windows.</summary>
    public sealed class ContextualInputHintsView : MonoBehaviour
    {
        private TextMeshProUGUI text;
        private RectTransform row;
        private GameObject journal;
        private Sprite slotFrame;
        private CharacterInfoPartyRailView partyRail;
        private QuestJournalPanelView questJournal;
        private Button inventoryButton, journalButton;

        public void BindPartyRail(CharacterInfoPartyRailView view) => partyRail = view;
        public void BindJournal(QuestJournalPanelView view) => questJournal = view;

        public static ContextualInputHintsView Ensure(Canvas canvas)
        {
            if (canvas == null) return null;
            canvas = canvas.rootCanvas;
            var view = canvas.GetComponent<ContextualInputHintsView>() ?? canvas.gameObject.AddComponent<ContextualInputHintsView>();
            view.CreateRow(canvas);
            return view;
        }

        private void Awake() => CreateRow(GetComponent<Canvas>());

        private void CreateRow(Canvas canvas)
        {
            if (row != null || canvas == null) return;
            var style = System.Array.Find(canvas.GetComponentsInChildren<TextMeshProUGUI>(true), label => label.font != null);
            var frameTexture = Resources.Load<Texture2D>("Godmist/hud-shortcut-frame");
            if (frameTexture != null)
            {
                frameTexture.filterMode = FilterMode.Point;
                slotFrame = Sprite.Create(frameTexture, new Rect(0, 0, frameTexture.width, frameTexture.height), Vector2.one * .5f);
            }
            row = new GameObject("HUD Window Shortcuts", typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(canvas.transform, false);
            row.anchorMin = row.anchorMax = row.pivot = new Vector2(1f, 0f);
            row.anchoredPosition = new Vector2(-28f, 24f);
            row.sizeDelta = new Vector2(82f, 102f);

            inventoryButton = CreateShortcut("I", "hud-inventory-32", 54f, style, out text);
            inventoryButton.onClick.AddListener(() => partyRail?.TogglePartyView());
            journalButton = CreateShortcut("J", "hud-journal-32", 0f, style, out _);
            journalButton.onClick.AddListener(() => questJournal?.Toggle());
            journal = journalButton.gameObject;
            row.gameObject.SetActive(false);
        }

        private Button CreateShortcut(string key, string resource, float y, TMP_Text style, out TextMeshProUGUI label)
        {
            var group = CreateRect(row, key, new Vector2(0f, y), new Vector2(82f, 48f));
            var hitArea = group.gameObject.AddComponent<Image>();
            hitArea.color = Color.clear;
            var button = group.gameObject.AddComponent<Button>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            CreateFrame(group, "Key Frame", new Vector2(-54f, 10f), 28f);
            var backing = CreateRect(group, "Icon Background", new Vector2(-4f, 4f), new Vector2(40f, 40f))
                .gameObject.AddComponent<Image>();
            backing.color = new Color(.09f, .085f, .11f);
            backing.raycastTarget = false;
            button.targetGraphic = backing;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.4f, 1.4f, 1.4f);
            colors.pressedColor = new Color(.7f, .7f, .7f);
            button.colors = colors;
            CreateFrame(group, "Icon Frame", Vector2.zero, 48f);
            label = CreateRect(group, "Key", new Vector2(-54f, 10f), new Vector2(28f, 28f)).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = style != null ? style.font : TMP_Settings.defaultFontAsset;
            if (style != null) label.fontSharedMaterial = style.fontSharedMaterial ?? style.font.material;
            label.text = key;
            label.fontSize = 21f;
            label.color = new Color(.93f, .88f, .72f);
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;

            var icon = CreateRect(group, "Icon", new Vector2(-8f, 8f), new Vector2(32f, 32f)).gameObject.AddComponent<RawImage>();
            icon.texture = Resources.Load<Texture2D>("Godmist/" + resource);
            icon.enabled = icon.texture != null;
            if (icon.texture != null) icon.texture.filterMode = FilterMode.Point;
            icon.raycastTarget = false;
            return button;
        }

        private void CreateFrame(RectTransform parent, string name, Vector2 position, float size)
        {
            var frame = CreateRect(parent, name, position, Vector2.one * size).gameObject.AddComponent<Image>();
            frame.raycastTarget = false;
            frame.sprite = slotFrame;
            frame.enabled = slotFrame != null;
        }

        private void OnDestroy()
        {
            if (slotFrame == null) return;
            if (Application.isPlaying) Destroy(slotFrame);
            else DestroyImmediate(slotFrame);
        }

        private static RectTransform CreateRect(RectTransform parent, string name, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private void Update()
        {
            if (row == null) return;
            var mode = GameBootstrap.Instance?.Session?.CurrentMode;
            var show = (mode is GameModeState.Exploration or GameModeState.Combat)
                && !ControllerInput.Active && !DevConsoleView.IsOpen
                && !UIWorkspaceInputRouter.HasFocusedTextInput()
                && !UIWorkspaceInputRouter.HasFocusedContextMenu()
                && UIWorkspaceWindowManager.Instance?.HasOpenWindows != true;
            if (row.gameObject.activeSelf != show) row.gameObject.SetActive(show);
            if (!show) return;
            var showJournal = questJournal != null && GameBootstrap.Instance.QuestService != null;
            if (inventoryButton.gameObject.activeSelf != (partyRail != null)) inventoryButton.gameObject.SetActive(partyRail != null);
            if (journal.activeSelf != showJournal) journal.SetActive(showJournal);
            var inventory = (RectTransform)text.transform.parent;
            inventory.anchoredPosition = new Vector2(0f, showJournal ? 54f : 0f);
        }
    }
}

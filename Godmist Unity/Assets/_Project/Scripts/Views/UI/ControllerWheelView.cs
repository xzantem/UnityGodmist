using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Godmist.Views.UI
{
    /// <summary>Controller-only radial presentation of existing actions. Never executes on trigger release.</summary>
    public sealed class ControllerWheelView : MonoBehaviour
    {
        public sealed class Entry
        {
            public readonly string Id, Label, Reason;
            public readonly Sprite Icon;
            public readonly Func<bool> Available;
            public readonly Action Execute, Inspect;
            public Entry(string id, string label, Action execute, Func<bool> available = null, string reason = null, Sprite icon = null, Action inspect = null)
            { Id = id; Label = label; Execute = execute; Available = available; Reason = reason; Icon = icon; Inspect = inspect; }
            public bool Enabled => Available?.Invoke() ?? true;
        }
        private const int PageSize = 8;
        private readonly List<Button> buttons = new();
        private readonly List<Image> icons = new();
        private IReadOnlyList<Entry> actions;
        private Button template;
        private Image frameTemplate;
        private TMP_Text title, detail, footer;
        private ControllerNavigationScope scope;
        private RectTransform panel;
        private int selected, page, openedFrame;
        private string heading;
        private Action cancel;
        public string SelectedId => actions != null && selected >= 0 && selected < actions.Count ? actions[selected].Id : null;
        public bool IsOpen => gameObject.activeSelf;
        public int PageEntryCount => Mathf.Min(PageSize,(actions?.Count ?? 0)-page*PageSize);
        public Action SelectionChanged;
        public Vector2 SelectedScreenPosition => buttons.Count > selected % PageSize
            ? RectTransformUtility.WorldToScreenPoint(null, buttons[selected % PageSize].transform.position) : Vector2.zero;
        private void OnDisable() => SelectionChanged?.Invoke();

        public void Configure(Button buttonTemplate, TMP_Text headingTemplate, Image frame)
        {
            template = buttonTemplate; frameTemplate = frame;
            var rect = (RectTransform)transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            gameObject.AddComponent<CanvasRenderer>();
            var shade = gameObject.AddComponent<Image>(); shade.color = new Color(0, 0, 0, .48f);
            panel = new GameObject("Wheel", typeof(RectTransform), typeof(CanvasRenderer)).GetComponent<RectTransform>();
            panel.SetParent(transform, false); panel.sizeDelta = new Vector2(700, 620);
            var ring = panel.gameObject.AddComponent<ControllerWheelGraphic>();
            ring.color = new Color(.12f, .11f, .14f, .98f); ring.raycastTarget = false;
            title = Label("Title", headingTemplate, new Vector2(0, 48), new Vector2(240, 76));
            detail = Label("Selection", headingTemplate, new Vector2(0, -25), new Vector2(245, 115));
            detail.fontSize = 18;
            footer = Label("Controls", headingTemplate, new Vector2(0, -290), new Vector2(660, 50)); footer.fontSize = 17;
            scope = gameObject.AddComponent<ControllerNavigationScope>();
            scope.CancelAction.AddListener(() => cancel?.Invoke());
        }
        private TMP_Text Label(string name, TMP_Text source, Vector2 at, Vector2 size)
        {
            var label = Instantiate(source, panel); label.name = name; label.gameObject.SetActive(true);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = label.rectTransform.pivot = new Vector2(.5f, .5f);
            label.rectTransform.anchoredPosition = at; label.rectTransform.sizeDelta = size;
            label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal; label.fontSize = 22;
            var fitter = label.GetComponent<ContentSizeFitter>(); if (fitter != null) fitter.enabled = false;
            return label;
        }
        public void Show(string name, IReadOnlyList<Entry> entries, string preferred, Action back)
        {
            if (entries == null || entries.Count == 0) return;
            heading = name; actions = entries; cancel = back; selected = 0;
            for (var i = 0; i < entries.Count; i++) if (entries[i].Id == preferred) selected = i;
            page = selected / PageSize; openedFrame = Time.frameCount;
            gameObject.SetActive(true); transform.SetAsLastSibling(); Populate();
        }
        public void Hide() => gameObject.SetActive(false);
        private void Populate()
        {
            while (buttons.Count < PageSize)
            {
                var button = Instantiate(template, panel); button.gameObject.SetActive(false);
                var fitter = button.GetComponent<ContentSizeFitter>(); if (fitter != null) fitter.enabled = false;
                if (frameTemplate != null)
                {
                    var border = new GameObject("Radial Entry Frame",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image)).GetComponent<Image>();
                    border.transform.SetParent(button.transform,false);
                    border.sprite = frameTemplate.sprite; border.type = frameTemplate.type;
                    border.color = frameTemplate.color; border.material = frameTemplate.material;
                    border.pixelsPerUnitMultiplier = frameTemplate.pixelsPerUnitMultiplier;
                    border.raycastTarget = false; border.fillCenter = false;
                    border.rectTransform.anchorMin = Vector2.zero; border.rectTransform.anchorMax = Vector2.one;
                    border.rectTransform.offsetMin = border.rectTransform.offsetMax = Vector2.zero;
                    border.gameObject.SetActive(true);
                }
                var icon = new GameObject("Action Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
                icon.transform.SetParent(button.transform, false); icon.raycastTarget = false; icon.preserveAspect = true;
                icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(.5f, 1);
                icon.rectTransform.pivot = new Vector2(.5f, 1); icon.rectTransform.anchoredPosition = new Vector2(0, -6);
                icon.rectTransform.sizeDelta = new Vector2(36, 36); icons.Add(icon); buttons.Add(button);
            }
            for (var i = 0; i < PageSize; i++)
            {
                var index = page * PageSize + i; var button = buttons[i];
                button.gameObject.SetActive(index < actions.Count); if (index >= actions.Count) continue;
                var entry = actions[index];
                var angle = i * Mathf.PI * 2 / Mathf.Max(1,PageEntryCount);
                var rect = (RectTransform)button.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                rect.anchoredPosition = new Vector2(Mathf.Sin(angle) * 250, Mathf.Cos(angle) * 210);
                rect.sizeDelta = new Vector2(175, 92);
                if (button.targetGraphic != null) button.targetGraphic.color = Color.white;
                var colors = button.colors;
                colors.normalColor = new Color(.14f,.13f,.15f,1); colors.highlightedColor = new Color(.3f,.27f,.2f,1);
                colors.selectedColor = new Color(.3f,.27f,.2f,1); colors.pressedColor = new Color(.4f,.35f,.22f,1);
                button.colors = colors;
                button.interactable = true; // Disabled actions remain inspectable, guarded on execution.
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                var text = button.GetComponentInChildren<TMP_Text>(true); text.text = entry.Label; text.fontSize = 17;
                text.alignment = TextAlignmentOptions.Center; text.textWrappingMode = TextWrappingModes.Normal;
                text.enableAutoSizing = true; text.fontSizeMin = 14; text.fontSizeMax = 17;
                text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
                text.rectTransform.offsetMin = new Vector2(5, 5); text.rectTransform.offsetMax = new Vector2(-5, entry.Icon != null ? -43 : -5);
                icons[i].sprite = entry.Icon; icons[i].enabled = entry.Icon != null;
                button.onClick.RemoveAllListeners(); button.onClick.AddListener(() => { selected = index; Confirm(); });
            }
            Refresh();
        }
        private void Refresh()
        {
            title.text = heading;
            var entry = actions[selected];
            detail.text = entry.Label + (entry.Enabled ? string.Empty : "\n<color=#C59A87>" + (entry.Reason ?? "Teraz niedostępne") + "</color>");
            footer.text = "A — wybierz   B — wróć" + (actions[selected].Inspect != null ? "   Y — szczegóły" : "") + (actions.Count > PageSize ? $"   LB / RB — strona {page + 1}/{Mathf.CeilToInt(actions.Count / (float)PageSize)}" : "");
            for (var i = 0; i < PageSize && page * PageSize + i < actions.Count; i++)
            {
                var entryAt = actions[page * PageSize + i];
                buttons[i].GetComponentInChildren<TMP_Text>(true).color = entryAt.Enabled ? new Color(.93f,.88f,.72f) : new Color(.5f,.48f,.43f);
                icons[i].color = entryAt.Enabled ? Color.white : new Color(.4f,.4f,.4f);
            }
            EventSystem.current?.SetSelectedGameObject(buttons[selected % PageSize].gameObject);
        }
        public static int DirectionIndex(Vector2 input, int count = PageSize)
        {
            count = Mathf.Max(1,count);
            return ((Mathf.FloorToInt(Mathf.Atan2(input.x,input.y)/(Mathf.PI*2)*count+.5f)%count)+count)%count;
        }
        public void Tick(Vector2 stick, bool previous, bool next, bool confirm, bool back, bool inspect = false)
        {
            if (Time.frameCount == openedFrame) return;
            if (back) { cancel?.Invoke(); return; }
            if (previous || next)
            {
                var pages = Mathf.CeilToInt(actions.Count / (float)PageSize);
                SelectionChanged?.Invoke();
                page = (page + (next ? 1 : pages - 1)) % pages; selected = page * PageSize; Populate();
            }
            if (stick.sqrMagnitude > .16f)
            {
                var candidate = page * PageSize + DirectionIndex(stick,PageEntryCount);
                if (candidate < actions.Count && candidate != selected) { SelectionChanged?.Invoke(); selected = candidate; Refresh(); }
            }
            if (inspect) actions[selected].Inspect?.Invoke();
            if (confirm) Confirm(); else Refresh();
        }
        private void Confirm()
        {
            if (!actions[selected].Enabled) return;
            var action = actions[selected].Execute; Hide(); action?.Invoke();
        }
        private void LateUpdate()
        {
            if (panel == null) return;
            var canvas = GetComponentInParent<Canvas>();
            var scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f) / Mathf.Max(.01f, canvas.scaleFactor);
            panel.localScale = Vector3.one * scale;
        }
    }
}

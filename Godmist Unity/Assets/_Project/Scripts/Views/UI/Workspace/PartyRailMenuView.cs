using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Godmist.Views.UI.Workspace
{
    [DefaultExecutionOrder(100)] // Let the workspace see popup focus before Escape dismisses it.
    public sealed class PartyRailMenuView : MonoBehaviour, IPointerClickHandler
    {
        public sealed class Entry
        {
            public readonly string Label;
            public readonly string DisabledReason;
            public readonly Action Action;
            public readonly bool Enabled, SeparatorBefore;
            public readonly IReadOnlyList<Entry> Children;
            public readonly string ChildrenHint;

            public Entry(string label, Action action = null, bool enabled = true,
                bool separatorBefore = false, IReadOnlyList<Entry> children = null,
                string childrenHint = "Wybierz grupę, do której dołączyć.", string disabledReason = null)
            {
                Label = label; Action = action; Enabled = enabled;
                SeparatorBefore = separatorBefore; Children = children;
                ChildrenHint = childrenHint;
                DisabledReason = enabled ? null : disabledReason;
            }
        }

        [SerializeField] private RectTransform panel;
        [SerializeField] private Transform entryRoot;
        [SerializeField] private Button entryTemplate;
        [SerializeField] private TMP_Text header;
        [SerializeField] private RectTransform separatorTemplate;
        [SerializeField] private TMP_Text hintText;
        [SerializeField, Min(0)] private float anchorGap = 44; // 36px drawer handle + 8px breathing room.
        public TMP_Text CreateControllerLabel(Transform parent, string labelName)
        {
            var label = Instantiate(header, parent); label.name = labelName; label.raycastTarget = false;
            label.gameObject.SetActive(false); return label;
        }

        public ControllerWheelView CreateControllerWheel()
        {
            var node = new GameObject("Controller Action Wheel", typeof(RectTransform));
            node.SetActive(false);
            node.transform.SetParent(transform.parent, false);
            var wheel = node.AddComponent<ControllerWheelView>();
            wheel.Configure(entryTemplate, header, panel.GetComponentsInChildren<Image>(true).FirstOrDefault(image=>!image.fillCenter && image.sprite!=null) ?? panel.GetComponent<Image>());
            return wheel;
        }

        private readonly List<Button> entries = new();
        private readonly List<RectTransform> separators = new();
        private IReadOnlyList<Entry> rootActions;
        private string rootTitle, rootHint;
        private Vector2 screenPosition;
        private RectTransform adjacentTo;
        private GameObject previousSelection;
        private int openedFrame;
        private bool inSubmenu;
        private bool isShowing;
        private Button submenuParent;

        // World/NPC/obstacle menus retain their existing tuple API and actions.
        public void Show(Vector2 screen, IReadOnlyList<(string Label, Action Action, bool Enabled)> actions, string title = null)
            => Show(screen, actions.Select(action => new Entry(action.Label, action.Action, action.Enabled)).ToArray(), title);

        public void Show(Vector2 screen, IReadOnlyList<Entry> actions, string title,
            string hint = null, RectTransform anchor = null)
        {
            if (!isShowing) previousSelection = EventSystem.current?.currentSelectedGameObject;
            isShowing = true;
            rootActions = actions; rootTitle = title; rootHint = hint;
            screenPosition = screen; adjacentTo = anchor; inSubmenu = false;
            gameObject.SetActive(true); transform.SetAsLastSibling(); openedFrame = Time.frameCount;
            Populate(actions, title, hint);
        }

        private void Populate(IReadOnlyList<Entry> actions, string title, string hint)
        {
            if (header != null) { header.text = title ?? string.Empty; header.gameObject.SetActive(!string.IsNullOrEmpty(title)); header.transform.SetAsFirstSibling(); }
            if (hintText != null) { hintText.text = hint ?? string.Empty; hintText.gameObject.SetActive(!string.IsNullOrEmpty(hint)); }
            foreach (var separator in separators) separator.gameObject.SetActive(false);
            while (entries.Count < actions.Count) entries.Add(Instantiate(entryTemplate, entryRoot));
            var separatorIndex = 0;
            for (var i = 0; i < entries.Count; i++)
            {
                var button = entries[i]; var visible = i < actions.Count;
                button.gameObject.SetActive(visible);
                if (!visible) continue;
                var action = actions[i];
                if (action.SeparatorBefore && separatorTemplate != null)
                {
                    if (separatorIndex == separators.Count) separators.Add(Instantiate(separatorTemplate, entryRoot));
                    var separator = separators[separatorIndex++]; separator.gameObject.SetActive(true); separator.SetAsLastSibling();
                }
                button.transform.SetAsLastSibling();
                var label = button.GetComponentInChildren<TMP_Text>(true);
                label.text = action.Label;
                var normal = entryTemplate.GetComponentInChildren<TMP_Text>(true).color;
                label.color = action.Enabled ? normal : new Color(normal.r * .55f, normal.g * .55f, normal.b * .55f, normal.a);
                button.interactable = action.Enabled; button.onClick.RemoveAllListeners();
                var availability = button.GetComponent<ContextMenuAvailabilityHint>();
                if (availability == null) availability = button.gameObject.AddComponent<ContextMenuAvailabilityHint>();
                availability.Bind(action.Label, action.DisabledReason);
                button.onClick.AddListener(() =>
                {
                    if (!action.Enabled) return;
                    if (action.Children != null && action.Children.Count > 0)
                    {
                        submenuParent = button;
                        inSubmenu = true;
                        var children = new List<Entry> { new("Wróć", Back) };
                        children.AddRange(action.Children);
                        Populate(children, rootTitle, action.ChildrenHint);
                        return;
                    }
                    if (inSubmenu && action.Action == (Action)Back) { Back(); return; }
                    Hide(); action.Action?.Invoke();
                });
            }
            if (hintText != null) hintText.transform.SetAsLastSibling();
            ConfigureNavigation();
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
            Canvas.ForceUpdateCanvases();
            PositionPanel();
        }

        private static bool CanFocus(Button button) => button.gameObject.activeSelf
            && (button.interactable || button.GetComponent<ContextMenuAvailabilityHint>()?.CanInspect == true);

        private void ConfigureNavigation()
        {
            var focusable = entries.Where(CanFocus).ToArray();
            foreach (var button in entries) button.navigation = new Navigation { mode = Navigation.Mode.None };
            if (focusable.Length > 0 && EventSystem.current != null)
            {
                // Pooled rows may keep the same selected object after changing content.
                if (EventSystem.current.currentSelectedGameObject == focusable[0].gameObject)
                    EventSystem.current.SetSelectedGameObject(null);
                EventSystem.current.SetSelectedGameObject(focusable[0].gameObject);
            }
        }

        public void MoveFocus(int direction)
        {
            var focusable = entries.Where(CanFocus).ToArray();
            if (focusable.Length == 0) return;
            var selected = EventSystem.current?.currentSelectedGameObject;
            var index = Array.FindIndex(focusable, button => button.gameObject == selected);
            index = index < 0 ? (direction < 0 ? focusable.Length - 1 : 0)
                : (index + (direction < 0 ? -1 : 1) + focusable.Length) % focusable.Length;
            EventSystem.current?.SetSelectedGameObject(focusable[index].gameObject);
        }

        private void PositionPanel()
        {
            var root = (RectTransform)transform;
            var canvas = GetComponentInParent<Canvas>();
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenPosition, camera, out var point);
            if (adjacentTo != null)
            {
                var corners = new Vector3[4]; adjacentTo.GetWorldCorners(corners);
                var left = root.InverseTransformPoint(corners[0]).x;
                var right = root.InverseTransformPoint(corners[2]).x;
                point.x = right + anchorGap;
                if (point.x + panel.rect.width > root.rect.xMax) point.x = left - panel.rect.width - anchorGap;
            }
            point.x = Mathf.Clamp(point.x, root.rect.xMin, Mathf.Max(root.rect.xMin, root.rect.xMax - panel.rect.width));
            point.y = Mathf.Clamp(point.y, Mathf.Min(root.rect.yMax, root.rect.yMin + panel.rect.height), root.rect.yMax);
            panel.localPosition = new Vector3(point.x, point.y, 0);
        }

        public void Back()
        {
            if (!inSubmenu) { Hide(); return; }
            inSubmenu = false; Populate(rootActions, rootTitle, rootHint);
            if (submenuParent != null && submenuParent.gameObject.activeInHierarchy && submenuParent.interactable)
                EventSystem.current?.SetSelectedGameObject(submenuParent.gameObject);
        }

        public void Hide()
        {
            if (isShowing)
                (GetComponentInParent<UIWorkspaceWindowManager>() ?? UIWorkspaceWindowManager.Instance)?.ConsumeContextMenuInput();
            var selected = EventSystem.current?.currentSelectedGameObject;
            if (selected != null && selected.transform.IsChildOf(transform))
                EventSystem.current.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null);
            isShowing = false; gameObject.SetActive(false);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (Time.frameCount == openedFrame) return;
            var canvas = GetComponentInParent<Canvas>();
            if (!RectTransformUtility.RectangleContainsScreenPoint(panel, e.position,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera)) Hide();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) Back();
            else if (Input.GetKeyDown(KeyCode.Tab))
                MoveFocus(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? -1 : 1);
        }
    }
}

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Godmist.Views.UI.Workspace
{
    public sealed class PartyRailMenuView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private RectTransform panel;
        [SerializeField] private Transform entryRoot;
        [SerializeField] private Button entryTemplate;
        private readonly List<Button> entries = new();
        private int openedFrame;
        public void Show(Vector2 screen, IReadOnlyList<(string Label, Action Action, bool Enabled)> actions)
        {
            gameObject.SetActive(true); transform.SetAsLastSibling(); openedFrame = Time.frameCount;
            while (entries.Count < actions.Count) entries.Add(Instantiate(entryTemplate, entryRoot));
            for (var i = 0; i < entries.Count; i++)
            {
                var visible = i < actions.Count; entries[i].gameObject.SetActive(visible);
                if (!visible) continue;
                var action = actions[i]; var button = entries[i];
                button.GetComponentInChildren<TMP_Text>(true).text = action.Label;
                button.interactable = action.Enabled; button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => { Hide(); action.Action?.Invoke(); });
            }
            Canvas.ForceUpdateCanvases();
            var root = (RectTransform)transform;
            var canvas = GetComponentInParent<Canvas>();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out var point);
            // Clamp the popup inside its canvas, including near the bottom of the rail.
            point.x = Mathf.Clamp(point.x, root.rect.xMin, Mathf.Max(root.rect.xMin, root.rect.xMax - panel.rect.width));
            point.y = Mathf.Clamp(point.y, Mathf.Min(root.rect.yMax, root.rect.yMin + panel.rect.height), root.rect.yMax);
            panel.localPosition = new Vector3(point.x, point.y, 0);
        }
        public void Hide() => gameObject.SetActive(false);
        public void OnPointerClick(PointerEventData e) { if (Time.frameCount != openedFrame) Hide(); }
        private void Update() { if (Input.GetKeyDown(KeyCode.Escape)) Hide(); }
    }
}

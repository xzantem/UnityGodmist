using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Godmist.Views.UI.Workspace
{
    public sealed class CharacterProgressionBadgeView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Button button;
        private TMP_Text label;
        private UIWorkspaceTooltipView tooltip;
        private int points;
        private bool canOpen;
        public static CharacterProgressionBadgeView Create(RectTransform anchor, TMP_Text fontSource, Sprite frame, Action open)
        {
            var node = new GameObject("ProgressionPointsBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            node.layer = anchor.gameObject.layer; node.transform.SetParent(anchor, false);
            node.AddComponent<LayoutElement>().ignoreLayout = true;
            var rect = (RectTransform)node.transform;
            rect.anchorMin = rect.anchorMax = Vector2.one; rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-4, -4); rect.sizeDelta = new Vector2(40, 26);
            node.GetComponent<Image>().color = new Color(.10f, .10f, .12f, 1);
            var borderNode = new GameObject("Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            borderNode.layer = node.layer; borderNode.transform.SetParent(rect, false);
            var border = borderNode.GetComponent<Image>(); border.sprite = frame; border.type = Image.Type.Sliced;
            border.raycastTarget = false; border.enabled = frame != null;
            Stretch(border.rectTransform);
            var labelNode = new GameObject("Points", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelNode.layer = node.layer; labelNode.transform.SetParent(rect, false);
            var text = labelNode.GetComponent<TextMeshProUGUI>();
            text.font = fontSource?.font ?? TMP_Settings.defaultFontAsset;
            text.fontSharedMaterial = fontSource?.fontSharedMaterial != null ? fontSource.fontSharedMaterial : text.font?.material;
            text.fontSize = 16; text.fontStyle = FontStyles.Bold; text.color = new Color(1, .82f, .37f);
            text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
            Stretch(text.rectTransform);
            var view = node.AddComponent<CharacterProgressionBadgeView>(); view.label = text;
            view.button = node.GetComponent<Button>(); view.button.transition = Selectable.Transition.None;
            view.button.navigation = new Navigation { mode = Navigation.Mode.None };
            view.button.onClick.AddListener(() => { if (view.isActiveAndEnabled && view.points > 0 && view.canOpen) open?.Invoke(); });
            return view;
        }
        private static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        public void Bind(int count, bool allowed)
        {
            count = Mathf.Max(0, count);
            if (points != count || canOpen != allowed) tooltip?.InvalidateSource(this);
            if (points != count)
            {
                label.text = "+" + count;
                var rect = (RectTransform)transform;
                rect.sizeDelta = new Vector2(Mathf.Clamp(label.GetPreferredValues().x + 10, 40,
                    Mathf.Max(40, ((RectTransform)rect.parent).rect.width - 8)), 26);
            }
            points = count; canOpen = allowed; button.interactable = allowed;
            gameObject.SetActive(count > 0);
        }
        public bool Contains(PointerEventData data) => isActiveAndEnabled && data != null
            && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)transform, data.position, data.enterEventCamera);
        public void OnPointerEnter(PointerEventData data)
        {
            tooltip ??= UIWorkspaceTooltipView.ActiveInstance;
            tooltip?.ShowCompactText("Punkty rozwoju", $"Niewydane punkty: {points}.\n" + (canOpen
                ? "Kliknij, aby otworzyć rozwój tej postaci." : "Rozwój jest teraz niedostępny, np. podczas walki tej postaci."), this);
        }
        public void OnPointerExit(PointerEventData data) => tooltip?.NotifySourceExit(this);
        private void OnDisable() => tooltip?.InvalidateSource(this);
    }
}

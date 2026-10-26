using System;
using Godmist.Data.NPC;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Godmist.Views.NPC
{
    public class DialogueChoiceButtonView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        ISelectHandler, IDeselectHandler, ILayoutElement
    {
        [SerializeField] private Button button;
        [SerializeField] private TextMeshProUGUI label;
        [SerializeField, Min(20)] private float minimumHeight = 60;
        [SerializeField] private Color normalTextColor = new(.91f, .875f, .78f, 1);
        [SerializeField] private Color highlightedTextColor = new(.96f, .76f, .31f, 1);

        private string choiceId;
        private Action<string> selectionCallback;
        private bool hovered, selected;
        private float measuredHeight = 60;

        public float minWidth => -1;
        public float preferredWidth => -1;
        public float flexibleWidth => -1;
        public float minHeight => minimumHeight;
        public float preferredHeight => measuredHeight;
        public float flexibleHeight => 0;
        public int layoutPriority => 2;
        public void CalculateLayoutInputHorizontal() { }
        public void CalculateLayoutInputVertical() => RefreshHeight();

        private void Awake()
        {
            if (button != null) button.onClick.AddListener(HandleClicked);
        }

        public void Bind(
            DialogueChoiceData choice,
            Action<string> nextSelectionCallback)
        {
            if (choice == null)
            {
                return;
            }

            choiceId = choice.ChoiceId;
            selectionCallback = nextSelectionCallback;
            SetLabel(choice.DisplayText);
        }

        public void BindAction(string displayText, Action action)
        {
            choiceId = string.Empty;
            selectionCallback = _ => action?.Invoke();
            SetLabel(displayText);
        }

        private void SetLabel(string displayText)
        {
            if (label != null)
            {
                label.text = displayText;
                RefreshColor();
                LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
            }
        }

        public void OnPointerEnter(PointerEventData data) { hovered = true; RefreshColor(); }
        public void OnPointerExit(PointerEventData data) { hovered = false; RefreshColor(); }
        public void OnSelect(BaseEventData data) { selected = true; RefreshColor(); }
        public void OnDeselect(BaseEventData data) { selected = false; RefreshColor(); }
        private void OnDisable() { hovered = selected = false; RefreshColor(); }
        private void OnDestroy() { if (button != null) button.onClick.RemoveListener(HandleClicked); }
        private void RefreshColor()
        {
            if (label != null) label.color = (hovered || selected) && (button == null || button.interactable)
                ? highlightedTextColor : normalTextColor;
        }
        private void RefreshHeight()
        {
            if (label == null) return;
            // The root receives its new width before Unity updates the stretched label.
            // Measure that final width rather than the label's previous-frame rect.
            var rect = label.rectTransform;
            var width = ((RectTransform)rect.parent).rect.width
                * (rect.anchorMax.x - rect.anchorMin.x) + rect.sizeDelta.x;
            if (width < 20) return;
            var height = Mathf.Max(minimumHeight,
                Mathf.Ceil(label.GetPreferredValues(label.text, width, 10000).y + 24));
            measuredHeight = height;
        }

        private void HandleClicked()
        {
            selectionCallback?.Invoke(choiceId);
        }
    }
}

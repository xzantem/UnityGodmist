using Godmist.Runtime.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Godmist.Views.UI
{
    // Presentation of the active scout's existing combat-scoped inventory.
    public sealed class CombatScoutKnifeCounterView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private TMP_Text countText;
        private BattleUnit unit;
        private int available = -1, capacity = -1;
        private bool hovered;
        public BattleUnit HighlightOwner => isActiveAndEnabled && hovered ? unit : null;

        public static CombatScoutKnifeCounterView Create(Transform parent, TMP_Text fontSource)
        {
            var node = new GameObject("ScoutKnifeCounter", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            node.layer = parent.gameObject.layer;
            node.transform.SetParent(parent, false);
            node.AddComponent<LayoutElement>().ignoreLayout = true;
            var rect = (RectTransform)node.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(8f, -112f);
            rect.sizeDelta = new Vector2(80f, 48f);
            var frame = node.GetComponent<Image>();
            frame.sprite = parent.Find("PortraitFrame")?.GetComponent<Image>()?.sprite;
            frame.type = Image.Type.Sliced;
            frame.color = frame.sprite != null ? Color.white : new Color(.12f, .12f, .15f, 1f);
            frame.raycastTarget = true;
            var view = node.AddComponent<CombatScoutKnifeCounterView>();
            view.CreateLabel("Label", "Sztylety", fontSource, 13f, 4f, 17f);
            view.countText = view.CreateLabel("Count", "", fontSource, 20f, 20f, 24f);
            return view;
        }

        private TMP_Text CreateLabel(string name, string value, TMP_Text source, float size, float top, float height)
        {
            var node = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            node.layer = gameObject.layer;
            node.transform.SetParent(transform, false);
            var text = node.GetComponent<TextMeshProUGUI>();
            text.font = source?.font ?? TMP_Settings.defaultFontAsset;
            text.fontSharedMaterial = source?.fontSharedMaterial != null ? source.fontSharedMaterial : text.font?.material;
            text.fontSize = size;
            text.color = source != null ? source.color : new Color(.85f, .82f, .67f);
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.text = value;
            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, 1);
            rect.sizeDelta = new Vector2(-8f, height); rect.anchoredPosition = new Vector2(0, -top);
            return text;
        }

        public void Bind(BattleUnit next, bool known)
        {
            var visible = known && next?.IsPlayerSide == true && next.Character?.ClassData?.Id == "class.scout";
            if (!visible) next = null;
            if (!ReferenceEquals(unit, next))
            {
                ClearHover(); unit = next; available = capacity = -1;
            }
            gameObject.SetActive(visible);
            if (!visible) return;
            if (available == next.AvailableKnives && capacity == next.MaxKnives) return;
            available = next.AvailableKnives; capacity = next.MaxKnives;
            countText.text = $"{available}/{capacity}";
            countText.color = available > 0 ? new Color(.94f, .83f, .51f) : new Color(1f, .35f, .28f);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (unit == null || !isActiveAndEnabled) return;
            hovered = true;
        }
        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
        }
        private void OnDisable() => ClearHover();
        private void ClearHover()
        {
            hovered = false;
        }
    }
}

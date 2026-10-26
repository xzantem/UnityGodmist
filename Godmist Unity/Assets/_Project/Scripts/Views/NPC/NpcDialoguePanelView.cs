using Godmist.Data.NPC;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.NPC
{
    /// <summary>Owns the authored dialogue panel's bounded content layout.</summary>
    public sealed class NpcDialoguePanelView : MonoBehaviour
    {
        [SerializeField] private Image npcIllustration;
        [SerializeField] private RectTransform panelFrame;
        [SerializeField] private RectTransform textColumn;
        [SerializeField] private LayoutElement portraitLayout;
        [SerializeField] private AspectRatioFitter portraitAspect;
        [SerializeField] private RectTransform conversationContent;
        [SerializeField] private ScrollRect conversationScroll;
        [SerializeField] private LayoutElement conversationLayout;
        [SerializeField] private GameObject servicesRow;
        [SerializeField, Range(.3f, .9f)] private float maximumHeightRatio = .72f;

        private bool layoutDirty = true;
        private bool resetScroll;
        private Vector2 lastSize;

        public void BindNpc(NpcDefinition definition)
        {
            if (npcIllustration != null)
            {
                npcIllustration.sprite = definition != null
                    ? definition.DialoguePortrait != null ? definition.DialoguePortrait : definition.Portrait
                    : null;
                npcIllustration.enabled = npcIllustration.sprite != null;
                if (portraitAspect != null && npcIllustration.sprite != null)
                    portraitAspect.aspectRatio = npcIllustration.sprite.rect.width / npcIllustration.sprite.rect.height;
            }
        }

        public void RefreshLayout(bool restartScroll = true)
        {
            layoutDirty = true;
            resetScroll |= restartScroll;
        }

        private void OnEnable() => RefreshLayout();

        private void LateUpdate()
        {
            var size = ((RectTransform)transform).rect.size;
            if (size != lastSize) { lastSize = size; layoutDirty = true; }
            if (!layoutDirty) return;
            RebuildLayout();
        }

        public void RebuildLayout()
        {
            if (panelFrame == null || textColumn == null || conversationContent == null
                || conversationScroll == null || conversationLayout == null) return;

            if (servicesRow != null)
            {
                var hasService = false;
                for (var index = 0; index < servicesRow.transform.childCount; index++)
                    hasService |= servicesRow.transform.GetChild(index).gameObject.activeSelf;
                servicesRow.SetActive(hasService);
            }
            if (portraitLayout != null) portraitLayout.preferredWidth = panelFrame.rect.width * .35f;
            // Let the authored groups supply widths before measuring wrapped text.
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelFrame);
            LayoutRebuilder.ForceRebuildLayoutImmediate(conversationContent);
            var desired = LayoutUtility.GetPreferredHeight(conversationContent);
            var reserved = Mathf.Max(0, LayoutUtility.GetPreferredHeight(textColumn)
                - conversationLayout.preferredHeight) + 52;
            var maximum = Mathf.Max(60, ((RectTransform)transform).rect.height * maximumHeightRatio - reserved);
            conversationLayout.preferredHeight = Mathf.Clamp(desired, 1, maximum);
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelFrame);
            LayoutRebuilder.ForceRebuildLayoutImmediate(conversationContent);
            conversationScroll.vertical = desired > conversationLayout.preferredHeight + 1;
            if (conversationScroll.verticalScrollbar != null)
                conversationScroll.verticalScrollbar.gameObject.SetActive(conversationScroll.vertical);
            if (resetScroll || !conversationScroll.vertical) conversationScroll.verticalNormalizedPosition = 1;
            layoutDirty = false;
            resetScroll = false;
        }
    }
}

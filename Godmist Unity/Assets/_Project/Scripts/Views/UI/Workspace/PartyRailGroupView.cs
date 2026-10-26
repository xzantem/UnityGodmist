using UnityEngine;
using UnityEngine.UI;
namespace Godmist.Views.UI.Workspace
{
    public sealed class PartyRailGroupView : MonoBehaviour
    {
        [SerializeField] private Transform memberRoot;
        [SerializeField] private Image dropHighlight;
        [SerializeField] private Image connector;
        [SerializeField] private Image connectorTop;
        [SerializeField] private Image connectorBottom;
        [SerializeField] private Image separator;
        private int memberCount;
        public Transform MemberRoot => memberRoot;
        public RectTransform Rect => (RectTransform)transform;
        public void ConfigurePresentation(int count, bool separateBefore)
        {
            memberCount = count;
            if (separator != null) separator.enabled = separateBefore;
            // Cards have opaque surfaces. Draw the bracket above them in the left gutter.
            foreach (var image in new[] { connector, connectorTop, connectorBottom, separator })
                if (image != null) image.transform.SetAsLastSibling();
            Highlight(false);
        }
        public void Highlight(bool value)
        {
            // Retain the old serialized reference, but never cover portraits with a drop rectangle.
            if (dropHighlight != null) dropHighlight.enabled = false;
            var color = value ? new Color(.88f, .67f, .24f) : new Color(.48f, .51f, .43f);
            SetConnector(connector, value, color);
            SetConnector(connectorTop, value, color);
            SetConnector(connectorBottom, value, color);
        }
        private void SetConnector(Image image, bool highlighted, Color color)
        {
            if (image == null) return;
            image.enabled = memberCount > 1 || highlighted;
            image.color = color;
        }
    }
}

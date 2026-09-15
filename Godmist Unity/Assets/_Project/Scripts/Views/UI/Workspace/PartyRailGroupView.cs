using UnityEngine;
using UnityEngine.UI;
namespace Godmist.Views.UI.Workspace
{
    public sealed class PartyRailGroupView : MonoBehaviour
    {
        [SerializeField] private Transform memberRoot;
        [SerializeField] private Image dropHighlight;
        public Transform MemberRoot => memberRoot;
        public RectTransform Rect => (RectTransform)transform;
        public void Highlight(bool value) { if (dropHighlight != null) dropHighlight.enabled = value; }
    }
}

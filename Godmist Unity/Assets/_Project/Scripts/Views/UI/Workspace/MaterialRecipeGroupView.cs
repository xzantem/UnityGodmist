using TMPro;
using UnityEngine;
namespace Godmist.Views.UI.Workspace
{
    /// <summary>Explicitly authored heading and recipe grid for one material family.</summary>
    public sealed class MaterialRecipeGroupView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI heading;
        [SerializeField] private Transform content;
        public Transform Content => content;
        public void SetHeading(string title) { if (heading != null) heading.text = title; }
    }
}

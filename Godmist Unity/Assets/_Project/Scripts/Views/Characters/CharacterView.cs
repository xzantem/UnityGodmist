using Godmist.Runtime.Combat;
using TMPro;
using UnityEngine;

namespace Godmist.Views.Characters
{
    public class CharacterView : MonoBehaviour
    {
        [Header("Visual References")]
        [SerializeField] private SpriteRenderer bodyRenderer;
        [SerializeField] private TextMeshPro nameLabel;

        [Header("Colors")]
        [SerializeField] private Color playerSideColor = new(0.35f, 0.75f, 1f, 1f);
        [SerializeField] private Color enemySideColor = new(1f, 0.4f, 0.4f, 1f);

        public BattleUnit BoundUnit { get; private set; }

        public void Bind(BattleUnit unit)
        {
            BoundUnit = unit;
            Refresh();
        }

        public void Refresh()
        {
            if (BoundUnit == null)
            {
                return;
            }

            if (nameLabel != null)
            {
                nameLabel.text = BoundUnit.Character.Definition.DisplayName;
            }

            if (bodyRenderer != null)
            {
                bodyRenderer.color = BoundUnit.IsPlayerSide ? playerSideColor : enemySideColor;
            }
        }
    }
}
using Godmist.Runtime.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI
{
    public class CombatTurnPortraitView : MonoBehaviour
    {
        [SerializeField] private Image portrait;
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Image activeHighlight;
        [SerializeField] private Color playerActiveColor = new(0.88f, 0.67f, 0.24f, 1f);
        [SerializeField] private Color enemyActiveColor = new(0.78f, 0.20f, 0.18f, 1f);

        public void Bind(BattleUnit unit, bool isActive)
        {
            if (unit?.Character == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            if (portrait != null)
            {
                portrait.sprite = unit.Character.Definition?.VisualSprite;
            }

            // The timeline communicates turn sequence, while the party rail
            // communicates current state. It deliberately remains portrait-only.
            if (healthSlider != null)
            {
                healthSlider.gameObject.SetActive(false);
            }

            if (activeHighlight != null)
            {
                activeHighlight.enabled = isActive;
                activeHighlight.color = unit.IsPlayerSide
                    ? playerActiveColor
                    : enemyActiveColor;
            }
        }

    }
}

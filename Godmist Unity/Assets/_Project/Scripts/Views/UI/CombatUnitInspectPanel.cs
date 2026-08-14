using Godmist.Runtime.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI
{
    public class CombatUnitInspectPanel : MonoBehaviour
    {
        [SerializeField] private CombatEdgeDrawer edgeDrawer;
        [SerializeField] private Image portrait;
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Slider shieldSlider;
        [SerializeField] private Slider resourceSlider;
        [SerializeField] private Image sideFrame;
        [SerializeField] private Color playerFrameColor = new(0.88f, 0.67f, 0.24f, 1f);
        [SerializeField] private Color enemyFrameColor = new(0.78f, 0.20f, 0.18f, 1f);

        private void Awake()
        {
            Hide();
        }

        public void Show(BattleUnit unit)
        {
            if (unit?.Character?.Definition == null)
            {
                Hide();
                return;
            }

            var character = unit.Character;
            var maxHealth = Mathf.Max(1f, character.Stats?.MaxHealth.GetValue() ?? 1f);
            var shield = character.ShieldPool?.TotalShield ?? 0;
            var resource = character.ResourcePool;

            if (portrait != null)
            {
                portrait.sprite = character.Definition.VisualSprite;
            }

            SetSliderValue(healthSlider, character.CurrentHealth, maxHealth);
            SetSliderVisible(shieldSlider, shield > 0);
            SetSliderValue(shieldSlider, shield, Mathf.Max(maxHealth, shield));
            SetSliderVisible(resourceSlider, resource != null && resource.MaxValue > 0);
            SetSliderValue(resourceSlider, resource?.CurrentValue ?? 0, resource?.MaxValue ?? 1);
            if (resource != null)
            {
                CombatResourceColors.ApplyToFill(resourceSlider, resource.ResourceType);
            }

            if (sideFrame != null)
            {
                sideFrame.color = unit.IsPlayerSide
                    ? playerFrameColor
                    : enemyFrameColor;
            }

            if (edgeDrawer != null)
            {
                edgeDrawer.Show();
            }
            else
            {
                gameObject.SetActive(true);
            }
        }

        public void Hide()
        {
            if (edgeDrawer != null)
            {
                edgeDrawer.Hide();
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private static void SetSliderVisible(Slider slider, bool visible)
        {
            if (slider != null)
            {
                slider.gameObject.SetActive(visible);
            }
        }

        private static void SetSliderValue(Slider slider, float currentValue, float maxValue)
        {
            if (slider == null)
            {
                return;
            }

            slider.minValue = 0f;
            slider.maxValue = Mathf.Max(1f, maxValue);
            slider.SetValueWithoutNotify(Mathf.Clamp(currentValue, 0f, slider.maxValue));
        }
    }
}

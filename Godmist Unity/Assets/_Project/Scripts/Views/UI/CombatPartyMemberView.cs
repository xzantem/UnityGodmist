using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI
{
    public class CombatPartyMemberView : MonoBehaviour
    {
        [SerializeField] private Image portrait;
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Slider resourceSlider;
        [SerializeField] private Image lifeStateMarker;
        [SerializeField] private CombatStatusCollectionView statusCollection;
        [SerializeField] private Color aliveColor = new(0.88f, 0.67f, 0.24f, 1f);
        [SerializeField] private Color downedColor = new(0.78f, 0.20f, 0.18f, 1f);
        [SerializeField] private Color deadColor = new(0.20f, 0.22f, 0.25f, 1f);

        public void Bind(BattleUnit unit)
        {
            if (unit?.Character == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            var character = unit.Character;
            var maxHealth = Mathf.Max(1f, character.Stats?.MaxHealth.GetValue() ?? 1f);
            var resource = character.ResourcePool;

            if (portrait != null)
            {
                portrait.sprite = character.Definition?.VisualSprite;
            }

            SetSliderValue(healthSlider, character.CurrentHealth, maxHealth);
            SetSliderVisible(resourceSlider, resource != null && resource.MaxValue > 0);
            SetSliderValue(resourceSlider, resource?.CurrentValue ?? 0, resource?.MaxValue ?? 1);
            if (resource != null)
            {
                CombatResourceColors.ApplyToFill(resourceSlider, resource.ResourceType);
            }

            if (lifeStateMarker != null)
            {
                lifeStateMarker.color = character.LifeState switch
                {
                    CharacterLifeState.Downed => downedColor,
                    CharacterLifeState.Dead => deadColor,
                    _ => aliveColor
                };
            }

            statusCollection?.Bind(unit);
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

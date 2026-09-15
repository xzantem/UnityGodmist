using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI
{
    public class CombatPartyMemberView : MonoBehaviour
    {
        [SerializeField] private TMPro.TextMeshProUGUI healthValueText, resourceValueText;
        [SerializeField] private Image portrait;
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Slider resourceSlider;
        [SerializeField] private Image lifeStateMarker;
        [SerializeField] private CombatStatusCollectionView statusCollection;
        [SerializeField] private Color aliveColor = new(0.88f, 0.67f, 0.24f, 1f);
        [SerializeField] private Color downedColor = new(0.78f, 0.20f, 0.18f, 1f);
        [SerializeField] private Color deadColor = new(0.20f, 0.22f, 0.25f, 1f);

        [SerializeField] private Image selectedMarker;
        [SerializeField] private Image turnMarker;

        private Godmist.Runtime.Characters.CharacterInstance explorationCharacter;

        public void Bind(BattleUnit unit, bool selected = false, bool hasTurn = false)
        {
            explorationCharacter = null;
            BindPresentation(unit?.Character, selected, hasTurn);
            if (unit?.Character != null) statusCollection?.Bind(unit);
        }

        public void BindCharacter(Godmist.Runtime.Characters.CharacterInstance character, bool selected = false, bool hasTurn = false)
        {
            BindPresentation(character, selected, hasTurn);
            if (explorationCharacter != character)
            {
                explorationCharacter = character;
                statusCollection?.BindCharacter(character);
            }
        }

        private void BindPresentation(Godmist.Runtime.Characters.CharacterInstance character, bool selected, bool hasTurn)
        {
            if (character == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            if (selectedMarker != null) selectedMarker.enabled = selected;
            if (turnMarker != null) turnMarker.enabled = hasTurn;
            var maxHealth = Mathf.Max(1f, character.Stats?.MaxHealth.GetValue() ?? 1f);
            var resource = character.ResourcePool;
            if(healthValueText!=null)healthValueText.text=$"{character.CurrentHealth}/{maxHealth:0}";
            if(resourceValueText!=null){resourceValueText.gameObject.SetActive(resource!=null&&resource.MaxValue>0);resourceValueText.text=resource==null?"":$"{resource.CurrentValue}/{resource.MaxValue}";}

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
                lifeStateMarker.enabled = character.LifeState != CharacterLifeState.Alive;
                lifeStateMarker.color = character.LifeState switch
                {
                    CharacterLifeState.Downed => downedColor,
                    CharacterLifeState.Dead => deadColor,
                    _ => aliveColor
                };
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

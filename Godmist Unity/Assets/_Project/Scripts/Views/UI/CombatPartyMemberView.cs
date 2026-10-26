using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI
{
    public class CombatPartyMemberView : MonoBehaviour
    {
        [SerializeField] private TMPro.TextMeshProUGUI healthValueText, resourceValueText;
        [SerializeField] private TMPro.TextMeshProUGUI characterNameText;
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
        private Image shieldOverlay;
        private CombatDownedIndicatorView downedIndicator;
        private Godmist.Views.UI.Workspace.UIWorkspaceStylePalette downedStyle;
        public bool IsPointerOverDownedCountdown(Vector2 point, Camera camera)
            => downedIndicator != null && downedIndicator.ContainsScreenPoint(point,camera);

        public void Bind(BattleUnit unit, bool selected = false, bool hasTurn = false)
        {
            explorationCharacter = null;
            if (selectedMarker != null) selectedMarker.color = new Color(.35f,.85f,1f);
            if (turnMarker != null) turnMarker.color = new Color(1f,.81f,.4f);
            BindPresentation(unit?.Character, selected, hasTurn);
            if (downedStyle == null && CombatDownedIndicatorView.ShouldShow(unit))
                downedStyle = (healthSlider != null ? healthSlider.GetComponent<Godmist.Views.UI.Workspace.UIWorkspaceSliderStyleApplier>()?.Palette : null)
                    ?? GetComponentInChildren<Godmist.Views.UI.Workspace.UIWorkspaceStyleApplier>(true)?.Palette;
            CombatDownedIndicatorView.UpdatePortrait(ref downedIndicator, portrait, unit, downedStyle);
            // The old life-state strip overlaps the portrait's bottom edge in the
            // workspace variant. The countdown now conveys downed state there.
            if (lifeStateMarker != null && downedIndicator?.IsVisible == true)
                lifeStateMarker.enabled = false;
            if (unit?.Character != null) statusCollection?.Bind(unit);
        }

        public void BindCharacter(Godmist.Runtime.Characters.CharacterInstance character, bool selected = false, bool hasTurn = false)
        {
            if (selectedMarker != null) selectedMarker.color = Color.white;
            BindPresentation(character, selected, hasTurn);
            downedIndicator?.Bind(null);
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
            if (characterNameText != null)
                characterNameText.text = $"{character.Definition?.DisplayName}\nPoz. {character.Level}";
            if (selectedMarker != null) selectedMarker.enabled = selected;
            if (turnMarker != null) turnMarker.enabled = hasTurn;
            var maxHealth = Mathf.Max(1f, character.Stats?.MaxHealth.GetValue() ?? 1f);
            var resource = character.ResourcePool;
            if(healthValueText!=null)healthValueText.text=CombatHealthPresentation.Format(character);
            if(resourceValueText!=null){resourceValueText.gameObject.SetActive(resource!=null&&resource.MaxValue>0);resourceValueText.text=resource==null?"":$"{resource.CurrentValue}/{resource.MaxValue}";}

            if (portrait != null)
            {
                portrait.sprite = character.Definition?.UiPortraitSprite;
            }

            SetSliderValue(healthSlider, character.CurrentHealth, maxHealth);
            CombatHealthPresentation.UpdateShieldOverlay(ref shieldOverlay, healthSlider,
                character.ShieldPool?.TotalShield ?? 0, maxHealth);
            SetSliderVisible(resourceSlider, resource != null && resource.MaxValue > 0);
            SetSliderValue(resourceSlider, resource?.CurrentValue ?? 0, resource?.MaxValue ?? 1);
            if (resource != null)
            {
                CombatResourceColors.ApplyToFill(resourceSlider, resource.ResourceType);
                resourceSlider?.GetComponent<CombatSimpleTooltipTrigger>()?.InitializeResource(null, resource);
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

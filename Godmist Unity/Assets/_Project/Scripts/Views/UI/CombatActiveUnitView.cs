using System.Collections.Generic;
using Godmist.Runtime.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI
{
    public class CombatActiveUnitView : MonoBehaviour
    {
        [SerializeField] private Image portrait;
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Slider shieldSlider;
        [SerializeField] private Slider resourceSlider;
        [SerializeField] private TextMeshProUGUI healthValueText;
        [SerializeField] private TextMeshProUGUI resourceValueText;
        [SerializeField] private Image sideFrame;
        [SerializeField] private Transform actionPointPipRoot;
        [SerializeField] private GameObject actionPointPipTemplate;
        [SerializeField] private CombatStatusCollectionView statusCollection;
        [SerializeField] private Color playerAccentColor = new(0.88f, 0.67f, 0.24f, 1f);
        [SerializeField] private Color enemyAccentColor = new(0.78f, 0.20f, 0.18f, 1f);
        [SerializeField] private Color inactivePipColor = new(0.12f, 0.15f, 0.19f, 0.95f);

        private readonly List<Image> actionPointPips = new();

        public void Bind(BattleUnit unit, bool isActive)
        {
            if (unit?.Character == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            var character = unit.Character;
            var maxHealth = Mathf.Max(1f, character.Stats?.MaxHealth.GetValue() ?? 1f);
            var shield = character.ShieldPool?.TotalShield ?? 0;
            var resource = character.ResourcePool;
            var accentColor = unit.IsPlayerSide ? playerAccentColor : enemyAccentColor;

            if (portrait != null)
            {
                portrait.sprite = character.Definition?.VisualSprite;
            }

            if (sideFrame != null)
            {
                sideFrame.color = accentColor;
                sideFrame.enabled = isActive;
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

            if (healthValueText != null)
            {
                healthValueText.text = FormatHealthValue(
                    character.CurrentHealth,
                    maxHealth,
                    shield);
            }

            SetValueTextVisible(resourceValueText, resource != null && resource.MaxValue > 0);
            if (resourceValueText != null && resource != null)
            {
                resourceValueText.text = $"{resource.CurrentValue}/{resource.MaxValue}";
            }

            RefreshActionPointPips(unit, accentColor);
            statusCollection?.Bind(unit);
        }

        private void RefreshActionPointPips(BattleUnit unit, Color activePipColor)
        {
            var maximum = Mathf.Max(0, unit.MaxActionPoints);
            EnsureActionPointPipCount(maximum);

            if (actionPointPipRoot != null)
            {
                actionPointPipRoot.gameObject.SetActive(maximum > 0);
            }

            for (var index = 0; index < actionPointPips.Count; index++)
            {
                var pip = actionPointPips[index];
                if (pip == null)
                {
                    continue;
                }

                var isVisible = index < maximum;
                pip.gameObject.SetActive(isVisible);
                if (isVisible)
                {
                    pip.color = index < unit.CurrentActionPoints
                        ? activePipColor
                        : inactivePipColor;
                }
            }
        }

        private void EnsureActionPointPipCount(int count)
        {
            if (actionPointPipRoot == null || actionPointPipTemplate == null)
            {
                return;
            }

            actionPointPipTemplate.SetActive(false);

            while (actionPointPips.Count < count)
            {
                var pipObject = Instantiate(actionPointPipTemplate, actionPointPipRoot);
                var pip = pipObject.GetComponent<Image>();
                if (pip == null)
                {
                    Destroy(pipObject);
                    Debug.LogError(
                        "Action point pip template requires an Image component.",
                        this);
                    return;
                }

                actionPointPips.Add(pip);
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

        private static void SetValueTextVisible(TextMeshProUGUI text, bool visible)
        {
            if (text != null)
            {
                text.gameObject.SetActive(visible);
            }
        }

        private static string FormatHealthValue(float health, float maximumHealth, int shield)
        {
            var value = $"{health:0}/{maximumHealth:0}";
            return shield > 0 ? $"{value} (+{shield})" : value;
        }
    }
}

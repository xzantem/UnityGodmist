using System.Collections.Generic;
using Godmist.Runtime.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI
{
    public class CombatActiveUnitView : MonoBehaviour
    {
        [SerializeField] private CombatVitalsView vitals;
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

        [SerializeField] private TextMeshProUGUI unitNameText;

        private readonly List<Image> actionPointPips = new();
        private CombatScoutKnifeCounterView knifeCounter;
        public BattleUnit KnifeHighlightOwner => knifeCounter?.HighlightOwner;

        public void Bind(BattleUnit unit, bool isActive, int previewCost = 0, bool known = true)
        {
            if (unit?.Character == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            SetKnownPresentation(known);
            if (knifeCounter == null && known && unit.IsPlayerSide && unit.Character.ClassData?.Id == "class.scout")
                knifeCounter = CombatScoutKnifeCounterView.Create(transform, unitNameText);
            knifeCounter?.Bind(unit, known);

            if (!known)
            {
                if (unitNameText != null) unitNameText.text = "Nieznany przeciwnik";
                if (portrait != null) { portrait.sprite = null; portrait.color = new Color(.18f, .18f, .18f, 1f); }
                if (sideFrame != null) { sideFrame.color = enemyAccentColor; sideFrame.enabled = isActive; }
                return;
            }

            var character = unit.Character;
            if (unitNameText != null) unitNameText.text = character.Definition?.DisplayName ?? character.CharacterId;
            var maxHealth = Mathf.Max(1f, character.Stats?.MaxHealth.GetValue() ?? 1f);
            var shield = character.ShieldPool?.TotalShield ?? 0;
            var resource = character.ResourcePool;
            var accentColor = unit.IsPlayerSide ? playerAccentColor : enemyAccentColor;

            if (portrait != null)
            {
                portrait.sprite = character.Definition?.UiPortraitSprite;
                portrait.color = Color.white;
            }

            if (sideFrame != null)
            {
                sideFrame.color = accentColor;
                sideFrame.enabled = isActive;
            }

            if(vitals != null) { vitals.Bind(unit,previewCost); statusCollection?.Bind(unit); return; }

            SetSliderValue(healthSlider, character.CurrentHealth, maxHealth);
            SetSliderVisible(shieldSlider, shield > 0);
            SetSliderValue(shieldSlider, shield, Mathf.Max(maxHealth, shield));
            SetSliderVisible(resourceSlider, resource != null && resource.MaxValue > 0);
            SetSliderValue(resourceSlider, resource?.CurrentValue ?? 0, resource?.MaxValue ?? 1);
            if (resource != null)
            {
                CombatResourceColors.ApplyToFill(resourceSlider, resource.ResourceType);
                resourceSlider?.GetComponent<CombatSimpleTooltipTrigger>()?.InitializeResource(null, resource);
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

            RefreshActionPointPips(unit);
            statusCollection?.Bind(unit);
        }

        private void SetKnownPresentation(bool known)
        {
            // Keep the authored panel and its LayoutElement alive during hidden
            // enemy turns; only conceal information, not the panel's footprint.
            if (vitals != null) vitals.gameObject.SetActive(known);
            if (statusCollection != null) statusCollection.gameObject.SetActive(known);
            SetSliderVisible(healthSlider, known);
            SetSliderVisible(shieldSlider, known);
            SetSliderVisible(resourceSlider, known);
            SetValueTextVisible(healthValueText, known);
            SetValueTextVisible(resourceValueText, known);
            if (actionPointPipRoot != null) actionPointPipRoot.gameObject.SetActive(known);
        }

        private void RefreshActionPointPips(BattleUnit unit)
        {
            var maximum = ActionPointUnits.PipCount(Mathf.Max(unit.MaxActionPoints, unit.CurrentActionPoints));
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
                    ActionPointPipPresentation.Apply(pip, unit.CurrentActionPoints, index, Color.white, inactivePipColor);
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
            return CombatHealthPresentation.Format(health, maximumHealth, shield);
        }
    }
}

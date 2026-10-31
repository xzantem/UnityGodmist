using Godmist.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Text;
using Godmist.Core.Enums;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Combat;
using Godmist.Views.UI.Workspace;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI
{
    /// <summary>
    /// Floating inspect window for a selected combat unit. The production path
    /// instantiates the authored UI Workspace prefab.
    /// </summary>
    public sealed class CombatUnitInspectWindowView : MonoBehaviour
    {
        [Header("UI Workspace references")]
        [SerializeField] private CombatVitalsView vitals;
        [SerializeField] private UIWorkspaceWindow window;
        [SerializeField] private Button closeButton;
        [SerializeField] private Image background;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI identityText;
        [SerializeField] private TextMeshProUGUI statsLeftText;
        [SerializeField] private TextMeshProUGUI statsRightText;
        [SerializeField] private TextMeshProUGUI statsLeftValues;
        [SerializeField] private TextMeshProUGUI statsRightValues;
        [SerializeField] private TextMeshProUGUI resourceLabelText;
        [SerializeField] private TextMeshProUGUI actionValueText;
        [SerializeField] private LayoutElement overviewLayout;
        [SerializeField] private LayoutElement statsLayout;
        [SerializeField] private TextMeshProUGUI statusesHeaderText;
        [SerializeField] private TextMeshProUGUI skillsHeaderText;
        [SerializeField] private Image portrait;
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Slider shieldSlider;
        [SerializeField] private Slider resourceSlider;
        [SerializeField] private TextMeshProUGUI healthValueText;
        [SerializeField] private TextMeshProUGUI resourceValueText;
        [SerializeField] private Image sideFrame;
        [SerializeField] private Transform actionPointPipRoot;
        [SerializeField] private GameObject actionPointPipTemplate;
        [SerializeField] private ScrollRect contentScroll;
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private RectTransform statusRoot;
        [SerializeField] private RectTransform skillRoot;

        [SerializeField] private CombatStatusInspectEntryView statusTemplate;
        [SerializeField] private CombatSkillInspectEntryView skillTemplate;
        [SerializeField] private GameObject emptyStatuses;
        [SerializeField] private GameObject emptySkills;
        private readonly List<GameObject> statusRows = new();
        private readonly List<GameObject> skillRows = new();
        private readonly List<Image> actionPointPips = new();

        private readonly CharacterStatHoverController statHover = new();
        private RectTransform rectTransform;
        private BattleUnit boundUnit;
        private UIWorkspaceTooltipView tooltip;
        private bool closeListenerAttached;
        private string statusSignature;
        private string skillSignature;

        public BattleUnit BoundUnit => boundUnit;
        public bool IsVisible => gameObject.activeSelf;

        public static CombatUnitInspectWindowView Create(
            Transform parent,
            CombatUnitInspectWindowView prefab = null)
        {
            if (parent == null) return null;

            if (prefab != null)
            {
                var instance = Instantiate(prefab, parent, false);
                instance.ResolveReferences();
                instance.AttachCloseListener();
                instance.Hide();
                return instance;
            }

            Debug.LogError("Combat inspect requires its UIW prefab.");
            return null;
        }

        private void Awake()
        {
            ResolveReferences();
            AttachCloseListener();
        }

        private void OnDestroy()
        {
            if (closeListenerAttached && closeButton != null)
            {
                closeButton.onClick.RemoveListener(Hide);
            }
        }

        public void Show(
            BattleUnit unit,
            UIWorkspaceTooltipView sourceTooltip = null)
        {
            if (unit?.Character == null)
            {
                Hide();
                return;
            }

            tooltip = sourceTooltip ?? UIWorkspaceTooltipView.ActiveInstance;
            var isNewUnit = boundUnit != unit;
            boundUnit = unit;
            gameObject.SetActive(true);
            if (canvasGroup != null) canvasGroup.alpha = 1f;
            Refresh();
            if (isNewUnit && contentScroll != null)
            {
                contentScroll.verticalNormalizedPosition = 1f;
            }

            window?.Focus();
        }

        public void Refresh()
        {
            if (boundUnit?.Character == null || titleText == null)
            {
                return;
            }

            var character = boundUnit.Character;
            var definition = character.Definition;
            var name = definition?.DisplayName ?? character.CharacterId;
            if (titleText != null) titleText.text = name;
            if (identityText != null)
            {
                identityText.text = $"{ClassLabel(character.ClassData?.ClassName)}  •  " +
                    $"Poziom {character.Level}" + (character.LifeState == CharacterLifeState.Alive ? string.Empty : character.LifeState == CharacterLifeState.Dead ? "  •  Nie żyje" : "  •  Powalony");
            }

            var maxHealth = character.Stats == null
                ? 1f
                : character.MaximumHealth;
            var shield = character.ShieldPool?.TotalShield ?? 0;
            var resource = character.ResourcePool;

            if (portrait != null)
            {
                portrait.sprite = definition?.UiPortraitSprite;
                portrait.enabled = portrait.sprite != null;
            }

            if(vitals != null) vitals.Bind(boundUnit,statValueLinks:true);
            else
            {
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
                healthValueText.text = CharacterStatValueLinks.Health(character);
            }

            SetValueTextVisible(resourceValueText, resource != null && resource.MaxValue > 0);
            if (resourceValueText != null && resource != null)
            {
                resourceValueText.text = CharacterStatValueLinks.Resource(character);
            }

            if (sideFrame != null)
            {
                sideFrame.color = boundUnit.IsPlayerSide
                    ? new Color(.88f, .67f, .24f, 1f)
                    : new Color(.78f, .20f, .18f, 1f);
            }

            RefreshActionPointPips(boundUnit);
            }

            if(resourceLabelText!=null)
            {
                resourceLabelText.text=resource==null?string.Empty:CombatResourceColors.ResourceName(resource.ResourceType);
                resourceLabelText.gameObject.SetActive(resource!=null && resource.MaxValue>0);
            }
            if(resourceValueText!=null)resourceValueText.gameObject.SetActive(resource!=null && resource.MaxValue>0);
            var hasActionPoints=Mathf.Max(boundUnit.MaxActionPoints,boundUnit.CurrentActionPoints)>0;
            if(actionValueText!=null)actionValueText.gameObject.SetActive(hasActionPoints);
            if(actionPointPipRoot!=null)actionPointPipRoot.gameObject.SetActive(hasActionPoints);
            if(overviewLayout!=null)overviewLayout.minHeight=overviewLayout.preferredHeight=hasActionPoints?140:112;

            RefreshStatColumns();
            if(statsLayout!=null)
                statsLayout.minHeight=statsLayout.preferredHeight=Mathf.Ceil(Mathf.Max(statsLeftText?.preferredHeight??0,statsRightText?.preferredHeight??0)+30);
            tooltip ??= UIWorkspaceTooltipView.ActiveInstance;
            var nextStatusSignature = BuildStatusSignature(
                character.PassiveEffects,
                boundUnit.ActiveAuras,
                boundUnit.OutgoingDamageRedirects,
                boundUnit);
            if (!string.Equals(statusSignature, nextStatusSignature, StringComparison.Ordinal))
            {
                statusSignature = nextStatusSignature;
                RebuildStatusRows(
                    character.PassiveEffects,
                    boundUnit.ActiveAuras,
                    boundUnit.OutgoingDamageRedirects,
                    boundUnit);
            }

            var nextSkillSignature = BuildSkillSignature(character);
            if (!string.Equals(skillSignature, nextSkillSignature, StringComparison.Ordinal))
            {
                skillSignature = nextSkillSignature;
                RebuildSkillRows(character, boundUnit);
            }

            Canvas.ForceUpdateCanvases();
            if (contentRoot != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
            }
        }

        public void Hide()
        {
            statHover.Clear();
            boundUnit = null;
            statusSignature = null;
            skillSignature = null;
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

        private void OnDisable() => statHover.Clear();
        private void LateUpdate()
        {
            if(boundUnit?.Character==null)return;
            RefreshStatColumns();
            statHover.Tick(Input.mousePosition);
        }
        private void RefreshStatColumns()
        {
            var character=boundUnit.Character;
            BuildStatsColumns(character,out var left,out var right);
            ApplyStatsColumn(statsLeftText,statsLeftValues,CharacterStatValueLinks.Decorate(left,character));
            ApplyStatsColumn(statsRightText,statsRightValues,CharacterStatValueLinks.Decorate(right,character,true));
            statHover.Bind(character,transform,tooltip,statsLeftText,statsLeftValues,statsRightText,statsRightValues,
                vitals!=null?vitals.HealthValueText:healthValueText,vitals!=null?vitals.ResourceValueText:resourceValueText);
        }

        private void RefreshActionPointPips(BattleUnit unit)
        {
            var maximum = ActionPointUnits.PipCount(Mathf.Max(unit?.MaxActionPoints ?? 0, unit?.CurrentActionPoints ?? 0));
            EnsureActionPointPipCount(maximum);

            if (actionPointPipRoot != null)
            {
                actionPointPipRoot.gameObject.SetActive(maximum > 0);
            }

            for (var index = 0; index < actionPointPips.Count; index++)
            {
                var pip = actionPointPips[index];
                if (pip == null) continue;

                var isVisible = index < maximum;
                pip.gameObject.SetActive(isVisible);
                if (isVisible)
                {
                    ActionPointPipPresentation.Apply(pip, unit.CurrentActionPoints, index,
                        Color.white, new Color(.12f, .15f, .19f, .95f));
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
            if (slider != null) slider.gameObject.SetActive(visible);
        }

        private static void SetSliderValue(
            Slider slider,
            float currentValue,
            float maximumValue)
        {
            if (slider == null) return;

            slider.minValue = 0f;
            slider.maxValue = Mathf.Max(1f, maximumValue);
            slider.SetValueWithoutNotify(Mathf.Clamp(
                currentValue,
                0f,
                slider.maxValue));
        }

        private static void SetValueTextVisible(
            TextMeshProUGUI text,
            bool visible)
        {
            if (text != null) text.gameObject.SetActive(visible);
        }

        private static string FormatHealthValue(
            int health,
            float maximumHealth,
            int shield)
        {
            return CombatHealthPresentation.Format(health, maximumHealth, shield);
        }

        private void ResolveReferences()
        {
            rectTransform ??= GetComponent<RectTransform>();
        }

        private void AttachCloseListener()
        {
            if (closeListenerAttached || closeButton == null) return;

            closeButton.onClick.AddListener(Hide);
            closeListenerAttached = true;
        }

        private void RebuildStatusRows(IEnumerable<PassiveEffectRuntime> effects,
            IEnumerable<CombatAuraRuntime> activeAuras, IEnumerable<CombatDamageRedirectRuntime> outgoingRedirects, BattleUnit owner)
        {
            DestroyRows(statusRows);
            if (statusRoot == null || statusTemplate == null) return;
            foreach (var presentation in CombatStatusPresentationFactory.Create(effects, activeAuras, outgoingRedirects, owner))
            {
                var row = Instantiate(statusTemplate, statusRoot);
                row.Initialize(presentation, tooltip);
                row.gameObject.SetActive(true);
                statusRows.Add(row.gameObject);
            }
            if (emptyStatuses != null) emptyStatuses.SetActive(statusRows.Count == 0);
            if (statusesHeaderText != null) statusesHeaderText.gameObject.SetActive(statusRows.Count > 0);
        }

        private void RebuildSkillRows(CharacterInstance character, BattleUnit caster)
        {
            DestroyRows(skillRows);
            if (skillRoot == null || skillTemplate == null) return;
            var skills = character.GetKnownSkills();
            if (skills == null || skills.Count == 0) skills = new List<Godmist.Data.Skills.SkillData>(character.GetActiveSkills());
            foreach (var skill in skills)
            {
                if (skill == null) continue;
                var row = Instantiate(skillTemplate, skillRoot);
                row.Initialize(skill, caster, tooltip);
                row.gameObject.SetActive(true);
                skillRows.Add(row.gameObject);
            }
            if (emptySkills != null) emptySkills.SetActive(skillRows.Count == 0);
        }

        private static string BuildStatusSignature(
            IEnumerable<PassiveEffectRuntime> effects,
            IEnumerable<CombatAuraRuntime> activeAuras,
            IEnumerable<CombatDamageRedirectRuntime> outgoingRedirects,
            BattleUnit owner = null)
        {
            var result = new StringBuilder();
            foreach (var effect in CombatStatusPresentationFactory.Create(
                         effects,
                         activeAuras,
                         outgoingRedirects,
                         owner))
            {
                result.Append('|')
                    .Append(effect.Effect?.EffectId)
                    .Append(':')
                    .Append(effect.Effect?.DisplayName)
                    .Append(':')
                    .Append(effect.Effect?.RemainingDuration)
                    .Append(':')
                    .Append(effect.Aura?.AuraId)
                    .Append(':')
                    .Append(effect.Aura?.StatusDamagePerTurn)
                    .Append(':')
                    .Append(effect.Aura?.StatusDamageMultiplier)
                    .Append(':')
                    .Append(effect.OutgoingRedirect?.SourceEffectId)
                    .Append(':')
                    .Append(effect.OutgoingRedirect?.TargetCharacterId)
                    .Append(':')
                    .Append(effect.OutgoingRedirect?.RedirectPercent);
                foreach (var member in effect.Effects)
                    result.Append(';').Append(member.EffectId).Append(':')
                        .Append(member.RemainingDuration).Append(':').Append(member.Stacks);
            }

            return result.ToString();
        }

        private static string BuildSkillSignature(CharacterInstance character)
        {
            var result = new StringBuilder();
            var skills = character?.GetKnownSkills();
            if (skills == null || skills.Count == 0)
            {
                skills = character == null
                    ? new List<Godmist.Data.Skills.SkillData>()
                    : new List<Godmist.Data.Skills.SkillData>(character.GetActiveSkills());
            }

            foreach (var skill in skills)
            {
                result.Append('|').Append(skill?.SkillId);
            }

            return result.ToString();
        }

        private static string ClassLabel(string name) => name switch
        { "Warrior" => "Wojownik", "Scout" => "Zwiadowca", "Paladin" => "Paladyn", "Mage" => "Mag", _ => name ?? "Jednostka" };

        private static void BuildStatsColumns(
            CharacterInstance character,
            out string leftColumn,
            out string rightColumn)
        {
            var stats = character.Stats;
            if (stats == null)
            {
                leftColumn = "Statystyki niedostępne.";
                rightColumn = string.Empty;
                return;
            }

            var left = new StringBuilder();
            AppendStatRange(left, UiTerms.Attack, FormatRange(
                stats.MinAttack.GetValue(),
                stats.MaxAttack.GetValue()));
            AppendStat(left, UiTerms.Stat(CharacterStatType.PhysicalDefense), stats.PhysicalDefense.GetValue(), format: "0");
            AppendStat(left, UiTerms.Stat(CharacterStatType.MagicalDefense), stats.MagicalDefense.GetValue(), format: "0");
            AppendStat(left, UiTerms.Stat(CharacterStatType.PhysicalDefensePenetration),
                stats.PhysicalDefensePenetration.GetValue() * 100f, "%");
            AppendStat(left, UiTerms.Stat(CharacterStatType.MagicalDefensePenetration),
                stats.MagicalDefensePenetration.GetValue() * 100f, "%");
            AppendStat(left, UiTerms.Stat(CharacterStatType.WeaponAccuracy), character.WeaponAccuracy, format: "0");
            AppendStat(left, UiTerms.Stat(CharacterStatType.Dodge), stats.Dodge.GetValue(), format: "0");
            AppendStat(left, UiTerms.Stat(CharacterStatType.CritChance),
                stats.CritChance.GetValue() * 100f, "%");
            AppendStatRange(left, UiTerms.Stat(CharacterStatType.CritDamage),
                "×" + stats.CritDamage.GetValue().ToString("0.##", UiTerms.PolishCulture));

            AppendStat(left, UiTerms.Stat(CharacterStatType.Speed), stats.Speed.GetValue());

            var resource = character.ResourcePool;
            if (resource?.ResourceType == ResourceType.Mana)
            {
                AppendStat(left, UiTerms.Stat(CharacterStatType.ResourceRegeneration, resource.ResourceType), resource.Regeneration);
            }

            var right = new StringBuilder();
            AppendStat(right, UiTerms.ResistanceRow(ResistanceType.Bleed),
                character.Resistances?.BleedResistance ?? 0f, "%", true);
            AppendStat(right, UiTerms.ResistanceRow(ResistanceType.Poison),
                character.Resistances?.PoisonResistance ?? 0f, "%", true);
            AppendStat(right, UiTerms.ResistanceRow(ResistanceType.Fire),
                character.Resistances?.FireResistance ?? 0f, "%", true);
            AppendStat(right, UiTerms.ResistanceRow(ResistanceType.Weakening),
                character.Resistances?.WeakeningResistance ?? 0f, "%", true);
            AppendStat(right, UiTerms.ResistanceRow(ResistanceType.Suppression),
                character.Resistances?.SuppressionResistance ?? 0f, "%", true);
            AppendStat(right, UiTerms.ResistanceRow(ResistanceType.Move),
                character.Resistances?.MoveResistance ?? 0f, "%", true);

            leftColumn = left.ToString();
            rightColumn = right.ToString();
        }

        private static void AppendStat(
            StringBuilder result,
            string label,
            float value,
            string suffix = "",
            bool valueIsFraction = false,
            string format = "0.#")
        {
            if (result.Length > 0) result.Append('\n');
            var displayedValue = valueIsFraction ? value * 100f : value;
            result.Append(label)
                .Append(": ")
                .Append(displayedValue.ToString(format, UiTerms.PolishCulture))
                .Append(suffix);
        }

        private static void ApplyStatsColumn(TMP_Text labels,TMP_Text values,string content)
        {
            if(labels==null)return;
            if(values==null){labels.text=content;return;}
            var names=new StringBuilder();var numbers=new StringBuilder();
            foreach(var line in content.Split('\n'))
            {
                if(names.Length>0){names.Append('\n');numbers.Append('\n');}
                var separator=line.IndexOf(": ",StringComparison.Ordinal);
                names.Append(separator<0?line:line.Substring(0,separator));
                if(separator>=0)
                {
                    var value=line.Substring(separator+2);
                    numbers.Append(value.EndsWith("x",StringComparison.Ordinal)?"×"+value.Substring(0,value.Length-1):value);
                }
            }
            labels.text=names.ToString();values.text=numbers.ToString();
        }

        private static void AppendStatRange(StringBuilder result, string label, string value)
        {
            if (result.Length > 0) result.Append('\n');
            result.Append(label).Append(": ").Append(value);
        }

        private static string FormatRange(float minimum, float maximum)
        {
            return $"{Mathf.RoundToInt(minimum)}–{Mathf.RoundToInt(maximum)}";
        }

        private string FormatStatus(PassiveEffectRuntime effect)
        {
            var name = HeroCombatStatusText.Name(effect.EffectId, effect.DisplayName);
            if (string.IsNullOrWhiteSpace(name)) name = tooltip?.GetGlossaryTitle(effect.EffectId);
            if (string.IsNullOrWhiteSpace(name)) name = FormatEffectId(effect.EffectId);
            return effect.RemainingDuration > 0 && !HeroCombatStatusText.IsFieldSlow(effect.EffectId)
                ? $"{name}  •  {effect.RemainingDuration} t."
                : name;
        }

        private static string FormatEffectId(string effectId)
        {
            var words = (effectId ?? string.Empty).Replace('_', ' ').Replace('-', ' ').Split(' ');
            for (var index = 0; index < words.Length; index++)
            {
                if (string.IsNullOrWhiteSpace(words[index])) continue;
                words[index] = char.ToUpperInvariant(words[index][0]) + words[index].Substring(1);
            }

            return string.Join(" ", words);
        }

        private static void DestroyRows(List<GameObject> rows)
        {
            foreach (var row in rows)
            {
                if (row != null) Destroy(row);
            }

            rows.Clear();
        }
    }
}

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
    /// instantiates a UI Workspace prefab; a small runtime fallback remains useful
    /// for scenes that have not been wired yet.
    /// </summary>
    public sealed class CombatUnitInspectWindowView : MonoBehaviour
    {
        [Header("UI Workspace references")]
        [SerializeField] private UIWorkspaceWindow window;
        [SerializeField] private Button closeButton;
        [SerializeField] private Image background;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI identityText;
        [SerializeField] private TextMeshProUGUI statsLeftText;
        [SerializeField] private TextMeshProUGUI statsRightText;
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

        private readonly List<GameObject> statusRows = new();
        private readonly List<GameObject> skillRows = new();
        private readonly List<Image> actionPointPips = new();

        private RectTransform rectTransform;
        private BattleUnit boundUnit;
        private UIWorkspaceTooltipView tooltip;
        private bool hasPositioned;
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

            var gameObject = new GameObject(
                "Combat Unit Inspect Window",
                typeof(RectTransform),
                typeof(Image),
                typeof(CanvasGroup));
            gameObject.transform.SetParent(parent, false);
            var view = gameObject.AddComponent<CombatUnitInspectWindowView>();
            view.Build();
            view.ResolveReferences();
            view.AttachCloseListener();
            view.Hide();
            return view;
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
            if (!hasPositioned)
            {
                rectTransform.anchoredPosition = Vector2.zero;
                hasPositioned = true;
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
                identityText.text = $"{character.ClassData?.ClassName ?? "Jednostka"}  •  " +
                    $"Poziom {character.Level}  •  {character.LifeState}";
            }

            var maxHealth = character.Stats == null
                ? 1
                : Mathf.Max(1, Mathf.RoundToInt(character.Stats.MaxHealth.GetValue()));
            var shield = character.ShieldPool?.TotalShield ?? 0;
            var resource = character.ResourcePool;

            if (portrait != null)
            {
                portrait.sprite = definition?.VisualSprite;
                portrait.enabled = portrait.sprite != null;
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

            if (sideFrame != null)
            {
                sideFrame.color = boundUnit.IsPlayerSide
                    ? new Color(.88f, .67f, .24f, 1f)
                    : new Color(.78f, .20f, .18f, 1f);
            }

            RefreshActionPointPips(boundUnit);

            BuildStatsColumns(character, out var statsLeft, out var statsRight);
            if (statsLeftText != null) statsLeftText.text = statsLeft;
            if (statsRightText != null) statsRightText.text = statsRight;
            tooltip ??= UIWorkspaceTooltipView.ActiveInstance;
            var nextStatusSignature = BuildStatusSignature(character.PassiveEffects);
            if (!string.Equals(statusSignature, nextStatusSignature, StringComparison.Ordinal))
            {
                statusSignature = nextStatusSignature;
                RebuildStatusRows(character.PassiveEffects);
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
            boundUnit = null;
            statusSignature = null;
            skillSignature = null;
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

        private void RefreshActionPointPips(BattleUnit unit)
        {
            var maximum = Mathf.Max(0, unit?.MaxActionPoints ?? 0);
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
                    pip.color = index < unit.CurrentActionPoints
                        ? Color.white
                        : new Color(.12f, .15f, .19f, .95f);
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
            int maximumHealth,
            int shield)
        {
            var value = $"{health}/{maximumHealth}";
            return shield > 0 ? $"{value} (+{shield})" : value;
        }

        private void Build()
        {
            rectTransform = transform as RectTransform;
            background = GetComponent<Image>();
            canvasGroup = GetComponent<CanvasGroup>();
            window = gameObject.AddComponent<UIWorkspaceWindow>();
            window.ConfigureResizeAxes(false, true);

            rectTransform.anchorMin = new Vector2(.5f, .5f);
            rectTransform.anchorMax = new Vector2(.5f, .5f);
            rectTransform.pivot = new Vector2(.5f, .5f);
            rectTransform.sizeDelta = new Vector2(420f, 620f);
            background.color = new Color(.035f, .055f, .10f, .98f);
            background.raycastTarget = true;
            canvasGroup.alpha = 1f;

            var header = CreateRect("Header", rectTransform, new Vector2(0f, 1f),
                new Vector2(1f, 1f), new Vector2(.5f, 1f), Vector2.zero,
                new Vector2(-16f, -48f));
            var headerImage = header.gameObject.AddComponent<Image>();
            headerImage.color = new Color(.08f, .13f, .22f, 1f);
            headerImage.raycastTarget = true;
            header.gameObject.AddComponent<UIWorkspaceWindowDragHandle>();

            titleText = CreateText("Title", header, 20f, FontStyles.Bold,
                TextAlignmentOptions.Left, Color.white);
            Stretch(titleText.rectTransform, new Vector2(14f, 0f), new Vector2(-46f, 0f));

            var close = CreateRect("Close", header, new Vector2(1f, .5f),
                new Vector2(1f, .5f), new Vector2(1f, .5f),
                new Vector2(-16f, 0f), new Vector2(30f, 30f));
            var closeImage = close.gameObject.AddComponent<Image>();
            closeImage.color = new Color(.45f, .16f, .18f, 1f);
            closeButton = close.gameObject.AddComponent<Button>();
            closeButton.targetGraphic = closeImage;
            var closeText = CreateText("Close Text", close, 18f, FontStyles.Bold,
                TextAlignmentOptions.Center, Color.white);
            Stretch(closeText.rectTransform, Vector2.zero, Vector2.zero);
            closeText.text = "×";

            var scrollObject = CreateRect("Scroll View", rectTransform,
                Vector2.zero, Vector2.one, new Vector2(.5f, .5f),
                new Vector2(0f, -23f), new Vector2(-24f, -70f));
            var scrollImage = scrollObject.gameObject.AddComponent<Image>();
            scrollImage.color = new Color(.055f, .08f, .14f, .98f);
            scrollImage.raycastTarget = true;
            contentScroll = scrollObject.gameObject.AddComponent<ScrollRect>();
            contentScroll.horizontal = false;
            contentScroll.vertical = true;
            contentScroll.movementType = ScrollRect.MovementType.Clamped;
            contentScroll.scrollSensitivity = 28f;

            var viewport = CreateRect("Viewport", scrollObject,
                Vector2.zero, Vector2.one, new Vector2(.5f, .5f),
                Vector2.zero, Vector2.zero);
            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = Color.white;
            viewportImage.raycastTarget = true;
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            contentRoot = CreateRect("Content", viewport,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(.5f, 1f), Vector2.zero, new Vector2(0f, 0f));
            ConfigureVerticalLayout(contentRoot, true);
            contentScroll.viewport = viewport;
            contentScroll.content = contentRoot;

            identityText = CreateText("Identity", contentRoot, 13f, FontStyles.Normal,
                TextAlignmentOptions.Left, new Color(.73f, .82f, .94f));
            ConfigureContentText(identityText, 22f);

            var overview = CreateRect("Overview", contentRoot,
                Vector2.zero, Vector2.one, new Vector2(.5f, .5f),
                Vector2.zero, Vector2.zero);
            overview.gameObject.AddComponent<LayoutElement>().preferredHeight = 122f;
            var overviewLayout = overview.gameObject.AddComponent<HorizontalLayoutGroup>();
            overviewLayout.padding = new RectOffset(4, 4, 4, 4);
            overviewLayout.spacing = 12f;
            overviewLayout.childAlignment = TextAnchor.MiddleLeft;
            overviewLayout.childControlWidth = true;
            overviewLayout.childControlHeight = true;
            overviewLayout.childForceExpandWidth = false;
            overviewLayout.childForceExpandHeight = true;

            portrait = CreateRect("Portrait", overview,
                Vector2.zero, Vector2.one, new Vector2(.5f, .5f),
                Vector2.zero, new Vector2(84f, 84f))
                .gameObject.AddComponent<Image>();
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            portrait.gameObject.AddComponent<LayoutElement>().preferredWidth = 84f;

            var bars = CreateRect("Combat Bars", overview,
                Vector2.zero, Vector2.one, new Vector2(.5f, .5f),
                Vector2.zero, Vector2.zero);
            var barsLayout = bars.gameObject.AddComponent<VerticalLayoutGroup>();
            barsLayout.spacing = 4f;
            barsLayout.childAlignment = TextAnchor.MiddleLeft;
            barsLayout.childControlWidth = true;
            barsLayout.childControlHeight = true;
            barsLayout.childForceExpandWidth = true;
            barsLayout.childForceExpandHeight = false;
            bars.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            healthSlider = CreateSlider("Health", bars,
                new Color(.16f, .12f, .14f, 1f), new Color(.78f, .24f, .28f, 1f));
            healthValueText = CreateSliderValueText(healthSlider, "Health Value");

            shieldSlider = CreateSlider("Shield", bars,
                new Color(.12f, .14f, .18f, 1f), new Color(.32f, .65f, .94f, 1f));

            resourceSlider = CreateSlider("Resource", bars,
                new Color(.12f, .14f, .18f, 1f), new Color(.3f, .78f, .55f, 1f));
            resourceValueText = CreateSliderValueText(resourceSlider, "Resource Value");

            var actionPoints = CreateRect("Action Points", bars,
                Vector2.zero, Vector2.one, new Vector2(.5f, .5f),
                Vector2.zero, new Vector2(0f, 18f));
            actionPoints.gameObject.AddComponent<LayoutElement>().preferredHeight = 18f;
            var actionPointsLayout = actionPoints.gameObject.AddComponent<HorizontalLayoutGroup>();
            actionPointsLayout.spacing = 3f;
            actionPointsLayout.childAlignment = TextAnchor.MiddleLeft;
            actionPointsLayout.childControlWidth = false;
            actionPointsLayout.childControlHeight = true;
            actionPointsLayout.childForceExpandWidth = false;
            actionPointsLayout.childForceExpandHeight = false;
            actionPointPipRoot = actionPoints;
            var pipTemplateObject = new GameObject(
                "Action Point Pip Template",
                typeof(RectTransform),
                typeof(Image));
            pipTemplateObject.transform.SetParent(actionPoints, false);
            var pipTemplateRect = pipTemplateObject.transform as RectTransform;
            pipTemplateRect.sizeDelta = new Vector2(12f, 12f);
            pipTemplateObject.GetComponent<Image>().color = new Color(.88f, .67f, .24f, 1f);
            actionPointPipTemplate = pipTemplateObject;
            pipTemplateObject.SetActive(false);

            var statsRoot = CreateRect("Stats", contentRoot,
                Vector2.zero, Vector2.one, new Vector2(.5f, 1f),
                Vector2.zero, Vector2.zero);
            var statsLayout = statsRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            statsLayout.spacing = 12f;
            statsLayout.childAlignment = TextAnchor.UpperLeft;
            statsLayout.childControlWidth = true;
            statsLayout.childControlHeight = true;
            statsLayout.childForceExpandWidth = true;
            statsLayout.childForceExpandHeight = false;
            var statsFitter = statsRoot.gameObject.AddComponent<ContentSizeFitter>();
            statsFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            statsFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            statsLeftText = CreateText("Stats Left", statsRoot, 13f,
                FontStyles.Normal, TextAlignmentOptions.Left,
                new Color(.88f, .9f, .95f));
            ConfigureContentText(statsLeftText);
            statsLeftText.GetComponent<LayoutElement>().flexibleWidth = 1f;

            statsRightText = CreateText("Stats Right", statsRoot, 13f,
                FontStyles.Normal, TextAlignmentOptions.Left,
                new Color(.88f, .9f, .95f));
            ConfigureContentText(statsRightText);
            statsRightText.GetComponent<LayoutElement>().flexibleWidth = 1f;

            statusesHeaderText = CreateText("Statuses Header", contentRoot, 15f,
                FontStyles.Bold, TextAlignmentOptions.Left, Color.white);
            ConfigureContentText(statusesHeaderText, 24f);
            statusesHeaderText.text = "Statusy / buffy / debuffy";

            statusRoot = CreateRect("Status List", contentRoot,
                Vector2.zero, Vector2.one, new Vector2(.5f, 1f),
                Vector2.zero, Vector2.zero);
            ConfigureVerticalLayout(statusRoot, false);

            skillsHeaderText = CreateText("Skills Header", contentRoot, 15f,
                FontStyles.Bold, TextAlignmentOptions.Left, Color.white);
            ConfigureContentText(skillsHeaderText, 24f);
            skillsHeaderText.text = "Skille";

            skillRoot = CreateRect("Skill List", contentRoot,
                Vector2.zero, Vector2.one, new Vector2(.5f, 1f),
                Vector2.zero, Vector2.zero);
            ConfigureVerticalLayout(skillRoot, false);

            var resizeHandle = CreateRect("Resize Handle", rectTransform,
                new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(1f, 0f), new Vector2(-10f, 10f),
                new Vector2(20f, 20f));
            var resizeImage = resizeHandle.gameObject.AddComponent<Image>();
            resizeImage.color = new Color(.49f, .65f, .78f, .45f);
            resizeImage.raycastTarget = true;
            resizeHandle.gameObject.AddComponent<UIWorkspaceWindowResizeHandle>();
        }

        private void ResolveReferences()
        {
            rectTransform ??= GetComponent<RectTransform>();
            background ??= GetComponent<Image>();
            canvasGroup ??= GetComponent<CanvasGroup>();
            window ??= GetComponent<UIWorkspaceWindow>();
            titleText ??= FindChildComponent<TextMeshProUGUI>("Title");
            identityText ??= FindChildComponent<TextMeshProUGUI>("Identity");
            statsLeftText ??= FindChildComponent<TextMeshProUGUI>("Stats Left");
            statsRightText ??= FindChildComponent<TextMeshProUGUI>("Stats Right");
            statusesHeaderText ??= FindChildComponent<TextMeshProUGUI>("Statuses Header");
            skillsHeaderText ??= FindChildComponent<TextMeshProUGUI>("Skills Header");
            portrait ??= FindChildComponent<Image>("Portrait");
            healthSlider ??= FindChildComponent<Slider>("Health");
            shieldSlider ??= FindChildComponent<Slider>("Shield");
            resourceSlider ??= FindChildComponent<Slider>("Resource");
            healthValueText ??= FindChildComponent<TextMeshProUGUI>("Health Value");
            resourceValueText ??= FindChildComponent<TextMeshProUGUI>("Resource Value");
            sideFrame ??= FindChildComponent<Image>("Side Accent");
            actionPointPipRoot ??= FindChildComponent<Transform>("Action Points");
            actionPointPipTemplate ??= FindChildGameObject("Action Point Pip Template");
            contentScroll ??= GetComponentInChildren<ScrollRect>(true);
            contentRoot ??= contentScroll?.content;
            statusRoot ??= FindChildComponent<RectTransform>("Status List");
            skillRoot ??= FindChildComponent<RectTransform>("Skill List");

            window?.ConfigureResizeAxes(false, true);
            ConfigureScrollViewport();
        }

        private void AttachCloseListener()
        {
            if (closeListenerAttached || closeButton == null) return;

            closeButton.onClick.AddListener(Hide);
            closeListenerAttached = true;
        }

        private void ConfigureScrollViewport()
        {
            if (contentScroll == null)
            {
                return;
            }

            contentScroll.horizontal = false;
            contentScroll.vertical = true;
            contentScroll.movementType = ScrollRect.MovementType.Clamped;

            var viewport = contentScroll.viewport;
            if (viewport == null)
            {
                return;
            }

            var legacyMask = viewport.GetComponent<Mask>();
            if (legacyMask != null)
            {
                legacyMask.enabled = false;
            }

            var rectMask = viewport.GetComponent<RectMask2D>()
                ?? viewport.gameObject.AddComponent<RectMask2D>();
            rectMask.enabled = true;
        }

        private T FindChildComponent<T>(string childName)
            where T : Component
        {
            foreach (var component in GetComponentsInChildren<T>(true))
            {
                if (component != null && component.gameObject.name == childName)
                {
                    return component;
                }
            }

            return null;
        }

        private GameObject FindChildGameObject(string childName)
        {
            foreach (var child in GetComponentsInChildren<Transform>(true))
            {
                if (child != null && child.gameObject.name == childName)
                {
                    return child.gameObject;
                }
            }

            return null;
        }

        private static void ConfigureContentText(
            TextMeshProUGUI text,
            float minimumHeight = 0f)
        {
            if (text == null) return;

            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            var fitter = text.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var layout = text.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = minimumHeight;
        }

        private static Slider CreateSlider(
            string name,
            Transform parent,
            Color backgroundColor,
            Color fillColor)
        {
            var gameObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image),
                typeof(Slider),
                typeof(LayoutElement));
            gameObject.transform.SetParent(parent, false);

            var rect = gameObject.transform as RectTransform;
            rect.sizeDelta = new Vector2(0f, 20f);
            var background = gameObject.GetComponent<Image>();
            background.color = backgroundColor;
            background.raycastTarget = false;

            var fillRect = CreateRect(
                "Fill",
                rect,
                Vector2.zero,
                Vector2.one,
                new Vector2(.5f, .5f),
                Vector2.zero,
                Vector2.zero);
            var fillImage = fillRect.gameObject.AddComponent<Image>();
            fillImage.color = fillColor;
            fillImage.raycastTarget = false;

            var slider = gameObject.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.direction = Slider.Direction.LeftToRight;
            slider.fillRect = fillRect;
            slider.targetGraphic = background;
            gameObject.GetComponent<LayoutElement>().preferredHeight = 20f;
            return slider;
        }

        private static TextMeshProUGUI CreateSliderValueText(
            Slider slider,
            string name)
        {
            var text = CreateText(
                name,
                slider.transform,
                12f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                Color.white);
            Stretch(text.rectTransform, Vector2.zero, Vector2.zero);
            return text;
        }

        private void RebuildStatusRows(IEnumerable<PassiveEffectRuntime> effects)
        {
            DestroyRows(statusRows);
            if (statusRoot == null) return;

            var hasEffects = false;
            foreach (var presentation in CombatStatusPresentationFactory.Create(effects))
            {
                hasEffects = true;
                var effect = presentation.Effect;
                var row = CreateRow("Status", statusRoot, 24f);
                var icon = CreateRect("Icon", row.transform as RectTransform,
                    new Vector2(0f, .5f), new Vector2(0f, .5f),
                    new Vector2(0f, .5f), new Vector2(12f, 0f), new Vector2(20f, 20f))
                    .gameObject.AddComponent<Image>();
                icon.sprite = tooltip?.GetGlossaryIcon(effect.EffectId);
                icon.enabled = icon.sprite != null;
                icon.raycastTarget = false;

                var label = CreateText("Label", row.transform as RectTransform, 12f,
                    FontStyles.Normal, TextAlignmentOptions.Left,
                    presentation.Kind == CombatStatusKind.Debuff
                        ? new Color(1f, .52f, .46f)
                        : presentation.Kind == CombatStatusKind.Buff
                            ? new Color(.42f, 1f, .67f)
                            : Color.white);
                SetRect(label.rectTransform, new Vector2(0f, 0f),
                    new Vector2(1f, 1f), new Vector2(0f, .5f),
                    new Vector2(40f, 0f), new Vector2(-44f, 0f));
                label.text = FormatStatus(effect);
                var entry = row.AddComponent<CombatStatusInspectEntryView>();
                entry.Initialize(presentation, tooltip);
                statusRows.Add(row);
            }

            if (!hasEffects)
            {
                statusRows.Add(CreateEmptyStateText(
                    "Brak efektów",
                    statusRoot));
            }
        }

        private void RebuildSkillRows(CharacterInstance character, BattleUnit caster)
        {
            DestroyRows(skillRows);
            if (skillRoot == null) return;

            var skills = character.Skills;
            if (skills == null || skills.Count == 0)
            {
                skills = new List<Godmist.Data.Skills.SkillData>(character.GetActiveSkills());
            }

            var hasSkills = false;
            foreach (var skill in skills)
            {
                if (skill == null) continue;
                hasSkills = true;
                var row = CreateRow("Skill", skillRoot, 26f);
                var icon = CreateRect("Icon", row.transform as RectTransform,
                    new Vector2(0f, .5f), new Vector2(0f, .5f),
                    new Vector2(0f, .5f), new Vector2(10f, 0f), new Vector2(22f, 22f))
                    .gameObject.AddComponent<Image>();
                icon.sprite = skill.Icon;
                icon.enabled = icon.sprite != null;
                icon.raycastTarget = false;

                var label = CreateText("Label", row.transform as RectTransform, 13f,
                    FontStyles.Normal, TextAlignmentOptions.Left, Color.white);
                SetRect(label.rectTransform, new Vector2(0f, 0f),
                    new Vector2(1f, 1f), new Vector2(0f, .5f),
                    new Vector2(40f, 0f), new Vector2(-44f, 0f));
                label.text = skill.DisplayName;
                row.AddComponent<CombatSkillInspectEntryView>()
                    .Initialize(skill, caster, tooltip);
                skillRows.Add(row);
            }

            if (!hasSkills)
            {
                skillRows.Add(CreateEmptyStateText(
                    "Brak umiejętności",
                    skillRoot));
            }
        }

        private static string BuildStatusSignature(IEnumerable<PassiveEffectRuntime> effects)
        {
            var result = new StringBuilder();
            foreach (var effect in CombatStatusPresentationFactory.Create(effects))
            {
                result.Append('|')
                    .Append(effect.Effect?.EffectId)
                    .Append(':')
                    .Append(effect.Effect?.DisplayName)
                    .Append(':')
                    .Append(effect.Effect?.RemainingDuration);
            }

            return result.ToString();
        }

        private static string BuildSkillSignature(CharacterInstance character)
        {
            var result = new StringBuilder();
            var skills = character?.Skills;
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
            AppendStatRange(left, "Atak", FormatRange(
                stats.MinAttack.GetValue(),
                stats.MaxAttack.GetValue()));
            AppendStat(left, "Obrona fizyczna", stats.PhysicalDefense.GetValue());
            AppendStat(left, "Obrona magiczna", stats.MagicalDefense.GetValue());
            AppendStat(left, "Przebicie fizyczne",
                stats.PhysicalDefensePenetration.GetValue() * 100f, "%");
            AppendStat(left, "Przebicie magiczne",
                stats.MagicalDefensePenetration.GetValue() * 100f, "%");
            AppendStat(left, "Celność", character.WeaponAccuracy, "%");
            AppendStat(left, "Unik", stats.Dodge.GetValue());
            AppendStat(left, "Szansa na krytyk",
                stats.CritChance.GetValue() * 100f, "%");
            AppendStat(left, "Obrażenia krytyczne",
                stats.CritDamage.GetValue() * 100f, "%");

            var right = new StringBuilder();
            AppendStat(right, "Szybkość", stats.Speed.GetValue());

            var resource = character.ResourcePool;
            if (resource?.ResourceType == ResourceType.Mana)
            {
                AppendStat(right, "Regeneracja many", resource.Regeneration);
            }

            AppendStatHeader(right, "Odporności");
            AppendStat(right, "Krwawienie",
                character.Resistances?.BleedResistance ?? 0f, "%", true);
            AppendStat(right, "Trucizna",
                character.Resistances?.PoisonResistance ?? 0f, "%", true);
            AppendStat(right, "Ogień",
                character.Resistances?.FireResistance ?? 0f, "%", true);
            AppendStat(right, "Osłabienie",
                character.Resistances?.WeakeningResistance ?? 0f, "%", true);
            AppendStat(right, "Stłumienie",
                character.Resistances?.SuppressionResistance ?? 0f, "%", true);
            AppendStat(right, "Przesunięcie",
                character.Resistances?.MoveResistance ?? 0f, "%", true);

            leftColumn = left.ToString();
            rightColumn = right.ToString();
        }

        private static void AppendStat(
            StringBuilder result,
            string label,
            float value,
            string suffix = "",
            bool valueIsFraction = false)
        {
            if (result.Length > 0) result.Append('\n');
            var displayedValue = valueIsFraction ? value * 100f : value;
            result.Append(label)
                .Append(": ")
                .Append(displayedValue.ToString("0.#"))
                .Append(suffix);
        }

        private static void AppendStatHeader(StringBuilder result, string title)
        {
            if (result.Length > 0) result.Append("\n\n");
            result.Append("<b>").Append(title).Append("</b>");
        }

        private static void AppendStatRange(StringBuilder result, string label, string value)
        {
            if (result.Length > 0) result.Append('\n');
            result.Append(label).Append(": ").Append(value);
        }

        private static string FormatRange(float minimum, float maximum)
        {
            return $"{minimum:0.#}–{maximum:0.#}";
        }

        private string FormatStatus(PassiveEffectRuntime effect)
        {
            var name = effect.DisplayName;
            if (string.IsNullOrWhiteSpace(name)) name = tooltip?.GetGlossaryTitle(effect.EffectId);
            if (string.IsNullOrWhiteSpace(name)) name = FormatEffectId(effect.EffectId);
            return effect.RemainingDuration > 0
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

        private static GameObject CreateRow(string name, Transform parent, float height)
        {
            var row = new GameObject(name, typeof(RectTransform), typeof(Image));
            row.transform.SetParent(parent, false);
            var rect = row.transform as RectTransform;
            rect.sizeDelta = new Vector2(0f, height);
            row.GetComponent<Image>().color = new Color(1f, 1f, 1f, .035f);
            row.GetComponent<Image>().raycastTarget = true;
            row.AddComponent<LayoutElement>().preferredHeight = height;
            return row;
        }

        private static GameObject CreateEmptyStateText(
            string textValue,
            Transform parent)
        {
            var text = CreateText(
                "Empty State",
                parent,
                13f,
                FontStyles.Italic,
                TextAlignmentOptions.Left,
                new Color(.69f, .75f, .82f, 1f));
            text.text = textValue;
            ConfigureContentText(text, 24f);
            return text.gameObject;
        }

        private static TextMeshProUGUI CreateText(
            string name,
            Transform parent,
            float fontSize,
            FontStyles style,
            TextAlignmentOptions alignment,
            Color color)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            gameObject.transform.SetParent(parent, false);
            var text = gameObject.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform CreateRect(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            var rect = gameObject.transform as RectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            return rect;
        }

        private static void SetRect(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void ConfigureVerticalLayout(
            RectTransform root,
            bool controlChildHeight)
        {
            if (root == null) return;

            var layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = controlChildHeight;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = root.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
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

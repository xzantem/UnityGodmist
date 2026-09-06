using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.NPC;
using Godmist.Runtime.Game;
using Godmist.Runtime.NPC;
using Godmist.Systems.NPC;
using Godmist.Views.Game;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Godmist.Views.UI.Workspace
{
    [Serializable]
    public sealed class NpcServiceTabDefinition
    {
        public NpcServiceKind Kind;
        public Sprite Icon;
    }

    /// <summary>
    /// One UI Workspace shell for services offered by an interacted NPC. Roles
    /// determine available tabs; individual content views retain service logic.
    /// </summary>
    public sealed class NpcServicesWindowView : MonoBehaviour
    {
        [SerializeField] private UIWorkspaceWindow window;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private Transform tabsContent;
        [SerializeField] private NpcServiceTabView tabTemplate;
        [SerializeField] private NpcServiceTabDefinition[] tabDefinitions;
        [SerializeField] private TradeServiceWindowView tradeService;
        [SerializeField] private BlacksmithCraftingWindowView equipmentCraftingService;
        [SerializeField] private MaterialCraftingWindowView materialCraftingService;
        [SerializeField] private EquipmentUpgradeWindowView equipmentUpgradeService;
        [SerializeField] private EquipmentUpgradeWindowView equipmentReforgeService;

        private readonly List<NpcServiceTabView> spawnedTabs = new();
        private readonly NpcLoyaltyService loyaltyService = new();
        private GameSession session;
        private NpcInstance npc;
        private string recipientCharacterId = string.Empty;
        private NpcServiceKind? selectedService;
        private bool isServiceSessionOpen;
        private NpcLoyaltyHeaderView loyaltyHeader;
        private int lastDisplayedLoyaltyLevel = -1;
        private long lastDisplayedGoldSpent = -1;

        private void Awake()
        {
            if (tabTemplate != null) tabTemplate.gameObject.SetActive(false);
            EnsureLoyaltyHeader();

            // TradeServiceWindowView is a serialized child/content reference of
            // PF_UIW_NpcServicesWindow. Never create a second one at runtime:
            // doing so loses all prefab references and leaves a visible template
            // or an empty, fallback-generated layout behind.
            if (tradeService == null)
            {
                Debug.LogError(
                    "NpcServicesWindowView has no serialized TradeServiceWindowView. "
                    + "Assign the Trade tab component in PF_UIW_NpcServicesWindow.",
                    this);
            }
        }

        private void OnDisable()
        {
            if (!isServiceSessionOpen) return;
            EndServiceSession();
        }

        private void Update()
        {
            if (!isServiceSessionOpen || npc == null) return;

            var currentLevel = npc.LoyaltyLevel;
            var goldSpent = loyaltyService.GetGoldSpentTowardNextLevel(npc);
            if (currentLevel == lastDisplayedLoyaltyLevel
                && goldSpent == lastDisplayedGoldSpent)
            {
                return;
            }

            UpdateLoyaltyHeader();
        }

        public bool Open(NpcInstance nextNpc, string nextRecipientCharacterId)
        {
            session = GameBootstrap.Instance?.Session;
            if (session == null || nextNpc?.Definition == null
                || string.IsNullOrWhiteSpace(nextRecipientCharacterId)
                || !session.PlayerParty.IsActiveMember(nextRecipientCharacterId))
            {
                return false;
            }

            npc = nextNpc;
            recipientCharacterId = nextRecipientCharacterId;
            BuildTabs();
            if (!GetAvailableServices().Any()) return false;

            // Normalize runtime-created loyalty UI every time the service
            // window opens. This also repairs instances kept alive while
            // Unity reloads scripts with domain reload disabled.
            EnsureLoyaltyHeader();

            if (!session.TrySetMode(GameModeState.Menu)) return false;

            isServiceSessionOpen = true;
            if (titleText != null)
            {
                titleText.text = $"Usługi: {npc.Definition.DisplayName}";
            }
            UpdateLoyaltyHeader();
            window?.Open();
            SelectService(GetDefaultService());
            return true;
        }

        private void BuildTabs()
        {
            ClearTabs();
            foreach (var kind in GetAvailableServices())
            {
                if (tabTemplate == null || tabsContent == null) continue;
                var tab = Instantiate(tabTemplate, tabsContent);
                tab.gameObject.SetActive(true);
                tab.Bind(GetTabDefinition(kind)?.Icon, selectedService == kind,
                    () => SelectService(kind));
                spawnedTabs.Add(tab);
            }
        }

        private IEnumerable<NpcServiceKind> GetAvailableServices()
        {
            if (npc?.Definition?.GetRole<TraderRoleData>() != null)
            {
                yield return NpcServiceKind.Trade;
            }

            if (npc?.Definition?.GetRole<BlacksmithRoleData>() != null)
            {
                yield return NpcServiceKind.EquipmentCrafting;
                yield return NpcServiceKind.MaterialCrafting;
                yield return NpcServiceKind.EquipmentUpgrade;
                yield return NpcServiceKind.EquipmentReforge;
            }
        }

        private NpcServiceKind GetDefaultService()
        {
            foreach (var service in GetAvailableServices()) return service;
            return NpcServiceKind.EquipmentCrafting;
        }

        private void SelectService(NpcServiceKind kind)
        {
            HideSelectedService();
            selectedService = kind;

            var didShow = kind switch
            {
                NpcServiceKind.Trade => tradeService != null
                    && tradeService.Show(npc, recipientCharacterId),
                NpcServiceKind.EquipmentCrafting => equipmentCraftingService != null
                    && equipmentCraftingService.Show(npc, recipientCharacterId),
                NpcServiceKind.MaterialCrafting => materialCraftingService != null
                    && materialCraftingService.Show(npc, recipientCharacterId),
                NpcServiceKind.EquipmentUpgrade => equipmentUpgradeService != null
                    && equipmentUpgradeService.Show(npc, recipientCharacterId),
                NpcServiceKind.EquipmentReforge => equipmentReforgeService != null
                    && equipmentReforgeService.Show(npc, recipientCharacterId),
                _ => false
            };

            if (!didShow)
            {
                selectedService = null;
                return;
            }

            RefreshTabSelection();
        }

        private void HideSelectedService()
        {
            switch (selectedService)
            {
                case NpcServiceKind.Trade:
                    tradeService?.Hide();
                    break;
                case NpcServiceKind.EquipmentCrafting:
                    equipmentCraftingService?.Hide();
                    break;
                case NpcServiceKind.MaterialCrafting:
                    materialCraftingService?.Hide();
                    break;
                case NpcServiceKind.EquipmentUpgrade:
                    equipmentUpgradeService?.Hide();
                    break;
                case NpcServiceKind.EquipmentReforge:
                    equipmentReforgeService?.Hide();
                    break;
            }
        }

        private void RefreshTabSelection()
        {
            for (var index = 0; index < spawnedTabs.Count; index++)
            {
                var kind = GetAvailableServiceAt(index);
                spawnedTabs[index].Bind(GetTabDefinition(kind)?.Icon,
                    selectedService == kind, () => SelectService(kind));
            }
        }

        private NpcServiceKind GetAvailableServiceAt(int index)
        {
            return GetAvailableServices().ElementAt(index);
        }

        private NpcServiceTabDefinition GetTabDefinition(NpcServiceKind kind)
        {
            return (tabDefinitions ?? Array.Empty<NpcServiceTabDefinition>())
                .FirstOrDefault(definition => definition != null && definition.Kind == kind);
        }

        private void EndServiceSession()
        {
            HideSelectedService();
            selectedService = null;
            npc = null;
            recipientCharacterId = string.Empty;
            isServiceSessionOpen = false;
            lastDisplayedLoyaltyLevel = -1;
            lastDisplayedGoldSpent = -1;

            if (session != null && session.CurrentMode == GameModeState.Menu)
            {
                session.TrySetMode(GameModeState.Exploration);
            }
        }

        private void ClearTabs()
        {
            foreach (var tab in spawnedTabs)
            {
                if (tab != null) Destroy(tab.gameObject);
            }
            spawnedTabs.Clear();
        }

        private void EnsureLoyaltyHeader()
        {
            if (titleText == null) return;

            var windowRect = window?.transform as RectTransform;
            // Header is the actual visible title-bar rect. The PF_UIW_Window
            // root has a border/padding offset, so anchoring here would place
            // the loyalty indicator in the strip above the title bar.
            var host = windowRect?.Find("Header") as RectTransform
                ?? titleText.transform.parent as RectTransform
                ?? windowRect;
            if (host == null) return;

            var existingCandidates = windowRect
                ?.GetComponentsInChildren<NpcLoyaltyHeaderView>(true)
                ?? Array.Empty<NpcLoyaltyHeaderView>();
            var existing = existingCandidates.FirstOrDefault();
            if (existing != null)
            {
                foreach (var duplicate in existingCandidates)
                {
                    if (duplicate != existing)
                        Destroy(duplicate.gameObject);
                }

                if (existing.transform.parent != host)
                    existing.transform.SetParent(host, false);
                loyaltyHeader = existing;
                ConfigureLoyaltyHeaderRoot(existing.rectTransform);
                existing.ConfigureReferenceText(titleText);
                existing.rectTransform.SetAsLastSibling();
                return;
            }

            var indicatorObject = new GameObject(
                "NpcLoyaltyHeader",
                typeof(RectTransform),
                typeof(TextMeshProUGUI),
                typeof(NpcLoyaltyHeaderView));
            indicatorObject.transform.SetParent(host, false);

            var indicatorRect = (RectTransform)indicatorObject.transform;
            ConfigureLoyaltyHeaderRoot(indicatorRect);

            var indicatorText = indicatorObject.GetComponent<TextMeshProUGUI>();
            indicatorText.font = titleText.font;
            indicatorText.fontSize = Mathf.Min(titleText.fontSize - 3f, 15f);
            indicatorText.fontSizeMin = 10f;
            indicatorText.fontSizeMax = indicatorText.fontSize;
            indicatorText.enableAutoSizing = false;
            indicatorText.color = titleText.color;
            indicatorText.alignment = TextAlignmentOptions.Right;
            indicatorText.verticalAlignment = VerticalAlignmentOptions.Top;
            indicatorText.overflowMode = TextOverflowModes.Ellipsis;
            indicatorText.raycastTarget = false;
            indicatorText.text = string.Empty;

            var titleRect = titleText.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 0f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.offsetMin = new Vector2(16f, 0f);
            titleRect.offsetMax = new Vector2(-288f, 0f);
            titleText.alignment = TextAlignmentOptions.Left;

            loyaltyHeader = indicatorObject.GetComponent<NpcLoyaltyHeaderView>();
            loyaltyHeader.ConfigureReferenceText(titleText);
            indicatorRect.SetAsLastSibling();
        }

        private void UpdateLoyaltyHeader()
        {
            if (loyaltyHeader == null) EnsureLoyaltyHeader();
            if (loyaltyHeader == null || npc == null) return;

            var currentLevel = npc.LoyaltyLevel;
            var spent = loyaltyService.GetGoldSpentTowardNextLevel(npc);
            var required = loyaltyService.GetGoldRequiredForNextLevel(npc);
            loyaltyHeader.Bind(npc, BuildLoyaltyTooltipData(npc));
            loyaltyHeader.SetProgress(
                currentLevel,
                spent,
                required,
                currentLevel >= loyaltyService.MaximumLevel);
            loyaltyHeader.RefreshTooltip();
            lastDisplayedLoyaltyLevel = currentLevel;
            lastDisplayedGoldSpent = spent;
        }

        private NpcLoyaltyTooltipData BuildLoyaltyTooltipData(NpcInstance targetNpc)
        {
            var currentLevel = targetNpc.LoyaltyLevel;
            var maximumLevel = loyaltyService.MaximumLevel;
            var breakpoints = new List<NpcLoyaltyBreakpointPresentation>();
            for (var level = 1; level <= maximumLevel; level++)
            {
                var labels = new List<string>();
                AppendMaterialBreakpointLabel(labels, level, RarityTier.Common,
                    "Handel materiałami pospolitymi");
                AppendMaterialBreakpointLabel(labels, level, RarityTier.Uncommon,
                    "Handel materiałami niepospolitymi");
                AppendMaterialBreakpointLabel(labels, level, RarityTier.Rare,
                    "Handel materiałami rzadkimi");
                AppendMaterialBreakpointLabel(labels, level, RarityTier.Ancient,
                    "Handel materiałami starożytnymi");
                AppendMaterialBreakpointLabel(labels, level, RarityTier.Legendary,
                    "Handel materiałami legendarnymi");

                if (level > 1
                    && loyaltyService.GetDiscountPercent(level)
                        != loyaltyService.GetDiscountPercent(level - 1))
                {
                    labels.Add($"Ceny usług niższe o {loyaltyService.GetDiscountPercent(level)}%");
                }

                AppendBlacksmithSpecialEffectLabel(labels, targetNpc, level);
                if (labels.Count == 0) continue;

                breakpoints.Add(new NpcLoyaltyBreakpointPresentation(
                    level,
                    string.Join(" · ", labels),
                    currentLevel >= level));
            }

            return new NpcLoyaltyTooltipData(
                currentLevel,
                maximumLevel,
                loyaltyService.GetDiscountPercent(targetNpc),
                loyaltyService.GetGoldSpentTowardNextLevel(targetNpc),
                loyaltyService.GetGoldRequiredForNextLevel(targetNpc),
                breakpoints);
        }

        private void AppendMaterialBreakpointLabel(
            List<string> labels,
            int level,
            RarityTier rarity,
            string label)
        {
            if (loyaltyService.GetMaterialTradeUnlockLevel(rarity) == level)
                labels.Add(label);
        }

        private static void AppendBlacksmithSpecialEffectLabel(
            List<string> labels,
            NpcInstance targetNpc,
            int level)
        {
            if (targetNpc?.Definition?.GetRole<BlacksmithRoleData>() == null) return;

            var label = level switch
            {
                3 => "Tworzony ekwipunek ma o 5% lepsze statystyki główne",
                6 => "Nowy ekwipunek zaczyna z 25% ulepszenia",
                11 => "Niższy koszt ponownego ulepszania",
                14 => "Tworzony ekwipunek ma co najmniej 4 unikalności",
                15 => "Przekuwanie ekwipunku nie może obniżyć unikalności",
                _ => null
            };
            if (!string.IsNullOrWhiteSpace(label)) labels.Add(label);
        }

        private static void ConfigureLoyaltyHeaderRoot(RectTransform rect)
        {
            if (rect == null) return;

            // Keep this as a fixed-size overlay inside the visible Header. A
            // stretch-based rect here can inherit the window's full width when
            // Unity preserves a runtime-created hierarchy across reloads.
            rect.anchorMin = new Vector2(1f, .5f);
            rect.anchorMax = new Vector2(1f, .5f);
            rect.pivot = new Vector2(1f, .5f);
            rect.sizeDelta = new Vector2(260f, 44f);
            rect.anchoredPosition = new Vector2(-44f, 0f);

            var layout = rect.GetComponent<LayoutElement>()
                ?? rect.gameObject.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;

        }

    }

    public sealed class NpcLoyaltyHeaderView : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        internal RectTransform rectTransform => transform as RectTransform;

        private NpcInstance npc;
        private NpcLoyaltyTooltipData tooltipData;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI levelText;
        private TextMeshProUGUI progressText;
        private Slider progressSlider;
        private RectTransform progressRow;

        private TextMeshProUGUI referenceText;

        internal void ConfigureReferenceText(TextMeshProUGUI reference)
        {
            referenceText = reference;
        }

        public void Bind(NpcInstance nextNpc, NpcLoyaltyTooltipData nextTooltipData)
        {
            npc = nextNpc;
            tooltipData = nextTooltipData;
            EnsureProgressVisuals();
        }

        public void SetProgress(int level, long spent, int required, bool isMax)
        {
            EnsureProgressVisuals();
            if (levelText != null)
            {
                levelText.text = level.ToString();
                levelText.fontStyle = FontStyles.Bold;
            }

            if (progressSlider != null)
            {
                progressSlider.minValue = 0f;
                progressSlider.maxValue = 1f;
                progressSlider.interactable = false;
                progressSlider.value = isMax || required <= 0
                    ? 1f
                    : Mathf.Clamp01((float)(spent / (double)required));
                SetSliderGraphicsRaycastTarget(progressSlider, false);
            }

            if (progressText != null)
                progressText.text = isMax ? "MAX" : $"{spent} / {required}";
        }

        public void RefreshTooltip()
        {
            if (tooltipData == null) return;
            UIWorkspaceTooltipView.ActiveInstance?.RefreshNpcLoyaltyTooltip(tooltipData, this);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (npc == null) return;
            UIWorkspaceTooltipView.ActiveInstance?.ShowNpcLoyalty(tooltipData, this);
        }

        public void OnPointerExit(PointerEventData eventData) =>
            UIWorkspaceTooltipView.ActiveInstance?.NotifySourceExit(this);

        private void EnsureProgressVisuals()
        {
            if (titleText == null)
            {
                titleText = transform.Find("LoyaltyTitle")
                    ?.GetComponent<TextMeshProUGUI>();
            }

            if (titleText == null)
            {
                var titleObject = new GameObject(
                    "LoyaltyTitle",
                    typeof(RectTransform),
                    typeof(TextMeshProUGUI));
                titleObject.transform.SetParent(transform, false);
                titleText = titleObject.GetComponent<TextMeshProUGUI>();

                var titleRect = titleText.rectTransform;
                titleRect.anchorMin = new Vector2(0f, 1f);
                titleRect.anchorMax = new Vector2(1f, 1f);
                titleRect.pivot = new Vector2(.5f, 1f);
                titleRect.offsetMin = new Vector2(0f, -18f);
                titleRect.offsetMax = new Vector2(0f, -2f);
            }

            var rootText = GetComponent<TextMeshProUGUI>();
            if (referenceText == null) referenceText = rootText;
            if (rootText != null)
            {
                // The root is now only the transparent pointer target. The
                // visible label lives in its own fixed-height child rect.
                rootText.text = string.Empty;
                rootText.raycastTarget = true;
            }
            if (titleText == null) return;

            titleText.font = referenceText?.font ?? titleText.font;
            titleText.fontSize = referenceText != null
                ? Mathf.Min(referenceText.fontSize - 3f, 15f)
                : titleText.fontSize;
            titleText.fontSizeMin = 10f;
            titleText.fontSizeMax = titleText.fontSize;
            titleText.enableAutoSizing = false;
            titleText.color = referenceText?.color ?? titleText.color;
            titleText.text = "Lojalność";
            titleText.verticalAlignment = VerticalAlignmentOptions.Middle;
            titleText.alignment = TextAlignmentOptions.Right;
            titleText.margin = new Vector4(0f, 0f, 8f, 0f);
            titleText.raycastTarget = false;

            if (progressRow == null)
            {
                // Domain-reload-disabled sessions can retain children created
                // by an older version while private references are reset. Do
                // not let those stale full-width rows remain visible.
                for (var index = transform.childCount - 1; index >= 0; index--)
                {
                    var child = transform.GetChild(index);
                    if (child.name != "LoyaltyProgressRow") continue;

                    child.gameObject.SetActive(false);
                    Destroy(child.gameObject);
                }

                var rowObject = new GameObject(
                    "LoyaltyProgressRow",
                    typeof(RectTransform));
                rowObject.transform.SetParent(transform, false);
                progressRow = rowObject.GetComponent<RectTransform>();
                progressRow.anchorMin = new Vector2(0f, 0f);
                progressRow.anchorMax = new Vector2(1f, 0f);
                progressRow.pivot = new Vector2(.5f, 0f);
                progressRow.offsetMin = Vector2.zero;
                progressRow.offsetMax = new Vector2(0f, 23f);

                levelText = CreateText("Level", rowObject.transform);
                var levelRect = levelText.rectTransform;
                levelRect.anchorMin = new Vector2(0f, 0f);
                levelRect.anchorMax = new Vector2(0f, 1f);
                levelRect.pivot = new Vector2(0f, .5f);
                levelRect.sizeDelta = new Vector2(20f, 0f);
                levelRect.anchoredPosition = Vector2.zero;
                levelText.alignment = TextAlignmentOptions.Center;

                progressSlider = UIWorkspaceTooltipView.ActiveInstance
                    ?.InstantiateNpcLoyaltyProgressSlider(rowObject.transform);
                if (progressSlider != null)
                {
                    ConfigureProgressSliderRect();
                    ConfigureProgressSlider();
                }
            }

            // Unity can preserve runtime children during a script reload. If
            // an older version left a layout group behind, remove it before
            // applying the explicit rect layout below.
            var staleLayout = progressRow?.GetComponent<HorizontalLayoutGroup>();
            if (staleLayout != null) Destroy(staleLayout);

            if (progressSlider == null)
            {
                progressSlider = UIWorkspaceTooltipView.ActiveInstance
                    ?.InstantiateNpcLoyaltyProgressSlider(progressRow);
                if (progressSlider != null)
                {
                    ConfigureProgressSliderRect();
                    ConfigureProgressSlider();
                }
            }
        }

        private void ConfigureProgressSliderRect()
        {
            if (progressSlider == null || progressRow == null) return;

            var sliderRect = progressSlider.GetComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0f, 0f);
            sliderRect.anchorMax = new Vector2(1f, 1f);
            sliderRect.offsetMin = new Vector2(25f, 0f);
            sliderRect.offsetMax = Vector2.zero;
            sliderRect.localScale = Vector3.one;
        }

        private void ConfigureProgressSlider()
        {
            progressSlider.name = "LoyaltyProgressSlider";
            var sliderLayout = progressSlider.gameObject.GetComponent<LayoutElement>()
                ?? progressSlider.gameObject.AddComponent<LayoutElement>();
            sliderLayout.minWidth = 150f;
            sliderLayout.preferredWidth = 178f;
            sliderLayout.flexibleWidth = 1f;
            sliderLayout.minHeight = 23f;
            sliderLayout.preferredHeight = 23f;

            progressText = CreateText("ProgressValue", progressSlider.transform);
            var progressRect = progressText.rectTransform;
            progressRect.anchorMin = Vector2.zero;
            progressRect.anchorMax = Vector2.one;
            progressRect.offsetMin = Vector2.zero;
            progressRect.offsetMax = Vector2.zero;
            progressText.alignment = TextAlignmentOptions.Center;
            progressText.raycastTarget = false;
        }

        private TextMeshProUGUI CreateText(string name, Transform parent)
        {
            var textObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = titleText.font;
            text.fontSize = Mathf.Max(10f, titleText.fontSize);
            text.color = titleText.color;
            text.alignment = TextAlignmentOptions.Right;
            text.verticalAlignment = VerticalAlignmentOptions.Middle;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }

        private static void SetSliderGraphicsRaycastTarget(Slider slider, bool value)
        {
            foreach (var graphic in slider.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = value;
        }
    }
}

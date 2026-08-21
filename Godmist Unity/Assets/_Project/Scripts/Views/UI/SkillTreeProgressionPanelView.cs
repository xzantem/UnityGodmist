using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Game;
using Godmist.Systems.Characters;
using Godmist.Views.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI
{
    public class SkillTreeProgressionPanelView : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TextMeshProUGUI characterNameText;
        [SerializeField] private TextMeshProUGUI progressionText;
        [SerializeField] private Transform treeTabRoot;
        [SerializeField] private Button treeTabTemplate;
        [SerializeField] private Transform[] tierRoots = new Transform[7];
        [SerializeField] private Button nodeTemplate;
        [SerializeField] private RectTransform selectedTreeHeader;
        [SerializeField] private TextMeshProUGUI selectedTreeNameText;
        [SerializeField] private RectTransform globalPassivePanel;
        [SerializeField] private TextMeshProUGUI globalPassiveText;

        private readonly SkillTreeProgressionSystem progressionSystem = new();
        private GameSession session;
        private CharacterInstance character;
        private SkillTreeDefinition selectedTree;
        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

        private void Awake()
        {
            CreateHeaderLayout();
            if (treeTabTemplate != null) treeTabTemplate.gameObject.SetActive(false);
            if (nodeTemplate != null) nodeTemplate.gameObject.SetActive(false);
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        public void Open(GameSession gameSession, string characterId)
        {
            if (gameSession == null || gameSession.ActiveCombat != null
                || string.IsNullOrWhiteSpace(characterId)
                || !gameSession.CharactersById.TryGetValue(characterId, out character)
                || character == null)
            {
                Close();
                return;
            }

            session = gameSession;
            selectedTree = character.ClassData?.SkillTrees?.FirstOrDefault(
                value => value != null && !string.IsNullOrWhiteSpace(value.Id));
            if (panelRoot != null) panelRoot.SetActive(true);
            Refresh();
        }

        public void OpenActiveLeader()
        {
            var gameSession = GameBootstrap.Instance?.Session;
            var leaderId = gameSession?.PlayerParty?.ActiveLeaderId;
            if (!string.IsNullOrWhiteSpace(leaderId)) Open(gameSession, leaderId);
        }

        public void Close()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
            session = null; character = null; selectedTree = null;
        }

        private void Refresh()
        {
            if (character == null || session == null)
            {
                return;
            }

            if (session.ActiveCombat != null)
            {
                Close();
                return;
            }

            if (characterNameText != null) characterNameText.text = character.Definition?.DisplayName ?? character.CharacterId;
            if (progressionText != null) progressionText.text = $"Poziom {character.Level} | XP {character.CurrentExperience} | Punkty: {character.AvailableSkillPoints}";
            RefreshSelectedTreeHeader();
            RebuildTabs(); RebuildNodes();
        }

        private void RebuildTabs()
        {
            Clear(treeTabRoot, treeTabTemplate?.transform);
            if (treeTabRoot == null || treeTabTemplate == null)
            {
                return;
            }

            foreach (var tree in character.ClassData?.SkillTrees ?? new())
            {
                if (tree == null || string.IsNullOrWhiteSpace(tree.Id)) continue;
                var button = Instantiate(treeTabTemplate, treeTabRoot);
                button.gameObject.SetActive(true);
                var label = button.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.text = tree.DisplayName;
                var treeForButton = tree;
                button.onClick.AddListener(() => { selectedTree = treeForButton; Refresh(); });
            }
        }

        private void RebuildNodes()
        {
            if (tierRoots != null)
            {
                foreach (var root in tierRoots) Clear(root, nodeTemplate?.transform);
            }

            if (selectedTree == null || nodeTemplate == null || tierRoots == null)
            {
                return;
            }

            var tierCount = Mathf.Min(7, tierRoots.Length);
            for (var tier = 1; tier <= tierCount; tier++)
            {
                var root = tierRoots[tier - 1];
                if (root == null) continue;
                for (var column = 1; column <= 5; column++)
                {
                    var node = selectedTree?.Nodes?.FirstOrDefault(value => value?.Tier == tier && value.Column == column);
                    if (node == null) { CreateSpacer(root); continue; }
                    var button = Instantiate(nodeTemplate, root);
                    button.gameObject.SetActive(true);
                    var ranks = character.SkillTreeProgress?
                        .FirstOrDefault(value => value?.TreeId == selectedTree.Id)?
                        .NodeInvestments?.FirstOrDefault(value => value?.NodeId == node.Id)?.Ranks ?? 0;
                    var label = button.GetComponentInChildren<TextMeshProUGUI>();
                    if (label != null)
                    {
                        label.text = $"{node.DisplayName}\n{ranks}/{node.EffectiveMaxRanks}";
                    }

                    var treeForButton = selectedTree;
                    var nodeForButton = node;
                    button.onClick.AddListener(
                        () => TryInvest(treeForButton, nodeForButton));
                }
            }
        }

        private void TryInvest(
            SkillTreeDefinition tree,
            SkillTreeNodeDefinition node)
        {
            if (session == null || character == null || tree == null || node == null)
            {
                return;
            }

            if (session.ActiveCombat != null)
            {
                Close();
                return;
            }

            progressionSystem.TryInvest(character, tree.Id, node.Id);
            Refresh();
        }

        private static void CreateSpacer(Transform root)
        {
            if (root == null)
            {
                return;
            }

            var spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(root, false);
            spacer.GetComponent<LayoutElement>().ignoreLayout = false;
        }

        private void RefreshSelectedTreeHeader()
        {
            if (selectedTree == null)
            {
                if (selectedTreeNameText != null) selectedTreeNameText.text = string.Empty;
                if (globalPassiveText != null) globalPassiveText.text = string.Empty;
                return;
            }

            var investedRanks = SkillTreeProgressionSystem.GetInvestedRanks(
                character,
                selectedTree.Id);
            if (selectedTreeNameText != null)
            {
                selectedTreeNameText.text = $"{selectedTree.DisplayName}  |  Wydano: {investedRanks}/{SkillTreeProgressionSystem.MaximumPointsPerTree}";
            }

            if (globalPassiveText == null) return;

            var passive = selectedTree.GlobalPassive;
            if (passive == null || string.IsNullOrWhiteSpace(passive.DisplayName))
            {
                globalPassiveText.text = "Brak globalnej pasywy";
                return;
            }

            var summary = string.Join(", ", SkillTreeProgressionSystem
                .GetGlobalPassiveModifiers(selectedTree, investedRanks)
                .Select(FormatModifier));
            if (string.IsNullOrWhiteSpace(summary)) summary = "brak efektu";

            globalPassiveText.text = $"{passive.DisplayName}\nAktualnie: {summary}";
        }

        private void CreateHeaderLayout()
        {
            if (selectedTreeHeader != null
                && selectedTreeNameText != null
                && globalPassivePanel != null
                && globalPassiveText != null)
            {
                return;
            }

            if (panelRoot == null || characterNameText == null || progressionText == null)
            {
                return;
            }

            var panelTransform = panelRoot.transform as RectTransform;
            if (panelTransform == null) return;

            selectedTreeHeader = CreatePanel(
                panelTransform,
                "SelectedTreeHeader",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(16f, -174f),
                new Vector2(800f, 46f));
            selectedTreeNameText = CreateText(
                selectedTreeHeader,
                "SelectedTreeName",
                26f,
                TextAlignmentOptions.Left);

            globalPassivePanel = CreatePanel(
                panelTransform,
                "GlobalPassivePanel",
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-16f, -174f),
                new Vector2(390f, 46f));
            globalPassiveText = CreateText(
                globalPassivePanel,
                "GlobalPassiveText",
                15f,
                TextAlignmentOptions.Left);

            if (treeTabRoot is not RectTransform treeTabs) return;

            treeTabs.anchorMin = new Vector2(0f, 1f);
            treeTabs.anchorMax = new Vector2(0f, 1f);
            treeTabs.pivot = new Vector2(0f, 1f);
            treeTabs.anchoredPosition = new Vector2(16f, -122f);
            treeTabs.sizeDelta = new Vector2(800f, 36f);

            if (treeTabTemplate != null
                && treeTabTemplate.TryGetComponent<LayoutElement>(out var layoutElement))
            {
                layoutElement.preferredWidth = 180f;
                layoutElement.preferredHeight = 36f;
            }
        }

        private static RectTransform CreatePanel(
            RectTransform parent,
            string objectName,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            var panel = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            var rectTransform = panel.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = pivot;
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;
            panel.GetComponent<Image>().color = new Color(0.10f, 0.15f, 0.21f, 0.96f);
            return rectTransform;
        }

        private TextMeshProUGUI CreateText(
            RectTransform parent,
            string objectName,
            float fontSize,
            TextAlignmentOptions alignment)
        {
            var textObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
            var rectTransform = textObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = new Vector2(14f, 6f);
            rectTransform.offsetMax = new Vector2(-14f, -6f);

            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = characterNameText.font;
            text.fontSharedMaterial = characterNameText.fontSharedMaterial;
            text.fontSize = fontSize;
            text.color = characterNameText.color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private static string FormatModifier(StatModifier modifier)
        {
            var value = modifier.Value;
            var isPercentage = modifier.Operation == StatModifierOperation.Percent
                || modifier.StatType is CharacterStatType.CritChance or CharacterStatType.Dodge;
            var formattedValue = isPercentage
                ? $"{value * 100f:+0.##;-0.##;0}%"
                : $"{value:+0.##;-0.##;0}";

            return $"{formattedValue} {GetStatDisplayName(modifier.StatType)}";
        }

        private static string GetStatDisplayName(CharacterStatType statType)
        {
            return statType switch
            {
                CharacterStatType.MinAttack => "Atak min.",
                CharacterStatType.MaxAttack => "Atak maks.",
                CharacterStatType.PhysicalDefense => "Obrona fiz.",
                CharacterStatType.MagicalDefense => "Obrona mag.",
                CharacterStatType.CritChance => "Szansa kryt.",
                CharacterStatType.CritDamage => "Obrazenia kryt.",
                CharacterStatType.MaxHealth => "Zdrowie",
                CharacterStatType.Dodge => "Unik",
                CharacterStatType.Speed => "Szybkosc",
                CharacterStatType.ResourceRegeneration => "Regeneracja zasobu",
                CharacterStatType.WeaponAccuracy => "Celnosc",
                CharacterStatType.MaxResource => "Maks. zasob",
                _ => statType.ToString()
            };
        }

        private static void Clear(Transform root, Transform template)
        {
            if (root == null) return;
            for (var index = root.childCount - 1; index >= 0; index--)
                if (root.GetChild(index) != template) Destroy(root.GetChild(index).gameObject);
        }
    }
}

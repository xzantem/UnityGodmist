using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Game;
using Godmist.Systems.Characters;
using Godmist.Views.UI.Workspace;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI
{
    public class SkillTreeProgressionPanelView : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private UIWorkspaceWindow workspaceWindow;
        [SerializeField] private TextMeshProUGUI windowTitleText;
        [SerializeField] private TextMeshProUGUI progressionText;
        [SerializeField] private Transform treeTabRoot;
        [SerializeField] private Button treeTabTemplate;
        [SerializeField] private Transform[] tierRoots = new Transform[7];
        [SerializeField] private Button nodeTemplate;
        [SerializeField] private RectTransform selectedTreeHeader;
        [SerializeField] private TextMeshProUGUI selectedTreeNameText;
        [SerializeField] private RectTransform globalPassivePanel;
        [SerializeField] private TextMeshProUGUI globalPassiveText;
        [SerializeField] private RectTransform prerequisiteConnectionRoot;
        [SerializeField] private Image prerequisiteConnectionTemplate;
        [SerializeField] private RectTransform[] tierGateRoots = new RectTransform[3];
        [SerializeField] private TextMeshProUGUI[] tierGateTexts = new TextMeshProUGUI[3];

        private readonly SkillTreeProgressionSystem progressionSystem = new();
        private readonly Dictionary<string, RectTransform> nodeRectsById = new();
        private GameSession session;
        private CharacterInstance character;
        private SkillTreeDefinition selectedTree;
        private Coroutine treeVisualRefresh;
        private Vector2 lastTreeAreaSize = new(-1f, -1f);
        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

        private void Awake()
        {
            if (treeTabTemplate != null) treeTabTemplate.gameObject.SetActive(false);
            if (nodeTemplate != null) nodeTemplate.gameObject.SetActive(false);
            if (prerequisiteConnectionTemplate != null) prerequisiteConnectionTemplate.gameObject.SetActive(false);
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void OnDisable()
        {
            if (treeVisualRefresh != null) StopCoroutine(treeVisualRefresh);
            session = null;
            character = null;
            selectedTree = null;
            nodeRectsById.Clear();
            treeVisualRefresh = null;
            lastTreeAreaSize = new Vector2(-1f, -1f);
        }

        private void Update()
        {
            if (!IsOpen || selectedTree == null) return;

            var treeArea = GetTreeArea();
            if (treeArea == null) return;
            var currentSize = treeArea.rect.size;
            if ((currentSize - lastTreeAreaSize).sqrMagnitude <= 0.01f) return;

            lastTreeAreaSize = currentSize;
            ScheduleTreeVisualRefresh();
        }

        public void Open(GameSession gameSession, string characterId)
        {
            if (gameSession == null || gameSession.IsCharacterInCombat(characterId)
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
            workspaceWindow?.Focus();
            Refresh();
            Canvas.ForceUpdateCanvases();
            ResizeGlobalPassiveHeader();
            workspaceWindow?.ClampToWorkspace();
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

            if (session.IsCharacterInCombat(character?.CharacterId))
            {
                Close();
                return;
            }

            if (windowTitleText != null) windowTitleText.text = $"Drzewka umiejętności — {character.Definition?.DisplayName ?? character.CharacterId}";
            if (progressionText != null) progressionText.text = $"Poziom {character.Level} | XP {character.CurrentExperience} | Punkty: {character.AvailableSkillPoints}";
            RefreshSelectedTreeHeader();
            RebuildTabs(); RebuildNodes();
            ScheduleTreeVisualRefresh();
        }

        private void ScheduleTreeVisualRefresh()
        {
            if (treeVisualRefresh != null) StopCoroutine(treeVisualRefresh);
            treeVisualRefresh = StartCoroutine(RefreshTreeVisualsAfterLayout());
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
                if (label != null)
                {
                    label.text = $"{tree.DisplayName}\n{SkillTreeProgressionSystem.GetInvestedRanks(character, tree.Id)}/{SkillTreeProgressionSystem.MaximumPointsPerTree}";
                }
                if (tree.Id == selectedTree?.Id)
                {
                    var palette = button.GetComponent<UIWorkspaceStyleApplier>()?.Palette;
                    if (palette != null)
                    {
                        var colors = button.colors;
                        colors.normalColor = palette.GetAccentColor(UIWorkspaceAccentStyle.Selected);
                        colors.selectedColor = colors.normalColor;
                        button.colors = colors;
                    }
                }
                var treeForButton = tree;
                button.onClick.AddListener(() => { selectedTree = treeForButton; Refresh(); });
            }
        }

        private void RebuildNodes()
        {
            nodeRectsById.Clear();
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
                var hasNodes = selectedTree.Nodes.Any(n=>n!=null && n.Tier==tier);
                root.gameObject.SetActive(hasNodes);
                if(!hasNodes)continue;
                for (var column = 1; column <= 5; column++)
                {
                    var node = selectedTree?.Nodes?.FirstOrDefault(value => value?.Tier == tier && value.Column == column);
                    if (node == null) { CreateSpacer(root, nodeTemplate); continue; }
                    var button = Instantiate(nodeTemplate, root);
                    button.gameObject.SetActive(true);
                    nodeRectsById[node.Id] = button.transform as RectTransform;
                    var ranks = character.SkillTreeProgress?
                        .FirstOrDefault(value => value?.TreeId == selectedTree.Id)?
                        .NodeInvestments?.FirstOrDefault(value => value?.NodeId == node.Id)?.Ranks ?? 0;
                    var treeForButton = selectedTree;
                    var nodeForButton = node;
                    var blockReason = progressionSystem.GetInvestmentBlockReason(character, selectedTree.Id, node.Id);
                    var isStructurallyUnlocked = progressionSystem.IsNodeStructurallyUnlocked(
                        character,
                        selectedTree.Id,
                        node.Id);
                    var nodeView = button.GetComponent<SkillTreeNodeView>();
                    if (nodeView != null)
                    {
                        nodeView.Configure(
                            node,
                            ranks,
                            isStructurallyUnlocked,
                            string.IsNullOrWhiteSpace(blockReason),
                            blockReason,
                            () => TryInvest(treeForButton, nodeForButton));
                    }
                    else
                    {
                        var label = button.GetComponentInChildren<TextMeshProUGUI>();
                        if (label != null) label.text = $"{node.DisplayName}\n{ranks}/{node.EffectiveMaxRanks}";
                        button.interactable = string.IsNullOrWhiteSpace(blockReason);
                        button.onClick.RemoveAllListeners();
                        if (button.interactable) button.onClick.AddListener(() => TryInvest(treeForButton, nodeForButton));
                    }
                }
            }
        }

        private IEnumerator RefreshTreeVisualsAfterLayout()
        {
            yield return new WaitForEndOfFrame();
            if (selectedTree == null || character == null) yield break;

            Canvas.ForceUpdateCanvases();
            ResizeGlobalPassiveHeader();
            RefreshTierGates();
            ArrangeTreeVisualLayers();
            RebuildPrerequisiteConnections();
            var treeArea = GetTreeArea();
            if (treeArea != null) lastTreeAreaSize = treeArea.rect.size;
            workspaceWindow?.ClampToWorkspace();
            treeVisualRefresh = null;
        }

        private void ResizeGlobalPassiveHeader()
        {
            if (globalPassiveText == null || globalPassivePanel == null) return;
            var row = globalPassivePanel.parent as RectTransform;
            var layout = row != null ? row.GetComponent<LayoutElement>() : null;
            if (layout == null) return;
            var textRect = globalPassiveText.rectTransform;
            var width = textRect.rect.width;
            if (width <= 0f) return;
            var padding = Mathf.Max(0f, globalPassivePanel.rect.height - textRect.rect.height);
            var height = Mathf.Ceil(globalPassiveText.GetPreferredValues(globalPassiveText.text, width, Mathf.Infinity).y + padding);
            layout.preferredHeight = Mathf.Max(62f, height);
            if (row.parent.GetComponent<LayoutGroup>() == null)
            {
                // The window body uses anchored regions rather than a vertical layout.
                var delta = layout.preferredHeight - row.rect.height;
                row.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, layout.preferredHeight);
                row.anchoredPosition -= new Vector2(0f, delta * (1f - row.pivot.y));
                var area = GetTreeArea();
                if (area != null && area.parent == row.parent)
                    area.offsetMax -= new Vector2(0f, delta);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(row.parent as RectTransform);
        }

        private RectTransform GetTreeArea() => prerequisiteConnectionRoot?.parent as RectTransform;

        private void ArrangeTreeVisualLayers()
        {
            // Gates and lines are visual aids only. Keep them underneath the interactive
            // node slots, otherwise a gate outline can steal pointer events from a node.
            prerequisiteConnectionRoot?.SetAsFirstSibling();
            if (tierGateRoots != null)
            {
                foreach (var gate in tierGateRoots)
                {
                    if (gate == null) continue;
                    gate.SetAsFirstSibling();
                    foreach (var graphic in gate.GetComponentsInChildren<Graphic>(true))
                    {
                        graphic.raycastTarget = false;
                    }
                }
            }

            if (tierRoots != null && tierRoots.Length > 0 && tierRoots[0] != null)
            {
                tierRoots[0].parent?.SetAsLastSibling();
            }
        }

        private void RefreshTierGates()
        {
            RefreshTierGate(0, 2, 3, 10);
            RefreshTierGate(1, 4, 5, 20);
            RefreshTierGate(2, 6, 6, 30);
        }

        private void RefreshTierGate(int gateIndex, int firstTierIndex, int lastTierIndex, int threshold)
        {
            if (tierGateRoots == null || gateIndex >= tierGateRoots.Length) return;
            var gate = tierGateRoots[gateIndex];
            if (gate == null) return;

            var invested = SkillTreeProgressionSystem.GetInvestedRanks(character, selectedTree.Id);
            var locked = invested < threshold && selectedTree.Nodes.Any(n=>n!=null && n.Tier>=firstTierIndex+1 && n.Tier<=lastTierIndex+1);
            gate.gameObject.SetActive(locked);
            if (!locked) return;

            if (tierGateTexts != null && gateIndex < tierGateTexts.Length && tierGateTexts[gateIndex] != null)
            {
                tierGateTexts[gateIndex].text = $"{invested}/{threshold}";
            }

            if (tierRoots == null || firstTierIndex >= tierRoots.Length || lastTierIndex >= tierRoots.Length
                || tierRoots[firstTierIndex] is not RectTransform first || tierRoots[lastTierIndex] is not RectTransform last
                || gate.parent is not RectTransform gateParent)
            {
                return;
            }

            var firstCorners = new Vector3[4];
            var lastCorners = new Vector3[4];
            first.GetWorldCorners(firstCorners);
            last.GetWorldCorners(lastCorners);
            var topLeft = gateParent.InverseTransformPoint(firstCorners[1]);
            var bottomRight = gateParent.InverseTransformPoint(lastCorners[3]);
            const float padding = 8f;
            gate.anchorMin = gate.anchorMax = new Vector2(0.5f, 0.5f);
            gate.pivot = new Vector2(0.5f, 0.5f);
            gate.anchoredPosition = (topLeft + bottomRight) * 0.5f;
            gate.sizeDelta = new Vector2(
                Mathf.Abs(bottomRight.x - topLeft.x) + padding * 2f,
                Mathf.Abs(topLeft.y - bottomRight.y) + padding * 2f);
        }

        private void RebuildPrerequisiteConnections()
        {
            Clear(prerequisiteConnectionRoot, prerequisiteConnectionTemplate?.transform);
            if (prerequisiteConnectionRoot == null || prerequisiteConnectionTemplate == null || selectedTree?.Nodes == null) return;

            foreach (var node in selectedTree.Nodes)
            {
                if (node?.Requirements == null || !nodeRectsById.TryGetValue(node.Id, out var target)) continue;
                foreach (var requirement in node.Requirements)
                {
                    if (requirement == null || !nodeRectsById.TryGetValue(requirement.NodeId, out var source)) continue;
                    var line = Instantiate(prerequisiteConnectionTemplate, prerequisiteConnectionRoot);
                    line.gameObject.SetActive(true);
                    line.raycastTarget = false;
                    ConfigureConnectionLine(line.rectTransform, source, target);
                }
            }
        }

        private void ConfigureConnectionLine(RectTransform line, RectTransform source, RectTransform target)
        {
            if (line == null || source == null || target == null || prerequisiteConnectionRoot == null) return;

            var sourceCorners = new Vector3[4];
            var targetCorners = new Vector3[4];
            source.GetWorldCorners(sourceCorners);
            target.GetWorldCorners(targetCorners);

            // Corner indices are independent of each RectTransform's pivot: 0/3 are the
            // bottom edge and 1/2 the top edge. This keeps a prerequisite line attached
            // to the visible slot edge instead of the target's centre.
            var sourceBottomCentre = (sourceCorners[0] + sourceCorners[3]) * 0.5f;
            var targetTopCentre = (targetCorners[1] + targetCorners[2]) * 0.5f;
            var sourcePoint = prerequisiteConnectionRoot.InverseTransformPoint(sourceBottomCentre);
            var targetPoint = prerequisiteConnectionRoot.InverseTransformPoint(targetTopCentre);
            var delta = targetPoint - sourcePoint;
            line.anchorMin = line.anchorMax = new Vector2(0.5f, 0.5f);
            line.pivot = new Vector2(0f, 0.5f);
            line.anchoredPosition = sourcePoint;
            line.sizeDelta = new Vector2(delta.magnitude, line.sizeDelta.y);
            line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        private void TryInvest(
            SkillTreeDefinition tree,
            SkillTreeNodeDefinition node)
        {
            if (session == null || character == null || tree == null || node == null)
            {
                return;
            }

            if (session.IsCharacterInCombat(character?.CharacterId))
            {
                Close();
                return;
            }

            if (progressionSystem.TryInvest(character, tree.Id, node.Id))
                session.NotifyCharacterProgressionChanged(character.CharacterId);
            Refresh();
        }

        private static void CreateSpacer(Transform root, Button template)
        {
            if (root == null)
            {
                return;
            }

            var spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(root, false);
            var rect = spacer.GetComponent<RectTransform>();
            var layout = spacer.GetComponent<LayoutElement>();
            var templateLayout = template != null ? template.GetComponent<LayoutElement>() : null;
            var width = templateLayout != null && templateLayout.preferredWidth >= 0f
                ? templateLayout.preferredWidth
                : 64f;

            // A spacer reserves a grid column only. It must never increase the preferred
            // height reported by its parent HorizontalLayoutGroup.
            layout.minWidth = width;
            layout.preferredWidth = width;
            layout.flexibleWidth = 0f;
            layout.minHeight = 0f;
            layout.preferredHeight = 0f;
            layout.flexibleHeight = 0f;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 0f);
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

            var summary = GetGlobalPassiveSummary(selectedTree, investedRanks);

            globalPassiveText.text = $"{passive.DisplayName}\nAktualnie: {summary}";
        }

        public static string GetGlobalPassiveSummary(SkillTreeDefinition tree, int investedRanks)
        {
            if (tree == null) return string.Empty;
            var points = Mathf.Max(0, investedRanks);
            // These passives contain combat rules as well as ordinary stat modifiers.
            switch (tree.Id)
            {
                case Godmist.Systems.Combat.ScoutCombatSystem.AssassinTree:
                    return $"+{points * .2f:0.##}% Kryt, +{points * .5f:0.##}% obrażeń wręcz";
                case Godmist.Systems.Combat.ScoutCombatSystem.BladeTree:
                    return $"{points}% szansy na zachowanie sztyletu, +{points}% siły zatrucia";
                case Godmist.Systems.Combat.WarriorCombatSystem.Commander:
                    return $"+{points * .3f:0.##}% Kryt, {points * .002f:0.###}% obrażeń trafienia jako nieuchronne za każdy procent ulepszenia broni";
                case Godmist.Systems.Combat.WarriorCombatSystem.Brawler:
                    return $"+{points}% maksymalnej wściekłości, {points * 2}% wydanej wściekłości odzyskiwane jako HP";
            }
            var summary = string.Join(", ", SkillTreeProgressionSystem
                .GetGlobalPassiveModifiers(tree, points).Select(FormatModifier));
            if (!string.IsNullOrWhiteSpace(summary)) return summary;
            return points == 0 ? "Nie wydano jeszcze punktów."
                : string.IsNullOrWhiteSpace(tree.GlobalPassive?.Description)
                    ? "Brak opisu pasywu." : tree.GlobalPassive.Description;
        }

        private static string FormatModifier(StatModifier modifier)
        {
            var value = modifier.Value;
            var isPercentage = modifier.Operation == StatModifierOperation.Percent
                || modifier.StatType is CharacterStatType.CritChance or CharacterStatType.Dodge;
            var formattedValue = isPercentage
                ? modifier.StatType == CharacterStatType.Dodge
                    ? $"{value * 100f:+0;-0;0}%"
                    : modifier.StatType == CharacterStatType.CritDamage
                        ? $"{value * 100f:+0.00;-0.00;0.00}%"
                        : $"{value * 100f:+0.##;-0.##;0}%"
                : modifier.StatType == CharacterStatType.CritDamage
                    ? $"{value:+0.00;-0.00;0.00}"
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

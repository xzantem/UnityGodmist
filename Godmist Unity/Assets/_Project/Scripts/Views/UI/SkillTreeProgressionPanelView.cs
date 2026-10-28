using Godmist.Core.Utilities;
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
using UnityEngine.EventSystems;

namespace Godmist.Views.UI
{
    public class SkillTreeProgressionPanelView : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private UIWorkspaceWindow workspaceWindow;
        [SerializeField] private TextMeshProUGUI windowTitleText;
        [SerializeField] private TextMeshProUGUI progressionText;
        [SerializeField] private TextMeshProUGUI experienceValueText;
        [SerializeField] private TextMeshProUGUI availablePointsText;
        [SerializeField] private ScrollRect treeScroll;
        [SerializeField] private Image experienceFill;
        [SerializeField] private RectTransform headerSeparator;
        [SerializeField] private Image detailsIcon;
        [SerializeField] private TextMeshProUGUI detailsTitle;
        [SerializeField] private TextMeshProUGUI detailsRank;
        [SerializeField] private TextMeshProUGUI detailsBody;
        [SerializeField] private TextMeshProUGUI investmentBlockText;
        [SerializeField] private ScrollRect detailsScroll;
        [SerializeField] private Button investButton;
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
        [SerializeField] private Image[] tierGateFills = new Image[3];

        private readonly SkillTreeProgressionSystem progressionSystem = new();
        private readonly CharacterProgressionSystem characterProgressionSystem = new();
        private readonly Dictionary<string, RectTransform> nodeRectsById = new();
        private readonly Dictionary<string, SkillTreeNodeView> nodeViewsById = new();
        private GameSession session;
        private CharacterInstance character;
        private SkillTreeDefinition selectedTree;
        private string selectedNodeId;
        private Coroutine treeVisualRefresh;
        private Vector2 lastTreeAreaSize = new(-1f, -1f);
        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

        private void Awake()
        {
            if (treeTabTemplate != null) treeTabTemplate.gameObject.SetActive(false);
            if (nodeTemplate != null) nodeTemplate.gameObject.SetActive(false);
            if (prerequisiteConnectionTemplate != null) prerequisiteConnectionTemplate.gameObject.SetActive(false);
            if (investButton != null) investButton.onClick.AddListener(InvestSelectedNode);
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void OnDisable()
        {
            if (session != null) session.CharacterProgressionChanged -= OnCharacterProgressionChanged;
            UIWorkspaceTooltipView.ActiveInstance?.HideForSourceWithin(transform);
            if (treeVisualRefresh != null) StopCoroutine(treeVisualRefresh);
            session = null;
            character = null;
            selectedTree = null;
            nodeRectsById.Clear();
            nodeViewsById.Clear();
            selectedNodeId = null;
            treeVisualRefresh = null;
            lastTreeAreaSize = new Vector2(-1f, -1f);
        }

        private void OnDestroy()
        {
            if (investButton != null) investButton.onClick.RemoveListener(InvestSelectedNode);
            if (session != null) session.CharacterProgressionChanged -= OnCharacterProgressionChanged;
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
                || !gameSession.CharactersById.TryGetValue(characterId, out var selectedCharacter)
                || selectedCharacter == null)
            {
                Close();
                return;
            }

            if (session != null) session.CharacterProgressionChanged -= OnCharacterProgressionChanged;
            session = gameSession;
            session.CharacterProgressionChanged += OnCharacterProgressionChanged;
            character = selectedCharacter;
            selectedTree = character.ClassData?.SkillTrees?.FirstOrDefault(
                value => value != null && !string.IsNullOrWhiteSpace(value.Id));
            selectedNodeId = null;
            if (treeScroll != null) treeScroll.verticalNormalizedPosition = 1f;
            if (panelRoot != null) panelRoot.SetActive(true);
            workspaceWindow?.Focus();
            Refresh();
            Canvas.ForceUpdateCanvases();
            ResizeGlobalPassiveHeader();
            workspaceWindow?.ClampToWorkspace();
        }

        public void Close()
        {
            if (session != null) session.CharacterProgressionChanged -= OnCharacterProgressionChanged;
            if (panelRoot != null) panelRoot.SetActive(false);
            session = null; character = null; selectedTree = null;
        }

        private void OnCharacterProgressionChanged(string characterId)
        {
            if (IsOpen && character?.CharacterId == characterId) Refresh();
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

            if (windowTitleText != null) windowTitleText.text = $"{character.Definition?.DisplayName ?? character.CharacterId} — Rozwój";
            var maximumLevel = character.Level >= CharacterProgressionSystem.MaximumLevel;
            var requiredExperience = characterProgressionSystem.GetExperienceRequiredForNextLevel(character.Level);
            if (progressionText != null) progressionText.text = experienceValueText != null ? $"Poziom {character.Level}" : maximumLevel
                ? $"Poziom {character.Level}  •  XP: maksimum"
                : $"Poziom {character.Level}  •  XP: {character.CurrentExperience}/{requiredExperience}";
            if (experienceValueText != null) experienceValueText.text = maximumLevel
                ? "maksimum" : $"{character.CurrentExperience}/{requiredExperience}";
            if (experienceFill != null) experienceFill.fillAmount = maximumLevel
                ? 1f : requiredExperience > 0 ? Mathf.Clamp01((float)character.CurrentExperience / requiredExperience) : 0f;
            if (availablePointsText != null) availablePointsText.text = $"Punkty rozwoju: {character.AvailableSkillPoints}";
            RefreshSelectedTreeHeader();
            RebuildTabs(); RebuildNodes();
            selectedNodeId = selectedTree?.Nodes?.FirstOrDefault(node => node?.Id == selectedNodeId)?.Id
                ?? selectedTree?.Nodes?.Where(node => node != null).OrderBy(node => node.Tier).ThenBy(node => node.Column).FirstOrDefault()?.Id;
            RefreshSelectedNode();
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
                    label.text = $"{tree.DisplayName}  {SkillTreeProgressionSystem.GetInvestedRanks(character, tree.Id)}/{SkillTreeProgressionSystem.MaximumPointsPerTree}";
                    label.color = tree.Id == selectedTree?.Id
                        ? new Color(1f, .8f, .35f) : new Color(.91f, .875f, .78f);
                }
                if (tree.Id == selectedTree?.Id)
                {
                    var selectedFrame = button.transform.Find("SelectionBorder")?.GetComponent<Image>();
                    if (selectedFrame != null) selectedFrame.enabled = true;
                }
                var treeForButton = tree;
                button.onClick.AddListener(() => {
                    selectedTree = treeForButton; selectedNodeId = null;
                    if (treeScroll != null) treeScroll.verticalNormalizedPosition = 1f;
                    Refresh();
                });
            }
        }

        private void RebuildNodes()
        {
            nodeRectsById.Clear();
            nodeViewsById.Clear();
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
                    var nodeForButton = node;
                    var treeForButton = selectedTree;
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
                            () => SelectNode(nodeForButton.Id),
                            () => TryInvest(treeForButton, nodeForButton),
                            character);
                        nodeViewsById[node.Id] = nodeView;
                    }
                    else
                    {
                        var label = button.GetComponentInChildren<TextMeshProUGUI>();
                        if (label != null) label.text = $"{node.DisplayName}\n{ranks}/{node.EffectiveMaxRanks}";
                        button.interactable = true;
                        button.onClick.RemoveAllListeners();
                        button.onClick.AddListener(() => SelectNode(nodeForButton.Id));
                    }
                }
            }
        }

        private IEnumerator RefreshTreeVisualsAfterLayout()
        {
            // Layout needs a frame, not a rendered Game view (also runs in batch tests).
            yield return null;
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
            if (row == null || treeScroll == null) return;
            var textRect = globalPassiveText.rectTransform;
            var width = textRect.rect.width;
            if (width <= 0f) return;
            var height = Mathf.Max(80f, Mathf.Ceil(globalPassiveText.GetPreferredValues(globalPassiveText.text, width, Mathf.Infinity).y));
            row.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            var bottom = -row.anchoredPosition.y + height;
            if (headerSeparator != null) headerSeparator.anchoredPosition = new Vector2(headerSeparator.anchoredPosition.x, -bottom - 4f);
            var scrollRect = (RectTransform)treeScroll.transform;
            scrollRect.offsetMax = new Vector2(scrollRect.offsetMax.x, -bottom - 18f);
            LayoutRebuilder.ForceRebuildLayoutImmediate(row.parent as RectTransform);
        }

        private RectTransform GetTreeArea() => prerequisiteConnectionRoot?.parent as RectTransform;

        private void ArrangeTreeVisualLayers()
        {
            // Gates and lines are visual aids only. Keep them underneath the interactive
            // node slots, otherwise a gate outline can steal pointer events from a node.
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
            prerequisiteConnectionRoot?.SetAsFirstSibling();

            if (tierRoots != null && tierRoots.Length > 0 && tierRoots[0] != null)
            {
                tierRoots[0].parent?.SetAsLastSibling();
            }
        }

        private void RefreshTierGates()
        {
            RefreshTierGate(0, 2, 3, SkillTreeProgressionSystem.GetRequiredRanksForTier(3));
            RefreshTierGate(1, 4, 5, SkillTreeProgressionSystem.GetRequiredRanksForTier(5));
            RefreshTierGate(2, 6, 6, SkillTreeProgressionSystem.GetRequiredRanksForTier(7));
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
            if (tierGateFills != null && gateIndex < tierGateFills.Length && tierGateFills[gateIndex] != null)
            {
                tierGateFills[gateIndex].fillAmount = threshold > 0 ? Mathf.Clamp01((float)invested / threshold) : 1f;
            }

            if (tierRoots == null || firstTierIndex >= tierRoots.Length || lastTierIndex >= tierRoots.Length
                || gate.parent is not RectTransform gateParent)
            {
                return;
            }

            var first = tierRoots.Skip(firstTierIndex).Take(lastTierIndex - firstTierIndex + 1)
                .OfType<RectTransform>().FirstOrDefault(root => root.gameObject.activeSelf);
            if (first == null) return;

            var firstCorners = new Vector3[4];
            first.GetWorldCorners(firstCorners);
            var topLeft = gateParent.InverseTransformPoint(firstCorners[1]);
            var topRight = gateParent.InverseTransformPoint(firstCorners[2]);
            gate.anchorMin = gate.anchorMax = new Vector2(0.5f, 0.5f);
            gate.pivot = new Vector2(0.5f, 0.5f);
            var anchorPosition = new Vector2(
                Mathf.Lerp(gateParent.rect.xMin, gateParent.rect.xMax, gate.anchorMin.x),
                Mathf.Lerp(gateParent.rect.yMin, gateParent.rect.yMax, gate.anchorMin.y));
            gate.anchoredPosition = new Vector2((topLeft.x + topRight.x) * .5f, topLeft.y + 22f) - anchorPosition;
            gate.sizeDelta = new Vector2(Mathf.Abs(topRight.x - topLeft.x), 28f);
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
                    line.color = GetNodeRanks(requirement.NodeId) >= requirement.MinimumRanks
                        ? new Color(.88f, .68f, .28f, 1f) : new Color(.44f, .48f, .43f, 1f);
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

            var sourceCentre = (sourceCorners[0] + sourceCorners[2]) * .5f;
            var targetCentre = (targetCorners[0] + targetCorners[2]) * .5f;
            Vector3 sourceEdge, targetEdge;
            if (Mathf.Abs(targetCentre.y - sourceCentre.y) < .5f)
            {
                var right = targetCentre.x > sourceCentre.x;
                sourceEdge = right ? (sourceCorners[2] + sourceCorners[3]) * .5f : (sourceCorners[0] + sourceCorners[1]) * .5f;
                targetEdge = right ? (targetCorners[0] + targetCorners[1]) * .5f : (targetCorners[2] + targetCorners[3]) * .5f;
            }
            else
            {
                var down = targetCentre.y < sourceCentre.y;
                sourceEdge = down ? (sourceCorners[0] + sourceCorners[3]) * .5f : (sourceCorners[1] + sourceCorners[2]) * .5f;
                targetEdge = down ? (targetCorners[1] + targetCorners[2]) * .5f : (targetCorners[0] + targetCorners[3]) * .5f;
            }
            var sourcePoint = prerequisiteConnectionRoot.InverseTransformPoint(sourceEdge);
            var targetPoint = prerequisiteConnectionRoot.InverseTransformPoint(targetEdge);
            var delta = targetPoint - sourcePoint;
            line.anchorMin = line.anchorMax = new Vector2(0.5f, 0.5f);
            line.pivot = new Vector2(0f, 0.5f);
            var anchorPosition = prerequisiteConnectionRoot.rect.center;
            line.anchoredPosition = (Vector2)sourcePoint - anchorPosition;
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
            {
                session.NotifyCharacterProgressionChanged(character.CharacterId);
                // The progression notification refreshes every open view, including this one.
            }
            else Refresh();
        }

        private int GetNodeRanks(string nodeId) => character?.SkillTreeProgress?
            .FirstOrDefault(progress => progress?.TreeId == selectedTree?.Id)?.NodeInvestments?
            .FirstOrDefault(investment => investment?.NodeId == nodeId)?.Ranks ?? 0;

        private void SelectNode(string nodeId)
        {
            if (selectedTree?.Nodes?.Any(node => node?.Id == nodeId) != true) return;
            selectedNodeId = nodeId;
            UIWorkspaceTooltipView.ActiveInstance?.HideForSourceWithin(transform);
            if (detailsScroll != null) detailsScroll.verticalNormalizedPosition = 1f;
            RefreshSelectedNode();
        }

        private void InvestSelectedNode()
        {
            var node = selectedTree?.Nodes?.FirstOrDefault(value => value?.Id == selectedNodeId);
            TryInvest(selectedTree, node);
        }

        private void RefreshSelectedNode()
        {
            foreach (var pair in nodeViewsById) pair.Value.SetSelected(pair.Key == selectedNodeId);
            var node = selectedTree?.Nodes?.FirstOrDefault(value => value?.Id == selectedNodeId);
            var ranks = GetNodeRanks(selectedNodeId);
            if (detailsTitle != null) detailsTitle.text = node?.DisplayName ?? "Brak węzłów";
            if (detailsIcon != null) { detailsIcon.sprite = node?.Icon; detailsIcon.enabled = detailsIcon.sprite != null; }
            if (detailsRank != null) detailsRank.text = node == null ? "" : $"Ranga: {ranks}/{node.EffectiveMaxRanks}";
            if (detailsBody != null) detailsBody.text = node == null ? "" : SkillTreeNodeView.BuildDetails(node, ranks, UIWorkspaceTooltipView.ActiveInstance, character);
            var reason = GetSelectedNodeBlockReason(node, ranks);
            if (investmentBlockText != null) investmentBlockText.text = reason;
            if (investButton != null)
            {
                investButton.gameObject.SetActive(node != null && ranks < node.EffectiveMaxRanks);
                investButton.interactable = node != null && string.IsNullOrWhiteSpace(reason)
                    && session != null && !session.IsCharacterInCombat(character.CharacterId);
            }
        }

        private string GetSelectedNodeBlockReason(SkillTreeNodeDefinition node, int ranks)
        {
            if (node == null || character == null) return "";
            if (ranks >= node.EffectiveMaxRanks) return "Maksymalna ranga";
            var invested = SkillTreeProgressionSystem.GetInvestedRanks(character, selectedTree.Id);
            var tierRequirement = SkillTreeProgressionSystem.GetRequiredRanksForTier(node.Tier);
            if (invested < tierRequirement) return $"Wymaga {tierRequirement} pkt w drzewku ({invested}/{tierRequirement})";
            var missing = (node.Requirements ?? new()).Where(requirement => requirement != null
                && GetNodeRanks(requirement.NodeId) < requirement.MinimumRanks).Select(requirement => {
                    var source = selectedTree.Nodes.FirstOrDefault(value => value?.Id == requirement.NodeId);
                    return $"{source?.DisplayName ?? requirement.NodeId} {GetNodeRanks(requirement.NodeId)}/{requirement.MinimumRanks}";
                }).ToArray();
            if (missing.Length > 0) return "Wymaga: " + string.Join(", ", missing);
            return progressionSystem.GetInvestmentBlockReason(character, selectedTree.Id, node.Id);
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
            if (selectedTreeNameText != null) selectedTreeNameText.text = string.Empty;

            if (globalPassiveText == null) return;

            var passive = selectedTree.GlobalPassive;
            if (passive == null || string.IsNullOrWhiteSpace(passive.DisplayName))
            {
                globalPassiveText.text = "Brak globalnej pasywy";
                return;
            }

            var summary = GetGlobalPassiveSummary(selectedTree, investedRanks, character?.ResourcePool?.ResourceType ?? ResourceType.None);

            globalPassiveText.text = $"<b>Premia za punkt</b>\n{GetGlobalPassiveSummary(selectedTree, 1, character?.ResourcePool?.ResourceType ?? ResourceType.None)}\nAktualnie: {summary}";
        }

        public static string GetGlobalPassiveSummary(SkillTreeDefinition tree, int investedRanks, ResourceType resource = ResourceType.None)
        {
            if (tree == null) return string.Empty;
            var points = Mathf.Max(0, investedRanks);
            // These passives contain combat rules as well as ordinary stat modifiers.
            switch (tree.Id)
            {
                case Godmist.Systems.Combat.PaladinCombatSystem.Priest:
                    return UiTerms.Present($"+{points}% siły leczenia, {points}% faktycznego leczenia przywraca HP rzucającego", resource);
                case Godmist.Systems.Combat.PaladinCombatSystem.Crusader:
                    return UiTerms.Present($"+{points}% obrażeń przeciw nieumarłym i demonom", resource);
                case Godmist.Systems.Combat.MageCombatSystem.Warlock:
                    return UiTerms.Present($"-{points * Godmist.Systems.Combat.MageCombatSystem.ManaCostReductionPerPoint * 100f:0.##}% kosztów many, +{points}% obrażeń przy jednym celu", resource);
                case Godmist.Systems.Combat.MageCombatSystem.Fire:
                    return UiTerms.Present($"+{points * 2}% obrażeń podpalenia, Kuli Ognia i Ognistej Strzały, +{points}% odporności na ogień", resource);
                case Godmist.Systems.Combat.ScoutCombatSystem.AssassinTree:
                    return UiTerms.Present($"+{points * .2f:0.##} {UiTerms.PercentagePoints} szansy krytycznej, +{points * .5f:0.##}% obrażeń wręcz", resource);
                case Godmist.Systems.Combat.ScoutCombatSystem.BladeTree:
                    return UiTerms.Present($"{points}% szansy na zachowanie sztyletu, +{points}% siły zatrucia", resource);
                case Godmist.Systems.Combat.WarriorCombatSystem.Commander:
                    return UiTerms.Present($"+{points * .3f:0.##} {UiTerms.PercentagePoints} szansy krytycznej, {points * .002f:0.###}% obrażeń trafienia jako nieuchronne za każdy procent ulepszenia broni", resource);
                case Godmist.Systems.Combat.WarriorCombatSystem.Brawler:
                    return UiTerms.Present($"+{points}% maksymalnej wściekłości, {points * 2}% wydanej wściekłości odzyskiwane jako HP", resource);
            }
            var summary = string.Join(", ", SkillTreeProgressionSystem
                .GetGlobalPassiveModifiers(tree, points).Select(modifier => FormatModifier(modifier, resource)));
            if (!string.IsNullOrWhiteSpace(summary)) return summary;
            return points == 0 ? "Nie wydano jeszcze punktów."
                : string.IsNullOrWhiteSpace(tree.GlobalPassive?.Description)
                    ? "Brak opisu pasywu." : UiTerms.Present(tree.GlobalPassive.Description, resource);
        }

        private static string FormatModifier(StatModifier modifier, ResourceType resource)
        {
            return UIWorkspaceTooltipView.FormatStatusStatModifier(modifier.StatType, modifier.Operation, modifier.Value, resource);
        }

        private static string GetStatDisplayName(CharacterStatType statType)
        {
            return UiTerms.Stat(statType);
        }

        private static void Clear(Transform root, Transform template)
        {
            if (root == null) return;
            for (var index = root.childCount - 1; index >= 0; index--)
                if (root.GetChild(index) != template)
                {
                    var child = root.GetChild(index).gameObject;
                    UIWorkspaceTooltipView.ActiveInstance?.HideForSourceWithin(child.transform);
                    var events = EventSystem.current;
                    var selected = events != null ? events.currentSelectedGameObject : null;
                    if (selected != null && selected.transform.IsChildOf(child.transform)) events.SetSelectedGameObject(null);
                    child.SetActive(false);
                    if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
                }
        }
    }
}

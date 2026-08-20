using System.Linq;
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

        private readonly SkillTreeProgressionSystem progressionSystem = new();
        private GameSession session;
        private CharacterInstance character;
        private SkillTreeDefinition selectedTree;

        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

        private void Awake()
        {
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
                        label.text = $"{node.DisplayName}\n{ranks}/{node.MaxRanks}";
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

        private static void Clear(Transform root, Transform template)
        {
            if (root == null) return;
            for (var index = root.childCount - 1; index >= 0; index--)
                if (root.GetChild(index) != template) Destroy(root.GetChild(index).gameObject);
        }
    }
}

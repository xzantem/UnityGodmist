using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Game;
using Godmist.Views.Exploration;
using Godmist.Views.UI;
using Godmist.Core.Enums;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Godmist.Views.UI.Workspace
{
    public enum ControllerCharacterPanel { Inventory, Development, Skills, Stats }
    public sealed class CharacterInfoPartyRailView : MonoBehaviour
    {
        [SerializeField] private Transform portraitRoot;
        [SerializeField] private CharacterInfoPartyPortraitView portraitTemplate;
        [SerializeField] private CharacterInfoWindowView characterInfoWindowPrefab;
        [SerializeField] private UIWorkspaceWindowManager workspaceManager;
        [SerializeField] private ExplorationPartyController partyController;
        [SerializeField] private PartyRailGroupView groupTemplate;
        [SerializeField] private PartyRailMenuView menuTemplate;
        [SerializeField] private CombatUnitInspectWindowView inspectTemplate;
        [SerializeField] private RectTransform dropZone;
        [SerializeField] private Image dragGhost, newGroupMarker;
        private readonly List<CharacterInfoPartyPortraitView> portraits = new();
        private readonly List<PartyRailGroupView> groupViews = new();
        private readonly Dictionary<string, CharacterInfoWindowView> windows = new();
        private GameSession session;
        private PartyRailMenuView menu;
        private CombatUnitInspectWindowView inspectWindow;
        private SkillTreeProgressionPanelView developmentWindow;
        private bool dirty;
        private CharacterInfoPartyPortraitView dragged;
        private string destinationMember;
        private int destinationIndex, newGroupIndex;
        private bool validDrop;
        private UIWorkspaceTooltipView dragTooltip;

        private void Awake()
        {
            if (workspaceManager == null) workspaceManager = UIWorkspaceWindowManager.Instance;
            CancelDrag();
        }
        private void Update()
        {
            var next = Godmist.Views.Game.GameBootstrap.Instance?.Session;
            if (next != session)
            {
                Unsubscribe(); CancelDrag(); menu?.Hide(); developmentWindow?.Close(); session = next;
                if (session != null) { session.PartyRosterChanged += Refresh; session.PlayerParty.GroupsChanged += Refresh; }
                dirty = true;
            }
            if (dragged != null && Input.GetKeyDown(KeyCode.Escape)) CancelDrag();
            if (session != null && (session.CurrentMode == GameModeState.Exploration || session.CurrentMode == GameModeState.Combat)
                && !DevConsoleView.IsOpen && !UIWorkspaceInputRouter.HasFocusedTextInput()
                && Input.GetKeyDown(KeyCode.I))
                ToggleActiveCharacterInfo();
            if (dirty && dragged == null) Rebuild();
            foreach (var portrait in portraits)
            {
                var combat = session?.GetCombatForCharacter(portrait.CharacterId);
                var unit = combat?.Units.FirstOrDefault(value => value.Character?.CharacterId == portrait.CharacterId);
                portrait.RefreshPresentation(unit, portrait.CharacterId == session?.PlayerParty.ActiveLeaderId,
                    unit != null && combat.ActiveUnit == unit);
            }
        }
        private void Unsubscribe()
        {
            if (session == null) return;
            session.PartyRosterChanged -= Refresh; session.PlayerParty.GroupsChanged -= Refresh;
        }
        private void OnDisable() { CancelDrag(); menu?.Hide(); }
        private void OnDestroy()
        {
            Unsubscribe();
            if (menu != null) Destroy(menu.gameObject);
            if (inspectWindow != null) Destroy(inspectWindow.gameObject);
            if (developmentWindow != null) Destroy(developmentWindow.gameObject);
            foreach (var window in windows.Values) if (window != null) Destroy(window.gameObject);
        }
        public void Refresh() { menu?.Hide(); dirty = true; }
        private void Rebuild()
        {
            dirty = false;
            foreach (var portrait in portraits) if (portrait != null) portrait.FlushProgressionFeedback();
            foreach (var group in groupViews) if (group != null) { group.gameObject.SetActive(false); Destroy(group.gameObject); }
            groupViews.Clear(); portraits.Clear();
            if (session == null || portraitTemplate == null || portraitRoot == null || groupTemplate == null) return;
            for (var i = 0; i < session.PlayerParty.GroupCount; i++)
            {
                var group = Instantiate(groupTemplate, portraitRoot); group.gameObject.SetActive(true); group.Highlight(false);
                groupViews.Add(group);
                var members = session.PlayerParty.GetGroup(i);
                for (var j = 0; j < members.Count; j++)
                {
                    if (!session.CharactersById.TryGetValue(members[j], out var character)) continue;
                    var portrait = Instantiate(portraitTemplate, group.MemberRoot);
                    portrait.gameObject.SetActive(true);
                    portrait.Bind(character, value => Select(value.CharacterId));
                    portrait.ConfigureRail(this, j == 0, j == members.Count - 1);
                    portraits.Add(portrait);
                }
                group.ConfigurePresentation(members.Count, i > 0);
            }
        }
        private void Select(string id) { menu?.Hide(); partyController?.SelectMember(id); }
        public void Center(string id) => partyController?.CenterMember(id);
        public void ShowMenu(CharacterInfoPartyPortraitView portrait, Vector2 position)
        {
            if (session == null || dragged != null || menuTemplate == null || workspaceManager?.WorkspaceRoot == null) return;
            UIWorkspaceTooltipView.ActiveInstance?.HideForSourceWithin(portrait.transform);
            menu ??= Instantiate(menuTemplate, workspaceManager.WorkspaceRoot);
            var id = portrait.CharacterId;
            if (!session.CharactersById.TryGetValue(id, out var character)) return;
            var party = session.PlayerParty; var groupIndex = party.GetGroupIndex(id);
            if (groupIndex < 0) return;
            var actions = new List<PartyRailMenuView.Entry> {
                new("Karta postaci", () => OpenCharacterInfo(character), characterInfoWindowPrefab != null),
                new("Statystyki i efekty", () => Inspect(character), inspectTemplate != null),
                new("Wycentruj kamerę", () => Center(id), partyController != null),
                new("Odłącz od grupy", () => party.TryDetach(id), party.GetGroup(groupIndex).Count > 1, separatorBefore: true)
            };
            var groups = new List<PartyRailMenuView.Entry>();
            for (var i = 0; i < party.GroupCount; i++)
            {
                if (i == groupIndex) continue;
                var target = party.GetGroup(i)[0];
                var name = session.CharactersById[target].Definition?.DisplayName ?? target;
                groups.Add(new(name, () => party.TryJoinGroup(id, target)));
            }
            if (groups.Count > 0) actions.Add(new("Dołącz do grupy →", children: groups));
            var hint = party.GetGroup(groupIndex).Count == 1 ? "Postać porusza się samodzielnie." : null;
            menu.Show(position, actions, character.Definition?.DisplayName ?? id, hint);
        }
        private void Inspect(CharacterInstance character)
        {
            if (workspaceManager?.WorkspaceRoot == null) return;
            inspectWindow ??= CombatUnitInspectWindowView.Create(workspaceManager.WorkspaceRoot, inspectTemplate);
            var unit = session.Combats.SelectMany(combat => combat.Units).FirstOrDefault(value => value.Character == character)
                ?? new BattleUnit { Character = character, IsPlayerSide = true };
            inspectWindow?.Show(unit, UIWorkspaceTooltipView.ActiveInstance);
        }
        public void BeginDrag(CharacterInfoPartyPortraitView portrait, PointerEventData e)
        {
            if (session == null) return;
            menu?.Hide(); dragged = portrait;
            dragTooltip = UIWorkspaceTooltipView.ActiveInstance;
            dragTooltip?.BeginDragSuppression();
            if (dragGhost != null) { dragGhost.sprite = portrait.PortraitSprite; dragGhost.gameObject.SetActive(true); dragGhost.transform.SetAsLastSibling(); }
            Drag(e);
        }
        public void Drag(PointerEventData e)
        {
            if (dragged == null) return;
            // Status icons and other hover sources may queue a tooltip as the
            // pointer crosses them. The drag preview owns this interaction.
            dragTooltip?.Hide();
            foreach (var row in portraits) row.MarkInsertion(false, false);
            foreach (var group in groupViews) group.Highlight(false);
            if (newGroupMarker != null) newGroupMarker.enabled = false;
            validDrop = dropZone != null && RectTransformUtility.RectangleContainsScreenPoint(dropZone, e.position, e.pressEventCamera);
            destinationMember = null; destinationIndex = -1; newGroupIndex = groupViews.Count;
            MoveMarker(dragGhost, e, new Vector2(24, 0));
            if (!validDrop) return;
            for (var i = 0; i < groupViews.Count; i++)
            {
                var group = groupViews[i];
                var corners = new Vector3[4]; group.Rect.GetWorldCorners(corners);
                if (e.position.y > RectTransformUtility.WorldToScreenPoint(e.pressEventCamera, corners[1]).y) { newGroupIndex = i; break; }
                if (!RectTransformUtility.RectangleContainsScreenPoint(group.Rect, e.position, e.pressEventCamera)) continue;
                var members = session.PlayerParty.GetGroup(i);
                destinationMember = members[0]; destinationIndex = members.Count; group.Highlight(true);
                foreach (var row in portraits.Where(row => members.Contains(row.CharacterId)))
                {
                    var center = RectTransformUtility.WorldToScreenPoint(e.pressEventCamera, row.Rect.TransformPoint(row.Rect.rect.center));
                    if (e.position.y > center.y) { destinationIndex = members.ToList().IndexOf(row.CharacterId); row.MarkInsertion(true, false); break; }
                }
                if (destinationIndex == members.Count) portraits.First(row => row.CharacterId == members[members.Count - 1]).MarkInsertion(false, true);
                return;
            }
            if (newGroupMarker != null) { newGroupMarker.enabled = true; MoveMarker(newGroupMarker, e, Vector2.zero); }
        }
        private static void MoveMarker(Image marker, PointerEventData e, Vector2 offset)
        {
            if (marker == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)marker.transform.parent, e.position,
                e.pressEventCamera, out var local);
            marker.rectTransform.localPosition = new Vector3(local.x + offset.x, local.y + offset.y, 0);
        }
        public void EndDrag(PointerEventData e)
        {
            if (dragged == null) return;
            Drag(e); var id = dragged.CharacterId;
            if (validDrop && session != null)
            {
                if (destinationMember == null) session.PlayerParty.TryDetach(id, newGroupIndex);
                else session.PlayerParty.TryJoinGroup(id, destinationMember, destinationIndex);
            }
            CancelDrag();
        }
        public void CancelDrag()
        {
            dragTooltip?.EndDragSuppression();
            dragTooltip = null;
            dragged = null; validDrop = false;
            if (dragGhost != null) dragGhost.gameObject.SetActive(false);
            if (newGroupMarker != null) newGroupMarker.enabled = false;
            foreach (var row in portraits) if (row != null) row.MarkInsertion(false, false);
            foreach (var group in groupViews) if (group != null) group.Highlight(false);
        }
        public bool CanOpenDevelopment(CharacterInstance character) => character != null && session != null
            && session.CharactersById.TryGetValue(character.CharacterId, out var registered) && ReferenceEquals(registered, character)
            && character.AvailableSkillPoints > 0 && !session.IsCharacterInCombat(character.CharacterId);
        public void OpenCharacterDevelopment(CharacterInstance character)
        {
            if (!CanOpenDevelopment(character)) return;
            var manager = workspaceManager ?? UIWorkspaceWindowManager.Instance;
            if (manager?.WorkspaceRoot == null || characterInfoWindowPrefab?.DevelopmentWindowPrefab == null) return;
            menu?.Hide(); UIWorkspaceTooltipView.ActiveInstance?.HideForSourceWithin(transform);
            if (developmentWindow == null)
                developmentWindow = Instantiate(characterInfoWindowPrefab.DevelopmentWindowPrefab, manager.WorkspaceRoot);
            developmentWindow.Open(session, character.CharacterId);
        }
        public void ShowProgressionFeedback(CharacterInstance character, int amount, int levels, RectTransform portrait, TMPro.TMP_Text fontSource)
        {
            if (!isActiveAndEnabled || character == null || amount <= 0) return;
            var root = workspaceManager?.WorkspaceRoot ?? GetComponentInParent<Canvas>()?.transform as RectTransform;
            if (root == null) return;
            var canvas = root.GetComponentInParent<Canvas>();
            var uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            var screen = RectTransformUtility.WorldToScreenPoint(uiCamera, portrait.TransformPoint(new Vector2(portrait.rect.center.x, portrait.rect.yMax)));
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, uiCamera, out var local);
            local.y += levels > 0 ? 74 : 50;
            if (local.y > root.rect.yMax - 80)
                local.x += portrait.rect.width * .5f + 226;
            local.x = Mathf.Clamp(local.x, root.rect.xMin + 210, root.rect.xMax - 210);
            local.y = Mathf.Clamp(local.y, root.rect.yMin + 55, root.rect.yMax - 80);
            var text = $"+{amount} XP" + (levels > 0 ? "\nZdobyto poziom!" + (levels > 1 ? $" ×{levels}" : "") : "");
            var view = CombatFloatingFeedbackView.Create(root, fontSource);
            view.Show(text, levels > 0 ? new Color(1, .82f, .35f) : new Color(.55f, .85f, 1f), local, 0, levels > 0);
            var world = partyController?.ActiveMemberViews.FirstOrDefault(v => v != null && v.CharacterId == character.CharacterId && v.gameObject.activeInHierarchy);
            var camera = Camera.main;
            if (world != null && camera != null)
            {
                Vector3? Anchor() => world != null ? world.BodyRenderer != null
                    ? new Vector3(world.BodyRenderer.bounds.center.x, world.BodyRenderer.bounds.max.y, 0)
                    : world.transform.position + Vector3.up : null;
                var anchor = Anchor().Value;
                var viewport = camera.WorldToViewportPoint(anchor);
                if (viewport.z > 0 && viewport.x >= 0 && viewport.x <= 1 && viewport.y >= 0 && viewport.y <= 1
                    && (world.BodyRenderer == null || world.BodyRenderer.enabled && !world.BodyRenderer.forceRenderingOff))
                    view.BindWorldAnchor(camera, root, Anchor, anchor, new Vector2(0, levels > 0 ? 64 : 40));
            }
        }
        public void OpenControllerCharacterPanel(ControllerCharacterPanel panel, string characterId)
        {
            var id = characterId;
            if (session == null || string.IsNullOrEmpty(id) || !session.CharactersById.TryGetValue(id, out var character)) return;
            if (panel == ControllerCharacterPanel.Stats) { Inspect(character); return; }
            OpenCharacterInfo(character);
            if (!windows.TryGetValue(id, out var view) || view == null) return;
            if (panel == ControllerCharacterPanel.Development) view.OpenSkillTree();
            if (panel == ControllerCharacterPanel.Skills) view.OpenSkillLoadout();
        }

        private void OpenCharacterInfo(CharacterInstance character)
        {
            if (character == null || characterInfoWindowPrefab == null || session == null) return;
            if (windows.TryGetValue(character.CharacterId, out var existing) && existing != null)
            {
                existing.gameObject.SetActive(true); existing.Initialize(session, character.CharacterId); return;
            }
            var manager = workspaceManager ?? UIWorkspaceWindowManager.Instance;
            if (manager?.WorkspaceRoot == null) return;
            var previousWindow = manager.FocusedWindow;
            var view = Instantiate(characterInfoWindowPrefab, manager.WorkspaceRoot);
            windows[character.CharacterId] = view; view.Initialize(session, character.CharacterId);
            view.CampAction = (ownerId, itemId) => partyController != null ? partyController.TryCamp(ownerId, itemId) : "Brak aktywnej drużyny.";
            manager.CascadeAndFocus(view.GetComponent<UIWorkspaceWindow>(), previousWindow);
        }
        private void ToggleActiveCharacterInfo()
        {
            var leaderId = session?.PlayerParty.ActiveLeaderId;
            if (string.IsNullOrEmpty(leaderId)
                || !session.CharactersById.TryGetValue(leaderId, out var character)) return;

            menu?.Hide();

            if (windows.TryGetValue(leaderId, out var existing)
                && existing != null && existing.gameObject.activeInHierarchy)
            {
                existing.GetComponent<UIWorkspaceWindow>()?.Close();
                return;
            }

            OpenCharacterInfo(character);
        }
    }
}

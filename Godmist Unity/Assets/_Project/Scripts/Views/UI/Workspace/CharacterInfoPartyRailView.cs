using System.Collections.Generic;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Game;
using UnityEngine;

namespace Godmist.Views.UI.Workspace
{
    public sealed class CharacterInfoPartyRailView : MonoBehaviour
    {
        [SerializeField] private Transform portraitRoot;
        [SerializeField] private CharacterInfoPartyPortraitView portraitTemplate;
        [SerializeField] private CharacterInfoWindowView characterInfoWindowPrefab;
        [SerializeField] private UIWorkspaceWindowManager workspaceManager;

        private readonly List<CharacterInfoPartyPortraitView> portraits = new();
        private readonly Dictionary<string, CharacterInfoWindowView> windows = new();
        private GameSession session;

        private void Awake()
        {
            if (portraitTemplate != null) portraitTemplate.gameObject.SetActive(false);
            if (workspaceManager == null) workspaceManager = UIWorkspaceWindowManager.Instance;
        }

        private void Update()
        {
            var nextSession = Godmist.Views.Game.GameBootstrap.Instance?.Session;
            if (nextSession == session) return;
            if (session != null) session.PartyRosterChanged -= Refresh;
            session = nextSession;
            if (session != null) session.PartyRosterChanged += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (session != null) session.PartyRosterChanged -= Refresh;
        }

        public void Refresh()
        {
            foreach (var portrait in portraits)
            {
                if (portrait != null) Destroy(portrait.gameObject);
            }

            portraits.Clear();
            if (session == null || portraitTemplate == null || portraitRoot == null) return;

            foreach (var id in session.PlayerParty.ActiveMemberIds)
            {
                if (!session.CharactersById.TryGetValue(id, out var character)) continue;
                var portrait = Instantiate(portraitTemplate, portraitRoot);
                portrait.gameObject.SetActive(true);
                portrait.Bind(character, OpenCharacterInfo);
                portraits.Add(portrait);
            }
        }

        private void OpenCharacterInfo(CharacterInstance character)
        {
            if (character == null || characterInfoWindowPrefab == null || session == null) return;
            if (windows.TryGetValue(character.CharacterId, out var existing) && existing != null)
            {
                existing.gameObject.SetActive(true);
                existing.Initialize(session, character.CharacterId);
                return;
            }

            var manager = workspaceManager ?? UIWorkspaceWindowManager.Instance;
            if (manager?.WorkspaceRoot == null) return;
            var previousWindow = manager.FocusedWindow;
            var view = Instantiate(characterInfoWindowPrefab, manager.WorkspaceRoot);
            windows[character.CharacterId] = view;
            view.Initialize(session, character.CharacterId);
            manager.CascadeAndFocus(view.GetComponent<UIWorkspaceWindow>(), previousWindow);
        }
    }
}

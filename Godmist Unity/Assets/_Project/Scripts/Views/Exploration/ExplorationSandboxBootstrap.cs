using System;
using System.Collections.Generic;
using Godmist.Data.Characters;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Game;
using Godmist.Systems.Characters;
using Godmist.Views.Game;
using Godmist.Data.NPC;
using Godmist.Runtime.NPC;
using Godmist.Systems.NPC;
using Godmist.Views.NPC;
using UnityEngine;

namespace Godmist.Views.Exploration
{
    public class ExplorationSandboxBootstrap : MonoBehaviour
    {
        [SerializeField] private ExplorationPartyController partyController;
        [SerializeField] private List<ExplorationCharacterBinding> partyBindings = new();
        [SerializeField] private List<NpcSceneBinding> npcBindings = new();
        private readonly CharacterInstanceFactory characterFactory = new();
        private readonly NpcInstanceFactory npcFactory = new();

        private void Start()
        {
            var gameBootstrap = GameBootstrap.Instance;

            if (gameBootstrap == null)
            {
                Debug.LogError("ExplorationSandboxBootstrap: uruchom scene przez Bootstrap.");
                return;
            }

            var session = gameBootstrap.Session;
            var characterViews = new List<ExplorationCharacterView>();

            foreach (var binding in partyBindings)
            {
                var character = GetOrCreateCharacter(session, binding);

                if (character == null) continue;

                if (!session.PlayerParty.ContainsMember(character.CharacterId))
                {
                    session.TryAddCharacterToActiveParty(character.CharacterId);
                }

                binding.CharacterView.Bind(character);
                characterViews.Add(binding.CharacterView);
            }

            foreach (var binding in npcBindings)
            {
                var npc = GetOrCreateNpc(session, binding);

                if (npc == null)
                {
                    continue;
                }

                binding.NpcView.Initialize(partyController, npc);
            }

            if (string.IsNullOrEmpty(session.PlayerParty.ActiveLeaderId) && characterViews.Count > 0)
            {
                session.TrySetActiveLeader(characterViews[0].CharacterId);
            }

            partyController.Initialize(session, characterViews);

            // Arungard owns a separate town position. The active dungeon may
            // remain in the session after a return scroll, but its saved
            // coordinates belong to DungeonSandbox and must not be applied to
            // this scene or overwritten by town movement.
            if (session.HasExplorationPosition)
            {
                partyController.RestorePartyFormation(session.ExplorationPosition);
            }
            else if (partyController.ActiveLeaderTransform != null)
            {
                session.TrySetExplorationPosition(
                    partyController.ActiveLeaderTransform.position);
            }
        }

        private CharacterInstance GetOrCreateCharacter(
            GameSession session,
            ExplorationCharacterBinding binding)
        {
            if (binding == null || binding.Definition == null || binding.CharacterView == null) return null;

            var characterId = binding.Definition.Id;

            if (string.IsNullOrWhiteSpace(characterId))
            {
                Debug.LogError("ExplorationSandboxBootstrap: CharacterDefinition ma puste Id.");
                return null;
            }

            if (session.CharactersById
                .TryGetValue(characterId, out var existingCharacter)) return existingCharacter;

            if (binding.ClassData == null)
            {
                Debug.LogError("ExplorationSandboxBootstrap: brak CharacterClassData.");
                return null;
            }

            var character = characterFactory.Create(binding.Definition, binding.ClassData);

            return session.TryRegisterCharacter(character) ? character : null;
        }

        private NpcInstance GetOrCreateNpc(
            GameSession session,
            NpcSceneBinding binding)
        {
            if (binding == null || binding.Definition == null || binding.NpcView == null)
            {
                return null;
            }

            var npcId = binding.Definition.Id;

            if (string.IsNullOrWhiteSpace(npcId))
            {
                Debug.LogError("ExplorationSandboxBootstrap: NpcDefinition ma puste Id.");
                return null;
            }

            if (session.NpcsById.TryGetValue(npcId, out var existingNpc))
            {
                return existingNpc;
            }

            var npc = npcFactory.Create(binding.Definition);

            return session.TryRegisterNpc(npc) ? npc : null;
        }
    }

    [Serializable]
    public class ExplorationCharacterBinding
    {
        public CharacterDefinition Definition;
        public CharacterClassData ClassData;
        public ExplorationCharacterView CharacterView;
    }

    [Serializable]
    public class NpcSceneBinding
    {
        public NpcDefinition Definition;
        public NpcView NpcView;
    }
}

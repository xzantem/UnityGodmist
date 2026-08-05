using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Systems.Characters;
using Godmist.Systems.Dungeons;
using Godmist.Views.Game;
using UnityEngine;

namespace Godmist.Views.Exploration
{
    public class ExplorationSandboxBootstrap : MonoBehaviour
    {
        [SerializeField] private ExplorationPartyController partyController;
        [SerializeField] private DungeonDefinition dungeonDefinition;
        [SerializeField] private List<ExplorationCharacterBinding> partyBindings = new();
        private readonly CharacterInstanceFactory characterFactory = new();
        private readonly DungeonEncounterRewardPreparationSystem rewardPreparationSystem = new();

        private void Start()
        {
            var gameBootstrap = GameBootstrap.Instance;

            if (gameBootstrap == null)
            {
                Debug.LogError("ExplorationSandboxBootstrap: uruchom scene przez Bootstrap.");
                return;
            }

            var session = gameBootstrap.Session;
            InitializeDungeon(session);
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

            if (string.IsNullOrEmpty(session.PlayerParty.ActiveLeaderId) && characterViews.Count > 0)
            {
                session.TrySetActiveLeader(characterViews[0].CharacterId);
            }

            partyController.Initialize(session, characterViews);

            if (session.ActiveDungeon != null && session.ActiveDungeon.HasPlayerPosition)
            {
                partyController.RestorePartyFormation(session.ActiveDungeon.PlayerPosition);
            }
        }

        private void InitializeDungeon(GameSession session)
        {
            if (dungeonDefinition == null)
            {
                Debug.LogError("ExplorationSandboxBootstrap: brak DungeonDefinition.");
                return;
            }

            if (session.ActiveDungeon != null && session.ActiveDungeon.DungeonId == dungeonDefinition.Id) return;

            var dungeon = new DungeonInstance(dungeonDefinition, seed: 0);

            rewardPreparationSystem.Prepare(dungeon);
            session.TrySetActiveDungeon(dungeon);
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
    }

    [Serializable]
    public class ExplorationCharacterBinding
    {
        public CharacterDefinition Definition;
        public CharacterClassData ClassData;
        public ExplorationCharacterView CharacterView;
    }
}
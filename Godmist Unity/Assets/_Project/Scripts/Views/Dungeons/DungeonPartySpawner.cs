using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Runtime.Game;
using Godmist.Views.Exploration;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    public class DungeonPartySpawner : MonoBehaviour
    {
        [SerializeField] private ExplorationPartyController partyController;
        [SerializeField] private ExplorationCharacterView characterPrefab;
        [SerializeField] private Transform partyRoot;

        private readonly List<ExplorationCharacterView> spawnedViews = new();
        public ExplorationPartyController PartyController => partyController;

        public bool TrySpawn(
            GameSession session,
            Vector2 startPosition)
        {
            if (session == null
                || partyController == null
                || characterPrefab == null)
            {
                Debug.LogError(
                    "DungeonPartySpawner: brak GameSession, PartyController lub prefabu postaci.");

                return false;
            }

            ClearSpawnedViews();

            foreach (var characterId in session.PlayerParty.ActiveMemberIds)
            {
                if (!session.CharactersById.TryGetValue(
                        characterId,
                        out var character)
                    || character.LifeState != CharacterLifeState.Alive)
                {
                    continue;
                }

                var parent = partyRoot != null ? partyRoot : transform;

                var characterView = Instantiate(
                    characterPrefab,
                    parent);

                characterView.Bind(character);
                spawnedViews.Add(characterView);
            }

            if (spawnedViews.Count == 0)
            {
                Debug.LogError(
                    "DungeonPartySpawner: brak zywych aktywnych czlonkow druzyny.");

                return false;
            }

            if (!TryEnsureActiveLeaderHasView(session))
            {
                Debug.LogError(
                    "DungeonPartySpawner: nie udalo sie wybrac zywego lidera druzyny.");

                return false;
            }

            partyController.Initialize(session, spawnedViews);
            partyController.RestorePartyFormation(startPosition);

            return true;
        }

        private bool TryEnsureActiveLeaderHasView(GameSession session)
        {
            if (session == null || spawnedViews.Count == 0) return false;

            foreach (var spawnedView in spawnedViews)
            {
                if (spawnedView != null
                    && spawnedView.CharacterId
                        == session.PlayerParty.ActiveLeaderId)
                {
                    return true;
                }
            }

            var fallbackLeader = spawnedViews[0];

            if (fallbackLeader == null
                || string.IsNullOrWhiteSpace(fallbackLeader.CharacterId))
            {
                return false;
            }

            return session.TrySetActiveLeader(fallbackLeader.CharacterId);
        }

        private void ClearSpawnedViews()
        {
            foreach (var spawnedView in spawnedViews)
            {
                if (spawnedView != null)
                {
                    Destroy(spawnedView.gameObject);
                }
            }

            spawnedViews.Clear();
        }
    }
}
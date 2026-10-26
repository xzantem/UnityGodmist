using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Quests;
using Godmist.Views.Game;
using Godmist.Systems.Dungeons;
using UnityEngine;

namespace Godmist.Views.Quests
{
    public class QuestContentBootstrap : MonoBehaviour
    {
        [SerializeField]
        private List<QuestDefinition> questDefinitions = new();

        private void Start()
        {
            var gameBootstrap = GameBootstrap.Instance;

            if (gameBootstrap == null
                || gameBootstrap.Session == null
                || gameBootstrap.QuestService == null)
            {
                Debug.LogError(
                    "QuestContentBootstrap: brak GameBootstrap, "
                    + "GameSession lub QuestService.");

                return;
            }

            var definitions = new List<QuestDefinition>(questDefinitions);
            if (gameBootstrap.ContentCatalog?.QuestDefinitions != null)
            {
                foreach (var definition in gameBootstrap.ContentCatalog.QuestDefinitions)
                    if (definition != null && definition.QuestType == QuestType.Radiant
                        && !definitions.Contains(definition))
                        definitions.Add(definition);
            }

            foreach (var definition in definitions)
            {
                if (definition == null
                    || string.IsNullOrWhiteSpace(definition.Id))
                {
                    continue;
                }

                if (gameBootstrap.Session.TryGetQuest(
                        definition.Id,
                        out _))
                {
                    continue;
                }

                gameBootstrap.QuestService.TryRegisterQuest(
                    definition,
                    QuestStatus.Unavailable);
            }

            if (gameBootstrap.Session.ActiveDungeon == null)
            {
                DungeonOfferSystem.EnsureOffers(gameBootstrap.Session,
                    gameBootstrap.ContentCatalog?.DungeonDefinitions);
                gameBootstrap.QuestService.EnsureMissionBoard();
            }
        }
    }
}

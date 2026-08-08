using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Quests;
using Godmist.Views.Game;
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

            foreach (var definition in questDefinitions)
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
        }
    }
}
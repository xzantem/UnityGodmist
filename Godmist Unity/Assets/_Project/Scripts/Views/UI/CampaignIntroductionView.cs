using Godmist.Core.Enums;
using Godmist.Data.Quests;
using Godmist.Systems.Characters;
using Godmist.Views.Game;
using TMPro;
using UnityEngine;

namespace Godmist.Views.UI
{
    public sealed class CampaignIntroductionView : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private QuestDefinition firstQuest;
        [SerializeField, TextArea] private string introduction = "Porozmawiaj ze strażnikiem oznaczonym wykrzyknikiem. Ma dla was pierwsze zlecenie do Katakumb.";
        private void Start() { if (messageText != null) messageText.text = introduction; }
        private void Update()
        {
            if (panelRoot == null) return;
            var session = GameBootstrap.Instance?.Session;
            var show = session != null && firstQuest != null && session.CurrentMode == GameModeState.Exploration
                && session.WorldState.HasFlag(NewCampaignService.InitializedFlag)
                && (!session.TryGetQuest(firstQuest.Id, out var quest)
                    || quest.Status == QuestStatus.Unavailable || quest.Status == QuestStatus.Available);
            if (panelRoot.activeSelf != show) panelRoot.SetActive(show);
        }
    }
}

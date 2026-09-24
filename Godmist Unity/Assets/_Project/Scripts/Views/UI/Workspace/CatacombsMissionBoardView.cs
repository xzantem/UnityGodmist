using System;
using Godmist.Core.Enums;
using Godmist.Runtime.Game;
using Godmist.Runtime.NPC;
using Godmist.Runtime.Quests;
using Godmist.Systems.Quests;
using Godmist.Views.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI.Workspace
{
    public sealed class CatacombsMissionBoardView : MonoBehaviour
    {
        [SerializeField] private UIWorkspaceWindow window;
        [SerializeField] private Transform questList;
        [SerializeField] private Button questButtonTemplate;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI detailsText;
        [SerializeField] private TextMeshProUGUI rewardText;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private Button actionButton;
        [SerializeField] private TextMeshProUGUI actionButtonText;

        private GameSession session;
        private QuestService service;
        private string selectedQuestId;

        private void Awake()
        {
            if (actionButton != null) actionButton.onClick.AddListener(Act);
        }

        private void OnDestroy()
        {
            if (actionButton != null) actionButton.onClick.RemoveListener(Act);
        }

        private void OnDisable()
        {
            if (session?.CurrentMode == GameModeState.Menu)
                session.TrySetMode(GameModeState.Exploration);
        }

        public bool Open(NpcInstance npc)
        {
            if (npc?.NpcId != "npc.arungard.questgiver_01") return false;
            var bootstrap = GameBootstrap.Instance;
            session = bootstrap?.Session;
            service = bootstrap?.QuestService;
            if (session == null || service == null || window == null
                || questList == null || questButtonTemplate == null
                || !session.TrySetMode(GameModeState.Menu)) return false;

            selectedQuestId = CatacombsMissionBoard.KillQuestId;
            messageText.text = string.Empty;
            window.Open();
            Refresh();
            return true;
        }

        private void Refresh()
        {
            for (var i = questList.childCount - 1; i >= 0; i--)
            {
                var child = questList.GetChild(i);
                if (child == questButtonTemplate.transform) continue;
                Destroy(child.gameObject);
            }

            QuestInstance firstVisible = null;
            foreach (var questId in CatacombsMissionBoard.QuestIds)
            {
                if (!session.TryGetQuest(questId, out var quest)
                    || !IsVisible(quest)) continue;
                service.EnsureRadiantRewardOffer(questId);
                firstVisible ??= quest;
                var row = Instantiate(questButtonTemplate, questList);
                row.gameObject.SetActive(true);
                var rowTitle = row.transform.Find("TextRoot/Title")?.GetComponent<TextMeshProUGUI>();
                var rowDetail = row.transform.Find("TextRoot/Detail")?.GetComponent<TextMeshProUGUI>();
                var rowBorder = row.transform.Find("Border")?.GetComponent<Image>();
                if (rowTitle != null) rowTitle.text = quest.Definition.DisplayName;
                if (rowDetail != null) rowDetail.text = $"Poziom {quest.RadiantRewardOffer?.Level ?? 0} · "
                    + (quest.Status switch
                    {
                        QuestStatus.Active => "W trakcie",
                        QuestStatus.Completed => "Odbierz nagrodę",
                        _ => "Dostępne"
                    });
                if (rowBorder != null && questId == selectedQuestId)
                {
                    var palette = row.GetComponent<UIWorkspaceStyleApplier>()?.Palette;
                    if (palette != null)
                        rowBorder.color = palette.GetAccentColor(UIWorkspaceAccentStyle.Selected);
                }
                var capturedId = questId;
                row.onClick.AddListener(() => Select(capturedId));
            }

            if (!session.TryGetQuest(selectedQuestId, out var selected)
                || !IsVisible(selected))
            {
                selected = firstVisible;
                selectedQuestId = selected?.QuestId;
            }
            RefreshDetails(selected);
        }

        private bool IsVisible(QuestInstance quest) =>
            quest.Status is QuestStatus.Active or QuestStatus.Completed
            || CatacombsMissionBoard.CanOffer(session, quest.QuestId);

        private void Select(string questId)
        {
            selectedQuestId = questId;
            messageText.text = string.Empty;
            Refresh();
        }

        private void RefreshDetails(QuestInstance quest)
        {
            if (quest == null)
            {
                titleText.text = "Brak dostępnych zleceń";
                detailsText.text = "Wróć, gdy pojawią się nowe wyprawy.";
                rewardText.text = string.Empty;
                actionButton.interactable = false;
                actionButtonText.text = "Brak zlecenia";
                return;
            }

            titleText.text = quest.Definition.DisplayName;
            var offer = quest.RadiantRewardOffer;
            detailsText.text = quest.Definition.Description;
            if (offer != null)
                detailsText.text += $"\nPoziom zlecenia: {offer.Level}. "
                    + "Liczą się wyprawy na tym lub wyższym poziomie.";
            if (quest.QuestId == CatacombsMissionBoard.BossQuestId)
            {
                var level = offer?.Level ?? CatacombsMissionBoard.BossOfferLevel(session);
                if (level > 0) detailsText.text += $"\nWyprawa z bossem: poziom {level}.";
            }
            if (quest.Status == QuestStatus.Active)
            {
                foreach (var objective in quest.CurrentStage.Objectives)
                {
                    if (objective == null) continue;
                    quest.TryGetObjectiveProgress(objective.ObjectiveId, out var progress);
                    detailsText.text += $"\n{objective.Description}: {progress?.CurrentAmount ?? 0}/{objective.RequiredAmount}";
                }
            }

            rewardText.text = offer == null ? "Nagroda niedostępna." :
                $"Nagroda: {offer.Experience:N0} doświadczenia, {offer.Gold:N0} złota\n"
                + $"{offer.Honor} honoru"
                + (offer.BonusItem == null ? "\nBez dodatkowego przedmiotu"
                    : $"\nPrzedmiot: {offer.BonusItem.DisplayName}");

            actionButton.interactable = quest.Status is QuestStatus.Unavailable
                or QuestStatus.Available or QuestStatus.Completed;
            actionButtonText.text = quest.Status switch
            {
                QuestStatus.Completed => "Odbierz nagrodę",
                QuestStatus.Active => "W trakcie",
                _ => "Przyjmij zlecenie"
            };
        }

        private void Act()
        {
            if (!session.TryGetQuest(selectedQuestId, out var quest)) return;
            var success = quest.Status switch
            {
                QuestStatus.Completed => service.TryTurnInRadiantQuest(quest.QuestId),
                QuestStatus.Unavailable => CatacombsMissionBoard.CanOffer(session, quest.QuestId)
                    && service.TryMakeQuestAvailable(quest.QuestId)
                    && service.TryStartQuest(quest.QuestId),
                QuestStatus.Available => CatacombsMissionBoard.CanOffer(session, quest.QuestId)
                    && service.TryStartQuest(quest.QuestId),
                _ => false
            };
            messageText.text = success ? "Zapisano zlecenie." : "Nie można teraz wykonać tej czynności.";
            if (success) GameBootstrap.Instance?.RequestAutosave("Zlecenie Katakumb");
            Refresh();
        }
    }
}

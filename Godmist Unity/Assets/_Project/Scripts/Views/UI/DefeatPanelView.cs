using Godmist.Views.Game;
using Godmist.Systems.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Godmist.Views.UI
{
    public sealed class DefeatPanelView : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button latestButton;
        [SerializeField] private Button otherButton;
        [SerializeField] private Button menuButton;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private SaveLoadPanelView saveLoadPanel;
        [SerializeField] private PauseMenuView pauseMenu;
        private bool shown;
        private void Awake()
        {
            panelRoot.SetActive(false);
            latestButton.onClick.AddListener(LoadLatest);
            otherButton.onClick.AddListener(OpenOther);
            menuButton.onClick.AddListener(ReturnToMenu);
            saveLoadPanel.Closed += ReturnFromList;
        }
        private void Update()
        {
            if (shown || GameBootstrap.Instance?.Session?.IsDefeated != true) return;
            shown = true;
            Refresh();
        }
        private void Refresh()
        {
            panelRoot.SetActive(true);
            var hasSaves = GameBootstrap.Instance.GetDefeatSaves().Count > 0;
            latestButton.interactable = otherButton.interactable = hasSaves;
            var reason = CampaignDefeatSystem.GetDefeatReason(GameBootstrap.Instance.Session, out var deadMain);
            var name = deadMain?.Definition?.DisplayName;
            if (string.IsNullOrWhiteSpace(name)) name = "Główna postać";
            messageText.text = reason switch
            {
                CampaignDefeatReason.MainCharacterDead => $"{name} nie żyje.",
                CampaignDefeatReason.EntirePartyDead => "Cała aktywna drużyna nie żyje.",
                CampaignDefeatReason.NoLivingRescuer => "Nie ma kto uratować powalonych.",
                _ => "Drużyna poniosła porażkę."
            };
            if (!hasSaves) messageText.text += "\nBrak poprawnego zapisu tej kampanii.";
        }
        private void LoadLatest()
        {
            if (!GameBootstrap.Instance.TryLoadLatestAfterDefeat(out var message)) messageText.text = message;
        }
        private void OpenOther() { panelRoot.SetActive(false); pauseMenu.OpenLoadAfterDefeat(); }
        private void ReturnFromList() { if (shown && GameBootstrap.Instance?.Session?.IsDefeated == true) Refresh(); }
        private void ReturnToMenu()
        {
            if (!GameBootstrap.Instance.TryReturnToMainMenu()) messageText.text = "Nie udało się otworzyć menu.";
        }
        private void OnDestroy()
        {
            if (saveLoadPanel != null) saveLoadPanel.Closed -= ReturnFromList;
            latestButton?.onClick.RemoveListener(LoadLatest);
            otherButton?.onClick.RemoveListener(OpenOther);
            menuButton?.onClick.RemoveListener(ReturnToMenu);
        }
    }
}

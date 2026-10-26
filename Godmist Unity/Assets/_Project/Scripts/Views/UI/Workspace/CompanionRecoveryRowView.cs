using System;
using TMPro;
using Godmist.Runtime.Characters;
using Godmist.Core.Enums;
using UnityEngine;
using UnityEngine.UI;
namespace Godmist.Views.UI.Workspace
{
    public sealed class CompanionRecoveryRowView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI title;
        [SerializeField] private TextMeshProUGUI price;
        [SerializeField] private Button recoverButton;
        [SerializeField] private Image portrait;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private CanvasGroup buttonGroup;
        [SerializeField] private UIWorkspaceStylePalette palette;
        [SerializeField] private RecoveryPriceTooltipView priceTooltip;
        public void Bind(CharacterInstance character, int cost, int gold, bool activeSlot, string breakdown, Action recover)
        {
            title.text = character.Definition?.DisplayName ?? character.CharacterId;
            levelText.text = $"Poziom {character.Level}";
            portrait.sprite = character.Definition?.UiPortraitSprite;
            portrait.enabled = portrait.sprite != null;
            var state = character.LifeState == CharacterLifeState.Dead ? "Nie żyje" : "Porzucony";
            statusText.text = state + (activeSlot ? " · Wróci do drużyny" : " · Trafi do rezerwy");
            var canAfford = gold >= cost;
            price.text = $"{cost:N0}";
            price.color = canAfford ? palette.GetTextColor(UIWorkspaceTextStyle.Body)
                : palette.GetAccentColor(UIWorkspaceAccentStyle.Negative);
            buttonGroup.alpha = 1f;
            priceTooltip.Bind(breakdown + (canAfford ? string.Empty : $"\nBrakuje {cost - gold:N0} złota."));
            recoverButton.interactable = canAfford;
            recoverButton.GetComponentInChildren<TMP_Text>(true).text = character.LifeState == CharacterLifeState.Dead
                ? "Wskrześ" : "Odzyskaj";
            recoverButton.onClick.RemoveAllListeners();
            recoverButton.onClick.AddListener(() => recover());
        }
    }
}

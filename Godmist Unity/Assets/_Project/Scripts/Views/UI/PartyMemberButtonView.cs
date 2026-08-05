using System;
using Godmist.Runtime.Characters;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI
{
    public class PartyMemberButtonView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TextMeshProUGUI characterNameText;
        [SerializeField] private TextMeshProUGUI characterInfoText;
        [SerializeField] private Color normalColor = new(0.11f, 0.15f, 0.2f, 1f);
        [SerializeField] private Color selectedColor = new(0.35f, 0.28f, 0.12f, 1f);

        private CharacterInstance character;
        private Action<CharacterInstance> selectionCallback;

        public void Bind(
            CharacterInstance nextCharacter,
            bool isSelected,
            Action<CharacterInstance> nextSelectionCallback)
        {
            character = nextCharacter;
            selectionCallback = nextSelectionCallback;

            characterNameText.text = character?.Definition != null
                ? character.Definition.DisplayName
                : "Nieprawidlowa postac";

            characterInfoText.text = character?.ClassData != null
                ? $"{character.ClassData.ClassName} | poziom {character.Level}"
                : "-";

            if (backgroundImage != null)
            {
                backgroundImage.color = isSelected ? selectedColor : normalColor;
            }

            button.onClick.RemoveAllListeners();
            button.interactable = character != null;
            button.onClick.AddListener(NotifySelected);
        }

        private void NotifySelected()
        {
            selectionCallback?.Invoke(character);
        }
    }
}
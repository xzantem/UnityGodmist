using System;
using Godmist.Runtime.Characters;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI.Workspace
{
    public sealed class CharacterInfoPartyPortraitView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image portrait;
        [SerializeField] private TextMeshProUGUI nameText;

        private CharacterInstance character;
        private Action<CharacterInstance> onClick;

        public void Bind(CharacterInstance nextCharacter, Action<CharacterInstance> nextOnClick)
        {
            character = nextCharacter;
            onClick = nextOnClick;
            if (portrait != null)
            {
                portrait.sprite = character?.Definition?.VisualSprite;
                portrait.enabled = portrait.sprite != null;
            }

            if (nameText != null)
            {
                nameText.text = character?.Definition?.DisplayName ?? character?.CharacterId ?? "-";
            }

            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.interactable = character != null;
            button.onClick.AddListener(() => onClick?.Invoke(character));
        }
    }
}

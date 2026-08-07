using System;
using Godmist.Data.NPC;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.NPC
{
    public class DialogueChoiceButtonView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TextMeshProUGUI label;

        private string choiceId;
        private Action<string> selectionCallback;

        private void Awake()
        {
            button.onClick.AddListener(HandleClicked);
        }

        public void Bind(
            DialogueChoiceData choice,
            Action<string> nextSelectionCallback)
        {
            if (choice == null)
            {
                return;
            }

            choiceId = choice.ChoiceId;
            selectionCallback = nextSelectionCallback;

            if (label != null)
            {
                label.text = choice.DisplayText;
            }
        }

        private void HandleClicked()
        {
            selectionCallback?.Invoke(choiceId);
        }
    }
}
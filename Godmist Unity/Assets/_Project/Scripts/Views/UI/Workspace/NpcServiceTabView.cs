using System;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI.Workspace
{
    public enum NpcServiceKind
    {
        Trade,
        EquipmentCrafting,
        MaterialCrafting,
        EquipmentUpgrade,
        EquipmentReforge,
        EquipmentInfusion
    }

    public sealed class NpcServiceTabView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image icon;
        [SerializeField] private Image selectedBorder;

        private Action selectAction;

        private void Awake()
        {
            if (button != null) button.onClick.AddListener(Select);
        }

        private void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(Select);
        }

        public void Bind(Sprite nextIcon, bool isSelected, Action nextSelectAction)
        {
            if (icon != null) icon.sprite = nextIcon;
            if (selectedBorder != null) selectedBorder.enabled = isSelected;
            selectAction = nextSelectAction;
        }


        private void Select()
        {
            selectAction?.Invoke();
        }
    }
}

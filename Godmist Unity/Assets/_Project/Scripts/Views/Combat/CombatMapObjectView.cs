using Godmist.Runtime.Combat;
using UnityEngine;

namespace Godmist.Views.Combat
{
    public class CombatMapObjectView : MonoBehaviour
    {
        [SerializeField] private CombatSandboxBootstrap sandboxBootstrap;
        [SerializeField] private string objectId;
        [SerializeField] private SpriteRenderer targetRenderer;

        [Header("Colors")]
        [SerializeField] private Color activeColor = new(0.95f, 0.68f, 0.2f, 1f);
        [SerializeField] private Color activatedLeverColor = new(0.35f, 0.8f, 0.45f, 1f);
        [SerializeField] private Color inactiveColor = new(0.35f, 0.4f, 0.45f, 0.35f);
        [SerializeField] private Color closedDoorColor = new(0.45f, 0.23f, 0.1f, 1f);
        [SerializeField] private Color openDoorColor = new(0.3f, 0.7f, 0.45f, 0.35f);

        private void OnEnable()
        {
            if (sandboxBootstrap != null)
            {
                sandboxBootstrap.CombatMapObjectChanged += HandleCombatMapObjectChanged;
            }
        }

        private void Start()
        {
            Refresh();
        }

        private void OnDisable()
        {
            if (sandboxBootstrap != null)
            {
                sandboxBootstrap.CombatMapObjectChanged -= HandleCombatMapObjectChanged;
            }
        }

        private void HandleCombatMapObjectChanged(CombatMapObject changedObject)
        {
            if (changedObject == null || changedObject.ObjectId != objectId) return;

            Refresh();
        }

        private void Refresh()
        {
            if (sandboxBootstrap == null || targetRenderer == null) return;

            var mapObject = sandboxBootstrap.GetCombatMapObject(objectId);

            if (mapObject == null) return;

            if (mapObject is DoorCombatMapObject door)
            {
                targetRenderer.color = door.IsOpen ? openDoorColor : closedDoorColor;

                return;
            }

            if (mapObject is LeverCombatMapObject lever)
            {
                targetRenderer.color = lever.IsActivated ? activatedLeverColor : activeColor;
            }
        }
    }
}
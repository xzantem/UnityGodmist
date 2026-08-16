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
        [SerializeField] private Color inspectColor = new(0.5f, 0.86f, 1f, 1f);
        private bool isHovered;

        private void Awake()
        {
            // Scene map objects previously had no collider, therefore Unity never sent
            // OnMouseEnter/Exit and their inspect state was unreachable.  This collider is
            // presentation-only: combat targeting still uses the battle grid.
            if (targetRenderer == null || GetComponent<Collider2D>() != null) return;
            if (targetRenderer.sprite == null) return;

            var hoverCollider = gameObject.AddComponent<BoxCollider2D>();
            hoverCollider.isTrigger = true;
            hoverCollider.offset = targetRenderer.sprite.bounds.center;
            hoverCollider.size = targetRenderer.sprite.bounds.size;
        }

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

            targetRenderer.enabled = mapObject != null;

            if (mapObject == null) return;

            if (mapObject is DoorCombatMapObject door)
            {
                targetRenderer.color = door.IsOpen ? openDoorColor : closedDoorColor;
                ApplyInspectTint();
                return;
            }

            if (mapObject is LeverCombatMapObject lever)
            {
                targetRenderer.color = lever.IsActivated ? activatedLeverColor : activeColor;
                ApplyInspectTint();
                return;
            }

            targetRenderer.color = mapObject.BlocksMovement ? inactiveColor : activeColor;
            ApplyInspectTint();
        }

        private void OnMouseEnter()
        {
            isHovered = true;
            Refresh();
        }

        private void OnMouseExit()
        {
            isHovered = false;
            Refresh();
        }

        private void ApplyInspectTint()
        {
            if (isHovered) targetRenderer.color = Color.Lerp(targetRenderer.color, inspectColor, .45f);
        }
    }
}

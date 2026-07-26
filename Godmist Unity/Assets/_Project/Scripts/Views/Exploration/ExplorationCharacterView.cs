using Godmist.Runtime.Characters;
using TMPro;
using UnityEngine;

namespace Godmist.Views.Exploration
{
    public class ExplorationCharacterView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer bodyRenderer;
        [SerializeField] private TextMeshPro nameLabel;
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private Collider2D movementCollider;
        [SerializeField] private Color leaderColor = new(0.92f, 0.68f, 0.27f, 1f);
        [SerializeField] private Color followerColor = new(0.35f, 0.75f, 1f, 1f);

        public string CharacterId { get; private set; } = string.Empty;
        public Vector2 Position => transform.position;

        public void Bind(CharacterInstance character)
        {
            if (character == null) return;

            CharacterId = character.CharacterId;

            if (nameLabel != null)
            {
                nameLabel.text = character.Definition != null ? character.Definition.DisplayName : CharacterId;
            }
        }

        public void SetIsActiveLeader(bool isActiveLeader)
        {
            if (bodyRenderer != null)
            {
                bodyRenderer.color = isActiveLeader ? leaderColor : followerColor;
            }

            if (body != null) body.simulated = isActiveLeader;
            if (movementCollider != null) movementCollider.enabled = isActiveLeader;
        }

        public void Move(Vector2 offset)
        {
            if (offset.sqrMagnitude < 0.0001f) return;


            if (body != null && body.simulated)
            {
                body.MovePosition(body.position + offset);
                return;
            }

            transform.position += (Vector3)offset;
        }

        public void MoveTowards(Vector2 targetPosition, float maximumDistance, float stopDistance)
        {
            var offset = targetPosition - Position;

            if (offset.sqrMagnitude <= stopDistance * stopDistance) return;

            var destination = targetPosition - offset.normalized * stopDistance;
            var nextPosition = Vector2.MoveTowards(Position, destination, maximumDistance);

            Move(nextPosition - Position);
        }

        private void Awake()
        {
            if (bodyRenderer == null || nameLabel == null) return;

            var labelRenderer = nameLabel.GetComponent<MeshRenderer>();

            if (labelRenderer == null) return;

            labelRenderer.sortingLayerID = bodyRenderer.sortingLayerID;
            labelRenderer.sortingOrder = bodyRenderer.sortingOrder + 1;
        }
    }
}
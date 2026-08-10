using Godmist.Runtime.Characters;
using System.Collections.Generic;
using Godmist.Data.Characters;
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

        private IReadOnlyList<Sprite> idleAnimationFrames;
        private IReadOnlyList<Sprite> runAnimationFrames;
        private IReadOnlyList<Sprite> activeAnimationFrames;
        private float nextAnimationFrameTime;
        private float secondsPerAnimationFrame;
        private int animationFrameIndex;
        private float lastMovementTime = float.NegativeInfinity;

        public string CharacterId { get; private set; } = string.Empty;
        public Vector2 Position => transform.position;

        public void Bind(CharacterInstance character)
        {
            if (character == null) return;

            CharacterId = character.CharacterId;

            ConfigureVisual(character.Definition);

            if (nameLabel != null)
            {
                nameLabel.text = character.Definition != null ? character.Definition.DisplayName : CharacterId;
            }
        }

        public void Bind(CharacterDefinition definition)
        {
            if (definition == null) return;

            CharacterId = definition.Id;
            ConfigureVisual(definition);

            if (nameLabel != null)
            {
                nameLabel.text = definition.DisplayName;
            }
        }

        public void SetTint(Color tint)
        {
            if (bodyRenderer != null)
            {
                bodyRenderer.color = tint;
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

            SetFacingDirection(offset);
            PlayRunAnimation();
            lastMovementTime = Time.time;

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

        private void Update()
        {
            if (Time.time - lastMovementTime > 0.08f)
            {
                PlayIdleAnimation();
            }

            if (bodyRenderer == null || activeAnimationFrames == null || activeAnimationFrames.Count < 2) return;
            if (Time.time < nextAnimationFrameTime) return;

            animationFrameIndex = (animationFrameIndex + 1) % activeAnimationFrames.Count;
            bodyRenderer.sprite = activeAnimationFrames[animationFrameIndex];
            nextAnimationFrameTime = Time.time + secondsPerAnimationFrame;
        }

        private void ConfigureVisual(CharacterDefinition definition)
        {
            if (bodyRenderer == null || definition == null) return;

            idleAnimationFrames = definition.IdleAnimationFrames;
            runAnimationFrames = definition.RunAnimationFrames;

            if (idleAnimationFrames == null || idleAnimationFrames.Count == 0)
            {
                idleAnimationFrames = definition.VisualSprite != null
                    ? new[] { definition.VisualSprite }
                    : null;
            }

            PlayAnimation(idleAnimationFrames, definition.IdleAnimationFramesPerSecond);
        }

        private void PlayIdleAnimation()
        {
            if (activeAnimationFrames == idleAnimationFrames) return;

            PlayAnimation(idleAnimationFrames, 4f);
        }

        private void PlayRunAnimation()
        {
            if (runAnimationFrames == null || runAnimationFrames.Count == 0) return;
            if (activeAnimationFrames == runAnimationFrames) return;

            PlayAnimation(runAnimationFrames, 8f);
        }

        private void PlayAnimation(IReadOnlyList<Sprite> frames, float framesPerSecond)
        {
            if (bodyRenderer == null || frames == null || frames.Count == 0) return;

            activeAnimationFrames = frames;
            animationFrameIndex = 0;
            bodyRenderer.sprite = activeAnimationFrames[animationFrameIndex];
            secondsPerAnimationFrame = 1f / Mathf.Max(1f, framesPerSecond);
            nextAnimationFrameTime = Time.time + secondsPerAnimationFrame;
        }

        private void SetFacingDirection(Vector2 movementDirection)
        {
            if (bodyRenderer == null || Mathf.Abs(movementDirection.x) < 0.0001f) return;

            bodyRenderer.transform.localRotation = movementDirection.x > 0f
                ? Quaternion.Euler(0f, 0f, 0f)
                : Quaternion.Euler(180f, 0f, 180f);
        }

        public void SetPosition(Vector2 position)
        {
            lastMovementTime = float.NegativeInfinity;
            PlayIdleAnimation();
            transform.position = position;

            if (body != null)
            {
                body.position = position;
            }
        }
    }
}

using Godmist.Runtime.Combat;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Godmist.Views.Characters
{
    public class CharacterView : MonoBehaviour
    {
        [Header("Visual References")]
        [SerializeField] private SpriteRenderer bodyRenderer;
        [SerializeField] private TextMeshPro nameLabel;

        [Header("Colors")]
        [SerializeField] private Color playerSideColor = new(0.35f, 0.75f, 1f, 1f);
        [SerializeField] private Color enemySideColor = new(1f, 0.4f, 0.4f, 1f);

        [Header("Animation")]
        [SerializeField] private float movementSpeed = 6f;
        [SerializeField] private float hitReactionDuration = 0.15f;

        private IReadOnlyList<Sprite> idleAnimationFrames;
        private IReadOnlyList<Sprite> runAnimationFrames;
        private IReadOnlyList<Sprite> activeAnimationFrames;
        private Sprite hitSprite;
        private float nextAnimationFrameTime;
        private float secondsPerAnimationFrame;
        private float hitReactionEndTime;
        private int animationFrameIndex;
        private int lastKnownHealth;
        private Vector3 targetWorldPosition;
        private bool hasTargetWorldPosition;

        public BattleUnit BoundUnit { get; private set; }

        public void Bind(BattleUnit unit)
        {
            BoundUnit = unit;
            ConfigureVisual();
            lastKnownHealth = BoundUnit?.Character?.CurrentHealth ?? 0;
            Refresh();
        }

        public void Refresh()
        {
            if (BoundUnit == null)
            {
                return;
            }

            if (nameLabel != null)
            {
                nameLabel.text = BoundUnit.Character.Definition.DisplayName;
            }

            if (bodyRenderer != null)
            {
                bodyRenderer.color = BoundUnit.IsPlayerSide ? playerSideColor : enemySideColor;
            }

            var currentHealth = BoundUnit.Character.CurrentHealth;

            if (currentHealth < lastKnownHealth)
            {
                PlayHitReaction();
            }

            lastKnownHealth = currentHealth;
        }

        public void SetWorldPosition(Vector3 position, bool snapToPosition = false)
        {
            targetWorldPosition = position;

            if (snapToPosition)
            {
                transform.position = position;
                hasTargetWorldPosition = false;
                PlayIdleAnimation();
                return;
            }

            hasTargetWorldPosition = Vector3.SqrMagnitude(transform.position - targetWorldPosition) > 0.0001f;

            if (hasTargetWorldPosition)
            {
                SetFacingDirection(targetWorldPosition - transform.position);
                PlayRunAnimation();
            }
        }

        private void Update()
        {
            UpdateMovement();
            UpdateAnimation();
        }

        private void UpdateMovement()
        {
            if (!hasTargetWorldPosition) return;

            transform.position = Vector3.MoveTowards(
                transform.position,
                targetWorldPosition,
                movementSpeed * Time.deltaTime);

            if (Vector3.SqrMagnitude(transform.position - targetWorldPosition) > 0.0001f) return;

            transform.position = targetWorldPosition;
            hasTargetWorldPosition = false;
            PlayIdleAnimation();
        }

        private void UpdateAnimation()
        {
            if (bodyRenderer == null) return;

            if (Time.time < hitReactionEndTime)
            {
                bodyRenderer.sprite = hitSprite;
                return;
            }

            if (activeAnimationFrames == null || activeAnimationFrames.Count < 2) return;
            if (Time.time < nextAnimationFrameTime) return;

            animationFrameIndex = (animationFrameIndex + 1) % activeAnimationFrames.Count;
            bodyRenderer.sprite = activeAnimationFrames[animationFrameIndex];
            nextAnimationFrameTime = Time.time + secondsPerAnimationFrame;
        }

        private void ConfigureVisual()
        {
            var definition = BoundUnit?.Character?.Definition;

            if (bodyRenderer == null || definition == null) return;

            idleAnimationFrames = definition.IdleAnimationFrames;
            runAnimationFrames = definition.RunAnimationFrames;
            hitSprite = definition.HitSprite;

            if (idleAnimationFrames == null || idleAnimationFrames.Count == 0)
            {
                idleAnimationFrames = definition.VisualSprite != null
                    ? new[] { definition.VisualSprite }
                    : null;
            }

            PlayIdleAnimation();
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

        private void PlayHitReaction()
        {
            if (hitSprite == null) return;

            hitReactionEndTime = Time.time + hitReactionDuration;
            bodyRenderer.sprite = hitSprite;
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

        private void SetFacingDirection(Vector3 movementDirection)
        {
            if (bodyRenderer == null || Mathf.Abs(movementDirection.x) < 0.0001f) return;

            bodyRenderer.transform.localRotation = movementDirection.x > 0f
                ? Quaternion.Euler(0f, 0f, 0f)
                : Quaternion.Euler(180f, 0f, 180f);
        }
    }
}

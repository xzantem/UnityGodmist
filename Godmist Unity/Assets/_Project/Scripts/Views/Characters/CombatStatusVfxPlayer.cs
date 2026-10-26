using System;
using Godmist.Core.Enums;
using Godmist.Core.Constants;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using UnityEngine;

namespace Godmist.Views.Characters
{
    /// <summary>Two status motifs plus an optional hard-status silhouette wash; no saved state.</summary>
    public sealed class CombatStatusVfxPlayer : IDisposable
    {
        private readonly SpriteRenderer body;
        private readonly SpriteRenderer[] renderers = new SpriteRenderer[2];
        private static readonly string[] Priority = { StatusEffectIds.Freeze, StatusEffectIds.Paralysis,
            StatusEffectIds.Stun, StatusEffectIds.Immobilize, StatusEffectIds.Burn, StatusEffectIds.Bleed, StatusEffectIds.Poison };
        private readonly string[] selected = new string[2];
        private readonly float[] started = new float[2];
        private CharacterInstance character;
        private Sprite lockedSprite;
        private SpriteRenderer overlayTint;
        private Godmist.Data.Characters.CharacterDefinition lockedDefinition;
        public CombatStatusVfxPlayer(SpriteRenderer body) => this.body = body;

        public static bool IsMotionLocked(CharacterInstance owner) => owner?.LifeState == CharacterLifeState.Alive
            && (Has(owner, StatusEffectIds.Freeze) || Has(owner, StatusEffectIds.Paralysis));

        public void Tick(CharacterInstance next, BattleUnit unit = null, bool allowPoseLock = true)
        {
            if (character != next) { Clear(); character = next; }
            if (body != null && allowPoseLock && IsMotionLocked(next))
            {
                if (lockedSprite == null || lockedDefinition != next.Definition)
                { lockedSprite = body.sprite; lockedDefinition = next.Definition; }
                if (lockedSprite != null) body.sprite = lockedSprite;
            }
            else { lockedSprite = null; lockedDefinition = null; }
            var visible = body != null && body.enabled && !body.forceRenderingOff && body.color.a > .05f
                && next?.LifeState == CharacterLifeState.Alive && !StatusEffectSystem.IsInvisible(unit)
                && !Has(next, StatusEffectIds.Invisibility);
            UpdateOverlayTint(next, visible);
            var index = 0;
            var hasOverlay = false;
            if (visible)
                foreach (var id in Priority)
                {
                    if (!Has(next, id)) continue;
                    var overlay = id is StatusEffectIds.Freeze or StatusEffectIds.Paralysis;
                    if (overlay && hasOverlay) continue;
                    UpdateEffect(index++, true, id);
                    hasOverlay |= overlay;
                    if (index == renderers.Length) break;
                }
            // Marks are owned by their existing combat counters. Reuse the same
            // two-motif budget and leave hard control / damage statuses first.
            if (visible && index < renderers.Length && unit?.WarriorState.OrderTurns > 0)
                UpdateEffect(index++, true, "destruction-mark");
            if (visible && index < renderers.Length && unit?.ScoutState.WeakPointTurns > 0)
                UpdateEffect(index++, true, "weakpoint-mark");
            if (visible && unit != null)
            {
                if (index < renderers.Length && CombatBossVfxFeedback.HasRoyalCommand(unit))
                    UpdateEffect(index++, true, "royal-command");
                if (index < renderers.Length && unit.SkeletonSlayerState?.DefenseTurns > 0)
                    UpdateEffect(index++, true, "slayer-ward");
                if (index < renderers.Length && unit.SkeletonSlayerState?.HammerTurns > 0)
                    UpdateEffect(index++, true, "hammer-charge");
                if (index < renderers.Length && unit.ActiveToggleSkillIds?.Contains(WarriorCombatSystem.DeathWish) == true)
                    UpdateEffect(index++, true, "death-wish");
                if (index < renderers.Length && unit.ActiveToggleSkillIds?.Contains("skill.mage.offering") == true
                    && CombatDamageSystem.HasUniqueBuff(unit, UniqueBuffType.MageOffering))
                    UpdateEffect(index++, true, "offering");
                if (index < renderers.Length && unit.ActiveToggleSkillIds?.Contains("skill.mage.tiring") == true)
                    UpdateEffect(index++, true, "fatigue");
            }
            while (index < renderers.Length) UpdateEffect(index++, false, null);
        }

        private static bool Has(CharacterInstance owner, string id)
            => PassiveEffectLifecycleSystem.HasActiveEffect(owner, id);

        private void UpdateOverlayTint(CharacterInstance owner, bool visible)
        {
            var ice = Has(owner, StatusEffectIds.Freeze);
            if (!visible || body.sprite == null || !ice && !Has(owner, StatusEffectIds.Paralysis))
            { if (overlayTint != null) overlayTint.enabled = false; return; }
            if (overlayTint == null)
            {
                var child = new GameObject("Hard Status Tint"); child.transform.SetParent(body.transform, false);
                overlayTint = child.AddComponent<SpriteRenderer>();
            }
            // Reuse the exact actor silhouette instead of replacing the authored body or material.
            overlayTint.sprite = body.sprite; overlayTint.flipX = body.flipX; overlayTint.flipY = body.flipY;
            overlayTint.sortingLayerID = body.sortingLayerID; overlayTint.sortingOrder = body.sortingOrder + 3;
            overlayTint.color = ice ? new Color(.55f, .75f, .95f, .5f * body.color.a)
                : new Color(.55f, .57f, .60f, .7f * body.color.a);
            overlayTint.forceRenderingOff = false; overlayTint.enabled = true;
        }

        private void UpdateEffect(int index, bool visible, string id)
        {
            if (!visible || !CombatTargetVfxResolver.TryResolve(id, out var clip))
            { if (renderers[index] != null) renderers[index].enabled = false; return; }
            if (renderers[index] == null)
            {
                var child = new GameObject("Status VFX " + id);
                child.transform.SetParent(body.transform, false);
                renderers[index] = child.AddComponent<SpriteRenderer>();
            }
            var renderer = renderers[index];
            if (selected[index] != id) { selected[index] = id; started[index] = Time.time; }
            renderer.gameObject.name = "Status VFX " + id;
            var bounds = CombatTargetVfxResolver.ActorVisibleBounds(body.sprite);
            renderer.transform.localScale = clip.FitBody && clip.ContentHeight > 0
                ? Vector3.one * Mathf.Max(.1f, bounds.size.y / clip.ContentHeight) : Vector3.one;
            renderer.transform.localPosition = clip.AboveHead
                ? new Vector3(bounds.center.x, bounds.max.y + clip.ContentHeight * .5f + 2f / 32f, 0)
                : clip.FitBody ? new Vector3(bounds.center.x, bounds.min.y, 0) + (Vector3)clip.Offset
                : (Vector3)clip.Offset;
            var elapsed = Time.time - started[index];
            var phase = clip.RepeatSeconds > 0 ? elapsed % Mathf.Max(clip.Duration, clip.RepeatSeconds) : elapsed;
            renderer.sprite = clip.FrameAt(phase, true);
            renderer.sortingLayerID = body.sortingLayerID;
            renderer.sortingOrder = body.sortingOrder + 4 + index;
            renderer.forceRenderingOff = false;
            renderer.color = new Color(1, 1, 1, body.color.a * clip.Opacity);
            renderer.enabled = clip.RepeatSeconds <= 0 || phase < clip.Duration;
        }

        public void Clear()
        {
            lockedSprite = null; lockedDefinition = null;
            if (overlayTint != null) overlayTint.enabled = false;
            for (var i = 0; i < renderers.Length; i++)
            { selected[i] = null; if (renderers[i] != null) renderers[i].enabled = false; }
        }
        public void Dispose()
        {
            Clear();
            foreach (var renderer in renderers)
                if (renderer != null)
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(renderer.gameObject);
                    else UnityEngine.Object.DestroyImmediate(renderer.gameObject);
                }
            if (overlayTint != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(overlayTint.gameObject);
                else UnityEngine.Object.DestroyImmediate(overlayTint.gameObject);
            }
        }
    }
}

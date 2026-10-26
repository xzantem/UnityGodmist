using System;
using System.Collections.Generic;
using UnityEngine;

namespace Godmist.Views.Characters
{
    /// <summary>Temporary override on the existing lit/outlined body, plus separate registered VFX.</summary>
    public sealed class CombatPosePlayer : IDisposable
    {
        private readonly SpriteRenderer body;
        private readonly List<SpriteRenderer> effects = new();
        private CombatPose pose;
        private Sprite previous;
        private float ends;
        public bool IsPlaying => pose != null && Time.time < ends;
        public bool IsHitReaction => IsPlaying && pose.Action == "hit";

        public CombatPosePlayer(SpriteRenderer body) => this.body = body;

        public void Play(CombatPose next, float duration)
        {
            if (body == null || next?.Actor == null) return;
            if (pose == null) previous = body.sprite;
            pose = next;
            ends = Time.time + Mathf.Max(.01f, duration);
            while (effects.Count < next.Effects.Length)
            {
                var child = new GameObject("Combat Pose Effect");
                child.transform.SetParent(body.transform, false);
                effects.Add(child.AddComponent<SpriteRenderer>());
            }
            Tick();
        }

        public bool Tick() => Tick(Time.time);

        public bool Tick(float now)
        {
            if (pose == null || body == null) return false;
            if (now >= ends) { Clear(); return false; }
            body.sprite = pose.Actor;
            for (var i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                // Authored execution effects are static companions of the pose,
                // not shorter bursts. Status and transformation owners are separate.
                effect.enabled = i < pose.Effects.Length && body.enabled && !body.forceRenderingOff;
                effect.forceRenderingOff = body.forceRenderingOff;
                if (i >= pose.Effects.Length) continue;
                effect.sprite = pose.Effects[i];
                effect.transform.localPosition = pose.EffectOffsets[i];
                effect.sortingLayerID = body.sortingLayerID;
                effect.sortingOrder = body.sortingOrder + pose.EffectOrders[i];
            }
            return true;
        }

        public void Clear()
        {
            if (pose != null && body != null) body.sprite = previous;
            pose = null;
            foreach (var effect in effects) if (effect != null) effect.enabled = false;
        }

        public void Dispose()
        {
            Clear();
            foreach (var effect in effects)
            {
                if (effect == null) continue;
                if (Application.isPlaying) UnityEngine.Object.Destroy(effect.gameObject);
                else UnityEngine.Object.DestroyImmediate(effect.gameObject);
            }
            effects.Clear();
        }
    }
}

using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using UnityEngine;

namespace Godmist.Views.Characters
{
    /// <summary>A short target-head layer; never replaces the target's animation or reaction.</summary>
    public sealed class CombatTargetEffectPlayer : MonoBehaviour
    {
        private SpriteRenderer body;
        private BattleUnit target;
        private SpriteRenderer[] layers;
        private float ends;
        private Vector2[] offsets;

        public static CombatTargetEffectPlayer Play(SpriteRenderer body, BattleUnit target, Sprite[] sprites, float duration, Vector2[] offsets = null)
        {
            var root = new GameObject("Target Skill Effect");
            root.transform.SetParent(body.transform, false);
            var player = root.AddComponent<CombatTargetEffectPlayer>();
            player.body = body;
            player.target = target;
            player.offsets = offsets;
            player.ends = Time.time + Mathf.Max(.01f, duration);
            player.layers = new SpriteRenderer[sprites.Length];
            for (var i = 0; i < sprites.Length; i++)
            {
                var child = new GameObject("Effect Layer");
                child.transform.SetParent(root.transform, false);
                var layer = child.AddComponent<SpriteRenderer>();
                layer.sprite = sprites[i];
                player.layers[i] = layer;
            }
            player.LateUpdate();
            return player;
        }

        private void LateUpdate()
        {
            if (body == null || Time.time >= ends || target?.Character?.LifeState != CharacterLifeState.Alive)
            {
                Clear();
                return;
            }
            var bounds = body.bounds;
            transform.position = new Vector3(bounds.center.x, bounds.max.y, body.transform.position.z);
            for (var i = 0; i < layers.Length; i++)
            {
                layers[i].enabled = body.enabled && body.gameObject.activeInHierarchy && !body.forceRenderingOff;
                layers[i].forceRenderingOff = body.forceRenderingOff;
                layers[i].sortingLayerID = body.sortingLayerID;
                layers[i].sortingOrder = body.sortingOrder + i + 1;
                // Authored target-head registration is measured from the target's
                // grounded pivot. Legacy effects retain the automatic bounds placement.
                if(offsets!=null && i<offsets.Length && offsets[i]!=Vector2.zero)
                    layers[i].transform.position=body.transform.position+(Vector3)offsets[i];
            }
        }

        public void Clear()
        {
            foreach (var layer in layers ?? System.Array.Empty<SpriteRenderer>()) if (layer != null) layer.enabled = false;
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
        }

        public static void ClearAttached(SpriteRenderer body)
        {
            if (body == null) return;
            foreach (var effect in body.GetComponentsInChildren<CombatTargetEffectPlayer>(true)) effect.Clear();
        }
    }
}

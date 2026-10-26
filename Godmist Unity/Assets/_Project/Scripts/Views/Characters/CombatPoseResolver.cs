using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using UnityEngine;

namespace Godmist.Views.Characters
{
    public sealed class CombatPose
    {
        public readonly Sprite Actor;
        public readonly Sprite[] Effects;
        public readonly int[] EffectOrders;
        public readonly string Action;
        public readonly string Direction;
        public readonly Sprite[] TargetHeadEffects;
        public readonly Vector2[] EffectOffsets;
        public readonly float[] EffectDurations;
        public readonly float TargetHeadDuration;
        public readonly Vector2[] TargetHeadOffsets;

        internal CombatPose(Sprite actor, Sprite[] effects, int[] orders, string action, string direction, Sprite[] targetHeadEffects = null, Vector2[] effectOffsets = null, float[] effectDurations = null, float targetHeadDuration = 0, Vector2[] targetHeadOffsets = null)
        { Actor = actor; Effects = effects; EffectOrders = orders; Action = action; Direction = direction; TargetHeadEffects = targetHeadEffects ?? Array.Empty<Sprite>(); EffectOffsets = effectOffsets ?? new Vector2[effects.Length]; EffectDurations = effectDurations ?? new float[effects.Length]; TargetHeadDuration = targetHeadDuration; TargetHeadOffsets = targetHeadOffsets ?? new Vector2[TargetHeadEffects.Length]; }
    }

    /// <summary>Approved pack catalog is shared by sandbox and borrowed exploration renderers.</summary>
    public static class CombatPoseResolver
    {
        private const string Root = "Godmist/CombatPoses/";
        private static readonly Dictionary<string, Pack> Packs = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, CombatPose> Poses = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, Sprite> Sprites = new(StringComparer.Ordinal);
        private static readonly Dictionary<Sprite, Bounds> VisibleActorBounds = new();
        public static bool TryGetVisibleBounds(Sprite sprite, out Bounds bounds)
        {
            if (sprite != null) return VisibleActorBounds.TryGetValue(sprite,out bounds);
            bounds=default; return false;
        }
        private static bool loaded;

        #pragma warning disable 0649
        [Serializable] private sealed class Catalog { public Pack[] packs; }
        [Serializable] private sealed class Pack
        {
            public string unit;
            public int actorSize;
            public int pivotBottomPixels;
            public Skill[] skills;
            public Frame[] frames;
        }
        [Serializable] private sealed class Skill
        { public string id; public string action; public string meleeAction; public bool frontOnly; }
        [Serializable] private sealed class Frame
        { public string action; public string direction; public Layer[] layers; }
        [Serializable] private sealed class Layer
        { public string resource; public bool actor; public int canvasSize; public bool customPivot; public float pivotX, pivotY; public bool targetHead; public float offsetX, offsetY; public int[] opaquePixelRect; public int durationMs; }
        #pragma warning restore 0649

        public static bool TryResolveSkill(string characterId, string skillId, Vector2 facing,
            AttackType attackType, bool knifeRecovery, out CombatPose pose)
        {
            pose = null;
            if (!TryGetPack(characterId, out var pack)) return false;
            foreach (var skill in pack.skills ?? Array.Empty<Skill>())
            {
                if (skill.id != skillId) continue;
                var action = attackType == AttackType.Melee && !string.IsNullOrEmpty(skill.meleeAction)
                    ? skill.meleeAction : skill.action;
                if (knifeRecovery && action == "throw") action = "extract";
                return TryResolve(pack, action, skill.frontOnly ? "s" : CharacterAnimationResolver.AssetDirectionFor(characterId, facing), out pose);
            }
            return false;
        }

        public static bool TryResolveAction(string characterId, string action, Vector2 facing, out CombatPose pose)
        {
            pose = null;
            return TryGetPack(characterId, out var pack)
                && TryResolve(pack, action, CharacterAnimationResolver.AssetDirectionFor(characterId, facing), out pose);
        }

        public static bool TryResolveDowned(string characterId, out CombatPose pose)
        {
            pose = null;
            return TryGetPack(characterId, out var pack)
                && TryResolve(pack, "front-knockdown", "s", out pose);
        }

        public static bool TryResolveHelpUp(string characterId, Vector2 facing, out CombatPose pose)
        {
            pose = null;
            return TryGetPack(characterId, out var pack)
                && TryResolve(pack, "help-up", CharacterAnimationResolver.AssetDirectionFor(characterId, facing), out pose);
        }

        public static Vector2 Facing(CombatSkillPresentation presentation, Vector2 fallback)
        {
            if (!presentation.Aim.HasValue) return fallback;
            var aim = presentation.Aim.Value;
            var direction = new Vector2(aim.X - presentation.Origin.X, aim.Y - presentation.Origin.Y);
            return direction.sqrMagnitude > .000001f ? direction.normalized : fallback;
        }

        private static bool TryGetPack(string characterId, out Pack pack)
        {
            pack = null;
            if (!loaded)
            {
                loaded = true;
                LoadCatalog(Root + "catalog");
                LoadCatalog(Root + "hero-special-catalog");
                LoadCatalog(Root + "skeleton-slayer-catalog");
                LoadCatalog(Root + "bandit-leader-catalog");
            }
            return CharacterAnimationResolver.TryGetCharacterUnit(characterId, out var unit)
                && Packs.TryGetValue(unit, out pack);
        }

        private static void LoadCatalog(string resourcePath)
        {
            var text = Resources.Load<TextAsset>(resourcePath);
            var catalog = text == null ? null : JsonUtility.FromJson<Catalog>(text.text);
            if (catalog?.packs == null) return;
            foreach (var item in catalog.packs)
            {
                if (item == null || string.IsNullOrEmpty(item.unit)) continue;
                if (!Packs.TryGetValue(item.unit, out var existing))
                {
                    Packs[item.unit] = item;
                    continue;
                }

                // Special catalogs extend the base catalog. Keeping the merge here
                // means a hero's existing skill pose definitions stay available.
                existing.frames = Merge(existing.frames, item.frames);
                if (item.skills != null && item.skills.Length > 0)
                    existing.skills = Merge(existing.skills, item.skills);
            }
        }

        private static bool TryResolve(Pack pack, string action, string direction, out CombatPose pose)
        {
            var key = pack.unit + ":" + action + ":" + direction;
            if (Poses.TryGetValue(key, out pose) && IsValid(pose)) return true;
            Poses.Remove(key);
            foreach (var frame in pack.frames ?? Array.Empty<Frame>())
            {
                if (frame.action != action || frame.direction != direction) continue;
                var actorIndex = Array.FindIndex(frame.layers, layer => layer.actor);
                if (actorIndex < 0) return false;
                Sprite actor = null;
                var effects = new List<Sprite>(); var orders = new List<int>();
                var targetHeadEffects = new List<Sprite>();
                var targetHeadOffsets = new List<Vector2>();
                var offsets = new List<Vector2>();
                var durations = new List<float>(); var targetHeadDuration = 0f;
                for (var i = 0; i < frame.layers.Length; i++)
                {
                    var layer = frame.layers[i];
                    if (!Sprites.TryGetValue(layer.resource, out var sprite) || sprite == null || sprite.texture == null)
                    {
                        // Static dictionaries can outlive Unity-created sprites when
                        // Play Mode runs without a domain reload.
                        if (!ReferenceEquals(sprite, null)) VisibleActorBounds.Remove(sprite);
                        var texture = Resources.Load<Texture2D>(Root + layer.resource);
                        if (texture == null) return false;
                        texture.filterMode = FilterMode.Point;
                        texture.wrapMode = TextureWrapMode.Clamp;
                        texture.anisoLevel = 0;
                        var bottom = pack.pivotBottomPixels + (layer.canvasSize - pack.actorSize) / 2f;
                        sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                            layer.customPivot ? new Vector2(layer.pivotX, layer.pivotY) : new Vector2(.5f, bottom / texture.height), 32f, 0, SpriteMeshType.FullRect);
                        sprite.name = layer.resource;
                        // Optional bottom-left alpha bounds baked from the approved PNG.
                        // Preserve the canvas, pivot and pixel scale; no runtime readback.
                        var rect = layer.opaquePixelRect;
                        if (layer.actor && rect?.Length == 4 && rect[0]>=0 && rect[1]>=0
                            && rect[2]>0 && rect[3]>0 && rect[0]+rect[2]<=texture.width && rect[1]+rect[3]<=texture.height)
                        {
                            var min = (new Vector2(rect[0],rect[1])-sprite.pivot)/sprite.pixelsPerUnit;
                            var size = new Vector2(rect[2],rect[3])/sprite.pixelsPerUnit;
                            VisibleActorBounds[sprite]=new Bounds(min+size*.5f,size);
                        }
                        Sprites[layer.resource] = sprite;
                    }
                    if (layer.actor) actor = sprite;
                    else if (layer.targetHead)
                    { targetHeadEffects.Add(sprite); targetHeadOffsets.Add(new Vector2(layer.offsetX,layer.offsetY)); if(layer.durationMs>0)targetHeadDuration=targetHeadDuration>0?Mathf.Min(targetHeadDuration,layer.durationMs/1000f):layer.durationMs/1000f; }
                    else { effects.Add(sprite); orders.Add(i - actorIndex); offsets.Add(new Vector2(layer.offsetX, layer.offsetY)); durations.Add(layer.durationMs/1000f); }
                }
                pose = new CombatPose(actor, effects.ToArray(), orders.ToArray(), action, direction, targetHeadEffects.ToArray(), offsets.ToArray(), durations.ToArray(), targetHeadDuration, targetHeadOffsets.ToArray());
                Poses[key] = pose;
                return true;
            }
            pose = null;
            return false;
        }

        private static bool IsValid(CombatPose pose)
        {
            if (pose?.Actor == null || pose.Actor.texture == null) return false;
            foreach (var effect in pose.Effects)
                if (effect == null || effect.texture == null) return false;
            foreach (var effect in pose.TargetHeadEffects)
                if (effect == null || effect.texture == null) return false;
            return true;
        }

        private static T[] Merge<T>(T[] original, T[] additions)
        {
            if (original == null || original.Length == 0) return additions ?? Array.Empty<T>();
            if (additions == null || additions.Length == 0) return original;
            var merged = new T[original.Length + additions.Length];
            Array.Copy(original, merged, original.Length);
            Array.Copy(additions, 0, merged, original.Length, additions.Length);
            return merged;
        }
    }
}

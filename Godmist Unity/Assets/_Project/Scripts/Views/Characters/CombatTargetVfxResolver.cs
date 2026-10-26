using System;
using System.Collections.Generic;
using UnityEngine;

namespace Godmist.Views.Characters
{
    public sealed class CombatTargetVfxClip
    {
        public readonly Sprite[] Frames;
        public readonly float FrameSeconds;
        public readonly Vector2 Offset;
        public readonly bool AboveHead, FitBody;
        public readonly float ContentHeight;
        public readonly float RepeatSeconds, Opacity;
        public float Duration => Frames.Length * FrameSeconds;
        public CombatTargetVfxClip(Sprite[] frames, float frameSeconds, Vector2 offset,
            bool aboveHead = false, bool fitBody = false, float contentHeight = 0,
            float repeatSeconds = 0, float opacity = 1)
        { Frames = frames; FrameSeconds = Mathf.Max(.01f, frameSeconds); Offset = offset;
            AboveHead = aboveHead; FitBody = fitBody; ContentHeight = contentHeight;
            RepeatSeconds = Mathf.Max(0, repeatSeconds); Opacity = Mathf.Clamp01(opacity); }
        public Sprite FrameAt(float elapsed, bool loop = false)
        {
            var index = Mathf.Max(0, Mathf.FloorToInt(elapsed / FrameSeconds));
            return Frames[loop ? index % Frames.Length : Mathf.Min(index, Frames.Length - 1)];
        }
    }

    /// <summary>Shared immutable art cache, like the existing combat pose catalog.</summary>
    public static class CombatTargetVfxResolver
    {
        private const string Root = "Godmist/CombatPoses/TargetVfx/";
        private static readonly Dictionary<string, CombatTargetVfxClip> Clips = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, int[]> ActorRects = new(StringComparer.Ordinal);
        private static bool loaded;
        #pragma warning disable 0649
        [Serializable] private sealed class Catalog { public Entry[] clips; public Actor[] actors; }
        [Serializable] private sealed class Actor { public string name; public int[] rect; }
        [Serializable] private sealed class Entry
        { public string id; public string[] frames; public float frameSeconds, pivotX, pivotY, offsetX, offsetY, contentHeight, repeatSeconds; public float opacity = 1; public bool aboveHead, fitBody; }
        #pragma warning restore 0649
        public static bool TryResolve(string id, out CombatTargetVfxClip clip)
        {
            // Runtime-created sprites may be destroyed when leaving Play Mode.
            if (loaded && Clips.TryGetValue(id, out var cached)
                && Array.Exists(cached.Frames, frame => frame == null))
            { Clips.Clear(); loaded = false; }
            EnsureLoaded();
            return Clips.TryGetValue(id, out clip);
        }

        private static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            LoadCatalog("catalog"); LoadCatalog("pass2-catalog"); LoadCatalog("pass3-catalog"); LoadCatalog("pass4-catalog"); LoadCatalog("pass5-catalog"); LoadCatalog("pass6-catalog"); LoadCatalog("pass7-catalog"); LoadCatalog("pass8-catalog"); LoadCatalog("pass9-catalog"); LoadCatalog("pass10-catalog"); LoadCatalog("pass11-catalog"); LoadCatalog("pass12-catalog");
        }

        private static void LoadCatalog(string name)
        {
            var json = Resources.Load<TextAsset>(Root + name);
            if (json == null) return;
            var catalog = JsonUtility.FromJson<Catalog>(json.text);
            foreach (var actor in catalog.actors ?? Array.Empty<Actor>()) ActorRects[actor.name] = actor.rect;
            foreach (var entry in catalog.clips ?? Array.Empty<Entry>())
                    {
                        var frames = new List<Sprite>();
                        foreach (var resource in entry.frames)
                        {
                            var texture = Resources.Load<Texture2D>(Root + resource);
                            if (texture == null) break;
                            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                                new Vector2(entry.pivotX, entry.pivotY), 32f, 0, SpriteMeshType.FullRect);
                            sprite.name = resource;
                            frames.Add(sprite);
                        }
                        if (frames.Count == entry.frames.Length && frames.Count > 0)
                            Clips[entry.id] = new CombatTargetVfxClip(frames.ToArray(), entry.frameSeconds,
                                new Vector2(entry.offsetX, entry.offsetY), entry.aboveHead, entry.fitBody, entry.contentHeight,
                                entry.repeatSeconds, entry.opacity > 0 ? entry.opacity : 1);
            }
        }

        public static Bounds ActorVisibleBounds(Sprite sprite)
        {
            if (sprite == null) return new Bounds(Vector3.zero, Vector3.one);
            if (CombatPoseResolver.TryGetVisibleBounds(sprite, out var bounds)) return bounds;
            EnsureLoaded();
            if (!ActorRects.TryGetValue(sprite.name, out var rect)) return sprite.bounds;
            var size = new Vector3(rect[2], rect[3], 0) / sprite.pixelsPerUnit;
            var center = new Vector3(rect[0] + rect[2] * .5f - sprite.pivot.x,
                rect[1] + rect[3] * .5f - sprite.pivot.y, 0) / sprite.pixelsPerUnit;
            return new Bounds(center, size);
        }
    }
}

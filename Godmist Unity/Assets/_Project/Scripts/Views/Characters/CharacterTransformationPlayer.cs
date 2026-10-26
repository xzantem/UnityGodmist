using System.Collections.Generic;
using UnityEngine;
namespace Godmist.Views.Characters
{
    internal sealed class CharacterTransformationPlayer
    {
        private IReadOnlyList<Sprite> frames;
        private float start;
        private float fps = 8f;
        public bool IsPlaying => frames != null && frames.Count > 0 && (Time.time - start) * fps < frames.Count;
        public void Play(IReadOnlyList<Sprite> value, float framesPerSecond = 8f) { frames = value; fps = Mathf.Max(.01f, framesPerSecond); start = Time.time; }
        public void Stop() => frames = null;
        public bool Tick(SpriteRenderer renderer)
        {
            if (renderer == null || frames == null || frames.Count == 0) return false;
            var index = Mathf.FloorToInt((Time.time - start) * fps);
            if (index >= frames.Count) { frames = null; return false; }
            renderer.sprite = frames[index]; return true;
        }
    }
}

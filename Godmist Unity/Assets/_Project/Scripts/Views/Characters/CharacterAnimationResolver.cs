using System;
using System.Collections.Generic;
using UnityEngine;

namespace Godmist.Views.Characters
{
    public enum CharacterAnimationState
    {
        Idle,
        Walk
    }

    /// <summary>
    /// Runtime lookup for the imported directional character packs.
    /// The existing BodyRenderer remains the owner of lighting, outline and
    /// depth bindings; this class only supplies cached sprite frames.
    /// </summary>
    public sealed class CharacterAnimationClip
    {
        private readonly Sprite[] frames;
        private readonly float[] frameDurations;

        internal CharacterAnimationClip(Sprite[] frames, float[] frameDurations)
        {
            this.frames = frames;
            this.frameDurations = frameDurations;
        }

        public IReadOnlyList<Sprite> Frames => frames;
        public int FrameCount => frames.Length;
        internal bool HasValidFrames
        {
            get
            {
                foreach (var frame in frames)
                    if (frame == null || frame.texture == null) return false;
                return true;
            }
        }

        public float GetFrameDuration(int index)
        {
            if (frameDurations == null || frameDurations.Length == 0)
                return .25f;

            var safeIndex = Mathf.Clamp(index, 0, frameDurations.Length - 1);
            return Mathf.Max(.01f, frameDurations[safeIndex]);
        }
    }

    public static class CharacterAnimationResolver
    {
        private static readonly Unity.Profiling.ProfilerMarker ResolveSample = new("Godmist.Animation.Resolve");
        private static readonly Unity.Profiling.ProfilerMarker LoadSpriteSample = new("Godmist.Animation.LoadSprite");
        private static readonly Unity.Profiling.ProfilerMarker PrepareSample = new("Godmist.Animation.PrepareDirections");
        private const string ResourceRoot = "Godmist/CharacterAnimations";
        private const int CharacterFrameCount = 4;
        private const int CharacterFrameSize = 64;
        private const int DeadKingFrameSize = 128;
        private const float PixelsPerUnit = 32f;
        private const int DefaultGroundPivotPixels = 3;

        private static readonly string[] Directions = { "e", "ne", "n", "nw", "w", "sw", "s", "se" };
        private static readonly Dictionary<string, string> CharacterUnits =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["character.mage"] = "alison",
                ["character.paladin"] = "reimond",
                ["jerzy"] = "jerzy",
                ["wladimir"] = "wladimir",
                ["enemy.catacombs.archer"] = "skeleton-archer",
                ["enemy.catacombs.skeleton_archer"] = "skeleton-archer",
                ["enemy.catacombs.defender"] = "skeleton-defender",
                ["enemy.catacombs.skeleton_defender"] = "skeleton-defender",
                ["enemy.catacombs.occultist"] = "skeleton-occultist",
                ["enemy.catacombs.skeleton_occultist"] = "skeleton-occultist",
                ["enemy.catacombs.swordsman"] = "skeleton-swordsman",
                ["enemy.catacombs.skeleton_swordsman"] = "skeleton-swordsman",
                ["enemy.catacombs.undead"] = "undead-fleshy",
                ["enemy.catacombs.undead_fleshy"] = "undead-fleshy",
                ["enemy.catacombs.ghoul"] = "ghoul-humanoid",
                ["enemy.catacombs.ghoul_humanoid"] = "ghoul-humanoid",
                ["enemy.catacombs.wraith"] = "wraith",
                ["enemy.catacombs.dead_king"] = "dead-king",
                ["enemy.catacombs.dead_king.enraged"] = "dead-king-enraged",
                ["enemy.catacombs.spectral_knight"] = "spectral-knight",
                ["enemy.catacombs.skeleton_slayer"] = "skeleton-slayer-p1",
                ["enemy.catacombs.skeleton_slayer.enraged"] = "skeleton-slayer-p2",
                ["skeleton.test"] = "skeleton-swordsman",
                ["boss.test"] = "dead-king",
                ["enemy.forest.bandit_thug"] = "bandit-cutthroat",
                ["enemy.forest.bandit_shooter"] = "bandit-shooter",
                ["enemy.forest.bandit_leader"] = "bandit-leader-p1",
                ["enemy.forest.bandit_leader.revealed"] = "bandit-leader-p2",
                ["enemy.forest.elf_archer"] = "elf-archer",
                ["enemy.forest.elf_druid"] = "elf-druid",
                ["enemy.forest.elf_poacher"] = "elf-poacher",
                ["enemy.forest.rabid_wolf"] = "rabid-wolf",
                ["enemy.forest.infected_tree"] = "infected-tree",
                ["enemy.forest.infected_ent"] = "infected-ent-p1",
                ["enemy.forest.infected_ent.burning"] = "infected-ent-p2"
            };

        private static readonly Dictionary<string, string> NpcUnits =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["npc.arungard.alchemist_01"] = "alchemist",
                ["npc.arungard.blacksmith_01"] = "blacksmith",
                ["npc.arungard.enchanter_01"] = "enchanter",
                ["npc.arungard.priest_01"] = "priest",
                ["npc.arungard.questgiver_01"] = "gate-guardian"
            };

        private static readonly Dictionary<string, CharacterAnimationClip> ClipCache =
            new Dictionary<string, CharacterAnimationClip>(StringComparer.Ordinal);
        private static readonly HashSet<string> MissingClips = new HashSet<string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, Sprite> SpriteCache =
            new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private static readonly Dictionary<string, CharacterPackCatalog> CharacterCatalog =
            new Dictionary<string, CharacterPackCatalog>(StringComparer.Ordinal);
        private static readonly Dictionary<string, NpcPackCatalog> NpcCatalog =
            new Dictionary<string, NpcPackCatalog>(StringComparer.Ordinal);
        private static bool catalogLoaded;

        #pragma warning disable 0649

        [Serializable]
        private sealed class RuntimeCatalog
        {
            public CharacterPackCatalog[] characters;
            public NpcPackCatalog[] npcs;
        }

        [Serializable]
        private sealed class CharacterPackCatalog
        {
            public string unit;
            public int frameSize;
            public float[] idleDurationMs;
            public float[] walkDurationMs;
            public int pivotBottomPixels;
            public int corpseFrameSize;

            public float Duration(CharacterAnimationState state, int frameIndex)
            {
                var values = state == CharacterAnimationState.Walk ? walkDurationMs : idleDurationMs;
                if (values == null || values.Length == 0) return state == CharacterAnimationState.Walk ? .2f : .4f;
                return Mathf.Max(.01f, values[Mathf.Clamp(frameIndex, 0, values.Length - 1)] / 1000f);
            }

            public int PivotBottomPixels()
            {
                if (pivotBottomPixels > 0) return pivotBottomPixels;
                return string.Equals(unit, "dead-king", StringComparison.Ordinal) ? 12 : DefaultGroundPivotPixels;
            }
        }

        [Serializable]
        private sealed class NpcPackCatalog
        {
            public string unit;
            public int frameSize;
            public float[] conversationIdleDurationMs;
            public float[] workDurationMs;
            public int workFrameCount;
            public int pivotBottomPixels;
            public string stationResource;
            public float stationOffsetX;
            public float stationOffsetY;

            public float Duration(int frameIndex, bool work)
            {
                var values = work ? workDurationMs : conversationIdleDurationMs;
                if (values == null || values.Length == 0) return work ? .2f : .4f;
                return Mathf.Max(.01f, values[Mathf.Clamp(frameIndex, 0, values.Length - 1)] / 1000f);
            }

            public int PivotBottomPixels()
            {
                return pivotBottomPixels > 0 ? pivotBottomPixels : DefaultGroundPivotPixels;
            }
        }

        #pragma warning restore 0649

        public static bool HasImportedCharacterAnimation(string characterId)
        {
            return TryGetUnit(CharacterUnits, characterId, out _);
        }

        public static bool HasImportedNpcIdle(string npcId)
        {
            return TryGetUnit(NpcUnits, npcId, out _);
        }

        public static bool TryResolveCharacter(
            string characterId,
            Vector2 facingDirection,
            CharacterAnimationState state,
            out CharacterAnimationClip clip)
        {
            using var sample = ResolveSample.Auto();
            clip = null;
            if (!TryGetUnit(CharacterUnits, characterId, out var unit))
                return false;

            EnsureCatalogLoaded();
            var direction = AssetDirectionFor(characterId, facingDirection);
            var stateName = state == CharacterAnimationState.Walk ? "walk" : "idle";
            var key = unit + ":" + direction + ":" + stateName;
            if (ClipCache.TryGetValue(key, out clip))
            {
                if (clip.HasValidFrames) return true;
                ClipCache.Remove(key);
            }
            if (MissingClips.Contains(key))
                return false;

            var pack = GetCharacterCatalog(unit);
            var frameSize = pack != null && pack.frameSize > 0
                ? pack.frameSize
                : string.Equals(unit, "dead-king", StringComparison.Ordinal) ? DeadKingFrameSize : CharacterFrameSize;
            var frames = new Sprite[CharacterFrameCount];
            var durations = new float[CharacterFrameCount];
            for (var frameIndex = 0; frameIndex < CharacterFrameCount; frameIndex++)
            {
                var resourceKey = unit + "/" + unit + "-" + direction + "-" + stateName + "-"
                    + frameIndex.ToString("00") + "-" + frameSize;
                var pivotBottomPixels = pack?.PivotBottomPixels()
                    ?? (string.Equals(unit, "dead-king", StringComparison.Ordinal) ? 12 : DefaultGroundPivotPixels);
                if (!TryLoadSprite(resourceKey, unit + "-" + direction + "-" + stateName + "-" + frameIndex,
                    pivotBottomPixels,
                    out frames[frameIndex]))
                {
                    MissingClips.Add(key);
                    clip = null;
                    return false;
                }

                durations[frameIndex] = pack?.Duration(state, frameIndex)
                    ?? (state == CharacterAnimationState.Walk ? .2f : .4f);
            }

            clip = new CharacterAnimationClip(frames, durations);
            ClipCache[key] = clip;
            return true;
        }

        public static bool TryResolveNpcIdle(string npcId, out CharacterAnimationClip clip)
            => TryResolveNpc(npcId, false, out clip);

        public static bool TryResolveNpcWork(string npcId, out CharacterAnimationClip clip)
            => TryResolveNpc(npcId, true, out clip);

        public static bool TryResolveNpcForeground(string npcId, bool work, out CharacterAnimationClip clip)
        {
            // The cauldron must cover the apron all the way to its rear rim.
            // Its old upper-body crop includes the apron inside the pot silhouette;
            // unlike the smith's hands over the anvil, it is not a foreground layer.
            // Hands and the exposed spoon remain visible above the pot on the body.
            if (TryGetUnit(NpcUnits, npcId, out var unit) && unit == "alchemist")
            {
                clip = null;
                return false;
            }
            return TryResolveNpc(npcId, work, out clip, true);
        }

        public static bool TryResolveNpcStation(string npcId, out Sprite sprite, out Vector2 offset)
        {
            sprite = null;
            offset = default;
            if (!TryGetUnit(NpcUnits, npcId, out var unit)) return false;
            EnsureCatalogLoaded();
            var pack = GetNpcCatalog(unit);
            if (string.IsNullOrEmpty(pack?.stationResource)) return false;
            offset = new Vector2(pack.stationOffsetX, pack.stationOffsetY);
            var key = pack.stationResource;
            if (SpriteCache.TryGetValue(key, out sprite) && sprite != null && sprite.texture != null) return true;
            var texture = Resources.Load<Texture2D>(ResourceRoot + "/" + key);
            if (texture == null) return false;
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(0, 1), PixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = key;
            SpriteCache[key] = sprite;
            return true;
        }

        private static bool TryResolveNpc(string npcId, bool work, out CharacterAnimationClip clip, bool foreground = false)
        {
            clip = null;
            if (!TryGetUnit(NpcUnits, npcId, out var unit))
                return false;

            EnsureCatalogLoaded();
            var state = work ? "work" : "conversation-idle";
            var key = unit + ":" + state + (foreground ? ":upper" : "");
            if (ClipCache.TryGetValue(key, out clip))
            {
                if (clip.HasValidFrames) return true;
                ClipCache.Remove(key);
            }
            if (MissingClips.Contains(key))
                return false;

            var pack = GetNpcCatalog(unit);
            if (foreground && string.IsNullOrEmpty(pack?.stationResource)) return false;
            var count = work ? (pack?.workFrameCount > 0 ? pack.workFrameCount : 8) : CharacterFrameCount;
            var frames = new Sprite[count];
            var durations = new float[count];
            var frameSize = pack != null && pack.frameSize > 0 ? pack.frameSize : CharacterFrameSize;
            for (var frameIndex = 0; frameIndex < count; frameIndex++)
            {
                var resourceKey = unit + "/" + unit + "-" + state + "-"
                    + frameIndex.ToString("00") + "-" + frameSize + (foreground ? "-upper" : "");
                if (!TryLoadSprite(resourceKey, unit + "-" + state + "-" + frameIndex,
                    pack?.PivotBottomPixels() ?? DefaultGroundPivotPixels,
                    out frames[frameIndex]))
                {
                    MissingClips.Add(key);
                    clip = null;
                    return false;
                }

                durations[frameIndex] = pack?.Duration(frameIndex, work) ?? .4f;
            }

            clip = new CharacterAnimationClip(frames, durations);
            ClipCache[key] = clip;
            return true;
        }

        private static bool TryGetUnit(Dictionary<string, string> units, string id, out string unit)
        {
            unit = null;
            return !string.IsNullOrWhiteSpace(id) && units.TryGetValue(id, out unit);
        }

        public static bool TryGetCharacterUnit(string id, out string unit)
            => TryGetUnit(CharacterUnits, id, out unit);

        public static bool TryResolveCorpse(string characterId, out Sprite sprite)
        {
            sprite = null;
            if (!TryGetCharacterUnit(characterId, out var unit))
                return false;

            EnsureCatalogLoaded();
            var pack = GetCharacterCatalog(unit);
            var size = pack?.corpseFrameSize > 0 ? pack.corpseFrameSize
                : unit == "dead-king" ? DeadKingFrameSize : CharacterFrameSize;
            var name = unit + "-corpse-" + size;
            // Corpses rest at the death position rather than a standing feet pivot.
            return TryLoadSprite(unit + "/" + name, name, size / 2, out sprite);
        }

        // This pack's two side profiles were authored with E/W filenames reversed.
        // Keep logical facing/world coordinates intact; correct only asset lookup.
        public static string AssetDirectionFor(string characterId, Vector2 direction)
        {
            var facing = DirectionFor(direction);
            return characterId == "enemy.catacombs.spectral_knight"
                ? facing switch { "e" => "w", "w" => "e", _ => facing }
                : facing;
        }

        public static string DirectionFor(Vector2 direction)
        {
            return Directions[DirectionIndexFor(direction)];
        }

        public static int DirectionIndexFor(Vector2 direction)
        {
            if (direction.sqrMagnitude < .000001f)
                return 6;

            var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            var index = Mathf.RoundToInt(angle / 45f);
            return (index % Directions.Length + Directions.Length) % Directions.Length;
        }

        public static void PreparePartyDirections(string characterId)
        {
            // Enemy spawners bind a runtime instance before assigning EnemyId.
            // Scope preparation by the existing imported unit mapping instead.
            if (!TryGetUnit(CharacterUnits, characterId, out var unit)
                || unit is not ("alison" or "reimond" or "jerzy" or "wladimir")) return;
            PrepareCharacterDirections(characterId);
        }

        public static void PrepareCharacterDirections(string characterId)
        {
            using var sample = PrepareSample.Auto();
            if (!TryGetUnit(CharacterUnits, characterId, out _)) return;
            // Use the normal resolver so frames, pivots, durations, fallback
            // and recovery of invalid cached Unity objects stay identical.
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4;
                var facing = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                TryResolveCharacter(characterId, facing, CharacterAnimationState.Idle, out _);
                TryResolveCharacter(characterId, facing, CharacterAnimationState.Walk, out _);
            }
        }

        private static bool TryLoadSprite(string resourceKey, string spriteName, int pivotBottomPixels, out Sprite sprite)
        {
            if (SpriteCache.TryGetValue(resourceKey, out sprite) && sprite != null && sprite.texture != null)
                return true;

            using var sample = LoadSpriteSample.Auto();
            var texture = Resources.Load<Texture2D>(ResourceRoot + "/" + resourceKey);
            if (texture == null)
            {
                sprite = null;
                return false;
            }

            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.anisoLevel = 0;
            var pivot = new Vector2(.5f, Mathf.Clamp01(pivotBottomPixels / Mathf.Max(1f, texture.height)));
            sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                pivot,
                PixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
            sprite.name = spriteName;
            SpriteCache[resourceKey] = sprite;
            return true;
        }

        private static void EnsureCatalogLoaded()
        {
            if (catalogLoaded) return;
            catalogLoaded = true;

            foreach(var resource in new[]{"catalog","skeleton-slayer-catalog","bandit-leader-catalog"})
            {
                var text = Resources.Load<TextAsset>(ResourceRoot + "/" + resource);
                var catalog = text == null ? null : JsonUtility.FromJson<RuntimeCatalog>(text.text);
                if (catalog == null) continue;
                if (catalog.characters != null)
                    foreach (var pack in catalog.characters)
                        if (pack != null && !string.IsNullOrWhiteSpace(pack.unit)) CharacterCatalog[pack.unit] = pack;
                if (catalog.npcs != null)
                    foreach (var pack in catalog.npcs)
                        if (pack != null && !string.IsNullOrWhiteSpace(pack.unit)) NpcCatalog[pack.unit] = pack;
            }
        }

        private static CharacterPackCatalog GetCharacterCatalog(string unit)
        {
            CharacterCatalog.TryGetValue(unit, out var pack);
            return pack;
        }

        private static NpcPackCatalog GetNpcCatalog(string unit)
        {
            NpcCatalog.TryGetValue(unit, out var pack);
            return pack;
        }
    }
}

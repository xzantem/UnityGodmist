using System;
using System.Collections.Generic;
using Godmist.Data.Characters;
using UnityEditor;

namespace Godmist.Editor
{
    /// <summary>Ordinary-enemy MDEF authoring. HP and ATK belong to CharacterDefinition assets.</summary>
    public static class EnemyRoleDurabilitySetup
    {
        static readonly int[] Levels = { 1, 10, 20, 30, 40, 48, 50 };
        readonly struct Profile
        {
            public readonly int[] Magical;
            public Profile(int[] magical) { Magical = magical; }
        }
        static readonly Dictionary<string, Profile> Profiles = new()
        {
            ["enemy.catacombs.archer"] = new(new[] { 6, 10, 24, 50, 106, 201, 225 }),
            ["enemy.catacombs.defender"] = new(new[] { 10, 16, 38, 80, 170, 322, 360 }),
            ["enemy.catacombs.ghoul"] = new(new[] { 10, 16, 38, 80, 170, 322, 360 }),
            ["enemy.catacombs.occultist"] = new(new[] { 48, 76, 180, 380, 808, 1530, 1710 }),
            ["enemy.catacombs.swordsman"] = new(new[] { 8, 12, 28, 60, 128, 242, 270 }),
            ["enemy.catacombs.undead"] = new(new[] { 9, 14, 33, 70, 149, 282, 315 }),
            ["enemy.catacombs.wraith"] = new(new[] { 35, 56, 133, 280, 595, 1127, 1260 }),
            ["enemy.forest.bandit_shooter"] = new(new[] { 4, 6, 14, 30, 64, 121, 135 }),
            ["enemy.forest.bandit_thug"] = new(new[] { 5, 8, 19, 40, 85, 161, 180 }),
            ["enemy.forest.elf_archer"] = new(new[] { 5, 8, 19, 40, 85, 161, 180 }),
            ["enemy.forest.elf_druid"] = new(new[] { 25, 40, 95, 200, 425, 805, 900 }),
            ["enemy.forest.elf_poacher"] = new(new[] { 6, 10, 24, 50, 106, 201, 225 }),
            ["enemy.forest.infected_tree"] = new(new[] { 5, 8, 19, 40, 85, 161, 180 }),
            ["enemy.forest.rabid_wolf"] = new(new[] { 2, 3, 8, 15, 32, 60, 68 }),
        };

        internal static float MagicalDefense(string id, int level, float fallback)
        {
            if (id == null || !Profiles.TryGetValue(id, out var profile)) return fallback;
            var index = Array.IndexOf(Levels, level);
            return index < 0 ? fallback : profile.Magical[index];
        }

        // Absolute values, never a multiplier of already-authored values.
        internal static void Configure(string id, SerializedObject data)
        {
            if (id == null || !Profiles.TryGetValue(id, out var profile)) return;
            var growth = data.FindProperty("<GrowthData>k__BackingField");
            data.FindProperty("<BaseStats>k__BackingField").FindPropertyRelative("<MagicalDefense>k__BackingField").floatValue = profile.Magical[0];
            var defense = growth.FindPropertyRelative("<DefenseCurve>k__BackingField");
            for (var i = 0; i < defense.arraySize; i++)
            {
                var point = defense.GetArrayElementAtIndex(i);
                var index = Array.IndexOf(Levels, point.FindPropertyRelative("<Level>k__BackingField").intValue);
                if (index < 0) continue; // New archetype anchors are restored below.
                point.FindPropertyRelative("<MagicalDefense>k__BackingField").floatValue = profile.Magical[index];
            }
        }

        public static bool ApplyToDefinition(CharacterDefinition definition)
        {
            if (definition == null || !Profiles.ContainsKey(definition.Id ?? string.Empty)) return false;
            var originalState = EditorJsonUtility.ToJson(definition);
            var data = new SerializedObject(definition);
            Configure(definition.Id, data);
            EnemyArchetypeStatsSetup.Configure(definition.Id, data);
            if (!data.ApplyModifiedPropertiesWithoutUndo() || EditorJsonUtility.ToJson(definition) == originalState) return false;
            EditorUtility.SetDirty(definition);
            return true;
        }

        [MenuItem("Godmist/Content/Apply ordinary enemy magical defense")]
        public static void ApplyAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before authoring enemy durability.");
            foreach (var guid in AssetDatabase.FindAssets("t:CharacterDefinition", new[] {
                "Assets/_Project/ScriptableObjects/Catacombs/Enemies", "Assets/_Project/ScriptableObjects/Dungeons/Forest" }))
            {
                var definition = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (ApplyToDefinition(definition)) AssetDatabase.SaveAssetIfDirty(definition);
            }
        }
    }
}

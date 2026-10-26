using System;
using System.Collections.Generic;
using Godmist.Data.Characters;
using UnityEditor;

namespace Godmist.Editor
{
    /// <summary>Approved ordinary-enemy HP/MDEF correction. Bosses and ATK are outside this authoring pass.</summary>
    public static class EnemyRoleDurabilitySetup
    {
        static readonly int[] Levels = { 1, 10, 20, 30, 40, 48, 50 };
        static readonly int[] HealthLevels = { 1, 10, 15, 20, 25, 30, 40, 48, 50 };
        readonly struct Profile
        {
            public readonly int[] Health, Magical;
            public Profile(int[] health, int[] magical) { Health = health; Magical = magical; }
        }
        static readonly Dictionary<string, Profile> Profiles = new()
        {
            ["enemy.catacombs.archer"] = new(new[] { 202, 442, 791, 1243, 1939, 2745, 5344, 8699, 9391 }, new[] { 6, 10, 24, 50, 106, 201, 225 }),
            ["enemy.catacombs.defender"] = new(new[] { 209, 478, 826, 1283, 2007, 2846, 6009, 11509, 12548 }, new[] { 10, 16, 38, 80, 170, 322, 360 }),
            ["enemy.catacombs.ghoul"] = new(new[] { 208, 450, 800, 1253, 1943, 2742, 5297, 8417, 9170 }, new[] { 10, 16, 38, 80, 170, 322, 360 }),
            ["enemy.catacombs.occultist"] = new(new[] { 176, 408, 736, 1160, 1817, 2578, 5082, 8308, 8981 }, new[] { 48, 76, 180, 380, 808, 1530, 1710 }),
            ["enemy.catacombs.swordsman"] = new(new[] { 210, 462, 823, 1291, 2009, 2842, 5617, 8911, 9791 }, new[] { 8, 12, 28, 60, 128, 242, 270 }),
            ["enemy.catacombs.undead"] = new(new[] { 299, 668, 1177, 1840, 2877, 4078, 8149, 13035, 14303 }, new[] { 9, 14, 33, 70, 149, 282, 315 }),
            ["enemy.catacombs.wraith"] = new(new[] { 176, 398, 714, 1121, 1748, 2475, 4922, 7776, 8542 }, new[] { 35, 56, 133, 280, 595, 1127, 1260 }),
            ["enemy.forest.bandit_shooter"] = new(new[] { 176, 383, 681, 1078, 1684, 2407, 4763, 7997, 8665 }, new[] { 4, 6, 14, 30, 64, 121, 135 }),
            ["enemy.forest.bandit_thug"] = new(new[] { 197, 427, 756, 1193, 1855, 2642, 5194, 8412, 9290 }, new[] { 5, 8, 19, 40, 85, 161, 180 }),
            ["enemy.forest.elf_archer"] = new(new[] { 161, 343, 608, 961, 1498, 2137, 4174, 6959, 7501 }, new[] { 5, 8, 19, 40, 85, 161, 180 }),
            ["enemy.forest.elf_druid"] = new(new[] { 146, 326, 582, 921, 1444, 2065, 4125, 6970, 7526 }, new[] { 25, 40, 95, 200, 425, 805, 900 }),
            ["enemy.forest.elf_poacher"] = new(new[] { 179, 383, 679, 1073, 1667, 2376, 4639, 7517, 8238 }, new[] { 6, 10, 24, 50, 106, 201, 225 }),
            ["enemy.forest.infected_tree"] = new(new[] { 235, 550, 951, 1491, 2351, 3371, 7431, 12622, 14259 }, new[] { 5, 8, 19, 40, 85, 161, 180 }),
            ["enemy.forest.rabid_wolf"] = new(new[] { 138, 294, 522, 826, 1294, 1849, 3653, 6173, 6655 }, new[] { 2, 3, 8, 15, 32, 60, 68 }),
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
            var curve = growth.FindPropertyRelative("<HealthCurve>k__BackingField");
            curve.arraySize = HealthLevels.Length;
            for (var i = 0; i < HealthLevels.Length; i++)
            {
                var point = curve.GetArrayElementAtIndex(i);
                point.FindPropertyRelative("<Level>k__BackingField").intValue = HealthLevels[i];
                point.FindPropertyRelative("<MaxHealth>k__BackingField").intValue = profile.Health[i];
            }
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

        [MenuItem("Godmist/Content/Apply ordinary enemy durability correction")]
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

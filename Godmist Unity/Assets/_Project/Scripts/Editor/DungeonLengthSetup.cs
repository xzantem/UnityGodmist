using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    public static class DungeonLengthSetup
    {
        public static bool Apply(DungeonDefinition definition)
        {
            if (definition == null || !definition.IsAvailableAsOffer || definition.IsStoryDungeon) return false;
            var data = new SerializedObject(definition);
            var profiles = data.FindProperty("<LengthProfiles>k__BackingField");
            profiles.arraySize = 3;
            for (var i = 0; i < 3; i++)
            {
                var multiplier = i + 1;
                var roomMultiplier = definition.Theme == DungeonTheme.Forest ? multiplier : 1;
                var profile = profiles.GetArrayElementAtIndex(i);
                void Set(string name, int value) => profile.FindPropertyRelative("<"+name+">k__BackingField").intValue = value;
                Set("Length", multiplier);
                Set("FloorCount", definition.Theme == DungeonTheme.Forest ? 1 : multiplier);
                Set("MinRoomsPerFloor", 14 * roomMultiplier);
                Set("MaxRoomsPerFloor", 17 * roomMultiplier);
                // Half the former encounter range, rounded up where a whole
                // fight is needed. Density uses the base room budget; added
                // branch transit rooms do not force additional encounters.
                Set("MinEncountersPerFloor", (9 * roomMultiplier + 1) / 2);
                Set("MaxEncountersPerFloor", (11 * roomMultiplier + 1) / 2);
                Set("MinBranchCount", multiplier);
                Set("MaxBranchCount", multiplier + 2);
                Set("MinBranchTransitRoomCount", multiplier == 3 ? 3 : 2);
                Set("MaxBranchTransitRoomCount", multiplier == 1 ? 3 : multiplier == 2 ? 5 : 8);
                Set("MinLoopArmRoomCount", multiplier + 1);
                Set("MaxLoopArmRoomCount", multiplier == 1 ? 4 : multiplier * 2 + 1);
            }
            if (!data.ApplyModifiedPropertiesWithoutUndo()) return false;
            EditorUtility.SetDirty(definition);AssetDatabase.SaveAssetIfDirty(definition);return true;
        }
        [MenuItem("Godmist/Content/Apply expedition length profiles")]
        public static void ApplyAll()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:DungeonDefinition",new[]{"Assets/_Project/ScriptableObjects/Dungeons"}))
                Apply(AssetDatabase.LoadAssetAtPath<DungeonDefinition>(AssetDatabase.GUIDToAssetPath(guid)));
        }
    }
}

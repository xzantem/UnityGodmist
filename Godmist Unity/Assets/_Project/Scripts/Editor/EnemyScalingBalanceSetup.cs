using Godmist.Data.Characters;
using Godmist.Data.Skills;
using UnityEditor;

namespace Godmist.Editor
{
    // Legacy entry point for role authoring. HP/ATK belong to the authored assets.
    public static class EnemyScalingBalanceSetup
    {
        const string Root = "Assets/_Project/ScriptableObjects/";
        public static bool ApplyToDefinition(CharacterDefinition definition) => EnemyRoleStatsSetup.ApplyToDefinition(definition);

        [MenuItem("Godmist/Content/Apply enemy role balance")]
        public static void ApplyAll()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:CharacterDefinition", new[] { Root + "Catacombs/Enemies", Root + "Dungeons/Forest" }))
            {
                var definition = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (ApplyToDefinition(definition)) AssetDatabase.SaveAssetIfDirty(definition);
            }
            foreach (var guid in AssetDatabase.FindAssets("t:SkillData", new[] { Root + "Catacombs" }))
            {
                var skill = AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(guid));
                if (skill.SkillId != "skill.enemy.catacombs.skeleton_slayer_task") continue;
                var data = new SerializedObject(skill);
                // Effective Rage cost; do not discount again when applying this update.
                data.FindProperty("<ResourceCost>k__BackingField").intValue = 50;
                data.FindProperty("<TooltipDescription>k__BackingField").stringValue =
                    "Przyspiesza następną turę o 100%.";
                if (data.ApplyModifiedPropertiesWithoutUndo()) AssetDatabase.SaveAssetIfDirty(skill);
            }
        }
    }
}

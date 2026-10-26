using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Skills;
using Godmist.Systems.Combat;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    /// <summary>Reviewed content decisions, not a runtime rule inferred from attack type or effect order.</summary>
    public static class ContextualSkillHitSetup
    {
        public enum Dependency { DamageHit, IndependentArea, SharedContactHit }
        public readonly struct Rule
        {
            public Rule(string id, int index, EffectType type, Dependency dependency)
            { SkillId = id; EffectIndex = index; EffectType = type; Kind = dependency; }
            public string SkillId { get; }
            public int EffectIndex { get; }
            public EffectType EffectType { get; }
            public Dependency Kind { get; }
        }
        static Rule Hit(string id, int index, EffectType type) => new(id, index, type, Dependency.DamageHit);
        static Rule Area(string id, int index, EffectType type) => new(id, index, type, Dependency.IndependentArea);
        static Rule Contact(string id, int index, EffectType type) => new(id, index, type, Dependency.SharedContactHit);

        // Each damage dependency references the hit of effect 0 against this SAME target.
        // Missed/absorbed HP damage, failed status application and blocked displacement are distinct outcomes.
        public static IReadOnlyList<Rule> Rules { get; } = new[]
        {
            Hit("skill.mage.fireball",1,EffectType.ApplyStatus),
            Hit("skill.mage.fire_arrow",1,EffectType.ApplyStatus),
            Hit("skill.mage.fire_arrow",2,EffectType.ModifyVar),
            Hit("skill.paladin.blinding_light",1,EffectType.ModifyStat),
            Hit("skill.paladin.crushing_blow",1,EffectType.ModifyResistance),
            Hit("skill.paladin.crushing_blow",2,EffectType.ApplyStatus),
            Hit("skill.scout.poisoned_dagger",1,EffectType.ModifyResistance),
            Hit("skill.scout.poisoned_dagger",2,EffectType.ApplyStatus),
            Hit("skill.warrior.bleeding_destruction",1,EffectType.ApplyStatus),
            Hit("skill.warrior.immobilize",1,EffectType.ApplyStatus),
            Hit("skill.warrior.right_hook",1,EffectType.ApplyStatus),
            Hit("skill.push",1,EffectType.Push),
            Hit("skill.enemy.catacombs.ancient_incantation",1,EffectType.ApplyStatus),
            Hit("skill.enemy.catacombs.bone_club",1,EffectType.ApplyStatus),
            Hit("skill.enemy.catacombs.burning_arrow",1,EffectType.ApplyStatus),
            Hit("skill.enemy.catacombs.dead_king_blade",1,EffectType.ModifyVar),
            Hit("skill.enemy.catacombs.manic_bite",1,EffectType.ApplyStatus),
            Hit("skill.enemy.catacombs.rend",1,EffectType.ApplyStatus),
            Hit("skill.enemy.catacombs.skeleton_slayer_crushing",1,EffectType.ApplyStatus),
            Hit("skill.enemy.catacombs.skeleton_slayer_bone_crush",1,EffectType.ApplyStatus),
            Hit("skill.enemy.catacombs.skeleton_slayer_bone_crush",2,EffectType.ModifyVar),
            Hit("skill.enemy.catacombs.spectral_knight_strike",1,EffectType.ApplyStatus),
            Hit("skill.enemy.forest.archer_arrow_rain",1,EffectType.ModifyStat),
            Hit("skill.enemy.forest.bandit_knife",1,EffectType.ApplyStatus),
            Hit("skill.enemy.forest.ent_branch",1,EffectType.Push),
            Hit("skill.enemy.forest.ent_branch",2,EffectType.ApplyStatus),
            Hit("skill.enemy.forest.ent_fist",1,EffectType.ApplyStatus),
            Hit("skill.enemy.forest.poacher_knife",1,EffectType.ApplyStatus),
            Hit("skill.enemy.forest.shooter_piercing_bolt",1,EffectType.ApplyStatus),
            Hit("skill.enemy.forest.shooter_shot",1,EffectType.ModifyStat),
            Hit("skill.enemy.forest.tree_dark_roots",1,EffectType.ApplyStatus),
            Hit("skill.enemy.forest.wolf_bite",1,EffectType.ApplyStatus),

            // Ground shock, forest ignition, spreading plague and spectral chill affect the area itself.
            Area("skill.enemy.catacombs.skeleton_slayer_tremors",1,EffectType.Push),
            Area("skill.enemy.catacombs.skeleton_slayer_tremors",2,EffectType.ModifyStat),
            Area("skill.enemy.forest.ent_ignition",1,EffectType.ApplyStatus),
            Area("skill.enemy.forest.tree_plague_growth",1,EffectType.ExtendStatusDuration),
            Area("skill.enemy.catacombs.dead_king_wave",1,EffectType.ModifyStat),

            // These physical contacts have no damage effect. Both effects share the existing per-target hit roll;
            // bleed/disorientation must not depend on moving the target or overcoming a separate resistance.
            Contact("skill.warrior.kick",0,EffectType.Push),
            Contact("skill.warrior.kick",1,EffectType.ModifyStat),
            Contact("skill.enemy.catacombs.ancient_whip",0,EffectType.Push),
            Contact("skill.enemy.catacombs.ancient_whip",1,EffectType.ApplyStatus)
        };

        public static bool Apply(SerializedObject data)
        {
            if (data.targetObject is not SkillData) return false;
            var id = data.FindProperty("<SkillId>k__BackingField").stringValue;
            var effects = data.FindProperty("effects");
            foreach (var rule in Rules.Where(r => r.SkillId == id))
            {
                if (rule.EffectIndex >= effects.arraySize)
                    throw new InvalidOperationException($"{id}: missing reviewed effect {rule.EffectIndex}.");
                var effect = effects.GetArrayElementAtIndex(rule.EffectIndex);
                if ((EffectType)effect.FindPropertyRelative("effectType").enumValueIndex != rule.EffectType)
                    throw new InvalidOperationException($"{id}: effect {rule.EffectIndex} changed type; review its dependency.");
                var conditions = effect.FindPropertyRelative("conditions");
                if (rule.Kind == Dependency.SharedContactHit)
                {
                    effect.FindPropertyRelative("alwaysHits").boolValue = false;
                    continue;
                }
                if (effects.arraySize == 0 || (EffectType)effects.GetArrayElementAtIndex(0)
                    .FindPropertyRelative("effectType").enumValueIndex != EffectType.DealDamage)
                    throw new InvalidOperationException($"{id}: reviewed source effect 0 is no longer damage.");
                if (rule.Kind == Dependency.DamageHit && effect.FindPropertyRelative("targetModeIndex").intValue
                    != effects.GetArrayElementAtIndex(0).FindPropertyRelative("targetModeIndex").intValue)
                    throw new InvalidOperationException($"{id}: dependent effect targets a different group; review it.");
                // Do not accidentally convert a dependency on poison/stun success into a damage-hit dependency.
                var found = false;
                for (var i = conditions.arraySize - 1; i >= 0; i--)
                {
                    var c = conditions.GetArrayElementAtIndex(i);
                    if ((EffectConditionType)c.FindPropertyRelative("conditionType").enumValueIndex
                        != EffectConditionType.ReferencedEffectSucceeded
                        || c.FindPropertyRelative("referencedEffectIndex").intValue != 0) continue;
                    if (rule.Kind == Dependency.IndependentArea || found) conditions.DeleteArrayElementAtIndex(i);
                    else found = true;
                }
                if (rule.Kind == Dependency.DamageHit && !found)
                {
                    var i = conditions.arraySize; conditions.InsertArrayElementAtIndex(i);
                    var c = conditions.GetArrayElementAtIndex(i);
                    c.FindPropertyRelative("conditionType").enumValueIndex = (int)EffectConditionType.ReferencedEffectSucceeded;
                    c.FindPropertyRelative("referencedEffectIndex").intValue = 0;
                }
                // The referenced damage already rolled accuracy. Area effects use their own chance/resistance.
                effect.FindPropertyRelative("alwaysHits").boolValue = true;
            }
            return data.hasModifiedProperties;
        }

        [MenuItem("Godmist/Content/Apply Contextual Skill Hit Dependencies")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            var skills = AssetDatabase.FindAssets("t:SkillData", new[] { "Assets/_Project/ScriptableObjects" })
                .Select(g => AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
            foreach (var id in Rules.Select(r => r.SkillId).Distinct())
                if (skills.Count(s => s.SkillId == id) != 1) throw new InvalidOperationException("Missing/duplicate skill: " + id);
            var changed = 0;
            foreach (var skill in skills)
            {
                var data = new SerializedObject(skill);
                if (!Apply(data)) continue;
                data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(skill); changed++;
                var validation = CombatDataValidator.ValidateSkill(skill);
                if (!validation.IsValid) throw new InvalidOperationException(skill.SkillId + ": " + string.Join("; ", validation.Errors));
                Debug.Log("CONTEXTUAL_SKILL_UPDATED " + skill.SkillId);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"CONTEXTUAL_SKILL_HIT_CONFIGURED: {changed} skills changed.");
        }
    }
}

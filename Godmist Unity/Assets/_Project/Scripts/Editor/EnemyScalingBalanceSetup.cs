using System.Collections.Generic;
using Godmist.Data.Characters;
using Godmist.Data.Skills;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    // Static content calibration with bounded multi-encounter simulations against a developed
    // level48 party. Excellent tier45 gear, Common rarity, jewelry, no upgrades/uniques.
    // Never scales to the player's current gear. The shared late-growth bonus ramps
    // from level10 to48; complementary early calibration vanishes at48.
    public static class EnemyScalingBalanceSetup
    {
        public const float CommonHealthGrowthFactor = 2f;
        public const float CommonAttackGrowthFactor = 2.5f;
        public const float EarlyHealthGrowthFactor = 1f;
        public const float EarlyAttackGrowthFactor = 1f;
        public const float BaseHealthFactor = .44f;
        public const float BaseAttackFactor = .63f;
        // Apply once to the already-rounded bases used by the approved chart.
        // Keep legacy growth compensation and offsets fixed: this is an additive
        // base increase at every level, not a new multiplier on accumulated growth.
        public const float OrdinaryBaseStatMultiplier = 1.4f;
        public const float BossBaseStatMultiplier = 1.4f;
        // Fixed reference of the previously verified L48/L50 calibration.
        // Bosses and the three-hit summoned knight retain these starting factors.
        public const float AnchorBaseHealthFactor = .4f;
        public const float AnchorBaseAttackFactor = .6f;
        const string Root = "Assets/_Project/ScriptableObjects/";
        static readonly Dictionary<string,Vector3> OriginalBases = new()
        {
            ["enemy.catacombs.archer"]=new(300,40,60),["enemy.catacombs.dead_king"]=new(900,48,96),
            ["enemy.catacombs.defender"]=new(550,25,45),["enemy.catacombs.ghoul"]=new(400,36,66),
            ["enemy.catacombs.occultist"]=new(250,55,80),["enemy.catacombs.skeleton_slayer"]=new(1250,60,85),
            ["enemy.catacombs.spectral_knight"]=new(3,42,64),["enemy.catacombs.swordsman"]=new(350,42,64),
            ["enemy.catacombs.undead"]=new(500,30,48),["enemy.catacombs.wraith"]=new(270,25,75),
            ["enemy.forest.bandit_leader"]=new(500,65,90),["enemy.forest.bandit_shooter"]=new(270,45,70),
            ["enemy.forest.bandit_thug"]=new(350,35,55),["enemy.forest.elf_archer"]=new(240,50,70),
            ["enemy.forest.elf_druid"]=new(200,20,30),["enemy.forest.elf_poacher"]=new(310,40,60),
            ["enemy.forest.infected_ent"]=new(1400,55,75),["enemy.forest.infected_tree"]=new(450,30,35),
            ["enemy.forest.rabid_wolf"]=new(190,36,72)
        };
        public static bool TryGetOriginalBase(string id,out Vector3 stats) => OriginalBases.TryGetValue(
            (id ?? "").Replace(".enraged","").Replace(".revealed","").Replace(".burning",""),out stats);
        readonly struct Profile
        {
            public readonly float Health, MinimumAttack, MaximumAttack;
            public readonly float LateHealth, LateMinimumAttack, LateMaximumAttack;
            public readonly float EarlyHealth, EarlyMinimumAttack, EarlyMaximumAttack;
            public readonly bool ApplyEarlyThreat;
            public Profile(float healthGrowth, float minimumGrowth, float maximumGrowth,
                float healthFactor = CommonHealthGrowthFactor, float attackFactor = CommonAttackGrowthFactor,
                float earlyHealthFactor = EarlyHealthGrowthFactor, float earlyAttackFactor = EarlyAttackGrowthFactor,
                bool applyEarlyThreat = true)
            {
                ApplyEarlyThreat = applyEarlyThreat;
                Health = healthGrowth; MinimumAttack = minimumGrowth; MaximumAttack = maximumGrowth;
                LateHealth = Mathf.Round(healthGrowth * healthFactor * 1000f) / 1000f - healthGrowth;
                LateMinimumAttack = Mathf.Round(minimumGrowth * attackFactor * 1000f) / 1000f - minimumGrowth;
                LateMaximumAttack = Mathf.Round(maximumGrowth * attackFactor * 1000f) / 1000f - maximumGrowth;
                EarlyHealth = Mathf.Round(healthGrowth * earlyHealthFactor * 1000f) / 1000f - healthGrowth;
                EarlyMinimumAttack = Mathf.Round(minimumGrowth * earlyAttackFactor * 1000f) / 1000f - minimumGrowth;
                EarlyMaximumAttack = Mathf.Round(maximumGrowth * earlyAttackFactor * 1000f) / 1000f - maximumGrowth;
            }
        }

        // Original growth values make reauthoring idempotent. Early and late
        // HP/ATK adjustments have complementary weights; legacy fields stay intact.
        static readonly Dictionary<string, Profile> Profiles = new()
        {
            ["enemy.catacombs.swordsman"] = new(23, 2.73f, 4.16f),
            ["enemy.catacombs.undead"] = new(33, 1.95f, 3.12f),
            ["enemy.catacombs.wraith"] = new(18, 1.63f, 4.88f),
            ["enemy.catacombs.occultist"] = new(17, 3.58f, 5.2f),
            ["enemy.catacombs.archer"] = new(20, 2.6f, 3.9f),
            ["enemy.catacombs.ghoul"] = new(26, 2.34f, 4.29f),
            ["enemy.catacombs.defender"] = new(36, 1.63f, 2.93f),
            ["enemy.catacombs.spectral_knight"] = new(0, 2.73f, 4.16f, earlyHealthFactor: .6f, earlyAttackFactor: .6f, applyEarlyThreat: false),
            ["enemy.forest.bandit_thug"] = new(23, 2.28f, 3.58f),
            ["enemy.forest.bandit_shooter"] = new(18, 2.93f, 4.55f),
            ["enemy.forest.elf_archer"] = new(16, 3.25f, 4.55f),
            ["enemy.forest.elf_poacher"] = new(21, 2.6f, 3.9f),
            ["enemy.forest.rabid_wolf"] = new(13, 2.34f, 4.68f),
            ["enemy.forest.elf_druid"] = new(13, 1.3f, 1.95f),
            ["enemy.forest.infected_tree"] = new(30, 1.95f, 2.28f),
            // Boss counters and phase mechanics require a separate simulation baseline.
            ["enemy.catacombs.dead_king"] = new(59, 3.12f, 6.24f, 1.25f, 1.5f, 1f, 1f, false),
            ["enemy.catacombs.skeleton_slayer"] = new(82, 3.9f, 5.53f, 2.25f, 2.5f, 1f, 1f, false),
            ["enemy.forest.infected_ent"] = new(91, 3.58f, 4.88f, 2.5f, 2.5f, 1f, 1f, false),
            // Hidden Strike is x5 guaranteed critical; applying the ordinary ATK
            // multiplier here made the simulated fight overwhelmingly one-sided.
            ["enemy.forest.bandit_leader"] = new(33, 4.23f, 5.85f, 2.5f, 1.15f, 1f, 1f, false)
        };

        static bool TryProfile(string id, out Profile profile)
        {
            id = id switch
            {
                "enemy.catacombs.dead_king.enraged" => "enemy.catacombs.dead_king",
                "enemy.catacombs.skeleton_slayer.enraged" => "enemy.catacombs.skeleton_slayer",
                "enemy.forest.infected_ent.burning" => "enemy.forest.infected_ent",
                "enemy.forest.bandit_leader.revealed" => "enemy.forest.bandit_leader",
                _ => id
            };
            return Profiles.TryGetValue(id ?? "", out profile);
        }

        public static bool UsesEarlyThreatCalibration(string id) =>
            TryProfile(id, out var profile) && profile.ApplyEarlyThreat;

        public static bool UsesBossBaseBoost(string id) => TryProfile(id, out var profile)
            && !profile.ApplyEarlyThreat && TryGetOriginalBase(id, out var original) && original.x != 3;

        public static bool ApplyToDefinition(CharacterDefinition definition)
        {
            if (definition == null || !TryProfile(definition.Id, out var profile)
                || !TryGetOriginalBase(definition.Id, out var originalBase)) return false;
            var data = new SerializedObject(definition);
            // Keep BOTH verified anchors. Changing the compensation denominator alone
            // would preserve either L48 or L50, but move the other endpoint.
            var anchorBase = new Vector3(originalBase.x == 3 ? 3 : Mathf.Max(1, Mathf.RoundToInt(originalBase.x * AnchorBaseHealthFactor)),
                Mathf.Max(1, Mathf.RoundToInt(originalBase.y * AnchorBaseAttackFactor)),
                Mathf.Max(1, Mathf.RoundToInt(originalBase.z * AnchorBaseAttackFactor)));
            var reduced = profile.ApplyEarlyThreat
                ? new Vector3(Mathf.Max(1, Mathf.RoundToInt(originalBase.x * BaseHealthFactor)),
                    Mathf.Max(1, Mathf.RoundToInt(originalBase.y * BaseAttackFactor)),
                    Mathf.Max(1, Mathf.RoundToInt(originalBase.z * BaseAttackFactor)))
                : anchorBase;
            var compensation = (originalBase - anchorBase) / 199f;
            var lateOffset = anchorBase - reduced;
            var baseMultiplier = profile.ApplyEarlyThreat ? OrdinaryBaseStatMultiplier
                : UsesBossBaseBoost(definition.Id) ? BossBaseStatMultiplier : 1f;
            var authoredBase = new Vector3(Mathf.RoundToInt(reduced.x * baseMultiplier),
                Mathf.RoundToInt(reduced.y * baseMultiplier), Mathf.RoundToInt(reduced.z * baseMultiplier));
            var basis = data.FindProperty("<BaseStats>k__BackingField");
            basis.FindPropertyRelative("<MaxHealth>k__BackingField").intValue = (int)authoredBase.x;
            basis.FindPropertyRelative("<MinAttack>k__BackingField").intValue = (int)authoredBase.y;
            basis.FindPropertyRelative("<MaxAttack>k__BackingField").intValue = (int)authoredBase.z;
            var growth = data.FindProperty("<GrowthData>k__BackingField");
            growth.FindPropertyRelative("<MaxHealth>k__BackingField").floatValue = profile.Health;
            growth.FindPropertyRelative("<MinAttack>k__BackingField").floatValue = profile.MinimumAttack;
            growth.FindPropertyRelative("<MaxAttack>k__BackingField").floatValue = profile.MaximumAttack;
            growth.FindPropertyRelative("<LateMaxHealth>k__BackingField").floatValue = profile.LateHealth + compensation.x;
            growth.FindPropertyRelative("<LateMinAttack>k__BackingField").floatValue = profile.LateMinimumAttack + compensation.y;
            growth.FindPropertyRelative("<LateMaxAttack>k__BackingField").floatValue = profile.LateMaximumAttack + compensation.z;
            growth.FindPropertyRelative("<EarlyMaxHealth>k__BackingField").floatValue = profile.EarlyHealth;
            growth.FindPropertyRelative("<EarlyMinAttack>k__BackingField").floatValue = profile.EarlyMinimumAttack;
            growth.FindPropertyRelative("<EarlyMaxAttack>k__BackingField").floatValue = profile.EarlyMaximumAttack;
            growth.FindPropertyRelative("<LateMaxHealthOffset>k__BackingField").floatValue = lateOffset.x;
            growth.FindPropertyRelative("<LateMinAttackOffset>k__BackingField").floatValue = lateOffset.y;
            growth.FindPropertyRelative("<LateMaxAttackOffset>k__BackingField").floatValue = lateOffset.z;
            var coreChanged = data.ApplyModifiedPropertiesWithoutUndo();
            var rolesChanged = EnemyRoleStatsSetup.ApplyToDefinition(definition);
            if (!coreChanged && !rolesChanged) return false;
            EditorUtility.SetDirty(definition);
            return true;
        }

        [MenuItem("Godmist/Content/Apply enemy scaling playtest balance")]
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

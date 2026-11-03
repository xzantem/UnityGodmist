using System.Collections.Generic;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    // Frozen pre-0.1.3 inputs for historical opt-in simulations only.
    // Production HP/ATK and content setup never read this reference.
    internal static class HistoricalEnemyBalanceReference
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
        // Historical classification for explicit comparison experiments, not production authoring.
        public static bool UsesBossBaseBoost(string id) => TryGetOriginalBase(id, out _)
            && (id.StartsWith("enemy.catacombs.dead_king", System.StringComparison.Ordinal)
                || id.StartsWith("enemy.catacombs.skeleton_slayer", System.StringComparison.Ordinal)
                || id.StartsWith("enemy.forest.infected_ent", System.StringComparison.Ordinal)
                || id.StartsWith("enemy.forest.bandit_leader", System.StringComparison.Ordinal));
        public static bool UsesEarlyThreatCalibration(string id) => TryGetOriginalBase(id, out _)
            && id != "enemy.catacombs.spectral_knight" && !UsesBossBaseBoost(id);

    }
}

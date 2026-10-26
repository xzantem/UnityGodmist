using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Data.Skills;
using Godmist.Data.Effects;
using Godmist.Runtime.Combat;

namespace Godmist.Systems.Combat
{
    /// <summary>Execution cues, independent of casting poses and persistent status state.</summary>
    public static class CombatTargetVfxFeedback
    {
        public static void PublishArea(CombatFeedbackBus feedback, CombatSession session,
            BattleUnit caster, SkillData skill,
            IReadOnlyDictionary<int, GridPosition?> selected = null,
            IReadOnlyDictionary<int, int> rotations = null,
            IReadOnlyDictionary<int, IReadOnlyList<BattleUnit>> targets = null, string actionId = null)
        {
            if (feedback == null || session?.Grid == null || caster == null || skill == null) return;
            var kind = skill.SkillId switch
            {
                "skill.enemy.catacombs.skeleton_slayer_tremors" => CombatTargetVfxKind.Tremor,
                "skill.mage.energy_split" => CombatTargetVfxKind.EnergyGround,
                "skill.mage.fire_arrow" or "skill.enemy.catacombs.burning_arrow" => CombatTargetVfxKind.FireGround,
                "skill.enemy.forest.shooter_piercing_bolt" or "skill.enemy.forest.archer_arrow_rain" => CombatTargetVfxKind.ArrowGround,
                "skill.scout.smoke" => CombatTargetVfxKind.SmokeGround,
                "skill.warrior.whirlwind" => CombatTargetVfxKind.SmokeGround,
                "skill.paladin.grounding_wave" => CombatTargetVfxKind.PullDust,
                "skill.enemy.catacombs.savage_roar" => CombatTargetVfxKind.PullDust,
                "skill.enemy.catacombs.vomit" or "skill.enemy.forest.poacher_poison_bag"
                    or "skill.enemy.forest.druid_poison_pulse" or "skill.enemy.forest.tree_plague_growth" => CombatTargetVfxKind.ToxicGround,
                "skill.enemy.forest.druid_healing_pulse" => CombatTargetVfxKind.GrowthGround,
                "skill.enemy.forest.shooter_flare" => CombatTargetVfxKind.FlareGround,
                "skill.enemy.catacombs.dead_king_cleave" => CombatTargetVfxKind.SmokeGround,
                "skill.enemy.catacombs.dead_king_wave" => CombatTargetVfxKind.SpectralLanding,
                "skill.enemy.catacombs.dead_king_scream" => CombatTargetVfxKind.ShadowGround,
                "skill.enemy.forest.ent_fist" => CombatTargetVfxKind.Tremor,
                "skill.enemy.forest.ent_branch" => CombatTargetVfxKind.WoodGround,
                "skill.enemy.forest.ent_ignition" or "skill.enemy.forest.bandit_leader_fire" => CombatTargetVfxKind.FireGround,
                _ => (CombatTargetVfxKind?)null
            };
            if (!kind.HasValue || skill.TargetModes.Count == 0) return;
            // These skills use mode zero. Snapshot its resolved geometry before any hit,
            // displacement, death or follow-up changes the board. Reuse gameplay shapes.
            var mode = PassiveEffectCombatExecutionSystem.ResolveMode(caster, skill, skill.TargetModes[0]);
            var aim = selected != null && selected.TryGetValue(0, out var chosen) ? chosen : null;
            var rotation = rotations != null && rotations.TryGetValue(0, out var turns) ? turns : 0;
            var targeting = new CombatTargetingService();
            IEnumerable<GridPosition> positions;
            if (mode.TargetType == TargetType.AreaAroundCaster)
                positions = CombatTargetingService.IsDirectionalShape(mode.AbsoluteRange.Shape)
                    ? aim.HasValue ? targeting.GetDirectionalRangePositions(session, caster, mode.AbsoluteRange,
                        aim.Value, rotationQuarterTurns: rotation) : System.Array.Empty<GridPosition>()
                    : targeting.GetPositionsInRange(session, caster.Position, mode.AbsoluteRange);
            else if (mode.TargetType == TargetType.AreaAroundTile && aim.HasValue)
                positions = targeting.GetRelativeRangePositions(session, caster, mode, aim.Value, rotation);
            else
                positions = targets != null && targets.TryGetValue(0, out var recipients)
                    ? recipients.Where(t => t != null && !StatusEffectSystem.IsInvisible(t)).Select(t => t.Position)
                    : System.Array.Empty<GridPosition>();
            if (skill.SkillId == "skill.enemy.forest.shooter_flare" && targets != null)
            {
                // Flare is TargetType.All in TWO modes (enemies and allies),
                // not a radius. Mark their union without inventing a floor circle.
                var flashed = targets.Where(pair => pair.Key is 0 or 1).SelectMany(pair => pair.Value)
                    .Where(t => t != null && !StatusEffectSystem.IsInvisible(t)).Distinct().ToArray();
                positions = flashed.Select(t => t.Position);
                foreach (var target in flashed)
                    feedback.Publish(new CombatFeedbackEvent(target, CombatFeedbackType.TargetEffect,
                        source: caster, targetVfx: CombatTargetVfxKind.FlareHead, actionId: actionId));
            }
            // Reserve the target cues before floor coverage reaches the renderer
            if (skill.SkillId == "skill.enemy.catacombs.dead_king_scream" && targets != null
                && targets.TryGetValue(0, out var screamedAt))
                foreach (var target in screamedAt.Where(t => t != null && !StatusEffectSystem.IsInvisible(t)).Distinct())
                    feedback.Publish(new CombatFeedbackEvent(target, CombatFeedbackType.TargetEffect,
                        source: caster, targetVfx: CombatTargetVfxKind.ShadowImpact, actionId: actionId));
            // budget. Sound is independent of either of the roar's status rolls.
            if (skill.SkillId == "skill.enemy.catacombs.savage_roar" && targets != null
                && targets.TryGetValue(0, out var heardBy))
                foreach (var target in heardBy.Where(t => t != null && !StatusEffectSystem.IsInvisible(t)).Distinct())
                    feedback.Publish(new CombatFeedbackEvent(target, CombatFeedbackType.TargetEffect,
                        source: caster, targetVfx: CombatTargetVfxKind.Shout, actionId: actionId));
            var origin = mode.TargetType == TargetType.AreaAroundTile ? aim ?? caster.Position : caster.Position;
            var surfaces = session.DungeonContext?.SurfaceMap;
            bool CanShowGround(GridPosition position)
            {
                if (!session.Grid.IsWalkable(position) && position != caster.Position) return false;
                if (surfaces?.HasElevatedSurfaces == true && surfaces.BlocksFloorSight(
                    DungeonBattleGridFactory.ToDungeonTraversalPosition(session.DungeonContext, origin),
                    DungeonBattleGridFactory.ToDungeonTraversalPosition(session.DungeonContext, position))) return false;
                return true;
            }
            HashSet<GridPosition> roarArea = skill.SkillId is "skill.enemy.catacombs.savage_roar" or "skill.enemy.forest.tree_plague_growth"
                ? new HashSet<GridPosition>(positions.Where(CanShowGround)) : null;
            foreach (var position in roarArea ?? positions.Distinct())
            {
                if (roarArea == null && !CanShowGround(position)) continue;
                // Large roar/plague ranges use their exact contour within the
                // existing renderer budget; recipients carry the interior cue.
                if (roarArea != null && roarArea.Contains(new(position.X - 1, position.Y, position.Level))
                    && roarArea.Contains(new(position.X + 1, position.Y, position.Level))
                    && roarArea.Contains(new(position.X, position.Y - 1, position.Level))
                    && roarArea.Contains(new(position.X, position.Y + 1, position.Level))) continue;
                feedback.Publish(new CombatFeedbackEvent(caster, CombatFeedbackType.TargetEffect,
                    position: position, source: caster, targetVfx: kind, actionId: actionId));
            }
        }

        public static void PublishImpact(CombatFeedbackBus feedback, BattleUnit caster, BattleUnit target,
            SkillData skill, SkillEffectData effect, bool succeeded, GridPosition impactPosition, int healthDamage,
            float delaySeconds = 0, string actionId = null)
        {
            if (feedback == null) return;
            CombatTargetVfxKind? cue = null;
            if (skill.SkillId == "skill.warrior.shout" && effect.EffectType == EffectType.ApplyStatus)
                cue = CombatTargetVfxKind.Shout;
            else if (effect.EffectType == EffectType.PaladinHeal && succeeded)
                cue = CombatTargetVfxKind.Heal;
            else if (effect.EffectType == EffectType.Push && succeeded && skill.SkillId == "skill.warrior.kick")
                cue = CombatTargetVfxKind.Blunt;
            else if (effect.EffectType == EffectType.Push && succeeded && skill.SkillId == "skill.enemy.catacombs.ancient_whip"
                && target.Position != impactPosition)
                cue = CombatTargetVfxKind.CutFragments;
            else if (effect.EffectType == EffectType.ModifyStat && succeeded && skill.SkillId == "skill.scout.smoke")
                cue = CombatTargetVfxKind.SmokeHead;
            else if (succeeded)
                cue = EnemySupportCue(skill, effect) ?? (skill.SkillId, effect.EffectType) switch
                {
                    ("skill.warrior.death_wish", EffectType.ModifyVar) => CombatTargetVfxKind.DeathWish,
                    ("skill.mage.offering", EffectType.ApplyUniqueBuff) => CombatTargetVfxKind.Offering,
                    ("skill.mage.tiring", EffectType.BlockResourceRegeneration) => CombatTargetVfxKind.Fatigue,
                    ("skill.scout.tonic", EffectType.ModifyStat) when effect.ModifierValue > 0 => CombatTargetVfxKind.Tonic,
                    ("skill.scout.tonic", EffectType.ModifyStat) when effect.ModifierValue < 0 => CombatTargetVfxKind.Fatigue,
                    ("skill.warrior.destruction", EffectType.MarkForDestruction) => CombatTargetVfxKind.DestructionMark,
                    ("skill.scout.weak_point", EffectType.MarkWeakPoint) => CombatTargetVfxKind.WeakPointMark,
                    ("skill.mage.sacrifice", EffectType.MageSacrifice) => CombatTargetVfxKind.Sacrifice,
                    ("skill.mage.ritual", EffectType.MageRitual) => CombatTargetVfxKind.HolyGrace,
                    ("skill.mage.shield", EffectType.Shield) => CombatTargetVfxKind.ArcaneWard,
                    ("skill.mage.magic_focus", EffectType.ApplyUniqueBuff) => CombatTargetVfxKind.ArcaneFocus,
                    ("skill.paladin.blessing", EffectType.PaladinBlessing) => CombatTargetVfxKind.HolyGrace,
                    ("skill.paladin.holy_regeneration", EffectType.PaladinRegeneration) => CombatTargetVfxKind.HolyGrace,
                    ("skill.paladin.reflection_shield", EffectType.ModifyStat) => CombatTargetVfxKind.HolyGrace,
                    ("skill.warrior.protection", EffectType.ProtectAlly) => CombatTargetVfxKind.HolyGrace,
                    ("skill.warrior.regroup", EffectType.ModifyActionValue) => CombatTargetVfxKind.Rally,
                    ("skill.warrior.bandage", EffectType.HealMissingHealthPercent or EffectType.RemoveStatus) => CombatTargetVfxKind.Heal,
                    ("skill.paladin.blinding_light", EffectType.ModifyStat) => CombatTargetVfxKind.HolyBlind,
                    ("skill.mage.fire_aura", EffectType.MageFireAura) => CombatTargetVfxKind.HolyGrace,
                    _ => null
                };
            if (effect.EffectType == EffectType.DealDamage)
            {
                if (skill.SkillId == "skill.paladin.transfusion" && succeeded) cue = CombatTargetVfxKind.Transfusion;
                else if (succeeded && skill.SkillId is "skill.enemy.catacombs.dead_king_blade" or "skill.enemy.catacombs.dead_king_wave")
                    cue = CombatTargetVfxKind.SpectralContact;
                else if (succeeded && skill.SkillId == "skill.enemy.forest.druid_nature_wrath"
                    && SummonSystem.CountOwnedSummons(caster.WarriorState.Session, caster, "forest.growth") > 0)
                    cue = CombatTargetVfxKind.RootContact;
                else if (succeeded && skill.SkillId is "skill.enemy.forest.ent_fist" or "skill.enemy.forest.ent_branch")
                    cue = CombatTargetVfxKind.WoodImpact;
                else if (succeeded && skill.SkillId is "skill.enemy.forest.ent_ignition" or "skill.enemy.forest.bandit_leader_fire")
                    cue = CombatTargetVfxKind.Fire;
                else if (succeeded && skill.SkillId == "skill.enemy.forest.tree_dark_roots")
                    cue = CombatTargetVfxKind.RootContact;
                else if (succeeded && skill.SkillId == "skill.enemy.forest.tree_plague_growth")
                    cue = CombatTargetVfxKind.ToxicContact;
                else if (succeeded && skill.SkillId == "skill.enemy.catacombs.life_drain")
                    cue = healthDamage > 0 ? CombatTargetVfxKind.LifeSiphon : CombatTargetVfxKind.SpectralContact;
                else if (succeeded && skill.SkillId is "skill.enemy.catacombs.manic_bite" or "skill.enemy.forest.wolf_bite")
                    cue = healthDamage > 0 && HasBlood(target) ? CombatTargetVfxKind.FangContact : CombatTargetVfxKind.CutFragments;
                else if (succeeded && skill.SkillId is "skill.enemy.catacombs.spectral_knight_strike" or "skill.enemy.catacombs.fright")
                    cue = CombatTargetVfxKind.SpectralContact;
                else if (succeeded && skill.SkillId == "skill.enemy.catacombs.ancient_incantation")
                    cue = CombatTargetVfxKind.AncientContact;
                else if (skill.SkillId == "skill.mage.fireball") cue = CombatTargetVfxKind.Fire;
                else if (succeeded && (skill.SkillId is "skill.mage.energy_orb" or "skill.mage.energy_split"))
                    cue = CombatTargetVfxKind.EnergyImpact;
                else if (succeeded && skill.SkillId == "skill.mage.fire_arrow") cue = CombatTargetVfxKind.Fire;
                else if (succeeded && (skill.SkillId is "skill.paladin.verdict" or "skill.paladin.blinding_light"))
                    cue = CombatTargetVfxKind.HolyImpact;
                else if (succeeded && IsPierce(skill.SkillId))
                    cue = healthDamage > 0 && HasBlood(target) ? CombatTargetVfxKind.PierceBlood : CombatTargetVfxKind.CutFragments;
                else if (succeeded && (skill.SkillId is "skill.warrior.right_hook" or "skill.paladin.crushing_blow"
                    or "skill.enemy.catacombs.skeleton_slayer_crushing" or "skill.enemy.catacombs.skeleton_slayer_bone_crush"
                    or "skill.enemy.catacombs.bone_club"))
                    cue = CombatTargetVfxKind.Blunt;
                else if (succeeded && IsCut(skill.SkillId))
                    cue = healthDamage > 0 && HasBlood(target) ? CombatTargetVfxKind.CutBlood : CombatTargetVfxKind.CutFragments;
            }
            if (cue.HasValue)
                feedback.Publish(new CombatFeedbackEvent(target, CombatFeedbackType.TargetEffect,
                    position: impactPosition, source: caster, targetVfx: cue, cueDelaySeconds: delaySeconds, actionId: actionId));
            if (succeeded && skill.SkillId == "skill.mage.sacrifice" && effect.EffectType == EffectType.MageSacrifice)
                feedback.Publish(new CombatFeedbackEvent(caster, CombatFeedbackType.TargetEffect,
                    source: caster, targetVfx: CombatTargetVfxKind.ArcaneFocus, actionId: actionId));
            if (succeeded && skill.SkillId == "skill.enemy.catacombs.bone_guardian"
                && effect.EffectType == EffectType.ApplyUniqueBuff && target != caster)
                feedback.Publish(new CombatFeedbackEvent(caster, CombatFeedbackType.TargetEffect,
                    source: caster, targetVfx: CombatTargetVfxKind.BoneWard, actionId: actionId));
        }

        // One support signal per actual recipient, even for compound buffs.
        public static CombatTargetVfxKind? EnemySupportCue(SkillData skill, SkillEffectData effect) =>
            (skill.SkillId, effect.EffectType) switch
            {
                ("skill.enemy.forest.ent_bark", EffectType.ModifyStat) => CombatTargetVfxKind.BarkWard,
                ("skill.enemy.forest.ent_roots", EffectType.ApplyStatus) => CombatTargetVfxKind.RootContact,
                ("skill.enemy.forest.bandit_leader_order", EffectType.ModifyVar or EffectType.ModifyStat or EffectType.ModifyActionValue) => CombatTargetVfxKind.BanditOrder,
                ("skill.enemy.catacombs.battle_fervor", EffectType.ModifyStat or EffectType.ModifyActionValue) => CombatTargetVfxKind.Rally,
                ("skill.enemy.catacombs.bone_fervor", EffectType.ModifyVar or EffectType.ModifyStat or EffectType.ModifyActionValue) => CombatTargetVfxKind.Rally,
                ("skill.enemy.catacombs.bone_guardian", EffectType.ApplyUniqueBuff) => CombatTargetVfxKind.BoneWard,
                ("skill.enemy.catacombs.blood_scent", EffectType.ModifyStat) => CombatTargetVfxKind.DeathWish,
                ("skill.enemy.catacombs.quiver_of_envy", EffectType.ModifyStat or EffectType.ModifyVar) => CombatTargetVfxKind.Tonic,
                ("skill.enemy.forest.archer_hawkeye", EffectType.ApplyUniqueBuff) => CombatTargetVfxKind.Tonic,
                ("skill.enemy.forest.wolf_lick_wounds", EffectType.Heal) => CombatTargetVfxKind.Heal,
                ("skill.enemy.forest.druid_healing_pulse", EffectType.Heal) => CombatTargetVfxKind.Heal,
                ("skill.enemy.catacombs.vomit", EffectType.ApplyStatus or EffectType.ModifyStat) => CombatTargetVfxKind.ToxicContact,
                ("skill.enemy.forest.poacher_poison_bag" or "skill.enemy.forest.druid_poison_pulse", EffectType.ApplyStatus) => CombatTargetVfxKind.ToxicContact,
                ("skill.enemy.forest.tree_dark_roots", EffectType.ApplyStatus) => CombatTargetVfxKind.ToxicContact,
                ("skill.enemy.forest.tree_plague_growth", EffectType.ExtendStatusDuration) => CombatTargetVfxKind.ToxicContact,
                ("skill.enemy.forest.tree_binding_roots", EffectType.ApplyStatus) => CombatTargetVfxKind.RootContact,
                ("skill.enemy.forest.shooter_flare", EffectType.RemoveStatus) => CombatTargetVfxKind.HolyGrace,
                ("skill.enemy.catacombs.dead_king_command", EffectType.ModifyVar) => CombatTargetVfxKind.RoyalCommand,
                ("skill.enemy.catacombs.dead_king_wave", EffectType.ModifyStat) => CombatTargetVfxKind.SpectralContact,
                ("skill.enemy.catacombs.dead_king_blade", EffectType.ModifyVar) => CombatTargetVfxKind.SpectralContact,
                ("skill.enemy.catacombs.skeleton_slayer_hammer", EffectType.ApplyStatus) => CombatTargetVfxKind.RootContact,
                _ => null
            };

        public static void PublishSummon(CombatFeedbackBus feedback, BattleUnit caster, BattleUnit summoned, SkillData skill, string actionId)
        {
            if (feedback == null || summoned == null) return;
            var kind = skill.SkillId switch
            {
                "skill.enemy.forest.druid_healing_growth" => (CombatTargetVfxKind?)CombatTargetVfxKind.GrowthGround,
                "skill.enemy.forest.druid_poison_growth" => CombatTargetVfxKind.ToxicGround,
                "skill.enemy.catacombs.dead_king_guard" => CombatTargetVfxKind.SpectralReveal,
                _ => null
            };
            if (kind.HasValue) feedback.Publish(new CombatFeedbackEvent(summoned, CombatFeedbackType.TargetEffect,
                position: summoned.Position, source: caster, targetVfx: kind, actionId: actionId));
        }

        public static bool IsBearTrap(SummonedCombatMapObject trap) =>
            trap?.SourceSkillId == "skill.enemy.forest.poacher_bear_trap";
        public static bool HasTriggerPresentation(SummonedCombatMapObject trap) =>
            trap?.Definition != null && (trap.Definition.TriggerSprite != null || IsBearTrap(trap));
        // Entry activation has no casting pose. Placement publishes no cue;
        // the standing object's existing visibility rule is unchanged.
        public static float TriggerDuration(SummonedCombatMapObject trap) =>
            IsBearTrap(trap) ? .5f : .18f;

        public static void PublishVisibilityTransition(CombatFeedbackBus feedback, BattleUnit caster, BattleUnit target,
            SkillData skill, SkillEffectData effect, bool succeeded, bool wasInvisible, GridPosition before,
            string actionId)
        {
            if (feedback == null || !succeeded || effect.StatusId != StatusEffectIds.Invisibility) return;
            var invisible = StatusEffectSystem.IsInvisible(target);
            if (effect.EffectType == EffectType.ApplyStatus && !wasInvisible && invisible)
            {
                var kind = skill.SkillId switch
                {
                    "skill.enemy.catacombs.dematerialize" => (CombatTargetVfxKind?)CombatTargetVfxKind.SpectralDeparture,
                    "skill.enemy.forest.bandit_ambush" => CombatTargetVfxKind.SmokeGround,
                    _ => null
                };
                // Ground snapshot, not an actor-following beacon at its hidden position.
                if (kind.HasValue) feedback.Publish(new CombatFeedbackEvent(target, CombatFeedbackType.TargetEffect,
                    position: before, source: caster, targetVfx: kind, actionId: actionId));
            }
            else if (skill.SkillId == "skill.enemy.catacombs.materialize" && effect.EffectType == EffectType.RemoveStatus
                && wasInvisible && !invisible)
            {
                feedback.Publish(new CombatFeedbackEvent(target, CombatFeedbackType.TargetEffect,
                    source: caster, targetVfx: CombatTargetVfxKind.SpectralReveal, actionId: actionId));
            }
        }

        public static float ChainDelay(SkillData skill, SkillEffectData effect, BattleUnit target,
            IReadOnlyDictionary<int, IReadOnlyList<BattleUnit>> targets)
        {
            if (skill.SkillId is not ("skill.scout.ricochet" or "skill.paladin.blinding_light")
                || targets == null || !targets.TryGetValue(effect.TargetModeIndex, out var chain)) return 0;
            for (var i = 0; i < chain.Count; i++) if (chain[i] == target) return i * .16f;
            return 0;
        }

        public static bool IsGround(CombatTargetVfxKind kind) => kind is CombatTargetVfxKind.Tremor
            or CombatTargetVfxKind.EnergyGround or CombatTargetVfxKind.FireGround
            or CombatTargetVfxKind.SmokeGround or CombatTargetVfxKind.ArrowGround or CombatTargetVfxKind.PullDust
            or CombatTargetVfxKind.KnifeRecover or CombatTargetVfxKind.SpectralLanding or CombatTargetVfxKind.SpectralDeparture
            or CombatTargetVfxKind.ToxicGround or CombatTargetVfxKind.GrowthGround or CombatTargetVfxKind.FlareGround or CombatTargetVfxKind.ShadowGround
            or CombatTargetVfxKind.WoodGround or CombatTargetVfxKind.MineBurst or CombatTargetVfxKind.TrapGround;

        private static bool IsPierce(string skill) => skill is "skill.scout.aimed_throw"
            or "skill.scout.poisoned_dagger" or "skill.scout.ricochet" or "skill.scout.quick_strikes"
            or "skill.enemy.forest.shooter_shot" or "skill.enemy.forest.shooter_piercing_bolt"
            or "skill.enemy.forest.archer_arrow" or "skill.enemy.forest.archer_arrow_rain"
            or "skill.enemy.catacombs.bone_bow" or "skill.enemy.catacombs.burning_arrow"
            or "skill.enemy.forest.bandit_backstab" or "skill.enemy.forest.bandit_leader_strike";

        private static bool IsCut(string skill) => skill is "skill.warrior.cut"
            or "skill.warrior.bleeding_destruction" or "skill.enemy.forest.bandit_leader_cut" or "skill.warrior.immobilize"
            or "skill.warrior.whirlwind" or "skill.enemy.catacombs.bone_sword" or "skill.enemy.bone_sword"
            or "skill.enemy.catacombs.cross_cut" or "skill.enemy.catacombs.rend"
            or "skill.enemy.forest.bandit_knife" or "skill.enemy.forest.poacher_knife" or "skill.enemy.catacombs.dead_king_cleave";

        public static void PublishMovement(CombatFeedbackBus feedback, BattleUnit caster, BattleUnit target,
            SkillData skill, SkillEffectData effect, bool succeeded, GridPosition before, GridPosition casterBefore,
            IReadOnlyList<GridPosition> committedPath = null, string actionId = null)
        {
            if (feedback == null || !succeeded) return;
            if (effect.EffectType == EffectType.Charge && skill.SkillId is "skill.enemy.forest.wolf_leap"
                or "skill.enemy.catacombs.disorienting_leap" or "skill.enemy.catacombs.fright")
            {
                // Charge can return success when a barrier stops it. Only mark
                // committed movement, never the desired target or hidden endpoint.
                if (caster.Position == casterBefore || StatusEffectSystem.IsInvisible(caster)) return;
                if (skill.SkillId == "skill.enemy.catacombs.disorienting_leap")
                    Emit(casterBefore.X, casterBefore.Y, casterBefore.Level, CombatTargetVfxKind.PullDust);
                Emit(caster.Position.X, caster.Position.Y, caster.Position.Level,
                    skill.SkillId == "skill.enemy.catacombs.fright" ? CombatTargetVfxKind.SpectralLanding : CombatTargetVfxKind.PullDust);
            }
            else if (effect.EffectType == EffectType.Push && skill.SkillId is "skill.enemy.catacombs.ancient_whip" or "skill.enemy.forest.ent_branch"
                && target.Position != before && !StatusEffectSystem.IsInvisible(target))
            {
                foreach (var position in committedPath ?? new[] { before, target.Position })
                    Emit(position.X, position.Y, position.Level, CombatTargetVfxKind.PullDust);
            }
            else if (effect.EffectType == EffectType.Pull && skill.SkillId is "skill.scout.hook" or "skill.paladin.grounding_wave")
            {
                // Only committed cells, never the requested distance through a blocker.
                foreach (var position in committedPath ?? new[] { before, target.Position })
                    Emit(position.X, position.Y, position.Level, CombatTargetVfxKind.PullDust);
            }
            else if (effect.EffectType == EffectType.SwapPositions && skill.SkillId == "skill.scout.swap")
            {
                Emit(before.X, before.Y, before.Level, CombatTargetVfxKind.SmokeGround);
                Emit(casterBefore.X, casterBefore.Y, casterBefore.Level, CombatTargetVfxKind.SmokeGround);
            }
            else if (effect.EffectType == EffectType.Teleport && skill.SkillId == "skill.scout.shadow")
                Emit(casterBefore.X, casterBefore.Y, casterBefore.Level, CombatTargetVfxKind.SmokeGround);

            void Emit(int x, int y, int level, CombatTargetVfxKind kind) => feedback.Publish(new CombatFeedbackEvent(
                caster, CombatFeedbackType.TargetEffect, position: new GridPosition(x, y, level), source: caster, targetVfx: kind, actionId: actionId));
        }

        private static bool HasBlood(BattleUnit target)
        {
            var id = target?.Character?.Definition?.Id ?? "";
            // Shipped skeleton and wraith IDs do not all contain their species.
            if (id is "enemy.catacombs.swordsman" or "enemy.catacombs.archer" or "enemy.catacombs.defender"
                or "enemy.catacombs.occultist" or "enemy.catacombs.wraith" or "enemy.catacombs.spectral_knight"
                or "enemy.forest.infected_tree") return false;
            return !id.Contains("skeleton") && !id.Contains("ghost") && !id.Contains("spirit")
                && !id.Contains("dead_king") && !id.Contains(".ent") && !id.Contains("golem");
        }
    }
}

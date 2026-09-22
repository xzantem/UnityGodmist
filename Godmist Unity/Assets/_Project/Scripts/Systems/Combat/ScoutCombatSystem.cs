using System;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public static partial class ScoutCombatSystem
    {
        public static KnifeCombatMapObject RecoverableKnife(CombatSession session, BattleUnit owner, Godmist.Data.Skills.SkillData skill, GridPosition? position)
        {
            if (session?.Grid == null || owner?.Character == null || skill?.CanRecoverKnives != true
                || !position.HasValue || skill.TargetModes.Count != 1 || owner.AvailableKnives >= owner.MaxKnives) return null;
            var mode = PassiveEffectCombatExecutionSystem.ResolveMode(owner,skill,skill.TargetModes[0]);
            if (!CombatTargetingService.IsPositionWithinRange(owner.Position,position.Value,mode.AbsoluteRange)
                || skill.RequiresLineOfSight && !new CombatLineOfSightService().HasLineOfSight(session,owner.Position,position.Value)) return null;
            return session.MapObjects.OfType<KnifeCombatMapObject>().FirstOrDefault(k=>k.Position==position.Value && k.OwnerCharacterId==owner.Character.CharacterId);
        }
        public static bool RecoverKnife(CombatSession session, BattleUnit owner, Godmist.Data.Skills.SkillData skill, GridPosition? position)
        {
            var knife=RecoverableKnife(session,owner,skill,position);
            if(knife==null)return false;
            // One physical map entry is recovered once; no accuracy/status roll.
            var recovered = Math.Min(owner.MaxKnives - owner.AvailableKnives, knife.KnifeCount);
            owner.AvailableKnives += recovered;
            knife.KnifeCount -= recovered;
            if (knife.KnifeCount == 0) session.MapObjects.Remove(knife);
            return recovered > 0;
        }
        public const string AssassinTree = "tree.scout.assassin";
        public static int AssassinRank(BattleUnit owner, string node) => Rank(owner, AssassinTree, "node.scout.assassin." + node);
        public static bool TryEmbedKnife(CombatSession session, string actionId, BattleUnit owner, BattleUnit target, AttackType attackType, Func<float> roll)
        {
            var rank = AssassinRank(owner, "sharp_knife");
            if (session == null || string.IsNullOrEmpty(actionId) || target?.Character?.LifeState != CharacterLifeState.Alive
                || attackType != AttackType.Ranged || rank == 0
                || !session.PendingScoutKnifeActions.TryGetValue(actionId, out var pending)
                || pending.Owner != owner || pending.Count <= 0) return false;
            var chance = .15f + .05f * Math.Min(2, AssassinRank(owner, "keen_eye"));
            if (roll() >= chance) return false;
            pending.Count--;
            target.ScoutState.Knives.Add(new EmbeddedScoutKnife { Owner = owner, BleedMultiplier = rank == 1 ? .6f : .9f });
            return true;
        }
        public static void FinishKnifeAction(CombatSession session, string actionId)
        {
            if (session == null || !session.PendingScoutKnifeActions.Remove(actionId, out var pending)) return;
            for(var i=0;i<pending.Count;i++) DropKnife(session,pending.Owner,pending.DropPosition);
        }
        static void DropKnife(CombatSession session, BattleUnit owner, GridPosition position)
        {
            if (!ScoutKnifeSystem.TryFindRandomDropPosition(session, position, out var drop)) drop = position;
            session.MapObjects.Add(new KnifeCombatMapObject("knife."+Guid.NewGuid().ToString("N"),drop,owner.Character.CharacterId,1));
        }
        public static void ReleaseKnivesOnDeath(CombatSession session, BattleUnit target)
        {
            foreach(var knife in target.ScoutState.Knives) DropKnife(session,knife.Owner,target.Position);
            target.ScoutState.Knives.Clear();
            target.ScoutState.AngerBySource.Clear();
        }
        public static bool CanExtractKnife(CombatSession session, BattleUnit owner, BattleUnit target)
        {
            return session?.Grid != null && session.ActiveUnit == owner && owner?.Character?.LifeState == CharacterLifeState.Alive
                && owner.IsPlayerSide && target?.Character?.LifeState == CharacterLifeState.Alive
                && session.Units.Contains(owner) && session.Units.Contains(target)
                && !StatusEffectSystem.IsHardCrowdControlled(owner) && !StatusEffectSystem.IsSleeping(owner)
                && Math.Abs(owner.Position.X-target.Position.X)+Math.Abs(owner.Position.Y-target.Position.Y)==1
                && session.Grid.CanMoveBetween(owner.Position,target.Position)
                && owner.AvailableKnives < owner.MaxKnives && target.ScoutState.Knives.Any(k=>k.Owner==owner);
        }
        public static bool TryExtractKnife(CombatSession session, BattleUnit owner, BattleUnit target, StatusEffectSystem statuses)
        {
            if (!CanExtractKnife(session,owner,target) || statuses == null) return false;
            var knife=target.ScoutState.Knives.First(k=>k.Owner==owner);
            target.ScoutState.Knives.Remove(knife);owner.AvailableKnives++;
            statuses.ApplyStatus(owner,target,Godmist.Core.Constants.StatusEffectIds.Bleed,.9f,3,1,knife.BleedMultiplier,0f,"scout.sharp_knife");
            return true;
        }
        public const string BladeTree = "tree.scout.blade_dancer";
        public static int Rank(BattleUnit owner, string tree, string node)
        {
            var progress = owner?.Character?.SkillTreeProgress?.FirstOrDefault(x => x.TreeId == tree);
            return Mathf.Clamp(progress?.NodeInvestments?.FirstOrDefault(x => x.NodeId == node)?.Ranks ?? 0, 0, 3);
        }
        public static int BladeRank(BattleUnit owner, string node) => Rank(owner, BladeTree, "node.scout.blade_dancer." + node);
        public static bool IsDirect(DamageType type) => type != DamageType.Bleed && type != DamageType.Poison && type != DamageType.Burn;
        public static void RecordHit(BattleUnit source, BattleUnit target, DamageType type)
        {
            if (source?.Character == null || target?.Character == null || source.IsPlayerSide == target.IsPlayerSide
                || !IsDirect(type) || BladeRank(source, "anger") == 0 || target.Character.LifeState != CharacterLifeState.Alive) return;
            AddAnger(source, target);
        }
        public static void RecordDamageTaken(BattleUnit source, BattleUnit target, DamageType type)
        {
            if (source?.Character == null || target?.Character == null || source.IsPlayerSide == target.IsPlayerSide
                || !IsDirect(type) || BladeRank(target, "revenge") == 0 || BladeRank(target, "anger") == 0
                || source.Character.LifeState != CharacterLifeState.Alive) return;
            AddAnger(target, source);
        }
        static void AddAnger(BattleUnit owner, BattleUnit target)
        {
            var rank = BladeRank(owner, "mental_control");
            var amount = 10 + (rank == 0 ? 0 : rank * 2 - 1);
            target.ScoutState.AngerBySource.TryGetValue(owner, out var current);
            target.ScoutState.AngerBySource[owner] = Math.Min(100, current + amount);
        }
        public static float ReceivedDamageMultiplier(BattleUnit source, BattleUnit target)
        {
            if (source?.Character == null || target?.Character == null) return 1f;
            var bonus = 0f;
            foreach (var entry in target.ScoutState.AngerBySource)
            {
                if (entry.Key.IsPlayerSide != source.IsPlayerSide) continue;
                var strength = .0015f + .0005f * Math.Min(2, BladeRank(entry.Key, "strong_insult"));
                // Same named debuff does not multiply recursively or stack between Scouts.
                bonus = Mathf.Max(bonus, entry.Value * strength);
            }
            return 1f + bonus;
        }
    }
}

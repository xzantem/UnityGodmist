using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;

namespace Godmist.Systems.Combat
{
    public enum SkillUseBlockReason
    {
        None, InvalidActor, RequiredStatus, Immobilized, CrowdControlled, NotActiveTurn,
        MissingEffects, InvalidToggle, MixedPlacement, ToggleAlreadyUsed, WarriorRequirement,
        NotEnoughActionPoints, MissingResource, WrongResource, NotEnoughResource, NotEnoughKnives,
        EnemyTurn, ActionInProgress, MissingSummon
    }

    public readonly struct SkillSelectionAvailability
    {
        public readonly SkillUseBlockReason Reason;
        public readonly int ActionCost;
        public readonly bool IsActiveToggle;
        public bool CanSelect => Reason == SkillUseBlockReason.None;
        public SkillSelectionAvailability(SkillUseBlockReason reason, int cost, bool activeToggle)
        { Reason = reason; ActionCost = cost; IsActiveToggle = activeToggle; }
    }

    public partial class SkillExecutionSystem
    {
        // Target-independent execution rules. Selection must not reject a skill
        // solely because the eventual target may require an automatic approach.
        public static SkillUseBlockReason GetCasterBlockReason(CombatSession session, BattleUnit caster,
            SkillData skill, bool deactivating, bool requireCombatContext = true)
        {
            if (caster?.Character == null || skill == null || caster.Character.LifeState != CharacterLifeState.Alive)
                return SkillUseBlockReason.InvalidActor;
            if (!string.IsNullOrWhiteSpace(skill.RequiredCasterStatusId)
                && !StatusEffectSystem.HasStatus(caster, skill.RequiredCasterStatusId)) return SkillUseBlockReason.RequiredStatus;
            if (StatusEffectSystem.IsImmobilized(caster)
                && (ContainsMobilityEffect(skill) || skill.Effects?.Any(e => e?.EffectType == EffectType.SwapPositions) == true))
                return SkillUseBlockReason.Immobilized;
            if (StatusEffectSystem.IsHardCrowdControlled(caster)) return SkillUseBlockReason.CrowdControlled;
            if (requireCombatContext && (session == null || session.Outcome != CombatOutcomeState.Ongoing
                || session.ActiveUnit != caster && session.EntryActionCaster != caster || !session.Units.Contains(caster)))
                return SkillUseBlockReason.NotActiveTurn;
            if (skill.Effects == null || skill.Effects.Count == 0) return SkillUseBlockReason.MissingEffects;
            if (!deactivating && skill.IsToggle && !ToggleSkillRules.IsValid(skill)) return SkillUseBlockReason.InvalidToggle;
            if (ContainsEffectType(skill, EffectType.CreateObject) && ContainsEffectType(skill, EffectType.Summon))
                return SkillUseBlockReason.MixedPlacement;
            if (!deactivating && skill.IsToggle && caster.ToggleSkillIdsUsedThisTurn?.Contains(skill.SkillId) == true)
                return SkillUseBlockReason.ToggleAlreadyUsed;
            if (!deactivating && !WarriorCombatSystem.CanActivate(caster, skill)) return SkillUseBlockReason.WarriorRequirement;
            return SkillUseBlockReason.None;
        }

        public static SkillUseBlockReason GetPaymentBlockReason(BattleUnit caster, SkillData skill, int actionCost, bool recoveringKnife)
        {
            if (caster.CurrentActionPoints < actionCost) return SkillUseBlockReason.NotEnoughActionPoints;
            var resourceCost = PassiveEffectCombatExecutionSystem.ResolveResourceCost(caster, skill, recoveringKnife);
            var resource = caster.Character.ResourcePool;
            if (resourceCost > 0 && resource == null) return SkillUseBlockReason.MissingResource;
            if (resourceCost > 0 && resource.ResourceType != skill.ResourceType) return SkillUseBlockReason.WrongResource;
            if (resource != null && resource.CurrentValue < resourceCost && !WarriorCombatSystem.CanOverspend(caster, skill, resourceCost))
                return SkillUseBlockReason.NotEnoughResource;
            return SkillUseBlockReason.None;
        }

        public static SkillSelectionAvailability GetSelectionAvailability(CombatSession session, BattleUnit caster, SkillData skill, bool busy = false)
        {
            if (caster?.Character == null || skill == null) return new(SkillUseBlockReason.InvalidActor, 0, false);
            var activeToggle = skill?.IsToggle == true && caster?.ActiveToggleSkillIds?.Contains(skill.SkillId) == true;
            var cost = activeToggle ? 0 : PaladinCombatSystem.PreviewHealCost(session, caster, skill, null,
                CombatActionCostCalculator.CalculateSkillCost(caster, skill));
            SkillSelectionAvailability Result(SkillUseBlockReason reason) => new(reason, cost, activeToggle);
            if (caster?.IsPlayerSide != true) return Result(SkillUseBlockReason.EnemyTurn);
            var block = GetCasterBlockReason(session, caster, skill, activeToggle, session != null);
            if (block != SkillUseBlockReason.None) return Result(block);
            if (busy || session?.MembershipChangePending == true) return Result(SkillUseBlockReason.ActionInProgress);
            if (!HasRequiredOwnedSummons(session, caster, skill)) return Result(SkillUseBlockReason.MissingSummon);
            if (activeToggle) return Result(SkillUseBlockReason.None);
            block = GetPaymentBlockReason(caster, skill, cost, false);
            if (block == SkillUseBlockReason.None && !ScoutKnifeSystem.CanPay(caster, skill) && !skill.FreeKnivesInMelee)
                block = SkillUseBlockReason.NotEnoughKnives;
            // Recovery is a separate use of the same skill, with its existing
            // discount and resource cost. Query it without consuming a map object.
            if (block != SkillUseBlockReason.None && skill.CanRecoverKnives && session?.MapObjects != null
                && session.MapObjects.OfType<KnifeCombatMapObject>().Any(k => ScoutCombatSystem.RecoverableKnife(session, caster, skill, k.Position) != null))
            {
                var recoveryCost = CombatActionCostCalculator.CalculateSkillCost(caster, skill, true);
                if (GetPaymentBlockReason(caster, skill, recoveryCost, true) == SkillUseBlockReason.None)
                { cost = recoveryCost; block = SkillUseBlockReason.None; }
            }
            return Result(block);
        }

        private static bool HasRequiredOwnedSummons(CombatSession session, BattleUnit caster, SkillData skill) =>
            skill?.Effects == null || !skill.Effects.Any(effect => effect != null
                && !string.IsNullOrWhiteSpace(effect.DamagePerOwnedSummonGroup)
                && SummonSystem.CountOwnedSummons(session, caster, effect.DamagePerOwnedSummonGroup) == 0);

        private void ReportCasterBlock(SkillUseBlockReason reason, BattleUnit caster, SkillData skill)
        {
            switch (reason)
            {
                case SkillUseBlockReason.Immobilized: battleLog?.Invoke($"{caster.Character.Definition.DisplayName} cannot use a mobility skill while immobilized."); break;
                case SkillUseBlockReason.CrowdControlled: battleLog?.Invoke($"{caster.Character.Definition.DisplayName} cannot use skills while crowd controlled."); break;
                case SkillUseBlockReason.MissingEffects: UnityEngine.Debug.LogWarning($"Skill '{skill.SkillId}' has no effects."); break;
                case SkillUseBlockReason.InvalidToggle: UnityEngine.Debug.LogWarning($"Toggle skill '{skill.SkillId}' contains an unsupported effect."); break;
                case SkillUseBlockReason.MixedPlacement: UnityEngine.Debug.LogWarning($"Skill '{skill.SkillId}' mixes CreateObject and Summon. This is not supported in v1."); break;
                case SkillUseBlockReason.ToggleAlreadyUsed: battleLog?.Invoke("Toggle can be activated only once per turn."); break;
                case SkillUseBlockReason.WarriorRequirement: battleLog?.Invoke(skill.SkillId == WarriorDischargeRules.SkillId ? "Wyładowanie wymaga co najmniej 20 furii." : "Za mało wściekłości do podtrzymania Pragnienia Śmierci."); break;
            }
        }
    }
}

using System;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Effects;
using Godmist.Runtime.Combat;

namespace Godmist.Systems.Combat
{
    public sealed class SummonedObjectEffectExecutor
    {
        private readonly Action<string> battleLog;
        private readonly CombatDamageSystem damageSystem;
        private readonly CombatRecoverySystem recoverySystem;
        private readonly CombatShieldSystem shieldSystem;
        private readonly StatusEffectSystem statusEffectSystem;
        private readonly ModifyStatSystem modifyStatSystem;
        private readonly ModifyVarSystem modifyVarSystem;
        private readonly CombatFeedbackBus feedbackBus;

        public SummonedObjectEffectExecutor(
            Action<string> battleLog = null,
            CombatDamageSystem damageSystem = null,
            CombatRecoverySystem recoverySystem = null,
            CombatShieldSystem shieldSystem = null,
            StatusEffectSystem statusEffectSystem = null,
            ModifyStatSystem modifyStatSystem = null,
            ModifyVarSystem modifyVarSystem = null,
            CombatFeedbackBus feedbackBus = null)
        {
            this.battleLog = battleLog;
            this.damageSystem = damageSystem ?? new CombatDamageSystem(battleLog);
            this.recoverySystem = recoverySystem ?? new CombatRecoverySystem(battleLog);
            this.shieldSystem = shieldSystem ?? new CombatShieldSystem(battleLog);
            this.statusEffectSystem = statusEffectSystem ?? new StatusEffectSystem(battleLog);
            this.modifyStatSystem = modifyStatSystem ?? new ModifyStatSystem(battleLog);
            this.modifyVarSystem = modifyVarSystem ?? new ModifyVarSystem(battleLog);
            this.feedbackBus = feedbackBus;
        }

        public void ExecuteTurn(
            CombatSession session,
            SummonedCombatMapObject summonedObject)
        {
            ExecuteEffects(
                session,
                summonedObject,
                includeTriggeredEffects: true,
                includePersistentModifiers: true);
        }

        public bool ExecuteEntry(CombatSession session, SummonedCombatMapObject trap, BattleUnit target)
        {
            if (session == null || trap?.Definition?.TriggersOnEntry != true || target?.Character == null
                || target.Character.LifeState != CharacterLifeState.Alive || !session.SummonedObjects.Contains(trap)) return false;
            var source = session.Units.FirstOrDefault(unit => unit?.Character?.CharacterId == trap.CreatorCharacterId);
            if (source == null || trap.Definition.EffectTargetingType == TargetingType.Enemies && source.IsPlayerSide == target.IsPlayerSide
                || trap.Definition.EffectTargetingType == TargetingType.Allies && source.IsPlayerSide != target.IsPlayerSide) return false;
            if(trap.Definition.EntryAffectsArea){ExecuteDetonation(session,trap);return true;}
            PublishTrigger(trap);
            for (var i = 0; i < trap.Definition.Effects.Count; i++)
                if (trap.Definition.Effects[i] != null)
                    ExecuteEffect(session, source, trap, trap.Definition.Effects[i], i, target, BuildEffectSourceId(trap, i));
            return true;
        }

        public void ApplyPersistentEffects(
            CombatSession session,
            SummonedCombatMapObject summonedObject)
        {
            ExecuteEffects(session,summonedObject,includeTriggeredEffects:false,includePersistentModifiers:true);
        }

        public void ExecuteDetonation(CombatSession session,SummonedCombatMapObject trap)
        {
            if(session==null || trap?.Definition==null || !session.SummonedObjects.Contains(trap))return;
            var source=session.Units.FirstOrDefault(u=>u?.Character?.CharacterId==trap.CreatorCharacterId);
            if(source==null)return;
            var presentationAction = CombatBossVfxFeedback.BeginTrapResolution(feedbackBus, source, trap);
            PublishTrigger(trap);
            foreach(var target in GetTargets(session,source,trap,trap.Definition.EffectTargetingType).ToArray())
                for(var i=0;i<trap.Definition.Effects.Count;i++)
                    if(trap.Definition.Effects[i]!=null)ExecuteEffect(session,source,trap,trap.Definition.Effects[i],i,target,BuildEffectSourceId(trap,i),presentationAction);
            CombatBossVfxFeedback.PublishTrapArea(feedbackBus,session,source,trap,presentationAction);
        }

        private void PublishTrigger(SummonedCombatMapObject trap)
        {
            if (CombatTargetVfxFeedback.HasTriggerPresentation(trap))
                feedbackBus?.Publish(new CombatFeedbackEvent(null, CombatFeedbackType.MapObjectTriggered,
                    position: trap.Position, mapObject: trap));
        }

        private void ExecuteEffects(
            CombatSession session,
            SummonedCombatMapObject summonedObject,
            bool includeTriggeredEffects,
            bool includePersistentModifiers)
        {
            if (session == null
                || summonedObject?.Definition == null
                || !session.SummonedObjects.Contains(summonedObject))
            {
                return;
            }

            if (PaladinCombatSystem.IsField(summonedObject))
            {
                PaladinCombatSystem.FieldEffects(session,summonedObject,includeTriggeredEffects,damageSystem,statusEffectSystem,feedbackBus);
                return;
            }
            var source = session.Units.FirstOrDefault(unit =>
                unit?.Character != null
                && string.Equals(
                    unit.Character.CharacterId,
                    summonedObject.CreatorCharacterId,
                    StringComparison.OrdinalIgnoreCase));

            if (source?.Character == null
                || summonedObject.Definition.Effects == null)
            {
                return;
            }

            for (var effectIndex = 0;
                effectIndex < summonedObject.Definition.Effects.Count;
                effectIndex++)
            {
                var effect = summonedObject.Definition.Effects[effectIndex];

                if (effect == null)
                {
                    continue;
                }

                var isPersistentModifier =
                    effect.EffectType == SummonedObjectEffectType.ModifyStat
                    || effect.EffectType == SummonedObjectEffectType.ModifyVar;

                if (isPersistentModifier && !includePersistentModifiers)
                {
                    continue;
                }

                if (!isPersistentModifier && !includeTriggeredEffects)
                {
                    continue;
                }

                var sourceId = BuildEffectSourceId(summonedObject, effectIndex);

                if (isPersistentModifier)
                {
                    RemoveModifierFromTargetsOutsideRange(
                        session,
                        source,
                        summonedObject,
                        effect,
                        sourceId);
                }

                var targets = GetTargets(
                    session,
                    source,
                    summonedObject,
                    summonedObject.Definition.EffectTargetingType);

                foreach (var target in targets)
                {
                    ExecuteEffect(
                        session,
                        source,
                        summonedObject,
                        effect,
                        effectIndex,
                        target,
                        sourceId);
                }
            }
        }

        private void ExecuteEffect(
            CombatSession session,
            BattleUnit source,
            SummonedCombatMapObject summonedObject,
            SummonedObjectEffectData effect,
            int effectIndex,
            BattleUnit target,
            string sourceId, string presentationAction = null)
        {
            switch (effect.EffectType)
            {
                case SummonedObjectEffectType.DealDamage:
                    var position = target.Position;
                    if (ExecuteDamage(session, source, target, effect) && presentationAction != null)
                        feedbackBus.Publish(new CombatFeedbackEvent(target, CombatFeedbackType.TargetEffect,
                            source: source, position: position, targetVfx: CombatTargetVfxKind.Blunt, actionId: presentationAction));
                    break;

                case SummonedObjectEffectType.Heal:
                    recoverySystem.Heal(target, effect.BaseValue);
                    break;

                case SummonedObjectEffectType.RestoreResource:
                    recoverySystem.RestoreResource(target, effect.BaseValue);
                    break;

                case SummonedObjectEffectType.DrainResource:
                    var previousResource = target?.Character?.ResourcePool?.CurrentValue ?? 0;
                    target?.Character?.ResourcePool?.Drain(effect.BaseValue);
                    MomentumResourceRule.RefreshSpeedAfterChange(target?.Character, previousResource);
                    break;

                case SummonedObjectEffectType.Shield:
                    shieldSystem.GrantShield(target, effect.BaseValue, sourceId);
                    break;

                case SummonedObjectEffectType.ApplyStatus:
                    var applied = statusEffectSystem.ApplyStatus(
                        source,
                        target,
                        effect.StatusId,
                        effect.BaseChance,
                        effect.DurationTurns,
                        effect.Stacks,
                        effect.StatusDamageMultiplier,
                        effect.StatusHealPercent,
                        sourceId);
                    if (applied && presentationAction != null)
                        feedbackBus.Publish(new CombatFeedbackEvent(target, CombatFeedbackType.TargetEffect,
                            source: source, targetVfx: CombatTargetVfxKind.TrapContact, actionId: presentationAction));
                    break;

                case SummonedObjectEffectType.ModifyStat:
                    modifyStatSystem.ApplyFromSummonedObject(
                        source,
                        target,
                        effect.ModifierStatType,
                        effect.ModifierOperation,
                        effect.ModifierStacking,
                        effect.ModifierValue,
                        effect.DurationTurns,
                        sourceId);
                    break;

                case SummonedObjectEffectType.ModifyVar:
                    modifyVarSystem.ApplyFromSummonedObject(
                        source,
                        target,
                        effect.ModifierVariableType,
                        effect.ModifierStacking,
                        effect.ModifierValue,
                        effect.DurationTurns,
                        sourceId);
                    break;
            }
        }

        public void RemovePersistentEffects(
            CombatSession session,
            SummonedCombatMapObject summonedObject)
        {
            if (session == null || summonedObject?.Definition?.Effects == null)
            {
                return;
            }

            for (var effectIndex = 0;
                effectIndex < summonedObject.Definition.Effects.Count;
                effectIndex++)
            {
                var effect = summonedObject.Definition.Effects[effectIndex];

                if (effect?.EffectType != SummonedObjectEffectType.ModifyStat
                    && effect?.EffectType != SummonedObjectEffectType.ModifyVar)
                {
                    continue;
                }

                var sourceId = BuildEffectSourceId(summonedObject, effectIndex);

                foreach (var unit in session.Units)
                {
                    RemoveModifier(unit, effect.EffectType, sourceId);
                }
            }
        }

        private void RemoveModifierFromTargetsOutsideRange(
            CombatSession session,
            BattleUnit source,
            SummonedCombatMapObject summonedObject,
            SummonedObjectEffectData effect,
            string sourceId)
        {
            foreach (var unit in session.Units)
            {
                if (unit?.Character == null)
                {
                    continue;
                }

                var isAlive = unit.Character.LifeState == CharacterLifeState.Alive;
                var isAlly = unit.IsPlayerSide == source.IsPlayerSide;
                var isEligibleTarget = summonedObject.Definition.EffectTargetingType
                    == TargetingType.All
                    || summonedObject.Definition.EffectTargetingType
                    == TargetingType.Allies && isAlly
                    || summonedObject.Definition.EffectTargetingType
                    == TargetingType.Enemies && !isAlly;

                var isWithinRange = isAlive
                    && isEligibleTarget
                    && CombatTargetingService.IsPositionWithinRange(
                        summonedObject.Position,
                        unit.Position,
                        summonedObject.Definition.EffectRange);

                if (!isWithinRange)
                {
                    RemoveModifier(unit, effect.EffectType, sourceId);
                }
            }
        }

        private void RemoveModifier(
            BattleUnit target,
            SummonedObjectEffectType effectType,
            string sourceId)
        {
            if (effectType == SummonedObjectEffectType.ModifyStat)
            {
                modifyStatSystem.RemoveFromSummonedObject(target, sourceId);
                return;
            }

            if (effectType == SummonedObjectEffectType.ModifyVar)
            {
                modifyVarSystem.RemoveFromSummonedObject(target, sourceId);
            }
        }

        private static string BuildEffectSourceId(
            SummonedCombatMapObject summonedObject,
            int effectIndex)
        {
            return $"summoned.{summonedObject.ObjectId}.effect.{effectIndex}";
        }

        private bool ExecuteDamage(
            CombatSession session,
            BattleUnit source,
            BattleUnit target,
            SummonedObjectEffectData effect)
        {
            var damage = CombatDamageValueCalculator.CalculateDamageValue(
                source,
                effect.DamageValueSource,
                effect.DamageMultiplier);

            if (damage <= 0)
            {
                return false;
            }

            var shieldBefore = target.Character.ShieldPool?.TotalShield ?? 0;
            var healthDamage = damageSystem.ApplySummonedObjectDamage(
                session,
                target,
                damage,
                effect.DamageType);
            return healthDamage > 0 || (target.Character.ShieldPool?.TotalShield ?? 0) < shieldBefore;
        }

        private static System.Collections.Generic.IReadOnlyList<BattleUnit> GetTargets(
            CombatSession session,
            BattleUnit source,
            SummonedCombatMapObject summonedObject,
            TargetingType targetingType)
        {
            var targets = new System.Collections.Generic.List<BattleUnit>();

            foreach (var unit in session.Units)
            {
                if (unit?.Character == null
                    || unit.Character.LifeState != CharacterLifeState.Alive
                    || !CombatTargetingService.IsPositionWithinRange(
                        summonedObject.Position,
                        unit.Position,
                        summonedObject.Definition.EffectRange))
                {
                    continue;
                }

                var isAlly = unit.IsPlayerSide == source.IsPlayerSide;

                if (targetingType == TargetingType.All
                    || targetingType == TargetingType.Allies && isAlly
                    || targetingType == TargetingType.Enemies && !isAlly)
                {
                    targets.Add(unit);
                }
            }

            return targets;
        }
    }
}

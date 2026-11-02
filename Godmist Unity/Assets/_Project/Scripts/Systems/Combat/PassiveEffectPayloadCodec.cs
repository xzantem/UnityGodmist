using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Serialization;
using Godmist.Core.Types;
using Godmist.Data.Effects;
using Godmist.Runtime.Effects;

namespace Godmist.Systems.Combat
{
    // One field-mapping owner for lifecycle copies and persistence. Validation remains at the restore boundary.
    public static class PassiveEffectPayloadCodec
    {
        private sealed class Contract
        {
            public Type RuntimeType;
            public Func<PassiveEffectPayloadRuntime, PassiveEffectPayloadSaveData> Write;
            public Func<PassiveEffectPayloadSaveData, bool, PassiveEffectPayloadRuntime> Read;
        }
        private static readonly Dictionary<PassiveEffectPayloadType, Contract> Contracts = BuildContracts();
        public static IEnumerable<PassiveEffectPayloadType> RegisteredTypes => Contracts.Keys;

        private static Contract Get(PassiveEffectPayloadType type) => Contracts.TryGetValue(type, out var contract) ? contract
            : throw new NotSupportedException($"No passive payload contract for {type}.");

        public static bool IsPersistent(PassiveEffectPayloadType type) => Get(type).Write != null;
        public static bool IsPersistent(PassiveEffectPayloadRuntime payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            var contract = Get(payload.Type);
            if (payload.GetType() != contract.RuntimeType) throw new NotSupportedException($"Unregistered passive payload runtime type {payload.GetType()}.");
            return contract.Write != null;
        }
        public static Type RuntimeType(PassiveEffectPayloadType type) => Get(type).RuntimeType;

        public static PassiveEffectPayloadSaveData Write(PassiveEffectPayloadRuntime payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            var contract = Get(payload.Type);
            if (payload.GetType() != contract.RuntimeType) throw new NotSupportedException($"Unregistered passive payload runtime type {payload.GetType()}.");
            if (payload.GetType() != contract.RuntimeType) throw new NotSupportedException($"Unregistered passive payload runtime type {payload.GetType()}.");
            if (contract.Write == null) throw new NotSupportedException($"Combat-only payload {payload.Type} cannot be saved.");
            return contract.Write(payload);
        }

        public static PassiveEffectPayloadRuntime Read(PassiveEffectPayloadSaveData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            var read = Get(data.Type).Read;
            if (read == null) throw new NotSupportedException($"Combat-only payload {data.Type} cannot be restored.");
            return read(data, true);
        }

        public static PassiveEffectPayloadRuntime Clone(PassiveEffectPayloadRuntime payload)
        {
            if (payload == null) return null;
            var contract = Get(payload.Type);
            if (payload.GetType() != contract.RuntimeType) throw new NotSupportedException($"Unregistered passive payload runtime type {payload.GetType()}.");
            if (payload is CounterattackPayload counterattack)
                return new CounterattackPayload { Skill = counterattack.Skill, Range = counterattack.Range?.DeepCopy(),
                    Effects = counterattack.Effects?.Select(effect => effect?.DeepCopy()).ToList() ?? new List<SkillEffectData>() };
            return contract.Read(contract.Write(payload), false);
        }

        private static string Text(string value, bool normalize) => normalize ? value ?? string.Empty : value;

        private static Dictionary<PassiveEffectPayloadType, Contract> BuildContracts()
        {
            var result = new Dictionary<PassiveEffectPayloadType, Contract>();
            void Add<T>(PassiveEffectPayloadType type, Func<T, PassiveEffectPayloadSaveData> write,
                Func<PassiveEffectPayloadSaveData, bool, T> read) where T : PassiveEffectPayloadRuntime => result.Add(type, new Contract {
                    RuntimeType = typeof(T), Write = value => { var data = write((T)value); data.Type = type; return data; }, Read = (data, normalize) => read(data, normalize) });
            Add<DamageOverTimePayload>(PassiveEffectPayloadType.DamageOverTime,
                value => new() { RemainingTotalDamage = value.RemainingTotalDamage, DamagePerTurn = value.DamagePerTurn },
                (data, normalize) => new() { RemainingTotalDamage = data.RemainingTotalDamage, DamagePerTurn = data.DamagePerTurn });
            Add<SleepHealingPayload>(PassiveEffectPayloadType.SleepHealing, value => new() { MaxHealthPercent = value.MaxHealthPercent },
                (data, normalize) => new() { MaxHealthPercent = data.MaxHealthPercent });
            Add<UniqueCombatBuffPayload>(PassiveEffectPayloadType.UniqueCombatBuff,
                value => new() { UniqueBuffType = value.BuffType, ActionPointModifier = value.ActionPointModifier },
                (data, normalize) => new() { BuffType = data.UniqueBuffType, ActionPointModifier = data.ActionPointModifier });
            Add<ResourceRegenerationBlockPayload>(PassiveEffectPayloadType.ResourceRegenerationBlock, value => new(), (data, normalize) => new());
            Add<DamageRedirectPayload>(PassiveEffectPayloadType.DamageRedirect,
                value => new() { DamageRedirectPercent = value.RedirectPercent, ProtectorCharacterId = value.ProtectorCharacterId,
                    ProtectorDisplayName = value.ProtectorDisplayName, ProtectedCharacterId = value.ProtectedCharacterId, ProtectedCharacterDisplayName = value.ProtectedCharacterDisplayName },
                (data, normalize) => new() { RedirectPercent = data.DamageRedirectPercent, ProtectorCharacterId = Text(data.ProtectorCharacterId, normalize),
                    ProtectorDisplayName = Text(data.ProtectorDisplayName, normalize), ProtectedCharacterId = Text(data.ProtectedCharacterId, normalize), ProtectedCharacterDisplayName = Text(data.ProtectedCharacterDisplayName, normalize) });
            Add<OnHitStatusPayload>(PassiveEffectPayloadType.OnHitStatus,
                value => new() { OnHitStatusId = value.StatusId, OnHitStatusChance = value.Chance, OnHitStatusDurationTurns = value.DurationTurns,
                    OnHitStatusDamageMultiplier = value.StatusDamageMultiplier, OnHitStatusDamagePerTurn = value.StatusDamagePerTurn },
                (data, normalize) => new() { StatusId = Text(data.OnHitStatusId, normalize), Chance = data.OnHitStatusChance, DurationTurns = data.OnHitStatusDurationTurns,
                    StatusDamageMultiplier = data.OnHitStatusDamageMultiplier, StatusDamagePerTurn = data.OnHitStatusDamagePerTurn });
            Add<StatModifiersPayload>(PassiveEffectPayloadType.StatModifiers,
                value => new() { StatModifiers = WriteStats(value.Modifiers) }, (data, normalize) => new() { Modifiers = ReadStats(data.StatModifiers) });
            Add<VariableModifiersPayload>(PassiveEffectPayloadType.VariableModifiers,
                value => new() { VariableModifiers = WriteVariables(value.Modifiers) }, (data, normalize) => new() { Modifiers = ReadVariables(data.VariableModifiers, normalize) });
            Add<ResistanceModifierPayload>(PassiveEffectPayloadType.ResistanceModifier,
                value => new() { ResistanceType = value.ResistanceType, ResistanceModifierValue = value.Value },
                (data, normalize) => new() { ResistanceType = data.ResistanceType, Value = data.ResistanceModifierValue });
            Add<PeriodicRecoveryPayload>(PassiveEffectPayloadType.PeriodicRecovery,
                value => new() { HealthPercent = value.HealthPercent, ResourcePercent = value.ResourcePercent, HealthRoundingCarry = value.HealthRoundingCarry, ResourceRoundingCarry = value.ResourceRoundingCarry },
                (data, normalize) => new() { HealthPercent = data.HealthPercent, ResourcePercent = data.ResourcePercent, HealthRoundingCarry = data.HealthRoundingCarry, ResourceRoundingCarry = data.ResourceRoundingCarry });
            result.Add(PassiveEffectPayloadType.Counterattack, new Contract { RuntimeType = typeof(CounterattackPayload) });
            return result;
        }

        private static List<StatModifierSaveData> WriteStats(List<StatModifier> values) => (values ?? new()).Where(value => value != null)
            .Select(value => new StatModifierSaveData { StatType = value.StatType, Operation = value.Operation, Stacking = value.Stacking, Value = value.Value }).ToList();
        private static List<StatModifier> ReadStats(List<StatModifierSaveData> values) => (values ?? new()).Select(value => new StatModifier {
            StatType = value.StatType, Operation = value.Operation, Stacking = value.Stacking, Value = value.Value }).ToList();
        private static List<GameplayVariableModifierSaveData> WriteVariables(List<GameplayVariableModifier> values) => (values ?? new()).Where(value => value != null)
            .Select(value => new GameplayVariableModifierSaveData { VariableType = value.VariableType, ReferenceId = value.ReferenceId, Stacking = value.Stacking, Value = value.Value }).ToList();
        private static List<GameplayVariableModifier> ReadVariables(List<GameplayVariableModifierSaveData> values, bool normalize) => (values ?? new())
            .Select(value => new GameplayVariableModifier { VariableType = value.VariableType, ReferenceId = Text(value.ReferenceId, normalize), Stacking = value.Stacking, Value = value.Value }).ToList();
    }
}

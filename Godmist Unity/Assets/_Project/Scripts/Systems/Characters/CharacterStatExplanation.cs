using System;
using System.Linq;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Effects;
using Godmist.Systems.Combat;
using UnityEngine;

namespace Godmist.Systems.Characters
{
    // Pure observation of already reconciled data. Never refreshes, reconciles or mutates
    // the inspected character. Derived rules run against a detached stats/loadout copy.
    public sealed class CharacterStatExplanation
    {
        public sealed class Contribution
        {
            public CharacterStatType Stat;
            public string Source;
            public StatModifierOperation Operation;
            public StatModifierStacking Stacking;
            public float Value;
            public int Stage;
        }
        public readonly List<Contribution> Rows = new();
        public readonly Dictionary<CharacterStatType,float> Values = new();
        public static CharacterStatExplanation Create(CharacterInstance source)
        {
            var result=new CharacterStatExplanation();if(source?.Stats==null)return result;
            var copy=new CharacterInstance
            {
                Definition=source.Definition,ClassData=source.ClassData,Level=source.Level,CurrentHealth=source.CurrentHealth,
                Equipment=source.Equipment,PassiveEffects=new(source.PassiveEffects??new()),
                SkillTreeProgress=source.SkillTreeProgress,Stats=new CharacterStats(),
                ResourcePool=source.ResourcePool==null?null:new ResourcePool(source.ResourcePool.ResourceType,source.ResourcePool.CurrentValue,source.ResourcePool.MaxValue,source.ResourcePool.Regeneration)
            };
            foreach(CharacterStatType type in Enum.GetValues(typeof(CharacterStatType)))
            {
                var stat=source.Stats.GetStat(type);copy.Stats.GetStat(type)=new Stat(stat.BaseValue);
                var raw=CharacterStatPipelineSystem.EvaluateModifiers(stat.BaseValue,type,copy.PassiveEffects,false,type!=CharacterStatType.Speed);
                var effective=CharacterStatPipelineSystem.EvaluateModifiers(stat.BaseValue,type,copy.PassiveEffects,applyMaximum:type!=CharacterStatType.Speed);
                copy.Stats.GetStat(type).SetEffectiveValue(effective);
                if(raw!=effective)result.Rows.Add(new Contribution{Stat=type,Source="#minimum",Operation=StatModifierOperation.FlatAbsolute,Value=effective-raw,Stage=5});
            }
            foreach(var effect in copy.PassiveEffects.Where(PassiveEffectLifecycleSystem.IsActive))
            foreach(var modifier in effect.GetPayload<StatModifiersPayload>()?.Modifiers??new())
            {
                if(modifier==null || modifier.Value==0 && modifier.Operation!=StatModifierOperation.Maximum)continue;
                var stage=modifier.StatType==CharacterStatType.Speed && modifier.Operation==StatModifierOperation.Maximum ? 7 : modifier.Operation switch{StatModifierOperation.FlatBase=>0,StatModifierOperation.Percent=>modifier.Stacking==StatModifierStacking.Additive?1:2,StatModifierOperation.FlatAbsolute=>3,_=>4};
                result.Rows.Add(new Contribution{Stat=modifier.StatType,Source=effect.EffectId,Operation=modifier.Operation,Stacking=modifier.Stacking,Value=modifier.Value,Stage=stage});
            }
            void Observe(CharacterStatType type,string sourceId,StatModifierOperation operation,float value)
            {
                if(value!=0)result.Rows.Add(new Contribution{Stat=type,Source=sourceId,Operation=operation,Stacking=StatModifierStacking.Multiplicative,Value=value,Stage=6});
            }
            WarriorCombatSystem.ApplyStats(copy,Observe);
            MageCombatSystem.ApplyStats(copy,Observe);
            UniqueEquipmentCombatRules.ApplyDerivedStats(copy,Observe);
            var speed=MomentumResourceRule.SpeedBonus(copy.ResourcePool,copy.ResourcePool?.CurrentValue??0);
            copy.Stats.Speed.SetEffectiveValue(copy.Stats.Speed.GetValue()+speed);
            Observe(CharacterStatType.Speed,"#momentum",StatModifierOperation.FlatAbsolute,speed);
            copy.Stats.Speed.SetEffectiveValue(CharacterStatPipelineSystem.ApplyFinalSpeedMaximum(copy,copy.Stats.Speed.GetValue()));
            foreach(CharacterStatType type in Enum.GetValues(typeof(CharacterStatType)))result.Values[type]=copy.Stats.GetStat(type).GetValue();
            return result;
        }
    }
}

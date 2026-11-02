using Godmist.Core.Enums;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Combat;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public class CombatVariableSystem
    {
        public float GetMultiplier(
            CharacterInstance character,
            GameplayVariableType variableType,
            string referenceId = null, string excludedEffectId = null, CombatValueExplanation explanation = null)
        {
            if (character?.PassiveEffects == null)
            {
                return 1f;
            }

            var additivePercent = 0f;
            var multiplicativePercent = 1f;

            foreach (var effect in character.PassiveEffects)
            {
                if ((excludedEffectId != null && effect?.EffectId==excludedEffectId) || !PassiveEffectLifecycleSystem.IsActive(effect))
                {
                    continue;
                }

                var payload = effect.GetPayload<VariableModifiersPayload>();
                if (payload == null)
                {
                    continue;
                }

                foreach (var modifier in payload.Modifiers)
                {
                    if (modifier == null
                        || modifier.VariableType != variableType
                        || ((variableType == GameplayVariableType.DamageDealtPercentInDungeonTheme || variableType == GameplayVariableType.DamageAgainstTag || variableType == GameplayVariableType.DamageInEnvironment)
                            && !string.Equals(
                                modifier.ReferenceId,
                                referenceId,
                                System.StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    explanation?.Add(!string.IsNullOrWhiteSpace(effect.DisplayName)?effect.DisplayName:effect.EffectId,
                        (modifier.Stacking==StatModifierStacking.Multiplicative?"×":"+")+(modifier.Stacking==StatModifierStacking.Multiplicative?1+modifier.Value:modifier.Value*100).ToString("0.####",System.Globalization.CultureInfo.GetCultureInfo("pl-PL"))+(modifier.Stacking==StatModifierStacking.Multiplicative?"":"%"));
                    if (modifier.Stacking == StatModifierStacking.Multiplicative)
                    {
                        multiplicativePercent *= 1f + modifier.Value;
                        continue;
                    }

                    additivePercent += modifier.Value;
                }
            }

            return Mathf.Max(
                0f,
                (1f + additivePercent) * multiplicativePercent);
        }

        public float GetExperienceMultiplier(CharacterInstance character)
        {
            return GetMultiplier(character, GameplayVariableType.ExperienceGainPercent);
        }

        public float GetGoldMultiplier(CharacterInstance character)
        {
            return GetMultiplier(character, GameplayVariableType.GoldGainPercent);
        }

        public float GetDungeonThemeDamageMultiplier(
            CharacterInstance character,
            DungeonTheme theme)
        {
            return GetMultiplier(
                character,
                GameplayVariableType.DamageDealtPercentInDungeonTheme,
                theme.ToString());
        }

        public int ApplyDamageModifiers(
            BattleUnit source,
            BattleUnit target,
            int requestedDamage,CombatValueExplanation explanation=null,Godmist.Data.Skills.SkillData previewSkill=null)
        {
            if (requestedDamage <= 0)
            {
                return 0;
            }

            var dealtMultiplier = GetOutgoingDamageMultiplier(source,explanation,previewSkill);

            var angerMultiplier = ScoutCombatSystem.ReceivedDamageMultiplier(source, target);
            var receivedMultiplier = GetMultiplier(
                target?.Character,
                GameplayVariableType.DamageReceivedPercent);

            return Mathf.Max(
                0,
                Mathf.RoundToInt(
                    requestedDamage * dealtMultiplier * receivedMultiplier * angerMultiplier));
        }
        public float GetOutgoingDamageMultiplier(BattleUnit source,CombatValueExplanation explanation=null,Godmist.Data.Skills.SkillData previewSkill=null)
        {
            var value=GetMultiplier(source?.Character,GameplayVariableType.DamageDealtPercent,explanation:explanation);
            if(previewSkill!=null)value=WarriorCombatSystem.PreviewDealtMultiplier(source,previewSkill,value);
            var lowHealthBonus=WarriorCombatSystem.LowHealthDamageBonus(source,previewSkill);
            value+=lowHealthBonus;
            if(lowHealthBonus!=0)explanation?.Add("Szał Bitewny",lowHealthBonus);
            explanation?.Add("damage_dealt",value);
            return value;
        }
    }
}

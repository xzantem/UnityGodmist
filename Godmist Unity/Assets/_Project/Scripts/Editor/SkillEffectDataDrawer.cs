using System;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    [CustomPropertyDrawer(typeof(SkillEffectData))]
    public class SkillEffectDataDrawer : PropertyDrawer
    {
        private const float Spacing = 2f;

        public override bool CanCacheInspectorGUI(
            SerializedProperty property)
        {
            return false;
        }

        public override void OnGUI(
            Rect position,
            SerializedProperty property,
            GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var effectType = property.FindPropertyRelative("effectType");
            var polarity = property.FindPropertyRelative("polarity");
            var alwaysHits = property.FindPropertyRelative("alwaysHits");
            var targetModeIndex = property.FindPropertyRelative("targetModeIndex");
            var baseValue = property.FindPropertyRelative("baseValue");
            var damageType = property.FindPropertyRelative("damageType");
            var damageValueSource =
                property.FindPropertyRelative("damageValueSource");
            var damageMultiplier =
                property.FindPropertyRelative("damageMultiplier");
            var damageHitCount = property.FindPropertyRelative("damageHitCount");
            var bonusPenetration = property.FindPropertyRelative("bonusPenetration");
            var bonusDamageMultiplierChance = property.FindPropertyRelative("bonusDamageMultiplierChance");
            var bonusDamageMultiplier = property.FindPropertyRelative("bonusDamageMultiplier");
            var onHitStatusId = property.FindPropertyRelative("onHitStatusId");
            var onHitStatusChance = property.FindPropertyRelative("onHitStatusChance");
            var onHitStatusDurationTurns = property.FindPropertyRelative("onHitStatusDurationTurns");
            var lifeStealPercent = property.FindPropertyRelative("lifeStealPercent");
            var resourceDrainPercent = property.FindPropertyRelative("resourceDrainPercent");
            var delayCasterTurns =
                property.FindPropertyRelative("delayCasterTurns");
            var displacementDistance =
                property.FindPropertyRelative("displacementDistance");
            var baseChance = property.FindPropertyRelative("baseChance");
            var statusId = property.FindPropertyRelative("statusId");
            var durationTurns = property.FindPropertyRelative("durationTurns");
            var statusDamageMultiplier =
                property.FindPropertyRelative("statusDamageMultiplier");
            var statusHealPercent =
                property.FindPropertyRelative("statusHealPercent");
            var counterattackRange =
                property.FindPropertyRelative("counterattackRange");
            var counterattackEffects =
                property.FindPropertyRelative("counterattackEffects");
            var conditions = property.FindPropertyRelative("conditions");
            var modifierStatType =
                property.FindPropertyRelative("modifierStatType");
            var modifierVariableType =
                property.FindPropertyRelative("modifierVariableType");
            var modifierOperation =
                property.FindPropertyRelative("modifierOperation");
            var modifierStacking =
                property.FindPropertyRelative("modifierStacking");
            var modifierValue =
                property.FindPropertyRelative("modifierValue");
            var uniqueBuffType = property.FindPropertyRelative("uniqueBuffType");
            var uniqueBuffStacks = property.FindPropertyRelative("uniqueBuffStacks");
            var resistanceType = property.FindPropertyRelative("resistanceType");
            var actionPointModifier = property.FindPropertyRelative("actionPointModifier");
            var actionValuePercent = property.FindPropertyRelative("actionValuePercent");
            var resourceRegenerationBlockTurns = property.FindPropertyRelative("resourceRegenerationBlockTurns");
            var healthSacrificePercent = property.FindPropertyRelative("healthSacrificePercent");
            var resourceRestorePerSacrificedHealth = property.FindPropertyRelative("resourceRestorePerSacrificedHealth");
            var redirectedDamagePercent = property.FindPropertyRelative("redirectedDamagePercent");
            var auraRadius = property.FindPropertyRelative("auraRadius");
            var auraIncludesCaster = property.FindPropertyRelative("auraIncludesCaster");
            var auraTargetingType = property.FindPropertyRelative("auraTargetingType");
            var shieldAverageAttackMultiplier = property.FindPropertyRelative("shieldAverageAttackMultiplier");
            var teleportDestinationRange =
                property.FindPropertyRelative("teleportDestinationRange");
            var summonedObjectDefinition =
                property.FindPropertyRelative("summonedObjectDefinition");
            var objectPlacementRange =
                property.FindPropertyRelative("objectPlacementRange");
            var maxActiveObjects =
                property.FindPropertyRelative("maxActiveObjects");
            var summonedCharacterDefinition =
                property.FindPropertyRelative("summonedCharacterDefinition");
            var summonedClassData =
                property.FindPropertyRelative("summonedClassData");
            var summonPlacementRange =
                property.FindPropertyRelative("summonPlacementRange");
            var summonDurationTurns =
                property.FindPropertyRelative("summonDurationTurns");
            var maxActiveSummons =
                property.FindPropertyRelative("maxActiveSummons");
            var summonPercentMaxHealth =
                property.FindPropertyRelative("summonPercentMaxHealth");
            var y = position.y;
            var selectedEffect =
                (EffectType)effectType.enumValueIndex;
            var isCounterattackEffect = property.propertyPath.Contains(
                "counterattackEffects");
            var isToggleSkill = !isCounterattackEffect
                && property.serializedObject.FindProperty("<IsToggle>k__BackingField")
                    ?.boolValue == true;

            DrawEffectTypePopup(position, ref y, effectType, isToggleSkill);

            if (selectedEffect == EffectType.CreateObject)
            {
                DrawField(
                    position,
                    ref y,
                    summonedObjectDefinition,
                    true);

                DrawField(
                    position,
                    ref y,
                    objectPlacementRange,
                    true);

                DrawField(
                    position,
                    ref y,
                    maxActiveObjects);

                EditorGUI.EndProperty();
                return;
            }

            if (selectedEffect == EffectType.Summon)
            {
                DrawField(position, ref y, summonedCharacterDefinition, true);
                DrawField(position, ref y, summonedClassData, true);
                DrawField(position, ref y, summonPlacementRange, true);
                DrawField(position, ref y, summonDurationTurns);
                DrawField(position, ref y, maxActiveSummons);
                DrawField(position, ref y, summonPercentMaxHealth);
                EditorGUI.EndProperty();
                return;
            }

            if (!isCounterattackEffect)
            {
                DrawTargetModePopup(
                    position,
                    ref y,
                    property,
                    targetModeIndex);
            }

            var selectedPolarity =
                (EffectPolarity)polarity.enumValueIndex;

            if (selectedEffect != EffectType.DealDamage)
            {
                DrawField(position, ref y, polarity);

                if (selectedPolarity == EffectPolarity.Negative)
                {
                    DrawField(position, ref y, alwaysHits);
                }
            }

            if (selectedEffect == EffectType.DealDamage)
            {
                DrawField(position, ref y, damageType);
                DrawField(position, ref y, damageValueSource);
                DrawField(position, ref y, damageMultiplier);
                DrawField(position, ref y, damageHitCount);
                DrawField(position, ref y, property.FindPropertyRelative("additionalHitTrials"));
                DrawField(position, ref y, property.FindPropertyRelative("additionalHitChance"));
                DrawField(position, ref y, bonusPenetration);
                DrawField(position, ref y, bonusDamageMultiplierChance);
                if (bonusDamageMultiplierChance.floatValue > 0f) DrawField(position, ref y, bonusDamageMultiplier);
                DrawField(position, ref y, lifeStealPercent);
                DrawField(position, ref y, resourceDrainPercent);

                if ((DamageType)damageType.enumValueIndex
                    == DamageType.Physical)
                {
                    DrawField(position, ref y, alwaysHits);
                }
            }
            else if (selectedEffect == EffectType.Push
                     || selectedEffect == EffectType.Pull
                     || selectedEffect == EffectType.Fallback
                     || selectedEffect == EffectType.Charge)
            {
                DrawField(position, ref y, displacementDistance);

                if (selectedEffect == EffectType.Pull
                    || selectedEffect == EffectType.Charge)
                {
                    DrawField(position, ref y, baseChance);
                }
            }
            else if (selectedEffect == EffectType.Teleport)
            {
                DrawField(
                    position,
                    ref y,
                    teleportDestinationRange,
                    true);
            }
            else if (selectedEffect == EffectType.Heal
                     || selectedEffect == EffectType.RestoreResource
                     || selectedEffect == EffectType.Shield)
            {
                DrawField(position, ref y, baseValue);
                if (selectedEffect == EffectType.Shield) DrawField(position, ref y, shieldAverageAttackMultiplier);
            }
            else if (selectedEffect == EffectType.ModifyStat)
            {
                DrawField(position, ref y, modifierStatType);
                DrawField(position, ref y, modifierOperation);

                if ((StatModifierOperation)modifierOperation.enumValueIndex
                    == StatModifierOperation.Percent)
                {
                    DrawField(position, ref y, modifierStacking);
                }

                DrawField(position, ref y, modifierValue);
                if (!isToggleSkill) DrawField(position, ref y, durationTurns);
            }
            else if (selectedEffect == EffectType.ModifyVar)
            {
                DrawField(position, ref y, modifierVariableType);
                DrawField(position, ref y, modifierStacking);
                DrawField(position, ref y, modifierValue);
                if (!isToggleSkill) DrawField(position, ref y, durationTurns);
            }
            else if (selectedEffect == EffectType.ApplyStatus)
            {
                DrawStatusIdPopup(position, ref y, statusId);
                DrawField(position, ref y, baseChance);
                if (!isToggleSkill) DrawField(position, ref y, durationTurns);

                if (UsesStatusDamageMultiplier(statusId.stringValue))
                {
                    DrawField(
                        position,
                        ref y,
                        statusDamageMultiplier);
                }

                if (statusId.stringValue == StatusEffectIds.Sleep)
                {
                    DrawField(
                        position,
                        ref y,
                        statusHealPercent);
                }

                if (statusId.stringValue == StatusEffectIds.Counterattack)
                {
                    DrawField(position, ref y, counterattackRange, true);
                    DrawField(position, ref y, counterattackEffects, true);
                }
            }
            else if (selectedEffect == EffectType.ModifyActionValue) DrawField(position, ref y, actionValuePercent);
            else if (selectedEffect == EffectType.DrainResource) DrawField(position, ref y, baseValue);
            else if (selectedEffect == EffectType.BlockResourceRegeneration)
            {
                if (!isToggleSkill) DrawField(position, ref y, resourceRegenerationBlockTurns);
            }
            else if (selectedEffect == EffectType.SacrificeHealthForResource)
            {
                DrawField(position, ref y, healthSacrificePercent);
                DrawField(position, ref y, resourceRestorePerSacrificedHealth);
            }
            else if (selectedEffect == EffectType.ModifyResistance)
            {
                DrawField(position, ref y, resistanceType);
                DrawField(position, ref y, modifierValue);
                if (!isToggleSkill) DrawField(position, ref y, durationTurns);
            }
            else if (selectedEffect == EffectType.ApplyUniqueBuff)
            {
                DrawUniqueBuffTypePopup(position, ref y, uniqueBuffType, isToggleSkill);
                var buffType = (UniqueBuffType)uniqueBuffType.enumValueIndex;
                if (buffType == UniqueBuffType.ActionPointsNextTurn)
                {
                    DrawField(position, ref y, actionPointModifier);
                    if (!isToggleSkill) DrawField(position, ref y, durationTurns);
                }
                else if (buffType == UniqueBuffType.OnHitStatus)
                {
                    DrawStatusIdPopup(position, ref y, onHitStatusId);
                    DrawField(position, ref y, onHitStatusChance);
                    DrawField(position, ref y, onHitStatusDurationTurns);
                    if (UsesStatusDamageMultiplier(onHitStatusId.stringValue))
                    {
                        DrawField(position, ref y, statusDamageMultiplier);
                    }
                    if (!isToggleSkill) DrawField(position, ref y, durationTurns);
                }
                else if (buffType == UniqueBuffType.DamageRedirect)
                {
                    DrawField(position, ref y, redirectedDamagePercent);
                    if (!isToggleSkill) DrawField(position, ref y, durationTurns);
                }
                else if (buffType == UniqueBuffType.AuraStatus)
                {
                    DrawStatusIdPopup(position, ref y, statusId);
                    DrawField(position, ref y, baseChance);
                    if (!isToggleSkill) DrawField(position, ref y, durationTurns);
                    if (UsesStatusDamageMultiplier(statusId.stringValue))
                    {
                        DrawField(position, ref y, statusDamageMultiplier);
                    }
                    DrawField(position, ref y, auraRadius);
                    DrawField(position, ref y, auraIncludesCaster);
                    DrawField(position, ref y, auraTargetingType);
                }
                else
                {
                    DrawField(position, ref y, uniqueBuffStacks);
                    if (!isToggleSkill) DrawField(position, ref y, durationTurns);
                }
            }

            if (!isToggleSkill && SupportsDelayedExecution(selectedEffect))
            {
                DrawField(position, ref y, delayCasterTurns);
            }

            if (!isToggleSkill) DrawField(position, ref y, conditions, true);
            if(selectedEffect==EffectType.SwapPositions || selectedEffect==EffectType.MarkWeakPoint)
            {
                DrawField(position,ref y,baseChance);
                if(selectedEffect==EffectType.MarkWeakPoint)DrawField(position,ref y,durationTurns);
            }
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(
            SerializedProperty property,
            GUIContent label)
        {
            var effectType = property.FindPropertyRelative("effectType");
            var polarity = property.FindPropertyRelative("polarity");
            var alwaysHits = property.FindPropertyRelative("alwaysHits");
            var targetModeIndex = property.FindPropertyRelative("targetModeIndex");
            var baseValue = property.FindPropertyRelative("baseValue");
            var damageType = property.FindPropertyRelative("damageType");
            var damageValueSource =
                property.FindPropertyRelative("damageValueSource");
            var damageMultiplier =
                property.FindPropertyRelative("damageMultiplier");
            var damageHitCount = property.FindPropertyRelative("damageHitCount");
            var bonusPenetration = property.FindPropertyRelative("bonusPenetration");
            var bonusDamageMultiplierChance = property.FindPropertyRelative("bonusDamageMultiplierChance");
            var bonusDamageMultiplier = property.FindPropertyRelative("bonusDamageMultiplier");
            var onHitStatusId = property.FindPropertyRelative("onHitStatusId");
            var onHitStatusChance = property.FindPropertyRelative("onHitStatusChance");
            var onHitStatusDurationTurns = property.FindPropertyRelative("onHitStatusDurationTurns");
            var lifeStealPercent = property.FindPropertyRelative("lifeStealPercent");
            var resourceDrainPercent = property.FindPropertyRelative("resourceDrainPercent");
            var delayCasterTurns =
                property.FindPropertyRelative("delayCasterTurns");
            var displacementDistance =
                property.FindPropertyRelative("displacementDistance");
            var baseChance = property.FindPropertyRelative("baseChance");
            var statusId = property.FindPropertyRelative("statusId");
            var durationTurns = property.FindPropertyRelative("durationTurns");
            var statusDamageMultiplier =
                property.FindPropertyRelative("statusDamageMultiplier");
            var statusHealPercent =
                property.FindPropertyRelative("statusHealPercent");
            var counterattackRange =
                property.FindPropertyRelative("counterattackRange");
            var counterattackEffects =
                property.FindPropertyRelative("counterattackEffects");
            var conditions = property.FindPropertyRelative("conditions");
            var modifierStatType =
                property.FindPropertyRelative("modifierStatType");
            var modifierVariableType =
                property.FindPropertyRelative("modifierVariableType");
            var modifierOperation =
                property.FindPropertyRelative("modifierOperation");
            var modifierStacking =
                property.FindPropertyRelative("modifierStacking");
            var modifierValue =
                property.FindPropertyRelative("modifierValue");
            var uniqueBuffType = property.FindPropertyRelative("uniqueBuffType");
            var uniqueBuffStacks = property.FindPropertyRelative("uniqueBuffStacks");
            var resistanceType = property.FindPropertyRelative("resistanceType");
            var actionPointModifier = property.FindPropertyRelative("actionPointModifier");
            var actionValuePercent = property.FindPropertyRelative("actionValuePercent");
            var resourceRegenerationBlockTurns = property.FindPropertyRelative("resourceRegenerationBlockTurns");
            var healthSacrificePercent = property.FindPropertyRelative("healthSacrificePercent");
            var resourceRestorePerSacrificedHealth = property.FindPropertyRelative("resourceRestorePerSacrificedHealth");
            var redirectedDamagePercent = property.FindPropertyRelative("redirectedDamagePercent");
            var auraRadius = property.FindPropertyRelative("auraRadius");
            var auraIncludesCaster = property.FindPropertyRelative("auraIncludesCaster");
            var auraTargetingType = property.FindPropertyRelative("auraTargetingType");
            var shieldAverageAttackMultiplier = property.FindPropertyRelative("shieldAverageAttackMultiplier");
            var teleportDestinationRange =
                property.FindPropertyRelative("teleportDestinationRange");
            var summonedObjectDefinition =
                property.FindPropertyRelative("summonedObjectDefinition");
            var objectPlacementRange =
                property.FindPropertyRelative("objectPlacementRange");
            var maxActiveObjects =
                property.FindPropertyRelative("maxActiveObjects");
            var summonedCharacterDefinition =
                property.FindPropertyRelative("summonedCharacterDefinition");
            var summonedClassData =
                property.FindPropertyRelative("summonedClassData");
            var summonPlacementRange =
                property.FindPropertyRelative("summonPlacementRange");
            var summonDurationTurns =
                property.FindPropertyRelative("summonDurationTurns");
            var maxActiveSummons =
                property.FindPropertyRelative("maxActiveSummons");
            var summonPercentMaxHealth =
                property.FindPropertyRelative("summonPercentMaxHealth");

            var height = 0f;
            var selectedEffect =
                (EffectType)effectType.enumValueIndex;
            var isCounterattackEffect = property.propertyPath.Contains(
                "counterattackEffects");
            var isToggleSkill = !isCounterattackEffect
                && property.serializedObject.FindProperty("<IsToggle>k__BackingField")
                    ?.boolValue == true;

            AddHeight(ref height, effectType);

            if (selectedEffect == EffectType.CreateObject)
            {
                AddHeight(
                    ref height,
                    summonedObjectDefinition,
                    true);

                AddHeight(
                    ref height,
                    objectPlacementRange,
                    true);

                AddHeight(ref height, maxActiveObjects);

                return height;
            }

            if (selectedEffect == EffectType.Summon)
            {
                AddHeight(ref height, summonedCharacterDefinition, true);
                AddHeight(ref height, summonedClassData, true);
                AddHeight(ref height, summonPlacementRange, true);
                AddHeight(ref height, summonDurationTurns);
                AddHeight(ref height, maxActiveSummons);
                AddHeight(ref height, summonPercentMaxHealth);
                return height;
            }

            if (!isCounterattackEffect)
            {
                AddHeight(ref height, targetModeIndex);
            }

            var selectedPolarity =
                (EffectPolarity)polarity.enumValueIndex;

            if (selectedEffect != EffectType.DealDamage)
            {
                AddHeight(ref height, polarity);

                if (selectedPolarity == EffectPolarity.Negative)
                {
                    AddHeight(ref height, alwaysHits);
                }
            }

            if (selectedEffect == EffectType.DealDamage)
            {
                AddHeight(ref height, damageType);
                AddHeight(ref height, damageValueSource);
                AddHeight(ref height, damageMultiplier);
                AddHeight(ref height, damageHitCount);
                AddHeight(ref height, property.FindPropertyRelative("additionalHitTrials"));
                AddHeight(ref height, property.FindPropertyRelative("additionalHitChance"));
                AddHeight(ref height, bonusPenetration);
                AddHeight(ref height, bonusDamageMultiplierChance);
                if (bonusDamageMultiplierChance.floatValue > 0f) AddHeight(ref height, bonusDamageMultiplier);
                AddHeight(ref height, lifeStealPercent);
                AddHeight(ref height, resourceDrainPercent);

                if ((DamageType)damageType.enumValueIndex
                    == DamageType.Physical)
                {
                    AddHeight(ref height, alwaysHits);
                }
            }
            else if (selectedEffect == EffectType.Push
                     || selectedEffect == EffectType.Pull
                     || selectedEffect == EffectType.Fallback
                     || selectedEffect == EffectType.Charge)
            {
                AddHeight(ref height, displacementDistance);

                if (selectedEffect == EffectType.Pull
                    || selectedEffect == EffectType.Charge)
                {
                    AddHeight(ref height, baseChance);
                }
            }
            else if (selectedEffect == EffectType.Teleport)
            {
                AddHeight(
                    ref height,
                    teleportDestinationRange,
                    true);
            }
            else if (selectedEffect == EffectType.Heal
                     || selectedEffect == EffectType.RestoreResource
                     || selectedEffect == EffectType.Shield)
            {
                AddHeight(ref height, baseValue);
                if (selectedEffect == EffectType.Shield) AddHeight(ref height, shieldAverageAttackMultiplier);
            }
            else if (selectedEffect == EffectType.ModifyStat)
            {
                AddHeight(ref height, modifierStatType);
                AddHeight(ref height, modifierOperation);

                if ((StatModifierOperation)modifierOperation.enumValueIndex
                    == StatModifierOperation.Percent)
                {
                    AddHeight(ref height, modifierStacking);
                }

                AddHeight(ref height, modifierValue);
                if (!isToggleSkill) AddHeight(ref height, durationTurns);
            }
            else if (selectedEffect == EffectType.ModifyVar)
            {
                AddHeight(ref height, modifierVariableType);
                AddHeight(ref height, modifierStacking);
                AddHeight(ref height, modifierValue);
                if (!isToggleSkill) AddHeight(ref height, durationTurns);
            }
            else if (selectedEffect == EffectType.ApplyStatus)
            {
                AddHeight(ref height, statusId);
                AddHeight(ref height, baseChance);
                if (!isToggleSkill) AddHeight(ref height, durationTurns);

                if (UsesStatusDamageMultiplier(statusId.stringValue))
                {
                    AddHeight(
                        ref height,
                        statusDamageMultiplier);
                }

                if (statusId.stringValue == StatusEffectIds.Sleep)
                {
                    AddHeight(ref height, statusHealPercent);
                }

                if (statusId.stringValue == StatusEffectIds.Counterattack)
                {
                    AddHeight(ref height, counterattackRange, true);
                    AddHeight(ref height, counterattackEffects, true);
                }
            }
            else if (selectedEffect == EffectType.ModifyActionValue) AddHeight(ref height, actionValuePercent);
            else if (selectedEffect == EffectType.DrainResource) AddHeight(ref height, baseValue);
            else if (selectedEffect == EffectType.BlockResourceRegeneration)
            {
                if (!isToggleSkill) AddHeight(ref height, resourceRegenerationBlockTurns);
            }
            else if (selectedEffect == EffectType.SacrificeHealthForResource)
            {
                AddHeight(ref height, healthSacrificePercent);
                AddHeight(ref height, resourceRestorePerSacrificedHealth);
            }
            else if (selectedEffect == EffectType.ModifyResistance)
            {
                AddHeight(ref height, resistanceType);
                AddHeight(ref height, modifierValue);
                if (!isToggleSkill) AddHeight(ref height, durationTurns);
            }
            else if (selectedEffect == EffectType.ApplyUniqueBuff)
            {
                AddHeight(ref height, uniqueBuffType);
                var buffType = (UniqueBuffType)uniqueBuffType.enumValueIndex;
                if (buffType == UniqueBuffType.ActionPointsNextTurn)
                {
                    AddHeight(ref height, actionPointModifier);
                    if (!isToggleSkill) AddHeight(ref height, durationTurns);
                }
                else if (buffType == UniqueBuffType.OnHitStatus)
                {
                    AddHeight(ref height, onHitStatusId);
                    AddHeight(ref height, onHitStatusChance);
                    AddHeight(ref height, onHitStatusDurationTurns);
                    if (UsesStatusDamageMultiplier(onHitStatusId.stringValue))
                    {
                        AddHeight(ref height, statusDamageMultiplier);
                    }
                    if (!isToggleSkill) AddHeight(ref height, durationTurns);
                }
                else if (buffType == UniqueBuffType.DamageRedirect)
                {
                    AddHeight(ref height, redirectedDamagePercent);
                    if (!isToggleSkill) AddHeight(ref height, durationTurns);
                }
                else if (buffType == UniqueBuffType.AuraStatus)
                {
                    AddHeight(ref height, statusId);
                    AddHeight(ref height, baseChance);
                    if (!isToggleSkill) AddHeight(ref height, durationTurns);
                    if (UsesStatusDamageMultiplier(statusId.stringValue))
                    {
                        AddHeight(ref height, statusDamageMultiplier);
                    }
                    AddHeight(ref height, auraRadius);
                    AddHeight(ref height, auraIncludesCaster);
                    AddHeight(ref height, auraTargetingType);
                }
                else
                {
                    AddHeight(ref height, uniqueBuffStacks);
                    if (!isToggleSkill) AddHeight(ref height, durationTurns);
                }
            }

            if (!isToggleSkill && SupportsDelayedExecution(selectedEffect))
            {
                AddHeight(ref height, delayCasterTurns);
            }

            if (!isToggleSkill) AddHeight(ref height, conditions, true);
            if(selectedEffect==EffectType.SwapPositions || selectedEffect==EffectType.MarkWeakPoint)
            {
                AddHeight(ref height,baseChance);
                if(selectedEffect==EffectType.MarkWeakPoint)AddHeight(ref height,durationTurns);
            }
            return height;
        }

        private static bool SupportsDelayedExecution(EffectType effectType)
        {
            return effectType == EffectType.DealDamage
                || effectType == EffectType.ApplyStatus
                || effectType == EffectType.Heal
                || effectType == EffectType.RestoreResource
                || effectType == EffectType.Shield
                || effectType == EffectType.ModifyStat
                || effectType == EffectType.ModifyVar;
        }

        private static void DrawField(
            Rect position,
            ref float y,
            SerializedProperty property,
            bool includeChildren = false)
        {
            var height = EditorGUI.GetPropertyHeight(
                property,
                includeChildren);

            var fieldRect = new Rect(
                position.x,
                y,
                position.width,
                height);

            EditorGUI.PropertyField(
                fieldRect,
                property,
                includeChildren);

            y += height + Spacing;
        }

        private static void DrawTargetModePopup(
            Rect position,
            ref float y,
            SerializedProperty effectProperty,
            SerializedProperty targetModeIndex)
        {
            var height = EditorGUIUtility.singleLineHeight;
            var fieldRect = new Rect(
                position.x,
                y,
                position.width,
                height);

            var targetModes =
                effectProperty.serializedObject.FindProperty("targetModes");

            if (targetModes == null || targetModes.arraySize == 0)
            {
                EditorGUI.PropertyField(
                    fieldRect,
                    targetModeIndex,
                    new GUIContent("Target Mode"));

                y += height + Spacing;
                return;
            }

            var labels = new string[targetModes.arraySize];

            for (var index = 0;
                 index < targetModes.arraySize;
                 index++)
            {
                var targetMode =
                    targetModes.GetArrayElementAtIndex(index);

                var targetType =
                    targetMode.FindPropertyRelative("targetType");

                var targetingType =
                    targetMode.FindPropertyRelative("targetingType");

                var targetTypeName =
                    targetType.enumDisplayNames[targetType.enumValueIndex];

                var selectedTargetType =
                    (TargetType)targetType.enumValueIndex;

                labels[index] = selectedTargetType == TargetType.Caster
                    ? $"{index} - {targetTypeName}"
                    : $"{index} - {targetTypeName} / " +
                      $"{targetingType.enumDisplayNames[targetingType.enumValueIndex]}";
            }

            var selectedIndex = Mathf.Clamp(
                targetModeIndex.intValue,
                0,
                targetModes.arraySize - 1);

            targetModeIndex.intValue = EditorGUI.Popup(
                fieldRect,
                "Target Mode",
                selectedIndex,
                labels);

            y += height + Spacing;
        }

        private static void DrawStatusIdPopup(
            Rect position,
            ref float y,
            SerializedProperty statusId)
        {
            var height = EditorGUIUtility.singleLineHeight;

            var fieldRect = new Rect(
                position.x,
                y,
                position.width,
                height);

            StatusEffectIdPopup.Draw(
                fieldRect,
                statusId,
                "Status");

            y += height + Spacing;
        }

        private static void DrawEffectTypePopup(
            Rect position,
            ref float y,
            SerializedProperty effectType,
            bool isToggleSkill)
        {
            var allowedTypes = isToggleSkill
                ? ToggleSkillRules.AllowedEffectTypes
                : new[]
            {
                EffectType.DealDamage, EffectType.Push, EffectType.ApplyStatus,
                EffectType.Heal, EffectType.RestoreResource, EffectType.Shield,
                EffectType.ModifyStat, EffectType.ModifyVar, EffectType.Pull,
                EffectType.Fallback, EffectType.Charge, EffectType.Teleport,
                EffectType.CreateObject, EffectType.Summon, EffectType.ModifyActionValue,
                EffectType.DrainResource, EffectType.SacrificeHealthForResource,
                EffectType.ModifyResistance, EffectType.ApplyUniqueBuff,
                EffectType.BlockResourceRegeneration
            };
            var labels = Array.ConvertAll(allowedTypes, value => value.ToString());
            var currentType = (EffectType)effectType.enumValueIndex;
            var selectedIndex = Array.IndexOf(allowedTypes, currentType);
            var fieldRect = new Rect(position.x, y, position.width,
                EditorGUIUtility.singleLineHeight);
            if (selectedIndex < 0)
            {
                selectedIndex = 0;
                EditorGUI.HelpBox(fieldRect,
                    isToggleSkill
                        ? "This effect is not supported by toggle skills. Choose a persistent effect."
                        : "Legacy effect — choose an ApplyUniqueBuff variant.",
                    MessageType.Warning);
            }
            else
            {
                selectedIndex = EditorGUI.Popup(fieldRect, "Effect Type",
                    selectedIndex, labels);
                effectType.enumValueIndex = (int)allowedTypes[selectedIndex];
            }

            y += EditorGUIUtility.singleLineHeight + Spacing;
        }

        private static void DrawUniqueBuffTypePopup(
            Rect position,
            ref float y,
            SerializedProperty uniqueBuffType,
            bool isToggleSkill)
        {
            if (!isToggleSkill)
            {
                DrawField(position, ref y, uniqueBuffType);
                return;
            }

            var allowedTypes = ToggleSkillRules.AllowedUniqueBuffTypes;
            var labels = Array.ConvertAll(allowedTypes, value => value.ToString());
            var currentType = (UniqueBuffType)uniqueBuffType.enumValueIndex;
            var selectedIndex = Array.IndexOf(allowedTypes, currentType);
            var fieldRect = new Rect(position.x, y, position.width,
                EditorGUIUtility.singleLineHeight);

            if (selectedIndex < 0)
            {
                EditorGUI.HelpBox(fieldRect,
                    "This buff is consumed or instantaneous and cannot be a toggle.",
                    MessageType.Warning);
            }
            else
            {
                selectedIndex = EditorGUI.Popup(fieldRect, "Unique Buff Type",
                    selectedIndex, labels);
                uniqueBuffType.enumValueIndex = (int)allowedTypes[selectedIndex];
            }

            y += EditorGUIUtility.singleLineHeight + Spacing;
        }

        private static void AddHeight(
            ref float totalHeight,
            SerializedProperty property,
            bool includeChildren = false)
        {
            if (totalHeight > 0f)
            {
                totalHeight += Spacing;
            }

            totalHeight += EditorGUI.GetPropertyHeight(
                property,
                includeChildren);
        }

        private static bool UsesStatusDamageMultiplier(
            string statusId)
        {
            return statusId == StatusEffectIds.Bleed
                || statusId == StatusEffectIds.Poison
                || statusId == StatusEffectIds.Burn;
        }
    }
}

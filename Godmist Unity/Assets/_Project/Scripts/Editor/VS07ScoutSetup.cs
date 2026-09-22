using System;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Skills;
using Godmist.Data.World;
using Godmist.Systems.Combat;
using UnityEditor;
using UnityEngine;

// Explicit, repeatable content authoring. Does not run on import or change live saves.
public static partial class VS07ScoutSetup
{
    const string Root = "Assets/_Project/ScriptableObjects/";
    static string F(string name) => "<" + name + ">k__BackingField";
    static void Set(SerializedProperty p, object value)
    {
        if (value is UnityEngine.Object obj) p.objectReferenceValue = obj;
        else if (value is string text) p.stringValue = text;
        else if (value is bool flag) p.boolValue = flag;
        else if (value is Enum) p.intValue = Convert.ToInt32(value);
        else if (value is int integer) p.intValue = integer;
        else p.floatValue = Convert.ToSingle(value);
    }
    static T Load<T>(string path) where T : UnityEngine.Object =>
        AssetDatabase.LoadAssetAtPath<T>(Root + path) ?? throw new InvalidOperationException(path);
    static void Field(SerializedObject so, string name, object value) => Set(so.FindProperty(F(name)), value);
    static void Effect(SerializedObject so, int index, EffectType type, Action<SerializedProperty> configure = null)
    {
        var list = so.FindProperty("effects");
        list.arraySize = Math.Max(list.arraySize, index + 1);
        var p = list.GetArrayElementAtIndex(index);
        Set(p.FindPropertyRelative("effectType"), type);
        Set(p.FindPropertyRelative("baseChance"), 1f);
        Set(p.FindPropertyRelative("damageMultiplier"), 1f);
        Set(p.FindPropertyRelative("damageHitCount"), 1);
        Set(p.FindPropertyRelative("bonusDamageMultiplier"), 1f);
        Set(p.FindPropertyRelative("durationTurns"), 1);
        Set(p.FindPropertyRelative("stacks"), 1);
        Set(p.FindPropertyRelative("alwaysHits"), type != EffectType.DealDamage);
        configure?.Invoke(p);
    }
    static SkillData Skill(string file, string id, string name, int resource, int ap, int knives,
        int range, float accuracy, string description, Action<SerializedObject> configure,
        TargetType target = TargetType.Single)
    {
        var path = Root + "Skills/" + file + ".asset";
        var skill = AssetDatabase.LoadAssetAtPath<SkillData>(path);
        if (skill == null) { skill = ScriptableObject.CreateInstance<SkillData>(); AssetDatabase.CreateAsset(skill, path); }
        var so = new SerializedObject(skill);
        Field(so,"SkillId",id); Field(so,"DisplayName",name); Field(so,"TooltipDescription",description);
        Field(so,"ResourceCost",resource); Field(so,"ResourceType",ResourceType.Momentum);
        Field(so,"ActionCost",ap); Field(so,"KnifeCost",knives); Field(so,"SkillAccuracy",accuracy);
        Field(so,"AttackType",knives > 0 ? AttackType.Ranged : AttackType.Unknown);
        Field(so,"PointBlankAttackType",knives > 0 ? AttackType.Melee : AttackType.Unknown);
        Field(so,"PointBlankDamageMultiplier",1f); Field(so,"FreeKnivesInMelee",knives > 0);
        Field(so,"GuaranteedCritical",false); Field(so,"CanRecoverKnives",false);
        Field(so,"RequiresLineOfSight",target == TargetType.Single);
        if (skill.Icon == null) Field(so,"Icon",Load<SkillData>("Skills/SD_KnifeToss.asset").Icon);
        var modes = so.FindProperty("targetModes"); modes.ClearArray(); modes.arraySize = 1;
        var mode = modes.GetArrayElementAtIndex(0);
        Set(mode.FindPropertyRelative("targetType"),target);
        Set(mode.FindPropertyRelative("targetingType"),TargetingType.Enemies);
        var absolute = mode.FindPropertyRelative("absoluteRange");
        Set(absolute.FindPropertyRelative("shape"),RangeShape.Square);
        Set(absolute.FindPropertyRelative("minRange"),1); Set(absolute.FindPropertyRelative("maxRange"),range);
        so.FindProperty("effects").ClearArray();
        configure(so); so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(skill);
        var report = CombatDataValidator.ValidateSkill(skill);
        if (!report.IsValid) throw new InvalidOperationException(id + ": " + string.Join("; ",report.Errors));
        return skill;
    }

    [MenuItem("Godmist/Content/Configure VS-07 Scout starters")]
    public static void ConfigureStarters()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var basic = Skill("SD_KnifeToss","skill.knife_toss","Rzut Sztyletem",0,60,1,4,82,
            "Zadaje losowe obrażenia fizyczne. W zwarciu zadaje 60% obrażeń i nie zużywa sztyletu.", so => {
                Field(so,"PointBlankDamageMultiplier",.6f); Effect(so,0,EffectType.DealDamage); });
        var swap = Skill("SD_ScoutSwap","skill.scout.swap","Wymiana",20,20,0,1,100,
            "80% szansy na zamianę miejsc z sąsiadującym przeciwnikiem.", so => {
                Set(so.FindProperty("targetModes").GetArrayElementAtIndex(0).FindPropertyRelative("absoluteRange").FindPropertyRelative("shape"),RangeShape.Radial);
                Effect(so,0,EffectType.SwapPositions,p => Set(p.FindPropertyRelative("baseChance"),.8f)); });
        var smoke = Skill("SD_ScoutSmoke","skill.scout.smoke","Chmura Dymu",65,30,0,0,100,
            "80% szansy na obniżenie celności każdego przeciwnika o 20 na 3 tury.", so =>
                Effect(so,0,EffectType.ModifyStat,p => {
                    Set(p.FindPropertyRelative("baseChance"),.8f); Set(p.FindPropertyRelative("durationTurns"),3);
                    Set(p.FindPropertyRelative("modifierStatType"),CharacterStatType.WeaponAccuracy);
                    Set(p.FindPropertyRelative("modifierOperation"),StatModifierOperation.FlatAbsolute);
                    Set(p.FindPropertyRelative("modifierValue"),-20f);
                }),TargetType.All);
        var hook = Skill("SD_ScoutHook","skill.scout.hook","Linka z Hakiem",25,30,0,6,100,
            "75% szansy na przyciągnięcie wroga o 3 pola. Wskazany własny sztylet odzyskuje bez losowania.",so => {
                Field(so,"CanRecoverKnives",true); Effect(so,0,EffectType.Pull,p => {
                    Set(p.FindPropertyRelative("baseChance"),.75f); Set(p.FindPropertyRelative("displacementDistance"),3);
                }); });
        var aimed = Skill("SD_ScoutAimedThrow","skill.scout.aimed_throw","Celny Rzut",50,70,1,4,85,
            "Zadaje losowe obrażenia fizyczne. Trafienie jest zawsze krytyczne. W zwarciu nie zużywa sztyletu.",so => {
                Field(so,"GuaranteedCritical",true); Effect(so,0,EffectType.DealDamage); });
        var starters = new[] {basic,swap,smoke,hook,aimed};
        var character = new SerializedObject(Load<CharacterDefinition>("Characters/CD_Jerzy.asset"));
        Field(character,"BasicAttackSkill",basic);
        var list = character.FindProperty(F("StartingSkills")); list.arraySize = starters.Length;
        for(var i=0;i<starters.Length;i++) list.GetArrayElementAtIndex(i).objectReferenceValue=starters[i];
        character.ApplyModifiedPropertiesWithoutUndo();
        var scout = new SerializedObject(Load<CharacterClassData>("Classes/CCD_Scout.asset"));
        Field(scout,"BasicAttackSkill",basic); scout.ApplyModifiedPropertiesWithoutUndo();
        var catalog = new SerializedObject(Load<GameContentCatalog>("World/GC_MainContentCatalog.asset"));
        list = catalog.FindProperty(F("SkillDefinitions"));
        foreach(var skill in starters)
        {
            var found = false;
            for(var i=0;i<list.arraySize;i++) found |= list.GetArrayElementAtIndex(i).objectReferenceValue == skill;
            if (found) continue;
            list.InsertArrayElementAtIndex(list.arraySize); list.GetArrayElementAtIndex(list.arraySize-1).objectReferenceValue=skill;
        }
        catalog.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssets();
    }
}

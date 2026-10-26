using System;
using System.Linq;
using System.Collections.Generic;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Skills;
using Godmist.Data.World;
using Godmist.Data.Combat;
using Godmist.Systems.Combat;
using UnityEditor;
using UnityEngine;

// Explicit, repeatable content authoring. Does not run on import or change live saves.
public static partial class VS09PaladinSetup
{
    // Copy-only update: do not rerun full skill authoring over current balance.
    [MenuItem("Godmist/Content/Refresh control resistance wording")]
    public static void RefreshControlResistanceWording()
    {
        var skill = Load<SkillData>("Skills/SD_PaladinCrushingBlow.asset");
        var serialized = new SerializedObject(skill);
        var description = serialized.FindProperty(F("TooltipDescription"));
        var updated = description.stringValue.Replace("odporności na hard CC", "odporności na kontrolę");
        if (description.stringValue == updated) return;
        description.stringValue = updated;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssetIfDirty(skill);
    }

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
        Field(so,"ResourceCost",resource); Field(so,"ResourceType",ResourceType.Mana);
        Field(so,"ActionCost",ap); Field(so,"KnifeCost",knives); Field(so,"SkillAccuracy",accuracy);
        Field(so,"AttackType",knives > 0 ? AttackType.Melee : AttackType.Melee);
        Field(so,"PointBlankAttackType",knives > 0 ? AttackType.Melee : AttackType.Unknown);
        Field(so,"PointBlankDamageMultiplier",1f); Field(so,"FreeKnivesInMelee",false);
        Field(so,"IsToggle",false); Field(so,"GuaranteedCritical",false); Field(so,"CanRecoverKnives",false);
        Field(so,"RequiresLineOfSight",target == TargetType.Single || target == TargetType.MultipleChosen);
        if (skill.Icon == null) Field(so,"Icon",Load<SkillData>("Skills/SD_KnifeToss.asset").Icon);
        var modes = so.FindProperty("targetModes"); modes.ClearArray(); modes.arraySize = 1;
        var mode = modes.GetArrayElementAtIndex(0);
        Set(mode.FindPropertyRelative("targetType"),target);
        Set(mode.FindPropertyRelative("targetingType"),TargetingType.Enemies);
        Set(mode.FindPropertyRelative("includesCaster"),false);
        var absolute = mode.FindPropertyRelative("absoluteRange");
        Set(absolute.FindPropertyRelative("shape"),RangeShape.Radial);
        Set(absolute.FindPropertyRelative("minRange"),1); Set(absolute.FindPropertyRelative("maxRange"),range);
        so.FindProperty("effects").ClearArray();
        configure(so); Godmist.Editor.ContextualSkillHitSetup.Apply(so); so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(skill);
        var report = CombatDataValidator.ValidateSkill(skill);
        if (!report.IsValid) throw new InvalidOperationException(id + ": " + string.Join("; ",report.Errors));
        return skill;
    }

    static void Ally(SerializedObject so,bool caster=false)
    {
        var mode=so.FindProperty("targetModes").GetArrayElementAtIndex(0);
        Set(mode.FindPropertyRelative("targetingType"),TargetingType.Allies);Set(mode.FindPropertyRelative("includesCaster"),caster);
    }
    static void Status(SerializedObject so,int index,string id,float chance,int turns,float damage=0,bool hit=true)
    {
        Effect(so,index,EffectType.ApplyStatus,p=>{Set(p.FindPropertyRelative("statusId"),id);Set(p.FindPropertyRelative("baseChance"),chance);Set(p.FindPropertyRelative("durationTurns"),turns);Set(p.FindPropertyRelative("statusDamageMultiplier"),damage);if(hit)RequireHit(p);});
    }
    static void RequireHit(SerializedProperty effect)
    {
        var list=effect.FindPropertyRelative("conditions");list.arraySize=1;var c=list.GetArrayElementAtIndex(0);
        Set(c.FindPropertyRelative("conditionType"),EffectConditionType.ReferencedEffectSucceeded);Set(c.FindPropertyRelative("referencedEffectIndex"),0);
    }
    static void Magic(SerializedObject so, float multiplier=1f) => Effect(so,0,EffectType.DealDamage,p=>{Set(p.FindPropertyRelative("damageType"),DamageType.Magical);Set(p.FindPropertyRelative("damageMultiplier"),multiplier);});
    static void Toggle(SerializedObject so) { Field(so,"IsToggle",true); Field(so,"ToggleUpkeepFlatResourceCost",0);Field(so,"ToggleUpkeepMaxResourcePercent",0f); }
    static void Rect(SerializedProperty range,int length,int width) {Set(range.FindPropertyRelative("shape"),RangeShape.Rectangle);Set(range.FindPropertyRelative("minRange"),1);Set(range.FindPropertyRelative("maxRange"),length);Set(range.FindPropertyRelative("rectangleWidth"),width);}
    static SummonedObjectDefinition FieldDefinition()
    {
        var path=Root+"Skills/SOD_JudgementField.asset";var asset=AssetDatabase.LoadAssetAtPath<SummonedObjectDefinition>(path);
        if(asset==null){asset=ScriptableObject.CreateInstance<SummonedObjectDefinition>();AssetDatabase.CreateAsset(asset,path);}
        var so=new SerializedObject(asset);Set(so.FindProperty("objectId"),PaladinCombatSystem.FieldId);Set(so.FindProperty("displayName"),"Pole Osądu");
        Set(so.FindProperty("defaultDurationTurns"),4);Set(so.FindProperty("turnSpeed"),40f);Set(so.FindProperty("maxHits"),int.MaxValue);Set(so.FindProperty("blocksMovement"),false);Set(so.FindProperty("blocksLineOfSight"),false);
        Set(so.FindProperty("effectTargetingType"),TargetingType.Enemies);Set(so.FindProperty("effectRange").FindPropertyRelative("shape"),RangeShape.Square);Set(so.FindProperty("effectRange").FindPropertyRelative("maxRange"),2);
        var icon=Load<SkillData>("Skills/SD_KnifeToss.asset").Icon;Set(so.FindProperty("icon"),icon);Set(so.FindProperty("worldSprite"),icon);so.FindProperty("effects").ClearArray();so.ApplyModifiedPropertiesWithoutUndo();return asset;
    }
    [MenuItem("Godmist/Content/Configure VS-09 Paladin")]
    public static void Configure()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
        var field=FieldDefinition();var skills=new Dictionary<string,SkillData>();
        skills["verdict"]=Skill("SD_PaladinVerdict","skill.paladin.verdict","Wyrok",0,60,0,1,83,"Losowe obrażenia fizyczne.",so=>Effect(so,0,EffectType.DealDamage));
        skills["crushing_blow"]=Skill("SD_PaladinCrushingBlow","skill.paladin.crushing_blow","Miażdżący Cios",30,70,0,1,75,"Minimalne obrażenia fizyczne; najpierw 90% szansy na obniżenie odporności na kontrolę o 30% na 3 tury, następnie 50% szansy na ogłuszenie na 2 tury.",so=>{Effect(so,0,EffectType.DealDamage,p=>Set(p.FindPropertyRelative("damageValueSource"),DamageValueSource.MinAttack));Effect(so,1,EffectType.ModifyResistance,p=>{Set(p.FindPropertyRelative("resistanceType"),ResistanceType.Suppression);Set(p.FindPropertyRelative("modifierValue"),-.3f);Set(p.FindPropertyRelative("durationTurns"),3);Set(p.FindPropertyRelative("baseChance"),.9f);RequireHit(p);});Status(so,2,StatusEffectIds.Stun,.5f,2);});
        skills["heal"]=Skill("SD_PaladinHeal","skill.paladin.heal","Uzdrowienie",30,40,0,8,100,"Przywraca 100–140% siły leczenia (średniego ataku).",so=>{Ally(so,true);Effect(so,0,EffectType.PaladinHeal,p=>Set(p.FindPropertyRelative("damageMultiplier"),1.2f));});
        skills["transfusion"]=Skill("SD_PaladinTransfusion","skill.paladin.transfusion","Święta Transfuzja",60,65,0,5,100,"Losowe obrażenia magiczne; rzucający odzyskuje 200% faktycznie zadanych obrażeń jako HP.",so=>{Magic(so);Set(so.FindProperty("effects").GetArrayElementAtIndex(0).FindPropertyRelative("lifeStealPercent"),2f);});
        skills["reflection_shield"]=Skill("SD_PaladinReflectionShield","skill.paladin.reflection_shield","Ochronny Blask",40,40,0,8,100,"Zwiększa unik sojusznika o 40% na 5 tur.",so=>{Ally(so);Effect(so,0,EffectType.ModifyStat,p=>{Set(p.FindPropertyRelative("modifierStatType"),CharacterStatType.Dodge);Set(p.FindPropertyRelative("modifierOperation"),StatModifierOperation.Percent);Set(p.FindPropertyRelative("modifierValue"),.4f);Set(p.FindPropertyRelative("durationTurns"),5);});});
        skills["group_heal"]=Skill("SD_PaladinGroupHeal","skill.paladin.group_heal","Grupowe Uzdrowienie",35,60,0,0,100,"Przywraca wszystkim sojusznikom 42–58% siły leczenia.",so=>{Ally(so,true);Effect(so,0,EffectType.PaladinHeal,p=>Set(p.FindPropertyRelative("damageMultiplier"),.5f));},TargetType.All);
        skills["holy_regeneration"]=Skill("SD_PaladinHolyRegeneration","skill.paladin.holy_regeneration","Święta Regeneracja",50,55,0,8,100,"Przywraca łącznie 40% maksymalnego HP w ciągu 4 tur. Natychmiastowa Ulga oddaje część tej sumy natychmiast.",so=>{Ally(so,true);Effect(so,0,EffectType.PaladinRegeneration);});
        skills["blessing"]=Skill("SD_PaladinBlessing","skill.paladin.blessing","Błogosławieństwo",40,40,0,7,100,"Sojusznik zadaje 10% więcej i otrzymuje 10% mniej obrażeń przez 4 tury.",so=>{Ally(so);Effect(so,0,EffectType.PaladinBlessing);});
        skills["judgement_field"]=Skill("SD_PaladinJudgementField","skill.paladin.judgement_field","Pole Osądu",35,60,0,0,100,"Jedno pole na mapie, nowe zastępuje poprzednie. Przez 4 tury: -5 szybkości i 50% losowych obrażeń magicznych przeciwnikom w kwadracie o zasięgu 2.",so=>Effect(so,0,EffectType.CreateObject,p=>{Set(p.FindPropertyRelative("summonedObjectDefinition"),field);Set(p.FindPropertyRelative("maxActiveObjects"),1);var r=p.FindPropertyRelative("objectPlacementRange");Set(r.FindPropertyRelative("shape"),RangeShape.Radial);Set(r.FindPropertyRelative("minRange"),0);Set(r.FindPropertyRelative("maxRange"),4);}),TargetType.Caster);
        skills["blinding_light"]=Skill("SD_PaladinBlindingLight","skill.paladin.blinding_light","Oślepiające Światło",60,75,0,5,100,"Łańcuch do 3 celów: 60% losowych obrażeń magicznych i 85% szansy na -15 celności przez 3 tury.",so=>{var mode=so.FindProperty("targetModes").GetArrayElementAtIndex(0);Set(mode.FindPropertyRelative("chainTargets"),3);var r=mode.FindPropertyRelative("relativeRange");Set(r.FindPropertyRelative("shape"),RangeShape.Square);Set(r.FindPropertyRelative("minRange"),1);Set(r.FindPropertyRelative("maxRange"),2);Magic(so,.6f);Effect(so,1,EffectType.ModifyStat,p=>{Set(p.FindPropertyRelative("modifierStatType"),CharacterStatType.WeaponAccuracy);Set(p.FindPropertyRelative("modifierOperation"),StatModifierOperation.FlatAbsolute);Set(p.FindPropertyRelative("modifierValue"),-15f);Set(p.FindPropertyRelative("durationTurns"),3);Set(p.FindPropertyRelative("baseChance"),.85f);RequireHit(p);});},TargetType.Chain);
        skills["grounding_wave"]=Skill("SD_PaladinGroundingWave","skill.paladin.grounding_wave","Fala Przyciągająca",65,75,0,6,100,"Przyciąga przeciwników o 1 pole ku środkowi wybranego kwadratu o zasięgu 5.",so=>{var r=so.FindProperty("targetModes").GetArrayElementAtIndex(0).FindPropertyRelative("relativeRange");Set(r.FindPropertyRelative("shape"),RangeShape.Square);Set(r.FindPropertyRelative("minRange"),1);Set(r.FindPropertyRelative("maxRange"),5);Effect(so,0,EffectType.Pull,p=>Set(p.FindPropertyRelative("displacementDistance"),1));},TargetType.AreaAroundTile);
        ConfigureTrees(skills);
        var character=new SerializedObject(Load<CharacterDefinition>("Characters/CD_Paladin.asset"));var starters=character.FindProperty(F("StartingSkills"));starters.ClearArray();foreach(var id in new[]{"verdict","crushing_blow","heal","transfusion","reflection_shield"}){starters.InsertArrayElementAtIndex(starters.arraySize);starters.GetArrayElementAtIndex(starters.arraySize-1).objectReferenceValue=skills[id];}Field(character,"BasicAttackSkill",skills["verdict"]);character.ApplyModifiedPropertiesWithoutUndo();
        var cls=new SerializedObject(Load<CharacterClassData>("Classes/CCD_Paladin.asset"));Field(cls,"BasicAttackSkill",skills["verdict"]);var trees=cls.FindProperty(F("SkillTrees"));trees.arraySize=2;trees.GetArrayElementAtIndex(0).objectReferenceValue=Load<SkillTreeDefinition>("Skills/SkillTrees/ST_Paladin_Priest.asset");trees.GetArrayElementAtIndex(1).objectReferenceValue=Load<SkillTreeDefinition>("Skills/SkillTrees/ST_Paladin_Crusader.asset");cls.ApplyModifiedPropertiesWithoutUndo();
        var catalog=new SerializedObject(Load<GameContentCatalog>("World/GC_MainContentCatalog.asset"));var list=catalog.FindProperty(F("SkillDefinitions"));foreach(var skill in skills.Values){var found=false;for(var i=0;i<list.arraySize;i++)found|=list.GetArrayElementAtIndex(i).objectReferenceValue==skill;if(!found){list.InsertArrayElementAtIndex(list.arraySize);list.GetArrayElementAtIndex(list.arraySize-1).objectReferenceValue=skill;}}catalog.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssets();
    }
    static readonly string[][] Nodes = {
        new[]{"priest","group_heal","Grupowe Uzdrowienie","50% Leczenie","","group_heal","1","1","1"},
        new[]{"priest","healing_power","Siła Leczenia","Zwiększa siłę Uzdrowienia oraz Grupowego Leczenia o 15%/30%","group_heal","","1","2","2"},
        new[]{"priest","healing_aura","Aura Leczenia","Zapewnia 7%/15% leczenie przyległym sojusznikom w każdej turze","","","1","3","2"},
        new[]{"priest","healing_chain","Łańcuch Leczenia","Każdy kolejny przyległy sojusznik przedłuża obszar działania Aury Leczenia do siebie","healing_aura","","1","4","1"},
        new[]{"priest","holy_artifacts","Przewodniki Łaski","Obiekty stworzone przez gracza (sztylety, runy, obiekty natury, etc.) liczone są do Łańcucha Leczenia","healing_chain","","1","5","1"},
        new[]{"priest","battle_zeal","Ożywcza Łaska","Grupowe Uzdrowienie zwiększa szybkość o 10 na jedną turą, a potem o 5 na jedną turę","group_heal","","2","1","1"},
        new[]{"priest","universal_grace","Wszechstronna Łaska","Grupowe Uzdrowienie zwiększa obronę fizyczną i magiczną i unik na 2 tury o 2%/5%/10%","battle_zeal","","2","2","3"},
        new[]{"priest","unexpected_grace","Niespodziewana Łaska","Daje szansę na leczenie krytyczne umiejętnościom leczniczym równą 50%/100%/150% szansy krytycznej","","","2","3","3"},
        new[]{"priest","critical_focus","Precyzja Uzdrowiciela","Leczenia obejmujące tylko jedną osobę mają 50%/100% większą szansę na leczenie krytyczne","unexpected_grace,healer_focus","","2","4","2"},
        new[]{"priest","healer_focus","Skupienie Uzdrowiciela","Leczenia obejmujące tylko jedną osobę mają zwiększoną siłę o 15%/30%","","","2","5","2"},
        new[]{"priest","haste","Natychmiastowa Ulga","Przywraca 15%/25%/40% zdrowia odnawianego przez Świętą Regenerację od razu po użyciu","holy_regeneration","","3","1","3"},
        new[]{"priest","holy_regeneration","Święta Regeneracja","Regeneruje 40% zdrowia sojusznika przez 4 tury","","holy_regeneration","3","2","1"},
        new[]{"priest","anointed_warrior","Namaszczony Wojownik","Każdy leczniczy cios krytyczny zwiększa obrażenia uleczonego celu o 10%/20% na 2 tury","unexpected_grace","","3","3","2"},
        new[]{"priest","persistent_healer","Ratunek w Potrzebie","Zmniejsza koszt punktów akcji Uzdrowienia o 25%/50%/100%, jeśli cel otrzymał obrażenia większe niż 20% maksymalnego zdrowia w jednej turze. Użycie Uzdrowienia na tym celu zdejmuje zniżkę.","","","3","4","3"},
        new[]{"priest","encouragement","Pokrzepienie","Leczenia obejmujące tylko jedną osobę przyśpieszają jej akcję o 20%","","","3","5","1"},
        new[]{"priest","lasting_effect","Trwała Regeneracja","Święta Regeneracja po upłynięciu czasu trwania leczy 25% początkowej wartości przez kolejne 4/2 tury","haste","","4","1","2"},
        new[]{"priest","salvation","Łaska Zwrotna","Rzucający otrzymuje dodatkowo 50% odnowionego surowca przez Łaskę","grace_collection","","4","2","1"},
        new[]{"priest","grace_collection","Gromadzenie Łaski","Otrzymywanie i zadawanie obrażeń pod działaniem Błogosławieństwa daje ładunek Łaski (max 6), po upłynięciu czasu trwania, cel regeneruje 4/8 surowca za każdy ładunek","blessing","","4","3","2"},
        new[]{"priest","blessing","Błogosławieństwo","Zwiększa zadawane obrażenia o 10%, zmniejsza przyjmowane obrażenia o 10% na 4 tury","","blessing","4","4","1"},
        new[]{"priest","strong_blessing","Silne Błogosławieństwo","Wzmacnia efekt Błogosławieństwa do 15%/20%","blessing","","4","5","2"},
        new[]{"crusader","judgement_field","Pole Osądu","Tworzy na 4 tury obszar, w którym przeciwnicy są spowolnieni o 5 i otrzymują 50% losowych obrażeń magicznych co turę","","judgement_field","1","1","1"},
        new[]{"crusader","field_range","Zasięg Osądu","Zwiększa obszar Pola Osądu o 1/2","judgement_field","","1","2","2"},
        new[]{"crusader","gravedigger","Pogromca Nieumarłych","Zwiększa obrażenia zadawane Nieumarłym o 15%/30%","","","1","3","2"},
        new[]{"crusader","hatred","Nienawiść Przeklętych","Atakowanie Nieumarłych i Demonów ma 50% szansy na nałożenie prowokacji na 1 turę","gravedigger,exorcist","","1","4","1"},
        new[]{"crusader","exorcist","Egzorcysta","Zwiększa obrażenia zadawane Demonom o 15%/30%","","","1","5","2"},
        new[]{"crusader","judgement_release","Ostateczny Osąd","Po ukończeniu czasu działania Pole Osądu zadaje przeciwnikom w nim 50/100/150% losowych obrażeń magicznych","judgement_field","","2","1","3"},
        new[]{"crusader","blinding_light","Oślepiające Światło","Zadaje 60% losowych obrażeń magicznych, [85%] Celność -15 3t","","blinding_light","2","2","1"},
        new[]{"crusader","expose","Odsłonięcie Słabości","Przeciwnicy objęci Oślepiającym Światłem otrzymują 25%/50% więcej obrażeń z następnego ataku","blinding_light","","2","3","2"},
        new[]{"crusader","advantage","Przewaga Żywotności","Zwiększa obrażenia zadawane przeciwnikom o mniejszej ilości maksymalnego zdrowia o 1% za każde 2% różnicy (max 50%)","","","2","4","1"},
        new[]{"crusader","harsh_verdict","Srogi Wyrok","Wyrok ma 20%/40% szansy na odepchnięcie przeciwnika o 1 pole","","","2","5","2"},
        new[]{"crusader","exhaustion","Ciężar Osądu","Pole Osądu w przedostatniej turze działania ogłusza na 1/2 tury","judgement_release","","3","1","2"},
        new[]{"crusader","blinding","Obezwładniający Blask","Oślepiające Światło ma 90% szansy na ogłuszenie na 3 tury, każdy kolejny cel ma 20% mniejszą szansę na zostanie ogłuszonym","blinding_light","","3","2","1"},
        new[]{"crusader","torment_max","Narastające Udręczenie","Zwiększa maksymalną ilość Udręczenia do 6/8/10","torment","","3","3","3"},
        new[]{"crusader","torment","Udręczenie","Ataki nakładają Udręczenie, które zmniejsza obronę o 2% i unik o 2, do 4 razy","","","3","4","1"},
        new[]{"crusader","severe_judge","Surowy Sędzia","Ciosy krytyczne zwiększają szansę na Srogi Wyrok o 40% i zwiększają siłę odepchnięcia o 1","harsh_verdict","","3","5","1"},
        new[]{"crusader","grounding_wave","Fala Przyciągająca","Przyciąga o 1 w kierunku środku obszaru działania","","grounding_wave","4","1","1"},
        new[]{"crusader","destruction_wave","Wzmocnione Przyciąganie","Zwiększa siłę przyciągnięcia Fali Przyciągającej o 1","grounding_wave","","4","2","1"},
        new[]{"crusader","kicking_downed","Bezwzględny Wyrok","Zwiększa obrażenia zadawane ogłuszonym przeciwnikom o 15%/30%/50%","","","4","3","3"},
        new[]{"crusader","torment_damage","Przełamana Obrona","Nałożenie co najmniej 4 Udręczenia sprawia że przeciwnik otrzymuje dodatkowe obrażenia nieuchronne o wartości 30%/60%/100% obrażeń które zmitygowałaby zabrana obrona","torment","","4","4","3"},
        new[]{"crusader","control","Pewność Egzekutora","Zwiększa szansę na cios krytyczny o 50%/100%[p] jeśli przeciwnik ma mniej aktualnego zdrowia","severe_judge","","4","5","2"},
    };
    static void ConfigureTrees(Dictionary<string,SkillData> skills)
    {
        foreach(var treeName in new[]{"priest","crusader"})
        {
            var path=Root+"Skills/SkillTrees/ST_Paladin_"+(treeName=="priest"?"Priest":"Crusader")+".asset";
            var tree=AssetDatabase.LoadAssetAtPath<SkillTreeDefinition>(path);if(tree==null){tree=ScriptableObject.CreateInstance<SkillTreeDefinition>();AssetDatabase.CreateAsset(tree,path);}
            var so=new SerializedObject(tree);Field(so,"Id","tree.paladin."+treeName);Field(so,"DisplayName",treeName=="priest"?"Kapłan":"Krzyżowiec");
            var global=so.FindProperty(F("GlobalPassive"));Set(global.FindPropertyRelative(F("DisplayName")),"Pasyw drzewka");Set(global.FindPropertyRelative(F("Description")),treeName=="priest"?"Za punkt: +1% siły leczenia; 1% faktycznego leczenia przywraca HP rzucającego.":"Za punkt: +1% obrażeń przeciw nieumarłym i demonom.");global.FindPropertyRelative(F("PassivesPerPoint")).ClearArray();
            var list=so.FindProperty(F("Nodes"));list.ClearArray();
            foreach(var node in Nodes)
            {
                if(node[0]!=treeName)continue;list.InsertArrayElementAtIndex(list.arraySize);var p=list.GetArrayElementAtIndex(list.arraySize-1);
                Set(p.FindPropertyRelative(F("Id")),"node.paladin."+treeName+"."+node[1]);Set(p.FindPropertyRelative(F("DisplayName")),node[2]);Set(p.FindPropertyRelative(F("Description")),node[3]);
                Set(p.FindPropertyRelative(F("Tier")),int.Parse(node[6]));Set(p.FindPropertyRelative(F("Column")),int.Parse(node[7]));Set(p.FindPropertyRelative(F("MaxRanks")),int.Parse(node[8]));
                var grantedSkill=node[5].Length>0?skills[node[5]]:null;
                p.FindPropertyRelative(F("GrantedSkill")).objectReferenceValue=grantedSkill;
                var nodeIcon=grantedSkill!=null&&grantedSkill.Icon!=null?grantedSkill.Icon:VS22RemainingPassiveSetup.TryLoadPassiveIcon("node.paladin."+treeName+"."+node[1]);
                p.FindPropertyRelative(F("Icon")).objectReferenceValue=nodeIcon!=null?nodeIcon:skills["verdict"].Icon;
                p.FindPropertyRelative(F("PassiveEffects")).ClearArray();
                var req=p.FindPropertyRelative(F("Requirements"));req.ClearArray();foreach(var id in node[4].Split(',')){if(id.Length==0)continue;req.InsertArrayElementAtIndex(req.arraySize);var q=req.GetArrayElementAtIndex(req.arraySize-1);Set(q.FindPropertyRelative(F("NodeId")),"node.paladin."+treeName+"."+id);Set(q.FindPropertyRelative(F("MinimumRanks")),1);}
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

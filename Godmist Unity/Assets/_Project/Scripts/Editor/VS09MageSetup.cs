using System;
using System.Linq;
using System.Collections.Generic;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Skills;
using Godmist.Data.World;
using Godmist.Systems.Combat;
using UnityEditor;
using UnityEngine;

// Explicit, repeatable content authoring. Does not run on import or change live saves.
public static partial class VS09MageSetup
{
    [MenuItem("Godmist/Content/Apply Mana Economy Balance")]
    public static void ApplyManaEconomyBalance()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var mage = Load<CharacterDefinition>("Characters/CD_Mage.asset");
        var character = new SerializedObject(mage);
        Set(character.FindProperty(F("BaseStats")).FindPropertyRelative(F("ResourceRegeneration")), MageCombatSystem.BaseManaRegeneration);
        character.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssetIfDirty(mage);

        var tree = Load<SkillTreeDefinition>("Skills/SkillTrees/ST_Mage_Warlock.asset");
        var serialized = new SerializedObject(tree);
        Set(serialized.FindProperty(F("GlobalPassive")).FindPropertyRelative(F("Description")),
            "Za punkt: -0,5% kosztu many, +1% obrażeń przeciw pojedynczemu celowi.");
        var nodes = serialized.FindProperty(F("Nodes"));
        for (var i = 0; i < nodes.arraySize; i++)
        {
            var node = nodes.GetArrayElementAtIndex(i);
            if (node.FindPropertyRelative(F("Id")).stringValue == "node.mage.warlock.energy_saving")
                Set(node.FindPropertyRelative(F("Description")), "Zwiększa regenerację many o 0,25%/0,5% za każdy brakujący 1% many");
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssetIfDirty(tree);
    }

    [MenuItem("Godmist/Content/Apply Magic Shield playtest balance")]
    public static void ApplyShieldPlaytestBalance()
    {
        var skill = Load<SkillData>("Skills/SD_MageShield.asset");
        var serialized = new SerializedObject(skill);
        var effects = serialized.FindProperty("effects");
        if (effects.arraySize != 1 || effects.GetArrayElementAtIndex(0).FindPropertyRelative("effectType").intValue != (int)EffectType.Shield)
            throw new InvalidOperationException("Magic Shield no longer has its expected single shield effect.");
        var effect = effects.GetArrayElementAtIndex(0);
        Set(effect.FindPropertyRelative("shieldAverageAttackMultiplier"), 2.5f);
        Set(effect.FindPropertyRelative("durationTurns"), CombatShieldSystem.MageShieldDuration);
        Field(serialized, "TooltipDescription", "Tarcza o wartości 250% średniego ataku na 3 własne tury odbiorcy. Ponowne rzucenie zastępuje poprzednią Magiczną Tarczę i odnawia czas.");
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
    [MenuItem("Godmist/Content/Configure VS-09 Mage")]
    public static void Configure()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
        var skills=new Dictionary<string,SkillData>();
        skills["energy_orb"]=Skill("SD_MageEnergyOrb","skill.mage.energy_orb","Kula Energii",5,75,0,6,100,"Zadaje losowe obrażenia magiczne.",so=>Magic(so));
        skills["fireball"]=Skill("SD_MageFireball","skill.mage.fireball","Kula Ognia",40,75,0,6,100,"Losowe obrażenia magiczne; 75% szansy na podpalenie 25% średniego ataku na turę przez 3 tury.",so=>{Magic(so);Status(so,1,StatusEffectIds.Burn,.75f,3,.25f);});
        skills["focus"]=Skill("SD_MageFocus","skill.mage.focus","Przemiana Witalności",0,85,0,0,100,"Poświęca 10% maksymalnego HP i odzyskuje dwa razy tyle MP.",so=>Effect(so,0,EffectType.SacrificeHealthForResource,p=>{Set(p.FindPropertyRelative("healthSacrificePercent"),.1f);Set(p.FindPropertyRelative("resourceRestorePerSacrificedHealth"),2f);}),TargetType.Caster);
        skills["shield"]=Skill("SD_MageShield","skill.mage.shield","Magiczna Tarcza",40,90,0,6,100,"Tarcza o wartości 250% średniego ataku na 3 własne tury odbiorcy. Ponowne rzucenie zastępuje poprzednią Magiczną Tarczę i odnawia czas.",so=>{Ally(so,true);Effect(so,0,EffectType.Shield,p=>{Set(p.FindPropertyRelative("shieldAverageAttackMultiplier"),2.5f);Set(p.FindPropertyRelative("durationTurns"),CombatShieldSystem.MageShieldDuration);});});
        skills["tiring"]=Skill("SD_MageTiringSpells","skill.mage.tiring","Wycieńczające Zaklęcia",0,0,0,0,100,Godmist.Systems.Combat.MageTiringSpellSystem.Description,so=>{Toggle(so);Effect(so,0,EffectType.BlockResourceRegeneration);},TargetType.Caster);
        skills["offering"]=Skill("SD_MageOffering","skill.mage.offering","Ofiara",0,0,0,0,100,"Zwiększa zadawane i otrzymywane obrażenia o 30%.",so=>{Toggle(so);Effect(so,0,EffectType.ApplyUniqueBuff,p=>Set(p.FindPropertyRelative("uniqueBuffType"),UniqueBuffType.MageOffering));},TargetType.Caster);
        skills["magic_focus"]=Skill("SD_MageMagicFocus","skill.mage.magic_focus","Przemiana Witalności Magii",35,40,0,0,100,"Zawsze największe obrażenia przez 3 tury.",so=>Effect(so,0,EffectType.ApplyUniqueBuff,p=>{Set(p.FindPropertyRelative("uniqueBuffType"),UniqueBuffType.MageMaximumDamage);Set(p.FindPropertyRelative("durationTurns"),3);}),TargetType.Caster);
        skills["sacrifice"]=Skill("SD_MageSacrifice","skill.mage.sacrifice","Przejęcie Mocy",40,40,0,8,100,"Sojusznik zadaje o 20% mniej obrażeń, rzucający o 20% więcej przez 3 tury.",so=>{Ally(so);Effect(so,0,EffectType.MageSacrifice);},TargetType.MultipleChosen);
        skills["energy_split"]=Skill("SD_MageEnergySplit","skill.mage.energy_split","Roztrojenie Energii",35,70,0,8,100,"Trzy promienie: prosto i dwa ukośne; 70% losowych obrażeń magicznych.",so=>{Magic(so,.7f);Set(so.FindProperty("targetModes").GetArrayElementAtIndex(0).FindPropertyRelative("absoluteRange").FindPropertyRelative("shape"),RangeShape.ThreeRays);},TargetType.AreaAroundCaster);
        skills["ritual"]=Skill("SD_MageRitual","skill.mage.ritual","Rytuał Odnowy",50,30,0,0,100,"Drużyna poświęca 10% maksymalnego HP i regeneruje łącznie 30% HP przez 5 tur.",so=>{Ally(so,true);Effect(so,0,EffectType.MageRitual,p=>{Set(p.FindPropertyRelative("percentageValue"),.3f);Set(p.FindPropertyRelative("durationTurns"),5);});},TargetType.All);
        skills["fire_aura"]=Skill("SD_MageFireAura","skill.mage.fire_aura","Ognista Aura",65,45,0,6,100,"Aura na 3 tury: wrogowie w zasięgu 1 od sojusznika mają 50% szansy na podpalenie 90%.",so=>{Ally(so);Effect(so,0,EffectType.MageFireAura,p=>{Set(p.FindPropertyRelative("statusId"),StatusEffectIds.Burn);Set(p.FindPropertyRelative("baseChance"),.5f);Set(p.FindPropertyRelative("durationTurns"),3);Set(p.FindPropertyRelative("statusDamageMultiplier"),.9f);Set(p.FindPropertyRelative("auraRadius"),1);Set(p.FindPropertyRelative("auraTargetingType"),TargetingType.Enemies);});});
        skills["fire_arrow"]=Skill("SD_MageFireArrow","skill.mage.fire_arrow","Ognista Strzała",40,60,0,6,100,"Losowe obrażenia magiczne, 30% penetracji; 70% podpalenie 30% na 2 tury i -15% obrażeń na 4 tury.",so=>{Magic(so);Set(so.FindProperty("effects").GetArrayElementAtIndex(0).FindPropertyRelative("bonusPenetration"),.3f);Status(so,1,StatusEffectIds.Burn,.7f,2,.3f);Effect(so,2,EffectType.ModifyVar,p=>{Set(p.FindPropertyRelative("modifierVariableType"),GameplayVariableType.DamageDealtPercent);Set(p.FindPropertyRelative("modifierValue"),-.15f);Set(p.FindPropertyRelative("durationTurns"),4);});Set(so.FindProperty("targetModes").GetArrayElementAtIndex(0).FindPropertyRelative("absoluteRange").FindPropertyRelative("shape"),RangeShape.Line);},TargetType.AreaAroundCaster);
        ConfigureTrees(skills);
        var character=new SerializedObject(Load<CharacterDefinition>("Characters/CD_Mage.asset"));var starters=character.FindProperty(F("StartingSkills"));starters.ClearArray();foreach(var id in new[]{"energy_orb","fireball","focus","shield","tiring"}){starters.InsertArrayElementAtIndex(starters.arraySize);starters.GetArrayElementAtIndex(starters.arraySize-1).objectReferenceValue=skills[id];}Field(character,"BasicAttackSkill",skills["energy_orb"]);character.ApplyModifiedPropertiesWithoutUndo();
        ApplyManaEconomyBalance();
        var cls=new SerializedObject(Load<CharacterClassData>("Classes/CCD_Mage.asset"));Field(cls,"BasicAttackSkill",skills["energy_orb"]);var trees=cls.FindProperty(F("SkillTrees"));trees.arraySize=2;trees.GetArrayElementAtIndex(0).objectReferenceValue=Load<SkillTreeDefinition>("Skills/SkillTrees/ST_Mage_Warlock.asset");trees.GetArrayElementAtIndex(1).objectReferenceValue=Load<SkillTreeDefinition>("Skills/SkillTrees/ST_Mage_Fire.asset");cls.ApplyModifiedPropertiesWithoutUndo();
        var catalog=new SerializedObject(Load<GameContentCatalog>("World/GC_MainContentCatalog.asset"));var list=catalog.FindProperty(F("SkillDefinitions"));foreach(var skill in skills.Values){var found=false;for(var i=0;i<list.arraySize;i++)found|=list.GetArrayElementAtIndex(i).objectReferenceValue==skill;if(!found){list.InsertArrayElementAtIndex(list.arraySize);list.GetArrayElementAtIndex(list.arraySize-1).objectReferenceValue=skill;}}catalog.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssets();
    }
    static readonly string[][] Nodes = {
        new[]{"warlock","dispersion","Kondensacja Energii","Zwiększa obrażenia Kuli Energii o 10%/17.5%/25%","","","1","1","3"},
        new[]{"warlock","mastery","Ekonomia Zaklęć","Zmniejsza koszt many wszystkich umiejętności o 5","","","1","2","1"},
        new[]{"warlock","defensive_power","Witalna Moc","Zwiększa zdrowie o 0.1/0.3/0.5 za każdy punkt ataku","","","1","3","3"},
        new[]{"warlock","superhuman_offering","Spotęgowana Ofiara","Zwiększa efekt pozytywny oraz negatywny Ofiary do 35%/40%/50%","offering","","1","4","3"},
        new[]{"warlock","offering","Ofiara","Zwiększa obrażenia zadawane i przyjmowane o 30%","","offering","1","5","1"},
        new[]{"warlock","magic_saving","Desperacka Oszczędność","Zmniejsza koszty many o 20% poniżej 50% zdrowia","","","2","1","1"},
        new[]{"warlock","energy_saving","Odzyskiwanie Many","Zwiększa regenerację many o 0,25%/0,5% za każdy brakujący 1% many","mastery","","2","2","2"},
        new[]{"warlock","magic_focus","Przemiana Witalności Magii","Przez następne 3 tury zadajesz zawsze największe obrażenia;","","magic_focus","2","3","1"},
        new[]{"warlock","calm","Kontrolowana Ofiara","Zmniejsza negatywny efekt Ofiary o 10%/20%","superhuman_offering","","2","4","2"},
        new[]{"warlock","sacrifice","Przejęcie Mocy","Zmniejsza obrażenia wybranego sojusznika o 20%; zwiększając swoje obrażenia o 20% na 3 tury;","","sacrifice","2","5","1"},
        new[]{"warlock","energy_sense","Witalność z Zagrożenia","Każdy przeciwnik odległy o maksymalnie 5 pól daje regenerację zdrowia 0.5%/1% na turę, maksymalnie do 1.5%/3%","","","3","1","2"},
        new[]{"warlock","beyond_limits","Nadmiar Many","Pozwala na regenerację many 15%/30% poza maksymalną ilość","energy_saving","","3","2","2"},
        new[]{"warlock","master_concentration","Mistrzowska Koncentracja","Zwiększa czas trwania Skupienia Magii do 5 tur","magic_focus","","3","3","1"},
        new[]{"warlock","memory","Trening Pamięciowy","Zwiększa ilość maksymalnych umiejętności o 1/2","","","3","4","2"},
        new[]{"warlock","mass_sacrifice","Zbiorowe Przejęcie Mocy","Możesz użyć Przejęcia Mocy na 2/3 sojusznikach","sacrifice","","3","5","2"},
        new[]{"warlock","adaptation","Adaptacja Energetyczna","Otrzymanie obrażeń za co najmniej 20% maksymalnego zdrowia zmniejsza obrażenia 3 następnych ataków od tego Celu o 10%/20% obrażeń zadanych podczas pierwotnego ciosu","","","4","1","2"},
        new[]{"warlock","energy_discharge","Wyładowanie Energii","Jeśli masz powyżej maksymalnej ilości many, obrażenia następnego ataku mają szansę na bycie ciosem krytycznym. Szansa wynosi 0.2%/0.6%/1% za każdy nadmierny punkt many i wszystkie są zużywane","beyond_limits","","4","2","3"},
        new[]{"warlock","energy_split","Roztrojenie Energii","Zadaje 70% losowych obrażeń magicznych","","energy_split","4","3","1"},
        new[]{"warlock","power_saving","Skupione Promienie","Zwiększa współczynnik ataku Roztrojenia Energii o 15/30 pp.","energy_split","","4","4","2"},
        new[]{"warlock","ritual","Rytuał Odnowy","Zadaje wszystkim członkom drużyny 10% ich maksymalnego zdrowia w zamian za regenerację 30% w ciągu 5 tur;","","ritual","4","5","1"},
        new[]{"fire","burning_aura","Paląca Aura","Zwiększa obrażenia Ognistej Aury o 15%/30%","fire_aura","","1","1","2"},
        new[]{"fire","fire_aura","Ognista Aura","Tworzy aurę ognia o zasięgu 1 wokół celu z [50%] podpalenie 90%","","fire_aura","1","2","1"},
        new[]{"fire","pyromania","Podstawy Piromancji","Zwiększa szansę na podpalenie od Ognistej Aury i Kuli Ognia o 10%/25%","fire_aura","","1","3","2"},
        new[]{"fire","distracting_fire","Rozpraszający Ogień","Podpaleni przeciwnicy zadają o 10%/20% mniej obrażeń","","","1","4","2"},
        new[]{"fire","cauterize","Dotkliwe Oparzenia","Podpalanie przeciwników zwiększa zadawane im obrażenia o 10%/20%","","","1","5","2"},
        new[]{"fire","adrenaline","Ognista Adrenalina","Zwiększa szybkość podczas bycia podpalonym o 15","protection","","2","1","1"},
        new[]{"fire","protection","Ognista Ochrona","Zwiększa obronę fizyczną oraz magiczną podczas bycia podpalonym o 20%/50%","","","2","2","2"},
        new[]{"fire","aggressive_fire","Rozprzestrzenianie Płomieni","Podpalenia mają 50% na zduplikowanie się na przeciwnikach w zasięgu 1 od podpalonego z 50% siły pierwotnego podpalenia, maksymalnie do trzech pokoleń kopii","","","2","3","1"},
        new[]{"fire","fire_feeding","Żywienie się Ogniem","Każde gasnące podpalenie na polu bitwy przywraca 10%/20% zadanych obrażeń jako zdrowie","","","2","4","2"},
        new[]{"fire","arcana","Arkana Ognia","Zwiększa obrażenia wszystkich podpaleń o 10%/20%/30%","","","2","5","3"},
        new[]{"fire","fire_resistance","Odporność na Podpalenie","Zmniejsza obrażenia otrzymywane od podpaleń o 25% i samopodpaleń o 50%","fire_offering","","3","1","1"},
        new[]{"fire","fire_offering","Ognista Ofiara","Podpalanie przeciwników daje 15%/30%/50% szansy na samopodpalenie (niepomniejszona przez odporność na ogień)","protection","","3","2","3"},
        new[]{"fire","expansion","Zasięg Pożaru","Zwiększa zasięg Agresywnego Ognia do 3","aggressive_fire","","3","3","1"},
        new[]{"fire","sanguine","Podsycanie Żaru","Jeśli podpalenie zadaje mniej niż 10% maksymalnego zdrowia przeciwnika na turę, jego siła zwiększa się o 15%/30% (może po tym nałożyć się Obezwładniający Żar jeśli siła wzrośnie powyżej 10% maksymalnego zdrowia)","choleric","","3","4","2"},
        new[]{"fire","choleric","Obezwładniający Żar","Jeśli podpalenie zadaje więcej niż 10% maksymalnego zdrowia przeciwnika na turę, ma 10%/25% szansy na ogłuszenie na 2 tury w każdej turze","arcana","","3","5","2"},
        new[]{"fire","fire_arrow","Ognista Strzała","Zadaje losowe obrażenia magiczne z penetracją magiczną 30%, [70%] podpalenie 30% na 2 tury, zmniejsza obrażenia o 15% na 4 tury","","fire_arrow","4","1","1"},
        new[]{"fire","piercing","Przeszywający Płomień","Zwiększa magiczną penetrację Ognistej Strzały do 50%/70%","fire_arrow","","4","2","2"},
        new[]{"fire","conduction","Nieuchronny Żar","Podpaleni przeciwnicy otrzymują 7%/15% dodatkowych obrażeń nieuchronnych","","","4","3","2"},
        new[]{"fire","weakness","Odsłaniający Żar","Podpaleni przeciwnicy mają 15%/40% na otrzymanie obrażeń krytycznych","conduction","","4","4","2"},
        new[]{"fire","erosion","Wypalanie Odporności","Atakowanie przeciwników zmniejsza ich odporność na ogień o 5%/10% do końca bitwy, nakłada się do 5 razy","","","4","5","2"},
    };
    static void ConfigureTrees(Dictionary<string,SkillData> skills)
    {
        foreach(var treeName in new[]{"warlock","fire"})
        {
            var path=Root+"Skills/SkillTrees/ST_Mage_"+(treeName=="warlock"?"Warlock":"Fire")+".asset";
            var tree=AssetDatabase.LoadAssetAtPath<SkillTreeDefinition>(path);if(tree==null){tree=ScriptableObject.CreateInstance<SkillTreeDefinition>();AssetDatabase.CreateAsset(tree,path);}
            var so=new SerializedObject(tree);Field(so,"Id","tree.mage."+treeName);Field(so,"DisplayName",treeName=="warlock"?"Czarnoksiężnik":"Mag Ognia");
            var global=so.FindProperty(F("GlobalPassive"));Set(global.FindPropertyRelative(F("DisplayName")),"Pasyw drzewka");Set(global.FindPropertyRelative(F("Description")),treeName=="warlock"?"Za punkt: -0,5% kosztu many, +1% obrażeń przeciw pojedynczemu celowi.":"Za punkt: +2% obrażeń ognia, +1% odporności na ogień.");global.FindPropertyRelative(F("PassivesPerPoint")).ClearArray();
            var list=so.FindProperty(F("Nodes"));list.ClearArray();
            foreach(var node in Nodes)
            {
                if(node[0]!=treeName)continue;list.InsertArrayElementAtIndex(list.arraySize);var p=list.GetArrayElementAtIndex(list.arraySize-1);
                Set(p.FindPropertyRelative(F("Id")),"node.mage."+treeName+"."+node[1]);Set(p.FindPropertyRelative(F("DisplayName")),node[2]);Set(p.FindPropertyRelative(F("Description")),node[3]);
                Set(p.FindPropertyRelative(F("Tier")),int.Parse(node[6]));Set(p.FindPropertyRelative(F("Column")),int.Parse(node[7]));Set(p.FindPropertyRelative(F("MaxRanks")),int.Parse(node[8]));
                var grantedSkill=node[5].Length>0?skills[node[5]]:null;
                p.FindPropertyRelative(F("GrantedSkill")).objectReferenceValue=grantedSkill;
                var nodeIcon=grantedSkill!=null&&grantedSkill.Icon!=null?grantedSkill.Icon:VS22RemainingPassiveSetup.TryLoadPassiveIcon("node.mage."+treeName+"."+node[1]);
                p.FindPropertyRelative(F("Icon")).objectReferenceValue=nodeIcon!=null?nodeIcon:skills["energy_orb"].Icon;
                p.FindPropertyRelative(F("PassiveEffects")).ClearArray();
                var req=p.FindPropertyRelative(F("Requirements"));req.ClearArray();foreach(var id in node[4].Split(',')){if(id.Length==0)continue;req.InsertArrayElementAtIndex(req.arraySize);var q=req.GetArrayElementAtIndex(req.arraySize-1);Set(q.FindPropertyRelative(F("NodeId")),"node.mage."+treeName+"."+id);Set(q.FindPropertyRelative(F("MinimumRanks")),1);}
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

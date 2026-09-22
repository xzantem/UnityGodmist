using System;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Items;
using Godmist.Data.Quests;
using Godmist.Data.World;
using Godmist.Views.Exploration;
using Godmist.Views.Game;
using Godmist.Views.NPC;
using Godmist.Views.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Explicit authoring command, never runs when entering Play Mode.
public static class VS05CampaignSetup
{
    const string Root = "Assets/_Project/ScriptableObjects/";
    const string Output = Root + "StartingCampaign/";
    static T Load<T>(string path) where T : UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(Root + path)
        ?? throw new InvalidOperationException("Missing asset: " + path);
    static void Set(UnityEngine.Object target, string name, object value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty("<" + name + ">k__BackingField") ?? so.FindProperty(name);
        if (p == null) throw new InvalidOperationException(name);
        if (value is string text) p.stringValue = text;
        else if (value is bool flag) p.boolValue = flag;
        else if (value is float number) p.floatValue = number;
        else if (value is int integer) p.intValue = integer;
        else p.objectReferenceValue = (UnityEngine.Object)value;
        so.ApplyModifiedPropertiesWithoutUndo();
        if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        EditorUtility.SetDirty(target);
    }
    static T Asset<T>(string name) where T : ScriptableObject
    {
        var path = Output + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) return existing;
        var created = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(created, path); return created;
    }
    [MenuItem("Godmist/Content/Configure VS-05 campaign")]
    public static void Configure()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before authoring.");
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!AssetDatabase.IsValidFolder(Output.TrimEnd('/')))
            AssetDatabase.CreateFolder(Root.TrimEnd('/'), "StartingCampaign");
        var catalog = Load<GameContentCatalog>("World/GC_MainContentCatalog.asset");
        var profile = Asset<NewCampaignDefinition>("NC_Arungard");
        profile.StartingGold = 500; profile.Characters.Clear();
        // Legacy IntendedClass: 0 Warrior, 1 Scout, 2 Mage, 3 Paladin.
        var classes = new[] { "Scout", "Warrior", "Mage", "Paladin" };
        var characters = new[] { "Jerzy", "Wladimir_Test", "Mage", "Paladin" };
        var displayNames = new[] { "Jerzy", "Wladimir", "Alison", "Reimond" };
        var weapons = new[] { "ScoutSwordAndDagger", "WarriorLongsword", "MageWand", "PaladinHammer" };
        var armors = new[] { "ScoutTunic", "WarriorHauberk", "MageRobe", "PaladinPlateArmor" };
        var aliases = new[] { new[] { "Rusty", "Holey" }, new[] { "Broken", "Scratched" }, new[] { "Splintery", "Torn" }, new[] { "Misshapen", "Pierced" } };
        var names = new[] { new[] { "Zardzewiała broń", "Dziurawy pancerz" }, new[] { "Złamana broń", "Porysowany pancerz" }, new[] { "Drzazgowata różdżka", "Podarta szata" }, new[] { "Zniekształcony młot", "Przebity pancerz" } };
        float[,] stats = { {3,15,1,.07f,80,4,0,50,10}, {7,14,.5f,.03f,78,6,2,75,8}, {5,10,.6f,0,0,2,6,30,4}, {7,10,.5f,.02f,75,8,8,100,6} };
        for (var c = 0; c < 4; c++)
        {
            var cls = Load<CharacterClassData>("Classes/CCD_" + classes[c] + ".asset");
            var character = Load<CharacterDefinition>("Characters/CD_" + characters[c] + ".asset");
            Set(character,"DisplayName",displayNames[c]); Set(character,"ClassData",cls);
            var entry = new StartingCharacterDefinition { Character = character, Class = cls };
            for (var slot = 0; slot < 2; slot++)
            {
                var template = Load<EquipmentItemDefinition>("Equipment/EID_" + (slot == 0 ? weapons[c] : armors[c]) + ".asset");
                var item = Asset<EquipmentItemDefinition>("EID_Start_" + classes[c] + (slot == 0 ? "Weapon" : "Armor"));
                EditorUtility.CopySerialized(template, item);
                Set(item,"Id","equipment.start." + classes[c].ToLowerInvariant() + (slot == 0 ? ".weapon" : ".armor"));
                Set(item,"DisplayName",names[c][slot]); Set(item,"RequiredClass",cls);
                Set(item,"BaseRarity",(int)RarityTier.Common); Set(item,"BaseValue",0); Set(item,"IsStarterEquipment",true);
                if (slot == 0) entry.Weapon = item; else entry.Armor = item;
                if (!catalog.ItemDefinitions.Contains(item)) catalog.ItemDefinitions.Add(item);
                for (var k = 0; k < 3; k++)
                {
                    var kind = (EquipmentPartKind)(slot * 3 + k);
                    var part = Asset<EquipmentPartDefinition>("EPD_" + classes[c] + "_" + aliases[c][slot] + kind);
                    Set(part,"Id","part.start." + classes[c].ToLowerInvariant() + "." + kind.ToString().ToLowerInvariant());
                    Set(part,"DisplayName",names[c][slot]); Set(part,"RequiredClass",cls);
                    Set(part,"Kind",(int)kind); Set(part,"LevelBracket",1); Set(part,"IsStarterOnly",true);
                    part.CraftingMaterials.Clear();
                    // None is an absent material reference, not a craftable free recipe.
                    var so = new SerializedObject(part); var st = so.FindProperty("<Stats>k__BackingField");
                    foreach (var field in typeof(EquipmentPartStats).GetFields()) st.FindPropertyRelative(field.Name).floatValue = 0;
                    if (kind == EquipmentPartKind.WeaponHead) { st.FindPropertyRelative("MinAttackBase").floatValue=stats[c,0]; st.FindPropertyRelative("MaxAttackBase").floatValue=stats[c,1]; st.FindPropertyRelative("CritDamageBase").floatValue=stats[c,2]; }
                    if (kind == EquipmentPartKind.WeaponBinder) st.FindPropertyRelative("CritChanceBase").floatValue=stats[c,3];
                    if (kind == EquipmentPartKind.WeaponHandle) st.FindPropertyRelative("WeaponAccuracyBase").floatValue=stats[c,4];
                    if (kind == EquipmentPartKind.ArmorPlate) { st.FindPropertyRelative("PhysicalDefenseBase").floatValue=stats[c,5]; st.FindPropertyRelative("MagicalDefenseBase").floatValue=stats[c,6]; }
                    if (kind == EquipmentPartKind.ArmorBinder) st.FindPropertyRelative("MaxHealthBase").floatValue=stats[c,7];
                    if (kind == EquipmentPartKind.ArmorBase) st.FindPropertyRelative("DodgeBase").floatValue=stats[c,8];
                    so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(part);
                    (slot == 0 ? entry.WeaponParts : entry.ArmorParts).Add(part);
                    if (!catalog.EquipmentPartDefinitions.Contains(part)) catalog.EquipmentPartDefinitions.Add(part);
                }
            }
            profile.Characters.Add(entry);
            if (!catalog.CharacterDefinitions.Contains(character)) catalog.CharacterDefinitions.Add(character);
            if (!catalog.CharacterClasses.Contains(cls)) catalog.CharacterClasses.Add(cls);
        }
        EditorUtility.SetDirty(profile); EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
        var bootstrapScene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Bootstrap/Bootstrap.unity");
        profile = AssetDatabase.LoadAssetAtPath<NewCampaignDefinition>(Output + "NC_Arungard.asset");
        if (profile == null) throw new InvalidOperationException("Cannot reload saved new-campaign profile.");
        var bootstrap = bootstrapScene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<GameBootstrap>(true)).Single();
        Set(bootstrap,"newCampaign",profile); EditorSceneManager.MarkSceneDirty(bootstrapScene); EditorSceneManager.SaveScene(bootstrapScene);
        if (new SerializedObject(bootstrap).FindProperty("newCampaign").objectReferenceValue != profile)
            throw new InvalidOperationException("New-campaign scene reference was not assigned.");
        var town = EditorSceneManager.OpenScene("Assets/_Project/Scenes/City/Arungard.unity");
        profile = AssetDatabase.LoadAssetAtPath<NewCampaignDefinition>(Output + "NC_Arungard.asset");
        var owner = town.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ExplorationSandboxBootstrap>(true)).Single();
        Set(owner,"createMissingCharacters",false);
        var bindings = new SerializedObject(owner); var array = bindings.FindProperty("partyBindings");
        var templateView = (ExplorationCharacterView)array.GetArrayElementAtIndex(0).FindPropertyRelative("CharacterView").objectReferenceValue;
        var existingCount = array.arraySize;
        for (var i=0;i<4;i++)
        {
            if (i>=array.arraySize) array.InsertArrayElementAtIndex(array.arraySize);
            var row = array.GetArrayElementAtIndex(i);
            var view = i<existingCount ? (ExplorationCharacterView)row.FindPropertyRelative("CharacterView").objectReferenceValue :
                UnityEngine.Object.Instantiate(templateView,templateView.transform.parent);
            if (i>=existingCount) { view.name="StartingCharacter_"+classes[i]; view.transform.position=templateView.transform.position+new Vector3(i*.5f,0,0); }
            row.FindPropertyRelative("Definition").objectReferenceValue=profile.Characters[i].Character;
            row.FindPropertyRelative("ClassData").objectReferenceValue=profile.Characters[i].Class;
            row.FindPropertyRelative("CharacterView").objectReferenceValue=view;
        }
        array.arraySize=4; bindings.ApplyModifiedPropertiesWithoutUndo();
        if(PrefabUtility.IsPartOfPrefabInstance(owner)) PrefabUtility.RecordPrefabInstancePropertyModifications(owner);
        foreach (var npc in town.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<NpcView>(true)))
        {
            var so=new SerializedObject(npc);var marker=so.FindProperty("questMarker");
            if (marker.objectReferenceValue!=null) continue;
            var label=(TextMeshPro)so.FindProperty("nameLabel").objectReferenceValue;
            if(label==null) continue;
            var copy=UnityEngine.Object.Instantiate(label,npc.transform);copy.name="QuestOfferMarker";
            copy.text="!";copy.color=new Color(1f,.8f,.15f);copy.fontSize=5;copy.alignment=TextAlignmentOptions.Center;
            copy.transform.localPosition=new Vector3(0,1.2f,0);copy.gameObject.SetActive(false);
            marker.objectReferenceValue=copy.gameObject;so.ApplyModifiedPropertiesWithoutUndo();
            if(PrefabUtility.IsPartOfPrefabInstance(npc)) PrefabUtility.RecordPrefabInstancePropertyModifications(npc);
        }
        var canvas=town.GetRootGameObjects().Single(g=>g.name=="ExplorationUI").GetComponent<Canvas>();
        var hint=town.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CampaignIntroductionView>(true)).SingleOrDefault()?.transform;
        if(hint!=null) hint.SetParent(canvas.transform,false);
        if(hint==null)
        {
            var go=new GameObject("CampaignIntroduction",typeof(RectTransform),typeof(CampaignIntroductionView));go.transform.SetParent(canvas.transform,false);hint=go.transform;
            var rect=(RectTransform)hint;rect.anchorMin=rect.anchorMax=new Vector2(.5f,1);rect.pivot=new Vector2(.5f,1);rect.anchoredPosition=new Vector2(0,-16);rect.sizeDelta=new Vector2(680,64);
            var label=new GameObject("Message",typeof(RectTransform),typeof(TextMeshProUGUI));label.transform.SetParent(hint,false);
            var lr=(RectTransform)label.transform;lr.anchorMin=Vector2.zero;lr.anchorMax=Vector2.one;lr.offsetMin=lr.offsetMax=Vector2.zero;
            var text=label.GetComponent<TextMeshProUGUI>();text.font=TMP_Settings.defaultFontAsset;text.fontSize=20;text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;
            var view=go.GetComponent<CampaignIntroductionView>();Set(view,"panelRoot",label);Set(view,"messageText",text);Set(view,"firstQuest",Load<QuestDefinition>("Quests/QD_InvestigateCatacombs.asset"));
        }
        EditorSceneManager.MarkSceneDirty(town); EditorSceneManager.SaveScene(town);AssetDatabase.SaveAssets();
        Debug.Log("VS-05 campaign configured: 500 gold, 4 L1 loadouts, 24 starter-only parts, quest markers and introduction.");
    }
}

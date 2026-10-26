using System;
using System.IO;
using System.Linq;
using Godmist.Data.Characters;
using Godmist.Data.Skills;
using Godmist.Data.World;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    public static class WarriorDischargeSetup
    {
        const string Root = "Assets/_Project/ScriptableObjects/";
        public const string Path = Root + "Skills/SD_WarriorDischarge.asset";
        static string Field(string name) => "<" + name + ">k__BackingField";
        [MenuItem("Godmist/Content/Apply Warrior Discharge")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            var tree = AssetDatabase.LoadAssetAtPath<SkillTreeDefinition>(Root + "Skills/SkillTrees/ST_Warrior_Brawler.asset");
            var character = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(Root + "Characters/CD_Wladimir_Test.asset");
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(Root + "World/GC_MainContentCatalog.asset");
            foreach (var asset in new UnityEngine.Object[] { tree, character, catalog })
            {
                var source = AssetDatabase.GetAssetPath(asset);
                var backup = System.IO.Path.GetFullPath("../LocalHandoffs/warrior-discharge-20261024/before-assets/" + source);
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(backup));
                if (!File.Exists(backup)) File.Copy(source, backup);
            }
            var skill = AssetDatabase.LoadAssetAtPath<SkillData>(Path);
            if (skill == null)
            {
                skill = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<SkillData>(Root + "Skills/SD_WarriorCut.asset"));
                skill.name = "SD_WarriorDischarge"; AssetDatabase.CreateAsset(skill, Path);
            }
            var data = new SerializedObject(skill);
            data.FindProperty(Field("SkillId")).stringValue = "skill.warrior.discharge";
            data.FindProperty(Field("DisplayName")).stringValue = "Wyładowanie";
            data.FindProperty(Field("TooltipDescription")).stringValue = "Zużywa 20–100 furii. Zadaje 100% ataku +1% za każdy wydany punkt (maks. 200%).";
            data.FindProperty(Field("ResourceCost")).intValue = 20;
            data.FindProperty(Field("ActionCost")).intValue = 60;
            data.FindProperty(Field("SkillAccuracy")).floatValue = 82;
            // Reuse the existing Brawler rage artwork rather than a placeholder.
            data.FindProperty(Field("Icon")).objectReferenceValue = tree.Nodes.Single(n => n.Id == "node.warrior.brawler.release_rage").Icon;
            data.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(skill);
            data = new SerializedObject(character);
            var starters = data.FindProperty(Field("StartingSkills"));
            for (var i = 0; i < starters.arraySize; i++)
                if (starters.GetArrayElementAtIndex(i).objectReferenceValue is SkillData old && old.SkillId == "skill.warrior.bandage")
                    starters.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<SkillData>(Root + "Skills/SD_WarriorBleedingDestruction.asset");
            data.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(character);
            data = new SerializedObject(tree); var nodes = data.FindProperty(Field("Nodes"));
            for (var i = 0; i < nodes.arraySize; i++)
            {
                var node = nodes.GetArrayElementAtIndex(i); var id = node.FindPropertyRelative(Field("Id")).stringValue;
                string name = null, description = null;
                if (id == "node.warrior.brawler.bleeding_destruction")
                {
                    name = "Wyładowanie"; description = skill.TooltipDescription;
                    node.FindPropertyRelative(Field("GrantedSkill")).objectReferenceValue = skill;
                    node.FindPropertyRelative(Field("Icon")).objectReferenceValue = skill.Icon;
                }
                if (id == "node.warrior.brawler.wounding")
                { name = "Żądza Zwycięstwa"; description = "Zabicie celu Wyładowaniem zwraca 25%/50% zużytej furii."; }
                if (id == "node.warrior.brawler.larger_wound")
                { name = "Przełamanie"; description = "Wyładowanie ignoruje 20%/40% obrony fizycznej; dwukrotnie więcej przy zużyciu co najmniej 50 furii."; }
                if (name == null) continue;
                node.FindPropertyRelative(Field("DisplayName")).stringValue = name;
                node.FindPropertyRelative(Field("Description")).stringValue = description;
            }
            data.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(tree);
            if (!catalog.SkillDefinitions.Contains(skill))
            {
                data = new SerializedObject(catalog); var skills = data.FindProperty(Field("SkillDefinitions"));
                skills.InsertArrayElementAtIndex(skills.arraySize); skills.GetArrayElementAtIndex(skills.arraySize - 1).objectReferenceValue = skill;
                data.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(catalog);
            }
            AssetDatabase.SaveAssets();
        }
    }
}

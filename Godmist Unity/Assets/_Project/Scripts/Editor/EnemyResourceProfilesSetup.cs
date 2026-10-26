using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Combat;
using Godmist.Data.Skills;
using Godmist.Data.World;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    // Explicit content update; never runs automatically on import.
    public static class EnemyResourceProfilesSetup
    {
        const string Root = "Assets/_Project/ScriptableObjects/";
        static readonly HashSet<UnityEngine.Object> touched = new();
        static void MarkDirty(UnityEngine.Object asset) { EditorUtility.SetDirty(asset); touched.Add(asset); }
        static string F(string name) => "<" + name + ">k__BackingField";
        static readonly HashSet<string> RageIds = new() {
            "enemy.catacombs.swordsman", "enemy.catacombs.defender", "enemy.catacombs.ghoul", "enemy.catacombs.undead",
            "enemy.catacombs.spectral_knight", "enemy.catacombs.skeleton_slayer", "enemy.catacombs.skeleton_slayer.enraged" };
        static readonly HashSet<string> MomentumIds = new() {
            "enemy.catacombs.archer", "enemy.forest.bandit_shooter", "enemy.forest.bandit_thug", "enemy.forest.elf_archer",
            "enemy.forest.elf_poacher", "enemy.forest.rabid_wolf", "enemy.forest.bandit_leader", "enemy.forest.bandit_leader.revealed" };
        static readonly HashSet<string> ManaIds = new() {
            "enemy.catacombs.occultist", "enemy.catacombs.wraith", "enemy.catacombs.dead_king", "enemy.catacombs.dead_king.enraged",
            "enemy.forest.elf_druid", "enemy.forest.infected_tree", "enemy.forest.infected_ent", "enemy.forest.infected_ent.burning" };

        public static bool TryGetType(string id, out ResourceType type)
        {
            type = RageIds.Contains(id ?? "") ? ResourceType.Rage : MomentumIds.Contains(id ?? "") ? ResourceType.Momentum
                : ManaIds.Contains(id ?? "") ? ResourceType.Mana : ResourceType.None;
            return type != ResourceType.None;
        }
        public static int RageCost(int oldCost) => Mathf.CeilToInt(Mathf.Max(0, oldCost) / 10f) * 5;

        static CharacterClassData Profile(bool forest, ResourceType type)
        {
            var directory = Root + (forest ? "Dungeons/Forest/" : "Catacombs/Enemies/");
            var stem = forest ? "CCD_ForestEnemy" : "CCD_CatacombsEnemy";
            var suffix = type == ResourceType.Rage ? "_Rage" : type == ResourceType.Momentum ? "_Momentum" : "";
            var path = directory + stem + suffix + ".asset";
            var profile = AssetDatabase.LoadAssetAtPath<CharacterClassData>(path);
            if (profile == null)
            {
                var template = AssetDatabase.LoadAssetAtPath<CharacterClassData>(directory + stem + ".asset")
                    ?? throw new InvalidOperationException("Missing enemy resource class template: " + directory);
                profile = UnityEngine.Object.Instantiate(template);
                profile.name = stem + suffix;
                AssetDatabase.CreateAsset(profile, path);
            }
            var so = new SerializedObject(profile);
            var id = "class.enemy." + (forest ? "forest" : "catacombs");
            if (type != ResourceType.Mana) id += type == ResourceType.Rage ? ".rage" : ".momentum";
            so.FindProperty(F("Id")).stringValue = id;
            so.FindProperty(F("ResourceType")).intValue = (int)type;
            so.FindProperty(F("StartingResourcePercent")).intValue = type == ResourceType.Mana ? 100 : 0;
            if (type != ResourceType.Mana) so.FindProperty(F("ClassName")).stringValue =
                (forest ? "Przeciwnik lasu" : "Przeciwnik katakumb") + (type == ResourceType.Rage ? " — furia" : " — impet");
            if (so.ApplyModifiedPropertiesWithoutUndo()) MarkDirty(profile);
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(Root + "World/GC_MainContentCatalog.asset");
            if (catalog != null && !catalog.CharacterClasses.Contains(profile))
            { catalog.CharacterClasses.Add(profile); MarkDirty(catalog); }
            return profile;
        }

        public static void ApplyToDefinition(CharacterDefinition definition)
        {
            if (definition == null || !TryGetType(definition.Id, out var type)) return;
            var profile = Profile(definition.Id.StartsWith("enemy.forest.", StringComparison.Ordinal), type);
            var so = new SerializedObject(definition);
            so.FindProperty(F("ClassData")).objectReferenceValue = profile;
            var stats = so.FindProperty(F("BaseStats"));
            stats.FindPropertyRelative(F("MaxResource")).intValue = type == ResourceType.Rage ? 50 : type == ResourceType.Momentum ? 200 : 100;
            stats.FindPropertyRelative(F("ResourceRegeneration")).intValue = type == ResourceType.Mana ? 15 : 0;
            if (so.ApplyModifiedPropertiesWithoutUndo()) MarkDirty(definition);
            foreach (var skill in definition.StartingSkills.Append(definition.BasicAttackSkill).Where(s => s != null).Distinct())
            {
                var data = new SerializedObject(skill);
                if (type == ResourceType.Rage && skill.ResourceType != ResourceType.Rage)
                    data.FindProperty(F("ResourceCost")).intValue = RageCost(skill.ResourceCost);
                data.FindProperty(F("ResourceType")).intValue = (int)type;
                if (data.ApplyModifiedPropertiesWithoutUndo()) MarkDirty(skill);
            }
        }

        public static void ApplyToEncounter(EncounterDefinition encounter)
        {
            if (encounter == null) return;
            var so = new SerializedObject(encounter);
            var entries = so.FindProperty(F("Enemies"));
            if (entries == null) return;
            for (var i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                var definition = entry.FindPropertyRelative(F("CharacterDefinition")).objectReferenceValue as CharacterDefinition;
                if (definition == null || !TryGetType(definition.Id, out _)) continue;
                entry.FindPropertyRelative(F("ClassData")).objectReferenceValue = definition.ClassData;
            }
            if (so.ApplyModifiedPropertiesWithoutUndo()) MarkDirty(encounter);
        }

        [MenuItem("Godmist/Content/Apply enemy resource playtest profiles")]
        public static void ApplyAll()
        {
            touched.Clear();
            var directories = new[] { Root + "Catacombs/Enemies", Root + "Dungeons/Forest" };
            foreach (var guid in AssetDatabase.FindAssets("t:CharacterDefinition", directories))
                ApplyToDefinition(AssetDatabase.LoadAssetAtPath<CharacterDefinition>(AssetDatabase.GUIDToAssetPath(guid)));
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(Root + "World/GC_MainContentCatalog.asset");
            foreach (var encounter in catalog.EncounterDefinitions) ApplyToEncounter(encounter);
            foreach (var asset in touched.ToArray()) if (asset != null) AssetDatabase.SaveAssetIfDirty(asset);
            touched.Clear();
        }
    }
}

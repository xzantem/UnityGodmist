using System;
using System.Collections.Generic;
using System.IO;
using Godmist.Data.Characters;
using Godmist.Systems.Characters;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    /// <summary>Static role calibration. Never depends on the current party or its equipment.</summary>
    public static class EnemyRoleStatsSetup
    {
        static readonly int[] Levels = { 1, 10, 20, 30, 40, 50 };
        static readonly float[] Physical = { 40, 65, 160, 320, 550, 1400 };
        static readonly float[] Magical = { 50, 80, 190, 400, 850, 1800 };
        readonly struct Profile
        {
            public readonly float Physical, Magical, DodgeStart, DodgeEnd, CritStart, CritEnd, CritDamage;
            public Profile(float physical, float magical, float dodgeStart, float dodgeEnd, float critStart, float critEnd, float critDamage)
            { Physical=physical;Magical=magical;DodgeStart=dodgeStart;DodgeEnd=dodgeEnd;CritStart=critStart;CritEnd=critEnd;CritDamage=critDamage; }
        }
        static readonly Dictionary<string,Profile> Profiles = new()
        {
            ["enemy.catacombs.swordsman"] = new(0.35f, 0.3f, 12f, 24f, 0.1f, 0.14f, 2f),
            ["enemy.catacombs.undead"] = new(0.35f, 0.35f, 2f, 6f, 0.05f, 0.07f, 1.5f),
            ["enemy.catacombs.wraith"] = new(0.35f, 1.4f, 12f, 24f, 0.08f, 0.11f, 1.8f),
            ["enemy.catacombs.occultist"] = new(0.1f, 1.9f, 6f, 12f, 0.05f, 0.08f, 1.85f),
            ["enemy.catacombs.archer"] = new(0.15f, 0.25f, 10f, 22f, 0.08f, 0.12f, 2.1f),
            ["enemy.catacombs.ghoul"] = new(0.2f, 0.2f, 24f, 44f, 0.1f, 0.14f, 1.8f),
            ["enemy.catacombs.defender"] = new(2f, 0.2f, 4f, 8f, 0.03f, 0.05f, 1.5f),
            ["enemy.forest.rabid_wolf"] = new(0.1f, 0.05f, 32f, 50f, 0.1f, 0.14f, 2.1f),
            ["enemy.forest.bandit_thug"] = new(0.25f, 0.2f, 20f, 34f, 0.09f, 0.13f, 1.85f),
            ["enemy.forest.bandit_shooter"] = new(0.125f, 0.1f, 12f, 24f, 0.08f, 0.12f, 2f),
            ["enemy.forest.elf_archer"] = new(0.1f, 0.2f, 32f, 55f, 0.1f, 0.14f, 1.9f),
            ["enemy.forest.elf_poacher"] = new(0.2f, 0.25f, 26f, 46f, 0.08f, 0.11f, 1.7f),
            ["enemy.forest.elf_druid"] = new(0.1f, 0.5f, 14f, 26f, 0.03f, 0.045f, 1.4f),
            ["enemy.forest.infected_tree"] = new(0.8f, 0.2f, 0f, 0f, 0.03f, 0.04f, 1.6f),
            ["enemy.catacombs.skeleton_slayer"] = new(0.45f, 0.25f, 12f, 22f, 0.13f, 0.17f, 2f),
            ["enemy.catacombs.dead_king"] = new(0.35f, 0.7f, 15f, 30f, 0.09f, 0.12f, 1.9f),
            ["enemy.forest.infected_ent"] = new(0.9f, 0.35f, 0f, 0f, 0.07f, 0.09f, 1.9f),
            ["enemy.forest.bandit_leader"] = new(0.25f, 0.2f, 30f, 50f, 0.12f, 0.16f, 2f),
        };
        static string BaseId(string id) => id switch
        {
            "enemy.catacombs.dead_king.enraged"=>"enemy.catacombs.dead_king",
            "enemy.catacombs.skeleton_slayer.enraged"=>"enemy.catacombs.skeleton_slayer",
            "enemy.forest.infected_ent.burning"=>"enemy.forest.infected_ent",
            "enemy.forest.bandit_leader.revealed"=>"enemy.forest.bandit_leader",
            _=>id
        };
        static void Float(SerializedProperty parent,string name,float value) => parent.FindPropertyRelative("<"+name+">k__BackingField").floatValue=value;
        public static bool ApplyToDefinition(CharacterDefinition definition)
        {
            if(definition==null||!Profiles.TryGetValue(BaseId(definition.Id)??string.Empty,out var profile))return false;
            var originalState=EditorJsonUtility.ToJson(definition);
            var data=new SerializedObject(definition);
            var basis=data.FindProperty("<BaseStats>k__BackingField");
            Float(basis,"PhysicalDefense",Mathf.Round(profile.Physical*Physical[0]));
            Float(basis,"MagicalDefense",EnemyRoleDurabilitySetup.MagicalDefense(definition.Id,1,Mathf.Round(profile.Magical*Magical[0])));
            Float(basis,"Dodge",profile.DodgeStart);Float(basis,"CritChance",profile.CritStart);Float(basis,"CritDamage",profile.CritDamage);
            var growth=data.FindProperty("<GrowthData>k__BackingField");
            var units=new CharacterLevelGrowthSystem().GetCumulativeGrowthMultiplier(50);
            Float(growth,"PhysicalDefense",0);Float(growth,"MagicalDefense",0);
            Float(growth,"Dodge",(profile.DodgeEnd-profile.DodgeStart)/units);
            Float(growth,"CritChance",(profile.CritEnd-profile.CritStart)/units);
            Float(growth,"CritDamage",0);
            // Late defender armor follows actual pre-defense Cut damage. Using the
            // displayed unarmored damage as its anchor also counted post-defense
            // Commander bonuses and overshot the approved 60–70% reduction.
            var defender=BaseId(definition.Id)=="enemy.catacombs.defender";
            var levels=defender?new[]{1,10,20,30,40,48,50}:Levels;
            var curve=growth.FindPropertyRelative("<DefenseCurve>k__BackingField");curve.arraySize=levels.Length;
            for(var i=0;i<levels.Length;i++)
            {
                var knot=curve.GetArrayElementAtIndex(i);
                var index=Array.IndexOf(Levels,levels[i]);
                var physical=levels[i]==48?1500:profile.Physical*Physical[index];
                var magical=levels[i]==48?profile.Magical*Mathf.Lerp(Magical[4],Magical[5],(181f-109f)/(199f-109f)):profile.Magical*Magical[index];
                if(defender&&levels[i]==50)physical=1900;
                knot.FindPropertyRelative("<Level>k__BackingField").intValue=levels[i];
                Float(knot,"PhysicalDefense",physical);Float(knot,"MagicalDefense",EnemyRoleDurabilitySetup.MagicalDefense(definition.Id,levels[i],magical));
            }
            var resistance=data.FindProperty("<BaseResistances>k__BackingField");
            switch(BaseId(definition.Id))
            {
                case "enemy.catacombs.wraith":case "enemy.catacombs.dead_king":
                    Float(resistance,"BleedResistance",1);Float(resistance,"PoisonResistance",1);break;
                case "enemy.catacombs.defender":
                    Float(resistance,"SuppressionResistance",.5f);Float(resistance,"MoveResistance",.5f);break;
                case "enemy.forest.infected_tree":case "enemy.forest.infected_ent":Float(resistance,"MoveResistance",.8f);break;
            }
            // Apply the approved durability correction in the same transaction so
            // re-running the older role/scaling authoring remains idempotent.
            EnemyRoleDurabilitySetup.Configure(definition.Id, data);
            EnemyArchetypeStatsSetup.Configure(definition.Id, data);
            if(!data.ApplyModifiedPropertiesWithoutUndo() || EditorJsonUtility.ToJson(definition)==originalState)return false;
            EditorUtility.SetDirty(definition);return true;
        }
        [MenuItem("Godmist/Content/Apply enemy role stat balance")]
        public static void ApplyAll()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before authoring enemy role stats.");
            var output=Path.GetFullPath("../LocalHandoffs/enemy-role-implementation-20261025");Directory.CreateDirectory(output);
            var changed=new List<string>();
            foreach(var guid in AssetDatabase.FindAssets("t:CharacterDefinition",new[]{"Assets/_Project/ScriptableObjects/Catacombs/Enemies","Assets/_Project/ScriptableObjects/Dungeons/Forest"}))
            {
                var definition=AssetDatabase.LoadAssetAtPath<CharacterDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if(!ApplyToDefinition(definition))continue;
                AssetDatabase.SaveAssetIfDirty(definition);changed.Add(definition.Id);
            }
            File.WriteAllLines(Path.Combine(output,"authored-ids.txt"),changed);
        }
    }
}

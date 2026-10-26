using System;
using System.Collections.Generic;
using Godmist.Data.Characters;
using Godmist.Systems.Characters;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    // Ordinary enemies reference equipped, same-level heroes, not a prescribed
    // damage-reduction percentage. Hybrid profiles mix roles explicitly.
    public static class EnemyArchetypeStatsSetup
    {
        public static readonly int[] Levels = { 1, 5, 10, 15, 20, 30, 40, 48, 50 };
        static readonly string[] Heroes = { "wladimir", "jerzy", "character.mage", "character.paladin" };
        static readonly Dictionary<string, float[]> References = new()
        {
            { "wladimir:physical", new float[] { 18f, 33.8f, 42.05f, 104.55f, 127.05f, 270.55f, 485.05f, 761.45f, 931.55f } },
            { "wladimir:magical", new float[] { 10f, 20.6f, 26.1f, 68.7f, 83.7f, 191.9f, 338.3f, 535.5f, 648.9f } },
            { "wladimir:dodge", new float[] { 18f, 25.2f, 27.45f, 32.95f, 37.45f, 43.95f, 52.45f, 60.05f, 66.95f } },
            { "wladimir:crit", new float[] { 0.03f, 0.0625f, 0.08115f, 0.10605f, 0.124f, 0.16685f, 0.2097f, 0.27325f, 0.31075f } },
            { "wladimir:critDamage", new float[] { 1.5f, 1.535f, 1.555f, 1.71f, 1.81f, 1.885f, 1.965f, 2.04f, 2.1f } },
            { "jerzy:physical", new float[] { 12f, 22.32f, 27.97f, 69.27f, 84.57f, 186.47f, 331.97f, 525.73f, 639.67f } },
            { "jerzy:magical", new float[] { 4f, 10.2f, 13.2f, 34.6f, 42.6f, 101.6f, 186f, 295.6f, 353.2f } },
            { "jerzy:dodge", new float[] { 27f, 37.32f, 39.72f, 45.52f, 50.32f, 60.72f, 71.72f, 85.48f, 92.92f } },
            { "jerzy:crit", new float[] { 0.07f, 0.0875f, 0.1025f, 0.125f, 0.14f, 0.1745f, 0.215f, 0.2525f, 0.2845f } },
            { "jerzy:critDamage", new float[] { 2f, 2.02f, 2.05f, 2.115f, 2.225f, 2.32f, 2.42f, 2.515f, 2.605f } },
            { "character.mage:physical", new float[] { 8f, 14f, 17.25f, 41.75f, 50.25f, 105.75f, 190.25f, 300.25f, 358.75f } },
            { "character.mage:magical", new float[] { 18f, 34.8f, 43.3f, 105.3f, 128.3f, 280.7f, 499.5f, 798.9001f, 969.9f } },
            { "character.mage:dodge", new float[] { 10f, 17.16f, 19.36f, 21.76f, 26.16f, 32.36f, 38.36f, 43.74f, 50.46f } },
            { "character.mage:crit", new float[] { 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f } },
            { "character.mage:critDamage", new float[] { 1.6f, 1.6f, 1.62f, 1.67f, 1.77f, 1.835f, 1.9f, 1.97f, 2.03f } },
            { "character.paladin:physical", new float[] { 24f, 44.4f, 55.4f, 130.4f, 160.4f, 342.4f, 606.4f, 953.6f, 1180.4f } },
            { "character.paladin:magical", new float[] { 24f, 45.2f, 56.2f, 134f, 164f, 351.6f, 623.6f, 981.2f, 1208f } },
            { "character.paladin:dodge", new float[] { 14f, 19.16f, 21.36f, 24.26f, 28.66f, 34.36f, 40.36f, 48.24f, 54.96f } },
            { "character.paladin:crit", new float[] { 0.02f, 0.04575f, 0.05575f, 0.06425f, 0.07425f, 0.0925f, 0.11075f, 0.12925f, 0.15925f } },
            { "character.paladin:critDamage", new float[] { 1.5f, 1.535f, 1.555f, 1.59f, 1.69f, 1.75f, 1.805f, 1.865f, 1.925f } },
        };
        readonly struct Profile
        {
            public readonly float[] Weights;
            public readonly float Physical, Magical, Dodge, Crit, CritDamage;
            public Profile(float w, float j, float m, float p, float physical, float magical, float dodge, float crit, float critDamage)
            { Weights = new[] { w,j,m,p }; Physical=physical; Magical=magical; Dodge=dodge; Crit=crit; CritDamage=critDamage; }
        }
        static readonly Dictionary<string,Profile> Profiles = new()
        {
            { "enemy.catacombs.swordsman", new Profile(0.85f, 0f, 0f, 0.15f, 1f, 0.8f, 0.65f, 1f, 1f) },
            { "enemy.catacombs.undead", new Profile(0.8f, 0f, 0f, 0.2f, 0.65f, 0.6f, 0.15f, 0.7f, 0.95f) },
            { "enemy.catacombs.wraith", new Profile(0f, 0.25f, 0.75f, 0f, 0.4f, 1.2f, 0.85f, 0.8f, 0.95f) },
            { "enemy.catacombs.occultist", new Profile(0f, 0f, 0.8f, 0.2f, 0.45f, 1.1f, 0.6f, 0.8f, 0.95f) },
            { "enemy.catacombs.archer", new Profile(0.2f, 0.8f, 0f, 0f, 0.6f, 0.6f, 0.6f, 1f, 1f) },
            { "enemy.catacombs.ghoul", new Profile(0.5f, 0.5f, 0f, 0f, 0.55f, 0.5f, 1.1f, 1.25f, 1.1f) },
            { "enemy.catacombs.defender", new Profile(0f, 0f, 0f, 1f, 1.65f, 0.5f, 0.5f, 0.8f, 1f) },
            { "enemy.forest.rabid_wolf", new Profile(0.2f, 0.8f, 0f, 0f, 0.25f, 0.25f, 1.15f, 1.2f, 1.05f) },
            { "enemy.forest.bandit_thug", new Profile(0.55f, 0.45f, 0f, 0f, 0.85f, 0.65f, 0.95f, 1.1f, 1.05f) },
            { "enemy.forest.bandit_shooter", new Profile(0.2f, 0.8f, 0f, 0f, 0.5f, 0.45f, 0.65f, 1f, 1f) },
            { "enemy.forest.elf_archer", new Profile(0f, 0.9f, 0.1f, 0f, 0.5f, 0.65f, 1.1f, 1.1f, 1f) },
            { "enemy.forest.elf_poacher", new Profile(0.3f, 0.7f, 0f, 0f, 0.8f, 0.7f, 0.95f, 1f, 0.95f) },
            { "enemy.forest.elf_druid", new Profile(0f, 0f, 0.8f, 0.2f, 0.55f, 1f, 0.75f, 0.6f, 0.9f) },
            { "enemy.forest.infected_tree", new Profile(0.2f, 0f, 0f, 0.8f, 1f, 0.5f, 0f, 0.5f, 1f) },
        };
        static float Reference(Profile p, string field, int anchor)
        {
            var value=0f;
            for(var i=0;i<Heroes.Length;i++) value += p.Weights[i]*References[Heroes[i]+":"+field][anchor];
            return value;
        }
        public static float Value(string id, string field, int anchor)
        {
            var p=Profiles[id];
            // Ordinary enemies do not also receive a hero's full equipment,
            // jewelry and skill-tree budget. Keep the L1 role, then use a
            // smaller share of that progression. Defender armor is its role's
            // exception; its other secondary stats use the ordinary budget.
            var progress=Mathf.Clamp01((Levels[anchor]-1)/19f);
            var budget=field switch {
                "physical" when id!="enemy.catacombs.defender" => Mathf.Lerp(1f,.65f,progress),
                "dodge" => Mathf.Lerp(1f,.55f,progress),
                "crit" => Mathf.Lerp(1f,.45f,progress),
                "critDamage" => Mathf.Lerp(1f,.9f,progress),
                _ => 1f
            };
            return field switch
            {
                "physical" => Mathf.Round(Reference(p,field,anchor)*budget*(id=="enemy.catacombs.defender" ? Mathf.Lerp(p.Physical,1f,(Levels[anchor]-1)/49f) : p.Physical)),
                "magical" => Mathf.Round(Reference(p,field,anchor)*p.Magical),
                "dodge" => Reference(p,field,anchor)*p.Dodge*budget,
                "crit" => Mathf.Clamp(Reference(p,field,anchor)*p.Crit*budget,0,.4f),
                "critDamage" => Mathf.Max(1.25f,Reference(p,field,anchor)*p.CritDamage*budget),
                _ => throw new ArgumentOutOfRangeException(nameof(field))
            };
        }
        public static bool Configure(string id, SerializedObject data)
        {
            if(id==null || !Profiles.ContainsKey(id)) return false;
            var basis=data.FindProperty("<BaseStats>k__BackingField");
            var growth=data.FindProperty("<GrowthData>k__BackingField");
            void Set(SerializedProperty owner,string field,float value) => owner.FindPropertyRelative("<"+field+">k__BackingField").floatValue=value;
            foreach(var pair in new[]{("PhysicalDefense","physical"),("MagicalDefense","magical"),("Dodge","dodge"),("CritChance","crit"),("CritDamage","critDamage")})
            {
                Set(basis,pair.Item1,Value(id,pair.Item2,0));
                var increase=pair.Item2 is "dodge" or "crit" or "critDamage" ? (Value(id,pair.Item2,Levels.Length-1)-Value(id,pair.Item2,0))
                    /new CharacterLevelGrowthSystem().GetCumulativeGrowthMultiplier(50) : 0f;
                Set(growth,pair.Item1,increase);
            }
            var curve=growth.FindPropertyRelative("<DefenseCurve>k__BackingField");curve.arraySize=Levels.Length;
            for(var i=0;i<Levels.Length;i++)
            {
                var point=curve.GetArrayElementAtIndex(i);
                point.FindPropertyRelative("<Level>k__BackingField").intValue=Levels[i];
                Set(point,"PhysicalDefense",Value(id,"physical",i)); Set(point,"MagicalDefense",Value(id,"magical",i));
            }
            return true;
        }
        public static bool ApplyToDefinition(CharacterDefinition definition)
        {
            if(definition==null) return false;
            var data=new SerializedObject(definition);
            if(!Configure(definition.Id,data) || !data.ApplyModifiedPropertiesWithoutUndo()) return false;
            EditorUtility.SetDirty(definition);return true;
        }
    }
}

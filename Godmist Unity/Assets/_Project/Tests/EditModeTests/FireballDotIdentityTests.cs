using System;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Skills;
using Godmist.Editor;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class FireballDotIdentityTests
    {
        const string PathToSkill="Assets/_Project/ScriptableObjects/Skills/SD_MageFireball.asset";
        static SkillData Skill=>AssetDatabase.LoadAssetAtPath<SkillData>(PathToSkill);
        static string Folder=>Path.GetFullPath("../LocalHandoffs/fireball-dot-implementation-20261103");
        [Test,Explicit("User-approved native Fireball authoring only, with idempotence and other-skill preservation checks.")]
        public void AuthorApprovedFireball()
        {
            Directory.CreateDirectory(Folder);
            var skills=AssetDatabase.FindAssets("t:SkillData",new[]{"Assets/_Project/ScriptableObjects"})
                .Select(g=>AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(g))).ToDictionary(s=>s,EditorJsonUtility.ToJson);
            var before=System.IO.Path.Combine(Folder,"before.json");
            Assert.That(File.Exists(before),Is.False,"Keep the original authoring evidence immutable.");
            File.WriteAllText(before,skills[Skill]);
            DotBalanceSetup.ApplyFireballIdentity();
            var after=EditorJsonUtility.ToJson(Skill);DotBalanceSetup.ApplyFireballIdentity();
            Assert.That(EditorJsonUtility.ToJson(Skill),Is.EqualTo(after));
            foreach(var entry in skills.Where(s=>s.Key!=Skill))Assert.That(EditorJsonUtility.ToJson(entry.Key),Is.EqualTo(entry.Value),entry.Key.SkillId);
            File.WriteAllText(System.IO.Path.Combine(Folder,"after.json"),after);
        }
        [Test]
        public void ProductionFireballHasApprovedIdentityWithoutChangingCostsOrTargeting()
        {
            Assert.That(Skill.Effects.Count,Is.EqualTo(2));Assert.That(Skill.Effects[0].DamageMultiplier,Is.EqualTo(.5f));
            var burn=Skill.Effects[1];Assert.That(burn.EffectType,Is.EqualTo(EffectType.ApplyStatus));Assert.That(burn.StatusId,Is.EqualTo("burn"));
            Assert.That(burn.BaseChance,Is.EqualTo(.85f));Assert.That(burn.StatusDamageMultiplier,Is.EqualTo(.45f));Assert.That(burn.DurationTurns,Is.EqualTo(2));
            Assert.That(Skill.ResourceCost,Is.EqualTo(40));Assert.That(Skill.ActionCost,Is.EqualTo(75));
            Assert.That(Skill.TargetModes[0].TargetType,Is.EqualTo(TargetType.Single));Assert.That(Skill.TargetModes[0].AbsoluteRange.MaxRange,Is.EqualTo(6));
        }
        [Test]
        public void ReauthoringAnIsolatedDefinitionIsIdempotent()
        {
            var clone=UnityEngine.Object.Instantiate(Skill);
            try
            {
                var data=new SerializedObject(clone);data.FindProperty("effects").GetArrayElementAtIndex(0).FindPropertyRelative("damageMultiplier").floatValue=1;
                var burn=data.FindProperty("effects").GetArrayElementAtIndex(1);
                burn.FindPropertyRelative("baseChance").floatValue=.75f;burn.FindPropertyRelative("durationTurns").intValue=3;
                burn.FindPropertyRelative("statusDamageMultiplier").floatValue=.25f;
                data.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(DotBalanceSetup.ApplyFireballIdentityToDefinition(clone),Is.True);
                var after=EditorJsonUtility.ToJson(clone);
                Assert.That(DotBalanceSetup.ApplyFireballIdentityToDefinition(clone),Is.False);
                Assert.That(EditorJsonUtility.ToJson(clone),Is.EqualTo(after));
            }
            finally{UnityEngine.Object.DestroyImmediate(clone);}
        }
    }
}

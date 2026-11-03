using System;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Configuration;
using Godmist.Data.World;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Systems.SaveLoad;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class PartyDotResistanceBalanceTests
    {
        const string Root="Assets/_Project/ScriptableObjects/";
        static string Folder=>Path.GetFullPath("../LocalHandoffs/party-dot-resistances-20261103");
        static CharacterDefinition Definition(string name)=>AssetDatabase.LoadAssetAtPath<CharacterDefinition>(Root+"Characters/CD_"+name+".asset");
        static GameContentCatalog Catalog=>AssetDatabase.LoadAssetAtPath<GameContentCatalog>(Root+"World/GC_MainContentCatalog.asset");
        static SerializedProperty R(SerializedObject data,string field)=>data.FindProperty("<BaseResistances>k__BackingField").FindPropertyRelative("<"+field+">k__BackingField");
        [Serializable] sealed class Snapshot { public string mage,paladin; }
        [Test,Explicit("Approved native data-only party DoT resistance authoring; no global curve edits.")]
        public void AuthorApprovedProfiles()
        {
            Directory.CreateDirectory(Folder);var file=Path.Combine(Folder,"before.json");Assert.That(File.Exists(file),Is.False);
            var definitions=AssetDatabase.FindAssets("t:CharacterDefinition",new[]{Root}).Select(g=>AssetDatabase.LoadAssetAtPath<CharacterDefinition>(AssetDatabase.GUIDToAssetPath(g))).ToDictionary(d=>d,EditorJsonUtility.ToJson);
            var mage=Definition("Mage");var paladin=Definition("Paladin");
            File.WriteAllText(file,JsonUtility.ToJson(new Snapshot{mage=definitions[mage],paladin=definitions[paladin]},true));
            var data=new SerializedObject(mage);R(data,"BleedResistance").floatValue=.4f;R(data,"PoisonResistance").floatValue=.4f;R(data,"FireResistance").floatValue=.6f;
            Assert.That(data.ApplyModifiedPropertiesWithoutUndo(),Is.True);AssetDatabase.SaveAssetIfDirty(mage);
            data=new SerializedObject(paladin);R(data,"BleedResistance").floatValue=.4f;
            Assert.That(data.ApplyModifiedPropertiesWithoutUndo(),Is.True);AssetDatabase.SaveAssetIfDirty(paladin);
            foreach(var entry in definitions.Where(d=>d.Key!=mage&&d.Key!=paladin))Assert.That(EditorJsonUtility.ToJson(entry.Key),Is.EqualTo(entry.Value),entry.Key.Id);
            File.WriteAllText(Path.Combine(Folder,"after.json"),JsonUtility.ToJson(new Snapshot{mage=EditorJsonUtility.ToJson(mage),paladin=EditorJsonUtility.ToJson(paladin)},true));
        }
        [TestCase("Mage",.4f,.4f,.6f)] [TestCase("Paladin",.4f,.4f,.5f)]
        [TestCase("Wladimir_Test",.7f,.5f,.4f)] [TestCase("Jerzy",.5f,.7f,.4f)]
        public void FreshCharactersUseApprovedUnclampedBaseData(string name,float bleed,float poison,float fire)
        {
            var d=Definition(name);var c=new CharacterInstanceFactory().Create(d,d.ClassData);
            Assert.That(c.BaseResistances.BleedResistance,Is.EqualTo(bleed));Assert.That(c.Resistances.BleedResistance,Is.EqualTo(bleed));
            Assert.That(c.Resistances.PoisonResistance,Is.EqualTo(poison));Assert.That(c.Resistances.FireResistance,Is.EqualTo(fire));
        }
        [TestCase("bleed",51,2,122)] [TestCase("poison",40,3,144)] [TestCase("burn",11,3,30)]
        public void ActualMageTicksUseUnchangedCurve(string status,int rawTick,int turns,int expected)
        {
            var d=Definition("Mage");var c=new CharacterInstanceFactory().Create(d,d.ClassData);var unit=new BattleUnit{Character=c,IsPlayerSide=true};
            var session=new CombatSession{Grid=new BattleGrid(8,8),Units=new(){unit}};var system=new StatusEffectSystem();
            Assert.That(system.ApplyStatus(null,unit,status,1,turns,1,1,0,"audit.party",publishFeedback:false,statusDamagePerTurnOverride:rawTick,sharedRoll:0f),Is.True);
            var before=c.CurrentHealth;
            for(var t=0;t<turns;t++){system.ProcessTurnStart(session,unit);system.ProcessTurnEnd(unit);}
            Assert.That(before-c.CurrentHealth,Is.EqualTo(expected));
        }
        [Test]
        public void GlobalCurveAndStatusChanceFunctionAreUnchanged()
        {
            for(var i=0;i<=100;i++)
            {
                var r=i/100f;Assert.That(DamageOverTimePassiveEffectProcessor.CalculateDamageMultiplier(r),Is.EqualTo(r<=.5f?2-2*r:1.5f-r).Within(.00001f));
            }
            Assert.That(StatusEffectSystem.CalculateEffectChance(.7f,.4f),Is.EqualTo(.7144f).Within(.0001f));
            Assert.That(StatusEffectSystem.CalculateEffectChance(.9f,.6f),Is.EqualTo(.72f).Within(.0001f));
        }
        [Test]
        public void LegacySnapshotAdoptsNewBaseAndPreservesDebuffAndWounds()
        {
            var profile=AssetDatabase.LoadAssetAtPath<NewCampaignDefinition>(Root+"StartingCampaign/NC_Arungard.asset");
            Assert.That(new NewCampaignService().TryCreate(profile,out var session,out var error),Is.True,error);
            var c=session.CharactersById["character.mage"];c.CurrentHealth=17;
            c.BaseResistances=JsonUtility.FromJson<ResistanceSet>(JsonUtility.ToJson(c.BaseResistances));
            c.BaseResistances.BleedResistance=.25f;c.BaseResistances.PoisonResistance=.25f;c.BaseResistances.FireResistance=.8f;
            var debuff=new PassiveEffectRuntime{EffectId="test.party.resistance",Lifetime=PassiveEffectLifetime.Persistent};
            debuff.SetPayload(new ResistanceModifierPayload{ResistanceType=ResistanceType.Bleed,Value=-.15f});c.PassiveEffects.Add(debuff);
            new CharacterStatPipelineSystem().Refresh(c);
            var snapshot=new GameSessionSaveSnapshotFactory().CreateSnapshot(session,"legacy.party.resists");
            Assert.That(new GameSessionRestoreFactory().TryCreate(snapshot,Catalog,out var restored,out var failure),Is.True,failure?.ToString());
            var actual=restored.CharactersById[c.CharacterId];
            Assert.That(actual.BaseResistances.BleedResistance,Is.EqualTo(.4f));Assert.That(actual.Resistances.BleedResistance,Is.EqualTo(.25f).Within(.0001f));
            Assert.That(actual.Resistances.PoisonResistance,Is.EqualTo(.4f));Assert.That(actual.Resistances.FireResistance,Is.EqualTo(.6f));
            Assert.That(actual.CurrentHealth,Is.EqualTo(17));new CharacterStatPipelineSystem().Refresh(actual);
            Assert.That(actual.Resistances.BleedResistance,Is.EqualTo(.25f).Within(.0001f),"This is authored base balance, not an effective-resistance floor that disables debuffs.");
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Editor;
using Godmist.Runtime.Combat;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Godmist.Tests.EditMode
{
    public sealed class ContextualSkillHitTests
    {
        readonly List<Object> created = new();
        Random.State previousRandom;
        [SetUp] public void Seed() { previousRandom = Random.state; Random.InitState(8123); }
        [TearDown] public void Cleanup()
        { foreach (var o in created) if (o != null) Object.DestroyImmediate(o); created.Clear(); Random.state = previousRandom; }
        static void Set(object o, string field, object value) => o.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(o, value);
        T Keep<T>(T o) where T : Object { created.Add(o); return o; }
        static BattleUnit Unit(bool player, int x = 1, int y = 1)
        {
            var d = AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/ScriptableObjects/Catacombs/Enemies/CD_Catacombs_swordsman.asset");
            var c = new CharacterInstanceFactory().Create(d,d.ClassData,1); c.CharacterId += Guid.NewGuid();
            c.Stats.MinAttack = new Stat(100); c.Stats.MaxAttack = new Stat(100); c.Stats.CritChance = new Stat(0);
            c.Stats.Dodge = new Stat(0); c.Stats.PhysicalDefense = new Stat(0); c.Stats.MaxHealth = new Stat(10000); c.CurrentHealth = 10000;
            return new BattleUnit { Character=c, IsPlayerSide=player, Position=new(x,y), CurrentActionPoints=10000, MaxActionPoints=10000 };
        }
        static CombatSession Session(params BattleUnit[] units)
        {
            var s = new CombatSession { Grid=new BattleGrid(8,8), Units=units.ToList(), ActiveUnit=units[0] };
            foreach (var u in units) u.WarriorState.Session=s;
            return s;
        }
        static SkillEffectData Damage(int hits=1)
        {
            var e=new SkillEffectData(); Set(e,"effectType",EffectType.DealDamage); Set(e,"damageHitCount",hits); Set(e,"alwaysHits",false); return e;
        }
        static SkillEffectData Modifier(EffectType type=EffectType.ModifyStat, float chance=1f, bool hit=false)
        {
            var e=new SkillEffectData(); Set(e,"effectType",type); Set(e,"alwaysHits",true); Set(e,"baseChance",chance);
            Set(e,"durationTurns",2); Set(e,"modifierValue",-10f); Set(e,"modifierStatType",CharacterStatType.WeaponAccuracy);
            if (hit)
            {
                var c=new SkillEffectConditionData(); Set(c,"conditionType",EffectConditionType.ReferencedEffectSucceeded); Set(c,"referencedEffectIndex",0);
                Set(e,"conditions",new List<SkillEffectConditionData>{c});
            }
            return e;
        }
        SkillData Skill(params SkillEffectData[] effects)
        {
            var skill=Keep(ScriptableObject.CreateInstance<SkillData>());
            Set(skill,"<SkillId>k__BackingField","skill.context.tests"); Set(skill,"<DisplayName>k__BackingField","Test");
            Set(skill,"<SkillAccuracy>k__BackingField",1000f); Set(skill,"<AttackType>k__BackingField",AttackType.Melee);
            Set(skill,"effects",effects.ToList()); Set(skill,"targetModes",new List<SkillTargetModeData>{new()}); return skill;
        }
        static bool Cast(CombatSession s, SkillData skill, params BattleUnit[] targets) =>
            new SkillExecutionSystem(statusEffectSystem:new StatusEffectSystem()).TryExecute(s,s.ActiveUnit,skill,
                new Dictionary<int,IReadOnlyList<BattleUnit>>{{0,targets}});
        static bool Modified(BattleUnit target, SkillData skill) => target.Character.PassiveEffects.Any(e => e.EffectId.StartsWith(skill.SkillId+"."));

        [Test] public void AuthoredProductionRulesAreValidAndReauthoringIsIdempotent()
        {
            var skills=AssetDatabase.FindAssets("t:SkillData",new[]{"Assets/_Project/ScriptableObjects"})
                .Select(g=>AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
            foreach (var group in ContextualSkillHitSetup.Rules.GroupBy(r=>r.SkillId))
            {
                var original=skills.Single(s=>s.SkillId==group.Key);
                Assert.That(CombatDataValidator.ValidateSkill(original).IsValid,Is.True,group.Key);
                var clone=Keep(Object.Instantiate(original)); var before=EditorJsonUtility.ToJson(clone);
                var data=new SerializedObject(clone); ContextualSkillHitSetup.Apply(data); data.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(EditorJsonUtility.ToJson(clone),Is.EqualTo(before),group.Key+" not authored or not idempotent");
            }
        }
        [Test] public void DamageDependencyIsPerTargetAndDoesNotRerollAccuracy()
        {
            var source=Unit(true);var hit=Unit(false,2,1);var miss=Unit(false,3,1);miss.Character.Stats.Dodge=new Stat(1e20f);
            var skill=Skill(Damage(),Modifier(hit:true));
            Assert.That(Cast(Session(source,hit,miss),skill,hit,miss),Is.True);
            Assert.That(hit.Character.CurrentHealth,Is.LessThan(10000));Assert.That(Modified(hit,skill),Is.True);
            Assert.That(miss.Character.CurrentHealth,Is.EqualTo(10000));Assert.That(Modified(miss,skill),Is.False);
        }
        [Test] public void FullyAbsorbedHitStillTriggersAnOnHitEffect()
        {
            var source=Unit(true);var target=Unit(false,2,1);target.Character.ShieldPool.AddLayer(10000);
            var skill=Skill(Damage(),Modifier(hit:true));Assert.That(Cast(Session(source,target),skill,target),Is.True);
            Assert.That(target.Character.CurrentHealth,Is.EqualTo(10000));Assert.That(target.Character.ShieldPool.TotalShield,Is.LessThan(10000));
            Assert.That(Modified(target,skill),Is.True);
        }
        [Test] public void AnySuccessfulHitInAMultihitSequenceTriggersOneFollowup()
        {
            var skill=Skill(Damage(3),Modifier(hit:true));Set(skill,"<SkillAccuracy>k__BackingField",100f);
            var foundMixed=false;
            for(var seed=1;seed<=30&&!foundMixed;seed++)
            {
                var source=Unit(true);var target=Unit(false,2,1);target.Character.Stats.Dodge=new Stat(100);
                var s=Session(source,target);var hits=0;s.DamageApplied+=(a,b,c,d,e)=>hits++;
                Random.InitState(seed);Assert.That(Cast(s,skill,target),Is.True);
                if(hits is 1 or 2){foundMixed=true;Assert.That(Modified(target,skill),Is.True);Assert.That(target.Character.PassiveEffects.Count(e=>e.EffectId.StartsWith(skill.SkillId+".")),Is.EqualTo(1));}
            }
            Assert.That(foundMixed,Is.True,"Fixed seeds must exercise a sequence containing both hits and misses.");
        }
        [Test] public void IndependentAreaEffectStillAppliesWhenDamageMisses()
        {
            var source=Unit(true);var target=Unit(false,2,1);target.Character.Stats.Dodge=new Stat(1e20f);
            var skill=Skill(Damage(),Modifier());Assert.That(Cast(Session(source,target),skill,target),Is.True);
            Assert.That(target.Character.CurrentHealth,Is.EqualTo(10000));Assert.That(Modified(target,skill),Is.True);
        }
        [Test] public void FailedPoisonDoesNotBlockADifferentEffectReferencingTheDamageHit()
        {
            var source=Unit(true);var target=Unit(false,2,1);
            var poison=new SkillEffectData();Set(poison,"effectType",EffectType.ApplyStatus);Set(poison,"alwaysHits",true);
            Set(poison,"statusId",StatusEffectIds.Poison);Set(poison,"baseChance",0f);
            var skill=Skill(Damage(),poison,Modifier(hit:true));Assert.That(Cast(Session(source,target),skill,target),Is.True);
            Assert.That(StatusEffectSystem.HasStatus(target,StatusEffectIds.Poison),Is.False);Assert.That(Modified(target,skill),Is.True);
        }
        [TestCase(EffectType.ModifyStat)] [TestCase(EffectType.ModifyVar)] [TestCase(EffectType.ModifyResistance)]
        public void ModifierChanceIsRolledExactlyOnceAndZeroNeverApplies(EffectType type)
        {
            var skill=Skill(Modifier(type,.85f));Set(skill,"<SkillId>k__BackingField","skill.paladin.blinding_light");
            for(var seed=1;seed<=20;seed++)
            {
                Random.InitState(seed);var expected=Random.value<.85f;
                var source=Unit(true);var target=Unit(false,2,1);Random.InitState(seed);
                Assert.That(Cast(Session(source,target),skill,target),Is.True);Assert.That(Modified(target,skill),Is.EqualTo(expected),type+" seed "+seed);
            }
            foreach(var chance in new[]{0f,1f})
            {
                Set(skill.Effects[0],"baseChance",chance);var source=Unit(true);var target=Unit(false,2,1);
                Assert.That(Cast(Session(source,target),skill,target),Is.True);Assert.That(Modified(target,skill),Is.EqualTo(chance==1f));
            }
        }
        [Test] public void KickCanDisorientWhenWallBlocksMovementButCannotDisorientOnAMiss()
        {
            var original=AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/ScriptableObjects/Skills/SD_WarriorKick.asset");
            var skill=Keep(Object.Instantiate(original));Set(skill,"<SkillAccuracy>k__BackingField",1000f);
            Set(skill,"<ResourceCost>k__BackingField",0);Set(skill.Effects[1],"baseChance",1f);
            var source=Unit(true,6,1);var target=Unit(false,7,1);
            Assert.That(Cast(Session(source,target),skill,target),Is.True);Assert.That(target.Position,Is.EqualTo(new GridPosition(7,1)));
            Assert.That(Modified(target,skill),Is.True);
            source=Unit(true,6,1);target=Unit(false,7,1);target.Character.Stats.Dodge=new Stat(1e20f);
            Assert.That(Cast(Session(source,target),skill,target),Is.True);Assert.That(Modified(target,skill),Is.False);
        }
        [Test] public void TooltipNamesActualDamageReferenceRatherThanPreviousPoison()
        {
            var poison=new SkillEffectData();Set(poison,"effectType",EffectType.ApplyStatus);Set(poison,"statusId",StatusEffectIds.Poison);
            var skill=Skill(Damage(),poison,Modifier(hit:true));
            var go=Keep(new GameObject("tooltip",typeof(RectTransform),typeof(UIWorkspaceTooltipView)));
            var text=go.GetComponent<UIWorkspaceTooltipView>().BuildLoadoutSkillDetails(skill,null);
            Assert.That(text,Does.Contain("Warunek: trafienie celu").And.Not.Contain("poprzedni efekt"));
        }
        [Test] public void PreviewSeparatesHitChanceFromIndependentAreaStatusChance()
        {
            var source=Unit(true);var target=Unit(false,2,1);target.Character.Stats.Dodge=new Stat(100);
            var burn=new SkillEffectData();Set(burn,"effectType",EffectType.ApplyStatus);Set(burn,"alwaysHits",true);
            Set(burn,"statusId",StatusEffectIds.Burn);Set(burn,"baseChance",.8f);
            var dependency=new SkillEffectConditionData();Set(dependency,"conditionType",EffectConditionType.ReferencedEffectSucceeded);
            Set(dependency,"referencedEffectIndex",0);Set(burn,"conditions",new List<SkillEffectConditionData>{dependency});
            var skill=Skill(Damage(3),burn);Set(skill,"<SkillAccuracy>k__BackingField",100f);
            var chance=StatusEffectSystem.CalculateStatusApplicationChance(source,target,StatusEffectIds.Burn,.8f);
            var preview=CombatSkillPreviewCalculator.CalculateTargetPreview(source,target,skill);
            Assert.That(preview.StatusChance,Is.EqualTo(chance*(1f-Mathf.Pow(1f-preview.HitChance,3))).Within(.0001f));
            Set(burn,"conditions",new List<SkillEffectConditionData>());
            preview=CombatSkillPreviewCalculator.CalculateTargetPreview(source,target,skill);
            Assert.That(preview.StatusChance,Is.EqualTo(chance).Within(.0001f));
        }
        [TestCase(EffectType.ModifyStat)] [TestCase(EffectType.ModifyVar)] [TestCase(EffectType.ModifyResistance)]
        public void TooltipShowsModifierChanceOnceAlongsideItsHitDependency(EffectType type)
        {
            var skill=Skill(Damage(),Modifier(type,.4f,true));
            var go=Keep(new GameObject("tooltip",typeof(RectTransform),typeof(UIWorkspaceTooltipView)));
            var text=System.Text.RegularExpressions.Regex.Replace(go.GetComponent<UIWorkspaceTooltipView>().BuildLoadoutSkillDetails(skill,null),"<[^>]*>","");
            Assert.That(text,Does.Contain("40% szansy:").And.Contain("Warunek: trafienie celu"));
            Assert.That(text.Split(new[]{"40% szansy:"},StringSplitOptions.None).Length,Is.EqualTo(2));
        }
    }
}

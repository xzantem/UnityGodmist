using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Core.Constants;
using Godmist.Data.Characters;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Runtime.Items;
using Godmist.Systems.Characters;
using Godmist.Systems.Combat;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public partial class CharacterStatBreakdownTests
    {
        const string Assets="Assets/_Project/ScriptableObjects/";
        const BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
        GameObject tooltipRoot;UIWorkspaceTooltipView tooltip,previous;
        static string Plain(string text)=>Regex.Replace(text,"<[^>]*>","");
        static T Field<T>(object source,string name)=>(T)source.GetType().GetField(name,Hidden).GetValue(source);
        static CharacterInstance Character(string hero)
            =>new CharacterInstanceFactory().Create(AssetDatabase.LoadAssetAtPath<CharacterDefinition>(Assets+"Characters/CD_"+(hero=="Warrior"?"Wladimir_Test":hero=="Scout"?"Jerzy":hero)+".asset"),AssetDatabase.LoadAssetAtPath<CharacterClassData>(Assets+"Classes/CCD_"+hero+".asset"));
        [SetUp] public void Setup()
        {
            previous=UIWorkspaceTooltipView.ActiveInstance;
            tooltipRoot=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_Tooltip.prefab"));
            tooltip=tooltipRoot.GetComponent<UIWorkspaceTooltipView>();typeof(UIWorkspaceTooltipView).GetProperty("ActiveInstance").SetValue(null,tooltip);
        }
        [TearDown] public void Cleanup(){Object.DestroyImmediate(tooltipRoot);typeof(UIWorkspaceTooltipView).GetProperty("ActiveInstance").SetValue(null,previous);}
        static void AllRanks(CharacterInstance c,int rank)
        {
            foreach(var tree in c.ClassData.SkillTrees)
            {
                var progress=c.SkillTreeProgress.FirstOrDefault(p=>p.TreeId==tree.Id);
                if(progress==null){progress=new SkillTreeProgress{TreeId=tree.Id};c.SkillTreeProgress.Add(progress);}
                progress.NodeInvestments.Clear();
                foreach(var node in tree.Nodes.Where(n=>n.GrantedSkill==null))progress.NodeInvestments.Add(new SkillTreeNodeInvestment{NodeId=node.Id,Ranks=Math.Min(rank,node.EffectiveMaxRanks)});
            }
        }
        [Test] public void ProjectionMatchesAllStatsAcrossClassesTalentsResourcesAndBurnWithoutMutation()
        {
            foreach(var hero in new[]{"Warrior","Scout","Mage","Paladin"})foreach(var rank in new[]{0,1,3})foreach(var fraction in new[]{0f,.4f,1f})foreach(var burn in new[]{false,true})
            {
                var c=Character(hero);AllRanks(c,rank);new CharacterStatPipelineSystem().Refresh(c);
                c.ResourcePool.CurrentValue=Mathf.RoundToInt(c.ResourcePool.MaxValue*fraction);
                if(burn)c.PassiveEffects.Add(new PassiveEffectRuntime{EffectId=StatusEffectIds.Burn,Lifetime=PassiveEffectLifetime.Timed,RemainingDuration=3});
                new CharacterStatPipelineSystem().Refresh(c);
                var before=JsonUtility.ToJson(c);var old=Enum.GetValues(typeof(CharacterStatType)).Cast<CharacterStatType>().ToDictionary(t=>t,t=>c.Stats.GetStat(t).GetValue());var random=UnityEngine.Random.state;
                var projection=CharacterStatExplanation.Create(c);
                foreach(var pair in old)
                {
                    Assert.That(projection.Values[pair.Key],Is.EqualTo(pair.Value).Within(.0001f),hero+" rank="+rank+" / "+pair.Key);
                    Assert.That(tooltip.BuildCharacterStatDetails(c,pair.Key),Does.Not.Contain("Wartość bieżąca"));
                }
                Assert.That(JsonUtility.ToJson(c),Is.EqualTo(before));Assert.That(UnityEngine.Random.state,Is.EqualTo(random));
                foreach(var pair in old)Assert.That(c.Stats.GetStat(pair.Key).GetValue(),Is.EqualTo(pair.Value));
            }
        }
        static void AddStat(CharacterInstance c,string name,StatModifierOperation operation,float min,float max,StatModifierStacking stacking=StatModifierStacking.Additive)
        {
            var e=new PassiveEffectRuntime{EffectId=name,DisplayName=name,Lifetime=PassiveEffectLifetime.Persistent};
            e.SetPayload(new StatModifiersPayload{Modifiers=new(){new StatModifier{StatType=CharacterStatType.MinAttack,Operation=operation,Stacking=stacking,Value=min},new StatModifier{StatType=CharacterStatType.MaxAttack,Operation=operation,Stacking=stacking,Value=max}}});c.PassiveEffects.Add(e);
        }
        [Test] public void OrderedStagesAndAdditivePercentSourcesExplainThePairedAttack()
        {
            var c=Character("Warrior");c.Equipment=new EquipmentSet();c.PassiveEffects.Clear();c.Stats.MinAttack=new Stat(40);c.Stats.MaxAttack=new Stat(60);
            AddStat(c,"Dodatek",StatModifierOperation.FlatBase,10,20);AddStat(c,"Premia A",StatModifierOperation.Percent,.2f,.2f);AddStat(c,"Premia B",StatModifierOperation.Percent,.3f,.3f);
            AddStat(c,"Mnożnik",StatModifierOperation.Percent,1,1,StatModifierStacking.Multiplicative);AddStat(c,"Kara",StatModifierOperation.FlatAbsolute,-5,-5);AddStat(c,"Ograniczenie",StatModifierOperation.Maximum,200,200);
            new CharacterStatPipelineSystem().Refresh(c);Assert.That(c.Stats.MinAttack.GetValue(),Is.EqualTo(145).Within(.001));Assert.That(c.Stats.MaxAttack.GetValue(),Is.EqualTo(200));
            var body=tooltip.BuildCharacterStatDetails(c,CharacterStatType.MinAttack,"145–200");
            Assert.That(Plain(body),Does.Contain("40–60").And.Contain("×150%").And.Contain("×200%").And.Contain("145–200").And.Contain("Limit"));
            Assert.That(body,Does.Contain("#8ED59B").And.Contain("#F08C85"));
        }
        [Test] public void ResistanceSourcesIncludeBaseEffectsMagePassiveAndOnlyActiveSockets()
        {
            var c=Character("Mage");AllRanks(c,1);c.BaseResistances.FireResistance=.2f;
            var effect=new PassiveEffectRuntime{EffectId="resistance-test",DisplayName="Osłabiona odporność",Lifetime=PassiveEffectLifetime.Persistent};effect.SetPayload(new ResistanceModifierPayload{ResistanceType=ResistanceType.All,Value=-.1f});c.PassiveEffects.Add(effect);
            var catalog=AssetDatabase.LoadAssetAtPath<Godmist.Data.World.GameContentCatalog>(Assets+"World/GC_MainContentCatalog.asset");
            c.Equipment.Weapon=new EquipmentItemInstance{Definition=catalog.ItemDefinitions.OfType<Godmist.Data.Items.EquipmentItemDefinition>().First(d=>d.RequiredClass?.Id=="class.mage"&&d.ItemType==ItemType.Weapon),InstanceId="resistance-weapon",UpgradeProgress=.2f};
            c.Equipment.Weapon.SocketedGaldurytes=new(){new GalduryteItemInstance{CustomName="Aktywny galduryt",IsIdentified=true,Effects=new(){new GalduryteEffectInstance{EffectType=GalduryteEffectType.AllResistance,Value=.1f}}},new GalduryteItemInstance{CustomName="Nieaktywne gniazdo",IsIdentified=true,Effects=new(){new GalduryteEffectInstance{EffectType=GalduryteEffectType.AllResistance,Value=.9f}}}};
            new CharacterStatPipelineSystem().Refresh(c);var before=JsonUtility.ToJson(c);var trace=new CombatValueExplanation();
            Assert.That(ModifyResistanceSystem.CalculateResistance(c,ResistanceType.Fire),Is.EqualTo(ModifyResistanceSystem.CalculateResistance(c,ResistanceType.Fire,explanation:trace)));
            var body=tooltip.BuildCharacterResistanceDetails(c,ResistanceType.Fire);
            Assert.That(Plain(body),Does.Contain("20%").And.Contain("Osłabiona odporność").And.Contain("Aktywny galduryt").And.Not.Contain("Nieaktywne gniazdo"));Assert.That(JsonUtility.ToJson(c),Is.EqualTo(before));
        }
        [Test] public void ResistancePrecisionDoesNotInventAnInactiveLimit()
        {
            var c=Character("Mage");c.BaseResistances.FireResistance=.1234567f;new CharacterStatPipelineSystem().Refresh(c);
            Assert.That(tooltip.BuildCharacterResistanceDetails(c,ResistanceType.Fire),Does.Not.Contain("Limit"));
            var e=new PassiveEffectRuntime{EffectId="resistance-cap-test",DisplayName="Premia odporności",Lifetime=PassiveEffectLifetime.Persistent};e.SetPayload(new ResistanceModifierPayload{ResistanceType=ResistanceType.Fire,Value=2});c.PassiveEffects.Add(e);new CharacterStatPipelineSystem().Refresh(c);
            Assert.That(Plain(tooltip.BuildCharacterResistanceDetails(c,ResistanceType.Fire)),Does.Contain("Limit").And.Contain("100%"));
        }
        [Test] public void PointerHitsTheNumericLinkAndLeavingNotifiesTheSharedTooltip()
        {
            var canvasRoot=new GameObject("Stat pointer proof",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));var cameraRoot=new GameObject("Stat pointer camera",typeof(Camera));var render=new RenderTexture(1000,700,24);var controller=new CharacterStatHoverController();
            try
            {
                var camera=cameraRoot.GetComponent<Camera>();camera.transform.position=new Vector3(0,0,-10);camera.orthographic=true;camera.targetTexture=render;camera.cullingMask=1<<31;
                canvasRoot.layer=31;var canvas=canvasRoot.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;canvas.sortingOrder=32760;
                var text=Object.Instantiate(Field<TextMeshProUGUI>(tooltip,"bodyText"),canvasRoot.transform);text.gameObject.layer=31;text.gameObject.SetActive(true);text.raycastTarget=true;
                var rect=text.rectTransform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=Vector2.zero;rect.sizeDelta=new Vector2(500,80);
                var c=Character("Warrior");text.text=CharacterStatValueLinks.Stat(c.Stats.MaxHealth.GetValue().ToString("0"),CharacterStatType.MaxHealth);tooltipRoot.SetActive(false);
                Canvas.ForceUpdateCanvases();text.ForceMeshUpdate();Assert.That(text.textInfo.linkCount,Is.EqualTo(1));
                var glyph=text.textInfo.characterInfo[text.textInfo.linkInfo[0].linkTextfirstCharacterIndex];var point=RectTransformUtility.WorldToScreenPoint(camera,text.transform.TransformPoint((glyph.bottomLeft+glyph.topRight)*.5f));
                controller.Bind(c,canvasRoot.transform,tooltip,text);controller.Tick(point);
                Assert.That(Field<object>(tooltip,"pendingSource"),Is.SameAs(controller),"The numeric glyph must be a working hover source");
                controller.Tick(new Vector2(-10000,-10000));Assert.That(Field<bool>(tooltip,"sourceHovered"),Is.False);
                controller.Clear();Assert.That(Field<object>(tooltip,"pendingSource"),Is.Null);
            }
            finally{controller.Clear();tooltipRoot.SetActive(true);cameraRoot.GetComponent<Camera>().targetTexture=null;Object.DestroyImmediate(render);Object.DestroyImmediate(cameraRoot);Object.DestroyImmediate(canvasRoot);}
        }
        [Test] public void BothAuthoredViewsHaveValueLinksAndExposeMaximumHealthAndResource()
        {
            var canvas=new GameObject("Character stat link test",typeof(RectTransform),typeof(Canvas));
            try
            {
                canvas.AddComponent<UIWorkspaceWindowManager>();
                var c=Character("Warrior");var session=new Godmist.Runtime.Game.GameSession(GameModeState.Exploration);session.TryRegisterCharacter(c);
                var card=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CharacterInfoWindow Variant.prefab"),canvas.transform).GetComponent<CharacterInfoWindowView>();typeof(CharacterInfoWindowView).GetMethod("Awake",Hidden).Invoke(card,null);card.Initialize(session,c.CharacterId);
                foreach(var field in new[]{"statsText","resistancesText","healthResourceText"})
                {var text=Field<TMP_Text>(card,field).text;Assert.That(text,Does.Contain("<link=\"character-"),field);Assert.That(text,Does.Not.Contain("<u>"));}
                var inspect=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CombatUnitInspectPanel.prefab"),canvas.transform).GetComponent<CombatUnitInspectWindowView>();inspect.Show(new BattleUnit{Character=c,IsPlayerSide=true},tooltip);
                foreach(var field in new[]{"statsLeftValues","statsRightValues"})Assert.That(Field<TMP_Text>(inspect,field).text,Does.Contain("<link=\"character-"),field);
                var vitals=Field<CombatVitalsView>(inspect,"vitals");Assert.That(vitals.HealthValueText.text,Does.Contain("character-stat.MaxHealth"));Assert.That(vitals.ResourceValueText.text,Does.Contain("character-stat.MaxResource"));
            }
            finally{Object.DestroyImmediate(canvas);}
        }
    }
}

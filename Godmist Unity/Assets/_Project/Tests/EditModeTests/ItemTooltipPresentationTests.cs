using System.Collections.Generic;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Items;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Items;
using Godmist.Systems.Characters;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Godmist.Tests.EditMode
{
    public class ItemTooltipPresentationTests
    {
        EquipmentItemDefinition definition;
        EquipmentItemInstance item;
        CharacterInstance owner;
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Field(ItemDefinition target,string name,object value)=>typeof(ItemDefinition).GetField("<"+name+">k__BackingField",Hidden).SetValue(target,value);

        [TestCase(52)]
        [TestCase(1234)]
        public void LootGoldTooltipUsesCurrencyNameAndAmount(int amount)
        {
            var canvas = new GameObject("loot currency test", typeof(RectTransform), typeof(Canvas));
            try
            {
                var tooltipObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_Tooltip.prefab"), canvas.transform);
                var tooltip = tooltipObject.GetComponent<UIWorkspaceTooltipView>();
                typeof(UIWorkspaceTooltipView).GetMethod("OnEnable", Hidden).Invoke(tooltip, null);
                var loot = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_DungeonLootWindow.prefab"), canvas.transform);
                var gold = loot.GetComponentInChildren<DungeonLootGoldSlotView>(true);
                gold.Bind(amount, loot.GetComponent<DungeonLootWindowView>());
                gold.OnPointerEnter(new UnityEngine.EventSystems.PointerEventData(null));
                ((System.Action)typeof(UIWorkspaceTooltipView).GetField("pendingShow", Hidden).GetValue(tooltip))();
                var title = (TMP_Text)typeof(UIWorkspaceTooltipView).GetField("titleText", Hidden).GetValue(tooltip);
                var body = (TMP_Text)typeof(UIWorkspaceTooltipView).GetField("bodyText", Hidden).GetValue(tooltip);
                Assert.That(title.text, Is.EqualTo("Złoto"));
                Assert.That(body.text, Is.EqualTo(amount.ToString()));
                var bounds = (RectTransform)typeof(UIWorkspaceTooltipView).GetField("tooltipRect", Hidden).GetValue(tooltip);
                Assert.That(bounds.rect.width, Is.LessThan(300f), "A currency tooltip should fit its short content.");
                Assert.That(gold.GetComponentInChildren<TMP_Text>(true).text, Is.EqualTo(amount.ToString()));
            }
            finally { Object.DestroyImmediate(canvas); }
        }

        [Test]
        public void LootPrefabUsesCurrentCurrencyAndVisibleFramedContent()
        {
            var canvas = new GameObject("loot chrome test", typeof(RectTransform), typeof(Canvas));
            try
            {
                canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var loot = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_DungeonLootWindow.prefab"), canvas.transform);
                var data = new SerializedObject(loot.GetComponent<DungeonLootWindowView>());
                var title = (TMP_Text)data.FindProperty("titleText").objectReferenceValue;
                title.text = "Łup ze skrzyni";
                Canvas.ForceUpdateCanvases(); title.ForceMeshUpdate(true, true);
                Assert.That(title.textInfo.meshInfo[0].vertexCount, Is.GreaterThan(0), "The header must not ellipsize away its entire title.");
                var gold = (DungeonLootGoldSlotView)data.FindProperty("goldSlot").objectReferenceValue;
                var goldIcon = (Image)new SerializedObject(gold).FindProperty("icon").objectReferenceValue;
                Assert.That(goldIcon.sprite, Is.SameAs(AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Icons/ResourceRewards/gold-32.png")));
                Assert.That(goldIcon.preserveAspect, Is.True);
                var template = (DungeonLootItemView)data.FindProperty("itemTemplate").objectReferenceValue;
                var palette = loot.GetComponentInChildren<UIWorkspaceStyleApplier>(true).Palette;
                Assert.That(template.GetComponent<Image>().sprite, Is.SameAs(palette.GetArtwork("icon_button_normal")));
                template.Bind(item, 1, loot.GetComponent<DungeonLootWindowView>());
                Assert.That(template.GetComponentInChildren<ItemRarityBorderGraphic>(true), Is.Not.Null);
            }
            finally { Object.DestroyImmediate(canvas); }
        }

        [Test]
        public void LootGoldCollectsOnlyWithLeftClickAndCannotBeClaimedTwice()
        {
            var canvas = new GameObject("loot collection test", typeof(RectTransform), typeof(Canvas));
            try
            {
                var session = new Godmist.Runtime.Game.GameSession(GameModeState.Exploration);
                var dungeon = new Godmist.Runtime.Dungeons.DungeonInstance("loot.ui", 1);
                var floor = new Godmist.Runtime.Dungeons.DungeonFloorInstance(0);
                floor.TrySetMap(new Godmist.Runtime.Dungeons.DungeonFloorMap());
                dungeon.TryAddFloor(floor); session.TrySetActiveDungeon(dungeon);
                var body = new Godmist.Runtime.Dungeons.DungeonEnemyBodyInstance("loot.body", "enemy", Vector2Int.zero, 52, null);
                Assert.That(floor.TryAddEnemyBody(body), Is.True);
                var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_DungeonLootWindow.prefab"), canvas.transform);
                var view = root.GetComponent<DungeonLootWindowView>(); view.Initialize(session, body.InstanceId);
                var gold = root.GetComponentInChildren<DungeonLootGoldSlotView>(true);
                var click = new UnityEngine.EventSystems.PointerEventData(null) { button = UnityEngine.EventSystems.PointerEventData.InputButton.Right };
                var balance = session.Gold;
                gold.OnPointerClick(click); Assert.That(session.Gold, Is.EqualTo(balance));
                click.button = UnityEngine.EventSystems.PointerEventData.InputButton.Middle;
                gold.OnPointerClick(click); Assert.That(session.Gold, Is.EqualTo(balance));
                click.button = UnityEngine.EventSystems.PointerEventData.InputButton.Left;
                gold.OnPointerClick(click); Assert.That(session.Gold, Is.EqualTo(balance + 52));
                Assert.That(gold.gameObject.activeSelf, Is.False);
                gold.OnPointerClick(click); Assert.That(session.Gold, Is.EqualTo(balance + 52));
            }
            finally { Object.DestroyImmediate(canvas); }
        }

        [SetUp] public void Setup()
        {
            definition=ScriptableObject.CreateInstance<EquipmentItemDefinition>();
            Field(definition,"DisplayName","Pancerz testowy");Field(definition,"Description","Pozwala zachować swobodę ruchu.");Field(definition,"ItemType",ItemType.Armor);Field(definition,"BaseRarity",RarityTier.Common);Field(definition,"Weight",5f);
            item=new EquipmentItemInstance{Definition=definition,InstanceId="tooltip candidate",ItemLevel=5,RequiredLevel=5,CurrentRarity=RarityTier.Common,StatModifiers=new(){new StatModifier{StatType=CharacterStatType.MaxHealth,Value=50},new StatModifier{StatType=CharacterStatType.Dodge,Value=10}}};
            owner=new CharacterInstanceFactory().Create(AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/ScriptableObjects/Characters/CD_Jerzy.asset"));
        }
        [TearDown] public void Cleanup()=>Object.DestroyImmediate(definition);
        [Test]
        public void JewelryTooltipPutsGemEffectsBeforeItemMetadata()
        {
            var ring = AssetDatabase.LoadAssetAtPath<JewelryItemDefinition>("Assets/_Project/ScriptableObjects/Equipment/Jewelry/EID_Ring.asset");
            var item = new Godmist.Systems.Items.JewelryGenerationService().GenerateSelected(ring, ring.Metals[0], ring.Gems[0]);
            var text = UIWorkspaceTooltipView.BuildEquipmentDetails(item, null);
            Assert.That(text, Does.Contain("<b>Efekty</b>"));
            var effect = text.IndexOf("Granat:");
            Assert.That(effect, Is.GreaterThan(text.IndexOf("<b>Efekty</b>")));
            Assert.That(effect, Is.LessThan(text.IndexOf(ItemDetailsDividerView.MarkerLine)));
            Assert.That(effect, Is.LessThan(text.IndexOf("Typ:")));
            Assert.That(text.LastIndexOf("Granat:"), Is.EqualTo(effect));
            Assert.That(text, Does.Contain("Poziom: 10"));
        }

        [Test]
        public void JewelryIngredientTooltipShowsFlatCriticalChanceAsPercent()
        {
            var modifier = new JewelryModifierData();
            typeof(JewelryModifierData).GetField("<Target>k__BackingField", Hidden).SetValue(modifier, JewelryModifierTarget.Stat);
            typeof(JewelryModifierData).GetField("<StatType>k__BackingField", Hidden).SetValue(modifier, CharacterStatType.CritChance);
            typeof(JewelryModifierData).GetField("<Operation>k__BackingField", Hidden).SetValue(modifier, StatModifierOperation.FlatAbsolute);
            typeof(JewelryModifierData).GetField("<Value>k__BackingField", Hidden).SetValue(modifier, .01f);
            var text = UIWorkspaceTooltipView.BuildJewelryIngredientEffects(new[] { modifier });
            Assert.That(text, Does.Contain("1%"));
            Assert.That(text, Does.Not.Contain("0.01"));
        }

        [Test]
        public void JewelryCraftingSummaryAggregatesActualStatsAndKeepsPercentUnits()
        {
            var jewelryDefinition = ScriptableObject.CreateInstance<JewelryItemDefinition>();
            try
            {
                var jewelry = new JewelryItemInstance { Definition = jewelryDefinition, StatModifiers = new()
                {
                    new StatModifier { StatType = CharacterStatType.MinAttack, Value = 2 },
                    new StatModifier { StatType = CharacterStatType.MaxAttack, Value = 2 },
                    new StatModifier { StatType = CharacterStatType.MinAttack, Value = 1 },
                    new StatModifier { StatType = CharacterStatType.MaxAttack, Value = 1 },
                    new StatModifier { StatType = CharacterStatType.MaxHealth, Operation = StatModifierOperation.Percent, Value = .1f }
                }};
                var summary = UIWorkspaceTooltipView.BuildJewelryCraftingSummary(jewelry);
                Assert.That(summary, Does.Contain("Atak: 3"));
                Assert.That(summary, Does.Contain("+10%"));
                Assert.That(summary, Does.Not.Contain(ItemStatColumnsView.Marker));
                Assert.That(summary, Does.Not.Contain("Poziom:"));
            }
            finally { Object.DestroyImmediate(jewelryDefinition); }
        }

        [Test]
        [TestCase(false)]
        [TestCase(true)]
        public void ReforgeSuccessProjectionUsesDetachedStatsAndPreservesOwnedItem(bool legacyBaseline)
        {
            item.UpgradeProgress=.4f;
            if(!legacyBaseline)item.BaseStatModifiers=new(){new StatModifier{StatType=CharacterStatType.MaxHealth,Value=50},new StatModifier{StatType=CharacterStatType.Dodge,Value=10}};
            var baseline=item.BaseStatModifiers;var stats=item.StatModifiers;
            var node=new GameObject("reforge preview test");
            try
            {
                var view=node.AddComponent<EquipmentUpgradeWindowView>();
                typeof(EquipmentUpgradeWindowView).GetField("selected",Hidden).SetValue(view,item);
                var preview=new Godmist.Systems.Items.EquipmentUpgradePreview{GoldCost=1,IncreaseChance=.7f,TargetRarity=RarityTier.Rare};
                var projected=(EquipmentItemInstance)typeof(EquipmentUpgradeWindowView).GetMethod("BuildReforgeSuccessPreview",Hidden).Invoke(view,new object[]{preview});
                Assert.That(projected,Is.Not.SameAs(item));Assert.That(projected.CurrentRarity,Is.EqualTo(RarityTier.Rare));
                Assert.That(projected.UpgradeProgress,Is.EqualTo(item.UpgradeProgress));Assert.That(projected.SocketCapacity,Is.EqualTo(item.SocketCapacity));
                Assert.That(UIWorkspaceTooltipView.BuildEquipmentReforgeChanges(item,projected),Does.Contain("50 → <color=#3D9E63>62</color>"));
                Assert.That(item.CurrentRarity,Is.EqualTo(RarityTier.Common));Assert.That(item.BaseStatModifiers,Is.SameAs(baseline));Assert.That(item.StatModifiers,Is.SameAs(stats));
                Assert.That(item.BaseStatModifiers.Count,Is.EqualTo(legacyBaseline?0:2));Assert.That(item.StatModifiers[0].Value,Is.EqualTo(50));
            }
            finally{Object.DestroyImmediate(node);}
        }
        [Test]
        public void ReforgeComparisonIncludesSecondaryStatsInTooltipUnits()
        {
            Field(definition,"ItemType",ItemType.Weapon);
            item.StatModifiers=new(){new StatModifier{StatType=CharacterStatType.MinAttack,Value=15},new StatModifier{StatType=CharacterStatType.MaxAttack,Value=28},new StatModifier{StatType=CharacterStatType.CritChance,Value=.05f},new StatModifier{StatType=CharacterStatType.CritDamage,Value=.53f},new StatModifier{StatType=CharacterStatType.WeaponAccuracy,Value=65}};
            var next=new EquipmentItemInstance{Definition=definition,StatModifiers=new(){new StatModifier{StatType=CharacterStatType.MinAttack,Value=15},new StatModifier{StatType=CharacterStatType.MaxAttack,Value=28},new StatModifier{StatType=CharacterStatType.CritChance,Value=.055f},new StatModifier{StatType=CharacterStatType.CritDamage,Value=.583f},new StatModifier{StatType=CharacterStatType.WeaponAccuracy,Value=66.95f}}};
            var text=UIWorkspaceTooltipView.BuildEquipmentReforgeChanges(item,next);
            Assert.That(text,Does.Contain($"5 pp → <color=#3D9E63>{5.5f:0.##} pp</color>"));
            Assert.That(text,Does.Contain($"Obrażenia krytyczne: {.53f:0.00} → <color=#3D9E63>{.58f:0.00}</color>"));
            Assert.That(text,Does.Contain("Celność: 65 → <color=#3D9E63>67</color>"));
            Assert.That(text,Does.Not.Contain("Atak:"));Assert.That(text,Does.Not.Contain("Gniazda:"));
        }
        [Test]
        public void ReforgeComparisonColorsDecreasesAndOmitsUnchangedRoundedStats()
        {
            var next=new EquipmentItemInstance{Definition=definition,StatModifiers=new(){new StatModifier{StatType=CharacterStatType.MaxHealth,Value=40},new StatModifier{StatType=CharacterStatType.Dodge,Value=9.9f}}};
            var text=UIWorkspaceTooltipView.BuildEquipmentReforgeChanges(item,next);
            Assert.That(text,Does.Contain("50 → <color=#E06C75>40</color>"));
            Assert.That(text,Does.Not.Contain("Unik"));
            Assert.That(UIWorkspaceTooltipView.BuildEquipmentReforgeChanges(item,item),Is.Empty);
        }
        [Test]
        public void CurrentUpgradeStatsShowActualValuesWithoutOldComparison()
        {
            Field(definition,"ItemType",ItemType.Weapon);
            item.UpgradeProgress=.2f;
            item.StatModifiers=new(){new StatModifier{StatType=CharacterStatType.MinAttack,Value=18},new StatModifier{StatType=CharacterStatType.MaxAttack,Value=33.6f},new StatModifier{StatType=CharacterStatType.CritChance,Value=.05f}};
            Assert.That(UIWorkspaceTooltipView.BuildEquipmentUpgradeChanges(item,item,showCurrentValues:true),
                Is.EqualTo("Atak: 18–34\nGniazda: 1"));
        }
        [Test]
        public void UpgradeComparisonShowsAttackAndSocketsButNotUnchangedStats()
        {
            Field(definition,"ItemType",ItemType.Weapon);
            item.StatModifiers=new(){new StatModifier{StatType=CharacterStatType.MinAttack,Value=15},new StatModifier{StatType=CharacterStatType.MaxAttack,Value=28},new StatModifier{StatType=CharacterStatType.CritChance,Value=.05f}};
            var next=new EquipmentItemInstance{Definition=definition,UpgradeProgress=.2f,StatModifiers=new(){new StatModifier{StatType=CharacterStatType.MinAttack,Value=18},new StatModifier{StatType=CharacterStatType.MaxAttack,Value=33.6f},new StatModifier{StatType=CharacterStatType.CritChance,Value=.05f}}};
            var body=UIWorkspaceTooltipView.BuildEquipmentUpgradeChanges(item,next);
            Assert.That(body,Does.Contain("Atak: 15–28 → <color=#3D9E63>18–34</color>").And.Contain("Gniazda: 0 → <color=#3D9E63>1</color>").And.Not.Contain("krytyczna"));
            Assert.That(UIWorkspaceTooltipView.BuildEquipmentDetails(next,null),Does.Contain("Atak: 18–34"));
        }
        [Test]
        public void UpgradeComparisonShowsBothArmorDefensesAndSocketLoss()
        {
            item.UpgradeProgress=.4f;item.StatModifiers=new(){new StatModifier{StatType=CharacterStatType.PhysicalDefense,Value=28},new StatModifier{StatType=CharacterStatType.MagicalDefense,Value=14}};
            var next=new EquipmentItemInstance{Definition=definition,UpgradeProgress=.2f,StatModifiers=new(){new StatModifier{StatType=CharacterStatType.PhysicalDefense,Value=24},new StatModifier{StatType=CharacterStatType.MagicalDefense,Value=12}}};
            Assert.That(UIWorkspaceTooltipView.BuildEquipmentUpgradeChanges(item,next),Does.Contain("Obrona fizyczna: 28 → <color=#E06C75>24</color>").And.Contain("Obrona magiczna: 14 → <color=#E06C75>12</color>").And.Contain("Gniazda: 2 → <color=#E06C75>1</color>"));
        }
        [Test]
        public void UpgradeComparisonKeepsPercentModifiersSeparateAndOmitsUnchangedRoundedValues()
        {
            Field(definition,"ItemType",ItemType.Weapon);
            item.StatModifiers=new(){new StatModifier{StatType=CharacterStatType.MinAttack,Value=15},new StatModifier{StatType=CharacterStatType.MinAttack,Operation=StatModifierOperation.Percent,Value=.1f}};
            var next=new EquipmentItemInstance{Definition=definition,UpgradeProgress=.1f,StatModifiers=new(){new StatModifier{StatType=CharacterStatType.MinAttack,Value=15.1f},new StatModifier{StatType=CharacterStatType.MinAttack,Operation=StatModifierOperation.Percent,Value=.12f}}};
            Assert.That(UIWorkspaceTooltipView.BuildEquipmentUpgradeChanges(item,next),Does.Contain("Minimalny atak (%): 10% → <color=#3D9E63>12%</color>").And.Contain("Gniazda: 0 → 0").And.Not.Contain("Atak: "));
        }
        [Test] public void EquipmentUsesSignedStatsCompactRequirementsAndCurrencyIcon()
        {
            var body=UIWorkspaceTooltipView.BuildEquipmentDetails(item,null);
            Assert.That(body,Does.Contain("Rzadkość: Pospolity").And.Contain("Jakość: Normalna").And.Contain("Maksymalne zdrowie: +50").And.Contain("Unik: +10").And.Not.Contain("Galduryty: 0/0"));
            Assert.That(body.IndexOf("Statystyki"),Is.LessThan(body.IndexOf("Poziom:")));
            Assert.That(body,Does.Not.Contain("Typ: Pancerz").And.Not.Contain("Ulepszenie:").And.Not.Contain("Wymagany poziom:"));
            Assert.That(UIWorkspaceTooltipView.BuildItemMeta(item),Is.EqualTo("5 kg • <sprite name=\"gold\"> 0"));
            Assert.That(UIWorkspaceTooltipView.BuildItemMeta(item,-2),Is.EqualTo("5 kg • <sprite name=\"gold\"> 0"));
        }
        [Test] public void ComparisonIncludesLostStatsAndExplainsWhatIsCompared()
        {
            var equipped=new EquipmentItemInstance{Definition=definition,CustomName="Noszony pancerz",StatModifiers=new(){new StatModifier{StatType=CharacterStatType.MaxHealth,Value=75},new StatModifier{StatType=CharacterStatType.PhysicalDefense,Value=4}}};
            owner.Equipment.SetItem(EquipmentSlot.Armor,equipped);
            var body=UIWorkspaceTooltipView.BuildEquipmentDetails(item,owner);
            Assert.That(body,Does.Contain("Porównanie z: Noszony pancerz").And.Contain("-25").And.Contain("Obrona fizyczna: 0").And.Contain("-4"));
            Assert.That(UIWorkspaceTooltipView.BuildEquipmentDetails(equipped,owner),Does.Not.Contain("Porównanie z:"));
        }
        [Test] public void PercentageModifiersDoNotGetAddedToFlatHealthBonuses()
        {
            item.StatModifiers.Add(new StatModifier{StatType=CharacterStatType.MaxHealth,Operation=StatModifierOperation.Percent,Value=.1f});
            item.StatModifiers.Add(new StatModifier{StatType=CharacterStatType.MaxHealth,Operation=StatModifierOperation.Percent,Stacking=StatModifierStacking.Multiplicative,Value=.2f});
            item.StatModifiers.Add(new StatModifier{StatType=CharacterStatType.CritDamage,Value=.25f});
            var criticalBonus=.25f.ToString("0.00");
            Assert.That(UIWorkspaceTooltipView.BuildEquipmentDetails(item,null),Does.Contain("Maksymalne zdrowie: +50").And.Contain("+32%").And.Contain("Obrażenia krytyczne: +"+criticalBonus).And.Not.Contain("×"+criticalBonus));
        }
        [TestCase("class.mage", "Regeneracja many", "Maksymalna mana")]
        [TestCase("class.warrior", "Regeneracja furii", "Maksymalna furia")]
        public void ResourceLabelsFollowEquipmentClass(string classId, string regeneration, string maximum)
        {
            item.RequiredClassId=classId;
            item.StatModifiers.Add(new StatModifier{StatType=CharacterStatType.ResourceRegeneration,Value=2});
            item.StatModifiers.Add(new StatModifier{StatType=CharacterStatType.MaxResource,Value=15});
            Assert.That(UIWorkspaceTooltipView.BuildEquipmentDetails(item,owner),Does.Contain(regeneration+": +2").And.Contain(maximum+": +15"));
        }
        [TestCase(.2f, .2f)]
        [TestCase(-.2f, -.2f)]
        [TestCase(-.1f, .2f)]
        public void PartTooltipSeparatesBaseAndPercentStatsAndPreservesAttackModifiers(float minimum, float maximum)
        {
            var part=ScriptableObject.CreateInstance<EquipmentPartDefinition>();
            try
            {
                part.Stats.CritChanceBase=.05f;
                part.Stats.MinAttackPercent=minimum;
                part.Stats.MaxAttackPercent=maximum;
                part.Stats.CritDamagePercent=.2f;
                var raw=(string)typeof(BlacksmithCraftingWindowView).GetMethod("BuildPartTooltip",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{part});
                var text=System.Text.RegularExpressions.Regex.Replace(raw,"<[^>]+>","");
                Assert.That(text,Does.Contain("Statystyki bazowe").And.Contain("Szansa krytyczna: +5 pp").And.Contain("Zmiany procentowe").And.Contain("Obrażenia krytyczne: +20%").And.Not.Contain("↑").And.Not.Contain("↓"));
                if(minimum==maximum) Assert.That(text,Does.Contain($"Atak: {minimum*100f:+0.##;-0.##;0}%").And.Not.Contain("Minimalny atak").And.Not.Contain("Maksymalny atak"));
                else Assert.That(text,Does.Contain("Minimalny atak: -10%").And.Contain("Maksymalny atak: +20%"));
            }
            finally { Object.DestroyImmediate(part); }
        }
        [Test] public void PartMaterialMovesOutOfTheLongTitleWithoutLosingItsName()
        {
            var part=ScriptableObject.CreateInstance<EquipmentPartDefinition>();
            try
            {
                typeof(EquipmentPartDefinition).GetField("<DisplayName>k__BackingField",Hidden).SetValue(part,"Młot — Owicie (Wyprawiona skóra kozy)");
                var flags=BindingFlags.Static|BindingFlags.NonPublic;
                var title=(string)typeof(BlacksmithCraftingWindowView).GetMethod("GetPartTooltipTitle",flags).Invoke(null,new object[]{part});
                var body=(string)typeof(BlacksmithCraftingWindowView).GetMethod("BuildPartTooltip",flags).Invoke(null,new object[]{part});
                Assert.That(title,Is.EqualTo("Młot — Owicie"));
                Assert.That(body,Does.Contain("Wyprawiona skóra kozy"));
            }
            finally { Object.DestroyImmediate(part); }
        }
        [Test] public void DifferentRequiredLevelAndPositiveUpgradeRemainVisible()
        {
            item.ItemLevel=15;item.RequiredLevel=12;item.UpgradeProgress=.25f;
            Assert.That(UIWorkspaceTooltipView.BuildEquipmentDetails(item,null),Does.Contain("Poziom przedmiotu: 15").And.Contain("Wymagany poziom: 12").And.Contain("Ulepszenie: 25%"));
        }
        [Test] public void ZeroCapacityStillShowsSocketedItemsAsInactive()
        {
            item.SocketedGaldurytes.Add(new GalduryteItemInstance{CustomName="Ocalony galduryt",IsIdentified=true,Effects=new(){new GalduryteEffectInstance{EffectType=GalduryteEffectType.BleedOnAttack,Value=.05f}}});
            Assert.That(UIWorkspaceTooltipView.BuildEquipmentDetails(item,null),Does.Contain("Galduryty: 1/0").And.Contain("nieaktywne gniazdo").And.Contain("Ocalony galduryt"));
        }
        [Test] public void FlavorHasNoGlossaryLinksAndMechanicalLinksAreUnderlined()
        {
            var glossary=ScriptableObject.CreateInstance<CombatTooltipGlossary>();
            typeof(CombatTooltipGlossary).GetField("entries",Hidden).SetValue(glossary,new List<CombatTooltipGlossary.Entry>{new(){Id="move",Title="ruchu"},new(){Id="dodge",Title="Unik"}});
            var node=new GameObject("Tooltip formatter");var tooltip=node.AddComponent<UIWorkspaceTooltipView>();
            typeof(UIWorkspaceTooltipView).GetField("glossary",Hidden).SetValue(tooltip,glossary);
            var body=(string)typeof(UIWorkspaceTooltipView).GetMethod("LinkItemMechanics",Hidden).Invoke(tooltip,new object[]{item,UIWorkspaceTooltipView.BuildEquipmentDetails(item,null)});
            Assert.That(body,Does.Not.Contain("<link=\"move\"").And.Contain("<link=\"dodge\"><color=#7EDBFF><u>Unik</u></color></link>"));
            Object.DestroyImmediate(node);Object.DestroyImmediate(glossary);
        }
        [Test] public void NativeTooltipKeepsLongItemBodyInsideItsAuthoredInsets()
        {
            var canvasNode=new GameObject("Tooltip canvas",typeof(RectTransform),typeof(Canvas));
            canvasNode.GetComponent<RectTransform>().sizeDelta=new Vector2(1920,1080);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_Tooltip.prefab");
            var node=Object.Instantiate(prefab,canvasNode.transform);var tooltip=node.GetComponent<UIWorkspaceTooltipView>();
            try
            {
                typeof(UIWorkspaceTooltipView).GetMethod("Awake",Hidden).Invoke(tooltip,null);
                var body=UIWorkspaceTooltipView.BuildEquipmentDetails(item,owner)+"\nOdporność na krwawienie i truciznę.\nOpis kolejnego efektu specjalnego przedmiotu.";
                typeof(UIWorkspaceTooltipView).GetMethod("SetContentCore",Hidden).Invoke(tooltip,new object[]{null,item.DisplayName,body,UIWorkspaceTooltipView.BuildItemMeta(item),true,false,(RarityTier?)item.CurrentRarity});
                for(var i=0;i<3;i++)
                {
                    Canvas.ForceUpdateCanvases();typeof(UIWorkspaceTooltipView).GetMethod("ResizeForContent",Hidden).Invoke(tooltip,null);
                    foreach(var rect in node.GetComponentsInChildren<RectTransform>())LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                }
                var text=(TMP_Text)typeof(UIWorkspaceTooltipView).GetField("bodyText",Hidden).GetValue(tooltip);text.ForceMeshUpdate(true,true);
                Assert.That(text.rectTransform.rect.height,Is.GreaterThanOrEqualTo(text.preferredHeight-.01f));
                Assert.That(text.isTextOverflowing,Is.False);
            }
            finally { Object.DestroyImmediate(canvasNode); }
        }
        [Test] public void GalduryteHoverUsesBulletsAndOmitsServiceInstructionsWithoutRevealingHiddenEffects()
        {
            var galduryte=new GalduryteItemInstance{Definition=definition,IsIdentified=true,ItemLevel=11,CurrentRarity=RarityTier.Rare,TargetType=GalduryteTargetType.Weapon,Effects=new(){new GalduryteEffectInstance{EffectType=GalduryteEffectType.BleedOnAttack,Value=.05f}}};
            var node=new GameObject("Galduryte tooltip");var tooltip=node.AddComponent<UIWorkspaceTooltipView>();
            try
            {
                var body=(string)typeof(UIWorkspaceTooltipView).GetMethod("BuildItemBody",Hidden).Invoke(tooltip,new object[]{galduryte,owner});
                Assert.That(body,Does.Contain("Efekty po osadzeniu").And.Contain("• ").And.Contain("Krwawienie przy trafieniu").And.Contain("Typ: Galduryt broni").And.Contain("Poziom galdurytu: 11").And.Contain("Rzadki"));
                Assert.That(body,Does.Not.Contain("Zaklinacz").And.Not.Contain("Usługi").And.Not.Contain("wolne gniazdo").And.Not.Contain("Wyposażenie:"));
                Assert.That(body.IndexOf("Krwawienie przy trafieniu"),Is.LessThan(body.IndexOf(ItemDetailsDividerView.MarkerId)));
                galduryte.IsIdentified=false;
                body=(string)typeof(UIWorkspaceTooltipView).GetMethod("BuildItemBody",Hidden).Invoke(tooltip,new object[]{galduryte,owner});
                Assert.That(body,Does.Contain("Niezidentyfikowany").And.Not.Contain("galduryte-effect:").And.Not.Contain("Krwawienie"));
                galduryte.IsIdentified=true;galduryte.IsDestroyed=true;
                body=(string)typeof(UIWorkspaceTooltipView).GetMethod("BuildItemBody",Hidden).Invoke(tooltip,new object[]{galduryte,owner});
                Assert.That(body,Does.Contain("Zniszczony — nie daje efektów").And.Not.Contain("galduryte-effect:").And.Not.Contain("Krwawienie"));
            }
            finally{Object.DestroyImmediate(node);}
        }
        [Test] public void CompactEffectLinksKeepExactRuntimeValuesAndFullRulesInNestedTooltips()
        {
            var galduryte=new GalduryteItemInstance{Definition=definition,IsIdentified=true,Effects=new(){
                new GalduryteEffectInstance{EffectType=GalduryteEffectType.PhysicalDamagePercent,Value=.06f},
                null,
                new GalduryteEffectInstance{EffectType=GalduryteEffectType.ItemDropChance,Value=.06f},
                new GalduryteEffectInstance{EffectType=GalduryteEffectType.PoisonOnAttack,Value=.02f}}};
            var node=new GameObject("Compact effects");var tooltip=node.AddComponent<UIWorkspaceTooltipView>();
            var previous=System.Globalization.CultureInfo.CurrentCulture;
            try
            {
                System.Globalization.CultureInfo.CurrentCulture=new System.Globalization.CultureInfo("pl-PL");
                var body=(string)typeof(UIWorkspaceTooltipView).GetMethod("BuildItemBody",Hidden).Invoke(tooltip,new object[]{galduryte,owner});
                Assert.That(body.Split('•').Length-1,Is.EqualTo(3));
                Assert.That(body,Does.Contain("Obrażenia fizyczne: +6%").And.Contain("Szansa znalezienia przedmiotu").And.Contain("Zatrucie przy trafieniu").And.Not.Contain("średnia premia").And.Not.Contain("raz na użycie"));
                var resolver=typeof(UIWorkspaceTooltipView).GetMethod("TryGetGlossaryEntry",Hidden);
                var links=System.Text.RegularExpressions.Regex.Matches(body,"<link=\"([^\"]+)\">");
                Assert.That(links.Count,Is.EqualTo(3)); // includes the noninteractive divider marker
                foreach(System.Text.RegularExpressions.Match link in links)
                {
                    if(link.Groups[1].Value==ItemDetailsDividerView.MarkerId)continue;
                    var args=new object[]{link.Groups[1].Value,null};
                    Assert.That((bool)resolver.Invoke(tooltip,args),Is.True);
                    var entry=(CombatTooltipGlossary.Entry)args[1];
                    if(entry.Id.Contains("ItemDropChance"))Assert.That(entry.Description,Does.Contain("+6%").And.Contain("średnia premia aktywnej drużyny"));
                    else Assert.That(entry.Description,Does.Contain("2%").And.Contain("3 tury").And.Contain("60%").And.Contain("raz na użycie umiejętności").And.Contain("wszystkich trafionych wrogów"));
                }
                Assert.That((bool)resolver.Invoke(tooltip,new object[]{"galduryte-effect:PoisonOnAttack:NaN",null}),Is.False);
                Assert.That((bool)resolver.Invoke(tooltip,new object[]{"galduryte-effect:9999:0.02",null}),Is.False);
                Assert.That(galduryte.GetEffectDetails(),Does.Contain("średnia premia aktywnej drużyny").And.Contain("raz na użycie umiejętności"));
            }
            finally{System.Globalization.CultureInfo.CurrentCulture=previous;Object.DestroyImmediate(node);}
        }
        [Test] public void ExtractionAndSocketingUseTheSameSummaryWithoutChangingPayload()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_NpcServicesWindow.prefab");
            var view=prefab.GetComponentInChildren<GalduryteExtractionWindowView>(true);
            var template=(GalduryteSocketRowView)typeof(GalduryteExtractionWindowView).GetField("socketRowTemplate",Hidden).GetValue(view);
            var row=Object.Instantiate(template);
            var stone=new GalduryteItemInstance{IsIdentified=true,CustomName="Kamień łupów",Effects=new()
            {
                new GalduryteEffectInstance{EffectType=GalduryteEffectType.ItemDropChance,Value=.03f},
                new GalduryteEffectInstance{EffectType=GalduryteEffectType.HealthPerTurn,Value=.0052f}
            }};
            var before=stone.GetEffectSummary();
            try
            {
                var title=(TMP_Text)typeof(GalduryteSocketRowView).GetField("titleText",Hidden).GetValue(row);
                var effects=(TMP_Text)typeof(GalduryteSocketRowView).GetField("effectText",Hidden).GetValue(row);
                row.BindForSocketing(stone,2);
                Assert.That(title.text,Does.Contain("Gniazdo 2"));
                Assert.That(effects.text,Does.Contain("• Szansa znalezienia przedmiotu: +3%").And.Not.Contain("średnia premia aktywnej drużyny"));
                Assert.That(effects.text,Does.Contain("Leczenie na początku tury: +"+GalduryteRuntimeText.FormatPercent(.0052f)+" maks."));
                Assert.That(stone.GetEffectSummary(),Is.EqualTo(before));
                row.BindForSocketing(stone);
                Assert.That(effects.text,Is.EqualTo("• " + before.Replace("\n", "\n• ")));
                stone.IsIdentified=false;row.BindForSocketing(stone);
                Assert.That(title.text,Does.Not.Contain("Gniazdo"));
                Assert.That(effects.text,Is.EqualTo("Niezidentyfikowany"));
                stone.IsIdentified=true;stone.IsDestroyed=true;row.BindForSocketing(stone,2);
                Assert.That(effects.text,Does.Contain("Zniszczony").And.Not.Contain("Znajdowanie").And.Not.Contain("Zdrowie"));
            }
            finally{Object.DestroyImmediate(row.gameObject);}
        }

        [Test] public void SocketingRowNumbersInstalledSocketsAndCompactsRegenerationWithoutChangingPayload()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_NpcServicesWindow.prefab");
            var view=prefab.GetComponentInChildren<GalduryteSocketingWindowView>(true);
            var template=(GalduryteSocketRowView)typeof(GalduryteSocketingWindowView).GetField("socketRowTemplate",Hidden).GetValue(view);
            var row=Object.Instantiate(template);
            var stone=new GalduryteItemInstance{IsIdentified=true,CustomName="Kamień odnowy",Effects=new()
            {
                new GalduryteEffectInstance{EffectType=GalduryteEffectType.HealthPerTurn,Value=.0052f},
                new GalduryteEffectInstance{EffectType=GalduryteEffectType.ResourcePerTurn,Value=.02f}
            }};
            var before=stone.GetEffectSummary();
            try
            {
                row.BindForSocketing(stone,3);
                var title=(TMP_Text)typeof(GalduryteSocketRowView).GetField("titleText",Hidden).GetValue(row);
                var effects=(TMP_Text)typeof(GalduryteSocketRowView).GetField("effectText",Hidden).GetValue(row);
                Assert.That(title.text,Does.Contain("Gniazdo 3").And.Contain("Kamień odnowy"));
                Assert.That(effects.text,Does.Contain("Leczenie na początku tury: +"+GalduryteRuntimeText.FormatPercent(.0052f)+" maks.").And.Contain("Odzysk zasobu na początku tury: +2% maks.").And.Not.Contain("Na początku swojej tury"));
                Assert.That(stone.GetEffectSummary(),Is.EqualTo(before));
                row.SetInactive(true);Assert.That(title.text,Does.Contain("nieaktywne gniazdo"));
                row.BindForSocketing(stone);Assert.That(title.text,Does.Not.Contain("Gniazdo").And.Not.Contain("nieaktywne"));
                stone.IsIdentified=false;row.BindForSocketing(stone,4);
                Assert.That(effects.text,Is.EqualTo("Niezidentyfikowany"));
                stone.IsIdentified=true;stone.IsDestroyed=true;row.BindForSocketing(stone,4);
                Assert.That(effects.text,Does.Contain("Zniszczony — nie daje efektów").And.Not.Contain("Zdrowie"));
            }
            finally{Object.DestroyImmediate(row.gameObject);}
        }
        private static string Plain(string value) => System.Text.RegularExpressions.Regex.Replace(value ?? "", "<[^>]+>", "");

        [TestCaseSource(nameof(AllGalduryteEffects))]
        public void SameRolledEffectHasOneDescriptionAcrossEveryItemSurface(GalduryteEffectType type)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_NpcServicesWindow.prefab");
            var view=prefab.GetComponentInChildren<GalduryteExtractionWindowView>(true);
            var template=(GalduryteSocketRowView)typeof(GalduryteExtractionWindowView).GetField("socketRowTemplate",Hidden).GetValue(view);
            var row=Object.Instantiate(template);
            var node=new GameObject("Shared item formatter");var tooltip=node.AddComponent<UIWorkspaceTooltipView>();
            var stone=new GalduryteItemInstance{Definition=definition,IsIdentified=true,Effects=new(){
                new GalduryteEffectInstance{EffectType=type,Value=.026f},null}};
            var originalEffect=stone.Effects[0];
            try
            {
                var expected="• "+stone.GetEffectSummary();
                var text=(TMP_Text)typeof(GalduryteSocketRowView).GetField("effectText",Hidden).GetValue(row);
                row.Bind(stone,true);Assert.That(Plain(text.text),Is.EqualTo(expected),"Candidate preview");
                row.BindForSocketing(stone,2);Assert.That(Plain(text.text),Is.EqualTo(expected),"Installed/extraction preview");
                row.BindForExtraction(stone,true,null);Assert.That(Plain(text.text),Is.EqualTo(expected),"Legacy extraction row");
                var identification=(string)typeof(GalduryteIdentificationWindowView).GetMethod("GetRevealedEffects",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{stone});
                Assert.That(Plain(identification),Is.EqualTo(expected),"Identification result");
                var list=(string)typeof(UIWorkspaceTooltipView).GetMethod("BuildGalduryteEffectList",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{stone,true});
                Assert.That(Plain(list),Is.EqualTo(expected),"Item hover");
                item.SocketedGaldurytes.Add(stone);
                var hover=(string)typeof(UIWorkspaceTooltipView).GetMethod("BuildItemBody",Hidden).Invoke(tooltip,new object[]{item,owner});
                var pinned=UIWorkspaceTooltipView.BuildEquipmentDetails(item,owner);
                Assert.That(Plain(hover),Is.EqualTo(Plain(pinned)),"Equipment hover/pinned/comparison");
                Assert.That(Plain(pinned),Does.Contain(expected));
                Assert.That(GalduryteRuntimeText.FormatEffectRange(type,.026f,.026f),Is.EqualTo(stone.GetEffectSummary()),"Crafting range names and units");
                Assert.That(stone.Effects[0],Is.SameAs(originalEffect)); Assert.That(originalEffect.Value,Is.EqualTo(.026f)); Assert.That(stone.Effects[1],Is.Null);
                if(GalduryteRuntimeText.HasEffectDetails(type))
                {
                    var id="galduryte-effect:"+type+":"+.026f.ToString("R",System.Globalization.CultureInfo.InvariantCulture);
                    var args=new object[]{id,null};
                    Assert.That((bool)typeof(UIWorkspaceTooltipView).GetMethod("TryGetGlossaryEntry",Hidden).Invoke(tooltip,args),Is.True);
                    var entry=(CombatTooltipGlossary.Entry)args[1];
                    Assert.That(entry.Description,Does.Contain(GalduryteRuntimeText.FormatEffect(stone.Effects[0])));
                    if(GalduryteRuntimeText.IsAttackTrigger(type))Assert.That(entry.Description,Does.Contain(GalduryteRuntimeText.AttackTriggerRules));
                }
            }
            finally{Object.DestroyImmediate(row.gameObject);Object.DestroyImmediate(node);}
        }
        private static System.Array AllGalduryteEffects => System.Enum.GetValues(typeof(GalduryteEffectType));

        [TestCase(ConsumableUseType.Potion,ConsumableRuntimeEffectType.RestoreHealth,.15f,0)]
        [TestCase(ConsumableUseType.Potion,ConsumableRuntimeEffectType.RegenHealth,.15f,3)]
        [TestCase(ConsumableUseType.Potion,ConsumableRuntimeEffectType.ModifyDefense,.1f,8)]
        [TestCase(ConsumableUseType.Potion,ConsumableRuntimeEffectType.ModifyDamage,.1f,8)]
        [TestCase(ConsumableUseType.Bomb,ConsumableRuntimeEffectType.DealPhysicalDamage,1f,0)]
        [TestCase(ConsumableUseType.Bomb,ConsumableRuntimeEffectType.ApplyPoison,.6f,3)]
        public void AlchemyIngredientItemTooltipAndCraftRefillSummaryUseOneDescription(
            ConsumableUseType useType,ConsumableRuntimeEffectType type,float value,int duration)
        {
            var consumableDefinition=ScriptableObject.CreateInstance<ConsumableItemDefinition>();
            typeof(ConsumableItemDefinition).GetField("<UseType>k__BackingField",Hidden).SetValue(consumableDefinition,useType);
            Field(consumableDefinition,"Id","consistency.test");Field(consumableDefinition,"DisplayName","Przedmiot testowy");
            var ingredient=new ConsumableEffectDefinition();
            var runtime=new ConsumableEffectRuntime{EffectType=type,Value=value,DurationTurns=duration};
            var consumable=new ConsumableItemInstance(consumableDefinition){Definition=consumableDefinition,RuntimeEffects=new(){runtime}};
            foreach(var pair in new[]{("EffectType",(object)type),("Value",(object)value),("DurationTurns",(object)duration)})
                typeof(ConsumableEffectDefinition).GetField("<"+pair.Item1+">k__BackingField",Hidden).SetValue(ingredient,pair.Item2);
            var node=new GameObject("Alchemy item formatter");var tooltip=node.AddComponent<UIWorkspaceTooltipView>();
            try
            {
                var summary=useType==ConsumableUseType.Bomb ? UIWorkspaceTooltipView.BuildBombEffectSummary(consumable)
                    : UIWorkspaceTooltipView.BuildPotionEffectSummary(consumable);
                Assert.That(summary,Is.Not.Null.And.Not.Empty);
                Assert.That(UIWorkspaceTooltipView.BuildAlchemyIngredientEffects(ingredient,consumableDefinition),Is.EqualTo(summary));
                var body=(string)typeof(UIWorkspaceTooltipView).GetMethod("BuildItemBody",Hidden).Invoke(tooltip,new object[]{consumable,null});
                Assert.That(Plain(body),Does.Contain(summary));
            }
            finally{Object.DestroyImmediate(node);Object.DestroyImmediate(consumableDefinition);}
        }

        [Test] public void JewelryGemDescriptionIsSharedByIngredientCraftingAndEquipmentTooltip()
        {
            var ring=AssetDatabase.LoadAssetAtPath<JewelryItemDefinition>("Assets/_Project/ScriptableObjects/Equipment/Jewelry/EID_Ring.asset");
            foreach(var gem in ring.Gems)
            {
                var jewelry=new Godmist.Systems.Items.JewelryGenerationService().GenerateSelected(ring,ring.Metals[0],gem);
                var ingredient=UIWorkspaceTooltipView.BuildJewelryIngredientEffects(gem.Modifiers).Trim();
                if(string.IsNullOrEmpty(ingredient))continue;
                Assert.That(UIWorkspaceTooltipView.BuildJewelryCraftingSummary(jewelry),Does.Contain(ingredient));
                Assert.That(Plain(UIWorkspaceTooltipView.BuildEquipmentDetails(jewelry,null)),Does.Contain(ingredient));
            }
        }

        [Test] public void EveryConfiguredAlchemyIngredientHasAReadableSharedSummary()
        {
            var count=0;
            foreach(var guid in AssetDatabase.FindAssets("t:AlchemistRoleData"))
            {
                var role=AssetDatabase.LoadAssetAtPath<Godmist.Data.NPC.AlchemistRoleData>(AssetDatabase.GUIDToAssetPath(guid));
                foreach(var option in role.IngredientOptions)
                foreach(var useType in new[]{ConsumableUseType.Potion,ConsumableUseType.Bomb})
                {
                    var effect=option.GetEffect(useType);
                    if(effect==null)continue;
                    var baseDefinition=useType==ConsumableUseType.Bomb ? role.BombBaseDefinition : role.PotionBaseDefinition;
                    var summary=UIWorkspaceTooltipView.BuildAlchemyIngredientEffects(effect,baseDefinition);
                    Assert.That(summary,Is.Not.Null.And.Not.Empty,option.Material.DisplayName+" "+useType);
                    Assert.That(summary,Does.Not.Contain(effect.EffectType.ToString()),"Internal enum names must not reach players");
                    count++;
                }
            }
            Assert.That(count,Is.GreaterThan(20),"Real alchemist catalog was verified");
        }

        [Test] public void NestedEquipmentEffectTooltipsRenderAboveEarlierLevelsAndReuseTheirOrder()
        {
            var canvasNode=new GameObject("Nested tooltip canvas",typeof(RectTransform),typeof(Canvas));
            canvasNode.GetComponent<RectTransform>().sizeDelta=new Vector2(1920,1080);
            var node=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_Tooltip.prefab"),canvasNode.transform);
            var tooltip=node.GetComponent<UIWorkspaceTooltipView>();
            try
            {
                typeof(UIWorkspaceTooltipView).GetMethod("Awake",Hidden).Invoke(tooltip,null);
                tooltip.ShowItem(item, owner, new object());
                ((System.Action)typeof(UIWorkspaceTooltipView).GetField("pendingShow",Hidden).GetValue(tooltip))();
                var rootBorder = tooltip.GetComponentInChildren<ItemRarityBorderGraphic>(true);
                Assert.That(rootBorder.enabled, Is.True);
                var rootOrder=node.GetComponent<Canvas>().sortingOrder;
                var create=typeof(UIWorkspaceTooltipView).GetMethod("CreateNestedTooltip",Hidden,null,new[]{typeof(CombatTooltipGlossary.Entry)},null);
                var entry=new CombatTooltipGlossary.Entry{Id="test",Title="Objaśnienie",Description="Pełne zasady efektu."};
                create.Invoke(tooltip,new object[]{entry});create.Invoke(tooltip,new object[]{entry});
                var nested=(List<UIWorkspaceTooltipView>)typeof(UIWorkspaceTooltipView).GetField("nested",Hidden).GetValue(tooltip);
                Assert.That(nested[0].GetComponent<Canvas>().sortingOrder,Is.EqualTo(rootOrder+1));
                Assert.That(nested[1].GetComponent<Canvas>().sortingOrder,Is.EqualTo(rootOrder+2));
                foreach (var child in nested)
                {
                    var accents = child.GetComponentsInChildren<ItemRarityBorderGraphic>(true);
                    Assert.That(accents, Has.Length.EqualTo(1));
                    Assert.That(accents[0].enabled, Is.False, "Glossary clones must clear inherited item rarity.");
                }
                Assert.That(rootBorder.enabled, Is.True);
                // EditMode cannot run the runtime's deferred Destroy. Remove the
                // closed level synchronously before opening another at that depth.
                var closed=nested[1];nested.RemoveAt(1);Object.DestroyImmediate(closed.gameObject);
                create.Invoke(tooltip,new object[]{entry});
                Assert.That(nested[1].GetComponent<Canvas>().sortingOrder,Is.EqualTo(rootOrder+2));
                Assert.That(node.GetComponent<Canvas>().sortingOrder,Is.EqualTo(rootOrder));
            }
            finally
            {
                ((List<UIWorkspaceTooltipView>)typeof(UIWorkspaceTooltipView).GetField("nested",Hidden).GetValue(tooltip)).Clear();
                Object.DestroyImmediate(canvasNode);
            }
        }
        [Test] public void FiveSocketEquipmentHoverKeepsEveryBonusAndMovesRepeatedRulesToEffectLinks()
        {
            item.UpgradeProgress=1f;
            for(var i=0;i<5;i++)item.SocketedGaldurytes.Add(new GalduryteItemInstance
            {
                CustomName="Kamień "+(i+1),IsIdentified=true,Effects=new()
                {
                    new GalduryteEffectInstance{EffectType=GalduryteEffectType.GeneralDamagePercent,Value=.026f+i*.003f},
                    new GalduryteEffectInstance{EffectType=GalduryteEffectType.ItemDropChance,Value=.03f+i*.003f},
                    new GalduryteEffectInstance{EffectType=GalduryteEffectType.PoisonOnAttack,Value=.02f}
                }
            });
            var node=new GameObject("Five socket tooltip");var tooltip=node.AddComponent<UIWorkspaceTooltipView>();
            try
            {
                var body=(string)typeof(UIWorkspaceTooltipView).GetMethod("BuildItemBody",Hidden).Invoke(tooltip,new object[]{item,null});
                Assert.That(body,Does.Contain("Galduryty: 5/5").And.Not.Contain("średnia premia").And.Not.Contain("raz na użycie"));
                Assert.That(body.Split('•').Length-1,Is.EqualTo(16)); // 15 bonuses and the quality separator
                for(var i=0;i<5;i++)
                {
                    Assert.That(body,Does.Contain("<b>Kamień "+(i+1)+"</b>").And.Contain(GalduryteRuntimeText.FormatEffectSummary(item.SocketedGaldurytes[i].Effects[0])));
                    var id="galduryte-effect:ItemDropChance:"+item.SocketedGaldurytes[i].Effects[1].Value.ToString("R",System.Globalization.CultureInfo.InvariantCulture);
                    Assert.That(body,Does.Contain(id));
                    var args=new object[]{id,null};
                    Assert.That((bool)typeof(UIWorkspaceTooltipView).GetMethod("TryGetGlossaryEntry",Hidden).Invoke(tooltip,args),Is.True);
                    Assert.That(((CombatTooltipGlossary.Entry)args[1]).Description,Does.Contain("średnia premia aktywnej drużyny"));
                }
                var pinned=UIWorkspaceTooltipView.BuildEquipmentDetails(item,null);
                Assert.That(Plain(pinned),Is.EqualTo(Plain(body)));
                Assert.That(pinned,Does.Not.Contain("galduryte-effect:"));
                item.UpgradeProgress=.2f;
                item.SocketedGaldurytes[1].IsIdentified=false;
                item.SocketedGaldurytes[2].IsDestroyed=true;
                body=(string)typeof(UIWorkspaceTooltipView).GetMethod("BuildItemBody",Hidden).Invoke(tooltip,new object[]{item,null});
                Assert.That(body,Does.Contain("Galduryty: 5/1").And.Not.Contain("<b>Kamień 2</b>").And.Contain("<b>Kamień 3</b> — nieaktywne gniazdo").And.Contain("Zniszczony — nie daje efektów").And.Contain("<b>Kamień 4</b> — nieaktywne gniazdo"));
            }
            finally{Object.DestroyImmediate(node);}
        }
        [Test] public void LongGalduryteFlavorWrapsWithinCompactTooltipWidth()
        {
            Field(definition,"Description",new string('W',120));
            var galduryte=new GalduryteItemInstance{Definition=definition,IsIdentified=true,CurrentRarity=RarityTier.Mythic};
            var canvasNode=new GameObject("Compact tooltip canvas",typeof(RectTransform),typeof(Canvas));
            canvasNode.GetComponent<RectTransform>().sizeDelta=new Vector2(1920,1080);
            var node=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_Tooltip.prefab"),canvasNode.transform);
            var tooltip=node.GetComponent<UIWorkspaceTooltipView>();
            try
            {
                typeof(UIWorkspaceTooltipView).GetMethod("Awake",Hidden).Invoke(tooltip,null);
                var body=(string)typeof(UIWorkspaceTooltipView).GetMethod("BuildItemBody",Hidden).Invoke(tooltip,new object[]{galduryte,owner});
                typeof(UIWorkspaceTooltipView).GetMethod("SetContentCore",Hidden).Invoke(tooltip,new object[]{null,ItemRarityPresentation.Name(galduryte),body,UIWorkspaceTooltipView.BuildItemMeta(galduryte),true,false,(RarityTier?)galduryte.CurrentRarity});
                for(var i=0;i<3;i++){Canvas.ForceUpdateCanvases();typeof(UIWorkspaceTooltipView).GetMethod("ResizeForContent",Hidden).Invoke(tooltip,null);}
                Assert.That(((RectTransform)node.transform).rect.width,Is.LessThanOrEqualTo(440.01f));
                var title=(TMP_Text)typeof(UIWorkspaceTooltipView).GetField("titleText",Hidden).GetValue(tooltip);title.ForceMeshUpdate(true,true);
                Assert.That(title.textInfo.characterInfo[0].color,Is.EqualTo((Color32)ItemRarityPresentation.Color(RarityTier.Mythic)));
                var text=(TMP_Text)typeof(UIWorkspaceTooltipView).GetField("bodyText",Hidden).GetValue(tooltip);text.ForceMeshUpdate(true,true);
                Assert.That(text.isTextOverflowing,Is.False);
            }
            finally{Object.DestroyImmediate(canvasNode);}
        }
        [TestCase(1)] [TestCase(2)] [TestCase(6)]
        public void RuneCompositionsStayPixelSharpAndReuseTheirCachedSprite(int count)
        {
            var library=Resources.Load<ItemIconLibrary>(ItemIconPresentation.LibraryResourcePath);
            var galduryte=new GalduryteItemInstance{Definition=definition,IsIdentified=true};
            for(var i=0;i<count;i++)galduryte.Effects.Add(new GalduryteEffectInstance{PowderColor=(GaldurytePowderColor)i});
            var key=ItemIconPresentation.BuildGalduryteKey(galduryte);
            var sprite=library.Find(key);
            Assert.That(sprite,Is.Not.Null);
            Assert.That(sprite.texture.filterMode,Is.EqualTo(FilterMode.Point));
            Assert.That(sprite.rect.size,Is.EqualTo(new Vector2(32,32)));
            Assert.That(library.Find(key),Is.SameAs(sprite));
            galduryte.IsIdentified=false;Assert.That(ItemIconPresentation.BuildGalduryteKey(galduryte),Is.Empty);
            galduryte.IsIdentified=true;galduryte.IsDestroyed=true;Assert.That(ItemIconPresentation.BuildGalduryteKey(galduryte),Is.Empty);
        }


        [Test]
        public void DestroyedNeverActivatedTooltipIsNotAnActiveHoverTarget()
        {
            var root = new GameObject("inactive tooltip lifecycle");
            root.SetActive(false);
            var tooltip = root.AddComponent<UIWorkspaceTooltipView>();
            typeof(UIWorkspaceTooltipView).GetMethod("OnEnable", Hidden).Invoke(tooltip, null);
            Assert.That(UIWorkspaceTooltipView.ActiveInstance, Is.SameAs(tooltip));
            Object.DestroyImmediate(root);
            Assert.That(ReferenceEquals(UIWorkspaceTooltipView.ActiveInstance, null), Is.True);
        }

        [TestCase("stash")] [TestCase("loot")] [TestCase("trade")]
        [TestCase("equipment")] [TestCase("service")] [TestCase("consumable")]
        public void ItemRarityBordersSurviveInteractionAndResetWhenRebound(string context)
        {
            var node = new GameObject(context, typeof(RectTransform));
            node.SetActive(false);
            ((RectTransform)node.transform).sizeDelta = new Vector2(72,72);
            var iconNode = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconNode.transform.SetParent(node.transform, false);
            var icon = iconNode.GetComponent<Image>();
            var selection = new GameObject("Selection", typeof(RectTransform), typeof(Image));
            selection.transform.SetParent(node.transform, false);
            var selectedFrame = selection.GetComponent<Image>();
            selectedFrame.enabled = true;
            var sample = new EquipmentItemInstance { Definition = definition, CurrentRarity = RarityTier.Legendary };
            var consumableDefinition = ScriptableObject.CreateInstance<ConsumableItemDefinition>();
            Field(consumableDefinition, "Id", "rarity.consumable");
            var consumable = new ConsumableItemInstance(consumableDefinition) { CurrentRarity = RarityTier.Legendary };
            System.Action<ItemInstance> bind;
            System.Action interact = () => {};
            switch (context)
            {
                case "stash":
                    var stash = node.AddComponent<CampaignStorageItemView>();
                    typeof(CampaignStorageItemView).GetField("icon", Hidden).SetValue(stash, icon);
                    bind = value => stash.Bind(null, value, 2, true); break;
                case "loot":
                    var loot = node.AddComponent<DungeonLootItemView>();
                    bind = value => loot.Bind(value, 2, null); break;
                case "trade":
                    var trade = node.AddComponent<TradeItemSlotView>();
                    bind = value => trade.Bind(null, value, 2, TradeItemSlotSource.TraderOffer, "", true); break;
                case "equipment":
                    var equipment = node.AddComponent<CharacterInfoEquipmentSlotView>();
                    typeof(CharacterInfoEquipmentSlotView).GetField("selectionBorder", Hidden).SetValue(equipment, selectedFrame);
                    bind = value => equipment.Bind(value as EquipmentItemInstance);
                    interact = () => { equipment.SetSelected(true); equipment.SetDropHighlight(true); }; break;
                case "service":
                    var service = node.AddComponent<BlacksmithCraftingSlotView>();
                    bind = value => service.SetItemTooltip(value, null);
                    interact = () => { service.SetSelected(true); service.SetSelectionInteractable(false); service.OnPointerEnter(null); service.OnPointerExit(null); }; break;
                default:
                    var combat = node.AddComponent<CombatSkillSlotView>();
                    typeof(CombatSkillSlotView).GetField("icon", Hidden).SetValue(combat, icon);
                    bind = value => combat.InitializeConsumable(value == null ? null : consumable, null, null, null);
                    interact = () => combat.RefreshAvailability(false, 10, false, "0", true); break;
            }
            try
            {
                bind(sample);
                var border = node.GetComponentInChildren<ItemRarityBorderGraphic>(true);
                Assert.That(border, Is.Not.Null);
                Assert.That(border.color, Is.EqualTo(ItemRarityPresentation.Color(RarityTier.Legendary)));
                Assert.That(border.raycastTarget, Is.False);
                Assert.That(border.maskable, Is.True);
                Assert.That(border.GetComponent<CanvasRenderer>(), Is.Not.Null);
                Assert.That(border.GetComponent<LayoutElement>().ignoreLayout, Is.True);
                Assert.That(border.rectTransform.offsetMin, Is.EqualTo(new Vector2(4,4)));
                interact();
                Assert.That(border.enabled, Is.True);
                Assert.That(border.color, Is.EqualTo(ItemRarityPresentation.Color(RarityTier.Legendary)));
                Assert.That(selectedFrame.enabled, Is.True);
                bind(null);
                Assert.That(border.enabled, Is.False);
                sample.CurrentRarity = consumable.CurrentRarity = RarityTier.Godly;
                bind(sample);
                Assert.That(node.GetComponentsInChildren<ItemRarityBorderGraphic>(true), Has.Length.EqualTo(1));
                Assert.That(border.enabled, Is.True);
                Assert.That(border.color, Is.EqualTo(ItemRarityPresentation.Color(RarityTier.Godly)));
                // A skill/category rebound must never inherit the previous item's rarity.
                if (context == "consumable") node.GetComponent<CombatSkillSlotView>().Initialize(null, null, null);
                if (context == "service") node.GetComponent<BlacksmithCraftingSlotView>().SetTooltip("Kategoria", "");
                if (context is "service" or "consumable") Assert.That(border.enabled, Is.False);
            }
            finally { Object.DestroyImmediate(node); Object.DestroyImmediate(consumableDefinition); }
        }

        [TestCase(false)] [TestCase(true)]
        public void ItemTooltipRarityBelongsToItsContentAndClearsForNonItems(bool nested)
        {
            var canvas = new GameObject("Rarity canvas", typeof(RectTransform), typeof(Canvas));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_Tooltip.prefab");
            var node = Object.Instantiate(prefab, canvas.transform);
            var tooltip = node.GetComponent<UIWorkspaceTooltipView>();
            try
            {
                typeof(UIWorkspaceTooltipView).GetMethod("Awake", Hidden).Invoke(tooltip, null);
                var content = tooltip;
                if (nested)
                {
                    content = Object.Instantiate(prefab, canvas.transform).GetComponent<UIWorkspaceTooltipView>();
                    typeof(UIWorkspaceTooltipView).GetMethod("Awake", Hidden).Invoke(content, null);
                    typeof(UIWorkspaceTooltipView).GetField("contentTargetOverride", Hidden).SetValue(tooltip, content);
                }
                item.CurrentRarity = RarityTier.Rare;
                tooltip.ShowItem(item, owner, new object());
                ((System.Action)typeof(UIWorkspaceTooltipView).GetField("pendingShow", Hidden).GetValue(tooltip))();
                var border = content.GetComponentInChildren<ItemRarityBorderGraphic>(true);
                Assert.That(border, Is.Not.Null);
                Assert.That(border.color, Is.EqualTo(ItemRarityPresentation.Color(RarityTier.Rare)));
                if (nested) Assert.That(tooltip.GetComponentInChildren<ItemRarityBorderGraphic>(true), Is.Null);
                typeof(UIWorkspaceTooltipView).GetMethod("SetContentCore", Hidden).Invoke(content,
                    new object[] { null, "Zasada", "Opis", "", false, true, null });
                Assert.That(border.enabled, Is.False);
                item.CurrentRarity = RarityTier.Mythic;
                content.ShowItemWithExtra(item, owner, new object(), "Cena: 10");
                ((System.Action)typeof(UIWorkspaceTooltipView).GetField("pendingShow", Hidden).GetValue(content))();
                Assert.That(border.enabled, Is.True);
                Assert.That(border.color, Is.EqualTo(ItemRarityPresentation.Color(RarityTier.Mythic)));
                Assert.That(content.GetComponentsInChildren<ItemRarityBorderGraphic>(true), Has.Length.EqualTo(1));
            }
            finally { Object.DestroyImmediate(canvas); }
        }

        [TestCase(RarityTier.Common)] [TestCase(RarityTier.Godly)]
        public void ItemRarityBorderMeshLeavesArtworkCentreTransparent(RarityTier rarity)
        {
            var frame = new GameObject("Frame", typeof(RectTransform));
            ((RectTransform)frame.transform).sizeDelta = new Vector2(72,72);
            ItemRarityBorderGraphic border = null;
            try
            {
                ItemRarityPresentation.ApplyBorder(ref border, (RectTransform)frame.transform, (RarityTier?)rarity);
                using var mesh = new VertexHelper();
                typeof(ItemRarityBorderGraphic).GetMethod("OnPopulateMesh", Hidden, null, new[]{typeof(VertexHelper)}, null).Invoke(border, new object[]{ mesh });
                Assert.That(mesh.currentVertCount, Is.GreaterThan(0));
                var vertex = new UIVertex();
                for (var i = 0; i < mesh.currentVertCount; i++)
                {
                    mesh.PopulateUIVertex(ref vertex, i);
                    Assert.That(Mathf.Abs(vertex.position.x) >= 12f || Mathf.Abs(vertex.position.y) >= 12f, Is.True,
                        "The accent must not paint over the centre of the item illustration.");
                }
                ItemRarityPresentation.ApplyBorder(ref border, (RectTransform)frame.transform, (RarityTier?)null);
                Assert.That(border.enabled, Is.False);
            }
            finally { Object.DestroyImmediate(frame); }
        }

        [Test]
        public void CloningItemFrameReusesItsAccentWithoutLeakingOldRarity()
        {
            var frame = new GameObject("Clone source", typeof(RectTransform));
            frame.layer = 5;
            ItemRarityBorderGraphic original = null, copied = null;
            GameObject clone = null;
            try
            {
                ItemRarityPresentation.ApplyBorder(ref original, (RectTransform)frame.transform, (RarityTier?)RarityTier.Rare);
                clone = Object.Instantiate(frame);
                ItemRarityPresentation.ApplyBorder(ref copied, (RectTransform)clone.transform, (RarityTier?)null);
                Assert.That(copied, Is.Not.Null);
                Assert.That(copied.enabled, Is.False);
                ItemRarityPresentation.ApplyBorder(ref copied, (RectTransform)clone.transform, (RarityTier?)RarityTier.Godly);
                Assert.That(copied.gameObject.layer, Is.EqualTo(5));
                Assert.That(copied.color, Is.EqualTo(ItemRarityPresentation.Color(RarityTier.Godly)));
                Assert.That(clone.GetComponentsInChildren<ItemRarityBorderGraphic>(true), Has.Length.EqualTo(1));
                Assert.That(original.enabled, Is.True);
                Assert.That(original.color, Is.EqualTo(ItemRarityPresentation.Color(RarityTier.Rare)));
            }
            finally { Object.DestroyImmediate(frame); if(clone!=null) Object.DestroyImmediate(clone); }
        }

        [Test]
        public void ItemRarityBorderActuallyRendersAndIsClippedByTheViewport()
        {
            var cameraNode = new GameObject("Rarity render camera", typeof(Camera));
            var camera = cameraNode.GetComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 64;
            camera.transform.position = new Vector3(0,0,-10);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.1f,.08f,.12f,1);
            camera.cullingMask = 1 << 31;
            var target = new RenderTexture(128,128,24);
            camera.targetTexture = target;
            var canvasNode = new GameObject("Rarity render canvas",typeof(RectTransform),typeof(Canvas));
            canvasNode.layer = 31;
            var canvas = canvasNode.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera; canvas.planeDistance = 1; canvas.pixelPerfect = true;
            var viewport = new GameObject("Viewport",typeof(RectTransform));
            viewport.layer = 31; viewport.transform.SetParent(canvasNode.transform,false);
            var viewportRect = (RectTransform)viewport.transform;
            viewportRect.anchorMin = viewportRect.anchorMax = new Vector2(.5f,.5f);
            viewportRect.sizeDelta = new Vector2(48,48);
            var frame = new GameObject("Item frame",typeof(RectTransform));
            frame.layer = 31;frame.transform.SetParent(viewport.transform,false);
            ((RectTransform)frame.transform).sizeDelta = new Vector2(72,72);
            ItemRarityBorderGraphic border = null;
            var image = new Texture2D(128,128,TextureFormat.RGBA32,false);
            var previous = RenderTexture.active;
            try
            {
                ItemRarityPresentation.ApplyBorder(ref border,(RectTransform)frame.transform,(RarityTier?)RarityTier.Rare);
                Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
                image.ReadPixels(new Rect(0,0,128,128),0,0);image.Apply();
                var edge=image.GetPixel(32,64);
                Assert.That(edge.b,Is.GreaterThan(edge.r*1.5f),"A rare item's blue outline must be visible in the render.");
                var centre=image.GetPixel(64,64);
                Assert.That(centre.b,Is.LessThan(centre.r*1.5f),"The accent must not tint the artwork area.");
                viewport.AddComponent<RectMask2D>();
                Canvas.ForceUpdateCanvases();camera.Render();
                image.ReadPixels(new Rect(0,0,128,128),0,0);image.Apply();
                var clipped=image.GetPixel(32,64);
                Assert.That(clipped.b,Is.LessThan(clipped.r*1.5f),"The frame must not bleed outside a scroll viewport.");
            }
            finally
            {
                RenderTexture.active=previous;
                camera.targetTexture=null;
                Object.DestroyImmediate(image);Object.DestroyImmediate(target);
                Object.DestroyImmediate(canvasNode);Object.DestroyImmediate(cameraNode);
            }
        }

    }
}

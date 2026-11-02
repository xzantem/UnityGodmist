using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Combat;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    /// <summary>Explicit read-only contact sheet export using the shipped tooltip prefab.</summary>
    public sealed class HeroStatusGalleryPreview
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        readonly List<Object> owned = new();
        readonly List<Card> cards = new();
        List<SkillData> skills;
        CharacterClassData previewClass;
        [Serializable] sealed class Card
        {
            public string group, scenario, file, title, body, meta, kind;
            [NonSerialized] public CombatStatusPresentation presentation;
        }
        [Serializable] sealed class Gallery { public Card[] cards; }
        T Own<T>(T value) where T : Object { owned.Add(value); return value; }
        static object Get(object value, string field) => value.GetType().GetField(field, Hidden).GetValue(value);
        static void Invoke(object value, string method, params object[] args) => value.GetType().GetMethod(method, Hidden).Invoke(value, args);
        BattleUnit Unit(string name = "Bohater")
        {
            var definition = Own(ScriptableObject.CreateInstance<CharacterDefinition>());
            var data = new SerializedObject(definition); data.FindProperty("<DisplayName>k__BackingField").stringValue = name; data.ApplyModifiedPropertiesWithoutUndo();
            return new BattleUnit { IsPlayerSide = true, Character = new CharacterInstance {
                CharacterId = Guid.NewGuid().ToString(), Definition = definition, ClassData = previewClass, Skills = new(skills),
                LifeState = CharacterLifeState.Alive, CurrentHealth = 800, ResourcePool = new ResourcePool(ResourceType.Rage, 150, 200, 0),
                Stats = new CharacterStats { MaxHealth = new Stat(1000), MinAttack = new Stat(100), MaxAttack = new Stat(100),
                    PhysicalDefense = new Stat(100), MagicalDefense = new Stat(100), CritChance = new Stat(.1f), CritDamage = new Stat(1.5f),
                    PhysicalDefensePenetration = new Stat(0), MagicalDefensePenetration = new Stat(0), MaxResource = new Stat(200) }
            }};
        }
        static void Rank(BattleUnit unit, string tree, string node, int rank)
        {
            var progress = unit.Character.SkillTreeProgress.FirstOrDefault(item => item.TreeId == tree);
            if (progress == null) { progress = new() { TreeId = tree }; unit.Character.SkillTreeProgress.Add(progress); }
            progress.NodeInvestments.Add(new() { NodeId = "node." + tree.Substring(5) + "." + node, Ranks = rank });
        }
        static PassiveEffectRuntime Effect(string id, string name = null, int turns = 3) => new() {
            EffectId = id, DisplayName = name ?? id, RemainingDuration = turns, Stacks = 1,
            Lifetime = turns > 0 ? PassiveEffectLifetime.Timed : PassiveEffectLifetime.CombatScoped
        };
        void Add(string group, string scenario, BattleUnit owner, params PassiveEffectRuntime[] effects)
        {
            foreach (var presentation in CombatStatusPresentationFactory.Create(effects, owner: owner))
                cards.Add(new Card { group = group, scenario = scenario, presentation = presentation });
        }
        void Stat(string group, string id, string scenario, CharacterStatType stat, float value, int turns = 3, StatModifierOperation operation = StatModifierOperation.FlatAbsolute)
        {
            var effect = Effect(id, turns: turns); effect.StatModifiers = new() { new() { StatType = stat, Operation = operation, Value = value } };
            Add(group, scenario, Unit(), effect);
        }
        void Variable(string group, string id, string scenario, float value)
        {
            var effect = Effect(id); effect.VariableModifiers = new() { new() { VariableType = GameplayVariableType.DamageDealtPercent, Value = value } };
            Add(group, scenario, Unit(), effect);
        }
        void Scenarios()
        {
            var commander = Unit("Reimond"); var target = Unit("Przeciwnik"); var ally = Unit("Sojusznik");
            commander.Position = new GridPosition(1, 2); target.Position = new GridPosition(2, 2); ally.Position = new GridPosition(2, 3); target.IsPlayerSide = false;
            var session = new CombatSession { Grid = new BattleGrid(8, 8), Units = new() { commander, target, ally } };
            Rank(commander, WarriorCombatSystem.Commander, "breakdown", 3); Rank(commander, WarriorCombatSystem.Commander, "marking", 2); Rank(commander, WarriorCombatSystem.Commander, "flanking", 2);
            target.WarriorState.Session = session; target.WarriorState.OrderSource = commander; target.WarriorState.OrderTurns = 3;
            Add("Wojownik", "Rozbicie 3, Wskazanie Słabości 2, Flankowanie 2; dwóch przyległych sojuszników", target);
            var protectedUnit = Unit(); protectedUnit.Position = target.Position; session.Units[1] = protectedUnit;
            protectedUnit.WarriorState.Session = session; protectedUnit.WarriorState.ProtectionTurns = 3; protectedUnit.WarriorState.Riposte = true;
            Add("Wojownik", "Riposta; dwóch przyległych sojuszników", protectedUnit);
            var warrior = Unit(); warrior.WarriorState.TraumaTurns = 5; warrior.WarriorState.TraumaBonus = .6f;
            Add("Wojownik", "Trauma 2", warrior);
            warrior = Unit(); warrior.WarriorState.FreeMovementTurns = 2; warrior.WarriorState.FreeStepsUsed = 1;
            Add("Wojownik", "Wykorzystano jeden z dwóch darmowych kroków", warrior);
            Stat("Wojownik", "warrior.amok", "Amok 3", CharacterStatType.MaxResource, 40, 5);
            Variable("Wojownik", "warrior.shortage", "Przykładowy niedobór furii: kara 20%", -.2f);
            var death = Unit(); var deathSkill = skills.Single(skill => skill.SkillId == WarriorCombatSystem.DeathWish);
            var dealt = Effect("skill.warrior.death_wish.modify_var.0", deathSkill.DisplayName, 0);
            var received = Effect("skill.warrior.death_wish.modify_var.1", deathSkill.DisplayName, 0);
            dealt.Sources.Add(PassiveEffectSourceFactory.FromSkill(death, deathSkill, 0)); received.Sources.Add(PassiveEffectSourceFactory.FromSkill(death, deathSkill, 1));
            dealt.VariableModifiers = new() { new() { VariableType = GameplayVariableType.DamageDealtPercent, Value = .75f } };
            received.VariableModifiers = new() { new() { VariableType = GameplayVariableType.DamageReceivedPercent, Value = .75f } };
            Add("Wojownik", "Desperacja 1; maksymalna furia 200", death, dealt, received);

            var scout = Unit(); scout.ScoutState.WeakPointTurns = 5; scout.ScoutState.WeakPointCritDamage = .35f;
            Add("Zwiadowca", "Wykorzystanie Słabości 3", scout);
            scout = Unit(); scout.ScoutState.AnalysisTurns = 3; scout.ScoutState.AnalysisPenetration = .3f; Add("Zwiadowca", "Dogłębna Analiza 2", scout);
            var angerSource = Unit("Zwiadowca"); Rank(angerSource, ScoutCombatSystem.BladeTree, "strong_insult", 2);
            scout = Unit(); scout.ScoutState.AngerBySource[angerSource] = 80; Add("Zwiadowca", "80 Złości, Silna Zniewaga 2", scout);
            scout = Unit(); scout.ScoutState.Knives.Add(new() { Owner = angerSource, BleedMultiplier = .9f }); scout.ScoutState.Knives.Add(new() { Owner = angerSource, BleedMultiplier = .9f });
            Add("Zwiadowca", "Dwa wbite sztylety, Głębokie Wbicie 2", scout);
            Stat("Zwiadowca", "scout.knee_throw", "Rzut w Kolano", CharacterStatType.Speed, -15);
            var tonic = skills.Single(skill => skill.SkillId == "skill.scout.tonic");
            var speed = Effect("skill.scout.tonic.modify_stat.0", tonic.DisplayName, 5); speed.Sources.Add(PassiveEffectSourceFactory.FromSkill(scout, tonic, 0));
            speed.StatModifiers = new() { new() { StatType = CharacterStatType.Speed, Operation = StatModifierOperation.FlatAbsolute, Value = 30 } }; Add("Zwiadowca", "Tonik z Siłą Naparu — przyspieszenie", Unit(), speed);
            speed = Effect("skill.scout.tonic.modify_stat.1", tonic.DisplayName, 3); speed.Sources.Add(PassiveEffectSourceFactory.FromSkill(scout, tonic, 1));
            speed.StatModifiers = new() { new() { StatType = CharacterStatType.Speed, Operation = StatModifierOperation.FlatAbsolute, Value = -15 } }; Add("Zwiadowca", "Tonik z Siłą Naparu — późniejsze spowolnienie", Unit(), speed);

            var caster = Unit("Paladyn"); var patient = Unit();
            var regen = Effect("paladin.holy_regeneration", "Święta Regeneracja", 3);
            regen.SetStateValue(PassiveEffectRuntimeStateValue.Integer("remaining", 101)); regen.SetStateValue(PassiveEffectRuntimeStateValue.Integer("total", 400)); regen.SetStateValue(PassiveEffectRuntimeStateValue.Integer("lasting", 2));
            Add("Paladyn", "101 HP do odnowienia, Trwała Regeneracja 2", patient, regen);
            regen = Effect("paladin.holy_regeneration", "Święta Regeneracja", 2); regen.SetStateValue(PassiveEffectRuntimeStateValue.Integer("remaining", 80)); regen.SetStateValue(PassiveEffectRuntimeStateValue.Boolean("tail", true));
            Add("Paladyn", "Aktywna końcowa faza Trwałej Regeneracji", Unit(), regen);
            Rank(caster, PaladinCombatSystem.Priest, "grace_collection", 2); Rank(caster, PaladinCombatSystem.Priest, "salvation", 1);
            patient = Unit(); patient.WarriorState.Session = new CombatSession { Units = new() { caster, patient } };
            var blessing = Effect("paladin.blessing", "Błogosławieństwo"); blessing.Sources.Add(PassiveEffectSourceFactory.FromRuntimeSystem(caster, blessing.EffectId));
            blessing.VariableModifiers = new() { new() { VariableType = GameplayVariableType.DamageDealtPercent, Value = .2f }, new() { VariableType = GameplayVariableType.DamageReceivedPercent, Value = -.2f } };
            blessing.SetStateValue(PassiveEffectRuntimeStateValue.Integer("grace", 5)); Add("Paladyn", "Silne Błogosławieństwo 2, Gromadzenie Łaski 2, Łaska Zwrotna; 5 ładunków", patient, blessing);
            var universal = Effect("paladin.universal", turns: 2); universal.StatModifiers = new() {
                new() { StatType = CharacterStatType.PhysicalDefense, Operation = StatModifierOperation.Percent, Value = .1f },
                new() { StatType = CharacterStatType.MagicalDefense, Operation = StatModifierOperation.Percent, Value = .1f },
                new() { StatType = CharacterStatType.Dodge, Operation = StatModifierOperation.Percent, Value = .1f } };
            Add("Paladyn", "Wszechstronna Łaska 3", Unit(), universal);
            var zealBase = Effect("paladin.zeal.base", turns: 2); var zealExtra = Effect("paladin.zeal.extra", turns: 1);
            foreach (var effect in new[] { zealBase, zealExtra }) { effect.Sources.Add(PassiveEffectSourceFactory.FromRuntimeSystem(caster, effect.EffectId)); effect.StatModifiers = new() { new() { StatType = CharacterStatType.Speed, Operation = StatModifierOperation.FlatAbsolute, Value = 5 } }; }
            Add("Paladyn", "Ożywcza Łaska — dwie składowe o różnych czasach", Unit(), zealBase, zealExtra);
            Variable("Paladyn", "paladin.anointed", "Namaszczony Wojownik 2", .2f);
            var torment = Effect("paladin.torment", turns: 0); torment.Stacks = 6; torment.StatModifiers = new() {
                new() { StatType = CharacterStatType.PhysicalDefense, Operation = StatModifierOperation.Percent, Value = -.12f },
                new() { StatType = CharacterStatType.MagicalDefense, Operation = StatModifierOperation.Percent, Value = -.12f },
                new() { StatType = CharacterStatType.Dodge, Operation = StatModifierOperation.FlatAbsolute, Value = -12 } };
            Add("Paladyn", "Sześć nałożeń Udręczenia", Unit(), torment);
            var exposure = Effect("paladin.exposure"); exposure.SetStateValue(PassiveEffectRuntimeStateValue.Number("strength", .5f)); Add("Paladyn", "Odsłonięcie Słabości 2", Unit(), exposure);
            Stat("Paladyn", "object.preview.paladin.slow", "Cel w obszarze Pola Osądu", CharacterStatType.Speed, -5, 9999);

            var mage = Unit(); Rank(mage, MageCombatSystem.Warlock, "superhuman_offering", 3); Rank(mage, MageCombatSystem.Warlock, "calm", 2);
            var offering = Effect("skill.mage.offering.unique.0", "Ofiara", 0); offering.SetPayload(new UniqueCombatBuffPayload { BuffType = UniqueBuffType.MageOffering }); offering.Sources.Add(PassiveEffectSourceFactory.FromSkill(mage, skills.Single(skill => skill.SkillId == "skill.mage.offering"), 0));
            Add("Mag", "Spotęgowana Ofiara 3, Kontrolowana Ofiara 2", mage, offering);
            Variable("Mag", "mage.sacrifice.gain.ally", "Przejęcie Mocy — zysk maga", .2f); Variable("Mag", "mage.sacrifice.loss.mage", "Przejęcie Mocy — osłabienie sojusznika", -.2f);
            mage = Unit(); var attacker = Unit("Przeciwnik A"); mage.MageState.Adaptation[attacker.Character.CharacterId] = new() { RemainingHits = 2, Reduction = 37 }; mage.WarriorState.Session = new CombatSession { Units = new() { mage, attacker } };
            Add("Mag", "Adaptacja Energetyczna — zostały dwa trafienia od tego napastnika", mage);
            mage = Unit(); mage.MageState.FireErosion["caster"] = 4;
            var erosion = Effect("mage.erosion.caster", turns: 0); erosion.SetPayload(new ResistanceModifierPayload { ResistanceType = ResistanceType.Fire, Value = -.4f }); Add("Mag", "Wypalanie Odporności 2, cztery nałożenia", mage, erosion);
            var tiring = Effect("mage.tiring"); tiring.Stacks = 3; tiring.StatModifiers = new() { new() { StatType = CharacterStatType.Speed, Operation = StatModifierOperation.FlatAbsolute, Value = -15 } }; Add("Mag", "Wycieńczające Zaklęcia — trzy nałożenia", Unit(), tiring);
            var focus = Effect("skill.mage.magic_focus.unique.0", "Skupienie Magii", 5); focus.SetPayload(new UniqueCombatBuffPayload { BuffType = UniqueBuffType.MageMaximumDamage }); focus.Sources.Add(PassiveEffectSourceFactory.FromSkill(mage, skills.Single(skill => skill.SkillId == "skill.mage.magic_focus"), 0)); Add("Mag", "Mistrzowska Koncentracja — wydłużony czas", Unit(), focus);
            var ritual = Effect("skill.mage.ritual.regeneration.0", "Rytuał Odnowy", 5); ritual.SetPayload(new PeriodicRecoveryPayload { HealthPercent = .06f }); ritual.Sources.Add(PassiveEffectSourceFactory.FromSkill(mage, skills.Single(skill => skill.SkillId == "skill.mage.ritual"), 0)); Add("Mag", "Rytuał Odnowy — aktywna regeneracja", Unit(), ritual);

            foreach (var id in new[] { "bleed", "poison", "burn", "stun", "immobilize", "invisibility", "taunt" })
            {
                var effect = Effect(id, "", 3);
                if (id is "bleed" or "poison" or "burn") effect.SetPayload(new DamageOverTimePayload { RemainingTotalDamage = 90, DamagePerTurn = 30 });
                Add("Statusy wspólne", "Przykładowy aktywny status; DoT: 30 obrażeń na turę", Unit(), effect);
            }
            foreach (var type in new[] { UniqueBuffType.GuaranteedCriticalHit, UniqueBuffType.GuaranteedDodge, UniqueBuffType.MaximumDamage })
            {
                var skill = skills.FirstOrDefault(item => !item.SkillId.StartsWith("skill.enemy.") && !item.SkillId.StartsWith("skill.test.")
                    && item.Effects.Any(part => part.EffectType == EffectType.ApplyUniqueBuff && part.UniqueBuffType == type));
                if (skill == null) continue;
                var owner = Unit(); var effect = Effect(skill.SkillId + ".preview.one_use", skill.DisplayName);
                effect.Sources.Add(PassiveEffectSourceFactory.FromSkill(owner, skill, 0));
                effect.SetPayload(new UniqueCombatBuffPayload { BuffType = type }); Add("Efekty jednorazowe", skill.DisplayName + " — jeden gotowy ładunek", owner, effect);
            }
        }

        [Test, Explicit("Exports representative hero statuses with the actual shipped tooltip prefab.")]
        public void Export()
        {
            Assert.That(EditorApplication.isPlayingOrWillChangePlaymode, Is.False);
            var output = Path.GetFullPath("../LocalHandoffs/hero-status-gallery-20261027"); Directory.CreateDirectory(output);
            var oldActive = UIWorkspaceTooltipView.ActiveInstance;
            var oldRender = RenderTexture.active;
            Camera renderCamera = null;
            try
            {
                skills = AssetDatabase.FindAssets("t:SkillData", new[] { "Assets/_Project/ScriptableObjects/Skills" }).Select(guid => AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(guid))).ToList();
                previewClass = Own(ScriptableObject.CreateInstance<CharacterClassData>());
                previewClass.SkillTrees.AddRange(AssetDatabase.FindAssets("t:SkillTreeDefinition", new[] { "Assets/_Project/ScriptableObjects/Skills/SkillTrees" }).Select(guid => AssetDatabase.LoadAssetAtPath<SkillTreeDefinition>(AssetDatabase.GUIDToAssetPath(guid))));
                Scenarios();
                var canvasRoot = Own(new GameObject("Hero status preview canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler)));
                canvasRoot.hideFlags = HideFlags.HideAndDontSave; var canvas = canvasRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
                var cameraRoot = Own(new GameObject("Hero status preview camera", typeof(Camera))); cameraRoot.hideFlags = HideFlags.HideAndDontSave;
                var camera = cameraRoot.GetComponent<Camera>(); renderCamera = camera; camera.orthographic = true; camera.transform.position = new Vector3(0, 0, -10); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.08f, .075f, .09f, 1); camera.cullingMask = 1 << 31;
                canvas.worldCamera = camera; canvas.planeDistance = 1; canvasRoot.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                var render = Own(new RenderTexture(512, 1200, 24)); camera.targetTexture = render;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_Tooltip.prefab"); Assert.That(prefab, Is.Not.Null);
                var index = 0;
                foreach (var card in cards)
                {
                    var root = Object.Instantiate(prefab, canvasRoot.transform); root.SetActive(true);
                    try
                    {
                        var view = root.GetComponentInChildren<UIWorkspaceTooltipView>(true); Invoke(view, "Awake"); Invoke(view, "OnEnable");
                        Invoke(view, "SetStatusContent", card.presentation);
                        var rect = (RectTransform)Get(view, "tooltipRect"); rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1); rect.pivot = new Vector2(.5f, 1); rect.anchoredPosition = new Vector2(0, -20);
                        foreach (var transform in canvasRoot.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer = 31;
                        Canvas.ForceUpdateCanvases(); foreach (var text in root.GetComponentsInChildren<TMP_Text>(true)) text.ForceMeshUpdate(); Invoke(view, "ResizeForContent"); Canvas.ForceUpdateCanvases();
                        rect.anchoredPosition = new Vector2(0, -20);
                        camera.Render(); RenderTexture.active = render;
                        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                        var low = RectTransformUtility.WorldToScreenPoint(camera, corners[0]); var high = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
                        var x = Mathf.Max(0, Mathf.FloorToInt(low.x) - 8); var y = Mathf.Max(0, Mathf.FloorToInt(low.y) - 8);
                        var width = Mathf.Min(512 - x, Mathf.CeilToInt(high.x - low.x) + 16); var height = Mathf.Min(1200 - y, Mathf.CeilToInt(high.y - low.y) + 16);
                        var pixels = new Texture2D(width, height, TextureFormat.RGBA32, false);
                        try { pixels.ReadPixels(new Rect(x, y, width, height), 0, 0); pixels.Apply(); card.file = (++index).ToString("00") + ".png"; File.WriteAllBytes(Path.Combine(output, card.file), pixels.EncodeToPNG()); }
                        finally { Object.DestroyImmediate(pixels); }
                        card.title = ((TMP_Text)Get(view, "titleText")).text; card.body = ((TMP_Text)Get(view, "bodyText")).text; card.meta = ((TMP_Text)Get(view, "metaText")).text; card.kind = card.presentation.Kind.ToString();
                    }
                    finally { Object.DestroyImmediate(root); }
                }
                File.WriteAllText(Path.Combine(output, "gallery.json"), JsonUtility.ToJson(new Gallery { cards = cards.ToArray() }, true));
                Assert.That(cards.Count, Is.GreaterThanOrEqualTo(35)); Debug.Log("Exported " + cards.Count + " native hero status tooltip previews: " + output);
            }
            finally
            {
                RenderTexture.active = oldRender;
                if (renderCamera != null) renderCamera.targetTexture = null;
                foreach (var item in owned.AsEnumerable().Reverse()) if (item != null) Object.DestroyImmediate(item);
                typeof(UIWorkspaceTooltipView).GetProperty("ActiveInstance").SetValue(null, oldActive);
            }
        }
    }
}

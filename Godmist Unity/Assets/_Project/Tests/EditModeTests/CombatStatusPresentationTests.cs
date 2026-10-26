using System.Collections.Generic;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Views.UI;
using NUnit.Framework;

namespace Godmist.Tests.EditMode
{
    public sealed class CombatStatusPresentationTests
    {
        [TestCase(CharacterStatType.Speed, StatModifierOperation.FlatBase, 4f, "Szybkość: +4")]
        [TestCase(CharacterStatType.Speed, StatModifierOperation.FlatAbsolute, -4f, "Szybkość: -4")]
        [TestCase(CharacterStatType.Speed, StatModifierOperation.Percent, .2f, "Szybkość: +20%")]
        [TestCase(CharacterStatType.Dodge, StatModifierOperation.FlatAbsolute, 4f, "Unik: +4")]
        [TestCase(CharacterStatType.Dodge, StatModifierOperation.Maximum, 0f, "Limit — Unik: 0")]
        [TestCase(CharacterStatType.CritChance, StatModifierOperation.FlatBase, .1f, "Szansa krytyczna: +10%")]
        public void BuffAndDebuffModifiersUsePlayerFacingTextInBothTooltipViews(
            CharacterStatType stat, StatModifierOperation operation, float value, string expected)
        {
            var effect = new PassiveEffectRuntime { EffectId = "test.modifier", RemainingDuration = 3,
                StatModifiers = new List<StatModifier> { new() { StatType = stat, Operation = operation, Value = value } },
                VariableModifiers = new List<GameplayVariableModifier> { new() { VariableType = GameplayVariableType.DamageDealtPercent, Value = .15f } } };
            var presentation = new CombatStatusPresentation(effect, CombatStatusKind.Buff, 1);
            var node = new UnityEngine.GameObject("Plain modifier tooltip");
            try
            {
                var body = node.AddComponent<Godmist.Views.UI.Workspace.UIWorkspaceTooltipView>().BuildPresentedStatusPayload(presentation);
                var legacy = (string)typeof(CombatTooltipView).GetMethod("BuildPresentedStatusPayload",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, new object[] { presentation });
                foreach (var text in new[] { body, legacy })
                {
                    Assert.That(text, Does.Contain(expected).And.Contain("Obrażenia zadawane: +15%"));
                    Assert.That(text, Does.Not.Contain("Modyfikator").And.Not.Contain("pkt").And.Not.Contain("addytywnie"));
                }
                Assert.That(effect.StatModifiers[0].Value, Is.EqualTo(value));
            }
            finally { UnityEngine.Object.DestroyImmediate(node); }
        }

        [TestCase(StatusEffectIds.Bleed)]
        [TestCase(StatusEffectIds.Poison)]
        [TestCase(StatusEffectIds.Burn)]
        public void DamageStacksShowOneCombinedNextTickWithoutChangingRuntime(string statusId)
        {
            var first = SkillPart(0, "skill.first"); var second = SkillPart(0, "skill.second");
            first.EffectId = second.EffectId = statusId;
            first.SetPayload(new DamageOverTimePayload { RemainingTotalDamage = 10, DamagePerTurn = 99 });
            second.SetPayload(new DamageOverTimePayload { RemainingTotalDamage = 10, DamagePerTurn = 99 });
            var entries = CombatStatusPresentationFactory.Create(new[] { first, second });
            Assert.That(entries, Has.Count.EqualTo(1));
            Assert.That(entries[0].StackCount, Is.EqualTo(2));
            Assert.That(entries[0].NextTickDamage, Is.EqualTo(8), "Round each real tick, not the merged damage total.");
            var node = new UnityEngine.GameObject("Stacked damage tooltip");
            try
            {
                var text = node.AddComponent<Godmist.Views.UI.Workspace.UIWorkspaceTooltipView>().BuildPresentedStatusPayload(entries[0]);
                Assert.That(text, Does.Contain("łącznie 8 obrażeń").And.Not.Contain("99"));
                Assert.That(text.Split(new[] { "obrażeń" }, System.StringSplitOptions.None).Length, Is.EqualTo(2));
                Assert.That(first.GetPayload<DamageOverTimePayload>().RemainingTotalDamage, Is.EqualTo(10));
                Assert.That(second.RemainingDuration, Is.EqualTo(3));
                Assert.That(entries[0].Effects[0], Is.SameAs(first));
                Assert.That(entries[0].Effects[1], Is.SameAs(second));
                var meta = (string)typeof(Godmist.Views.UI.Workspace.UIWorkspaceTooltipView).GetMethod("BuildStatusMeta",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(null, new object[] { first, null, null, entries[0].StackCount });
                Assert.That(meta, Does.Contain("Stosy: 2"));
                var legacyText = (string)typeof(CombatTooltipView).GetMethod("BuildPresentedStatusPayload",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(null, new object[] { entries[0] });
                Assert.That(legacyText, Does.Contain("Łącznie: 8 obrażeń").And.Contain("Stosy: 2"));
            }
            finally { UnityEngine.Object.DestroyImmediate(node); }
        }

        [TestCase(false)] [TestCase(true)]
        public void CombinedBleedingPreservesDifferentExpiryTimesAndRebuildsAfterExpiry(bool exploration)
        {
            var first = SkillPart(0, turns: 2); var second = SkillPart(1, turns: 5);
            first.EffectId = second.EffectId = StatusEffectIds.Bleed;
            first.SetPayload(new DamageOverTimePayload { RemainingTotalDamage = 12 });
            second.SetPayload(new DamageOverTimePayload { RemainingTotalDamage = 25 });
            var entry = CombatStatusPresentationFactory.Create(new[] { first, second })[0];
            if (exploration) entry = new CombatStatusPresentation(entry.Effect, entry.Kind, entry.Priority,
                explorationCharacter: new Godmist.Runtime.Characters.CharacterInstance(), groupedEffects: entry.Effects);
            var node = new UnityEngine.GameObject("Mixed bleeding tooltip");
            try
            {
                var text = node.AddComponent<Godmist.Views.UI.Workspace.UIWorkspaceTooltipView>().BuildPresentedStatusPayload(entry);
                Assert.That(text, Does.Contain("łącznie 11 obrażeń").And.Contain("Stosy: 2"));
                if (exploration) Assert.That(text, Does.Contain("Pozostało: 12 s").And.Contain("Pozostało: 30 s").And.Not.Contain("ticki"));
                else Assert.That(text, Does.Contain("2 t.").And.Contain("5 t."));
                first.RemainingDuration = 0;
                var refreshed = CombatStatusPresentationFactory.Create(new[] { first, second });
                Assert.That(refreshed, Has.Count.EqualTo(1));
                Assert.That(refreshed[0].StackCount, Is.EqualTo(1));
                Assert.That(refreshed[0].NextTickDamage, Is.EqualTo(5));
            }
            finally { UnityEngine.Object.DestroyImmediate(node); }
        }

        [Test]
        public void DifferentDamageStatusesRemainSeparateAndExistingStacksAreNotMultipliedAgain()
        {
            var bleed = SkillPart(0); var poison = SkillPart(1);
            bleed.EffectId = StatusEffectIds.Bleed; poison.EffectId = StatusEffectIds.Poison;
            bleed.Stacks = 2;
            bleed.SetPayload(new DamageOverTimePayload { RemainingTotalDamage = 30, DamagePerTurn = 1 });
            var entries = CombatStatusPresentationFactory.Create(new[] { bleed, poison });
            Assert.That(entries, Has.Count.EqualTo(2));
            Assert.That(entries[0].NextTickDamage, Is.EqualTo(10));
            Assert.That(entries[0].StackCount, Is.EqualTo(2));
            var node = new UnityEngine.GameObject("Single stored stack tooltip");
            try
            {
                var text = node.AddComponent<Godmist.Views.UI.Workspace.UIWorkspaceTooltipView>().BuildPresentedStatusPayload(entries[0]);
                Assert.That(text, Does.Contain("10 obrażeń"), "The remaining damage total is authoritative for ticking.");
            }
            finally { UnityEngine.Object.DestroyImmediate(node); }
        }

        [Test]
        public void KnownStatusWithNumericPayloadDoesNotRepeatItsGenericRule()
        {
            var slow = new PassiveEffectRuntime
            {
                EffectId = StatusEffectIds.Slow,
                StatModifiers = new List<StatModifier> { new() { StatType = CharacterStatType.Speed, Value = -20 } }
            };
            var node = new UnityEngine.GameObject("Numeric status tooltip");
            try
            {
                var view = node.AddComponent<Godmist.Views.UI.Workspace.UIWorkspaceTooltipView>();
                var text = view.BuildPresentedStatusPayload(new CombatStatusPresentation(slow, CombatStatusKind.Status, 1));
                Assert.That(text, Does.Contain("-20").And.Not.Contain("Zmniejsza szybkość o 20%"));
            }
            finally { UnityEngine.Object.DestroyImmediate(node); }
        }

        [TestCase(false)] [TestCase(true)]
        public void ResolveIcon_BuffUsesSkillBeforeMissingOrPlaceholderIcon(bool placeholder)
        {
            var texture = new UnityEngine.Texture2D(2, 2);
            var fallback = UnityEngine.Sprite.Create(texture, new UnityEngine.Rect(0, 0, 1, 1), UnityEngine.Vector2.zero);
            var skillIcon = UnityEngine.Sprite.Create(texture, new UnityEngine.Rect(1, 0, 1, 1), UnityEngine.Vector2.zero);
            var skill = UnityEngine.ScriptableObject.CreateInstance<Godmist.Data.Skills.SkillData>();
            SetSkill(skill, "SkillId", "skill.test"); SetSkill(skill, "Icon", skillIcon);
            try
            {
                var entry = CombatStatusPresentationFactory.Create(new[] { SkillPart(0), SkillPart(1) })[0];
                UnityEngine.Sprite Lookup(string id) => id == "buff" || placeholder ? fallback : null;
                Assert.That(CombatStatusPresentationFactory.ResolveIcon(entry, Lookup, new[] { skill }), Is.SameAs(skillIcon));
                Assert.That(CombatStatusPresentationFactory.ResolveIcon(entry, Lookup, null), Is.SameAs(fallback));
                Assert.That(CombatStatusPresentationFactory.ResolveIcon(entry, _ => null, null), Is.Null);
                SetSkill(skill, "Icon", null);
                Assert.That(CombatStatusPresentationFactory.ResolveIcon(entry, Lookup, new[] { skill }), Is.SameAs(fallback));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(skill); UnityEngine.Object.DestroyImmediate(skillIcon);
                UnityEngine.Object.DestroyImmediate(fallback); UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void ResolveIcon_PreservesDedicatedIconAndMatchesMostSpecificSource()
        {
            var texture = new UnityEngine.Texture2D(2, 2);
            var fallback = UnityEngine.Sprite.Create(texture, new UnityEngine.Rect(0, 0, 1, 1), UnityEngine.Vector2.zero);
            var dedicated = UnityEngine.Sprite.Create(texture, new UnityEngine.Rect(1, 0, 1, 1), UnityEngine.Vector2.zero);
            var parent = UnityEngine.ScriptableObject.CreateInstance<Godmist.Data.Skills.SkillData>();
            var child = UnityEngine.ScriptableObject.CreateInstance<Godmist.Data.Skills.SkillData>();
            SetSkill(parent, "SkillId", "skill.test"); SetSkill(parent, "Icon", fallback);
            SetSkill(child, "SkillId", "skill.test.extra"); SetSkill(child, "Icon", dedicated);
            try
            {
                var entry = CombatStatusPresentationFactory.Create(new[] { SkillPart(0, "skill.test.extra") })[0];
                Assert.That(CombatStatusPresentationFactory.ResolveIcon(entry, _ => null, new[] { parent, child }), Is.SameAs(dedicated));
                Assert.That(CombatStatusPresentationFactory.ResolveIcon(entry, _ => null, new[] { child, parent }), Is.SameAs(dedicated));
                SetSkill(child, "Icon", fallback);
                Assert.That(CombatStatusPresentationFactory.ResolveIcon(entry, id => id == "buff" ? fallback : dedicated, new[] { child }), Is.SameAs(dedicated));
                SetSkill(child, "SkillId", "skill.test.other");
                Assert.That(CombatStatusPresentationFactory.ResolveIcon(entry, _ => null, new[] { child }), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent); UnityEngine.Object.DestroyImmediate(child);
                UnityEngine.Object.DestroyImmediate(dedicated); UnityEngine.Object.DestroyImmediate(fallback);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void ResolveIcon_AuraAndRedirectUseSourceSkill(bool redirect)
        {
            var texture = new UnityEngine.Texture2D(1, 1);
            var icon = UnityEngine.Sprite.Create(texture, new UnityEngine.Rect(0, 0, 1, 1), UnityEngine.Vector2.zero);
            var skill = UnityEngine.ScriptableObject.CreateInstance<Godmist.Data.Skills.SkillData>();
            SetSkill(skill, "SkillId", "skill.test"); SetSkill(skill, "Icon", icon);
            try
            {
                var entry = new CombatStatusPresentation(new PassiveEffectRuntime { EffectId = "buff" }, CombatStatusKind.Buff, 3,
                    aura: redirect ? null : new CombatAuraRuntime { AuraId = "skill.test.aura.0.caster" },
                    outgoingRedirect: redirect ? new CombatDamageRedirectRuntime { SourceEffectId = "skill.test.damage_redirect.0" } : null);
                Assert.That(CombatStatusPresentationFactory.ResolveIcon(entry, _ => null, new[] { skill }), Is.SameAs(icon));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(skill); UnityEngine.Object.DestroyImmediate(icon);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void SetSkill(Godmist.Data.Skills.SkillData skill, string property, object value)
        {
            typeof(Godmist.Data.Skills.SkillData).GetField("<" + property + ">k__BackingField",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(skill, value);
        }

        private static PassiveEffectRuntime SkillPart(int index, string skill = "skill.test", string caster = "caster", int turns = 3)
        {
            return new PassiveEffectRuntime
            {
                EffectId = skill + ".modifier." + index, DisplayName = "Wzmocnienie",
                Lifetime = PassiveEffectLifetime.Timed, RemainingDuration = turns,
                Sources = new List<PassiveEffectSourceRuntime>
                {
                    new() { SourceType = PassiveEffectSourceType.Skill, SourceId = skill + ".effect." + index, SourceCharacterId = caster }
                }
            };
        }

        [Test] public void Create_GroupsSkillPartsWithoutMergingOrMutatingRuntimePayloads()
        {
            var first = SkillPart(0); var second = SkillPart(1);
            first.StatModifiers = new List<StatModifier> { new() { StatType = CharacterStatType.Speed, Value = 20 } };
            second.VariableModifiers = new List<GameplayVariableModifier> { new() { VariableType = GameplayVariableType.DamageDealtPercent, Value = .25f } };
            var entries = CombatStatusPresentationFactory.Create(new[] { first, second });
            Assert.That(entries, Has.Count.EqualTo(1));
            Assert.That(entries[0].Effects, Is.EqualTo(new[] { first, second }));
            Assert.That(entries[0].Duration, Is.EqualTo(3));
            Assert.That(entries[0].StackCount, Is.EqualTo(1));
            Assert.That(first.Stacks, Is.EqualTo(1)); Assert.That(second.Stacks, Is.EqualTo(1));
            Assert.That(first.VariableModifiers, Is.Empty); Assert.That(second.StatModifiers, Is.Empty);
        }

        [TestCase("skill.other", "caster")]
        [TestCase("skill.test", "another-caster")]
        public void Create_DoesNotGroupDifferentSourcesEvenWithIdenticalDisplayName(string skill, string caster)
        {
            Assert.That(CombatStatusPresentationFactory.Create(new[] { SkillPart(0), SkillPart(1, skill, caster) }), Has.Count.EqualTo(2));
        }

        [Test] public void Create_RemovesExpiredPartButKeepsRemainingBuff()
        {
            var first = SkillPart(0); var second = SkillPart(1, turns: 1);
            var entries = CombatStatusPresentationFactory.Create(new[] { first, second });
            Assert.That(entries, Has.Count.EqualTo(1)); Assert.That(entries[0].HasMixedDurations, Is.True);
            Assert.That(entries[0].Duration, Is.Zero);
            second.RemainingDuration = 0;
            entries = CombatStatusPresentationFactory.Create(new[] { first, second });
            Assert.That(entries, Has.Count.EqualTo(1)); Assert.That(entries[0].Effects.Count, Is.EqualTo(1));
            Assert.That(entries[0].Duration, Is.EqualTo(3));
        }

        [Test] public void Create_GroupsLegacyExplorationPotionPartsByInstance()
        {
            var first = SkillPart(0); var second = SkillPart(1); var other = SkillPart(2);
            first.Sources.Clear(); second.Sources.Clear(); other.Sources.Clear();
            first.EffectId = "consumable.potion-id.ModifySpeed.0";
            second.EffectId = "consumable.potion-id.periodic_recovery.1";
            other.EffectId = "consumable.other-potion.ModifySpeed.0";
            var entries = CombatStatusPresentationFactory.Create(new[] { first, second, other });
            Assert.That(entries, Has.Count.EqualTo(2)); Assert.That(entries[0].Effects, Has.Count.EqualTo(2));
        }

        [Test] public void InspectSignatureChangesWhenSecondaryComponentExpires()
        {
            var first = SkillPart(0); var second = SkillPart(1);
            var effects = new[] { first, second };
            var method = typeof(CombatUnitInspectWindowView).GetMethod("BuildStatusSignature",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            var before = (string)method.Invoke(null, new object[] { effects, null, null, null });
            second.RemainingDuration = 0;
            var after = (string)method.Invoke(null, new object[] { effects, null, null, null });
            Assert.That(after, Is.Not.EqualTo(before));
        }

        [TestCase(false)] [TestCase(true)]
        public void GroupedTooltipIncludesEveryPayloadAndIndividualDurations(bool exploration)
        {
            var first = SkillPart(0); var second = SkillPart(1, turns: 5);
            first.StatModifiers = new List<StatModifier> { new() { StatType = CharacterStatType.Speed, Value = 20 } };
            second.VariableModifiers = new List<GameplayVariableModifier> { new() { VariableType = GameplayVariableType.DamageDealtPercent, Value = .25f } };
            var entry = CombatStatusPresentationFactory.Create(new[] { first, second })[0];
            if (exploration) entry = new CombatStatusPresentation(entry.Effect, entry.Kind, entry.Priority,
                explorationCharacter: new Godmist.Runtime.Characters.CharacterInstance(), groupedEffects: entry.Effects);
            var go = new UnityEngine.GameObject("Grouped status tooltip");
            try
            {
                var view = go.AddComponent<Godmist.Views.UI.Workspace.UIWorkspaceTooltipView>();
                var text = view.BuildPresentedStatusPayload(entry);
                Assert.That(text, Does.Contain("20").And.Contain("25%"));
                if (exploration)
                {
                    Assert.That(text, Does.Contain("Pozostało: 18 s").And.Contain("Pozostało: 30 s").And.Not.Contain("ticki"));
                }
                else Assert.That(text, Does.Contain("Pozostało: 3 t.").And.Contain("Pozostało: 5 t."));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void Create_IncludesActiveAuraForOwnerWithoutPassiveEffect()
        {
            var auras = new List<CombatAuraRuntime>
            {
                new()
                {
                    AuraId = "warding_aura.aura.0",
                    DisplayName = "Warding Aura",
                    StatusId = StatusEffectIds.Freeze,
                    Radius = 2,
                    IncludesCaster = false,
                    TargetingType = TargetingType.Enemies
                }
            };

            var presentations = CombatStatusPresentationFactory.Create(
                null,
                auras);

            Assert.That(presentations, Has.Count.EqualTo(1));
            Assert.That(presentations[0].Effect.EffectId,
                Is.EqualTo(StatusEffectIds.Freeze));
            Assert.That(presentations[0].Effect.DisplayName,
                Is.EqualTo("Warding Aura"));
            Assert.That(presentations[0].Aura, Is.SameAs(auras[0]));
            Assert.That(presentations[0].Kind, Is.EqualTo(CombatStatusKind.Buff));
        }

        [Test]
        public void Create_AuraPresentationCarriesDamagePerTurn()
        {
            var aura = new CombatAuraRuntime
            {
                AuraId = "burning_aura.aura.0",
                DisplayName = "Burning Aura",
                StatusId = StatusEffectIds.Burn,
                StatusDamageMultiplier = .9f,
                StatusDamagePerTurn = 18
            };

            var presentations = CombatStatusPresentationFactory.Create(
                null,
                new[] { aura });

            var damage = presentations[0].Effect.GetPayload<DamageOverTimePayload>();
            Assert.That(damage, Is.Not.Null);
            Assert.That(damage.RemainingTotalDamage, Is.EqualTo(18));
        }

        [Test]
        public void Create_DoesNotDuplicateAuraStatusAlreadyAppliedToOwner()
        {
            var aura = new CombatAuraRuntime
            {
                AuraId = "burning_aura.aura.0",
                DisplayName = "Burning Aura",
                StatusId = StatusEffectIds.Burn
            };
            var effect = new PassiveEffectRuntime
            {
                EffectId = StatusEffectIds.Burn,
                Lifetime = PassiveEffectLifetime.Timed,
                RemainingDuration = 1,
                Sources = new List<PassiveEffectSourceRuntime>
                {
                    new() { SourceId = aura.AuraId }
                }
            };

            var presentations = CombatStatusPresentationFactory.Create(
                new[] { effect },
                new[] { aura });

            Assert.That(presentations, Has.Count.EqualTo(1));
            Assert.That(presentations[0].Effect, Is.SameAs(effect));
            Assert.That(presentations[0].Aura, Is.SameAs(aura));
        }

        [Test]
        public void Create_IncludesOutgoingRedirectForProtector()
        {
            var redirect = new CombatDamageRedirectRuntime
            {
                SourceEffectId = "skill.redirect.damage_redirect.0",
                TargetCharacterId = "protected",
                TargetDisplayName = "Chroniony",
                RedirectPercent = .4f
            };

            var presentations = CombatStatusPresentationFactory.Create(
                null,
                null,
                new List<CombatDamageRedirectRuntime> { redirect });

            Assert.That(presentations, Has.Count.EqualTo(1));
            Assert.That(presentations[0].OutgoingRedirect, Is.SameAs(redirect));
            Assert.That(
                presentations[0].Effect.GetPayload<DamageRedirectPayload>()
                    .ProtectedCharacterDisplayName,
                Is.EqualTo("Chroniony"));
        }

        [Test]
        public void Create_ExcludesPersistentEffectsEvenWhenCombatRelevant()
        {
            var experience = new PassiveEffectRuntime
            {
                EffectId = "passive.experience"
            };
            experience.VariableModifiers = new List<GameplayVariableModifier>
            {
                new()
                {
                    VariableType = GameplayVariableType.ExperienceGainPercent,
                    Value = .25f
                }
            };

            var damage = new PassiveEffectRuntime
            {
                EffectId = "passive.damage"
            };
            damage.VariableModifiers = new List<GameplayVariableModifier>
            {
                new()
                {
                    VariableType = GameplayVariableType.DamageDealtPercent,
                    Value = .25f
                }
            };

            var combatDamage = new PassiveEffectRuntime
            {
                EffectId = "passive.combat-damage",
                Lifetime = PassiveEffectLifetime.CombatScoped
            };
            combatDamage.VariableModifiers = new List<GameplayVariableModifier>
            {
                new()
                {
                    VariableType = GameplayVariableType.DamageDealtPercent,
                    Value = .25f
                }
            };

            var presentations = CombatStatusPresentationFactory.Create(
                new[] { experience, damage, combatDamage });

            Assert.That(presentations, Has.Count.EqualTo(1));
            Assert.That(presentations[0].Effect, Is.SameAs(combatDamage));
        }
    }
}

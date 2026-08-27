using System.Collections.Generic;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Views.UI;
using NUnit.Framework;

namespace Godmist.Tests.EditMode
{
    public sealed class CombatStatusPresentationTests
    {
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
    }
}

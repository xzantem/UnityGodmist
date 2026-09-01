using System.Linq;
using Godmist.Systems.Debugging;
using NUnit.Framework;

namespace Godmist.Tests.EditMode
{
    public class DevConsoleCommandSchemaTests
    {
        [Test]
        public void Registry_DeclaresEveryKnownConsoleCommand()
        {
            var help = DevConsoleCommandSchemaRegistry.BuildHelp();

            Assert.That(help, Does.Contain("createitem <itemId> [amount] [characterId]"));
            Assert.That(help, Does.Contain("createconsumable <itemId> [Effect value ...] [characterId]"));
            Assert.That(help, Does.Contain("Przykład: createitem item.weapon.debug characterId=jerzy amount=3"));
            Assert.That(
                DevConsoleCommandSchemaRegistry.All.Select(schema => schema.Name),
                Is.Unique);
        }

        [Test]
        public void Parser_NormalizesNamedOptionalArgumentsInAnyOrder()
        {
            var normalized = DevConsoleCommandSchemaParser.TryNormalize(
                "createitem item.weapon.debug characterId=jerzy amount=3",
                out var tokens,
                out var error);

            Assert.That(normalized, Is.True, error);
            Assert.That(
                tokens,
                Is.EqualTo(new[]
                {
                    "createitem",
                    "item.weapon.debug",
                    "3",
                    "jerzy"
                }));
        }

        [Test]
        public void Parser_UsesDefaultAmountWhenOnlyCharacterIsNamed()
        {
            var normalized = DevConsoleCommandSchemaParser.TryNormalize(
                "createitem item.weapon.debug characterId=jerzy",
                out var tokens,
                out var error);

            Assert.That(normalized, Is.True, error);
            Assert.That(
                tokens,
                Is.EqualTo(new[]
                {
                    "createitem",
                    "item.weapon.debug",
                    "1",
                    "jerzy"
                }));
        }

        [Test]
        public void Parser_DistinguishesMissingAndInvalidRequiredArguments()
        {
            var missing = DevConsoleCommandSchemaParser.TryNormalize(
                "gold",
                out _,
                out var missingError);
            var invalid = DevConsoleCommandSchemaParser.TryNormalize(
                "gold many",
                out _,
                out var invalidError);

            Assert.That(missing, Is.False);
            Assert.That(missingError, Does.Contain("Brak wymaganego argumentu"));
            Assert.That(invalid, Is.False);
            Assert.That(invalidError, Does.Contain("amount"));
            Assert.That(invalidError, Does.Contain("nieprawidłową wartość"));
        }

        [Test]
        public void Parser_PreservesExistingOverloadedCommandForms()
        {
            var oldForm = DevConsoleCommandSchemaParser.TryNormalize(
                "xp jerzy 10",
                out var oldTokens,
                out var oldError);
            var shortForm = DevConsoleCommandSchemaParser.TryNormalize(
                "xp 10",
                out var shortTokens,
                out var shortError);
            var namedForm = DevConsoleCommandSchemaParser.TryNormalize(
                "xp amount=10 characterId=jerzy",
                out var namedTokens,
                out var namedError);

            Assert.That(oldForm, Is.True, oldError);
            Assert.That(shortForm, Is.True, shortError);
            Assert.That(namedForm, Is.True, namedError);
            Assert.That(oldTokens, Is.EqualTo(new[] { "xp", "jerzy", "10" }));
            Assert.That(shortTokens, Is.EqualTo(new[] { "xp", "10" }));
            Assert.That(namedTokens, Is.EqualTo(new[] { "xp", "jerzy", "10" }));
        }
    }
}

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
            Assert.That(help, Does.Contain("setworldflag <flagId> <value>"));
            Assert.That(help, Does.Contain("createequipment <itemId> <quality> <partId>"));
            Assert.That(help, Does.Contain("upgrade <npcId> <instanceId> <targetProgress>"));
            Assert.That(help, Does.Contain("markquestobjective <questId> <objectiveId> [amount]"));
            Assert.That(help, Does.Contain("removeitem <characterId> <instanceId>"));
            Assert.That(help, Does.Contain("clearinventory [characterId]"));
            Assert.That(help, Does.Contain("setlevel <characterId> <level>"));
            Assert.That(help, Does.Contain("setdanger <theme> <0-100>"));
            Assert.That(help, Does.Contain("setloyalty <npcId> <level>"));
            Assert.That(help, Does.Contain("spawnobject <objectId> <x> <y> [instanceId]"));
            Assert.That(help, Does.Contain("help [command]"));
            Assert.That(help, Does.Contain("Przykład: createitem item.weapon.debug characterId=jerzy amount=3"));
            Assert.That(
                DevConsoleCommandSchemaRegistry.All.Select(schema => schema.Name),
                Is.Unique);
            Assert.That(
                DevConsoleCommandSchemaRegistry.All.Select(schema => schema.Name),
                Does.Not.Contain("craftmaterial"));
            Assert.That(
                DevConsoleCommandSchemaRegistry.All.Select(schema => schema.Name),
                Does.Not.Contain("craftequipment"));
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
        public void Parser_NormalizesGalduryteCommandWithDefaultIdentificationStatus()
        {
            var normalized = DevConsoleCommandSchemaParser.TryNormalize(
                "creategalduryte 1 weapon",
                out var tokens,
                out var error);

            Assert.That(normalized, Is.True, error);
            Assert.That(
                tokens,
                Is.EqualTo(new[]
                {
                    "creategalduryte",
                    "1",
                    "weapon",
                    "identified"
                }));
        }

        [Test]
        public void Parser_NormalizesUnidentifiedGalduryteForExplicitCharacter()
        {
            var normalized = DevConsoleCommandSchemaParser.TryNormalize(
                "creategalduryte 1 armor unidentified characterId=jerzy",
                out var tokens,
                out var error);

            Assert.That(normalized, Is.True, error);
            Assert.That(
                tokens,
                Is.EqualTo(new[]
                {
                    "creategalduryte",
                    "1",
                    "armor",
                    "unidentified",
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

        [Test]
        public void Parser_NormalizesNamedEquipmentCreationAndUpgradeArguments()
        {
            var createNormalized = DevConsoleCommandSchemaParser.TryNormalize(
                "createequipment equipment.weapon Excellent part.head part.binder part.handle characterId=jerzy",
                out var createTokens,
                out var createError);
            var upgradeNormalized = DevConsoleCommandSchemaParser.TryNormalize(
                "upgrade npc.blacksmith item.instance.001 0.5 characterId=jerzy",
                out var upgradeTokens,
                out var upgradeError);

            Assert.That(createNormalized, Is.True, createError);
            Assert.That(createTokens, Is.EqualTo(new[]
            {
                "createequipment", "equipment.weapon", "Excellent", "part.head",
                "part.binder", "part.handle", "jerzy"
            }));
            Assert.That(upgradeNormalized, Is.True, upgradeError);
            Assert.That(upgradeTokens, Is.EqualTo(new[]
            {
                "upgrade", "npc.blacksmith", "item.instance.001", "0.5", "0.99", "jerzy"
            }));
        }

        [Test]
        public void Parser_AllowsMasterpieceEquipmentQuality()
        {
            var normalized = DevConsoleCommandSchemaParser.TryNormalize(
                "createequipment equipment.weapon Masterpiece part.head part.binder part.handle",
                out var tokens,
                out var error);

            Assert.That(normalized, Is.True, error);
            Assert.That(tokens[2], Is.EqualTo("Masterpiece"));
        }

        [Test]
        public void Parser_AllowsZeroFloorsAndNegativeDungeonCoordinates()
        {
            var gotoResult = DevConsoleCommandSchemaParser.TryNormalize(
                "goto dungeon.debug 0 room.start",
                out var gotoTokens,
                out var gotoError);
            var spawnResult = DevConsoleCommandSchemaParser.TryNormalize(
                "spawnobject object.debug -2 0",
                out var spawnTokens,
                out var spawnError);

            Assert.That(gotoResult, Is.True, gotoError);
            Assert.That(gotoTokens, Is.EqualTo(new[] { "goto", "dungeon.debug", "0", "room.start" }));
            Assert.That(spawnResult, Is.True, spawnError);
            Assert.That(spawnTokens, Is.EqualTo(new[] { "spawnobject", "object.debug", "-2", "0" }));
        }

        [Test]
        public void Parser_NormalizesSpecificHelpCommand()
        {
            var normalized = DevConsoleCommandSchemaParser.TryNormalize(
                "help createitem",
                out var tokens,
                out var error);

            Assert.That(normalized, Is.True, error);
            Assert.That(tokens, Is.EqualTo(new[] { "help", "createitem" }));
        }

        [Test]
        public void Parser_PreservesBareNoclipForToggle()
        {
            var normalized = DevConsoleCommandSchemaParser.TryNormalize(
                "noclip",
                out var tokens,
                out var error);

            Assert.That(normalized, Is.True, error);
            Assert.That(tokens, Is.EqualTo(new[] { "noclip" }));
        }
    }
}

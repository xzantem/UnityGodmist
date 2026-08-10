using Godmist.Core.Serialization;
using Godmist.Systems.SaveLoad;
using NUnit.Framework;

namespace Godmist.Tests.EditMode
{
    public class GameSaveJsonSerializerTests
    {
        [Test]
        public void Serialize_WhenSaveDataIsValid_ReturnsReadableJson()
        {
            var serializer = new GameSaveJsonSerializer();
            var saveData = new GameSaveData
            {
                SaveVersion = 1,
                SaveId = "save.test.01",
                DisplayName = "Testowa kampania",
                CreatedAtUtc = "2026-08-10T10:00:00.0000000Z",
                UpdatedAtUtc = "2026-08-10T10:05:00.0000000Z"
            };

            var json = serializer.Serialize(saveData);

            Assert.That(json, Does.Contain("\"SaveVersion\": 1"));
            Assert.That(json, Does.Contain("\"SaveId\": \"save.test.01\""));
            Assert.That(json, Does.Contain("\"DisplayName\": \"Testowa kampania\""));
        }

        [Test]
        public void Serialize_WhenSaveDataIsNull_ReturnsEmptyString()
        {
            var serializer = new GameSaveJsonSerializer();

            var json = serializer.Serialize(null);

            Assert.That(json, Is.Empty);
        }

        [Test]
        public void TryDeserialize_WhenJsonIsValid_ReturnsEquivalentData()
        {
            var serializer = new GameSaveJsonSerializer();
            const string json = @"{
  ""SaveVersion"": 1,
  ""SaveId"": ""save.test.01"",
  ""DisplayName"": ""Testowa kampania"",
  ""CreatedAtUtc"": ""2026-08-10T10:00:00.0000000Z"",
  ""UpdatedAtUtc"": ""2026-08-10T10:05:00.0000000Z""
}";

            var result = serializer.TryDeserialize(json, out var saveData);

            Assert.That(result, Is.True);
            Assert.That(saveData.SaveVersion, Is.EqualTo(1));
            Assert.That(saveData.SaveId, Is.EqualTo("save.test.01"));
            Assert.That(saveData.DisplayName, Is.EqualTo("Testowa kampania"));
            Assert.That(saveData.CreatedAtUtc, Is.EqualTo("2026-08-10T10:00:00.0000000Z"));
            Assert.That(saveData.UpdatedAtUtc, Is.EqualTo("2026-08-10T10:05:00.0000000Z"));
        }

        [Test]
        public void TryDeserialize_WhenJsonIsEmpty_ReturnsFalse()
        {
            var serializer = new GameSaveJsonSerializer();

            var result = serializer.TryDeserialize(string.Empty, out var saveData);

            Assert.That(result, Is.False);
            Assert.That(saveData, Is.Null);
        }

        [Test]
        public void TryDeserialize_WhenSaveIdIsMissing_ReturnsFalse()
        {
            var serializer = new GameSaveJsonSerializer();
            const string json = @"{
  ""SaveVersion"": 1,
  ""DisplayName"": ""Uszkodzony zapis""
}";

            var result = serializer.TryDeserialize(json, out var saveData);

            Assert.That(result, Is.False);
            Assert.That(saveData, Is.Null);
        }

        [Test]
        public void CreateNew_CreatesVersionedSaveWithMetadata()
        {
            var saveData = GameSaveData.CreateNew("  Kampania Jerzego  ");

            Assert.That(saveData.SaveVersion, Is.EqualTo(GameSaveData.CurrentSaveVersion));
            Assert.That(saveData.SaveId, Is.Not.Empty);
            Assert.That(saveData.DisplayName, Is.EqualTo("Kampania Jerzego"));
            Assert.That(saveData.CreatedAtUtc, Is.Not.Empty);
            Assert.That(saveData.UpdatedAtUtc, Is.EqualTo(saveData.CreatedAtUtc));
        }
    }
}
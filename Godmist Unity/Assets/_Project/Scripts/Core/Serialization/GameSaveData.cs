using System;

namespace Godmist.Core.Serialization
{
    [Serializable]
    public class GameSaveData
    {
        public const int CurrentSaveVersion = 1;

        public int SaveVersion = CurrentSaveVersion;
        public string SaveId = string.Empty;
        public string DisplayName = string.Empty;
        public string CreatedAtUtc = string.Empty;
        public string UpdatedAtUtc = string.Empty;

        public static GameSaveData CreateNew(string displayName)
        {
            var now = DateTime.UtcNow.ToString("O");

            return new GameSaveData
            {
                SaveVersion = CurrentSaveVersion,
                SaveId = Guid.NewGuid().ToString("N"),
                DisplayName = string.IsNullOrWhiteSpace(displayName)
                    ? "Nowa kampania"
                    : displayName.Trim(),
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
        }
    }
}
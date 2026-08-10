using Godmist.Core.Serialization;
using UnityEngine;

namespace Godmist.Systems.SaveLoad
{
    public class GameSaveJsonSerializer
    {
        public string Serialize(GameSaveData saveData, bool prettyPrint = true)
        {
            if (saveData == null)
            {
                return string.Empty;
            }

            return JsonUtility.ToJson(saveData, prettyPrint);
        }

        public bool TryDeserialize(string json, out GameSaveData saveData)
        {
            saveData = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                saveData = JsonUtility.FromJson<GameSaveData>(json);
            }
            catch
            {
                return false;
            }

            if (saveData == null
                || saveData.SaveVersion <= 0
                || string.IsNullOrWhiteSpace(saveData.SaveId))
            {
                saveData = null;
                return false;
            }

            return true;
        }
    }
}
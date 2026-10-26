using System.IO;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    /// <summary>Presentation-only import/wiring of the reviewed Bandit Leader pack.</summary>
    public static class BanditLeaderArtSetup
    {
        public const string ConfigPath="Assets/_Project/Scripts/Editor/BanditLeaderArtBindings.json";

        [MenuItem("Godmist/Content/Bosses 2.0/Configure Approved Bandit Leader Art")]
        public static void Configure()
        {
            var config=JsonUtility.FromJson<DeadKingArtSetup.Config>(File.ReadAllText(ConfigPath));
            DeadKingArtSetup.Configure(config);
            Debug.Log($"BANDIT_LEADER_ART_CONFIGURED: {config.imports.Length} exact textures; both phases, motion, poses, effects, corpses, portraits, skill/status icons.");
        }
    }
}

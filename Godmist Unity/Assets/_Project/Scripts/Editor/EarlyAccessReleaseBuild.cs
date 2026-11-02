using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Godmist.Editor
{
    /// <summary>Explicit, local Windows release builds in the existing Editor.</summary>
    [InitializeOnLoad]
    public static class EarlyAccessReleaseBuild
    {
        public const string CandidateVersion = Godmist.Core.Constants.GameVersion.Release;
        public static string Output => Path.GetFullPath("../LocalHandoffs/beta-012-20261031");
        public static string PlayerDirectory => Path.GetFullPath("../Builds/" + CandidateVersion + "/Windows-x64");
        private static string Request => Path.Combine(Output, "release.request");
        private static bool running;

        static EarlyAccessReleaseBuild() => EditorApplication.update += Tick;

        private static void Tick()
        {
            if (running || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling
                || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer || !File.Exists(Request)) return;
            var command = File.ReadAllText(Request).Trim();
            File.Delete(Request);
            running = true;
            try
            {
                if (command == "configure-and-build") { ConfigureCandidate(); BuildCandidate(); }
                else if (command == "configure") ConfigureCandidate();
                else if (command == "build") BuildCandidate();
                else throw new InvalidOperationException("Unknown explicit release command: " + command);
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(Output, "state.txt"), "FAILED: " + error);
                Debug.LogException(error);
            }
            finally { running = false; }
        }

        public static void ConfigureCandidate()
        {
            Directory.CreateDirectory(Output);
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before release configuration.");
            var company = PlayerSettings.companyName;
            var product = PlayerSettings.productName;
            PlayerSettings.bundleVersion = Godmist.Core.Constants.GameVersion.Bundle;
            GodmistMainMenuSetup.ConfigureReleaseVersionLabel();
            AssetDatabase.SaveAssets();
            if (PlayerSettings.companyName != company || PlayerSettings.productName != product)
                throw new InvalidOperationException("Release configuration must preserve the existing save location.");
            File.WriteAllText(Path.Combine(Output, "configuration.json"), JsonUtility.ToJson(new Configuration
            {
                version = PlayerSettings.bundleVersion, company = company, product = product,
                saveRoot = Path.Combine(Application.persistentDataPath, "Saves"), development = false,
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray()
            }, true));
            File.WriteAllText(Path.Combine(Output, "state.txt"), "CONFIGURED: " + CandidateVersion);
        }

        public static void BuildCandidate(string playerDirectory = null, string output = null)
        {
            playerDirectory ??= PlayerDirectory;
            output ??= Output;
            Directory.CreateDirectory(output);
            if (PlayerSettings.bundleVersion != Godmist.Core.Constants.GameVersion.Bundle) throw new InvalidOperationException("Configure the candidate first.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                throw new InvalidOperationException("Windows x64 build support is not installed.");
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0 || scenes[0] != GodmistMainMenuSetup.ScenePath)
                throw new InvalidOperationException("Bootstrap must be the first enabled scene.");
            foreach (var path in scenes)
            {
                var preview = EditorSceneManager.OpenPreviewScene(path);
                try
                {
                    var missing = preview.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                        .Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
                    if (missing != 0) throw new InvalidOperationException(path + ": missing scripts=" + missing);
                }
                finally { EditorSceneManager.ClosePreviewScene(preview); }
            }
            var executable = Path.Combine(playerDirectory, "Godmist.exe");
            if (File.Exists(executable)) throw new InvalidOperationException("Candidate already exists; use a fresh version or preserve it before rebuilding.");
            Directory.CreateDirectory(playerDirectory);
            File.WriteAllText(Path.Combine(output, "state.txt"), "BUILD_RUNNING: " + DateTime.UtcNow.ToString("O"));
            using var log = new StreamWriter(Path.Combine(output, "build.log"), false, new UTF8Encoding(false)) { AutoFlush = true };
            void Record(string message, string stack, LogType type)
            {
                log.WriteLine("[" + type + "] " + message);
                if (type is LogType.Error or LogType.Exception) log.WriteLine(stack);
            }
            Application.logMessageReceived += Record;
            try
            {
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes, locationPathName = executable, target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                });
                var data = new Result
                {
                    result = report.summary.result.ToString(), errors = report.summary.totalErrors,
                    warnings = report.summary.totalWarnings, bytes = report.summary.totalSize,
                    seconds = report.summary.totalTime.TotalSeconds, version = PlayerSettings.bundleVersion,
                    output = executable, development = (report.summary.options & BuildOptions.Development) != 0,
                    scenes = scenes, messages = report.steps.SelectMany(s => s.messages)
                        .Where(m => m.type is LogType.Warning or LogType.Error or LogType.Exception)
                        .Select(m => m.type + ": " + m.content).Distinct().ToArray()
                };
                File.WriteAllText(Path.Combine(output, "build-result.json"), JsonUtility.ToJson(data, true));
                File.WriteAllText(Path.Combine(output, "state.txt"), "BUILD_" + data.result + ": " + data.output);
                if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Windows candidate build failed; see build-result.json.");
            }
            finally { Application.logMessageReceived -= Record; }
        }

        [Serializable] private sealed class Configuration
        { public string version, company, product, saveRoot; public bool development; public string[] scenes; }
        [Serializable] private sealed class Result
        { public string result, version, output; public int errors, warnings; public ulong bytes; public double seconds; public bool development; public string[] scenes, messages; }
    }
}

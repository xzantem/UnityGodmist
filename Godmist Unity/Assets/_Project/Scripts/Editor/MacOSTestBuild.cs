using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Godmist.Editor
{
    /// <summary>Explicit local cross-build; never configures or publishes a release.</summary>
    [InitializeOnLoad]
    public static class MacOSTestBuild
    {
        private static string Output => Path.GetFullPath("../LocalHandoffs/macos-universal-test-20261031");
        private static string Request => Path.Combine(Output, "build.request");
        private static bool running;

        static MacOSTestBuild() => EditorApplication.update += Tick;

        private static void Tick()
        {
            if (running || !File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode
                || EditorApplication.isCompiling || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer) return;
            running = true;
            try
            {
                var command = File.ReadAllText(Request).Trim();
                if (command == "close-for-module-restart")
                {
                    File.Delete(Request);
                    if (Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount)
                        .Any(i => UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))
                        throw new InvalidOperationException("Cannot restart: unsaved scenes.");
                    File.WriteAllText(Path.Combine(Output, "state.txt"), "CLOSING_FOR_MODULE_RESTART");
                    EditorApplication.Exit(0);
                    return;
                }
                if (command == "restore-target")
                {
                    File.Delete(Request);
                    var target = (BuildTarget)Enum.Parse(typeof(BuildTarget), File.ReadAllText(Path.Combine(Output, "original-target.txt")).Trim());
                    if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(target), target))
                        throw new InvalidOperationException("Cannot restore original Editor target.");
                    File.WriteAllText(Path.Combine(Output, "restore-target.txt"), target.ToString());
                    return;
                }
                if (command == "preflight")
                {
                    File.Delete(Request);
                    File.WriteAllText(Path.Combine(Output, "preflight.txt"),
                        "supported=" + BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX)
                        + "; active=" + EditorUserBuildSettings.activeBuildTarget
                        + "; dirtyScenes=" + Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount)
                            .Count(i => UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty));
                    return;
                }
                if (command != "build") throw new InvalidOperationException("Unknown macOS test command.");
                if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX))
                    throw new InvalidOperationException("Mac build support unavailable in this Editor; restart may be required after module installation.");
                if (Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount)
                    .Any(i => UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))
                    throw new InvalidOperationException("Unsaved scenes: save or discard your changes before building.");
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneOSX)
                {
                    File.WriteAllText(Path.Combine(Output, "state.txt"), "SWITCHING_TO_MACOS");
                    if (!SessionState.GetBool("Godmist.MacOSTestBuild.Switching", false))
                    {
                        SessionState.SetBool("Godmist.MacOSTestBuild.Switching", true);
                        File.WriteAllText(Path.Combine(Output, "original-target.txt"), EditorUserBuildSettings.activeBuildTarget.ToString());
                        if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX))
                            throw new InvalidOperationException("Cannot switch to macOS.");
                    }
                    return; // Re-enter after target scripts/importers have reloaded.
                }
                File.Delete(Request);
                Build();
            }
            catch (Exception error)
            {
                if (File.Exists(Request)) File.Delete(Request);
                File.WriteAllText(Path.Combine(Output, "state.txt"), "FAILED: " + error);
                Debug.LogException(error);
            }
            finally { running = false; }
        }

        public static void Build(string app = null, string output = null)
        {
            output ??= Output;
            Directory.CreateDirectory(output);
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0 || scenes[0] != GodmistMainMenuSetup.ScenePath)
                throw new InvalidOperationException("Bootstrap must be the first enabled scene.");
            foreach (var path in scenes)
            {
                var preview = EditorSceneManager.OpenPreviewScene(path);
                try
                {
                    if (preview.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                        .Any(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) != 0))
                        throw new InvalidOperationException("Missing scripts: " + path);
                }
                finally { EditorSceneManager.ClosePreviewScene(preview); }
            }
            app ??= Path.GetFullPath("../Builds/0.1.2-test.1/macOS-Universal/Godmist.app");
            if (Directory.Exists(app)) throw new InvalidOperationException("Refusing to overwrite an existing test build: " + app);
            var platform = BuildPipeline.GetBuildTargetName(BuildTarget.StandaloneOSX);
            var architecture = EditorUserBuildSettings.GetPlatformSettings(platform, "Architecture");
            using var log = new StreamWriter(Path.Combine(output, "build.log")) { AutoFlush = true };
            void Record(string message, string stack, LogType type)
            {
                log.WriteLine("[" + type + "] " + message);
                if (type is LogType.Error or LogType.Exception) log.WriteLine(stack);
            }
            Application.logMessageReceived += Record;
            try
            {
                EditorUserBuildSettings.SetPlatformSettings(platform, "Architecture", "x64arm64");
                if (EditorUserBuildSettings.GetPlatformSettings(platform, "Architecture").ToLowerInvariant() != "x64arm64")
                    throw new InvalidOperationException("Universal architecture configuration did not persist.");
                File.WriteAllText(Path.Combine(output, "state.txt"), "BUILD_RUNNING");
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes, target = BuildTarget.StandaloneOSX, targetGroup = BuildTargetGroup.Standalone,
                    locationPathName = app, options = BuildOptions.CleanBuildCache | BuildOptions.DetailedBuildReport
                });
                var result = new Result
                {
                    result = report.summary.result.ToString(), errors = report.summary.totalErrors,
                    warnings = report.summary.totalWarnings, seconds = report.summary.totalTime.TotalSeconds,
                    bytes = report.summary.totalSize, output = app, version = PlayerSettings.bundleVersion,
                    company = PlayerSettings.companyName, product = PlayerSettings.productName, scenes = scenes,
                    messages = report.steps.SelectMany(s => s.messages)
                        .Where(m => m.type is LogType.Warning or LogType.Error or LogType.Exception)
                        .Select(m => m.type + ": " + m.content).Distinct().ToArray()
                };
                File.WriteAllText(Path.Combine(output, "build-result.json"), JsonUtility.ToJson(result, true));
                File.WriteAllText(Path.Combine(output, "state.txt"), "BUILD_" + result.result);
                if (report.summary.result != BuildResult.Succeeded || report.summary.totalErrors != 0 || !Directory.Exists(app))
                    throw new InvalidOperationException("macOS build did not produce an error-free app; see build-result.json.");
            }
            finally
            {
                Application.logMessageReceived -= Record;
                EditorUserBuildSettings.SetPlatformSettings(platform, "Architecture", architecture);
                SessionState.EraseBool("Godmist.MacOSTestBuild.Switching");
            }
        }

        [Serializable] private sealed class Result
        {
            public string result, output, version, company, product;
            public int errors, warnings;
            public double seconds;
            public ulong bytes;
            public string[] scenes, messages;
        }
    }
}

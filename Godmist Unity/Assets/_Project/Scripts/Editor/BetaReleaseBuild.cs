using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Godmist.Editor
{
    /// <summary>Explicit local prerelease requests in the project's existing Editor. Never uploads or publishes.</summary>
    [InitializeOnLoad]
    public static class BetaReleaseBuild
    {
        private static string Output => EarlyAccessReleaseBuild.Output;
        private static string Request => Path.Combine(Output, "beta.request");
        private static bool running;
        static BetaReleaseBuild() => EditorApplication.update += Tick;

        private static void Tick()
        {
            if (running || !File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode
                || EditorApplication.isCompiling || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer) return;
            running = true;
            try
            {
                if (Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount)
                    .Any(i => UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))
                    throw new InvalidOperationException("Unsaved scenes; prerelease tools will not replace them.");
                var command = File.ReadAllText(Request).Trim();
                if (command == "configure")
                {
                    File.Delete(Request);
                    File.WriteAllText(Path.Combine(Output, "original-target.txt"), EditorUserBuildSettings.activeBuildTarget.ToString());
                    EarlyAccessReleaseBuild.ConfigureCandidate();
                    return;
                }
                var target = command switch
                {
                    "windows" => BuildTarget.StandaloneWindows64,
                    "mac" => BuildTarget.StandaloneOSX,
                    "restore" => (BuildTarget)Enum.Parse(typeof(BuildTarget), File.ReadAllText(Path.Combine(Output, "original-target.txt")).Trim()),
                    _ => throw new InvalidOperationException("Unknown beta request: " + command)
                };
                if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
                    throw new InvalidOperationException("Build support unavailable: " + target);
                if (EditorUserBuildSettings.activeBuildTarget != target)
                {
                    File.WriteAllText(Path.Combine(Output, "beta-state.txt"), "SWITCHING: " + target);
                    if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, target))
                        throw new InvalidOperationException("Target switch failed: " + target);
                    return; // Retain the request across the target's domain reload/import.
                }
                File.Delete(Request);
                if (command == "restore") { File.WriteAllText(Path.Combine(Output, "beta-state.txt"), "RESTORED: " + target); return; }
                if (PlayerSettings.bundleVersion != Godmist.Core.Constants.GameVersion.Bundle)
                    throw new InvalidOperationException("Configure the prerelease version before building.");
                File.WriteAllText(Path.Combine(Output, "beta-state.txt"), "BUILDING: " + target);
                var logDirectory = Path.Combine(Output, command);
                Directory.CreateDirectory(logDirectory);
                if (command == "windows") EarlyAccessReleaseBuild.BuildCandidate(output: logDirectory);
                else MacOSTestBuild.Build(Path.GetFullPath("../Builds/" + EarlyAccessReleaseBuild.CandidateVersion + "/macOS-Universal/Godmist.app"), logDirectory);
                var result = File.ReadAllText(Path.Combine(logDirectory, "build-result.json"));
                if (!result.Contains("\"Succeeded\"")) throw new InvalidOperationException("Build did not succeed; inspect " + logDirectory);
                File.WriteAllText(Path.Combine(Output, "beta-state.txt"), "SUCCEEDED: " + target);
            }
            catch (Exception error)
            {
                if (File.Exists(Request)) File.Delete(Request);
                File.WriteAllText(Path.Combine(Output, "beta-state.txt"), "FAILED: " + error);
                Debug.LogException(error);
            }
            finally { running = false; }
        }
    }
}

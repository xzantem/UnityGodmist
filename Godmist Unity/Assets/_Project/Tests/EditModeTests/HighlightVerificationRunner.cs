using System;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    /// <summary>Local, explicit verification in the owning Editor; uses the normal Test Runner setup.</summary>
    [InitializeOnLoad]
    public static class HighlightVerificationRunner
    {
        public static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../../LocalHandoffs/highlight-review-20261101/implementation"));
        static TestRunnerApi runner;
        static HighlightVerificationRunner()
        {
            EditorApplication.update += Tick;
            runner = ScriptableObject.CreateInstance<TestRunnerApi>();
            runner.RegisterCallbacks(new Results());
        }
        static void Tick()
        {
            if (SessionState.GetBool("Godmist.HighlightVerification.Active", false)) return;
            if (!EditorApplication.isCompiling && !EditorApplication.isUpdating)
            {
                var refresh = Path.Combine(Output, "refresh.request");
                if (!EditorApplication.isPlayingOrWillChangePlaymode && File.Exists(refresh))
                {
                    File.Delete(refresh);
                    AssetDatabase.Refresh();
                    return;
                }
                var restore = Path.Combine(Output, "restore-background.request");
                if (!EditorApplication.isPlayingOrWillChangePlaymode && File.Exists(restore))
                {
                    PlayerSettings.runInBackground = bool.Parse(File.ReadAllText(restore).Trim());
                    AssetDatabase.SaveAssets();
                    File.Delete(restore);
                    return;
                }
            }
            var path = Path.Combine(Output, "tests.request");
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(path)) return;
            var names = File.ReadAllText(path).Trim().Split(';');
            File.Delete(path);
            SessionState.SetBool("Godmist.HighlightVerification.Active", true);
            runner.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, testNames = names }));
        }
        sealed class Results : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
            {
                if (!SessionState.GetBool("Godmist.HighlightVerification.Active", false)) return;
                Directory.CreateDirectory(Output);
                File.WriteAllText(Path.Combine(Output, "test-state.txt"), "RUNNING");
            }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                if (!SessionState.GetBool("Godmist.HighlightVerification.Active", false)) return;
                SessionState.SetBool("Godmist.HighlightVerification.Active", false);
                TestRunnerApi.SaveResultToFile(result, Path.Combine(Output, "results.xml"));
                File.WriteAllText(Path.Combine(Output, "test-state.txt"), $"{result.TestStatus}: passed={result.PassCount}, failed={result.FailCount}, skipped={result.SkipCount}");
            }
        }
    }
}

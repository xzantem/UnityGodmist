using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    /// <summary>Re-register after Play Mode domain reloads; keep this checkout's results out of the shared user-data directory.</summary>
    [InitializeOnLoad]
    internal static class ControllerVerificationResults
    {
        private static string DirectoryPath => Path.GetFullPath(Path.Combine(Application.dataPath, "../../LocalHandoffs/controller-support-20261031"));
        static ControllerVerificationResults()
        {
            TestRunnerApi.RegisterTestCallback(new Results());
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            var root = DirectoryPath;
            if (File.Exists(Path.Combine(root, "status.request")))
            {
                File.Delete(Path.Combine(root, "status.request"));
                File.WriteAllText(Path.Combine(root, "editor-status.txt"), $"playing={EditorApplication.isPlaying}; compiling={EditorApplication.isCompiling}; dirty={Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount).Any(i => UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)}; utc={System.DateTime.UtcNow:O}");
            }
            var path = Path.Combine(root, "verify.request");
            if (!File.Exists(path) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            var filter = File.ReadAllText(path).Trim();
            File.Delete(path);
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, testNames = filter.Split(';') }));
        }

        private sealed class Results : ICallbacks
        {
            private static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../../LocalHandoffs/controller-support-20261031"));
            public void RunStarted(ITestAdaptor test) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result)
            {
                if (!result.FullName.StartsWith("Godmist.Tests.EditMode.ControllerProductionPlayTests.")) return;
                Directory.CreateDirectory(Output);
                TestRunnerApi.SaveResultToFile(result, Path.Combine(Output, "play-" + result.Name + ".xml"));
            }
            public void RunFinished(ITestResultAdaptor result)
            {
                Directory.CreateDirectory(DirectoryPath);
                TestRunnerApi.SaveResultToFile(result, Path.Combine(DirectoryPath, "remap-results.xml"));
                if (!ContainsController(result)) return;
                Directory.CreateDirectory(Output);
                TestRunnerApi.SaveResultToFile(result, Path.Combine(Output, "play-results.xml"));
                File.WriteAllText(Path.Combine(Output, "play-state.txt"), $"{result.ResultState}; passed={result.PassCount}; failed={result.FailCount}; utc={result.EndTime:O}");
            }
            private static bool ContainsController(ITestResultAdaptor result) =>
                result.FullName.StartsWith("Godmist.Tests.EditMode.ControllerProductionPlayTests")
                || result.Children.Any(ContainsController);
        }
    }
}

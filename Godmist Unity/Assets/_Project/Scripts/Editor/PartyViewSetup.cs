using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    /// <summary>Explicit local authoring request, processed only by an idle Editor.</summary>
    [InitializeOnLoad]
    public static partial class PartyViewSetup
    {
        public const string Output = "../LocalHandoffs/party-view-20261102";
        public const string Root = "Assets/_Project/Prefabs/UI/Workspace/";
        static PartyViewSetup() => EditorApplication.update += Poll;
        private static void Poll()
        {
            var path = Path.Combine(Output, "author.request");
            if (!File.Exists(path) || EditorApplication.isCompiling || EditorApplication.isUpdating
                || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(path);
            try { Apply(); File.WriteAllText(Path.Combine(Output, "author-state.txt"), "PASS apply"); }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(Output, "author-state.txt"), error.ToString());
                Debug.LogException(error);
            }
        }
    }
}

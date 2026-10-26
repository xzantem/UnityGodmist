using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Godmist.Editor
{
    /// <summary>Explicit application icon authoring in the existing Editor.</summary>
    [InitializeOnLoad]
    public static class GodmistApplicationIconSetup
    {
        const string Art = "Assets/_Project/Art/Icons/Application/Ghorn";
        static string Output => Path.GetFullPath("../LocalHandoffs/application-icon-20261027");
        static string Request => Path.Combine(Output, "configure.request");
        static bool running;
        static GodmistApplicationIconSetup() => EditorApplication.update += Tick;

        static void Tick()
        {
            if (running || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling
                || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer || !File.Exists(Request)) return;
            var command = File.ReadAllText(Request).Trim();
            File.Delete(Request);
            running = true;
            try
            {
                if (command == "finalize-pixel-icons")
                {
                    var destination = Art + "/Godmist-Ghorn.ico";
                    File.Copy(Path.GetFullPath("../output/ghorn-game-icon-v2/Godmist-Ghorn.ico"), destination, true);
                    AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
                    GodmistWindowsIconPostprocess.Apply(BuildTarget.StandaloneWindows64,
                        Path.GetFullPath("../Builds/0.1.0-rc.1/Windows-x64-icon/Godmist.exe"));
                    File.WriteAllText(Path.Combine(Output, "state.txt"), "PIXEL_ICONS_FINALIZED");
                    return;
                }
                if (command != "configure" && command != "configure-and-build")
                    throw new InvalidOperationException("Unknown icon command: " + command);
                Configure();
                if (command == "configure-and-build")
                    EarlyAccessReleaseBuild.BuildCandidate(Path.GetFullPath("../Builds/0.1.0-rc.1/Windows-x64-icon"), Output);
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(Output, "state.txt"), "FAILED: " + error);
                Debug.LogException(error);
            }
            finally { running = false; }
        }

        public static void Configure()
        {
            Directory.CreateDirectory(Output);
            var source = Path.GetFullPath("../output/ghorn-game-icon-v2");
            Directory.CreateDirectory(Art);
            foreach (var size in new[] { 16, 24, 32, 48, 64, 128, 256, 512, 1024 })
            {
                var path = Art + "/ghorn-" + size + ".png";
                File.Copy(Path.Combine(source, "ghorn-" + size + ".png"), path, true);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Default;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 1024;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.isReadable = true;
                importer.sRGBTexture = true;
                importer.SaveAndReimport();
            }
            var targets = new[] { NamedBuildTarget.Unknown, NamedBuildTarget.Standalone };
            var rows = targets.Select(target =>
            {
                var sizes = PlayerSettings.GetIconSizes(target, IconKind.Any);
                if (sizes.Length == 0) throw new InvalidOperationException("No application icon slots for " + target.TargetName);
                var textures = sizes.Select(size => AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "/ghorn-" + size + ".png")).ToArray();
                if (textures.Any(t => t == null)) throw new InvalidOperationException("Unsupported icon slot sizes: " + string.Join(",", sizes));
                PlayerSettings.SetIcons(target, textures, IconKind.Any);
                var actual = PlayerSettings.GetIcons(target, IconKind.Any);
                if (!actual.SequenceEqual(textures)) throw new InvalidOperationException("Icon settings readback mismatch.");
                return new Target { name = target.TargetName, sizes = sizes, paths = actual.Select(AssetDatabase.GetAssetPath).ToArray() };
            }).ToArray();
            var baseIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "/ghorn-64.png");
            foreach (var size in new[] { 128, 256, 512, 1024 })
            {
                var large = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "/ghorn-" + size + ".png");
                for (var y = 0; y < size; y++) for (var x = 0; x < size; x++)
                    if (large.GetPixel(x, y) != baseIcon.GetPixel(x / (size / 64), y / (size / 64)))
                        throw new InvalidOperationException("Large icon is not nearest-neighbor from 64.");
            }
            AssetDatabase.SaveAssets();
            File.WriteAllText(Path.Combine(Output, "configuration.json"), JsonUtility.ToJson(new Result
            { version = PlayerSettings.bundleVersion, targets = rows }, true));
            File.WriteAllText(Path.Combine(Output, "state.txt"), "CONFIGURED: icon references and nearest64 readback PASS");
        }

        [Serializable] class Target { public string name; public int[] sizes; public string[] paths; }
        [Serializable] class Result { public string version; public Target[] targets; }
    }
}

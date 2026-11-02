using System.Linq;
using Godmist.Views.Combat;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Godmist.Editor
{
    public sealed class CombatOutlineBuildValidation : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public const string AssetPath = "Assets/Resources/" + CombatOutlineShader.ResourcePath + ".shader";
        public int callbackOrder => 0;

        public static void ValidateResourceDependency()
        {
            var shader = CombatOutlineShader.Load();
            if (shader == null || AssetDatabase.GetAssetPath(shader) != AssetPath)
                throw new BuildFailedException("Combat outlines require the shader resource at " + AssetPath);
            if (ShaderUtil.ShaderHasError(shader))
                throw new BuildFailedException("Combat outline shader has compilation errors.");
        }

        public void OnPreprocessBuild(BuildReport report) => ValidateResourceDependency();

        public void OnPostprocessBuild(BuildReport report)
        {
            if (!report.packedAssets.SelectMany(p => p.contents).Any(a => a.sourceAssetPath == AssetPath))
                throw new BuildFailedException("Combat outline shader is missing from the packed player assets: " + AssetPath);
        }
    }
}

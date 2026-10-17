using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Zeldexile.EditorTools
{
    /// <summary>Windows test build. Output: D:\19 - Zeldexile\Builds\Windows\Zeldexile.exe</summary>
    public static class WindowsBuild
    {
        private const string Output = "../Builds/Windows/Zeldexile.exe";

        [MenuItem("Zeldexile/Gerar build Windows")]
        public static void Build()
        {
            PrototypeSceneBuilder.Build();
            BuildFromSavedScenes();
        }

        /// <summary>Same build, from the scenes as they are saved (nothing is regenerated).</summary>
        [MenuItem("Zeldexile/Gerar build Windows (cenas salvas)")]
        public static void BuildFromSavedScenes()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(RegionSceneBuilder.ScenePath) == null) RegionSceneBuilder.Build();
            RegionSceneBuilder.EnsureMaterials();
            RegionSceneBuilder.AddToBuild();

            PlayerSettings.productName = "Zeldexile";
            PlayerSettings.companyName = "Zeldexile";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { RegionSceneBuilder.ScenePath, PrototypeSceneBuilder.ScenePath, PrototypeSceneBuilder.ShowcasePath },
                locationPathName = Output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });

            var summary = report.summary;
            Debug.Log($"Zeldexile build: {summary.result}, {summary.totalErrors} erro(s), {summary.totalSize / (1024 * 1024)} MB em {summary.outputPath}");
            if (Application.isBatchMode && summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}

using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zeldexile.Gameplay;

namespace Zeldexile.EditorTools
{
    /// <summary>
    /// Scene of the first open-world region. The valley itself is built at runtime from
    /// Resources/World/Regions/vale_inicial.json (RegionWorld); this scene only holds the builder, the camera
    /// post-processing and the saved materials that keep instanced shader variants in builds.
    /// </summary>
    public static class RegionSceneBuilder
    {
        public const string ScenePath = "Assets/Zeldexile/Scenes/ValeInicial.unity";

        /// <summary>Also from the command line: -executeMethod Zeldexile.EditorTools.RegionSceneBuilder.Build</summary>
        [MenuItem("Zeldexile/Recriar cena do Vale Inicial")]
        public static void Build()
        {
            EnsureMaterials();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            new GameObject("Vale Inicial").AddComponent<RegionWorld>().RegionId = "vale_inicial";
            var camera = Object.FindAnyObjectByType<Camera>();
            if (camera != null) PostProcessSetup.Apply(camera);
            System.IO.Directory.CreateDirectory("Assets/Zeldexile/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuild();
            AssetDatabase.SaveAssets();
            Debug.Log($"Zeldexile: cena da região salva em {ScenePath}");
        }

        /// <summary>
        /// Player with only the region, for measuring it: Builds/ValeBench/Zeldexile.exe. Run it with
        /// "-zxbench &lt;folder&gt;" (RegionBenchmark). Leaves the other scenes and the main build untouched.
        /// </summary>
        [MenuItem("Zeldexile/Gerar build de medição do Vale")]
        public static void BuildBenchmarkPlayer()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) Build();
            EnsureMaterials();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "../Builds/ValeBench/Zeldexile.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            var summary = report.summary;
            Debug.Log($"Zeldexile build de medição: {summary.result}, {summary.totalErrors} erro(s), {summary.totalSize / (1024 * 1024)} MB em {summary.outputPath}");
            if (Application.isBatchMode && summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        /// <summary>Puts the region first in the build list (the game opens there), keeping every other scene.</summary>
        public static void AddToBuild()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) return;
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>Saved materials with GPU instancing on, so builds keep the instanced variants.</summary>
        public static void EnsureMaterials()
        {
            Ensure("Assets/Zeldexile/Resources/Materials/Folhagem.mat", "Zeldexile/Foliage", true);
            Ensure("Assets/Zeldexile/Resources/Materials/FolhagemContorno.mat", "Zeldexile/FoliageOutline", true);
            Ensure("Assets/Zeldexile/Resources/Materials/Riacho.mat", "Zeldexile/Stream", false);
        }

        private static void Ensure(string path, string shaderName, bool instanced)
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"Zeldexile: shader {shaderName} não encontrado.");
                return;
            }

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            AssetDatabase.CreateAsset(new Material(shader) { enableInstancing = instanced }, path);
        }
    }
}

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zeldexile.Gameplay;

namespace Zeldexile.EditorTools
{
    public static class PrototypeSceneBuilder
    {
        public const string ScenePath = "Assets/Zeldexile/Scenes/Prototype.unity";

        /// <summary>Also called from the command line: -executeMethod Zeldexile.EditorTools.PrototypeSceneBuilder.Build</summary>
        [MenuItem("Zeldexile/Recriar cena de protótipo")]
        public static void Build()
        {
            EnsureGrassMaterial();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            new GameObject("Zeldexile").AddComponent<PrototypeWorld>();
            var camera = Object.FindAnyObjectByType<Camera>();
            if (camera != null) PostProcessSetup.Apply(camera);

            System.IO.Directory.CreateDirectory("Assets/Zeldexile/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"Zeldexile: cena salva em {ScenePath}");
        }

        /// <summary>Saved material with GPU instancing on, so builds keep the grass shader's instanced variant.</summary>
        private static void EnsureGrassMaterial()
        {
            const string path = "Assets/Zeldexile/Resources/Materials/Grama.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            System.IO.Directory.CreateDirectory("Assets/Zeldexile/Resources/Materials");
            var material = new Material(Shader.Find("Zeldexile/Grass")) { enableInstancing = true };
            AssetDatabase.CreateAsset(material, path);
        }
    }
}

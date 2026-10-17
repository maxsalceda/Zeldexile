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
            BuildShowcase();
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene(ShowcasePath, true)
            };
            RegionSceneBuilder.AddToBuild();
            AssetDatabase.SaveAssets();
            Debug.Log($"Zeldexile: cenas salvas em {ScenePath} e {ShowcasePath}");
        }

        public const string ShowcasePath = "Assets/Zeldexile/Scenes/Showcase.unity";

        /// <summary>Hero showcase: the character going through every action on a stage, with an orbiting camera.</summary>
        [MenuItem("Zeldexile/Criar cena de vitrine do herói")]
        public static void BuildShowcase()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            new GameObject("Vitrine do herói").AddComponent<CharacterShowcase>();
            System.IO.Directory.CreateDirectory("Assets/Zeldexile/Scenes");
            EditorSceneManager.SaveScene(scene, ShowcasePath);
            Debug.Log($"Zeldexile: vitrine salva em {ShowcasePath}");
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

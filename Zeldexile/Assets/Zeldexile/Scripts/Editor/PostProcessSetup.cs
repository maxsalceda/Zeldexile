using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace Zeldexile.EditorTools
{
    /// <summary>
    /// Post Processing v2 on the scene camera: soft bloom on the sun, glints and lightning, Neutral
    /// tonemapping with a light grade, a gentle vignette and SMAA. The profile is saved as an asset.
    /// </summary>
    public static class PostProcessSetup
    {
        public const string ProfilePath = "Assets/Zeldexile/Settings/PosProcessamento.asset";

        public static void Apply(Camera camera)
        {
            camera.allowHDR = true;
            if (!camera.TryGetComponent<PostProcessLayer>(out var layer)) layer = camera.gameObject.AddComponent<PostProcessLayer>();
            var guids = AssetDatabase.FindAssets("t:PostProcessResources");
            if (guids.Length > 0)
                layer.Init(AssetDatabase.LoadAssetAtPath<PostProcessResources>(AssetDatabase.GUIDToAssetPath(guids[0])));
            layer.volumeTrigger = camera.transform;
            layer.volumeLayer = 1 << 0;
            layer.antialiasingMode = PostProcessLayer.Antialiasing.SubpixelMorphologicalAntialiasing;

            var volume = new GameObject("Pós-processamento").AddComponent<PostProcessVolume>();
            volume.isGlobal = true;
            volume.priority = 1f;
            volume.sharedProfile = LoadOrCreateProfile();
        }

        private static PostProcessProfile LoadOrCreateProfile()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PostProcessProfile>(ProfilePath);
            if (existing != null) AssetDatabase.DeleteAsset(ProfilePath);

            System.IO.Directory.CreateDirectory("Assets/Zeldexile/Settings");
            var profile = ScriptableObject.CreateInstance<PostProcessProfile>();

            var bloom = profile.AddSettings<Bloom>();
            bloom.intensity.Override(1.3f);
            bloom.threshold.Override(0.95f);
            bloom.softKnee.Override(0.6f);
            bloom.diffusion.Override(7f);

            var grading = profile.AddSettings<ColorGrading>();
            grading.gradingMode.Override(GradingMode.HighDefinitionRange);
            grading.tonemapper.Override(Tonemapper.Neutral);
            grading.postExposure.Override(0.2f);
            grading.saturation.Override(12f);
            grading.contrast.Override(8f);
            grading.temperature.Override(3f);

            var vignette = profile.AddSettings<Vignette>();
            vignette.intensity.Override(0.26f);
            vignette.smoothness.Override(0.45f);

            AssetDatabase.CreateAsset(profile, ProfilePath);
            foreach (var settings in profile.settings) AssetDatabase.AddObjectToAsset(settings, profile);
            AssetDatabase.SaveAssets();
            return profile;
        }
    }
}

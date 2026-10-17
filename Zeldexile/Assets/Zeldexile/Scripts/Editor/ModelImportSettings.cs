using UnityEditor;
using UnityEngine;

namespace Zeldexile.EditorTools
{
    /// <summary>
    /// Import rules for the models built by Tools/Blender/build_models.py:
    /// meters 1:1, flat normals kept, no materials (the game assigns them by "@slot" name), no animation.
    /// </summary>
    public sealed class ModelImportSettings : AssetPostprocessor
    {
        public const string ModelsFolder = "Assets/Zeldexile/Resources/Models";

        /// <summary>Skinned models (the Meshy hero): bones and skin weights kept, the game poses the bones itself.</summary>
        public const string SkinnedFolder = ModelsFolder + "/Skinned";

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(ModelsFolder)) return;
            var importer = (ModelImporter)assetImporter;
            if (assetPath.StartsWith(SkinnedFolder))
            {
                importer.globalScale = 1f;
                importer.useFileScale = true;
                importer.bakeAxisConversion = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                // Generic keeps the skinned mesh and its bone transforms. Only the clips under Animacoes are read
                // (MotionClip samples them); the models' own clips are not.
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.importAnimation = assetPath.Contains("/Animacoes/");
                importer.animationCompression = ModelImporterAnimationCompression.Off;
                importer.optimizeGameObjects = false;
                importer.importCameras = false;
                importer.importLights = false;
                importer.importBlendShapes = false;
                importer.importVisibility = false;
                importer.importNormals = ModelImporterNormals.Import;
                importer.importTangents = ModelImporterTangents.None;
                // SkinnedHero reads the skin weights to repair the Meshy bindposes.
                importer.isReadable = true;
                importer.addCollider = false;
                return;
            }

            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.importVisibility = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.None;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.meshOptimizationFlags = MeshOptimizationFlags.Everything;
            // Nature models are merged into instanced meshes at runtime, which needs their vertex data.
            importer.isReadable = assetPath.StartsWith(ModelsFolder + "/Nature");
            importer.addCollider = false;
        }

        /// <summary>Also called from the command line after the Blender script runs.</summary>
        [MenuItem("Zeldexile/Reimportar modelos do Blender")]
        public static void Reimport()
        {
            AssetDatabase.ImportAsset(ModelsFolder, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            Debug.Log("Zeldexile: modelos reimportados.");
        }
    }
}

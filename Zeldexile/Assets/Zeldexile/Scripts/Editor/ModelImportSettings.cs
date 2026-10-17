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

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(ModelsFolder)) return;
            var importer = (ModelImporter)assetImporter;
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

using System.Text;
using UnityEditor;
using UnityEngine;

namespace Zeldexile.EditorTools
{
    /// <summary>Prints the hierarchy of the hero model (names, positions, rotations) to the log.</summary>
    public static class ModelInspector
    {
        [MenuItem("Zeldexile/Listar hierarquia do herói")]
        public static void DumpHero()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelImportSettings.ModelsFolder + "/Characters/heroi.fbx");
            var sb = new StringBuilder("Zeldexile hierarchy:\n");
            Dump(prefab.transform, 0, sb);
            Debug.Log(sb.ToString());
        }

        private static void Dump(Transform t, int depth, StringBuilder sb)
        {
            sb.Append(new string(' ', depth * 2)).Append(t.name)
                .Append("  pos=").Append(t.position.ToString("F3"))
                .Append(" rot=").Append(t.localEulerAngles.ToString("F1"))
                .Append(" scale=").Append(t.localScale.ToString("F2")).Append('\n');
            for (var i = 0; i < t.childCount; i++) Dump(t.GetChild(i), depth + 1, sb);
        }
    }
}

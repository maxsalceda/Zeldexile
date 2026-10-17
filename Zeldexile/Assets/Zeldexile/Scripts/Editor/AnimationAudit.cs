using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Zeldexile.EditorTools
{
    /// <summary>
    /// Writes, for the hero model and every clip under Animacoes, its skeleton (path and rest local transform of each
    /// bone) and what its clips animate, to Logs/animacoes/&lt;name&gt;.txt, plus a comparison of each clip's skeleton
    /// with the hero's. Command line: -executeMethod Zeldexile.EditorTools.AnimationAudit.Run
    /// </summary>
    public static class AnimationAudit
    {
        private const string Hero = ModelImportSettings.SkinnedFolder + "/heroi_meshy.fbx";
        private const string Clips = ModelImportSettings.SkinnedFolder + "/Animacoes";

        [MenuItem("Zeldexile/Auditar esqueletos das animações")]
        public static void Run()
        {
            AssetDatabase.Refresh();
            var dir = Path.Combine(Application.dataPath, "..", "Logs", "animacoes");
            Directory.CreateDirectory(dir);
            var hero = Skeleton(Hero);
            File.WriteAllText(Path.Combine(dir, "heroi_meshy.txt"), Describe(Hero, hero));
            var summary = new StringBuilder();
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Clips }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var bones = Skeleton(path);
                var name = Path.GetFileNameWithoutExtension(path);
                File.WriteAllText(Path.Combine(dir, name + ".txt"), Describe(path, bones));
                summary.AppendLine(Compare(name, hero, bones));
            }

            File.WriteAllText(Path.Combine(dir, "_comparacao.txt"), summary.ToString());
            Debug.Log("Zeldexile: auditoria em " + dir);
        }

        private struct Bone
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
        }

        private static string F(float v) => v.ToString("0.#####", CultureInfo.InvariantCulture);
        private static string V(Vector3 v) => $"({F(v.x)}, {F(v.y)}, {F(v.z)})";
        private static string Q(Quaternion q) => $"({F(q.x)}, {F(q.y)}, {F(q.z)}, {F(q.w)})";

        private static Dictionary<string, Bone> Skeleton(string assetPath)
        {
            var bones = new Dictionary<string, Bone>();
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (root == null) return bones;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == root.transform) continue;
                bones[AnimationUtility.CalculateTransformPath(t, root.transform)] =
                    new Bone { Position = t.localPosition, Rotation = t.localRotation, Scale = t.localScale };
            }

            return bones;
        }

        private static string Describe(string assetPath, Dictionary<string, Bone> bones)
        {
            var s = new StringBuilder();
            s.AppendLine(assetPath);
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (root != null)
            {
                s.AppendLine("root scale " + V(root.transform.localScale));
                foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    s.AppendLine($"skin {skin.name}: {skin.bones.Length} bones, mesh {(skin.sharedMesh != null ? skin.sharedMesh.vertexCount : 0)} verts, root {(skin.rootBone != null ? skin.rootBone.name : "-")}");
            }

            s.AppendLine($"transforms {bones.Count}");
            foreach (var pair in bones.OrderBy(p => p.Key))
                s.AppendLine($"  {pair.Key}  p{V(pair.Value.Position)} r{Q(pair.Value.Rotation)} s{V(pair.Value.Scale)}");

            foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<AnimationClip>())
            {
                if (clip.name.StartsWith("__preview__")) continue;
                var bindings = AnimationUtility.GetCurveBindings(clip);
                s.AppendLine($"clip '{clip.name}' length {F(clip.length)} s, {F(clip.frameRate)} fps, loop {clip.isLooping}, {bindings.Length} curves");
                foreach (var group in bindings.GroupBy(b => b.path).OrderBy(g => g.Key))
                {
                    var props = string.Join(",", group.Select(b => b.propertyName).Distinct()
                        .Select(p => p.Split('.')[0].Replace("m_Local", "")).Distinct());
                    s.AppendLine($"  anim {group.Key}: {props}");
                }

                var hips = bindings.Where(b => b.path.EndsWith("Hips") && b.propertyName.StartsWith("m_LocalPosition")).ToArray();
                foreach (var b in hips)
                {
                    var c = AnimationUtility.GetEditorCurve(clip, b);
                    s.AppendLine($"  hips {b.propertyName}: start {F(c.Evaluate(0f))} end {F(c.Evaluate(clip.length))} min {F(c.keys.Min(k => k.value))} max {F(c.keys.Max(k => k.value))}");
                }
            }

            return s.ToString();
        }

        /// <summary>Same paths? Same rest rotations and bone offsets? Lists the first differences.</summary>
        private static string Compare(string name, Dictionary<string, Bone> hero, Dictionary<string, Bone> clip)
        {
            var s = new StringBuilder();
            var shared = hero.Keys.Intersect(clip.Keys).ToList();
            var onlyClip = clip.Keys.Except(hero.Keys).ToList();
            var onlyHero = hero.Keys.Except(clip.Keys).ToList();
            float maxAngle = 0f, maxOffset = 0f;
            string worstAngle = "", worstOffset = "";
            foreach (var key in shared)
            {
                var a = Quaternion.Angle(hero[key].Rotation, clip[key].Rotation);
                var d = (hero[key].Position - clip[key].Position).magnitude / Mathf.Max(1e-6f, hero[key].Position.magnitude);
                if (a > maxAngle) { maxAngle = a; worstAngle = key; }
                if (d > maxOffset && hero[key].Position.magnitude > 1e-5f) { maxOffset = d; worstOffset = key; }
            }

            s.AppendLine($"{name}: {shared.Count} paths shared, {onlyClip.Count} only in clip, {onlyHero.Count} only in hero");
            s.AppendLine($"  max rest rotation diff {F(maxAngle)} deg at {worstAngle}; max bone offset diff {F(maxOffset * 100f)} % at {worstOffset}");
            foreach (var k in onlyClip.Take(12)) s.AppendLine("  clip only: " + k);
            foreach (var k in onlyHero.Take(12)) s.AppendLine("  hero only: " + k);
            return s.ToString();
        }
    }
}

using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Zeldexile.Characters;

namespace Zeldexile.Tests
{
    /// <summary>
    /// The bone-for-bone clips on the real hero: every bone the clip animates must lie along the same direction as in
    /// the clip and turn the same way (twist included), at every moment of the clip. Writes the measurements, the
    /// strikes and the right hand's path to Logs/animacoes/verificacao.txt.
    /// </summary>
    public sealed class DirectClipTests
    {
        private static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        /// <summary>The children a bone points at, both skeletons sharing them: its only child, or the aim of the chest and hand.</summary>
        private static int[] Aim(HeroSkeleton skeleton, DirectClip clip, int bone)
        {
            var name = skeleton.Names[bone];
            string[] wanted = null;
            if (name == "Spine2") wanted = new[] { "Neck" };
            else if (name == "LeftHand" || name == "RightHand") wanted = new[] { name + "Index1", name + "Ring1" };
            var result = new System.Collections.Generic.List<int>();
            if (wanted != null)
            {
                foreach (var w in wanted)
                {
                    var k = skeleton.IndexOf(w);
                    if (k >= 0 && clip.Drives(k)) result.Add(k);
                }

                return result.Count == wanted.Length ? result.ToArray() : new int[0];
            }

            for (var k = 0; k < skeleton.Bones.Count; k++)
                if (skeleton.Parents[k] == bone) result.Add(k);
            return result.Count == 1 && clip.Drives(result[0]) ? result.ToArray() : new int[0];
        }

        [UnityTest]
        public IEnumerator ClipsPlayBoneForBone()
        {
            var holder = new GameObject("Herói");
            var rig = CharacterRig.Build(holder.transform, RigStyle.Hero);
            yield return null;
            Assert.That(rig.Direct, Is.Not.Null, "the skinned hero has no bone-for-bone layer");
            var skeleton = rig.Direct.Skeleton;
            var log = new StringBuilder();
            var clips = new[] { rig.Sprint, rig.BasicAttack1, rig.BasicAttack2, rig.DrawSword, rig.SheatheSword };
            var names = new[] { "correr_rapido", "ataque_basico_1", "ataque_basico_2", "sacar_espada", "guardar_espada" };
            var worstDirection = 0f;
            var worstTurn = 0f;
            for (var c = 0; c < clips.Length; c++)
            {
                var clip = clips[c];
                Assert.That(clip, Is.Not.Null, names[c] + " did not load");
                log.AppendLine($"{clip.Name}: {F(clip.Duration)} s, hips scale {F(clip.HipsScale)}, strikes [{string.Join(", ", clip.Strikes.ConvertAll(F))}]");
                var rotations = new Quaternion[skeleton.Bones.Count];
                float clipDirection = 0f, clipTurn = 0f;
                string worstBone = "";
                for (var t = 0f; t <= clip.Duration + 1e-4f; t += clip.Duration / 40f)
                {
                    for (var i = 0; i < rotations.Length; i++) rotations[i] = skeleton.Bones[i].localRotation;
                    clip.Sample(t, false, rotations, out var hips);
                    for (var i = 0; i < rotations.Length; i++) skeleton.Bones[i].localRotation = rotations[i];
                    skeleton.Bones[skeleton.Hips].localPosition = hips;
                    var root = skeleton.Root;
                    for (var i = 0; i < rotations.Length; i++)
                    {
                        if (!clip.Drives(i)) continue;
                        var name = skeleton.Names[i];
                        // Turn: the hero's bone, undone by the fixed rest correction, against the clip's.
                        var hero = Quaternion.Inverse(root.rotation) * skeleton.Bones[i].rotation * Quaternion.Inverse(clip.Correction(i));
                        var turn = Quaternion.Angle(hero, clip.SourceRotation(name, t));
                        // Direction: along the limb, toward the one child it leads to (the neck for the chest, the
                        // knuckles between index and ring for the hand). Other children (shoulders, thumb) sit where
                        // each skeleton's anatomy puts them, which is proportion, not motion.
                        var dir = 0f;
                        var aim = Aim(skeleton, clip, i);
                        if (aim.Length > 0)
                        {
                            Vector3 h = Vector3.zero, s = Vector3.zero;
                            foreach (var k in aim)
                            {
                                h += root.InverseTransformPoint(skeleton.Bones[k].position) - root.InverseTransformPoint(skeleton.Bones[i].position);
                                s += clip.SourcePosition(skeleton.Names[k], t) - clip.SourcePosition(name, t);
                            }

                            if (h.sqrMagnitude > 1e-14f && s.sqrMagnitude > 1e-14f) dir = Vector3.Angle(h, s);
                        }

                        if (dir > clipDirection) worstBone = name;
                        clipDirection = Mathf.Max(clipDirection, dir);
                        clipTurn = Mathf.Max(clipTurn, turn);
                    }
                }

                log.AppendLine($"  max direction error {F(clipDirection)} deg (at {worstBone}), max turn error {F(clipTurn)} deg");
                worstDirection = Mathf.Max(worstDirection, clipDirection);
                worstTurn = Mathf.Max(worstTurn, clipTurn);

                // The right hand against the hips, in the hips' rest frame (model space), every 0.1 s.
                for (var t = 0f; t <= clip.Duration; t += 0.1f)
                {
                    var hand = clip.RightHandAt(t);
                    var i = Mathf.Min(clip.RightHandTrack.Length - 1, Mathf.RoundToInt(t / DirectClip.TrackStep));
                    var next = clip.RightHandTrack[Mathf.Min(clip.RightHandTrack.Length - 1, i + 1)];
                    var speed = (next - clip.RightHandTrack[i]).magnitude / DirectClip.TrackStep / skeleton.HipsHeight;
                    log.AppendLine($"  t {F(t)} hand ({F(hand.x / skeleton.HipsHeight)}, {F(hand.y / skeleton.HipsHeight)}, {F(hand.z / skeleton.HipsHeight)}) hips-heights, speed {F(speed)}");
                }
            }

            var folder = Path.Combine(Application.dataPath, "..", "Logs", "animacoes");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "verificacao.txt"), log.ToString());
            Object.Destroy(holder);
            Assert.That(worstDirection, Is.LessThan(0.5f), "a bone points away from the clip's\n" + log);
            Assert.That(worstTurn, Is.LessThan(0.5f), "a bone turns differently from the clip's\n" + log);
        }
    }
}

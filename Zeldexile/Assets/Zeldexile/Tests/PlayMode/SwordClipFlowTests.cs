using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Zeldexile.Characters;
using Zeldexile.Gameplay;
using Zeldexile.Player;

namespace Zeldexile.Tests
{
    /// <summary>
    /// The bone-for-bone clips in play on the island: the sprint while the run button is held, the sword drawn from
    /// the waist before the first swing, the two-swing basic combo chained by pressing again, and the sword put back.
    /// While a clip owns the body the bones must be exactly the clip's pose.
    /// </summary>
    public sealed class SwordClipFlowTests
    {
        private GameObject _world;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _world = new GameObject("Zeldexile");
            _world.AddComponent<PrototypeWorld>();
            yield return new WaitForSeconds(1f);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            PlayerInputReader.ScriptedMove = null;
            PlayerInputReader.ScriptedSprint = false;
            var ours = new[]
            {
                "Zeldexile", "Mundo", "Jogador", "Câmera", "HUD", "Efeitos", "Loot", "Sol", "Corrente de ar",
                "Plataforma de gelo", "Projétil", "Arco", "Atmosfera", "Game feel", "Grama densa", "Clima", "Raio"
            };
            foreach (var go in Object.FindObjectsByType<GameObject>())
                if (go != null && go.transform.parent == null &&
                    (ours.Contains(go.name) || go.name.StartsWith("Arma:") || go.name.StartsWith("Material:")))
                    Object.Destroy(go);
            yield return null;
        }

        private static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        /// <summary>Largest angle between the hero's bones and the clip's pose at the layer's current time.</summary>
        private static float PoseError(CharacterRig rig)
        {
            var direct = rig.Direct;
            var skeleton = direct.Skeleton;
            var rotations = new Quaternion[skeleton.Bones.Count];
            for (var i = 0; i < rotations.Length; i++) rotations[i] = skeleton.Bones[i].localRotation;
            direct.Current.Sample(direct.Time, direct.Current == rig.Sprint, rotations, out _);
            var worst = 0f;
            for (var i = 0; i < rotations.Length; i++)
                worst = Mathf.Max(worst, Quaternion.Angle(rotations[i], skeleton.Bones[i].localRotation));
            return worst;
        }

        [UnityTest]
        public IEnumerator SprintDrawComboAndSheathe()
        {
            var combat = Object.FindAnyObjectByType<PlayerCombat>();
            var motor = combat.Motor;
            var rig = combat.Rig;
            var log = new StringBuilder();
            Assert.That(rig.Direct, Is.Not.Null);
            Assert.That(combat.UsesSheath, Is.True, "the hero has the draw and sheathe clips");
            Assert.That(combat.Sword, Is.EqualTo(PlayerCombat.SwordState.Sheathed));
            var blade = rig.GetComponentsInChildren<Transform>(true).First(t => t.name == "Empunhadura");
            Assert.That(blade.parent.name, Is.EqualTo("Bainha"), "the sword starts in the waist sheath");
            // Where the sheathed sword sits on the body (character space: x right, y up, z forward, meters from the feet).
            var body = combat.transform;
            var parts = blade.GetComponentsInChildren<MeshFilter>(true);
            var hiltAt = body.InverseTransformPoint(blade.position);
            var far = hiltAt;
            foreach (var mf in parts)
            {
                var b = mf.sharedMesh.bounds;
                for (var c = 0; c < 8; c++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                    var p = body.InverseTransformPoint(mf.transform.TransformPoint(corner));
                    if ((p - hiltAt).sqrMagnitude > (far - hiltAt).sqrMagnitude) far = p;
                }
            }

            log.AppendLine($"sheathed: hilt ({F(hiltAt.x)}, {F(hiltAt.y)}, {F(hiltAt.z)}) tip ({F(far.x)}, {F(far.y)}, {F(far.z)}) m");

            // Sprint.
            PlayerInputReader.ScriptedMove = new Vector2(0f, 1f);
            PlayerInputReader.ScriptedSprint = true;
            yield return new WaitForSeconds(1.2f);
            Assert.That(motor.Sprinting, Is.True);
            Assert.That(rig.Direct.Current, Is.EqualTo(rig.Sprint));
            Assert.That(rig.Direct.Weight, Is.EqualTo(1f));
            yield return null;
            var sprintError = PoseError(rig);
            log.AppendLine($"sprint: pose error {F(sprintError)} deg");
            Assert.That(sprintError, Is.LessThan(0.05f), "sprint bones are the clip's");
            PlayerInputReader.ScriptedMove = null;
            PlayerInputReader.ScriptedSprint = false;
            yield return new WaitForSeconds(1.2f);
            Assert.That(rig.Direct.Weight, Is.EqualTo(0f), "stopping hands the body back");

            // First press: the draw, then the first swing by itself.
            PlayerInputReader.ScriptedAttackPress = true;
            yield return null;
            yield return null;
            Assert.That(combat.CurrentClip, Is.EqualTo(PlayerCombat.ClipAction.Draw));
            Assert.That(rig.Direct.Current, Is.EqualTo(rig.DrawSword));
            var lastBlade = blade.position;
            var biggestJump = 0f;
            var drawStart = Time.time;
            while (combat.CurrentClip == PlayerCombat.ClipAction.Draw && Time.time - drawStart < 5f)
            {
                yield return null;
                if (rig.Direct.Weight >= 1f)
                {
                    var err = PoseError(rig);
                    Assert.That(err, Is.LessThan(0.05f), "draw bones are the clip's");
                }

                biggestJump = Mathf.Max(biggestJump, (blade.position - lastBlade).magnitude);
                lastBlade = blade.position;
            }

            log.AppendLine($"draw: grab at {F(combat.DrawGrabTime())} s, biggest blade step per frame {F(biggestJump)} m");
            Assert.That(combat.Sword, Is.EqualTo(PlayerCombat.SwordState.Drawn));
            Assert.That(combat.CurrentClip, Is.EqualTo(PlayerCombat.ClipAction.Attack1), "the queued swing follows the draw");
            Assert.That(blade.parent.name, Is.Not.EqualTo("Bainha"), "the sword is in the hand");
            Assert.That(motor.Rooted(), Is.True, "the hero stands while the swing plays");

            // Press again during the first swing: the second follows it.
            yield return new WaitForSeconds(0.4f);
            PlayerInputReader.ScriptedAttackPress = true;
            yield return new WaitForSeconds(rig.BasicAttack1.Duration - 0.2f);
            Assert.That(combat.CurrentClip, Is.EqualTo(PlayerCombat.ClipAction.Attack2));
            yield return new WaitForSeconds(0.3f);
            Assert.That(rig.Direct.Current, Is.EqualTo(rig.BasicAttack2));
            var attackError = PoseError(rig);
            log.AppendLine($"attack 2: pose error {F(attackError)} deg");
            Assert.That(attackError, Is.LessThan(0.05f));
            yield return new WaitForSeconds(rig.BasicAttack2.Duration);
            Assert.That(combat.CurrentClip, Is.EqualTo(PlayerCombat.ClipAction.None), "no press, the combo ends");

            // The sheathe button puts the sword back.
            yield return new WaitForSeconds(0.3f);
            PlayerInputReader.ScriptedSheathePress = true;
            yield return null;
            yield return null;
            Assert.That(combat.CurrentClip, Is.EqualTo(PlayerCombat.ClipAction.Sheathe));
            yield return new WaitForSeconds(rig.SheatheSword.Duration + 0.2f);
            Assert.That(combat.Sword, Is.EqualTo(PlayerCombat.SwordState.Sheathed));
            Assert.That(blade.parent.name, Is.EqualTo("Bainha"));
            log.AppendLine($"sheathe: release at {F(combat.SheatheReleaseTime())} s");

            var folder = Path.Combine(Application.dataPath, "..", "Logs", "animacoes");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "fluxo.txt"), log.ToString());
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Zeldexile.Characters;
using Zeldexile.Domain;
using Zeldexile.Domain.Regions;
using Zeldexile.Gameplay;
using Zeldexile.Player;
using Zeldexile.World.Atmosphere;

namespace Zeldexile.Tests
{
    /// <summary>
    /// Plays the hero with scripted stick input (the real PlayerMotor, camera and animator) along the Circuito do Vale
    /// and on the island, and measures how the movement feels: time to speed, stopping, how the body turns, whether
    /// the planted foot slides, floats or sinks, and how far the pose jumps between frames. Each run saves a CSV per
    /// frame, a summary and the frames of a video to Logs/movimento/&lt;tag&gt; (tag from ZX_TAG, default "atual").
    /// </summary>
    public sealed class MovementFeelTests
    {
        private const int Fps = 30;

        private static string Tag => string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("ZX_TAG"))
            ? "atual" : System.Environment.GetEnvironmentVariable("ZX_TAG");

        private static string Dir(string sub)
        {
            var dir = Path.Combine(Application.dataPath, "..", "Logs", "movimento", Tag, sub);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            return dir;
        }

        [TearDown]
        public void ClearScript()
        {
            PlayerInputReader.ScriptedMove = null;
            PlayerInputReader.ScriptedSprint = false;
            PlayerInputReader.ScriptedLook = Vector2.zero;
            Time.captureFramerate = 0;
        }

        /// <summary>One stretch of the drive: what the stick does for how long.</summary>
        private struct Leg
        {
            public string Name;
            public float Seconds;
            public bool Move;
            public bool Sprint;
            /// <summary>Degrees added to the heading toward the path (180 = turn back).</summary>
            public float Offset;
            /// <summary>Stick deflection (0..1).</summary>
            public float Stick;
        }

        private static readonly Leg[] Drive =
        {
            new Leg { Name = "parado", Seconds = 1.5f },
            new Leg { Name = "andar", Seconds = 5f, Move = true, Stick = 1f },
            new Leg { Name = "parar", Seconds = 1.5f },
            new Leg { Name = "correr", Seconds = 6f, Move = true, Sprint = true, Stick = 1f },
            new Leg { Name = "curva", Seconds = 3f, Move = true, Sprint = true, Stick = 1f, Offset = 70f },
            new Leg { Name = "andar2", Seconds = 3f, Move = true, Stick = 1f },
            new Leg { Name = "meia-volta", Seconds = 2.5f, Move = true, Stick = 1f, Offset = 180f },
            new Leg { Name = "devagar", Seconds = 3f, Move = true, Stick = 0.5f },
            new Leg { Name = "parar2", Seconds = 2f },
        };

        [UnityTest]
        public IEnumerator DriveTheValleyCircuit()
        {
            if (RegionWorld.Current == null)
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("ValeInicial");
                for (var i = 0; i < 5 && RegionWorld.Current == null; i++) yield return null;
            }

            AtmosphereController.Current.Set(10f, WeatherKind.Clear);
            var world = RegionWorld.Current;
            var line = world.Terrain.PathLineOf(0);
            var motor = world.Player.GetComponent<PlayerMotor>();
            var start = line.At(20f, out _);
            Teleport(motor, world.WorldAt(start.u, start.v, 0.2f));
            float s = 20f;
            yield return Run(motor, "vale", () =>
            {
                // Follow the circuit: the target runs ahead of the hero along the line.
                for (var k = 0; k < 40; k++)
                {
                    var p = line.At(s, out _);
                    var flat = world.WorldAt(p.u, p.v) - motor.transform.position;
                    flat.y = 0f;
                    if (flat.magnitude > 3f) break;
                    s += 0.5f;
                }

                var ahead = line.At(s + 3f, out _);
                var to = world.WorldAt(ahead.u, ahead.v) - motor.transform.position;
                to.y = 0f;
                return to;
            });
        }

        [UnityTest]
        public IEnumerator DriveTheIsland()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("Prototype");
            PlayerMotor motor = null;
            for (var i = 0; i < 10 && motor == null; i++)
            {
                yield return null;
                motor = Object.FindAnyObjectByType<PlayerMotor>();
            }

            Assert.That(motor, Is.Not.Null);
            if (AtmosphereController.Current != null) AtmosphereController.Current.Set(10f, WeatherKind.Clear);
            var heading = motor.transform.forward;
            heading.y = 0f;
            yield return Run(motor, "ilha", () => heading);
        }

        private static void Teleport(PlayerMotor motor, Vector3 position)
        {
            var cc = motor.GetComponent<CharacterController>();
            cc.enabled = false;
            motor.transform.position = position;
            cc.enabled = true;
            Physics.SyncTransforms();
        }

        private static IEnumerator Run(PlayerMotor motor, string name, System.Func<Vector3> path)
        {
            var frames = Dir(name);
            var animator = motor.GetComponent<CharacterAnimator>();
            var rig = animator.Rig;
            var cam = motor.CameraRig;
            var character = motor.GetComponent<PlayerCharacter>();
            var camera = cam.GetComponent<Camera>();
            for (var i = 0; i < 20; i++) yield return null;

            Time.captureFramerate = Fps;
            var csv = new StringBuilder("t;trecho;estado;x;y;z;vel;yaw;yaw_vel;erro_frente;giro_s;pe_e_alt;pe_d_alt;pe_e_vel;pe_d_vel;apoio_desliza;pose_salto\n");
            var labels = new StringBuilder();
            var joints = new[] { rig.Hips, rig.Torso, rig.LegL, rig.LegR, rig.KneeL, rig.KneeR, rig.ArmL, rig.ArmR, rig.AnkleL, rig.AnkleR };
            var lastRot = new Quaternion[joints.Length];
            for (var j = 0; j < joints.Length; j++) lastRot[j] = joints[j].rotation;
            Vector3? lastL = null, lastR = null;
            float lastHL = 0f, lastHR = 0f;
            var lastYaw = motor.transform.eulerAngles.y;
            var summary = new Dictionary<string, Stats>();
            var t = 0f;
            var n = 0;
            var stick = Vector3.forward;

            foreach (var leg in Drive)
            {
                var stats = summary[leg.Name] = new Stats();
                var legStart = t;
                var decided = false;
                for (var time = 0f; time < leg.Seconds; time += 1f / Fps)
                {
                    character.Stamina.Refill();
                    var dir = path();
                    if (dir.sqrMagnitude < 0.01f) dir = motor.transform.forward;
                    // The heading is chosen at the start of the stretch for turns, so the hero makes one clear turn.
                    if (!decided || leg.Offset == 0f) stick = Quaternion.Euler(0f, leg.Offset, 0f) * dir.normalized;
                    decided = true;
                    var camYaw = cam.Yaw;
                    var local = Quaternion.Euler(0f, -camYaw, 0f) * stick;
                    PlayerInputReader.ScriptedMove = leg.Move ? new Vector2(local.x, local.z).normalized * leg.Stick : Vector2.zero;
                    PlayerInputReader.ScriptedSprint = leg.Sprint;
                    // The camera drifts behind the hero, a little to the side, as a player would keep it.
                    var wantYaw = motor.transform.eulerAngles.y + 20f;
                    PlayerInputReader.ScriptedLook = new Vector2(Mathf.DeltaAngle(camYaw, wantYaw) * 0.04f, 0f);

                    yield return null;
                    t += 1f / Fps;
                    var dt = 1f / Fps;

                    var v = motor.Velocity;
                    var flat = new Vector3(v.x, 0f, v.z);
                    var speed = flat.magnitude;
                    var yaw = motor.transform.eulerAngles.y;
                    var turnRate = Mathf.DeltaAngle(lastYaw, yaw) / dt;
                    lastYaw = yaw;
                    var velYaw = speed > 0.2f ? Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg : yaw;
                    var facingError = speed > 0.5f ? Mathf.Abs(Mathf.DeltaAngle(yaw, velYaw)) : 0f;
                    var footL = Lowest(rig, rig.AnkleL, rig.ToesL);
                    var footR = Lowest(rig, rig.AnkleR, rig.ToesR);
                    var hL = Height(footL);
                    var hR = Height(footR);
                    var sL = lastL.HasValue ? Flat(footL - lastL.Value).magnitude / dt : 0f;
                    var sR = lastR.HasValue ? Flat(footR - lastR.Value).magnitude / dt : 0f;
                    lastL = footL;
                    lastR = footR;
                    // The planted foot: the lower one, when the other is clearly up.
                    // Planted: the lower foot, clearly below the other and not rising or falling.
                    var slide = float.NaN;
                    var vL = Mathf.Abs(hL - lastHL) / dt;
                    var vR = Mathf.Abs(hR - lastHR) / dt;
                    lastHL = hL;
                    lastHR = hR;
                    if (motor.IsGrounded && Mathf.Abs(hL - hR) > 0.04f && (hL < hR ? vL : vR) < 0.3f) slide = hL < hR ? sL : sR;
                    var pop = 0f;
                    for (var j = 0; j < joints.Length; j++)
                    {
                        // Relative to the body, so turning the whole hero is not a pose jump.
                        var rel = Quaternion.Inverse(motor.transform.rotation) * joints[j].rotation;
                        var relLast = Quaternion.Inverse(Quaternion.Euler(0f, yaw - turnRate * dt, 0f)) * lastRot[j];
                        pop = Mathf.Max(pop, Quaternion.Angle(rel, relLast));
                        lastRot[j] = joints[j].rotation;
                    }

                    stats.Add(t - legStart, speed, facingError, Mathf.Abs(turnRate), Mathf.Min(hL, hR), slide, pop, motor.IsGrounded);
                    var p = motor.transform.position;
                    csv.AppendLine(string.Join(";", F(t), leg.Name, motor.Current, F(p.x), F(p.y), F(p.z), F(speed), F(yaw), F(velYaw),
                        F(facingError), F(turnRate), F(hL), F(hR), F(sL), F(sR), F(slide), F(pop)));

                    Save(camera, Path.Combine(frames, $"f{n:0000}.jpg"));
                    labels.AppendLine($"{Tag} — {name}: {leg.Name}  {speed:0.0} m/s");
                    n++;
                }
            }

            Time.captureFramerate = 0;
            PlayerInputReader.ScriptedMove = null;
            PlayerInputReader.ScriptedSprint = false;
            PlayerInputReader.ScriptedLook = Vector2.zero;

            File.WriteAllText(Path.Combine(frames, "quadros.csv"), csv.ToString());
            File.WriteAllText(Path.Combine(frames, "labels.txt"), labels.ToString());
            var clips = $"clipes: andar {rig.Walk?.Speed:0.00} m/s em {rig.Walk?.Duration:0.00} s, correr {rig.Run?.Speed:0.00} m/s em {rig.Run?.Duration:0.00} s";
            var report = new StringBuilder($"{Tag} {name}\n{clips}\ntrecho;vel_media;vel_max;t90;erro_frente_medio;giro_max;pe_alt_media;pe_alt_min;pe_alt_max;desliza_mediana;desliza_p90;pose_salto_p99;no_chao\n");
            foreach (var pair in summary) report.AppendLine(pair.Key + ";" + pair.Value);
            File.WriteAllText(Path.Combine(frames, "resumo.csv"), report.ToString());
            TestContext.WriteLine(report.ToString());
            Assert.That(n, Is.GreaterThan(100));
        }

        private sealed class Stats
        {
            private readonly List<float> _speed = new List<float>(), _time = new List<float>(), _facing = new List<float>();
            private readonly List<float> _feet = new List<float>(), _slide = new List<float>(), _pop = new List<float>();
            private float _turnMax;
            private int _grounded, _count;

            public void Add(float time, float speed, float facing, float turn, float foot, float slide, float pop, bool grounded)
            {
                _time.Add(time);
                _speed.Add(speed);
                if (speed > 0.5f) _facing.Add(facing);
                _turnMax = Mathf.Max(_turnMax, turn);
                if (grounded)
                {
                    _feet.Add(foot);
                    _grounded++;
                }

                if (!float.IsNaN(slide)) _slide.Add(slide);
                _pop.Add(pop);
                _count++;
            }

            public override string ToString()
            {
                var max = 0f;
                foreach (var s in _speed) max = Mathf.Max(max, s);
                var t90 = float.NaN;
                for (var i = 0; i < _speed.Count; i++)
                    if (_speed[i] >= max * 0.9f) { t90 = _time[i]; break; }
                return string.Join(";", F(Mean(_speed)), F(max), F(t90), F(Mean(_facing)), F(_turnMax), F(Mean(_feet)),
                    F(Q(_feet, 0f)), F(Q(_feet, 1f)), F(Q(_slide, 0.5f)), F(Q(_slide, 0.9f)), F(Q(_pop, 0.99f)), $"{_grounded}/{_count}");
            }

            private static float Mean(List<float> l)
            {
                if (l.Count == 0) return float.NaN;
                var sum = 0f;
                foreach (var x in l) sum += x;
                return sum / l.Count;
            }

            private static float Q(List<float> l, float q)
            {
                if (l.Count == 0) return float.NaN;
                var sorted = new List<float>(l);
                sorted.Sort();
                return sorted[Mathf.Clamp(Mathf.RoundToInt(q * (sorted.Count - 1)), 0, sorted.Count - 1)];
            }
        }

        /// <summary>The lowest contact point (heel, sole, ball, toe tip) of one foot, in world space.</summary>
        private static Vector3 Lowest(CharacterRig rig, Transform ankle, Transform toes)
        {
            var best = new Vector3(0f, float.MaxValue, 0f);
            foreach (var (bone, local) in rig.Contacts)
            {
                if (bone != ankle && bone != toes) continue;
                var w = bone.TransformPoint(local);
                if (w.y < best.y) best = w;
            }

            return best.y == float.MaxValue ? ankle.position : best;
        }

        /// <summary>Height of a point above the ground under it (negative: sunk into it).</summary>
        private static float Height(Vector3 point)
        {
            return Physics.Raycast(point + Vector3.up * 1.5f, Vector3.down, out var hit, 4f, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore)
                ? point.y - hit.point.y
                : float.NaN;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private static string F(float x) => float.IsNaN(x) ? "" : x.ToString("0.###", CultureInfo.InvariantCulture);

        private static void Save(Camera camera, string path)
        {
            var rt = RenderTexture.GetTemporary(960, 540, 24);
            var previous = camera.targetTexture;
            camera.targetTexture = rt;
            if (RegionWorld.Current != null) RegionWorld.Current.RenderFor(camera);
            else camera.Render();
            camera.targetTexture = previous;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(path, tex.EncodeToJPG(85));
            Object.Destroy(tex);
            RenderTexture.ReleaseTemporary(rt);
        }
    }
}

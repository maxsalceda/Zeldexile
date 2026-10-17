using System.Collections;
using System.Globalization;
using System.IO;
using System.Reflection;
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
    /// Pictures of the start region rendered by the engine, saved to Logs/vale: every documented camera spot of the
    /// region data (entrance, valley, orchard by the lake, bridge, woods, shallow shore, lookout, edge...), a top view
    /// of the built terrain, the same view at sunset, and a short walk with the hero for the exploration video.
    /// </summary>
    public sealed class RegionCaptureTests
    {
        private sealed class WalkSource : IAnimationSource
        {
            public Locomotion Locomotion => Locomotion.Grounded;
            public Vector3 Velocity { get; set; }
            public float MaxGroundSpeed => 7f;
        }

        private static string Dir
        {
            get
            {
                var dir = Path.Combine(Application.dataPath, "..", "Logs", "vale");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        private static IEnumerator Load()
        {
            if (RegionWorld.Current == null)
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("ValeInicial");
                for (var i = 0; i < 5 && RegionWorld.Current == null; i++) yield return null;
            }

            AtmosphereController.Current.Set(10f, WeatherKind.Clear);
            for (var i = 0; i < 10; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator RenderDocumentedShots()
        {
            yield return Load();
            var world = RegionWorld.Current;
            var shot = MakeShotCamera();
            var index = new StringBuilder("id;nome;u;v;altura_sobre_chao;yaw;pitch;arquivo\n");
            foreach (var s in world.Data.shots)
            {
                Place(shot, world, s);
                Save(world, shot, $"vale-{s.id}.png", 1600, 900);
                index.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0};{1};{2};{3};{4};{5};{6};vale-{0}.png", s.id, s.name, s.u, s.v, s.above, s.yaw, s.pitch));
                yield return null;
            }

            File.WriteAllText(Path.Combine(Dir, "capturas.csv"), index.ToString());

            // Gameplay view: the hero at the entrance, through the real third-person camera.
            var hero = world.Player.gameObject;
            var controller = hero.GetComponent<CharacterController>();
            controller.enabled = false;
            hero.transform.SetPositionAndRotation(world.SpawnPoint, Quaternion.Euler(0f, world.Data.spawn.yaw, 0f));
            controller.enabled = true;
            var rig = Camera.main.GetComponent<ThirdPersonCamera>();
            typeof(ThirdPersonCamera).GetField("_yaw", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(rig, world.Data.spawn.yaw);
            for (var i = 0; i < 10; i++) yield return null;
            Save(world, Camera.main, "vale-jogo-entrada.png", 1600, 900);

            // The valley at sunset from the lookout.
            var lookout = world.Data.shots[1];
            AtmosphereController.Current.Set(17.6f, WeatherKind.Clear);
            yield return null;
            Place(shot, world, lookout);
            Save(world, shot, "vale-por-do-sol.png", 1600, 900);
            AtmosphereController.Current.Set(10f, WeatherKind.Clear);
            Object.Destroy(shot.gameObject);
        }

        [UnityTest]
        public IEnumerator RenderTopView()
        {
            yield return Load();
            var world = RegionWorld.Current;
            var cam = MakeShotCamera();
            // A narrow perspective from very high up: nearly orthographic, and the water shaders (which read
            // perspective depth) still render correctly.
            cam.fieldOfView = 12f;
            var height = 500f / Mathf.Tan(6f * Mathf.Deg2Rad);
            cam.nearClipPlane = 50f;
            cam.farClipPlane = height + 400f;
            cam.transform.SetPositionAndRotation(world.Origin + new Vector3(500f, height, 500f), Quaternion.Euler(90f, 0f, 0f));
            // No haze from straight above.
            var atmosphere = AtmosphereController.Current;
            var fog = atmosphere.FogDistanceScale;
            atmosphere.FogDistanceScale = 100f;
            atmosphere.Set(12f, WeatherKind.Clear);
            yield return null;
            Save(world, cam, "vale-vista-superior.png", 2048, 2048);
            atmosphere.FogDistanceScale = fog;
            atmosphere.Set(10f, WeatherKind.Clear);
            Object.Destroy(cam.gameObject);
        }

        /// <summary>Frames of the hero walking the main circuit at walking speed, for the exploration video.</summary>
        [UnityTest]
        public IEnumerator RecordExplorationWalk()
        {
            yield return Load();
            var world = RegionWorld.Current;
            var frames = Path.Combine(Dir, "passeio");
            if (Directory.Exists(frames)) Directory.Delete(frames, true);
            Directory.CreateDirectory(frames);

            var player = world.Player.gameObject;
            player.GetComponent<PlayerMotor>().enabled = false;
            player.GetComponent<CharacterController>().enabled = false;
            var animator = player.GetComponent<CharacterAnimator>();
            var source = new WalkSource();
            animator.Source = source;
            var rig = Camera.main.GetComponent<ThirdPersonCamera>();
            var yawField = typeof(ThirdPersonCamera).GetField("_yaw", BindingFlags.NonPublic | BindingFlags.Instance);
            var pitchField = typeof(ThirdPersonCamera).GetField("_pitch", BindingFlags.NonPublic | BindingFlags.Instance);

            // Three stretches of the main circuit: the entrance into the orchard, the central bridge, the north shore.
            var line = world.Terrain.PathLineOf(0);
            var stretches = new[] { (0f, 110f), (480f, 580f), (1190f, 1290f) };
            Time.captureFramerate = 24;
            var n = 0;
            var labels = new StringBuilder();
            const float speed = 4f;
            foreach (var (from, to) in stretches)
            {
                var yaw = 0f;
                var first = true;
                for (var s = from; s < to; s += speed / 24f)
                {
                    var p = line.At(s, out _);
                    var ahead = line.At(s + 3f, out _);
                    var dir = new Vector3(ahead.u - p.u, 0f, ahead.v - p.v).normalized;
                    var target = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                    yaw = first ? target : Mathf.LerpAngle(yaw, target, 0.08f);
                    first = false;
                    player.transform.SetPositionAndRotation(world.WorldAt(p.u, p.v), Quaternion.Euler(0f, yaw, 0f));
                    source.Velocity = dir * speed;
                    yawField?.SetValue(rig, Mathf.LerpAngle((float)yawField.GetValue(rig), yaw + 18f, 0.06f));
                    pitchField?.SetValue(rig, 9f);
                    yield return null;
                    Save(world, Camera.main, Path.Combine("passeio", $"f{n:0000}.jpg"), 1280, 720, true);
                    labels.AppendLine($"Vale dos Pomares — {(from < 100f ? "entrada e pomar" : from < 1000f ? "ponte central" : "margem norte do lago")}");
                    n++;
                }
            }

            Time.captureFramerate = 0;
            File.WriteAllText(Path.Combine(frames, "labels.txt"), labels.ToString());
            player.GetComponent<CharacterController>().enabled = true;
            player.GetComponent<PlayerMotor>().enabled = true;
            Assert.That(n, Is.GreaterThan(200));
        }

        private static Camera MakeShotCamera()
        {
            var shot = Object.Instantiate(Camera.main.gameObject).GetComponent<Camera>();
            foreach (var b in shot.GetComponents<MonoBehaviour>())
                if (b.GetType().Name != "PostProcessLayer")
                    Object.Destroy(b);
            foreach (var l in shot.GetComponents<AudioListener>()) Object.Destroy(l);
            shot.enabled = false;
            shot.farClipPlane = 3000f;
            return shot;
        }

        private static void Place(Camera cam, RegionWorld world, ShotData s)
        {
            cam.fieldOfView = s.fov;
            cam.transform.SetPositionAndRotation(world.WorldAt(s.u, s.v, s.above), Quaternion.Euler(s.pitch, s.yaw, 0f));
        }

        private static void Save(RegionWorld world, Camera camera, string fileName, int width, int height, bool jpg = false)
        {
            var rt = new RenderTexture(width, height, 24);
            var previous = camera.targetTexture;
            camera.targetTexture = rt;
            world.RenderFor(camera);
            camera.targetTexture = previous;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(Dir, fileName), jpg ? tex.EncodeToJPG(88) : tex.EncodeToPNG());
            Object.Destroy(tex);
            rt.Release();
        }
    }
}

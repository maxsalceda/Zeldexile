using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Zeldexile.Characters;
using Zeldexile.Domain;
using Zeldexile.Gameplay;
using Zeldexile.World.Atmosphere;

namespace Zeldexile.Tests
{
    /// <summary>
    /// Renders preview images to Logs/ so the art direction can be checked without opening the editor:
    /// the island from the player camera, and a sheet with every locomotion pose.
    /// </summary>
    public sealed class VisualPreviewTests
    {
        private sealed class FixedSource : IAnimationSource
        {
            public Locomotion Locomotion { get; set; }
            public Vector3 Velocity { get; set; }
            public float MaxGroundSpeed => 7.5f;
        }

        [UnityTest]
        public IEnumerator RenderIslandAndPoseSheet()
        {
            // Load the real scene so previews include its camera post-processing.
            UnityEngine.SceneManagement.SceneManager.LoadScene("Prototype");
            for (var i = 0; i < 20; i++) yield return null;
            var world = GameObject.Find("Zeldexile");

            Save(Camera.main, "preview-island.png");

            // Overview of the relief and a close look at the lake shore, from a camera that copies the main one.
            var shot = Object.Instantiate(Camera.main.gameObject).GetComponent<Camera>();
            foreach (var b in shot.GetComponents<MonoBehaviour>()) Object.Destroy(b);
            shot.enabled = false;
            shot.transform.SetPositionAndRotation(new Vector3(-30f, 50f, -72f), Quaternion.Euler(30f, 35f, 0f));
            Save(shot, "preview-overview.png");
            shot.transform.SetPositionAndRotation(new Vector3(8f, 7f, -32f), Quaternion.Euler(14f, 50f, 0f));
            Save(shot, "preview-lake.png");
            shot.transform.SetPositionAndRotation(new Vector3(73f, 4.5f, -17f), Quaternion.Euler(10f, 22f, 0f));
            Save(shot, "preview-gate.png");

            // Same island at other times and weathers, from the overview angle.
            shot.transform.SetPositionAndRotation(new Vector3(-30f, 50f, -72f), Quaternion.Euler(30f, 35f, 0f));
            var atmosphere = AtmosphereController.Current;
            atmosphere.Set(17.7f, WeatherKind.Clear);
            yield return null;
            Save(shot, "preview-sunset.png");
            atmosphere.Set(23f, WeatherKind.Clear);
            yield return null;
            Save(shot, "preview-night.png");
            atmosphere.Set(12f, WeatherKind.Storm);
            for (var i = 0; i < 30; i++) yield return null;
            Save(Camera.main, "preview-storm.png");
            atmosphere.Set(10f, WeatherKind.Clear);
            Object.Destroy(shot.gameObject);

            // Hero sheets, compared side by side with output/imagegen/aventureiro-anime-seis-poses.png.
            var sheet = new GameObject("Folha de poses");
            sheet.transform.position = new Vector3(0f, 200f, 0f);
            var backdrop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backdrop.transform.SetParent(sheet.transform, false);
            backdrop.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            backdrop.transform.localScale = new Vector3(40f, 1f, 40f);
            backdrop.GetComponent<Renderer>().sharedMaterial = World.Palette.Solid(new Color(0.86f, 0.86f, 0.84f));
            var cam = new GameObject("Câmera de poses").AddComponent<Camera>();
            cam.enabled = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.93f, 0.93f, 0.92f);
            cam.fieldOfView = 30f;

            // 1) The reference's six poses, in its 3 x 2 layout: rest 3/4, walking profile, raised shield,
            //    sword extension, crouch, rear 3/4. Character yaw 180 faces the camera.
            var mock = new (Locomotion loco, Vector3 velocity, ActionKind action, float at, string stance, float yaw)[]
            {
                (Locomotion.Grounded, Vector3.zero, ActionKind.None, 0f, "armed", 150f),
                (Locomotion.Grounded, new Vector3(0f, 0f, 4f), ActionKind.None, 0f, "armed", 90f),
                (Locomotion.Grounded, Vector3.zero, ActionKind.None, 0f, "guard", 150f),
                (Locomotion.Grounded, Vector3.zero, ActionKind.Thrust, 0.62f, "armed", 235f),
                (Locomotion.Grounded, Vector3.zero, ActionKind.Pickup, 0.5f, "armed", 160f),
                (Locomotion.Grounded, Vector3.zero, ActionKind.None, 0f, "armed", 330f),
            };
            var heroes = new GameObject[mock.Length];
            for (var i = 0; i < mock.Length; i++)
            {
                var col = i % 3;
                var row = i / 3;
                heroes[i] = Hero(sheet.transform, new Vector3(-3.6f + col * 3.6f, row == 0 ? 2.5f : -0.3f, 0f), mock[i].yaw,
                    mock[i].loco, mock[i].velocity, mock[i].action, mock[i].at, mock[i].stance);
            }

            // Orthographic, nearly level, like a character sheet; rows are spread in depth and lifted apart.
            // A plain sheet with no floor: the reference has no ground either.
            backdrop.SetActive(false);
            cam.orthographic = true;
            cam.orthographicSize = 3.15f;
            cam.transform.position = sheet.transform.position + new Vector3(0f, 2.0f, -14f);
            cam.transform.rotation = Quaternion.identity;
            for (var i = 0; i < 40; i++) yield return null;
            Save(cam, "preview-hero-mockup.png", 1536, 1024);
            foreach (var h in heroes) Object.Destroy(h);
            cam.orthographic = false;
            backdrop.SetActive(true);

            // 2) Turnaround at rest with the gear in hand: front, 3/4, side, back.
            var yaws = new[] { 180f, 135f, 90f, 0f };
            heroes = new GameObject[yaws.Length];
            for (var i = 0; i < yaws.Length; i++)
                heroes[i] = Hero(sheet.transform, new Vector3(-3.3f + i * 2.2f, 0f, 0f), yaws[i], Locomotion.Grounded,
                    Vector3.zero, ActionKind.None, 0f, "armed");
            cam.transform.position = sheet.transform.position + new Vector3(0f, 1.15f, -8.6f);
            cam.transform.rotation = Quaternion.Euler(2f, 0f, 0f);
            for (var i = 0; i < 40; i++) yield return null;
            Save(cam, "preview-hero-turnaround.png", 1600, 700);
            foreach (var h in heroes) Object.Destroy(h);

            // 3) Close-up of the face and hair, three-quarter.
            var face = Hero(sheet.transform, Vector3.zero, 160f, Locomotion.Grounded, Vector3.zero, ActionKind.None, 0f, "");
            cam.transform.position = sheet.transform.position + new Vector3(0.05f, 1.62f, -1.25f);
            cam.transform.rotation = Quaternion.Euler(2f, 0f, 0f);
            for (var i = 0; i < 20; i++) yield return null;
            Save(cam, "preview-hero-face.png", 900, 900);
            Object.Destroy(face);

            // 4) Every remaining action, for the animation review (plus a creature for scale).
            var actions = new (Locomotion loco, Vector3 velocity, ActionKind action, float at, string stance)[]
            {
                (Locomotion.Grounded, new Vector3(0f, 0f, 7f), ActionKind.None, 0f, "armed"),
                (Locomotion.Grounded, Vector3.zero, ActionKind.SlashLight, 0.55f, "armed"),
                (Locomotion.Grounded, Vector3.zero, ActionKind.SlashHeavy, 0.55f, "armed"),
                (Locomotion.Grounded, Vector3.zero, ActionKind.Throw, 0.5f, "armed"),
                (Locomotion.Grounded, Vector3.zero, ActionKind.Parry, 0.3f, "guard"),
                (Locomotion.Grounded, Vector3.zero, ActionKind.None, 0f, "aim"),
                (Locomotion.Grounded, Vector3.zero, ActionKind.Hurt, 0.2f, "armed"),
                (Locomotion.Grounded, Vector3.zero, ActionKind.None, 0f, "kneel"),
                (Locomotion.Airborne, new Vector3(0f, 3f, 2f), ActionKind.None, 0f, ""),
                (Locomotion.Climbing, new Vector3(0f, 2f, 0f), ActionKind.None, 0f, ""),
                (Locomotion.Gliding, new Vector3(0f, -1f, 6f), ActionKind.None, 0f, ""),
                (Locomotion.Swimming, new Vector3(0f, 0f, 2f), ActionKind.None, 0f, ""),
            };
            heroes = new GameObject[actions.Length + 1];
            for (var i = 0; i < actions.Length; i++)
                heroes[i] = Hero(sheet.transform, new Vector3(-7.5f + (i % 6) * 3f, actions[i].loco == Locomotion.Swimming ? 0.6f : 0f, i < 6 ? 3.5f : 0f),
                    125f, actions[i].loco, actions[i].velocity, actions[i].action, actions[i].at, actions[i].stance);
            var creature = new GameObject("Criatura");
            creature.transform.SetParent(sheet.transform, false);
            creature.transform.localPosition = new Vector3(9f, 0f, 1.5f);
            creature.transform.localRotation = Quaternion.Euler(0f, 210f, 0f);
            var ca = creature.AddComponent<CharacterAnimator>();
            ca.Rig = CharacterRig.Build(creature.transform, RigStyle.ForTier(Domain.EnemyTier.Blue));
            ca.Source = new FixedSource { Locomotion = Locomotion.Grounded, Velocity = Vector3.zero };
            heroes[actions.Length] = creature;
            cam.transform.position = sheet.transform.position + new Vector3(0.6f, 6.4f, -14.5f);
            cam.transform.rotation = Quaternion.Euler(18f, 0f, 0f);
            cam.fieldOfView = 46f;
            for (var i = 0; i < 40; i++) yield return null;
            Save(cam, "preview-poses.png");

            Object.Destroy(cam.gameObject);
            Object.Destroy(sheet);
            Object.Destroy(cam.gameObject);
            Object.Destroy(world);
            foreach (var name in new[] { "Mundo", "Jogador", "HUD", "Efeitos", "Loot", "Câmera", "Sol", "Atmosfera", "Game feel", "Grama densa", "Clima" })
            {
                var go = GameObject.Find(name);
                if (go != null) Object.Destroy(go);
            }
        }

        /// <summary>
        /// Motion review, seen from the side: one row of frames each for the walk (1.5 m/s), the run (7.5 m/s), the
        /// flat slash, the overhead slash and the heavy chop. Saved as Logs/preview-motion.png.
        /// </summary>
        [UnityTest]
        public IEnumerator RenderMotionSheet()
        {
            const float WalkSpeed = 1.5f;
            const int frames = 8, cellW = 300, cellH = 380;
            var rows = new[] { "walk", "run", "slash", "overhead", "heavy" };
            var sheet = new Texture2D(frames * cellW, rows.Length * cellH, TextureFormat.RGB24, false);
            var stage = new GameObject("Folha de movimento");
            stage.transform.position = new Vector3(0f, 300f, 0f);
            var light = new GameObject("Luz").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            // Earlier tests may leave their sun in the scene: only light the stage when there is none.
            light.enabled = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Length == 1;
            var cam = new GameObject("Câmera de movimento").AddComponent<Camera>();
            cam.enabled = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.93f, 0.93f, 0.92f);
            cam.orthographic = true;
            cam.orthographicSize = 1.15f;
            cam.transform.position = stage.transform.position + new Vector3(0f, 1.0f, -6f);

            for (var row = 0; row < rows.Length; row++)
            {
                var kind = rows[row];
                var moving = kind == "walk" || kind == "run";
                // Side view: walking toward screen right; swings seen three-quarter so the trunk twist shows.
                var hero = Hero(stage.transform, Vector3.zero, moving ? 90f : 125f, Locomotion.Grounded,
                    kind == "walk" ? new Vector3(0f, 0f, WalkSpeed) : kind == "run" ? new Vector3(0f, 0f, 7.5f) : Vector3.zero,
                    ActionKind.None, 0f, moving ? "" : "armed");
                var animator = hero.GetComponent<CharacterAnimator>();
                if (row == 0 && animator.Rig.Slash != null)
                    foreach (var (start, peak, end) in animator.Rig.Slash.Strikes)
                        Debug.Log($"Golpe capturado: {start:F2} - {peak:F2} - {end:F2} de {animator.Rig.Slash.Duration:F2} s");
                // FixedSource velocity is world space; the walk reads its length only.
                for (var i = 0; i < 60; i++) yield return null;

                for (var f = 0; f < frames; f++)
                {
                    if (moving)
                    {
                        // One full stride over the row: a captured loop when the rig has one, else 2 procedural steps.
                        var rig = animator.Rig;
                        var clip = kind == "walk" ? rig.Walk : rig.Run;
                        var speed = kind == "walk" ? WalkSpeed : 7.5f;
                        var period = clip != null ? Mathf.Max(clip.CycleDistance / speed, clip.Duration / 1.6f)
                            : (kind == "walk" ? 3f : 3.9f) / speed;
                        if (f == 0 && clip != null)
                            Debug.Log($"Clipe {kind}: {clip.Speed:F2} m/s, ciclo {clip.CycleDistance:F2} m em {clip.Duration:F2} s");
                        yield return new WaitForSeconds(period / frames);
                    }
                    else
                    {
                        var action = kind == "heavy" ? ActionKind.SlashHeavy : ActionKind.SlashLight;
                        if (f == 0 && kind == "overhead")
                        {
                            animator.Play(ActionKind.SlashLight);
                            animator.Play(ActionKind.SlashLight); // second swing of the combo: overhead
                        }

                        animator.Hold(action, (f + 0.5f) / frames * 0.9f);
                        for (var i = 0; i < 12; i++) yield return null;
                    }

                    var rt = new RenderTexture(cellW, cellH, 24);
                    cam.targetTexture = rt;
                    cam.Render();
                    RenderTexture.active = rt;
                    sheet.ReadPixels(new Rect(0, 0, cellW, cellH), f * cellW, (rows.Length - 1 - row) * cellH);
                    RenderTexture.active = null;
                    cam.targetTexture = null;
                    rt.Release();
                }

                Object.Destroy(hero);
                yield return null;
            }

            sheet.Apply();
            var dir = Path.Combine(Application.dataPath, "..", "Logs");
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, "preview-motion.png"), sheet.EncodeToPNG());
            Object.Destroy(sheet);
            Object.Destroy(cam.gameObject);
            Object.Destroy(light.gameObject);
            Object.Destroy(stage);
        }

        /// <summary>A hero rig in a pose, with the gear shown as HeroPresenter would show it in play.</summary>
        private static GameObject Hero(Transform parent, Vector3 at, float yaw, Locomotion loco, Vector3 velocity,
            ActionKind action, float progress, string stance)
        {
            var holder = new GameObject("Herói");
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = at;
            holder.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var animator = holder.AddComponent<CharacterAnimator>();
            animator.Rig = CharacterRig.Build(holder.transform, RigStyle.Hero);
            animator.Source = new FixedSource { Locomotion = loco, Velocity = velocity };
            var guard = stance == "guard";
            var armed = stance == "armed" || guard;
            animator.Guarding = guard;
            animator.Aiming = stance == "aim";
            animator.Kneeling = stance == "kneel";
            animator.Armed = armed;
            animator.ShieldInHand = armed;
            if (action != ActionKind.None) animator.Hold(action, progress);
            foreach (var r in animator.Rig.GetComponentsInChildren<Renderer>(true))
            {
                switch (CharacterRig.Classify(r.name))
                {
                    case GearPart.SwordHand: r.enabled = armed; break;
                    case GearPart.SwordBack: r.enabled = !armed; break;
                    case GearPart.ShieldHand: r.enabled = armed; break;
                    case GearPart.ShieldBack: r.enabled = !armed; break;
                    // The starting kit has no bow, as in the reference: it only shows while aiming.
                    case GearPart.BowHand: r.enabled = stance == "aim"; break;
                    case GearPart.BowBack: r.enabled = false; break;
                }
            }

            return holder;
        }

        private static void Save(Camera camera, string fileName, int width = 1280, int height = 720)
        {
            Assert.That(camera, Is.Not.Null);
            var rt = new RenderTexture(width, height, 24);
            var previous = camera.targetTexture;
            camera.targetTexture = rt;
            camera.Render();
            camera.targetTexture = previous;

            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;

            var dir = Path.Combine(Application.dataPath, "..", "Logs");
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, fileName), tex.EncodeToPNG());
            Object.Destroy(tex);
            rt.Release();
        }
    }
}

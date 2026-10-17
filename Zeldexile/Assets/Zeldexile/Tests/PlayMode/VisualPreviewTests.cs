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

            // Pose sheet: the hero in every locomotion state, held stance and one-shot action, plus a creature.
            var sheet = new GameObject("Folha de poses");
            sheet.transform.position = new Vector3(0f, 200f, 0f);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.SetParent(sheet.transform, false);
            floor.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(22f, 1f, 10f);
            floor.GetComponent<Renderer>().sharedMaterial = World.Palette.Solid(World.Palette.Ground);

            var poses = new (Locomotion loco, Vector3 velocity, ActionKind action, float at, string stance)[]
            {
                (Locomotion.Grounded, Vector3.zero, ActionKind.None, 0f, ""),
                (Locomotion.Grounded, new Vector3(0f, 0f, 7f), ActionKind.None, 0f, ""),
                (Locomotion.Airborne, new Vector3(0f, 3f, 2f), ActionKind.None, 0f, ""),
                (Locomotion.Climbing, new Vector3(0f, 2f, 0f), ActionKind.None, 0f, ""),
                (Locomotion.Gliding, new Vector3(0f, -1f, 6f), ActionKind.None, 0f, ""),
                (Locomotion.Swimming, new Vector3(0f, 0f, 2f), ActionKind.None, 0f, ""),
                (Locomotion.Grounded, Vector3.zero, ActionKind.SlashLight, 0.55f, ""),
                (Locomotion.Grounded, Vector3.zero, ActionKind.SlashHeavy, 0.5f, ""),
                (Locomotion.Grounded, Vector3.zero, ActionKind.Thrust, 0.5f, ""),
                (Locomotion.Grounded, Vector3.zero, ActionKind.Throw, 0.5f, ""),
                (Locomotion.Grounded, Vector3.zero, ActionKind.None, 0f, "aim"),
                (Locomotion.Grounded, Vector3.zero, ActionKind.None, 0f, "guard"),
                (Locomotion.Grounded, Vector3.zero, ActionKind.Pickup, 0.5f, ""),
                (Locomotion.Grounded, Vector3.zero, ActionKind.None, 0f, "kneel"),
                (Locomotion.Grounded, Vector3.zero, ActionKind.Hurt, 0.2f, ""),
                (Locomotion.Grounded, new Vector3(0f, 0f, 4f), ActionKind.None, 0f, "cascudo"),
            };
            for (var i = 0; i < poses.Length; i++)
            {
                var holder = new GameObject("Pose " + i);
                holder.transform.SetParent(sheet.transform, false);
                holder.transform.localPosition = new Vector3(-8.75f + (i % 8) * 2.5f, poses[i].loco == Locomotion.Swimming ? 0.6f : 0f, i < 8 ? 4.5f : -2.5f);
                // Three-quarter view from the hero's left, so forward arms and swings read as such.
                holder.transform.localRotation = Quaternion.Euler(0f, 115f, 0f);
                var animator = holder.AddComponent<CharacterAnimator>();
                var creature = poses[i].stance == "cascudo";
                animator.Rig = CharacterRig.Build(holder.transform, creature ? RigStyle.ForTier(Domain.EnemyTier.Blue) : RigStyle.Hero);
                animator.Source = new FixedSource { Locomotion = poses[i].loco, Velocity = poses[i].velocity };
                animator.Guarding = poses[i].stance == "guard";
                animator.Aiming = poses[i].stance == "aim";
                animator.Kneeling = poses[i].stance == "kneel";
                animator.Armed = poses[i].stance == "guard" || poses[i].action == ActionKind.SlashLight ||
                                 poses[i].action == ActionKind.SlashHeavy || poses[i].action == ActionKind.Thrust;
                if (poses[i].action != ActionKind.None) animator.Hold(poses[i].action, poses[i].at);
                if (!creature)
                {
                    // Show the gear the stance calls for, as the presenter would in play.
                    foreach (var r in animator.Rig.GetComponentsInChildren<Renderer>(true))
                    {
                        var name = r.name;
                        var inHand = poses[i].action == ActionKind.SlashLight || poses[i].action == ActionKind.SlashHeavy ||
                                     poses[i].action == ActionKind.Thrust || poses[i].action == ActionKind.Throw || poses[i].stance == "guard";
                        if (name.Contains("~Arma@") || name.Contains("~Arma Guarda") || name.Contains("~Arma Punho")) r.enabled = inHand;
                        if (name.Contains("~Arma Costas")) r.enabled = !inHand;
                        if (name.Contains("~Escudo@") || name.Contains("~Escudo Aro") || name.Contains("~Escudo Umbo")) r.enabled = poses[i].stance == "guard";
                        if (name.Contains("~Escudo Costas")) r.enabled = poses[i].stance != "guard";
                        if (name.Contains("~Arco@") || name.Contains("~Arco Corda")) r.enabled = poses[i].stance == "aim";
                        if (name.Contains("~Arco Costas")) r.enabled = poses[i].stance != "aim";
                    }
                }
            }

            var cam = new GameObject("Câmera de poses").AddComponent<Camera>();
            cam.transform.position = sheet.transform.position + new Vector3(0f, 4.6f, -10f);
            cam.transform.rotation = Quaternion.Euler(20f, 0f, 0f);
            cam.fieldOfView = 54f;
            cam.enabled = false;

            for (var i = 0; i < 40; i++) yield return null;
            Save(cam, "preview-poses.png");

            Object.Destroy(sheet);
            Object.Destroy(cam.gameObject);
            Object.Destroy(world);
            foreach (var name in new[] { "Mundo", "Jogador", "HUD", "Efeitos", "Loot", "Câmera", "Sol", "Atmosfera", "Game feel", "Grama densa", "Clima" })
            {
                var go = GameObject.Find(name);
                if (go != null) Object.Destroy(go);
            }
        }

        private static void Save(Camera camera, string fileName)
        {
            Assert.That(camera, Is.Not.Null);
            var rt = new RenderTexture(1280, 720, 24);
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

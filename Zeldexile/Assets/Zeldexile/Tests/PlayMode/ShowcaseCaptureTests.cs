using System.Collections;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Zeldexile.Gameplay;

namespace Zeldexile.Tests
{
    /// <summary>
    /// Records the hero showcase frame by frame to Logs/showcase/ (JPG frames + labels.txt), at a fixed frame
    /// rate so motion is exact. Tools/make_showcase_video.py turns it into an MP4. Explicit: it only runs when
    /// asked for by name (-testFilter Zeldexile.Tests.ShowcaseCaptureTests).
    /// </summary>
    [Explicit("Long capture; run by name.")]
    public sealed class ShowcaseCaptureTests
    {
        private const int Fps = 24;
        private const int Width = 960;
        private const int Height = 540;

        [UnityTest]
        public IEnumerator RecordShowcase()
        {
            var dir = Path.Combine(Application.dataPath, "..", "Logs", "showcase");
            Directory.CreateDirectory(dir);
            foreach (var old in Directory.GetFiles(dir)) File.Delete(old);

            var go = new GameObject("Vitrine do herói");
            var show = go.AddComponent<CharacterShowcase>();
            show.ManualClock = true;
            Time.captureFramerate = Fps;
            yield return null;

            var rt = new RenderTexture(Width, Height, 24) { antiAliasing = 4 };
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            var labels = new StringBuilder();
            var frames = Mathf.CeilToInt(CharacterShowcase.TotalSeconds * Fps);
            for (var i = 0; i < frames; i++)
            {
                show.Advance(1f / Fps);
                yield return null; // animator LateUpdate runs with deltaTime = 1/Fps

                var cam = show.ViewCamera;
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = null;
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(dir, $"frame_{i:0000}.jpg"), tex.EncodeToJPG(88));
                labels.AppendLine(show.CurrentLabel);
            }

            File.WriteAllText(Path.Combine(dir, "labels.txt"), labels.ToString());
            Time.captureFramerate = 0;
            rt.Release();
            Object.Destroy(tex);
            Object.Destroy(go);
            Assert.That(Directory.GetFiles(dir, "*.jpg").Length, Is.EqualTo(frames));
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
using Zeldexile.Player;

namespace Zeldexile.Gameplay
{
    /// <summary>
    /// Performance measurement of a region in the real build: "Zeldexile.exe -zxbench C:\out" opens the valley,
    /// holds the camera at every documented spot for a few seconds, flies along the main circuit at gameplay
    /// height, then writes frame times (average, 95th percentile, worst), memory and build times to
    /// bench.csv / bench.txt, plus a screenshot per spot, and quits. VSync is turned off while measuring.
    /// </summary>
    public sealed class RegionBenchmark : MonoBehaviour
    {
        private string _dir;
        private readonly StringBuilder _csv = new StringBuilder("trecho;quadros;media_ms;p95_ms;pior_ms;fps_medio;lotes_natureza;instancias_natureza\n");
        private readonly StringBuilder _txt = new StringBuilder();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-zxbench");
            if (index < 0 || index + 1 >= args.Length) return;
            Application.runInBackground = true;
            var go = new GameObject("Benchmark da região");
            DontDestroyOnLoad(go);
            var bench = go.AddComponent<RegionBenchmark>();
            bench._dir = args[index + 1];
            bench.StartCoroutine(bench.Run());
        }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(_dir);
            if (RegionWorld.Current == null)
            {
                var start = Time.realtimeSinceStartup;
                UnityEngine.SceneManagement.SceneManager.LoadScene("ValeInicial");
                while (RegionWorld.Current == null) yield return null;
                _txt.AppendLine($"Carregamento da cena + montagem da região: {(Time.realtimeSinceStartup - start) * 1000f:0} ms");
            }

            var world = RegionWorld.Current;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            for (var i = 0; i < 60; i++) yield return null;

            var cam = Camera.main;
            var rig = cam.GetComponent<ThirdPersonCamera>();
            if (rig != null) rig.enabled = false;

            _txt.AppendLine($"Máquina: {SystemInfo.processorType} ({SystemInfo.processorCount} threads), {SystemInfo.systemMemorySize} MB RAM");
            _txt.AppendLine($"GPU: {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}, {SystemInfo.graphicsMemorySize} MB)");
            _txt.AppendLine($"Resolução: {Screen.width}x{Screen.height}, qualidade '{QualitySettings.names[QualitySettings.GetQualityLevel()]}', sombras até {QualitySettings.shadowDistance} m, VSync desligado");
            _txt.AppendLine($"Montagem da região por etapa (ms acumulados): {string.Join(", ", world.Timings.Select(t => $"{t.stage} {t.ms}"))}");
            _txt.AppendLine($"Objetos instanciados: {world.Nature.InstanceCount} em {world.Nature.BatchCount} lotes; colisores {world.ColliderCount}; coletáveis {world.PickupCount}");

            // Documented spots.
            foreach (var s in world.Data.shots)
            {
                cam.fieldOfView = s.fov;
                cam.transform.SetPositionAndRotation(world.WorldAt(s.u, s.v, s.above), Quaternion.Euler(s.pitch, s.yaw, 0f));
                for (var i = 0; i < 30; i++) yield return null;
                yield return Measure("ponto:" + s.id, 3f, null);
                ScreenCapture.CaptureScreenshot(Path.Combine(_dir, $"bench-{s.id}.png"));
                yield return null;
            }

            // Gameplay-height flight along the main circuit (camera 2 m above ground, 6.5 m behind the eye line).
            var line = world.Terrain.PathLineOf(0);
            var along = 0f;
            yield return Measure("sobrevoo do circuito principal (6 m/s)", 60f, dt =>
            {
                along = (along + dt * 6f) % line.Length;
                var p = line.At(along, out _);
                var q = line.At((along + 8f) % line.Length, out _);
                var eye = world.WorldAt(p.u, p.v, 3.2f);
                var look = world.WorldAt(q.u, q.v, 1.5f);
                cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(look - eye));
            });

            _txt.AppendLine($"Memória: alocada {Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024)} MB, reservada {Profiler.GetTotalReservedMemoryLong() / (1024 * 1024)} MB, mono {Profiler.GetMonoUsedSizeLong() / (1024 * 1024)} MB");
            File.WriteAllText(Path.Combine(_dir, "bench.csv"), _csv.ToString());
            File.WriteAllText(Path.Combine(_dir, "bench.txt"), _txt.ToString());
            yield return new WaitForSecondsRealtime(1f);
            Application.Quit();
        }

        private IEnumerator Measure(string name, float seconds, Action<float> step)
        {
            var times = new List<float>();
            var batches = 0;
            var instances = 0;
            var end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end)
            {
                step?.Invoke(Time.unscaledDeltaTime);
                yield return null;
                times.Add(Time.unscaledDeltaTime * 1000f);
                var nature = RegionWorld.Current.Nature;
                batches = Mathf.Max(batches, nature.DrawnBatches);
                instances = Mathf.Max(instances, nature.DrawnInstances);
            }

            times.Sort();
            var avg = times.Average();
            var p95 = times[Mathf.Clamp((int)(times.Count * 0.95f), 0, times.Count - 1)];
            var worst = times[times.Count - 1];
            _csv.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0};{1};{2:0.00};{3:0.00};{4:0.00};{5:0.0};{6};{7}", name, times.Count, avg, p95, worst, 1000f / avg, batches, instances));
        }
    }
}

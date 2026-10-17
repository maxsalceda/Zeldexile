using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Zeldexile.Domain.Regions;
using Zeldexile.Gameplay;

namespace Zeldexile.Tests
{
    /// <summary>
    /// The start region running in the engine: it builds, its ground is continuous across sectors and has no holes,
    /// the entrance is safe, bridges carry the player, and a capsule with the player's size and limits can walk every
    /// path end to end. Writes Logs/vale/percursos.txt with the measured lengths and walking times.
    /// </summary>
    public sealed class RegionWorldTests
    {
        private RegionWorld _world;

        [UnitySetUp]
        public IEnumerator LoadRegion()
        {
            if (RegionWorld.Current == null)
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("ValeInicial");
                for (var i = 0; i < 5 && RegionWorld.Current == null; i++) yield return null;
            }

            _world = RegionWorld.Current;
            Assert.That(_world, Is.Not.Null, "RegionWorld did not start.");
            yield return null;
        }

        [Test]
        public void RegionBuildsWithItsContent()
        {
            Assert.That(_world.Data.id, Is.EqualTo("vale_inicial"));
            Assert.That(_world.Nature.InstanceCount, Is.GreaterThan(20000));
            Assert.That(_world.Bridges.Count, Is.GreaterThanOrEqualTo(3));
            Assert.That(_world.Water.LakeSurface, Is.Not.Null);
            Assert.That(_world.Water.Streams.Count, Is.EqualTo(_world.Terrain.StreamCount));
            Assert.That(_world.PickupCount, Is.GreaterThan(50));
            Assert.That(_world.Player, Is.Not.Null);
            foreach (var (stage, ms) in _world.Timings) TestContext.WriteLine($"{stage}: {ms} ms");
        }

        /// <summary>Writes the expansion connections (with their built heights) and the land cover shares.</summary>
        [Test]
        public void WritesConnectionsAndCover()
        {
            var dir = Path.Combine(Application.dataPath, "..", "Logs", "vale");
            Directory.CreateDirectory(dir);
            var csv = new StringBuilder("id;nome;borda;u;v;altura_sobre_lago_m;y_mundo;direcao_graus;largura_m;terreno;vegetacao;material;agua;restricoes\n");
            foreach (var c in _world.Data.connections)
            {
                var u = Mathf.Clamp(c.u, 0f, _world.Data.sizeU);
                var v = Mathf.Clamp(c.v, 0f, _world.Data.sizeV);
                var h = _world.Field.Height(u, v);
                var s = _world.Field.Nearest(u, v);
                // A river connection is described by its water surface, not its bed.
                if (s.Water == WaterKind.Stream) h = s.WaterSurface;
                csv.AppendLine(string.Join(";", c.id, c.name, c.edge, u.ToString("0"), v.ToString("0"), h.ToString("0.0"), (h + _world.Data.lakeLevel).ToString("0.0"),
                    c.direction.ToString("0"), c.width.ToString("0.0"), c.terrain, c.vegetation, c.material, c.water, c.restrictions));
                Assert.That(Mathf.Min(Mathf.Min(u, _world.Data.sizeU - u), Mathf.Min(v, _world.Data.sizeV - v)), Is.LessThan(0.5f), c.id + " is not on an edge");
            }

            File.WriteAllText(Path.Combine(dir, "conexoes.csv"), csv.ToString());
            var cover = CoverStats.Measure(_world.Field);
            File.WriteAllText(Path.Combine(dir, "cobertura.csv"),
                $"cobertura;participacao\ncampos e prados;{cover[0]:P1}\nbosques;{cover[1]:P1}\npomares;{cover[2]:P1}\nagua;{cover[3]:P1}\nrochas e encostas;{cover[4]:P1}\n");
            TestContext.WriteLine(csv.ToString());
        }

        [Test]
        public void TerrainSpansTheWholeEnvelope()
        {
            var sectors = GameObject.Find("Terreno").transform;
            Assert.That(sectors.childCount, Is.EqualTo(25));
            var bounds = new Bounds();
            var first = true;
            foreach (Transform s in sectors)
            {
                var b = s.GetComponent<MeshRenderer>().bounds;
                if (first) bounds = b;
                else bounds.Encapsulate(b);
                first = false;
            }

            Assert.That(bounds.size.x, Is.EqualTo(1000f).Within(0.01f), "East-west extent (m)");
            Assert.That(bounds.size.z, Is.EqualTo(1000f).Within(0.01f), "South-north extent (m)");
            TestContext.WriteLine($"Envelope medido: {bounds.size.x:0.00} m x {bounds.size.z:0.00} m = {bounds.size.x * bounds.size.z / 1e6f:0.000} km²; " +
                                  $"altura {bounds.min.y - _world.Data.lakeLevel:0.0} a {bounds.max.y - _world.Data.lakeLevel:0.0} m sobre o lago");
        }

        [Test]
        public void SectorEdgesMatchExactly()
        {
            var meshes = new Dictionary<string, Mesh>();
            foreach (Transform s in GameObject.Find("Terreno").transform) meshes[s.name.Replace("Setor ", "")] = s.GetComponent<MeshFilter>().sharedMesh;
            var mismatches = 0;
            foreach (var pair in meshes)
            {
                SectorCoord.TryParse(pair.Key, out var c);
                var east = new SectorCoord(c.X + 1, c.Z).Id;
                if (meshes.TryGetValue(east, out var other)) mismatches += Compare(pair.Value, other, (c.X + 1) * 200f, true);
                var north = new SectorCoord(c.X, c.Z + 1).Id;
                if (meshes.TryGetValue(north, out other)) mismatches += Compare(pair.Value, other, (c.Z + 1) * 200f, false);
            }

            Assert.That(mismatches, Is.EqualTo(0));
        }

        private static int Compare(Mesh a, Mesh b, float line, bool alongX)
        {
            var ha = EdgeHeights(a, line, alongX);
            var hb = EdgeHeights(b, line, alongX);
            var bad = 0;
            foreach (var pair in ha)
                if (hb.TryGetValue(pair.Key, out var y) && Mathf.Abs(y - pair.Value) > 1e-4f) bad++;
            Assert.That(ha.Count, Is.GreaterThan(50));
            return bad;
        }

        private static Dictionary<int, float> EdgeHeights(Mesh m, float line, bool alongX)
        {
            var result = new Dictionary<int, float>();
            foreach (var v in m.vertices)
            {
                var coord = alongX ? v.x : v.z;
                if (Mathf.Abs(coord - line) > 1e-3f) continue;
                result[Mathf.RoundToInt((alongX ? v.z : v.x) * 10f)] = v.y;
            }

            return result;
        }

        [Test]
        public void GroundHasNoHoles()
        {
            var misses = new List<string>();
            var o = _world.Origin;
            for (var v = 2.5f; v < 1000f; v += 10f)
            for (var u = 2.5f; u < 1000f; u += 10f)
            {
                var hits = Physics.RaycastAll(new Vector3(o.x + u, o.y + 400f, o.z + v), Vector3.down, 800f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                var ground = false;
                foreach (var h in hits) ground |= h.collider.name.StartsWith("Setor ");
                if (!ground) misses.Add($"({u:0},{v:0})");
            }

            Assert.That(misses, Is.Empty, string.Join(" ", misses));
        }

        [Test]
        public void EntranceIsSafe()
        {
            var spawn = _world.SpawnPoint;
            Assert.That(Physics.Raycast(spawn + Vector3.up, Vector3.down, out var hit, 5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore), Is.True);
            Assert.That(hit.collider.name, Does.StartWith("Setor "));
            var blockers = Physics.OverlapCapsule(spawn + Vector3.up * 0.5f, spawn + Vector3.up * 1.6f, 0.6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            foreach (var c in blockers)
                Assert.That(c.name.StartsWith("Setor ") || c.GetComponentInParent<CharacterController>() != null, Is.True, "Spawn blocked by " + c.name);
        }

        [Test]
        public void BridgesHaveAContinuousDeck()
        {
            foreach (var b in _world.Bridges)
            {
                for (var t = 0f; t <= 1f; t += 0.02f)
                {
                    var p = Vector3.Lerp(b.A, b.B, t) + Vector3.up * 3f;
                    Assert.That(Physics.Raycast(p, Vector3.down, out var hit, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore), Is.True, $"{b.Name}: no floor at {t:0.00}");
                    Assert.That(hit.point.y, Is.EqualTo(b.DeckY).Within(0.6f), $"{b.Name}: floor height at {t:0.00} is {hit.point.y - b.DeckY:0.00} m off");
                }
            }
        }

        /// <summary>
        /// Walks every path with a CharacterController the size of the player (slope 50 degrees, step 0.4 m) at
        /// walking speed, steering along the centerline. Fails where it gets stuck or drops more than a step.
        /// </summary>
        [Test]
        public void EveryPathCanBeWalked()
        {
            var playerGo = _world.Player.gameObject;
            playerGo.SetActive(false);
            var report = new StringBuilder();
            var failures = new List<string>();
            try
            {
                for (var i = 0; i < _world.Terrain.PathCount; i++)
                {
                    var data = _world.Terrain.PathDataOf(i);
                    var line = _world.Terrain.PathLineOf(i);
                    var result = Walk(line, out var walked, out var climb, out var problem);
                    if (!result) failures.Add($"{data.id}: {problem}");
                    report.AppendLine($"{data.id};{data.name};{data.kind};{line.Length:0};{walked:0};{climb:0};{walked / 4f / 60f:0.0};{walked / 7f / 60f:0.0};{(result ? "ok" : problem)}");
                }
            }
            finally
            {
                playerGo.SetActive(true);
            }

            var dir = Path.Combine(Application.dataPath, "..", "Logs", "vale");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "percursos.csv"), "trilha;nome;tipo;comprimento_plano_m;percorrido_3d_m;subida_total_m;min_andando;min_correndo;resultado\n" + report);
            TestContext.WriteLine(report.ToString());
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        private bool Walk(Polyline line, out float walked, out float climb, out string problem)
        {
            var o = _world.Origin;
            var go = new GameObject("Caminhante de teste") { layer = 2 };
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.slopeLimit = 50f;
            cc.stepOffset = 0.4f;
            var start = line.At(0f, out _);
            go.transform.position = o + new Vector3(start.u, _world.Field.Height(start.u, start.v) + 0.3f, start.v);
            Physics.SyncTransforms();
            walked = 0f;
            climb = 0f;
            problem = null;
            const float dt = 1f / 30f;
            const float speed = 4f;
            var vy = 0f;
            var s = 0f;
            var stuckTime = 0f;
            var last = go.transform.position;
            try
            {
                while (s < line.Length - 1f)
                {
                    var target2 = line.At(Mathf.Min(s + 2.5f, line.Length), out _);
                    var target = o + new Vector3(target2.u, 0f, target2.v);
                    var pos = go.transform.position;
                    var to = new Vector3(target.x - pos.x, 0f, target.z - pos.z);
                    var move = to.sqrMagnitude > 1e-4f ? to.normalized * speed : Vector3.zero;
                    vy = cc.isGrounded ? -2f : vy - 22f * dt;
                    cc.Move((move + Vector3.up * vy) * dt);
                    var now = go.transform.position;
                    var step = now - last;
                    walked += step.magnitude;
                    if (step.y > 0f) climb += step.y;
                    if (step.y < -1.2f && cc.isGrounded)
                    {
                        problem = $"queda de {-step.y:0.0} m perto de ({now.x - o.x:0},{now.z - o.z:0})";
                        return false;
                    }

                    var horizontal = new Vector2(step.x, step.z).magnitude;
                    stuckTime = horizontal < speed * dt * 0.25f ? stuckTime + dt : 0f;
                    if (stuckTime > 2f)
                    {
                        problem = $"preso perto de ({now.x - o.x:0},{now.z - o.z:0})";
                        return false;
                    }

                    last = now;
                    // Progress along the line (ignoring the far end of a loop, which touches its start).
                    line.Nearest(new P2(now.x - o.x, now.z - o.z), out var along);
                    if (along > s && along < s + 15f) s = along;
                    if (walked > line.Length * 3f + 50f)
                    {
                        problem = "não avança pela trilha";
                        return false;
                    }
                }

                return true;
            }
            finally
            {
                Object.Destroy(go);
            }
        }
    }
}

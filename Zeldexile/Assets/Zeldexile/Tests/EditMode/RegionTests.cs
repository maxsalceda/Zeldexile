using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Zeldexile.Domain.Regions;

namespace Zeldexile.Tests
{
    /// <summary>
    /// The start region's data and rules, without running the game: size and grid, the sector registry,
    /// the edge contract with a future neighbor, coherent water, graded paths and bridges, deterministic scatter.
    /// </summary>
    public sealed class RegionTests
    {
        private static RegionData _data;
        private static RegionTerrain _terrain;
        private static HeightField _field;

        private static RegionData LoadVale()
        {
            var path = Path.Combine(Application.dataPath, "Zeldexile/Resources/World/Regions/vale_inicial.json");
            return JsonUtility.FromJson<RegionData>(File.ReadAllText(path));
        }

        [OneTimeSetUp]
        public void Bake()
        {
            _data = LoadVale();
            _terrain = new RegionTerrain(_data);
            _field = new HeightField(_terrain);
        }

        [Test]
        public void EnvelopeIsOneSquareKilometerOnTheGrid()
        {
            Assert.That(_data.sizeU, Is.EqualTo(1000f));
            Assert.That(_data.sizeV, Is.EqualTo(1000f));
            Assert.That((_field.CountU - 1) * _field.Cell, Is.EqualTo(1000f).Within(1e-3f));
            Assert.That((_field.CountV - 1) * _field.Cell, Is.EqualTo(1000f).Within(1e-3f));
            Assert.That(WorldGrid.DefaultSectorSize % _data.cellSize, Is.EqualTo(0f), "Cells must tile a sector exactly.");
            Assert.That(_data.originX % WorldGrid.DefaultSectorSize, Is.EqualTo(0f));
            Assert.That(_data.originZ % WorldGrid.DefaultSectorSize, Is.EqualTo(0f));
        }

        [Test]
        public void SectorIdsAreStableAndAcceptNegativeCoordinates()
        {
            var grid = new WorldGrid();
            Assert.That(grid.SectorAt(0f, 0f).Id, Is.EqualTo("s0_0"));
            Assert.That(grid.SectorAt(999.9f, 999.9f).Id, Is.EqualTo("s4_4"));
            Assert.That(grid.SectorAt(-0.1f, 250f).Id, Is.EqualTo("s-1_1"));
            Assert.That(grid.SectorAt(-200f, -200.1f).Id, Is.EqualTo("s-1_-2"));
            Assert.That(SectorCoord.TryParse("s-3_12", out var c) && c.X == -3 && c.Z == 12, Is.True);
        }

        [Test]
        public void AtlasRegistersTwentyFiveSectorsAndOpenNeighbors()
        {
            var atlas = new WorldAtlas(new WorldGrid());
            atlas.Add(_data);
            var sectors = new List<SectorCoord>(atlas.SectorsOf(_data));
            Assert.That(sectors.Count, Is.EqualTo(25));
            Assert.That(atlas.RegionAt(new SectorCoord(2, 2)), Is.SameAs(_data));
            var open = new List<SectorCoord>(atlas.OpenNeighbors(_data));
            Assert.That(open, Does.Contain(new SectorCoord(2, 5)), "North side can grow.");
            Assert.That(open, Does.Contain(new SectorCoord(-1, 3)), "West side can grow into negative sectors.");
            Assert.That(open.Count, Is.EqualTo(20));
        }

        [Test]
        public void ANorthNeighborJoinsWithoutSeamOrMovingAnything()
        {
            var atlas = new WorldAtlas(new WorldGrid());
            atlas.Add(_data);
            var scatterBefore = new Scatterer(_terrain, _field, ScatterCatalog.Default(), null).Run();

            // A test region north of the valley: flat-ish ground, different seed and lake level.
            var north = new RegionData
            {
                id = "teste_norte", seed = 99, originX = 0f, originZ = 1000f, sizeU = 1000f, sizeV = 400f,
                lakeLevel = 35f, cellSize = 2.5f, lake = null,
                relief = new ReliefData { baseHeight = 40f, rollAmplitude = 3f, edgeBand = 60f },
                hills = new[] { new HillData { id = "x", u = 500f, v = 300f, radius = 200f, height = 30f } }
            };
            atlas.Add(north);
            Assert.That(atlas.RegionAt(new SectorCoord(2, 5)), Is.SameAs(north));
            Assert.Throws<InvalidOperationException>(() => atlas.Add(new RegionData { id = "sobreposta", originX = 800f, originZ = 800f, sizeU = 400f, sizeV = 400f }));

            // The contract: an existing region answers for every point of its own border, corners included.
            float? ValeEdge(float x, float z)
            {
                var u = x - _data.originX;
                var v = z - _data.originZ;
                if (u < 0f || v < 0f || u > _data.sizeU || v > _data.sizeV) return null;
                return _field.WorldY(u, v);
            }

            var northField = new HeightField(new RegionTerrain(north, ValeEdge));
            var top = _field.CountV - 1;
            for (var i = 0; i < _field.CountU; i++)
            {
                var vale = _data.lakeLevel + _field.At(i, top).H;
                var other = north.lakeLevel + northField.At(i, 0).H;
                Assert.That(other, Is.EqualTo(vale).Within(1e-3f), $"Seam at x = {i * _field.Cell}");
            }

            // The valley's objects and ids did not change.
            var scatterAfter = new Scatterer(_terrain, _field, ScatterCatalog.Default(), null).Run();
            Assert.That(scatterAfter.Count, Is.EqualTo(scatterBefore.Count));
            for (var k = 0; k < scatterBefore.Count; k += 97)
            {
                Assert.That(scatterAfter[k].U, Is.EqualTo(scatterBefore[k].U));
                Assert.That(scatterAfter[k].V, Is.EqualTo(scatterBefore[k].V));
            }
        }

        [Test]
        public void StreamsNeverRunUphillAndMeetTheLakeAtItsLevel()
        {
            for (var i = 0; i < _terrain.StreamCount; i++)
            {
                var line = _terrain.StreamLineOf(i);
                var previous = float.MaxValue;
                for (var s = 0f; s <= line.Length; s += 1f)
                {
                    var ws = _terrain.StreamSurface(i, s, out _, out _);
                    Assert.That(ws, Is.LessThanOrEqualTo(previous + 1e-3f), $"{_terrain.StreamDataOf(i).id} rises at s = {s}");
                    previous = ws;
                }

                var d = _terrain.StreamDataOf(i);
                if (d.feedsLake) Assert.That(_terrain.StreamSurface(i, line.Length, out _, out _), Is.EqualTo(0f).Within(0.01f));
                if (d.leavesLake) Assert.That(_terrain.StreamSurface(i, 0f, out _, out _), Is.EqualTo(0f).Within(0.01f));
            }
        }

        [Test]
        public void StreamsAreSunkIntoTheirBanks()
        {
            for (var i = 0; i < _terrain.StreamCount; i++)
            {
                var line = _terrain.StreamLineOf(i);
                var dry = 0;
                var total = 0;
                for (var s = 4f; s <= line.Length - 4f; s += 4f)
                {
                    var p = line.At(s, out var dir);
                    if (_field.Nearest(p.u, p.v).LakeDistance < 4f) continue;
                    var ws = _terrain.StreamSurface(i, s, out var width, out _);
                    var side = dir.Left * (width * 0.5f + 3f);
                    var l = _field.Height(p.u + side.u, p.v + side.v);
                    var r = _field.Height(p.u - side.u, p.v - side.v);
                    total++;
                    if (Mathf.Min(l, r) < ws + 0.15f) dry++;
                }

                // Junctions with another stream or a fall's lip may dip below the water line for a sample or two.
                Assert.That(dry, Is.LessThanOrEqualTo(Mathf.Max(2, total / 15)), $"{_terrain.StreamDataOf(i).id}: {dry}/{total} samples where a bank is under the water");
            }
        }

        [Test]
        public void PathsAreGradedAndDry()
        {
            for (var i = 0; i < _terrain.PathCount; i++)
            {
                var line = _terrain.PathLineOf(i);
                var data = _terrain.PathDataOf(i);
                var prev = float.NaN;
                var steepest = 0f;
                var wet = 0;
                for (var s = 0f; s <= line.Length; s += 2f)
                {
                    var p = line.At(s, out _);
                    if (OnBridge(p)) { prev = float.NaN; continue; }
                    var h = _field.Height(p.u, p.v);
                    if (!float.IsNaN(prev)) steepest = Mathf.Max(steepest, Mathf.Abs(h - prev) / 2f);
                    prev = h;
                    if (_field.Nearest(p.u, p.v).Water != WaterKind.None) wet++;
                }

                // Access paths may ford a stream; the rest stay dry. Main circuit under ~16 degrees, hill trails under ~17.
                var limit = data.kind == "acesso" ? 0.4f : data.kind == "secundaria" ? 0.31f : 0.29f;
                Assert.That(steepest, Is.LessThan(limit), $"{data.id}: steepest step {Mathf.Atan(steepest) * Mathf.Rad2Deg:0.0} degrees");
                if (data.kind != "acesso") Assert.That(wet, Is.EqualTo(0), $"{data.id} goes through water");
            }
        }

        [Test]
        public void BridgesClearTheWaterAndMeetTheirPaths()
        {
            Assert.That(_terrain.Bridges.Count, Is.GreaterThanOrEqualTo(3));
            foreach (var b in _terrain.Bridges)
            {
                Assert.That(b.StreamId, Is.Not.Null, $"{b.Data.id} crosses no stream");
                Assert.That(b.Deck - 0.35f - b.WaterSurface, Is.GreaterThanOrEqualTo(b.Data.clearance - 0.01f), b.Data.id);
                Assert.That(b.Data.width, Is.InRange(2.5f, 4f), b.Data.id);
                foreach (var end in new[] { b.A, b.B })
                    Assert.That(Mathf.Abs(_field.Height(end.u, end.v) - b.Deck), Is.LessThan(0.5f), $"{b.Data.id}: step at its end {end}");
            }
        }

        [Test]
        public void CoverIsCloseToThePlan()
        {
            var cover = CoverStats.Measure(_field);
            TestContext.WriteLine($"campo {cover[0]:P1}, bosque {cover[1]:P1}, pomar {cover[2]:P1}, água {cover[3]:P1}, rocha {cover[4]:P1}");
            Assert.That(cover[(int)Cover.Meadow], Is.InRange(0.38f, 0.56f));
            Assert.That(cover[(int)Cover.Forest], Is.InRange(0.15f, 0.25f));
            Assert.That(cover[(int)Cover.Orchard], Is.InRange(0.10f, 0.20f));
            Assert.That(cover[(int)Cover.Water], Is.InRange(0.08f, 0.14f));
            Assert.That(cover[(int)Cover.Rock], Is.InRange(0.07f, 0.15f));
        }

        [Test]
        public void ScatterIsDeterministicWithStableIds()
        {
            var a = new Scatterer(_terrain, _field, ScatterCatalog.Default(), null);
            var b = new Scatterer(_terrain, _field, ScatterCatalog.Default(), null);
            var ra = a.Run();
            var rb = b.Run();
            Assert.That(ra.Count, Is.EqualTo(rb.Count));
            Assert.That(ra.Count, Is.GreaterThan(20000));
            var ids = new HashSet<string>();
            foreach (var i in ra) Assert.That(ids.Add(a.IdOf(i)), Is.True, "Duplicated id " + a.IdOf(i));
            Assert.That(a.IdOf(ra[123]), Is.EqualTo(b.IdOf(rb[123])));
        }

        [Test]
        public void SolidObjectsKeepOffPathsAndBridges()
        {
            var layers = ScatterCatalog.Default();
            var scatter = new Scatterer(_terrain, _field, layers, null).Run();
            foreach (var i in scatter)
            {
                var layer = layers[i.Layer];
                if (layer.Radius <= 0f) continue;
                var s = _field.Nearest(i.U, i.V);
                var clear = s.PathDistance - Scatterer.PathHalfWidth(s.PathKind) - layer.Radius * i.Scale;
                Assert.That(clear, Is.GreaterThan(0.5f), $"{layer.Id} at ({i.U:0},{i.V:0}) overlaps a path");
            }
        }

        [Test]
        public void SpawnIsFlatDryAndOnThePath()
        {
            var s = _field.Nearest(_data.spawn.u, _data.spawn.v);
            Assert.That(s.Water, Is.EqualTo(WaterKind.None));
            Assert.That(_field.NormalY(_data.spawn.u, _data.spawn.v), Is.GreaterThan(0.95f));
            Assert.That(s.PathDistance, Is.LessThan(6f));
        }

        private static bool OnBridge(P2 p)
        {
            foreach (var b in _terrain.Bridges)
                if (RegionMath.SegmentDistance(p, b.A, b.B, out var t) < 2.5f && t > 0.01f && t < 0.99f)
                    return true;
            return false;
        }
    }
}

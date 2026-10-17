using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Zeldexile.Characters;

namespace Zeldexile.Tests
{
    /// <summary>Checks that the Blender export lands in Unity with the right axes, scale and slots.</summary>
    public sealed class ModelImportTests
    {
        private static Bounds RenderBounds(GameObject go)
        {
            // Only what is drawn: gear held in the hands starts hidden and would otherwise hang below the feet.
            var renderers = System.Array.FindAll(go.GetComponentsInChildren<Renderer>(), r => r.enabled);
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        [Test]
        public void HeroModelHasJointsOnTheRightSides()
        {
            Assert.That(ModelLibrary.Has("Characters/heroi"), Is.True, "Run Tools/Blender/build_models.py first.");
            var holder = new GameObject("Teste herói");
            var blender = RigStyle.Hero;
            blender.SkinnedModel = null; // the Blender model, not the skinned Meshy hero
            var rig = CharacterRig.Build(holder.transform, blender);
            try
            {
                Assert.That(rig.ArmR.position.x, Is.GreaterThan(rig.ArmL.position.x), "Right arm must be on +X.");
                Assert.That(rig.Head.position.y, Is.GreaterThan(rig.Torso.position.y));
                Assert.That(rig.Hips.localRotation, Is.EqualTo(Quaternion.identity));

                var eye = rig.Head.GetComponentsInChildren<Renderer>().First(r => r.name.Contains("~Olho D"));
                Assert.That(eye.bounds.center.z, Is.GreaterThan(rig.Head.position.z), "Face must look toward +Z.");
                Assert.That(eye.bounds.center.x, Is.GreaterThan(rig.Head.position.x), "Right eye must be on +X.");

                rig.Glider.gameObject.SetActive(true);
                var b = RenderBounds(rig.gameObject);
                Assert.That(b.min.y, Is.EqualTo(0f).Within(0.08f), "Feet on the ground.");
                Assert.That(b.max.y, Is.InRange(1.7f, 2.9f), "Head (and glider) height in meters.");
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void SkinnedHeroStandsOnTheRigConventions()
        {
            Assert.That(ModelLibrary.Has("Skinned/heroi_meshy"), Is.True, "Missing Resources/Models/Skinned/heroi_meshy.fbx.");
            var holder = new GameObject("Teste herói Meshy");
            var rig = CharacterRig.Build(holder.transform, RigStyle.Hero);
            try
            {
                Assert.That(rig.GetComponentInChildren<SkinnedMeshRenderer>(), Is.Not.Null, "The hero should be the skinned Meshy model.");
                Assert.That(rig.ArmR.position.x, Is.GreaterThan(rig.ArmL.position.x), "Right arm must be on +X.");
                Assert.That(rig.LegR.position.x, Is.GreaterThan(rig.LegL.position.x), "Right leg must be on +X.");
                Assert.That(rig.Head.position.y, Is.GreaterThan(rig.Chest.position.y));
                Assert.That(rig.Hips.localPosition.x, Is.EqualTo(0f).Within(0.001f), "Hips over the origin.");
                Assert.That(rig.Hips.localPosition.y, Is.InRange(0.8f, 1.1f), "Hips height in meters.");
                Assert.That(rig.transform.InverseTransformPoint(rig.Head.position).y, Is.InRange(1.3f, 1.75f), "Head height in meters.");
                Assert.That(rig.LowestContactHeight(), Is.EqualTo(0f).Within(0.12f), "Soles on the ground.");
                foreach (var part in new[] { GearPart.SwordHand, GearPart.SwordBack, GearPart.ShieldHand, GearPart.ShieldBack })
                    Assert.That(rig.GetComponentsInChildren<Renderer>(true).Any(r => CharacterRig.Classify(r.name) == part), Is.True, $"Gear {part}.");
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void EveryMeshHasAColorSlot()
        {
            foreach (var path in new[] { "Characters/heroi", "Characters/cascudo", "Props/bau", "Props/pilar_cobre", "Props/grama" })
            {
                var prefab = ModelLibrary.Load(path);
                Assert.That(prefab, Is.Not.Null, path);
                foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
                    Assert.That(ModelLibrary.SlotOf(r.name), Is.Not.Empty, $"{path}: {r.name}");
            }
        }

        [Test]
        public void AnimeHeroIsSmoothAndUsesTheSmoothOutline()
        {
            Assert.That(ModelLibrary.Has("Characters/heroi_anime"), Is.True, "Run build_hero_game.py -- --anime first.");
            var model = ModelLibrary.Spawn("Characters/heroi_anime", null, RigStyle.Hero.ColorFor);
            try
            {
                var skull = model.GetComponentsInChildren<MeshFilter>().First(f => f.name.Contains("~Crânio@"));
                var mesh = skull.sharedMesh;
                // Faceted meshes repeat every vertex per triangle; a smooth one shares them.
                // (Character meshes are not readable: count indices, not the triangles array.)
                Assert.That(mesh.vertexCount, Is.LessThan(mesh.GetIndexCount(0) / 3), "Skull must keep its smooth normals.");
                var jacket = model.GetComponentsInChildren<Renderer>().First(r => r.name.Contains("~Jaqueta@"));
                Assert.That(jacket.sharedMaterial.shader.name, Is.EqualTo("Zeldexile/ToonOutlineSmooth"));
            }
            finally
            {
                Object.DestroyImmediate(model);
            }
        }

        [Test]
        public void PropsSitOnTheirOrigin()
        {
            var chest = ModelLibrary.SpawnProp("bau", null, Vector3.zero);
            try
            {
                var b = RenderBounds(chest);
                Assert.That(b.min.y, Is.EqualTo(0f).Within(0.03f));
                Assert.That(b.size.y, Is.EqualTo(0.8f).Within(0.08f));
                Assert.That(b.size.x, Is.EqualTo(1.24f).Within(0.08f), "Chest is wider along X.");
            }
            finally
            {
                Object.DestroyImmediate(chest);
            }
        }
    }
}

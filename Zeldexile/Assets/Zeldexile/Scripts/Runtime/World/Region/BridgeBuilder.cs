using System;
using System.Collections.Generic;
using UnityEngine;
using Zeldexile.Domain.Regions;

namespace Zeldexile.World.Region
{
    /// <summary>
    /// Wooden footbridges in one building language: planks across two beams, posts with a rope rail, piles in the
    /// water on long spans and stones at the abutments. The look is instanced parts (no two bridges identical:
    /// plank variants, small offsets); the walkable part is one continuous box, so feet never catch between planks.
    /// </summary>
    public static class BridgeBuilder
    {
        public const float DeckThickness = 0.4f;
        public const float RailHeight = 1.0f;
        /// <summary>The deck runs this far into the ground at each end, so the path meets it without a gap.</summary>
        public const float Overlap = 1.2f;

        public sealed class Built
        {
            public string Id;
            public string Name;
            public GameObject Root;
            public Vector3 A;
            public Vector3 B;
            public float Width;
            /// <summary>World Y of the walking surface.</summary>
            public float DeckY;
        }

        public static List<Built> Build(Transform parent, RegionTerrain terrain, HeightField field, InstancedNature nature,
            Func<string, InstancedNature.Kind> kindOf)
        {
            var list = new List<Built>();
            var root = new GameObject("Pontes").transform;
            root.SetParent(parent, false);
            var data = terrain.Data;
            var origin = new Vector3(data.originX, data.lakeLevel, data.originZ);
            foreach (var span in terrain.Bridges)
                list.Add(BuildOne(root, span, field, origin, nature, kindOf));
            return list;
        }

        private static Built BuildOne(Transform root, RegionTerrain.BridgeSpan span, HeightField field, Vector3 origin,
            InstancedNature nature, Func<string, InstancedNature.Kind> kindOf)
        {
            var rng = new System.Random(RegionMath.HashString(span.Data.id));
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            var a = origin + new Vector3(span.A.u, span.Deck, span.A.v);
            var b = origin + new Vector3(span.B.u, span.Deck, span.B.v);
            var axis = b - a;
            axis.y = 0f;
            var length = axis.magnitude;
            var forward = axis / length;
            var rotation = Quaternion.LookRotation(forward, Vector3.up);
            var right = rotation * Vector3.right;
            var mid = (a + b) * 0.5f;
            var width = span.Data.width;
            var deckY = mid.y;
            var total = length + Overlap * 2f;

            var go = new GameObject(span.Data.name);
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(mid, rotation);

            // Walking surface: one box under the planks' top, running into the ground at both ends.
            var deck = go.AddComponent<BoxCollider>();
            deck.center = new Vector3(0f, -DeckThickness / 2f, 0f);
            deck.size = new Vector3(width, DeckThickness, total);

            // Planks across the deck.
            var plankKinds = new[] { kindOf("ponte_tabua_0"), kindOf("ponte_tabua_1"), kindOf("ponte_tabua_2") };
            const float pitch = 0.31f;
            var planks = Mathf.FloorToInt(total / pitch);
            for (var i = 0; i < planks; i++)
            {
                var z = -total / 2f + (i + 0.5f) * pitch;
                var kind = plankKinds[rng.Next(plankKinds.Length)];
                var p = mid + forward * z + right * R(-0.04f, 0.04f) + Vector3.up * (-0.09f + R(-0.012f, 0.008f));
                var r = rotation * Quaternion.Euler(0f, R(-1.8f, 1.8f), R(-0.8f, 0.8f));
                Put(nature, kind, p, r, new Vector3(width + R(0.05f, 0.2f), 1f, 1f), width);
            }

            // Two beams under the planks.
            var beam = kindOf("ponte_viga");
            foreach (var side in new[] { -1f, 1f })
                Put(nature, beam, mid + right * side * (width / 2f - 0.3f) + Vector3.up * (-0.09f - 0.26f), rotation, new Vector3(1f, 1f, total), total * 0.5f);

            // Posts at both ends and along the span, with a sagging rope rail between them.
            var post = kindOf("ponte_poste");
            var rope = kindOf("ponte_corda");
            var bays = Mathf.Max(1, Mathf.CeilToInt(length / 4.2f));
            foreach (var side in new[] { -1f, 1f })
            {
                Vector3? previous = null;
                for (var i = 0; i <= bays; i++)
                {
                    var z = -length / 2f + length * i / bays;
                    var basePos = mid + forward * z + right * side * (width / 2f + 0.1f) + Vector3.up * -0.35f;
                    Put(nature, post, basePos, rotation * Quaternion.Euler(0f, R(0f, 60f), R(-1.5f, 1.5f)), Vector3.one, 1.5f);
                    var top = basePos + Vector3.up * (RailHeight + 0.35f - 0.05f);
                    if (previous.HasValue) Rope(nature, rope, previous.Value, top, 0.14f);
                    previous = top;
                }

                // Rail collider: posts and rope keep the player on the deck.
                var rail = go.AddComponent<BoxCollider>();
                rail.center = new Vector3(side * (width / 2f + 0.1f), RailHeight / 2f, 0f);
                rail.size = new Vector3(0.2f, RailHeight, length);
            }

            // Piles in the water on long spans.
            if (length > 9f)
            {
                var pile = kindOf("ponte_pilar");
                var count = Mathf.FloorToInt(length / 6f);
                for (var i = 1; i <= count; i++)
                {
                    var z = -length / 2f + length * i / (count + 1);
                    foreach (var side in new[] { -1f, 1f })
                    {
                        var p = mid + forward * z + right * side * (width / 2f - 0.3f);
                        var ground = field.WorldY(p.x - origin.x, p.z - origin.z) - 0.4f;
                        var topY = deckY - 0.09f - 0.26f;
                        if (topY - ground < 0.3f) continue;
                        Put(nature, pile, new Vector3(p.x, ground, p.z), rotation * Quaternion.Euler(0f, R(0f, 60f), 0f), new Vector3(1f, topY - ground, 1f), 0.4f);
                        var col = new GameObject("Pilar").AddComponent<CapsuleCollider>();
                        col.transform.SetParent(go.transform, false);
                        col.transform.position = new Vector3(p.x, (ground + topY) / 2f, p.z);
                        col.height = topY - ground;
                        col.radius = 0.15f;
                    }
                }
            }

            // Retaining stones beside both ends.
            var stones = new[] { kindOf("rocha_0"), kindOf("rocha_1"), kindOf("rocha_2") };
            foreach (var end in new[] { -1f, 1f })
            foreach (var side in new[] { -1f, 1f })
            {
                var p = mid + forward * end * (length / 2f + R(0.3f, 1.4f)) + right * side * (width / 2f + R(0.6f, 1.3f));
                var y = field.WorldY(p.x - origin.x, p.z - origin.z) - 0.18f;
                var s = R(0.55f, 0.9f);
                Put(nature, stones[rng.Next(stones.Length)], new Vector3(p.x, y, p.z), Quaternion.Euler(0f, R(0f, 360f), 0f), Vector3.one * s, s);
            }

            return new Built { Id = span.Data.id, Name = span.Data.name, Root = go, A = a, B = b, Width = width, DeckY = deckY };
        }

        private static void Rope(InstancedNature nature, InstancedNature.Kind rope, Vector3 from, Vector3 to, float sag)
        {
            // Three straight pieces approximate the catenary.
            var points = new Vector3[4];
            for (var i = 0; i < 4; i++)
            {
                var t = i / 3f;
                points[i] = Vector3.Lerp(from, to, t) + Vector3.down * sag * 4f * t * (1f - t);
            }

            for (var i = 0; i < 3; i++)
            {
                var d = points[i + 1] - points[i];
                Put(nature, rope, (points[i] + points[i + 1]) * 0.5f, Quaternion.LookRotation(d.normalized, Vector3.up), new Vector3(1f, 1f, d.magnitude), d.magnitude * 0.5f);
            }
        }

        private static void Put(InstancedNature nature, InstancedNature.Kind kind, Vector3 position, Quaternion rotation, Vector3 scale, float radius)
        {
            if (kind == null || kind.Near == null) return;
            nature.Add(kind, Matrix4x4.TRS(position, rotation, scale), radius);
        }
    }
}

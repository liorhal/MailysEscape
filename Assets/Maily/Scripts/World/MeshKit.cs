using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Maily
{
    /// <summary>Accumulates flat-shaded low-poly geometry, then bakes one Mesh.</summary>
    public class MeshKit
    {
        public readonly List<Vector3> V = new List<Vector3>();
        public readonly List<Vector3> N = new List<Vector3>();
        public readonly List<int> T = new List<int>();

        public int Count => V.Count;

        public void Tri(Vector3 a, Vector3 b, Vector3 c)
        {
            var n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-10f) return;
            n.Normalize();
            int i = V.Count;
            V.Add(a); V.Add(b); V.Add(c);
            N.Add(n); N.Add(n); N.Add(n);
            T.Add(i); T.Add(i + 1); T.Add(i + 2);
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d) { Tri(a, b, c); Tri(a, c, d); }

        /// <summary>Flat upward polygon at height y (points in XZ, any winding).</summary>
        public void Polygon(IList<Vector2> poly, float y)
        {
            var tris = Geo.Triangulate(poly);
            for (int i = 0; i < tris.Count; i += 3)
            {
                var a = poly[tris[i]]; var b = poly[tris[i + 1]]; var c = poly[tris[i + 2]];
                // Geo returns CCW (seen from +Y looking down => clockwise in Unity's left-handed view),
                // so emit a,c,b for an upward normal.
                Tri(new Vector3(a.x, y, a.y), new Vector3(c.x, y, c.y), new Vector3(b.x, y, b.y));
            }
        }

        /// <summary>Vertical walls around a polygon from y0 to y1, facing outward.</summary>
        public void Walls(IList<Vector2> poly, float y0, float y1)
        {
            bool ccw = Geo.SignedArea(poly) > 0;
            for (int i = 0; i < poly.Count; i++)
            {
                var p = poly[i]; var q = poly[(i + 1) % poly.Count];
                if (!ccw) { var t = p; p = q; q = t; }
                var a = new Vector3(p.x, y0, p.y); var b = new Vector3(q.x, y0, q.y);
                var c = new Vector3(q.x, y1, q.y); var d = new Vector3(p.x, y1, p.y);
                Quad(a, d, c, b);
            }
        }

        /// <summary>Hip-style roof: outer ring at y, inner (shrunk) ring at y+rise, capped.</summary>
        public void HipRoof(IList<Vector2> poly, float y, float overhang, float rise, float shrink)
        {
            var c = Geo.Centroid(poly);
            var outer = new List<Vector2>(); var inner = new List<Vector2>();
            foreach (var p in poly)
            {
                var d = p - c; float len = d.magnitude;
                var dir = len > 1e-4f ? d / len : Vector2.zero;
                outer.Add(p + dir * overhang);
                inner.Add(c + d * shrink);
            }
            bool ccw = Geo.SignedArea(poly) > 0;
            for (int i = 0; i < poly.Count; i++)
            {
                int j = (i + 1) % poly.Count;
                Vector2 o0 = outer[i], o1 = outer[j], i0 = inner[i], i1 = inner[j];
                if (!ccw) { var t = o0; o0 = o1; o1 = t; t = i0; i0 = i1; i1 = t; }
                Quad(new Vector3(o0.x, y, o0.y), new Vector3(i0.x, y + rise, i0.y),
                     new Vector3(i1.x, y + rise, i1.y), new Vector3(o1.x, y, o1.y));
            }
            Polygon(inner, y + rise);
            // underside of the overhang
            for (int i = 0; i < poly.Count; i++)
            {
                int j = (i + 1) % poly.Count;
                Vector2 o0 = outer[i], o1 = outer[j], p0 = poly[i], p1 = poly[j];
                if (ccw) { var t = o0; o0 = o1; o1 = t; t = p0; p0 = p1; p1 = t; }
                Quad(new Vector3(o0.x, y, o0.y), new Vector3(p0.x, y, p0.y),
                     new Vector3(p1.x, y, p1.y), new Vector3(o1.x, y, o1.y));
            }
        }

        /// <summary>Flat ribbon along a polyline (roads, paths).</summary>
        public void Ribbon(IList<Vector2> line, float width, float y)
        {
            float h = width * 0.5f;
            for (int i = 0; i < line.Count - 1; i++)
            {
                var a = line[i]; var b = line[i + 1];
                var d = b - a; if (d.sqrMagnitude < 1e-6f) continue;
                d.Normalize(); var n = new Vector2(-d.y, d.x) * h;
                Quad(new Vector3(a.x - n.x, y, a.y - n.y), new Vector3(a.x + n.x, y, a.y + n.y),
                     new Vector3(b.x + n.x, y, b.y + n.y), new Vector3(b.x - n.x, y, b.y - n.y));
                // round joint: small disc at the next vertex so corners don't gap
                Disc(b, h, y, 8);
            }
            if (line.Count > 0) Disc(line[0], h, y, 8);
        }

        public void Disc(Vector2 c, float r, float y, int seg)
        {
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2 / seg, a1 = (i + 1) * Mathf.PI * 2 / seg;
                Tri(new Vector3(c.x, y, c.y),
                    new Vector3(c.x + Mathf.Cos(a1) * r, y, c.y + Mathf.Sin(a1) * r),
                    new Vector3(c.x + Mathf.Cos(a0) * r, y, c.y + Mathf.Sin(a0) * r));
            }
        }

        /// <summary>Low-poly faceted blob (an icosahedron, optionally jittered) — tree crowns, bushes.</summary>
        public void Blob(Vector3 c, Vector3 r, int seed, float jitter = 0.15f)
        {
            var verts = Ico.Verts; var faces = Ico.Faces;
            var rnd = new System.Random(seed);
            var p = new Vector3[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                float k = 1f + ((float)rnd.NextDouble() * 2 - 1) * jitter;
                p[i] = c + Vector3.Scale(verts[i] * k, r);
            }
            for (int i = 0; i < faces.Length; i += 3) Tri(p[faces[i]], p[faces[i + 1]], p[faces[i + 2]]);
        }

        /// <summary>Faceted vertical prism (trunks, posts).</summary>
        public void Prism(Vector3 bottom, float r0, float r1, float h, int seg)
        {
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2 / seg, a1 = (i + 1) * Mathf.PI * 2 / seg;
                var b0 = bottom + new Vector3(Mathf.Cos(a0) * r0, 0, Mathf.Sin(a0) * r0);
                var b1 = bottom + new Vector3(Mathf.Cos(a1) * r0, 0, Mathf.Sin(a1) * r0);
                var t0 = bottom + new Vector3(Mathf.Cos(a0) * r1, h, Mathf.Sin(a0) * r1);
                var t1 = bottom + new Vector3(Mathf.Cos(a1) * r1, h, Mathf.Sin(a1) * r1);
                Quad(b0, t0, t1, b1);
                Tri(bottom + Vector3.up * h, t1, t0);
            }
        }

        /// <summary>Axis-aligned box (fences, benches).</summary>
        public void Box(Vector3 c, Vector3 size, float yaw = 0)
        {
            var q = Quaternion.Euler(0, yaw, 0); var e = size * 0.5f;
            Vector3 P(float x, float y, float z) => c + q * new Vector3(x * e.x, y * e.y, z * e.z);
            Quad(P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), P(1, -1, -1));
            Quad(P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), P(-1, -1, 1));
            Quad(P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), P(-1, -1, -1));
            Quad(P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1));
            Quad(P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1));
            Quad(P(-1, -1, 1), P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1));
        }

        public Mesh Bake(string name)
        {
            var m = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            m.SetVertices(V); m.SetNormals(N); m.SetTriangles(T, 0);
            m.RecalculateBounds();
            return m;
        }
    }

    static class Ico
    {
        public static readonly Vector3[] Verts;
        public static readonly int[] Faces =
        {
            0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
            3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
        };
        static Ico()
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            Verts = new[]
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
            };
            for (int i = 0; i < Verts.Length; i++) Verts[i] = Verts[i].normalized;
            // This face order already yields outward Cross(b-a, c-a) normals = Unity front faces.
        }
    }

    public static class Geo
    {
        public static float SignedArea(IList<Vector2> p)
        {
            float a = 0;
            for (int i = 0; i < p.Count; i++) { var u = p[i]; var v = p[(i + 1) % p.Count]; a += u.x * v.y - v.x * u.y; }
            return a * 0.5f;
        }

        public static Vector2 Centroid(IList<Vector2> p)
        {
            Vector2 s = Vector2.zero; foreach (var v in p) s += v; return s / Mathf.Max(1, p.Count);
        }

        public static bool Contains(IList<Vector2> poly, Vector2 pt)
        {
            bool inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                if (((poly[i].y > pt.y) != (poly[j].y > pt.y)) &&
                    pt.x < (poly[j].x - poly[i].x) * (pt.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            }
            return inside;
        }

        /// <summary>Ear-clipping triangulation. Returns indices into poly, CCW (math orientation).</summary>
        public static List<int> Triangulate(IList<Vector2> poly)
        {
            var res = new List<int>();
            int n = poly.Count; if (n < 3) return res;
            var idx = new List<int>(n);
            if (SignedArea(poly) > 0) for (int i = 0; i < n; i++) idx.Add(i);
            else for (int i = n - 1; i >= 0; i--) idx.Add(i);
            int guard = 0;
            while (idx.Count > 3 && guard++ < 10000)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int a = idx[(i + idx.Count - 1) % idx.Count], b = idx[i], c = idx[(i + 1) % idx.Count];
                    Vector2 A = poly[a], B = poly[b], C = poly[c];
                    if (Cross(B - A, C - B) <= 1e-7f) continue; // reflex
                    bool ear = true;
                    for (int k = 0; k < idx.Count && ear; k++)
                    {
                        int p = idx[k]; if (p == a || p == b || p == c) continue;
                        if (InTri(poly[p], A, B, C)) ear = false;
                    }
                    if (!ear) continue;
                    res.Add(a); res.Add(b); res.Add(c);
                    idx.RemoveAt(i); clipped = true; break;
                }
                if (!clipped) break; // degenerate — bail out with what we have (fan the rest)
            }
            if (idx.Count >= 3) for (int i = 1; i < idx.Count - 1; i++) { res.Add(idx[0]); res.Add(idx[i]); res.Add(idx[i + 1]); }
            return res;
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        static bool InTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(b - a, p - a), d2 = Cross(c - b, p - b), d3 = Cross(a - c, p - c);
            return d1 >= 0 && d2 >= 0 && d3 >= 0;
        }
    }
}

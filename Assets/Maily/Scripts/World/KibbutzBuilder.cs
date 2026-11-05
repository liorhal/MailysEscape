using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Maily
{
    /// <summary>
    /// Builds a low-poly Kibbutz Hatzor from the OSM layout: ground, lawns/parks/orchards, roads and
    /// footpaths, extruded buildings with terracotta hip roofs, trees and the perimeter fence.
    /// Run in the Editor (edit mode); everything is generated under one root object.
    /// </summary>
    public static class KibbutzBuilder
    {
        public const string RootName = "Kibbutz Hatzor";
        public const int BuildingLayer = 0;

        public class Result
        {
            public GameObject Root;
            public KibbutzLayout Layout;
            public List<List<Vector2>> BuildingPolys = new List<List<Vector2>>();
            public List<(List<Vector2> line, float w, string kind)> Roads = new List<(List<Vector2>, float, string)>();
            public List<(List<Vector2> poly, string kind, string name, float area)> Areas = new List<(List<Vector2>, string, string, float)>();
            public List<Vector2> TreeSpots = new List<Vector2>();
            public Vector2 HomeDoor; public Vector2 HomeYardDir;
        }

        // material buckets
        static Dictionary<string, MeshKit> _kits;
        static Dictionary<string, Material> _mats;

        static MeshKit Kit(string key, Color c, float smooth = 0.12f, float emission = 0f)
        {
            if (!_kits.TryGetValue(key, out var k)) { k = new MeshKit(); _kits[key] = k; _mats[key] = Mats.Get(key, c, smooth, emission); }
            return k;
        }

        public static Result Build(string json, float radius = 600f)
        {
            _kits = new Dictionary<string, MeshKit>();
            _mats = new Dictionary<string, Material>();
            var res = new Result { Layout = KibbutzLayout.FromJson(json) };
            var L = res.Layout;

            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject(RootName);
            res.Root = root;

            // ---------- ground ----------
            var ground = new MeshKit();
            ground.Polygon(new List<Vector2> { new Vector2(-1400, -1400), new Vector2(1400, -1400), new Vector2(1400, 1400), new Vector2(-1400, 1400) }, 0f);
            Emit(root, "Fields", ground, "Field", Palette.Field, false);
            // inside the fence the kibbutz is a sun-dried lawn on the kurkar hill
            var hill = Kit("Kurkar", Color.Lerp(Palette.Lawn, Palette.Kurkar, 0.55f));
            var disc = new List<Vector2>();
            for (int i = 0; i < 48; i++) { float a = i * Mathf.PI * 2 / 48; disc.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (radius + 25)); }
            hill.Polygon(disc, 0.01f);

            var groundCol = new GameObject("Ground Collider");
            groundCol.transform.SetParent(root.transform);
            var bc = groundCol.AddComponent<BoxCollider>();
            bc.size = new Vector3(2800, 1, 2800); bc.center = new Vector3(0, -0.45f, 0); // top at y=0.05

            // ---------- areas ----------
            var areas = L.areas.Select(a => (poly: KibbutzLayout.Points(a.pts), a.kind, a.name))
                               .Select(a => (a.poly, a.kind, a.name, area: Mathf.Abs(Geo.SignedArea(a.poly))))
                               .Where(a => a.poly.Count >= 3 && a.poly.Any(p => p.magnitude < radius))
                               .OrderByDescending(a => a.area).ToList();
            int rank = 0;
            foreach (var a in areas)
            {
                res.Areas.Add(a);
                float y; string key; Color c; float sm = 0.1f;
                switch (a.kind)
                {
                    case "grass": case "village_green": y = 0.04f; key = "Lawn"; c = Palette.Lawn; break;
                    case "park": case "wood": case "forest": case "garden": y = 0.035f; key = "LawnDeep"; c = Palette.LawnDeep; break;
                    case "orchard": case "farmland": case "meadow": case "scrub": y = 0.025f; key = "Orchard"; c = Color.Lerp(Palette.Field, Palette.LawnDeep, 0.45f); break;
                    case "playground": y = 0.06f; key = "Playground"; c = Color.Lerp(Palette.Playground, Palette.Kurkar, 0.45f); break;
                    case "pitch": case "sports_centre": y = 0.055f; key = "Pitch"; c = Palette.Pitch; break;
                    case "swimming_pool": y = 0.07f; key = "Pool"; c = Palette.PoolWater; sm = 0.85f; break;
                    case "parking": y = 0.05f; key = "Parking"; c = Palette.Parking; break;
                    case "cemetery": y = 0.03f; key = "Cemetery"; c = Color.Lerp(Palette.Lawn, Palette.Kurkar, 0.5f); break;
                    case "industrial": y = 0.02f; key = "Industrial"; c = Color.Lerp(Palette.Kurkar, Palette.Parking, 0.5f); break;
                    default: y = 0.03f; key = "Yard"; c = Color.Lerp(Palette.Kurkar, Palette.Lawn, 0.35f); break;
                }
                y += (rank++ % 6) * 0.002f;
                Kit(key, c, sm).Polygon(a.poly, y);
                if (a.kind == "swimming_pool")
                {
                    // pool rim
                    var rim = Kit("PoolRim", Palette.WallWhite);
                    for (int i = 0; i < a.poly.Count; i++)
                    {
                        var p = a.poly[i]; var q = a.poly[(i + 1) % a.poly.Count];
                        var line = new List<Vector2> { p, q };
                        rim.Ribbon(line, 0.6f, y + 0.08f);
                    }
                }
            }

            // ---------- roads ----------
            foreach (var r in L.roads)
            {
                var line = KibbutzLayout.Points(r.pts);
                if (line.Count < 2) continue;
                res.Roads.Add((line, r.w, r.kind));
                bool foot = r.kind == "footway" || r.kind == "path" || r.kind == "cycleway" || r.kind == "pedestrian";
                bool track = r.kind == "track";
                if (foot) Kit("Path", Palette.PathStone).Ribbon(line, r.w, 0.11f);
                else if (track) Kit("Track", Color.Lerp(Palette.Kurkar, Palette.Trunk, 0.3f)).Ribbon(line, r.w, 0.085f);
                else
                {
                    Kit("Curb", Color.Lerp(Palette.Asphalt, Palette.WallWhite, 0.55f)).Ribbon(line, r.w + 0.5f, 0.09f);
                    Kit("Asphalt", Palette.Asphalt, 0.2f).Ribbon(line, r.w, 0.1f);
                }
            }

            // ---------- buildings ----------
            var walls = new MeshKit(); // collider source (walls + roofs, all materials)
            var rnd = new System.Random(1946);
            foreach (var b in L.buildings)
            {
                var poly = KibbutzLayout.Points(b.pts);
                if (poly.Count < 3 || Mathf.Abs(Geo.SignedArea(poly)) < 4f) continue;
                if (!poly.Any(p => p.magnitude < radius)) continue;
                res.BuildingPolys.Add(poly);
                float h = Mathf.Clamp(b.h, 2.4f, 12f);
                string kind = b.kind ?? "yes";
                bool dining = b.name == "Hatzor dining room";
                bool house = kind == "house" || kind == "residential" || kind == "detached" || kind == "kindergarten" || kind == "apartments";
                bool industrial = kind == "industrial" || kind == "warehouse" || kind == "farm_auxiliary" || kind == "shed";
                bool shelter = kind == "shelter" || kind == "bunker";
                bool greenhouse = kind == "greenhouse";
                float area = Mathf.Abs(Geo.SignedArea(poly));
                if (kind == "yes") house = area < 260f; // most untagged small blocks are homes

                Color wall; string wallKey;
                if (dining) { wall = Palette.WallWhite; wallKey = "WallWhite"; }
                else if (industrial) { wall = Palette.Industrial; wallKey = "WallIndustrial"; }
                else if (shelter) { wall = Color.Lerp(Palette.Industrial, Palette.Kurkar, 0.4f); wallKey = "WallShelter"; h = 2.2f; }
                else if (greenhouse) { wall = Palette.Greenhouse; wallKey = "WallGreenhouse"; }
                else if (kind == "kindergarten") { wall = Palette.WallOchre; wallKey = "WallOchre"; }
                else
                {
                    int pick = rnd.Next(3);
                    wall = pick == 0 ? Palette.WallCream : pick == 1 ? Palette.WallWhite : Palette.WallOchre;
                    wallKey = pick == 0 ? "WallCream" : pick == 1 ? "WallWhite" : "WallOchre";
                }
                var wk = Kit(wallKey, wall);
                wk.Walls(poly, 0f, h); walls.Walls(poly, 0f, h);

                if (house && !dining)
                {
                    float rise = Mathf.Clamp(Mathf.Sqrt(area) * 0.22f, 0.9f, 2.4f);
                    Kit("RoofTile", Palette.RoofTile, 0.2f).HipRoof(poly, h, 0.45f, rise, 0.35f);
                    walls.Polygon(poly, h + rise * 0.6f);
                }
                else
                {
                    var roofKey = greenhouse ? "WallGreenhouse" : "RoofFlat";
                    Kit(roofKey, greenhouse ? Palette.Greenhouse : Palette.RoofFlat).Polygon(poly, h);
                    walls.Polygon(poly, h);
                    if (!greenhouse && !shelter) // parapet
                    {
                        var k = Kit(wallKey, wall);
                        for (int i = 0; i < poly.Count; i++)
                            k.Ribbon(new List<Vector2> { poly[i], poly[(i + 1) % poly.Count] }, 0.3f, h + 0.35f);
                    }
                }

                // windows & door — dark insets on the longer walls
                if (!greenhouse)
                {
                    var win = Kit("Window", Color.Lerp(Palette.Shadow, Palette.UiInk, 0.4f), 0.6f);
                    var door = Kit("Door", Color.Lerp(Palette.Trunk, Palette.RoofTile, 0.3f));
                    bool ccw = Geo.SignedArea(poly) > 0;
                    int longest = 0; float best = 0;
                    for (int i = 0; i < poly.Count; i++) { float len = (poly[(i + 1) % poly.Count] - poly[i]).magnitude; if (len > best) { best = len; longest = i; } }
                    for (int i = 0; i < poly.Count; i++)
                    {
                        var p = poly[i]; var q = poly[(i + 1) % poly.Count];
                        float len = (q - p).magnitude; if (len < 3.2f) continue;
                        var d = (q - p) / len; var outN = ccw ? new Vector2(d.y, -d.x) : new Vector2(-d.y, d.x);
                        int nWin = Mathf.Clamp(Mathf.FloorToInt(len / 3.5f), 1, 6);
                        for (int w = 0; w < nWin; w++)
                        {
                            float t = (w + 0.5f) / nWin * len;
                            if (i == longest && w == nWin / 2 && !industrial)
                            {
                                WallInset(door, p + d * t, d, outN, 0.95f, 0f, 2.05f);
                                if (house) { res.HomeDoor = p + d * t + outN * 2.5f; res.HomeYardDir = outN; }
                                continue;
                            }
                            WallInset(win, p + d * t, d, outN, industrial ? 1.6f : 1.1f, 1.0f, Mathf.Min(h - 0.5f, 2.2f));
                        }
                    }
                }
            }
            var bgo = new GameObject("Building Colliders");
            bgo.transform.SetParent(root.transform);
            bgo.isStatic = true;
            var bmesh = Mats.SaveMesh(walls.Bake("Kibbutz_BuildingCollider"));
            bgo.AddComponent<MeshCollider>().sharedMesh = bmesh;

            // ---------- trees ----------
            PlantTrees(res, radius);

            // ---------- perimeter fence ----------
            float fr = radius + 15f;
            var post = Kit("FencePost", Palette.DogGray, 0.3f);
            int segs = 96;
            var fence = new GameObject("Perimeter Fence");
            fence.transform.SetParent(root.transform);
            for (int i = 0; i < segs; i++)
            {
                float a0 = i * Mathf.PI * 2 / segs, a1 = (i + 1) * Mathf.PI * 2 / segs;
                var p0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * fr; var p1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * fr;
                int posts = 10;
                for (int k = 0; k < posts; k++)
                {
                    var p = Vector2.Lerp(p0, p1, k / (float)posts);
                    post.Prism(new Vector3(p.x, 0, p.y), 0.06f, 0.05f, 1.7f, 5);
                }
                var mid = (p0 + p1) * 0.5f; float yaw = -Mathf.Atan2(p1.y - p0.y, p1.x - p0.x) * Mathf.Rad2Deg;
                float len = (p1 - p0).magnitude;
                post.Box(new Vector3(mid.x, 1.65f, mid.y), new Vector3(len, 0.05f, 0.05f), yaw);
                Kit("FenceMesh", Color.Lerp(Palette.DogGray, Palette.Lawn, 0.25f), 0.3f).Box(new Vector3(mid.x, 0.85f, mid.y), new Vector3(len, 1.5f, 0.02f), yaw);
                var col = new GameObject("Fence " + i);
                col.transform.SetParent(fence.transform);
                col.transform.SetPositionAndRotation(new Vector3(mid.x, 1.5f, mid.y), Quaternion.Euler(0, yaw, 0));
                col.AddComponent<BoxCollider>().size = new Vector3(len + 0.5f, 3f, 0.6f);
                col.isStatic = true;
            }

            // ---------- emit all buckets ----------
            var vis = new GameObject("Visuals");
            vis.transform.SetParent(root.transform);
            foreach (var kv in _kits) Emit(vis, kv.Key, kv.Value, kv.Key, Color.white, true);
            return res;
        }

        static void WallInset(MeshKit k, Vector2 c, Vector2 d, Vector2 outN, float width, float y0, float y1)
        {
            var o = outN * 0.03f; var hw = d * (width * 0.5f);
            var a = c - hw + o; var b = c + hw + o;
            // facing outward: build quad so its normal == outN
            var A = new Vector3(a.x, y0, a.y); var B = new Vector3(b.x, y0, b.y);
            var C = new Vector3(b.x, y1, b.y); var D = new Vector3(a.x, y1, a.y);
            var n = Vector3.Cross(D - A, C - A);
            if (Vector3.Dot(n, new Vector3(outN.x, 0, outN.y)) > 0) k.Quad(A, D, C, B); else k.Quad(B, C, D, A);
        }

        static void Emit(GameObject parent, string name, MeshKit kit, string matKey, Color c, bool fromBucket)
        {
            if (kit.Count == 0) return;
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform);
            go.isStatic = true;
            var mesh = Mats.SaveMesh(kit.Bake("Kibbutz_" + name));
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = fromBucket ? _mats[matKey] : Mats.Get(matKey, c);
        }

        // ------------------------------------------------------------------ trees
        static void PlantTrees(Result res, float radius)
        {
            var L = res.Layout;
            var rnd = new System.Random(7);
            var spots = new List<(Vector2 p, int type)>(); // 0 ficus, 1 eucalyptus, 2 citrus, 3 palm, 4 pine
            var orchardSpots = new List<(Vector2 p, int type)>();
            for (int i = 0; i + 1 < (L.trees?.Length ?? 0); i += 2) spots.Add((new Vector2(L.trees[i], L.trees[i + 1]), 0));

            // garden trees: kibbutz homes sit among big shade trees
            foreach (var poly in res.BuildingPolys)
            {
                var c = Geo.Centroid(poly); float area = Mathf.Abs(Geo.SignedArea(poly));
                if (area > 300f || rnd.NextDouble() > 0.75) continue;
                float reach = Mathf.Sqrt(area) * 0.5f + 3.5f + (float)rnd.NextDouble() * 3f;
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2f;
                double roll = rnd.NextDouble();
                spots.Add((c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * reach, roll < 0.55 ? 0 : roll < 0.8 ? 2 : roll < 0.92 ? 1 : 3));
            }

            foreach (var a in res.Areas)
            {
                float density; int type;
                switch (a.kind)
                {
                    case "wood": case "forest": density = 1f / 55f; type = -1; break;
                    case "orchard": density = 0; type = 2; break;
                    case "park": density = 1f / 110f; type = -2; break;
                    case "grass": density = 1f / 200f; type = -2; break;
                    case "cemetery": density = 1f / 100f; type = 4; break;
                    default: continue;
                }
                var min = new Vector2(a.poly.Min(p => p.x), a.poly.Min(p => p.y));
                var max = new Vector2(a.poly.Max(p => p.x), a.poly.Max(p => p.y));
                if (a.kind == "orchard")
                {
                    min = Vector2.Max(min, new Vector2(-radius, -radius)); max = Vector2.Min(max, new Vector2(radius, radius));
                    for (float x = min.x + 2; x < max.x; x += 5.5f)
                        for (float z = min.y + 2; z < max.y; z += 5f)
                        {
                            var p = new Vector2(x + (float)rnd.NextDouble() * 0.6f, z);
                            if (p.magnitude < radius && Geo.Contains(a.poly, p)) orchardSpots.Add((p, 2));
                        }
                    continue;
                }
                int n = Mathf.Min(400, Mathf.RoundToInt(a.area * density));
                for (int i = 0; i < n; i++)
                {
                    var p = new Vector2(Mathf.Lerp(min.x, max.x, (float)rnd.NextDouble()), Mathf.Lerp(min.y, max.y, (float)rnd.NextDouble()));
                    if (!Geo.Contains(a.poly, p)) continue;
                    int t = type >= 0 ? type : type == -1 ? (rnd.NextDouble() < 0.7 ? 1 : 4) : (rnd.NextDouble() < 0.12 ? 3 : rnd.NextDouble() < 0.6 ? 0 : 1);
                    spots.Add((p, t));
                }
            }
            // street trees along residential roads
            foreach (var r in res.Roads)
            {
                if (r.kind != "residential" && r.kind != "living_street" && r.kind != "service") continue;
                for (int i = 0; i < r.line.Count - 1; i++)
                {
                    var a = r.line[i]; var b = r.line[i + 1]; float len = (b - a).magnitude; if (len < 1) continue;
                    var d = (b - a) / len; var nrm = new Vector2(-d.y, d.x);
                    for (float t = 6; t < len; t += 16 + (float)rnd.NextDouble() * 10)
                    {
                        float side = rnd.NextDouble() < 0.5 ? -1 : 1;
                        spots.Add((a + d * t + nrm * side * (r.w * 0.5f + 2.2f), rnd.NextDouble() < 0.5 ? 0 : 1));
                    }
                }
            }

            // orchard rows go last and are capped, so they never crowd out the village's shade trees
            spots.AddRange(orchardSpots.OrderBy(_ => rnd.Next()).Take(900));

            // filter: inside radius, not in buildings, not on roads, not crowded
            var kept = new List<(Vector2 p, int type)>();
            var grid = new Dictionary<(int, int), List<Vector2>>();
            foreach (var s in spots)
            {
                if (s.p.magnitude > radius) continue;
                if (OnRoad(res, s.p, 1.2f)) continue;
                if (InBuilding(res, s.p, 2.2f)) continue;
                var cell = ((int)Mathf.Floor(s.p.x / 4), (int)Mathf.Floor(s.p.y / 4));
                bool crowded = false; float minD = s.type == 2 ? 3.5f : 4.5f;
                for (int dx = -1; dx <= 1 && !crowded; dx++)
                    for (int dz = -1; dz <= 1 && !crowded; dz++)
                        if (grid.TryGetValue((cell.Item1 + dx, cell.Item2 + dz), out var lst))
                            foreach (var q in lst) if ((q - s.p).sqrMagnitude < minD * minD) { crowded = true; break; }
                if (crowded) continue;
                if (!grid.TryGetValue(cell, out var l2)) grid[cell] = l2 = new List<Vector2>();
                l2.Add(s.p);
                kept.Add(s);
                if (kept.Count >= 4200) break;
            }

            var trunk = Kit("Trunk", Palette.Trunk);
            var ficus = Kit("Foliage", Palette.Foliage);
            var euca = Kit("FoliageGray", Palette.FoliageGray);
            var citrus = Kit("FoliageLime", Palette.FoliageLime);
            var fruit = Kit("Citrus", Palette.Citrus, 0.3f);
            var pine = Kit("FoliagePine", Color.Lerp(Palette.Foliage, Palette.Shadow, 0.35f));
            var palm = Kit("FoliagePalm", Color.Lerp(Palette.FoliageLime, Palette.Foliage, 0.5f));
            var cols = new GameObject("Tree Colliders");
            cols.transform.SetParent(res.Root.transform);
            int seed = 0;
            foreach (var (p, type) in kept)
            {
                seed++;
                var b = new Vector3(p.x, 0, p.y);
                float s = 0.8f + (float)rnd.NextDouble() * 0.5f;
                float trunkR;
                switch (type)
                {
                    case 0: // ficus — wide, dark, low
                        trunkR = 0.35f * s;
                        trunk.Prism(b, trunkR, trunkR * 0.7f, 3.2f * s, 6);
                        ficus.Blob(b + new Vector3(0, 4.6f * s, 0), new Vector3(3.6f, 2.4f, 3.6f) * s, seed);
                        ficus.Blob(b + new Vector3(1.6f, 4.0f * s, 0.8f), new Vector3(2.2f, 1.7f, 2.2f) * s, seed + 99);
                        break;
                    case 1: // eucalyptus — tall, airy
                        trunkR = 0.28f * s;
                        trunk.Prism(b, trunkR, trunkR * 0.5f, 9f * s, 5);
                        euca.Blob(b + new Vector3(0, 9.5f * s, 0), new Vector3(2.4f, 3.2f, 2.4f) * s, seed);
                        euca.Blob(b + new Vector3(0.9f, 7.2f * s, -0.5f), new Vector3(1.7f, 2.0f, 1.7f) * s, seed + 7);
                        break;
                    case 2: // citrus
                        trunkR = 0.14f;
                        trunk.Prism(b, 0.14f, 0.1f, 0.9f, 5);
                        citrus.Blob(b + new Vector3(0, 1.9f, 0), new Vector3(1.6f, 1.3f, 1.6f) * s, seed, 0.1f);
                        for (int f = 0; f < 4; f++)
                        {
                            float a = f * 1.57f + seed;
                            fruit.Blob(b + new Vector3(Mathf.Cos(a) * 1.3f * s, 1.7f + (f % 2) * 0.5f, Mathf.Sin(a) * 1.3f * s), Vector3.one * 0.16f, seed + f, 0);
                        }
                        break;
                    case 3: // palm
                        trunkR = 0.25f;
                        trunk.Prism(b, 0.3f, 0.22f, 7f * s, 6);
                        for (int f = 0; f < 6; f++)
                        {
                            float a = f * Mathf.PI / 3;
                            palm.Blob(b + new Vector3(Mathf.Cos(a) * 1.5f, 7f * s, Mathf.Sin(a) * 1.5f), new Vector3(1.9f, 0.25f, 0.6f), seed + f, 0.05f);
                        }
                        break;
                    default: // pine / cypress
                        trunkR = 0.25f;
                        trunk.Prism(b, 0.25f, 0.15f, 2f, 5);
                        pine.Blob(b + new Vector3(0, 4.2f * s, 0), new Vector3(1.3f, 3.6f, 1.3f) * s, seed, 0.08f);
                        break;
                }
                res.TreeSpots.Add(p);
                var c = new GameObject("T");
                c.transform.SetParent(cols.transform);
                c.transform.position = b + Vector3.up * 1.5f;
                var cc = c.AddComponent<CapsuleCollider>(); cc.radius = trunkR + 0.1f; cc.height = 3f;
                c.isStatic = true;
            }
        }

        public static bool OnRoad(Result res, Vector2 p, float margin)
        {
            foreach (var r in res.Roads)
            {
                float lim = r.w * 0.5f + margin;
                for (int i = 0; i < r.line.Count - 1; i++)
                    if (DistSeg(p, r.line[i], r.line[i + 1]) < lim) return true;
            }
            return false;
        }

        public static bool InBuilding(Result res, Vector2 p, float margin)
        {
            foreach (var poly in res.BuildingPolys)
            {
                if (Geo.Contains(poly, p)) return true;
                for (int i = 0; i < poly.Count; i++)
                    if (DistSeg(p, poly[i], poly[(i + 1) % poly.Count]) < margin) return true;
            }
            return false;
        }

        public static float DistSeg(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a; float t = ab.sqrMagnitude < 1e-6f ? 0 : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
        }
    }
}

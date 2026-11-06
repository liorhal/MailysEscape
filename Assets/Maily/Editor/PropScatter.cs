using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Maily.EditorTools
{
    /// <summary>
    /// Dresses the kibbutz with Kenney CC0 models (Assets/Maily/Models/Kenney): cars in the real parking
    /// lots, tractors by the industrial area and orchards, flowers / bushes / planters in house gardens,
    /// kurkar rocks, and stumps / logs in the woods.
    /// </summary>
    public static class PropScatter
    {
        const string Root = "Assets/Maily/Models/Kenney";
        static readonly Dictionary<string, GameObject> Cache = new Dictionary<string, GameObject>();

        static GameObject Load(string rel)
        {
            if (Cache.TryGetValue(rel, out var g) && g != null) return g;
            g = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/{rel}.glb");
            Cache[rel] = g; return g;
        }

        static bool Free(Vector3 p, float r) => !Physics.CheckSphere(new Vector3(p.x, r + 0.25f, p.z), r, ~0, QueryTriggerInteraction.Ignore)
            && !(Physics.Raycast(new Vector3(p.x, 40f, p.z), Vector3.down, out var h, 60f, ~0, QueryTriggerInteraction.Ignore) && h.point.y > 0.6f);

        /// <summary>Instantiate a model scaled so its largest horizontal size (or height) matches, on the ground.</summary>
        static GameObject Place(string rel, Transform parent, Vector3 pos, float yaw, float size, bool byHeight, bool collider)
        {
            var prefab = Load(rel); if (prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(pos, Quaternion.identity);
            var rs = go.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) { Object.DestroyImmediate(go); return null; }
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            float cur = byHeight ? b.size.y : Mathf.Max(b.size.x, b.size.z);
            if (cur > 1e-4f) go.transform.localScale *= size / cur;
            b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            go.transform.position += new Vector3(pos.x - b.center.x, 0.05f - b.min.y, pos.z - b.center.z);
            if (collider)
            {
                b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                var bc = go.AddComponent<BoxCollider>();
                bc.center = go.transform.InverseTransformPoint(b.center);
                var s = go.transform.lossyScale;
                bc.size = new Vector3(b.size.x / s.x, b.size.y / s.y, b.size.z / s.z);
            }
            go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            // the model's pivot may be off-centre: re-centre the rotated footprint on the checked spot
            b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            go.transform.position += new Vector3(pos.x - b.center.x, 0f, pos.z - b.center.z);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            foreach (Transform t in go.GetComponentsInChildren<Transform>()) GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
            return go;
        }

        public static string Scatter(KibbutzBuilder.Result w, System.Random rnd, Transform parent, Vector3 spawn)
        {
            Cache.Clear();
            var root = new GameObject("Props (Kenney CC0)").transform; root.SetParent(parent);
            float R() => (float)rnd.NextDouble();
            bool AwayFromSpawn(Vector3 p) => (p - spawn).magnitude > 7f;
            int cars = 0, tractors = 0, garden = 0, rocks = 0, wood = 0, planters = 0;

            // ---- cars in the real parking lots ----
            string[] carModels = { "Cars/sedan", "Cars/suv", "Cars/hatchback-sports", "Cars/van", "Cars/sedan", "Cars/suv-luxury", "Cars/delivery" };
            foreach (var lot in w.Areas.Where(a => a.kind == "parking").OrderByDescending(a => a.area))
            {
                if (cars >= 46) break;
                var poly = lot.poly; int longest = 0; float best = 0;
                for (int i = 0; i < poly.Count; i++) { float l = (poly[(i + 1) % poly.Count] - poly[i]).magnitude; if (l > best) { best = l; longest = i; } }
                var d = (poly[(longest + 1) % poly.Count] - poly[longest]).normalized; var n = new Vector2(-d.y, d.x);
                var min = new Vector2(poly.Min(p => p.x), poly.Min(p => p.y)); var max = new Vector2(poly.Max(p => p.x), poly.Max(p => p.y));
                var c = Geo.Centroid(poly); float ext = (max - min).magnitude * 0.6f;
                int inLot = 0; float yaw = Mathf.Atan2(n.x, n.y) * Mathf.Rad2Deg;
                for (float a = -ext; a <= ext && inLot < 10; a += 2.9f)
                    for (float b = -ext; b <= ext && inLot < 10; b += 5.6f)
                    {
                        var p2 = c + d * a + n * b;
                        if (!Geo.Contains(poly, p2) || p2.magnitude > 590f || R() > 0.55f) continue;
                        var p = new Vector3(p2.x, 0.05f, p2.y);
                        if (!AwayFromSpawn(p) || !Free(p, 1.3f)) continue;
                        if (Place(carModels[rnd.Next(carModels.Length)], root, p, yaw + (R() < 0.5f ? 0 : 180) + (R() - 0.5f) * 6f, 4.3f, false, true) != null) { cars++; inLot++; }
                    }
            }

            // ---- tractors: industrial yard and orchard edges ----
            foreach (var kind in new[] { "industrial", "orchard", "farmland" })
                foreach (var a in w.Areas.Where(x => x.kind == kind).OrderByDescending(x => x.area).Take(3))
                {
                    for (int t = 0; t < 40 && tractors < 6; t++)
                    {
                        var p2 = a.poly[rnd.Next(a.poly.Count)] * 0.85f + Geo.Centroid(a.poly) * 0.15f;
                        if (p2.magnitude > 580f) continue;
                        var p = new Vector3(p2.x, 0.05f, p2.y);
                        if (!Free(p, 2.2f) || KibbutzBuilder.OnRoad(w, p2, 1f)) continue;
                        string m = R() < 0.65f ? "Cars/tractor" : (R() < 0.5f ? "Cars/tractor-shovel" : "Cars/truck");
                        if (Place(m, root, p, R() * 360f, m.EndsWith("truck") ? 6f : 3.8f, false, true) != null) { tractors++; break; }
                    }
                }

            // ---- house gardens: flowers, bushes, pots along the walls ----
            string[] flowers = { "Nature/flower_redA", "Nature/flower_redB", "Nature/flower_yellowA", "Nature/flower_yellowB", "Nature/flower_purpleA", "Nature/flower_purpleB" };
            string[] bushes = { "Nature/plant_bush", "Nature/plant_bushLarge", "Nature/plant_bushSmall", "Nature/plant_bushDetailed" };
            foreach (var poly in w.BuildingPolys)
            {
                if (garden >= 760) break;
                float area = Mathf.Abs(Geo.SignedArea(poly));
                if (area > 320f || R() > 0.75f) continue;
                bool ccw = Geo.SignedArea(poly) > 0;
                int count = 2 + rnd.Next(4);
                for (int k = 0; k < count; k++)
                {
                    int e = rnd.Next(poly.Count); var a = poly[e]; var b2 = poly[(e + 1) % poly.Count];
                    if ((b2 - a).magnitude < 2f) continue;
                    var dd = (b2 - a).normalized; var outN = ccw ? new Vector2(dd.y, -dd.x) : new Vector2(-dd.y, dd.x);
                    var p2 = Vector2.Lerp(a, b2, 0.15f + R() * 0.7f) + outN * (0.9f + R() * 0.8f);
                    if (KibbutzBuilder.OnRoad(w, p2, 0.6f)) continue;
                    var p = new Vector3(p2.x, 0.05f, p2.y);
                    if (!AwayFromSpawn(p) || !Free(p, 0.3f)) continue;
                    double roll = R();
                    GameObject g = roll < 0.55
                        ? Place(flowers[rnd.Next(flowers.Length)], root, p, R() * 360f, 0.35f + R() * 0.2f, true, false)
                        : roll < 0.9 ? Place(bushes[rnd.Next(bushes.Length)], root, p, R() * 360f, 0.7f + R() * 0.6f, true, false)
                        : Place(R() < 0.5f ? "Nature/pot_large" : "Nature/pot_small", root, p, R() * 360f, 0.55f, true, false);
                    if (g != null) garden++;
                    // a few more flowers clumped with each one
                    if (roll < 0.55)
                        for (int c2 = 0; c2 < 2; c2++)
                        {
                            var q = p + new Vector3((R() - 0.5f) * 0.9f, 0, (R() - 0.5f) * 0.9f);
                            if (Free(q, 0.2f) && Place(flowers[rnd.Next(flowers.Length)], root, q, R() * 360f, 0.3f + R() * 0.2f, true, false) != null) garden++;
                        }
                }
            }

            // ---- kurkar rocks and grass tufts toward the fence ----
            for (int t = 0; t < 900 && rocks < 70; t++)
            {
                float ang = R() * Mathf.PI * 2f, rad = Mathf.Lerp(380f, 590f, Mathf.Sqrt(R()));
                var p = new Vector3(Mathf.Cos(ang) * rad, 0.05f, Mathf.Sin(ang) * rad);
                if (KibbutzBuilder.OnRoad(w, new Vector2(p.x, p.z), 1f) || !Free(p, 1.1f)) continue;
                bool big = R() < 0.3f;
                string m = big ? (R() < 0.5f ? "Nature/rock_largeA" : "Nature/rock_largeC") : R() < 0.5f ? (R() < 0.5f ? "Nature/rock_smallA" : "Nature/rock_smallC") : (R() < 0.5f ? "Nature/grass_large" : "Nature/grass_leafs");
                if (Place(m, root, p, R() * 360f, big ? 1.4f + R() : 0.5f + R() * 0.3f, !big, big) != null) rocks++;
            }

            // ---- woods: stumps and logs ----
            foreach (var a in w.Areas.Where(x => x.kind == "wood" || x.kind == "forest" || x.kind == "park"))
            {
                var min = new Vector2(a.poly.Min(p => p.x), a.poly.Min(p => p.y)); var max = new Vector2(a.poly.Max(p => p.x), a.poly.Max(p => p.y));
                int want = Mathf.Clamp(Mathf.RoundToInt(a.area / 1500f), 0, 6);
                for (int t = 0; t < want * 10 && want > 0; t++)
                {
                    var p2 = new Vector2(Mathf.Lerp(min.x, max.x, R()), Mathf.Lerp(min.y, max.y, R()));
                    if (!Geo.Contains(a.poly, p2) || p2.magnitude > 590f) continue;
                    var p = new Vector3(p2.x, 0.05f, p2.y);
                    if (!Free(p, 0.9f)) continue;
                    bool log = R() < 0.45f;
                    if (Place(log ? (R() < 0.5f ? "Nature/log" : "Nature/log_large") : (R() < 0.5f ? "Nature/stump_round" : "Nature/stump_old"), root, p, R() * 360f, log ? 1.8f : 0.6f, !log, true) != null) { wood++; want--; }
                }
            }

            // ---- planters around the dining hall ----
            var dining = w.Layout.buildings.FirstOrDefault(b => b.name == "Hatzor dining room");
            if (dining != null)
            {
                var poly = KibbutzLayout.Points(dining.pts); bool ccw = Geo.SignedArea(poly) > 0;
                for (int i = 0; i < poly.Count && planters < 10; i++)
                {
                    var a = poly[i]; var b2 = poly[(i + 1) % poly.Count]; if ((b2 - a).magnitude < 6f) continue;
                    var dd = (b2 - a).normalized; var outN = ccw ? new Vector2(dd.y, -dd.x) : new Vector2(-dd.y, dd.x);
                    foreach (var f in new[] { 0.25f, 0.75f })
                    {
                        var p2 = Vector2.Lerp(a, b2, f) + outN * 1.6f; var p = new Vector3(p2.x, 0.05f, p2.y);
                        if (Free(p, 0.5f) && Place("Suburban/planter", root, p, Mathf.Atan2(dd.x, dd.y) * Mathf.Rad2Deg, 1.2f, false, true) != null) planters++;
                    }
                }
            }
            Physics.SyncTransforms();
            return $"props: {cars} cars, {tractors} tractors/trucks, {garden} garden plants, {rocks} rocks/grass, {wood} stumps/logs, {planters} planters";
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Maily.EditorTools
{
    /// <summary>
    /// Moving traffic: bakes two road graphs from the OSM ways (cars: streets; golf carts: streets + footpaths,
    /// which is where kibbutz carts really drive) and spawns Kenney CC0 cars and procedural golf carts on them.
    /// </summary>
    public static class TrafficSetup
    {
        static readonly string[] CarKinds = { "residential", "tertiary", "secondary", "service", "unclassified", "living_street" };
        static readonly string[] CartExtra = { "footway", "path" };
        static readonly string[] CarModels = { "sedan", "suv", "hatchback-sports", "van", "suv-luxury", "delivery", "sedan", "van" };

        public static string Build(KibbutzBuilder.Result w, System.Random rnd, Vector3 spawn)
        {
            var root = new GameObject("Traffic").transform;
            var cars = Graph(w, CarKinds, false, root);
            var carts = Graph(w, CarKinds.Concat(CartExtra).ToArray(), true, root);

            var used = new List<Vector3> { spawn };
            int nCars = 0, nCarts = 0;
            foreach (var model in CarModels)
                if (SpawnCar(cars, model, rnd, root, used)) nCars++;
            Color[] bodies = { Palette.WallWhite, new Color(0.86f, 0.82f, 0.68f), new Color(0.36f, 0.58f, 0.42f), new Color(0.32f, 0.5f, 0.72f), Palette.WallWhite, new Color(0.78f, 0.3f, 0.26f), new Color(0.86f, 0.82f, 0.68f), Palette.WallWhite };
            Color[] shirts = { new Color(0.32f, 0.45f, 0.7f), Palette.WallWhite, new Color(0.55f, 0.62f, 0.4f), new Color(0.7f, 0.42f, 0.32f) };
            for (int i = 0; i < bodies.Length; i++)
            {
                var p = Pick(carts, rnd, used, 35f); if (p == null) continue;
                var go = new GameObject("Golf cart " + (i + 1)); go.transform.SetParent(root); go.transform.position = p.Value;
                go.AddComponent<Rigidbody>().isKinematic = true;
                var v = go.AddComponent<TrafficVehicle>();
                CharacterKit.BuildCart(v, bodies[i], shirts[i % shirts.Length], i % 3 == 1);
                v.Speed = 3.6f + (float)rnd.NextDouble() * 1.2f; v.Lane = 0.55f;
                nCarts++;
            }
            return $"Traffic: car graph {cars.Nodes.Length} nodes, cart graph {carts.Nodes.Length} nodes; {nCars} cars, {nCarts} golf carts";
        }

        static bool SpawnCar(RoadGraph g, string model, System.Random rnd, Transform root, List<Vector3> used)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Maily/Models/Kenney/Cars/{model}.glb");
            var p = Pick(g, rnd, used, 45f);
            if (prefab == null || p == null) return false;
            var go = new GameObject("Car " + model); go.transform.SetParent(root); go.transform.position = p.Value;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, go.transform);
            inst.transform.localPosition = Vector3.zero; inst.transform.localRotation = Quaternion.identity;
            var rs = inst.GetComponentsInChildren<Renderer>();
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            float k = (model == "delivery" ? 5.2f : 4.3f) / Mathf.Max(0.01f, b.size.z);
            inst.transform.localScale = Vector3.one * k;
            b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            inst.transform.position += new Vector3(go.transform.position.x - b.center.x, go.transform.position.y - b.min.y, go.transform.position.z - b.center.z);
            b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);

            go.AddComponent<Rigidbody>().isKinematic = true;
            var bc = go.AddComponent<BoxCollider>(); bc.center = go.transform.InverseTransformPoint(b.center); bc.size = b.size;
            var v = go.AddComponent<TrafficVehicle>();
            v.Wheels = inst.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("wheel-") && (t.name.EndsWith("left") || t.name.EndsWith("right"))).ToArray();
            v.WheelRadius = 0.3f * k; v.Length = b.size.z; v.IsCart = false;
            v.Speed = 6.5f + (float)rnd.NextDouble() * 2f; v.Lane = 1.5f;
            return true;
        }

        static Vector3? Pick(RoadGraph g, System.Random rnd, List<Vector3> used, float gap)
        {
            for (int t = 0; t < 200; t++)
            {
                int i = rnd.Next(g.Nodes.Length);
                var p = g.Nodes[i];
                if (g.Degree(i) == 0 || p.magnitude > 520f) continue;
                if (used.Any(u => (u - p).magnitude < gap)) continue;
                used.Add(p); return p;
            }
            return null;
        }

        /// <summary>Nodes = road vertices merged on a 1 m grid; edges = consecutive vertices; keeps the largest connected piece.</summary>
        static RoadGraph Graph(KibbutzBuilder.Result w, string[] kinds, bool carts, Transform parent)
        {
            var ids = new Dictionary<(int, int), int>(); var pos = new List<Vector3>(); var adj = new List<HashSet<int>>();
            int Node(Vector2 p)
            {
                var key = (Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y));
                if (ids.TryGetValue(key, out var id)) return id;
                id = pos.Count; ids[key] = id; pos.Add(new Vector3(p.x, 0f, p.y)); adj.Add(new HashSet<int>()); return id;
            }
            foreach (var (line, _, kind) in w.Roads)
            {
                if (!kinds.Contains(kind)) continue;
                for (int i = 0; i + 1 < line.Count; i++)
                {
                    if (line[i].magnitude > 600f || line[i + 1].magnitude > 600f) continue;
                    // split long segments so the lane offset follows gentle curves
                    int steps = Mathf.Max(1, Mathf.CeilToInt((line[i + 1] - line[i]).magnitude / 25f));
                    int prev = Node(line[i]);
                    for (int s = 1; s <= steps; s++)
                    {
                        int cur = Node(Vector2.Lerp(line[i], line[i + 1], s / (float)steps));
                        if (cur != prev) { adj[prev].Add(cur); adj[cur].Add(prev); }
                        prev = cur;
                    }
                }
            }
            // largest connected component
            var comp = new int[pos.Count]; for (int i = 0; i < comp.Length; i++) comp[i] = -1;
            int bestC = -1, bestN = 0, c = 0;
            for (int i = 0; i < pos.Count; i++)
            {
                if (comp[i] >= 0) continue;
                int n = 0; var st = new Stack<int>(); st.Push(i); comp[i] = c;
                while (st.Count > 0) { int x = st.Pop(); n++; foreach (var y in adj[x]) if (comp[y] < 0) { comp[y] = c; st.Push(y); } }
                if (n > bestN) { bestN = n; bestC = c; }
                c++;
            }
            var remap = new Dictionary<int, int>(); var nodes = new List<Vector3>();
            for (int i = 0; i < pos.Count; i++) if (comp[i] == bestC) { remap[i] = nodes.Count; nodes.Add(pos[i]); }
            var start = new List<int> { 0 }; var flat = new List<int>();
            for (int i = 0; i < pos.Count; i++)
            {
                if (comp[i] != bestC) continue;
                foreach (var y in adj[i]) flat.Add(remap[y]);
                start.Add(flat.Count);
            }
            var go = new GameObject(carts ? "Road Graph (golf carts)" : "Road Graph (cars)"); go.transform.SetParent(parent);
            var g = go.AddComponent<RoadGraph>();
            g.ForCarts = carts; g.Nodes = nodes.ToArray(); g.AdjStart = start.ToArray(); g.Adj = flat.ToArray();
            return g;
        }
    }
}

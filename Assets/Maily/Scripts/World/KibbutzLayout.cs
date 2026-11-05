using System;
using System.Collections.Generic;
using UnityEngine;

namespace Maily
{
    /// <summary>OpenStreetMap-derived layout of Kibbutz Hatzor, in metres around the dining hall
    /// (x = east, z = north). Data © OpenStreetMap contributors, ODbL.</summary>
    [Serializable]
    public class KibbutzLayout
    {
        public float originLat, originLon, radius;
        public Building[] buildings;
        public Road[] roads;
        public Area[] areas;
        public float[] trees;

        [Serializable] public class Building { public float[] pts; public float h; public string kind; public string name; }
        [Serializable] public class Road { public float[] pts; public float w; public string kind; }
        [Serializable] public class Area { public float[] pts; public string kind; public string name; }

        public static List<Vector2> Points(float[] flat)
        {
            var l = new List<Vector2>(flat.Length / 2);
            for (int i = 0; i + 1 < flat.Length; i += 2) l.Add(new Vector2(flat[i], flat[i + 1]));
            return l;
        }

        public static KibbutzLayout FromJson(string json) => JsonUtility.FromJson<KibbutzLayout>(json);
    }
}

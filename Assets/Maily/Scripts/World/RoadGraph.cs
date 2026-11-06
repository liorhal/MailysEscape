using System.Collections.Generic;
using UnityEngine;

namespace Maily
{
    /// <summary>A drivable network baked from the OSM roads (nodes + adjacency). Built once in the Editor.</summary>
    public class RoadGraph : MonoBehaviour
    {
        public static RoadGraph Cars, Carts;
        public bool ForCarts;
        public Vector3[] Nodes = new Vector3[0];
        public int[] AdjStart = new int[0];  // Nodes.Length + 1 entries
        public int[] Adj = new int[0];

        void OnEnable() { if (ForCarts) Carts = this; else Cars = this; }

        public int Degree(int n) => AdjStart[n + 1] - AdjStart[n];
        public int Neighbour(int n, int i) => Adj[AdjStart[n] + i];

        /// <summary>Next node after arriving at `at` from `from`: any branch but the way back (U-turn at dead ends).</summary>
        public int Next(int from, int at)
        {
            int d = Degree(at);
            if (d == 0) return from;
            if (d == 1) return Neighbour(at, 0);
            for (int tries = 0; tries < 8; tries++)
            {
                int n = Neighbour(at, Random.Range(0, d));
                if (n != from) return n;
            }
            return Neighbour(at, 0);
        }
    }
}

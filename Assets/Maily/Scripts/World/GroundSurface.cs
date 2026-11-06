using System.Collections.Generic;
using UnityEngine;

namespace Maily
{
    /// <summary>Marks a walkable visual surface (lawns, roads, paths…) that carries a MeshCollider, so
    /// paws rest on what is drawn instead of sinking into raised roads and paths.</summary>
    public class GroundSurface : MonoBehaviour
    {
        public static readonly List<Collider> All = new List<Collider>();
        Collider _c;

        void OnEnable() { _c = GetComponent<Collider>(); if (_c != null) All.Add(_c); }
        void OnDisable() { if (_c != null) All.Remove(_c); }
    }

    public static class Ground
    {
        /// <summary>Height of the top-most walkable surface at (x, z); 0 when off every surface.</summary>
        public static float HeightAt(Vector3 p)
        {
            float best = 0f;
            var ray = new Ray(new Vector3(p.x, 2f, p.z), Vector3.down);
            foreach (var c in GroundSurface.All)
                if (c != null && c.Raycast(ray, out var hit, 3f) && hit.point.y > best) best = hit.point.y;
            return best;
        }
    }
}

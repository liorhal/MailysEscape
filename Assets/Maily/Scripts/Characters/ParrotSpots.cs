using UnityEngine;

namespace Maily
{
    /// <summary>Lawn points (baked at build time) where dararot may land after being chased.</summary>
    public class ParrotSpots : MonoBehaviour
    {
        public Vector3[] Spots;
        void Awake() => Parrot.LandingSpots = Spots ?? new Vector3[0];
    }
}

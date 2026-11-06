using System.Collections.Generic;
using UnityEngine;

namespace Maily
{
    /// <summary>A named place in the kibbutz (from the OpenStreetMap data), shown on the overview map.</summary>
    public class Landmark : MonoBehaviour
    {
        public enum Kind { Place, Home }
        public static readonly List<Landmark> All = new List<Landmark>();

        public string Label = "";
        public Kind Type = Kind.Place;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);
    }
}

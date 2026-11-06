using System.Collections.Generic;
using UnityEngine;

namespace Maily
{
    /// <summary>מרוץ שערים — run through 8 gates in order before the clock runs out.</summary>
    public class GateRace : MiniGame
    {
        const int GateCount = 8;
        readonly List<Vector3> _gates = new List<Vector3>();
        readonly List<GameObject> _gateObjs = new List<GameObject>();
        int _next;

        public override string Status => _next < _gates.Count ? $"שער {_next + 1} מתוך {_gates.Count}" : "כל השערים!";
        public override string ResultText => _next >= _gates.Count
            ? $"עברת את כל השערים, ונשארו {Mathf.CeilToInt(TimeLeft)} שניות"
            : $"עברת {_next} שערים מתוך {_gates.Count}";

        public override void Begin()
        {
            TimeLeft = 50f;
            var dir = Spot.transform.forward; dir.y = 0; dir.Normalize();
            var cur = Spot.transform.position;
            for (int i = 0; i < GateCount; i++)
            {
                Vector3? found = null;
                for (int t = 0; t < 40 && found == null; t++)
                {
                    float ang = Random.Range(-65f, 65f) * (t < 20 ? 1f : 2.5f);
                    var d = Quaternion.Euler(0, ang, 0) * dir;
                    var p = cur + d * Random.Range(9f, 14f); p.y = 0.05f;
                    if (IsFree(p, 1.4f) && _gates.TrueForAll(g => FlatDist(g, p) > 6f) && FlatDist(Spot.transform.position, p) > 4f) { found = p; dir = d; }
                }
                if (found == null) { dir = Quaternion.Euler(0, 120, 0) * dir; continue; }
                _gates.Add(found.Value); cur = found.Value;
                _gateObjs.Add(BuildGate(found.Value, dir, i));
            }
            Refresh();
        }

        GameObject BuildGate(Vector3 p, Vector3 dir, int index)
        {
            var root = Empty("Gate " + (index + 1), p);
            root.transform.rotation = Quaternion.LookRotation(dir);
            var side = Vector3.right * 1.3f;
            Prim(PrimitiveType.Cylinder, side + Vector3.up * 0.9f, new Vector3(0.14f, 0.9f, 0.14f), Palette.WallWhite, "GatePost", 0f, root.transform);
            Prim(PrimitiveType.Cylinder, -side + Vector3.up * 0.9f, new Vector3(0.14f, 0.9f, 0.14f), Palette.WallWhite, "GatePost", 0f, root.transform);
            var bar = Prim(PrimitiveType.Cube, Vector3.up * 1.8f, new Vector3(2.75f, 0.16f, 0.16f), Palette.MiniSky, "GateBar", 1.2f, root.transform);
            var flag = Prim(PrimitiveType.Cube, Vector3.up * 1.55f, new Vector3(0.9f, 0.32f, 0.03f), Palette.MiniSky, "GateBar", 1.2f, root.transform);
            Prim(PrimitiveType.Cylinder, Vector3.up * 0.02f, new Vector3(2.6f, 0.01f, 2.6f), Palette.MiniSky, "GateRing", 0.8f, root.transform);
            return root;
        }

        void Refresh()
        {
            for (int i = 0; i < _gateObjs.Count; i++)
            {
                bool passed = i < _next, current = i == _next;
                _gateObjs[i].SetActive(!passed);
                float s = current ? 1f : 0.75f;
                _gateObjs[i].transform.localScale = Vector3.one * s;
                foreach (var r in _gateObjs[i].GetComponentsInChildren<MeshRenderer>())
                    if (r.sharedMaterial.name.Contains("GateBar") || r.sharedMaterial.name.Contains("GateRing") || r.sharedMaterial.name.Contains("GateNext") || r.sharedMaterial.name.Contains("GateLater"))
                        r.sharedMaterial = current ? Mats.Get("GateNext", Palette.CoinGold, 0.2f, 1.8f) : Mats.Get("GateLater", Palette.MiniSky, 0.2f, 0.6f);
            }
        }

        public override bool Tick(float dt)
        {
            TimeLeft -= dt;
            if (_gates.Count == 0) return true;
            if (_next < _gates.Count && FlatDist(MailyPos, _gates[_next]) < 1.7f)
            {
                Sfx.I?.Play2D(Sfx.I.Pickup, 0.9f, 1f + _next * 0.08f);
                Fx.Sparkle(_gates[_next] + Vector3.up * 1.2f, Palette.CoinGold);
                _next++; Score = _next;
                Refresh();
                if (_next >= _gates.Count) { Score = 100 + Mathf.CeilToInt(TimeLeft); return true; }
            }
            return TimeLeft <= 0f;
        }

        public override int Medal()
        {
            if (_next >= _gates.Count) return TimeLeft >= 18f ? 3 : TimeLeft >= 8f ? 2 : 1;
            return _next >= 5 ? 1 : 0;
        }
    }
}

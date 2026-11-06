using System.Collections.Generic;
using UnityEngine;

namespace Maily
{
    /// <summary>חלוקת עיתונים — the Friday paper round: grab a rolled newspaper from the pile, run it to a glowing
    /// doorstep, come back for the next one. Five houses in 90 seconds.</summary>
    public class PaperRound : MiniGame
    {
        class Door { public Vector3 P; public GameObject Beam, Ring; public bool Done; }

        readonly List<Door> _doors = new List<Door>();
        GameObject _carried; Vector3 _pile;
        const int Count = 5;

        public override string Status => $"עיתונים: {Score} / {Count}   ·   " + (_carried != null ? "רוצו לבית מסומן!" : "קחו עיתון מהערימה");
        public override string ResultText => Score >= Count ? "כל העיתונים חולקו — הקיבוץ יודע הכל!" : $"מיילי חילקה {Score} עיתונים";

        public override void Begin()
        {
            TimeLeft = 90f;
            _pile = Spot.transform.position + Spot.transform.right * 1.6f;
            for (int i = 0; i < 6; i++)
            {
                var r = Prim(PrimitiveType.Cylinder, _pile + new Vector3((i % 3 - 1) * 0.16f, 0.1f + (i / 3) * 0.15f, 0), new Vector3(0.14f, 0.2f, 0.14f), Palette.WallWhite, "Newspaper");
                r.transform.rotation = Quaternion.Euler(0, 0, 90);
            }
            // doorsteps: open ground right beside a house, spread out, 18–60 m from the pile
            var center = Spot.transform.position;
            for (int tries = 0; tries < 400 && _doors.Count < Count; tries++)
            {
                var d = Random.insideUnitCircle.normalized * Random.Range(18f, 60f);
                var p = new Vector3(center.x + d.x, 0f, center.z + d.y);
                if (!IsFree(p, 0.7f) || !NextToHouse(p)) continue;
                bool spread = true; foreach (var o in _doors) if (FlatDist(o.P, p) < 12f) { spread = false; break; }
                if (!spread) continue;
                p.y = Ground.HeightAt(p);
                var door = new Door { P = p };
                door.Ring = Prim(PrimitiveType.Cylinder, p + Vector3.up * 0.03f, new Vector3(1.8f, 0.01f, 1.8f), Spot.Accent, "Spot_" + Spot.Game, 1.5f);
                door.Beam = Prim(PrimitiveType.Cylinder, p + Vector3.up * 6f, new Vector3(0.32f, 6f, 0.32f), Spot.Accent, "Spot_" + Spot.Game, 1.5f);
                _doors.Add(door);
            }
        }

        static bool NextToHouse(Vector3 p)
        {
            foreach (var c in Physics.OverlapSphere(p + Vector3.up * 1.2f, 3.2f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (c.GetComponent<GroundSurface>() != null || c.attachedRigidbody != null) continue;
                var b = c.bounds;
                if (b.size.y > 2.2f && (b.size.x > 4f || b.size.z > 4f)) return true; // a building, not a tree or a fence post
            }
            return false;
        }

        public override bool Tick(float dt)
        {
            TimeLeft -= dt;
            var m = MailyPos;
            if (_carried == null && FlatDist(m, _pile) < 1.9f && Score < _doors.Count)
            {
                _carried = Prim(PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.09f, 0.16f, 0.09f), Palette.WallWhite, "Newspaper");
                var gait = MailyController.I.GetComponentInChildren<MailyGait>();
                var head = gait != null && gait.Head != null ? gait.Head : MailyController.I.transform;
                var fwd = MailyController.I.transform.forward;
                _carried.transform.position = head.position + fwd * 0.13f - Vector3.up * 0.05f;
                _carried.transform.rotation = Quaternion.LookRotation(fwd) * Quaternion.Euler(0, 0, 90);
                _carried.transform.SetParent(head, true);
                Sfx.I?.Play2D(Sfx.I.Page, 0.7f, 1.4f);
            }
            foreach (var d in _doors)
            {
                if (d.Done) continue;
                float pulse = 1f + 0.12f * Mathf.Sin(Time.time * 5f);
                d.Ring.transform.localScale = new Vector3(1.8f * pulse, 0.01f, 1.8f * pulse);
                if (_carried != null && FlatDist(m, d.P) < 1.7f)
                {
                    d.Done = true; Score++;
                    _carried.transform.SetParent(null, true);
                    _carried.transform.SetPositionAndRotation(d.P + Vector3.up * 0.08f, Quaternion.Euler(0, Random.Range(0, 180f), 90f));
                    _carried = null;
                    d.Beam.SetActive(false); d.Ring.SetActive(false);
                    Sfx.I?.Play3D(Sfx.I.Thud, d.P, 1f); Sfx.I?.Play2D(Sfx.I.Match, 0.6f, 1f + Score * 0.05f);
                    Fx.Sparkle(d.P + Vector3.up * 0.3f, Spot.Accent);
                }
            }
            return Score >= Mathf.Max(1, _doors.Count) || TimeLeft <= 0f;
        }

        public override int Medal() => Score >= Count ? 3 : Score >= 4 ? 2 : Score >= 2 ? 1 : 0;
    }
}

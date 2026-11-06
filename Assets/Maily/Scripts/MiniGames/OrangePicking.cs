using System.Collections.Generic;
using UnityEngine;

namespace Maily
{
    /// <summary>קטיף תפוזים — oranges drop from the orchard trees; grab as many as you can in 35 s.</summary>
    public class OrangePicking : MiniGame
    {
        class Orange { public GameObject Go; public float Vy, Life; public bool Landed; }
        readonly List<Orange> _oranges = new List<Orange>();
        float _spawnT;
        const float Radius = 13f;

        public override string Status => $"תפוזים: {Score}";
        public override string ResultText => $"מיילי אספה {Score} תפוזים";

        public override void Begin() { TimeLeft = 35f; }

        public override bool Tick(float dt)
        {
            TimeLeft -= dt;
            _spawnT -= dt;
            if (_spawnT <= 0f && TimeLeft > 1f)
            {
                _spawnT = Random.Range(0.35f, 0.65f);
                var c = Spot.transform.position;
                var d = Random.insideUnitCircle * Radius;
                var p = new Vector3(c.x + d.x, 3.8f, c.z + d.y);
                if (MiniGame.IsFree(new Vector3(p.x, 0f, p.z), 0.25f))
                {
                    var go = Prim(PrimitiveType.Sphere, p, Vector3.one * 0.26f, Palette.Citrus, "OrangeFruit", 0.5f);
                    _oranges.Add(new Orange { Go = go, Life = 6f });
                }
            }
            var m = MailyPos;
            for (int i = _oranges.Count - 1; i >= 0; i--)
            {
                var o = _oranges[i]; var t = o.Go.transform;
                if (!o.Landed)
                {
                    o.Vy -= 12f * dt; t.position += Vector3.up * o.Vy * dt;
                    if (t.position.y <= 0.18f) { t.position = new Vector3(t.position.x, 0.18f, t.position.z); o.Vy = -o.Vy * 0.3f; if (Mathf.Abs(o.Vy) < 0.6f) { o.Landed = true; o.Vy = 0f; } }
                }
                else o.Life -= dt;
                if (FlatDist(m, t.position) < 0.85f && t.position.y < 1.3f)
                {
                    Score++;
                    Sfx.I?.Play3D(Sfx.I.Pickup, t.position, 0.8f, 1f + Mathf.Min(Score, 25) * 0.02f);
                    Fx.Sparkle(t.position, Palette.Citrus);
                    Object.Destroy(o.Go); _oranges.RemoveAt(i); continue;
                }
                if (o.Life < 1.2f) t.localScale = Vector3.one * 0.26f * Mathf.Clamp01(o.Life / 1.2f) * (0.8f + 0.2f * Mathf.Sin(Time.time * 20f));
                if (o.Life <= 0f) { Object.Destroy(o.Go); _oranges.RemoveAt(i); }
            }
            return TimeLeft <= 0f;
        }

        public override void Cleanup() { _oranges.Clear(); base.Cleanup(); }
        public override int Medal() => Score >= 22 ? 3 : Score >= 14 ? 2 : Score >= 7 ? 1 : 0;
    }
}

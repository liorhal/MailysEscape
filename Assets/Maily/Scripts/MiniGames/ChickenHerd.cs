using System.Collections.Generic;
using UnityEngine;

namespace Maily
{
    /// <summary>לול התרנגולות — the hens escaped! Herd all six back through the coop's open side in 60 s.
    /// Hens scuttle away from Maily; a bark (E) sends nearby hens running.</summary>
    public class ChickenHerd : MiniGame
    {
        class Hen { public Transform T, Head; public Vector3 Vel; public float Panic, Peck, Wander; public bool Home; }

        readonly List<Hen> _hens = new List<Hen>();
        Vector3 _coop; Quaternion _coopRot; MailyController _m;
        const int Count = 6;

        public override string Status => $"תרנגולות בלול: {Score} / {Count}";
        public override string ResultText => Score >= Count ? "כל התרנגולות בלול — רועת צאן אמיתית!" : $"{Score} תרנגולות חזרו ללול";

        public override void Begin()
        {
            TimeLeft = 60f;
            var s = Spot.transform.position;
            _coop = FindFree(s, 6f, 9f, 2.6f) ?? s + Spot.transform.forward * 7f;
            var toSpot = s - _coop; toSpot.y = 0;
            _coopRot = Quaternion.LookRotation(toSpot.sqrMagnitude > 0.01f ? toSpot.normalized : Vector3.forward); // open side faces the spot
            BuildCoop();

            for (int i = 0; i < Count; i++)
            {
                var p = FindFree(_coop, 7f, 13f, 0.5f) ?? _coop + Quaternion.Euler(0, i * 60f, 0) * Vector3.forward * 9f;
                _hens.Add(BuildHen(p, i));
            }
            _m = MailyController.I;
            if (_m != null) _m.Barked += OnBark;
        }

        void BuildCoop()
        {
            var root = Empty("Coop", _coop); root.transform.rotation = _coopRot;
            var wood = Palette.Trunk; var roof = Palette.RoofTile;
            void Wall(Vector3 pos, Vector3 size)
            {
                var w = Prim(PrimitiveType.Cube, pos, size, wood, "CoopWood", 0f, root.transform);
                w.AddComponent<BoxCollider>();
            }
            Prim(PrimitiveType.Cube, new Vector3(0, 0.04f, 0), new Vector3(3.2f, 0.08f, 3.2f), Color.Lerp(Palette.Kurkar, Palette.Trunk, 0.4f), "CoopFloor", 0f, root.transform);
            Wall(new Vector3(0, 0.8f, -1.6f), new Vector3(3.2f, 1.6f, 0.12f));   // back
            Wall(new Vector3(-1.6f, 0.8f, 0), new Vector3(0.12f, 1.6f, 3.2f));   // sides
            Wall(new Vector3(1.6f, 0.8f, 0), new Vector3(0.12f, 1.6f, 3.2f));
            Wall(new Vector3(-1.15f, 0.8f, 1.6f), new Vector3(0.9f, 1.6f, 0.12f)); // front, open in the middle
            Wall(new Vector3(1.15f, 0.8f, 1.6f), new Vector3(0.9f, 1.6f, 0.12f));
            foreach (var x in new[] { -0.85f, 0.85f })
                Prim(PrimitiveType.Cube, new Vector3(x, 1.95f, 0), new Vector3(1.9f, 0.08f, 3.5f), roof, "CoopRoof", 0f, root.transform).transform.localRotation = Quaternion.Euler(0, 0, x < 0 ? 25 : -25);
            Prim(PrimitiveType.Cube, new Vector3(0, 0.12f, 2.2f), new Vector3(1.3f, 0.06f, 1.3f), wood, "CoopWood", 0f, root.transform).transform.localRotation = Quaternion.Euler(-8, 0, 0); // ramp
            // a glowing doorway so the goal reads at a glance
            Prim(PrimitiveType.Cube, new Vector3(0, 1.62f, 1.6f), new Vector3(1.4f, 0.1f, 0.14f), Spot.Accent, "Spot_" + Spot.Game, 1.4f, root.transform);
        }

        Hen BuildHen(Vector3 p, int i)
        {
            var root = Empty("Hen", p);
            var coat = i % 3 == 0 ? Palette.DogGinger : Palette.WallWhite;
            Prim(PrimitiveType.Sphere, new Vector3(0, 0.26f, 0), new Vector3(0.34f, 0.3f, 0.42f), coat, "Hen" + (i % 3 == 0 ? "Brown" : "White"), 0f, root.transform);
            Prim(PrimitiveType.Sphere, new Vector3(0, 0.36f, -0.2f), new Vector3(0.18f, 0.24f, 0.14f), coat, "Hen" + (i % 3 == 0 ? "Brown" : "White"), 0f, root.transform); // tail
            var head = Empty("HeadPivot", Vector3.zero).transform; head.SetParent(root.transform, false); head.localPosition = new Vector3(0, 0.42f, 0.16f);
            Spawned.Remove(head.gameObject);
            Prim(PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.16f, coat, "Hen" + (i % 3 == 0 ? "Brown" : "White"), 0f, head);
            Prim(PrimitiveType.Cube, new Vector3(0, 0.1f, 0), new Vector3(0.03f, 0.08f, 0.1f), Palette.BullyRed, "HenComb", 0f, head);
            Prim(PrimitiveType.Cube, new Vector3(0, -0.01f, 0.1f), new Vector3(0.04f, 0.04f, 0.08f), Palette.CoinGold, "HenBeak", 0f, head);
            foreach (var x in new[] { -0.07f, 0.07f })
                Prim(PrimitiveType.Cylinder, new Vector3(x, 0.07f, 0), new Vector3(0.025f, 0.07f, 0.025f), Palette.CoinGold, "HenBeak", 0f, root.transform);
            root.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
            return new Hen { T = root.transform, Head = head, Wander = Random.value * 10f };
        }

        void OnBark()
        {
            foreach (var h in _hens)
                if (!h.Home && FlatDist(h.T.position, MailyPos) < 7f) { h.Panic = 1.6f; Sfx.I?.Play3D(Sfx.I.Cluck, h.T.position, 0.8f, Random.Range(1.1f, 1.35f)); }
        }

        bool InCoop(Vector3 p)
        {
            var local = Quaternion.Inverse(_coopRot) * (p - _coop);
            return Mathf.Abs(local.x) < 1.45f && local.z > -1.45f && local.z < 1.45f;
        }

        public override bool Tick(float dt)
        {
            TimeLeft -= dt;
            var m = MailyPos;
            foreach (var h in _hens)
            {
                h.Panic -= dt; h.Wander += dt;
                var p = h.T.position;
                Vector3 want;
                float speed;
                if (h.Home)
                {
                    // potter about inside
                    want = (_coop - p) * 0.4f + new Vector3(Mathf.Sin(h.Wander * 0.7f), 0, Mathf.Cos(h.Wander * 0.9f)) * 0.3f;
                    speed = 0.4f;
                }
                else
                {
                    var away = p - m; away.y = 0; float d = away.magnitude;
                    float fear = Mathf.Clamp01((3.8f - d) / 3f);
                    want = new Vector3(Mathf.Sin(h.Wander * 0.5f + h.GetHashCode()), 0, Mathf.Cos(h.Wander * 0.6f)) * 0.25f;
                    if (fear > 0f || h.Panic > 0f) want += away.normalized * (1f + (h.Panic > 0f ? 1f : 0f));
                    var toCoop = _coop - p; toCoop.y = 0;
                    if (toCoop.magnitude > 18f) want += toCoop.normalized * 0.8f; // don't wander off the farm
                    speed = h.Panic > 0f ? 4.2f : fear > 0f ? 2.2f + fear * 1.6f : 0.35f;
                }
                want.y = 0;
                var vel = want.sqrMagnitude > 0.0001f ? want.normalized * speed : Vector3.zero;
                h.Vel = Vector3.Lerp(h.Vel, vel, 1f - Mathf.Exp(-dt * 6f));
                if (h.Vel.sqrMagnitude > 0.0004f)
                {
                    var dir = h.Vel.normalized;
                    // walls, houses and trees: slide along by turning
                    if (Physics.Raycast(p + Vector3.up * 0.25f, dir, 0.45f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        var side = Vector3.Cross(Vector3.up, dir);
                        h.Vel = (Physics.Raycast(p + Vector3.up * 0.25f, side, 0.6f, ~0, QueryTriggerInteraction.Ignore) ? -side : side) * h.Vel.magnitude;
                    }
                    p += h.Vel * dt;
                    h.T.rotation = Quaternion.Slerp(h.T.rotation, Quaternion.LookRotation(h.Vel.normalized), 1f - Mathf.Exp(-dt * 10f));
                }
                p.y = Ground.HeightAt(p);
                h.T.position = p;
                // body bob while running, pecking while idle
                float run = Mathf.Clamp01(h.Vel.magnitude / 3f);
                h.T.GetChild(0).localPosition = new Vector3(0, 0.26f + Mathf.Abs(Mathf.Sin(h.Wander * 18f)) * 0.05f * run, 0);
                h.Peck = run < 0.2f ? Mathf.Max(0f, Mathf.Sin(h.Wander * 3f)) : 0f;
                h.Head.localRotation = Quaternion.Euler(h.Peck * 60f, 0, 0);

                if (!h.Home && InCoop(p))
                {
                    h.Home = true; Score++;
                    Sfx.I?.Play3D(Sfx.I.Cluck, p, 1f, 0.95f); Sfx.I?.Play2D(Sfx.I.Match, 0.6f, 1f + Score * 0.05f);
                    Fx.Sparkle(p + Vector3.up * 0.4f, Spot.Accent);
                }
                else if (!h.Home && h.Panic <= 0f && Random.value < dt * 0.15f) Sfx.I?.Play3D(Sfx.I.Cluck, p, 0.5f, Random.Range(1f, 1.3f));
            }
            return Score >= Count || TimeLeft <= 0f;
        }

        public override void Cleanup()
        {
            if (_m != null) _m.Barked -= OnBark;
            _hens.Clear();
            base.Cleanup();
        }

        public override int Medal() => Score >= 6 ? 3 : Score >= 4 ? 2 : Score >= 2 ? 1 : 0;
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Maily
{
    /// <summary>
    /// A car or a kibbutz golf cart (קלנועית) wandering the road network. Pure-pursuit steering keeps it to the
    /// right of the road centre; it slows for bends, stops for Maily, dogs and other vehicles, and honks.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class TrafficVehicle : MonoBehaviour
    {
        public static readonly List<TrafficVehicle> All = new List<TrafficVehicle>();

        public bool IsCart;
        public float Speed = 6f;
        public float Lane = 1.4f;          // metres right of the centre line
        public float Length = 4f;
        public Transform[] Wheels;
        public float WheelRadius = 0.32f;
        public Transform Driver;           // bobs a little (carts)

        RoadGraph _g;
        int _from, _to;
        float _v, _honkCd, _stuck, _phase;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Start()
        {
            _g = IsCart ? RoadGraph.Carts : RoadGraph.Cars;
            if (_g == null || _g.Nodes.Length < 2) { enabled = false; return; }
            // start at the nearest node, heading to any neighbour
            float best = float.MaxValue;
            for (int i = 0; i < _g.Nodes.Length; i++)
            {
                float d = (_g.Nodes[i] - transform.position).sqrMagnitude;
                if (d < best && _g.Degree(i) > 0) { best = d; _from = i; }
            }
            _to = _g.Neighbour(_from, Random.Range(0, _g.Degree(_from)));
            var p = _g.Nodes[_from]; p.y = transform.position.y;
            transform.position = p + Right(_from, _to) * Lane;
            var dir = _g.Nodes[_to] - _g.Nodes[_from]; dir.y = 0;
            if (dir.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(dir);
            _phase = Random.value * 10f;
            GetComponent<Rigidbody>().isKinematic = true;
        }

        Vector3 Right(int a, int b)
        {
            var d = _g.Nodes[b] - _g.Nodes[a]; d.y = 0;
            if (d.sqrMagnitude < 0.0001f) return Vector3.zero;
            d.Normalize(); return new Vector3(d.z, 0, -d.x);
        }

        void Update()
        {
            float dt = Time.deltaTime; if (dt <= 0f) return;
            var gm = GameManager.I;
            bool frozen = gm != null && gm.State == GameState.Fighting; // the street fight owns the screen
            _honkCd -= dt;

            // target: the next node, shifted into our lane
            var target = _g.Nodes[_to] + Right(_from, _to) * Lane;
            var pos = transform.position;
            var to = target - pos; to.y = 0;
            if (to.magnitude < Mathf.Max(2.2f, Length * 0.5f))
            {
                int nx = _g.Next(_from, _to); _from = _to; _to = nx;
                target = _g.Nodes[_to] + Right(_from, _to) * Lane; to = target - pos; to.y = 0;
            }

            // speed: slow into bends, stop for anything in front
            float bend = Vector3.Angle(transform.forward, to);
            float want = Speed * Mathf.Lerp(1f, 0.35f, Mathf.Clamp01((bend - 15f) / 75f));
            float block = Blocked(out bool forMaily);
            if (block < 999f)
            {
                want = block < Length * 0.5f + 1.2f ? 0f : Mathf.Min(want, (block - Length * 0.5f - 1f) * 1.2f);
                if (forMaily && _v > 0.5f && _honkCd <= 0f && Sfx.I != null)
                {
                    Sfx.I.Play3D(IsCart ? Sfx.I.Beep : Sfx.I.Honk, pos, 0.9f, IsCart ? 1f : Random.Range(0.92f, 1.06f));
                    _honkCd = 3.5f;
                }
            }
            if (frozen) want = 0f;
            _v = Mathf.MoveTowards(_v, want, (want < _v ? 9f : 2.5f) * dt);

            // stuck behind something that isn't moving away (a parked prop, another car head-on): turn around
            _stuck = _v < 0.1f && !frozen && !forMaily ? _stuck + dt : 0f;
            if (_stuck > 5f) { (_from, _to) = (_to, _from); _stuck = 0f; }

            if (to.sqrMagnitude > 0.01f)
            {
                float turn = (IsCart ? 75f : 60f) * Mathf.Clamp(_v / 2f, 0.25f, 1.4f);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), turn * dt);
            }
            pos += transform.forward * _v * dt;
            pos.y = Ground.HeightAt(pos);
            transform.position = pos;

            if (Wheels != null)
                foreach (var w in Wheels) if (w != null) w.Rotate(Vector3.right, _v * dt / WheelRadius * Mathf.Rad2Deg, Space.Self);
            if (Driver != null)
            {
                _phase += dt * (2f + _v);
                Driver.localRotation = Quaternion.Euler(Mathf.Sin(_phase) * 2f * Mathf.Min(1f, _v), Mathf.Sin(_phase * 0.37f) * 12f, 0f);
            }
        }

        /// <summary>Distance to the nearest thing ahead in our path (999 = clear).</summary>
        float Blocked(out bool forMaily)
        {
            bool hitMaily = false;
            float look = 3f + _v * 1.4f + Length * 0.5f;
            var p = transform.position; var f = transform.forward;
            float best = 999f;
            void Check(Vector3 q, bool maily)
            {
                var d = q - p; d.y = 0;
                float ahead = Vector3.Dot(d, f);
                if (ahead <= 0f || ahead > look) return;
                float side = Mathf.Abs(Vector3.Dot(d, transform.right));
                if (side > 1.4f) return;
                if (ahead < best) { best = ahead; hitMaily = maily; }
            }
            if (MailyController.I != null) Check(MailyController.I.transform.position, true);
            foreach (var d in NpcDog.All) Check(d.transform.position, false);
            foreach (var d in HostileDog.All) Check(d.transform.position, false);
            foreach (var v in All)
            {
                if (v == this) continue;
                // ignore oncoming traffic in the other lane, but not a car heading straight at us
                if (Vector3.Dot(v.transform.forward, transform.forward) < -0.3f && Mathf.Abs(Vector3.Dot(v.transform.position - p, transform.right)) > 1.2f) continue;
                Check(v.transform.position - f * v.Length * 0.5f, false);
            }
            forMaily = hitMaily;
            return best;
        }
    }
}

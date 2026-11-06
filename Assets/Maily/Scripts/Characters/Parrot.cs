using System.Collections.Generic;
using UnityEngine;

namespace Maily
{
    /// <summary>
    /// A darara (rose-ringed parakeet). Pecks on lawns; hops away when Maily comes close; if Maily
    /// catches up it bursts into the air squawking (counts as chased), circles, then lands elsewhere.
    /// </summary>
    public class Parrot : MonoBehaviour
    {
        public static readonly List<Parrot> All = new List<Parrot>();
        public static Vector3[] LandingSpots = new Vector3[0];

        public Transform Body, Head, WingL, WingR;
        public bool Chased { get; private set; }
        public bool Airborne => _mode != Mode.Ground && _mode != Mode.Flee;

        enum Mode { Ground, Flee, TakeOff, Fly, Land }
        Mode _mode;
        Vector3 _flyTarget, _circleCenter, _landSpot;
        float _t, _peck, _flap, _circleAngle, _hopPhase;

        const float NoticeDist = 9f, CatchDist = 1.7f, FleeSpeed = 5.6f;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Start() { _peck = Random.value * 3f; _hopPhase = Random.value; }

        void Update()
        {
            float dt = Time.deltaTime; _t += dt;
            var maily = MailyController.I;
            Vector3 toM = maily != null ? maily.transform.position - transform.position : Vector3.one * 999; toM.y = 0;
            float dM = toM.magnitude;
            bool playing = GameManager.I == null || GameManager.I.State == GameState.Playing;

            switch (_mode)
            {
                case Mode.Ground:
                    _peck += dt;
                    Body.localRotation = Quaternion.Euler(-25 + (Mathf.Repeat(_peck, 2.2f) < 0.35f ? 45f : 0f), 0, 0);
                    if (Mathf.Repeat(_peck, 4f) < dt) transform.Rotate(0, Random.Range(-90f, 90f), 0);
                    if (playing && dM < NoticeDist) { _mode = Mode.Flee; _t = 0; Sfx.I?.Play3D(Sfx.I.Squawk, transform.position, 0.35f, 1.3f); }
                    Wings(0f, dt);
                    break;

                case Mode.Flee:
                    if (dM < CatchDist) { TakeOff(true); break; }
                    if (dM > NoticeDist * 1.6f || _t > 9f) { if (_t > 9f) TakeOff(false); else _mode = Mode.Ground; break; }
                    var away = -toM.normalized;
                    // zig-zag hops away from Maily
                    away = Quaternion.Euler(0, Mathf.Sin(_t * 3.1f) * 35f, 0) * away;
                    if (Physics.Raycast(transform.position + Vector3.up * 0.3f, away, 1.2f, ~0, QueryTriggerInteraction.Ignore))
                        away = Quaternion.Euler(0, 90, 0) * away;
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(away), dt * 10f);
                    transform.position += away * FleeSpeed * dt;
                    _hopPhase += dt * 7f;
                    var p = transform.position; p.y = Ground.HeightAt(p) + Mathf.Abs(Mathf.Sin(_hopPhase * Mathf.PI)) * 0.35f; transform.position = p;
                    Body.localRotation = Quaternion.Euler(-10, 0, 0);
                    Wings(0.5f, dt);
                    break;

                case Mode.TakeOff:
                    transform.position += (Vector3.up * 6f + transform.forward * 3f) * dt;
                    Wings(1f, dt);
                    Body.localRotation = Quaternion.Euler(-35, 0, 0);
                    if (transform.position.y > 9f) { _mode = Mode.Fly; _t = 0; _circleCenter = transform.position + transform.right * 10f; }
                    break;

                case Mode.Fly:
                    Wings(1f, dt);
                    if (_t < 6f)
                    {
                        _circleAngle += dt * 0.9f;
                        var target = _circleCenter + new Vector3(Mathf.Cos(_circleAngle), 0, Mathf.Sin(_circleAngle)) * 10f + Vector3.up * Mathf.Sin(_t) * 1.5f;
                        MoveFly(target, 10f, dt);
                    }
                    else
                    {
                        if (_landSpot == Vector3.zero) _landSpot = PickLanding();
                        var above = _landSpot + Vector3.up * 8f;
                        MoveFly(above, 13f, dt);
                        if ((above - transform.position).magnitude < 1.5f) { _mode = Mode.Land; }
                    }
                    break;

                case Mode.Land:
                    Wings(0.7f, dt);
                    MoveFly(_landSpot + Vector3.up * 0.05f, 4f, dt);
                    if ((transform.position - _landSpot).magnitude < 0.3f)
                    {
                        transform.position = _landSpot + Vector3.up * 0.05f;
                        transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y, 0);
                        _mode = Mode.Ground; _landSpot = Vector3.zero;
                    }
                    break;
            }
            Head.localRotation = Quaternion.Euler(25, Mathf.Sin(_t * 2.3f) * 30f, 0);
        }

        void TakeOff(bool caught)
        {
            _mode = Mode.TakeOff; _t = 0;
            Sfx.I?.Play3D(Sfx.I.Squawk, transform.position, 1f, Random.Range(0.9f, 1.15f));
            Fx.Feathers(transform.position + Vector3.up * 0.3f);
            if (caught && !Chased) { Chased = true; GameManager.I?.ChasedParrot(); }
        }

        void MoveFly(Vector3 target, float speed, float dt)
        {
            var d = target - transform.position;
            if (d.sqrMagnitude < 0.001f) return;
            transform.position = Vector3.MoveTowards(transform.position, target, speed * dt);
            var flat = d; flat.y *= 0.3f;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat.normalized), dt * 4f);
        }

        Vector3 PickLanding()
        {
            if (LandingSpots.Length == 0) return transform.position + Random.insideUnitSphere * 30f;
            for (int i = 0; i < 8; i++)
            {
                var s = LandingSpots[Random.Range(0, LandingSpots.Length)];
                if ((s - transform.position).magnitude < 120f) return new Vector3(s.x, 0f, s.z);
            }
            var any = LandingSpots[Random.Range(0, LandingSpots.Length)];
            return new Vector3(any.x, 0f, any.z);
        }

        void Wings(float intensity, float dt)
        {
            _flap += dt * Mathf.Lerp(2f, 22f, intensity);
            float a = intensity > 0.05f ? Mathf.Sin(_flap) * 55f * intensity : -6f;
            WingL.localRotation = Quaternion.Euler(0, 0, a);
            WingR.localRotation = Quaternion.Euler(0, 0, -a);
        }
    }
}

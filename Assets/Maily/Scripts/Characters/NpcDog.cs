using System.Collections.Generic;
using UnityEngine;

namespace Maily
{
    /// <summary>A kibbutz dog: wanders its patch, greets Maily (hearts, bark, wagging), follows her a bit.</summary>
    public class NpcDog : MonoBehaviour
    {
        public static readonly List<NpcDog> All = new List<NpcDog>();

        public string DogName = "Dog";
        public float Size = 1f;
        public Transform Body, Head, Tail, EarL, EarR;
        public Transform[] Legs;
        public float WanderRadius = 14f;

        public bool Met { get; private set; }
        Vector3 _home, _target, _vel;
        float _wait, _phase, _gait, _wag, _happy, _followUntil, _barkTimer, _hop;
        enum Mode { Wander, Greet, Follow }
        Mode _mode;
        Vector3 _bodyBase;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Start()
        {
            _home = transform.position; _target = _home; _wait = Random.Range(0.5f, 3f);
            _bodyBase = Body.localPosition; _barkTimer = Random.Range(6f, 16f);
            _phase = Random.value * 10f;
        }

        void Update()
        {
            var maily = MailyController.I;
            float dt = Time.deltaTime;
            Vector3 toM = maily != null ? maily.transform.position - transform.position : Vector3.one * 999;
            toM.y = 0; float dM = toM.magnitude;
            bool playing = GameManager.I == null || GameManager.I.State == GameState.Playing;

            if (!Met && playing && dM < 3.2f) Greet();

            float speed = 0f; Vector3 dest = transform.position;
            switch (_mode)
            {
                case Mode.Greet:
                    _happy -= dt; Face(toM, dt * 8f);
                    _hop += dt * 9f;
                    if (_happy <= 0) { _mode = Mode.Follow; _followUntil = Time.time + 9f; }
                    break;
                case Mode.Follow:
                    if (Time.time > _followUntil || dM > 25f) { _mode = Mode.Wander; _target = _home; break; }
                    if (dM > 2.6f) { dest = maily.transform.position - toM.normalized * 2.2f; speed = Mathf.Min(maily.SprintSpeed * 0.95f, dM * 1.6f); }
                    else Face(toM, dt * 6f);
                    break;
                default:
                    if (_wait > 0) { _wait -= dt; if (Met && dM < 6f) Face(toM, dt * 4f); }
                    else
                    {
                        var d = _target - transform.position; d.y = 0;
                        if (d.magnitude < 0.6f) { _wait = Random.Range(1.5f, 5f); PickTarget(); }
                        else { dest = _target; speed = 1.6f * Mathf.Lerp(0.8f, 1.2f, Size - 0.5f); }
                    }
                    break;
            }

            if (speed > 0f)
            {
                var dir = dest - transform.position; dir.y = 0;
                if (dir.sqrMagnitude > 0.01f)
                {
                    dir.Normalize();
                    if (Physics.Raycast(transform.position + Vector3.up * 0.4f * Size, dir, 1.0f * Size + 0.3f, ~0, QueryTriggerInteraction.Ignore))
                    { if (_mode == Mode.Wander) { PickTarget(); _wait = 0.5f; } speed = 0f; }
                    else
                    {
                        Face(dir, dt * 6f);
                        transform.position += dir * speed * dt;
                    }
                }
            }
            var p = transform.position; p.y = 0.05f; transform.position = p;
            _vel = Vector3.Lerp(_vel, Vector3.forward * speed, 1f - Mathf.Exp(-10f * dt));

            // occasional ambient bark
            _barkTimer -= dt;
            if (_barkTimer <= 0f) { _barkTimer = Random.Range(10f, 25f); if (dM < 40f) Bark(); }

            Animate(dt, speed);
        }

        void Greet()
        {
            Met = true; _mode = Mode.Greet; _happy = 1.4f; _hop = 0f;
            Bark();
            Sfx.I?.Play3D(Sfx.I.Heart, transform.position, 0.8f);
            Fx.Hearts(transform.position + Vector3.up * 1.1f * Size);
            GameManager.I?.MetDog(DogName);
        }

        public void Bark()
        {
            if (Sfx.I == null) return;
            var clip = Size < 0.85f ? Sfx.I.Yip : Sfx.I.Bark;
            Sfx.I.Play3D(clip, transform.position, 0.9f, Mathf.Lerp(1.35f, 0.8f, (Size - 0.6f) / 0.65f));
            _wag = 1f;
        }

        void PickTarget()
        {
            var r = Random.insideUnitCircle * WanderRadius;
            _target = _home + new Vector3(r.x, 0, r.y);
        }

        void Face(Vector3 dir, float k)
        {
            dir.y = 0; if (dir.sqrMagnitude < 0.001f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Mathf.Clamp01(k));
        }

        void Animate(float dt, float speed)
        {
            float speedN = Mathf.Clamp01(speed / 6f);
            _gait = Mathf.MoveTowards(_gait, speedN, dt / 0.3f);
            _phase += dt * Mathf.Lerp(5f, 14f, speedN) / Mathf.Sqrt(Size);
            for (int i = 0; i < Legs.Length; i++)
            {
                float ph = _phase + (i == 0 || i == 3 ? 0 : Mathf.PI);
                Legs[i].localRotation = Quaternion.Euler(Mathf.Sin(ph) * 32f * _gait, 0, 0);
            }
            float hop = _mode == Mode.Greet ? Mathf.Abs(Mathf.Sin(_hop)) * 0.25f * Size : 0f;
            float breathe = Mathf.Sin(Time.time * Mathf.PI) * 0.012f * Size;
            Body.localPosition = _bodyBase + Vector3.up * (Mathf.Abs(Mathf.Sin(_phase)) * 0.05f * Size * _gait + hop + breathe);
            Body.localRotation = Quaternion.Euler(Mathf.Sin(_phase * 2f) * 3f * _gait, 0, Mathf.Sin(_phase) * 2f * _gait);

            bool happy = _mode != Mode.Wander || (Met && _wag > 0);
            _wag = Mathf.MoveTowards(_wag, 0f, dt / 2.5f);
            float wagSpeed = happy ? 22f : 6f, wagAmp = happy ? 38f : 12f;
            var tb = Tail.localEulerAngles;
            Tail.localRotation = Quaternion.Euler(tb.x > 180 ? tb.x - 360 : tb.x, Mathf.Sin(Time.time * wagSpeed) * wagAmp, 0);
            float ear = Mathf.Sin(Time.time * 3f + _phase) * 4f + (happy ? -10f : 0f);
            EarL.localRotation = Quaternion.Euler(ear, 0, 0); EarR.localRotation = Quaternion.Euler(ear, 0, 0);
            if (MailyController.I != null)
            {
                var look = MailyController.I.transform.position - Head.position; look.y = 0;
                var local = Quaternion.Inverse(transform.rotation) * look;
                float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -55f, 55f);
                if (look.magnitude > 12f) yaw = Mathf.Sin(Time.time * 0.4f + _phase) * 20f;
                Head.localRotation = Quaternion.Slerp(Head.localRotation, Quaternion.Euler(-6, yaw, 0), 1f - Mathf.Exp(-5f * dt));
            }
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Maily
{
    /// <summary>
    /// A street dog (כלב רחוב). Patrols its patch, growls when Maily comes near, charges, and starts a
    /// fight on contact. In a fight FightManager owns its position and feeds pose values; once beaten
    /// it calms down for good (wags, never challenges again).
    /// </summary>
    public class HostileDog : MonoBehaviour
    {
        public static readonly List<HostileDog> All = new List<HostileDog>();

        public string DogName = "כלב רחוב";
        public float Size = 1.3f;
        public Transform Body, Head, Tail;
        public Transform[] Legs;

        [Header("Fight personality")]
        public float MaxHp = 100f;
        public float Aggression = 0.6f;   // how often it attacks when in range
        public float BlockChance = 0.3f;  // reacts to Maily's attacks by blocking
        public float ReactTime = 0.35f;   // seconds between decisions
        public float DamageMul = 1f;
        public float SpeedMul = 1f;

        [Header("Exploration")]
        public float PatrolRadius = 14f, NoticeDist = 11f, GiveUpDist = 24f, ChargeSpeed = 6.3f;

        public bool Defeated { get; private set; }
        public bool InFight { get; private set; }
        public Vector3 Home { get; private set; }

        // pose inputs written by FightManager each frame while fighting
        [System.NonSerialized] public float FightSpeed, FightLunge, FightCrouch, FightHurt;

        enum Mode { Patrol, Charge, Return }
        Mode _mode;
        Vector3 _target;
        float _wait, _cooldown, _growlT, _phase, _gait;
        Vector3 _bodyBase;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Start()
        {
            Home = transform.position; _target = Home; _bodyBase = Body.localPosition;
            _phase = Random.value * 10f;
        }

        public void EnterFight() { InFight = true; _mode = Mode.Patrol; }

        public void ExitFight(bool beaten)
        {
            InFight = false; FightLunge = FightCrouch = FightHurt = FightSpeed = 0f;
            transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y, 0);
            var p = transform.position; p.y = 0.05f; transform.position = p;
            if (beaten) { Defeated = true; _mode = Mode.Return; _target = Home; }
            else { _cooldown = 15f; _mode = Mode.Return; _target = Home; }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (InFight) { Animate(dt, FightSpeed, true); return; }

            _cooldown -= dt;
            var maily = MailyController.I;
            Vector3 toM = maily != null ? maily.transform.position - transform.position : Vector3.one * 999; toM.y = 0;
            float dM = toM.magnitude;
            // street dogs leave Maily alone while she plays a mini-game
            bool playing = GameManager.I != null && GameManager.I.State == GameState.Playing
                           && !(MiniGameManager.I != null && MiniGameManager.I.Busy);

            float speed = 0f; Vector3 dest = transform.position;
            if (!Defeated && playing && _cooldown <= 0f && _mode != Mode.Charge && dM < NoticeDist)
            {
                _mode = Mode.Charge; Growl(1f);
            }

            switch (_mode)
            {
                case Mode.Charge:
                    if (!playing || dM > GiveUpDist) { _mode = Mode.Return; _target = Home; break; }
                    if (dM < 2.0f * Mathf.Max(1f, Size * 0.8f) && FightManager.I != null && FightManager.I.Begin(this)) return;
                    dest = maily.transform.position; speed = ChargeSpeed * SpeedMul;
                    _growlT -= dt; if (_growlT <= 0f) { _growlT = 1.6f; Growl(0.7f); }
                    break;
                case Mode.Return:
                    dest = _target; speed = 2.4f;
                    if ((transform.position - _target).sqrMagnitude < 1f) { _mode = Mode.Patrol; _wait = 2f; }
                    break;
                default:
                    if (_wait > 0f) _wait -= dt;
                    else
                    {
                        var d = _target - transform.position; d.y = 0;
                        if (d.magnitude < 0.6f) { _wait = Random.Range(1.5f, 4.5f); var r = Random.insideUnitCircle * PatrolRadius; _target = Home + new Vector3(r.x, 0, r.y); }
                        else { dest = _target; speed = Defeated ? 1.2f : 1.7f; }
                    }
                    // grumble when Maily is around but not close enough to charge
                    if (!Defeated && dM < NoticeDist * 1.8f) { _growlT -= dt; if (_growlT <= 0f) { _growlT = Random.Range(3f, 6f); Growl(0.45f); } Face(toM, dt * 2f); }
                    break;
            }

            if (speed > 0f)
            {
                var dir = dest - transform.position; dir.y = 0;
                if (dir.sqrMagnitude > 0.01f)
                {
                    dir.Normalize();
                    if (Physics.Raycast(transform.position + Vector3.up * 0.5f * Size, dir, 1.1f * Size, ~0, QueryTriggerInteraction.Ignore)
                        && _mode != Mode.Charge)
                    { var r = Random.insideUnitCircle * PatrolRadius; _target = Home + new Vector3(r.x, 0, r.y); speed = 0f; }
                    else
                    {
                        // charging slides along obstacles instead of stopping dead
                        if (_mode == Mode.Charge && Physics.Raycast(transform.position + Vector3.up * 0.5f * Size, dir, 1.1f * Size, ~0, QueryTriggerInteraction.Ignore))
                            dir = Quaternion.Euler(0, 70, 0) * dir;
                        Face(dir, dt * 8f);
                        transform.position += dir * speed * dt;
                    }
                }
            }
            var pos = transform.position; pos.y = 0.05f; transform.position = pos;
            Animate(dt, speed, false);
        }

        public void Growl(float vol)
        {
            if (Sfx.I == null) return;
            Sfx.I.Play3D(Sfx.I.Growl, transform.position, vol, Mathf.Lerp(1.15f, 0.8f, (Size - 1f) / 0.5f));
        }

        public void Bark() { if (Sfx.I != null) Sfx.I.Play3D(Sfx.I.Bark, transform.position, 1f, Mathf.Lerp(0.9f, 0.65f, (Size - 1f) / 0.5f)); }

        void Face(Vector3 dir, float k)
        {
            dir.y = 0; if (dir.sqrMagnitude < 0.001f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Mathf.Clamp01(k));
        }

        void Animate(float dt, float speed, bool fighting)
        {
            float speedN = Mathf.Clamp01(Mathf.Abs(speed) / 6f);
            _gait = Mathf.MoveTowards(_gait, speedN, dt / 0.25f);
            _phase += dt * Mathf.Lerp(5f, 15f, speedN) / Mathf.Sqrt(Size) * Mathf.Sign(speed == 0 ? 1 : speed);
            float crouch = FightCrouch, lunge = FightLunge, hurt = FightHurt;
            for (int i = 0; i < Legs.Length; i++)
            {
                float ph = _phase + (i == 0 || i == 3 ? 0 : Mathf.PI);
                bool front = i < 2;
                float swing = Mathf.Sin(ph) * 34f * _gait;
                if (front) swing -= lunge * 45f; else swing += lunge * 30f; // pounce: reach forward, push back
                Legs[i].localRotation = Quaternion.Euler(swing, 0, 0);
            }
            bool angry = !Defeated && (fighting || _mode == Mode.Charge);
            float breathe = Mathf.Sin(Time.time * (angry ? 2.2f : 1f) * Mathf.PI) * 0.015f * Size;
            float bob = Mathf.Abs(Mathf.Sin(_phase)) * 0.06f * Size * _gait;
            Body.localPosition = _bodyBase + new Vector3(0, bob + breathe - crouch * 0.14f * Size, lunge * 0.25f * Size);
            Body.localRotation = Quaternion.Euler(Mathf.Sin(_phase * 2f) * 3f * _gait + crouch * 6f + lunge * 10f - hurt * 18f, 0, Mathf.Sin(_phase) * 2f * _gait);
            // head: low and forward when aggressive, snaps forward on a lunge
            float headPitch = (angry ? 14f : -6f) + lunge * 20f - hurt * 25f + crouch * 8f;
            Head.localRotation = Quaternion.Slerp(Head.localRotation, Quaternion.Euler(headPitch, 0, 0), 1f - Mathf.Exp(-12f * dt));
            // tail: stiff and high when hostile, wagging once beaten
            float tailX = angry ? -25f : -40f;
            float tailY = Defeated ? Mathf.Sin(Time.time * 14f) * 30f : Mathf.Sin(Time.time * 2f) * 4f;
            Tail.localRotation = Quaternion.Euler(tailX, tailY, 0);
        }
    }
}

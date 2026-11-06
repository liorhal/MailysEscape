using System;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Maily
{
    /// <summary>One side of a fight. Positions are 1-D along the arena axis (+X = screen right).</summary>
    public class Fighter
    {
        public enum Act { Idle, Walk, Jump, Bite, Pounce, Bark, Block, Hurt, KO }

        public string Name;
        public bool IsMaily;
        public Transform Root;
        public float GroundY;
        public float X, Y, Vy, KnockV, MoveInput;
        public float Hp, MaxHp = 100f, DamageMul = 1f, SpeedMul = 1f, Radius = 0.45f;
        public float Facing = 1f;            // +1 faces +X
        public Act State;
        public float T, Stun, BlockTimer, BarkCd;
        public bool HasHit, AirAttack;
        public float Lunge, Crouch, Hurt, Ko; // smoothed pose outputs
        public float VelX;                    // last frame's motion along X (for animation)

        public bool Airborne => Y > 0.001f || Vy > 0f;
        public bool CanAct => State == Act.Idle || State == Act.Walk || (State == Act.Jump);
        public bool Attacking => State == Act.Bite || State == Act.Pounce || State == Act.Bark;
    }

    /// <summary>
    /// Street-dog fights: a side-view, best-of-three fighting mode (health bars, round clock,
    /// "round 1 / fight!" calls, knockouts). Original design for this game; cartoon hits, no gore.
    /// </summary>
    public class FightManager : MonoBehaviour
    {
        public static FightManager I { get; private set; }

        public CinemachineCamera FightCam;
        public float RoundTime = 45f;
        public int RoundsToWin = 2;

        public enum Phase { None, Intro, RoundIntro, Fight, RoundEnd, MatchEnd }
        public Phase Current { get; private set; } = Phase.None;
        public Fighter Maily { get; private set; }
        public Fighter Enemy { get; private set; }
        public HostileDog Opponent { get; private set; }
        public int Round { get; private set; }
        public int MailyRounds { get; private set; }
        public int EnemyRounds { get; private set; }
        public float Clock { get; private set; }
        public bool Active => Current != Phase.None;

        /// <summary>Big centre-screen call: text, seconds to show, emphasis (1 = normal, 2 = huge).</summary>
        public event Action<string, float, float> Announce;
        public event Action FightStarted, FightEnded;

        // move table: startup, active, recovery, damage, reach (beyond touching), knockback, stun
        struct Move { public float Startup, Active, Recovery, Damage, Reach, Knock, Stun; }
        static readonly Move BiteMove = new Move { Startup = 0.10f, Active = 0.12f, Recovery = 0.22f, Damage = 7f, Reach = 0.38f, Knock = 1.4f, Stun = 0.32f };
        static readonly Move AirBiteMove = new Move { Startup = 0.08f, Active = 0.16f, Recovery = 0.18f, Damage = 9f, Reach = 0.42f, Knock = 1.8f, Stun = 0.38f };
        static readonly Move PounceMove = new Move { Startup = 0.28f, Active = 0.26f, Recovery = 0.42f, Damage = 14f, Reach = 0.45f, Knock = 3.2f, Stun = 0.6f };
        static readonly Move BarkMove = new Move { Startup = 0.14f, Active = 0.10f, Recovery = 0.32f, Damage = 4f, Reach = 1.7f, Knock = 2.2f, Stun = 0.9f };
        const float BarkCooldown = 5f, Gravity = 22f, JumpV = 6.2f, WalkSpeed = 2.7f;

        Vector3 _center, _dir;
        float _bound = 4.2f, _phaseT, _shake, _aiT, _announceStep;
        Vector3 _home; float _homeYaw;
        MailyController _maily; MailyGait _gait; CharacterController _cc;
        bool _mailyWonRound;
        float _roundStartHp;

        void Awake() => I = this;

        void Start()
        {
            _maily = MailyController.I;
            if (_maily != null) { _home = _maily.transform.position; _homeYaw = _maily.transform.eulerAngles.y; _gait = _maily.GetComponentInChildren<MailyGait>(); _cc = _maily.GetComponent<CharacterController>(); }
            if (FightCam != null) FightCam.Priority = 0;
        }

        // ------------------------------------------------------------------ start / finish
        public bool Begin(HostileDog dog)
        {
            if (Active || dog == null || dog.Defeated || _maily == null) return false;
            var gm = GameManager.I; if (gm == null || gm.State != GameState.Playing) return false;

            Opponent = dog;
            // balance: Maily is the hero — a bit more health; street dogs hit 25% softer than their raw stat
            Maily = new Fighter { Name = "מיילי", IsMaily = true, Root = _maily.transform, GroundY = 0.06f, MaxHp = 120f, Radius = 0.45f };
            Enemy = new Fighter { Name = dog.DogName, Root = dog.transform, GroundY = 0.05f, MaxHp = dog.MaxHp, DamageMul = dog.DamageMul * 0.75f, SpeedMul = dog.SpeedMul, Radius = 0.39f * dog.Size };
            Clock = RoundTime;
            ChooseArena(_maily.transform.position, dog.transform.position);
            // start positions: Maily on the right of the screen (+X)
            Maily.X = ProjectX(_maily.transform.position); Enemy.X = ProjectX(dog.transform.position);
            Maily.Facing = -1f; Enemy.Facing = 1f; Maily.Hp = Maily.MaxHp; Enemy.Hp = Enemy.MaxHp;
            MailyRounds = EnemyRounds = 0; Round = 0;

            gm.EnterFight();
            dog.EnterFight();
            _maily.FightLock = true; if (_cc != null) _cc.enabled = false;
            if (FightCam != null) FightCam.Priority = 40;
            Current = Phase.Intro; _phaseT = 0f; _announceStep = 0;
            dog.Growl(1f);
            Sfx.I?.Play2D(Sfx.I.Gong, 0.8f, 0.9f);
            Announce?.Invoke($"מיילי נגד {dog.DogName}", 2.2f, 1.5f);
            FightStarted?.Invoke();
            Debug.Log($"[Maily] Fight started vs {dog.DogName}");
            return true;
        }

        void Finish()
        {
            bool won = MailyRounds > EnemyRounds;
            var dog = Opponent;
            Current = Phase.None;
            if (FightCam != null) FightCam.Priority = 0;
            if (_gait != null) _gait.FightLunge = _gait.FightCrouch = _gait.FightHurt = 0f;
            // stand Maily back up, level
            var p = Maily.Root.position; p.y = Maily.GroundY;
            Maily.Root.SetPositionAndRotation(p, Quaternion.Euler(0, Maily.Root.eulerAngles.y, 0));
            if (_cc != null) _cc.enabled = true;
            _maily.FightLock = false;
            if (!won) _maily.Place(_home, _homeYaw);
            dog.ExitFight(won);
            GameManager.I.ExitFight();
            if (won) GameManager.I.BeatBully(dog.DogName); else GameManager.I.LostFight(dog.DogName);
            FightEnded?.Invoke();
            Debug.Log($"[Maily] Fight over vs {dog.DogName}: {(won ? "won" : "lost")} {MailyRounds}-{EnemyRounds}");
        }

        void ChooseArena(Vector3 a, Vector3 b)
        {
            var mid = (a + b) * 0.5f; mid.y = 0f;
            var baseDir = b - a; baseDir.y = 0; if (baseDir.sqrMagnitude < 0.01f) baseDir = _maily.transform.forward;
            baseDir.Normalize();
            float[] angles = { 0, 25, -25, 50, -50, 75, -75, 90, 115, -115, 140, -140 };
            float bestScore = -1f; Vector3 bestDir = baseDir; Vector3 bestCenter = mid;
            foreach (var ang in angles)
            {
                var d = Quaternion.Euler(0, ang, 0) * baseDir;
                float left = Clear(mid, -d, 5.2f), right = Clear(mid, d, 5.2f);
                var camFwd = Vector3.Cross(d, Vector3.up);
                float cam = Clear(mid + Vector3.up * 0.7f, -camFwd, 6.5f);
                float score = Mathf.Min(left, right) + Mathf.Min(cam, 5f) * 0.6f;
                if (score > bestScore) { bestScore = score; bestDir = d; bestCenter = mid; }
                if (left >= 5.2f && right >= 5.2f && cam >= 6.5f) break;
            }
            // Maily must start on +X (screen right): flip the axis if she is on the other side
            _dir = bestDir; _center = bestCenter;
            if (Vector3.Dot(a - _center, _dir) < 0f) _dir = -_dir;
            _bound = Mathf.Clamp(Mathf.Min(Clear(_center, _dir, 5.2f), Clear(_center, -_dir, 5.2f)) - 0.7f, 2.2f, 4.5f);
        }

        float Clear(Vector3 from, Vector3 dir, float max)
        {
            float best = max;
            foreach (var h in Physics.RaycastAll(from + Vector3.up * 0.5f, dir, max, ~0, QueryTriggerInteraction.Ignore))
            {
                if (_maily != null && h.collider.transform.IsChildOf(_maily.transform)) continue;
                if (h.collider is BoxCollider bc && bc.size.x > 1000f) continue; // the ground slab
                best = Mathf.Min(best, h.distance);
            }
            return best;
        }

        float ProjectX(Vector3 p) => Mathf.Clamp(Vector3.Dot(p - _center, _dir), -_bound, _bound);

        // ------------------------------------------------------------------ loop
        void Update()
        {
            if (!Active) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return; // paused
            _phaseT += dt;

            switch (Current)
            {
                case Phase.Intro:
                    // walk both into their marks while the camera swings round
                    Maily.X = Mathf.MoveTowards(Maily.X, 2.0f, dt * 3f); Enemy.X = Mathf.MoveTowards(Enemy.X, -2.0f, dt * 3f);
                    Maily.VelX = Enemy.VelX = 0f;
                    if (_phaseT > 2.3f) StartRound(1);
                    break;
                case Phase.RoundIntro:
                    if (_announceStep == 0 && _phaseT > 1.2f) { _announceStep = 1; Announce?.Invoke("קרב!", 0.9f, 2f); Sfx.I?.Play2D(Sfx.I.Gong, 1f, 1.2f); Opponent.Bark(); }
                    if (_phaseT > 1.7f) { Current = Phase.Fight; _phaseT = 0f; }
                    break;
                case Phase.Fight:
                    Clock -= dt;
                    ReadMailyInput();
                    RunAi(dt);
                    Step(Maily, Enemy, dt); Step(Enemy, Maily, dt);
                    Separate();
                    if (Maily.State == Fighter.Act.KO || Enemy.State == Fighter.Act.KO) EndRound(Enemy.State == Fighter.Act.KO, true);
                    else if (Clock <= 0f) { Clock = 0f; EndRound(Maily.Hp / Maily.MaxHp >= Enemy.Hp / Enemy.MaxHp, false); }
                    break;
                case Phase.RoundEnd:
                    Step(Maily, Enemy, dt); Step(Enemy, Maily, dt);
                    if (_announceStep == 0 && _phaseT > 1.5f)
                    {
                        _announceStep = 1;
                        Announce?.Invoke(_mailyWonRound ? "מיילי לוקחת את הסיבוב" : $"{Enemy.Name} לוקח את הסיבוב", 1.4f, 1f);
                    }
                    if (_phaseT > 3.2f)
                    {
                        if (MailyRounds >= RoundsToWin || EnemyRounds >= RoundsToWin) EndMatch();
                        else StartRound(Round + 1);
                    }
                    break;
                case Phase.MatchEnd:
                    Step(Maily, Enemy, dt); Step(Enemy, Maily, dt);
                    if (_phaseT > 3.6f) { Finish(); return; }
                    break;
            }
            ApplyPoses(Maily, Enemy, dt); ApplyPoses(Enemy, Maily, dt);
        }

        void StartRound(int n)
        {
            Round = n; Clock = RoundTime;
            foreach (var f in new[] { Maily, Enemy })
            {
                f.Hp = f.MaxHp; f.Y = f.Vy = f.KnockV = 0f; f.State = Fighter.Act.Idle; f.T = 0f; f.Stun = 0f; f.BarkCd = 0f; f.MoveInput = 0f; f.Ko = 0f;
            }
            Maily.X = 2.0f; Enemy.X = -2.0f; Maily.Facing = -1f; Enemy.Facing = 1f;
            _roundStartHp = Maily.Hp;
            Current = Phase.RoundIntro; _phaseT = 0f; _announceStep = 0;
            bool last = MailyRounds == RoundsToWin - 1 && EnemyRounds == RoundsToWin - 1;
            Announce?.Invoke(last ? "סיבוב אחרון" : $"סיבוב {n}", 1.1f, 1.3f);
            Debug.Log($"[Maily] Round {n}");
        }

        void EndRound(bool mailyWins, bool ko)
        {
            _mailyWonRound = mailyWins;
            if (mailyWins) MailyRounds++; else EnemyRounds++;
            Current = Phase.RoundEnd; _phaseT = 0f; _announceStep = 0;
            Maily.MoveInput = Enemy.MoveInput = 0f;
            Announce?.Invoke(ko ? "נוקאאוט!" : "נגמר הזמן!", 1.4f, 2f);
            Sfx.I?.Play2D(Sfx.I.Gong, 1f, 0.8f);
            _shake = Mathf.Max(_shake, ko ? 1.2f : 0.4f);
            Debug.Log($"[Maily] Round {Round} -> {(mailyWins ? "Maily" : Enemy.Name)} ({(ko ? "KO" : "time")}), score {MailyRounds}-{EnemyRounds}");
        }

        void EndMatch()
        {
            Current = Phase.MatchEnd; _phaseT = 0f;
            bool won = MailyRounds > EnemyRounds;
            bool flawless = won && Maily.Hp >= Maily.MaxHp - 0.01f;
            Announce?.Invoke(won ? (flawless ? "ניצחון מושלם!" : "מיילי ניצחה!") : $"{Enemy.Name} ניצח!", 3f, 2f);
            if (won) { Sfx.I?.Play2D(Sfx.I.Sting, 1f); _maily.RaiseBarked(); Sfx.I?.Play3D(Sfx.I.Bark, Maily.Root.position, 1f, 1.1f); }
            else Opponent.Growl(1f);
        }

        // ------------------------------------------------------------------ input & AI
        void ReadMailyInput()
        {
            var f = Maily;
            float move = 0f; bool jump = false, bite = false, pounce = false, block = false, bark = false;
            var k = Keyboard.current;
            if (k != null)
            {
                if (k.dKey.isPressed || k.rightArrowKey.isPressed) move += 1f;
                if (k.aKey.isPressed || k.leftArrowKey.isPressed) move -= 1f;
                jump |= k.wKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame;
                bite |= k.jKey.wasPressedThisFrame;
                pounce |= k.kKey.wasPressedThisFrame;
                block |= k.sKey.isPressed || k.downArrowKey.isPressed || k.lKey.isPressed;
                bark |= k.eKey.wasPressedThisFrame;
            }
            var m = Mouse.current;
            if (m != null) { bite |= m.leftButton.wasPressedThisFrame; pounce |= m.rightButton.wasPressedThisFrame; }
            var g = Gamepad.current;
            if (g != null)
            {
                float sx = g.leftStick.ReadValue().x; if (Mathf.Abs(sx) > Mathf.Abs(move)) move = sx;
                jump |= g.buttonSouth.wasPressedThisFrame;
                bite |= g.buttonWest.wasPressedThisFrame;
                pounce |= g.buttonNorth.wasPressedThisFrame;
                block |= g.buttonEast.isPressed || g.leftStick.ReadValue().y < -0.6f;
                bark |= g.rightShoulder.wasPressedThisFrame;
            }
            Command(f, move, jump, bite, pounce, block, bark);
        }

        void RunAi(float dt)
        {
            var e = Enemy; var d = Opponent; var m = Maily;
            float toward = Mathf.Sign(m.X - e.X);
            float gap = Mathf.Abs(m.X - e.X) - (m.Radius + e.Radius);
            // reflex: block an incoming attack (checked every frame, gated by the dog's reactions)
            if (e.CanAct && !e.Airborne && m.Attacking && m.T < 0.12f && gap < 1.4f && UnityEngine.Random.value < d.BlockChance * dt * 12f)
            { Command(e, 0, false, false, false, true, false); e.BlockTimer = 0.45f; return; }
            if (e.State == Fighter.Act.Block)
            {
                e.BlockTimer -= dt;
                Command(e, 0, false, false, false, e.BlockTimer > 0f, false);
                return;
            }
            _aiT -= dt;
            if (_aiT > 0f) { Command(e, e.MoveInput, false, false, false, false, false); return; }
            _aiT = d.ReactTime * UnityEngine.Random.Range(0.7f, 1.3f);
            float r = UnityEngine.Random.value;
            if (gap > 0.55f)
            {
                if (gap < 1.7f && r < d.Aggression * 0.35f) Command(e, 0, false, false, true, false, false);
                else if (gap > 2.5f && r < 0.08f) Command(e, toward, true, false, false, false, false); // hop in
                else Command(e, toward, false, false, false, false, false);
            }
            else
            {
                // after committing to an attack the dog catches its breath — Maily's opening
                if (r < d.Aggression) { Command(e, 0, false, true, false, false, false); _aiT += d.ReactTime * UnityEngine.Random.Range(0.4f, 1.0f); }
                else if (r < d.Aggression + 0.15f) { Command(e, 0, false, false, true, false, false); _aiT += d.ReactTime * 1.2f; }
                else if (r < d.Aggression + 0.3f) { Command(e, -toward, false, false, false, false, false); _aiT = 0.35f; }
                else { Command(e, 0, false, false, false, true, false); e.BlockTimer = 0.35f; }
            }
        }

        void Command(Fighter f, float move, bool jump, bool bite, bool pounce, bool block, bool bark)
        {
            f.MoveInput = 0f;
            if (f.State == Fighter.Act.Block && !block) { f.State = Fighter.Act.Idle; f.T = 0f; }
            if (!f.CanAct && f.State != Fighter.Act.Block) return;
            if (bark && f.IsMaily && f.BarkCd <= 0f && !f.Airborne) { StartAttack(f, Fighter.Act.Bark); f.BarkCd = BarkCooldown; _maily.RaiseBarked(); Sfx.I?.Play3D(Sfx.I.Bark, f.Root.position, 1f, 0.95f); return; }
            if (bite) { StartAttack(f, Fighter.Act.Bite); return; }
            if (pounce && !f.Airborne) { StartAttack(f, Fighter.Act.Pounce); return; }
            if (block && !f.Airborne) { f.State = Fighter.Act.Block; f.T = 0f; return; }
            if (f.State == Fighter.Act.Block) return;
            if (jump && !f.Airborne) { f.Vy = JumpV; f.State = Fighter.Act.Jump; f.T = 0f; if (f.IsMaily) _maily.RaiseJumped(); Sfx.I?.Play3D(Sfx.I.Hop, f.Root.position, 0.6f); }
            f.MoveInput = Mathf.Clamp(move, -1f, 1f);
            if (!f.Airborne) f.State = Mathf.Abs(f.MoveInput) > 0.1f ? Fighter.Act.Walk : Fighter.Act.Idle;
        }

        void StartAttack(Fighter f, Fighter.Act act)
        {
            f.AirAttack = f.Airborne && act == Fighter.Act.Bite;
            f.State = act; f.T = 0f; f.HasHit = false; f.MoveInput = 0f;
            Sfx.I?.Play3D(Sfx.I.Whoosh, f.Root.position, 0.5f, act == Fighter.Act.Pounce ? 0.8f : 1.2f);
            if (!f.IsMaily && act == Fighter.Act.Pounce) Opponent.Growl(0.6f);
        }

        Move MoveOf(Fighter f) => f.State == Fighter.Act.Pounce ? PounceMove : f.State == Fighter.Act.Bark ? BarkMove : f.AirAttack ? AirBiteMove : BiteMove;

        // ------------------------------------------------------------------ simulation
        void Step(Fighter f, Fighter other, float dt)
        {
            f.T += dt;
            f.BarkCd = Mathf.Max(0f, f.BarkCd - dt);
            float x0 = f.X;

            if (f.CanAct || f.State == Fighter.Act.Block) f.Facing = Mathf.Sign(other.X - f.X) == 0 ? f.Facing : Mathf.Sign(other.X - f.X);

            switch (f.State)
            {
                case Fighter.Act.Walk:
                    float spd = WalkSpeed * f.SpeedMul * (Mathf.Sign(f.MoveInput) == f.Facing ? 1f : 0.8f);
                    f.X += f.MoveInput * spd * dt;
                    break;
                case Fighter.Act.Jump:
                    f.X += f.MoveInput * WalkSpeed * 1.1f * f.SpeedMul * dt;
                    break;
                case Fighter.Act.Hurt:
                    if (f.T > f.Stun) { f.State = Fighter.Act.Idle; f.T = 0f; }
                    break;
                case Fighter.Act.Bite:
                case Fighter.Act.Pounce:
                case Fighter.Act.Bark:
                    var mv = MoveOf(f);
                    if (f.State == Fighter.Act.Pounce && f.T > mv.Startup && f.T < mv.Startup + mv.Active) f.X += f.Facing * 4.6f * f.SpeedMul * dt;
                    if (f.AirAttack) f.X += f.MoveInput * WalkSpeed * dt;
                    if (!f.HasHit && f.T >= mv.Startup && f.T <= mv.Startup + mv.Active) TryHit(f, other, mv);
                    if (f.T > mv.Startup + mv.Active + mv.Recovery) { f.State = f.Airborne ? Fighter.Act.Jump : Fighter.Act.Idle; f.T = 0f; }
                    break;
            }

            // knockback & gravity
            f.X += f.KnockV * dt; f.KnockV = Mathf.MoveTowards(f.KnockV, 0f, 9f * dt);
            if (f.Airborne || f.Vy != 0f)
            {
                f.Vy -= Gravity * dt; f.Y += f.Vy * dt;
                if (f.Y <= 0f)
                {
                    f.Y = 0f; f.Vy = 0f;
                    if (f.State == Fighter.Act.Jump) { f.State = Fighter.Act.Idle; f.T = 0f; }
                    if (f.IsMaily) _maily.RaiseLanded();
                    Fx.Dust(Pos(f), 0.6f);
                }
            }
            f.X = Mathf.Clamp(f.X, -_bound, _bound);
            f.VelX = (f.X - x0) / dt;
        }

        void TryHit(Fighter a, Fighter d, Move mv)
        {
            float dist = Mathf.Abs(a.X - d.X);
            if (dist > a.Radius + d.Radius + mv.Reach) return;
            if (Mathf.Sign(d.X - a.X) != a.Facing && dist > 0.2f) return;           // must face the target
            if (Mathf.Abs(a.Y - d.Y) > 0.9f) return;                                // jumped over it
            if (d.State == Fighter.Act.KO) return;
            a.HasHit = true;
            var contact = Pos(a) + Vector3.up * 0.45f + _dir * a.Facing * (a.Radius + 0.15f);
            bool blocked = d.State == Fighter.Act.Block && Mathf.Sign(a.X - d.X) == d.Facing;
            float dmg = mv.Damage * a.DamageMul;
            if (blocked)
            {
                d.Hp -= dmg * 0.15f; d.KnockV = a.Facing * mv.Knock * 0.4f;
                if (a.State == Fighter.Act.Bark) { d.State = Fighter.Act.Hurt; d.T = 0f; d.Stun = 0.25f; }
                Sfx.I?.Play3D(Sfx.I.Block, contact, 0.9f, UnityEngine.Random.Range(0.9f, 1.1f));
                Fx.Sparkle(contact, Palette.Spike);
                _shake = Mathf.Max(_shake, 0.15f);
            }
            else
            {
                d.Hp -= dmg; d.KnockV = a.Facing * mv.Knock;
                d.State = Fighter.Act.Hurt; d.T = 0f; d.Stun = mv.Stun; d.Hurt = 1f;
                if (d.Airborne) d.Vy = Mathf.Max(d.Vy, 2.5f);
                bool heavy = a.State == Fighter.Act.Pounce;
                Sfx.I?.Play3D(heavy ? Sfx.I.HitHeavy : Sfx.I.Hit, contact, 1f, UnityEngine.Random.Range(0.9f, 1.12f));
                if (d.IsMaily) Sfx.I?.Play3D(Sfx.I.Yip, contact, 0.8f, 1.3f); else if (UnityEngine.Random.value < 0.4f) Sfx.I?.Play3D(Sfx.I.Yip, contact, 0.6f, 0.75f);
                Fx.Sparkle(contact, heavy ? Palette.CoinGold : Palette.Bone);
                _shake = Mathf.Max(_shake, heavy ? 0.7f : 0.35f);
            }
            if (d.Hp <= 0f)
            {
                d.Hp = 0f; d.State = Fighter.Act.KO; d.T = 0f; d.KnockV = a.Facing * 4f; d.Vy = 3f;
                Fx.Hearts(contact); // cartoon "seeing stars"
            }
        }

        void Separate()
        {
            float min = Maily.Radius + Enemy.Radius - 0.1f;
            float gap = Maily.X - Enemy.X;
            if (Mathf.Abs(gap) < min && Mathf.Abs(Maily.Y - Enemy.Y) < 0.6f)
            {
                float push = (min - Mathf.Abs(gap)) * 0.5f * (gap >= 0 ? 1f : -1f);
                Maily.X = Mathf.Clamp(Maily.X + push, -_bound, _bound);
                Enemy.X = Mathf.Clamp(Enemy.X - push, -_bound, _bound);
            }
        }

        Vector3 Pos(Fighter f) => _center + _dir * f.X + Vector3.up * (f.GroundY + f.Y);

        // ------------------------------------------------------------------ presentation
        void ApplyPoses(Fighter f, Fighter other, float dt)
        {
            float lungeT = 0f, crouchT = 0f;
            if (f.Attacking)
            {
                var mv = MoveOf(f);
                float a = Mathf.Clamp01(f.T / mv.Startup), r = Mathf.Clamp01((f.T - mv.Startup - mv.Active) / mv.Recovery);
                float strength = f.State == Fighter.Act.Pounce ? 1f : f.State == Fighter.Act.Bark ? 0.45f : 0.75f;
                lungeT = (f.T < mv.Startup ? a * 0.4f : 1f - r) * strength;
                if (f.State == Fighter.Act.Pounce && f.T < mv.Startup) { crouchT = a; lungeT = 0f; }
            }
            if (f.State == Fighter.Act.Block) crouchT = 1f;
            f.Lunge = Mathf.MoveTowards(f.Lunge, lungeT, dt * 12f);
            f.Crouch = Mathf.MoveTowards(f.Crouch, crouchT, dt * 9f);
            f.Hurt = Mathf.MoveTowards(f.Hurt, f.State == Fighter.Act.Hurt ? 1f : 0f, dt * 5f);
            f.Ko = Mathf.MoveTowards(f.Ko, f.State == Fighter.Act.KO ? 1f : 0f, dt * 3f);

            var face = Quaternion.LookRotation(_dir * f.Facing, Vector3.up);
            var rot = face * Quaternion.Euler(0f, 0f, 80f * f.Ko * (f.IsMaily ? 1f : -1f));
            var pos = Pos(f) + Vector3.up * (0.12f * f.Ko);
            f.Root.SetPositionAndRotation(pos, rot);

            float speed = f.VelX * f.Facing; // + = moving toward where it faces
            if (f.IsMaily)
            {
                _maily.SetFightMotion(_dir * f.VelX + Vector3.up * f.Vy, !f.Airborne);
                if (_gait != null) { _gait.FightLunge = f.Lunge; _gait.FightCrouch = f.Crouch; _gait.FightHurt = Mathf.Max(f.Hurt, f.Ko); }
            }
            else
            {
                Opponent.FightSpeed = speed; Opponent.FightLunge = f.Lunge; Opponent.FightCrouch = f.Crouch; Opponent.FightHurt = Mathf.Max(f.Hurt, f.Ko);
            }
        }

        void LateUpdate()
        {
            if (!Active || FightCam == null) return;
            var mid = _center + _dir * ((Maily.X + Enemy.X) * 0.5f);
            float sep = Mathf.Abs(Maily.X - Enemy.X);
            float dist = Mathf.Clamp(2.0f + sep * 0.75f, 3.0f, 6.2f);
            if (Current == Phase.Intro) dist += 1.2f * (1f - Mathf.Clamp01(_phaseT / 2f));
            var fwd = Vector3.Cross(_dir, Vector3.up); // camera right == +X, so Maily (+X) is on screen right
            // low, nearly level camera: fighters fill the lower half, the kibbutz is the backdrop
            var camPos = mid - fwd * dist + Vector3.up * 0.8f;
            var look = mid + Vector3.up * 0.72f;
            _shake = Mathf.MoveTowards(_shake, 0f, Time.deltaTime * 2.5f);
            var jitter = Time.deltaTime > 0f ? UnityEngine.Random.insideUnitSphere * _shake * 0.07f : Vector3.zero;
            FightCam.transform.SetPositionAndRotation(camPos + jitter, Quaternion.LookRotation(look - camPos, Vector3.up));
        }
    }
}

using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

namespace Maily
{
    /// <summary>
    /// Maily's family. The game opens on a short scene at the garden gate: the family steps out, sees Maily by
    /// the open gate, calls her, and she bolts, with the family running after her. They keep chasing until she
    /// outruns them (Shift to sprint). If they catch her, they carry her home and she starts again from the
    /// gate. Back home they stand by the door, and if Maily comes close again the chase is back on.
    /// </summary>
    public class Family : MonoBehaviour
    {
        public static Family I { get; private set; }

        public Vector3 Door, Gate;
        public float GateYaw;
        public List<FamilyMember> Members = new List<FamilyMember>();

        public bool IntroActive { get; private set; }
        public bool Chasing { get; private set; }
        public int Catches { get; private set; }

        enum Phase { Waiting, Intro, Chase, Home }
        Phase _phase = Phase.Waiting;
        float _t, _cooldown, _chaseTime;
        CinemachineCamera _cam;
        MailyController _m; CharacterController _cc;
        Vector3 _runDir;

        const float CatchDist = 1.05f, GiveUpDist = 30f, GiveUpTime = 30f, NoticeDist = 10f;

        void Awake() => I = this;

        void Update()
        {
            var gm = GameManager.I; _m = MailyController.I;
            if (gm == null || _m == null) return;
            if (_cc == null) _cc = _m.GetComponent<CharacterController>();
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            switch (_phase)
            {
                case Phase.Waiting:
                    if (gm.State == GameState.Playing) BeginIntro();
                    foreach (var f in Members) f.Idle(dt, Door);
                    break;
                case Phase.Intro: TickIntro(dt); break;
                case Phase.Chase: TickChase(dt, gm); break;
                case Phase.Home: TickHome(dt, gm); break;
            }
        }

        // ------------------------------------------------------------------ the opening scene
        void BeginIntro()
        {
            _phase = Phase.Intro; IntroActive = true; _t = 0f;
            _m.Place(Gate, 0f);                                   // Place re-enables the controller…
            _m.FightLock = true; if (_cc != null) _cc.enabled = false; // …so lock her after it
            var toHouse = Door - Gate; toHouse.y = 0;
            _m.transform.rotation = Quaternion.LookRotation(toHouse.sqrMagnitude > 0.01f ? toHouse : Vector3.forward); // she peeks back at the house
            _runDir = -toHouse.normalized;
            if (Physics.Raycast(Gate + Vector3.up * 0.4f, _runDir, 6f, ~0, QueryTriggerInteraction.Ignore))
                _runDir = Quaternion.Euler(0, 70f, 0) * _runDir; // don't sprint into a hedge
            for (int i = 0; i < Members.Count; i++) Members[i].Teleport(Door + Side(toHouse) * (i - 0.5f) * 1.1f, Gate);

            // a side-on shot that frames the door, the family and Maily at the gate
            var go = new GameObject("Intro Camera");
            _cam = go.AddComponent<CinemachineCamera>();
            var lens = LensSettings.Default; lens.FieldOfView = 44f; lens.NearClipPlane = 0.1f; lens.FarClipPlane = 1500f; _cam.Lens = lens;
            var mid = (Door + Gate) * 0.5f;
            var side = Side(toHouse);
            var pos = mid + side * (toHouse.magnitude * 0.7f + 6f) + Vector3.up * 2.6f - toHouse.normalized * 2f;
            if (Physics.Linecast(mid + Vector3.up * 1.5f, pos, ~0, QueryTriggerInteraction.Ignore)) pos = mid - side * (toHouse.magnitude * 0.7f + 6f) + Vector3.up * 2.6f - toHouse.normalized * 2f;
            go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(mid + Vector3.up * 0.7f - pos));
            _cam.Priority = 60;
        }

        static Vector3 Side(Vector3 fwd) { fwd.y = 0; if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward; fwd.Normalize(); return new Vector3(fwd.z, 0, -fwd.x); }

        void TickIntro(float dt)
        {
            float prev = _t; _t += dt;
            bool At(float s) => prev < s && _t >= s;
            var gm = GameManager.I;

            if (At(0.4f)) { gm.Say("\"מיילי?! מי השאיר את השער פתוח?\""); Sfx.I?.Play2D(Sfx.I.Click, 0.6f); }
            if (_t < 2.0f)
            {
                // the family steps out onto the path
                foreach (var f in Members) f.MoveToward(Gate, 1.6f, dt, 3.2f);
                _m.SetFightMotion(Vector3.zero, true);
            }
            if (At(1.6f))
            {
                _m.transform.rotation = Quaternion.LookRotation(_runDir);
                _m.RaiseBarked(); Sfx.I?.Play3D(Sfx.I.Bark, _m.transform.position, 1f, 1.15f);
            }
            if (At(2.1f)) { gm.Say("\"מיילי! חזרי הביתה!\""); }
            if (_t >= 1.9f && _t < 3.9f)
            {
                // Maily bolts down the path
                var v = _runDir * 7.5f;
                var p = _m.transform.position + v * dt;
                if (!Physics.Raycast(_m.transform.position + Vector3.up * 0.4f, _runDir, 0.8f, ~0, QueryTriggerInteraction.Ignore))
                {
                    p.y = Ground.HeightAt(p) + 0.01f; _m.transform.position = p;
                    _m.SetFightMotion(v, true);
                }
                else _m.SetFightMotion(Vector3.zero, true);
            }
            if (_t >= 2.3f) foreach (var f in Members) f.MoveToward(_m.transform.position, f.RunSpeed * 0.8f, dt);
            if (_t >= 4.1f) EndIntro();
        }

        void EndIntro()
        {
            IntroActive = false;
            if (_cam != null) Destroy(_cam.gameObject);
            if (_cc != null) _cc.enabled = true;
            _m.SetFightMotion(Vector3.zero, true);
            _m.FightLock = false;
            StartChase(first: true);
        }

        // ------------------------------------------------------------------ the chase
        void StartChase(bool first)
        {
            _phase = Phase.Chase; Chasing = true; _chaseTime = 0f;
            GameManager.I.Say(first ? "המשפחה רודפת אחרייך! לחצו Shift כדי לברוח מהר" : "\"הנה מיילי! תפסו אותה!\"");
            Sfx.I?.Play2D(Sfx.I.Whoosh, 0.7f);
        }

        bool Paused(GameManager gm) => gm.State != GameState.Playing || gm.MapOpen || (MiniGameManager.I != null && MiniGameManager.I.Busy);

        void TickChase(float dt, GameManager gm)
        {
            if (Paused(gm)) { foreach (var f in Members) f.Idle(dt, f.transform.position + f.transform.forward); return; }
            _chaseTime += dt;
            var mp = _m.transform.position;
            float nearest = float.MaxValue;
            foreach (var f in Members)
            {
                f.MoveToward(mp, f.RunSpeed, dt);
                float d = FlatDist(f.transform.position, mp);
                nearest = Mathf.Min(nearest, d);
                if (d < CatchDist) { Caught(); return; }
            }
            if (nearest > GiveUpDist || _chaseTime > GiveUpTime)
            {
                _phase = Phase.Home; Chasing = false; _cooldown = 4f;
                gm.Say("\"היא ברחה לנו... מיילי!\"  המשפחה חוזרת הביתה");
            }
        }

        void TickHome(float dt, GameManager gm)
        {
            _cooldown -= dt;
            var toHouse = Door - Gate;
            for (int i = 0; i < Members.Count; i++)
            {
                var spot = Door + Side(toHouse) * (i - 0.5f) * 1.2f + toHouse.normalized * -0.6f;
                if (FlatDist(Members[i].transform.position, spot) > 0.4f) Members[i].MoveToward(spot, 2.2f, dt);
                else Members[i].Idle(dt, Gate);
            }
            if (Paused(gm) || _cooldown > 0f) return;
            foreach (var f in Members)
                if (FlatDist(f.transform.position, _m.transform.position) < NoticeDist) { StartChase(first: false); return; }
        }

        void Caught()
        {
            Catches++;
            var gm = GameManager.I;
            gm.Say("נתפסת! המשפחה מחזירה את מיילי הביתה");
            Sfx.I?.Play2D(Sfx.I.Heart, 1f, 0.9f);
            Fx.Hearts(_m.transform.position + Vector3.up * 0.9f);
            var toHouse = Door - Gate; toHouse.y = 0;
            _m.Place(Gate, Quaternion.LookRotation(-toHouse.normalized).eulerAngles.y);
            for (int i = 0; i < Members.Count; i++) Members[i].Teleport(Door + Side(toHouse) * (i - 0.5f) * 1.2f, Gate);
            _phase = Phase.Home; Chasing = false; _cooldown = 6f; // a head start before they notice her again
        }

        static float FlatDist(Vector3 a, Vector3 b) { a.y = 0; b.y = 0; return Vector3.Distance(a, b); }
    }
}

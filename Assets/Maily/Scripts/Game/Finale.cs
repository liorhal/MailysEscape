using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Maily
{
    /// <summary>
    /// What happens when Maily finishes stage 1 without being caught.
    /// 1. The dogs' revenge: every friend she made and the street dogs gather behind her and chase the family
    ///    back into the house. A moment later the family comes out with a rifle and, one by one, the dogs are
    ///    knocked out of the story (cartoon: a spin, a puff of dust and stars, gone). Only Maily is left.
    /// 2. The great chase: a straight farm lane with fences on both sides, so no moving sideways. Maily runs on her
    ///    own; Space jumps hay bales, logs and barrels, S ducks under laundry lines and branches. Each stumble
    ///    lets the family close in. Reach the open fields = "חופשית!", get caught = "נתפסה ברגע האחרון".
    /// </summary>
    public class Finale : MonoBehaviour
    {
        public static Finale I { get; private set; }
        /// <summary>The obstacle chase is on (touch controls show jump / duck).</summary>
        public bool Running => _phase == Phase.Runner;

        enum Phase { Off, Pack, Runner }
        Phase _phase = Phase.Off;
        float _t;

        MailyController _m; CharacterController _cc; MailyGait _gait;
        CinemachineCamera _cam;
        Vector3 _door, _gate, _dir;

        class PackDog { public Transform T; public Vector3 Slot; public float Hop, Down; public bool Shot, Gone; }
        readonly List<PackDog> _pack = new List<PackDog>();
        GameObject _rifle, _flash;
        int _nextShot; float _shotT;
        bool _familyInside, _familyOut, _runnerCalled;

        void Awake() => I = this;

        // ================================================================== part 1: the dogs' revenge
        public void Begin()
        {
            if (_phase != Phase.Off) return;
            _phase = Phase.Pack; _t = 0f;
            _m = MailyController.I; _cc = _m.GetComponent<CharacterController>(); _gait = _m.GetComponentInChildren<MailyGait>();
            var fam = Family.I;
            if (fam != null) fam.Halt();
            _m.FightLock = true; if (_cc != null) _cc.enabled = false;
            if (fam != null) { fam.Halt(); _door = fam.Door; _gate = fam.Gate; }
            else { _gate = _m.transform.position; _door = _gate + Vector3.forward * 4f; }
            // the open direction away from the house with the most room
            var away = _gate - _door; away.y = 0; away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward;
            _dir = away; float best = -1f;
            foreach (var a in new[] { 0f, 30f, -30f, 60f, -60f, 90f, -90f })
            {
                var d = Quaternion.Euler(0, a, 0) * away;
                float free = Physics.Raycast(_gate + Vector3.up * 0.5f, d, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore) ? hit.distance : 20f;
                if (free > best + 2f) { best = free; _dir = d; }
            }
            float reach = Mathf.Clamp(best - 2f, 8f, 16f);

            // Maily and the pack, out on the path facing the house
            var side = new Vector3(_dir.z, 0, -_dir.x);
            var mStart = _gate + _dir * reach;
            Put(_m.transform, mStart, -_dir);
            var dogs = new List<Transform>();
            // copies: disabling a dog takes it off its own list
            foreach (var d in new List<NpcDog>(NpcDog.All)) if (d.Met) { d.enabled = false; dogs.Add(d.transform); }
            foreach (var h in new List<HostileDog>(HostileDog.All)) { h.enabled = false; dogs.Add(h.transform); }
            for (int i = 0; i < dogs.Count; i++)
            {
                int row = i / 3 + 1; float col = (i % 3 - 1) * 1.4f + (row % 2 == 0 ? 0.7f : 0f);
                var slot = side * col + _dir * (row * 1.3f);
                _pack.Add(new PackDog { T = dogs[i], Slot = slot, Hop = Random.value * 6f });
                Put(dogs[i], mStart + slot, -_dir);
            }
            if (fam != null)
                for (int i = 0; i < fam.Members.Count; i++)
                {
                    fam.Members[i].gameObject.SetActive(true);
                    fam.Members[i].Teleport(_gate + _dir * 2.5f + side * (i - 0.5f) * 1.2f, mStart);
                }

            // a side-on wide shot
            var go = new GameObject("Finale Camera"); _cam = go.AddComponent<CinemachineCamera>();
            var lens = LensSettings.Default; lens.FieldOfView = 50f; lens.NearClipPlane = 0.1f; lens.FarClipPlane = 1500f; _cam.Lens = lens;
            var mid = (_door + mStart) * 0.5f;
            var pos = mid + side * (reach * 0.45f + 4f) + Vector3.up * 2.6f;
            if (Physics.Linecast(mid + Vector3.up * 1.5f, pos, ~0, QueryTriggerInteraction.Ignore)) pos = mid - side * (reach * 0.45f + 4f) + Vector3.up * 2.6f;
            go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(mid + Vector3.up * 0.6f - pos));
            _cam.Priority = 60;
            Captions.I?.Show("כל החברים של מיילי, וגם כלבי הרחוב, באו לעזור לה!", 3f);
        }

        static void Put(Transform t, Vector3 p, Vector3 look)
        {
            p.y = Ground.HeightAt(p) + 0.01f; t.position = p;
            look.y = 0; if (look.sqrMagnitude > 0.001f) t.rotation = Quaternion.LookRotation(look);
        }

        void Update()
        {
            float dt = Time.deltaTime; if (dt <= 0f) return;
            var gm = GameManager.I; if (gm == null || gm.State == GameState.Ended) return;
            if (_phase == Phase.Pack) TickPack(dt);
            else if (_phase == Phase.Runner) TickRunner(dt);
        }

        void TickPack(float dt)
        {
            float prev = _t; _t += dt;
            bool At(float s) => prev < s && _t >= s;
            var fam = Family.I;
            var side = new Vector3(_dir.z, 0, -_dir.x);
            var target = _gate + _dir * 3.2f;   // where the pack stops, just outside the gate

            // the pack charges the family, barking
            if (_t < 3.4f)
            {
                var mp = _m.transform.position;
                var step = Vector3.MoveTowards(mp, target, 5.5f * dt);
                Put(_m.transform, step, -_dir);
                _m.SetFightMotion(-_dir * (Vector3.Distance(mp, target) > 0.1f ? 5.5f : 0f), true);
                foreach (var p in _pack) if (!p.Gone) Put(p.T, Vector3.MoveTowards(p.T.position, target + p.Slot, 5.5f * dt), -_dir);
                if (Random.value < dt * 3f && _pack.Count > 0) Sfx.I?.Play3D(Sfx.I.Bark, _pack[Random.Range(0, _pack.Count)].T.position, 0.8f, Random.Range(0.8f, 1.3f));
            }
            else _m.SetFightMotion(Vector3.zero, true);
            foreach (var p in _pack) if (!p.Shot) Bob(p, dt);

            // the family flees into the house…
            if (fam != null && !_familyInside)
            {
                if (_t < 0.8f) foreach (var f in fam.Members) f.Idle(dt, _m.transform.position);
                else
                {
                    bool all = true;
                    foreach (var f in fam.Members)
                    {
                        if (!f.gameObject.activeSelf) continue;
                        f.MoveToward(_door, 4.6f, dt);
                        if (Flat(f.transform.position, _door) < 0.6f) f.gameObject.SetActive(false); else all = false;
                    }
                    if (all) _familyInside = true;
                }
            }
            if (At(1.0f)) { Captions.I?.Show("\"הצילו!\" המשפחה ברחה הביתה וטרקה את הדלת", 2.6f); Sfx.I?.Play2D(Sfx.I.Whoosh, 0.7f, 0.8f); }
            if (At(4.0f) && _familyInside) Sfx.I?.Play2D(Sfx.I.Thud, 0.9f, 0.7f);

            // …and comes back out with a rifle
            if (_familyInside && !_familyOut && _t > 4.8f && fam != null)
            {
                _familyOut = true;
                for (int i = 0; i < fam.Members.Count; i++)
                {
                    fam.Members[i].gameObject.SetActive(true);
                    fam.Members[i].Teleport(_door + side * (i - 0.5f) * 1.0f, target);
                }
                if (fam.Members.Count > 0) GiveRifle(fam.Members[0]);
                Captions.I?.Show("אחרי רגע הדלת נפתחה... והם יצאו עם רובה!", 2.4f);
            }
            if (_familyOut && fam != null)
            {
                for (int i = 0; i < fam.Members.Count; i++)
                {
                    var f = fam.Members[i];
                    var stand = _door + _dir * 1.4f + side * (i - 0.5f) * 1.2f;
                    if (Flat(f.transform.position, stand) > 0.2f) f.MoveToward(stand, 2.2f, dt);
                    else f.Idle(dt, AimTarget());
                    if (i == 0 && f.ArmR != null) f.ArmR.localRotation = Quaternion.Euler(-88f, 0f, 0f); // rifle up
                }
                if (_t > 6.6f) Shoot(dt, fam);
            }
            if (_flash != null && _flash.activeSelf && Time.time - _shotT > 0.06f) _flash.SetActive(false);

            // only Maily is left: run!
            if (_nextShot >= _pack.Count && _familyOut && _t > 6.6f && !_runnerCalled && Time.time - _shotT > 1.2f)
            {
                _runnerCalled = true;
                Captions.I?.Show("\"עכשיו תורך, מיילי!\"  רוצי!!", 2f);
                _m.RaiseBarked(); Sfx.I?.Play3D(Sfx.I.Bark, _m.transform.position, 1f, 1.2f);
                Invoke(nameof(StartRunner), 1.6f);
            }
        }

        Vector3 AimTarget()
        {
            for (int i = _nextShot; i < _pack.Count; i++) if (!_pack[i].Shot) return _pack[i].T.position;
            return _m.transform.position;
        }

        void Bob(PackDog p, float dt)
        {
            p.Hop += dt * 9f;
            var pos = p.T.position; pos.y = Ground.HeightAt(pos) + Mathf.Abs(Mathf.Sin(p.Hop)) * 0.12f; p.T.position = pos;
        }

        void GiveRifle(FamilyMember f)
        {
            if (_rifle != null || f.ArmR == null) return;
            _rifle = new GameObject("Rifle");
            _rifle.transform.SetParent(f.ArmR, false);
            var dark = new Color(0.18f, 0.18f, 0.2f);
            Part(PrimitiveType.Cylinder, _rifle.transform, new Vector3(0, -1.0f, 0), new Vector3(0.05f, 0.45f, 0.05f), dark, "RifleMetal");
            Part(PrimitiveType.Cube, _rifle.transform, new Vector3(0, -0.6f, -0.03f), new Vector3(0.08f, 0.38f, 0.12f), Palette.Trunk, "RifleWood");
            _flash = Part(PrimitiveType.Sphere, _rifle.transform, new Vector3(0, -1.5f, 0), Vector3.one * 0.28f, Palette.CoinGold, "MuzzleFlash", 4f);
            _flash.SetActive(false);
        }

        static GameObject Part(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Color c, string key, float emission = 0f)
        {
            var go = GameObject.CreatePrimitive(t); Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false); go.transform.localPosition = pos; go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = Mats.Get(key, c, 0.3f, emission);
            return go;
        }

        void Shoot(float dt, Family fam)
        {
            // knocked-out dogs spin, flop over and vanish in a puff
            foreach (var p in _pack)
            {
                if (!p.Shot || p.Gone) continue;
                p.Down += dt;
                p.T.rotation = Quaternion.LookRotation(-_dir) * Quaternion.Euler(0, p.Down * 720f, Mathf.Min(1f, p.Down / 0.3f) * 90f);
                if (p.Down > 0.7f)
                {
                    p.Gone = true;
                    Fx.Dust(p.T.position, 1.2f); Fx.Sparkle(p.T.position + Vector3.up * 0.5f, Palette.CoinGold);
                    p.T.gameObject.SetActive(false);
                }
            }
            if (_nextShot >= _pack.Count || Time.time - _shotT < 0.55f) return;
            var dog = _pack[_nextShot++];
            dog.Shot = true; _shotT = Time.time;
            if (_flash != null) _flash.SetActive(true);
            Sfx.I?.Play2D(Sfx.I.Shot, 0.9f, Random.Range(0.95f, 1.08f));
            Sfx.I?.Play3D(Sfx.I.Yip, dog.T.position, 0.8f, 1.3f);
        }

        // ================================================================== part 2: the great chase
        static readonly Vector3 Track = new Vector3(-1650f, 0f, -1750f);  // runs towards +Z, far outside the kibbutz
        const float Length = 420f, RunSpeed = 7.6f, FamilySpeed = 6.7f, MaxLead = 12f, StartGap = 10f;

        class Obstacle { public float Z, Depth, Height; public bool High, Moving, Hit; public Transform T; }
        readonly List<Obstacle> _obs = new List<Obstacle>();
        readonly List<(Transform t, Vector3 v)> _birds = new List<(Transform, Vector3)>();
        readonly List<Vector3> _perches = new List<Vector3>();
        Transform _course;
        float _z, _y, _vy, _speed, _stumble, _duck, _famZ, _runT;
        bool _grounded = true;

        void StartRunner()
        {
            if (_course == null) BuildCourse();
            _phase = Phase.Runner; _runT = 0f; _z = 0f; _y = 0f; _vy = 0f; _speed = 0f; _famZ = -StartGap;
            foreach (var p in _pack) if (p.T != null) p.T.gameObject.SetActive(false);
            if (_rifle != null) _rifle.SetActive(false); // they drop it and just run
            _m.transform.SetPositionAndRotation(Track, Quaternion.identity);
            var fam = Family.I;
            if (fam != null)
                for (int i = 0; i < fam.Members.Count; i++)
                    fam.Members[i].Teleport(Track + new Vector3((i - 0.5f) * 1.4f, 0f, _famZ), Track + Vector3.forward * 20f);
            if (_cam == null) { _cam = new GameObject("Finale Camera").AddComponent<CinemachineCamera>(); _cam.Priority = 60; }
            var lens = LensSettings.Default; lens.FieldOfView = 58f; lens.NearClipPlane = 0.1f; lens.FarClipPlane = 1500f; _cam.Lens = lens;
            PlaceRunnerCam(1f);
            Captions.I?.Show("רוצי! רווח = קפיצה, S = התכופפות. אי אפשר לזוז לצדדים!", 4f);
        }

        void BuildCourse()
        {
            _course = new GameObject("Chase Course").transform; _course.position = Track;
            var field = Box(new Vector3(0, -0.25f, Length * 0.5f), new Vector3(90f, 0.5f, Length + 120f), Color.Lerp(Palette.Field, Palette.LawnDeep, 0.35f), "RunField", true);
            Box(new Vector3(0, 0.005f, Length * 0.5f), new Vector3(3.6f, 0.01f, Length + 120f), Color.Lerp(Palette.Kurkar, Palette.Trunk, 0.25f), "RunLane", false);
            // fences on both sides: the reason she can't swerve
            for (float z = -20f; z < Length + 40f; z += 3f)
                foreach (var x in new[] { -2.3f, 2.3f })
                {
                    Box(new Vector3(x, 0.55f, z), new Vector3(0.12f, 1.1f, 0.12f), Palette.Trunk, "RunPost", false);
                    if (z % 9f < 0.1f) _perches.Add(Track + new Vector3(x, 1.15f, z));
                }
            foreach (var x in new[] { -2.3f, 2.3f })
                foreach (var y in new[] { 0.45f, 0.9f })
                    Box(new Vector3(x, y, Length * 0.5f + 10f), new Vector3(0.06f, 0.1f, Length + 60f), Color.Lerp(Palette.Trunk, Palette.WallOchre, 0.3f), "RunRail", false);
            // orchards and cypresses beyond the fences
            var rnd = new System.Random(7);
            for (float z = -10f; z < Length + 60f; z += 6f)
                foreach (var sgn in new[] { -1f, 1f })
                {
                    float x = sgn * (6f + (float)rnd.NextDouble() * 10f);
                    bool cypress = rnd.NextDouble() < 0.3;
                    Cyl(new Vector3(x, 0.6f, z), new Vector3(0.25f, 0.6f, 0.25f), Palette.Trunk, "RunTrunk");
                    var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(crown.GetComponent<Collider>());
                    crown.transform.SetParent(_course, false);
                    crown.transform.localPosition = new Vector3(x, cypress ? 2.6f : 1.9f, z);
                    crown.transform.localScale = cypress ? new Vector3(0.9f, 3.2f, 0.9f) : new Vector3(2.4f, 1.8f, 2.4f);
                    crown.GetComponent<MeshRenderer>().sharedMaterial = Mats.Get(cypress ? "RunCypress" : "RunCrown", cypress ? Color.Lerp(Palette.Foliage, Palette.UiInk, 0.3f) : Palette.Foliage, 0.1f);
                }
            // the finish: the kibbutz back gate and open fields beyond
            foreach (var x in new[] { -2.6f, 2.6f }) Box(new Vector3(x, 1.6f, Length), new Vector3(0.3f, 3.2f, 0.3f), Palette.WallWhite, "RunGate", false);
            Box(new Vector3(0, 3.1f, Length), new Vector3(5.6f, 0.4f, 0.3f), Palette.CoinGold, "RunGateTop", false, 0.8f);
            var sign = new GameObject("Finish sign"); sign.transform.SetParent(_course, false);
            sign.transform.localPosition = new Vector3(0, 3.1f, Length - 0.2f); sign.transform.localRotation = Quaternion.Euler(0, 180, 0);
            var t = sign.AddComponent<TMPro.TextMeshPro>(); t.fontSize = 3.2f; t.alignment = TMPro.TextAlignmentOptions.Center; t.color = Palette.UiInk;
            t.rectTransform.sizeDelta = new Vector2(5.4f, 0.5f); Heb.Set(t, "אל השדות הפתוחים");

            // obstacles: every 12–19 m, a mix to jump and to duck under
            float z0 = 26f; int n = 0;
            while (z0 < Length - 12f)
            {
                int kind = n < 2 ? n : rnd.Next(7);   // the first two teach: jump, then duck
                _obs.Add(MakeObstacle(kind, z0));
                z0 += 12f + (float)rnd.NextDouble() * 7f; n++;
            }
            // a few parked golf carts and a scarecrow along the way, just for the eye
            for (float z = 60f; z < Length; z += 85f)
            {
                var cart = new GameObject("Parked cart"); cart.transform.SetParent(_course, false);
                cart.transform.localPosition = new Vector3(z % 170f < 1f ? 4.2f : -4.2f, 0f, z); cart.transform.localRotation = Quaternion.Euler(0, 90, 0);
                var v = cart.AddComponent<TrafficVehicle>(); v.enabled = false;
                CharacterKit.BuildCart(v, Palette.WallWhite, Palette.MiniSky, true);
                cart.GetComponent<Rigidbody>().isKinematic = true; // parked: a prop, not traffic
            }
        }

        Obstacle MakeObstacle(int kind, float z)
        {
            var o = new Obstacle { Z = z };
            var root = new GameObject("Obstacle " + kind).transform; root.SetParent(_course, false); root.localPosition = new Vector3(0, 0, z);
            o.T = root;
            switch (kind)
            {
                case 0: // hay bale
                    o.Height = 0.55f; o.Depth = 1.0f;
                    Box(root, new Vector3(0, 0.3f, 0), new Vector3(2.6f, 0.6f, 1.0f), Color.Lerp(Palette.Citrus, Palette.WallOchre, 0.55f), "RunHay");
                    Box(root, new Vector3(0, 0.3f, 0), new Vector3(2.62f, 0.08f, 1.02f), Palette.Trunk, "RunTwine");
                    break;
                case 1: // laundry line with sheets: duck!
                    o.High = true; o.Depth = 0.5f;
                    foreach (var x in new[] { -2.0f, 2.0f }) Box(root, new Vector3(x, 0.85f, 0), new Vector3(0.1f, 1.7f, 0.1f), Palette.Trunk, "RunPost");
                    Box(root, new Vector3(0, 1.62f, 0), new Vector3(4f, 0.03f, 0.03f), Palette.WallWhite, "RunLine");
                    Color[] sheets = { Palette.WallWhite, Palette.MiniSky, Palette.MiniCards };
                    for (int i = 0; i < 3; i++) Box(root, new Vector3(-1.2f + i * 1.2f, 1.04f, 0), new Vector3(1.05f, 1.15f, 0.03f), sheets[i], "RunSheet" + i);
                    break;
                case 2: // log
                    o.Height = 0.45f; o.Depth = 0.55f;
                    Cyl(root, new Vector3(0, 0.25f, 0), new Vector3(0.5f, 1.4f, 0.5f), Palette.Trunk, "RunLog", new Vector3(0, 0, 90));
                    break;
                case 3: // irrigation pipe
                    o.Height = 0.35f; o.Depth = 0.3f;
                    Cyl(root, new Vector3(0, 0.18f, 0), new Vector3(0.3f, 2.4f, 0.3f), Palette.Silver, "RunPipe", new Vector3(0, 0, 90));
                    break;
                case 4: // a stack of crates
                    o.Height = 0.7f; o.Depth = 0.75f;
                    Box(root, new Vector3(-0.5f, 0.36f, 0), new Vector3(0.9f, 0.72f, 0.75f), Color.Lerp(Palette.Trunk, Palette.WallOchre, 0.4f), "RunCrate");
                    Box(root, new Vector3(0.5f, 0.36f, 0), new Vector3(0.9f, 0.72f, 0.75f), Color.Lerp(Palette.Trunk, Palette.WallOchre, 0.5f), "RunCrate2");
                    break;
                case 5: // low branch across the lane: duck!
                    o.High = true; o.Depth = 0.45f;
                    Cyl(root, new Vector3(0, 0.62f, 0), new Vector3(0.18f, 2.6f, 0.18f), Palette.Trunk, "RunLog", new Vector3(0, 0, 90));
                    var leaves = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(leaves.GetComponent<Collider>());
                    leaves.transform.SetParent(root, false); leaves.transform.localPosition = new Vector3(1.6f, 0.9f, 0); leaves.transform.localScale = new Vector3(1.6f, 1.0f, 1.0f);
                    leaves.GetComponent<MeshRenderer>().sharedMaterial = Mats.Get("RunCrown", Palette.Foliage, 0.1f);
                    break;
                default: // a barrel rolling towards her
                    o.Height = 0.6f; o.Depth = 0.6f; o.Moving = true;
                    Cyl(root, new Vector3(0, 0.3f, 0), new Vector3(0.6f, 0.5f, 0.6f), Palette.BullyRed, "RunBarrel", new Vector3(0, 0, 90));
                    break;
            }
            return o;
        }

        void TickRunner(float dt)
        {
            _runT += dt;
            var k = Keyboard.current; var pad = Gamepad.current;
            bool jump = (k != null && (k.spaceKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame)) || (pad != null && pad.buttonSouth.wasPressedThisFrame);
            bool duckHeld = (k != null && (k.sKey.isPressed || k.downArrowKey.isPressed || k.leftCtrlKey.isPressed)) || (pad != null && (pad.buttonEast.isPressed || pad.leftStick.ReadValue().y < -0.5f));
            bool go = _runT > 1.6f; // a beat to read the caption

            // speed and stumbles
            _stumble -= dt;
            float want = !go ? 0f : _stumble > 0f ? 2.4f : RunSpeed;
            _speed = Mathf.MoveTowards(_speed, want, (want < _speed ? 30f : 9f) * dt);
            _z += _speed * dt;

            // jump / duck (vertical only: the lane is fenced)
            if (jump && _grounded && go) { _vy = 7.4f; _grounded = false; Sfx.I?.Play2D(Sfx.I.Hop, 0.6f); _m.RaiseJumped(); }
            if (!_grounded)
            {
                _vy -= 26f * dt; _y += _vy * dt;
                if (_y <= 0f) { _y = 0f; _vy = 0f; _grounded = true; _m.RaiseLanded(); }
            }
            bool duck = duckHeld && _grounded;
            _duck = Mathf.MoveTowards(_duck, duck ? 1f : 0f, dt * 10f);
            if (_gait != null) { _gait.FightCrouch = _duck; _gait.FightHurt = Mathf.Clamp01(_stumble * 1.5f); }
            _m.transform.SetPositionAndRotation(Track + new Vector3(0f, _y, _z), Quaternion.identity);
            _m.SetFightMotion(new Vector3(0f, _vy, _speed), _grounded);

            // obstacles
            foreach (var o in _obs)
            {
                if (o.Moving && !o.Hit && o.Z - _z < 30f && go) { o.Z -= 2.6f * dt; o.T.localPosition = new Vector3(0, 0, o.Z); o.T.GetChild(0).Rotate(Vector3.up, -300f * dt, Space.Self); }
                if (o.Hit) continue;
                float front = _z + 0.35f, back = _z - 0.35f;
                if (front < o.Z - o.Depth * 0.5f || back > o.Z + o.Depth * 0.5f) continue;
                bool hit = o.High ? _duck < 0.5f : _y < o.Height - 0.05f;
                if (!hit) continue;
                o.Hit = true; _stumble = 0.9f; _speed = 1.5f;
                Sfx.I?.Play2D(Sfx.I.HitHeavy, 0.8f, 1.1f); Fx.Dust(_m.transform.position, 1f);
                o.T.localPosition += new Vector3(Random.value < 0.5f ? -1.2f : 1.2f, 0, 1.0f); // knocked aside
                o.T.localRotation = Quaternion.Euler(0, Random.Range(-40f, 40f), Random.Range(-25f, 25f));
                Captions.I?.Show(o.High ? "אוי! צריך להתכופף (S)" : "אוי! צריך לקפוץ (רווח)", 1.4f);
            }

            // parrots burst off the fence posts as she passes
            for (int i = _perches.Count - 1; i >= 0; i--)
                if (_perches[i].z - (Track.z + _z) < 6f)
                {
                    var b = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(b.GetComponent<Collider>());
                    b.transform.position = _perches[i]; b.transform.localScale = new Vector3(0.22f, 0.2f, 0.32f);
                    b.GetComponent<MeshRenderer>().sharedMaterial = Mats.Get("RunParrot", Palette.ParrotGreen, 0.2f);
                    _birds.Add((b.transform, new Vector3(Mathf.Sign(_perches[i].x - Track.x) * 3f, 4f, 2f + Random.value * 3f)));
                    if (Random.value < 0.4f) Sfx.I?.Play2D(Sfx.I.Squawk, 0.35f, Random.Range(0.9f, 1.2f));
                    _perches.RemoveAt(i);
                }
            for (int i = _birds.Count - 1; i >= 0; i--)
            {
                var (t, v) = _birds[i];
                if (t == null) { _birds.RemoveAt(i); continue; }
                t.position += v * dt; t.localScale *= 1f - dt * 0.15f;
                if (t.position.y > 25f) { Destroy(t.gameObject); _birds.RemoveAt(i); }
            }

            // the family: a bit slower than her clean run, but never more than MaxLead behind
            float famSpeed = go ? (_z - _famZ > MaxLead ? _speed : FamilySpeed) : 0f;
            _famZ += famSpeed * dt;
            var fam = Family.I;
            if (fam != null)
                for (int i = 0; i < fam.Members.Count; i++)
                {
                    var f = fam.Members[i];
                    var p = Track + new Vector3((i - 0.5f) * 1.4f, 0f, _famZ - i * 0.6f);
                    if (famSpeed > 0.1f) f.MoveToward(p + Vector3.forward * 0.01f, famSpeed + 4f, dt); else f.Idle(dt, _m.transform.position);
                    f.transform.position = new Vector3(p.x, 0f, Mathf.Min(f.transform.position.z, p.z + 0.2f));
                }
            if (Random.value < dt * 0.25f && go) Captions.I?.Show(Random.value < 0.5f ? "\"מיילי! תחזרי!\"" : "\"עצרי! יש עוגיות בבית!\"", 1.6f);

            PlaceRunnerCam(dt);
            float gap = _z - _famZ;
            Captions.I?.Pill($"המשפחה מאחורייך: {Mathf.Max(0f, gap):0} מ'   ·   עוד {Mathf.Max(0f, Length - _z):0} מ'", gap < 4f ? Palette.BullyRed : Palette.Bronze);

            if (go && gap < 0.9f) Finish(false);
            else if (_z >= Length + 2f) Finish(true);
        }

        void PlaceRunnerCam(float dt)
        {
            if (_cam == null) return;
            var mp = _m.transform.position;
            var want = new Vector3(Track.x + 2.4f, 2.3f, mp.z - 5.6f);
            var t = _cam.transform;
            t.position = dt >= 1f ? want : Vector3.Lerp(t.position, want, 1f - Mathf.Exp(-dt * 8f));
            t.rotation = Quaternion.LookRotation(new Vector3(Track.x, 0.6f, mp.z + 6f) - t.position);
        }

        void Finish(bool free)
        {
            _phase = Phase.Off;
            if (_gait != null) { _gait.FightCrouch = 0f; _gait.FightHurt = 0f; }
            if (free)
            {
                Fx.Hearts(_m.transform.position + Vector3.up * 0.8f);
                GameManager.I.EndGame("free", "חופשית!",
                    "מיילי קפצה מעל המכשול האחרון ורצה אל השדות הפתוחים. המשפחה עצרה, התנשפה ונופפה לשלום. אולי היא תחזור לארוחת ערב?");
            }
            else
                GameManager.I.EndGame("caught", "נתפסה ברגע האחרון",
                    "הפעם המשפחה הייתה מהירה יותר. מיילי חזרה הביתה, קיבלה קערה מלאה ונרדמה במלונה, וחלמה על דררות.");
        }

        // ------------------------------------------------------------------ small builders
        GameObject Box(Vector3 local, Vector3 size, Color c, string key, bool collider, float emission = 0f) => Box(_course, local, size, c, key, collider, emission);
        static GameObject Box(Transform parent, Vector3 local, Vector3 size, Color c, string key, bool collider = false, float emission = 0f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (!collider) Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false); go.transform.localPosition = local; go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = Mats.Get(key, c, 0.15f, emission);
            return go;
        }
        void Cyl(Vector3 local, Vector3 size, Color c, string key) => Cyl(_course, local, size, c, key, Vector3.zero);
        static void Cyl(Transform parent, Vector3 local, Vector3 size, Color c, string key, Vector3 euler)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false); go.transform.localPosition = local; go.transform.localScale = size; go.transform.localEulerAngles = euler;
            go.GetComponent<MeshRenderer>().sharedMaterial = Mats.Get(key, c, 0.15f);
        }

        static float Flat(Vector3 a, Vector3 b) { a.y = 0; b.y = 0; return Vector3.Distance(a, b); }
    }
}

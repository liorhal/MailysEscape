using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

namespace Maily
{
    /// <summary>
    /// Road safety, kibbutz style. If Maily stands in the middle of a road and a car has to stop for her, the
    /// driver gets out and phones the police. Patrol cars (and, this being a cartoon, a tank) come screaming in;
    /// an officer picks Maily up, puts her in the back of a patrol car and they drive off. Fade to black: Maily
    /// spends 30 seconds in a cell, then the police drop her off at home.
    /// </summary>
    public class Arrest : MonoBehaviour
    {
        public static Arrest I { get; private set; }
        public static bool InJail => I != null && I._phase == Phase.Jail;

        public float StopSeconds = 1.4f, JailSeconds = 30f;
        static readonly Vector3 JailOrigin = new Vector3(-1650f, 0f, 1650f);

        enum Phase { Idle, Scene, Jail }
        Phase _phase = Phase.Idle;
        float _t, _cooldown, _sirenT, _drive; // _drive: time since Maily is in the patrol car
        int _step;

        MailyController _m; CharacterController _cc; MailyGait _gait;
        TrafficVehicle _car;
        CinemachineCamera _cam;
        Vector3 _road, _side, _center;
        FamilyMember _driver, _officer;
        readonly List<GameObject> _spawned = new List<GameObject>();
        class Cruiser { public Transform T; public Vector3 From, To; public Renderer Red, Blue; public bool Tank; }
        readonly List<Cruiser> _fleet = new List<Cruiser>();
        Cruiser _ride;
        Transform _jail; Vector3 _cellSpot;

        void Awake() => I = this;

        void Update()
        {
            var gm = GameManager.I; if (gm == null) return;
            float dt = Time.deltaTime; if (dt <= 0f) return;
            _m = MailyController.I; if (_m == null) return;
            _cooldown -= dt;
            switch (_phase)
            {
                case Phase.Idle: Watch(gm); break;
                case Phase.Scene: TickScene(dt); break;
                case Phase.Jail: TickJail(dt); break;
            }
        }

        // ------------------------------------------------------------------ the trigger
        void Watch(GameManager gm)
        {
            if (_cooldown > 0f || gm.State != GameState.Playing || gm.MapOpen) return;
            if (MiniGameManager.I != null && MiniGameManager.I.Busy) return;
            if (HouseInterior.I != null && HouseInterior.I.Active) return;
            if (Family.I != null && Family.I.IntroActive) return;
            foreach (var v in TrafficVehicle.All)
                if (!v.IsCart && v.StoppedForMaily > StopSeconds && OnRoad()) { Begin(v); return; }
        }

        bool OnRoad()
        {
            var p = _m.transform.position + Vector3.up * 0.6f;
            return Physics.Raycast(p, Vector3.down, out var hit, 1.5f, ~0, QueryTriggerInteraction.Ignore) && hit.collider.name == "Asphalt";
        }

        /// <summary>Test hook (the web page can call it via unityInstance.SendMessage("Systems", "DebugArrest")).</summary>
        public void DebugArrest()
        {
            _m = MailyController.I; TrafficVehicle best = null; float bd = float.MaxValue;
            foreach (var v in TrafficVehicle.All) { if (v.IsCart) continue; float d = (v.transform.position - _m.transform.position).sqrMagnitude; if (d < bd) { bd = d; best = v; } }
            if (best == null || GameManager.I.State != GameState.Playing) return;
            // put Maily on the road in front of that car
            var p = best.transform.position + best.transform.forward * (best.Length * 0.5f + 3f);
            p.y = Ground.HeightAt(p) + 0.1f;
            _m.Place(p, best.transform.eulerAngles.y + 180f);
            Begin(best);
        }

        void Begin(TrafficVehicle car)
        {
            _phase = Phase.Scene; _t = 0f; _step = 0; _car = car; car.Hold = true;
            _cc = _m.GetComponent<CharacterController>(); _gait = _m.GetComponentInChildren<MailyGait>();
            GameManager.I.SetCutscene(true);
            Family.I?.Halt();
            _m.FightLock = true; if (_cc != null) _cc.enabled = false;
            _m.SetFightMotion(Vector3.zero, true);
            _road = car.transform.forward; _road.y = 0; _road.Normalize();
            _side = new Vector3(_road.z, 0, -_road.x);
            _center = _m.transform.position;
            Sfx.I?.Play3D(Sfx.I.Honk, car.transform.position, 1f, 0.95f);

            // the driver, out of the driver's door
            _driver = Person("Driver", car.transform.position - _side * 1.4f + _road * 0.4f, new Color(0.36f, 0.55f, 0.42f), new Color(0.3f, 0.3f, 0.34f), Palette.DogBrown, false);
            var phone = Prim(PrimitiveType.Cube, _driver.ArmR, new Vector3(0, -0.6f, 0.05f), new Vector3(0.06f, 0.13f, 0.03f), Palette.UiInk, "Phone");
            phone.name = "Phone";

            var go = new GameObject("Arrest Camera"); _cam = go.AddComponent<CinemachineCamera>();
            var lens = LensSettings.Default; lens.FieldOfView = 50f; lens.NearClipPlane = 0.1f; lens.FarClipPlane = 1500f; _cam.Lens = lens;
            _cam.Priority = 60;
            Frame((_center + car.transform.position) * 0.5f, 8f, 3.2f, true);
            Captions.I?.Show("צפצוף! מכונית נעצרה מול מיילי, באמצע הכביש...", 2.6f);
        }

        void Frame(Vector3 look, float dist, float height, bool snap)
        {
            var pos = look + _side * dist + Vector3.up * height - _road * dist * 0.35f;
            if (Physics.Linecast(look + Vector3.up * 1.2f, pos, ~0, QueryTriggerInteraction.Ignore)) pos = look - _side * dist + Vector3.up * height - _road * dist * 0.35f;
            var t = _cam.transform;
            if (snap) t.position = pos; else t.position = Vector3.Lerp(t.position, pos, 1f - Mathf.Exp(-Time.deltaTime * 2.5f));
            t.rotation = Quaternion.Slerp(t.rotation, Quaternion.LookRotation(look + Vector3.up * 0.7f - t.position), snap ? 1f : 1f - Mathf.Exp(-Time.deltaTime * 4f));
        }

        // ------------------------------------------------------------------ the scene
        void TickScene(float dt)
        {
            float prev = _t; _t += dt;
            bool At(float s) => prev < s && _t >= s;
            var mp = _m.transform.position;

            // 0–3.5 s: the driver gets out, phone to the ear
            if (_t < 3.6f)
            {
                var stand = _center - _road * 1.6f - _side * 1.2f;
                if ((_driver.transform.position - stand).sqrMagnitude > 0.05f && _t < 1.6f) _driver.MoveToward(stand, 1.4f, dt);
                else _driver.Idle(dt, mp);
                if (_t > 1.4f) { _driver.ArmR.localRotation = Quaternion.Euler(-150f, 0f, 18f); } // phone to the ear
                Frame((_center + _car.transform.position) * 0.5f, 7f, 2.6f, false);
            }
            if (At(1.8f)) Captions.I?.Show("\"הלו, משטרה? יש פה כלבה באמצע הכביש!\"", 2.4f);

            // 3.6 s: sirens, the fleet rolls in from both ends of the road
            if (At(3.6f)) CallFleet();
            if (_t >= 3.6f && _step < 3)
            {
                _sirenT -= dt; if (_sirenT <= 0f) { _sirenT = 1.9f; Sfx.I?.Play2D(Sfx.I.Siren, 0.45f); }
                float k = Mathf.Clamp01((_t - 3.6f) / 3.2f); k = 1f - (1f - k) * (1f - k);
                foreach (var c in _fleet) if (c != _ride || _step < 3) Drive(c, Vector3.Lerp(c.From, c.To, k), (c.To - c.From).normalized, k < 1f);
                _driver.Idle(dt, _fleet.Count > 0 ? _fleet[0].T.position : mp);
                Frame(_center, Mathf.Lerp(8f, 15f, k), Mathf.Lerp(3f, 7f, k), false);
            }
            if (At(4.2f)) Captions.I?.Show("ווי-וו ווי-וו! ניידות... והרבה. ואפילו טנק?!", 2.6f);
            Blink();

            // 7.2 s: an officer walks over and picks Maily up
            if (At(7.2f)) { _step = 1; _officer = Person("Officer", _ride.T.position + _ride.T.right * 1.4f, new Color(0.16f, 0.24f, 0.44f), new Color(0.14f, 0.16f, 0.22f), Palette.UiInk, false); Cap(_officer); }
            if (_step == 1)
            {
                if (_t > 11f) _officer.Teleport(mp - _side * 0.6f, mp); // never stuck behind the car
                _officer.MoveToward(mp, 2.4f, dt, 0.55f);
                if ((_officer.transform.position - mp).sqrMagnitude < 0.7f) { _step = 2; Captions.I?.Show("\"בואי, כלבלבה. נוסעים לתחנה.\"", 2.2f); Sfx.I?.Play3D(Sfx.I.Yip, mp, 0.9f, 1.2f); }
            }
            if (_step == 2)
            {
                var door = _ride.T.position + _ride.T.right * 1.5f;
                _officer.MoveToward(door, 1.8f, dt, 0.2f);
                Carry();
                if ((_officer.transform.position - door).sqrMagnitude < 0.35f || _t > 17f)
                {
                    _step = 3; _drive = 0f;
                    _officer.gameObject.SetActive(false);
                    _m.transform.position = _ride.T.position + Vector3.up * 0.75f; // in the back seat
                    Sfx.I?.Play3D(Sfx.I.Thud, _ride.T.position, 0.8f, 0.8f);
                }
            }
            if (_step >= 2 && _step < 3) Frame(_officer.transform.position, 7f, 3f, false);

            // the patrol car pulls away, the screen goes black
            if (_step == 3)
            {
                float d0 = _drive; _drive += dt;
                bool After(float s) => d0 < s && _drive >= s;
                _ride.T.position += _ride.T.forward * 7f * Mathf.Clamp01((_drive - 0.1f) / 1.2f) * dt;
                _ride.T.position = new Vector3(_ride.T.position.x, Ground.HeightAt(_ride.T.position), _ride.T.position.z);
                _m.transform.position = _ride.T.position + Vector3.up * 0.75f;
                Frame(_ride.T.position, 9f, 3.5f, false);
                if (After(0.7f)) { Captions.I?.Show("ונוסעים...", 2f); Sfx.I?.Play2D(Sfx.I.Siren, 0.5f, 0.9f); }
                if (After(2.1f)) Captions.I?.Fade(1f, 1.2f);
                if (_drive > 3.1f && (Captions.I == null || Captions.I.IsBlack)) ToJail();
            }
        }

        void CallFleet()
        {
            _fleet.Clear();
            var slots = new[] { (_road, 7f, false), (-_road, 8f, false), (_side, 6.5f, false), (-_side + _road * 0.6f, 7.5f, false), (_road * 1.0f + _side * 0.3f, 14f, true) };
            for (int i = 0; i < slots.Length; i++)
            {
                var (dir, dist, tank) = slots[i];
                var to = _center + dir.normalized * dist;
                var from = to + (Vector3.Dot(dir, _road) >= 0 ? _road : -_road) * 45f;
                var c = tank ? BuildTank() : BuildCruiser();
                c.From = from; c.To = to;
                var face = to - from; face.y = 0;
                c.T.SetPositionAndRotation(from, Quaternion.LookRotation(face));
                _fleet.Add(c);
                if (!tank && _ride == null && i == 1) _ride = c;
            }
            if (_ride == null) _ride = _fleet[0];
        }

        static void Drive(Cruiser c, Vector3 p, Vector3 dir, bool moving)
        {
            p.y = Ground.HeightAt(p); c.T.position = p;
            dir.y = 0; if (moving && dir.sqrMagnitude > 0.01f) c.T.rotation = Quaternion.LookRotation(dir);
        }

        void Blink()
        {
            bool on = Mathf.Repeat(Time.time * 4f, 1f) < 0.5f;
            foreach (var c in _fleet) { if (c.Red != null) c.Red.enabled = on; if (c.Blue != null) c.Blue.enabled = !on; }
        }

        void Carry()
        {
            var o = _officer.transform;
            if (_cc != null) _cc.enabled = false;
            _m.transform.SetPositionAndRotation(o.position + o.forward * 0.42f + Vector3.up * 0.82f, o.rotation * Quaternion.Euler(0, 90, 0));
            _m.SetFightMotion(Vector3.zero, false);
            if (_officer.ArmL != null) _officer.ArmL.localRotation = Quaternion.Euler(-75f, 0, 0);
            if (_officer.ArmR != null) _officer.ArmR.localRotation = Quaternion.Euler(-75f, 0, 0);
        }

        // ------------------------------------------------------------------ jail
        void ToJail()
        {
            if (_jail == null) BuildJail();
            Cleanup();
            _phase = Phase.Jail; _t = 0f;
            _m.FightLock = false;
            _m.Place(_cellSpot, 0f);
            GameManager.I.SetCutscene(false);
            Captions.I?.Fade(0f, 1f);
            Captions.I?.Show("מיילי בכלא! חצי דקה לחשוב על מה שעשתה...", 3.5f);
        }

        void TickJail(float dt)
        {
            _t += dt;
            Captions.I?.Pill($"בכלא: עוד {Mathf.CeilToInt(Mathf.Max(0f, JailSeconds - _t))} שניות", new Color(0.16f, 0.24f, 0.44f));
            if (_t > JailSeconds - 1.2f && _t - dt <= JailSeconds - 1.2f) Captions.I?.Fade(1f, 1.5f);
            if (_t < JailSeconds) return;
            // released: the police drop her off at home
            _phase = Phase.Idle; _cooldown = 45f;
            var home = Family.I != null ? Family.I.Gate : _center;
            _m.Place(home + Vector3.up * 0.1f, 0f);
            Family.I?.Resume();
            Captions.I?.Fade(0f, 1f);
            Captions.I?.Show("השוטרים החזירו את מיילי הביתה. ומעכשיו: לא עומדים באמצע הכביש!", 4f);
        }

        void BuildJail()
        {
            _jail = new GameObject("Dog Jail").transform; _jail.position = JailOrigin;
            var concrete = new Color(0.62f, 0.62f, 0.6f);
            Box("Floor", new Vector3(0, -0.25f, 0), new Vector3(12f, 0.5f, 12f), new Color(0.5f, 0.5f, 0.48f), "JailFloor", true);
            // three solid walls (render inward only, so the camera sees in from outside) + a wall of bars
            var walls = new MeshKit();
            void Wall(Vector3 a, Vector3 b, Vector3 inward)
            {
                Vector3 A = a, B = b, C = b + Vector3.up * 2.8f, D = a + Vector3.up * 2.8f;
                if (Vector3.Dot(Vector3.Cross(B - A, C - A), inward) > 0) walls.Quad(A, B, C, D); else walls.Quad(B, A, D, C);
                var col = new GameObject("Wall collider").AddComponent<BoxCollider>(); col.transform.SetParent(_jail, false);
                col.transform.localPosition = (a + b) * 0.5f + Vector3.up * 1.4f - inward * 0.15f;
                bool alongX = Mathf.Abs(b.x - a.x) > Mathf.Abs(b.z - a.z);
                col.size = alongX ? new Vector3((b - a).magnitude + 0.4f, 2.8f, 0.3f) : new Vector3(0.3f, 2.8f, (b - a).magnitude + 0.4f);
            }
            Wall(new Vector3(-3, 0, 2.5f), new Vector3(3, 0, 2.5f), Vector3.back);
            Wall(new Vector3(-3, 0, -2.5f), new Vector3(-3, 0, 2.5f), Vector3.right);
            Wall(new Vector3(3, 0, 2.5f), new Vector3(3, 0, -2.5f), Vector3.left);
            var wgo = new GameObject("Cell walls"); wgo.transform.SetParent(_jail, false);
            wgo.AddComponent<MeshFilter>().sharedMesh = walls.Bake("JailWalls");
            var wmr = wgo.AddComponent<MeshRenderer>(); wmr.sharedMaterial = Mats.Get("JailWall", concrete, 0.1f);
            wmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // the bars
            for (float x = -2.9f; x <= 2.91f; x += 0.28f)
                Cyl("Bar", new Vector3(x, 1.4f, -2.5f), new Vector3(0.06f, 1.4f, 0.06f), new Color(0.28f, 0.3f, 0.34f), "JailBar");
            foreach (var y in new[] { 0.05f, 2.75f }) Box("Rail", new Vector3(0, y, -2.5f), new Vector3(6f, 0.1f, 0.1f), new Color(0.28f, 0.3f, 0.34f), "JailBar", false);
            var barCol = new GameObject("Bars collider").AddComponent<BoxCollider>(); barCol.transform.SetParent(_jail, false);
            barCol.transform.localPosition = new Vector3(0, 1.4f, -2.5f); barCol.size = new Vector3(6.4f, 2.8f, 0.2f);
            // a bench, a blanket, a bowl and a little barred window
            Box("Bench", new Vector3(-1.8f, 0.25f, 1.9f), new Vector3(2f, 0.5f, 0.7f), Palette.Trunk, "JailBench", true);
            Box("Blanket", new Vector3(1.6f, 0.03f, 1.4f), new Vector3(1.2f, 0.05f, 0.9f), Palette.BullyRed, "JailBlanket", false);
            Cyl("Bowl", new Vector3(2.2f, 0.05f, -1.4f), new Vector3(0.35f, 0.05f, 0.35f), Palette.Silver, "JailBowl");
            Box("Window", new Vector3(0, 2.1f, 2.48f), new Vector3(1.2f, 0.6f, 0.02f), new Color(0.6f, 0.8f, 0.95f), "JailWindow", false, 0.6f);
            for (int i = 0; i < 4; i++) Box("Window bar", new Vector3(-0.45f + i * 0.3f, 2.1f, 2.46f), new Vector3(0.04f, 0.62f, 0.04f), new Color(0.28f, 0.3f, 0.34f), "JailBar", false);
            var sign = new GameObject("Sign"); sign.transform.SetParent(_jail, false); sign.transform.localPosition = new Vector3(0, 3.1f, -2.55f); sign.transform.localRotation = Quaternion.Euler(0, 180, 0);
            var t = sign.AddComponent<TMPro.TextMeshPro>(); t.fontSize = 3f; t.alignment = TMPro.TextAlignmentOptions.Center; t.color = Palette.CoinGold;
            t.rectTransform.sizeDelta = new Vector2(6f, 0.6f); Heb.Set(t, "כלא הכלבים של חצור");
            var lgo = new GameObject("Cell light"); lgo.transform.SetParent(_jail, false); lgo.transform.localPosition = new Vector3(0, 2.6f, 0);
            var light = lgo.AddComponent<Light>(); light.type = LightType.Point; light.range = 10f; light.intensity = 2f; light.color = new Color(1f, 0.93f, 0.8f);
            _cellSpot = JailOrigin + new Vector3(0, 0.15f, 0.2f);
        }

        // ------------------------------------------------------------------ cast & props
        FamilyMember Person(string n, Vector3 p, Color shirt, Color pants, Color hair, bool longHair)
        {
            var go = new GameObject(n); _spawned.Add(go);
            var m = go.AddComponent<FamilyMember>();
            CharacterKit.BuildPerson(m, 1.78f, shirt, pants, hair, longHair);
            m.Teleport(p, _center);
            return m;
        }

        void Cap(FamilyMember m)
        {
            float k = 1.78f / 1.75f, headY = 0.82f * k + 0.58f * k + 0.14f * k;
            Prim(PrimitiveType.Cylinder, m.Body, new Vector3(0, headY + 0.12f, 0), new Vector3(0.28f, 0.05f, 0.28f), new Color(0.12f, 0.16f, 0.3f), "PoliceCap");
            Prim(PrimitiveType.Cube, m.Body, new Vector3(0, headY + 0.08f, 0.13f), new Vector3(0.24f, 0.02f, 0.1f), Palette.UiInk, "PoliceVisor");
        }

        Cruiser BuildCruiser()
        {
            var root = new GameObject("Patrol car").transform; _spawned.Add(root.gameObject);
            var white = Palette.WallWhite; var blue = new Color(0.14f, 0.3f, 0.7f); var tyre = new Color(0.15f, 0.15f, 0.17f);
            Prim(PrimitiveType.Cube, root, new Vector3(0, 0.55f, 0), new Vector3(1.8f, 0.6f, 4.2f), white, "CopBody");
            Prim(PrimitiveType.Cube, root, new Vector3(0, 0.56f, 0), new Vector3(1.82f, 0.18f, 4.1f), blue, "CopStripe");
            Prim(PrimitiveType.Cube, root, new Vector3(0, 1.05f, -0.2f), new Vector3(1.6f, 0.5f, 2.1f), new Color(0.55f, 0.7f, 0.82f), "CopGlass");
            Prim(PrimitiveType.Cube, root, new Vector3(0, 1.31f, -0.2f), new Vector3(1.62f, 0.05f, 2.0f), white, "CopBody");
            foreach (var x in new[] { -0.8f, 0.8f }) foreach (var z in new[] { 1.35f, -1.35f })
                Prim(PrimitiveType.Cylinder, root, new Vector3(x, 0.32f, z), new Vector3(0.62f, 0.12f, 0.62f), tyre, "CopTyre").transform.localRotation = Quaternion.Euler(0, 0, 90);
            var c = new Cruiser { T = root };
            c.Red = Prim(PrimitiveType.Cube, root, new Vector3(-0.35f, 1.42f, -0.2f), new Vector3(0.6f, 0.18f, 0.3f), new Color(1f, 0.15f, 0.12f), "CopRed", 3f).GetComponent<Renderer>();
            c.Blue = Prim(PrimitiveType.Cube, root, new Vector3(0.35f, 1.42f, -0.2f), new Vector3(0.6f, 0.18f, 0.3f), new Color(0.2f, 0.4f, 1f), "CopBlue", 3f).GetComponent<Renderer>();
            var sign = new GameObject("Police"); sign.transform.SetParent(root, false); sign.transform.localPosition = new Vector3(0.92f, 0.6f, 0); sign.transform.localRotation = Quaternion.Euler(0, -90, 0);
            var t = sign.AddComponent<TMPro.TextMeshPro>(); t.fontSize = 3.2f; t.alignment = TMPro.TextAlignmentOptions.Center; t.color = blue;
            t.rectTransform.sizeDelta = new Vector2(3.6f, 0.5f); Heb.Set(t, "משטרה");
            return c;
        }

        Cruiser BuildTank()
        {
            var root = new GameObject("Tank").transform; _spawned.Add(root.gameObject);
            var olive = new Color(0.4f, 0.45f, 0.27f); var dark = new Color(0.2f, 0.21f, 0.18f);
            Prim(PrimitiveType.Cube, root, new Vector3(0, 0.75f, 0), new Vector3(3.2f, 0.8f, 6f), olive, "TankHull");
            foreach (var x in new[] { -1.7f, 1.7f }) Prim(PrimitiveType.Cube, root, new Vector3(x, 0.5f, 0), new Vector3(0.7f, 1f, 6.4f), dark, "TankTrack");
            Prim(PrimitiveType.Cube, root, new Vector3(0, 1.5f, -0.4f), new Vector3(2.2f, 0.75f, 2.6f), olive, "TankHull");
            Prim(PrimitiveType.Cylinder, root, new Vector3(0, 1.55f, 2.4f), new Vector3(0.22f, 1.9f, 0.22f), dark, "TankGun").transform.localRotation = Quaternion.Euler(90, 0, 0);
            var c = new Cruiser { T = root, Tank = true };
            c.Red = Prim(PrimitiveType.Sphere, root, new Vector3(-0.5f, 2.0f, -0.6f), Vector3.one * 0.22f, new Color(1f, 0.15f, 0.12f), "CopRed", 3f).GetComponent<Renderer>();
            c.Blue = Prim(PrimitiveType.Sphere, root, new Vector3(0.5f, 2.0f, -0.6f), Vector3.one * 0.22f, new Color(0.2f, 0.4f, 1f), "CopBlue", 3f).GetComponent<Renderer>();
            return c;
        }

        GameObject Prim(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Color c, string key, float emission = 0f)
        {
            var go = GameObject.CreatePrimitive(type); Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false); go.transform.localPosition = pos; go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = Mats.Get(key, c, 0.3f, emission);
            return go;
        }

        void Box(string n, Vector3 local, Vector3 size, Color c, string key, bool collider, float emission = 0f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = n;
            if (!collider) Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(_jail, false); go.transform.localPosition = local; go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = Mats.Get(key, c, 0.15f, emission);
        }

        void Cyl(string n, Vector3 local, Vector3 size, Color c, string key)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); go.name = n; Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(_jail, false); go.transform.localPosition = local; go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = Mats.Get(key, c, 0.3f);
        }

        void Cleanup()
        {
            foreach (var g in _spawned) if (g != null) Destroy(g);
            _spawned.Clear(); _fleet.Clear(); _ride = null; _driver = null; _officer = null;
            if (_cam != null) Destroy(_cam.gameObject);
            if (_car != null) { _car.Hold = false; _car = null; }
        }
    }
}

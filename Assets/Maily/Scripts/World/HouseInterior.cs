using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace Maily
{
    /// <summary>
    /// Inside Maily's house, where the family brings her when they catch her. A dollhouse-style set (outer walls
    /// render from the inside only, interior walls are half height so the camera sees over them) with furniture,
    /// toys and a baby gate in the way. The parent guards the kitchen near the front door, the child wanders the
    /// living room; if they catch her she goes back to her bed. Reaching the front door ends the game: "ברחת מהבית".
    /// Built at runtime, far outside the kibbutz map.
    /// </summary>
    public class HouseInterior : MonoBehaviour
    {
        public static HouseInterior I { get; private set; }
        public static readonly Vector3 Origin = new Vector3(1650f, 0f, 1650f);

        public bool Active { get; private set; }

        Transform _root;
        Vector3 _bed, _doorPos;
        float _cooldown, _catchFlash;
        readonly List<Vector3> _parentRoute = new List<Vector3>(), _childRoute = new List<Vector3>();
        int _parentWp, _childWp;
        Transform _doorGlow;

        const float W = 9f, D = 6f, H = 2.8f, Half = 1.2f;

        void Awake() => I = this;

        Vector3 L(float x, float z) => Origin + new Vector3(x, 0f, z);

        // ------------------------------------------------------------------ build
        void Build()
        {
            _root = new GameObject("House Interior").transform;
            _root.position = Origin;

            // floor (with its own collider: this is far outside the kibbutz ground)
            Box("Floor", new Vector3(0, -0.25f, 0), new Vector3(W * 2 + 1, 0.5f, D * 2 + 1), Color.Lerp(Palette.Trunk, Palette.WallOchre, 0.55f), "IntFloor", true, ShadowCastingMode.Off);
            Box("Rug", new Vector3(-0.3f, 0.01f, 2.6f), new Vector3(3.6f, 0.02f, 2.4f), Color.Lerp(Palette.BullyRed, Palette.WallOchre, 0.4f), "IntRug", false, ShadowCastingMode.Off);
            Box("Kitchen tiles", new Vector3(6.5f, 0.012f, 0f), new Vector3(5f, 0.02f, 11.8f), Color.Lerp(Palette.WallWhite, Palette.Silver, 0.5f), "IntTiles", false, ShadowCastingMode.Off);

            // outer walls: colliders all round, visuals facing inward only (the camera can look in from outside)
            var walls = new MeshKit();
            void Outer(Vector3 a, Vector3 b, Vector3 inward)
            {
                Vector3 A = L(a.x, a.z) - Origin, B = L(b.x, b.z) - Origin, C = B + Vector3.up * H, Dd = A + Vector3.up * H;
                var n = Vector3.Cross(B - A, C - A);
                if (Vector3.Dot(n, inward) > 0) walls.Quad(A, B, C, Dd); else walls.Quad(B, A, Dd, C);
                var mid = (a + b) * 0.5f; var len = (b - a).magnitude;
                var col = new GameObject("Wall Collider").AddComponent<BoxCollider>();
                col.transform.SetParent(_root, false); col.transform.localPosition = new Vector3(mid.x, H * 0.5f, mid.z) - inward * 0.15f;
                bool alongX = Mathf.Abs(b.x - a.x) > Mathf.Abs(b.z - a.z);
                col.size = alongX ? new Vector3(len + 0.4f, H, 0.3f) : new Vector3(0.3f, H, len + 0.4f);
            }
            Outer(new Vector3(-W, 0, -D), new Vector3(W, 0, -D), Vector3.forward);
            Outer(new Vector3(W, 0, D), new Vector3(-W, 0, D), Vector3.back);
            Outer(new Vector3(-W, 0, D), new Vector3(-W, 0, -D), Vector3.right);
            Outer(new Vector3(W, 0, -D), new Vector3(W, 0, D), Vector3.left);
            var wgo = new GameObject("Outer Walls"); wgo.transform.SetParent(_root, false);
            wgo.AddComponent<MeshFilter>().sharedMesh = walls.Bake("InteriorWalls");
            var wmr = wgo.AddComponent<MeshRenderer>(); wmr.sharedMaterial = Mats.Get("IntWall", Color.Lerp(Palette.WallWhite, Palette.WallOchre, 0.35f), 0.1f);
            wmr.shadowCastingMode = ShadowCastingMode.Off;
            // a skirting board line so the walls read as a room
            foreach (var (p, s) in new[] { (new Vector3(0, 0.08f, -D + 0.04f), new Vector3(W * 2, 0.16f, 0.06f)), (new Vector3(0, 0.08f, D - 0.04f), new Vector3(W * 2, 0.16f, 0.06f)), (new Vector3(-W + 0.04f, 0.08f, 0), new Vector3(0.06f, 0.16f, D * 2)) })
                Box("Skirting", p, s, Palette.Trunk, "IntWood", false, ShadowCastingMode.Off);

            // half-height interior walls with doorways
            var wallC = Color.Lerp(Palette.WallWhite, Palette.WallOchre, 0.5f);
            Box("Wall bedroom A", new Vector3(-3f, Half * 0.5f, -2.5f), new Vector3(0.22f, Half, 7f), wallC, "IntHalfWall", true);
            Box("Wall bedroom B", new Vector3(-3f, Half * 0.5f, 4.4f), new Vector3(0.22f, Half, 3.2f), wallC, "IntHalfWall", true);
            Box("Wall kitchen A", new Vector3(4f, Half * 0.5f, -5.0f), new Vector3(0.22f, Half, 2.0f), wallC, "IntHalfWall", true);
            Box("Wall kitchen B", new Vector3(4f, Half * 0.5f, 1.6f), new Vector3(0.22f, Half, 8.8f), wallC, "IntHalfWall", true);
            Box("Wall hall", new Vector3(-1.2f, Half * 0.5f, -1.0f), new Vector3(3.6f, Half, 0.22f), wallC, "IntHalfWall", true);

            // bedroom: Maily's basket, the bed, wardrobe, toys
            _bed = L(-7f, -4f);
            Cyl("Dog basket", new Vector3(-7f, 0.08f, -4f), new Vector3(1.1f, 0.08f, 1.1f), Palette.BullyRed, "IntBasket");
            Cyl("Cushion", new Vector3(-7f, 0.14f, -4f), new Vector3(0.9f, 0.05f, 0.9f), Palette.WallOchre, "IntCushion");
            Box("Bed", new Vector3(-7.4f, 0.28f, 3.6f), new Vector3(2.2f, 0.56f, 3.2f), Palette.MiniSky, "IntBed", true);
            Box("Pillow", new Vector3(-7.4f, 0.62f, 4.8f), new Vector3(1.4f, 0.14f, 0.5f), Palette.WallWhite, "IntPillow", false);
            Box("Wardrobe", new Vector3(-4.2f, 1f, -5.4f), new Vector3(1.6f, 2f, 0.7f), Palette.Trunk, "IntWood", true);
            Box("Toy box", new Vector3(-5f, 0.25f, 0.4f), new Vector3(0.8f, 0.5f, 0.6f), Palette.Citrus, "IntToyBox", true);
            Box("Laundry basket", new Vector3(-8.2f, 0.25f, -0.6f), new Vector3(0.6f, 0.5f, 0.6f), Palette.WallWhite, "IntPillow", true);
            Toy(new Vector3(-6f, 0f, -1.6f), Palette.ParrotGreen); Toy(new Vector3(-4.2f, 0f, 2.2f), Palette.BullyRed);

            // living room: sofa, coffee table, armchair, TV, plants, scattered blocks
            Box("Sofa", new Vector3(-0.4f, 0.4f, 5.2f), new Vector3(2.8f, 0.8f, 1f), Color.Lerp(Palette.MiniSky, Palette.UiInk, 0.35f), "IntSofa", true);
            Box("Sofa back", new Vector3(-0.4f, 0.75f, 5.65f), new Vector3(2.8f, 0.7f, 0.25f), Color.Lerp(Palette.MiniSky, Palette.UiInk, 0.45f), "IntSofaBack", false);
            Box("Coffee table", new Vector3(-0.4f, 0.22f, 2.9f), new Vector3(1.4f, 0.44f, 0.8f), Palette.Trunk, "IntWood", true);
            Box("Armchair", new Vector3(2.6f, 0.4f, 3.2f), new Vector3(1f, 0.8f, 1f), Color.Lerp(Palette.Citrus, Palette.Trunk, 0.4f), "IntArmchair", true);
            Box("TV stand", new Vector3(0.6f, 0.3f, -5.5f), new Vector3(2.2f, 0.6f, 0.6f), Palette.Trunk, "IntWood", true);
            Box("TV", new Vector3(0.6f, 0.95f, -5.6f), new Vector3(1.6f, 0.7f, 0.08f), Palette.UiInk, "IntTV", false);
            Plant(new Vector3(3.4f, 0f, 5.3f)); Plant(new Vector3(-2.5f, 0f, -5.3f));
            for (int i = 0; i < 6; i++) Toy(new Vector3(-2f + i * 0.9f, 0f, 0.6f + (i % 2) * 0.8f), i % 2 == 0 ? Palette.MiniCards : Palette.CoinGold);
            // baby gate across the kitchen doorway (low: jump it!)
            Box("Baby gate", new Vector3(4f, 0.3f, -3.0f), new Vector3(0.12f, 0.6f, 1.4f), Palette.WallWhite, "IntGate", true);
            for (int i = 0; i < 5; i++) Box("Gate bar", new Vector3(4f, 0.3f, -3.6f + i * 0.3f), new Vector3(0.05f, 0.62f, 0.05f), Palette.Silver, "IntGateBar", false);

            // kitchen: counters, fridge, table and chairs, Maily's bowl
            Box("Counter", new Vector3(6.8f, 0.47f, 5.5f), new Vector3(4.2f, 0.94f, 0.8f), Palette.WallWhite, "IntCounter", true);
            Box("Counter top", new Vector3(6.8f, 0.96f, 5.5f), new Vector3(4.3f, 0.05f, 0.85f), Palette.UiInk, "IntCounterTop", false);
            Box("Fridge", new Vector3(8.4f, 0.9f, -5.4f), new Vector3(0.9f, 1.8f, 0.8f), Palette.Silver, "IntFridge", true);
            Box("Table", new Vector3(6.4f, 0.37f, 0.4f), new Vector3(1.6f, 0.74f, 1f), Palette.Trunk, "IntWood", true);
            foreach (var c in new[] { new Vector3(5.3f, 0, 0.4f), new Vector3(7.5f, 0, 0.4f), new Vector3(6.4f, 0, -0.6f) })
                Box("Chair", c + Vector3.up * 0.23f, new Vector3(0.45f, 0.46f, 0.45f), Palette.Trunk, "IntWood", true);
            Cyl("Bowl", new Vector3(5f, 0.05f, -5.2f), new Vector3(0.35f, 0.05f, 0.35f), Palette.BullyRed, "IntBasket");

            // the front door in the kitchen wall: glowing frame + sign
            _doorPos = L(8.75f, 3.2f);
            Box("Front door", new Vector3(8.92f, 1.05f, 3.2f), new Vector3(0.08f, 2.1f, 1.1f), Color.Lerp(Palette.Trunk, Palette.BullyRed, 0.3f), "IntDoor", false);
            var glow = Box("Door glow", new Vector3(8.86f, 1.12f, 3.2f), new Vector3(0.04f, 2.3f, 1.35f), Palette.CoinGold, "IntDoorGlow", false, ShadowCastingMode.Off, 1.6f);
            _doorGlow = glow.transform;
            Cyl("Door mat", new Vector3(8.2f, 0.015f, 3.2f), new Vector3(1.2f, 0.01f, 1.2f), Palette.CoinGold, "IntDoorGlow", 1.2f);
            var sign = new GameObject("Exit sign"); sign.transform.SetParent(_root, false);
            sign.transform.localPosition = new Vector3(8.8f, 2.45f, 3.2f); sign.transform.localRotation = Quaternion.Euler(0, 90, 0);
            var t = sign.AddComponent<TextMeshPro>(); t.fontSize = 4f; t.alignment = TextAlignmentOptions.Center; t.color = Palette.CoinGold;
            t.rectTransform.sizeDelta = new Vector2(3f, 0.8f); Heb.Set(t, "יציאה");

            // a warm room light so the house doesn't depend on the sun
            var lgo = new GameObject("Room light"); lgo.transform.SetParent(_root, false); lgo.transform.localPosition = new Vector3(0, 2.6f, 0);
            var light = lgo.AddComponent<Light>(); light.type = LightType.Point; light.range = 16f; light.intensity = 2.2f; light.color = new Color(1f, 0.9f, 0.75f);

            // patrol routes: the parent guards the kitchen and the door, the child roams the living room
            _parentRoute.AddRange(new[] { L(5.2f, 2.4f), L(6.8f, -0.9f), L(7.4f, -3.4f), L(5.2f, -2.2f) }); // near, not on, the door
            _childRoute.AddRange(new[] { L(-1.8f, 2.0f), L(2.6f, 1.2f), L(2.4f, -3.4f), L(-1.6f, -3.6f) });
        }

        GameObject Box(string n, Vector3 local, Vector3 size, Color c, string key, bool collider, ShadowCastingMode shadows = ShadowCastingMode.On, float emission = 0f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = n;
            if (!collider) Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(_root, false); go.transform.localPosition = local; go.transform.localScale = size;
            var mr = go.GetComponent<MeshRenderer>(); mr.sharedMaterial = Mats.Get(key, c, 0.15f, emission); mr.shadowCastingMode = shadows;
            return go;
        }

        void Cyl(string n, Vector3 local, Vector3 size, Color c, string key, float emission = 0f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); go.name = n;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(_root, false); go.transform.localPosition = local; go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = Mats.Get(key, c, 0.15f, emission);
        }

        void Toy(Vector3 local, Color c) => Box("Toy block", local + Vector3.up * 0.15f, new Vector3(0.3f, 0.3f, 0.3f), c, "IntToy" + ColorUtility.ToHtmlStringRGB(c), true);

        void Plant(Vector3 local)
        {
            Box("Pot", local + Vector3.up * 0.2f, new Vector3(0.45f, 0.4f, 0.45f), Palette.RoofTile, "IntPot", true);
            var leaves = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(leaves.GetComponent<Collider>());
            leaves.transform.SetParent(_root, false); leaves.transform.localPosition = local + Vector3.up * 0.8f; leaves.transform.localScale = new Vector3(0.8f, 0.9f, 0.8f);
            leaves.GetComponent<MeshRenderer>().sharedMaterial = Mats.Get("IntLeaves", Palette.Foliage, 0.1f);
        }

        // ------------------------------------------------------------------ play
        public void Enter()
        {
            if (_root == null) Build();
            Active = true; _cooldown = 3f;
            var fam = Family.I;
            if (fam != null)
            {
                fam.Halt();
                if (fam.Members.Count > 0) fam.Members[0].Teleport(_parentRoute[0], _bed);
                if (fam.Members.Count > 1) fam.Members[1].Teleport(_childRoute[0], _bed);
            }
            MailyController.I.Place(_bed + Vector3.up * 0.15f, 90f);
            Captions.I?.Show("המשפחה סגרה את מיילי בבית! מצאי את הדלת הקדמית וצאי החוצה", 4.5f);
        }

        void Update()
        {
            if (!Active) return;
            var gm = GameManager.I; var m = MailyController.I;
            if (gm == null || m == null || gm.State == GameState.Ended) return;
            float dt = Time.deltaTime; if (dt <= 0f) return;
            Captions.I?.Pill("צאי מהבית דרך הדלת הקדמית", Palette.MiniSky);
            if (_doorGlow != null) _doorGlow.localScale = new Vector3(0.04f, 2.3f, 1.35f) * (1f + 0.04f * Mathf.Sin(Time.time * 5f));
            if (gm.State != GameState.Playing) return;
            _cooldown -= dt;

            var mp = m.transform.position;
            var fam = Family.I;
            if (fam != null)
                for (int i = 0; i < fam.Members.Count; i++)
                {
                    var f = fam.Members[i];
                    var route = i == 0 ? _parentRoute : _childRoute;
                    ref int wp = ref (i == 0 ? ref _parentWp : ref _childWp);
                    float d = Flat(f.transform.position, mp);
                    if (_cooldown <= 0f && d < 4.5f)
                    {
                        f.MoveToward(mp, i == 0 ? 3.1f : 2.8f, dt);   // slower than Maily's walk: she can always slip past
                        if (d < 0.95f) { CaughtInside(); return; }
                    }
                    else
                    {
                        f.MoveToward(route[wp], 1.4f, dt, 0.3f);
                        if (Flat(f.transform.position, route[wp]) < 0.5f) wp = (wp + 1) % route.Count;
                    }
                }

            if (Flat(mp, _doorPos) < 1.0f) Escape();
        }

        void CaughtInside()
        {
            Sfx.I?.Play2D(Sfx.I.Heart, 0.9f, 0.85f);
            MailyController.I.Place(_bed + Vector3.up * 0.15f, 90f);
            Captions.I?.Show("\"לאן את הולכת? חזרה למיטה, מיילי!\"", 3f);
            _cooldown = 3f;
        }

        void Escape()
        {
            Active = false;
            Fx.Sparkle(MailyController.I.transform.position + Vector3.up * 0.6f, Palette.CoinGold);
            GameManager.I.EndGame("runaway", "ברחת מהבית",
                "מיילי דחפה את הדלת עם האף ויצאה החוצה. אף אחד לא שם לב, והקיבוץ כולו מחכה לה.");
        }

        static float Flat(Vector3 a, Vector3 b) { a.y = 0; b.y = 0; return Vector3.Distance(a, b); }
    }
}

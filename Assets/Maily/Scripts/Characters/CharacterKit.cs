using UnityEngine;

namespace Maily
{
    /// <summary>Primitive-built characters and pickups (silhouette first, two-tone minimum, tapered & tilted).</summary>
    public static class CharacterKit
    {
        public struct DogLook
        {
            public string Name; public Color Primary, Secondary; public float Size; public bool PointyEars; public bool CurlyTail;
            public DogLook(string n, Color p, Color s, float size, bool pointy, bool curly) { Name = n; Primary = p; Secondary = s; Size = size; PointyEars = pointy; CurlyTail = curly; }
        }

        public static readonly DogLook[] Dogs =
        {
            new DogLook("Shoko",  Palette.DogBrown,  Palette.DogCream, 1.15f, false, false),
            new DogLook("Bamba",  Palette.DogGinger, Palette.DogCream, 0.8f,  true,  true),
            new DogLook("Luna",   Palette.DogBlack,  Palette.DogWhite, 1.0f,  false, false),
            new DogLook("Kushi",  Palette.DogBlack,  Palette.DogGray,  0.95f, true,  false),
            new DogLook("Pita",   Palette.DogCream,  Palette.DogWhite, 0.7f,  false, true),
            new DogLook("Rex",    Palette.DogGray,   Palette.DogCream, 1.25f, true,  false),
            new DogLook("Mitzi",  Palette.DogWhite,  Palette.DogGinger,0.6f,  true,  true),
            new DogLook("Lucky",  Palette.DogGinger, Palette.DogWhite, 1.05f, false, false),
        };

        static GameObject Part(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Color c, string key, Vector3 euler = default, float emission = 0f)
        {
            var go = GameObject.CreatePrimitive(t);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localScale = scale; go.transform.localEulerAngles = euler;
            go.GetComponent<MeshRenderer>().sharedMaterial = Mats.Get(key, c, 0.15f, emission);
            return go;
        }

        static Transform Pivot(string name, Transform parent, Vector3 pos, Vector3 euler = default)
        {
            var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = pos; t.localEulerAngles = euler; return t;
        }

        static string Key(Color c) => "C_" + ColorUtility.ToHtmlStringRGB(c);

        /// <summary>Builds a dog under root (forward = +Z). Fills the NpcDog rig references.</summary>
        public static void BuildDog(NpcDog dog, DogLook look)
        {
            var root = dog.transform; float s = look.Size;
            string P = Key(look.Primary), S = Key(look.Secondary), D = Key(Palette.DogBlack);
            var body = Pivot("Body", root, new Vector3(0, 0.5f * s, 0));
            Part(PrimitiveType.Sphere, body, Vector3.zero, new Vector3(0.42f, 0.4f, 0.78f) * s, look.Primary, P);
            Part(PrimitiveType.Sphere, body, new Vector3(0, -0.06f, 0.22f) * s, new Vector3(0.32f, 0.3f, 0.34f) * s, look.Secondary, S); // chest

            var head = Pivot("Head", body, new Vector3(0, 0.26f, 0.38f) * s, new Vector3(-6, 0, 0));
            Part(PrimitiveType.Sphere, head, Vector3.zero, new Vector3(0.34f, 0.32f, 0.34f) * s, look.Primary, P);
            Part(PrimitiveType.Sphere, head, new Vector3(0, -0.06f, 0.17f) * s, new Vector3(0.17f, 0.14f, 0.24f) * s, look.Secondary, S, new Vector3(4, 0, 0)); // snout
            Part(PrimitiveType.Cube, head, new Vector3(0, -0.02f, 0.29f) * s, Vector3.one * 0.06f * s, Palette.DogBlack, D, new Vector3(0, 45, 0)); // nose
            Part(PrimitiveType.Sphere, head, new Vector3(-0.08f, 0.05f, 0.14f) * s, Vector3.one * 0.05f * s, Palette.DogBlack, D);
            Part(PrimitiveType.Sphere, head, new Vector3(0.08f, 0.05f, 0.14f) * s, Vector3.one * 0.05f * s, Palette.DogBlack, D);
            for (int side = -1; side <= 1; side += 2)
            {
                var ear = Pivot(side < 0 ? "EarL" : "EarR", head, new Vector3(0.11f * side, 0.13f, -0.02f) * s);
                if (look.PointyEars)
                    Part(PrimitiveType.Cube, ear, new Vector3(0.01f * side, 0.09f, 0) * s, new Vector3(0.09f, 0.18f, 0.04f) * s, look.Primary, P, new Vector3(0, 0, -12 * side));
                else
                    Part(PrimitiveType.Sphere, ear, new Vector3(0.05f * side, -0.07f, 0) * s, new Vector3(0.07f, 0.2f, 0.13f) * s, look.Secondary == Palette.DogWhite ? look.Primary : look.Secondary, look.Secondary == Palette.DogWhite ? P : S, new Vector3(0, 0, 18 * side));
                if (side < 0) dog.EarL = ear; else dog.EarR = ear;
            }

            dog.Legs = new Transform[4];
            int i = 0;
            for (int fz = 1; fz >= -1; fz -= 2)
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    var hip = Pivot("Leg", root, new Vector3(0.13f * sx, 0.38f * s, 0.22f * fz * s), new Vector3(0, 0, 3 * sx));
                    Part(PrimitiveType.Capsule, hip, new Vector3(0, -0.17f * s, 0), new Vector3(0.11f, 0.19f, 0.11f) * s, look.Primary, P);
                    Part(PrimitiveType.Sphere, hip, new Vector3(0, -0.35f * s, 0.02f * s), Vector3.one * 0.09f * s, look.Secondary, S); // paw (0.8× taper)
                    dog.Legs[i++] = hip;
                }

            var tail = Pivot("Tail", body, new Vector3(0, 0.12f, -0.36f) * s, new Vector3(look.CurlyTail ? -70 : -40, 0, 0));
            Part(PrimitiveType.Capsule, tail, new Vector3(0, 0.16f, 0) * s, new Vector3(0.07f, 0.17f, 0.07f) * s, look.Primary, P);
            Part(PrimitiveType.Sphere, tail, new Vector3(0, 0.32f, 0) * s, Vector3.one * 0.08f * s, look.Secondary, S);
            dog.Body = body; dog.Head = head; dog.Tail = tail; dog.DogName = look.Name; dog.Size = s;
        }

        public static void BuildParrot(Parrot p)
        {
            var root = p.transform;
            string G = Key(Palette.ParrotGreen), Dk = Key(Palette.ParrotDeep), R = Key(Palette.ParrotBeak), Bk = Key(Palette.DogBlack);
            var body = Pivot("Body", root, new Vector3(0, 0.2f, 0), new Vector3(-25, 0, 0));
            Part(PrimitiveType.Sphere, body, Vector3.zero, new Vector3(0.17f, 0.17f, 0.3f), Palette.ParrotGreen, G, default, 0.15f);
            var head = Pivot("Head", body, new Vector3(0, 0.07f, 0.14f), new Vector3(25, 0, 0));
            Part(PrimitiveType.Sphere, head, Vector3.zero, Vector3.one * 0.14f, Palette.ParrotGreen, G, default, 0.15f);
            Part(PrimitiveType.Cube, head, new Vector3(0, -0.02f, 0.08f), new Vector3(0.05f, 0.06f, 0.07f), Palette.ParrotBeak, R, new Vector3(30, 0, 0));
            Part(PrimitiveType.Cylinder, head, new Vector3(0, -0.035f, 0.0f), new Vector3(0.15f, 0.005f, 0.15f), Palette.DogBlack, Bk, new Vector3(-20, 0, 0)); // neck ring
            Part(PrimitiveType.Sphere, head, new Vector3(-0.055f, 0.02f, 0.04f), Vector3.one * 0.025f, Palette.DogBlack, Bk);
            Part(PrimitiveType.Sphere, head, new Vector3(0.055f, 0.02f, 0.04f), Vector3.one * 0.025f, Palette.DogBlack, Bk);
            var tail = Pivot("Tail", body, new Vector3(0, -0.02f, -0.14f), new Vector3(10, 0, 0));
            Part(PrimitiveType.Cube, tail, new Vector3(0, 0, -0.2f), new Vector3(0.05f, 0.015f, 0.42f), Palette.ParrotDeep, Dk);
            for (int side = -1; side <= 1; side += 2)
            {
                var w = Pivot(side < 0 ? "WingL" : "WingR", body, new Vector3(0.07f * side, 0.04f, 0.0f));
                Part(PrimitiveType.Cube, w, new Vector3(0.1f * side, 0, -0.03f), new Vector3(0.22f, 0.02f, 0.16f), Palette.ParrotDeep, Dk);
                if (side < 0) p.WingL = w; else p.WingR = w;
            }
            p.Body = body; p.Head = head;
        }

        public static void BuildTreat(Collectible c, bool coin)
        {
            var spin = Pivot("Spin", c.transform, new Vector3(0, 0.55f, 0));
            if (coin)
            {
                Part(PrimitiveType.Cylinder, spin, Vector3.zero, new Vector3(0.5f, 0.03f, 0.5f), Palette.CoinGold, "Coin", new Vector3(90, 0, 0), 1.6f);
                Part(PrimitiveType.Cylinder, spin, Vector3.zero, new Vector3(0.36f, 0.035f, 0.36f), Color.Lerp(Palette.CoinGold, Palette.RoofTile, 0.3f), "CoinInner", new Vector3(90, 0, 0), 1.2f);
            }
            else
            {
                Part(PrimitiveType.Capsule, spin, Vector3.zero, new Vector3(0.12f, 0.24f, 0.12f), Palette.Bone, "Bone", new Vector3(0, 0, 90), 0.35f);
                foreach (var x in new[] { -0.24f, 0.24f })
                    foreach (var y in new[] { -0.06f, 0.06f })
                        Part(PrimitiveType.Sphere, spin, new Vector3(x, y, 0), Vector3.one * 0.13f, Palette.Bone, "Bone", default, 0.35f);
            }
            c.Spin = spin; c.IsCoin = coin;
        }

        /// <summary>Maily's doghouse + water bowl beside her home.</summary>
        public static GameObject BuildDoghouse(Vector3 pos, float yaw)
        {
            var root = new GameObject("Maily's Doghouse").transform;
            root.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            string W = Key(Palette.WallOchre), R = Key(Palette.RoofTile), D = Key(Palette.UiInk), B = Key(Palette.PoolWater);
            Part(PrimitiveType.Cube, root, new Vector3(0, 0.45f, 0), new Vector3(1.1f, 0.9f, 1.3f), Palette.WallOchre, W);
            Part(PrimitiveType.Cube, root, new Vector3(-0.3f, 1.05f, 0), new Vector3(0.75f, 0.06f, 1.45f), Palette.RoofTile, R, new Vector3(0, 0, 38));
            Part(PrimitiveType.Cube, root, new Vector3(0.3f, 1.05f, 0), new Vector3(0.75f, 0.06f, 1.45f), Palette.RoofTile, R, new Vector3(0, 0, -38));
            Part(PrimitiveType.Cube, root, new Vector3(0, 0.33f, 0.651f), new Vector3(0.45f, 0.55f, 0.02f), Palette.UiInk, D);
            Part(PrimitiveType.Cylinder, root, new Vector3(0.9f, 0.05f, 0.6f), new Vector3(0.35f, 0.05f, 0.35f), Palette.RoofTile, R);
            Part(PrimitiveType.Cylinder, root, new Vector3(0.9f, 0.08f, 0.6f), new Vector3(0.28f, 0.02f, 0.28f), Palette.PoolWater, B);
            var col = root.gameObject.AddComponent<BoxCollider>(); col.center = new Vector3(0, 0.55f, 0); col.size = new Vector3(1.1f, 1.1f, 1.3f);
            return root.gameObject;
        }
    }
}

using System.Collections.Generic;
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
            new DogLook("שוקו",  Palette.DogBrown,  Palette.DogCream, 1.15f, false, false), // Shoko
            new DogLook("במבה",  Palette.DogGinger, Palette.DogCream, 0.8f,  true,  true),  // Bamba
            new DogLook("לונה",  Palette.DogBlack,  Palette.DogWhite, 1.0f,  false, false), // Luna
            new DogLook("טופי",  Palette.DogBlack,  Palette.DogGray,  0.95f, true,  false), // Toffee
            new DogLook("פיתה",  Palette.DogCream,  Palette.DogWhite, 0.7f,  false, true),  // Pita
            new DogLook("רקס",   Palette.DogGray,   Palette.DogCream, 1.25f, true,  false), // Rex
            new DogLook("מיצי",  Palette.DogWhite,  Palette.DogGinger,0.6f,  true,  true),  // Mitzi
            new DogLook("לאקי",  Palette.DogGinger, Palette.DogWhite, 1.05f, false, false), // Lucky
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

        /// <summary>Rig references of a primitive dog. Legs order: FL, FR, HL, HR.</summary>
        public struct DogParts { public Transform Body, Head, Tail, EarL, EarR; public Transform[] Legs; }

        /// <summary>Builds a friendly kibbutz dog. Fills the NpcDog rig references.</summary>
        public static void BuildDog(NpcDog dog, DogLook look)
        {
            var p = BuildDogParts(dog.transform, look);
            dog.Body = p.Body; dog.Head = p.Head; dog.Tail = p.Tail; dog.EarL = p.EarL; dog.EarR = p.EarR; dog.Legs = p.Legs;
            dog.DogName = look.Name; dog.Size = look.Size;
        }

        /// <summary>Builds a dog body under root (forward = +Z).</summary>
        public static DogParts BuildDogParts(Transform root, DogLook look)
        {
            var parts = new DogParts(); float s = look.Size;
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
                if (side < 0) parts.EarL = ear; else parts.EarR = ear;
            }

            parts.Legs = new Transform[4];
            int i = 0;
            for (int fz = 1; fz >= -1; fz -= 2)
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    var hip = Pivot("Leg", root, new Vector3(0.13f * sx, 0.38f * s, 0.22f * fz * s), new Vector3(0, 0, 3 * sx));
                    Part(PrimitiveType.Capsule, hip, new Vector3(0, -0.17f * s, 0), new Vector3(0.11f, 0.19f, 0.11f) * s, look.Primary, P);
                    Part(PrimitiveType.Sphere, hip, new Vector3(0, -0.35f * s, 0.02f * s), Vector3.one * 0.09f * s, look.Secondary, S); // paw (0.8× taper)
                    parts.Legs[i++] = hip;
                }

            var tail = Pivot("Tail", body, new Vector3(0, 0.12f, -0.36f) * s, new Vector3(look.CurlyTail ? -70 : -40, 0, 0));
            Part(PrimitiveType.Capsule, tail, new Vector3(0, 0.16f, 0) * s, new Vector3(0.07f, 0.17f, 0.07f) * s, look.Primary, P);
            Part(PrimitiveType.Sphere, tail, new Vector3(0, 0.32f, 0) * s, Vector3.one * 0.08f * s, look.Secondary, S);
            parts.Body = body; parts.Head = head; parts.Tail = tail;
            return parts;
        }

        // ---- street dogs ----
        public static readonly DogLook[] Bullies =
        {
            new DogLook("בולדוזר", Palette.BullyCoat1, Palette.BullyMuzzle, 1.1f,  false, false), // Bulldozer: big, slow, hits hard
            new DogLook("צלקת",    Palette.BullyCoat2, Palette.BullyMuzzle, 0.92f, true,  false), // Scar: fast and sneaky
            new DogLook("רעם",     Palette.BullyCoat3, Palette.BullyCoat2,  1.25f, true,  false), // Thunder: the boss
        };

        /// <summary>Builds a street dog: scruffy coat, glowing red eyes, studded collar, hackles.</summary>
        public static void BuildBully(HostileDog dog, DogLook look)
        {
            var p = BuildDogParts(dog.transform, look);
            float s = look.Size;
            string R = Key(Palette.BullyRed), Sp = Key(Palette.Spike), Bk = Key(Palette.DogBlack), P = Key(look.Primary);
            // angry red eyes (glow — the danger accent) under heavy brows
            Part(PrimitiveType.Sphere, p.Head, new Vector3(-0.08f, 0.05f, 0.155f) * s, Vector3.one * 0.055f * s, Palette.BullyRed, R, default, 2.5f);
            Part(PrimitiveType.Sphere, p.Head, new Vector3(0.08f, 0.05f, 0.155f) * s, Vector3.one * 0.055f * s, Palette.BullyRed, R, default, 2.5f);
            Part(PrimitiveType.Cube, p.Head, new Vector3(-0.08f, 0.095f, 0.15f) * s, new Vector3(0.1f, 0.025f, 0.04f) * s, look.Primary, P, new Vector3(0, 0, -20));
            Part(PrimitiveType.Cube, p.Head, new Vector3(0.08f, 0.095f, 0.15f) * s, new Vector3(0.1f, 0.025f, 0.04f) * s, look.Primary, P, new Vector3(0, 0, 20));
            // bared teeth
            Part(PrimitiveType.Cube, p.Head, new Vector3(-0.035f, -0.11f, 0.24f) * s, new Vector3(0.025f, 0.04f, 0.025f) * s, Palette.Bone, "Bone");
            Part(PrimitiveType.Cube, p.Head, new Vector3(0.035f, -0.11f, 0.24f) * s, new Vector3(0.025f, 0.04f, 0.025f) * s, Palette.Bone, "Bone");
            // studded collar around the neck
            var collarC = new Vector3(0, 0.2f, 0.3f) * s; var collarQ = Quaternion.Euler(-55, 0, 0);
            Part(PrimitiveType.Cylinder, p.Body, collarC, new Vector3(0.3f, 0.025f, 0.3f) * s, Palette.DogBlack, Bk, collarQ.eulerAngles);
            for (int k = 0; k < 7; k++)
            {
                // spikes sit on the body (uniform scale) around the collar ring, pointing outward
                float a = k / 7f * Mathf.PI * 2f;
                var dir = collarQ * new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Part(PrimitiveType.Cube, p.Body, collarC + dir * 0.155f * s, new Vector3(0.022f, 0.022f, 0.05f) * s, Palette.Spike, Sp, Quaternion.LookRotation(dir).eulerAngles);
            }
            // raised hackles along the back
            for (int k = 0; k < 4; k++)
                Part(PrimitiveType.Cube, p.Body, new Vector3(0, 0.2f, 0.12f - k * 0.1f) * s, new Vector3(0.05f, 0.08f, 0.05f) * s, look.Primary, P, new Vector3(20, 45, 0));
            dog.Body = p.Body; dog.Head = p.Head; dog.Tail = p.Tail; dog.Legs = p.Legs;
            dog.DogName = look.Name; dog.Size = s;
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

        /// <summary>Mini-game start marker: glowing ring, white post, floating icon for the game.</summary>
        public static void BuildSpot(MiniGameSpot spot)
        {
            var root = spot.transform; var c = spot.Accent; string A = "Spot_" + spot.Game, W = Key(Palette.WallWhite), I0 = Key(Palette.UiInk);
            var ring = Part(PrimitiveType.Cylinder, root, new Vector3(0, 0.03f, 0), new Vector3(2.6f, 0.012f, 2.6f), c, A, default, 1.4f);
            Part(PrimitiveType.Cylinder, root, new Vector3(0, 0.025f, 0), new Vector3(2.2f, 0.014f, 2.2f), Palette.UiInk, Key(Palette.UiInk));
            Part(PrimitiveType.Cylinder, root, new Vector3(0, 0.75f, 0), new Vector3(0.1f, 0.75f, 0.1f), Palette.WallWhite, W);
            var icon = Pivot("Icon", root, new Vector3(0, 2.1f, 0));
            Part(PrimitiveType.Sphere, icon, Vector3.zero, Vector3.one * 0.7f, c, A, default, 1.4f); // glowing orb behind the symbol
            switch (spot.Game)
            {
                case MiniGameSpot.Kind.Gates:
                    Part(PrimitiveType.Cube, icon, new Vector3(-0.35f, 0, 0), new Vector3(0.08f, 0.7f, 0.08f), Palette.WallWhite, W);
                    Part(PrimitiveType.Cube, icon, new Vector3(0.35f, 0, 0), new Vector3(0.08f, 0.7f, 0.08f), Palette.WallWhite, W);
                    Part(PrimitiveType.Cube, icon, new Vector3(0, 0.35f, 0), new Vector3(0.78f, 0.08f, 0.08f), Palette.WallWhite, W);
                    break;
                case MiniGameSpot.Kind.Frisbee:
                    Part(PrimitiveType.Cylinder, icon, Vector3.zero, new Vector3(0.9f, 0.03f, 0.9f), Palette.WallWhite, W, new Vector3(70, 0, 0));
                    break;
                case MiniGameSpot.Kind.Oranges:
                    Part(PrimitiveType.Sphere, icon, new Vector3(0, 0, 0.3f), Vector3.one * 0.42f, Palette.Citrus, "OrangeIcon", default, 0.6f);
                    Part(PrimitiveType.Cube, icon, new Vector3(0.05f, 0.22f, 0.3f), new Vector3(0.14f, 0.04f, 0.08f), Palette.Foliage, Key(Palette.Foliage));
                    break;
                case MiniGameSpot.Kind.Maze:      // a little chomping yellow-gold head
                    Part(PrimitiveType.Sphere, icon, new Vector3(0, 0, 0.3f), Vector3.one * 0.5f, Palette.CoinGold, Key(Palette.CoinGold), default, 0.5f);
                    Part(PrimitiveType.Cube, icon, new Vector3(0.18f, 0, 0.36f), new Vector3(0.3f, 0.16f, 0.5f), Palette.UiInk, I0, new Vector3(0, 0, 0));
                    break;
                case MiniGameSpot.Kind.Solitaire: // a fan of cards
                    for (int i = 0; i < 3; i++)
                        Part(PrimitiveType.Cube, icon, new Vector3((i - 1) * 0.16f, 0, 0.3f + i * 0.01f), new Vector3(0.32f, 0.46f, 0.02f), i == 1 ? Palette.BullyRed : Palette.WallWhite, i == 1 ? Key(Palette.BullyRed) : W, new Vector3(0, 0, (1 - i) * 18f));
                    break;
                case MiniGameSpot.Kind.Memory:    // two matching tiles
                    Part(PrimitiveType.Cube, icon, new Vector3(-0.2f, 0, 0.3f), new Vector3(0.34f, 0.34f, 0.05f), Palette.ParrotGreen, Key(Palette.ParrotGreen));
                    Part(PrimitiveType.Cube, icon, new Vector3(0.2f, 0, 0.3f), new Vector3(0.34f, 0.34f, 0.05f), Palette.ParrotGreen, Key(Palette.ParrotGreen));
                    break;
                case MiniGameSpot.Kind.Chickens:  // a hen
                    Part(PrimitiveType.Sphere, icon, new Vector3(0, -0.05f, 0.3f), new Vector3(0.42f, 0.34f, 0.5f), Palette.WallWhite, W);
                    Part(PrimitiveType.Sphere, icon, new Vector3(0, 0.2f, 0.52f), Vector3.one * 0.2f, Palette.WallWhite, W);
                    Part(PrimitiveType.Cube, icon, new Vector3(0, 0.33f, 0.52f), new Vector3(0.04f, 0.1f, 0.12f), Palette.BullyRed, Key(Palette.BullyRed));
                    break;
                case MiniGameSpot.Kind.Papers:    // a rolled newspaper
                    Part(PrimitiveType.Cylinder, icon, new Vector3(0, 0, 0.3f), new Vector3(0.2f, 0.32f, 0.2f), Palette.WallWhite, W, new Vector3(0, 0, 90));
                    Part(PrimitiveType.Cylinder, icon, new Vector3(0, 0, 0.3f), new Vector3(0.21f, 0.04f, 0.21f), Palette.BullyRed, Key(Palette.BullyRed), new Vector3(0, 0, 90));
                    break;
                default:
                    Part(PrimitiveType.Capsule, icon, new Vector3(0, 0, 0.3f), new Vector3(0.12f, 0.3f, 0.12f), Palette.Bone, "Bone", new Vector3(0, 0, 90), 0.35f);
                    foreach (var x in new[] { -0.3f, 0.3f }) foreach (var y in new[] { -0.07f, 0.07f })
                        Part(PrimitiveType.Sphere, icon, new Vector3(x, y, 0.3f), Vector3.one * 0.14f, Palette.Bone, "Bone", default, 0.35f);
                    break;
            }
            spot.Icon = icon; spot.Ring = ring.GetComponent<Renderer>();
        }

        /// <summary>A member of Maily's family: simple figure with pivots at hips and shoulders for the run cycle.</summary>
        public static void BuildPerson(FamilyMember m, float height, Color shirt, Color pants, Color hair, bool longHair)
        {
            var root = m.transform; float k = height / 1.75f;
            var skin = new Color(0.87f, 0.68f, 0.54f);
            float leg = 0.82f * k, torso = 0.58f * k, arm = 0.62f * k;
            foreach (var side in new[] { -1f, 1f })
            {
                var hip = Pivot(side < 0 ? "LegL" : "LegR", root, new Vector3(0.1f * k * side, leg, 0));
                Part(PrimitiveType.Capsule, hip, new Vector3(0, -leg * 0.5f, 0), new Vector3(0.15f * k, leg * 0.5f, 0.15f * k), pants, Key(pants));
                Part(PrimitiveType.Cube, hip, new Vector3(0, -leg + 0.04f, 0.05f * k), new Vector3(0.13f * k, 0.08f, 0.26f * k), Palette.UiInk, Key(Palette.UiInk));
                if (side < 0) m.LegL = hip; else m.LegR = hip;
            }
            var body = Pivot("Body", root, Vector3.zero); m.Body = body;
            Part(PrimitiveType.Capsule, body, new Vector3(0, leg + torso * 0.5f, 0), new Vector3(0.38f * k, torso * 0.5f + 0.06f, 0.24f * k), shirt, Key(shirt));
            float headY = leg + torso + 0.14f * k;
            Part(PrimitiveType.Sphere, body, new Vector3(0, headY, 0), Vector3.one * 0.25f * k, skin, Key(skin));
            Part(PrimitiveType.Sphere, body, new Vector3(0, headY + 0.05f * k, -0.03f * k), new Vector3(0.27f, longHair ? 0.24f : 0.18f, 0.27f) * k, hair, Key(hair));
            if (longHair) Part(PrimitiveType.Capsule, body, new Vector3(0, headY - 0.1f * k, -0.1f * k), new Vector3(0.22f, 0.16f, 0.1f) * k, hair, Key(hair));
            foreach (var x in new[] { -0.05f, 0.05f }) Part(PrimitiveType.Sphere, body, new Vector3(x * k, headY + 0.02f * k, 0.115f * k), Vector3.one * 0.035f * k, Palette.UiInk, Key(Palette.UiInk));
            foreach (var side in new[] { -1f, 1f })
            {
                var sh = Pivot(side < 0 ? "ArmL" : "ArmR", body, new Vector3(0.24f * k * side, leg + torso - 0.04f * k, 0));
                Part(PrimitiveType.Capsule, sh, new Vector3(0, -arm * 0.45f, 0), new Vector3(0.11f * k, arm * 0.45f, 0.11f * k), shirt, Key(shirt));
                Part(PrimitiveType.Sphere, sh, new Vector3(0, -arm * 0.92f, 0), Vector3.one * 0.1f * k, skin, Key(skin));
                if (side < 0) m.ArmL = sh; else m.ArmR = sh;
            }
        }

        /// <summary>A kibbutz golf cart (קלנועית) with an old-timer at the wheel in a kova tembel.</summary>
        public static void BuildCart(TrafficVehicle v, Color body, Color shirt, bool basket)
        {
            var root = v.transform; string B = Key(body), I = Key(Palette.UiInk), W = Key(Palette.WallWhite);
            var tyre = new Color(0.16f, 0.16f, 0.18f);
            Part(PrimitiveType.Cube, root, new Vector3(0, 0.36f, 0), new Vector3(1.05f, 0.22f, 2.3f), body, B);             // chassis
            Part(PrimitiveType.Cube, root, new Vector3(0, 0.62f, 0.86f), new Vector3(1.05f, 0.42f, 0.56f), body, B);         // front cowl
            Part(PrimitiveType.Cube, root, new Vector3(0, 0.86f, 1.0f), new Vector3(0.9f, 0.06f, 0.22f), body, B);           // dash
            Part(PrimitiveType.Cube, root, new Vector3(0, 0.66f, -0.1f), new Vector3(0.98f, 0.14f, 0.55f), Palette.UiInk, I); // seat
            Part(PrimitiveType.Cube, root, new Vector3(0, 0.98f, -0.38f), new Vector3(0.98f, 0.5f, 0.1f), Palette.UiInk, I);  // backrest
            foreach (var x in new[] { -0.5f, 0.5f }) foreach (var z in new[] { 0.95f, -0.45f })
                Part(PrimitiveType.Cylinder, root, new Vector3(x, 1.3f, z), new Vector3(0.05f, 0.62f, 0.05f), Palette.WallWhite, W);
            Part(PrimitiveType.Cube, root, new Vector3(0, 1.93f, 0.25f), new Vector3(1.2f, 0.07f, 1.75f), Palette.WallWhite, W); // canopy
            if (basket)
            {
                Part(PrimitiveType.Cube, root, new Vector3(0, 0.66f, -0.86f), new Vector3(0.9f, 0.32f, 0.5f), Palette.Trunk, Key(Palette.Trunk));
                Part(PrimitiveType.Sphere, root, new Vector3(-0.2f, 0.86f, -0.86f), Vector3.one * 0.2f, Palette.Citrus, Key(Palette.Citrus));
                Part(PrimitiveType.Sphere, root, new Vector3(0.15f, 0.85f, -0.8f), Vector3.one * 0.2f, Palette.Citrus, Key(Palette.Citrus));
            }
            foreach (var x in new[] { -0.3f, 0.3f })
                Part(PrimitiveType.Sphere, root, new Vector3(x, 0.66f, 1.15f), new Vector3(0.14f, 0.1f, 0.05f), new Color(1f, 0.95f, 0.8f), "CartLamp", default, 1.2f);
            // steering column + wheel
            Part(PrimitiveType.Cylinder, root, new Vector3(-0.22f, 0.98f, 0.62f), new Vector3(0.04f, 0.22f, 0.04f), Palette.UiInk, I, new Vector3(-35, 0, 0));
            Part(PrimitiveType.Cylinder, root, new Vector3(-0.22f, 1.14f, 0.5f), new Vector3(0.3f, 0.015f, 0.3f), Palette.UiInk, I, new Vector3(-55, 0, 0));
            // wheels: the pivot spins about its local X
            var wheels = new List<Transform>();
            foreach (var x in new[] { -0.52f, 0.52f }) foreach (var z in new[] { 0.78f, -0.72f })
            {
                var piv = Pivot("Wheel", root, new Vector3(x, 0.24f, z));
                Part(PrimitiveType.Cylinder, piv, Vector3.zero, new Vector3(0.48f, 0.08f, 0.48f), tyre, Key(tyre), new Vector3(0, 0, 90));
                Part(PrimitiveType.Cylinder, piv, new Vector3(x > 0 ? 0.08f : -0.08f, 0, 0), new Vector3(0.24f, 0.01f, 0.24f), Palette.Silver, Key(Palette.Silver), new Vector3(0, 0, 90));
                Part(PrimitiveType.Cube, piv, new Vector3(x > 0 ? 0.085f : -0.085f, 0, 0), new Vector3(0.02f, 0.2f, 0.05f), tyre, Key(tyre)); // shows the spin
                wheels.Add(piv);
            }
            // the driver: a kibbutz old-timer, white hair under a kova tembel
            var drv = Pivot("Driver", root, new Vector3(-0.22f, 0.74f, -0.12f));
            var skin = new Color(0.86f, 0.66f, 0.52f);
            Part(PrimitiveType.Capsule, drv, new Vector3(0, 0.42f, 0), new Vector3(0.42f, 0.34f, 0.3f), shirt, Key(shirt));
            Part(PrimitiveType.Sphere, drv, new Vector3(0, 0.88f, 0.02f), Vector3.one * 0.26f, skin, Key(skin));
            Part(PrimitiveType.Sphere, drv, new Vector3(0, 0.84f, -0.06f), new Vector3(0.27f, 0.2f, 0.22f), Palette.WallWhite, W);   // white hair
            Part(PrimitiveType.Cylinder, drv, new Vector3(0, 0.98f, 0.02f), new Vector3(0.36f, 0.015f, 0.36f), Palette.WallWhite, Key(new Color(0.93f, 0.92f, 0.86f))); // brim
            Part(PrimitiveType.Sphere, drv, new Vector3(0, 1.03f, 0.02f), new Vector3(0.27f, 0.2f, 0.27f), new Color(0.93f, 0.92f, 0.86f), Key(new Color(0.93f, 0.92f, 0.86f))); // kova tembel
            foreach (var x in new[] { -0.17f, 0.17f })
                Part(PrimitiveType.Capsule, drv, new Vector3(x * 0.8f, 0.45f, 0.28f), new Vector3(0.09f, 0.22f, 0.09f), skin, Key(skin), new Vector3(70, 0, 0)); // arms to the wheel
            Part(PrimitiveType.Cube, drv, new Vector3(0, 0.0f, 0.3f), new Vector3(0.36f, 0.12f, 0.5f), new Color(0.35f, 0.33f, 0.3f), Key(new Color(0.35f, 0.33f, 0.3f))); // legs
            v.Wheels = wheels.ToArray(); v.WheelRadius = 0.24f; v.Driver = drv; v.IsCart = true; v.Length = 2.4f;
            var bc = root.gameObject.AddComponent<BoxCollider>(); bc.center = new Vector3(0, 1.0f, 0.05f); bc.size = new Vector3(1.2f, 1.9f, 2.4f);
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

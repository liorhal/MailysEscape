using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Maily
{
    /// <summary>
    /// מבוך החטיפים — a 2D maze chase. Maily's own head (rendered from her 3D model) gobbles every treat in
    /// a hedge maze while the kibbutz cats prowl; a big bone lets her bark them back to the cattery.
    /// Arrows / WASD to steer, Q to stop.
    /// </summary>
    public class MazeChase : MiniGame
    {
        static readonly string[] Map =
        {
            "#####################",
            "#o...#.........#...o#",
            "#.##.#.###.###.#.##.#",
            "#...................#",
            "#.##.####.#.####.##.#",
            "#....#.........#....#",
            "####.#.###-###.#.####",
            "####.#.#CCCCC#.#.####",
            "#....#.#######.#....#",
            "#.##.............##.#",
            "#..#.#####.#####.#..#",
            "##.#...#.....#...#.##",
            "#....#.#.###.#.#....#",
            "#o##......M......##o#",
            "#####################",
        };
        const int W = 21, H = 15;
        const float Cell = 46f;
        static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        class Mover
        {
            public RectTransform Rt; public Vector2Int C, Dir, Want; public float T; public float Speed;
            public Vector2 Pos => new Vector2(C.x, C.y) + new Vector2(Dir.x, Dir.y) * T;
        }
        class Cat : Mover { public Image Body; public Color Coat; public float Wait, Wander; public bool Home; }

        GameObject _root; RectTransform _table;
        readonly Dictionary<Vector2Int, Image> _dots = new Dictionary<Vector2Int, Image>();
        readonly HashSet<Vector2Int> _big = new HashSet<Vector2Int>();
        Mover _m; readonly List<Cat> _cats = new List<Cat>();
        Vector2Int _start, _den = new Vector2Int(10, 7);
        int _lives = 3, _total; float _scared, _hurt, _chomp;
        readonly List<Image> _lifeIcons = new List<Image>();

        public override bool Is2D => true;
        public override string Status => $"חטיפים: {Score} / {_total}   ·   חיים: {_lives}";
        public override string ResultText => Score >= _total ? "מיילי ניקתה את כל המבוך!" : $"מיילי אכלה {Score} חטיפים מתוך {_total}";

        static char At(Vector2Int c) => c.x < 0 || c.y < 0 || c.x >= W || c.y >= H ? '#' : Map[c.y][c.x];
        static bool Open(Vector2Int c, bool cat) { char ch = At(c); return ch != '#' && (cat || (ch != '-' && ch != 'C')); }
        static Vector2 UI(Vector2 cell) => new Vector2((cell.x - (W - 1) * 0.5f) * Cell, ((H - 1) * 0.5f - cell.y) * Cell);

        public override void Begin()
        {
            TimeLeft = 150f;
            _table = Board2D.Table("Maze", new Vector2(W * Cell + 8, H * Cell + 8), Color.Lerp(Palette.Kurkar, Palette.WallWhite, 0.25f), out _root);
            Spawned.Add(_root);
            var hedge = Color.Lerp(Palette.LawnDeep, Palette.UiInk, 0.15f);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    var c = new Vector2Int(x, y); char ch = At(c);
                    if (ch == '#') Board2D.Box("Hedge", _table, hedge, UI(c), new Vector2(Cell - 4, Cell - 4));
                    else if (ch == '-') Board2D.Box("Door", _table, Palette.WallWhite, UI(c) + new Vector2(0, 14), new Vector2(Cell, 8));
                    else if (ch == '.') { _dots[c] = Board2D.Box("Treat", _table, Palette.Bone, UI(c), new Vector2(11, 11), null, null, Board2D.Circle).GetComponent<Image>(); _total++; }
                    else if (ch == 'o')
                    {
                        var b = Board2D.Box("BigBone", _table, Color.clear, UI(c), new Vector2(34, 34));
                        Board2D.Box("Shaft", b, Palette.Bone, Vector2.zero, new Vector2(26, 9));
                        foreach (var p in new[] { new Vector2(-13, 5), new Vector2(-13, -5), new Vector2(13, 5), new Vector2(13, -5) })
                            Board2D.Box("Knob", b, Palette.Bone, p, new Vector2(11, 11), null, null, Board2D.Circle);
                        _dots[c] = b.GetChild(0).GetComponent<Image>(); _big.Add(c); _total++;
                    }
                    else if (ch == 'M') _start = c;
                }
            Board2D.Label(_table, "חתוליה", 18, Palette.UiPaper, UI(new Vector2(10, 7)), new Vector2(200, 30));

            // cats: ginger, black, grey, calico
            Color[] coats = { Palette.DogGinger, new Color(0.18f, 0.17f, 0.2f), Palette.DogGray, Palette.DogCream };
            for (int i = 0; i < coats.Length; i++)
            {
                var cat = new Cat { Coat = coats[i], Speed = 3.9f + i * 0.2f, Wait = 1.5f + i * 3.5f, Wander = 0.25f + 0.12f * i, Home = true };
                cat.Rt = Board2D.Box("Cat", _table, Color.clear, Vector2.zero, new Vector2(Cell, Cell));
                foreach (var x in new[] { -11f, 11f })
                {
                    var ear = Board2D.Box("Ear", cat.Rt, coats[i], new Vector2(x, 13), new Vector2(16, 16), null, null, Hud.MakeSprite(true));
                    ear.localRotation = Quaternion.Euler(0, 0, x < 0 ? 20 : -20);
                }
                cat.Body = Board2D.Box("Head", cat.Rt, coats[i], new Vector2(0, -2), new Vector2(36, 32), null, null, Board2D.Circle).GetComponent<Image>();
                foreach (var x in new[] { -7f, 7f }) Board2D.Box("Eye", cat.Rt, Palette.Citrus, new Vector2(x, 1), new Vector2(8, 10), null, null, Board2D.Circle);
                Board2D.Box("Nose", cat.Rt, Palette.BullyRed, new Vector2(0, -7), new Vector2(6, 5), null, null, Board2D.Circle);
                _cats.Add(cat);
            }

            // Maily: her real head
            _m = new Mover { Speed = 6.2f };
            var head = new GameObject("Maily", typeof(RectTransform)); head.transform.SetParent(_table, false);
            _m.Rt = (RectTransform)head.transform; _m.Rt.sizeDelta = new Vector2(62, 62);
            var raw = head.AddComponent<RawImage>(); raw.texture = MailyPortrait.Get(); raw.raycastTarget = false;
            if (raw.texture == null) raw.color = Palette.CoinGold;

            for (int i = 0; i < 3; i++)
                _lifeIcons.Add(Board2D.Box("Life", _table, Palette.CoinGold, new Vector2(W * Cell * 0.5f - 20 - i * 30, -H * Cell * 0.5f - 34), new Vector2(22, 22), null, null, Board2D.Circle).GetComponent<Image>());
            Board2D.Label(_table, "חצים / WASD להזזה   ·   Q לסיום", 22, Palette.UiPaper, new Vector2(-120, -H * Cell * 0.5f - 34), new Vector2(700, 30));
            ResetPositions();
        }

        void ResetPositions()
        {
            _m.C = _start; _m.Dir = Vector2Int.zero; _m.Want = Vector2Int.left; _m.T = 0f;
            for (int i = 0; i < _cats.Count; i++)
            {
                var c = _cats[i]; c.C = new Vector2Int(8 + i % 3 + (i == 3 ? 1 : 0), 7); c.Dir = Vector2Int.zero; c.T = 0f; c.Home = true; c.Wait = 2.5f + i * 3.5f;
            }
            _scared = 0f;
        }

        public override bool Tick(float dt)
        {
            TimeLeft -= dt; _scared -= dt; _hurt -= dt; _chomp += dt;
            if (Board2D.Pressed(Key.Q)) return true;

            // input (buffered)
            var k = Keyboard.current; var pad = Gamepad.current;
            Vector2 stick = pad != null ? pad.leftStick.ReadValue() : Vector2.zero;
            if (k != null)
            {
                if (k.upArrowKey.isPressed || k.wKey.isPressed || stick.y > 0.5f) _m.Want = Vector2Int.down; // grid y grows downward
                else if (k.downArrowKey.isPressed || k.sKey.isPressed || stick.y < -0.5f) _m.Want = Vector2Int.up;
                else if (k.leftArrowKey.isPressed || k.aKey.isPressed || stick.x < -0.5f) _m.Want = Vector2Int.left;
                else if (k.rightArrowKey.isPressed || k.dKey.isPressed || stick.x > 0.5f) _m.Want = Vector2Int.right;
            }

            if (_hurt <= 0f)
            {
                Step(_m, dt, false);
                // reverse instantly mid-corridor
                if (_m.Dir != Vector2Int.zero && _m.Want == -_m.Dir) { _m.C += _m.Dir; _m.T = 1f - _m.T; _m.Dir = _m.Want; }
                Eat();
                foreach (var c in _cats) MoveCat(c, dt);
                Collide();
            }

            // visuals
            _m.Rt.anchoredPosition = UI(_m.Pos);
            float chomp = 1f + 0.08f * Mathf.Abs(Mathf.Sin(_chomp * 14f)) * (_m.Dir == Vector2Int.zero ? 0f : 1f);
            _m.Rt.localScale = new Vector3(_m.Dir.x > 0 ? -chomp : chomp, chomp, 1f) * (_hurt > 0f ? Mathf.Clamp01(_hurt) * 0.6f + 0.4f : 1f);
            _m.Rt.localRotation = Quaternion.Euler(0, 0, _m.Dir.y != 0 ? -_m.Dir.y * 12f : 0f);
            foreach (var c in _cats)
            {
                c.Rt.anchoredPosition = UI(c.Pos);
                bool scared = _scared > 0f && !c.Home;
                var col = scared ? (_scared < 2f && Mathf.Repeat(Time.time * 6f, 1f) < 0.5f ? Palette.WallWhite : Color.Lerp(c.Coat, Palette.MiniSky, 0.6f)) : c.Coat;
                foreach (var img in c.Rt.GetComponentsInChildren<Image>()) if (img.name == "Head" || img.name == "Ear") img.color = col;
                c.Rt.localScale = Vector3.one * (scared ? 0.85f + 0.05f * Mathf.Sin(Time.time * 20f) : 1f);
            }
            for (int i = 0; i < _lifeIcons.Count; i++) _lifeIcons[i].enabled = i < _lives;

            return TimeLeft <= 0f || _lives <= 0 || Score >= _total;
        }

        void Step(Mover m, float dt, bool cat)
        {
            float speed = m.Speed * (cat && _scared > 0f && !((Cat)m).Home ? 0.6f : 1f);
            float left = speed * dt;
            for (int guard = 0; guard < 4 && left > 0f; guard++)
            {
                if (m.Dir == Vector2Int.zero || m.T <= 0f)
                {
                    // at a cell centre: choose a direction
                    if (!cat)
                    {
                        if (Open(m.C + m.Want, false)) m.Dir = m.Want;
                        else if (m.Dir != Vector2Int.zero && !Open(m.C + m.Dir, false)) m.Dir = Vector2Int.zero;
                    }
                    if (m.Dir == Vector2Int.zero) return;
                }
                float need = 1f - m.T;
                if (left < need) { m.T += left; return; }
                left -= need; m.C += m.Dir; m.T = 0f;
                if (cat) ChooseCatDir((Cat)m);
                else if (Open(m.C + m.Want, false)) m.Dir = m.Want;
                else if (!Open(m.C + m.Dir, false)) { m.Dir = Vector2Int.zero; return; }
            }
        }

        void MoveCat(Cat c, float dt)
        {
            if (c.Home)
            {
                c.Wait -= dt;
                if (c.Wait > 0f) { c.T = 0f; c.Dir = Vector2Int.zero; return; }
                // walk out through the door
                c.C = new Vector2Int(10, 6); c.Dir = Vector2Int.zero; c.T = 0f; c.Home = false;
                c.Dir = Vector2Int.down; // up the screen (towards row 5)
            }
            if (c.Dir == Vector2Int.zero) ChooseCatDir(c);
            Step(c, dt, true);
        }

        void ChooseCatDir(Cat c)
        {
            var target = _m.C;
            bool flee = _scared > 0f;
            Vector2Int best = Vector2Int.zero; float bestScore = float.MaxValue; var options = new List<Vector2Int>();
            foreach (var d in Dirs)
            {
                var n = c.C + d;
                if (!Open(n, true) || At(n) == 'C' || (At(n) == '-' && !c.Home)) continue;
                if (d == -c.Dir && c.Dir != Vector2Int.zero) continue;
                options.Add(d);
                float dist = (n - target).sqrMagnitude * (flee ? -1f : 1f);
                if (dist < bestScore) { bestScore = dist; best = d; }
            }
            if (options.Count == 0) { c.Dir = -c.Dir; return; }
            c.Dir = Random.value < c.Wander ? options[Random.Range(0, options.Count)] : best;
        }

        void Eat()
        {
            var cell = _m.T < 0.5f ? _m.C : _m.C + _m.Dir;
            if (!_dots.TryGetValue(cell, out var img) || img == null || !img.gameObject.activeSelf) return;
            bool big = _big.Contains(cell);
            (big ? img.transform.parent.gameObject : img.gameObject).SetActive(false);
            _dots.Remove(cell);
            Score++;
            if (big) { _scared = 7f; Sfx.I?.Play2D(Sfx.I.Bark, 0.9f, 1.1f); }
            else Sfx.I?.Play2D(Sfx.I.Chomp, 0.5f, 1f + (Score % 2) * 0.12f);
        }

        void Collide()
        {
            foreach (var c in _cats)
            {
                if (c.Home || (c.Pos - _m.Pos).sqrMagnitude > 0.42f) continue;
                if (_scared > 0f)
                {
                    c.Home = true; c.C = _den; c.Dir = Vector2Int.zero; c.T = 0f; c.Wait = 3f;
                    Sfx.I?.Play2D(Sfx.I.Squawk, 0.7f, 0.7f);
                }
                else
                {
                    _lives--; _hurt = 1.2f;
                    Sfx.I?.Play2D(Sfx.I.Hit, 0.9f);
                    if (_lives > 0) ResetPositions();
                    return;
                }
            }
        }

        public override int Medal() => Score >= _total ? 3 : Score >= 100 ? 2 : Score >= 50 ? 1 : 0;
    }
}

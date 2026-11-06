using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Maily
{
    /// <summary>זיכרון — flip tiles to find the 8 pairs of kibbutz pictures in as few turns as possible.</summary>
    public class MemoryMatch : MiniGame
    {
        class Tile { public int Kind; public RectTransform Rt; public GameObject Face, Back; public bool Up, Done; public float Flip = 1f; public Vector2 Pos; }

        const float TileSize = 150f, Gap = 18f;
        static readonly string[] Words = { "מיילי", "דררה", "תפוז", "עצם", "לב", "חתול", "שמש", "פריזבי" };

        GameObject _root; RectTransform _table;
        readonly List<Tile> _tiles = new List<Tile>();
        Tile _a, _b; float _hide; int _moves;

        public override bool Is2D => true;
        public override string Status => $"זוגות: {Score} / 8   ·   תורות: {_moves}";
        public override string ResultText => Score >= 8 ? $"כל הזוגות נמצאו ב-{_moves} תורות" : $"נמצאו {Score} זוגות";

        public override void Begin()
        {
            TimeLeft = 120f;
            float span = 4 * TileSize + 3 * Gap;
            _table = Board2D.Table("Memory", new Vector2(span + 60, span + 100), Color.Lerp(Palette.Kurkar, Palette.WallWhite, 0.35f), out _root);
            Spawned.Add(_root);
            Board2D.Label(_table, "לחצו על שני אריחים ומצאו זוגות   ·   Q לסיום", 22, Palette.UiInk, new Vector2(0, -(span + 100) * 0.5f + 26), new Vector2(800, 30));
            var kinds = new List<int>(); for (int i = 0; i < 8; i++) { kinds.Add(i); kinds.Add(i); }
            for (int i = kinds.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (kinds[i], kinds[j]) = (kinds[j], kinds[i]); }
            for (int i = 0; i < 16; i++)
            {
                var t = new Tile { Kind = kinds[i] };
                t.Pos = new Vector2((i % 4 - 1.5f) * (TileSize + Gap), (1.5f - i / 4) * (TileSize + Gap) + 18f);
                t.Rt = Board2D.Box("Tile", _table, Color.clear, t.Pos, new Vector2(TileSize, TileSize));
                var back = Board2D.Box("Back", t.Rt, Palette.ParrotGreen, Vector2.zero, new Vector2(TileSize, TileSize));
                Board2D.Box("Inner", back, Color.Lerp(Palette.ParrotGreen, Palette.UiInk, 0.25f), Vector2.zero, new Vector2(TileSize - 16, TileSize - 16));
                Board2D.Label(back, "?", 70, new Color(1, 1, 1, 0.55f), Vector2.zero, new Vector2(TileSize, TileSize));
                var face = Board2D.Box("Face", t.Rt, Palette.UiPaper, Vector2.zero, new Vector2(TileSize, TileSize));
                Picture(face, t.Kind);
                Board2D.Label(face, Words[t.Kind], 24, Palette.UiInk, new Vector2(0, -52), new Vector2(TileSize, 30));
                t.Face = face.gameObject; t.Back = back.gameObject;
                _tiles.Add(t);
            }
            Refresh();
        }

        static void Picture(RectTransform face, int kind)
        {
            var c = new Vector2(0, 12);
            switch (kind)
            {
                case 0:
                    var p = new GameObject("Maily", typeof(RectTransform)); p.transform.SetParent(face, false);
                    var raw = p.AddComponent<RawImage>(); raw.texture = MailyPortrait.Get(); raw.raycastTarget = false;
                    ((RectTransform)p.transform).sizeDelta = new Vector2(104, 104); ((RectTransform)p.transform).anchoredPosition = c;
                    if (raw.texture == null) raw.color = Palette.CoinGold;
                    break;
                case 1: // green parrot
                    Board2D.Box("Body", face, Palette.ParrotGreen, c + new Vector2(0, -8), new Vector2(64, 74), null, null, Board2D.Circle);
                    Board2D.Box("Head", face, Palette.ParrotGreen, c + new Vector2(10, 30), new Vector2(46, 46), null, null, Board2D.Circle);
                    Board2D.Box("Beak", face, Palette.BullyRed, c + new Vector2(34, 26), new Vector2(18, 16), null, null, Board2D.Circle);
                    Board2D.Box("Eye", face, Palette.UiInk, c + new Vector2(16, 36), new Vector2(9, 9), null, null, Board2D.Circle);
                    break;
                case 2: // orange
                    Board2D.Box("Fruit", face, Palette.Citrus, c, new Vector2(84, 84), null, null, Board2D.Circle);
                    Board2D.Box("Leaf", face, Palette.Foliage, c + new Vector2(14, 44), new Vector2(30, 14), null, null, Board2D.Circle).localRotation = Quaternion.Euler(0, 0, 25);
                    break;
                case 3: // bone
                    Board2D.Box("Shaft", face, Palette.Bone, c, new Vector2(78, 22));
                    foreach (var q in new[] { new Vector2(-40, 12), new Vector2(-40, -12), new Vector2(40, 12), new Vector2(40, -12) })
                        Board2D.Box("Knob", face, Palette.Bone, c + q, new Vector2(28, 28), null, null, Board2D.Circle);
                    break;
                case 4: Board2D.Box("Heart", face, Palette.BullyRed, c, new Vector2(84, 84), null, null, Board2D.Suit(1)); break;
                case 5: // cat
                    foreach (var x in new[] { -24f, 24f })
                        Board2D.Box("Ear", face, Palette.DogGinger, c + new Vector2(x, 32), new Vector2(34, 34), null, null, Hud.MakeSprite(true)).localRotation = Quaternion.Euler(0, 0, x < 0 ? 20 : -20);
                    Board2D.Box("Head", face, Palette.DogGinger, c, new Vector2(84, 74), null, null, Board2D.Circle);
                    foreach (var x in new[] { -16f, 16f }) Board2D.Box("Eye", face, Palette.ParrotGreen, c + new Vector2(x, 6), new Vector2(14, 18), null, null, Board2D.Circle);
                    break;
                case 6: // sun
                    for (int i = 0; i < 8; i++)
                    {
                        var ray = Board2D.Box("Ray", face, Palette.CoinGold, c, new Vector2(10, 104));
                        ray.localRotation = Quaternion.Euler(0, 0, i * 22.5f);
                    }
                    Board2D.Box("Disc", face, Palette.CoinGold, c, new Vector2(66, 66), null, null, Board2D.Circle);
                    break;
                default: // frisbee
                    Board2D.Box("Disc", face, Palette.MiniFrisbee, c, new Vector2(92, 52), null, null, Board2D.Circle);
                    Board2D.Box("Rim", face, Color.Lerp(Palette.MiniFrisbee, Palette.WallWhite, 0.5f), c + new Vector2(0, 4), new Vector2(60, 26), null, null, Board2D.Circle);
                    break;
            }
        }

        public override bool Tick(float dt)
        {
            TimeLeft -= dt;
            if (Board2D.Pressed(Key.Q)) return true;

            if (_hide > 0f)
            {
                _hide -= dt;
                if (_hide <= 0f) { _a.Up = false; _b.Up = false; _a = _b = null; }
            }
            else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && Board2D.Mouse(_table) is Vector2 p)
            {
                foreach (var t in _tiles)
                {
                    if (t.Done || t.Up || Mathf.Abs(p.x - t.Pos.x) > TileSize * 0.5f || Mathf.Abs(p.y - t.Pos.y) > TileSize * 0.5f) continue;
                    t.Up = true; Sfx.I?.Play2D(Sfx.I.Card, 0.8f);
                    if (_a == null) _a = t;
                    else
                    {
                        _b = t; _moves++;
                        if (_a.Kind == _b.Kind)
                        {
                            _a.Done = _b.Done = true; Score++; _a = _b = null;
                            Sfx.I?.Play2D(Sfx.I.Match, 0.8f, 1f + Score * 0.04f);
                        }
                        else _hide = 0.85f;
                    }
                    break;
                }
            }
            Refresh(dt);
            return Score >= 8 || TimeLeft <= 0f;
        }

        void Refresh(float dt = 1f)
        {
            foreach (var t in _tiles)
            {
                float want = t.Up || t.Done ? 1f : -1f;
                t.Flip = Mathf.MoveTowards(t.Flip, want, dt * 9f);
                bool showFace = t.Flip > 0f;
                t.Face.SetActive(showFace); t.Back.SetActive(!showFace);
                t.Rt.localScale = new Vector3(Mathf.Max(0.02f, Mathf.Abs(t.Flip)), 1f, 1f) * (t.Done ? 0.96f : 1f);
            }
        }

        public override int Medal() => Score < 8 ? (Score >= 4 ? 1 : 0) : _moves <= 14 ? 3 : _moves <= 20 ? 2 : 1;
    }
}

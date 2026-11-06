using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Maily
{
    /// <summary>
    /// סוליטר — classic Klondike patience (draw one) on the dining-hall table. Drag cards with the mouse,
    /// double-click to send a card home, click the deck to draw. Maily's portrait is on every card back.
    /// </summary>
    public class Solitaire : MiniGame
    {
        class Card
        {
            public int Rank, Suit; public bool Up; public RectTransform Rt; public GameObject Face, Back; public Vector2 Target;
            public bool Red => Suit == 1 || Suit == 2;
        }

        static readonly Vector2 Size = new Vector2(104, 146);
        const float DownGap = 16f, UpGap = 28f;
        static readonly string[] Ranks = { "", "A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K" };

        GameObject _root; RectTransform _table;
        readonly List<Card> _stock = new List<Card>(), _waste = new List<Card>();
        readonly List<Card>[] _found = { new List<Card>(), new List<Card>(), new List<Card>(), new List<Card>() };
        readonly List<Card>[] _tab = new List<Card>[7];
        List<Card> _drag; List<Card> _dragFrom; Vector2 _grabOffset;
        float _lastClick; Card _lastCard; float _auto; RectTransform _stockSlot;

        public override bool Is2D => true;
        public override string Status => $"קלפים בבית: {Score} / 52";
        public override string ResultText => Score >= 52 ? "כל הקלפים מסודרים — סוליטר מושלם!" : $"{Score} קלפים הגיעו הביתה";

        static float ColX(int i) => (i - 3) * 172f;
        const float TopY = 282f, TabY = 104f;

        public override void Begin()
        {
            TimeLeft = 600f;
            _table = Board2D.Table("Solitaire", new Vector2(1240, 760), new Color(0.2f, 0.42f, 0.3f), out _root);
            Spawned.Add(_root);
            for (int i = 0; i < 7; i++) _tab[i] = new List<Card>();

            // empty slots
            _stockSlot = Slot(new Vector2(ColX(0), TopY), "");
            Slot(new Vector2(ColX(1), TopY), "");
            for (int f = 0; f < 4; f++) Slot(new Vector2(ColX(3 + f), TopY), "A");
            for (int i = 0; i < 7; i++) Slot(new Vector2(ColX(i), TabY), "K");
            Board2D.Label(_table, "גררו קלפים בעכבר   ·   לחיצה כפולה שולחת הביתה   ·   Q לסיום", 22, new Color(1, 1, 1, 0.75f), new Vector2(0, -360), new Vector2(1100, 30));

            var deck = new List<Card>();
            for (int s = 0; s < 4; s++) for (int r = 1; r <= 13; r++) deck.Add(Make(r, s));
            for (int i = deck.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (deck[i], deck[j]) = (deck[j], deck[i]); }
            int k = 0;
            for (int i = 0; i < 7; i++)
                for (int j = 0; j <= i; j++) { var c = deck[k++]; c.Up = j == i; _tab[i].Add(c); }
            while (k < deck.Count) _stock.Add(deck[k++]);
            foreach (var c in deck) c.Rt.anchoredPosition = new Vector2(ColX(0), TopY);
            Layout(true);
        }

        RectTransform Slot(Vector2 pos, string hint)
        {
            var s = Board2D.Box("Slot", _table, new Color(1, 1, 1, 0.12f), pos, Size);
            if (hint != "") Board2D.Label(s, hint, 40, new Color(1, 1, 1, 0.25f), Vector2.zero, Size);
            return s;
        }

        Card Make(int rank, int suit)
        {
            var c = new Card { Rank = rank, Suit = suit };
            c.Rt = Board2D.Box("Card", _table, Color.clear, Vector2.zero, Size);
            var ink = c.Red ? new Color(0.8f, 0.16f, 0.14f) : new Color(0.12f, 0.12f, 0.16f);
            // face
            var face = Board2D.Box("Face", c.Rt, new Color(0.98f, 0.97f, 0.93f), Vector2.zero, Size);
            Board2D.Label(face, Ranks[rank], 30, ink, new Vector2(-30, 50), new Vector2(60, 40)).fontStyle = TMPro.FontStyles.Bold;
            var small = Board2D.Box("Pip", face, ink, new Vector2(-31, 22), new Vector2(22, 22), null, null, Board2D.Suit(suit));
            if (rank > 10)
                Board2D.Label(face, Ranks[rank], 64, ink, new Vector2(8, -14), new Vector2(90, 80)).fontStyle = TMPro.FontStyles.Bold;
            Board2D.Box("Big", face, ink, rank > 10 ? new Vector2(26, -44) : new Vector2(6, -18), rank > 10 ? new Vector2(30, 30) : new Vector2(58, 58), null, null, Board2D.Suit(suit));
            c.Face = face.gameObject;
            // back: Maily on a hedge-green card
            var back = Board2D.Box("Back", c.Rt, Palette.WallWhite, Vector2.zero, Size);
            Board2D.Box("Inner", back, Color.Lerp(Palette.LawnDeep, Palette.UiInk, 0.2f), Vector2.zero, Size - new Vector2(12, 12));
            var p = new GameObject("Maily", typeof(RectTransform)); p.transform.SetParent(back, false);
            var raw = p.AddComponent<RawImage>(); raw.texture = MailyPortrait.Get(); raw.raycastTarget = false; ((RectTransform)p.transform).sizeDelta = new Vector2(84, 84);
            if (raw.texture == null) raw.enabled = false;
            c.Back = back.gameObject;
            return c;
        }

        void Layout(bool snap)
        {
            foreach (var c in _stock) c.Target = new Vector2(ColX(0), TopY);
            foreach (var c in _waste) c.Target = new Vector2(ColX(1), TopY);
            for (int f = 0; f < 4; f++) foreach (var c in _found[f]) c.Target = new Vector2(ColX(3 + f), TopY);
            for (int i = 0; i < 7; i++)
            {
                float y = TabY;
                foreach (var c in _tab[i]) { c.Target = new Vector2(ColX(i), y); y -= c.Up ? UpGap : DownGap; }
            }
            // draw order: piles left to right, each bottom to top; dragged cards last
            foreach (var pile in AllPiles()) foreach (var c in pile) { c.Rt.SetAsLastSibling(); c.Face.SetActive(c.Up); c.Back.SetActive(!c.Up); if (snap) c.Rt.anchoredPosition = c.Target; }
            if (_drag != null) foreach (var c in _drag) c.Rt.SetAsLastSibling();
        }

        IEnumerable<List<Card>> AllPiles()
        {
            yield return _stock; yield return _waste;
            foreach (var f in _found) yield return f;
            foreach (var t in _tab) yield return t;
        }

        public override bool Tick(float dt)
        {
            TimeLeft -= dt;
            if (Board2D.Pressed(Key.Q)) return true;
            var mouse = Pointer.current; var mp = Board2D.Mouse(_table);

            if (mouse != null && mp.HasValue)
            {
                var p = mp.Value;
                if (mouse.press.wasPressedThisFrame) Press(p);
                if (_drag != null)
                {
                    for (int i = 0; i < _drag.Count; i++) _drag[i].Rt.anchoredPosition = p + _grabOffset + new Vector2(0, -UpGap * i);
                    if (mouse.press.wasReleasedThisFrame) Drop(p);
                }
            }

            // auto-finish once everything is face up
            if (_drag == null && _stock.Count == 0 && _waste.Count == 0 && AllUp())
            {
                _auto -= dt;
                if (_auto <= 0f) { _auto = 0.07f; AutoOne(); }
            }

            foreach (var pile in AllPiles())
                foreach (var c in pile)
                    if (_drag == null || !_drag.Contains(c)) c.Rt.anchoredPosition = Vector2.Lerp(c.Rt.anchoredPosition, c.Target, 1f - Mathf.Exp(-dt * 18f));
            return Score >= 52 || TimeLeft <= 0f;
        }

        bool AllUp() { foreach (var t in _tab) foreach (var c in t) if (!c.Up) return false; return true; }

        static bool Hit(Vector2 p, Vector2 c, float h = -1f)
        {
            if (h < 0f) h = Size.y;
            return Mathf.Abs(p.x - c.x) <= Size.x * 0.5f && p.y <= c.y + Size.y * 0.5f && p.y >= c.y + Size.y * 0.5f - h;
        }

        void Press(Vector2 p)
        {
            // the deck: draw one, or turn the waste over
            if (Hit(p, new Vector2(ColX(0), TopY)))
            {
                if (_stock.Count > 0) { var c = _stock[_stock.Count - 1]; _stock.RemoveAt(_stock.Count - 1); c.Up = true; _waste.Add(c); }
                else { for (int i = _waste.Count - 1; i >= 0; i--) { _waste[i].Up = false; _stock.Add(_waste[i]); } _waste.Clear(); }
                Sfx.I?.Play2D(Sfx.I.Card, 0.8f);
                Layout(false); return;
            }
            // pick up: waste top, foundation tops, or any face-up run in a column
            if (_waste.Count > 0 && Hit(p, _waste[_waste.Count - 1].Target)) { Grab(_waste, _waste.Count - 1, p); return; }
            for (int f = 0; f < 4; f++)
                if (_found[f].Count > 0 && Hit(p, _found[f][_found[f].Count - 1].Target)) { Grab(_found[f], _found[f].Count - 1, p); return; }
            for (int i = 0; i < 7; i++)
            {
                var t = _tab[i];
                for (int j = t.Count - 1; j >= 0; j--)
                {
                    bool last = j == t.Count - 1;
                    if (!Hit(p, t[j].Target, last ? Size.y : (t[j].Up ? UpGap : DownGap))) continue;
                    if (!t[j].Up) { if (last) { t[j].Up = true; Sfx.I?.Play2D(Sfx.I.Card, 0.8f); Layout(false); } return; }
                    Grab(t, j, p); return;
                }
            }
        }

        void Grab(List<Card> from, int index, Vector2 p)
        {
            var card = from[index];
            // double-click: straight to a foundation
            if (_lastCard == card && Time.unscaledTime - _lastClick < 0.35f && index == from.Count - 1 && ToFoundation(from)) { _lastCard = null; return; }
            _lastCard = card; _lastClick = Time.unscaledTime;
            _drag = from.GetRange(index, from.Count - index); _dragFrom = from;
            _grabOffset = card.Rt.anchoredPosition - p;
            foreach (var c in _drag) c.Rt.SetAsLastSibling();
            Sfx.I?.Play2D(Sfx.I.Card, 0.6f, 1.2f);
        }

        void Drop(Vector2 p)
        {
            var first = _drag[0]; List<Card> to = null;
            var centre = p + _grabOffset;
            if (_drag.Count == 1)
                for (int f = 0; f < 4 && to == null; f++)
                    if ((centre - new Vector2(ColX(3 + f), TopY)).magnitude < 110f && FitsFoundation(first, _found[f])) to = _found[f];
            for (int i = 0; i < 7 && to == null; i++)
            {
                var t = _tab[i]; if (t == _dragFrom) continue;
                var anchor = t.Count > 0 ? t[t.Count - 1].Target : new Vector2(ColX(i), TabY);
                if (Mathf.Abs(centre.x - anchor.x) < 90f && centre.y < anchor.y + 110f && centre.y > anchor.y - 200f && FitsTableau(first, t)) to = t;
            }
            if (to != null) Move(_dragFrom, _dragFrom.IndexOf(first), to);
            else Sfx.I?.Play2D(Sfx.I.Click, 0.6f);
            _drag = null; _dragFrom = null;
            Layout(false);
        }

        static bool FitsFoundation(Card c, List<Card> f) => f.Count == 0 ? c.Rank == 1 : f[f.Count - 1].Suit == c.Suit && f[f.Count - 1].Rank == c.Rank - 1;
        static bool FitsTableau(Card c, List<Card> t)
        {
            if (t.Count == 0) return c.Rank == 13;
            var top = t[t.Count - 1];
            return top.Up && top.Red != c.Red && top.Rank == c.Rank + 1;
        }

        bool ToFoundation(List<Card> from)
        {
            var c = from[from.Count - 1];
            for (int f = 0; f < 4; f++)
                if (FitsFoundation(c, _found[f])) { Move(from, from.Count - 1, _found[f]); Layout(false); return true; }
            return false;
        }

        void Move(List<Card> from, int index, List<Card> to)
        {
            var run = from.GetRange(index, from.Count - index);
            from.RemoveRange(index, from.Count - index);
            to.AddRange(run);
            if (from.Count > 0 && System.Array.IndexOf(_tab, from) >= 0 && !from[from.Count - 1].Up) from[from.Count - 1].Up = true; // turn the next card
            Score = _found[0].Count + _found[1].Count + _found[2].Count + _found[3].Count;
            bool home = System.Array.IndexOf(_found, to) >= 0;
            Sfx.I?.Play2D(home ? Sfx.I.Match : Sfx.I.Card, home ? 0.6f : 0.8f, home ? 1f + Score * 0.01f : 1f);
        }

        void AutoOne()
        {
            foreach (var t in _tab) if (t.Count > 0 && ToFoundation(t)) return;
        }

        public override int Medal() => Score >= 52 ? 3 : Score >= 26 ? 2 : Score >= 10 ? 1 : 0;
    }
}

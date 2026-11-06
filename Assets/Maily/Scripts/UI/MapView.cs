using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Maily
{
    /// <summary>
    /// Overview map of Kibbutz Hatzor (M). The background is a one-time orthographic render of the real
    /// scene from above; markers show Maily (with heading), home, places, friendly dogs, street dogs,
    /// dararot, treats and mini-games, with a legend. A north-up minimap centred on Maily is always shown
    /// bottom-left while exploring; M enlarges it to the full map (the world is frozen while it is open).
    /// </summary>
    public class MapView : MonoBehaviour
    {
        public float Extent = 640f; // half-width of the mapped square, metres

        public float MiniRange = 170f; // metres shown across the corner minimap

        RectTransform _canvas, _root, _mapRect, _miniRoot, _miniContent;
        RawImage _image, _miniImage;
        RenderTexture _rt;
        Sprite _square, _tri, _circle, _star;
        const float MapSize = 940f, MiniSize = 330f;

        /// <summary>One drawing surface (big map or minimap) with its own marker pools.</summary>
        class Layer
        {
            public RectTransform Parent; public float Size; public float MarkerScale; public bool Labels;
            public readonly List<Image> Pool = new List<Image>();
            public readonly List<TextMeshProUGUI> Texts = new List<TextMeshProUGUI>();
            public int Used, TextsUsed;
        }
        Layer _big, _mini, _cur;

        void Start()
        {
            _square = Hud.MakeSprite(false); _tri = Hud.MakeSprite(true); _circle = MakeCircle();
            _star = Board2D.MakeShape(96, (x, y) =>   // five-pointed star: only mini-games use it
            {
                float a = Mathf.Atan2(x, y), r = Mathf.Sqrt(x * x + y * y);
                float k = Mathf.Repeat(a, Mathf.PI * 2f / 5f) / (Mathf.PI * 2f / 5f);    // 0..1 across one point
                return r <= Mathf.Lerp(0.42f, 0.98f, Mathf.Abs(k - 0.5f) * 2f);
            });
            Build();
            BuildMini();
            _root.gameObject.SetActive(false);
            _miniRoot.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ background render
        void RenderBackground()
        {
            if (_rt != null) return;
            _rt = new RenderTexture(2048, 2048, 24, RenderTextureFormat.ARGB32) { name = "KibbutzMap" };
            var go = new GameObject("MapCamera");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = Extent;
            cam.transform.SetPositionAndRotation(new Vector3(0, 400f, 0), Quaternion.Euler(90f, 0f, 0f)); // north (+Z) up
            cam.nearClipPlane = 1f; cam.farClipPlane = 800f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Palette.Field;
            var data = go.AddComponent<UniversalAdditionalCameraData>(); data.renderPostProcessing = true;
            cam.targetTexture = _rt;
            bool fog = RenderSettings.fog; RenderSettings.fog = false;   // no haze from 400 m up
            var hidden = new List<Renderer>();                             // characters are drawn as markers instead
            void Hide(Component c) { foreach (var r in c.GetComponentsInChildren<Renderer>()) if (r.enabled) { r.enabled = false; hidden.Add(r); } }
            if (MailyController.I != null) Hide(MailyController.I);
            foreach (var d in NpcDog.All) Hide(d);
            foreach (var d in HostileDog.All) Hide(d);
            foreach (var p in Parrot.All) Hide(p);
            foreach (var c in Collectible.All) Hide(c);
            foreach (var v in TrafficVehicle.All) Hide(v);
            if (Family.I != null) foreach (var f in Family.I.Members) Hide(f);
            cam.Render();
            foreach (var r in hidden) r.enabled = true;
            RenderSettings.fog = fog;
            cam.targetTexture = null;
            Destroy(go);
            _image.texture = _rt; _miniImage.texture = _rt;
        }

        void OnDestroy() { if (_rt != null) _rt.Release(); }

        // ------------------------------------------------------------------ UI
        void Build()
        {
            var go = new GameObject("Map Canvas");
            go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            _canvas = (RectTransform)go.transform;
            _root = Rect("Big Map", _canvas, Color.clear, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Rect("Dim", _root, new Color(Palette.UiInk.r, Palette.UiInk.g, Palette.UiInk.b, 0.97f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var frame = Rect("Frame", _root, Palette.UiPaper, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-230, -10), new Vector2(MapSize + 16, MapSize + 16));
            _mapRect = Rect("Map", frame, Color.clear, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(MapSize, MapSize));
            _image = _mapRect.gameObject.AddComponent<RawImage>(); _image.color = Color.white; _image.raycastTarget = false;
            // markers live in their own layer above the image
            var layer = Rect("Markers", _mapRect, Color.clear, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _big = new Layer { Parent = layer, Size = MapSize, MarkerScale = 1f, Labels = true };

            var title = Label(_root, "מפת קיבוץ חצור", 54, Palette.CoinGold, TextAlignmentOptions.Center);
            var trt = title.rectTransform; trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f); trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(-230, -2); trt.sizeDelta = new Vector2(900, 60);

            // legend (right side, RTL)
            var legend = Rect("Legend", _root, Palette.UiPanel, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-40, 0), new Vector2(440, 830));
            legend.pivot = new Vector2(1f, 0.5f);
            var lt = Label(legend, "מקרא", 40, Palette.CoinGold, TextAlignmentOptions.Right);
            lt.rectTransform.anchorMin = new Vector2(0, 1); lt.rectTransform.anchorMax = new Vector2(1, 1); lt.rectTransform.pivot = new Vector2(0.5f, 1);
            lt.rectTransform.sizeDelta = new Vector2(-40, 60); lt.rectTransform.anchoredPosition = new Vector2(0, -16);
            var rows = new (Sprite s, Color c, string text)[]
            {
                (_tri, Palette.CoinGold, "מיילי"),
                (_square, Palette.WallWhite, "הבית של מיילי"),
                (_square, Palette.UiInk, "מקום בקיבוץ"),
                (_circle, Palette.DogBrown, "כלב חבר (עוד לא נפגשתם)"),
                (_circle, new Color(Palette.DogBrown.r, Palette.DogBrown.g, Palette.DogBrown.b, 0.35f), "כלב חבר (כבר חברים)"),
                (_square, Palette.BullyRed, "כלב רחוב (סכנה!)"),
                (_circle, Palette.ParrotGreen, "דררה"),
                (_circle, Palette.Bone, "חטיף"),
                (_star, Palette.MiniSky, "משחקון (F) - מסגרת בצבע המדליה"),
                (_circle, Palette.MiniSky, "המשפחה של מיילי"),
                (_square, Palette.Silver, "רכב / קלנועית"),
            };
            for (int i = 0; i < rows.Length; i++)
            {
                var row = Rect("Row", legend, Color.clear, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -100 - i * 62), new Vector2(0, 54));
                var icon = Rect("Icon", row, rows[i].c, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-46, 0), new Vector2(30, 30));
                icon.GetComponent<Image>().sprite = rows[i].s; icon.GetComponent<Image>().type = Image.Type.Simple;
                if (rows[i].s == _square && rows[i].c != Palette.WallWhite && rows[i].c != Palette.UiInk) icon.localRotation = Quaternion.Euler(0, 0, 45);
                if (rows[i].c == Palette.UiInk) { icon.GetComponent<Image>().color = Palette.UiPaper; icon.sizeDelta = new Vector2(16, 16); }
                var t = Label(row, rows[i].text, 28, Palette.UiPaper, TextAlignmentOptions.Right);
                t.margin = new Vector4(10, 0, 84, 0);
            }
            var hint = Label(legend, "M או Esc לחזרה למשחק", 26, new Color(1, 1, 1, 0.6f), TextAlignmentOptions.Center);
            hint.rectTransform.anchorMin = new Vector2(0, 0); hint.rectTransform.anchorMax = new Vector2(1, 0); hint.rectTransform.pivot = new Vector2(0.5f, 0);
            hint.rectTransform.sizeDelta = new Vector2(0, 50); hint.rectTransform.anchoredPosition = new Vector2(0, 14);
        }

        /// <summary>Always-on corner minimap: north-up, centred on Maily, clipped to a framed square.</summary>
        void BuildMini()
        {
            _miniRoot = Rect("Minimap", _canvas, Palette.UiPaper, Vector2.zero, Vector2.zero, new Vector2(32, 32), new Vector2(MiniSize + 12, MiniSize + 12));
            _miniRoot.pivot = Vector2.zero; _miniRoot.anchoredPosition = new Vector2(32, 32);
            var view = Rect("View", _miniRoot, Palette.Field, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(MiniSize, MiniSize));
            view.gameObject.AddComponent<RectMask2D>();
            float content = MiniSize * 2f * Extent / MiniRange;
            _miniContent = Rect("Content", view, Color.clear, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(content, content));
            _miniImage = _miniContent.gameObject.AddComponent<RawImage>(); _miniImage.color = Color.white; _miniImage.raycastTarget = false;
            var layer = Rect("Markers", _miniContent, Color.clear, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _mini = new Layer { Parent = layer, Size = content, MarkerScale = 0.8f, Labels = false };

            var badge = Rect("North", _miniRoot, Palette.UiInk, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-26, -26), new Vector2(36, 36));
            var bimg = badge.GetComponent<Image>(); bimg.sprite = _circle; bimg.type = Image.Type.Simple;
            Label(badge, "צ", 24, Palette.CoinGold, TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
            var tag = Rect("Tag", _miniRoot, Palette.UiPanel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, 24), new Vector2(MiniSize + 12, 40));
            Label(tag, "M  הגדלת המפה", 24, Palette.UiPaper, TextAlignmentOptions.Center);
        }

        RectTransform Rect(string n, RectTransform parent, Color c, Vector2 amin, Vector2 amax, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(n, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false);
            rt.anchorMin = amin; rt.anchorMax = amax; rt.anchoredPosition = pos; rt.sizeDelta = size;
            if (c.a > 0f) { var img = go.AddComponent<Image>(); img.sprite = _square; img.type = Image.Type.Sliced; img.color = c; img.raycastTarget = false; }
            return rt;
        }

        TextMeshProUGUI Label(RectTransform parent, string s, float size, Color c, TextAlignmentOptions align)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.fontSize = size; t.color = c; t.alignment = align; t.raycastTarget = false; t.richText = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            Heb.Set(t, s);
            return t;
        }

        static Sprite MakeCircle()
        {
            const int n = 64; var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f));
                    byte a = (byte)(Mathf.Clamp01(n / 2f - 1f - d) * 255);
                    px[y * n + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }

        // ------------------------------------------------------------------ open / markers
        void Update()
        {
            var gm = GameManager.I; if (gm == null) return;
            var k = Keyboard.current; var pad = Gamepad.current;
            bool mKey = (k != null && k.mKey.wasPressedThisFrame) || (pad != null && pad.selectButton.wasPressedThisFrame);
            bool esc = k != null && k.escapeKey.wasPressedThisFrame;
            bool busy = MiniGameManager.I != null && MiniGameManager.I.Busy;
            if (gm.MapOpen && (mKey || esc)) gm.ToggleMap();
            else if (!gm.MapOpen && mKey && gm.State == GameState.Playing && !busy) { RenderBackground(); gm.ToggleMap(); }

            bool miniOn = !gm.MapOpen && gm.State == GameState.Playing && !(MiniGameManager.I != null && MiniGameManager.I.Board);
            if (miniOn) RenderBackground();
            if (_root.gameObject.activeSelf != gm.MapOpen) _root.gameObject.SetActive(gm.MapOpen);
            if (_miniRoot.gameObject.activeSelf != miniOn) _miniRoot.gameObject.SetActive(miniOn);
            if (gm.MapOpen) DrawMarkers(_big);
            if (miniOn)
            {
                var m = MailyController.I;
                if (m != null) _miniContent.anchoredPosition = -ToMap(m.transform.position, _mini.Size);
                DrawMarkers(_mini);
            }
        }

        Vector2 ToMap(Vector3 w, float size) => new Vector2(w.x / (2f * Extent), w.z / (2f * Extent)) * size;

        Image Marker(Sprite s, Color c, Vector3 world, float size, float rot = 0f)
        {
            Image img; var L = _cur; size *= L.MarkerScale;
            if (L.Used < L.Pool.Count) img = L.Pool[L.Used];
            else
            {
                var go = new GameObject("M", typeof(RectTransform));
                go.transform.SetParent(L.Parent, false);
                img = go.AddComponent<Image>(); img.raycastTarget = false;
                var rt0 = img.rectTransform; rt0.anchorMin = rt0.anchorMax = new Vector2(0.5f, 0.5f);
                L.Pool.Add(img);
            }
            L.Used++;
            img.gameObject.SetActive(true);
            img.sprite = s; img.color = c; img.type = Image.Type.Simple;
            var rt = img.rectTransform; rt.anchoredPosition = ToMap(world, L.Size); rt.sizeDelta = new Vector2(size, size); rt.localRotation = Quaternion.Euler(0, 0, rot);
            img.transform.SetAsLastSibling();
            return img;
        }

        void Text(string s, Vector3 world, Color c, float size, Vector2 offset)
        {
            var L = _cur; if (!L.Labels) return;
            TextMeshProUGUI t;
            if (L.TextsUsed < L.Texts.Count) t = L.Texts[L.TextsUsed];
            else
            {
                t = Label(L.Parent, "", size, c, TextAlignmentOptions.Center);
                var rt0 = t.rectTransform; rt0.anchorMin = rt0.anchorMax = new Vector2(0.5f, 0.5f); rt0.sizeDelta = new Vector2(300, 40);
                t.outlineWidth = 0.28f; t.outlineColor = new Color32(30, 28, 34, 255);
                L.Texts.Add(t);
            }
            L.TextsUsed++;
            t.gameObject.SetActive(true);
            t.fontSize = size; t.color = c;
            Heb.Set(t, s);
            t.rectTransform.anchoredPosition = ToMap(world, L.Size) + offset;
            t.transform.SetAsLastSibling();
        }

        void DrawMarkers(Layer layer)
        {
            _cur = layer; layer.Used = 0; layer.TextsUsed = 0;
            foreach (var c in Collectible.All) Marker(_circle, c.IsCoin ? Palette.CoinGold : Palette.Bone, c.transform.position, 7f);
            foreach (var l in Landmark.All)
            {
                bool home = l.Type == Landmark.Kind.Home;
                Marker(_square, home ? Palette.WallWhite : Palette.UiPaper, l.transform.position, home ? 18f : 10f, 45f);
                Text(l.Label, l.transform.position, home ? Palette.CoinGold : Palette.UiPaper, home ? 26f : 22f, new Vector2(0, 20));
            }
            foreach (var p in Parrot.All) Marker(_circle, p.Chased ? new Color(Palette.ParrotGreen.r, Palette.ParrotGreen.g, Palette.ParrotGreen.b, 0.35f) : Palette.ParrotGreen, p.transform.position, 12f);
            foreach (var d in NpcDog.All)
            {
                var c = d.Met ? new Color(Palette.DogBrown.r, Palette.DogBrown.g, Palette.DogBrown.b, 0.4f) : Palette.DogBrown;
                Marker(_circle, Palette.UiPaper, d.transform.position, 22f);
                Marker(_circle, c, d.transform.position, 17f);
                if (!d.Met) Text(d.DogName, d.transform.position, Palette.UiPaper, 20f, new Vector2(0, -20));
            }
            foreach (var h in HostileDog.All)
            {
                Marker(_square, Palette.UiInk, h.transform.position, 24f, 45f);
                Marker(_square, h.Defeated ? Palette.DogGray : Palette.BullyRed, h.transform.position, 18f, 45f);
                Text(h.DogName, h.transform.position, h.Defeated ? Palette.Silver : Palette.BullyRed, 22f, new Vector2(0, -22));
            }
            foreach (var s in MiniGameSpot.All)
            {
                int best = s.BestMedal;
                Marker(_star, Palette.UiInk, s.transform.position, 44f);
                Marker(_star, best > 0 ? MiniGameSpot.MedalColor(best) : Palette.UiPaper, s.transform.position, 38f);
                Marker(_star, s.Accent, s.transform.position, 28f);
                Text(s.Title, s.transform.position, s.Accent, 25f, new Vector2(0, -30));
            }
            foreach (var v in TrafficVehicle.All)
            {
                Marker(_square, Palette.UiInk, v.transform.position, v.IsCart ? 13f : 17f, -v.transform.eulerAngles.y);
                Marker(_square, v.IsCart ? Palette.WallWhite : Palette.Silver, v.transform.position, v.IsCart ? 9f : 12f, -v.transform.eulerAngles.y);
            }
            if (Family.I != null)
                foreach (var f in Family.I.Members)
                {
                    Marker(_circle, Palette.UiInk, f.transform.position, 20f);
                    Marker(_circle, Palette.MiniSky, f.transform.position, 15f);
                }
            var m = MailyController.I;
            if (m != null)
            {
                float pulse = 1f + 0.15f * Mathf.Sin(Time.unscaledTime * 6f);
                Marker(_tri, Palette.UiInk, m.transform.position, 40f * pulse, -m.transform.eulerAngles.y);
                Marker(_tri, Palette.CoinGold, m.transform.position, 30f * pulse, -m.transform.eulerAngles.y);
            }
            for (int i = layer.Used; i < layer.Pool.Count; i++) layer.Pool[i].gameObject.SetActive(false);
            for (int i = layer.TextsUsed; i < layer.Texts.Count; i++) layer.Texts[i].gameObject.SetActive(false);
            _cur = null;
        }
    }
}

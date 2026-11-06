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
    /// dararot, treats and mini-games, with a legend. The world is frozen while the map is open.
    /// </summary>
    public class MapView : MonoBehaviour
    {
        public float Extent = 640f; // half-width of the mapped square, metres

        RectTransform _root, _mapRect;
        RawImage _image;
        RenderTexture _rt;
        Sprite _square, _tri, _circle;
        readonly List<Image> _pool = new List<Image>();
        readonly List<TextMeshProUGUI> _labels = new List<TextMeshProUGUI>();
        int _used, _labelsUsed;
        const float MapSize = 940f;

        void Start()
        {
            _square = Hud.MakeSprite(false); _tri = Hud.MakeSprite(true); _circle = MakeCircle();
            Build();
            _root.gameObject.SetActive(false);
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
            cam.Render();
            foreach (var r in hidden) r.enabled = true;
            RenderSettings.fog = fog;
            cam.targetTexture = null;
            Destroy(go);
            _image.texture = _rt;
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
            _root = (RectTransform)go.transform;

            Rect("Dim", _root, new Color(Palette.UiInk.r, Palette.UiInk.g, Palette.UiInk.b, 0.97f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var frame = Rect("Frame", _root, Palette.UiPaper, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-230, -10), new Vector2(MapSize + 16, MapSize + 16));
            _mapRect = Rect("Map", frame, Color.clear, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(MapSize, MapSize));
            _image = _mapRect.gameObject.AddComponent<RawImage>(); _image.color = Color.white; _image.raycastTarget = false;
            // markers live in their own layer above the image
            var layer = Rect("Markers", _mapRect, Color.clear, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _mapRect = layer;

            var title = Label(_root, "מפת קיבוץ חצור", 54, Palette.CoinGold, TextAlignmentOptions.Center);
            var trt = title.rectTransform; trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f); trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(-230, -2); trt.sizeDelta = new Vector2(900, 60);

            // legend (right side, RTL)
            var legend = Rect("Legend", _root, Palette.UiPanel, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-40, 0), new Vector2(440, 700));
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
                (_square, Palette.MiniSky, "משחקון"),
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
            var hint = Label(legend, "M או Esc לסגירה", 26, new Color(1, 1, 1, 0.6f), TextAlignmentOptions.Center);
            hint.rectTransform.anchorMin = new Vector2(0, 0); hint.rectTransform.anchorMax = new Vector2(1, 0); hint.rectTransform.pivot = new Vector2(0.5f, 0);
            hint.rectTransform.sizeDelta = new Vector2(0, 50); hint.rectTransform.anchoredPosition = new Vector2(0, 14);
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

            if (_root.gameObject.activeSelf != gm.MapOpen) _root.gameObject.SetActive(gm.MapOpen);
            if (gm.MapOpen) DrawMarkers();
        }

        Vector2 ToMap(Vector3 w) => new Vector2(w.x / (2f * Extent), w.z / (2f * Extent)) * MapSize;

        Image Marker(Sprite s, Color c, Vector3 world, float size, float rot = 0f)
        {
            Image img;
            if (_used < _pool.Count) img = _pool[_used];
            else
            {
                var go = new GameObject("M", typeof(RectTransform));
                go.transform.SetParent(_mapRect, false);
                img = go.AddComponent<Image>(); img.raycastTarget = false;
                var rt0 = img.rectTransform; rt0.anchorMin = rt0.anchorMax = new Vector2(0.5f, 0.5f);
                _pool.Add(img);
            }
            _used++;
            img.gameObject.SetActive(true);
            img.sprite = s; img.color = c; img.type = Image.Type.Simple;
            var rt = img.rectTransform; rt.anchoredPosition = ToMap(world); rt.sizeDelta = new Vector2(size, size); rt.localRotation = Quaternion.Euler(0, 0, rot);
            img.transform.SetAsLastSibling();
            return img;
        }

        void Text(string s, Vector3 world, Color c, float size, Vector2 offset)
        {
            TextMeshProUGUI t;
            if (_labelsUsed < _labels.Count) t = _labels[_labelsUsed];
            else
            {
                t = Label(_mapRect, "", size, c, TextAlignmentOptions.Center);
                var rt0 = t.rectTransform; rt0.anchorMin = rt0.anchorMax = new Vector2(0.5f, 0.5f); rt0.sizeDelta = new Vector2(300, 40);
                t.outlineWidth = 0.28f; t.outlineColor = new Color32(30, 28, 34, 255);
                _labels.Add(t);
            }
            _labelsUsed++;
            t.gameObject.SetActive(true);
            t.fontSize = size; t.color = c;
            Heb.Set(t, s);
            t.rectTransform.anchoredPosition = ToMap(world) + offset;
            t.transform.SetAsLastSibling();
        }

        void DrawMarkers()
        {
            _used = 0; _labelsUsed = 0;
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
                Marker(_square, best > 0 ? MiniGameSpot.MedalColor(best) : Palette.UiPaper, s.transform.position, 30f, 45f);
                Marker(_square, s.Accent, s.transform.position, 22f, 45f);
                Text(s.Title, s.transform.position, s.Accent, 24f, new Vector2(0, 26));
            }
            var m = MailyController.I;
            if (m != null)
            {
                float pulse = 1f + 0.15f * Mathf.Sin(Time.unscaledTime * 6f);
                Marker(_tri, Palette.UiInk, m.transform.position, 40f * pulse, -m.transform.eulerAngles.y);
                Marker(_tri, Palette.CoinGold, m.transform.position, 30f * pulse, -m.transform.eulerAngles.y);
            }
            for (int i = _used; i < _pool.Count; i++) _pool[i].gameObject.SetActive(false);
            for (int i = _labelsUsed; i < _labels.Count; i++) _labels[i].gameObject.SetActive(false);
        }
    }
}

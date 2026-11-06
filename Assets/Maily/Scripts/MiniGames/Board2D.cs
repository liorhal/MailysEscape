using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Maily
{
    /// <summary>Shared UI kit for the 2D (board) mini-games: a full-screen table under the mini-game HUD.</summary>
    public static class Board2D
    {
        static Sprite _round, _circle;
        public static Sprite Round => _round != null ? _round : (_round = Hud.MakeSprite(false));
        public static Sprite Circle => _circle != null ? _circle : (_circle = MakeShape(64, (x, y) => x * x + y * y <= 1f));

        /// <summary>Canvas + dim backdrop + the play table; the caller destroys the returned root.</summary>
        public static RectTransform Table(string name, Vector2 size, Color felt, out GameObject root)
        {
            root = new GameObject(name + " Canvas");
            var canvas = root.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 14;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            var r = (RectTransform)root.transform;
            Box("Dim", r, new Color(Palette.UiInk.r, Palette.UiInk.g, Palette.UiInk.b, 0.86f), Vector2.zero, Vector2.zero, Vector2.zero, Vector2.one);
            var frame = Box("Frame", r, Palette.UiPaper, new Vector2(0, -140), size + new Vector2(20, 20));
            return Box("Table", frame, felt, Vector2.zero, size);
        }

        public static RectTransform Box(string n, RectTransform parent, Color c, Vector2 pos, Vector2 size, Vector2? amin = null, Vector2? amax = null, Sprite sprite = null)
        {
            var go = new GameObject(n, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false);
            rt.anchorMin = amin ?? new Vector2(0.5f, 0.5f); rt.anchorMax = amax ?? new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            if (c.a > 0f)
            {
                var img = go.AddComponent<Image>(); img.sprite = sprite ?? Round; img.type = sprite == null ? Image.Type.Sliced : Image.Type.Simple;
                img.color = c; img.raycastTarget = false;
            }
            return rt;
        }

        public static TextMeshProUGUI Label(RectTransform parent, string s, float size, Color c, Vector2 pos, Vector2 box)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.anchoredPosition = pos; rt.sizeDelta = box;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.fontSize = size; t.color = c; t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false; t.richText = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            Heb.Set(t, s);
            return t;
        }

        /// <summary>Pointer (mouse or finger) position in the table's local space (pivot-centred), or null.</summary>
        public static Vector2? Mouse(RectTransform table)
        {
            var m = Pointer.current; if (m == null) return null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(table, m.position.ReadValue(), null, out var local);
            return local;
        }

        /// <summary>Procedural white sprite from an implicit shape in [-1,1]² (y up).</summary>
        public static Sprite MakeShape(int n, System.Func<float, float, bool> inside)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int hits = 0; // 4× supersampling for smooth edges
                    for (int s = 0; s < 4; s++)
                    {
                        float fx = ((x + 0.25f + (s & 1) * 0.5f) / n) * 2f - 1f, fy = ((y + 0.25f + (s >> 1) * 0.5f) / n) * 2f - 1f;
                        if (inside(fx, fy)) hits++;
                    }
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(hits * 255 / 4));
                }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }

        static readonly Dictionary<int, Sprite> Suits = new Dictionary<int, Sprite>();
        /// <summary>0 spades, 1 hearts, 2 diamonds, 3 clubs.</summary>
        public static Sprite Suit(int s)
        {
            if (Suits.TryGetValue(s, out var sp) && sp != null) return sp;
            bool C(float x, float y, float cx, float cy, float r) => (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r;
            bool Tri(float x, float y, Vector2 a, Vector2 b, Vector2 c)
            {
                float d1 = (x - b.x) * (a.y - b.y) - (a.x - b.x) * (y - b.y), d2 = (x - c.x) * (b.y - c.y) - (b.x - c.x) * (y - c.y), d3 = (x - a.x) * (c.y - a.y) - (c.x - a.x) * (y - a.y);
                return !(((d1 < 0) || (d2 < 0) || (d3 < 0)) && ((d1 > 0) || (d2 > 0) || (d3 > 0)));
            }
            System.Func<float, float, bool> f = s switch
            {
                1 => (x, y) => C(x, y, -0.4f, 0.32f, 0.42f) || C(x, y, 0.4f, 0.32f, 0.42f) || Tri(x, y, new Vector2(-0.8f, 0.2f), new Vector2(0.8f, 0.2f), new Vector2(0, -0.85f)),
                2 => (x, y) => Mathf.Abs(x) / 0.7f + Mathf.Abs(y) / 0.95f <= 1f,
                3 => (x, y) => C(x, y, 0, 0.42f, 0.36f) || C(x, y, -0.42f, -0.12f, 0.36f) || C(x, y, 0.42f, -0.12f, 0.36f) || Tri(x, y, new Vector2(0, 0.1f), new Vector2(-0.3f, -0.9f), new Vector2(0.3f, -0.9f)),
                _ => (x, y) => C(x, y, -0.4f, -0.12f, 0.4f) || C(x, y, 0.4f, -0.12f, 0.4f) || Tri(x, y, new Vector2(-0.78f, -0.02f), new Vector2(0.78f, -0.02f), new Vector2(0, 0.92f)) || Tri(x, y, new Vector2(0, -0.2f), new Vector2(-0.3f, -0.92f), new Vector2(0.3f, -0.92f)),
            };
            return Suits[s] = MakeShape(96, f);
        }

        public static bool Pressed(Key k) => Keyboard.current != null && Keyboard.current[k].wasPressedThisFrame;
        public static bool Held(Key k) => Keyboard.current != null && Keyboard.current[k].isPressed;
    }

    /// <summary>A transparent portrait of Maily's real head, rendered once from her 3D model.</summary>
    public static class MailyPortrait
    {
        static RenderTexture _rt;

        public static Texture Get()
        {
            if (_rt != null) return _rt;
            var m = MailyController.I; if (m == null) return null;
            var gait = m.GetComponentInChildren<MailyGait>();
            var head = gait != null && gait.Head != null ? gait.Head : m.transform;
            _rt = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32) { name = "MailyPortrait", antiAliasing = 4 };
            const int layer = 31;
            var renderers = m.GetComponentsInChildren<Renderer>();
            var old = new int[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) { old[i] = renderers[i].gameObject.layer; renderers[i].gameObject.layer = layer; }
            var go = new GameObject("PortraitCam");
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.cullingMask = 1 << layer; cam.fieldOfView = 30f; cam.nearClipPlane = 0.02f; cam.farClipPlane = 5f;
            var data = go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); data.renderPostProcessing = false;
            var fwd = m.transform.forward; var c = head.position + Vector3.up * 0.19f + fwd * 0.07f; // the head bone is the neck joint
            go.transform.position = c + fwd * 0.6f + Vector3.up * 0.05f + m.transform.right * 0.12f;
            go.transform.LookAt(c);
            cam.targetTexture = _rt; cam.Render(); cam.targetTexture = null;
            Object.Destroy(go);
            for (int i = 0; i < renderers.Length; i++) renderers[i].gameObject.layer = old[i];
            return _rt;
        }
    }
}

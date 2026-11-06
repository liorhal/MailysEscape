using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Maily
{
    /// <summary>uGUI + TextMeshPro shell built at runtime, in Hebrew (RTL, mirrored layout):
    /// Title, HUD, Pause (with volume settings), Ended.</summary>
    public class Hud : MonoBehaviour
    {
        Canvas _canvas;
        RectTransform _title, _hud, _pause, _end;
        TextMeshProUGUI _prompt, _treats, _dogs, _parrots, _bullies, _timer, _toast, _endTime, _endStats, _friendHint, _parrotHint;
        Image _fade, _friendArrow, _parrotArrow;
        CanvasGroup _toastGroup, _controlsGroup;
        float _toastT = 99f, _fadeA, _controlsT;
        GameState _shown = (GameState)(-1);
        Sprite _white, _tri;

        void Start()
        {
            _white = MakeSprite(false); _tri = MakeSprite(true);
            BuildCanvas();
            var gm = GameManager.I;
            gm.Changed += Refresh;
            gm.Toast += ShowToast;
            gm.StateChanged += s => { _fadeA = 1f; };
            Refresh();
        }

        // ------------------------------------------------------------------ build
        void BuildCanvas()
        {
            var go = new GameObject("HUD Canvas");
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>(); _canvas.renderMode = RenderMode.ScreenSpaceOverlay; _canvas.sortingOrder = 10;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem"); es.AddComponent<EventSystem>(); es.AddComponent<InputSystemUIInputModule>();
            }
            var root = (RectTransform)go.transform;

            // ---- HUD (mirrored for right-to-left reading) ----
            _hud = Panel("HUD", root, Color.clear);
            var goals = Panel("Goals", _hud, Palette.UiPanel, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-32, -32), new Vector2(430, 244), new Vector2(1, 1));
            _treats = GoalRow(goals, 0, Palette.Bone);
            _dogs = GoalRow(goals, 1, Palette.DogBrown);
            _parrots = GoalRow(goals, 2, Palette.ParrotGreen);
            _bullies = GoalRow(goals, 3, Palette.BullyRed);
            var timerBox = Panel("Timer", _hud, Palette.UiPanel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(32, -32), new Vector2(200, 72), new Vector2(0, 1));
            _timer = Text(timerBox, "0:00", 40, Palette.UiPaper, TextAlignmentOptions.Center, FontStyles.Bold);

            var hints = Panel("Hints", _hud, Color.clear, new Vector2(0, 1), new Vector2(0, 1), new Vector2(32, -118), new Vector2(400, 120), new Vector2(0, 1));
            _friendHint = HintRow(hints, 0, Palette.DogBrown, out _friendArrow);
            _parrotHint = HintRow(hints, 1, Palette.ParrotGreen, out _parrotArrow);

            // top centre: the close, high camera keeps Maily in the lower middle of the screen
            var toast = Panel("Toast", _hud, Palette.UiPanel, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -32), new Vector2(1000, 84), new Vector2(0.5f, 1));
            _toastGroup = toast.gameObject.AddComponent<CanvasGroup>(); _toastGroup.alpha = 0;
            _toast = Text(toast, "", 36, Palette.UiPaper, TextAlignmentOptions.Center, FontStyles.Bold);

            var ctl = Panel("Controls", _hud, Palette.UiPanel, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-32, 32), new Vector2(600, 130), new Vector2(1, 0));
            _controlsGroup = ctl.gameObject.AddComponent<CanvasGroup>();
            var c1 = Text(ctl, "WASD ריצה    Shift ספרינט    רווח קפיצה", 28, Palette.UiPaper, TextAlignmentOptions.Right, FontStyles.Normal);
            c1.rectTransform.anchorMin = new Vector2(0, 0.5f); c1.margin = new Vector4(22, 0, 22, 0);
            var c2 = Text(ctl, "E נביחה    M מפה גדולה    F משחקון    Esc השהיה", 28, Palette.UiPaper, TextAlignmentOptions.Right, FontStyles.Normal);
            c2.rectTransform.anchorMax = new Vector2(1, 0.5f); c2.margin = new Vector4(22, 0, 22, 0);

            // ---- Title (backdrop = the live scene) ----
            _title = Panel("Title", root, Color.clear);
            var band = Panel("Band", _title, new Color(Palette.UiInk.r, Palette.UiInk.g, Palette.UiInk.b, 0.55f), new Vector2(0, 0.5f), new Vector2(1, 0.72f), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            var t1 = Text(band, "הבריחה הגדולה של מיילי", 120, Palette.CoinGold, TextAlignmentOptions.Center, FontStyles.Bold);
            t1.rectTransform.anchorMin = new Vector2(0, 0.35f); t1.rectTransform.anchorMax = new Vector2(1, 1);
            t1.fontMaterial.EnableKeyword("UNDERLAY_ON"); t1.fontMaterial.SetColor("_UnderlayColor", new Color(0.17f, 0.16f, 0.2f, 0.8f)); t1.fontMaterial.SetFloat("_UnderlayOffsetX", 0.6f); t1.fontMaterial.SetFloat("_UnderlayOffsetY", -0.6f);
            var t2 = Text(band, "יום של כלבה בקיבוץ חצור", 44, Palette.UiPaper, TextAlignmentOptions.Center, FontStyles.Normal);
            t2.rectTransform.anchorMin = new Vector2(0, 0.05f); t2.rectTransform.anchorMax = new Vector2(1, 0.38f);
            _prompt = Text(_title, "לחצו על מקש כלשהו", 36, new Color(Palette.UiPaper.r, Palette.UiPaper.g, Palette.UiPaper.b, 0.7f), TextAlignmentOptions.Center, FontStyles.Bold);
            _prompt.characterSpacing = 12;
            _prompt.rectTransform.anchorMin = new Vector2(0, 0.22f); _prompt.rectTransform.anchorMax = new Vector2(1, 0.28f);
            var goalsLine = Text(_title, "אספו 50 חטיפים · הכירו 8 כלבים · רדפו אחרי 6 דררות · נצחו 3 כלבי רחוב", 32, Palette.UiPaper, TextAlignmentOptions.Center, FontStyles.Normal);
            goalsLine.rectTransform.anchorMin = new Vector2(0, 0.12f); goalsLine.rectTransform.anchorMax = new Vector2(1, 0.18f);

            // ---- Pause ----
            _pause = Panel("Pause", root, new Color(Palette.UiInk.r, Palette.UiInk.g, Palette.UiInk.b, 0.7f));
            var pbox = Panel("Box", _pause, Palette.UiPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620, 640), new Vector2(0.5f, 0.5f));
            var ph = Text(pbox, "הפסקה", 72, Palette.CoinGold, TextAlignmentOptions.Top, FontStyles.Bold); ph.margin = new Vector4(0, 30, 0, 0);
            Button(pbox, "המשך", new Vector2(0, 140), () => GameManager.I.TogglePause());
            Button(pbox, "התחלה מחדש", new Vector2(0, 40), () => GameManager.I.Restart());
            Button(pbox, "יציאה", new Vector2(0, -60), () => GameManager.I.Quit());
            SliderRow(pbox, "מוזיקה", new Vector2(0, -160), AudioGain.Music, v => { AudioGain.Music = v; AudioGain.Save(); });
            SliderRow(pbox, "צלילים", new Vector2(0, -230), AudioGain.Sfx, v => { AudioGain.Sfx = v; AudioGain.Save(); });

            // ---- Ended ----
            _end = Panel("Ended", root, new Color(Palette.UiInk.r, Palette.UiInk.g, Palette.UiInk.b, 0.6f));
            var ebox = Panel("Box", _end, Palette.UiPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1060, 600), new Vector2(0.5f, 0.5f));
            var eh = Text(ebox, "מיילי עשתה את היום הכי טוב בעולם!", 54, Palette.CoinGold, TextAlignmentOptions.Top, FontStyles.Bold); eh.margin = new Vector4(20, 40, 20, 0);
            _endTime = Text(ebox, "", 38, Palette.UiPaper, TextAlignmentOptions.Center, FontStyles.Bold);
            _endTime.rectTransform.anchorMin = new Vector2(0, 0.5f); _endTime.rectTransform.anchorMax = new Vector2(1, 0.62f);
            _endStats = Text(ebox, "", 32, Palette.UiPaper, TextAlignmentOptions.Center, FontStyles.Normal);
            _endStats.rectTransform.anchorMin = new Vector2(0, 0.4f); _endStats.rectTransform.anchorMax = new Vector2(1, 0.5f);
            Button(ebox, "לשחק שוב", new Vector2(0, -210), () => GameManager.I.Restart());

            // ---- fade overlay ----
            var fade = Panel("Fade", root, Palette.UiInk);
            _fade = fade.GetComponent<Image>(); _fade.raycastTarget = false;
        }

        TextMeshProUGUI GoalRow(RectTransform parent, int row, Color iconColor)
        {
            var r = Panel("Row", parent, Color.clear, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -20 - row * 54), new Vector2(0, 50), new Vector2(0.5f, 1));
            var icon = Panel("Icon", r, iconColor, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-28, 0), new Vector2(30, 30), new Vector2(1, 0.5f));
            var t = Text(r, "", 34, Palette.UiPaper, TextAlignmentOptions.Right, FontStyles.Bold);
            t.margin = new Vector4(20, 0, 80, 0);
            return t;
        }

        TextMeshProUGUI HintRow(RectTransform parent, int row, Color c, out Image arrow)
        {
            var r = Panel("Hint", parent, Palette.UiPanel, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -row * 58), new Vector2(0, 50), new Vector2(0.5f, 1));
            var a = new GameObject("Arrow", typeof(RectTransform)).GetComponent<RectTransform>();
            a.SetParent(r, false); a.anchorMin = a.anchorMax = new Vector2(1, 0.5f); a.anchoredPosition = new Vector2(-30, 0); a.sizeDelta = new Vector2(30, 30);
            arrow = a.gameObject.AddComponent<Image>(); arrow.sprite = _tri; arrow.color = c; arrow.raycastTarget = false;
            var t = Text(r, "", 28, Palette.UiPaper, TextAlignmentOptions.Right, FontStyles.Normal);
            t.margin = new Vector4(10, 0, 60, 0);
            return t;
        }

        RectTransform Panel(string name, RectTransform parent, Color c, Vector2? amin = null, Vector2? amax = null, Vector2? pos = null, Vector2? size = null, Vector2? pivot = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false);
            rt.anchorMin = amin ?? Vector2.zero; rt.anchorMax = amax ?? Vector2.one; rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos ?? Vector2.zero; rt.sizeDelta = size ?? Vector2.zero;
            if (c.a > 0f) { var img = go.AddComponent<Image>(); img.sprite = _white; img.type = Image.Type.Sliced; img.color = c; img.raycastTarget = c.a > 0.5f; }
            return rt;
        }

        TextMeshProUGUI Text(RectTransform parent, string s, float size, Color c, TextAlignmentOptions align, FontStyles style)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.fontSize = size; t.color = c; t.alignment = align; t.fontStyle = style; t.raycastTarget = false;
            t.richText = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            Heb.Set(t, s);
            return t;
        }

        void Button(RectTransform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
        {
            var b = Panel(label, parent, Palette.CoinGold, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(340, 78), new Vector2(0.5f, 0.5f));
            var btn = b.gameObject.AddComponent<Button>();
            var cb = btn.colors; cb.normalColor = Color.white; cb.highlightedColor = new Color(1f, 0.92f, 0.75f); cb.pressedColor = new Color(0.85f, 0.75f, 0.5f); cb.fadeDuration = 0.08f; btn.colors = cb;
            btn.onClick.AddListener(() => { Sfx.I?.Play2D(Sfx.I.Click, 0.8f); onClick(); });
            Text(b, label, 38, Palette.UiInk, TextAlignmentOptions.Center, FontStyles.Bold);
        }

        void SliderRow(RectTransform parent, string label, Vector2 pos, float value, UnityEngine.Events.UnityAction<float> onChange)
        {
            // RTL: label on the right, slider to its left
            var row = Panel(label, parent, Color.clear, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(500, 50), new Vector2(0.5f, 0.5f));
            var t = Text(row, label, 30, Palette.UiPaper, TextAlignmentOptions.Right, FontStyles.Bold); t.rectTransform.anchorMin = new Vector2(0.7f, 0);
            var track = Panel("Track", row, new Color(1, 1, 1, 0.25f), new Vector2(0, 0.5f), new Vector2(0.66f, 0.5f), Vector2.zero, new Vector2(0, 12), new Vector2(0.5f, 0.5f));
            var fillArea = Panel("Fill Area", track, Color.clear);
            var fill = Panel("Fill", fillArea, Palette.CoinGold);
            var handleArea = Panel("Handle Area", track, Color.clear);
            var handle = Panel("Handle", handleArea, Palette.UiPaper, null, null, null, new Vector2(30, 30));
            handle.anchorMin = handle.anchorMax = new Vector2(0, 0.5f); handle.GetComponent<Image>().raycastTarget = true;
            var s = track.gameObject.AddComponent<Slider>();
            s.direction = Slider.Direction.RightToLeft;
            s.fillRect = fill; s.handleRect = handle; s.targetGraphic = handle.GetComponent<Image>();
            s.minValue = 0; s.maxValue = 1; s.value = value; s.onValueChanged.AddListener(onChange);
            track.GetComponent<Image>().raycastTarget = true;
        }

        internal static Sprite MakeSprite(bool triangle)
        {
            const int n = 64; var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float a;
                    if (triangle)
                    {
                        float u = (x + 0.5f) / n, v = (y + 0.5f) / n; float half = (1f - v) * 0.5f;
                        a = Mathf.Clamp01((half - Mathf.Abs(u - 0.5f)) * n * 0.7f) * Mathf.Clamp01(v * n * 0.5f) * Mathf.Clamp01((0.95f - v) * n);
                    }
                    else
                    {
                        float dx = Mathf.Max(0, Mathf.Max(12 - x, x - (n - 13))), dy = Mathf.Max(0, Mathf.Max(12 - y, y - (n - 13)));
                        a = Mathf.Clamp01(12.5f - Mathf.Sqrt(dx * dx + dy * dy));
                    }
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, triangle ? Vector4.zero : new Vector4(16, 16, 16, 16));
        }

        // ------------------------------------------------------------------ runtime
        void Update()
        {
            var gm = GameManager.I; if (gm == null) return;
            if (gm.State != _shown) { _shown = gm.State; ApplyState(); }

            var k = Keyboard.current; var pad = Gamepad.current; var mouse = Mouse.current;
            if (gm.State == GameState.Title)
            {
                bool any = (k != null && k.anyKey.wasPressedThisFrame) || (pad != null && (pad.startButton.wasPressedThisFrame || pad.buttonSouth.wasPressedThisFrame)) || (mouse != null && mouse.leftButton.wasPressedThisFrame);
                if (any) { Sfx.I?.Play2D(Sfx.I.Click); gm.StartGame(); _controlsT = 0; }
                _prompt.alpha = Mathf.Lerp(0.5f, 1f, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / 1.6f));
            }
            else if (((k != null && k.escapeKey.wasPressedThisFrame) || (pad != null && pad.startButton.wasPressedThisFrame))
                     && !gm.MapOpen && gm.MapClosedFrame != Time.frameCount) gm.TogglePause();

            if (gm.State == GameState.Playing || gm.State == GameState.Paused)
            {
                Heb.Set(_timer, GameManager.FormatTime(gm.RunTime));
                _controlsT += Time.unscaledDeltaTime;
                _controlsGroup.alpha = Mathf.Clamp01((22f - _controlsT) / 2f);
                UpdateHints();
            }

            _toastT += Time.unscaledDeltaTime;
            _toastGroup.alpha = _toastT < 0.25f ? _toastT / 0.25f : Mathf.Clamp01((3.2f - _toastT) / 0.4f);
            _fadeA = Mathf.MoveTowards(_fadeA, 0f, Time.unscaledDeltaTime / 0.3f);
            _fade.color = new Color(Palette.UiInk.r, Palette.UiInk.g, Palette.UiInk.b, _fadeA * 0.85f);
        }

        void ApplyState()
        {
            var s = GameManager.I.State;
            _title.gameObject.SetActive(s == GameState.Title);
            _hud.gameObject.SetActive(s == GameState.Playing || (s == GameState.Paused && !GameManager.I.PausedFromFight));
            _pause.gameObject.SetActive(s == GameState.Paused);
            _end.gameObject.SetActive(s == GameState.Ended);
            if (s == GameState.Ended)
            {
                var gm = GameManager.I;
                Heb.Set(_endTime, $"זמן {GameManager.FormatTime(gm.RunTime)}      שיא {GameManager.FormatTime(gm.BestTime)}");
                Heb.Set(_endStats, $"{gm.Treats} חטיפים · {gm.Coins} מטבעות · {gm.DogsMet} חברים חדשים · {gm.ParrotsChased} דררות · {gm.BulliesBeaten} ניצחונות");
            }
            Refresh();
        }

        void Refresh()
        {
            var gm = GameManager.I; if (gm == null || _treats == null) return;
            Row(_treats, "חטיפים", gm.Treats, gm.TreatGoal);
            Row(_dogs, "חברים", gm.DogsMet, gm.DogGoal);
            Row(_parrots, "דררות", gm.ParrotsChased, gm.ParrotGoal);
            Row(_bullies, "כלבי רחוב", gm.BulliesBeaten, gm.BullyGoal);
        }

        static void Row(TextMeshProUGUI t, string label, int v, int goal)
        {
            bool done = v >= goal;
            t.color = done ? Palette.CoinGold : Palette.UiPaper;
            Heb.Set(t, done ? $"{label}  {goal} / {goal}  הושלם!" : $"{label}  {Mathf.Min(v, goal)} / {goal}");
        }

        void ShowToast(string msg) { Heb.Set(_toast, msg); _toastT = 0f; }

        void UpdateHints()
        {
            var m = MailyController.I; var cam = Camera.main; if (m == null || cam == null) return;
            NpcDog bestDog = null; float bd = float.MaxValue;
            foreach (var d in NpcDog.All) { if (d.Met) continue; float dist = (d.transform.position - m.transform.position).sqrMagnitude; if (dist < bd) { bd = dist; bestDog = d; } }
            Parrot bestP = null; float bp = float.MaxValue;
            foreach (var p in Parrot.All) { if (p.Chased || p.Airborne) continue; float dist = (p.transform.position - m.transform.position).sqrMagnitude; if (dist < bp) { bp = dist; bestP = p; } }
            Hint(_friendHint, _friendArrow, bestDog != null ? bestDog.transform.position : (Vector3?)null, m, cam, bestDog != null ? "חבר חדש" : "פגשת את כל החברים!");
            Hint(_parrotHint, _parrotArrow, bestP != null ? bestP.transform.position : (Vector3?)null, m, cam, bestP != null ? "דררה" : (GameManager.I.ParrotsDone ? "כל הדררות גורשו!" : "הדררות עפות..."));
        }

        static void Hint(TextMeshProUGUI t, Image arrow, Vector3? target, MailyController m, Camera cam, string label)
        {
            arrow.enabled = target.HasValue;
            if (!target.HasValue) { Heb.Set(t, label); return; }
            var d = target.Value - m.transform.position; d.y = 0;
            float ang = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg - cam.transform.eulerAngles.y;
            arrow.rectTransform.localRotation = Quaternion.Euler(0, 0, -ang);
            Heb.Set(t, $"{label}  {Mathf.RoundToInt(d.magnitude)} מטר");
        }
    }
}

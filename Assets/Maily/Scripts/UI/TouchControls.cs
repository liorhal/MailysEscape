using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.UI;

namespace Maily
{
    /// <summary>
    /// Phone / tablet controls. On-screen controls that act as a virtual gamepad (the game already speaks
    /// gamepad everywhere): a joystick for walking, a drag area for the camera, and four action buttons whose
    /// labels follow what they do right now (exploring, fighting, the obstacle chase). Map and pause sit at the
    /// top. Shown on mobile browsers, or as soon as the screen is touched. Board games (solitaire, memory)
    /// are played by tapping the cards directly.
    /// </summary>
    public class TouchControls : MonoBehaviour
    {
        public static TouchControls I { get; private set; }
        public static bool Active => I != null && I._on;

        bool _on;
        RectTransform _root, _stick, _look, _actions, _top;
        TextMeshProUGUI _south, _west, _north, _east;
        GameObject _bark2;
        Sprite _circle;

        void Awake() => I = this;

        void Start()
        {
            _on = Application.isMobilePlatform;
            Build();
        }

        void Build()
        {
            _circle = Board2D.Circle;
            var go = new GameObject("Touch Canvas"); go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 40;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            _root = (RectTransform)go.transform;

            // camera drag area: the right half of the screen (behind the buttons)
            _look = Area("Look", _root, new Vector2(0.5f, 0f), new Vector2(1f, 0.82f));
            Control<OnScreenStick>(_look, "<Gamepad>/rightStick", s => s.movementRange = 160f);

            // joystick, bottom-left (on phones the minimap moves up to make room)
            var stickBase = Disc("Stick base", _root, new Vector2(0f, 0f), new Vector2(250, 250), 230f, new Color(1, 1, 1, 0.16f));
            _stick = Disc("Stick", stickBase, new Vector2(0.5f, 0.5f), Vector2.zero, 110f, new Color(1, 1, 1, 0.55f));
            Control<OnScreenStick>(_stick, "<Gamepad>/leftStick", s => s.movementRange = 85f);

            // action diamond, bottom-right
            _actions = new GameObject("Actions", typeof(RectTransform)).GetComponent<RectTransform>();
            _actions.SetParent(_root, false); _actions.anchorMin = _actions.anchorMax = new Vector2(1f, 0f); _actions.anchoredPosition = new Vector2(-250, 250);
            _south = Button(_actions, "<Gamepad>/buttonSouth", new Vector2(0, -110), Palette.CoinGold);
            _east = Button(_actions, "<Gamepad>/buttonEast", new Vector2(110, 0), Palette.MiniSky);
            _west = Button(_actions, "<Gamepad>/buttonWest", new Vector2(-110, 0), Palette.DogBrown);
            _north = Button(_actions, "<Gamepad>/buttonNorth", new Vector2(0, 110), Palette.ParrotGreen);
            var bark2 = Button(_actions, "<Gamepad>/rightShoulder", new Vector2(-150, 150), Palette.DogBrown, 90f);
            Heb.Set(bark2, "נביחה"); _bark2 = bark2.transform.parent.gameObject;

            // pause + map, top-left beside the timer (clear of the messages and the goals panel)
            _top = new GameObject("Top", typeof(RectTransform)).GetComponent<RectTransform>();
            _top.SetParent(_root, false); _top.anchorMin = _top.anchorMax = new Vector2(0f, 1f); _top.anchoredPosition = new Vector2(300, -68); // beside the timer
            Heb.Set(Button(_top, "<Gamepad>/start", new Vector2(0, 0), Palette.UiPanel, 96f), "II");
            Heb.Set(Button(_top, "<Gamepad>/select", new Vector2(108, 0), Palette.UiPanel, 96f), "מפה");
            _root.gameObject.SetActive(_on);
        }

        RectTransform Area(string n, RectTransform parent, Vector2 amin, Vector2 amax)
        {
            var rt = new GameObject(n, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false); rt.anchorMin = amin; rt.anchorMax = amax; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = rt.gameObject.AddComponent<Image>(); img.color = new Color(0, 0, 0, 0); img.raycastTarget = true;
            return rt;
        }

        RectTransform Disc(string n, RectTransform parent, Vector2 anchor, Vector2 pos, float size, Color c)
        {
            var rt = new GameObject(n, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false); rt.anchorMin = rt.anchorMax = anchor; rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(size, size);
            var img = rt.gameObject.AddComponent<Image>(); img.sprite = _circle; img.color = c; img.raycastTarget = true;
            return rt;
        }

        TextMeshProUGUI Button(RectTransform parent, string path, Vector2 pos, Color c, float size = 140f)
        {
            var b = Disc("Button " + path, parent, new Vector2(0.5f, 0.5f), pos, size, new Color(c.r, c.g, c.b, 0.72f));
            Control<OnScreenButton>(b, path, null);
            var t = Board2D.Label(b, "", size * 0.24f, Palette.UiPaper, Vector2.zero, new Vector2(size, size));
            t.fontStyle = FontStyles.Bold; t.outlineWidth = 0.2f; t.outlineColor = new Color32(30, 28, 34, 255);
            return t;
        }

        static void Control<T>(RectTransform rt, string path, System.Action<T> setup) where T : OnScreenControl
        {
            // the control path must be set while the object is inactive, before the virtual device is created
            rt.gameObject.SetActive(false);
            var c = rt.gameObject.AddComponent<T>();
            c.controlPath = path;
            setup?.Invoke(c);
            rt.gameObject.SetActive(true);
        }

        void Update()
        {
            // a first touch turns the controls on (tablets, touch laptops)
            if (!_on && Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            {
                _on = true; _root.gameObject.SetActive(true);
            }
            if (!_on) return;

            var gm = GameManager.I; if (gm == null) return;
            var mg = MiniGameManager.I;
            bool board = mg != null && mg.Board;
            bool fighting = gm.State == GameState.Fighting;
            bool runner = Finale.I != null && Finale.I.Running;
            bool world = gm.State == GameState.Playing && !gm.MapOpen && !board;
            bool play = world || fighting || runner;

            _stick.parent.gameObject.SetActive(play && !runner || board && mg.Game is MazeChase);
            _look.gameObject.SetActive(world);
            _actions.gameObject.SetActive(play);
            _top.gameObject.SetActive(gm.State != GameState.Title && gm.State != GameState.Ended && !runner && !fighting || gm.MapOpen || gm.State == GameState.Paused);
            _bark2.SetActive(fighting);

            if (fighting) { Set(_south, "קפיצה"); Set(_west, "נשיכה"); Set(_north, "זינוק"); Set(_east, "הגנה"); }
            else if (runner) { Set(_south, "קפיצה"); Set(_east, "התכופפות"); Set(_west, ""); Set(_north, ""); }
            else { Set(_south, "קפיצה"); Set(_west, "נביחה"); Set(_north, "F"); Set(_east, "ריצה"); }
            _west.transform.parent.gameObject.SetActive(!runner);
            _north.transform.parent.gameObject.SetActive(!runner);
        }

        readonly System.Collections.Generic.Dictionary<TextMeshProUGUI, string> _labels = new System.Collections.Generic.Dictionary<TextMeshProUGUI, string>();
        void Set(TextMeshProUGUI t, string s)
        {
            if (_labels.TryGetValue(t, out var old) && old == s) return;
            _labels[t] = s; Heb.Set(t, s);
        }
    }
}

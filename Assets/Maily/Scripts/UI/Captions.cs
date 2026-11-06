using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Maily
{
    /// <summary>
    /// Story captions (a subtitle bar near the bottom, shown in cutscenes too) and a status pill at the top
    /// (chase countdown, "find the front door", runner distance). Anything may call Show / Pill each frame.
    /// </summary>
    public class Captions : MonoBehaviour
    {
        public static Captions I { get; private set; }

        RectTransform _bar, _pill;
        TextMeshProUGUI _barText, _pillText;
        Image _pillBg;
        float _barT = -1f, _barHold, _pillSeen = -1f;

        void Awake() => I = this;

        void Start()
        {
            var go = new GameObject("Captions Canvas"); go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 25;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)go.transform;

            _bar = Board2D.Box("Caption", root, new Color(Palette.UiInk.r, Palette.UiInk.g, Palette.UiInk.b, 0.82f), new Vector2(0, 190), new Vector2(1000, 96), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            _barText = Board2D.Label(_bar, "", 38, Palette.UiPaper, Vector2.zero, new Vector2(960, 90));
            _barText.fontStyle = FontStyles.Bold; _barText.enableAutoSizing = true; _barText.fontSizeMin = 24; _barText.fontSizeMax = 38;
            _bar.gameObject.SetActive(false);

            _pill = Board2D.Box("Pill", root, Palette.BullyRed, new Vector2(0, -312), new Vector2(620, 64), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            _pillBg = _pill.GetComponent<Image>();
            _pillText = Board2D.Label(_pill, "", 32, Palette.UiPaper, Vector2.zero, new Vector2(600, 60));
            _pillText.fontStyle = FontStyles.Bold;
            _pill.gameObject.SetActive(false);
        }

        /// <summary>Subtitle for `seconds` (also logged, like toasts).</summary>
        public void Show(string text, float seconds = 3f)
        {
            if (_barText == null) return;
            Heb.Set(_barText, text); _barT = 0f; _barHold = seconds;
            _bar.gameObject.SetActive(true);
            Debug.Log("[Maily] " + text);
        }

        /// <summary>Status pill; call every frame while it should stay up.</summary>
        public void Pill(string text, Color? color = null)
        {
            if (_pillText == null) return;
            Heb.Set(_pillText, text);
            _pillBg.color = color ?? Palette.BullyRed;
            _pillSeen = Time.unscaledTime;
            _pill.gameObject.SetActive(true);
        }

        void Update()
        {
            if (_bar == null) return;
            var gm = GameManager.I;
            bool overlay = gm != null && (gm.State == GameState.Paused || gm.State == GameState.Ended || gm.MapOpen);
            if (_barT >= 0f)
            {
                _barT += Time.unscaledDeltaTime;
                if (_barT > _barHold || gm != null && gm.State == GameState.Ended) { _barT = -1f; _bar.gameObject.SetActive(false); }
                else _bar.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, Mathf.Clamp01(_barT / 0.15f));
            }
            if (_bar.gameObject.activeSelf && overlay) _bar.gameObject.SetActive(false);
            bool pillOn = Time.unscaledTime - _pillSeen < 0.15f && !overlay;
            if (_pill.gameObject.activeSelf != pillOn) _pill.gameObject.SetActive(pillOn);
        }
    }
}

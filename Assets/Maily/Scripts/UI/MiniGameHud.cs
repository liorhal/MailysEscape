using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Maily
{
    /// <summary>Mini-game overlay: "press F" prompt near a spot, live status + timer, hot/cold meter,
    /// 3-2-1 countdown, and the medal card. Hebrew / RTL.</summary>
    public class MiniGameHud : MonoBehaviour
    {
        RectTransform _prompt, _status, _result;
        TextMeshProUGUI _promptTitle, _promptDesc, _statusTitle, _statusLine, _timer, _countdown, _resultMedal, _resultText, _resultBonus;
        Image _warmBar, _warmBg, _resultFrame;
        CanvasGroup _countGroup;
        Sprite _white;

        void Start()
        {
            _white = Hud.MakeSprite(false);
            var go = new GameObject("MiniGame Canvas");
            go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 15;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)go.transform;

            // prompt (bottom centre, above the controls box)
            _prompt = Rect("Prompt", root, Palette.UiPanel, new Vector2(0.5f, 0), new Vector2(0, 190), new Vector2(900, 118));
            _promptTitle = Text(_prompt, 40, Palette.CoinGold); _promptTitle.rectTransform.anchorMin = new Vector2(0, 0.5f); _promptTitle.fontStyle = FontStyles.Bold;
            _promptDesc = Text(_prompt, 28, Palette.UiPaper); _promptDesc.rectTransform.anchorMax = new Vector2(1, 0.5f);

            // live status (top centre, under the toast)
            _status = Rect("Status", root, Palette.UiPanel, new Vector2(0.5f, 1), new Vector2(0, -130), new Vector2(760, 150));
            _statusTitle = Text(_status, 34, Palette.CoinGold); _statusTitle.rectTransform.anchorMin = new Vector2(0, 0.66f); _statusTitle.fontStyle = FontStyles.Bold;
            _statusLine = Text(_status, 30, Palette.UiPaper); _statusLine.rectTransform.anchorMin = new Vector2(0, 0.36f); _statusLine.rectTransform.anchorMax = new Vector2(1, 0.68f);
            _timer = Text(_status, 34, Palette.UiPaper); _timer.rectTransform.anchorMax = new Vector2(1, 0.36f);
            _warmBg = Rect("Warm", _status, new Color(1, 1, 1, 0.15f), new Vector2(0.5f, 0), new Vector2(0, 18), new Vector2(560, 22)).GetComponent<Image>();
            _warmBar = Rect("Fill", (RectTransform)_warmBg.transform, Palette.MiniSky, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560, 22)).GetComponent<Image>();
            _warmBar.type = Image.Type.Filled; _warmBar.fillMethod = Image.FillMethod.Horizontal; _warmBar.fillOrigin = (int)Image.OriginHorizontal.Right;

            // countdown
            var cd = Rect("Countdown", root, Color.clear, new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(600, 300));
            _countGroup = cd.gameObject.AddComponent<CanvasGroup>();
            _countdown = Text(cd, 200, Palette.CoinGold); _countdown.fontStyle = FontStyles.Bold;

            // medal card
            _result = Rect("Result", root, Palette.UiPanel, new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(820, 300));
            _resultFrame = _result.GetComponent<Image>();
            _resultMedal = Text(_result, 64, Palette.CoinGold); _resultMedal.rectTransform.anchorMin = new Vector2(0, 0.6f); _resultMedal.fontStyle = FontStyles.Bold;
            _resultText = Text(_result, 32, Palette.UiPaper); _resultText.rectTransform.anchorMin = new Vector2(0, 0.32f); _resultText.rectTransform.anchorMax = new Vector2(1, 0.6f);
            _resultBonus = Text(_result, 30, Palette.CoinGold); _resultBonus.rectTransform.anchorMax = new Vector2(1, 0.32f);
        }

        RectTransform Rect(string n, RectTransform parent, Color c, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(n, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, anchor.y >= 1f ? 1f : anchor.y <= 0f ? 0f : 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            if (c.a > 0f) { var img = go.AddComponent<Image>(); img.sprite = _white; img.type = Image.Type.Sliced; img.color = c; img.raycastTarget = false; }
            return rt;
        }

        TextMeshProUGUI Text(RectTransform parent, float size, Color c)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.fontSize = size; t.color = c; t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false; t.richText = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            Heb.Set(t, "");
            return t;
        }

        void Update()
        {
            var mg = MiniGameManager.I; var gm = GameManager.I;
            bool world = gm != null && gm.State == GameState.Playing && !gm.MapOpen;
            bool prompt = world && mg != null && mg.Current == MiniGameManager.Phase.Idle && mg.NearSpot != null;
            bool status = world && mg != null && mg.Busy;
            bool counting = status && mg.Current == MiniGameManager.Phase.Countdown;
            bool result = world && mg != null && mg.Current == MiniGameManager.Phase.Results;
            _prompt.gameObject.SetActive(prompt);
            _status.gameObject.SetActive(status);
            _countGroup.gameObject.SetActive(counting);
            _result.gameObject.SetActive(result);

            if (prompt)
            {
                var s = mg.NearSpot; int best = s.BestMedal;
                Heb.Set(_promptTitle, $"F {s.Title}");
                Heb.Set(_promptDesc, best > 0 ? $"{s.Description}  ·  שיא: {MiniGameSpot.MedalName(best)}" : s.Description);
            }
            if (status)
            {
                var g = mg.Game;
                Heb.Set(_statusTitle, mg.CurrentSpot.Title);
                Heb.Set(_statusLine, g.Status);
                _timer.gameObject.SetActive(g.ShowTimer);
                if (g.ShowTimer) { Heb.Set(_timer, Mathf.Max(0, Mathf.CeilToInt(g.TimeLeft)).ToString()); _timer.color = g.TimeLeft < 6f ? Palette.BullyRed : Palette.UiPaper; }
                bool warm = g.Warmth >= 0f;
                _warmBg.gameObject.SetActive(warm);
                if (warm) { _warmBar.fillAmount = g.Warmth; _warmBar.color = Color.Lerp(Palette.MiniSky, Palette.BullyRed, g.Warmth); }
            }
            if (counting)
            {
                float t = mg.PhaseTime; int n = Mathf.CeilToInt(3f - t);
                Heb.Set(_countdown, n > 0 ? n.ToString() : "!");
                float frac = t - Mathf.Floor(t);
                _countdown.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.5f, 1f, Mathf.Clamp01(frac / 0.2f));
                _countGroup.alpha = 1f - Mathf.Clamp01((frac - 0.7f) / 0.3f);
            }
            if (result)
            {
                int m = mg.LastMedal;
                Heb.Set(_resultMedal, m > 0 ? $"מדליית {MiniGameSpot.MedalName(m)}!" : "בפעם הבאה!");
                _resultMedal.color = m > 0 ? MiniGameSpot.MedalColor(m) : Palette.UiPaper;
                Heb.Set(_resultText, mg.Game != null ? mg.Game.ResultText : "");
                Heb.Set(_resultBonus, (mg.LastBonus > 0 ? $"+{mg.LastBonus} חטיפים בונוס" : "") + (mg.LastNewBest ? "   ·   שיא חדש!" : ""));
                var c = m > 0 ? MiniGameSpot.MedalColor(m) : Palette.UiInk;
                _resultFrame.color = new Color(Palette.UiInk.r, Palette.UiInk.g, Palette.UiInk.b, 0.92f);
                _result.localScale = Vector3.one * Mathf.Lerp(1.12f, 1f, Mathf.Clamp01(mg.PhaseTime / 0.2f));
            }
        }
    }
}

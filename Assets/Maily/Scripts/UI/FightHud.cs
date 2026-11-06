using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Maily
{
    /// <summary>Fight overlay: health bars (Maily right, opponent left), names, round pips, clock,
    /// big centre calls with a punch-in, bark-ready indicator and a controls card. Hebrew / RTL.</summary>
    public class FightHud : MonoBehaviour
    {
        RectTransform _root;
        Image _mFill, _mTrail, _eFill, _eTrail, _barkFill;
        Image[] _mPips = new Image[2], _ePips = new Image[2];
        TextMeshProUGUI _mName, _eName, _clock, _call, _barkLabel;
        CanvasGroup _group, _callGroup, _controlsGroup;
        float _callT, _callDur, _callScale = 1f, _controlsT;
        Sprite _white;
        int _fights;

        void Start()
        {
            _white = Hud.MakeSprite(false);
            Build();
            var fm = FightManager.I;
            fm.Announce += (text, dur, emphasis) => { Heb.Set(_call, text); _callT = 0f; _callDur = dur; _callScale = emphasis; };
            fm.FightStarted += () => { _fights++; _controlsT = 0f; Refresh(true); };
            _root.gameObject.SetActive(false);
        }

        void Build()
        {
            var go = new GameObject("Fight Canvas");
            go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 20;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            _root = (RectTransform)go.transform;
            _group = go.AddComponent<CanvasGroup>(); _group.blocksRaycasts = false;

            // health bars: Maily on the right (her side of the arena), opponent on the left
            Bar(true, out _mFill, out _mTrail, out _mName, _mPips);
            Bar(false, out _eFill, out _eTrail, out _eName, _ePips);

            var clockBox = Rect("Clock", _root, Palette.UiPanel, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(150, 96), new Vector2(0.5f, 1));
            _clock = Text(clockBox, "45", 64, Palette.UiPaper, TextAlignmentOptions.Center);

            // bark special indicator under Maily's bar
            var bark = Rect("Bark", _root, Palette.UiPanel, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -150), new Vector2(330, 40), new Vector2(1, 1));
            _barkFill = Rect("Fill", bark, Palette.CoinGold, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(1, 0.5f)).GetComponent<Image>();
            _barkFill.type = Image.Type.Filled; _barkFill.fillMethod = Image.FillMethod.Horizontal; _barkFill.fillOrigin = (int)Image.OriginHorizontal.Right;
            _barkFill.color = new Color(Palette.CoinGold.r, Palette.CoinGold.g, Palette.CoinGold.b, 0.55f);
            _barkLabel = Text(bark, "", 24, Palette.UiPaper, TextAlignmentOptions.Center);

            // centre call
            var call = Rect("Call", _root, Color.clear, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 80), new Vector2(0, 220), new Vector2(0.5f, 0.5f));
            _callGroup = call.gameObject.AddComponent<CanvasGroup>(); _callGroup.alpha = 0f;
            _call = Text(call, "", 130, Palette.CoinGold, TextAlignmentOptions.Center);
            _call.fontStyle = FontStyles.Bold;
            _call.fontMaterial.EnableKeyword("UNDERLAY_ON"); _call.fontMaterial.SetColor("_UnderlayColor", new Color(0.1f, 0.08f, 0.1f, 0.9f));
            _call.fontMaterial.SetFloat("_UnderlayOffsetX", 0.8f); _call.fontMaterial.SetFloat("_UnderlayOffsetY", -0.8f); _call.fontMaterial.SetFloat("_UnderlaySoftness", 0.2f);

            // controls card
            var ctl = Rect("Controls", _root, Palette.UiPanel, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(1180, 118), new Vector2(0.5f, 0));
            _controlsGroup = ctl.gameObject.AddComponent<CanvasGroup>();
            var c1 = Text(ctl, "A D תזוזה    W קפיצה    S חסימה", 30, Palette.UiPaper, TextAlignmentOptions.Center); c1.rectTransform.anchorMin = new Vector2(0, 0.5f);
            var c2 = Text(ctl, "J נשיכה    K הסתערות    E נביחה מיוחדת", 30, Palette.CoinGold, TextAlignmentOptions.Center); c2.rectTransform.anchorMax = new Vector2(1, 0.5f);
        }

        void Bar(bool maily, out Image fill, out Image trail, out TextMeshProUGUI name, Image[] pips)
        {
            float side = maily ? 1f : 0f, sign = maily ? -1f : 1f;
            var frame = Rect(maily ? "MailyBar" : "EnemyBar", _root, new Color(0.08f, 0.07f, 0.09f, 0.9f),
                new Vector2(side, 1), new Vector2(side, 1), new Vector2(40 * sign, -40), new Vector2(760, 52), new Vector2(side, 1));
            var inner = Rect("Inner", frame, Color.clear, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-12, -12), new Vector2(0.5f, 0.5f));
            trail = Rect("Trail", inner, new Color(1f, 0.92f, 0.85f, 0.9f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f)).GetComponent<Image>();
            fill = Rect("Fill", inner, maily ? Palette.CoinGold : Palette.BullyRed, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f)).GetComponent<Image>();
            foreach (var img in new[] { trail, fill })
            {
                img.type = Image.Type.Filled; img.fillMethod = Image.FillMethod.Horizontal;
                // bars drain toward the outer edge, so the centre-side end moves
                img.fillOrigin = maily ? (int)Image.OriginHorizontal.Right : (int)Image.OriginHorizontal.Left;
            }
            name = Text(Rect("Name", _root, Color.clear, new Vector2(side, 1), new Vector2(side, 1), new Vector2(46 * sign, -96), new Vector2(520, 50), new Vector2(side, 1)),
                "", 40, Palette.UiPaper, maily ? TextAlignmentOptions.Right : TextAlignmentOptions.Left);
            name.fontStyle = FontStyles.Bold;
            for (int i = 0; i < 2; i++)
            {
                var pip = Rect("Pip", _root, new Color(1, 1, 1, 0.25f), new Vector2(side, 1), new Vector2(side, 1), new Vector2((760 - 40 - i * 46) * sign * -1f + (maily ? -40 : 40) * 0f, -104), new Vector2(30, 30), new Vector2(0.5f, 0.5f));
                // place pips near the centre end of each bar
                pip.anchoredPosition = new Vector2(maily ? -(40 + 760 - 24 - i * 44) : (40 + 760 - 24 - i * 44), -112);
                pip.localRotation = Quaternion.Euler(0, 0, 45);
                pips[i] = pip.GetComponent<Image>();
            }
        }

        RectTransform Rect(string n, RectTransform parent, Color c, Vector2 amin, Vector2 amax, Vector2 pos, Vector2 size, Vector2 pivot)
        {
            var go = new GameObject(n, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false);
            rt.anchorMin = amin; rt.anchorMax = amax; rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = size;
            if (c.a > 0f) { var img = go.AddComponent<Image>(); img.sprite = _white; img.type = Image.Type.Sliced; img.color = c; img.raycastTarget = false; }
            return rt;
        }

        TextMeshProUGUI Text(RectTransform parent, string s, float size, Color c, TextAlignmentOptions align)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.fontSize = size; t.color = c; t.alignment = align; t.raycastTarget = false; t.richText = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            Heb.Set(t, s);
            return t;
        }

        void Update()
        {
            var fm = FightManager.I; var gm = GameManager.I;
            bool show = fm != null && fm.Active && gm != null && (gm.State == GameState.Fighting || gm.PausedFromFight);
            if (_root.gameObject.activeSelf != show) _root.gameObject.SetActive(show);
            if (!show) return;
            Refresh(false);

            // centre call: punch in (1.4× → 1×), hold, fade
            _callT += Time.unscaledDeltaTime;
            float a = _callT < 0.12f ? _callT / 0.12f : Mathf.Clamp01((_callDur - _callT) / 0.3f);
            _callGroup.alpha = a;
            float punch = _callT < 0.18f ? Mathf.Lerp(1.5f, 1f, _callT / 0.18f) : 1f;
            _call.rectTransform.localScale = Vector3.one * punch * Mathf.Lerp(0.6f, 1f, Mathf.InverseLerp(1f, 2f, _callScale));

            _controlsT += Time.unscaledDeltaTime;
            _controlsGroup.alpha = _fights <= 1 ? Mathf.Clamp01((14f - _controlsT) / 1.5f) : Mathf.Clamp01((5f - _controlsT) / 1f);
        }

        void Refresh(bool snap)
        {
            var fm = FightManager.I; if (fm == null || fm.Maily == null) return;
            float mh = fm.Maily.Hp / fm.Maily.MaxHp, eh = fm.Enemy.Hp / fm.Enemy.MaxHp;
            _mFill.fillAmount = mh; _eFill.fillAmount = eh;
            float k = snap ? 1f : 1f - Mathf.Exp(-2.2f * Time.unscaledDeltaTime);
            _mTrail.fillAmount = Mathf.Max(mh, Mathf.Lerp(_mTrail.fillAmount, mh, k));
            _eTrail.fillAmount = Mathf.Max(eh, Mathf.Lerp(_eTrail.fillAmount, eh, k));
            // low health pulses
            _mFill.color = mh < 0.3f ? Color.Lerp(Palette.CoinGold, Palette.BullyRed, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 10f)) : Palette.CoinGold;
            Heb.Set(_mName, fm.Maily.Name);
            Heb.Set(_eName, fm.Enemy.Name);
            Heb.Set(_clock, Mathf.CeilToInt(fm.Clock).ToString());
            for (int i = 0; i < 2; i++)
            {
                _mPips[i].color = fm.MailyRounds > i ? Palette.CoinGold : new Color(1, 1, 1, 0.25f);
                _ePips[i].color = fm.EnemyRounds > i ? Palette.BullyRed : new Color(1, 1, 1, 0.25f);
            }
            float cd = fm.Maily.BarkCd;
            _barkFill.fillAmount = 1f - cd / 5f;
            Heb.Set(_barkLabel, cd <= 0f ? "נביחה מוכנה! E" : "נביחה נטענת...");
        }
    }
}

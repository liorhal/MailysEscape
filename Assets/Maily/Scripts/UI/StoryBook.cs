using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Maily
{
    /// <summary>
    /// "איפה מיילי?" — a storybook voice that narrates Maily's day as she wanders, in homage to Leah Goldberg's
    /// "איה פלוטו" (a puppy who leaves home to explore the fields and finds his way back). Every line here is
    /// original writing for this game; the book's text is not quoted. Pages appear at story beats (first friend,
    /// first parrot, far from home, the family searching, coming home) and never interrupt fights or board games.
    /// </summary>
    public class StoryBook : MonoBehaviour
    {
        enum Beat { Start, Friend, Parrot, Far, Cart, Medal, Bully, Search, Half, End }

        static readonly Dictionary<Beat, string> Pages = new Dictionary<Beat, string>
        {
            { Beat.Start,  "בוקר אחד בקיבוץ חצור נשאר השער פתוח,\nומיילי, כלבה סקרנית, יצאה לטייל לבד. רק לרגע, רק עד הפינה." },
            { Beat.Friend, "ליד הגדר עמד כלב זר ורחרח באוויר.\n\"אני מיילי,\" כשכשה בזנב, ומאותו רגע הם היו חברים." },
            { Beat.Parrot, "דררה ירוקה צווחה מעל הדשא: \"קרי! קרי!\"\nמיילי קפצה, והדררה עפה אל הדקל וצחקה מלמעלה." },
            { Beat.Far,    "השבילים נגמרו, ומעבר לגדר נפתחו השדות.\nמיילי הביטה לאחור: הבית כבר קטן, קטן מאוד." },
            { Beat.Cart,   "קלנועית חלפה על פניה וצפצפה \"טיט-טיט!\"\n\"לאן, כלבלבה?\" חייך הסבא בכובע הטמבל, \"הבית שלך בכיוון ההוא!\"" },
            { Beat.Medal,  "בקיבוץ תמיד יש משחק למי שמחפש,\nומיילי שיחקה וזכתה, והזנב לא הפסיק לכשכש." },
            { Beat.Bully,  "כלב רחוב גדול חסם את הדרך ונהם בקול עבה,\nאבל מיילי לא ברחה, והוא הלך, מבויש, אל הצד השני של הכביש." },
            { Beat.Search, "ובבית? הקערה מלאה, והמלונה ריקה.\n\"איפה מיילי? מי ראה את מיילי?\" שואלים כולם ברחוב." },
            { Beat.Half,   "הבטן מלאה בחטיפים, והכפות כבר עייפות,\nוהשמש נמוכה מעל הברושים, אדומה ועגולה." },
            { Beat.End,    "בערב, כשנדלקו הפנסים בשבילים, חזרה מיילי הביתה.\n\"הנה היא! הנה מיילי!\" והיא נרדמה במלונה, וחלמה על דררות." },
        };

        readonly HashSet<Beat> _told = new HashSet<Beat>();
        readonly Queue<Beat> _queue = new Queue<Beat>();
        RectTransform _page; CanvasGroup _group; TextMeshProUGUI _head, _body;
        float _show = -1f, _playTime; int _pageNo;
        Vector3 _home; bool _haveHome;
        const float Hold = 8.5f;

        void Start()
        {
            var go = new GameObject("Story Canvas"); go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 20;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)go.transform;

            _page = Board2D.Box("Page", root, new Color(Palette.UiInk.r, Palette.UiInk.g, Palette.UiInk.b, 0.55f), new Vector2(0, -128), new Vector2(1010, 168), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            _page.pivot = new Vector2(0.5f, 1f);
            Board2D.Box("Spine", _page, Palette.BullyRed, new Vector2(493, 0), new Vector2(14, 168));   // a book's red spine on the right (RTL)
            _group = _page.gameObject.AddComponent<CanvasGroup>();
            _head = Board2D.Label(_page, "", 22, Palette.CoinGold, new Vector2(-10, 58), new Vector2(940, 30));
            _head.alignment = TextAlignmentOptions.Right;
            _body = Board2D.Label(_page, "", 29, Palette.UiPaper, new Vector2(-16, -14), new Vector2(940, 100));
            _body.enableAutoSizing = true; _body.fontSizeMin = 22; _body.fontSizeMax = 29;
            _body.alignment = TextAlignmentOptions.Right; _body.lineSpacing = 12f;
            _page.gameObject.SetActive(false);

            foreach (var l in Landmark.All) if (l.Type == Landmark.Kind.Home) { _home = l.transform.position; _haveHome = true; }
        }

        void Tell(Beat b) { if (_told.Add(b)) _queue.Enqueue(b); }

        void Update()
        {
            var gm = GameManager.I; var m = MailyController.I;
            if (gm == null || m == null) return;
            if (gm.State == GameState.Title) return;
            if (HouseInterior.I != null && HouseInterior.I.Active) { if (_show >= 0f) { _show = -1f; _page.gameObject.SetActive(false); } return; } // inside: no story pages

            if (gm.State == GameState.Playing) _playTime += Time.deltaTime;
            if (_playTime > 1.2f && !(Family.I != null && Family.I.IntroActive)) Tell(Beat.Start);
            if (gm.DogsMet >= 1) Tell(Beat.Friend);
            if (gm.ParrotsChased >= 1) Tell(Beat.Parrot);
            if (_haveHome && Vector3.Distance(m.transform.position, _home) > 230f) Tell(Beat.Far);
            foreach (var v in TrafficVehicle.All)
                if (v.IsCart && Vector3.Distance(v.transform.position, m.transform.position) < 6f) { Tell(Beat.Cart); break; }
            var mg = MiniGameManager.I;
            if (mg != null && mg.Current == MiniGameManager.Phase.Results && mg.LastMedal > 0 && mg.PhaseTime > 3.5f) Tell(Beat.Medal);
            if (gm.BulliesBeaten >= 1) Tell(Beat.Bully);
            if (_playTime > 240f) Tell(Beat.Search);
            if (gm.Treats >= Mathf.Max(1, gm.TreatGoal / 2)) Tell(Beat.Half);
            // the endings carry their own closing text (the end screen), so no bedtime page here

            // only between the action: no fights, board games, map or pause
            bool quiet = gm.State == GameState.Playing && !gm.MapOpen && (mg == null || mg.Current == MiniGameManager.Phase.Idle);
            bool ending = gm.State == GameState.Ended;
            if (_show < 0f && quiet && _queue.Count > 0) Show(_queue.Dequeue());

            if (_show >= 0f)
            {
                _show += Time.unscaledDeltaTime;
                bool hold = ending && _told.Contains(Beat.End);
                float a = Mathf.Clamp01(_show / 0.5f) * (hold ? 1f : Mathf.Clamp01((Hold - _show) / 0.7f));
                if (!quiet && !ending) a = Mathf.Min(a, 0f); // something started: get out of the way
                _group.alpha = a;
                _page.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, Mathf.Clamp01(_show / 0.3f));
                if ((!hold && _show > Hold) || (!quiet && !ending)) { _show = -1f; _page.gameObject.SetActive(false); }
            }
        }

        void Show(Beat b)
        {
            _pageNo++;
            Heb.Set(_head, $"איפה מיילי?   ·   עמוד {_pageNo}");
            Heb.Set(_body, Pages[b]);
            _page.anchoredPosition = new Vector2(0, b == Beat.End ? -40 : -128);
            _page.gameObject.SetActive(true); _show = 0f; _group.alpha = 0f;
            Sfx.I?.Play2D(Sfx.I.Page, 0.55f);
        }
    }
}

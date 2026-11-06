using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Maily
{
    public enum GameState { Title, Playing, Paused, Ended, Fighting, Cutscene }

    /// <summary>One state machine for the whole shell + the three goals of Maily's escape.</summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager I { get; private set; }

        [Header("Goals")]
        public int TreatGoal = 50;
        public int DogGoal = 8;
        public int ParrotGoal = 6;
        public int BullyGoal = 3;

        public GameState State { get; private set; } = GameState.Title;
        public int BulliesBeaten { get; private set; }
        GameState _resumeState = GameState.Playing;
        public int Treats { get; private set; }
        public int Coins { get; private set; }
        public int DogsMet { get; private set; }
        public int ParrotsChased { get; private set; }
        public float RunTime { get; private set; }
        public float BestTime => PlayerPrefs.GetFloat("BestTime", 0f);

        public event Action Changed;
        public event Action<string> Toast;
        public event Action<GameState> StateChanged;

        public bool TreatsDone => Treats >= TreatGoal;
        public bool DogsDone => DogsMet >= DogGoal;
        public bool ParrotsDone => ParrotsChased >= ParrotGoal;
        public bool BulliesDone => BulliesBeaten >= BullyGoal;
        public bool InWorld => State == GameState.Playing || State == GameState.Fighting;
        public bool InCutscene => State == GameState.Cutscene;
        public bool PausedFromFight => State == GameState.Paused && _resumeState == GameState.Fighting;

        /// <summary>Overview map: freezes the world while open (only from free exploration).</summary>
        public bool MapOpen { get; private set; }
        public int MapClosedFrame { get; private set; } = -1; // so the same Esc press doesn't also open Pause
        public void ToggleMap()
        {
            if (MapOpen) { MapOpen = false; MapClosedFrame = Time.frameCount; Time.timeScale = 1f; Changed?.Invoke(); return; }
            if (State != GameState.Playing) return;
            MapOpen = true; Time.timeScale = 0f; Changed?.Invoke();
        }

        /// <summary>Bonus treats from mini-games count toward the treats goal.</summary>
        public void AddBonusTreats(int n)
        {
            for (int i = 0; i < n && State == GameState.Playing; i++) AddTreat(false);
        }

        void Awake()
        {
            I = this;
            Time.timeScale = 1f;
            Application.targetFrameRate = 120;
#if UNITY_WEBGL && !UNITY_EDITOR
            WebPerformance();
            if (Application.absoluteURL.Contains("mute=1")) AudioListener.volume = 0f; // silent test runs
#endif
        }

        /// <summary>Browser build: lighter rendering so laptops with built-in graphics stay smooth.</summary>
        static void WebPerformance()
        {
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset urp)
            {
                urp.renderScale = Application.isMobilePlatform ? 0.65f : 0.8f;
                urp.shadowDistance = 45f;
                urp.msaaSampleCount = 1;
                urp.shadowCascadeCount = 1;
            }
            QualitySettings.lodBias = 0.7f;
        }

        void Start() => SetState(GameState.Title);

        void Update()
        {
            if (InWorld) RunTime += Time.deltaTime;
        }

        public void StartGame()
        {
            if (State != GameState.Title) return;
            SetState(GameState.Playing);
            Say("מיילי חמקה משער הגינה! צאו לטייל בקיבוץ חצור");
        }

        public void TogglePause()
        {
            if (InWorld) { _resumeState = State; SetState(GameState.Paused); }
            else if (State == GameState.Paused) SetState(_resumeState);
        }

        // ---- street-dog fights ----
        public void EnterFight() { if (State == GameState.Playing) SetState(GameState.Fighting); }
        public void ExitFight() { if (State == GameState.Fighting) SetState(GameState.Playing); }

        public void BeatBully(string name)
        {
            BulliesBeaten++;
            Say(BulliesBeaten == BullyGoal ? $"מיילי ניצחה את {name}! אף כלב רחוב לא יציק לה יותר!" : $"מיילי ניצחה את {name}!");
            Sfx.I?.Play2D(Sfx.I.Sting, 0.9f);
            Changed?.Invoke(); CheckWin();
        }

        public void LostFight(string name) => Say($"{name} ניצח הפעם... מיילי חוזרת הביתה להתאושש");

        public void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void SetState(GameState s)
        {
            State = s;
            MapOpen = false;
            Time.timeScale = s == GameState.Paused ? 0f : 1f;
            Debug.Log($"[Maily] State -> {s}");
            StateChanged?.Invoke(s);
            Changed?.Invoke();
        }

        public void AddTreat(bool coin)
        {
            if (State != GameState.Playing) return;
            Treats++; if (coin) Coins++;
            if (Treats == TreatGoal) { Say("כל החטיפים! כלבה טובה, מיילי!"); Sfx.I?.Play2D(Sfx.I.Sting, 0.8f); }
            Changed?.Invoke(); CheckWin();
        }

        public void MetDog(string name)
        {
            if (State != GameState.Playing) return;
            DogsMet++;
            Say(DogsMet == DogGoal ? $"מיילי הכירה את {name}. עכשיו היא מכירה את כל כלבי הקיבוץ!" : $"מיילי הכירה חבר חדש: {name}!");
            if (DogsMet == DogGoal) Sfx.I?.Play2D(Sfx.I.Sting, 0.8f);
            Changed?.Invoke(); CheckWin();
        }

        public void ChasedParrot()
        {
            if (State != GameState.Playing) return;
            ParrotsChased++;
            Say(ParrotsChased == ParrotGoal ? "הדררות לא ישכחו את מיילי!" : "דררה ברחה בצווחות!");
            if (ParrotsChased == ParrotGoal) Sfx.I?.Play2D(Sfx.I.Sting, 0.8f);
            Changed?.Invoke(); CheckWin();
        }

        public void Say(string msg) { Debug.Log("[Maily] " + msg); Toast?.Invoke(msg); }

        void CheckWin()
        {
            if (!(TreatsDone && DogsDone && ParrotsDone && BulliesDone)) return;
            if (State != GameState.Playing) return;
            if (BestTime <= 0f || RunTime < BestTime) { PlayerPrefs.SetFloat("BestTime", RunTime); PlayerPrefs.Save(); }
            Sfx.I?.Play2D(Sfx.I.Sting, 1f, 1.12f);
            // stage 1 done without being caught: the dogs' revenge, then the great chase
            if (Finale.I != null) { SetState(GameState.Cutscene); Finale.I.Begin(); }
            else EndGame("free", "חופשית!", "מיילי השלימה את כל המשימות.");
        }

        // ---- endings ----
        public static readonly string[] EndingIds = { "runaway", "free", "caught" };
        public string EndingId { get; private set; } = "";
        public string EndingTitle { get; private set; } = "";
        public string EndingText { get; private set; } = "";
        public int EndingsFound { get { int n = 0; foreach (var id in EndingIds) if (PlayerPrefs.GetInt("Ending_" + id, 0) == 1) n++; return n; } }

        /// <summary>Finish the game with one of the endings (see EndingIds).</summary>
        public void EndGame(string id, string title, string text)
        {
            EndingId = id; EndingTitle = title; EndingText = text;
            PlayerPrefs.SetInt("Ending_" + id, 1); PlayerPrefs.Save();
            Sfx.I?.Play2D(Sfx.I.Sting, 1f, id == "caught" ? 0.85f : 1.12f);
            SetState(GameState.Ended);
        }

        /// <summary>Cutscenes and the runner: the world keeps running, but the HUD, map and player input step aside.</summary>
        public void SetCutscene(bool on)
        {
            if (on && (State == GameState.Playing || State == GameState.Fighting)) SetState(GameState.Cutscene);
            else if (!on && State == GameState.Cutscene) SetState(GameState.Playing);
        }

        public static string FormatTime(float t) => $"{(int)(t / 60)}:{(int)(t % 60):00}";
    }
}

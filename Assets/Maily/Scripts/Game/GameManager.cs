using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Maily
{
    public enum GameState { Title, Playing, Paused, Ended }

    /// <summary>One state machine for the whole shell + the three goals of Maily's escape.</summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager I { get; private set; }

        [Header("Goals")]
        public int TreatGoal = 50;
        public int DogGoal = 8;
        public int ParrotGoal = 6;

        public GameState State { get; private set; } = GameState.Title;
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

        void Awake()
        {
            I = this;
            Time.timeScale = 1f;
            Application.targetFrameRate = 120;
        }

        void Start() => SetState(GameState.Title);

        void Update()
        {
            if (State == GameState.Playing) RunTime += Time.deltaTime;
        }

        public void StartGame()
        {
            if (State != GameState.Title) return;
            SetState(GameState.Playing);
            Say("Maily slipped out of the garden gate! Explore Kibbutz Hatzor");
        }

        public void TogglePause()
        {
            if (State == GameState.Playing) SetState(GameState.Paused);
            else if (State == GameState.Paused) SetState(GameState.Playing);
        }

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
            Time.timeScale = s == GameState.Paused ? 0f : 1f;
            Debug.Log($"[Maily] State -> {s}");
            StateChanged?.Invoke(s);
            Changed?.Invoke();
        }

        public void AddTreat(bool coin)
        {
            if (State != GameState.Playing) return;
            Treats++; if (coin) Coins++;
            if (Treats == TreatGoal) { Say("All the treats! Good girl, Maily!"); Sfx.I?.Play2D(Sfx.I.Sting, 0.8f); }
            Changed?.Invoke(); CheckWin();
        }

        public void MetDog(string name)
        {
            if (State != GameState.Playing) return;
            DogsMet++;
            Say(DogsMet == DogGoal ? $"Maily met {name} — that's every dog in the kibbutz!" : $"Maily made a friend: {name}!");
            if (DogsMet == DogGoal) Sfx.I?.Play2D(Sfx.I.Sting, 0.8f);
            Changed?.Invoke(); CheckWin();
        }

        public void ChasedParrot()
        {
            if (State != GameState.Playing) return;
            ParrotsChased++;
            Say(ParrotsChased == ParrotGoal ? "Those dararot won't forget Maily!" : "Chased a darara away! Squawk!");
            if (ParrotsChased == ParrotGoal) Sfx.I?.Play2D(Sfx.I.Sting, 0.8f);
            Changed?.Invoke(); CheckWin();
        }

        public void Say(string msg) { Debug.Log("[Maily] " + msg); Toast?.Invoke(msg); }

        void CheckWin()
        {
            if (!(TreatsDone && DogsDone && ParrotsDone)) return;
            if (BestTime <= 0f || RunTime < BestTime) { PlayerPrefs.SetFloat("BestTime", RunTime); PlayerPrefs.Save(); }
            Sfx.I?.Play2D(Sfx.I.Sting, 1f, 1.12f);
            SetState(GameState.Ended);
        }

        public static string FormatTime(float t) => $"{(int)(t / 60)}:{(int)(t % 60):00}";
    }
}

using UnityEngine;
using UnityEngine.InputSystem;

namespace Maily
{
    /// <summary>Runs one mini-game at a time: prompt near a spot → F → 3-2-1 → play → medal + bonus treats.</summary>
    public class MiniGameManager : MonoBehaviour
    {
        public static MiniGameManager I { get; private set; }

        public enum Phase { Idle, Countdown, Running, Results }
        public Phase Current { get; private set; } = Phase.Idle;
        public MiniGame Game { get; private set; }
        public MiniGameSpot CurrentSpot { get; private set; }
        public MiniGameSpot NearSpot { get; private set; }
        public float PhaseTime { get; private set; }
        public int LastMedal { get; private set; }
        public int LastBonus { get; private set; }
        public bool LastNewBest { get; private set; }

        /// <summary>A mini-game is in progress (street dogs leave Maily alone meanwhile).</summary>
        public bool Busy => Current == Phase.Countdown || Current == Phase.Running;
        /// <summary>A 2D board game is on screen: Maily stands still and the cursor is free.</summary>
        public bool Board => Busy && Game != null && Game.Is2D;

        const float NearDist = 2.6f;

        void Awake() => I = this;

        void Update()
        {
            var gm = GameManager.I; var m = MailyController.I;
            if (gm == null || m == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return; // paused / map open

            bool f = (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame) || (Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame && gm.State == GameState.Playing);

            // a fight (or the end screen) interrupts a mini-game
            if (Busy && gm.State != GameState.Playing) { Stop(false); return; }

            switch (Current)
            {
                case Phase.Idle:
                    NearSpot = null;
                    if (gm.State != GameState.Playing) break;
                    float best = NearDist;
                    foreach (var s in MiniGameSpot.All)
                    {
                        float d = Vector3.Distance(new Vector3(s.transform.position.x, 0, s.transform.position.z), new Vector3(m.transform.position.x, 0, m.transform.position.z));
                        if (d < best) { best = d; NearSpot = s; }
                    }
                    if (NearSpot != null && f) Begin(NearSpot);
                    break;
                case Phase.Countdown:
                    PhaseTime += dt;
                    int before = Mathf.CeilToInt(3f - (PhaseTime - dt)), now = Mathf.CeilToInt(3f - PhaseTime);
                    if (now != before && now > 0) Sfx.I?.Play2D(Sfx.I.Click, 1f, 1.5f);
                    if (PhaseTime >= 3f) { Current = Phase.Running; PhaseTime = 0f; Sfx.I?.Play2D(Sfx.I.Gong, 0.6f, 1.6f); }
                    break;
                case Phase.Running:
                    PhaseTime += dt;
                    if (f) Game.OnAction();
                    if (Game.Tick(dt)) Stop(true);
                    break;
                case Phase.Results:
                    PhaseTime += dt;
                    if (PhaseTime > 4f) { Current = Phase.Idle; Game = null; CurrentSpot = null; }
                    break;
            }
        }

        void Begin(MiniGameSpot spot)
        {
            CurrentSpot = spot; NearSpot = null;
            Game = spot.Game switch
            {
                MiniGameSpot.Kind.Gates => new GateRace(),
                MiniGameSpot.Kind.Frisbee => new FrisbeeCatch(),
                MiniGameSpot.Kind.Oranges => new OrangePicking(),
                MiniGameSpot.Kind.Maze => new MazeChase(),
                MiniGameSpot.Kind.Solitaire => new Solitaire(),
                MiniGameSpot.Kind.Memory => new MemoryMatch(),
                MiniGameSpot.Kind.Chickens => new ChickenHerd(),
                MiniGameSpot.Kind.Papers => new PaperRound(),
                _ => (MiniGame)new BoneHunt(),
            };
            Game.Spot = spot;
            Game.Begin();
            Current = Phase.Countdown; PhaseTime = 0f;
            Sfx.I?.Play2D(Sfx.I.Click, 1f);
            Debug.Log($"[Maily] Mini-game started: {spot.Game}");
        }

        void Stop(bool finished)
        {
            if (Game == null) { Current = Phase.Idle; return; }
            int medal = finished || Current == Phase.Running ? Game.Medal() : 0;
            Game.Cleanup();
            if (!finished && GameManager.I.State != GameState.Playing)
            {
                // interrupted (fight, end screen): no reward, quietly reset
                Current = Phase.Idle; Game = null; CurrentSpot = null; return;
            }
            LastMedal = medal;
            LastBonus = medal == 3 ? 5 : medal == 2 ? 3 : medal == 1 ? 1 : 0;
            LastNewBest = medal > CurrentSpot.BestMedal;
            if (LastNewBest) { PlayerPrefs.SetInt(CurrentSpot.BestKey, medal); PlayerPrefs.Save(); }
            Current = Phase.Results; PhaseTime = 0f;
            Sfx.I?.Play2D(medal >= 2 ? Sfx.I.Sting : Sfx.I.Heart, 1f);
            if (medal > 0) Fx.Hearts(MailyController.I.transform.position + Vector3.up * 0.9f);
            if (LastBonus > 0) GameManager.I.AddBonusTreats(LastBonus);
            Debug.Log($"[Maily] Mini-game {CurrentSpot.Game} over: medal {medal}, score {Game.Score}");
        }
    }
}

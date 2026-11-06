using System.Collections.Generic;
using UnityEngine;

namespace Maily
{
    /// <summary>Where a mini-game starts: a glowing ring + post with a floating icon. Walk in and press F.</summary>
    public class MiniGameSpot : MonoBehaviour
    {
        public enum Kind { Gates, Frisbee, Oranges, Bones, Maze, Solitaire, Memory, Chickens, Papers }
        public static readonly List<MiniGameSpot> All = new List<MiniGameSpot>();

        public Kind Game;
        public Transform Icon;
        public Renderer Ring;

        public string Title => Game switch
        {
            Kind.Gates => "מרוץ שערים",
            Kind.Frisbee => "תפיסת פריזבי",
            Kind.Oranges => "קטיף תפוזים",
            Kind.Maze => "מבוך החטיפים",
            Kind.Solitaire => "סוליטר בחדר האוכל",
            Kind.Memory => "משחק זיכרון",
            Kind.Chickens => "לול התרנגולות",
            Kind.Papers => "חלוקת עיתונים",
            _ => "חיפוש עצמות",
        };

        public string Description => Game switch
        {
            Kind.Gates => "עברו בכל השערים לפי הסדר לפני שהזמן נגמר",
            Kind.Frisbee => "רוצו לצל של הפריזבי ותפסו אותה",
            Kind.Oranges => "אספו כמה שיותר תפוזים שנופלים מהעצים",
            Kind.Maze => "משחק דו-ממדי: אכלו את כל החטיפים וברחו מהחתולים",
            Kind.Solitaire => "משחק קלפים דו-ממדי: סדרו את כל הקלפים לפי הצורות",
            Kind.Memory => "משחק דו-ממדי: מצאו את כל הזוגות בכמה שפחות תורות",
            Kind.Chickens => "החזירו את התרנגולות ללול. נביחה (E) מבריחה אותן",
            Kind.Papers => "קחו עיתון מהערימה והביאו אותו לבית המסומן",
            _ => "רחרחו לפי חם / קר וחפרו עם F",
        };

        public Color Accent => Game switch
        {
            Kind.Gates => Palette.MiniSky,
            Kind.Frisbee => Palette.MiniFrisbee,
            Kind.Oranges => Palette.Citrus,
            Kind.Maze => Palette.CoinGold,
            Kind.Solitaire => Palette.MiniCards,
            Kind.Memory => Palette.MiniMemory,
            Kind.Chickens => Palette.MiniHen,
            Kind.Papers => Palette.MiniNews,
            _ => Palette.Bone,
        };

        public string BestKey => "MiniBest_" + Game;
        /// <summary>Best medal so far: 0 none, 1 bronze, 2 silver, 3 gold.</summary>
        public int BestMedal => PlayerPrefs.GetInt(BestKey, 0);

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Update()
        {
            if (Icon == null) return;
            Icon.localPosition = new Vector3(0, 2.1f + Mathf.Sin(Time.time * 2f) * 0.12f, 0);
            Icon.localRotation = Quaternion.Euler(0, Time.time * 70f, 0);
            bool busy = MiniGameManager.I != null && MiniGameManager.I.Busy;
            if (Ring != null) Ring.enabled = !busy || MiniGameManager.I.CurrentSpot == this;
        }

        public static string MedalName(int m) => m switch { 3 => "זהב", 2 => "כסף", 1 => "ארד", _ => "" };
        public static Color MedalColor(int m) => m switch { 3 => Palette.CoinGold, 2 => Palette.Silver, 1 => Palette.Bronze, _ => Color.white };
    }
}

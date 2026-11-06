# הבריחה הגדולה של מיילי · Maily's Great Escape

**▶ לשחק בדפדפן / Play in the browser: https://liorhal.github.io/MailysEscape/**

משחק תלת-ממדי על מיילי, כלבה שבורחת מהבית ומטיילת בקיבוץ חצור. מפת הקיבוץ נבנתה מנתוני OpenStreetMap.
A 3D game about Maily, a dog who slips out of her garden gate and explores Kibbutz Hatzor, Israel, rebuilt from OpenStreetMap data. Made with Unity 6 (URP). Hebrew UI.

## איך משחקים · How to play

**שלב 1:** אספו 15 חטיפים, הכירו 2 חברים, רדפו אחרי דררה אחת ונצחו כלב רחוב אחד, בלי שהמשפחה תתפוס אתכם.
המשפחה רודפת אחרי מיילי בהתחלה: שרדו 20 שניות והם מוותרים.

| מקש | פעולה |
|---|---|
| WASD / חצים | ריצה |
| Shift | ספרינט |
| רווח | קפיצה |
| E | נביחה |
| F | משחקון (כשעומדים ליד כוכב) |
| M | מפה גדולה |
| Esc | השהיה |

**שלוש סיומות:** ברחת מהבית · חופשית! · נתפסה ברגע האחרון.

יש 9 משחקונים ברחבי הקיבוץ, בהם מבוך, סוליטר וזיכרון (דו-ממדיים), תפיסת פריזבי, קטיף תפוזים ולול התרנגולות. יש גם קרבות עם כלבי רחוב, קלנועיות ומכוניות שנוסעות בקיבוץ, ודפי סיפור בהשראת "איה פלוטו" מאת לאה גולדברג (הטקסטים מקוריים).

## Building

- Unity **6000.6.8f1** with Web Build Support.
- `Maily → Build Kibbutz Scene` regenerates the world (`Assets/Maily/Editor/GameSetup.cs`).
- `Maily → Build Web Version` builds the browser version into `Builds/Web` (`Assets/Maily/Editor/WebBuild.cs`).

The recorded sound effects and music (Pixabay) are **not** in this repository: Pixabay's licence allows them inside the game but not as standalone files. Without them the game uses its own synthesized sounds. To use recordings, drop files named after the clips (`Bark.ogg`, `Music.ogg`, …) into `Assets/Maily/Resources/Sfx/` (see `CREDITS-Pixabay.txt` there).

## Credits

- **Map data** © OpenStreetMap contributors, [ODbL](https://opendatacommons.org/licenses/odbl/)
- **3D props:** [Kenney](https://kenney.nl) Car Kit, Nature Kit, City Kit Suburban (CC0)
- **Surface textures:** [ambientCG](https://ambientcg.com) (CC0)
- **Sky:** "Kloofendal 48d Partly Cloudy (Pure Sky)" by Greg Zaal, [Poly Haven](https://polyhaven.com) (CC0)
- **Font:** Rubik ([SIL Open Font License](Assets/Maily/Fonts/Rubik-OFL.txt))
- **Sounds and music in the web build:** [Pixabay](https://pixabay.com) (Pixabay Content License); see `Assets/Maily/Resources/Sfx/CREDITS-Pixabay.txt`
- **Maily:** 3D model of the real Maily (made with Meshy AI)
- **Inspiration:** "איה פלוטו" by Leah Goldberg. All story text in the game is original.

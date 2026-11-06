using System.Text;
using TMPro;
using UnityEngine;

namespace Maily
{
    /// <summary>
    /// Hebrew text for TextMeshPro. TMP's right-to-left mode reverses the whole string, which would
    /// also flip numbers and Latin key names ("0:45" → "54:0"). Prep() pre-reverses those LTR runs so
    /// they come out the right way round, and mirrors brackets. Keep Hebrew strings free of rich-text tags.
    /// Font: Rubik (SIL Open Font License, see Assets/Maily/Fonts/Rubik-OFL.txt).
    /// </summary>
    public static class Heb
    {
        public const string FontResource = "Fonts/Rubik SDF";
        static TMP_FontAsset _font;

        public static TMP_FontAsset Font
        {
            get
            {
                if (_font == null) _font = Resources.Load<TMP_FontAsset>(FontResource);
                return _font;
            }
        }

        static bool IsLtr(char c) => (c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
        /// <summary>Bidi mirroring: in right-to-left text an opening bracket is drawn as its mirror glyph.</summary>
        static char Mirror(char c) => c switch { '(' => ')', ')' => '(', '[' => ']', ']' => '[', '{' => '}', '}' => '{', '<' => '>', '>' => '<', _ => c };

        static bool IsJoiner(char c) => c == ':' || c == '/' || c == '.' || c == ',' || c == '-' || c == '+' || c == ' ';

        /// <summary>Logical-order Hebrew → string ready for TMP with isRightToLeftText = true.</summary>
        public static string Prep(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var sb = new StringBuilder(s.Length);
            int i = 0;
            while (i < s.Length)
            {
                if (!IsLtr(s[i])) { sb.Append(Mirror(s[i])); i++; continue; }
                // extend the run across joiners only when more LTR characters follow ("1 / 50", "0:45")
                int end = i;
                int j = i + 1;
                while (j < s.Length)
                {
                    if (IsLtr(s[j])) { end = j; j++; continue; }
                    if (IsJoiner(s[j]))
                    {
                        int k = j; while (k < s.Length && IsJoiner(s[k])) k++;
                        if (k < s.Length && IsLtr(s[k]) && k - j <= 3) { j = k; continue; }
                    }
                    break;
                }
                for (int q = end; q >= i; q--) sb.Append(s[q]);
                i = end + 1;
            }
            return sb.ToString();
        }

        /// <summary>Apply Hebrew text + RTL + the Hebrew font to a TMP label.</summary>
        public static void Set(TMP_Text t, string s)
        {
            if (Font != null) t.font = Font;
            t.isRightToLeftText = true;
            t.text = Prep(s);
        }
    }
}

using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Maily.EditorTools
{
    /// <summary>Creates the dynamic TMP font asset for Rubik under Resources (Maily ▸ Create Hebrew Font).</summary>
    public static class HebrewFontSetup
    {
        const string TtfPath = "Assets/Maily/Fonts/Rubik.ttf";
        const string AssetPath = "Assets/Maily/Resources/Fonts/Rubik SDF.asset";

        [MenuItem("Maily/Create Hebrew Font")]
        public static string Ensure()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetPath);
            if (existing != null) return "Hebrew font already exists: " + AssetPath;

            var ttf = AssetDatabase.LoadAssetAtPath<Font>(TtfPath);
            if (ttf == null) return "ERROR: missing " + TtfPath;
            foreach (var f in new[] { "Assets/Maily/Resources", "Assets/Maily/Resources/Fonts" })
                if (!AssetDatabase.IsValidFolder(f)) AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(f).Replace('\\', '/'), System.IO.Path.GetFileName(f));

            var fa = TMP_FontAsset.CreateFontAsset(ttf, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            fa.name = "Rubik SDF";
            AssetDatabase.CreateAsset(fa, AssetPath);
            fa.atlasTextures[0].name = "Rubik SDF Atlas";
            AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
            fa.material.name = "Rubik SDF Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            // pre-warm the Hebrew alphabet, digits and the punctuation the game uses
            fa.TryAddCharacters("אבגדהוזחטיכךלמםנןסעפףצץקרשת0123456789:/!?.,·—-' ", out _);
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            return "Created " + AssetPath + " (" + fa.characterTable.Count + " glyphs)";
        }
    }
}

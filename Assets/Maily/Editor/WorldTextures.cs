using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Maily.EditorTools
{
    /// <summary>
    /// Puts CC0 surface textures (ambientCG, Assets/Maily/Textures) on the kibbutz materials: grass on the
    /// lawns, asphalt on the roads, pavers on footpaths, plaster on walls, terracotta tiles on roofs.
    /// The world meshes carry planar UVs in metres, so each material's tiling is "1 / metres per tile".
    /// Colours are a blend of the photo texture and the art-bible palette, so the look stays coherent.
    /// </summary>
    public static class WorldTextures
    {
        const string Dir = "Assets/Maily/Textures/";

        // material key → (texture set, metres per tile, palette weight 0 = pure photo … 1 = pure palette)
        static readonly Dictionary<string, (string tex, float metres, float palette)> Map = new Dictionary<string, (string, float, float)>
        {
            { "Field", ("Ground054", 6f, 0.45f) },
            { "Kurkar", ("Ground037", 5f, 0.4f) },
            { "Lawn", ("Grass004", 3f, 0.35f) },
            { "LawnDeep", ("Grass001", 3f, 0.35f) },
            { "Pitch", ("Grass001", 2.5f, 0.3f) },
            { "Orchard", ("Ground037", 4f, 0.4f) },
            { "Cemetery", ("Ground037", 4f, 0.4f) },
            { "Yard", ("Ground037", 4f, 0.45f) },
            { "Playground", ("Ground054", 3f, 0.4f) },
            { "Track", ("Ground054", 3f, 0.35f) },
            { "Asphalt", ("Asphalt010", 4f, 0.25f) },
            { "Parking", ("Asphalt012", 4f, 0.3f) },
            { "Industrial", ("Asphalt012", 5f, 0.4f) },
            { "Path", ("PavingStones070", 1.6f, 0.3f) },
            { "Curb", ("Concrete034", 2f, 0.3f) },
            { "PoolRim", ("Concrete034", 2f, 0.3f) },
            { "RoofFlat", ("Concrete034", 4f, 0.45f) },
            { "RoofTile", ("RoofingTiles006", 1.8f, 0.3f) },
            { "WallWhite", ("Plaster001", 2.5f, 0.6f) },
            { "WallCream", ("Plaster001", 2.5f, 0.6f) },
            { "WallOchre", ("Plaster001", 2.5f, 0.6f) },
            { "WallIndustrial", ("Plaster003", 3f, 0.6f) },
            { "WallShelter", ("Plaster003", 3f, 0.6f) },
        };

        public static string Apply()
        {
            int n = 0;
            foreach (var kv in Map)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>($"{Mats.Folder}/M_{kv.Key}.mat");
                if (mat == null) continue;
                var (tex, metres, palette) = kv.Value;
                var color = Load(tex + "_Color", false);
                var normal = Load(tex + "_NormalGL", true);
                if (color == null) continue;

                // palette colour expressed relative to the texture's average, blended with plain white
                var pal = mat.GetColor("_BaseColor");
                var avg = Average(color);
                var rel = new Color(pal.r / Mathf.Max(0.05f, avg.r), pal.g / Mathf.Max(0.05f, avg.g), pal.b / Mathf.Max(0.05f, avg.b), 1f);
                var tint = Color.Lerp(Color.white, rel, palette);
                tint.r = Mathf.Min(tint.r, 1.6f); tint.g = Mathf.Min(tint.g, 1.6f); tint.b = Mathf.Min(tint.b, 1.6f);

                mat.SetTexture("_BaseMap", color); mat.mainTexture = color;
                mat.SetColor("_BaseColor", tint);
                var scale = Vector2.one / metres;
                mat.SetTextureScale("_BaseMap", scale); mat.mainTextureScale = scale;
                if (normal != null)
                {
                    mat.SetTexture("_BumpMap", normal); mat.SetFloat("_BumpScale", 1f);
                    mat.EnableKeyword("_NORMALMAP");
                }
                EditorUtility.SetDirty(mat);
                n++;
            }
            AssetDatabase.SaveAssets();
            return $"Textured {n} world materials (ambientCG CC0)";
        }

        static Texture2D Load(string name, bool normal)
        {
            var path = Dir + name + ".jpg";
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return null;
            var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            bool readable = !normal;
            if (imp.textureType != type || imp.wrapMode != TextureWrapMode.Repeat || imp.anisoLevel != 8 || imp.isReadable != readable)
            {
                imp.textureType = type; imp.wrapMode = TextureWrapMode.Repeat; imp.anisoLevel = 8;
                imp.mipmapEnabled = true; imp.sRGBTexture = !normal; imp.isReadable = readable;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Color Average(Texture2D t)
        {
            var px = t.GetPixels32(Mathf.Min(4, t.mipmapCount - 1));
            double r = 0, g = 0, b = 0;
            foreach (var p in px) { r += p.r; g += p.g; b += p.b; }
            double k = 255.0 * Mathf.Max(1, px.Length);
            // textures are sRGB; compare in the same (gamma) space as the palette colours
            return new Color((float)(r / k), (float)(g / k), (float)(b / k));
        }
    }
}

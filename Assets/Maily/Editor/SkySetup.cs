using UnityEditor;
using UnityEngine;

namespace Maily.EditorTools
{
    /// <summary>
    /// Realistic sky: Poly Haven's CC0 "Kloofendal 48d Partly Cloudy (Pure Sky)" HDRI on a panoramic skybox.
    /// The skybox is rotated so the photo's sun sits exactly behind the scene's directional light, the light
    /// takes the photo's sun elevation (clamped for readable shadows), and fog takes the horizon colour.
    /// </summary>
    public static class SkySetup
    {
        const string HdrPath = "Assets/Maily/Sky/kloofendal_48d_partly_cloudy_puresky_4k.hdr";

        public static string Apply(Light sun)
        {
            var imp = AssetImporter.GetAtPath(HdrPath) as TextureImporter;
            if (imp == null) return "Sky: HDRI missing, kept procedural sky";
            if (imp.maxTextureSize != 4096 || imp.mipmapEnabled || !imp.isReadable)
            {
                imp.textureShape = TextureImporterShape.Texture2D;
                imp.maxTextureSize = 4096; imp.mipmapEnabled = false; imp.isReadable = true;
                imp.wrapModeU = TextureWrapMode.Repeat; imp.wrapModeV = TextureWrapMode.Clamp;
                imp.textureCompression = TextureImporterCompression.CompressedHQ;
                imp.SaveAndReimport();
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(HdrPath);

            // find the sun: brightest texel in a coarse scan of the upper hemisphere
            int w = tex.width, h = tex.height; float best = -1f; int bx = 0, by = 0;
            for (int y = h / 2; y < h; y += 4)
                for (int x = 0; x < w; x += 4)
                {
                    var c = tex.GetPixel(x, y); float l = c.r + c.g + c.b;
                    if (l > best) { best = l; bx = x; by = y; }
                }
            float u = (bx + 0.5f) / w, v = (by + 0.5f) / h;
            float theta = Mathf.PI * (1f - v);               // from zenith
            float phi = (0.5f - u) * 2f * Mathf.PI;            // Skybox/Panoramic: u = 0.5 - atan2(z, x) / 2π
            float elev = 90f - theta * Mathf.Rad2Deg;

            // light: keep its heading, take the photo's elevation (clamped so shadows stay readable)
            float lightElev = Mathf.Clamp(elev, 28f, 55f);
            var e = sun.transform.eulerAngles;
            sun.transform.rotation = Quaternion.Euler(lightElev, e.y, 0f);
            var toSun = -sun.transform.forward;
            float rot = (Mathf.Atan2(toSun.z, toSun.x) - phi) * Mathf.Rad2Deg; // the shader turns the sky mesh by +rot (CCW in x-z) about Y

            var path = Mats.Folder + "/M_SkyHDRI.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("Skybox/Panoramic")); AssetDatabase.CreateAsset(mat, path); }
            mat.shader = Shader.Find("Skybox/Panoramic");
            mat.SetTexture("_MainTex", tex);
            mat.SetFloat("_Mapping", 1f);       // latitude-longitude
            mat.SetFloat("_ImageType", 0f);     // 360°
            mat.SetFloat("_Rotation", Mathf.Repeat(rot, 360f));
            mat.SetFloat("_Exposure", 1.0f);
            mat.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f, 0.5f)); // neutral (shader doubles it)
            EditorUtility.SetDirty(mat);
            RenderSettings.skybox = mat;

            // fog = horizon colour of the photo (a band just above the horizon, all around)
            Color sum = Color.black; int n = 0;
            for (int y = Mathf.RoundToInt(h * 0.505f); y < h * 0.54f; y += 2)
                for (int x = 0; x < w; x += 16) { sum += tex.GetPixel(x, y); n++; }
            var horizon = (sum / Mathf.Max(1, n)).gamma;
            horizon = new Color(Mathf.Clamp01(horizon.r), Mathf.Clamp01(horizon.g), Mathf.Clamp01(horizon.b));
            RenderSettings.fogColor = Color.Lerp(horizon, Palette.Haze, 0.25f);
            RenderSettings.ambientSkyColor = Color.Lerp(Palette.SkyTop, horizon, 0.3f);

            // free the CPU copy in builds; re-imported readable next time the scene is built
            return $"Sky: HDRI sun at elevation {elev:0}°, light elevation {lightElev:0}°, skybox rotation {Mathf.Repeat(rot, 360f):0}°, fog {ColorUtility.ToHtmlStringRGB(RenderSettings.fogColor)}";
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Maily
{
    /// <summary>Palette-driven URP materials, cached by key. In the Editor they are saved as assets.</summary>
    public static class Mats
    {
        public const string Folder = "Assets/Maily/Generated";
        static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();

        public static Material Get(string key, Color color, float smooth = 0.12f, float emission = 0f)
        {
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            // Edit mode (scene build): materials are saved as assets. Play mode / builds: runtime-only
            // materials, so gameplay never edits or creates project assets.
            bool asAsset = !Application.isPlaying;
#if UNITY_EDITOR
            var path = $"{Folder}/M_{key}.mat";
            if (asAsset) m = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
#endif
            if (m == null)
            {
                var baseMat = GraphicsSettings.currentRenderPipeline != null
                    ? GraphicsSettings.currentRenderPipeline.defaultMaterial : null;
                m = baseMat != null ? new Material(baseMat) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
#if UNITY_EDITOR
                if (asAsset) { EnsureFolder(); UnityEditor.AssetDatabase.CreateAsset(m, path); }
#endif
            }
            m.name = "M_" + key;
            m.SetColor("_BaseColor", color);
            m.color = color;
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", 0f);
            if (emission > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", color * emission);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else m.DisableKeyword("_EMISSION");
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(m);
#endif
            Cache[key] = m;
            return m;
        }

        public static Mesh SaveMesh(Mesh mesh)
        {
#if UNITY_EDITOR
            EnsureFolder();
            var path = $"{Folder}/{mesh.name}.asset";
            var existing = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                existing.Clear();
                existing.indexFormat = mesh.indexFormat;
                existing.SetVertices(mesh.vertices); existing.SetNormals(mesh.normals);
                existing.SetTriangles(mesh.triangles, 0); existing.RecalculateBounds();
                UnityEditor.EditorUtility.SetDirty(existing);
                return existing;
            }
            UnityEditor.AssetDatabase.CreateAsset(mesh, path);
#endif
            return mesh;
        }

#if UNITY_EDITOR
        static void EnsureFolder()
        {
            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Maily")) UnityEditor.AssetDatabase.CreateFolder("Assets", "Maily");
            if (!UnityEditor.AssetDatabase.IsValidFolder(Folder)) UnityEditor.AssetDatabase.CreateFolder("Assets/Maily", "Generated");
        }
#endif

        public static void ClearCache() => Cache.Clear();
    }
}

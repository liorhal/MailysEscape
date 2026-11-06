using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Maily.EditorTools
{
    /// <summary>
    /// Turns Maily's static (rig-less) mesh into a skinned mesh with a small quadruped skeleton.
    /// Joint positions come from an analysis of the model's vertices (mesh units: +Y up, head at +Z):
    /// paws front z≈+0.36 / hind z≈−0.58, legs at |x|≈0.1, belly line y≈−0.10, tail behind z<−0.7.
    /// Weights are smooth region masks, so joints bend without tearing.
    /// </summary>
    public static class MailyRigBuilder
    {
        const string MeshPath = "Assets/Maily/Generated/Maily_Skinned.asset";

        static float S(float a, float b, float t) { t = Mathf.Clamp01((t - a) / (b - a)); return t * t * (3f - 2f * t); }

        public static string Rig(GameObject model, MailyController owner)
        {
            var mf = model.GetComponent<MeshFilter>();
            var mr = model.GetComponent<MeshRenderer>();
            if (mf == null || mr == null) return "rig: no MeshFilter/MeshRenderer on model root";
            var src = mf.sharedMesh;
            var mats = mr.sharedMaterials;

            // ---- skeleton (positions in the model's local mesh space) ----
            Transform B(string name, Transform parent, Vector3 meshPos)
            {
                var t = new GameObject(name).transform;
                t.SetParent(parent, false);
                t.position = model.transform.TransformPoint(meshPos);
                t.rotation = model.transform.rotation;
                return t;
            }
            var pelvis = B("Pelvis", model.transform, Vector3.zero);
            var chest = B("Chest", pelvis, new Vector3(0, 0.05f, 0.0f));
            var head = B("Head", chest, new Vector3(0, 0.18f, 0.47f));
            var tail = B("Tail", pelvis, new Vector3(0, 0.02f, -0.70f));
            var up = new Transform[4]; var lo = new Transform[4];
            // order: FL, FR, HL, HR  (L = −x side in mesh space)
            float[] sx = { -1, 1, -1, 1 };
            for (int i = 0; i < 4; i++)
            {
                bool front = i < 2;
                var hip = front ? new Vector3(0.10f * sx[i], -0.02f, 0.36f) : new Vector3(0.10f * sx[i], 0.02f, -0.58f);
                var knee = front ? new Vector3(0.105f * sx[i], -0.30f, 0.35f) : new Vector3(0.105f * sx[i], -0.30f, -0.62f);
                string n = (front ? "F" : "H") + (sx[i] < 0 ? "L" : "R");
                up[i] = B("Upper" + n, front ? chest : pelvis, hip);
                lo[i] = B("Lower" + n, up[i], knee);
            }
            var bones = new List<Transform> { pelvis, chest, head, tail };
            bones.AddRange(up); bones.AddRange(lo);
            int iPel = 0, iChest = 1, iHead = 2, iTail = 3;

            // ---- weights ----
            var verts = src.vertices;
            var bw = new BoneWeight[verts.Length];
            var w = new float[bones.Count];
            for (int v = 0; v < verts.Length; v++)
            {
                System.Array.Clear(w, 0, w.Length);
                var p = verts[v]; float ax = Mathf.Abs(p.x);
                float c = S(-0.20f, 0.20f, p.z);
                w[iPel] = 1f - c; w[iChest] = c;

                // tail: thin, centred, behind the rump
                float t = S(-0.68f, -0.75f, p.z) * S(-0.30f, -0.24f, p.y) * S(0.075f, 0.045f, ax);
                float a = Mathf.Min(t, w[iPel]); w[iPel] -= a; w[iTail] += a;

                // legs: below the belly line, off the centre line, inside the front / hind bands
                float legY = S(-0.03f, -0.17f, p.y);
                float legX = S(0.02f, 0.06f, ax);
                float frontBand = S(0.10f, 0.18f, p.z) * (1f - S(0.56f, 0.64f, p.z));
                float hindBand = S(-0.86f, -0.78f, p.z) * (1f - S(-0.40f, -0.32f, p.z));
                float lowF = S(-0.25f, -0.35f, p.y);
                int side = p.x < 0 ? 0 : 1;
                float lf = legY * legX * frontBand;
                a = Mathf.Min(lf, w[iChest]); w[iChest] -= a;
                w[4 + side] += a * (1f - lowF); w[8 + side] += a * lowF;
                float lh = legY * legX * hindBand;
                a = Mathf.Min(lh, w[iPel]); w[iPel] -= a;
                w[4 + 2 + side] += a * (1f - lowF); w[8 + 2 + side] += a * lowF;

                // head & neck
                float h = S(0.40f, 0.56f, p.z) * S(-0.02f, 0.14f, p.y);
                a = Mathf.Min(h, w[iChest]); w[iChest] -= a; w[iHead] += a;

                // keep the 4 largest, normalised
                var top = Enumerable.Range(0, w.Length).OrderByDescending(k => w[k]).Take(4).ToArray();
                float sum = top.Sum(k => w[k]); if (sum < 1e-5f) { top[0] = iPel; w[iPel] = 1f; sum = 1f; }
                bw[v] = new BoneWeight
                {
                    boneIndex0 = top[0], weight0 = w[top[0]] / sum,
                    boneIndex1 = top[1], weight1 = w[top[1]] / sum,
                    boneIndex2 = top[2], weight2 = w[top[2]] / sum,
                    boneIndex3 = top[3], weight3 = w[top[3]] / sum,
                };
            }

            var mesh = Object.Instantiate(src);
            mesh.name = "Maily_Skinned";
            mesh.boneWeights = bw;
            mesh.bindposes = bones.Select(b => b.worldToLocalMatrix * model.transform.localToWorldMatrix).ToArray();
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (existing != null) AssetDatabase.DeleteAsset(MeshPath);
            AssetDatabase.CreateAsset(mesh, MeshPath);

            Object.DestroyImmediate(mr); Object.DestroyImmediate(mf);
            var smr = model.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh; smr.sharedMaterials = mats;
            smr.bones = bones.ToArray(); smr.rootBone = pelvis;
            var bounds = mesh.bounds; bounds.Expand(0.6f); smr.localBounds = bounds;
            smr.quality = SkinQuality.Bone4;

            var gait = model.AddComponent<MailyGait>();
            gait.Owner = owner; gait.Pelvis = pelvis; gait.Chest = chest; gait.Head = head; gait.Tail = tail;
            gait.Upper = up; gait.Lower = lo;

            int legVerts = bw.Count(b => b.boneIndex0 >= 4), tailVerts = bw.Count(b => b.boneIndex0 == iTail), headVerts = bw.Count(b => b.boneIndex0 == iHead);
            return $"rigged Maily: {bones.Count} bones, {verts.Length} verts (legs {legVerts}, tail {tailVerts}, head {headVerts})";
        }
    }
}

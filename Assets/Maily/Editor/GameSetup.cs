using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Maily.EditorTools
{
    /// <summary>Builds the whole playable scene: Maily ▸ Build Kibbutz Scene.</summary>
    public static class GameSetup
    {
        public const string ScenePath = "Assets/Maily/Scenes/KibbutzHatzor.unity";
        public const string LayoutPath = "Assets/Maily/Data/hatzor_layout.json";
        public const string MailyModelPath = "Assets/Maily/Models/Maily.glb";

        // Model orientation fix-ups (Meshy export): yaw so her nose points +Z, target height in metres.
        public static float MailyYaw = 0f;
        public static float MailyHeight = 0.62f;

        [MenuItem("Maily/Build Kibbutz Scene")]
        public static string BuildAll()
        {
            var log = new System.Text.StringBuilder();
            Mats.ClearCache();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            SetupQualityAndPipeline(log);
            var sun = SetupLighting();
            SetupVolume();

            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(LayoutPath).text;
            var world = KibbutzBuilder.Build(json);
            log.AppendLine($"World: {world.BuildingPolys.Count} buildings, {world.Roads.Count} roads, {world.Areas.Count} areas, {world.TreeSpots.Count} trees");
            Physics.SyncTransforms();

            var rnd = new System.Random(2026);
            var (spawn, spawnYaw, home) = ChooseHome(world, rnd);
            var maily = SpawnMaily(spawn, spawnYaw);
            var dh = CharacterKit.BuildDoghouse(home.pos, home.yaw);
            dh.transform.SetParent(world.Root.transform, true);
            Physics.SyncTransforms();

            var gameplay = new GameObject("Gameplay").transform;
            int dogs = SpawnDogs(world, rnd, gameplay, spawn);
            int parrots = SpawnParrots(world, rnd, gameplay, spawn);
            int treats = SpawnTreats(world, rnd, gameplay, spawn);
            log.AppendLine($"Spawned: Maily @ {spawn}, {dogs} dogs, {parrots} parrots, {treats} treats");

            var systems = new GameObject("Systems");
            var gm = systems.AddComponent<GameManager>();
            gm.TreatGoal = Mathf.Min(50, treats); gm.DogGoal = dogs; gm.ParrotGoal = Mathf.Min(6, parrots);
            systems.AddComponent<Sfx>();
            var fx = systems.AddComponent<Fx>();
            fx.ParticleMat = ParticleMaterial();
            fx.CubeMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            fx.SphereMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            systems.AddComponent<Hud>();
            systems.AddComponent<PlayerIdentity>();
            SetupCameras(systems, maily, spawnYaw);

            EnsureFolder("Assets/Maily/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            var list = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
            AssetDatabase.SaveAssets();
            log.AppendLine("Saved " + ScenePath);
            Debug.Log("[Maily] " + log);
            return log.ToString();
        }

        // ------------------------------------------------------------------ pipeline / light / volume
        static void SetupQualityAndPipeline(System.Text.StringBuilder log)
        {
            var names = QualitySettings.names;
            QualitySettings.SetQualityLevel(names.Length - 1, true); // desktop → highest tier
            log.AppendLine("Quality tier: " + names[names.Length - 1] + " of [" + string.Join(",", names) + "]");
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.supportsHDR = true;
                urp.shadowDistance = 110f;
                EditorUtility.SetDirty(urp);
                log.AppendLine("URP asset: " + urp.name + " (HDR on, shadow distance 110 m)");
            }
            else log.AppendLine("WARNING: URP not active");
        }

        static Light SetupLighting()
        {
            var go = new GameObject("Sun (late afternoon, from the south-west)");
            var sun = go.AddComponent<Light>();
            sun.type = LightType.Directional; sun.color = Palette.SunKey; sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.85f;
            go.transform.rotation = Quaternion.Euler(40f, 58f, 0f); // elevation 40°, travelling towards the north-east
            RenderSettings.sun = sun;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Palette.SkyTop;
            RenderSettings.ambientEquatorColor = Palette.SkyEquator;
            RenderSettings.ambientGroundColor = Palette.SkyGround;

            var sky = AssetDatabase.LoadAssetAtPath<Material>(Mats.Folder + "/M_Sky.mat");
            if (sky == null) { sky = new Material(Shader.Find("Skybox/Procedural")); EnsureFolder(Mats.Folder); AssetDatabase.CreateAsset(sky, Mats.Folder + "/M_Sky.mat"); }
            sky.SetColor("_SkyTint", Color.Lerp(Palette.SkyTop, Color.white, 0.25f));
            sky.SetColor("_GroundColor", Palette.Kurkar);
            sky.SetFloat("_AtmosphereThickness", 0.75f);
            sky.SetFloat("_Exposure", 1.2f);
            sky.SetFloat("_SunSize", 0.035f);
            EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky;

            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Palette.Haze; RenderSettings.fogStartDistance = 160f; RenderSettings.fogEndDistance = 950f;
            return sun;
        }

        static void SetupVolume()
        {
            const string path = "Assets/Maily/Generated/KibbutzVolumeProfile.asset";
            EnsureFolder(Mats.Folder);
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            var tm = profile.Add<Tonemapping>(true); tm.mode.Override(TonemappingMode.ACES);
            var bloom = profile.Add<Bloom>(true); bloom.intensity.Override(0.35f); bloom.threshold.Override(1.1f); bloom.scatter.Override(0.65f);
            var vig = profile.Add<Vignette>(true); vig.intensity.Override(0.22f); vig.smoothness.Override(0.45f);
            var ca = profile.Add<ColorAdjustments>(true); ca.saturation.Override(10f); ca.contrast.Override(6f); ca.postExposure.Override(0.1f);
            var wb = profile.Add<WhiteBalance>(true); wb.temperature.Override(8f);
            foreach (var c in profile.components) { c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy; AssetDatabase.AddObjectToAsset(c, profile); }
            EditorUtility.SetDirty(profile);

            var go = new GameObject("Global Volume");
            var vol = go.AddComponent<Volume>(); vol.isGlobal = true; vol.sharedProfile = profile; vol.priority = 1;
        }

        static Material ParticleMaterial()
        {
            var path = Mats.Folder + "/M_Particles.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Particles/Simple Lit"));
            m.SetColor("_BaseColor", Color.white);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // ------------------------------------------------------------------ cameras
        static void SetupCameras(GameObject systems, Transform maily, float yaw)
        {
            var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>(); cam.nearClipPlane = 0.1f; cam.farClipPlane = 1500f;
            camGo.AddComponent<AudioListener>();
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true; data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.volumeLayerMask = ~0;
            var brain = camGo.AddComponent<CinemachineBrain>();
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 1.4f);
            camGo.transform.position = maily.position + new Vector3(0, 3, -6);

            var target = maily.Find("CamTarget");

            var gameGo = new GameObject("Gameplay Camera");
            var game = gameGo.AddComponent<CinemachineCamera>();
            game.Follow = target; game.LookAt = target;
            var lens = LensSettings.Default; lens.FieldOfView = 55f; lens.NearClipPlane = 0.1f; lens.FarClipPlane = 1500f; game.Lens = lens;
            var orbit = gameGo.AddComponent<CinemachineOrbitalFollow>();
            orbit.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
            orbit.Radius = 4.2f;
            orbit.TrackerSettings.BindingMode = BindingMode.WorldSpace;
            orbit.TrackerSettings.PositionDamping = new Vector3(0.4f, 0.3f, 0.4f);
            orbit.HorizontalAxis.Range = new Vector2(-180, 180); orbit.HorizontalAxis.Wrap = true; orbit.HorizontalAxis.Value = Mathf.DeltaAngle(0, yaw);
            orbit.VerticalAxis.Range = new Vector2(-10, 70); orbit.VerticalAxis.Value = 16f;
            orbit.HorizontalAxis.Recentering.Enabled = false; orbit.VerticalAxis.Recentering.Enabled = false;
            var comp = gameGo.AddComponent<CinemachineRotationComposer>();
            comp.Damping = new Vector2(0.35f, 0.35f);
            comp.TargetOffset = new Vector3(0, 0.35f, 0);

            var titleGo = new GameObject("Title Camera");
            var title = titleGo.AddComponent<CinemachineCamera>();
            var tl = LensSettings.Default; tl.FieldOfView = 42f; tl.NearClipPlane = 0.1f; tl.FarClipPlane = 1500f; title.Lens = tl;
            title.Priority = 20; game.Priority = 10;

            var dir = systems.AddComponent<CameraDirector>();
            dir.GameCam = game; dir.TitleCam = title; dir.Orbit = orbit;
        }

        // ------------------------------------------------------------------ Maily
        static Transform SpawnMaily(Vector3 pos, float yaw)
        {
            var root = new GameObject("Maily");
            root.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            var cc = root.AddComponent<CharacterController>();
            cc.height = 0.62f; cc.radius = 0.28f; cc.center = new Vector3(0, 0.33f, 0); cc.stepOffset = 0.3f; cc.slopeLimit = 50f; cc.skinWidth = 0.03f;
            root.AddComponent<MailyController>();
            var camTarget = new GameObject("CamTarget").transform; camTarget.SetParent(root.transform, false); camTarget.localPosition = new Vector3(0, 0.45f, 0);
            var pivot = new GameObject("Pivot").transform; pivot.SetParent(root.transform, false);
            var body = pivot.gameObject.AddComponent<DogBody>(); body.Owner = root.GetComponent<MailyController>();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MailyModelPath);
            if (prefab == null) { Debug.LogError("[Maily] Model not found at " + MailyModelPath); return root.transform; }
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            model.name = "Maily Model";
            model.transform.SetParent(pivot, false);
            model.transform.localRotation = Quaternion.Euler(0, MailyYaw, 0);
            foreach (var c in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            // scale to target height, feet on the ground, centred on the controller
            var b = Bounds(model);
            float k = MailyHeight / Mathf.Max(0.01f, b.size.y);
            model.transform.localScale *= k;
            b = Bounds(model);
            var offset = root.transform.position - new Vector3(b.center.x, b.min.y, b.center.z);
            model.transform.position += offset;
            return root.transform;
        }

        static Bounds Bounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
        }

        // ------------------------------------------------------------------ placement
        static bool Free(Vector3 p, float r = 0.5f) => !Physics.CheckSphere(p + Vector3.up * (r + 0.15f), r, ~0, QueryTriggerInteraction.Ignore);

        static (Vector3 spawn, float yaw, (Vector3 pos, float yaw) doghouse) ChooseHome(KibbutzBuilder.Result w, System.Random rnd)
        {
            // a house 100–220 m from the dining hall, right next to a footpath
            var feet = w.Roads.Where(r => r.kind == "footway" || r.kind == "path").ToList();
            var candidates = new List<(Vector2 c, Vector2 near, float d)>();
            foreach (var poly in w.BuildingPolys)
            {
                var c = Geo.Centroid(poly); float dist = c.magnitude;
                float area = Mathf.Abs(Geo.SignedArea(poly));
                if (dist < 100 || dist > 220 || area < 60 || area > 220) continue;
                Vector2 best = Vector2.zero; float bd = float.MaxValue;
                foreach (var r in feet)
                    for (int i = 0; i < r.line.Count - 1; i++)
                    {
                        var a = r.line[i]; var ab = r.line[i + 1] - a; float t = Mathf.Clamp01(Vector2.Dot(c - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
                        var q = a + ab * t; float dd = (q - c).magnitude; if (dd < bd) { bd = dd; best = q; }
                    }
                if (bd < 18f) candidates.Add((c, best, bd));
            }
            foreach (var cand in candidates.OrderBy(x => rnd.Next()))
            {
                var away = (cand.near - cand.c).normalized;
                var spawn = new Vector3(cand.near.x, 0.1f, cand.near.y);
                if (!Free(spawn, 0.4f)) continue;
                var dh2 = cand.near - away * 2.2f + new Vector2(-away.y, away.x) * 2.0f;
                var dh = new Vector3(dh2.x, 0.05f, dh2.y);
                float yaw = Mathf.Atan2(away.x, away.y) * Mathf.Rad2Deg;
                if (!Free(dh, 0.8f)) dh = spawn - new Vector3(away.x, 0, away.y) * 0f + new Vector3(-away.y, 0, away.x) * 3f;
                return (spawn, yaw, (dh, yaw));
            }
            return (new Vector3(5, 0.1f, 5), 0f, (new Vector3(8, 0.05f, 5), 0f));
        }

        static List<Vector3> LawnPoints(KibbutzBuilder.Result w, System.Random rnd, int perArea, params string[] kinds)
        {
            var pts = new List<Vector3>();
            foreach (var a in w.Areas.Where(a => kinds.Contains(a.kind)))
            {
                var min = new Vector2(a.poly.Min(p => p.x), a.poly.Min(p => p.y)); var max = new Vector2(a.poly.Max(p => p.x), a.poly.Max(p => p.y));
                int got = 0;
                for (int i = 0; i < perArea * 8 && got < perArea; i++)
                {
                    var p = new Vector2(Mathf.Lerp(min.x, max.x, (float)rnd.NextDouble()), Mathf.Lerp(min.y, max.y, (float)rnd.NextDouble()));
                    if (p.magnitude > 560f || !Geo.Contains(a.poly, p)) continue;
                    var v = new Vector3(p.x, 0.05f, p.y);
                    if (!Free(v, 0.6f)) continue;
                    pts.Add(v); got++;
                }
            }
            return pts;
        }

        static List<Vector3> Spread(List<Vector3> pts, int count, float minDist, Vector3 avoid, float avoidDist, System.Random rnd)
        {
            var res = new List<Vector3>();
            foreach (var p in pts.OrderBy(_ => rnd.Next()))
            {
                if ((p - avoid).magnitude < avoidDist) continue;
                if (res.Any(q => (q - p).magnitude < minDist)) continue;
                res.Add(p); if (res.Count >= count) break;
            }
            return res;
        }

        static int SpawnDogs(KibbutzBuilder.Result w, System.Random rnd, Transform parent, Vector3 spawn)
        {
            var pts = LawnPoints(w, rnd, 3, "park", "grass", "playground", "pitch", "village_green");
            var picks = Spread(pts, CharacterKit.Dogs.Length, 75f, spawn, 30f, rnd);
            for (int i = 0; i < picks.Count; i++)
            {
                var go = new GameObject("Dog " + CharacterKit.Dogs[i].Name);
                go.transform.SetParent(parent);
                go.transform.SetPositionAndRotation(picks[i], Quaternion.Euler(0, rnd.Next(360), 0));
                var dog = go.AddComponent<NpcDog>();
                CharacterKit.BuildDog(dog, CharacterKit.Dogs[i]);
            }
            return picks.Count;
        }

        static int SpawnParrots(KibbutzBuilder.Result w, System.Random rnd, Transform parent, Vector3 spawn)
        {
            var pts = LawnPoints(w, rnd, 4, "park", "grass", "orchard", "village_green", "meadow");
            var picks = Spread(pts, 10, 45f, spawn, 18f, rnd);
            var go = new GameObject("Parrot Landing Spots");
            go.transform.SetParent(parent);
            var holder = go.AddComponent<ParrotSpots>();
            holder.Spots = pts.OrderBy(_ => rnd.Next()).Take(80).ToArray();
            for (int i = 0; i < picks.Count; i++)
            {
                var p = new GameObject("Darara " + (i + 1));
                p.transform.SetParent(parent);
                p.transform.SetPositionAndRotation(picks[i], Quaternion.Euler(0, rnd.Next(360), 0));
                var parrot = p.AddComponent<Parrot>();
                CharacterKit.BuildParrot(parrot);
            }
            return picks.Count;
        }

        static int SpawnTreats(KibbutzBuilder.Result w, System.Random rnd, Transform parent, Vector3 spawn)
        {
            var pts = new List<Vector3>();
            foreach (var r in w.Roads)
            {
                bool ok = r.kind == "footway" || r.kind == "path" || r.kind == "residential" || r.kind == "living_street" || r.kind == "service" || r.kind == "pedestrian";
                if (!ok) continue;
                float carry = (float)rnd.NextDouble() * 12f;
                for (int i = 0; i < r.line.Count - 1; i++)
                {
                    var a = r.line[i]; var b = r.line[i + 1]; float len = (b - a).magnitude;
                    for (float t = carry; t < len; t += 13f)
                    {
                        var p = Vector2.Lerp(a, b, t / len);
                        if (p.magnitude > 560f) continue;
                        var v = new Vector3(p.x, 0.05f, p.y);
                        if ((v - spawn).magnitude < 5f || !Free(v, 0.35f)) continue;
                        if (rnd.NextDouble() < 0.45) pts.Add(v);
                    }
                    carry = 0;
                }
            }
            // a welcome trail: three bones leading away from home
            var picks = Spread(pts, 110, 9f, spawn, 0f, rnd);
            for (int i = 0; i < picks.Count; i++)
            {
                bool coin = rnd.NextDouble() < 0.3;
                var go = new GameObject(coin ? "Coin" : "Bone");
                go.transform.SetParent(parent);
                go.transform.position = picks[i];
                var c = go.AddComponent<Collectible>();
                CharacterKit.BuildTreat(c, coin);
            }
            return picks.Count;
        }

        static void EnsureFolder(string path)
        {
            var parts = path.Split('/'); var cur = parts[0];
            for (int i = 1; i < parts.Length; i++) { var next = cur + "/" + parts[i]; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]); cur = next; }
        }
    }
}

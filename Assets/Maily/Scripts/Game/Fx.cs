using UnityEngine;

namespace Maily
{
    /// <summary>One-shot particle bursts (low-poly mesh confetti, palette colours): sparkle, hearts, feathers, dust.</summary>
    public class Fx : MonoBehaviour
    {
        static Fx I;
        public Material ParticleMat;
        public Mesh CubeMesh, SphereMesh;

        void Awake() => I = this;

        static ParticleSystem Make(string name, Vector3 pos, Mesh mesh)
        {
            var go = new GameObject(name); go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.playOnAwake = false; main.loop = false; main.stopAction = ParticleSystemStopAction.Destroy;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh; r.mesh = mesh; r.sharedMaterial = I.ParticleMat;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.2f;
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f); rot.separateAxes = true; rot.x = new ParticleSystem.MinMaxCurve(-6f, 6f);
            var sol = ps.sizeOverLifetime; sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.4f), new Keyframe(0.15f, 1f), new Keyframe(1, 0f)));
            return ps;
        }

        public static void Sparkle(Vector3 pos, Color c)
        {
            if (I == null) return;
            var ps = Make("Sparkle", pos, I.CubeMesh);
            var m = ps.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f); m.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.14f); m.gravityModifier = 0.8f;
            m.startColor = new ParticleSystem.MinMaxGradient(c, Color.Lerp(c, Color.white, 0.6f));
            ps.Emit(16); ps.Play();
        }

        public static void Hearts(Vector3 pos)
        {
            if (I == null) return;
            var ps = Make("Hearts", pos, I.SphereMesh);
            var m = ps.main; m.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 1.6f); m.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.24f); m.gravityModifier = -0.15f;
            m.startColor = new ParticleSystem.MinMaxGradient(Palette.Heart, Color.Lerp(Palette.Heart, Palette.CoinGold, 0.3f));
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.3f;
            ps.Emit(12); ps.Play();
        }

        public static void Feathers(Vector3 pos)
        {
            if (I == null) return;
            var ps = Make("Feathers", pos, I.CubeMesh);
            var m = ps.main; m.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2f); m.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
            m.startSize3D = true;
            m.startSizeX = new ParticleSystem.MinMaxCurve(0.05f, 0.08f); m.startSizeY = new ParticleSystem.MinMaxCurve(0.01f, 0.015f); m.startSizeZ = new ParticleSystem.MinMaxCurve(0.14f, 0.2f);
            m.gravityModifier = 0.12f;
            m.startColor = new ParticleSystem.MinMaxGradient(Palette.ParrotGreen, Palette.ParrotDeep);
            var vel = ps.limitVelocityOverLifetime; vel.enabled = true; vel.limit = 1.2f; vel.dampen = 0.15f;
            ps.Emit(10); ps.Play();
        }

        public static void Dust(Vector3 pos, float amount)
        {
            if (I == null || amount < 0.1f) return;
            var ps = Make("Dust", pos, I.SphereMesh);
            bool small = amount < 0.5f; // sprint footfalls: a quick small puff; landings: a proper burst
            var m = ps.main;
            m.startLifetime = small ? new ParticleSystem.MinMaxCurve(0.15f, 0.28f) : new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
            m.startSize = small ? new ParticleSystem.MinMaxCurve(0.025f, 0.05f) : new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
            m.gravityModifier = -0.05f;
            m.startColor = new ParticleSystem.MinMaxGradient(Palette.Kurkar, Color.Lerp(Palette.Kurkar, Color.white, 0.4f));
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.25f; sh.rotation = new Vector3(90, 0, 0);
            ps.Emit(small ? 3 : Mathf.RoundToInt(6 + 8 * amount)); ps.Play();
        }
    }
}

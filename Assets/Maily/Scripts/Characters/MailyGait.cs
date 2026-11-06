using UnityEngine;

namespace Maily
{
    /// <summary>
    /// Procedural quadruped gait for Maily's skinned model (skeleton baked by MailyRigBuilder):
    /// trot (diagonal pairs) blending into a rotary gallop at sprint speed, knee/hock fold during
    /// swing, spine flex, stabilised head with idle sniffing, tail that lifts/streams/wags,
    /// legs tucked while airborne. Leg order everywhere: FL, FR, HL, HR.
    /// </summary>
    public class MailyGait : MonoBehaviour
    {
        public MailyController Owner;
        public Transform Pelvis, Chest, Head, Tail;
        public Transform[] Upper = new Transform[4];
        public Transform[] Lower = new Transform[4];

        [Header("Tuning")]
        public float TrotSwing = 30f, GallopSwing = 48f;
        public float TrotKnee = 38f, GallopKnee = 58f;
        public float HindFold = 0.5f;       // hocks fold less than front wrists
        public float BobAmp = 0.035f;       // in model units (≈ 1.7 cm on Maily)
        public float SpineFlex = 9f;

        public float Phase { get; private set; }   // 0..1 stride cycle

        // fight poses, written by FightManager each frame (0..1)
        [System.NonSerialized] public float FightLunge, FightCrouch, FightHurt;

        // phase offsets: trot = diagonal pairs, gallop = hinds then fronts (rotary-ish)
        static readonly float[] TrotOff = { 0.0f, 0.5f, 0.5f, 0.0f };
        static readonly float[] GallopOff = { 0.55f, 0.65f, 0.0f, 0.1f };

        Quaternion[] _upperRest = new Quaternion[4], _lowerRest = new Quaternion[4];
        Quaternion _chestRest, _headRest, _tailRest;
        Vector3 _pelvisRest;
        float _amp, _gallop, _air, _wag, _tailSwing, _tailSwingVel, _lastYaw, _sniffT;

        void Start()
        {
            if (Owner == null) Owner = GetComponentInParent<MailyController>();
            for (int i = 0; i < 4; i++) { _upperRest[i] = Upper[i].localRotation; _lowerRest[i] = Lower[i].localRotation; }
            _chestRest = Chest.localRotation; _headRest = Head.localRotation; _tailRest = Tail.localRotation;
            _pelvisRest = Pelvis.localPosition;
            Owner.Barked += () => _wag = 1f;
            _lastYaw = Owner.transform.eulerAngles.y;
        }

        static float Smooth(float a, float b, float t) { t = Mathf.Clamp01((t - a) / (b - a)); return t * t * (3f - 2f * t); }

        void LateUpdate()
        {
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            var v = Owner.Velocity; float speed = new Vector2(v.x, v.z).magnitude;
            float speedN = Mathf.Clamp01(speed / Owner.SprintSpeed);
            bool grounded = Owner.Grounded;

            // amplitude settles to zero over ~0.25 s on stop (never a hard freeze)
            _amp = Mathf.MoveTowards(_amp, grounded ? Smooth(0.03f, 0.35f, speedN) : 0f, dt / 0.25f);
            _gallop = Mathf.MoveTowards(_gallop, Smooth(0.58f, 0.85f, speedN), dt / 0.35f);
            _air = Mathf.MoveTowards(_air, grounded ? 0f : 1f, dt / 0.12f);

            // stride frequency rises with speed (Hz)
            float freq = 1.5f + 0.22f * speed;
            Phase = Mathf.Repeat(Phase + freq * dt, 1f);
            float tau = Mathf.PI * 2f;

            float swingAmp = Mathf.Lerp(TrotSwing, GallopSwing, _gallop) * _amp;
            float kneeAmp = Mathf.Lerp(TrotKnee, GallopKnee, _gallop) * _amp;

            for (int i = 0; i < 4; i++)
            {
                float off = Mathf.Lerp(TrotOff[i], GallopOff[i], _gallop);
                float q = (Phase + off) * tau;
                bool front = i < 2;
                // + angle = leg swings back (rotation about the bone's local x/right axis)
                float swing = Mathf.Sin(q) * swingAmp;
                // fold the lower leg while the leg travels forward (swing phase)
                float fold = Mathf.Max(0f, -Mathf.Cos(q)) * kneeAmp;
                float lower = front ? fold : -fold * HindFold;

                // airborne: fronts reach forward, hinds push back, everything tucks
                float airSwing = front ? -28f : 30f;
                float airFold = front ? 55f : -40f;
                swing = Mathf.Lerp(swing, airSwing, _air);
                lower = Mathf.Lerp(lower, airFold, _air);

                Upper[i].localRotation = _upperRest[i] * Quaternion.Euler(swing, 0f, 0f);
                Lower[i].localRotation = _lowerRest[i] * Quaternion.Euler(lower, 0f, 0f);
            }

            // body bob: two bounces per cycle at the trot, one big one at the gallop
            float trotBob = Mathf.Abs(Mathf.Sin(Phase * tau));
            float gallopBob = 0.5f + 0.5f * Mathf.Sin(Phase * tau - 0.6f);
            Pelvis.localPosition = _pelvisRest + Vector3.up * (Mathf.Lerp(trotBob, gallopBob, _gallop) * BobAmp * _amp);

            // spine: flex/extend through the gallop, a little lateral sway at the trot
            float flex = Mathf.Sin(Phase * tau + 1.2f) * SpineFlex * _gallop * _amp;
            float sway = Mathf.Sin(Phase * tau) * 3f * (1f - _gallop) * _amp;
            Chest.localRotation = _chestRest * Quaternion.Euler(flex, sway, 0f);

            // head: counter the spine (stabilised gaze), small nod; idle = sniff & look around
            _sniffT += dt;
            float idle = 1f - _amp;
            float sniffYaw = Mathf.Sin(_sniffT * 0.55f) * 14f * idle;
            float sniffPitch = (Mathf.Sin(_sniffT * 0.9f) > 0.85f ? 10f : 0f) * idle;
            float nod = Mathf.Sin(Phase * tau * 2f) * 3f * _amp * (1f - _gallop);
            float barkJolt = _wag > 0.8f ? -12f * (_wag - 0.8f) / 0.2f : 0f;
            Head.localRotation = _headRest * Quaternion.Euler(-flex * 0.8f + nod + sniffPitch + barkJolt, sniffYaw, 0f);

            // tail: lifts with speed, streams behind; side swing is a damped spring fed by turning
            float yaw = Owner.transform.eulerAngles.y;
            float yawRate = Mathf.DeltaAngle(_lastYaw, yaw) / dt; _lastYaw = yaw;
            _wag = Mathf.MoveTowards(_wag, 0f, dt / 2.2f);
            float wagFreq = Mathf.Lerp(1.6f, 9f, _wag);
            float wagAmp = Mathf.Lerp(14f * idle, 34f, _wag) + 4f * _amp;
            float target = Mathf.Sin(Time.time * wagFreq * tau) * wagAmp - Mathf.Clamp(yawRate * 0.08f, -25f, 25f);
            // spring for secondary motion (tail lags behind its target)
            float k = 90f, c = 12f;
            _tailSwingVel += (k * (target - _tailSwing) - c * _tailSwingVel) * dt;
            _tailSwing += _tailSwingVel * dt;
            // + rotation about x swings the hanging tail backward and up
            float lift = Mathf.Lerp(0f, 38f, _amp) + 10f * _wag + Mathf.Sin(Phase * tau + 2f) * 6f * _amp;
            Tail.localRotation = _tailRest * Quaternion.Euler(lift, _tailSwing, 0f);

            ApplyFightPose();
        }

        /// <summary>Layered on top of the gait: lunge (bite / pounce), crouch (block), hurt (recoil).</summary>
        void ApplyFightPose()
        {
            float l = FightLunge, c = FightCrouch, h = FightHurt;
            if (l + c + h < 0.001f) return;
            // lunge: body thrusts forward, head snaps down-and-out, fronts reach, hinds push
            Pelvis.localPosition += new Vector3(0f, -c * 0.12f + l * 0.02f, l * 0.16f);
            Chest.localRotation *= Quaternion.Euler(l * 8f + c * 6f - h * 10f, 0f, 0f);
            Head.localRotation *= Quaternion.Euler(l * 22f + c * 14f - h * 24f, 0f, 0f);
            for (int i = 0; i < 4; i++)
            {
                bool front = i < 2;
                float reach = front ? -l * 42f : l * 30f;
                float crouchUp = front ? -c * 18f : c * 22f;
                Upper[i].localRotation *= Quaternion.Euler(reach + crouchUp, 0f, 0f);
                Lower[i].localRotation *= Quaternion.Euler(front ? c * 30f + l * 15f : -c * 30f, 0f, 0f);
            }
            // ears-back flinch reads through the tail: tucked when hurt, low when crouching
            Tail.localRotation *= Quaternion.Euler(-h * 30f - c * 15f, 0f, 0f);
        }
    }
}

using UnityEngine;

namespace Maily
{
    /// <summary>
    /// Procedural motion for Maily's (rig-less) model: bounding gallop bob + pitch, weight-shift roll,
    /// lean into acceleration, bank into turns, jump stretch / landing squash, idle breathing, happy wiggle.
    /// Sits on a "Pivot" child between the controller root and the model.
    /// </summary>
    public class DogBody : MonoBehaviour
    {
        public MailyController Owner;
        public float StepFreq = 2.6f, BobAmp = 0.07f, GallopPitch = 7f, Roll = 2.5f;
        public float LeanMax = 8f, BankMax = 9f;

        Vector3 _baseScale, _basePos;
        float _phase, _gait, _lean, _bank, _squash, _wiggle;
        Vector3 _lastVel; float _lastYaw;

        void Start()
        {
            _baseScale = transform.localScale; _basePos = transform.localPosition;
            if (Owner == null) Owner = GetComponentInParent<MailyController>();
            Owner.Landed += () => { _squash = 1f; Fx.Dust(Owner.transform.position, 1f); };
            Owner.Jumped += () => _squash = -1f;
            Owner.Barked += () => _wiggle = 1f;
            _lastYaw = Owner.transform.eulerAngles.y;
        }

        void LateUpdate()
        {
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            var v = Owner.Velocity; var hv = new Vector3(v.x, 0, v.z);
            float speedN = Mathf.Clamp01(hv.magnitude / Owner.SprintSpeed);
            bool grounded = Owner.Grounded;

            // gait amplitude settles to zero over ~0.3 s, never a hard stop
            _gait = Mathf.MoveTowards(_gait, grounded ? speedN : 0f, dt / 0.3f);
            float prevPhase = _phase;
            _phase += dt * StepFreq * Mathf.Lerp(0.8f, 1.35f, speedN) * Mathf.PI * 2f;
            // a puff of kurkar dust on every other footfall while sprinting
            if (Owner.Sprinting && grounded && Mathf.Floor(prevPhase / (2f * Mathf.PI)) != Mathf.Floor(_phase / (2f * Mathf.PI)))
                Fx.Dust(Owner.transform.position - Owner.transform.forward * 0.4f, 0.3f);

            float bob = Mathf.Abs(Mathf.Sin(_phase)) * BobAmp * _gait * 1.6f;
            float pitch = Mathf.Sin(_phase * 2f) * GallopPitch * _gait;
            float roll = Mathf.Sin(_phase) * Roll * _gait;

            // lean into acceleration (local forward), bank into turns
            var acc = (hv - _lastVel) / dt; _lastVel = hv;
            float fwdAcc = Vector3.Dot(acc, Owner.transform.forward);
            _lean = Mathf.Lerp(_lean, Mathf.Clamp(fwdAcc * 0.35f, -LeanMax, LeanMax), 1f - Mathf.Exp(-8f * dt));
            float yaw = Owner.transform.eulerAngles.y; float yawRate = Mathf.DeltaAngle(_lastYaw, yaw) / dt; _lastYaw = yaw;
            _bank = Mathf.Lerp(_bank, Mathf.Clamp(-yawRate * 0.03f * speedN, -BankMax, BankMax), 1f - Mathf.Exp(-8f * dt));

            // airborne: nose up on rise, down on fall
            float airPitch = grounded ? 0f : Mathf.Clamp(-v.y * 2.2f, -14f, 14f);

            // idle breathing (always on — settle, never freeze)
            float breathe = Mathf.Sin(Time.time * 2f * Mathf.PI * 0.5f) * 0.015f * (1f - _gait);

            // squash & stretch
            _squash = Mathf.MoveTowards(_squash, 0f, dt / 0.18f);
            float sq = _squash > 0 ? -0.15f * _squash : 0.1f * -_squash; // land: squash, jump: stretch
            _wiggle = Mathf.MoveTowards(_wiggle, 0f, dt / 0.5f);
            float wig = Mathf.Sin(Time.time * 40f) * 6f * _wiggle;

            transform.localPosition = _basePos + Vector3.up * bob;
            transform.localRotation = Quaternion.Euler(pitch + _lean + airPitch, wig, roll + _bank);
            transform.localScale = new Vector3(_baseScale.x * (1f - sq * 0.5f), _baseScale.y * (1f + sq + breathe), _baseScale.z * (1f - sq * 0.5f));
        }
    }
}

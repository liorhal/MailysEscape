using UnityEngine;
using UnityEngine.InputSystem;

namespace Maily
{
    /// <summary>Maily: camera-relative run / sprint / jump / bark on a CharacterController.</summary>
    [RequireComponent(typeof(CharacterController))]
    public class MailyController : MonoBehaviour
    {
        public static MailyController I { get; private set; }

        public float WalkSpeed = 4.5f, SprintSpeed = 8.5f, Accel = 30f, TurnRate = 12f;
        public float Gravity = -26f, JumpSpeed = 7.5f;
        public Transform CameraYawSource;

        public Vector3 Velocity { get; private set; }
        public float Speed01 => new Vector2(Velocity.x, Velocity.z).magnitude / SprintSpeed;
        public bool Grounded { get; private set; } = true;
        public bool Sprinting { get; private set; }
        public event System.Action Jumped, Landed, Barked;

        CharacterController _cc;
        float _vy, _barkCooldown, _coyote;

        /// <summary>While true, the fight system positions Maily and feeds her motion (see SetFightMotion).</summary>
        public bool FightLock { get; set; }

        void Awake() { I = this; _cc = GetComponent<CharacterController>(); }

        /// <summary>Fight mode: report the motion the fighter applied, so gait / body animation follow it.</summary>
        public void SetFightMotion(Vector3 velocity, bool grounded) { Velocity = velocity; Grounded = grounded; }
        public void RaiseJumped() => Jumped?.Invoke();
        public void RaiseLanded() => Landed?.Invoke();
        public void RaiseBarked() => Barked?.Invoke();

        void Update()
        {
            if (FightLock) return;
            var gm = GameManager.I;
            bool active = gm == null || (gm.State == GameState.Playing && !gm.MapOpen);
            Vector2 input = Vector2.zero; bool jump = false, sprint = false, bark = false;
            if (active) ReadInput(out input, out jump, out sprint, out bark);

            // camera-relative direction
            var cam = Camera.main != null ? Camera.main.transform : transform;
            var fwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            if (fwd.sqrMagnitude < 0.01f) fwd = transform.forward;
            var right = Vector3.Cross(Vector3.up, fwd);
            var wish = (fwd * input.y + right * input.x);
            if (wish.sqrMagnitude > 1f) wish.Normalize();

            Sprinting = sprint && wish.sqrMagnitude > 0.01f;
            float max = Sprinting ? SprintSpeed : WalkSpeed;
            var hv = new Vector3(Velocity.x, 0, Velocity.z);
            hv = Vector3.MoveTowards(hv, wish * max, Accel * Time.deltaTime);

            // facing
            if (hv.sqrMagnitude > 0.05f)
            {
                var target = Quaternion.LookRotation(hv.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-TurnRate * Time.deltaTime));
            }

            // vertical
            bool wasGrounded = Grounded;
            Grounded = _cc.isGrounded;
            if (Grounded) { _coyote = 0.12f; if (_vy < 0) _vy = -2f; } else _coyote -= Time.deltaTime;
            if (jump && _coyote > 0f)
            {
                _vy = JumpSpeed; _coyote = 0f; Grounded = false;
                Jumped?.Invoke(); Sfx.I?.Play3D(Sfx.I.Hop, transform.position, 0.6f, Random.Range(0.95f, 1.1f));
            }
            _vy += Gravity * Time.deltaTime;

            var move = hv + Vector3.up * _vy;
            _cc.Move(move * Time.deltaTime);
            Velocity = new Vector3(hv.x, _vy, hv.z);
            if (!wasGrounded && _cc.isGrounded) Landed?.Invoke();

            _barkCooldown -= Time.deltaTime;
            if (bark && _barkCooldown <= 0f)
            {
                _barkCooldown = 0.6f;
                Sfx.I?.Play3D(Sfx.I.Bark, transform.position, 1f, Random.Range(0.95f, 1.08f));
                Barked?.Invoke();
            }

            // safety net: never fall out of the world
            if (transform.position.y < -10f) { _cc.enabled = false; transform.position = new Vector3(transform.position.x, 2f, transform.position.z); _cc.enabled = true; }
        }

        static void ReadInput(out Vector2 move, out bool jump, out bool sprint, out bool bark)
        {
            move = Vector2.zero; jump = sprint = bark = false;
            var k = Keyboard.current;
            if (k != null)
            {
                if (k.wKey.isPressed || k.upArrowKey.isPressed) move.y += 1;
                if (k.sKey.isPressed || k.downArrowKey.isPressed) move.y -= 1;
                if (k.dKey.isPressed || k.rightArrowKey.isPressed) move.x += 1;
                if (k.aKey.isPressed || k.leftArrowKey.isPressed) move.x -= 1;
                jump |= k.spaceKey.wasPressedThisFrame;
                sprint |= k.leftShiftKey.isPressed || k.rightShiftKey.isPressed;
                bark |= k.eKey.wasPressedThisFrame || k.bKey.wasPressedThisFrame;
            }
            var g = Gamepad.current;
            if (g != null)
            {
                var s = g.leftStick.ReadValue(); if (s.sqrMagnitude > move.sqrMagnitude) move = s;
                jump |= g.buttonSouth.wasPressedThisFrame;
                sprint |= g.rightTrigger.isPressed || g.leftStickButton.isPressed || g.buttonEast.isPressed;
                bark |= g.buttonWest.wasPressedThisFrame;
            }
        }

        /// <summary>Teleport safely (CharacterController must be disabled to move directly).</summary>
        public void Place(Vector3 pos, float yaw)
        {
            _cc = _cc != null ? _cc : GetComponent<CharacterController>();
            _cc.enabled = false; transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0)); _cc.enabled = true;
            Velocity = Vector3.zero; _vy = 0;
        }
    }
}

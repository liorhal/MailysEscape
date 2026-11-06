using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Maily
{
    /// <summary>
    /// Title: slow 5°/s orbit of Maily (title cam). Playing: Cinemachine orbital follow driven by
    /// mouse / right stick, auto-recentering behind Maily after ~1.2 s without look input.
    /// </summary>
    public class CameraDirector : MonoBehaviour
    {
        public CinemachineCamera GameCam, TitleCam;
        public CinemachineOrbitalFollow Orbit;
        public float MouseSensitivity = 0.12f, StickSensitivity = 140f;
        public float TitleRadius = 7.5f, TitleHeight = 3.2f, TitleDegPerSec = 5f;
        [Tooltip("Camera elevation (degrees above Maily) it returns to while she runs")]
        public float DefaultPitch = 35f;
        public float MinPitch = 10f, MaxPitch = 75f;

        float _titleAngle = 200f, _idleLook;

        void LateUpdate()
        {
            var gm = GameManager.I; var m = MailyController.I;
            if (gm == null || m == null) return;
            bool title = gm.State == GameState.Title;
            TitleCam.Priority = title ? 20 : 0;
            GameCam.Priority = 10;

            if (title)
            {
                _titleAngle += TitleDegPerSec * Time.unscaledDeltaTime;
                var center = m.transform.position + Vector3.up * 0.5f;
                var off = Quaternion.Euler(0, _titleAngle, 0) * new Vector3(0, TitleHeight, -TitleRadius);
                TitleCam.transform.position = center + off;
                // frame Maily off-centre (rule of thirds): look slightly to her side
                var look = center + Quaternion.Euler(0, _titleAngle, 0) * new Vector3(1.6f, 0.6f, 0);
                TitleCam.transform.rotation = Quaternion.LookRotation(look - TitleCam.transform.position);
                Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
                return;
            }

            bool playing = gm.State == GameState.Playing;
            bool locked = playing || gm.State == GameState.Fighting; // fights are keyboard / pad only
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
            if (!playing) return;

            Vector2 look2 = Vector2.zero;
            if (Mouse.current != null) look2 += Mouse.current.delta.ReadValue() * MouseSensitivity;
            if (Gamepad.current != null) look2 += Gamepad.current.rightStick.ReadValue() * StickSensitivity * Time.deltaTime;

            if (look2.sqrMagnitude > 0.0001f) _idleLook = 0f; else _idleLook += Time.deltaTime;
            float h = Orbit.HorizontalAxis.Value + look2.x;
            float v = Mathf.Clamp(Orbit.VerticalAxis.Value - look2.y, MinPitch, MaxPitch);

            // recenter behind Maily while she's moving and the player isn't looking around
            if (_idleLook > 1.2f && m.Speed01 > 0.15f)
            {
                float behind = m.transform.eulerAngles.y;
                h = h + Mathf.DeltaAngle(h, behind) * (1f - Mathf.Exp(-1.6f * Time.deltaTime));
                v = Mathf.Lerp(v, DefaultPitch, 1f - Mathf.Exp(-1f * Time.deltaTime));
            }
            Orbit.HorizontalAxis.Value = Mathf.Repeat(h + 180f, 360f) - 180f;
            Orbit.VerticalAxis.Value = v;
        }
    }
}

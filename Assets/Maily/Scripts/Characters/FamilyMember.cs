using UnityEngine;

namespace Maily
{
    /// <summary>One member of Maily's family: a simple figure with swinging arms and legs.</summary>
    public class FamilyMember : MonoBehaviour
    {
        public float RunSpeed = 5.6f;
        public Transform ArmL, ArmR, LegL, LegR, Body;
        public string Label = "";
        float _phase, _speed;

        public void Teleport(Vector3 p, Vector3 lookAt)
        {
            p.y = Ground.HeightAt(p); transform.position = p;
            var d = lookAt - p; d.y = 0; if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d);
            _speed = 0f;
        }

        public void MoveToward(Vector3 target, float speed, float dt, float stopAt = 0f)
        {
            var p = transform.position; var to = target - p; to.y = 0;
            float dist = to.magnitude;
            if (dist <= stopAt + 0.05f) { Idle(dt, target); return; }
            var dir = to / Mathf.Max(0.001f, dist);
            // walls, hedges and trees: try turning left or right
            if (Physics.Raycast(p + Vector3.up * 0.7f, dir, 0.9f, ~0, QueryTriggerInteraction.Ignore))
                foreach (var a in new[] { 40f, -40f, 80f, -80f, 120f, -120f })
                {
                    var alt = Quaternion.Euler(0, a, 0) * dir;
                    if (!Physics.Raycast(p + Vector3.up * 0.7f, alt, 0.9f, ~0, QueryTriggerInteraction.Ignore)) { dir = alt; break; }
                }
            _speed = Mathf.MoveTowards(_speed, speed, 12f * dt);
            p += dir * Mathf.Min(_speed * dt, dist);
            p.y = Ground.HeightAt(p);
            transform.position = p;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 1f - Mathf.Exp(-dt * 12f));
            Animate(dt);
        }

        public void Idle(float dt, Vector3 lookAt)
        {
            _speed = Mathf.MoveTowards(_speed, 0f, 12f * dt);
            var d = lookAt - transform.position; d.y = 0;
            if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), 1f - Mathf.Exp(-dt * 4f));
            Animate(dt);
        }

        void Animate(float dt)
        {
            float run = Mathf.Clamp01(_speed / 5f);
            _phase += dt * (3f + _speed * 1.9f);
            float s = Mathf.Sin(_phase) * Mathf.Lerp(8f, 50f, run) * (_speed > 0.1f ? 1f : 0.1f);
            if (LegL != null) LegL.localRotation = Quaternion.Euler(s, 0, 0);
            if (LegR != null) LegR.localRotation = Quaternion.Euler(-s, 0, 0);
            if (ArmL != null) ArmL.localRotation = Quaternion.Euler(-s * 0.9f, 0, -6f);
            if (ArmR != null) ArmR.localRotation = Quaternion.Euler(s * 0.9f, 0, 6f);
            if (Body != null)
            {
                Body.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(_phase)) * 0.06f * run, 0);
                Body.localRotation = Quaternion.Euler(10f * run, 0, 0); // lean into the run
            }
        }
    }
}

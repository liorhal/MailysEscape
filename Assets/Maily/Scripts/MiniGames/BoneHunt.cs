using UnityEngine;

namespace Maily
{
    /// <summary>חיפוש עצמות — three bones are buried nearby; follow the hot/cold sniff meter and dig with F.</summary>
    public class BoneHunt : MiniGame
    {
        const int Bones = 3;
        int _found;
        Vector3 _hidden;
        float _sniffT, _msgT;

        public override string Status => $"עצמות: {_found} מתוך {Bones}   ·   {WarmthWord}";
        public override string ResultText => _found >= Bones ? $"מיילי מצאה את כל העצמות, ונשארו {Mathf.CeilToInt(TimeLeft)} שניות" : $"מיילי מצאה {_found} עצמות";
        public override float Warmth => Mathf.Clamp01(1f - FlatDist(MailyPos, _hidden) / 30f);

        string WarmthWord
        {
            get
            {
                float w = Warmth;
                return w > 0.93f ? "רותח! חפרו כאן (F)" : w > 0.75f ? "חם מאוד" : w > 0.55f ? "חם" : w > 0.35f ? "פושר" : w > 0.15f ? "קר" : "קפוא";
            }
        }

        public override void Begin() { TimeLeft = 70f; Hide(); }

        void Hide()
        {
            _hidden = FindFree(MailyPos, 12f, 28f, 0.9f) ?? (Spot.transform.position + Vector3.forward * 10f);
        }

        public override bool Tick(float dt)
        {
            TimeLeft -= dt;
            // sniff puffs get faster as Maily gets warmer
            _sniffT -= dt;
            if (_sniffT <= 0f)
            {
                _sniffT = Mathf.Lerp(1.4f, 0.25f, Warmth);
                if (Warmth > 0.55f) Fx.Dust(MailyPos + MailyController.I.transform.forward * 0.5f, 0.15f);
            }
            _msgT -= dt;
            return TimeLeft <= 0f || _found >= Bones;
        }

        public override void OnAction()
        {
            if (FlatDist(MailyPos, _hidden) < 1.9f)
            {
                _found++; Score = _found;
                var p = _hidden + Vector3.up * 0.2f;
                Fx.Dust(p, 2f); Fx.Sparkle(p + Vector3.up * 0.4f, Palette.Bone);
                var bone = Prim(PrimitiveType.Capsule, p + Vector3.up * 0.3f, new Vector3(0.14f, 0.28f, 0.14f), Palette.Bone, "Bone", 0.35f);
                bone.transform.rotation = Quaternion.Euler(0, 0, 90);
                Object.Destroy(bone, 2.5f);
                Prim(PrimitiveType.Cylinder, new Vector3(_hidden.x, 0.07f, _hidden.z), new Vector3(1.1f, 0.02f, 1.1f), Palette.MiniDirt, "Dirt"); // dug hole
                Sfx.I?.Play2D(Sfx.I.Coin, 1f, 1.1f);
                MailyController.I.RaiseBarked();
                GameManager.I?.Say(_found >= Bones ? "כל העצמות נמצאו!" : "מצאה עצם! יש עוד...");
                if (_found < Bones) Hide();
            }
            else
            {
                Fx.Dust(MailyPos + MailyController.I.transform.forward * 0.4f, 0.6f);
                Sfx.I?.Play3D(Sfx.I.Hop, MailyPos, 0.5f, 0.6f);
                if (_msgT <= 0f) { _msgT = 2f; GameManager.I?.Say("אין כאן כלום... רחרחו עוד"); }
            }
        }

        public override int Medal() => _found >= Bones ? (TimeLeft >= 25f ? 3 : 2) : _found >= 1 ? 1 : 0;
    }
}

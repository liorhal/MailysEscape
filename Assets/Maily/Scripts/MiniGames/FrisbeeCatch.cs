using UnityEngine;

namespace Maily
{
    /// <summary>תפיסת פריזבי — six throws; run to the landing shadow and catch it (jumping helps).</summary>
    public class FrisbeeCatch : MiniGame
    {
        const int Throws = 6;
        int _throw, _caught;
        GameObject _disc, _shadow;
        Vector3 _from, _to;
        float _t, _flight, _pause = 1.2f, _height;
        bool _inFlight, _missed;

        public override bool ShowTimer => false;
        public override string Status => $"זריקה {Mathf.Min(_throw + (_inFlight ? 1 : 0), Throws)} מתוך {Throws}   ·   נתפסו {_caught}";
        public override string ResultText => $"מיילי תפסה {_caught} פריזבי מתוך {Throws}";

        public override void Begin()
        {
            _disc = Prim(PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.34f, 0.025f, 0.34f), Palette.MiniFrisbee, "Frisbee", 0.9f);
            _shadow = Prim(PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.6f, 0.004f, 0.6f), Palette.Shadow, "FrisbeeShadow");
            _disc.SetActive(false); _shadow.SetActive(false);
        }

        void Throw()
        {
            _from = Spot.transform.position + Vector3.up * 1.3f;
            var land = FindFree(MailyPos, 7f, 15f, 1.0f) ?? (MailyPos + Random.insideUnitSphere * 8f);
            _to = new Vector3(land.x, 0.6f, land.z);
            float dist = FlatDist(_from, _to);
            _flight = 1.5f + dist * 0.07f; _height = 2.5f + dist * 0.08f;
            _t = 0f; _inFlight = true; _missed = false;
            _disc.SetActive(true); _shadow.SetActive(true);
            _shadow.transform.position = new Vector3(_to.x, 0.13f, _to.z);
            Sfx.I?.Play3D(Sfx.I.Whoosh, _from, 1f, 0.8f);
        }

        public override bool Tick(float dt)
        {
            if (!_inFlight)
            {
                _pause -= dt;
                if (_pause <= 0f)
                {
                    if (_throw >= Throws) return true;
                    Throw();
                }
                return false;
            }

            _t += dt / _flight;
            float u = Mathf.Clamp01(_t);
            var p = Vector3.Lerp(_from, _to, u) + Vector3.up * Mathf.Sin(u * Mathf.PI) * _height;
            if (_missed) p = _disc.transform.position + (_to - _from).normalized * dt * 2f;
            _disc.transform.position = p;
            _disc.transform.rotation = Quaternion.Euler(Mathf.Sin(Time.time * 3f) * 8f, Time.time * 900f, 0f);
            _shadow.transform.localScale = new Vector3(0.3f + 0.6f * u, 0.004f, 0.3f + 0.6f * u);

            // catch: close to the disc, mouth height (a jump reaches higher throws)
            var mouth = MailyPos + Vector3.up * 0.5f;
            if (!_missed && u > 0.55f && FlatDist(mouth, p) < 1.15f && Mathf.Abs(p.y - mouth.y) < 0.95f)
            {
                _caught++; Score = _caught; _throw++; _inFlight = false; _pause = 1.1f;
                _disc.SetActive(false); _shadow.SetActive(false);
                Sfx.I?.Play2D(Sfx.I.Coin, 0.9f, 1f + _caught * 0.06f);
                Fx.Sparkle(p, Palette.MiniFrisbee);
                MailyController.I.RaiseBarked(); // happy wiggle + tail wag
                GameManager.I?.Say(_caught == Throws ? "כל הזריקות! איזו כלבה!" : "תפסה!");
                return false;
            }
            if (!_missed && u >= 1f)
            {
                _missed = true; _pause = 0.8f;
                Sfx.I?.Play3D(Sfx.I.Hop, p, 0.7f, 0.7f);
                Fx.Dust(p, 0.8f);
            }
            if (_missed)
            {
                _pause -= dt;
                if (_pause <= 0f) { _throw++; _inFlight = false; _pause = 0.9f; _disc.SetActive(false); _shadow.SetActive(false); }
            }
            return false;
        }

        public override int Medal() => _caught >= 5 ? 3 : _caught >= 3 ? 2 : _caught >= 1 ? 1 : 0;
    }
}

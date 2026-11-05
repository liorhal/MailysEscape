using System.Collections.Generic;
using UnityEngine;

namespace Maily
{
    /// <summary>A bone or a coin: spins, bobs, glows; Maily picks it up by running through it.</summary>
    public class Collectible : MonoBehaviour
    {
        public static readonly List<Collectible> All = new List<Collectible>();
        static float _comboTime; static int _combo;

        public Transform Spin;
        public bool IsCoin;
        bool _taken; float _takenT; float _seed;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);
        void Start() => _seed = Random.value * 10f;

        void Update()
        {
            if (_taken)
            {
                _takenT += Time.deltaTime;
                float k = _takenT / 0.25f;
                Spin.localScale = Vector3.one * (k < 0.4f ? Mathf.Lerp(1f, 1.5f, k / 0.4f) : Mathf.Lerp(1.5f, 0f, (k - 0.4f) / 0.6f));
                Spin.localPosition += Vector3.up * Time.deltaTime * 4f;
                if (k >= 1f) Destroy(gameObject);
                return;
            }
            Spin.localRotation = Quaternion.Euler(0, (Time.time + _seed) * (IsCoin ? 160f : 90f), IsCoin ? 0 : 20f);
            Spin.localPosition = new Vector3(0, 0.55f + Mathf.Sin((Time.time + _seed) * 2.2f) * 0.12f, 0);

            var m = MailyController.I;
            if (m == null || GameManager.I == null || GameManager.I.State != GameState.Playing) return;
            var d = m.transform.position - transform.position;
            if (Mathf.Abs(d.y) < 1.6f && new Vector2(d.x, d.z).sqrMagnitude < 1.25f * 1.25f) Take();
        }

        void Take()
        {
            _taken = true;
            _combo = Time.time - _comboTime < 2.5f ? _combo + 1 : 0; _comboTime = Time.time;
            float pitch = Mathf.Min(1f + _combo * 0.06f, 1.6f) * Random.Range(0.96f, 1.04f);
            if (Sfx.I != null) Sfx.I.Play3D(IsCoin ? Sfx.I.Coin : Sfx.I.Pickup, transform.position, 0.8f, pitch);
            Fx.Sparkle(Spin.position, IsCoin ? Palette.CoinGold : Palette.Bone);
            GameManager.I.AddTreat(IsCoin);
        }
    }
}

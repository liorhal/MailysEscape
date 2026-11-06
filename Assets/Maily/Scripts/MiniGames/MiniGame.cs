using System.Collections.Generic;
using UnityEngine;

namespace Maily
{
    /// <summary>Base for the kibbutz mini-games. Played in the open world with Maily's normal controls.</summary>
    public abstract class MiniGame
    {
        public MiniGameSpot Spot;
        public int Score;
        public float TimeLeft;

        protected readonly List<GameObject> Spawned = new List<GameObject>();

        public abstract string Status { get; }
        public abstract string ResultText { get; }
        public virtual bool ShowTimer => true;
        /// <summary>Played on a full-screen 2D board: Maily stands still and the mouse cursor is free.</summary>
        public virtual bool Is2D => false;
        /// <summary>0..1 hot/cold meter (bone hunt); &lt; 0 hides it.</summary>
        public virtual float Warmth => -1f;

        public abstract void Begin();
        /// <summary>Advance one frame; return true when the game is over.</summary>
        public abstract bool Tick(float dt);
        /// <summary>F pressed while running.</summary>
        public virtual void OnAction() { }
        /// <summary>0 none, 1 bronze, 2 silver, 3 gold.</summary>
        public abstract int Medal();

        public virtual void Cleanup()
        {
            foreach (var g in Spawned) if (g != null) Object.Destroy(g);
            Spawned.Clear();
        }

        // ---------------------------------------------------------------- helpers
        protected static Vector3 MailyPos => MailyController.I.transform.position;

        protected static float FlatDist(Vector3 a, Vector3 b) { a.y = 0; b.y = 0; return Vector3.Distance(a, b); }

        protected GameObject Prim(PrimitiveType t, Vector3 pos, Vector3 scale, Color c, string key, float emission = 0f, Transform parent = null)
        {
            var go = GameObject.CreatePrimitive(t);
            Object.Destroy(go.GetComponent<Collider>());
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = Mats.Get(key, c, 0.2f, emission);
            if (parent == null) Spawned.Add(go);
            return go;
        }

        protected GameObject Empty(string name, Vector3 pos)
        {
            var go = new GameObject(name); go.transform.position = pos; Spawned.Add(go); return go;
        }

        /// <summary>Open ground: no obstacle within r (Maily ignored), not inside a building, inside the fence.</summary>
        public static bool IsFree(Vector3 p, float r)
        {
            if (new Vector2(p.x, p.z).magnitude > 590f) return false;
            var m = MailyController.I != null ? MailyController.I.transform : null;
            foreach (var c in Physics.OverlapSphere(new Vector3(p.x, r + 0.25f, p.z), r, ~0, QueryTriggerInteraction.Ignore))
            {
                if (m != null && c.transform.IsChildOf(m)) continue;
                return false;
            }
            // under a roof? (mesh colliders don't report points deep inside a building)
            if (Physics.Raycast(new Vector3(p.x, 40f, p.z), Vector3.down, out var hit, 60f, ~0, QueryTriggerInteraction.Ignore) && hit.point.y > 0.6f)
                return false;
            return true;
        }

        public static Vector3? FindFree(Vector3 around, float minR, float maxR, float clear, int tries = 60)
        {
            for (int i = 0; i < tries; i++)
            {
                var d = Random.insideUnitCircle.normalized * Random.Range(minR, maxR);
                var p = new Vector3(around.x + d.x, 0.05f, around.z + d.y);
                if (IsFree(p, clear)) return p;
            }
            return null;
        }
    }
}

using AThousandDawns.Combat;
using AThousandDawns.Player;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 도깨비불 장판 (등불 귀신): 바닥에 잠시 남는 푸른 불. 안에 서 있으면 0.5초마다 맞고 감전 (잠시 느려짐). 끝 무렵엔 흐려져요.
    public sealed class GhostFire : MonoBehaviour
    {
        static readonly Color Fire = new Color(0.45f, 0.85f, 1f, 0.55f);

        PlayerController _player;
        Damageable _owner;
        Renderer _renderer;
        float _life, _total, _radius, _damage, _slow, _slowTime, _tick;

        public static GhostFire Create(Vector3 at, float radius, float life, float damage, float slow, float slowTime, PlayerController player, Damageable owner)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "도깨비불";
            EnemyFactory.StripCollider(go);
            go.transform.position = new Vector3(at.x, 0.03f, at.z);
            go.transform.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);
            var fire = go.AddComponent<GhostFire>();
            fire._renderer = go.GetComponent<Renderer>();
            fire._renderer.material.color = Fire;
            fire._renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fire._player = player;
            fire._owner = owner;
            fire._life = fire._total = life;
            fire._radius = radius;
            fire._damage = damage;
            fire._slow = slow;
            fire._slowTime = slowTime;
            return fire;
        }

        static readonly System.Collections.Generic.List<GhostFire> All = new System.Collections.Generic.List<GhostFire>();
        public static int Count => All.Count;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public static void ClearAll()
        {
            for (int i = All.Count - 1; i >= 0; i--) if (All[i] != null) Destroy(All[i].gameObject);
            All.Clear();
        }

        public bool Touches(Vector3 point) =>
            new Vector2(point.x - transform.position.x, point.z - transform.position.z).magnitude <= _radius;

        void Update()
        {
            _life -= Time.deltaTime;
            if (_life <= 0f)
            {
                Destroy(gameObject);
                return;
            }
            if (_renderer != null) _renderer.material.color = new Color(Fire.r, Fire.g, Fire.b, Fire.a * Mathf.Clamp01(_life / (_total * 0.3f)));
            if (_tick > 0f) _tick -= Time.deltaTime;
            if (_tick > 0f || _player == null || _player.IsDead) return;
            if (!Touches(_player.transform.position)) return;
            _tick = 0.5f;
            if (_player.TryReceiveHit(_damage, transform.position, 0f, _owner)) _player.ApplySlow(_slow, _slowTime);
        }
    }
}

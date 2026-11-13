using AThousandDawns.Combat;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 혈폭자의 핏덩이와 죽음의 폭발: 바닥 예고 원이 차오르다가 터져요. 반경 안이면 맞고, 핏덩이는 그 자리에 화상 장판을 남겨요.
    // 핏덩이는 떨어지기 전에 던진 혈폭자가 쓰러지면 터지지 않아요 (죽음의 폭발은 그대로 터져요).
    public sealed class BloodBurst : MonoBehaviour
    {
        EnemyDirector _director;
        Telegraph _marker;
        Damageable _owner;
        bool _hasOwner; // 던진 적이 있는 핏덩이 (죽음의 폭발 · 혈지 마기는 주인이 없어요)
        BurstKind _kind;
        float _timer, _total, _radius, _damage, _fireLife;

        public static int Live { get; private set; }

        public static BloodBurst Create(EnemyDirector director, Vector3 at, float radius, float fuse, float damage, Damageable owner, float fireLife, BurstKind kind = BurstKind.Default)
        {
            var go = new GameObject("핏덩이");
            go.transform.position = new Vector3(at.x, 0f, at.z);
            var b = go.AddComponent<BloodBurst>();
            b._director = director;
            b._owner = owner;
            b._hasOwner = owner != null;
            b._kind = kind != BurstKind.Default ? kind : owner != null ? BurstKind.BloodGlob : BurstKind.BloodDeath; // 핏덩이 / 죽음의 폭발
            b._timer = b._total = fuse;
            b._radius = radius;
            b._damage = damage;
            b._fireLife = fireLife;
            b._marker = Telegraph.Create(go.transform, director.Visuals.telegraphBase, director.Visuals.telegraphFill, director.Visuals.telegraphOutline);
            b._marker.Show(TelegraphShapes.Circle(radius), true);
            b._marker.SetPose(go.transform.position, Vector3.forward);
            b._marker.SetProgress(0f);
            return b;
        }

        void OnEnable() => Live++;
        void OnDisable() => Live--;

        void Update()
        {
            if (_hasOwner && (_owner == null || _owner.IsDead)) // 떨어지기 전에 던진 적을 쓰러뜨리면 핏덩이도 사라져요
            {
                Destroy(gameObject);
                return;
            }
            _timer -= Time.deltaTime;
            if (_marker != null) _marker.SetProgress(1f - Mathf.Clamp01(_timer / Mathf.Max(0.01f, _total)));
            if (_timer > 0f) return;
            var player = _director != null ? _director.Player : null;
            if (_director != null) _director.SpawnBurst(transform.position, _radius, _kind);
            if (player != null && !player.IsDead)
            {
                Vector3 d = player.transform.position - transform.position;
                d.y = 0f;
                if (d.magnitude <= _radius + player.BodyRadius) player.TryReceiveHit(_damage, transform.position, -1f, _owner);
            }
            if (_fireLife > 0f && _director != null && player != null)
                EnemyFirePatch.Create(transform.position, player, _director.Visuals.fire, _radius, _fireLife, 0.5f);
            Destroy(gameObject);
        }
    }
}

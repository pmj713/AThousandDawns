using AThousandDawns.Core;
using AThousandDawns.Player;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 플레이어를 태우는 바닥 불길 (열화 정예가 죽은 자리): 반경 안에 있으면 tick마다 화상 1중첩.
    public sealed class EnemyFirePatch : MonoBehaviour
    {
        PlayerController _player;
        float _radius, _life, _tick, _timer, _perStack;

        public static EnemyFirePatch Create(Vector3 at, PlayerController player, Material material, float radius, float life, float tick)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Fire (열화)";
            EnemyFactory.StripCollider(go);
            go.transform.position = new Vector3(at.x, 0.02f, at.z);
            go.transform.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);
            var renderer = go.GetComponent<Renderer>();
            if (material != null) renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var patch = go.AddComponent<EnemyFirePatch>();
            patch._player = player;
            patch._radius = radius;
            patch._life = life;
            patch._tick = Mathf.Max(0.05f, tick);
            patch._perStack = GameConfig.Get("player_burn.damage", 2f);
            return patch;
        }

        public float Radius => _radius;

        void Update()
        {
            _life -= Time.deltaTime;
            if (_life <= 0f)
            {
                Destroy(gameObject);
                return;
            }
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = _tick;
            if (_player == null || _player.IsDead) return;
            Vector3 d = _player.transform.position - transform.position;
            d.y = 0f;
            if (d.magnitude <= _radius + _player.BodyRadius) _player.AddBurnStack(_perStack);
        }
    }
}

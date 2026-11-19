using System.Collections.Generic;
using AThousandDawns.Combat;
using AThousandDawns.Player;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 슬라임 점액 웅덩이: 덮친 자리에 잠시 남아요. 안에 서 있는 동안 플레이어가 느려져요 (피해 · 경직 없음). 끝 무렵엔 흐려져요.
    //   도깨비불 장판(GhostFire)은 피해 0이어도 맞는 판정(경직 · 넉백)이 나서 따로 두었어요. 방을 옮기면 사라져요.
    public sealed class SlimePuddle : MonoBehaviour
    {
        static readonly Color Goo = new Color(0.45f, 0.85f, 0.4f, 0.5f);
        const float SlowLinger = 0.2f; // 웅덩이를 나온 뒤 둔화가 남는 시간

        PlayerController _player;
        Damageable _owner;
        Renderer _renderer;
        float _life, _total, _radius, _slow;

        public Damageable Owner => _owner; // 아트: 놓은 슬라임
        public float Radius => _radius;
        public float LifeLeft => Mathf.Max(0f, _life);

        static readonly List<SlimePuddle> All = new List<SlimePuddle>();
        public static int Count => All.Count;

        public static SlimePuddle Create(Vector3 at, float radius, float life, float slow, PlayerController player, Damageable owner)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "점액";
            EnemyFactory.StripCollider(go);
            go.transform.position = new Vector3(at.x, 0.025f, at.z);
            go.transform.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);
            var puddle = go.AddComponent<SlimePuddle>();
            puddle._renderer = go.GetComponent<Renderer>();
            puddle._renderer.material.color = Goo;
            puddle._renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            puddle._player = player;
            puddle._owner = owner;
            puddle._life = puddle._total = Mathf.Max(0.05f, life);
            puddle._radius = radius;
            puddle._slow = slow;
            return puddle;
        }

        void OnEnable()
        {
            All.Add(this);
            EnemyDirector.RoomReset += OnRoomReset;
        }

        void OnDisable()
        {
            All.Remove(this);
            EnemyDirector.RoomReset -= OnRoomReset;
        }

        void OnRoomReset()
        {
            if (this != null) Destroy(gameObject);
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
            if (_renderer != null) _renderer.material.color = new Color(Goo.r, Goo.g, Goo.b, Goo.a * Mathf.Clamp01(_life / (_total * 0.3f)));
            if (_player == null || _player.IsDead || !Touches(_player.transform.position)) return;
            _player.ApplySlow(_slow, SlowLinger);
        }
    }
}

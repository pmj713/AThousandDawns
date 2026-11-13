using AThousandDawns.Combat;
using AThousandDawns.Enemies;
using UnityEngine;
using UnityEngine.Rendering;

namespace AThousandDawns.Player
{
    // 잔영 가면: 대시를 시작한 자리에 잔상이 남아 주변 적(보스 빼고)을 끌어들여요.
    // explodeDamage가 있으면(0개면) 사라질 때 터지며 피해 (파생 피해: 강인함 무시, 방어력 적용, 경직 없음).
    public sealed class Afterimage : MonoBehaviour
    {
        static readonly Color ShadowColor = new Color(0.45f, 0.5f, 0.62f); // 임시
        static Material _material;
        const float StopDistance = 0.8f; // 잔상에 이만큼 붙으면 더 끌지 않아요

        float _life, _radius, _pullSpeed, _explodeDamage, _explodeRadius;

        public static void Create(Vector3 at, float life, float radius, float pullSpeed, float explodeDamage, float explodeRadius)
        {
            if (life <= 0f) return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "잔상";
            var collider = go.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            go.transform.position = new Vector3(at.x, 1f, at.z);
            go.transform.localScale = new Vector3(0.6f, 1f, 0.6f);
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = RuntimeMaterial.Unlit(ref _material, ShadowColor);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            var image = go.AddComponent<Afterimage>();
            image._life = life;
            image._radius = radius;
            image._pullSpeed = pullSpeed;
            image._explodeDamage = explodeDamage;
            image._explodeRadius = explodeRadius;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Vector3 center = transform.position;
            center.y = 0f;
            foreach (var enemy in FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None))
            {
                if (enemy.IsDead) continue;
                Vector3 offset = center - enemy.transform.position;
                offset.y = 0f;
                float distance = offset.magnitude;
                if (distance > _radius || distance <= StopDistance) continue;
                enemy.Displace(offset / distance * Mathf.Min(_pullSpeed * dt, distance - StopDistance));
            }

            _life -= dt;
            if (_life > 0f) return;
            if (_explodeDamage > 0f) Explode(center);
            Destroy(gameObject);
        }

        void Explode(Vector3 center)
        {
            foreach (var d in Damageable.All())
            {
                if (d.IsDead || !d.Targetable) continue;
                Vector3 offset = d.transform.position - center;
                offset.y = 0f;
                if (offset.magnitude > _explodeRadius + d.BodyRadius) continue;
                d.ApplyHit(DamageFormula.Proc(_explodeDamage, 1f, d.Defense), false, PopupStyle.Burst);
            }
        }
    }
}

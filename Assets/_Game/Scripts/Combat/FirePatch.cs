using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AThousandDawns.Combat
{
    // 바닥 불길. 간격마다 안에 있는 적에게 effect를 걸어요.
    //   절초 #열양 각인: 검이 떨어진 자리에 4초. 걸려 있는 화상의 지속 시간을 계속 갱신 (effect가 없을 때의 기본).
    //   염화 호리병: 화상 중인 적이 죽은 자리에 3초. 1초마다 화상 1중첩.
    public sealed class FirePatch : MonoBehaviour
    {
        static readonly Color FlameColor = new Color(0.89f, 0.35f, 0.13f); // #E25822 (임시)
        static Material _material;
        static readonly Collider[] Overlap = new Collider[32];

        float _radius, _life, _tick, _tickTimer;
        Action<StatusEffects> _effect;
        readonly HashSet<Damageable> _touched = new HashSet<Damageable>();

        public static FirePatch Create(Vector3 at, float radius, float life, float tick, Action<StatusEffects> effect = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "불길";
            var collider = go.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            go.transform.position = new Vector3(at.x, 0.03f, at.z);
            go.transform.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f);
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = RuntimeMaterial.Unlit(ref _material, FlameColor);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            var patch = go.AddComponent<FirePatch>();
            patch._radius = radius;
            patch._life = life;
            patch._tick = Mathf.Max(0.05f, tick);
            patch._effect = effect ?? (s => s.RefreshBurn());
            return patch;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _life -= dt;
            if (_life <= 0f)
            {
                Destroy(gameObject);
                return;
            }
            _tickTimer += dt;
            if (_tickTimer < _tick) return;
            _tickTimer -= _tick;

            _touched.Clear();
            int found = Physics.OverlapSphereNonAlloc(transform.position, _radius + 1f, Overlap, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < found; i++)
            {
                var d = Overlap[i].GetComponentInParent<Damageable>();
                if (d == null || d.IsDead || !d.Targetable || !_touched.Add(d)) continue;
                Vector3 offset = d.transform.position - transform.position;
                offset.y = 0f;
                if (offset.magnitude > _radius + d.BodyRadius) continue;
                _effect(StatusEffects.For(d));
            }
        }
    }
}

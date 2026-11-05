using System;
using UnityEngine;

namespace AThousandDawns.Combat
{
    // 맞을 수 있는 대상. 강인함·방어력은 몬스터 시트 값을 넣어요.
    public sealed class Damageable : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] float maxHealth = 100f;
        [SerializeField] float toughness;
        [SerializeField, Range(0f, 0.9f)] float defense;
        [SerializeField] bool invincible;
        [SerializeField] float bodyRadius = 0.4f;
        [SerializeField] Renderer flashRenderer;

        MaterialPropertyBlock _block;
        Color _baseColor = Color.white;
        float _flashTimer;

        public float MaxHealth => maxHealth;
        public float Health { get; private set; }
        public float Toughness => toughness;
        public float Defense => defense;
        public float BodyRadius => bodyRadius;
        public bool IsDead => !invincible && Health <= 0f;

        public event Action<Damageable, int> Damaged;

        void Awake()
        {
            Health = maxHealth;
            if (flashRenderer != null && flashRenderer.sharedMaterial != null && flashRenderer.sharedMaterial.HasProperty(BaseColorId))
                _baseColor = flashRenderer.sharedMaterial.GetColor(BaseColorId);
        }

        public void ApplyHit(int amount)
        {
            if (IsDead) return;
            if (!invincible) Health = Mathf.Max(0f, Health - amount);
            _flashTimer = 0.08f;
            DamagePopups.Spawn(transform.position + Vector3.up * 2.2f, amount);
            Damaged?.Invoke(this, amount);
        }

        void Update()
        {
            if (flashRenderer == null) return;
            if (_flashTimer <= 0f && _block == null) return;
            _flashTimer -= Time.deltaTime;
            _block ??= new MaterialPropertyBlock();
            _block.SetColor(BaseColorId, _flashTimer > 0f ? Color.white : _baseColor);
            flashRenderer.SetPropertyBlock(_block);
        }
    }
}

using System;
using UnityEngine;

namespace AThousandDawns.Combat
{
    // 플레이어 공격 종류. 방패 산적처럼 종류에 따라 다르게 받는 적이 있어요 (대시 공격 = 방패 파괴).
    public enum HitTag { Basic, Finisher, DashAttack, Skill, Ultimate }

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

        // 풀숲에 숨은 청죽사처럼 지금은 맞힐 수 없는 대상
        public bool Targetable { get; set; } = true;

        // 맞는 방향·공격 종류에 따른 상황 배율(⑧). 방패 정면 0.2처럼. 대시 공격에 방패가 깨지듯 상태를 바꿀 수도 있어요.
        public Func<Vector3, HitTag, float> SituationalResolver { get; set; }

        public float ResolveSituational(Vector3 attackerPosition, HitTag tag) =>
            SituationalResolver != null ? SituationalResolver(attackerPosition, tag) : 1f;

        public event Action<Damageable, int> Damaged;

        void Awake()
        {
            Health = maxHealth;
            ReadBaseColor();
        }

        // 코드로 만드는 적이 쓰는 설정. 체력을 가득 채워요.
        public void Configure(float newMaxHealth, float newToughness, float newDefense, float newBodyRadius, Renderer newFlashRenderer)
        {
            maxHealth = newMaxHealth;
            toughness = newToughness;
            defense = newDefense;
            bodyRadius = newBodyRadius;
            flashRenderer = newFlashRenderer;
            invincible = false;
            Health = maxHealth;
            ReadBaseColor();
        }

        void ReadBaseColor()
        {
            if (flashRenderer != null && flashRenderer.sharedMaterial != null && flashRenderer.sharedMaterial.HasProperty(BaseColorId))
                _baseColor = flashRenderer.sharedMaterial.GetColor(BaseColorId);
        }

        // reacts = false: 화상·번개 같은 파생 피해. 경직을 일으키지 않아요 (적이 LastHitReacts로 확인).
        public void ApplyHit(int amount, bool reacts = true, PopupStyle style = PopupStyle.Normal)
        {
            if (IsDead) return;
            if (!invincible) Health = Mathf.Max(0f, Health - amount);
            if (reacts) _flashTimer = 0.08f;
            DamagePopups.Spawn(transform.position + Vector3.up * 2.2f, amount, style);
            LastHitReacts = reacts;
            Damaged?.Invoke(this, amount);
        }

        public bool LastHitReacts { get; private set; } = true;

        // 빙결처럼 몸 색을 바꿔 둘 때 (null이면 원래 색)
        public void SetTint(Color? tint)
        {
            _tint = tint;
            _block ??= new MaterialPropertyBlock();
        }

        Color? _tint;

        void Update()
        {
            if (flashRenderer == null) return;
            if (_flashTimer <= 0f && _block == null) return;
            _flashTimer -= Time.deltaTime;
            _block ??= new MaterialPropertyBlock();
            _block.SetColor(BaseColorId, _flashTimer > 0f ? Color.white : _tint ?? _baseColor);
            flashRenderer.SetPropertyBlock(_block);
        }
    }
}

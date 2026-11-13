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
        public static event Action<Damageable> AnyDied; // 처치 시 효과 (염화 호리병, 구리 동전)
        public static event Action<Damageable, int> AnyDamaged; // 판 기록 (준 피해 · 최대 단일 피해)

        void Awake()
        {
            Health = maxHealth;
            ReadBaseColor();
        }

        // 켜진 대상 목록 (맞힐 때마다 FindObjectsByType로 씬을 뒤지며 배열을 만들지 않게)
        static readonly System.Collections.Generic.List<Damageable> Live = new System.Collections.Generic.List<Damageable>();
        static readonly System.Collections.Generic.Stack<System.Collections.Generic.List<Damageable>> Spare = new System.Collections.Generic.Stack<System.Collections.Generic.List<Damageable>>();

        void OnEnable() => Live.Add(this);
        void OnDisable() => Live.Remove(this);

        // foreach (var d in Damageable.All()): 지금 켜진 대상 전부. 도는 도중 맞은 적이 죽거나 새 적이 나와도 되게 복사본을 돌고,
        // 복사본 목록은 다 돈 뒤 돌려받아 다시 써요 (할당 없음). 여러 번 돌 목록은 CopyAll로 받아요.
        public static Snapshot All()
        {
            var list = Spare.Count > 0 ? Spare.Pop() : new System.Collections.Generic.List<Damageable>(Live.Count + 8);
            list.AddRange(Live);
            return new Snapshot(list);
        }

        public static void CopyAll(System.Collections.Generic.List<Damageable> into)
        {
            into.Clear();
            into.AddRange(Live);
        }

        public struct Snapshot : IDisposable
        {
            System.Collections.Generic.List<Damageable> _list;
            int _i;

            internal Snapshot(System.Collections.Generic.List<Damageable> list) { _list = list; _i = -1; }
            public Snapshot GetEnumerator() => this;
            public Damageable Current => _list[_i];
            public bool MoveNext() => ++_i < _list.Count;

            public void Dispose()
            {
                if (_list == null) return;
                _list.Clear();
                Spare.Push(_list);
                _list = null;
            }
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

        // 정예 철포삼: 강인함 +2
        public void AddToughness(float amount) => toughness += amount;

        // 정예 회춘
        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;
            Health = Mathf.Min(maxHealth, Health + amount);
        }

        // 짙은 길: 적 체력 +30% 처럼 최대 체력에 곱하고 가득 채워요.
        public void ScaleMaxHealth(float multiplier)
        {
            maxHealth *= multiplier;
            Health = maxHealth;
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
            if (Ward > 0 && amount > 0) // 가호가 먼저 막아요
            {
                int absorbed = Mathf.Min(Ward, amount);
                Ward -= absorbed;
                amount -= absorbed;
                if (Ward == 0) SetTint(null);
            }
            if (!invincible) Health = Mathf.Max(0f, Health - amount);
            if (reacts) _flashTimer = 0.08f;
            DamagePopups.Spawn(transform.position + Vector3.up * 2.2f, amount, style);
            LastHitReacts = reacts;
            Damaged?.Invoke(this, amount);
            AnyDamaged?.Invoke(this, amount);
            if (IsDead) AnyDied?.Invoke(this);
        }

        public bool LastHitReacts { get; private set; } = true;

        // 목탁승 가호: 이만큼의 피해를 먼저 막아요. 가호가 있는 동안 몸이 금빛이에요 (다 깎이면 원래 색)
        static readonly Color WardColor = new Color(1f, 0.82f, 0.4f);
        public int Ward { get; private set; }

        public void AddWard(int amount)
        {
            if (IsDead || amount <= 0) return;
            Ward = Mathf.Max(Ward, amount);
            SetTint(WardColor);
        }

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

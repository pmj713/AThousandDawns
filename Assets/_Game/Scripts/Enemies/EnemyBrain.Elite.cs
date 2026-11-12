using AThousandDawns.Combat;
using AThousandDawns.Core;
using System.Collections.Generic;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 정예 특성 (몬스터 탭 정예 첫 버전). 죽림·폐사찰은 하나, 장강 수채는 1~2개 (반반), 천마총은 2개 (몬스터 B142).
    public enum EliteTrait { None, IronShirt, Rejuvenation, Blaze, Split }

    // 정예: 일반 적에 특성 하나를 붙인 강화판 (몬스터 탭 정예 공통 규칙).
    // 체력 ×3 · 공격 ×1.2 · 크기 ×1.2, 이름 앞에 특성, 머리 위 표시는 모양으로 구분 (철포삼 네모 · 회춘 공 · 열화 기둥).
    // 철포삼: 경직·늦춰짐·나팔 끊기 없음, 강인함 +2 (빙결·둔화는 걸림)
    // 회춘: 마지막으로 맞고 3초 뒤부터 초당 최대 체력 10% 회복. 화상 중에는 회복 안 함
    // 열화: 맞히면 플레이어 화상 1중첩 (PlayerController), 죽은 자리에 불길, 자신이 받는 화상 피해 절반
    // 분열: 쓰러지면 같은 적 둘로 갈라져요 (정예가 아닌 작은 것, 그 지역 체력의 35%). 터지는 적 · 숨는 청죽사는 붙이지 않아요
    public abstract partial class EnemyBrain
    {
        static Material _ironMat, _rejuvMat, _blazeMat, _splitMat;

        readonly List<EliteTrait> _traits = new List<EliteTrait>(); // 장강 수채부터 둘까지 (몬스터 B142)
        float _sinceHit, _eliteScale = 1f;
        string _baseName;

        public bool IsElite { get; private set; }
        public EliteTrait Trait => _traits.Count > 0 ? _traits[0] : EliteTrait.None; // 첫 특성
        public IReadOnlyList<EliteTrait> Traits => _traits;
        public bool HasTrait(EliteTrait trait) => _traits.Contains(trait);
        public bool IsIronShirt => HasTrait(EliteTrait.IronShirt);
        public bool IsBlaze => HasTrait(EliteTrait.Blaze);

        public static string TraitLabel(EliteTrait trait) => trait switch
        {
            EliteTrait.IronShirt => "철포삼",
            EliteTrait.Rejuvenation => "회춘",
            EliteTrait.Blaze => "열화",
            EliteTrait.Split => "분열",
            _ => "",
        };

        // 붙이지 않는 조합: 방패형 + 철포삼, 자폭형 + 회춘 (몬스터 C144). 보스는 정예가 아니에요.
        public static bool Allows(EnemyKind kind, EliteTrait trait) =>
            kind != EnemyKind.Nokrim && kind != EnemyKind.Assassin && kind != EnemyKind.Qingzhu && kind != EnemyKind.IronFist && kind != EnemyKind.Takrang && kind != EnemyKind.IronDragon && kind != EnemyKind.BloodShadow && kind != EnemyKind.HeavenlyDemon && trait != EliteTrait.None
            && kind != EnemyKind.WaterGhost // 숨는 수귀는 정예가 아니에요 (머리 위 표시가 보여서)
            && !(kind == EnemyKind.Shield && trait == EliteTrait.IronShirt)
            && !(kind == EnemyKind.Rattan && trait == EliteTrait.IronShirt)
            && !(kind == EnemyKind.DemonGuard && trait == EliteTrait.IronShirt)
            && !(kind == EnemyKind.Bomber && trait == EliteTrait.Rejuvenation)
            && !(trait == EliteTrait.Split && (kind == EnemyKind.Bomber || kind == EnemyKind.BloodBomber || kind == EnemyKind.Snake)); // 터지는 적 · 숨는 적은 분열 없음

        // 만들어진 바로 뒤에 불러요 (EnemyDirector.Spawn 다음)
        public void MakeElite(EliteTrait trait)
        {
            if (IsElite || !Allows(Kind, trait)) return;
            IsElite = true;
            _baseName = DisplayName;
            Body.ScaleMaxHealth(GameConfig.Get("elite.hp_mult", 3f));
            AttackPower *= GameConfig.Get("elite.atk_mult", 1.2f);
            float scale = GameConfig.Get("elite.scale", 1.2f);
            if (_visual != null) _visual.localScale *= scale;
            _eliteScale = scale;
            ApplyTrait(trait);
        }

        // 장강 수채부터: 정예에 특성을 하나 더 (체력·공격 배율은 한 번만)
        public void AddEliteTrait(EliteTrait trait)
        {
            if (!IsElite || HasTrait(trait) || !Allows(Kind, trait)) return;
            ApplyTrait(trait);
        }

        void ApplyTrait(EliteTrait trait)
        {
            _traits.Add(trait);
            var labels = new List<string>();
            foreach (var t in _traits) labels.Add(TraitLabel(t));
            DisplayName = $"{string.Join("·", labels)} · {_baseName}";
            if (trait == EliteTrait.IronShirt) Body.AddToughness(GameConfig.Get("elite.iron_toughness", 2f));
            if (trait == EliteTrait.Blaze) Status.BurnTakenMultiplier = GameConfig.Get("elite.blaze_burn_taken", 0.5f);
            BuildTraitMark(trait, _eliteScale, _traits.Count - 1);
        }

        void BuildTraitMark(EliteTrait trait, float scale, int index)
        {
            var shape = trait == EliteTrait.IronShirt ? PrimitiveType.Cube : trait == EliteTrait.Rejuvenation ? PrimitiveType.Sphere : trait == EliteTrait.Split ? PrimitiveType.Capsule : PrimitiveType.Cylinder;
            var mark = GameObject.CreatePrimitive(shape);
            mark.name = $"Elite_{trait}";
            EnemyFactory.StripCollider(mark);
            mark.transform.SetParent(transform, false);
            mark.transform.localPosition = Vector3.up * (Height * scale + 0.35f + index * 0.4f); // 둘째 특성은 그 위에
            mark.transform.localScale = trait == EliteTrait.Blaze ? new Vector3(0.22f, 0.2f, 0.22f) : trait == EliteTrait.Split ? new Vector3(0.2f, 0.16f, 0.2f) : Vector3.one * 0.28f;
            var color = trait == EliteTrait.IronShirt ? new Color(0.72f, 0.75f, 0.8f)   // 쇠빛
                : trait == EliteTrait.Rejuvenation ? new Color(0.45f, 0.85f, 0.45f)    // 초록 숨결
                : trait == EliteTrait.Split ? new Color(0.62f, 0.4f, 0.85f)            // 갈라지는 보랏빛
                : new Color(1f, 0.45f, 0.1f);                                           // 불꽃
            var renderer = mark.GetComponent<Renderer>();
            renderer.sharedMaterial = trait == EliteTrait.IronShirt ? RuntimeMaterial.Unlit(ref _ironMat, color)
                : trait == EliteTrait.Rejuvenation ? RuntimeMaterial.Unlit(ref _rejuvMat, color)
                : trait == EliteTrait.Split ? RuntimeMaterial.Unlit(ref _splitMat, color)
                : RuntimeMaterial.Unlit(ref _blazeMat, color);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // 회춘: 3초 동안 안 맞고, 화상이 없으면 회복
        void TickElite(float dt)
        {
            if (!HasTrait(EliteTrait.Rejuvenation)) return;
            _sinceHit += dt;
            if (_sinceHit < GameConfig.Get("elite.rejuv_delay", 3f) || Status.BurnStacks > 0) return;
            Body.Heal(Body.MaxHealth * GameConfig.Get("elite.rejuv_ratio", 0.1f) * dt);
        }

        // 분열: 같은 적 둘 (정예 아님, 지역 배율은 SummonScale로) · 체력은 elite.split_health
        void Split()
        {
            int count = Mathf.RoundToInt(GameConfig.GetOptional("elite.split_count", 2f));
            float health = GameConfig.GetOptional("elite.split_health", 0.35f);
            Vector3 side = Vector3.Cross(Vector3.up, transform.forward).normalized;
            EnemyBrain first = null, second = null;
            for (int i = 0; i < count; i++)
            {
                Vector3 at = transform.position + side * ((i - (count - 1) * 0.5f) * 1.2f);
                if (!Director.IsFree(at, 0.45f)) at = transform.position;
                var child = Director.SpawnSummon(Kind, at);
                if (child == null) continue;
                child.Body.ScaleMaxHealth(health);
                child.DisplayName = $"갈라진 {child.DisplayName}";
                SplitChildren++;
                if (first == null) first = child; else if (second == null) second = child;
            }
            SplitApart?.Invoke(this, transform.position, first, second); // 갈라진 둘을 만든 뒤 (아트: 갈라짐 이펙트)
        }

        public static int SplitChildren { get; private set; } // 테스트용: 지금까지 갈라져 나온 수
        public static event System.Action<EnemyBrain, Vector3, EnemyBrain, EnemyBrain> SplitApart; // 쓰러진 정예, 쓰러진 자리, 갈라진 둘 (없으면 null)

        // 열화: 죽은 자리에 불길 · 분열: 둘로 갈라져요
        void OnEliteDied()
        {
            if (HasTrait(EliteTrait.Split)) Split();
            if (!HasTrait(EliteTrait.Blaze)) return;
            EnemyFirePatch.Create(transform.position, Player, Director.Visuals.fire,
                GameConfig.Get("elite.blaze_fire_radius", 1.5f),
                GameConfig.Get("elite.blaze_fire_life", 4f),
                GameConfig.Get("elite.blaze_fire_tick", 0.5f));
        }
    }
}

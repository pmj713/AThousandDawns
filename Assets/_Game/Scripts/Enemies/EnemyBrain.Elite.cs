using AThousandDawns.Combat;
using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 정예 특성 (몬스터 탭 정예 첫 버전). 죽림은 하나만 붙어요.
    public enum EliteTrait { None, IronShirt, Rejuvenation, Blaze }

    // 정예: 일반 적에 특성 하나를 붙인 강화판 (몬스터 탭 정예 공통 규칙).
    // 체력 ×3 · 공격 ×1.2 · 크기 ×1.2, 이름 앞에 특성, 머리 위 표시는 모양으로 구분 (철포삼 네모 · 회춘 공 · 열화 기둥).
    // 철포삼: 경직·늦춰짐·나팔 끊기 없음, 강인함 +2 (빙결·둔화는 걸림)
    // 회춘: 마지막으로 맞고 3초 뒤부터 초당 최대 체력 10% 회복. 화상 중에는 회복 안 함
    // 열화: 맞히면 플레이어 화상 1중첩 (PlayerController), 죽은 자리에 불길, 자신이 받는 화상 피해 절반
    public abstract partial class EnemyBrain
    {
        static Material _ironMat, _rejuvMat, _blazeMat;

        EliteTrait _trait;
        float _sinceHit;

        public bool IsElite { get; private set; }
        public EliteTrait Trait => _trait;
        public bool IsIronShirt => _trait == EliteTrait.IronShirt;
        public bool IsBlaze => _trait == EliteTrait.Blaze;

        public static string TraitLabel(EliteTrait trait) => trait switch
        {
            EliteTrait.IronShirt => "철포삼",
            EliteTrait.Rejuvenation => "회춘",
            EliteTrait.Blaze => "열화",
            _ => "",
        };

        // 붙이지 않는 조합: 방패형 + 철포삼, 자폭형 + 회춘 (몬스터 C144). 보스는 정예가 아니에요.
        public static bool Allows(EnemyKind kind, EliteTrait trait) =>
            kind != EnemyKind.Nokrim && kind != EnemyKind.Assassin && kind != EnemyKind.Qingzhu && kind != EnemyKind.IronFist && trait != EliteTrait.None
            && kind != EnemyKind.WaterGhost // 숨는 수귀는 정예가 아니에요 (머리 위 표시가 보여서)
            && !(kind == EnemyKind.Shield && trait == EliteTrait.IronShirt)
            && !(kind == EnemyKind.Rattan && trait == EliteTrait.IronShirt)
            && !(kind == EnemyKind.Bomber && trait == EliteTrait.Rejuvenation);

        // 만들어진 바로 뒤에 불러요 (EnemyDirector.Spawn 다음)
        public void MakeElite(EliteTrait trait)
        {
            if (IsElite || !Allows(Kind, trait)) return;
            IsElite = true;
            _trait = trait;
            Body.ScaleMaxHealth(GameConfig.Get("elite.hp_mult", 3f));
            AttackPower *= GameConfig.Get("elite.atk_mult", 1.2f);
            float scale = GameConfig.Get("elite.scale", 1.2f);
            if (_visual != null) _visual.localScale *= scale;
            DisplayName = $"{TraitLabel(trait)} · {DisplayName}";

            if (trait == EliteTrait.IronShirt) Body.AddToughness(GameConfig.Get("elite.iron_toughness", 2f));
            if (trait == EliteTrait.Blaze) Status.BurnTakenMultiplier = GameConfig.Get("elite.blaze_burn_taken", 0.5f);
            BuildTraitMark(trait, scale);
        }

        void BuildTraitMark(EliteTrait trait, float scale)
        {
            var shape = trait == EliteTrait.IronShirt ? PrimitiveType.Cube : trait == EliteTrait.Rejuvenation ? PrimitiveType.Sphere : PrimitiveType.Cylinder;
            var mark = GameObject.CreatePrimitive(shape);
            mark.name = $"Elite_{trait}";
            EnemyFactory.StripCollider(mark);
            mark.transform.SetParent(transform, false);
            mark.transform.localPosition = Vector3.up * (Height * scale + 0.35f);
            mark.transform.localScale = trait == EliteTrait.Blaze ? new Vector3(0.22f, 0.2f, 0.22f) : Vector3.one * 0.28f;
            var color = trait == EliteTrait.IronShirt ? new Color(0.72f, 0.75f, 0.8f)   // 쇠빛
                : trait == EliteTrait.Rejuvenation ? new Color(0.45f, 0.85f, 0.45f)    // 초록 숨결
                : new Color(1f, 0.45f, 0.1f);                                           // 불꽃
            var renderer = mark.GetComponent<Renderer>();
            renderer.sharedMaterial = trait == EliteTrait.IronShirt ? RuntimeMaterial.Unlit(ref _ironMat, color)
                : trait == EliteTrait.Rejuvenation ? RuntimeMaterial.Unlit(ref _rejuvMat, color)
                : RuntimeMaterial.Unlit(ref _blazeMat, color);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // 회춘: 3초 동안 안 맞고, 화상이 없으면 회복
        void TickElite(float dt)
        {
            if (_trait != EliteTrait.Rejuvenation) return;
            _sinceHit += dt;
            if (_sinceHit < GameConfig.Get("elite.rejuv_delay", 3f) || Status.BurnStacks > 0) return;
            Body.Heal(Body.MaxHealth * GameConfig.Get("elite.rejuv_ratio", 0.1f) * dt);
        }

        // 열화: 죽은 자리에 불길
        void OnEliteDied()
        {
            if (_trait != EliteTrait.Blaze) return;
            EnemyFirePatch.Create(transform.position, Player, Director.Visuals.fire,
                GameConfig.Get("elite.blaze_fire_radius", 1.5f),
                GameConfig.Get("elite.blaze_fire_life", 4f),
                GameConfig.Get("elite.blaze_fire_tick", 0.5f));
        }
    }
}

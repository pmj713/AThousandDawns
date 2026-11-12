using AThousandDawns.Combat;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 시험판용 회색 블록 적. 모델이 생기면 프리팹으로 바꿀 자리예요.
    public static class EnemyFactory
    {
        struct Shape
        {
            public string Name;
            public float Radius, Height;
            public PrimitiveType Primitive;
            public Vector3 BodyPosition, BodyScale;
            public Vector3 MarkPosition, MarkScale; // 바라보는 방향 표시 (칼, 엄니, 활, 방패, 나팔, 화약통, 머리)
        }

        static Shape ShapeOf(EnemyKind kind) => kind switch
        {
            EnemyKind.Boar => new Shape
            {
                Name = "멧돼지", Radius = 0.6f, Height = 1.2f, Primitive = PrimitiveType.Cube,
                BodyPosition = new Vector3(0f, 0.5f, 0f), BodyScale = new Vector3(1.1f, 0.9f, 1.5f),
                MarkPosition = new Vector3(0f, 0.45f, 0.85f), MarkScale = new Vector3(0.7f, 0.15f, 0.25f),
            },
            EnemyKind.Archer => new Shape
            {
                Name = "산적 궁수", Radius = 0.35f, Height = 1.6f, Primitive = PrimitiveType.Capsule,
                BodyPosition = new Vector3(0f, 0.8f, 0f), BodyScale = new Vector3(0.6f, 0.8f, 0.6f),
                MarkPosition = new Vector3(0f, 1.0f, 0.35f), MarkScale = new Vector3(0.6f, 0.08f, 0.08f),
            },
            EnemyKind.Hound => new Shape
            {
                Name = "들개", Radius = 0.3f, Height = 0.8f, Primitive = PrimitiveType.Cube,
                BodyPosition = new Vector3(0f, 0.35f, 0f), BodyScale = new Vector3(0.45f, 0.45f, 0.9f),
                MarkPosition = new Vector3(0f, 0.5f, 0.5f), MarkScale = new Vector3(0.25f, 0.2f, 0.3f),
            },
            EnemyKind.Shield => new Shape
            {
                Name = "방패 산적", Radius = 0.4f, Height = 1.7f, Primitive = PrimitiveType.Capsule,
                BodyPosition = new Vector3(0f, 0.85f, 0f), BodyScale = new Vector3(0.8f, 0.85f, 0.8f),
                MarkPosition = new Vector3(0f, 0.85f, 0.5f), MarkScale = new Vector3(0.95f, 1.05f, 0.12f),
            },
            EnemyKind.Horn => new Shape
            {
                Name = "나팔수", Radius = 0.35f, Height = 1.6f, Primitive = PrimitiveType.Capsule,
                BodyPosition = new Vector3(0f, 0.8f, 0f), BodyScale = new Vector3(0.6f, 0.8f, 0.6f),
                MarkPosition = new Vector3(0.2f, 1.25f, 0.35f), MarkScale = new Vector3(0.14f, 0.14f, 0.55f),
            },
            EnemyKind.Bomber => new Shape
            {
                Name = "화약 산적", Radius = 0.35f, Height = 1.5f, Primitive = PrimitiveType.Capsule,
                BodyPosition = new Vector3(0f, 0.75f, 0f), BodyScale = new Vector3(0.65f, 0.75f, 0.65f),
                MarkPosition = new Vector3(0f, 1.0f, -0.4f), MarkScale = new Vector3(0.45f, 0.55f, 0.35f),
            },
            EnemyKind.Snake => new Shape
            {
                Name = "청죽사", Radius = 0.3f, Height = 0.6f, Primitive = PrimitiveType.Cube,
                BodyPosition = new Vector3(0f, 0.15f, 0f), BodyScale = new Vector3(0.35f, 0.25f, 1.2f),
                MarkPosition = new Vector3(0f, 0.22f, 0.7f), MarkScale = new Vector3(0.3f, 0.2f, 0.3f),
            },
            EnemyKind.Assassin => new Shape
            {
                Name = "마교 살수", Radius = 0.35f, Height = 1.7f, Primitive = PrimitiveType.Capsule,
                BodyPosition = new Vector3(0f, 0.85f, 0f), BodyScale = new Vector3(0.55f, 0.85f, 0.55f),
                MarkPosition = new Vector3(0f, 0.9f, 0.35f), MarkScale = new Vector3(0.7f, 0.06f, 0.06f), // 쌍검
            },
            EnemyKind.Qingzhu => new Shape
            {
                Name = "청죽객", Radius = 0.4f, Height = 1.9f, Primitive = PrimitiveType.Capsule,
                BodyPosition = new Vector3(0f, 0.95f, 0f), BodyScale = new Vector3(0.65f, 0.95f, 0.65f),
                MarkPosition = new Vector3(0.3f, 1.0f, 0.5f), MarkScale = new Vector3(0.06f, 0.06f, 1.1f), // 장검
            },
            EnemyKind.IronFist => new Shape
            {
                Name = "철권 마웅", Radius = 0.55f, Height = 2.1f, Primitive = PrimitiveType.Capsule,
                BodyPosition = new Vector3(0f, 1.05f, 0f), BodyScale = new Vector3(1.1f, 1.05f, 1.1f),
                MarkPosition = new Vector3(0f, 1.2f, 0.6f), MarkScale = new Vector3(0.9f, 0.3f, 0.3f), // 쇠주먹
            },
            EnemyKind.Nokrim => new Shape
            {
                Name = "녹림왕", Radius = 0.7f, Height = 2.4f, Primitive = PrimitiveType.Capsule,
                BodyPosition = new Vector3(0f, 1.2f, 0f), BodyScale = new Vector3(1.3f, 1.2f, 1.3f),
                MarkPosition = new Vector3(0.8f, 1.3f, 0.7f), MarkScale = new Vector3(0.14f, 0.3f, 2f), // 대도
            },
            // ── 폐사찰
            EnemyKind.Wraith => new Shape
            {
                Name = "망령", Radius = 0.35f, Height = 1.6f, Primitive = PrimitiveType.Capsule,
                BodyPosition = new Vector3(0f, 1.0f, 0f), BodyScale = new Vector3(0.55f, 0.7f, 0.55f), // 떠 있어요
                MarkPosition = new Vector3(0f, 1.3f, 0.3f), MarkScale = new Vector3(0.25f, 0.25f, 0.25f), // 혼불
            },
            EnemyKind.Arhat => new Shape
            {
                Name = "석상 나한", Radius = 0.6f, Height = 2.0f, Primitive = PrimitiveType.Cube,
                BodyPosition = new Vector3(0f, 1.0f, 0f), BodyScale = new Vector3(1.1f, 2.0f, 0.9f),
                MarkPosition = new Vector3(0f, 1.3f, 0.5f), MarkScale = new Vector3(0.8f, 0.3f, 0.2f), // 합장한 손
            },
            EnemyKind.Moktak => new Shape
            {
                Name = "목탁승", Radius = 0.35f, Height = 1.6f, Primitive = PrimitiveType.Capsule,
                BodyPosition = new Vector3(0f, 0.8f, 0f), BodyScale = new Vector3(0.65f, 0.8f, 0.65f),
                MarkPosition = new Vector3(0.25f, 1.0f, 0.4f), MarkScale = new Vector3(0.3f, 0.25f, 0.3f), // 목탁
            },
            EnemyKind.Monk => new Shape
            {
                Name = "파계승", Radius = 0.4f, Height = 1.8f, Primitive = PrimitiveType.Capsule,
                BodyPosition = new Vector3(0f, 0.9f, 0f), BodyScale = new Vector3(0.75f, 0.9f, 0.75f),
                MarkPosition = new Vector3(0.35f, 1.0f, 0.4f), MarkScale = new Vector3(0.07f, 0.07f, 1.4f), // 선장
            },
            EnemyKind.Lantern => new Shape
            {
                Name = "등불 귀신", Radius = 0.35f, Height = 1.4f, Primitive = PrimitiveType.Sphere,
                BodyPosition = new Vector3(0f, 0.9f, 0f), BodyScale = new Vector3(0.7f, 0.8f, 0.7f),
                MarkPosition = new Vector3(0f, 1.4f, 0f), MarkScale = new Vector3(0.2f, 0.2f, 0.2f), // 등불 고리
            },
            EnemyKind.Hyegwang => new Shape
            {
                Name = "파계승 혜광", Radius = 0.55f, Height = 2.1f, Primitive = PrimitiveType.Capsule,
                BodyPosition = new Vector3(0f, 1.05f, 0f), BodyScale = new Vector3(1.0f, 1.05f, 1.0f),
                MarkPosition = new Vector3(0.45f, 1.1f, 0.4f), MarkScale = new Vector3(0.1f, 0.1f, 2.2f), // 긴 선장
            },
            EnemyKind.BloodBuddha => new Shape
            {
                Name = "혈불", Radius = 1.0f, Height = 3.2f, Primitive = PrimitiveType.Capsule,
                BodyPosition = new Vector3(0f, 1.6f, 0f), BodyScale = new Vector3(2.0f, 1.6f, 1.6f), // 거대 불상
                MarkPosition = new Vector3(0f, 2.6f, 0.7f), MarkScale = new Vector3(0.6f, 0.6f, 0.3f), // 붉은 얼굴
            },
            // ── 장강 수채
            EnemyKind.Pirate => new Shape
            {
                Name = "수적 졸개", Radius = 0.35f, Height = 1.6f, Primitive = PrimitiveType.Capsule,
                BodyPosition = new Vector3(0f, 0.8f, 0f), BodyScale = new Vector3(0.7f, 0.8f, 0.7f),
                MarkPosition = new Vector3(0.3f, 0.9f, 0.4f), MarkScale = new Vector3(0.1f, 0.08f, 0.6f), // 갈고리 칼
            },
            EnemyKind.Harpooner => new Shape
            {
                Name = "작살수", Radius = 0.35f, Height = 1.7f, Primitive = PrimitiveType.Capsule,
                BodyPosition = new Vector3(0f, 0.85f, 0f), BodyScale = new Vector3(0.65f, 0.85f, 0.65f),
                MarkPosition = new Vector3(0.3f, 1.2f, 0.3f), MarkScale = new Vector3(0.05f, 0.05f, 1.4f), // 작살
            },
            EnemyKind.Anchor => new Shape
            {
                Name = "쇠닻 거한", Radius = 0.65f, Height = 2.2f, Primitive = PrimitiveType.Capsule,
                BodyPosition = new Vector3(0f, 1.1f, 0f), BodyScale = new Vector3(1.3f, 1.1f, 1.2f),
                MarkPosition = new Vector3(0.6f, 0.4f, 0.6f), MarkScale = new Vector3(0.6f, 0.6f, 0.15f), // 쇠닻
            },
            EnemyKind.Rattan => new Shape
            {
                Name = "등패수", Radius = 0.4f, Height = 1.7f, Primitive = PrimitiveType.Capsule,
                BodyPosition = new Vector3(0f, 0.85f, 0f), BodyScale = new Vector3(0.75f, 0.85f, 0.75f),
                MarkPosition = new Vector3(0f, 0.85f, 0.5f), MarkScale = new Vector3(1.0f, 1.0f, 0.12f), // 둥근 등나무 방패
            },
            EnemyKind.WaterGhost => new Shape
            {
                Name = "수귀", Radius = 0.35f, Height = 1.5f, Primitive = PrimitiveType.Capsule,
                BodyPosition = new Vector3(0f, 0.75f, 0f), BodyScale = new Vector3(0.6f, 0.75f, 0.6f),
                MarkPosition = new Vector3(0f, 1.2f, 0.3f), MarkScale = new Vector3(0.5f, 0.15f, 0.15f), // 뻗은 손
            },
            EnemyKind.FlagBearer => new Shape
            {
                Name = "깃발수", Radius = 0.35f, Height = 1.6f, Primitive = PrimitiveType.Capsule,
                BodyPosition = new Vector3(0f, 0.8f, 0f), BodyScale = new Vector3(0.65f, 0.8f, 0.65f),
                MarkPosition = new Vector3(0.3f, 1.6f, 0f), MarkScale = new Vector3(0.05f, 1.6f, 0.05f), // 깃대
            },
            EnemyKind.Takrang => new Shape
            {
                Name = "탁랑객", Radius = 0.45f, Height = 1.9f, Primitive = PrimitiveType.Capsule,
                BodyPosition = new Vector3(0f, 0.95f, 0f), BodyScale = new Vector3(0.7f, 0.95f, 0.7f),
                MarkPosition = new Vector3(0.3f, 1.0f, 0.5f), MarkScale = new Vector3(0.08f, 0.06f, 1.2f), // 물결 무늬 장검
            },
            EnemyKind.IronDragon => new Shape
            {
                Name = "수채주 철룡", Radius = 0.9f, Height = 2.8f, Primitive = PrimitiveType.Capsule,
                BodyPosition = new Vector3(0f, 1.4f, 0f), BodyScale = new Vector3(1.7f, 1.4f, 1.5f), // 철갑 거구
                MarkPosition = new Vector3(0.9f, 1.2f, 0.6f), MarkScale = new Vector3(0.5f, 0.5f, 0.5f), // 유성추
            },
            // ── 천마총
            EnemyKind.Cultist => new Shape
            {
                Name = "마교도", Radius = 0.35f, Height = 1.7f, Primitive = PrimitiveType.Capsule,
                BodyPosition = new Vector3(0f, 0.85f, 0f), BodyScale = new Vector3(0.7f, 0.85f, 0.7f),
                MarkPosition = new Vector3(0.3f, 0.9f, 0.4f), MarkScale = new Vector3(0.1f, 0.06f, 0.8f), // 곡도
            },
            EnemyKind.BloodArcher => new Shape
            {
                Name = "혈궁수", Radius = 0.35f, Height = 1.7f, Primitive = PrimitiveType.Capsule,
                BodyPosition = new Vector3(0f, 0.85f, 0f), BodyScale = new Vector3(0.6f, 0.85f, 0.6f),
                MarkPosition = new Vector3(-0.3f, 1.0f, 0.3f), MarkScale = new Vector3(0.06f, 1.2f, 0.06f), // 붉은 활
            },
            EnemyKind.BloodShaman => new Shape
            {
                Name = "혈인", Radius = 0.35f, Height = 1.7f, Primitive = PrimitiveType.Capsule,
                BodyPosition = new Vector3(0f, 0.85f, 0f), BodyScale = new Vector3(0.65f, 0.85f, 0.65f),
                MarkPosition = new Vector3(0f, 1.4f, 0.35f), MarkScale = new Vector3(0.35f, 0.35f, 0.1f), // 피 부적
            },
            EnemyKind.DemonGuard => new Shape
            {
                Name = "천마 근위", Radius = 0.45f, Height = 1.9f, Primitive = PrimitiveType.Capsule,
                BodyPosition = new Vector3(0f, 0.95f, 0f), BodyScale = new Vector3(0.85f, 0.95f, 0.85f),
                MarkPosition = new Vector3(0f, 0.95f, 0.55f), MarkScale = new Vector3(1.0f, 1.3f, 0.12f), // 철판 방패
            },
            EnemyKind.BloodBomber => new Shape
            {
                Name = "혈폭자", Radius = 0.45f, Height = 1.6f, Primitive = PrimitiveType.Sphere,
                BodyPosition = new Vector3(0f, 0.8f, 0f), BodyScale = new Vector3(1.0f, 0.9f, 1.0f), // 부푼 몸
                MarkPosition = new Vector3(0.35f, 1.1f, 0.3f), MarkScale = new Vector3(0.3f, 0.3f, 0.3f), // 핏덩이
            },
            EnemyKind.BloodShadow => new Shape
            {
                Name = "혈영검마", Radius = 0.45f, Height = 1.9f, Primitive = PrimitiveType.Capsule,
                BodyPosition = new Vector3(0f, 0.95f, 0f), BodyScale = new Vector3(0.7f, 0.95f, 0.7f),
                MarkPosition = new Vector3(0.3f, 1.0f, 0.5f), MarkScale = new Vector3(0.05f, 0.05f, 1.3f), // 가는 장검
            },
            _ => new Shape
            {
                Name = "산적 졸개", Radius = 0.35f, Height = 1.6f, Primitive = PrimitiveType.Capsule,
                BodyPosition = new Vector3(0f, 0.8f, 0f), BodyScale = new Vector3(0.7f, 0.8f, 0.7f),
                MarkPosition = new Vector3(0.3f, 0.9f, 0.4f), MarkScale = new Vector3(0.08f, 0.08f, 0.7f),
            },
        };

        static Material MaterialOf(EnemyKind kind, EnemyVisuals v) => kind switch
        {
            EnemyKind.Boar => v.boar,
            EnemyKind.Archer => v.archer,
            EnemyKind.Hound => v.hound,
            EnemyKind.Shield => v.shield,
            EnemyKind.Horn => v.horn,
            EnemyKind.Bomber => v.bomber,
            EnemyKind.Snake => v.snake,
            EnemyKind.Nokrim => v.nokrim,
            EnemyKind.Assassin => v.assassin,
            EnemyKind.Qingzhu => v.qingzhu,
            EnemyKind.IronFist => v.ironfist,
            EnemyKind.Wraith => Tinted(kind, v.grunt, new Color(0.75f, 0.85f, 0.95f)), // 폐사찰: 그림이 생기기 전까지 졸개 재질에 색만
            EnemyKind.Arhat => Tinted(kind, v.grunt, new Color(0.55f, 0.55f, 0.5f)),
            EnemyKind.Moktak => Tinted(kind, v.grunt, new Color(0.6f, 0.45f, 0.3f)),
            EnemyKind.Monk => Tinted(kind, v.grunt, new Color(0.45f, 0.3f, 0.3f)),
            EnemyKind.Lantern => Tinted(kind, v.grunt, new Color(1f, 0.75f, 0.35f)),
            EnemyKind.Hyegwang => Tinted(kind, v.ironfist != null ? v.ironfist : v.grunt, new Color(0.5f, 0.25f, 0.2f)),
            EnemyKind.BloodBuddha => Tinted(kind, v.nokrim != null ? v.nokrim : v.grunt, new Color(0.6f, 0.15f, 0.12f)),
            EnemyKind.Pirate => Tinted(kind, v.grunt, new Color(0.35f, 0.45f, 0.55f)), // 장강 수채: 그림이 생기기 전까지 색만
            EnemyKind.Harpooner => Tinted(kind, v.archer != null ? v.archer : v.grunt, new Color(0.4f, 0.55f, 0.6f)),
            EnemyKind.Anchor => Tinted(kind, v.boar != null ? v.boar : v.grunt, new Color(0.3f, 0.35f, 0.4f)),
            EnemyKind.Rattan => Tinted(kind, v.shield != null ? v.shield : v.grunt, new Color(0.65f, 0.55f, 0.35f)),
            EnemyKind.WaterGhost => Tinted(kind, v.grunt, new Color(0.3f, 0.6f, 0.6f)),
            EnemyKind.FlagBearer => Tinted(kind, v.horn != null ? v.horn : v.grunt, new Color(0.6f, 0.3f, 0.25f)),
            EnemyKind.Takrang => Tinted(kind, v.qingzhu != null ? v.qingzhu : v.grunt, new Color(0.3f, 0.45f, 0.5f)),
            EnemyKind.IronDragon => Tinted(kind, v.nokrim != null ? v.nokrim : v.grunt, new Color(0.3f, 0.32f, 0.38f)),
            EnemyKind.Cultist => Tinted(kind, v.grunt, new Color(0.25f, 0.2f, 0.22f)), // 천마총: 그림이 생기기 전까지 색만
            EnemyKind.BloodArcher => Tinted(kind, v.archer != null ? v.archer : v.grunt, new Color(0.55f, 0.15f, 0.15f)),
            EnemyKind.BloodShaman => Tinted(kind, v.horn != null ? v.horn : v.grunt, new Color(0.5f, 0.1f, 0.12f)),
            EnemyKind.DemonGuard => Tinted(kind, v.shield != null ? v.shield : v.grunt, new Color(0.2f, 0.2f, 0.25f)),
            EnemyKind.BloodBomber => Tinted(kind, v.bomber != null ? v.bomber : v.grunt, new Color(0.7f, 0.2f, 0.18f)),
            EnemyKind.BloodShadow => Tinted(kind, v.qingzhu != null ? v.qingzhu : v.grunt, new Color(0.55f, 0.1f, 0.12f)),
            _ => v.grunt,
        };

        static readonly System.Collections.Generic.Dictionary<EnemyKind, Material> TintedCache = new System.Collections.Generic.Dictionary<EnemyKind, Material>();

        static Material Tinted(EnemyKind kind, Material from, Color color)
        {
            if (from == null) return null;
            if (!TintedCache.TryGetValue(kind, out var m) || m == null)
            {
                m = new Material(from) { color = color };
                TintedCache[kind] = m;
            }
            return m;
        }

        public static EnemyBrain Create(EnemyDirector director, EnemyKind kind, Vector3 position, EnemyVisuals visuals)
        {
            var shape = ShapeOf(kind);
            var root = new GameObject(shape.Name);
            root.transform.position = position;

            var cc = root.AddComponent<CharacterController>();
            cc.radius = shape.Radius;
            cc.height = Mathf.Max(shape.Height, shape.Radius * 2f);
            cc.center = new Vector3(0f, cc.height * 0.5f, 0f);
            cc.skinWidth = 0.02f;
            cc.stepOffset = 0f;
            cc.minMoveDistance = 0f;

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);

            var body = GameObject.CreatePrimitive(shape.Primitive);
            body.name = "Body";
            StripCollider(body);
            body.transform.SetParent(visual, false);
            body.transform.localPosition = shape.BodyPosition;
            body.transform.localScale = shape.BodyScale;
            var bodyRenderer = body.GetComponent<Renderer>();
            var bodyMaterial = MaterialOf(kind, visuals);
            if (bodyMaterial != null) bodyRenderer.sharedMaterial = bodyMaterial;

            var mark = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mark.name = kind == EnemyKind.Shield ? "Shield" : "Facing";
            StripCollider(mark);
            mark.transform.SetParent(visual, false);
            mark.transform.localPosition = shape.MarkPosition;
            mark.transform.localScale = shape.MarkScale;
            if (visuals.accent != null) mark.GetComponent<Renderer>().sharedMaterial = visuals.accent;

            root.AddComponent<Damageable>();
            var telegraph = Telegraph.Create(root.transform, visuals.telegraphBase, visuals.telegraphFill, visuals.telegraphOutline);

            EnemyBrain brain = kind switch
            {
                EnemyKind.Boar => root.AddComponent<BoarBrain>(),
                EnemyKind.Archer => root.AddComponent<ArcherBrain>(),
                EnemyKind.Hound => root.AddComponent<HoundBrain>(),
                EnemyKind.Shield => root.AddComponent<ShieldBrain>(),
                EnemyKind.Horn => root.AddComponent<HornBrain>(),
                EnemyKind.Bomber => root.AddComponent<BomberBrain>(),
                EnemyKind.Snake => root.AddComponent<SnakeBrain>(),
                EnemyKind.Nokrim => root.AddComponent<NokrimBrain>(),
                EnemyKind.Assassin => root.AddComponent<AssassinBrain>(),
                EnemyKind.Qingzhu => root.AddComponent<QingzhuBrain>(),
                EnemyKind.IronFist => root.AddComponent<IronFistBrain>(),
                EnemyKind.Wraith => root.AddComponent<WraithBrain>(),
                EnemyKind.Arhat => root.AddComponent<ArhatBrain>(),
                EnemyKind.Moktak => root.AddComponent<MoktakBrain>(),
                EnemyKind.Monk => root.AddComponent<MonkBrain>(),
                EnemyKind.Lantern => root.AddComponent<LanternBrain>(),
                EnemyKind.Hyegwang => root.AddComponent<HyegwangBrain>(),
                EnemyKind.BloodBuddha => root.AddComponent<BloodBuddhaBrain>(),
                EnemyKind.Pirate => root.AddComponent<PirateBrain>(),
                EnemyKind.Harpooner => root.AddComponent<HarpoonerBrain>(),
                EnemyKind.Anchor => root.AddComponent<AnchorBrain>(),
                EnemyKind.Rattan => root.AddComponent<RattanBrain>(),
                EnemyKind.WaterGhost => root.AddComponent<WaterGhostBrain>(),
                EnemyKind.FlagBearer => root.AddComponent<FlagBearerBrain>(),
                EnemyKind.Takrang => root.AddComponent<TakrangBrain>(),
                EnemyKind.IronDragon => root.AddComponent<IronDragonBrain>(),
                EnemyKind.Cultist => root.AddComponent<CultistBrain>(),
                EnemyKind.BloodArcher => root.AddComponent<BloodArcherBrain>(),
                EnemyKind.BloodShaman => root.AddComponent<BloodShamanBrain>(),
                EnemyKind.DemonGuard => root.AddComponent<DemonGuardBrain>(),
                EnemyKind.BloodBomber => root.AddComponent<BloodBomberBrain>(),
                EnemyKind.BloodShadow => root.AddComponent<BloodShadowBrain>(),
                _ => root.AddComponent<GruntBrain>(),
            };
            if (brain is ShieldBrain shieldBrain) shieldBrain.SetShieldVisual(mark.transform);
            brain.Init(director, shape.Name, visual, bodyRenderer, telegraph);
            return brain;
        }

        // Destroy는 프레임 끝에야 지워져서, 같은 프레임의 판정에 걸리지 않게 먼저 꺼요.
        public static void StripCollider(GameObject go)
        {
            var collider = go.GetComponent<Collider>();
            if (collider == null) return;
            collider.enabled = false;
            Object.Destroy(collider);
        }
    }
}

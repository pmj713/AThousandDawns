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
            EnemyKind.Nokrim => new Shape
            {
                Name = "녹림왕", Radius = 0.7f, Height = 2.4f, Primitive = PrimitiveType.Capsule,
                BodyPosition = new Vector3(0f, 1.2f, 0f), BodyScale = new Vector3(1.3f, 1.2f, 1.3f),
                MarkPosition = new Vector3(0.8f, 1.3f, 0.7f), MarkScale = new Vector3(0.14f, 0.3f, 2f), // 대도
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
            _ => v.grunt,
        };

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

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
            public Vector3 BodyScale;
        }

        static Shape ShapeOf(EnemyKind kind) => kind switch
        {
            EnemyKind.Boar => new Shape { Name = "멧돼지", Radius = 0.6f, Height = 1.2f, Primitive = PrimitiveType.Cube, BodyScale = new Vector3(1.1f, 0.9f, 1.5f) },
            EnemyKind.Archer => new Shape { Name = "산적 궁수", Radius = 0.35f, Height = 1.6f, Primitive = PrimitiveType.Capsule, BodyScale = new Vector3(0.6f, 0.8f, 0.6f) },
            _ => new Shape { Name = "산적 졸개", Radius = 0.35f, Height = 1.6f, Primitive = PrimitiveType.Capsule, BodyScale = new Vector3(0.7f, 0.8f, 0.7f) },
        };

        public static EnemyBrain Create(EnemyDirector director, EnemyKind kind, Vector3 position, EnemyVisuals visuals)
        {
            var shape = ShapeOf(kind);
            var root = new GameObject(shape.Name);
            root.transform.position = position;

            var cc = root.AddComponent<CharacterController>();
            cc.radius = shape.Radius;
            cc.height = shape.Height;
            cc.center = new Vector3(0f, shape.Height * 0.5f, 0f);
            cc.skinWidth = 0.02f;
            cc.stepOffset = 0f;
            cc.minMoveDistance = 0f;

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);

            var body = GameObject.CreatePrimitive(shape.Primitive);
            body.name = "Body";
            StripCollider(body);
            body.transform.SetParent(visual, false);
            body.transform.localPosition = new Vector3(0f, kind == EnemyKind.Boar ? 0.5f : shape.Height * 0.5f, 0f);
            body.transform.localScale = shape.BodyScale;
            var bodyRenderer = body.GetComponent<Renderer>();
            bodyRenderer.sharedMaterial = kind switch
            {
                EnemyKind.Boar => visuals.boar,
                EnemyKind.Archer => visuals.archer,
                _ => visuals.grunt,
            };

            // 바라보는 방향 표시 (졸개: 칼, 멧돼지: 엄니, 궁수: 활)
            var facing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            facing.name = "Facing";
            StripCollider(facing);
            facing.transform.SetParent(visual, false);
            facing.transform.localPosition = kind switch
            {
                EnemyKind.Boar => new Vector3(0f, 0.45f, 0.85f),
                EnemyKind.Archer => new Vector3(0f, 1.0f, 0.35f),
                _ => new Vector3(0.3f, 0.9f, 0.4f),
            };
            facing.transform.localScale = kind switch
            {
                EnemyKind.Boar => new Vector3(0.7f, 0.15f, 0.25f),
                EnemyKind.Archer => new Vector3(0.6f, 0.08f, 0.08f),
                _ => new Vector3(0.08f, 0.08f, 0.7f),
            };
            facing.GetComponent<Renderer>().sharedMaterial = visuals.accent;

            root.AddComponent<Damageable>();
            var telegraph = Telegraph.Create(root.transform, visuals.telegraphBase, visuals.telegraphFill, visuals.telegraphOutline);

            EnemyBrain brain = kind switch
            {
                EnemyKind.Boar => root.AddComponent<BoarBrain>(),
                EnemyKind.Archer => root.AddComponent<ArcherBrain>(),
                _ => root.AddComponent<GruntBrain>(),
            };
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

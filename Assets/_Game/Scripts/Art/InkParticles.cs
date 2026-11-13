using UnityEngine;

namespace AThousandDawns.Art
{
    // 입체로 보이는 불길 · 서리 · 연기 입자 (플레이 테스트: 카드·빌보드 불이 2D처럼 보여요).
    //   여러 입자가 각자 3D 공간에서 솟고 흔들리며 겹쳐서 어느 쪽에서 봐도 덩어리져 보여요.
    //   재질은 늘 빌드에 들어가는 Sprites/Default + 부드러운 먹 번짐(FxArt/InkMist).
    // 사용: InkParticles.Fire(부모, 자리, 키, 세기) · Embers · Smoke · Frost — 모양(Shape)은 상자·원뿔·메시 렌더러 중에서.
    public static class InkParticles
    {
        static Material _soft;

        public static Material Soft
        {
            get
            {
                if (_soft != null) return _soft;
                _soft = new Material(Shader.Find("Sprites/Default")) { name = "Ink Soft Particle" };
                var tex = Resources.Load<Texture2D>("FxArt/InkMist");
                if (tex != null) _soft.mainTexture = tex;
                return _soft;
            }
        }

        static ParticleSystem Make(Transform parent, string name, int maxParticles)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // 원뿔이 위를 보게
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World; // 움직여도 불길이 꼬리처럼 남아요
            main.playOnAwake = true;
            main.loop = true;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Soft;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortingOrder = 6;
            return ps;
        }

        static Gradient Grad(params (Color c, float t)[] keys)
        {
            var colors = new GradientColorKey[keys.Length];
            var alphas = new GradientAlphaKey[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                colors[i] = new GradientColorKey(keys[i].c, keys[i].t);
                alphas[i] = new GradientAlphaKey(keys[i].c.a, keys[i].t);
            }
            var g = new Gradient();
            g.SetKeys(colors, alphas);
            return g;
        }

        // 불길: 밑동의 노란 심 → 주황 → 주홍으로 솟으며 가늘어지고, 끝은 먹빛으로 사그라들어요
        public static ParticleSystem Fire(Transform parent, Vector3 localPos, float height, float rate = 1f)
        {
            var ps = Make(parent, "Ink Fire", 80);
            ps.transform.localPosition = localPos;
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(height * 1.2f, height * 2f);
            main.startSize = new ParticleSystem.MinMaxCurve(height * 0.35f, height * 0.6f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var emission = ps.emission;
            emission.rateOverTime = 28f * rate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = height * 0.22f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = Grad((new Color(1f, 0.92f, 0.55f, 0f), 0f), (new Color(1f, 0.78f, 0.3f, 0.95f), 0.12f), (new Color(0.98f, 0.42f, 0.12f, 0.85f), 0.45f),
                             (new Color(0.7f, 0.12f, 0.06f, 0.55f), 0.75f), (new Color(0.12f, 0.08f, 0.07f, 0f), 1f));
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0.25f)));
            var noise = ps.noise; // 일렁임
            noise.enabled = true;
            noise.strength = height * 0.6f;
            noise.frequency = 1.6f;
            noise.scrollSpeed = 1.2f;
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
            ps.Play();
            return ps;
        }

        // 불티: 작은 밝은 점이 위로 튀며 흩날려요
        public static ParticleSystem Embers(Transform parent, Vector3 localPos, float spread, float rate = 1f)
        {
            var ps = Make(parent, "Ink Embers", 40);
            ps.transform.localPosition = localPos;
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
            main.gravityModifier = -0.15f;
            var emission = ps.emission;
            emission.rateOverTime = 10f * rate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = spread;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = Grad((new Color(1f, 0.85f, 0.4f, 1f), 0f), (new Color(1f, 0.4f, 0.1f, 0.9f), 0.6f), (new Color(0.5f, 0.1f, 0.05f, 0f), 1f));
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.8f;
            noise.frequency = 2f;
            ps.Play();
            return ps;
        }

        // 먹 연기 (그을음): 회색 먹이 커지며 솟아 옅어져요
        public static ParticleSystem Smoke(Transform parent, Vector3 localPos, float spread, float rate = 1f, float darkness = 1f)
        {
            var ps = Make(parent, "Ink Smoke", 40);
            ps.transform.localPosition = localPos;
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(spread * 0.8f, spread * 1.4f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var emission = ps.emission;
            emission.rateOverTime = 6f * rate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 15f;
            shape.radius = spread * 0.5f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            float d = Mathf.Lerp(0.45f, 0.12f, darkness);
            col.color = Grad((new Color(d, d, d, 0f), 0f), (new Color(d, d * 0.95f, d * 0.92f, 0.45f), 0.2f), (new Color(d + 0.1f, d + 0.1f, d + 0.1f, 0f), 1f));
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 1.6f)));
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.5f;
            noise.frequency = 0.8f;
            ps.Play();
            return ps;
        }

        // 서리: 옅은 얼음빛 김이 몸에서 피어오르고 반짝이는 결정이 맴돌아요
        public static ParticleSystem Frost(Transform parent, Vector3 localPos, float spread, float rate = 1f)
        {
            var ps = Make(parent, "Ink Frost", 50);
            ps.transform.localPosition = localPos;
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.3f);
            var emission = ps.emission;
            emission.rateOverTime = 14f * rate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 40f;
            shape.radius = spread;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = Grad((new Color(0.85f, 0.95f, 1f, 0f), 0f), (new Color(0.75f, 0.9f, 1f, 0.85f), 0.25f), (new Color(0.55f, 0.75f, 0.95f, 0f), 1f));
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.3f;
            noise.frequency = 1.2f;
            ps.Play();
            return ps;
        }

        // 모양을 몸 크기 상자로 (빌드에서 메시 읽기 설정 없이도 돼요): 상자 안 여기저기서 위로 솟아요
        public static void EmitBox(ParticleSystem ps, Vector3 localCenter, Vector3 size, float rise)
        {
            if (ps == null) return;
            ps.transform.localPosition = localCenter;
            ps.transform.localRotation = Quaternion.identity;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = size;
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.y = new ParticleSystem.MinMaxCurve(rise * 0.5f, rise);
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var main = ps.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.15f);
        }

        public static void SetRate(ParticleSystem ps, float perSecond)
        {
            if (ps == null) return;
            var emission = ps.emission;
            emission.rateOverTime = perSecond;
        }
    }
}

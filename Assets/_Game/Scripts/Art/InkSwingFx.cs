using AThousandDawns.Enemies;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 고수 · 보스 휘두르기 궤적 (EnemyBrain.Swung: 부채꼴 근접 판정이 나는 순간 한 번) — 채색 호, 적 종류마다 색이 달라요.
    //   반경만큼 커지는 검기 초승달 하나 (각도가 넓으면 양옆으로 한 겹씩 더) + 끝을 따라 같은 색 불티.
    //   철권 마웅 주황 금빛 · 고블린 족장 초록 · 오우거 흙빛 주황 · 데스나이트 청록 · 혈영검마 진홍 · 다크 엘프 은보라 · 혜광 금빛 · 청죽객 비취 · 탁랑객 물빛.
    // 그림: SkillFx/SwordQi (흰 초승달, 볼록한 쪽이 그림 위).
    public class InkSwingFx : MonoBehaviour
    {
        static InkSwingFx _host;
        static Sprite _qi;
        static bool _loaded;

        readonly System.Collections.Generic.List<System.Func<float, bool>> _live = new System.Collections.Generic.List<System.Func<float, bool>>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            EnemyBrain.Swung -= OnSwung;
            EnemyBrain.Swung += OnSwung;
        }

        static InkSwingFx Host()
        {
            if (!_loaded) { _loaded = true; _qi = Resources.Load<Sprite>("SkillFx/SwordQi"); }
            if (_host == null) _host = new GameObject("Ink Swing Fx").AddComponent<InkSwingFx>();
            return _host;
        }

        static void OnSwung(EnemyBrain who, Vector3 at, Vector3 dir, float radius, float angle)
        {
            if (who == null) return;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) return;
            Host().Arc(at, dir.normalized, Mathf.Max(0.8f, radius), angle, Tint(who.Kind));
        }

        static Color Tint(EnemyKind kind) => kind switch
        {
            EnemyKind.IronFist => new Color(1f, 0.6f, 0.25f),
            EnemyKind.GoblinChief => new Color(0.55f, 0.9f, 0.3f),
            EnemyKind.Ogre => new Color(0.95f, 0.6f, 0.3f),
            EnemyKind.DeathKnight => new Color(0.3f, 1f, 0.85f),
            EnemyKind.BloodShadow => new Color(0.95f, 0.12f, 0.2f),
            EnemyKind.DarkElf => new Color(0.82f, 0.75f, 1f),
            EnemyKind.Hyegwang => new Color(1f, 0.82f, 0.3f),
            EnemyKind.Qingzhu => new Color(0.4f, 0.95f, 0.55f),
            EnemyKind.Takrang => new Color(0.35f, 0.7f, 1f),
            _ => new Color(1f, 0.85f, 0.6f),
        };

        void OnDestroy() { if (_host == this) _host = null; }

        void Update()
        {
            float dt = Time.deltaTime;
            InkRoomReset.Tick(_live, dt, transform); // 방을 옮기면 제 밑의 연출을 지워요
        }

        void Arc(Vector3 at, Vector3 dir, float radius, float angle, Color color)
        {
            if (angle >= 300f) // 한 바퀴 (혜광 선장 휘두르기 360°): 초승달 여덟 개가 차례로 둘레를 휩쓸고 금빛 불티 고리
            {
                for (int i = 0; i < 8; i++)
                {
                    Vector3 d = Quaternion.Euler(0f, i * 45f, 0f) * dir;
                    Crescent(at + d * radius * 0.55f, d, radius * 1.15f, color, i * 0.03f);
                }
                for (int i = 0; i < 8; i++)
                    InkParticles.Burst(at + Quaternion.Euler(0f, i * 45f + 22f, 0f) * dir * radius + Vector3.up * 0.8f, 3, Color.Lerp(color, Color.white, 0.5f), new Color(color.r, color.g, color.b, 0.4f), radius * 0.8f, 0.1f, 0.35f, 0f);
                return;
            }
            Crescent(at + dir * radius * 0.5f, dir, radius * 1.9f, color, 0f);
            if (angle > 150f)
            {
                Crescent(at + dir * radius * 0.25f, Quaternion.Euler(0f, 55f, 0f) * dir, radius * 1.5f, color, 0.04f);
                Crescent(at + dir * radius * 0.25f, Quaternion.Euler(0f, -55f, 0f) * dir, radius * 1.5f, color, 0.04f);
            }
            InkParticles.Burst(at + dir * radius * 0.8f + Vector3.up * 0.8f, 8, Color.Lerp(color, Color.white, 0.5f), new Color(color.r, color.g, color.b, 0.4f), radius * 1.4f, 0.1f, 0.35f, 0f);
        }

        void Crescent(Vector3 at, Vector3 dir, float width, Color color, float delay)
        {
            if (_qi == null || _live.Count > 200) return;
            var go = new GameObject("Swing Arc");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(at.x, 0.85f, at.z);
            go.transform.rotation = Quaternion.Euler(90f, InkSwordQi.Yaw(dir), 0f); // 볼록한 쪽이 앞으로
            float unit = width / Mathf.Max(0.01f, _qi.bounds.size.x);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _qi;
            sr.sortingOrder = 7;
            sr.enabled = false;
            float t = -delay;
            _live.Add(dt =>
            {
                if (go == null) return false;
                t += dt;
                if (t < 0f) return true;
                sr.enabled = true;
                float k = t / 0.25f;
                go.transform.localScale = Vector3.one * unit * Mathf.Lerp(0.6f, 1.1f, Mathf.Sqrt(Mathf.Clamp01(k)));
                sr.color = new Color(color.r, color.g, color.b, 1f - k * k);
                if (k < 1f) return true;
                Destroy(go);
                return false;
            });
        }
    }
}

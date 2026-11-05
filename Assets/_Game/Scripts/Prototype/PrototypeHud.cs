using AThousandDawns.Combat;
using AThousandDawns.Enemies;
using AThousandDawns.Player;
using UnityEngine;

namespace AThousandDawns.Prototype
{
    // 시험판 화면 표시 (IMGUI). 나중에 진짜 HUD로 바꿀 자리예요.
    public sealed class PrototypeHud : MonoBehaviour
    {
        static readonly Color HealthColor = new Color(0.78f, 0.27f, 0.24f);   // 매화 홍 #C8463C
        static readonly Color EnemyHealthColor = new Color(0.92f, 0.85f, 0.7f);
        static readonly Color BarBack = new Color(0f, 0f, 0f, 0.55f);

        [SerializeField] PlayerController player;
        [SerializeField] TrainingDummy[] dummies;
        [SerializeField] EnemyDirector director;
        [SerializeField] PrototypeArena arena;

        GUIStyle _label, _popup, _dummy, _big, _bossLabel;
        Camera _camera;

        void Update() => DamagePopups.Tick(Time.deltaTime);

        void OnGUI()
        {
            EnsureStyles();
            if (_camera == null) _camera = Camera.main;

            GUILayout.BeginArea(new Rect(16, 16, 640, 320), GUI.skin.box);
            GUILayout.Label("일조천회 시험판 · 전투", _label);
            GUILayout.Label("WASD 이동 · 좌클릭(누르고 있기) 평타 · 우클릭 회선참 · Q 만검귀종 · Space 대시", _label);
            GUILayout.Label("부르기 (바로 옆): 1 졸개 · 2 멧돼지 · 3 궁수 · 4 들개 · 5 방패 · 6 나팔수 · 7 화약 · 8 청죽사 · 9 녹림왕 (결투장)", _label);
            GUILayout.Label("G 기세 채우기 · F5 처음부터", _label);
            if (player != null)
            {
                GUILayout.Label($"체력 {player.Health:0}/{player.MaxHealth:0}", _label);
                Rect bar = GUILayoutUtility.GetRect(300, 10);
                bar.width = 300;
                DrawBar(bar, player.MaxHealth > 0f ? player.Health / player.MaxHealth : 0f, HealthColor);

                var combo = player.Combo;
                string slow = player.SlowLeft > 0f ? $"  · 둔화 {player.SlowRatio:0%} {player.SlowLeft:0.0}초" : "";
                if (player.BurnStacks > 0) slow += $"  · 화상 {player.BurnStacks}중첩";
                GUILayout.Label($"대시 {player.Dash.Charges}/{player.Dash.MaxCharges}  {(player.IsInvulnerable ? "(무적)" : "")}{slow}", _label);
                string ultimate = player.IsCastingUltimate ? "  · 만검귀종!" : player.UltimateReady ? "  · Q 만검귀종 준비" : "";
                GUILayout.Label($"기세 {player.Gauge:0}/{player.GaugeMax:0}{ultimate}", _label);
                string skill = player.IsCastingSkill ? "베는 중" : player.SkillCooldownLeft > 0f ? $"{player.SkillCooldownLeft:0.0}초" : "준비";
                GUILayout.Label($"회선참 {skill}", _label);
                if (combo != null) GUILayout.Label($"평타 {combo.HitIndex + 1}타 · {combo.CurrentPhase}", _label);
            }
            if (director != null && director.MeleeTokens != null)
            {
                string wave = arena != null && arena.RoundNumber > 0 ? $"{arena.RoundNumber}번째 배치 · " : "";
                GUILayout.Label($"{wave}적 {director.Enemies.Count}  ·  공격 중 근접 {director.MeleeTokens.InUse}/{director.MeleeTokens.Capacity}, 원거리 {director.RangedTokens.InUse}/{director.RangedTokens.Capacity}", _label);
            }
            GUILayout.EndArea();

            if (player != null && player.IsDead)
                GUI.Label(new Rect(0, Screen.height * 0.4f, Screen.width, 60), "쓰러졌어요 · F5 키로 다시 시작", _big);

            // 녹림왕이 깨어나면 화면 아래쪽 가운데에 큰 체력바 (왼쪽 위 도움말과 겹치지 않게)
            var boss = arena != null ? arena.Boss : null;
            if (boss != null && !boss.IsDead && boss.IsAwake && boss.Damageable != null)
            {
                float width = Mathf.Min(560f, Screen.width - 40f);
                var area = new Rect((Screen.width - width) * 0.5f, Screen.height - 64f, width, 18f);
                string state = boss.IsTransitioning ? " · 전환 중 (무적)" : boss.Grove != null && boss.Grove.IsBurning ? " · 불타는 죽림" : "";
                GUI.Label(new Rect(area.x, area.y, area.width, 24f), $"녹림왕 · {boss.Phase}페이즈{state}   {boss.Damageable.Health:0}/{boss.Damageable.MaxHealth:0}", _bossLabel);
                DrawBar(new Rect(area.x, area.y + 26f, area.width, 12f), boss.Damageable.Health / Mathf.Max(1f, boss.Damageable.MaxHealth), HealthColor);
            }

            if (_camera == null) return;

            if (dummies != null)
            {
                foreach (var d in dummies)
                {
                    if (d == null) continue;
                    Vector3 sp = _camera.WorldToScreenPoint(d.transform.position + Vector3.up * 2.8f);
                    if (sp.z <= 0f) continue;
                    string state = d.IsMeasuring ? "측정 중" : "고정";
                    string text = $"{d.Label}\nDPS {d.Dps:0.0}  ({d.Elapsed:0.0}초, 합계 {d.Total}, {state})";
                    var rect = new Rect(sp.x - 160, Screen.height - sp.y - 40, 320, 44);
                    GUI.Label(rect, text, _dummy);
                }
            }

            if (director != null)
            {
                foreach (var e in director.Enemies)
                {
                    if (e == null || e.Damageable == null || !e.Damageable.Targetable) continue; // 숨은 청죽사는 안 보여요
                    Vector3 sp = _camera.WorldToScreenPoint(e.transform.position + Vector3.up * (e.Height + 0.4f));
                    if (sp.z <= 0f) continue;
                    var dmg = e.Damageable;
                    DrawBar(new Rect(sp.x - 25, Screen.height - sp.y, 50, 6), dmg.MaxHealth > 0f ? dmg.Health / dmg.MaxHealth : 0f, EnemyHealthColor);
                }
            }

            foreach (var p in DamagePopups.Current)
            {
                Vector3 sp = _camera.WorldToScreenPoint(p.Position);
                if (sp.z <= 0f) continue;
                Color c = p.OnPlayer ? new Color(1f, 0.35f, 0.3f) : new Color(1f, 0.95f, 0.8f);
                c.a = 1f - p.Age / DamagePopups.Lifetime;
                _popup.normal.textColor = c;
                GUI.Label(new Rect(sp.x - 40, Screen.height - sp.y - 15, 80, 30), p.Amount.ToString(), _popup);
            }
        }

        static void DrawBar(Rect rect, float ratio, Color fill)
        {
            var old = GUI.color;
            GUI.color = BarBack;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = fill;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(ratio), rect.height), Texture2D.whiteTexture);
            GUI.color = old;
        }

        void EnsureStyles()
        {
            if (_label != null) return;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            _label.normal.textColor = Color.white;
            _popup = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _dummy = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
            _dummy.normal.textColor = Color.black;
            _big = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _big.normal.textColor = Color.white;
            _bossLabel = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _bossLabel.normal.textColor = Color.white;
        }
    }
}

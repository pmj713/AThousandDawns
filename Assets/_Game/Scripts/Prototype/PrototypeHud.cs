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

        GUIStyle _label, _popup, _dummy, _big;
        Camera _camera;

        void Update() => DamagePopups.Tick(Time.deltaTime);

        void OnGUI()
        {
            EnsureStyles();
            if (_camera == null) _camera = Camera.main;

            GUILayout.BeginArea(new Rect(16, 16, 600, 295), GUI.skin.box);
            GUILayout.Label("일조천회 시험판 · 전투", _label);
            GUILayout.Label("WASD 이동 · 좌클릭(누르고 있기) 평타 · 우클릭 회선참 · Space 대시 · 마우스로 조준", _label);
            GUILayout.Label("1 졸개 · 2 멧돼지 · 3 궁수 부르기 · R 처음부터", _label);
            if (player != null)
            {
                GUILayout.Label($"체력 {player.Health:0}/{player.MaxHealth:0}", _label);
                Rect bar = GUILayoutUtility.GetRect(300, 10);
                bar.width = 300;
                DrawBar(bar, player.MaxHealth > 0f ? player.Health / player.MaxHealth : 0f, HealthColor);

                var combo = player.Combo;
                GUILayout.Label($"대시 {player.Dash.Charges}/{player.Dash.MaxCharges}  {(player.IsInvulnerable ? "(무적)" : "")}", _label);
                GUILayout.Label($"기세 {player.Gauge:0}/{player.GaugeMax:0}", _label);
                string skill = player.IsCastingSkill ? "베는 중" : player.SkillCooldownLeft > 0f ? $"{player.SkillCooldownLeft:0.0}초" : "준비";
                GUILayout.Label($"회선참 {skill}", _label);
                if (combo != null) GUILayout.Label($"평타 {combo.HitIndex + 1}타 · {combo.CurrentPhase}", _label);
            }
            if (director != null && director.MeleeTokens != null)
            {
                string wave = arena != null && arena.WaveNumber > 0 ? $"{arena.WaveNumber}번째 무리 · " : "";
                GUILayout.Label($"{wave}적 {director.Enemies.Count}  ·  공격 중 근접 {director.MeleeTokens.InUse}/{director.MeleeTokens.Capacity}, 원거리 {director.RangedTokens.InUse}/{director.RangedTokens.Capacity}", _label);
            }
            GUILayout.EndArea();

            if (player != null && player.IsDead)
                GUI.Label(new Rect(0, Screen.height * 0.4f, Screen.width, 60), "쓰러졌어요 · R 키로 다시 시작", _big);

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
                    if (e == null || e.Damageable == null) continue;
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
        }
    }
}

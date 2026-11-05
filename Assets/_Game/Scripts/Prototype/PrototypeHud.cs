using AThousandDawns.Combat;
using AThousandDawns.Player;
using UnityEngine;

namespace AThousandDawns.Prototype
{
    // 시험판 화면 표시 (IMGUI). 나중에 진짜 HUD로 바꿀 자리예요.
    public sealed class PrototypeHud : MonoBehaviour
    {
        [SerializeField] PlayerController player;
        [SerializeField] TrainingDummy[] dummies;

        GUIStyle _label, _popup, _dummy;
        Camera _camera;

        void Update() => DamagePopups.Tick(Time.deltaTime);

        void OnGUI()
        {
            EnsureStyles();
            if (_camera == null) _camera = Camera.main;

            GUILayout.BeginArea(new Rect(16, 16, 520, 220), GUI.skin.box);
            GUILayout.Label("일조천회 시험판 · 전투", _label);
            GUILayout.Label("WASD 이동 · 좌클릭(누르고 있기) 평타 · Space 대시 · 마우스로 조준", _label);
            if (player != null)
            {
                var combo = player.Combo;
                GUILayout.Label($"대시 {player.Dash.Charges}/{player.Dash.MaxCharges}  {(player.IsInvulnerable ? "(무적)" : "")}", _label);
                GUILayout.Label($"기세 {player.Gauge:0}/{player.GaugeMax:0}", _label);
                if (combo != null) GUILayout.Label($"평타 {combo.HitIndex + 1}타 · {combo.CurrentPhase}", _label);
            }
            GUILayout.EndArea();

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

            foreach (var p in DamagePopups.Current)
            {
                Vector3 sp = _camera.WorldToScreenPoint(p.Position);
                if (sp.z <= 0f) continue;
                var c = _popup.normal.textColor;
                c.a = 1f - p.Age / DamagePopups.Lifetime;
                _popup.normal.textColor = c;
                GUI.Label(new Rect(sp.x - 40, Screen.height - sp.y - 15, 80, 30), p.Amount.ToString(), _popup);
            }
            var reset = _popup.normal.textColor;
            reset.a = 1f;
            _popup.normal.textColor = reset;
        }

        void EnsureStyles()
        {
            if (_label != null) return;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            _label.normal.textColor = Color.white;
            _popup = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _popup.normal.textColor = new Color(1f, 0.95f, 0.8f);
            _dummy = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
            _dummy.normal.textColor = Color.black;
        }
    }
}

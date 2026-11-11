using AThousandDawns.Enemies;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 혈불 혈수: 판정 순간 혈불 가슴에서 핏빛 먹 손이 예고 선을 따라 쭉 뻗었다가 되돌아와요.
    // 플레이어를 잡았으면(GrabbedPlayer) 손이 플레이어에게서 멈췄다가 끌어오고, 놓쳤으면 끝까지 뻗었다가 빈손으로 돌아와요.
    // 판정·끌어당김은 게임 코드(BloodBuddhaBrain)가 하고, 이건 보이기만 해요
    public class InkBloodHand : MonoBehaviour
    {
        const float ReachTime = 0.12f, HoldTime = 0.12f, ReturnTime = 0.3f;
        static Material s_material;

        Transform _arm, _hand;
        BloodBuddhaBrain _boss;
        Vector3 _dir;
        float _length, _t;

        public static void Spawn(BloodBuddhaBrain boss, Animator model)
        {
            if (boss == null) return;
            var go = new GameObject("Blood Hand (혈수)");
            go.AddComponent<InkBloodHand>().Setup(boss, model);
        }

        void Setup(BloodBuddhaBrain boss, Animator model)
        {
            _boss = boss;
            _dir = boss.transform.forward; _dir.y = 0f;
            _dir = _dir.sqrMagnitude > 1e-4f ? _dir.normalized : Vector3.forward;
            float range = Core.GameConfig.Get("bloodbuddha.grab_range", 10f);
            var player = FindFirstObjectByType<Player.PlayerController>();
            _length = range;
            if (boss.GrabbedPlayer && player != null)
            {
                var to = player.transform.position - boss.transform.position; to.y = 0f;
                _length = Mathf.Min(range, to.magnitude);   // 잡은 플레이어에게서 멈춰요
            }
            if (s_material == null)
            {
                s_material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                s_material.SetColor("_BaseColor", new Color(0.42f, 0.04f, 0.03f, 1f));   // 핏빛 먹
            }
            _arm = Part(PrimitiveType.Cylinder, "Arm");
            _hand = Part(PrimitiveType.Sphere, "Hand");
        }

        Transform Part(PrimitiveType type, string name)
        {
            var p = GameObject.CreatePrimitive(type);
            Destroy(p.GetComponent<Collider>());
            p.name = name;
            p.transform.SetParent(transform, false);
            var r = p.GetComponent<Renderer>();
            r.sharedMaterial = s_material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return p.transform;
        }

        void Update()
        {
            if (_boss == null) { Destroy(gameObject); return; }
            _t += Time.deltaTime;
            float reach;
            if (_t < ReachTime) reach = 1f - Mathf.Pow(1f - _t / ReachTime, 3f);           // 휙 뻗어요
            else if (_t < ReachTime + HoldTime) reach = 1f;
            else reach = 1f - Mathf.Clamp01((_t - ReachTime - HoldTime) / ReturnTime);    // 끌고 돌아와요
            if (_t > ReachTime + HoldTime + ReturnTime) { Destroy(gameObject); return; }

            Vector3 from = _boss.transform.position + Vector3.up * 1.9f + _dir * 0.6f;    // 가슴께
            float len = Mathf.Max(0.05f, _length * reach);
            Vector3 tip = from + _dir * len;
            tip.y = Mathf.Lerp(from.y, 1.0f, reach);                                       // 손끝은 플레이어 가슴 높이로
            Vector3 d = tip - from;
            _arm.position = from + d * 0.5f;
            _arm.rotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
            float thick = Mathf.Lerp(0.35f, 0.18f, reach);                                 // 뻗을수록 가늘게
            _arm.localScale = new Vector3(thick, d.magnitude * 0.5f, thick);
            _hand.position = tip;
            _hand.rotation = Quaternion.LookRotation(d.normalized);
            float open = _t < ReachTime + HoldTime * 0.5f ? 1f : 0.7f;                     // 펴서 뻗었다가 움켜쥐어요
            _hand.localScale = new Vector3(0.75f * open, 0.35f, 0.6f);
        }
    }
}

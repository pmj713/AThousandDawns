using System.Collections.Generic;
using AThousandDawns.Player;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 혼종 세트 기운: 이계 혼종 세트를 3개 다 맞추면(PlayerItems.HybridLevel = 2) 플레이어 몸 가장자리에 그 세트 색 기운이 숨 쉬듯 감돌아요.
    //   업화 주홍 · 서리 마나 하늘 · 뇌문 노랑 · 마검 쪽빛 · 미스릴 은빛 · 엘프 발걸음 초록. 둘 이상 맞췄으면 색이 천천히 번갈아요.
    // 세트가 바뀔 때만 다시 따져요 (PlayerItems.HybridChanged). 몸 재질의 _AuraColor (InkCharacter)를 써요.
    // 플레이어 피격 번쩍임(PlayerController)이 같은 렌더러의 속성 블록을 매 프레임 새로 써서, 기운은 LateUpdate에서 기존 블록에 얹어요.
    // 씬 빌더(PrototypeSceneBuilder.InkPlayer.cs)가 플레이어에 붙여요
    public class InkPlayerHybridAura : MonoBehaviour
    {
        static readonly int AuraId = Shader.PropertyToID("_AuraColor");

        static readonly Dictionary<Keyword, Color> SetColors = new Dictionary<Keyword, Color>
        {
            [Keyword.Yeolyang] = new Color(1f, 0.42f, 0.1f),     // 업화 주홍
            [Keyword.Bingbaek] = new Color(0.55f, 0.85f, 1f),    // 서리 마나 하늘
            [Keyword.Noejeong] = new Color(1f, 0.88f, 0.2f),     // 뇌문 노랑
            [Keyword.Ganggi] = new Color(0.22f, 0.32f, 0.9f),    // 마검 쪽빛
            [Keyword.Geumgang] = new Color(0.86f, 0.9f, 0.95f),  // 미스릴 은빛
            [Keyword.Gyeonggong] = new Color(0.35f, 0.92f, 0.4f), // 엘프 발걸음 초록
        };

        PlayerItems _items;
        Renderer[] _renderers;
        MaterialPropertyBlock _block;
        readonly List<Color> _active = new List<Color>();
        bool _shown;

        void Awake() => _items = GetComponent<PlayerItems>();

        void OnEnable()
        {
            PlayerItems.HybridChanged += OnHybridChanged;
            Refresh();
        }

        void OnDisable()
        {
            PlayerItems.HybridChanged -= OnHybridChanged;
            if (_shown) Apply(Color.clear);
        }

        void OnHybridChanged(PlayerItems items, Keyword keyword, int level)
        {
            if (items == _items) Refresh();
        }

        void Refresh()
        {
            _active.Clear();
            if (_items == null) return;
            foreach (var (keyword, color) in SetColors)
                if (_items.HybridLevel(keyword) >= 2) _active.Add(color);
        }

        void LateUpdate()
        {
            if (_active.Count == 0)
            {
                if (_shown) { Apply(Color.clear); _shown = false; }
                return;
            }
            // 둘 이상이면 3초마다 다음 색으로 천천히 넘어가요
            float t = Time.time / 3f;
            int i = (int)t % _active.Count;
            var color = Color.Lerp(_active[i], _active[(i + 1) % _active.Count], Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - Mathf.Floor(t)) * 3f - 2f)));
            color.a = 0.45f + 0.15f * Mathf.Sin(Time.time * 2.2f);
            Apply(color);
            _shown = true;
        }

        void Apply(Color aura)
        {
            if (_renderers == null) _renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            _block ??= new MaterialPropertyBlock();
            foreach (var r in _renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_block);
                _block.SetColor(AuraId, aura);
                r.SetPropertyBlock(_block);
            }
        }
    }
}

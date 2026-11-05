using AThousandDawns.Data;
using UnityEngine;

namespace AThousandDawns.Core
{
    // 씬에 하나 두고 BalanceConfig를 연결해요. 다른 스크립트는 GameConfig.Balance로 읽어요.
    [DefaultExecutionOrder(-1000)]
    public sealed class GameConfig : MonoBehaviour
    {
        [SerializeField] BalanceConfig balance;

        public static BalanceConfig Balance { get; private set; }

        void Awake() => Balance = balance;

        // 시트 값이 없을 때(설정 누락)도 시험판이 멈추지 않게 기본값을 받아요.
        public static float Get(string key, float fallback)
        {
            if (Balance != null && Balance.TryGet(key, out float value)) return value;
            Debug.LogWarning($"[GameConfig] '{key}' 값이 없어 기본값 {fallback}을 써요.");
            return fallback;
        }
    }
}

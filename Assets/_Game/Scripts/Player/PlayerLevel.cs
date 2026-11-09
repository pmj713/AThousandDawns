using System;
using UnityEngine;

namespace AThousandDawns.Player
{
    // 수련치와 레벨 (수련치 탭): 다음 레벨까지 필요한 수련치 = 기본 필요량 + 지금 레벨 × 레벨당 증가 (25 + 레벨 × 3).
    // 레벨이 오를 때마다 혈자리 하나를 열 수 있어요 (개혈 대기). 전투 중에는 경맥도를 못 여니 쌓아 뒀다가 전투가 끝나면 골라요.
    public sealed class PlayerLevel
    {
        readonly float _baseNeed, _needPerLevel;

        public int Level { get; private set; } = 1;
        public int Xp { get; private set; }      // 이번 레벨에서 모은 수련치
        public int Pending { get; private set; } // 아직 열지 않은 혈자리 (개혈 대기)
        public int NeedForNext => Mathf.Max(1, Mathf.RoundToInt(_baseNeed + Level * _needPerLevel));
        public int NeedLeft => NeedForNext - Xp;

        public event Action LeveledUp;

        public PlayerLevel(float baseNeed, float needPerLevel)
        {
            _baseNeed = baseNeed;
            _needPerLevel = needPerLevel;
        }

        // 오른 레벨 수를 돌려줘요.
        public int Add(int xp)
        {
            if (xp <= 0) return 0;
            Xp += xp;
            int ups = 0;
            while (Xp >= NeedForNext)
            {
                Xp -= NeedForNext;
                Level++;
                Pending++;
                ups++;
                LeveledUp?.Invoke();
            }
            return ups;
        }

        // 벽에 새긴 검결 (깊이 참오): 레벨업 없이 혈자리 하나
        public void GrantPending() => Pending++;

        public bool ConsumePending()
        {
            if (Pending <= 0) return false;
            Pending--;
            return true;
        }
    }
}

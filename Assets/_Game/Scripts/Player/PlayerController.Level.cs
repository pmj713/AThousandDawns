using AThousandDawns.Combat;
using AThousandDawns.Core;
using AThousandDawns.Enemies;

namespace AThousandDawns.Player
{
    // 수련치와 레벨업 (수련치 탭, 경맥도 탭 혈자리).
    //   적을 쓰러뜨리면 등급만큼 수련치 (일반 1 · 정예 5 · 살수 5 · 고수 15 · 보스 30).
    //   레벨업 순간: 최대 체력 +5 (현재 체력도 같이) · 몸이 빛남. 혈자리는 개혈 대기로 쌓였다가 경맥도에서 골라요.
    public sealed partial class PlayerController
    {
        PlayerLevel _level;
        float _levelMaxHealth;
        readonly int[] _xpByRank = new int[5];
        float _xpCarry; // 면벽수련 맥: 수련치 +%의 소수점은 모아 뒀다가

        public PlayerLevel Level => _level;

        void InitLevel()
        {
            _level = new PlayerLevel(GameConfig.Get("level.base_need", 25f), GameConfig.Get("level.need_per_level", 3f));
            _levelMaxHealth = GameConfig.Get("level.max_health", 5f);
            string[] keys = { "xp.normal", "xp.elite", "xp.assassin", "xp.master", "xp.boss" };
            float[] fallbacks = { 1f, 5f, 5f, 15f, 30f };
            for (int i = 0; i < keys.Length; i++) _xpByRank[i] = UnityEngine.Mathf.RoundToInt(GameConfig.Get(keys[i], fallbacks[i]));
            _level.LeveledUp += OnLeveledUp;
            Damageable.AnyDied += GainXpFromKill;
        }

        void GainXpFromKill(Damageable dead)
        {
            if (IsDead || !dead.TryGetComponent(out EnemyBrain enemy)) return; // 허수아비는 수련치 없음
            _xpCarry += _xpByRank[(int)enemy.Rank] * (1f + RunXpBonus);
            int whole = UnityEngine.Mathf.FloorToInt(_xpCarry + 1e-4f);
            _xpCarry -= whole;
            _level.Add(whole);
        }

        // 시험용 (L): 다음 레벨까지 남은 만큼
        public void GainLevelForTest() => _level.Add(_level.NeedLeft);

        void OnLeveledUp()
        {
            _baseMaxHealth += _levelMaxHealth;
            RefreshItems(); // 늘어난 최대 체력만큼 현재 체력도 채워요
            _flashTimer = 0.3f;
        }
    }
}

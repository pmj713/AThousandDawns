using System;
using AThousandDawns.Core;
using AThousandDawns.Enemies;

namespace AThousandDawns.Prototype
{
    // 한 판의 기록과 회귀의 기억 환산 (기억 시트 4~11행).
    // 기억 = 돌파한 층 × 3 + 일반 적 × 0.1 + 정예 × 1 + 살수 × 3 + 고수 × 5 + 보스 × 10 (+ 기연 방 × 5, 기시감 5, 천마 격파 30), 반올림.
    // 업보: 위 합계에 단계마다 +10% (기억 B12). 기연 방은 벽을 부숴 찾을 때 세요.
    public sealed class RunRecord
    {
        public int FloorsCleared;
        public readonly int[] Kills = new int[Enum.GetValues(typeof(EnemyRank)).Length];
        public int HiddenRooms;
        public int Deja;          // 기시감: 전생 이야기를 들음
        // 기억으로 바꾸지 않는 기록 (결과 화면에 보여 주기만)
        public float PlaySeconds;
        public int DamageDealt, MaxHit, DamageTaken, MaxTracking;
        public int NewItems;      // 기물 도감에 새로 오른 기물
        public bool FinalBoss;
        public int Karma;         // 이번 판 업보 단계

        public void AddKill(EnemyRank rank) => Kills[(int)rank]++;

        public struct Weights
        {
            public float Floor, Normal, Elite, Assassin, Master, Boss, HiddenRoom, Deja, FinalBoss, KarmaBonus;

            public static Weights FromConfig() => new Weights
            {
                Floor = GameConfig.Get("memory.per_floor", 3f),
                Normal = GameConfig.Get("memory.normal", 0.1f),
                Elite = GameConfig.Get("memory.elite", 1f),
                Assassin = GameConfig.Get("memory.assassin", 3f),
                Master = GameConfig.Get("memory.master", 5f),
                Boss = GameConfig.Get("memory.boss", 10f),
                HiddenRoom = GameConfig.Get("memory.hidden_room", 5f),
                Deja = GameConfig.Get("memory.deja", 5f),
                FinalBoss = GameConfig.Get("memory.final_boss", 30f),
                KarmaBonus = GameConfig.GetOptional("memory.karma_bonus", 0.1f),
            };
        }

        // 줄마다 (이름, 개수, 기억). 결과 화면이 그대로 그려요.
        public (string label, float count, float memory)[] Lines(Weights w)
        {
            (string label, float count, float memory)[] lines =
            {
                ("돌파한 층", (float)FloorsCleared, FloorsCleared * w.Floor),
                ("일반 적", Kills[(int)EnemyRank.Normal], Kills[(int)EnemyRank.Normal] * w.Normal),
                ("정예", Kills[(int)EnemyRank.Elite], Kills[(int)EnemyRank.Elite] * w.Elite),
                ("살수", Kills[(int)EnemyRank.Assassin], Kills[(int)EnemyRank.Assassin] * w.Assassin),
                ("고수", Kills[(int)EnemyRank.Master], Kills[(int)EnemyRank.Master] * w.Master),
                ("보스", Kills[(int)EnemyRank.Boss], Kills[(int)EnemyRank.Boss] * w.Boss),
                ("기연 방", HiddenRooms, HiddenRooms * w.HiddenRoom),
                ("기시감", Deja, Deja * w.Deja),
                ("천마 격파", FinalBoss ? 1 : 0, FinalBoss ? w.FinalBoss : 0f),
            };
            if (Karma <= 0) return lines;
            float sum = 0f;
            foreach (var line in lines) sum += line.memory;
            var withKarma = new (string label, float count, float memory)[lines.Length + 1];
            lines.CopyTo(withKarma, 0);
            withKarma[lines.Length] = ($"업보 {Karma}단계", Karma, sum * w.KarmaBonus * Karma);
            return withKarma;
        }

        // 기억으로 바꾸지 않는 기록 줄 (이름, 값 글)
        public (string label, string value)[] Stats() => new[]
        {
            ("준 피해", DamageDealt.ToString("N0")),
            ("최대 단일 피해", MaxHit.ToString("N0")),
            ("받은 피해", DamageTaken.ToString("N0")),
            ("최대 추적도", MaxTracking.ToString()),
            ("새로 만난 기물", $"{NewItems}종"),
        };

        public static string Clock(float seconds)
        {
            int s = Math.Max(0, (int)Math.Round(seconds));
            return s >= 3600 ? $"{s / 3600}:{s / 60 % 60:00}:{s % 60:00}" : $"{s / 60}:{s % 60:00}";
        }

        public int Memory(Weights w)
        {
            float total = 0f;
            foreach (var line in Lines(w)) total += line.memory;
            return (int)Math.Round(total, MidpointRounding.AwayFromZero);
        }
    }
}

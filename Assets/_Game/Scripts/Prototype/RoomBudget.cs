using System;
using System.Collections.Generic;
using AThousandDawns.Core;
using AThousandDawns.Enemies;

namespace AThousandDawns.Prototype
{
    // 방 난이도 예산: 적마다 점수를 매기고, 방마다 점수 예산 안에서 적을 뽑아 웨이브로 나눠요.
    // 들개는 둘이 한 무리로 나오고 점수도 무리 단위예요.
    public static class RoomBudget
    {
        public struct Limits
        {
            public int Archers; // 방마다 최대 궁수 (몬스터 K77)
            public int Horns;   // 나팔수 (호출이 겹치지 않게)
            public int Snakes;  // 청죽사 (숨을 풀숲 수까지)
        }

        // 죽림 일반 적 8종
        public static readonly EnemyKind[] Bamboo =
        {
            EnemyKind.Grunt, EnemyKind.Boar, EnemyKind.Archer, EnemyKind.Hound,
            EnemyKind.Shield, EnemyKind.Horn, EnemyKind.Bomber, EnemyKind.Snake,
        };

        // 폐사찰 일반 적 5종 + 죽림 궁수·들개 (몬스터 시트 41~45행)
        public static readonly EnemyKind[] Temple =
        {
            EnemyKind.Wraith, EnemyKind.Arhat, EnemyKind.Moktak, EnemyKind.Monk, EnemyKind.Lantern,
            EnemyKind.Archer, EnemyKind.Hound,
        };

        // 장강 수채 일반 적 6종 (몬스터 시트 46~51행)
        public static readonly EnemyKind[] River =
        {
            EnemyKind.Pirate, EnemyKind.Harpooner, EnemyKind.Anchor, EnemyKind.Rattan, EnemyKind.WaterGhost, EnemyKind.FlagBearer,
        };

        // 천마총 일반 적 5종 (몬스터 시트 52~56행)
        public static readonly EnemyKind[] Tomb =
        {
            EnemyKind.Cultist, EnemyKind.BloodArcher, EnemyKind.BloodShaman, EnemyKind.DemonGuard, EnemyKind.BloodBomber,
        };

        // 차원 겹침: 어느 지역에서든 섞여 나오는 판타지 적 (슬라임 · 고블린 넷)
        public static readonly EnemyKind[] Fantasy =
        {
            EnemyKind.Slime, EnemyKind.Goblin, EnemyKind.GoblinArcher, EnemyKind.GoblinRogue, EnemyKind.GoblinShaman,
        };

        public static bool IsFantasy(EnemyKind kind) => Array.IndexOf(Fantasy, kind) >= 0;

        // 지역 번호별 일반 적 (목록보다 크면 마지막 지역 것)
        public static EnemyKind[] ForRegion(int region) => WithFantasy(region <= 0 ? Bamboo : region == 1 ? Temple : region == 2 ? River : Tomb);

        public static int FantasyOverride = -1; // 테스트용 (0 무협 적만 · 1 섞기)

        // 무협 적 + 판타지 적 (시트 fantasy.enabled = 0이면 무협 적만)
        static EnemyKind[] WithFantasy(EnemyKind[] wuxia)
        {
            if (FantasyOverride == 0 || (FantasyOverride < 0 && GameConfig.GetOptional("fantasy.enabled", 1f) < 0.5f)) return wuxia;
            var all = new EnemyKind[wuxia.Length + Fantasy.Length];
            wuxia.CopyTo(all, 0);
            Fantasy.CopyTo(all, wuxia.Length);
            return all;
        }

        public static int PackSize(EnemyKind kind) => kind == EnemyKind.Hound || kind == EnemyKind.Pirate || kind == EnemyKind.Goblin ? 2 : 1; // 들개 · 수적 졸개 · 고블린은 둘씩

        public static int Cost(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Grunt: return Key("grunt", 1f);
                case EnemyKind.Boar: return Key("boar", 2f);
                case EnemyKind.Archer: return Key("archer", 2f);
                case EnemyKind.Hound: return Key("hound", 2f);
                case EnemyKind.Shield: return Key("shield", 2f);
                case EnemyKind.Horn: return Key("horn", 2f);
                case EnemyKind.Bomber: return Key("bomber", 1f);
                case EnemyKind.Snake: return Key("snake", 1f);
                case EnemyKind.Wraith: return Key("wraith", 2f);
                case EnemyKind.Arhat: return Key("arhat", 3f);
                case EnemyKind.Moktak: return Key("moktak", 2f);
                case EnemyKind.Monk: return Key("monk", 2f);
                case EnemyKind.Lantern: return Key("lantern", 1f);
                case EnemyKind.Pirate: return Key("pirate", 2f);
                case EnemyKind.Harpooner: return Key("harpooner", 2f);
                case EnemyKind.Anchor: return Key("anchor", 3f);
                case EnemyKind.Rattan: return Key("rattan", 2f);
                case EnemyKind.WaterGhost: return Key("waterghost", 1f);
                case EnemyKind.FlagBearer: return Key("flagbearer", 2f);
                case EnemyKind.Cultist: return Key("cultist", 1f);
                case EnemyKind.BloodArcher: return Key("bloodarcher", 2f);
                case EnemyKind.BloodShaman: return Key("bloodshaman", 2f);
                case EnemyKind.DemonGuard: return Key("demonguard", 3f);
                case EnemyKind.BloodBomber: return Key("bloodbomber", 1f);
                case EnemyKind.Slime: return Key("slime", 2f);
                case EnemyKind.Goblin: return Key("goblin", 2f);
                case EnemyKind.GoblinArcher: return Key("goblinarcher", 2f);
                case EnemyKind.GoblinRogue: return Key("goblinrogue", 2f);
                case EnemyKind.GoblinShaman: return Key("goblinshaman", 2f);
                default: return 99;
            }
        }

        // 방마다 수를 묶는 적: 멀리서 쏘는 적 (궁수 · 망령), 부르는 적 (나팔수 · 목탁승)
        static bool IsShooter(EnemyKind kind) => kind == EnemyKind.Archer || kind == EnemyKind.Wraith || kind == EnemyKind.Harpooner || kind == EnemyKind.BloodArcher || kind == EnemyKind.GoblinArcher;
        static bool IsCaller(EnemyKind kind) => kind == EnemyKind.Horn || kind == EnemyKind.Moktak || kind == EnemyKind.FlagBearer || kind == EnemyKind.BloodShaman || kind == EnemyKind.GoblinShaman;

        static int Key(string kind, float fallback) => Math.Max(1, (int)Math.Round(GameConfig.Get("room_cost." + kind, fallback)));

        // 예산을 다 쓸 때까지 무리를 뽑고, 섞어서 웨이브마다 점수가 고르게 나눠지도록 돌려줘요. 무리는 한 웨이브 안에 같이 있어요.
        public static List<List<EnemyKind>> Plan(int budget, int waves, IReadOnlyList<EnemyKind> pool, Func<EnemyKind, int> cost, Limits limits, Random rng)
        {
            var packs = new List<EnemyKind>();
            int archers = 0, horns = 0, snakes = 0, left = budget;
            var candidates = new List<EnemyKind>();
            while (left > 0)
            {
                candidates.Clear();
                foreach (var kind in pool)
                {
                    if (cost(kind) > left) continue;
                    if (IsShooter(kind) && archers >= limits.Archers) continue;
                    if (IsCaller(kind) && horns >= limits.Horns) continue;
                    if (kind == EnemyKind.Snake && snakes >= limits.Snakes) continue;
                    candidates.Add(kind);
                }
                if (candidates.Count == 0) break;
                var pick = candidates[rng.Next(candidates.Count)];
                packs.Add(pick);
                left -= cost(pick);
                if (IsShooter(pick)) archers++;
                if (IsCaller(pick)) horns++;
                if (pick == EnemyKind.Snake) snakes++;
            }

            for (int i = packs.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (packs[i], packs[j]) = (packs[j], packs[i]);
            }

            int waveCount = Math.Max(1, Math.Min(waves, packs.Count));
            var result = new List<List<EnemyKind>>();
            var points = new int[waveCount];
            for (int w = 0; w < waveCount; w++) result.Add(new List<EnemyKind>());
            foreach (var pack in packs)
            {
                int lightest = 0;
                for (int w = 1; w < waveCount; w++)
                    if (points[w] < points[lightest] || (points[w] == points[lightest] && result[w].Count < result[lightest].Count)) lightest = w;
                points[lightest] += cost(pack);
                for (int n = 0; n < PackSize(pack); n++) result[lightest].Add(pack);
            }
            return result;
        }
    }
}

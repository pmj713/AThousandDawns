using System;
using AThousandDawns.Core;
using AThousandDawns.Enemies;
using UnityEngine;

namespace AThousandDawns.Prototype
{
    // 업보 (기억 시트 B12, 몬스터 시트 B142): 천마를 격파하면 다음 판에 걸 수 있는 단계가 하나씩 열려요 (최대 6).
    // 마을 사당에서 골라요. 단계는 쌓여요 (3단계 = 1·2·3단계 모두). 단계마다 얻는 기억 +10%.
    //   1 적 체력 +15% · 2 정예 특성 +1 · 3 추적도 3으로 시작 · 4 적 공격 +15% · 5 방 은자 −20% · 6 고수·보스 체력 +25%
    public sealed partial class RoomRun
    {
        public const int KarmaStages = 6;

        public static readonly string[] KarmaRules =
        {
            "적 체력 +15%", "정예 특성 +1", "추적도 3으로 시작", "적 공격 +15%", "방 은자 −20%", "고수·보스 체력 +25%",
        };

        public static event Action<int> KarmaChanged; // 사당에서 단계를 바꿀 때 (마을 업보 등불)

        int _karma, _karmaMax;

        public int Karma => _karma;
        public int KarmaMax => _karmaMax; // 열린 단계 (마을 업보 등불이 매 프레임 읽어요)

        // 판 시작 (Start): 고른 단계 (열린 단계까지)
        void ApplyKarma(MetaSave meta)
        {
            _karmaMax = Mathf.Clamp(meta.karmaMax, 0, KarmaStages);
            _karma = Mathf.Clamp(meta.karma, 0, _karmaMax);
        }

        // 새 판을 시작할 때만 (이어하기는 저장한 추적도 그대로)
        void StartKarma()
        {
            if (_karma >= 3) SetTracking(Mathf.RoundToInt(GameConfig.GetOptional("karma.start_tracking", 3f)));
            if (_karma > 0) ShowBanner($"업보 {_karma}단계 · 얻는 기억 +{_karma * GameConfig.GetOptional("memory.karma_bonus", 0.1f):0%}");
        }

        float KarmaHealth(EnemyBrain enemy)
        {
            float h = _karma >= 1 ? 1f + GameConfig.GetOptional("karma.health", 0.15f) : 1f;
            if (_karma >= 6 && enemy != null && (enemy.Rank == EnemyRank.Master || enemy.Rank == EnemyRank.Boss)) h *= 1f + GameConfig.GetOptional("karma.boss_health", 0.25f);
            return h;
        }

        float KarmaAttack => _karma >= 4 ? 1f + GameConfig.GetOptional("karma.attack", 0.15f) : 1f;
        float KarmaSilver => _karma >= 5 ? 1f - GameConfig.GetOptional("karma.silver_cut", 0.2f) : 1f;

        // 2단계: 정예 특성 하나 더
        void AddKarmaTrait(EnemyBrain elite)
        {
            if (_karma >= 2) AddRandomTrait(elite);
        }

        // 테스트용: 방에서 나온 적처럼 정예 특성 · 지역 배율 · 업보를 걸어요
        public void ApplySpawnRulesForTest(EnemyBrain enemy)
        {
            if (enemy.IsElite) { RollSecondTrait(enemy); AddKarmaTrait(enemy); }
            ScaleForRegion(enemy);
        }

        // 결과 (ShowResult): 열린 가장 높은 단계로 천마를 격파하면 다음 단계가 열려요
        void UnlockKarma(MetaSave meta)
        {
            if (_karma >= meta.karmaMax) meta.karmaMax = Mathf.Min(KarmaStages, _karma + 1);
            _karmaMax = meta.karmaMax;
        }

        // 유품록 · 업보 장부 맨 아래: 사당 메뉴로 돌아가요 (업보가 아직 없으면 사당 메뉴가 없어서 닫혀요)
        EventChoice BackToShrine() => new EventChoice { Label = "뒤로 가기", Act = () => { if (KarmaMax > 0) { OpenShrineMenu(); } return null; } };

        // ── 사당 메뉴 (업보가 열린 뒤): 유품록 · 업보
        void OpenShrineMenu()
        {
            var v = new EventView { Id = "relic_book_menu", Title = "사당", Text = "향이 타는 사당. 전생의 유품과 업보가 여기에 머물러요." };
            v.Choices.Add(new EventChoice { Label = "유품록 · 다음 판에 지닐 유품 고르기", Act = () => { OpenRelicBook(); return null; } });
            v.Choices.Add(new EventChoice { Label = $"업보 · 지금 {_karma}단계 (열린 단계 {KarmaMax})", Act = () => { OpenKarmaView(); return null; } });
            v.Choices.Add(new EventChoice { Label = "닫는다", Act = () => null });
            OpenEventView(v, null);
        }

        public void OpenKarmaView()
        {
            Notify("karma", "업보: 짊어지면 판이 험해지는 대신 얻는 기억이 늘어요 · 열린 가장 높은 단계로 천마를 격파하면 다음 단계가 열려요");
            int max = KarmaMax;
            float bonus = GameConfig.GetOptional("memory.karma_bonus", 0.1f);
            var v = new EventView
            {
                Id = "karma", Title = "업보",
                Text = $"업보를 짊어지면 다음 판이 험해지는 대신 얻는 기억이 늘어요 (단계마다 +{bonus:0%}). 단계는 쌓여요. 열린 가장 높은 단계로 천마를 격파하면 다음 단계가 열려요.",
            };
            for (int stage = 0; stage <= max; stage++)
            {
                int s = stage;
                string rule = s == 0 ? "업보 없음" : $"{KarmaRules[s - 1]} · 기억 +{s * bonus:0%}";
                v.Choices.Add(new EventChoice
                {
                    Label = $"{(s == _karma ? "■" : "□")} {s}단계 · {rule}",
                    Act = () => { SetKarma(s); OpenKarmaView(); return null; },
                });
            }
            v.Choices.Add(BackToShrine()); // 닫기는 장부 오른쪽 위 (아트 UI: CloseEvent)
            OpenEventView(v, null);
        }

        void SetKarma(int stage)
        {
            var meta = MetaSave.Load();
            meta.karma = Mathf.Clamp(stage, 0, Mathf.Clamp(meta.karmaMax, 0, KarmaStages));
            meta.Save();
            _karma = meta.karma;
            KarmaChanged?.Invoke(_karma);
        }
    }
}

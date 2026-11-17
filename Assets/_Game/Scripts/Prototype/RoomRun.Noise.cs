using System.Collections.Generic;
using AThousandDawns.Core;
using AThousandDawns.Enemies;
using UnityEngine;

namespace AThousandDawns.Prototype
{
    // 소란 (마교 추적자 · 추적도를 없애며 바꾼 것): 이벤트에서 소란을 피우면 (뒤지기 · 부수기 · 털기 · 몰래 지나가기) 다음 싸우는 방 첫 웨이브에
    // 그 지역 졸개가 그만큼 더 와요. 업보 3단계는 싸우는 방마다 졸개 +1 (karma.extra_enemies).
    public sealed partial class RoomRun
    {
        int _noise;

        public int Noise => _noise;

        void AddNoise(int amount) => _noise = Mathf.Max(0, _noise + amount);

        // 보스·고수 처치 보상 (등급확률 시트 특별 보상 줄)
        static float[] SpecialGradeWeights() => new[]
        {
            GameConfig.Get("item_grade_special.common", 0.3f), GameConfig.Get("item_grade_special.uncommon", 0.32f),
            GameConfig.Get("item_grade_special.rare", 0.26f), GameConfig.Get("item_grade_special.legendary", 0.12f),
        };

        // Enter에서 (시주함 망령 다음): 소란 · 업보 3단계 졸개를 첫 웨이브에 더해요
        void AddNoiseEnemies(bool fixedWaves)
        {
            if (fixedWaves || _waves == null || _waves.Count == 0) return;
            int karmaExtra = _karma >= 3 ? Mathf.RoundToInt(GameConfig.GetOptional("karma.extra_enemies", 1f)) : 0;
            int count = _noise + karmaExtra;
            if (count <= 0) return;
            var kind = RoomBudget.ForRegion(_region)[0]; // 그 지역 졸개 (죽림 산적 · 폐사찰 망령 · 장강 수적 · 천마총 마교도)
            var first = new List<EnemyKind>(_waves[0]); // 원래 구성을 건드리지 않게 복사
            for (int i = 0; i < count; i++) first.Add(kind);
            _waves = new List<List<EnemyKind>>(_waves);
            _waves[0] = first;
            if (_noise > 0) ShowBanner($"소란을 듣고 · 졸개 {_noise}명이 몰려왔어요");
            _noise = 0;
        }
    }
}

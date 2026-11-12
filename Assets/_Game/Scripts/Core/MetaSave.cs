using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AThousandDawns.Core
{
    // 판 밖에 남는 것 (회귀해도 사라지지 않음): 회귀의 기억, 회차, 가장 깊이 간 기록.
    // persistentDataPath/meta.json 에 저장해요. 회귀록(해금)이 생기면 여기에 해금 목록을 더해요.
    [Serializable]
    public sealed class MetaSave
    {
        public int memory;        // 지금 가진 회귀의 기억 (회귀록에 쓰면 줄어요)
        public int totalMemory;   // 지금까지 얻은 기억 합계
        public int runs;          // 회차 (끝난 판 수)
        public int bestFloors;    // 가장 많이 돌파한 층
        public int bossKills;     // 지역 보스 처치 수
        public int finalBossKills; // 천마 격파 수 (유품 가지가 열려요)
        public List<string> bloomed = new List<string>(); // 회귀록에서 피운 봉우리
        public int[] meditation = new int[6]; // 면벽수련 심법별 포인트 (쾌 강 맥 오 은 기)
        public int storyFragments; // 기시감 노인에게 들은 이야기 조각 수
        public int weapon;              // 마을 무기 거치대에서 고른 무기 (0 검, 1 비도)
        public bool tutorialFloorDone;  // 입문 죽림 (첫 판 1층 고정 구성)을 한 번이라도 통과했는가
        public List<string> seenHints = new List<string>(); // 이미 본 첫 안내
        public List<string> seenItems = new List<string>(); // 한 번이라도 얻어 본 기물 (기물 도감)
        public List<string> relics = new List<string>();     // 유품록: 천마를 격파한 판에 지녔던 기물
        public List<string> relicPicks = new List<string>(); // 사당에서 고른, 다음 판에 지니고 시작할 유품
        public int karma;    // 사당에서 고른 업보 단계 (다음 판에 걸려요)
        public int karmaMax; // 열린 업보 단계 (열린 가장 높은 단계로 천마를 격파하면 +1)

        // 회귀 기록 (무공 요결 창 "회귀 기록" 탭)
        public float totalPlaySeconds;     // 모든 판 플레이 시간 합
        public int bestKarma = -1;         // 천마를 격파한 가장 높은 업보 단계 (-1: 아직)
        public int bestRegion;             // 가장 깊이 간 지역 (0 죽림 ~ 3 천마총)
        public List<NameCount> deathCauses = new List<NameCount>(); // 쓰러뜨린 것 (마지막으로 때린 적 · 기술)
        public List<NameCount> itemCounts = new List<NameCount>();  // 판이 끝날 때 지닌 기물 (판마다 1)
        public List<NameCount> skillCounts = new List<NameCount>(); // 판이 끝날 때 쓰던 초식
        public List<RunLog> recentRuns = new List<RunLog>();        // 최근 판 (새 판이 앞, 최대 10)

        // 테스트에서 실제 저장 파일을 건드리지 않게 바꿔요
        public static string PathOverride;

        public static string FilePath => PathOverride ?? Path.Combine(Application.persistentDataPath, "meta.json");

        public static MetaSave Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var meta = JsonUtility.FromJson<MetaSave>(File.ReadAllText(FilePath)) ?? new MetaSave();
                    if (meta.bloomed == null) meta.bloomed = new List<string>();
                    if (meta.seenHints == null) meta.seenHints = new List<string>();
                    if (meta.seenItems == null) meta.seenItems = new List<string>();
                    if (meta.relics == null) meta.relics = new List<string>();
                    if (meta.relicPicks == null) meta.relicPicks = new List<string>();
                    if (meta.deathCauses == null) meta.deathCauses = new List<NameCount>();
                    if (meta.itemCounts == null) meta.itemCounts = new List<NameCount>();
                    if (meta.skillCounts == null) meta.skillCounts = new List<NameCount>();
                    if (meta.recentRuns == null) meta.recentRuns = new List<RunLog>();
                    return meta;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MetaSave] 저장 파일을 읽지 못해 새로 시작해요: {e.Message}");
            }
            return new MetaSave();
        }

        public void Save()
        {
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(this, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MetaSave] 저장하지 못했어요: {e.Message}");
            }
        }

        // 이름별 횟수 +1 (회귀 기록)
        public static void Bump(List<NameCount> list, string name)
        {
            if (list == null || string.IsNullOrEmpty(name)) return;
            var found = list.Find(n => n.name == name);
            if (found != null) found.count++;
            else list.Add(new NameCount { name = name, count = 1 });
        }
    }

    [Serializable]
    public sealed class NameCount
    {
        public string name;
        public int count;
    }

    // 최근 판 한 줄 (회귀 기록)
    [Serializable]
    public sealed class RunLog
    {
        public int run;          // 제N회 회귀
        public bool victory;
        public string where;     // 결과 화면의 결말 ("천마 격파" · "폐사찰 2층 · 혜광의 염주에 쓰러짐")
        public int floors;
        public float seconds;
        public int karma;
        public int memory;
    }
}

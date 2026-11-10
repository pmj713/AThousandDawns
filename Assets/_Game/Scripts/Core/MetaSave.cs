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
    }
}

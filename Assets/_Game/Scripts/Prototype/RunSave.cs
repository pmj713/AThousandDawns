using System;
using System.Collections.Generic;
using System.IO;
using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Prototype
{
    // 판 중간 저장 (기획 대화: 방 단위 자동 저장, 이어하기는 그 방부터). run.json에 판 하나만 둬요 (저장 칸 하나).
    // 싸움·선택 창이 없는 안전한 순간에만 저장해요: 갈림길, 비운 방·싸움 없는 방, 객잔, 정비실.
    // 판이 끝나면 (결과 화면) 지워요. 진행 저장(meta.json) · 설정(settings.json)과 따로예요.
    [Serializable]
    public sealed class RunSave
    {
        [Serializable] public sealed class Room { public int x, y, kind, jars; public bool optional, visited, revealed, cleared, found, pouch; public string eventId; }
        [Serializable] public sealed class Fork { public int reward; public bool dense; }
        [Serializable] public sealed class Item { public string id; public bool onBoard; public int x, y; public bool purified; }
        [Serializable] public sealed class Part { public int kind; public bool onBoard; public int x, y; }
        [Serializable] public sealed class Point { public int x, y, kind; }
        [Serializable] public sealed class ShopSlot { public int kind, part, skill, price; public string itemId; public bool sold; }

        public int version = 1;
        public string savedAt;

        // 판
        public int phase;        // RoomRun.Phase (Fork · Floor · Inn · Rest)
        public int floor;
        public int region;     // 0 죽림 · 1 폐사찰
        public int pathReward;
        public bool pathDense;
        public List<Fork> forkOptions = new List<Fork>();
        public List<Room> rooms = new List<Room>();
        public int roomX, roomY;
        public bool masterDone;
        public bool rumor; // 객잔에서 소문을 들었음 (다음 층 기연 방)
        public int tracking, silver;
        public List<string> usedEvents = new List<string>();
        public int floorPower;
        public float floorTaken, bossGauge;
        public List<ShopSlot> shop = new List<ShopSlot>();
        public bool hasShop;

        // 기록 (결과 화면)
        public int floorsCleared, hiddenRooms, deja;
        public float playSeconds;
        public int damageDealt, maxHit, damageTaken, maxTracking;
        public int newItems;
        public int almsCurse;   // 시주함을 턴 뒤 다음 싸우는 방에 붙는 망령 수
        public bool ferry;     // 뱃사공: 다음 층 지도가 다 보여요
        public bool heroShield; // 무림맹 고수를 구함: 천마전을 보호막 두르고 시작
        public int[] kills = new int[0];

        // 플레이어
        public int weapon;
        public int ultimateForm; // 진전 (UltimateForm)
        public float health, gauge;
        public int potions, gongryeok, skill, level, xp, pending;
        public bool revive;
        public float moveBonus, gaugeBonus, takenIncrease, maxHealthCut;

        // 경맥도
        public int boardWidth, boardHeight;
        public List<Point> danjeons = new List<Point>();
        public List<Point> acupoints = new List<Point>();
        public List<Item> items = new List<Item>();
        public List<Part> parts = new List<Part>();
        public int forge0 = -1, forge1 = -1;
        public int globalMaxQi, danjeonMoves, startDanjeons = 1;

        // 테스트는 MetaSave.PathOverride 옆에 둬요 (진짜 저장을 건드리지 않게)
        public static string FilePath => MetaSave.PathOverride != null ? MetaSave.PathOverride + ".run.json" : Path.Combine(Application.persistentDataPath, "run.json");

        public static bool Exists => File.Exists(FilePath);

        public static RunSave Load()
        {
            try
            {
                if (File.Exists(FilePath)) return JsonUtility.FromJson<RunSave>(File.ReadAllText(FilePath));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RunSave] 판 저장을 읽지 못했어요: {e.Message}");
            }
            return null;
        }

        public void Save()
        {
            try
            {
                savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                File.WriteAllText(FilePath, JsonUtility.ToJson(this));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RunSave] 판을 저장하지 못했어요: {e.Message}");
            }
        }

        public static void Delete()
        {
            try
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RunSave] 판 저장을 지우지 못했어요: {e.Message}");
            }
        }

        string RegionText => RoomRun.RegionNames[UnityEngine.Mathf.Clamp(region, 0, RoomRun.RegionNames.Length - 1)];

        // 시작 화면 진행 정보 ("죽림 2층" 등)
        public string WhereText => (RoomRun.Phase)phase switch
        {
            RoomRun.Phase.Fork => $"{RegionText} {floor}층 갈림길",
            RoomRun.Phase.Inn => "객잔",
            RoomRun.Phase.Rest => "정비실",
            _ => $"{RegionText} {floor}층",
        };
    }
}

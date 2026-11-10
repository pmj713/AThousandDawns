using System.Collections.Generic;
using AThousandDawns.Core;
using AThousandDawns.Enemies;
using UnityEngine;

namespace AThousandDawns.Prototype
{
    // 추적도와 마교 살수 (몬스터 탭 126~129행, 기획 대화 추적도):
    // 전투 방을 비울 때마다 추적도 +1. 객잔에 들르면 절반. 살수를 쓰러뜨리면 −2.
    // 층에 들어갈 때 추적도 5 이상이면 살수 1명, 10 이상이면 2명 (그리고 녹림왕 체력 +15%).
    // 첫째 살수: 층에 들어와 방 2개를 지나면 시작 방에 나타나, 몇 초마다 방 하나씩 플레이어 쪽으로 와요 (지도에 붉게).
    //   싸우는 방에서 따라잡히면 1.5초 경고 뒤 난입, 비운 방이면 문이 잠기고 1:1 결투.
    // 둘째 살수: 관문 방에서 기다려요. 관문 방에 들어가면 결투.
    // 쓰러뜨리면 기물 3장 중 1장 (보스·살수 등급표) + 추적도 −2. 층을 떠나면 남은 살수는 사라져요.
    public sealed partial class RoomRun
    {
        public sealed class Hunter
        {
            public FloorRoom Room;      // 지도 위 자리 (나타나기 전엔 null)
            public bool AtGate;         // 둘째: 관문에서 기다림
            public bool Engaged, Dead;
            public float StepTimer;
            public EnemyBrain Brain;
        }

        readonly List<Hunter> _hunters = new List<Hunter>();
        int _roomsPassed;
        bool _duel;
        float _intrudeTimer = -1f;
        Hunter _intruder;
        Telegraph _intrudeMarker;
        Vector3 _intrudeSpot;

        public int Tracking { get; private set; }
        public IReadOnlyList<Hunter> Hunters => _hunters;
        public bool IsDuel => _duel;
        public bool AssassinIncoming => _intrudeTimer >= 0f;

        // 테스트용
        public void SetTracking(int value) => Tracking = Mathf.Max(0, value);

        void AddTracking(int amount) => Tracking = Mathf.Max(0, Tracking + amount);

        // 층에 들어갈 때 (지도를 만든 뒤, 시작 방에 들어가기 전)
        void BeginHunt()
        {
            EndHunt();
            _roomsPassed = 0;
            int count = Tracking >= Mathf.RoundToInt(GameConfig.Get("tracking.assassin2", 10f)) ? 2
                : Tracking >= Mathf.RoundToInt(GameConfig.Get("tracking.assassin1", 5f)) ? 1 : 0;
            if (count >= 1) _hunters.Add(new Hunter());
            if (count >= 2)
            {
                FloorRoom gate = null;
                foreach (var r in _map.Rooms) if (r.Kind == FloorRoomKind.Gate) gate = r;
                _hunters.Add(new Hunter { Room = gate, AtGate = true });
            }
            if (count > 0) ShowBanner(count == 1 ? $"추적도 {Tracking} · 마교 살수가 뒤를 쫓아요" : $"추적도 {Tracking} · 살수 둘, 하나는 관문에서 기다려요");
        }

        void EndHunt()
        {
            foreach (var h in _hunters)
                if (h.Brain != null && !h.Brain.IsDead) Destroy(h.Brain.gameObject); // 층을 떠나면 남은 살수는 사라져요
            _hunters.Clear();
            CancelIntrude();
            _duel = false;
        }

        // 방에 들어갈 때마다
        void OnHuntEnter(FloorRoom room)
        {
            if (room != _map.Start) _roomsPassed++;
            foreach (var h in _hunters)
            {
                if (h.Dead || h.Engaged) continue;
                if (h.Room == null && !h.AtGate && _roomsPassed >= Mathf.RoundToInt(GameConfig.Get("assassin.appear_rooms", 2f)))
                {
                    h.Room = _map.Start;
                    h.StepTimer = GameConfig.Get("assassin.step_time", 6f);
                    ShowBanner("마교 살수가 나타났어요 · 지도의 붉은 방");
                    Notify("assassin", "살수: 지도의 붉은 방에서 방 하나씩 쫓아와요 · 관문으로 빠져나가면 따돌릴 수 있어요");
                }
                if (h.Room == room) Engage(h);
            }
        }

        void TickHunt(float dt)
        {
            foreach (var h in _hunters)
            {
                if (h.Brain != null && h.Brain.IsDead && !h.Dead) OnAssassinDown(h);
                if (h.Dead || h.Engaged || h.Room == null || h.AtGate) continue;
                h.StepTimer -= dt;
                if (h.StepTimer > 0f) continue;
                h.StepTimer = GameConfig.Get("assassin.step_time", 6f);
                h.Room = StepToward(h.Room, _room);
                if (h.Room == _room) Engage(h);
            }

            if (_intrudeTimer < 0f) return;
            _intrudeTimer -= dt;
            float warn = GameConfig.Get("assassin.warning", 1.5f);
            if (_intrudeMarker != null) _intrudeMarker.SetProgress(1f - Mathf.Clamp01(_intrudeTimer / Mathf.Max(0.01f, warn)));
            if (_intrudeTimer > 0f) return;
            var hunter = _intruder;
            CancelIntrude();
            hunter.Brain = director.Spawn(EnemyKind.Assassin, _intrudeSpot);
            ScaleForRegion(hunter.Brain);
            Sfx.Play(SfxId.AssassinAppear);
        }

        // 따라잡힘: 1.5초 경고 (바닥 원) 뒤 나타나요. 싸우는 중이 아니면 문을 잠그고 결투.
        void Engage(Hunter h)
        {
            if (h.Engaged || _intrudeTimer >= 0f) return;
            h.Engaged = true;
            if (!_fighting)
            {
                _duel = true;
                _fighting = true;
                _waves = null;
                SetLocks(true);
            }
            _intruder = h;
            _intrudeTimer = GameConfig.Get("assassin.warning", 1.5f);
            _intrudeSpot = PickSpot();
            _intrudeMarker = Telegraph.Create(null, director.Visuals.telegraphBase, director.Visuals.telegraphFill, director.Visuals.telegraphOutline);
            _intrudeMarker.Show(TelegraphShapes.Circle(1f), true);
            _intrudeMarker.SetPose(_intrudeSpot, Vector3.forward);
            _intrudeMarker.SetProgress(0f);
            ShowBanner(_duel ? "마교 살수 · 결투" : "마교 살수가 난입해요!");
        }

        void CancelIntrude()
        {
            _intrudeTimer = -1f;
            _intruder = null;
            if (_intrudeMarker != null) Destroy(_intrudeMarker.gameObject);
            _intrudeMarker = null;
        }

        void OnAssassinDown(Hunter h)
        {
            h.Dead = true;
            AddTracking(-Mathf.RoundToInt(GameConfig.Get("tracking.kill_reduce", 2f)));
            arena.GrantOffer(SpecialGradeWeights());
            ShowBanner($"살수를 쓰러뜨렸어요 · 추적도 {Tracking}");
        }

        // 결투가 끝나면 (방은 이미 비운 방이라 은자·추적도는 없어요)
        void EndDuel()
        {
            _duel = false;
            _fighting = false;
            SetLocks(false);
        }

        // 나무 모양 지도에서 to 쪽으로 한 칸
        FloorRoom StepToward(FloorRoom from, FloorRoom to)
        {
            if (from == null || to == null || from == to) return from;
            var prev = new Dictionary<FloorRoom, FloorRoom> { [to] = null };
            var queue = new Queue<FloorRoom>();
            queue.Enqueue(to);
            while (queue.Count > 0)
            {
                var r = queue.Dequeue();
                if (r == from) break;
                for (int d = 0; d < 4; d++)
                {
                    var n = _map.Neighbor(r, d);
                    if (n == null || prev.ContainsKey(n)) continue;
                    prev[n] = r;
                    queue.Enqueue(n);
                }
            }
            return prev.TryGetValue(from, out var next) && next != null ? next : from;
        }

        // 보스·살수 처치 보상 (등급확률 시트 특별 보상 줄)
        static float[] SpecialGradeWeights() => new[]
        {
            GameConfig.Get("item_grade_special.common", 0.3f), GameConfig.Get("item_grade_special.uncommon", 0.32f),
            GameConfig.Get("item_grade_special.rare", 0.26f), GameConfig.Get("item_grade_special.legendary", 0.12f),
        };
    }
}

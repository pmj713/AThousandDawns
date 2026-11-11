using System.Collections.Generic;
using AThousandDawns.Core;
using AThousandDawns.Enemies;
using AThousandDawns.Items;
using AThousandDawns.Player;
using UnityEngine;

namespace AThousandDawns.Prototype
{
    // 폐사찰 전용 이벤트 3개 (2지역 기획): 폐사찰부터 이벤트 풀에 더해져요.
    //   시주함: 은자 50 시주 → 이번 층 받는 피해 −10% · 훔치면 은자 +80, 다음 싸우는 방에 망령 2마리가 더 붙어요.
    //   참회실: 기물 하나를 바치면 같은 등급 다른 기물로 바꿔 줘요. 받은 기물은 주화입마 부작용이 없어요 (정화).
    //   목어 소리: 따라가면 반반 · 숨은 석실 (기연 방 보상) / 매복한 파계승 3명과 전투, 이기면 희귀 기물.
    public sealed partial class RoomRun
    {
        public static readonly string[] TempleEventIds = { "almsbox", "penance", "moktak" };

        // 테스트용: 목어 소리 결과를 정해요 (0 석실 · 1 매복 · −1 무작위)
        public static int MoktakOverride = -1;

        int _almsCurse;     // 시주함을 털었음: 다음 싸우는 방 첫 웨이브에 망령 +N
        bool _ambushFight;  // 목어 소리 매복 전투 중 (이기면 희귀 기물)

        public int AlmsCurse => _almsCurse;

        bool BuildTempleEvent(EventView v, string id)
        {
            switch (id)
            {
                case "almsbox":
                {
                    v.Title = "시주함";
                    v.Text = "무너진 법당 앞, 자물쇠가 녹슨 시주함이 그대로 놓여 있다. 안에서 엽전 소리가 난다.";
                    int alms = Price("price.alms", 50f);
                    float guard = GameConfig.GetOptional("event.alms_guard", 0.1f);
                    int steal = Price("event.alms_steal", 80f);
                    int wraiths = Price("event.alms_wraiths", 2f);
                    v.Choices.Add(new EventChoice
                    {
                        Label = $"시주한다 · 은자 {alms} · 이번 층 받는 피해 −{guard:0%}",
                        Locked = Silver < alms ? "은자가 모자라요" : null,
                        Act = () =>
                        {
                            Silver -= alms;
                            _floorTaken -= guard;
                            Player.RunTakenIncrease -= guard;
                            return $"목탁 소리가 멀리서 들려요. 이번 층 받는 피해 −{guard:0%}";
                        },
                    });
                    v.Choices.Add(new EventChoice
                    {
                        Label = $"턴다 · 은자 +{steal}, 다음 싸우는 방에 망령 {wraiths}마리",
                        Act = () =>
                        {
                            Silver += steal;
                            _almsCurse += wraiths;
                            return $"은자 +{steal}. 등 뒤로 서늘한 기운이 따라붙어요 (다음 싸우는 방에 망령 {wraiths}마리)";
                        },
                    });
                    v.Choices.Add(Pass("합장하고 지나간다"));
                    return true;
                }

                case "penance":
                    v.Title = "참회실";
                    v.Text = "어둑한 참회실에 노승의 목소리만 울린다. \"내려놓을 것이 있는가.\"";
                    v.Choices.Add(new EventChoice
                    {
                        Label = "기물을 바친다 · 같은 등급 다른 기물로 · 받은 기물은 주화입마 부작용 없음",
                        Locked = PenanceCandidates().Count == 0 ? "바꿀 수 있는 기물이 없어요" : null,
                        Act = () => { OpenPenancePick(); return null; },
                    });
                    v.Choices.Add(Pass("고개만 숙이고 나온다"));
                    return true;

                case "moktak":
                {
                    v.Title = "목어 소리";
                    v.Text = "아무도 없는 절 안쪽에서 목어 두드리는 소리가 끊기지 않고 들린다.";
                    int monks = Price("event.moktak_monks", 3f);
                    v.Choices.Add(new EventChoice
                    {
                        Label = $"소리를 따라간다 · 반반: 숨은 석실 (기연) / 파계승 {monks}명 매복, 이기면 희귀 기물",
                        Act = FollowMoktak,
                    });
                    v.Choices.Add(Pass("귀를 막고 지나간다"));
                    return true;
                }
            }
            return false;
        }

        // ── 시주함: 다음 싸우는 방 첫 웨이브에 망령을 더해요 (Enter에서)
        void AddAlmsCurse()
        {
            if (_almsCurse <= 0 || _waves == null || _waves.Count == 0) return;
            var first = new List<EnemyKind>(_waves[0]); // 입문 고정 구성을 건드리지 않게 복사
            for (int i = 0; i < _almsCurse; i++) first.Add(EnemyKind.Wraith);
            _waves = new List<List<EnemyKind>>(_waves);
            _waves[0] = first;
            ShowBanner($"시주함의 업보 · 망령 {_almsCurse}마리가 따라왔어요");
            _almsCurse = 0;
        }

        // ── 참회실: 같은 등급에 아직 안 가진 다른 기물이 남아 있는 기물만
        List<OwnedItem> PenanceCandidates()
        {
            var list = new List<OwnedItem>();
            if (Player.Items == null) return list;
            foreach (var item in Player.Items.Items)
                if (SameGradeOther(item.Def, false) != null) list.Add(item);
            return list;
        }

        ItemDefinition SameGradeOther(ItemDefinition def, bool pick = true)
        {
            var db = GameConfig.Items;
            if (db == null) return null;
            var pool = new List<ItemDefinition>();
            foreach (var d in db.Available)
                if (d != null && d.grade == def.grade && d.id != def.id && !Player.Items.Has(d.id)) pool.Add(d);
            return pool.Count == 0 ? null : pool[pick ? _rng.Next(pool.Count) : 0]; // 고르지 않으면 있는지만 (난수를 안 써요)
        }

        void OpenPenancePick()
        {
            var v = new EventView { Id = "penance_pick", Title = "참회실", Text = "무엇을 내려놓을까?" };
            int shown = 0;
            foreach (var owned in PenanceCandidates())
            {
                if (shown++ >= 8) break;
                var item = owned;
                v.Choices.Add(new EventChoice
                {
                    Label = $"{item.Def.name} · {ItemDefinition.GradeLabel(item.Def.grade)}",
                    Act = () => Penance(item),
                });
            }
            v.Choices.Add(new EventChoice { Label = "그만둔다", Act = () => "노승이 말없이 염주를 굴려요." });
            OpenEventView(v, null);
        }

        public string Penance(OwnedItem item)
        {
            var other = SameGradeOther(item.Def);
            var next = other != null ? Player.Items.Replace(item, other) : null;
            if (next == null) return $"{item.Def.name}을(를) 내려놓으려 했지만 노승이 고개를 저어요.";
            next.Purified = true;
            return $"{item.Def.name}을(를) 내려놓고 {other.name}을(를) 받았어요. 주화입마에 들어도 부작용이 없어요.";
        }

        // ── 목어 소리
        string FollowMoktak()
        {
            bool hidden = MoktakOverride >= 0 ? MoktakOverride == 0 : _rng.NextDouble() < GameConfig.GetOptional("event.moktak_hidden", 0.5f);
            if (hidden)
            {
                _record.HiddenRooms++;
                OpenHiddenRoom(_room); // 기연 방과 같은 보상 창으로 바뀌어요
                return null;
            }
            CloseEvent();
            StartAmbushFight();
            ShowBanner("목어 소리가 뚝 끊기고 · 파계승들이 둘러싸요");
            return null;
        }

        void StartAmbushFight()
        {
            _ambushFight = true;
            _fighting = true;
            SetLocks(true);
            Player.EnterRoom();
            OnFightStart();
            var wave = new List<EnemyKind>();
            for (int i = 0, n = Price("event.moktak_monks", 3f); i < n; i++) wave.Add(EnemyKind.Monk);
            _waves = new List<List<EnemyKind>> { wave };
            _wave = -1;
            _elitesThisRoom = 0;
            _eliteWave = -1;
            StartNextWave();
        }
    }
}

using System;
using System.Collections.Generic;
using AThousandDawns.Core;
using AThousandDawns.Enemies;
using UnityEngine;

namespace AThousandDawns.Prototype
{
    // 장강 수채 전용 이벤트 3개 (3지역 기획): 장강 수채에서만 이벤트 풀에 더해져요.
    //   난파선: 뒤지면 추적도 +2, 대신 일반이 안 나오는 기물 고르기 (그 지역 등급표에서 일반만 뺌).
    //   수적 통행세: 은자 100을 낸다 / 거절하고 싸운다 (수적 졸개 둘 + 정예 수적 하나, 이기면 은자 +150) / 몰래 지나간다 (추적도 +3).
    //   뱃사공: 은자 60을 내면 다음 층 지도가 처음부터 전부 보여요 (기연 방이 있으면 그 자리도).
    public sealed partial class RoomRun
    {
        public static readonly string[] RiverEventIds = { "wreck", "toll", "ferry" };

        bool _tollFight;   // 수적 통행세를 거절하고 싸우는 중 (이기면 은자)
        bool _ferry;       // 뱃사공에게 삯을 냈음: 다음 층 지도가 다 보여요

        public bool FerryPending => _ferry;

        bool BuildRiverEvent(EventView v, string id)
        {
            switch (id)
            {
                case "wreck":
                {
                    v.Title = "난파선";
                    v.Text = "갈대숲 사이로 반쯤 가라앉은 상선이 기울어 있다. 갑판 아래 짐칸이 아직 잠기지 않았다.";
                    int noise = Price("event.wreck_tracking", 2f);
                    v.Choices.Add(new EventChoice
                    {
                        Label = $"짐칸을 뒤진다 · 추적도 +{noise}, 일반이 안 나오는 기물 고르기",
                        Act = () =>
                        {
                            AddTracking(noise);
                            var weights = RegionGradeWeights();
                            weights[0] = 0f; // 일반은 빼요
                            arena.GrantOffer(weights);
                            return $"썩은 상자 사이에서 쓸 만한 물건을 찾았어요. 삐걱대는 소리에 추적도 {Tracking}";
                        },
                    });
                    v.Choices.Add(Pass("지나간다"));
                    return true;
                }

                case "toll":
                {
                    v.Title = "수적 통행세";
                    v.Text = "잔교 끝을 수적들이 막아섰다. \"이 물길은 철룡 어르신 것이다. 지나가려면 내놓고 가라.\"";
                    int toll = Price("event.toll_silver", 100f);
                    int reward = Price("event.toll_reward", 150f);
                    int sneak = Price("event.toll_sneak_tracking", 3f);
                    v.Choices.Add(new EventChoice
                    {
                        Label = $"통행세를 낸다 · 은자 {toll}",
                        Locked = Silver < toll ? "은자가 모자라요" : null,
                        Act = () => { Silver -= toll; return "수적들이 길을 비켜 줘요."; },
                    });
                    v.Choices.Add(new EventChoice
                    {
                        Label = $"거절한다 · 수적 셋과 전투 (정예 하나), 이기면 은자 +{reward}",
                        Act = () => { CloseEvent(); StartTollFight(); return null; },
                    });
                    v.Choices.Add(new EventChoice
                    {
                        Label = $"몰래 지나간다 · 추적도 +{sneak}",
                        Act = () => { AddTracking(sneak); return $"갈대숲으로 돌아 지나갔지만 발자국이 남았어요. 추적도 {Tracking}"; },
                    });
                    return true;
                }

                case "ferry":
                {
                    v.Title = "뱃사공";
                    v.Text = "삿갓을 깊이 눌러쓴 뱃사공이 노를 짚고 서 있다. \"이 근방 물길이라면 눈 감고도 알지.\"";
                    int fare = Price("event.ferry_silver", 60f);
                    v.Choices.Add(new EventChoice
                    {
                        Label = $"뱃삯을 낸다 · 은자 {fare} · 다음 층 지도가 처음부터 다 보여요",
                        Locked = Silver < fare ? "은자가 모자라요" : _ferry ? "이미 물길을 들었어요" : null,
                        Act = () =>
                        {
                            Silver -= fare;
                            _ferry = true;
                            return "뱃사공이 다음 물길을 손가락으로 짚어 줘요. 다음 층에서는 지도가 다 보여요.";
                        },
                    });
                    v.Choices.Add(Pass("고개만 끄덕이고 지나간다"));
                    return true;
                }
            }
            return false;
        }

        // 수적 통행세: 수적 졸개 둘 + 정예 수적 하나 (ClearRoom에서 은자)
        void StartTollFight()
        {
            _tollFight = true;
            _fighting = true;
            SetLocks(true);
            Player.EnterRoom();
            OnFightStart();
            _waves = new List<List<EnemyKind>> { new List<EnemyKind> { EnemyKind.Pirate, EnemyKind.Pirate } };
            _wave = -1;
            _elitesThisRoom = 0;
            _eliteWave = 0;
            _eliteKind = EnemyKind.Pirate;
            var traits = new List<EliteTrait>();
            foreach (EliteTrait t in Enum.GetValues(typeof(EliteTrait)))
                if (EnemyBrain.Allows(EnemyKind.Pirate, t)) traits.Add(t);
            _eliteTrait = traits[_rng.Next(traits.Count)];
            StartNextWave();
        }

        // 뱃사공: 층에 들어설 때 지도를 다 밝혀요 (ChooseForkNow에서)
        void RevealFerry()
        {
            if (!_ferry || _map == null) return;
            _ferry = false;
            foreach (var room in _map.Rooms) room.Revealed = true;
            ShowBanner("뱃사공이 일러 준 물길대로 · 이번 층 지도가 다 보여요");
        }
    }
}

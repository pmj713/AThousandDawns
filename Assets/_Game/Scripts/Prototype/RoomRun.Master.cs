using AThousandDawns.Core;
using AThousandDawns.Enemies;
using AThousandDawns.Player;
using UnityEngine;

namespace AThousandDawns.Prototype
{
    // 고수 (미니보스): 지역마다 한 명이 마지막 층 관문 뒤, 객잔 바로 앞을 지켜요. 관문에서 F → 고수 결투장 → 객잔.
    // 죽림 고수는 둘 중 하나가 판마다 무작위 (같은 시드면 같은 고수):
    //   청죽객: 결투장 14×10, 대나무 기둥 6그루 · 비급 죽림섬
    //   철권 마웅: 결투장 14×10, 사방 돌벽 (돌진을 벽으로 유도해 기절시키기) · 비급 붕산권
    // 폐사찰 고수는 파계승 혜광 (돌벽 결투장) · 비급 금강장
    // 쓰러뜨리면 기물 대신 셋 중 하나 (기획 대화):
    //   비급 전수: 그 고수의 초식을 얻음 (회선참 대신)
    //   흡성: 공력 +1, 대신 이번 지역 동안 모든 기물의 최대 기 −1
    //   방면: 살려 보냄 → 추적도 0 + 은자
    public sealed partial class RoomRun
    {
        [SerializeField] Site master = new Site();     // 청죽객 결투장
        [SerializeField] Site ironMaster = new Site(); // 철권 마웅 결투장

        // 테스트에서 고수를 고정해요 (null이면 무작위)
        public static EnemyKind? MasterOverride;

        EnemyBrain _master;
        bool _masterDone, _masterChoiceShown;

        public EnemyBrain Master => _master;
        public IMasterBrain MasterInfo => _master as IMasterBrain;
        public bool MasterDone => _masterDone;

        public void EnterMaster()
        {
            ClearPending();
            _phase = Phase.Master;
            _room = null;
            _fighting = false;
            _masterChoiceShown = false;
            var kind = MasterOverride ?? (_region == 1 ? EnemyKind.Hyegwang // 폐사찰: 파계승 혜광
                : _rng.Next(2) == 0 ? EnemyKind.Qingzhu : EnemyKind.IronFist);
            var site = kind == EnemyKind.Qingzhu ? master : ironMaster; // 혜광도 돌벽 결투장
            MovePlayer(site.entry);
            director.ResetRoom();
            Player.EnterRoom();
            _master = director.Spawn(kind, site.center + new Vector3(0f, 0f, 2.5f));
            ScaleForRegion(_master); // 폐사찰: 체력 ×1.8 · 공격 ×1.3
            ((IMasterBrain)_master).SetZone(site.center, site.size);
            OnFightStart();
            ShowBanner($"{RegionName} 관문 · 고수 {_master.DisplayName}");
        }

        void TickMaster()
        {
            if (_master == null || !_master.IsDead || _masterChoiceShown || OffersOpen) return;
            _masterChoiceShown = true;
            Notify("master", "고수를 꺾었어요: 비급 전수(초식이 바뀜) · 흡성(공력 +1) · 방면(추적도 0) 중 하나");
            OpenMasterChoice(_masterPicks, new System.Collections.Generic.HashSet<int>());
        }

        // picks: 고를 수 있는 수 (면벽수련 오 20이면 2가지). taken: 이미 고른 것
        void OpenMasterChoice(int picks, System.Collections.Generic.HashSet<int> taken)
        {
            int spare = Price("master.spare_silver", 50f);
            bool iron = _master.Kind == EnemyKind.IronFist;
            bool monk = _master.Kind == EnemyKind.Hyegwang;
            var skill = iron ? SkillKind.Quake : monk ? SkillKind.Palm : SkillKind.BambooFlash;
            string skillName = PlayerController.SkillLabel(skill);
            string skillText = PlayerController.SkillText(skill);
            var v = new EventView
            {
                Id = "master",
                Title = iron ? "철권 마웅이 주먹을 거두었다" : monk ? "혜광이 선장을 내려놓았다" : "청죽객이 검을 내려놓았다",
                Text = iron
                    ? "\"허허, 내 주먹이 꺾였군. 권법을 가져가든, 기운을 가져가든, 그냥 보내 주든 마음대로 하게.\""
                    : monk
                    ? "\"파계한 몸이 또 졌구나. 훔쳐 둔 비급을 가져가든, 이 더러운 내력을 가져가든, 그냥 보내 주든 마음대로 하게.\""
                    : "\"졌소. 내 검을 가져가든, 기운을 가져가든, 그냥 보내 주든 마음대로 하시오.\"",
            };
            v.Choices.Add(new EventChoice
            {
                Label = $"비급 전수 · 초식이 {skillName}(으)로 바뀌어요 (우클릭: {skillText})",
                Locked = Player.Skill == skill ? "이미 배웠어요" : null,
                Act = () => { Player.LearnSkill(skill); return $"{skillName}을(를) 전수받았어요. 우클릭 초식이 바뀌었어요."; },
            });
            v.Choices.Add(new EventChoice
            {
                Label = "흡성 · 공력 +1, 대신 이번 지역 동안 모든 기물의 최대 기 −1",
                Act = () =>
                {
                    Player.Gongryeok += 1;
                    if (Player.Items != null) Player.Items.SetGlobalMaxQiBonus(Player.Items.GlobalMaxQiBonus - 1);
                    return $"{_master.DisplayName}의 내력을 빨아들였어요. 공력 +1 · 이질적인 기운에 기물 최대 기 −1";
                },
            });
            v.Choices.Add(new EventChoice
            {
                Label = $"방면 · 살려 보낸다 (추적도 0, 은자 +{spare})",
                Act = () =>
                {
                    Tracking = 0;
                    Silver += spare;
                    return $"{_master.DisplayName}이(가) 은혜를 갚겠다며 마교의 추적을 따돌려 줘요. 추적도 0 · 은자 +{spare}";
                },
            });
            if (picks > 1) v.Title += $" · {picks}가지 중 하나 더";
            for (int i = 0; i < v.Choices.Count; i++)
            {
                int index = i;
                var choice = v.Choices[i];
                if (taken.Contains(index)) choice.Locked = "이미 골랐어요";
                var act = choice.Act;
                choice.Act = () => { taken.Add(index); return act(); };
            }
            OpenEventView(v, () =>
            {
                if (picks > 1)
                {
                    OpenMasterChoice(picks - 1, taken); // 오 20: 하나 더
                    return;
                }
                _masterDone = true;
                EnterInn(); // 고수 다음은 객잔
            });
        }
    }
}

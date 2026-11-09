using AThousandDawns.Core;
using AThousandDawns.Enemies;
using AThousandDawns.Player;
using UnityEngine;

namespace AThousandDawns.Prototype
{
    // 고수 (미니보스): 지역마다 한 명이 2층 관문을 지켜요. 2층 관문에서 F → 고수 결투장 (14×10, 대나무 기둥 6그루) → 3층 갈림길.
    // 쓰러뜨리면 기물 대신 셋 중 하나 (기획 대화):
    //   비급 전수: 그 고수의 초식을 얻음 (청죽객 → 죽림섬, 회선참 대신)
    //   흡성: 공력 +1, 대신 이번 지역 동안 모든 기물의 최대 기 −1
    //   방면: 살려 보냄 → 추적도 0 + 은자
    // 죽림 첫 버전은 청죽객 하나 (철권 마웅은 다음에, 둘이 되면 무작위로 하나).
    public sealed partial class RoomRun
    {
        [SerializeField] Site master = new Site();

        QingzhuBrain _master;
        bool _masterDone, _masterChoiceShown;

        public QingzhuBrain Master => _master;
        public bool MasterDone => _masterDone;
        int MasterFloor => Mathf.RoundToInt(GameConfig.Get("room.master_floor", 2f));

        public void EnterMaster()
        {
            ClearPending();
            _phase = Phase.Master;
            _room = null;
            _fighting = false;
            _masterChoiceShown = false;
            MovePlayer(master.entry);
            director.ResetRoom();
            Player.EnterRoom();
            _master = (QingzhuBrain)director.Spawn(EnemyKind.Qingzhu, master.center + new Vector3(0f, 0f, 2.5f));
            _master.SetZone(master.center, master.size);
            ShowBanner("죽림 2층 관문 · 고수 청죽객");
        }

        void TickMaster()
        {
            if (_master == null || !_master.IsDead || _masterChoiceShown || OffersOpen) return;
            _masterChoiceShown = true;
            OpenMasterChoice();
        }

        void OpenMasterChoice()
        {
            int spare = Price("master.spare_silver", 50f);
            var v = new EventView
            {
                Id = "master",
                Title = "청죽객이 검을 내려놓았다",
                Text = "\"졌소. 내 검을 가져가든, 기운을 가져가든, 그냥 보내 주든 마음대로 하시오.\"",
            };
            v.Choices.Add(new EventChoice
            {
                Label = "비급 전수 · 초식이 죽림섬으로 바뀌어요 (우클릭: 가까운 적에게 3번 순간이동하며 베기)",
                Locked = Player.Skill == SkillKind.BambooFlash ? "이미 배웠어요" : null,
                Act = () => { Player.LearnSkill(SkillKind.BambooFlash); return "죽림섬을 전수받았어요. 우클릭 초식이 바뀌었어요."; },
            });
            v.Choices.Add(new EventChoice
            {
                Label = "흡성 · 공력 +1, 대신 이번 지역 동안 모든 기물의 최대 기 −1",
                Act = () =>
                {
                    Player.Gongryeok += 1;
                    if (Player.Items != null) Player.Items.SetGlobalMaxQiBonus(Player.Items.GlobalMaxQiBonus - 1);
                    return "청죽객의 내력을 빨아들였어요. 공력 +1 · 이질적인 기운에 기물 최대 기 −1";
                },
            });
            v.Choices.Add(new EventChoice
            {
                Label = $"방면 · 살려 보낸다 (추적도 0, 은자 +{spare})",
                Act = () =>
                {
                    Tracking = 0;
                    Silver += spare;
                    return $"청죽객이 은혜를 갚겠다며 마교의 추적을 따돌려 줘요. 추적도 0 · 은자 +{spare}";
                },
            });
            OpenEventView(v, () =>
            {
                _masterDone = true;
                BeginFork(_floor + 1);
            });
        }
    }
}

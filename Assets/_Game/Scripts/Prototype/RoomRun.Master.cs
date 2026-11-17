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
    // 장강 수채 고수는 탁랑객 (같은 결투장, 강 위 잔교로 꾸며짐) · 비급 탁랑참
    // 천마총 고수는 혈영검마 (같은 결투장, 마교 수련장으로 꾸며짐) · 비급 혈영참
    // 쓰러뜨리면 기물 대신 셋 중 하나 (기획 대화):
    //   비급 전수: 그 고수의 초식을 얻음 (회선참 대신 · 비도는 비도 초식이 바뀜)
    //   흡성: 공력 +1, 대신 이번 지역 동안 모든 기물의 최대 기 −1
    //   방면: 살려 보냄 → 은자 + 금창약
    public sealed partial class RoomRun
    {
        [SerializeField] Site master = new Site();     // 청죽객 결투장
        [SerializeField] Site ironMaster = new Site(); // 철권 마웅 결투장
        [SerializeField] Site fantasyMaster = new Site(); // 판타지 고수 결투장 (무너진 성 안뜰: 고블린 족장 · 다크 엘프 검사)

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
            var kind = MasterOverride ?? (_region >= 3 ? EnemyKind.BloodShadow // 천마총: 혈영검마
                : _region == 2 ? EnemyKind.Takrang // 장강 수채: 탁랑객
                : _region == 1 ? EnemyKind.Hyegwang // 폐사찰: 파계승 혜광
                : _rng.Next(2) == 0 ? EnemyKind.Qingzhu : EnemyKind.IronFist);
            if (MasterOverride == null && RoomBudget.FantasyOn && _rng.NextDouble() < GameConfig.GetOptional("fantasy.master_chance", 0.35f))
                kind = _rng.Next(2) == 0 ? EnemyKind.GoblinChief : EnemyKind.DarkElf; // 차원 겹침: 고수 자리에 판타지 고수가 무작위로 (고블린 족장 · 다크 엘프 검사)
            bool fantasyKind = kind == EnemyKind.GoblinChief || kind == EnemyKind.DarkElf;
            var site = kind == EnemyKind.Qingzhu ? master : fantasyKind && fantasyMaster.center != Vector3.zero ? fantasyMaster : ironMaster; // 고블린 족장도 돌벽 결투장 // 혜광 · 탁랑객도 돌벽 결투장
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
            Notify("master", "고수를 꺾었어요: 비급 전수(초식이 바뀜) · 흡성(공력 +1) · 방면(은자 · 금창약) 중 하나");
            OpenMasterChoice(_masterPicks, new System.Collections.Generic.HashSet<int>());
        }

        // picks: 고를 수 있는 수 (영혼수련 오 20이면 2가지). taken: 이미 고른 것
        void OpenMasterChoice(int picks, System.Collections.Generic.HashSet<int> taken)
        {
            int spare = Price("master.spare_silver", 50f);
            bool iron = _master.Kind == EnemyKind.IronFist;
            bool monk = _master.Kind == EnemyKind.Hyegwang;
            bool wave = _master.Kind == EnemyKind.Takrang;
            bool shadow = _master.Kind == EnemyKind.BloodShadow;
            bool chief = _master.Kind == EnemyKind.GoblinChief;
            bool elf = _master.Kind == EnemyKind.DarkElf;
            var skill = iron || chief ? SkillKind.Quake : monk ? SkillKind.Palm : wave ? SkillKind.Takrang : shadow || elf ? SkillKind.BloodShadow : SkillKind.BambooFlash;
            string skillName = PlayerController.SkillLabel(skill);
            string skillText = Player.UsesDagger ? PlayerController.DaggerScrollText(skill) : PlayerController.SkillText(skill); // 비도는 비도 초식
            var v = new EventView
            {
                Id = "master",
                Art = chief ? "master_goblin" : elf ? "master_darkelf" : iron ? "master_iron" : monk ? "master_monk" : wave ? "master_wave" : shadow ? "master_shadow" : "master_bamboo", // 고수마다 그림 (EventArt/master_*)
                Title = chief ? "고블린 족장이 철퇴를 내려놓았다" : elf ? "다크 엘프 검사가 쌍검을 거두었다" : iron ? "철권 마웅이 주먹을 거두었다" : monk ? "혜광이 선장을 내려놓았다" : wave ? "탁랑객이 검을 강물에 꽂았다" : shadow ? "혈영검마의 잔상이 흩어졌다" : "청죽객이 검을 내려놓았다",
                Text = chief
                    ? "\"크르릉… 네 녀석, 이쪽 세계 놈치고는 세군. 내 철퇴 다루는 법을 가져가든, 기운을 가져가든, 그냥 보내 주든 맘대로 해라.\""
                    : elf
                    ? "\"…빛의 땅 검객에게 진 건 처음이군. 그림자 걷는 법을 가져가든, 기운을 가져가든, 그냥 보내 주든 네 마음대로. 다음엔 내가 벤다.\""
                    : iron
                    ? "\"허허, 내 주먹이 꺾였군. 권법을 가져가든, 기운을 가져가든, 그냥 보내 주든 마음대로 하게.\""
                    : monk
                    ? "\"파계한 몸이 또 졌구나. 훔쳐 둔 비급을 가져가든, 이 더러운 내력을 가져가든, 그냥 보내 주든 마음대로 하게.\""
                    : shadow
                    ? "\"좌호법이 이렇게 지다니… 내 그림자를 가져가든, 피를 가져가든, 그냥 보내 주든 마음대로 하게. 천마께선 이미 틈 너머의 마법까지 검에 새기셨으니.\""
                    : wave
                    ? "\"흐린 물도 결국 바다로 가는 법. 내 물결을 가져가든, 기운을 가져가든, 그냥 흘려보내든 마음대로 하게.\""
                    : "\"졌소. 내 검을 가져가든, 기운을 가져가든, 그냥 보내 주든 마음대로 하시오.\"",
            };
            v.Choices.Add(new EventChoice
            {
                Label = Player.UsesDagger ? $"비급 전수 · 비도 초식이 {PlayerController.DaggerSkillLabel(skill)}(으)로 바뀌어요 ({skillText})" : $"비급 전수 · 초식이 {skillName}(으)로 바뀌어요 (우클릭: {skillText})",
                Locked = Player.Skill == skill ? "이미 배웠어요" : null,
                Act = () => { Player.LearnSkill(skill); Notify("scroll", "비급: 우클릭 초식이 바뀌어요 · 비도를 들면 비급마다 회수 초식이 달라요"); return Player.UsesDagger ? $"{skillName}을(를) 전수받았어요. 우클릭이 {PlayerController.DaggerSkillLabel(skill)}(으)로 바뀌었어요." : $"{skillName}을(를) 전수받았어요. 우클릭 초식이 바뀌었어요."; },
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
                Label = $"방면 · 살려 보낸다 (은자 +{spare}, 금창약 +1)",
                Act = () =>
                {
                    Silver += spare;
                    bool potion = Player.AddPotion(); // 고수가 품에서 금창약을 꺼내 줘요 (가득하면 그만큼 은자)
                    if (!potion) Silver += PotionSilver;
                    return $"{_master.DisplayName}이(가) 은혜를 갚겠다며 품에서 은자와 금창약을 꺼내 건네요. 은자 +{spare}{(potion ? " · 금창약 +1" : $" · 은자 +{PotionSilver} (금창약이 가득해요)")}";
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

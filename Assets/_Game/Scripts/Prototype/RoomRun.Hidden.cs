using AThousandDawns.Core;
using AThousandDawns.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AThousandDawns.Prototype
{
    // 기연 방 (판구조 기연 15%, 기획 대화 "기연 방: 내공 수위로 찾는 숨은 방"):
    //   층마다 일정 확률로 막다른 숨은 방이 하나 생겨요. 그 방 쪽 문은 벽으로 막혀 있고, 벽에 금이 가 있어요.
    //   금 간 벽 앞에서 F → 벽이 무너지고 길이 열려요. 공력 4 이상 (면벽수련 오 5면 3)이면 층에 들어설 때 지도에 희미하게 보여요.
    //   들어가면 영약 (공력 +1)이나 비급 (다른 초식) 중 하나. 찾은 기연 방은 회귀의 기억 +5.
    public sealed partial class RoomRun
    {
        // 테스트에서 기연 방을 꼭 만들어요
        public static bool HiddenOverride;

        int _hiddenSenseCut;
        GameObject _crack;
        int _crackDirection = -1;

        public int HiddenSensePower => Mathf.RoundToInt(GameConfig.GetOptional("room.hidden_sense_power", 4f)) - _hiddenSenseCut;
        public bool SensesHidden => Player.Gongryeok >= HiddenSensePower;

        // 금 간 벽 앞에 서 있음 (HUD 안내)
        public bool NearCrack => _crackDirection >= 0 && _phase == Phase.Floor && !_fighting
            && Flat(Player.transform.position - CrackPoint(_crackDirection)).magnitude < GameConfig.GetOptional("room.hidden_crack_radius", 2f);

        bool RollHidden() => HiddenOverride || _rng.NextDouble() < GameConfig.GetOptional("room.hidden_chance", 0.15f);

        // 층에 들어설 때: 기운을 느끼면 지도에 희미하게
        void SenseHidden()
        {
            var hidden = _map.Hidden;
            if (hidden == null || !SensesHidden) return;
            hidden.Revealed = true;
            ShowBanner("어딘가에서 기연의 기운이 느껴져요 · 지도의 희미한 방");
        }

        // 지금 방 옆에 찾지 못한 기연 방이 있으면 그쪽 벽에 금을 그려요 (null이면 치움)
        void PlaceCrack(FloorRoom room)
        {
            _crackDirection = -1;
            if (room != null)
                for (int d = 0; d < 4; d++)
                {
                    var n = _map.Neighbor(room, d);
                    if (n != null && n.Kind == FloorRoomKind.Hidden && !n.Found) _crackDirection = d;
                }
            if (_crackDirection < 0)
            {
                if (_crack != null) _crack.SetActive(false);
                return;
            }
            if (_crack == null)
            {
                _crack = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _crack.name = "HiddenCrack";
                var col = _crack.GetComponent<Collider>();
                col.enabled = false; // Destroy는 프레임 끝에 지워져서, 그 사이 캐릭터가 이 충돌 상자 위로 밀려 올라가지 않게 먼저 꺼요
                Destroy(col);
                var r = _crack.GetComponent<Renderer>();
                r.material.color = new Color(0.12f, 0.1f, 0.09f);
            }
            var dir = FloorMap.Directions[_crackDirection];
            float reach = (dir.x != 0 ? combat.size.x : combat.size.y) * 0.5f;
            _crack.transform.position = combat.center + new Vector3(dir.x, 0f, dir.y) * reach + Vector3.up * 1.1f;
            _crack.transform.rotation = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y));
            _crack.transform.localScale = new Vector3(0.25f, 2f, 0.6f); // 벽에 세로로 난 금
            _crack.SetActive(true);
        }

        Vector3 CrackPoint(int direction)
        {
            var dir = FloorMap.Directions[direction];
            float inset = (dir.x != 0 ? combat.size.x : combat.size.y) * 0.5f - 1f;
            return combat.center + new Vector3(dir.x, 0f, dir.y) * inset;
        }

        // 금 간 벽 앞에서 F: 벽이 무너지고 기연 방이 열려요
        bool TickCrack(Keyboard keyboard)
        {
            if (!AThousandDawns.Core.KeyBindings.Pressed(AThousandDawns.Core.Bind.Interact) || !NearCrack) return false;
            BreakCrack();
            return true;
        }

        public void BreakCrack()
        {
            if (_crackDirection < 0) return;
            var hidden = _map.Neighbor(_room, _crackDirection);
            hidden.Found = true;
            hidden.Revealed = true;
            _record.HiddenRooms++;
            if (combat.plugs[_crackDirection] != null) combat.plugs[_crackDirection].SetActive(false);
            PlaceCrack(null);
            ShowBanner("벽이 무너지고 숨은 동굴이 드러났어요 · 기연 방");
            Notify("hidden", "기연 방: 벽의 금을 부수면 열리는 숨은 방 · 영약이나 비급을 얻어요");
        }

        // 영약이나 비급 중 하나
        void OpenHiddenRoom(FloorRoom room)
        {
            var skill = Player.Skill == SkillKind.BambooFlash ? SkillKind.Quake
                : Player.Skill == SkillKind.Quake ? SkillKind.BambooFlash
                : _rng.Next(2) == 0 ? SkillKind.BambooFlash : SkillKind.Quake;
            string skillName = skill == SkillKind.Quake ? "붕산권" : "죽림섬";
            string skillText = skill == SkillKind.Quake ? "땅을 내리쳐 원형 충격파, 맞은 적을 밀어냄" : "가까운 적에게 3번 순간이동하며 베기";
            var v = new EventView
            {
                Id = "hidden",
                Title = "기연 · 숨은 동굴",
                Text = "이끼 낀 석실 한가운데, 앉은 채로 숨을 거둔 노고수의 유해가 있다. 곁에 작은 옥병과 낡은 책 한 권.",
            };
            v.Choices.Add(new EventChoice
            {
                Label = "영약을 마신다 · 공력 +1",
                Act = () => { Player.Gongryeok += 1; return "뜨거운 기운이 단전으로 모여요. 공력 +1"; },
            });
            v.Choices.Add(new EventChoice
            {
                Label = $"비급을 펼친다 · 초식이 {skillName}(으)로 바뀌어요 (우클릭: {skillText})",
                Act = () => { Player.LearnSkill(skill); return $"{skillName}을(를) 깨우쳤어요. 우클릭 초식이 바뀌었어요."; },
            });
            OpenEventView(v, null);
        }
    }
}

using AThousandDawns.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AThousandDawns.Prototype
{
    // 보상 방: 싸움 없이 작은 방 가운데에 보상이 반짝이며 놓여 있어요. 앞에서 상호작용(F)으로 받아요.
    //   기물 = 기물 주머니 (기물 고르기 창, 짙은 길이면 다음 지역 등급표) · 은자 · 수련 · 잡화 · 비급 · 대장간 · 부품 · 운기조식은 갈림길 보상 그대로.
    // 받지 않고 나가도 그 방에 남아 있어요 (다시 오면 그대로). 받으면 방을 비운 것으로 쳐요.
    public sealed partial class RoomRun
    {
        GameObject _pouch;
        Light _pouchLight;
        public static event System.Action<Vector3> PouchOpened; // 주머니를 연 자리 (이펙트용)
        public GameObject PouchObject => _pouch;              // 놓인 주머니 (이펙트가 따라붙어요)

        public bool NearPouch => _pouch != null && _pouch.activeSelf && _phase == Phase.Floor && !_fighting && _room != null && _room.Pouch && !OffersOpen
            && Flat(Player.transform.position - _pouch.transform.position).magnitude < GameConfig.GetOptional("room.pouch_radius", 1.6f);

        // 방에 들어올 때 (null이면 치움)
        void PlacePouch(FloorRoom room)
        {
            if (room != null && room.Kind == FloorRoomKind.Reward && !room.Cleared) room.Pouch = true; // 보상 방엔 받지 않은 보상이 늘 가운데에
            bool show = room != null && room.Pouch;
            if (!show)
            {
                if (_pouch != null) _pouch.SetActive(false);
                return;
            }
            if (_pouch == null) CreatePouch();
            _pouch.transform.position = Combat.center + Vector3.up * 0.5f;
            _pouch.SetActive(true);
        }

        void CreatePouch()
        {
            _pouch = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _pouch.name = "기물 주머니";
            var col = _pouch.GetComponent<Collider>();
            col.enabled = false; // 캐릭터가 올라타지 않게 (Destroy는 프레임 끝이라 먼저 꺼요)
            Destroy(col);
            _pouch.transform.localScale = new Vector3(0.7f, 0.6f, 0.7f);
            var r = _pouch.GetComponent<Renderer>();
            r.material.color = new Color(1f, 0.82f, 0.35f);
            var light = new GameObject("반짝임").AddComponent<Light>();
            light.transform.SetParent(_pouch.transform, false);
            light.type = LightType.Point;
            light.color = new Color(1f, 0.85f, 0.4f);
            light.range = 4f;
            _pouchLight = light;
        }

        // 반짝반짝: 크기와 빛이 숨 쉬듯
        void AnimatePouch()
        {
            if (_pouch == null || !_pouch.activeSelf) return;
            float t = Time.unscaledTime;
            float pulse = 1f + Mathf.Sin(t * 4f) * 0.08f;
            _pouch.transform.localScale = new Vector3(0.7f, 0.6f, 0.7f) * pulse;
            _pouch.transform.position = Combat.center + Vector3.up * (0.5f + Mathf.Sin(t * 2f) * 0.08f);
            if (_pouchLight != null) _pouchLight.intensity = 1.5f + Mathf.Sin(t * 6f) * 0.8f;
        }

        // 층 탐색 중 (싸우지 않을 때): 주머니 앞에서 F
        bool TickPouch()
        {
            AnimatePouch();
            if (!NearPouch || !KeyBindings.Pressed(Bind.Interact)) return false;
            OpenPouch();
            return true;
        }

        // 주머니 열기 → 기물 고르기 (테스트에서도 불러요)
        public void OpenPouch()
        {
            if (_room == null || !_room.Pouch) return;
            _room.Pouch = false;
            if (_pouch != null) PouchOpened?.Invoke(_pouch.transform.position); // 이펙트 신호 (아트: InkPouchFx)
            PlacePouch(null);
            _room.Cleared = true;
            Sfx.Play(SfxId.Coin);
            ShowBanner(GrantRoomReward());
            MarkSave();
        }

        // 보상 방 보상 (주머니를 열 때). 배너 글을 돌려줘요
        string GrantRoomReward()
        {
            if (GrantTutorialReward()) return "첫 기물을 얻었어요";
            switch (_path.Reward)
            {
                case RewardKind.Item:
                    arena.GrantOffer(_path.Dense ? DenseGradeWeights() : RegionGradeWeights()); // 그 지역 표 (짙으면 다음 지역 표)
                    return "기물 주머니 · 기물을 골라요";
                case RewardKind.Forge:
                    OpenForge(_path.Dense ? 2 : 1);
                    return "대장간";
                case RewardKind.Part:
                    OpenPartChoice(_path.Dense ? 2 : 1);
                    return "부품 고르기";
            }
            if (GrantForkReward(_path) is string extra) return "보상" + extra; // 은자 · 수련 · 잡화 · 비급
            float heal = Player.MaxHealth * GameConfig.Get("room.heal_ratio", 0.3f) * (_path.Dense ? DenseMultiplier : 1f);
            Player.Heal(heal);
            return $"운기조식 · 체력 +{heal:0}";
        }

        // 상호작용 안내 (보상 종류마다)
        public string PouchPrompt => InTutorialFloor ? "기물 주머니 열기" : _path.Reward switch
        {
            RewardKind.Item => "기물 주머니 열기",
            RewardKind.Forge => "대장간 화로 쓰기",
            RewardKind.Part => "부품 상자 열기",
            RewardKind.Silver => "전리품 더미 줍기",
            RewardKind.Training => "고수의 수련 흔적 살피기",
            RewardKind.Supply => "봇짐 풀기",
            RewardKind.Scroll => "낡은 서고 살피기",
            _ => "운기조식하기",
        };
    }
}

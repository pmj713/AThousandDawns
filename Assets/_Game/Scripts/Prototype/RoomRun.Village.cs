using AThousandDawns.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace AThousandDawns.Prototype
{
    // 마을 (기획 대화 마을 배치 최종안). 첫 판은 마을 없이 바로 죽림, 첫 사망 뒤부터 회귀하면 여기서 시작해요.
    //         [뒷산 동굴] 면벽수련
    //               │
    //  [객잔 마루] ★시작 ─ [매화나무] 회귀록
    //               │
    //  [사당] 유품록 ─ [마을 어귀] 무기 거치대 → 출발
    // 어귀를 나서면 씬을 다시 불러 판을 시작해요 (회귀록·면벽수련을 새로 적용).
    public sealed partial class RoomRun
    {
        public enum VillageSpot { None, Tree, Cave, Shrine, Rack }

        [SerializeField] Site village = new Site(); // entry = 객잔 마루, exit = 마을 어귀

        // 마을 안 자리 (마을 가운데 기준). 씬 빌더와 같은 값이에요.
        public static readonly Vector3 TreeOffset = new Vector3(-3f, 0f, 0f);
        public static readonly Vector3 CaveOffset = new Vector3(0f, 0f, 6f);
        public static readonly Vector3 ShrineOffset = new Vector3(-7f, 0f, -5f);
        public static readonly Vector3 RackOffset = new Vector3(1.5f, 0f, -5f);

        // 어귀를 나서서 씬을 다시 부를 때 마을을 건너뛰어요. 테스트에서도 써요.
        public static bool SkipVillage;

        public bool InVillage => _phase == Phase.Village;
        public VillageSpot NearSpot { get; private set; }
        MetaSave _villageMeta; // 마을에 있는 동안 (회귀록을 닫을 때 다시 읽어요)
        bool _villageBookOpen;
        MetaSave VillageMeta => _villageMeta ??= MetaSave.Load();

        // 첫 사망 뒤부터 마을에서 시작 (회차 1 이상)
        bool StartsInVillage()
        {
            bool skip = SkipVillage;
            SkipVillage = false;
            return !skip && MetaSave.Load().runs > 0;
        }

        public void EnterVillage()
        {
            ClearPending();
            _phase = Phase.Village;
            _room = null;
            _fighting = false;
            MovePlayer(village.entry);
            director.ResetRoom();
            arena.Blocked = false;
            Player.enabled = true;
            Time.timeScale = 1f;
            ShowBanner($"마을 · 회귀 {MetaSave.Load().runs}회차의 아침");
        }

        void TickVillage()
        {
            var keyboard = Keyboard.current;
            if (_villageBookOpen)
            {
                if (_book == null || !_book.IsOpen) CloseVillageBook(); // 회귀록 화면에서 Esc로 닫혔어요
                else if (keyboard != null && keyboard.bKey.wasPressedThisFrame) CloseVillageBook();
                return;
            }
            NearSpot = FindSpot();
            if (AThousandDawns.Core.KeyBindings.Pressed(AThousandDawns.Core.Bind.Interact) && NearSpot != VillageSpot.None) UseSpot(NearSpot);
            if (Flat(Player.transform.position - village.exit).magnitude < village.exitRadius) Depart();
        }

        VillageSpot FindSpot()
        {
            float r = GameConfig.GetOptional("village.interact_radius", 1.8f);
            Vector3 p = Player.transform.position;
            if (Near(TreeOffset)) return VillageSpot.Tree;
            if (Near(CaveOffset)) return VillageSpot.Cave;
            if (Near(ShrineOffset)) return VillageSpot.Shrine;
            if (Near(RackOffset)) return VillageSpot.Rack;
            return VillageSpot.None;
            bool Near(Vector3 offset) => Flat(p - (village.center + offset)).magnitude < r;
        }

        public string SpotPrompt(VillageSpot spot) => spot switch
        {
            VillageSpot.Tree => $"{KeyName(Bind.Interact)} 매화나무 · 회귀록",
            VillageSpot.Cave => RegressionBook.Bloomed(VillageMeta, "meditation") ? $"{KeyName(Bind.Interact)} 뒷산 동굴 · 면벽수련" : "뒷산 동굴 · 회귀록에서 면벽수련을 피우면 열려요",
            VillageSpot.Shrine => VillageMeta.finalBossKills > 0 ? $"{KeyName(Bind.Interact)} 사당 · 유품록" : "사당 · 첫 천마 격파 후 열려요 (유품록)",
            VillageSpot.Rack => RegressionBook.DaggerUnlocked(VillageMeta) ? $"{KeyName(Bind.Interact)} 무기 거치대 · 지금 {(Player.UsesDagger ? "비도" : "검")} (바꾸기)" : $"{KeyName(Bind.Interact)} 무기 거치대",
            _ => null,
        };

        public void UseSpot(VillageSpot spot)
        {
            switch (spot)
            {
                case VillageSpot.Tree:
                    OpenVillageBook(false);
                    Notify("book", "회귀록: 봉우리를 골라 개화하면 새 시스템이 열려요");
                    break;
                case VillageSpot.Cave:
                    if (RegressionBook.Bloomed(VillageMeta, "meditation")) { OpenVillageBook(true); Notify("meditation", "면벽수련: 심득을 심법에 나눠 넣어요 · 언제든 다시 나눌 수 있어요"); }
                    else ShowBanner("동굴 입구가 막혀 있어요 · 회귀록에서 면벽수련을 피우면 열려요");
                    break;
                case VillageSpot.Shrine:
                    if (VillageMeta.finalBossKills > 0) { OpenRelicBook(); Notify("relic", "유품록: 천마를 쓰러뜨린 판에 지녔던 기물을 골라 두면 다음 판을 지니고 시작해요"); }
                    else ShowBanner("닫힌 사당 · 첫 천마 격파 후 열려요");
                    break;
                case VillageSpot.Rack:
                    if (!RegressionBook.DaggerUnlocked(VillageMeta))
                    {
                        ShowBanner("검을 들었어요 · 비도는 회귀록 무기 가지에서 열려요");
                        break;
                    }
                    // 검 ↔ 비도 (다음에 회귀해도 기억해요)
                    var meta = MetaSave.Load();
                    meta.weapon = meta.weapon == 1 ? 0 : 1;
                    meta.Save();
                    _villageMeta = null;
                    Player.SetWeapon(meta.weapon == 1 ? AThousandDawns.Player.WeaponKind.Dagger : AThousandDawns.Player.WeaponKind.Sword);
                    ShowBanner(meta.weapon == 1 ? "비도를 들었어요 · 멀리서 던지고 우클릭 회수로 거둬들여요" : "검을 들었어요");
                    Notify("dagger", "비도: 꽂히거나 떨어진 비도는 6초 남아요 · 우클릭 회수로 거둬들이며 다시 베요");
                    break;
            }
        }

        void OpenVillageBook(bool meditation)
        {
            if (_book == null) return;
            _book.Open();
            _book.ShowMeditation(meditation);
            _villageBookOpen = true;
            Player.enabled = false; // 고르는 클릭이 평타로 남지 않게
            arena.Blocked = true;
        }

        void CloseVillageBook()
        {
            if (_book != null) _book.Close();
            _villageMeta = null;
            _villageBookOpen = false;
            Player.enabled = true;
            arena.Blocked = false;
        }

        // 마을 어귀: 출발 (씬을 다시 불러 회귀록·면벽수련을 적용하고 바로 죽림으로)
        // 마을 출발: 족자가 내려와 덮고 죽림이 번진 뒤 말려 올라가요 (회귀 연출은 거치지 않아요)
        public void Depart()
        {
            if (AThousandDawns.Fx.InkTransition.IsPlaying) return; // 어귀에 서 있는 동안 매 프레임 불려요
            Time.timeScale = 1f;
            int scene = SceneManager.GetActiveScene().buildIndex;
            if (!Transitions)
            {
                SkipVillage = true;
                SceneManager.LoadScene(scene);
                return;
            }
            AThousandDawns.Fx.InkTransition.Play(AThousandDawns.Fx.InkTransition.Kind.Depart, () =>
            {
                SkipVillage = true;
                SceneManager.LoadScene(scene);
            });
        }
    }
}

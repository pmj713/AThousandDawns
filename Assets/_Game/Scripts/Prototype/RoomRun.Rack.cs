using AThousandDawns.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AThousandDawns.Prototype
{
    // 마을 무기 거치대 창 (플레이테스트 6차): 상호작용하면 바로 바꾸지 않고 창이 떠요.
    //   검 · 비도 그림이 나란히 (요결 그림 HintArt/basic_attack · HintArt/dagger), 그림 밑에 "장착" 버튼. 지금 든 무기는 금빛 테두리 + "장착 중".
    //   비도가 회귀록 무기 가지에서 안 열렸으면 비도 칸은 흐리게 + 잠김 문구 (버튼 없음). Esc · B · 상호작용 · 닫기 버튼으로 닫아요.
    //   고른 무기는 다음 회귀에도 기억해요 (MetaSave.weapon). 글자 · 버튼 · 창은 InkUi 규칙.
    public sealed partial class RoomRun
    {
        bool _rackOpen;
        int _rackOpenedFrame;
        Texture2D _rackSword, _rackDagger;

        public bool RackOpen => _rackOpen;

        void OpenRack()
        {
            _rackOpen = true;
            _rackOpenedFrame = Time.frameCount;
            Player.enabled = false; // 고르는 클릭이 평타로 남지 않게
            arena.Blocked = true;
        }

        void CloseRack()
        {
            _rackOpen = false;
            Player.enabled = true;
            arena.Blocked = false;
        }

        void TickRack(Keyboard keyboard)
        {
            if (Time.frameCount == _rackOpenedFrame) return; // 연 키가 바로 닫지 않게
            bool close = (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) || Pad.Back || KeyBindings.Pressed(Bind.Interact);
            if (close) CloseRack();
        }

        // 검 ↔ 비도 (다음에 회귀해도 기억해요)
        public void EquipWeapon(bool dagger)
        {
            var meta = MetaSave.Load();
            meta.weapon = dagger ? 1 : 0;
            meta.Save();
            _villageMeta = null;
            Player.SetWeapon(dagger ? AThousandDawns.Player.WeaponKind.Dagger : AThousandDawns.Player.WeaponKind.Sword);
            ShowBanner(dagger ? "비도를 들었어요 · 멀리서 던지고 우클릭 회수로 거둬들여요" : "검을 들었어요");
            if (dagger) Notify("dagger", "비도: 꽂히거나 떨어진 비도는 6초 남아요 · 우클릭 회수로 거둬들이며 다시 베요");
        }

        void OnGUI()
        {
            if (!_rackOpen || Player == null) return;
            UiScale.Apply();
            GUI.depth = -5;
            if (_rackSword == null) _rackSword = Resources.Load<Texture2D>("HintArt/basic_attack");
            if (_rackDagger == null) _rackDagger = Resources.Load<Texture2D>("HintArt/dagger");
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f); // 뒤를 어둡게
            GUI.DrawTexture(new Rect(0, 0, UiScale.Width, UiScale.Height), Texture2D.whiteTexture);
            GUI.color = old;

            const float w = 1000f, h = 520f;
            var win = new Rect((UiScale.Width - w) * 0.5f, (UiScale.Height - h) * 0.5f, w, h);
            AThousandDawns.Fx.InkUi.Window(win);
            var ink = AThousandDawns.Fx.InkUi.Ink;
            GUI.Label(new Rect(win.x + 32, win.y + 20, 500, 44), "무기 거치대", AThousandDawns.Fx.InkUi.Text(AThousandDawns.Fx.InkUi.Heading, ink, TextAnchor.MiddleLeft, true));
            GUI.Label(new Rect(win.x + 32, win.y + 62, 700, 26), "고른 무기는 다음 회귀에도 들고 나가요", AThousandDawns.Fx.InkUi.Text(AThousandDawns.Fx.InkUi.Small, AThousandDawns.Fx.InkUi.SubInk));
            if (AThousandDawns.Fx.InkUi.Button(new Rect(win.xMax - 152, win.y + 24, 120, 44), "닫기", AThousandDawns.Fx.InkUi.Label)) CloseRack();

            bool daggerOpen = RegressionBook.DaggerUnlocked(VillageMeta);
            bool usesDagger = Player.UsesDagger;
            float cw = (w - 32 * 3) * 0.5f;
            WeaponCard(new Rect(win.x + 32, win.y + 104, cw, h - 136), "검", "가까이서 베는 세 번 연격", _rackSword, !usesDagger, true, false);
            WeaponCard(new Rect(win.x + 64 + cw, win.y + 104, cw, h - 136), "비도", "멀리서 던지고 우클릭으로 거둬들여요", _rackDagger, usesDagger, daggerOpen, true);
        }

        void WeaponCard(Rect card, string name, string about, Texture2D art, bool equipped, bool open, bool dagger)
        {
            var ink = AThousandDawns.Fx.InkUi.Ink;
            AThousandDawns.Fx.InkMeridianArt.PaperPanel(card, equipped ? new Color(1f, 0.93f, 0.75f, 0.97f) : new Color(1f, 0.99f, 0.96f, 0.97f));
            var pic = new Rect(card.x + 16, card.y + 16, card.width - 32, (card.width - 32) * 9f / 16f);
            var old = GUI.color;
            if (art != null)
            {
                GUI.color = open ? Color.white : new Color(0.55f, 0.55f, 0.55f, 0.6f); // 잠긴 무기는 흐리게
                GUI.DrawTexture(pic, art, ScaleMode.ScaleAndCrop);
            }
            if (equipped) // 지금 든 무기: 그림 둘레 금빛 테두리
            {
                GUI.color = AThousandDawns.Fx.InkUi.Gold;
                GUI.DrawTexture(new Rect(pic.x - 3, pic.y - 3, pic.width + 6, 3), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(pic.x - 3, pic.yMax, pic.width + 6, 3), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(pic.x - 3, pic.y, 3, pic.height), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(pic.xMax, pic.y, 3, pic.height), Texture2D.whiteTexture);
            }
            GUI.color = old;
            float y = pic.yMax + 10;
            GUI.Label(new Rect(card.x + 16, y, card.width - 32, 34), name, AThousandDawns.Fx.InkUi.Text(AThousandDawns.Fx.InkUi.Body, open ? ink : AThousandDawns.Fx.InkUi.Muted, TextAnchor.MiddleCenter, true));
            GUI.Label(new Rect(card.x + 16, y + 32, card.width - 32, 26), open ? about : "회귀록 무기 가지에서 열려요", AThousandDawns.Fx.InkUi.Text(AThousandDawns.Fx.InkUi.Small, AThousandDawns.Fx.InkUi.SubInk, TextAnchor.MiddleCenter));
            var button = new Rect(card.center.x - 90, card.yMax - 60, 180, 44);
            if (equipped)
                GUI.Label(button, "장착 중", AThousandDawns.Fx.InkUi.Text(AThousandDawns.Fx.InkUi.Label, AThousandDawns.Fx.InkUi.Accent, TextAnchor.MiddleCenter, true));
            else if (!open)
                GUI.Label(button, "잠김", AThousandDawns.Fx.InkUi.Text(AThousandDawns.Fx.InkUi.Label, AThousandDawns.Fx.InkUi.Muted, TextAnchor.MiddleCenter, true));
            else if (AThousandDawns.Fx.InkUi.Button(button, "장착", AThousandDawns.Fx.InkUi.Label))
                EquipWeapon(dagger);
        }
    }
}

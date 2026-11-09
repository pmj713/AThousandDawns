using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace AThousandDawns.Prototype
{
    // 일시정지 (Esc): 다른 창(진열대·이벤트·기물 고르기·경맥도·회귀록·결과)이 없을 때만 열려요. 시간이 멈춰요.
    //   계속하기 · 설정 (시작 화면과 같은 설정창) · 시작 화면으로 (한 번 더 눌러 확인. 이어하기는 마지막 저장부터, RoomRun.Save)
    public sealed partial class RoomRun
    {
        public enum PauseItem { Resume, Settings, Title }

        public bool IsPaused { get; private set; }
        public bool PauseSettingsOpen { get; private set; }
        public bool ConfirmTitle { get; private set; }
        public int PauseSelected { get; private set; }
        public SettingsPanel PauseSettings { get; } = new SettingsPanel();

        bool _overlayLastFrame;
        float _pauseTimeScale = 1f;
        MeridianScreen _meridian;

        // 다른 창이 열려 있으면 Esc는 그 창을 닫아요
        bool OverlayOpen()
        {
            if (_meridian == null) _meridian = FindFirstObjectByType<MeridianScreen>();
            return IsShopping || IsInEvent || OffersOpen || IsShowingResult || (_book != null && _book.IsOpen)
                || (_meridian != null && _meridian.IsOpen) || (Player != null && Player.Items != null && Player.Items.Pending != null);
        }

        // Update 맨 앞에서. 멈춰 있으면 true
        bool TickPause()
        {
            var k = Keyboard.current;
            var pad = Gamepad.current;
            bool esc = (k != null && k.escapeKey.wasPressedThisFrame) || (pad != null && pad.startButton.wasPressedThisFrame);
            if (!IsPaused)
            {
                bool overlay = OverlayOpen();
                if (esc && !overlay && !_overlayLastFrame) OpenPause();
                _overlayLastFrame = overlay;
                return IsPaused;
            }

            if (PauseSettingsOpen)
            {
                if (PauseSettings.TickKeys()) PauseSettingsOpen = false;
                return true;
            }
            bool up = (k != null && (k.upArrowKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame)) || (pad != null && pad.dpad.up.wasPressedThisFrame);
            bool down = (k != null && (k.downArrowKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame)) || (pad != null && pad.dpad.down.wasPressedThisFrame);
            bool ok = (k != null && (k.enterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame)) || (pad != null && pad.buttonSouth.wasPressedThisFrame);
            if (up) { PauseSelected = (PauseSelected + 2) % 3; ConfirmTitle = false; }
            if (down) { PauseSelected = (PauseSelected + 1) % 3; ConfirmTitle = false; }
            if (ok) ChoosePause((PauseItem)PauseSelected);
            else if (esc || (pad != null && pad.buttonEast.wasPressedThisFrame)) ClosePause();
            return true;
        }

        public void OpenPause()
        {
            if (IsPaused) return;
            IsPaused = true;
            PauseSelected = 0;
            ConfirmTitle = false;
            PauseSettingsOpen = false;
            _pauseTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            Player.enabled = false;
            arena.Blocked = true;
        }

        public void ClosePause()
        {
            if (!IsPaused) return;
            IsPaused = false;
            PauseSettingsOpen = false;
            Time.timeScale = _pauseTimeScale;
            Player.enabled = !IsForking; // 갈림길을 고르는 중이면 그대로 멈춰 있어요
            arena.Blocked = IsForking;
        }

        public void ChoosePause(PauseItem item)
        {
            PauseSelected = (int)item;
            switch (item)
            {
                case PauseItem.Resume:
                    ClosePause();
                    break;
                case PauseItem.Settings:
                    PauseSettingsOpen = true;
                    break;
                case PauseItem.Title:
                    if (!ConfirmTitle && _phase != Phase.Village)
                    {
                        ConfirmTitle = true; // 한 번 더 누르면 나가요
                        break;
                    }
                    if (_saveDirty && CanSave(true)) { _saveDirty = false; Capture().Save(); } // 나가기 전에 저장할 수 있으면 저장
                    Time.timeScale = 1f;
                    SceneManager.LoadScene("Title");
                    break;
            }
        }

        public void SetPauseSettings(bool open) => PauseSettingsOpen = open;
    }
}

using System;
using AThousandDawns.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AThousandDawns.Prototype
{
    // 천마 격파 엔딩: 천마가 쓰러지고 보상 기물을 고른 뒤, 결과 화면 전에 새벽이 밝아 와요.
    // 첫 격파는 대사 4줄, 두 번째부터는 1줄. 줄마다 F · Enter · Esc로 넘기고, 가만히 두면 줄마다 ending.line_time초 뒤 넘어가요.
    // 그림은 아트 쪽이 IsEnding · EndingTime · EndingLine을 읽어 그려요 (PrototypeHud.DrawEnding은 기본판).
    public sealed partial class RoomRun
    {
        static readonly string[] FirstEndingLines =
        {
            "천마가 무릎을 꿇는다. 검은 마기가 흩어지고, 무덤 위로 첫 빛이 스민다.",
            "천마: …천 번을 죽고도 돌아왔구나. 그 고집이… 결국 새벽을 데려왔어.",
            "기시감 노인: 이번 새벽은, 다시 저물지 않을지도 모르지.",
            "일조천회 — 천 번의 새벽 끝에, 한 번의 아침.",
        };

        static readonly string[] AgainEndingLines =
        {
            "천마가 다시 쓰러진다. 익숙한 새벽빛이 무덤을 적신다.",
        };

        public static event Action<bool> EndingStarted; // 첫 격파인지

        bool _ending, _endingDone, _endingFirst;
        float _endingStart, _lineStart;
        int _endingLine;

        public bool IsEnding => _ending;
        public bool EndingFirst => _endingFirst;
        public float EndingTime => _ending ? Time.unscaledTime - _endingStart : 0f;
        public int EndingLineIndex => _endingLine;
        public int EndingLineCount => EndingLines.Length;
        public string EndingLine => _ending && _endingLine < EndingLines.Length ? EndingLines[_endingLine] : null;
        string[] EndingLines => _endingFirst ? FirstEndingLines : AgainEndingLines;

        void StartEnding()
        {
            _ending = true;
            _endingFirst = MetaSave.Load().finalBossKills <= 0; // 결과에서 +1 되기 전
            _endingStart = _lineStart = Time.unscaledTime;
            _endingLine = 0;
            arena.Blocked = true;
            Player.enabled = false;
            Time.timeScale = 1f; // 천마가 무릎 꿇는 동작 · 새벽빛이 흘러가게
            EndingStarted?.Invoke(_endingFirst);
        }

        // TickResult에서: 엔딩이 끝나면 true (결과 화면으로)
        bool TickEnding()
        {
            if (!_ending) StartEnding();
            var keyboard = Keyboard.current;
            bool skip = KeyBindings.Pressed(Bind.Interact) || (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame));
            float lineTime = GameConfig.GetOptional("ending.line_time", 4f);
            if ((skip && Time.unscaledTime - _lineStart > 0.3f) || Time.unscaledTime - _lineStart >= lineTime) AdvanceEnding(); // 막 뜬 줄은 잠깐 못 넘겨요 (보상 고르던 키가 이어지지 않게)
            return _endingDone;
        }

        // 다음 줄 (마지막 줄 뒤엔 끝)
        public void AdvanceEnding()
        {
            if (!_ending) return;
            _endingLine++;
            _lineStart = Time.unscaledTime;
            if (_endingLine < EndingLines.Length) return;
            _ending = false;
            _endingDone = true;
        }
    }
}

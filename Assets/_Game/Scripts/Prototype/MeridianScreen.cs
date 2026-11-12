using AThousandDawns.Enemies;
using AThousandDawns.Items;
using AThousandDawns.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AThousandDawns.Prototype
{
    // 경맥도 화면 (E, 시험판 IMGUI). 전투 중(깨어 있는 적이 있으면)에는 열리지 않고 안내 문구를 띄워요. 여는 동안 시간이 멈춰요.
    //   처음 열면 하단전을 열 칸부터 골라요 (한 번 고르면 고정).
    //   기물은 끌어서 옮겨요: 칸에 놓으면 그 칸으로 (다른 기물이 있으면 자리를 바꿈), 보관함에 놓으면 보관함으로, 그 밖이면 제자리. 오른쪽 클릭 = 보관함으로.
    //   새로 얻은 기물은 빈 칸에 왼쪽 위부터 차례로 저절로 놓이고, 가득 차면 보관함으로 가요 (PlayerItems.Add).
    //   끌고 있으면 칸마다 그 기물이 놓였을 때의 상태가 테두리로 보여요 (회색 기 부족 · 초록 운기 · 빨강 주화입마).
    //   레벨이 오르면 개혈 대기가 쌓이고, 전투가 끝나면 (기물 고르기 뒤) 저절로 열려 혈자리(기혈·통혈·한혈 중 하나)를 고른 뒤 칸을 눌러 열어요.
    //   시험용: Shift+클릭 단전 열기/닫기 (최대 3), 공력 −/+ 버튼.
    public sealed class MeridianScreen : MonoBehaviour
    {
        const float K = 1.4f; // 경맥도 화면 크기 배율 (기준 1920×1080에서). 글자 크기도 같이 키웠어요 (EnsureStyles).
        const float CellSize = 96f * K, Gap = 6f * K, BagWidth = 300f * K, DetailHeight = 128f * K, HeaderHeight = 64f * K;

        static readonly Color LackingColor = new Color(0.6f, 0.6f, 0.6f);
        static readonly Color CirculatingColor = new Color(0.45f, 0.85f, 0.45f);
        static readonly Color DeviatedColor = new Color(1f, 0.4f, 0.35f);
        static readonly Color EmptyCell = new Color(0.12f, 0.12f, 0.14f);
        static readonly Color LowQi = new Color(0.24f, 0.21f, 0.3f);
        static readonly Color HighQi = new Color(0.82f, 0.6f, 0.24f);

        const float AcupointRowHeight = 44f * K;

        EnemyDirector _director;
        PrototypeArena _arena;
        OwnedItem _dragged, _hovered;
        Vector2Int? _hoveredCell;
        Vector2 _bagScroll;
        Rect _bagRect;
        float _noticeTimer, _autoDelay, _panelTop;
        string _notice;
        Acupoint _acupoint; // 열려고 고른 혈자리
        Vector2Int? _moveFrom; // 이혈: 옮기려고 고른 단전
        Vector2Int? _confirmCell; // 설정 "확인 창": 단전을 열거나 옮길 칸 (한 번 더 누르면 확정)
        RoomRun _run;
        bool InTutorial => (_run != null || (_run = FindFirstObjectByType<RoomRun>()) != null) && _run.InTutorialFloor;
        PartKind _placingPart; // 놓으려고 고른 부품 (부품 줄에서 고르고 빈 칸을 눌러요)
        bool _autoArmed, _wasInCombat;
        int _lastPending;
        [System.NonSerialized] GUIStyle _title, _cellName, _small, _bagButton, _noticeStyle, _detail, _tag, _button;

        public bool IsOpen { get; private set; }

        PlayerController Player => _director != null ? _director.Player : null;

        public void Init(EnemyDirector director, PrototypeArena arena)
        {
            _director = director;
            _arena = arena;
        }

        // 깨어 있는 적이 하나라도 있으면 전투 중
        public bool InCombat
        {
            get
            {
                if (_director == null) return false;
                foreach (var e in _director.Enemies)
                    if (e != null && !e.IsDead && e.IsAwake) return true;
                return false;
            }
        }

        void Update()
        {
            if (_noticeTimer > 0f) _noticeTimer -= Time.unscaledDeltaTime;
            UpdateAutoOpen();
            var keyboard = Keyboard.current;
            if (IsOpen)
            {
                if (TickPad()) return; // 게임패드 커서 (B는 놓기 취소가 먼저)
                if (AThousandDawns.Core.KeyBindings.Pressed(AThousandDawns.Core.Bind.Meridian) || (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) || AThousandDawns.Core.Pad.Back) Close();
                return;
            }
            if (AThousandDawns.Core.KeyBindings.Pressed(AThousandDawns.Core.Bind.Meridian)) TryOpen();
        }

        // 레벨이 오르면 (전투 중이면 전투가 끝난 뒤, 기물 고르기도 끝난 뒤) 경맥도가 저절로 열려 혈자리를 골라요.
        // 닫아도 개혈 대기는 남고, 다음 레벨업이나 다음 전투가 끝날 때 다시 열려요 (E로 언제든 열 수 있음).
        void UpdateAutoOpen()
        {
            var player = Player;
            if (IsOpen || player == null || player.Level == null) return;
            int pending = player.Level.Pending;
            bool combat = InCombat;
            if (pending > _lastPending || (_wasInCombat && !combat && pending > 0))
            {
                _autoArmed = true;
                _autoDelay = 0.4f;
            }
            _lastPending = pending;
            _wasInCombat = combat;
            if (!_autoArmed || pending <= 0)
            {
                _autoArmed = false;
                return;
            }
            if (combat || player.IsDead || (_arena != null && (_arena.IsChoosing || _arena.PendingOffers > 0)))
            {
                _autoDelay = 0.4f; // 기물 고르기가 먼저
                return;
            }
            _autoDelay -= Time.unscaledDeltaTime;
            if (_autoDelay > 0f) return;
            _autoArmed = false;
            TryOpen();
        }

        public void TryOpen()
        {
            var player = Player;
            if (player == null || player.IsDead || player.Items == null) return;
            if (_arena != null && _arena.IsChoosing) return;
            if (InCombat)
            {
                Notice("전투 중에는 경맥도를 열 수 없어요");
                return;
            }
            IsOpen = true;
            _dragged = null;
            Time.timeScale = 0f;
            player.enabled = false; // 클릭이 평타로 남지 않게
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            _dragged = null;
            Time.timeScale = 1f;
            if (Player != null) Player.enabled = true;
        }

        void OnDestroy()
        {
            if (IsOpen) Time.timeScale = 1f;
        }

        void Notice(string text)
        {
            _notice = text;
            _noticeTimer = 1.5f;
        }

        void OnGUI()
        {
            GUI.depth = -10;
            UiScale.Apply(); // 기준 1920×1080, 어떤 창 크기에도 같은 비율
            EnsureStyles();
            bool open = IsOpen && Player != null && Player.Items != null;
            if (open) DrawScreen(Player.Items);
            DrawNotice(open);
        }

        // 안내 문구는 경맥도보다 나중에 (맨 앞에) 그려요. 열려 있으면 판 바로 위, 아니면 화면 위쪽.
        void DrawNotice(bool open)
        {
            if (_noticeTimer <= 0f || string.IsNullOrEmpty(_notice)) return;
            float y = open ? Mathf.Max(8f, _panelTop - 48f * K) : UiScale.Height * 0.3f;
            var size = _noticeStyle.CalcSize(new GUIContent(_notice));
            var back = new Rect((UiScale.Width - size.x) * 0.5f - 16f * K, y, size.x + 32f * K, 40f * K);
            Fill(back, new Color(0.1f, 0.06f, 0.02f, 0.92f));
            Outline(back, new Color(1f, 0.85f, 0.4f), 1f);
            GUI.Label(back, _notice, _noticeStyle);
        }

        void DrawScreen(PlayerItems items)
        {
            var board = items.Board;
            _hovered = null;
            _hoveredCell = null;

            AThousandDawns.Fx.InkMeridianArt.Backdrop(UiScale.Width, UiScale.Height); // 먹 번짐 + 경혈도 바탕 (뒤의 HUD 글자가 비치지 않게)

            float boardW = board.Width * (CellSize + Gap) - Gap;
            float boardH = board.Height * (CellSize + Gap) - Gap;
            bool picking = Player.Level != null && Player.Level.Pending > 0 && board.Danjeons.Count > 0;
            if (!picking) _acupoint = Acupoint.None;
            bool hasParts = items.Parts.Count > 0;
            if (!hasParts) _placingPart = PartKind.None;
            float header = HeaderHeight + (picking ? AcupointRowHeight : 0f) + (hasParts ? AcupointRowHeight : 0f);
            float totalW = boardW + 24f * K + BagWidth;
            float totalH = header + boardH + 16f * K + DetailHeight;
            float left = (UiScale.Width - totalW) * 0.5f;
            float top = Mathf.Max(8f, (UiScale.Height - totalH) * 0.5f);
            _panelTop = top;

            DrawHeader(items, new Rect(left, top, totalW, HeaderHeight));
            if (picking) DrawAcupointRow(new Rect(left, top + HeaderHeight, totalW, AcupointRowHeight - 6f * K));
            if (hasParts) DrawPartRow(items, new Rect(left, top + HeaderHeight + (picking ? AcupointRowHeight : 0f), totalW, AcupointRowHeight - 6f * K));

            float boardTop = top + header;
            for (int y = 0; y < board.Height; y++)
            for (int x = 0; x < board.Width; x++)
            {
                var cell = new Vector2Int(x, y);
                var rect = new Rect(left + x * (CellSize + Gap), boardTop + y * (CellSize + Gap), CellSize, CellSize);
                DrawCell(items, cell, rect);
                if (PadActive && cell == PadCell) _padRect = rect;
            }

            DrawBag(items, new Rect(left + boardW + 24f * K, boardTop, BagWidth, boardH));
            DrawDetail(items, new Rect(left, boardTop + boardH + 16f * K, totalW, DetailHeight));
            HandleDrop(items);
            if (PadActive) AThousandDawns.Fx.InkPadUi.Frame(_padRect, 4f * K); // 게임패드 커서 칸: 먹 붓 테두리
            DrawDragGhost();
            if (PadActive) // 아래 가운데 붉은 낙관 안내
                AThousandDawns.Fx.InkPadUi.Hints(UiScale.Width * 0.5f, UiScale.Height - 58f, 34f,
                    ("A", _dragged != null ? "놓기" : "집기"), ("X", "주머니"), ("Y", "꺼내기"), ("LB·RB", "혈자리"), ("B", _dragged != null ? "취소" : "닫기"));
        }

        Rect _padRect; // 게임패드 커서 칸 (집은 기물이 그 위에 매달려요)

        void DrawHeader(PlayerItems items, Rect area)
        {
            var board = items.Board;
            GUI.Label(new Rect(area.x, area.y, area.width, 28f * K),
                $"경맥도 · 공력 {board.Power} · 단전 {board.Danjeons.Count}/{board.MaxDanjeons}   (E 닫기 · 끌어서 옮기기 · 오른쪽 클릭 주머니 · Shift+클릭 단전 시험)", _small);
            string guide = board.Danjeons.Count == 0
                ? "<color=#ffd27a><b>하단전을 열 칸을 고르세요.</b> 한 번 고르면 바뀌지 않아요. 칸의 기 = 단전마다 (공력 − 거리)</color>"
                : board.Danjeons.Count < items.StartDanjeons
                ? "<color=#ffd27a><b>두 번째 단전을 열 칸을 고르세요</b> (면벽수련 맥 20). 한 번 고르면 바뀌지 않아요.</color>"
                : items.DanjeonMoves > 0
                ? (_moveFrom.HasValue ? $"<color=#ffd27a><b>이혈</b> · 단전을 옮길 새 칸을 고르세요 (같은 칸을 누르면 취소)</color>" : $"<color=#ffd27a><b>이혈 {items.DanjeonMoves}번</b> · 옮길 단전을 누르세요</color>")
                : "칸의 기 = 단전마다 (공력 − 거리)의 합. 거리는 상하좌우로 센 칸 수예요.";
            GUI.Label(new Rect(area.x, area.y + 26f * K, area.width - 220f * K, 30f * K), guide, _small);

            var minus = new Rect(area.xMax - 210f * K, area.y + 26f * K, 100f * K, 26f * K);
            var plus = new Rect(area.xMax - 104f * K, area.y + 26f * K, 100f * K, 26f * K);
            if (GUI.Button(minus, "공력 − (시험)", _button)) Player.Gongryeok -= 1;
            if (GUI.Button(plus, "공력 + (시험)", _button)) Player.Gongryeok += 1;
        }

        // 개혈 대기: 세 종류가 항상 모두 나와요. 하나를 고르고 칸을 누르면 열려요 (한 칸에 하나).
        void DrawAcupointRow(Rect area)
        {
            Fill(area, new Color(0.25f, 0.2f, 0.08f, 0.9f));
            GUI.Label(new Rect(area.x + 8f * K, area.y + 8f * K, 250f * K, 24f * K), $"<color=#ffd27a><b>개혈 대기 {Player.Level.Pending}</b> · 고르고 칸 누르기</color>", _small);
            string[] labels =
            {
                $"기혈 · 기 +{Player.Items.Board.GihyeolQi}",
                "통혈 · 지나는 기가 줄지 않음",
                $"한혈 · 최대 기 +{Player.Items.Board.HanhyeolMaxQi}",
            };
            float x = area.x + 260f * K;
            float w = (area.xMax - x - 8f * K) / 3f;
            for (int i = 0; i < 3; i++)
            {
                var kind = (Acupoint)(i + 1);
                var rect = new Rect(x + i * w, area.y + 4f * K, w - 6f * K, area.height - 8f * K);
                bool chosen = _acupoint == kind;
                if (GUI.Button(rect, (chosen ? "▶ " : "") + labels[i], _button)) _acupoint = chosen ? Acupoint.None : kind;
                if (chosen) Outline(rect, new Color(1f, 0.85f, 0.4f), 2f);
            }
        }

        // 부품: 종류마다 남은 개수. 고르고 빈 칸을 누르면 놓여요 · 판의 부품은 오른쪽 클릭으로 거둬요
        void DrawPartRow(PlayerItems items, Rect area)
        {
            Fill(area, new Color(0.12f, 0.18f, 0.22f, 0.9f));
            GUI.Label(new Rect(area.x + 8f * K, area.y + 8f * K, 250f * K, 24f * K), "<color=#9fd4ff><b>부품</b> · 고르고 빈 칸 · 오른쪽 클릭 거두기</color>", _small);
            PartKind[] kinds = { PartKind.Tongmaek, PartKind.Jeomhyeol, PartKind.Hanok };
            string[] hints = { "지나는 기가 줄지 않음", "기를 막음", $"붙은 기물 최대 기 +{items.Board.HanokMaxQi}" };
            float x = area.x + 260f * K;
            float w = (area.xMax - x - 8f * K) / 3f;
            for (int i = 0; i < 3; i++)
            {
                var kind = kinds[i];
                int loose = items.CountParts(kind, true), total = items.CountParts(kind);
                var rect = new Rect(x + i * w, area.y + 4f * K, w - 6f * K, area.height - 8f * K);
                bool chosen = _placingPart == kind;
                GUI.enabled = total > 0;
                if (GUI.Button(rect, $"{(chosen ? "▶ " : "")}{PlayerItems.PartLabel(kind)} {loose}/{total} · {hints[i]}", _button))
                    _placingPart = chosen || loose == 0 ? PartKind.None : kind;
                GUI.enabled = true;
                ItemIcons.Draw(new Rect(rect.xMax - rect.height, rect.y + 2f, rect.height - 4f, rect.height - 4f), ItemIcons.Get(kind), total > 0 ? 1f : 0.35f);
                if (chosen) Outline(rect, new Color(0.6f, 0.85f, 1f), 2f);
            }
        }

        static string PartMark(PartKind kind) => kind switch
        {
            PartKind.Tongmaek => "<color=#9fd4ff><b>━ 통맥</b></color>",
            PartKind.Jeomhyeol => "<color=#ff8a7a><b>✕ 점혈</b></color>",
            PartKind.Hanok => "<color=#c8f0ff><b>◆ 한옥</b></color>",
            _ => "",
        };

        void DrawCell(PlayerItems items, Vector2Int cell, Rect rect)
        {
            var board = items.Board;
            int qi = board.QiAt(cell);
            var item = items.At(cell);
            bool hover = rect.Contains(Event.current.mousePosition) || (PadActive && cell == PadCell); // 게임패드 커서 칸도 마우스를 올린 것처럼
            if (hover)
            {
                _hoveredCell = cell;
                if (item != null) _hovered = item;
            }

            float t = Mathf.Clamp01(qi / Mathf.Max(3f, board.Power));
            AThousandDawns.Fx.InkMeridianArt.Cell(rect, qi <= 0 ? EmptyCell : Color.Lerp(LowQi, HighQi, t), board.IsDanjeon(cell)); // 붓 칸 (기 색으로 물듦)
            string tags = "";
            if (board.IsDanjeon(cell))
            {
                Outline(new Rect(rect.x + 4f * K, rect.y + 4f * K, rect.width - 8f * K, rect.height - 8f * K), new Color(1f, 0.85f, 0.4f), 2f);
                tags = _moveFrom == cell ? "<color=#ff8a7a><b>옮길 단전</b></color>" : "<b>단전</b>";
            }
            if (hover || AThousandDawns.Core.GameSettings.Current.qiNumbers == AThousandDawns.Core.GameSettings.QiNumbers.Always) // 설정: 기 숫자
                GUI.Label(new Rect(rect.x + 6f * K, rect.y + 4f * K, 50f * K, 18f * K), $"기 <b>{qi}</b>", _small);
            if (_confirmCell == cell) Outline(new Rect(rect.x + 2f * K, rect.y + 2f * K, rect.width - 4f * K, rect.height - 4f * K), new Color(1f, 0.55f, 0.45f), 3f); // 확인 대기
            if (board.Danjeons.Count == 0 && InTutorial && cell == new Vector2Int(board.Width / 2, board.Height / 2)) // 입문: 가운데 칸 추천
            {
                float pulse = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 4f);
                Outline(new Rect(rect.x + 2f * K, rect.y + 2f * K, rect.width - 4f * K, rect.height - 4f * K), new Color(1f, 0.85f, 0.4f, pulse), 4f);
                GUI.Label(new Rect(rect.x, rect.yMax - 22f * K, rect.width, 18f * K), "<b>추천</b>", _tag);
            }
            var point = board.AcupointAt(cell);
            if (point != Acupoint.None) tags = tags.Length > 0 ? $"{tags} {AcupointTag(point)}" : AcupointTag(point);
            if (point != Acupoint.None) ItemIcons.Draw(new Rect(rect.x + 3f * K, rect.yMax - 29f * K, 26f * K, 26f * K), ItemIcons.Get(point));
            if (tags.Length > 0) GUI.Label(new Rect(rect.x + 30f * K, rect.y + 4f * K, rect.width - 34f * K, 18f * K), tags, _tag); // 오른쪽 위: 단전 · 혈자리 (겹치지 않게 한 줄로)

            var part = board.PartAt(cell);
            if (part != PartKind.None)
            {
                Fill(new Rect(rect.x + 6f * K, rect.y + 24f * K, rect.width - 12f * K, rect.height - 34f * K), new Color(0.08f, 0.1f, 0.14f, 0.85f));
                float partSize = Mathf.Min(rect.width, rect.height) - 40f * K;
                if (ItemIcons.Draw(new Rect(rect.center.x - partSize * 0.5f, rect.y + 22f * K, partSize, partSize), ItemIcons.Get(part)))
                    GUI.Label(new Rect(rect.x, rect.yMax - 26f * K, rect.width, 22f * K), PartMark(part), _cellName);
                else GUI.Label(new Rect(rect.x, rect.y + 30f * K, rect.width, 40f * K), PartMark(part), _cellName);
            }
            if (item != null && item != _dragged) DrawItemInCell(item, rect);
            else if (item != null) GUI.Label(new Rect(rect.x, rect.y + 30f * K, rect.width, 40f * K), "<color=#9a9a9a>(옮기는 중)</color>", _cellName);

            // 끌고 있으면 이 칸에 놓았을 때의 상태. 혈자리를 고르는 중이면 열 수 있는 칸만 밝게.
            if (_placingPart != PartKind.None) Outline(rect, item == null && part == PartKind.None && !board.IsDanjeon(cell) ? new Color(0.6f, 0.85f, 1f, hover ? 1f : 0.35f) : new Color(0.4f, 0.4f, 0.4f), hover ? 3f : 1f);
            else if (_acupoint != Acupoint.None) Outline(rect, point == Acupoint.None ? new Color(1f, 0.85f, 0.4f, hover ? 1f : 0.35f) : new Color(0.4f, 0.4f, 0.4f), hover ? 3f : 1f);
            else if (_dragged != null) Outline(rect, StateColor(Preview(_dragged.Def, qi, board.MaxQiBonusAt(cell))), 3f);
            else if (hover) Outline(rect, Color.white, 1f);

            var e = Event.current;
            if (e.type == EventType.MouseDown && hover)
            {
                OnCellClicked(items, cell, e.button, e.shift);
                e.Use();
            }
        }

        void DrawItemInCell(OwnedItem item, Rect rect)
        {
            var def = item.Def;
            var state = PlayerItems.StateOf(item);
            // 아이콘은 칸 가운데에 흐리게 깔고 글자를 위에 (기 부족이면 더 흐리게)
            float iconSize = Mathf.Min(rect.width, rect.height) - 16f * K;
            ItemIcons.Draw(new Rect(rect.center.x - iconSize * 0.5f, rect.center.y - iconSize * 0.5f, iconSize, iconSize), def, state == QiState.Lacking ? 0.3f : 0.6f);
            GUI.Label(new Rect(rect.x + 2f * K, rect.y + 22f * K, rect.width - 4f * K, 38f * K), $"<b>{def.name}</b>", _cellName);
            GUI.Label(new Rect(rect.x + 2f * K, rect.y + 54f * K, rect.width - 4f * K, 18f * K), $"{Colored(def.keyword)} {StateLabel(item)}", _cellName);
            DrawQiBar(new Rect(rect.x + 8f * K, rect.yMax - 14f * K, rect.width - 16f * K, 6f * K), def.minQi, item.MaxQi, item.Qi);
            Outline(rect, StateColor(state), 2f);
        }

        // [최소━●━최대] 막대: 초록 = 운기 구간 (한혈이면 최대 기 +), 흰 점 = 지금 기
        static void DrawQiBar(Rect bar, int minQi, int maxQi, int qi)
        {
            float range = Mathf.Max(maxQi + 2, qi + 1, 1);
            float X(float v) => bar.x + bar.width * Mathf.Clamp01(v / range);
            Fill(bar, new Color(0.25f, 0.25f, 0.25f));
            Fill(new Rect(X(minQi), bar.y, Mathf.Max(2f, X(maxQi) - X(minQi)), bar.height), CirculatingColor);
            Fill(new Rect(X(qi) - 2f, bar.y - 3f, 4f, bar.height + 6f), Color.white);
        }

        // 보관함: 기물을 끌어 판에 놓거나, 판의 기물을 여기로 끌어 놓아 넣어요.
        void DrawBag(PlayerItems items, Rect area)
        {
            _bagRect = area;
            AThousandDawns.Fx.InkMeridianArt.Panel(area); // 먹 패널
            if (_dragged != null && _dragged.OnBoard)
                Outline(area, new Color(1f, 0.85f, 0.4f, area.Contains(Event.current.mousePosition) ? 1f : 0.4f), 2f); // 여기로 놓을 수 있어요
            string cap = !items.HasBag ? "없음" : items.BagCapacity < 0 ? $"{items.BagCount}개" : $"{items.BagCount}/{items.BagCapacity}칸";
            GUI.Label(new Rect(area.x + 8f * K, area.y + 4f * K, area.width - 16f * K, 22f * K), $"<b>주머니</b> {cap}  (놓지 않은 기물은 효과 없음)", _small);
            GUI.Label(new Rect(area.x + 8f * K, area.y + 24f * K, area.width - 16f * K, 20f * K), items.HasBag ? "<color=#9a9a9a>판의 기물을 여기로 끌어 놓으면 주머니로</color>" : "<color=#9a9a9a>회귀록에서 주머니를 열어야 넣을 수 있어요</color>", _small);
            float y = area.y + 48f * K;

            int count = items.BagCount;
            var view = new Rect(area.x + 4f * K, y, area.width - 8f * K, area.yMax - y - 4f * K);
            var content = new Rect(0f, 0f, view.width - 18f, Mathf.Max(view.height, count * 34f * K));
            _bagScroll = GUI.BeginScrollView(view, _bagScroll, content);
            float row = 0f;
            var e = Event.current;
            foreach (var item in items.Items)
            {
                if (item.OnBoard) continue;
                var rect = new Rect(0f, row, content.width, 30f * K);
                bool dragging = item == _dragged;
                string label = $"{(dragging ? "<color=#9a9a9a>(옮기는 중)</color> " : "")}<b>{item.Def.name}</b>  {Colored(item.Def.keyword)} · {ItemDefinition.GradeLabel(item.Def.grade)} · 기 {item.Def.minQi}–{item.Def.maxQi}";
                if (rect.Contains(e.mousePosition))
                {
                    _hovered = item;
                    if (e.type == EventType.MouseDown && e.button == 0 && !Busy)
                    {
                        _dragged = item; // 끌기 시작
                        e.Use();
                    }
                }
                GUI.Box(rect, label, _bagButton);
                ItemIcons.Draw(new Rect(rect.xMax - rect.height - 2f, rect.y + 1f, rect.height - 2f, rect.height - 2f), item.Def, dragging ? 0.4f : 1f);
                row += 34f * K;
            }
            GUI.EndScrollView();
        }

        // 하단전을 고르는 중이거나 혈자리를 고르는 중이면 기물을 옮기지 않아요
        bool Busy => _acupoint != Acupoint.None || _moveFrom.HasValue || (Player != null && Player.Items != null && Player.Items.Board.Danjeons.Count < Player.Items.StartDanjeons);

        // 손을 떼면: 칸 위면 놓기 (다른 기물이 있으면 자리 바꿈), 보관함 위면 보관함으로, 그 밖이면 제자리
        void HandleDrop(PlayerItems items)
        {
            var e = Event.current;
            if (_dragged == null || e.rawType != EventType.MouseUp) return;
            if (_hoveredCell.HasValue) items.Place(_dragged, _hoveredCell.Value);
            else if (_bagRect.Contains(e.mousePosition) && !items.ToBag(_dragged)) Notice(items.HasBag ? "주머니가 가득해요" : "주머니가 없어요 · 회귀록에서 열어요");
            _dragged = null;
            if (e.type == EventType.MouseUp) e.Use();
        }

        // 끌고 있는 기물을 마우스 옆에
        void DrawDragGhost()
        {
            if (_dragged == null) return;
            var m = Event.current.mousePosition;
            var matrix = GUI.matrix;
            if (PadActive) // 게임패드로 집은 기물: 커서 칸 위 붓끝에 매달려 흔들려요
            {
                var tip = new Vector2(_padRect.center.x, _padRect.y - 46f * K);
                m = new Vector2(tip.x, tip.y + 34f * K);
                float swing = Mathf.Sin(Time.unscaledTime * 3.2f) * 9f;
                GUIUtility.RotateAroundPivot(swing, tip);
                var old = GUI.color;
                GUI.color = new Color(0.1f, 0.09f, 0.08f, 0.9f); // 붓끝에서 내려온 먹 실
                GUI.DrawTexture(new Rect(tip.x - 1.5f, tip.y, 3f, 12f * K), Texture2D.whiteTexture);
                GUI.color = old;
            }
            var rect = new Rect(m.x - 48f * K, m.y - 24f * K, 96f * K, 44f * K);
            Fill(rect, new Color(0.15f, 0.12f, 0.08f, 0.92f));
            Outline(rect, Color.white, 1f);
            GUI.Label(rect, $"<b>{_dragged.Def.name}</b>\n{Colored(_dragged.Def.keyword)}", _cellName);
            ItemIcons.Draw(new Rect(rect.x - 44f * K, rect.y, 44f * K, 44f * K), _dragged.Def);
            GUI.matrix = matrix;
        }

        void DrawDetail(PlayerItems items, Rect area)
        {
            AThousandDawns.Fx.InkMeridianArt.Panel(area); // 먹 패널
            if (_acupoint != Acupoint.None)
            {
                string what = _acupoint == Acupoint.Gihyeol ? $"이 칸의 기가 {items.Board.GihyeolQi} 올라요."
                    : _acupoint == Acupoint.Tonghyeol ? "기가 이 칸을 지날 때 줄지 않아요. 단전에서 이 칸을 거쳐 가는 칸들의 기가 올라요. 기물도 놓을 수 있어요."
                    : $"이 칸에 놓인 기물의 최대 기가 {items.Board.HanhyeolMaxQi} 올라요 (주화입마가 덜 되고, 레벨도 그만큼 더 올라요).";
                string cellText = _hoveredCell.HasValue
                    ? items.Board.AcupointAt(_hoveredCell.Value) != Acupoint.None ? "  <color=#ff8a7a>이 칸에는 이미 혈자리가 있어요</color>" : $"  (이 칸의 기 {items.Board.QiAt(_hoveredCell.Value)})"
                    : "";
                GUI.Label(new Rect(area.x + 10f * K, area.y + 8f * K, area.width - 20f * K, area.height - 16f * K),
                    $"<b>{AcupointName(_acupoint)}</b> · {what}{cellText}\n한 칸에 혈자리는 하나만 열 수 있어요. 한 번 열면 바꿀 수 없어요. 다시 누르면 고르기를 취소해요.", _detail);
                return;
            }
            var shown = _hovered ?? _dragged;
            if (shown == null)
            {
                GUI.Label(new Rect(area.x + 10f * K, area.y + 8f * K, area.width - 20f * K, area.height - 16f * K),
                    "기물에 마우스를 올리면 효과가 보여요. 기물을 들고 칸 위에 올리면 그 칸에서의 상태가 보여요.\n" +
                    "기 부족: 효과 없음 · 운기: 효과, 키워드 1 · 주화입마: 효과 ×1.5와 부작용, 키워드 2. 레벨 = (기와 최대 기 중 작은 값) − 최소 기 + 1", _detail);
                return;
            }

            var def = shown.Def;
            string aux = string.IsNullOrEmpty(def.aux) ? "" : $" · 보조 #{def.aux}";
            string where = shown.OnBoard ? $"지금 기 {shown.Qi} · {StateLabel(shown)}" : "보관함 (효과 없음)";
            if (_dragged != null && _hoveredCell.HasValue)
            {
                int qi = items.Board.QiAt(_hoveredCell.Value);
                int bonus = items.Board.MaxQiBonusAt(_hoveredCell.Value);
                var preview = Preview(_dragged.Def, qi, bonus);
                where = $"이 칸에 놓으면 기 {qi}{(bonus > 0 ? $" (한혈: 최대 기 +{bonus})" : "")} → {PreviewLabel(_dragged.Def, qi, bonus, preview)}";
                def = _dragged.Def;
            }
            string text = $"<b>{def.name}</b>  {Colored(def.keyword)} · {ItemDefinition.GradeLabel(def.grade)}{aux} · 기 {def.minQi}–{def.maxQi}   <color=#ffd27a>{where}</color>\n" +
                          $"{def.effectText}\n" + (shown.Purified && def == shown.Def ? "<color=#9ad0ff>정화됨 · 주화입마 부작용 없음 (참회실)</color>" : $"<color=#ff8a7a>주화입마 부작용: {def.sideText}</color>");
            GUI.Label(new Rect(area.x + 10f * K, area.y + 8f * K, area.width - 20f * K, area.height - 16f * K), text, _detail);
        }

        // 설정 "확인 창"이 켜져 있으면 같은 칸을 두 번 눌러야 해요. 아직 확인 전이면 true (알림을 띄우고 멈춤)
        bool NeedsConfirm(Vector2Int cell, string message)
        {
            if (!AThousandDawns.Core.GameSettings.Current.confirmDialogs || _confirmCell == cell)
            {
                _confirmCell = null;
                return false;
            }
            _confirmCell = cell;
            Notice(message);
            return true;
        }

        void OnCellClicked(PlayerItems items, Vector2Int cell, int button, bool shift)
        {
            if (_confirmCell.HasValue && _confirmCell != cell) _confirmCell = null; // 다른 칸을 누르면 확인 취소
            var board = items.Board;
            if (shift && button == 0)
            {
                // 시험용: 단전 열기/닫기 (중단전·상단전 시험)
                if (board.IsDanjeon(cell)) items.RemoveDanjeon(cell);
                else if (!items.AddDanjeon(cell)) Notice($"단전은 {board.MaxDanjeons}개까지예요");
                return;
            }
            if (board.Danjeons.Count < items.StartDanjeons && !board.IsDanjeon(cell))
            {
                if (NeedsConfirm(cell, "한 번 더 누르면 여기에 단전을 열어요 (바꿀 수 없어요)")) return;
                items.AddDanjeon(cell); // 첫 하단전 (맥 20이면 둘째 단전까지)
                return;
            }
            // 이혈: 단전을 누르고 → 새 칸을 눌러 옮겨요
            if (items.DanjeonMoves <= 0) _moveFrom = null;
            if (items.DanjeonMoves > 0 && button == 0)
            {
                if (!_moveFrom.HasValue)
                {
                    if (board.IsDanjeon(cell))
                    {
                        _moveFrom = cell;
                        return;
                    }
                }
                else
                {
                    if (cell == _moveFrom.Value || board.IsDanjeon(cell)) _moveFrom = board.IsDanjeon(cell) && cell != _moveFrom.Value ? cell : (Vector2Int?)null;
                    else if (board.PartAt(cell) == PartKind.None && NeedsConfirm(cell, "한 번 더 누르면 단전을 이 칸으로 옮겨요 (이혈 1번 사용)")) return;
                    else if (items.MoveDanjeon(_moveFrom.Value, cell)) { _moveFrom = null; Notice("단전을 옮겼어요"); }
                    else Notice("부품이 있는 칸에는 단전을 못 옮겨요");
                    return;
                }
            }
            // 부품: 오른쪽 클릭이면 거두고, 고른 부품이 있으면 빈 칸에 놓아요
            var placed = items.PartAt(cell);
            if (button == 1 && placed != null)
            {
                items.RemovePart(placed);
                return;
            }
            if (_placingPart != PartKind.None && button == 0)
            {
                OwnedPart loose = null;
                foreach (var p in items.Parts) if (p.Kind == _placingPart && !p.OnBoard) { loose = p; break; }
                if (loose == null) _placingPart = PartKind.None;
                else if (!items.PlacePart(loose, cell)) Notice("기물·부품·단전이 없는 빈 칸에만 놓을 수 있어요");
                else if (items.CountParts(_placingPart, true) == 0) _placingPart = PartKind.None;
                return;
            }
            if (_acupoint != Acupoint.None)
            {
                if (button != 0) return;
                if (board.AcupointAt(cell) != Acupoint.None) Notice("한 칸에 혈자리는 하나만 열 수 있어요");
                else if (items.OpenAcupoint(cell, _acupoint) && Player.Level.ConsumePending()) _acupoint = Acupoint.None;
                return;
            }

            var item = items.At(cell);
            if (button == 1)
            {
                if (item != null && !items.ToBag(item)) Notice(items.HasBag ? "주머니가 가득해요" : "주머니가 없어요 · 회귀록에서 열어요");
                if (item == _dragged) _dragged = null;
                return;
            }
            if (button == 0 && item != null) _dragged = item; // 끌기 시작 (손을 떼는 곳에 놓여요)
        }

        // ── 게임패드: 십자키 칸 옮기기 · A 집기/놓기 (혈자리 · 부품 · 단전도 A) · X 주머니로 · Y 주머니 기물 꺼내기 · LB/RB 혈자리 종류 · B 놓기 취소
        public Vector2Int PadCell { get; private set; }
        public bool PadActive => IsOpen && AThousandDawns.Core.Pad.Active;
        public OwnedItem Held => _dragged; // 지금 집은 기물 (마우스로 끄는 중이거나 패드로 집은 것)
        int _padBagIndex;

        // 패드 입력을 썼으면 true (B로 놓기를 취소했으면 창을 닫지 않아요)
        bool TickPad()
        {
            var items = Player != null ? Player.Items : null;
            if (items == null) return false;
            var pad = AThousandDawns.Core.Pad.Active; // 읽기만 해도 스틱 방향을 갱신해요
            var board = items.Board;
            int dx = AThousandDawns.Core.Pad.Left ? -1 : AThousandDawns.Core.Pad.Right ? 1 : 0;
            int dy = AThousandDawns.Core.Pad.Up ? -1 : AThousandDawns.Core.Pad.Down ? 1 : 0;
            if (dx != 0 || dy != 0)
                PadCell = new Vector2Int(Mathf.Clamp(PadCell.x + dx, 0, board.Width - 1), Mathf.Clamp(PadCell.y + dy, 0, board.Height - 1));

            if (AThousandDawns.Core.Pad.Confirm)
            {
                if (_dragged != null) { items.Place(_dragged, PadCell); _dragged = null; }
                else OnCellClicked(items, PadCell, 0, false);
                return true;
            }
            if (AThousandDawns.Core.Pad.West) { OnCellClicked(items, PadCell, 1, false); return true; }
            if (AThousandDawns.Core.Pad.North && _dragged == null && !Busy)
            {
                var bag = new System.Collections.Generic.List<OwnedItem>();
                foreach (var it in items.Items) if (!it.OnBoard) bag.Add(it);
                if (bag.Count == 0) Notice("주머니가 비어 있어요");
                else { _dragged = bag[_padBagIndex++ % bag.Count]; Notice($"{_dragged.Def.name} · 놓을 칸에서 A"); }
                return true;
            }
            bool picking = Player.Level != null && Player.Level.Pending > 0 && board.Danjeons.Count > 0;
            if (picking && (AThousandDawns.Core.Pad.LeftShoulder || AThousandDawns.Core.Pad.RightShoulder))
            {
                int step = AThousandDawns.Core.Pad.LeftShoulder ? 2 : 1; // 기혈 → 통혈 → 한혈 → (없음)
                _acupoint = (Acupoint)(((int)_acupoint + step) % 4);
                return true;
            }
            if (AThousandDawns.Core.Pad.Back && (_dragged != null || _acupoint != Acupoint.None || _moveFrom.HasValue))
            {
                _dragged = null;
                _acupoint = Acupoint.None;
                _moveFrom = null;
                return true;
            }
            return false;
        }

        // ── 표시 도우미

        // maxBonus = 그 칸의 한혈 (최대 기 +)
        static QiState Preview(ItemDefinition def, int qi, int maxBonus) =>
            qi < def.minQi ? QiState.Lacking : qi > def.maxQi + maxBonus ? QiState.Deviated : QiState.Circulating;

        static string PreviewLabel(ItemDefinition def, int qi, int maxBonus, QiState state)
        {
            int level = qi < def.minQi ? 0 : Mathf.Min(qi, def.maxQi + maxBonus) - def.minQi + 1;
            return state == QiState.Lacking ? "기 부족" : state == QiState.Circulating ? $"운기 {level}레벨" : $"주화입마 {level}레벨";
        }

        static string AcupointName(Acupoint point) => point == Acupoint.Gihyeol ? "기혈" : point == Acupoint.Tonghyeol ? "통혈" : "한혈";

        static string AcupointTag(Acupoint point) => point switch
        {
            Acupoint.Gihyeol => "<color=#ffd27a><b>기혈</b></color>",
            Acupoint.Tonghyeol => "<color=#7fe0ff><b>통혈</b></color>",
            _ => "<color=#b8d8ff><b>한혈</b></color>",
        };

        static string StateLabel(OwnedItem item)
        {
            var state = PlayerItems.StateOf(item);
            string label = PreviewLabel(item.Def, item.Qi, item.MaxQiBonus, state);
            return state == QiState.Lacking ? $"<color=#9a9a9a>{label}</color>" : state == QiState.Deviated ? $"<color=#ff6a5a>{label}</color>" : label;
        }

        static Color StateColor(QiState state) => state == QiState.Lacking ? LackingColor : state == QiState.Deviated ? DeviatedColor : CirculatingColor;

        static string Colored(Keyword keyword)
        {
            string color = keyword switch
            {
                Keyword.Yeolyang => "#ff9a4a",
                Keyword.Bingbaek => "#8fd3ff",
                Keyword.Noejeong => "#ffe45c",
                Keyword.Ganggi => "#cdeeff",
                Keyword.Geumgang => "#e8c46a",
                Keyword.Gyeonggong => "#9be0a0",
                _ => "#dddddd",
            };
            return $"<color={color}>{PlayerKeywords.Label(keyword)}</color>";
        }

        static void Fill(Rect rect, Color color)
        {
            var old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        static void Outline(Rect rect, Color color, float width)
        {
            Fill(new Rect(rect.x, rect.y, rect.width, width), color);
            Fill(new Rect(rect.x, rect.yMax - width, rect.width, width), color);
            Fill(new Rect(rect.x, rect.y, width, rect.height), color);
            Fill(new Rect(rect.xMax - width, rect.y, width, rect.height), color);
        }

        void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, richText = true };
            _title.normal.textColor = Color.white;
            _small = new GUIStyle(GUI.skin.label) { fontSize = 18, richText = true, wordWrap = true };
            _small.normal.textColor = Color.white;
            _cellName = new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter, fontSize = 18 };
            _bagButton = new GUIStyle(GUI.skin.button) { fontSize = 18, richText = true, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(11, 11, 6, 6) };
            _bagButton.normal.textColor = _bagButton.hover.textColor = Color.white;
            _noticeStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _noticeStyle.normal.textColor = new Color(1f, 0.85f, 0.5f);
            _detail = new GUIStyle(_small) { fontSize = 19 };
            _tag = new GUIStyle(_small) { alignment = TextAnchor.UpperRight, fontSize = 16, wordWrap = false };
            _button = new GUIStyle(GUI.skin.button) { fontSize = 17, richText = true };
            AThousandDawns.Fx.InkMeridianArt.Restyle(_bagButton); // 먹 붓 획 바탕
            AThousandDawns.Fx.InkMeridianArt.Restyle(_button);
        }
    }
}

using AThousandDawns.Combat;
using AThousandDawns.Enemies;
using AThousandDawns.Items;
using AThousandDawns.Player;
using UnityEngine;

namespace AThousandDawns.Prototype
{
    // 시험판 화면 표시 (IMGUI). 나중에 진짜 HUD로 바꿀 자리예요.
    public sealed partial class PrototypeHud : MonoBehaviour
    {
        static readonly Color HealthColor = new Color(0.78f, 0.27f, 0.24f);   // 매화 홍 #C8463C
        static readonly Color EnemyHealthColor = new Color(0.92f, 0.85f, 0.7f);
        static readonly Color BarBack = new Color(0f, 0f, 0f, 0.55f);

        [SerializeField] PlayerController player;
        [SerializeField] TrainingDummy[] dummies;
        [SerializeField] EnemyDirector director;
        [SerializeField] PrototypeArena arena;
        [SerializeField] RoomRun run; // 판 구조 씬에만

        static readonly Color BurnDot = new Color(0.85f, 0.28f, 0.06f);  // 진한 주홍 (예고 #FF9A2E와 구분)
        static readonly Color SlowDot = new Color(0.55f, 0.82f, 0.98f);  // 하늘색
        static readonly Color ShockDot = new Color(1f, 0.89f, 0.3f);     // 노랑

        [System.NonSerialized] GUIStyle _hintLabel, _label, _wrapLabel, _popup, _dummy, _big, _bossLabel, _popupSmall, _popupBig, _statusLabel, _card, _itemLine, _itemSmall;
        Camera _camera;
        int _lastLevel = 1;
        float _levelUpTimer;

        void Update()
        {
            DamagePopups.Tick(Time.deltaTime);
            if (_levelUpTimer > 0f) _levelUpTimer -= Time.unscaledDeltaTime;
            if (player != null && player.Level != null && player.Level.Level > _lastLevel)
            {
                _lastLevel = player.Level.Level;
                _levelUpTimer = 2f;
            }
        }

        void OnGUI()
        {
            UiScale.Apply(); // 기준 1920×1080, 어떤 창 크기에도 같은 비율
            DrawHud();
            DrawMinimap();
            DrawFork();
            DrawShop();
            DrawEvent();
            DrawResult();
            DrawOffer(); // 기물 카드는 맨 위에
            DrawDiscard(); // 둘 곳이 없을 때 버릴 기물 고르기는 그보다 위에
            DrawPause(); // 일시정지는 맨 위
        }

        void DrawHud()
        {
            EnsureStyles();
            if (_camera == null) _camera = Camera.main;

            GUILayout.BeginArea(new Rect(16, 16, 700, 466), GUI.skin.box);
            GUILayout.Label("일조천회 시험판 · 전투", _label);
            GUILayout.Label("WASD 이동 · 좌클릭(누르고 있기) 평타 · 우클릭 회선참 · Q 만검귀종 · Space 대시 · R 금창약(누르고 있기)", _label);
            GUILayout.Label("부르기 (바로 옆): 1 졸개 · 2 멧돼지 · 3 궁수 · 4 들개 · 5 방패 · 6 나팔수 · 7 화약 · 8 청죽사 · 9 녹림왕 (결투장) · 0 살수 · - 청죽객 · = 철권 마웅 · Shift+숫자 정예", _label);
            GUILayout.Label("G 기세 채우기 · F5 처음부터 · 키워드 F1 열양 F2 빙백 F3 뇌정 F4 강기 F6 금강 F7 경공 F8 마도 F9 정종 · 기물 상태 F10 기 부족 F11 운기 F12 주화입마 · I 기물 고르기 · E 경맥도 · L 레벨업 · K 쓰러지기 · P 부품 셋 · O / Shift+O 무기 홈 1 / 2 바꾸기 · V 검 ↔ 비도", _wrapLabel);
            if (player != null)
            {
                GUILayout.Label($"체력 {player.Health:0}/{player.MaxHealth:0}", _label);
                Rect bar = GUILayoutUtility.GetRect(300, 10);
                bar.width = 300;
                DrawBar(bar, player.MaxHealth > 0f ? player.Health / player.MaxHealth : 0f, HealthColor);
                int shield = player.Shields.Total;
                if (shield > 0) DrawBar(new Rect(bar.x, bar.y - 5f, bar.width, 4f), shield / Mathf.Max(1f, player.MaxHealth), Color.white); // 보호막: 체력바 위 흰 막대

                var combo = player.Combo;
                string slow = player.SlowLeft > 0f ? $"  · 둔화 {player.SlowRatio:0%} {player.SlowLeft:0.0}초" : "";
                if (player.BurnStacks > 0) slow += $"  · 화상 {player.BurnStacks}중첩";
                if (shield > 0) slow += $"  · 보호막 {shield}";
                if (player.PerfectBonusLeft > 0f) slow += $"  · 간파 다음 공격 +{player.PerfectBonusRatio:0%}";
                if (player.MadoPenaltyLeft > 0f) slow += $"  · 받는 피해 +30% {player.MadoPenaltyLeft:0.0}초";
                if (player.GanggiImprintLeft > 0f) slow += $"  · 강기 각인 {player.GanggiImprintLeft:0.0}초";
                if (player.NoDashLeft > 0f) slow += $"  · 대시 불가 {player.NoDashLeft:0.0}초";
                if (player.BladeStacks > 0) slow += $"  · 검날 {player.BladeStacks}중첩";
                if (player.TabletUsed) slow += "  · 불괴 호패 씀";
                GUILayout.Label($"대시 {player.Dash.Charges}/{player.Dash.MaxCharges}  {(player.IsInvulnerable ? "(무적)" : "")}{slow}", _label);
                string ultimate = player.IsCastingUltimate ? "  · 만검귀종!" : player.UltimateReady ? "  · Q 만검귀종 준비" : "";
                GUILayout.Label($"기세 {player.Gauge:0}/{player.GaugeCap:0}{ultimate}", _label);
                string drink = player.IsDrinking ? $"  · 마시는 중 {player.PotionProgress:0%}" : "";
                GUILayout.Label($"금창약 {player.Potions}/{player.PotionMax}{drink}", _label);
                var level = player.Level;
                if (level != null) GUILayout.Label($"레벨 {level.Level} · 수련치 {level.Xp}/{level.NeedForNext}{(level.Pending > 0 ? $"  <color=#ffd27a>· 개혈 대기 {level.Pending}</color>" : "")}", _label);
                string skill = player.IsCastingSkill ? "베는 중" : player.SkillCooldownLeft > 0f ? $"{player.SkillCooldownLeft:0.0}초" : "준비";
                GUILayout.Label($"{player.SkillName} {skill}", _label);
                if (combo != null) GUILayout.Label($"평타 {combo.HitIndex + 1}타 · {combo.CurrentPhase}", _label);
                var keywords = player.Keywords;
                if (keywords != null)
                {
                    var sb = new System.Text.StringBuilder();
                    for (int i = 0; i < PlayerKeywords.KeywordCount; i++)
                    {
                        var k = (Keyword)i;
                        if (i > 0) sb.Append(" · ");
                        sb.Append(PlayerKeywords.Label(k)).Append(' ').Append(keywords.Count(k));
                    }
                    GUILayout.Label(sb.ToString(), _label);
                    if (player.Items != null && (player.Items.ForgeSlot(0).HasValue || player.Items.ForgeSlot(1).HasValue))
                    {
                        string Slot(int s) => player.Items.ForgeSlot(s) is Keyword fk ? PlayerKeywords.Label(fk) : "빈 홈";
                        GUILayout.Label($"무기 홈: {Slot(0)} · {Slot(1)}{(player.Items.ForgeSlot(0).HasValue && player.Items.ForgeSlot(0) == player.Items.ForgeSlot(1) ? " <color=#ffd27a>(강화판)</color>" : "")}", _label);
                    }
                    GUILayout.Label($"기물 상태: 기 부족 {keywords.Qi(QiState.Lacking)} · 운기 {keywords.Qi(QiState.Circulating)} · 주화입마 {keywords.Qi(QiState.Deviated)}   (2·4·6에서 단계 효과, 4면 절초 각인)", _label);
                }
            }
            if (director != null && director.MeleeTokens != null)
            {
                string wave = run != null ? $"{run.StatusText} · " : arena != null && arena.RoundNumber > 0 ? $"{arena.RoundNumber}번째 배치 · " : "";
                GUILayout.Label($"{wave}적 {director.Enemies.Count}  ·  공격 중 근접 {director.MeleeTokens.InUse}/{director.MeleeTokens.Capacity}, 원거리 {director.RangedTokens.InUse}/{director.RangedTokens.Capacity}", _label);
            }
            GUILayout.EndArea();

            DrawOwnedItems();

            // 레벨업 순간 (최대 체력 +5는 바로, 혈자리는 경맥도에서)
            if (_levelUpTimer > 0f) GUI.Label(new Rect(0, UiScale.Height * 0.22f, UiScale.Width, 50), "레벨 업! 최대 체력 +5 · 혈자리를 열 수 있어요", _big);

            string banner = run != null ? run.Banner : null;
            if (banner != null) GUI.Label(new Rect(0, UiScale.Height * 0.3f, UiScale.Width, 50), banner, _big);
            if (run != null && run.AtCounter)
                GUI.Label(new Rect(0, UiScale.Height * 0.62f, UiScale.Width, 50), "F 점소이에게 말 걸기", _big);
            // 튜토리얼: 조작 안내는 아래 가운데, 상황별 첫 안내는 오른쪽 위 한 줄
            if (run != null && !string.IsNullOrEmpty(run.Hint) && !run.IsPaused)
            {
                var hintRect = new Rect(UiScale.Width * 0.5f - 560f, UiScale.Height - 150f, 1120f, 52f);
                var oldHint = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.55f);
                GUI.DrawTexture(hintRect, Texture2D.whiteTexture);
                GUI.color = oldHint;
                GUI.Label(hintRect, $"<size=24>{run.Hint}</size>", _hintLabel);
            }
            if (run != null && !string.IsNullOrEmpty(run.HintNotice) && !run.IsPaused)
            {
                var noteRect = new Rect(UiScale.Width - 760f, 20f, 740f, 44f);
                var oldNote = GUI.color;
                GUI.color = new Color(0.1f, 0.08f, 0.05f, 0.7f);
                GUI.DrawTexture(noteRect, Texture2D.whiteTexture);
                GUI.color = oldNote;
                GUI.Label(noteRect, $"<size=18><color=#ffd27a>안내</color>  {run.HintNotice}</size>", _hintLabel);
            }
            if (run != null && run.InVillage && run.NearSpot != RoomRun.VillageSpot.None && (run.Book == null || !run.Book.IsOpen))
                GUI.Label(new Rect(0, UiScale.Height * 0.62f, UiScale.Width, 50), run.SpotPrompt(run.NearSpot), _big);
            if (run != null && run.AtGate && !run.IsForking)
                GUI.Label(new Rect(0, UiScale.Height * 0.62f, UiScale.Width, 50), $"F 다음 층으로 · 남은 전투 방 {run.Map.FightsLeft()} · 추적도 {run.Tracking}", _big);
            if (run != null && run.NearCrack)
                GUI.Label(new Rect(0, UiScale.Height * 0.62f, UiScale.Width, 50), "벽에 금이 가 있어요 · F 벽을 부수기", _big);

            if (player != null && player.IsDead && run == null) // 판 구조 씬은 결과 화면이 대신 떠요
                GUI.Label(new Rect(0, UiScale.Height * 0.4f, UiScale.Width, 60), "쓰러졌어요 · F5 키로 다시 시작", _big);

            // 녹림왕이 깨어나면 화면 아래쪽 가운데에 큰 체력바 (왼쪽 위 도움말과 겹치지 않게)
            EnemyBrain master = null; // 고수 (판 구조 결투장이든 샌드박스 - = 키든)
            if (director != null)
                foreach (var e in director.Enemies)
                    if (e is IMasterBrain && !e.IsDead)
                    {
                        master = e;
                        break;
                    }
            var masterInfo = master as IMasterBrain;
            if (master != null && masterInfo != null && master.IsAwake && master.Damageable != null)
            {
                float width = Mathf.Min(560f, UiScale.Width - 40f);
                var area = new Rect((UiScale.Width - width) * 0.5f, UiScale.Height - 64f, width, 18f);
                string state = masterInfo.IsTransitioning ? " · 전환 중 (무적)" : master.Statuses != null && master.Statuses.IsFrozen ? " · 빙결"
                    : master is IronFistBrain fist && fist.IsWallStunned ? " · 기절!" : "";
                GUI.Label(new Rect(area.x, area.y, area.width, 24f), $"고수 {master.DisplayName} · {masterInfo.Phase}페이즈{state}   {master.Damageable.Health:0}/{master.Damageable.MaxHealth:0}", _bossLabel);
                DrawBar(new Rect(area.x, area.y + 26f, area.width, 12f), master.Damageable.Health / Mathf.Max(1f, master.Damageable.MaxHealth), HealthColor);
            }
            var boss = arena != null ? arena.Boss : null;
            if (boss != null && !boss.IsDead && boss.IsAwake && boss.Damageable != null)
            {
                float width = Mathf.Min(560f, UiScale.Width - 40f);
                var area = new Rect((UiScale.Width - width) * 0.5f, UiScale.Height - 64f, width, 18f);
                string state = boss.IsTransitioning ? " · 전환 중 (무적)" : boss.Grove != null && boss.Grove.IsBurning ? " · 불타는 죽림" : "";
                var bossStatus = boss.Statuses;
                if (bossStatus != null && bossStatus.IsFrozen) state += " · 빙결";
                else if (bossStatus != null && bossStatus.FreezeImmunityLeft > 0f) state += $" · 서리 {bossStatus.FreezeImmunityLeft:0}초";
                GUI.Label(new Rect(area.x, area.y, area.width, 24f), $"녹림왕 · {boss.Phase}페이즈{state}   {boss.Damageable.Health:0}/{boss.Damageable.MaxHealth:0}", _bossLabel);
                DrawBar(new Rect(area.x, area.y + 26f, area.width, 12f), boss.Damageable.Health / Mathf.Max(1f, boss.Damageable.MaxHealth), HealthColor);
            }

            if (_camera == null) return;

            if (dummies != null)
            {
                foreach (var d in dummies)
                {
                    if (d == null) continue;
                    Vector3 sp = _camera.WorldToScreenPoint(d.transform.position + Vector3.up * 2.8f);
                    var g = UiScale.FromScreen(sp);
                    if (sp.z <= 0f) continue;
                    string state = d.IsMeasuring ? "측정 중" : "고정";
                    string text = $"{d.Label}\nDPS {d.Dps:0.0}  ({d.Elapsed:0.0}초, 합계 {d.Total}, {state})";
                    var rect = new Rect(g.x - 160, g.y - 40, 320, 44);
                    GUI.Label(rect, text, _dummy);
                    if (d.TryGetComponent(out StatusEffects dummyStatus)) DrawStatus(g.x, g.y + 18f, dummyStatus, true);
                }
            }

            if (director != null)
            {
                foreach (var e in director.Enemies)
                {
                    if (e == null || e.Damageable == null || !e.Damageable.Targetable) continue; // 숨은 청죽사는 안 보여요
                    Vector3 sp = _camera.WorldToScreenPoint(e.transform.position + Vector3.up * (e.Height + 0.4f));
                    var g = UiScale.FromScreen(sp);
                    if (sp.z <= 0f) continue;
                    var dmg = e.Damageable;
                    var bars = AThousandDawns.Core.GameSettings.Current.enemyBars; // 설정: 적 체력바
                    if (bars == AThousandDawns.Core.GameSettings.EnemyBars.Always || (bars == AThousandDawns.Core.GameSettings.EnemyBars.Damaged && dmg.Health < dmg.MaxHealth))
                        DrawBar(new Rect(g.x - 25, g.y, 50, 6), dmg.MaxHealth > 0f ? dmg.Health / dmg.MaxHealth : 0f, EnemyHealthColor);
                    if (e.IsElite || e.Rank == EnemyRank.Assassin) GUI.Label(new Rect(g.x - 90, g.y - 40, 180, 22), e.DisplayName, _statusLabel); // 정예: 이름 앞에 특성
                    DrawStatus(g.x, g.y, e.Statuses, false);
                }
            }

            // 피해 숫자: 화상은 작고 흐리게, 방전은 크고 굵게 (상태이상 탭 표시 규칙)
            foreach (var p in DamagePopups.Current)
            {
                Vector3 sp = _camera.WorldToScreenPoint(p.Position);
                var g = UiScale.FromScreen(sp);
                if (sp.z <= 0f) continue;
                Color c = p.Style switch
                {
                    PopupStyle.Player => new Color(1f, 0.35f, 0.3f),
                    PopupStyle.Dot => new Color(1f, 0.6f, 0.4f, 0.75f),
                    PopupStyle.Burst => new Color(1f, 0.9f, 0.3f),
                    _ => new Color(1f, 0.95f, 0.8f),
                };
                c.a *= 1f - p.Age / DamagePopups.Lifetime;
                var style = p.Style == PopupStyle.Dot ? _popupSmall : p.Style == PopupStyle.Burst ? _popupBig : _popup;
                style.normal.textColor = c;
                GUI.Label(new Rect(g.x - 50, g.y - 20, 100, 40), p.Amount.ToString(), style);
            }
        }

        // 가진 기물 (오른쪽 위): 이름 · 키워드 · 기 (최소–최대) · 상태와 레벨. 보관함의 기물은 효과가 없어요 (E 경맥도에 놓기).
        void DrawOwnedItems()
        {
            var items = player != null ? player.Items : null;
            if (items == null) return;
            float width = 400f;
            int count = items.Items.Count;
            int bag = items.BagCount;
            GUILayout.BeginArea(new Rect(UiScale.Width - width - 16f, 16f, width, 64f + Mathf.Max(1, count) * 21f + (bag > 0 ? 24f : 0f)), GUI.skin.box);
            string pending = arena != null && arena.PendingOffers > 0 && !arena.IsChoosing ? $" · 고르기 {arena.PendingOffers}개 남음" : "";
            string bagText = !items.HasBag ? "주머니 없음" : items.BagCapacity < 0 ? $"주머니 {bag}" : $"주머니 {bag}/{items.BagCapacity}";
            GUILayout.Label($"기물 {count}개 (경맥도 {count - bag} · {bagText}){pending}", _label);
            if (count == 0) GUILayout.Label(run != null ? "기물 길의 보상 방에서 3개 중 하나를 골라요 (I: 바로 고르기)" : "무리를 다 쓰러뜨리면 3개 중 하나를 골라요 (I: 바로 고르기)", _itemLine);
            if (bag > 0) GUILayout.Label("<color=#ffd27a>주머니의 기물은 효과가 없어요 · E로 경맥도에 놓기</color>", _itemSmall);
            foreach (var item in items.Items)
            {
                var def = item.Def;
                var state = PlayerItems.StateOf(item);
                int level = PlayerItems.LevelOf(item);
                string stateText = !item.OnBoard ? "<color=#9a9a9a>주머니</color>"
                    : state == QiState.Lacking ? "<color=#9a9a9a>기 부족</color>"
                    : state == QiState.Circulating ? $"운기 {level}레벨"
                    : $"<color=#ff6a5a>주화입마 {level}레벨</color>";
                string qi = item.OnBoard ? $"기 {item.Qi} " : "";
                GUILayout.Label($"<b>{def.name}</b> {Colored(def.keyword)} · {qi}({def.minQi}–{def.maxQi}) · {stateText}", _itemSmall);
            }
            GUILayout.EndArea();
        }

        // 층 지도 (오른쪽 아래): 지금 방은 붉게, 가 본 방은 밝게, 옆방은 흐리게. 시 = 시작, 보 = 보상 방, 관 = 관문, 기 = 기연 방 (? = 느껴지는 기연), ! = 아직 싸우는 중
        void DrawMinimap()
        {
            if (run == null || run.Map == null || run.CurrentPhase != RoomRun.Phase.Floor || run.IsForking) return;
            EnsureStyles();
            var map = run.Map;
            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
            foreach (var r in map.Rooms)
            {
                if (!r.Revealed) continue;
                minX = Mathf.Min(minX, r.Cell.x); maxX = Mathf.Max(maxX, r.Cell.x);
                minY = Mathf.Min(minY, r.Cell.y); maxY = Mathf.Max(maxY, r.Cell.y);
            }
            if (minX > maxX) return;
            const float cell = 34f, gap = 6f;
            float w = (maxX - minX + 1) * (cell + gap) - gap, h = (maxY - minY + 1) * (cell + gap) - gap;
            float ox = UiScale.Width - w - 24f, oy = UiScale.Height - h - 24f;
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(new Rect(ox - 8f, oy - 8f, w + 16f, h + 16f), Texture2D.whiteTexture);
            foreach (var r in map.Rooms)
            {
                if (!r.Revealed) continue;
                var rect = new Rect(ox + (r.Cell.x - minX) * (cell + gap), oy + (maxY - r.Cell.y) * (cell + gap), cell, cell);
                bool here = r == run.Room;
                GUI.color = here ? new Color(0.78f, 0.27f, 0.24f) : r.Visited ? new Color(0.85f, 0.85f, 0.8f, r.Cleared || !r.HasFight ? 0.55f : 0.9f) : new Color(1f, 1f, 1f, 0.15f);
                if (!here && r.Kind == FloorRoomKind.Hidden && !r.Found) GUI.color = new Color(0.75f, 0.6f, 1f, 0.25f); // 느껴지는 기연 방
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                GUI.color = Color.white;
                string mark = r.Kind == FloorRoomKind.Start ? "시" : r.Kind == FloorRoomKind.Reward ? "보" : r.Kind == FloorRoomKind.Gate ? "관" : r.Kind == FloorRoomKind.Event ? "사" : r.Kind == FloorRoomKind.Hidden ? (r.Found ? "기" : "?") : r.Visited && r.HasFight && !r.Cleared ? "!" : "";
                if (mark != "") GUI.Label(rect, mark, _statusLabel);
                // 살수가 있는 방은 붉은 테두리
                foreach (var hunter in run.Hunters)
                {
                    if (hunter.Dead || hunter.Engaged || hunter.Room != r) continue;
                    GUI.color = new Color(0.9f, 0.15f, 0.15f);
                    const float t = 4f;
                    GUI.DrawTexture(new Rect(rect.x - t, rect.y - t, rect.width + 2 * t, t), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(rect.x - t, rect.yMax, rect.width + 2 * t, t), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(rect.x - t, rect.y, t, rect.height), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(rect.xMax, rect.y, t, rect.height), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }
            }
            GUI.color = old;
        }

        // 일시정지 (Esc): 계속하기 · 설정 · 시작 화면으로
        void DrawPause()
        {
            if (run == null || !run.IsPaused) return;
            EnsureStyles();
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(0, 0, UiScale.Width, UiScale.Height), Texture2D.whiteTexture);
            GUI.color = old;
            if (run.PauseSettingsOpen)
            {
                if (run.PauseSettings.Draw(new Rect((UiScale.Width - 900f) * 0.5f, (UiScale.Height - 760f) * 0.5f, 900f, 760f))) run.SetPauseSettings(false);
                return;
            }
            if (run.PauseBookOpen)
            {
                if (run.PauseBook.Draw(new Rect((UiScale.Width - 1200f) * 0.5f, (UiScale.Height - 760f) * 0.5f, 1200f, 760f))) run.SetPauseBook(false);
                return;
            }
            float w = 420f, x = (UiScale.Width - w) * 0.5f, y = UiScale.Height * 0.32f;
            GUI.Label(new Rect(0, y - 70f, UiScale.Width, 50f), "일시정지", _big);
            string[] labels =
            {
                "계속하기",
                "설정",
                "무공 요결",
                run.ConfirmTitle ? "한 번 더 누르면 시작 화면으로 (이어하기는 마지막 저장부터)" : "시작 화면으로",
            };
            for (int i = 0; i < labels.Length; i++)
            {
                var rect = new Rect(x, y + i * 70f, w, 58f);
                bool on = run.PauseSelected == i;
                string text = on ? $"<b>▸ {labels[i]}</b>" : labels[i];
                if (GUI.Button(rect, text, _card)) run.ChoosePause((RoomRun.PauseItem)i);
            }
            GUI.Label(new Rect(0, y + labels.Length * 70f + 10f, UiScale.Width, 30f), "위아래 · Enter · Esc 돌아가기", _dummy);
        }

        // 객잔 진열대 (시간이 멈춰 있어요). 숫자 키 또는 클릭으로 사기, F·Esc 닫기
        void DrawShop()
        {
            if (run == null || !run.IsShopping || run.Shop == null) return;
            EnsureStyles();
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(new Rect(0, 0, UiScale.Width, UiScale.Height), Texture2D.whiteTexture);
            GUI.color = old;

            var slots = run.Shop.Slots;
            const float w = 760f, rowH = 64f;
            float h = 110f + slots.Count * (rowH + 6f);
            float x = (UiScale.Width - w) * 0.5f, y = (UiScale.Height - h) * 0.5f;
            GUI.Label(new Rect(0, y - 56f, UiScale.Width, 40f), $"객잔 진열대 · 은자 {run.Silver} · 숫자 키 또는 클릭 · F 닫기", _big);
            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                string blocker = run.Shop.Blocker(i, player, run.Silver);
                string price = s.Sold ? "<color=#9a9a9a>팔림</color>" : run.Silver < s.Price ? $"<color=#ff6a5a>{s.Price}</color>" : $"<color=#ffd27a>{s.Price}</color>";
                string body = s.Kind switch
                {
                    InnShop.SlotKind.Item => $"<b>{s.Item.name}</b>  {Colored(s.Item.keyword)} · {GradeColored(s.Item.grade)} · 기 {s.Item.minQi}–{s.Item.maxQi}\n<size=13>{s.Item.effectText}</size>",
                    InnShop.SlotKind.Potion => $"<b>금창약</b>  최대 체력 40% 회복 (R 누르고 있기)\n<size=13>지금 {player.Potions}/{player.PotionMax}개</size>",
                    InnShop.SlotKind.Part => $"<b>부품 {PlayerItems.PartLabel(s.Part)}</b>\n<size=13>경맥도 칸에 놓는 부품 (E · 부품 줄)</size>",
                    InnShop.SlotKind.Scroll => $"<b>비급 {(s.Skill == SkillKind.Quake ? "붕산권" : "죽림섬")}</b>  우클릭 초식이 바뀌어요 (지금 {player.SkillName})\n<size=13>{(s.Skill == SkillKind.Quake ? "땅을 내리쳐 원형 충격파, 맞은 적을 밀어냄" : "가까운 적에게 3번 순간이동하며 베기")}</size>",
                    InnShop.SlotKind.MoveDanjeon => $"<b>이혈</b>  단전 하나를 다른 칸으로 옮겨요\n<size=13>산 뒤 경맥도(E)에서 옮길 단전 → 새 칸{(player.Items.DanjeonMoves > 0 ? $" · 남은 이혈 {player.Items.DanjeonMoves}" : "")}</size>",
                    _ => "<b>숙박</b>  지금 바로 체력 30% 회복",
                };
                string text = $"<size=13>[{i + 1}]</size>  {body}\n<size=13>은자 {price}{(blocker != null && !s.Sold ? $"  <color=#9a9a9a>· {blocker}</color>" : "")}</size>";
                GUI.enabled = !s.Sold;
                if (GUI.Button(new Rect(x, y + i * (rowH + 6f), w, rowH), text, _card)) run.Buy(i);
                GUI.enabled = true;
            }
            if (!string.IsNullOrEmpty(run.ShopNotice))
                GUI.Label(new Rect(0, y + slots.Count * (rowH + 6f) + 10f, UiScale.Width, 40f), run.ShopNotice, _big);
        }

        // 이벤트 (시간이 멈춰 있어요): 제목 · 글 · 선택지. 🔒 선택지는 조건이 안 되면 흐리게 이유만.
        void DrawEvent()
        {
            if (run == null || !run.IsInEvent) return;
            EnsureStyles();
            var ev = run.CurrentEvent;
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(new Rect(0, 0, UiScale.Width, UiScale.Height), Texture2D.whiteTexture);
            GUI.color = old;

            const float w = 760f, rowH = 52f;
            int rows = ev.Result != null ? 1 : ev.Choices.Count;
            float h = 150f + rows * (rowH + 6f);
            float x = (UiScale.Width - w) * 0.5f, y = (UiScale.Height - h) * 0.5f;
            GUI.Label(new Rect(0, y - 56f, UiScale.Width, 40f), ev.Title, _big);
            GUI.Label(new Rect(x, y, w, 60f), $"<i>{ev.Text}</i>", _wrapLabel);
            float top = y + 70f;
            if (ev.Result != null)
            {
                if (GUI.Button(new Rect(x, top, w, rowH), $"{ev.Result}\n<size=13>[1] 닫기</size>", _card)) run.CloseEvent();
                return;
            }
            for (int i = 0; i < ev.Choices.Count; i++)
            {
                var c = ev.Choices[i];
                string text = c.Locked == null ? $"<size=13>[{i + 1}]</size>  {c.Label}" : $"<color=#8a8a8a><size=13>[{i + 1}]</size>  {c.Label}  · {c.Locked}</color>";
                GUI.enabled = c.Locked == null;
                if (GUI.Button(new Rect(x, top + i * (rowH + 6f), w, rowH), text, _card)) run.ChooseEvent(i);
                GUI.enabled = true;
            }
        }

        // 갈림길 카드 (시간이 멈춰 있어요). 1·2·3 또는 클릭
        void DrawFork()
        {
            if (run == null || !run.IsForking) return;
            EnsureStyles();
            var options = run.ForkOptions;
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(new Rect(0, 0, UiScale.Width, UiScale.Height), Texture2D.whiteTexture);
            GUI.color = old;

            const float cardW = 320f, cardH = 200f, gap = 20f;
            float total = options.Count * cardW + (options.Count - 1) * gap;
            float x = (UiScale.Width - total) * 0.5f, y = (UiScale.Height - cardH) * 0.5f;
            GUI.Label(new Rect(0, y - 56f, UiScale.Width, 40f), $"죽림 {run.Floor}층 갈림길 · 1 2 3 또는 클릭", _big);
            for (int i = 0; i < options.Count; i++)
            {
                var p = options[i];
                string qi = p.Dense ? "<color=#ff6a5a>기운 짙음</color>" : "<color=#bfe3c0>기운 옅음</color>";
                string text = $"<size=13>[{i + 1}]</size>  <b><size=21>{RoomRun.RewardLabel(p.Reward)}</size></b>\n{qi}\n\n{run.PathDetail(p)}";
                if (GUI.Button(new Rect(x + i * (cardW + gap), y, cardW, cardH), text, _card)) run.ChooseFork(i);
            }
        }

        // 버릴 기물 고르기: 경맥도도 주머니도 가득일 때 새 기물을 받으려면 가진 기물 하나를 버려요 (그 자리에 새 기물). Esc 포기
        void DrawDiscard()
        {
            var pending = arena != null ? arena.PendingItem : null;
            if (pending == null || player == null || player.Items == null) return;
            EnsureStyles();
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(new Rect(0, 0, UiScale.Width, UiScale.Height), Texture2D.whiteTexture);
            GUI.color = old;

            var items = player.Items;
            string why = items.HasBag ? $"경맥도도 주머니({items.BagCapacity}칸)도 가득해요" : "경맥도가 가득하고 주머니가 없어요 (회귀록에서 열어요)";
            float w = 760f, rowH = 40f;
            float x = (UiScale.Width - w) * 0.5f, y = UiScale.Height * 0.12f;
            GUI.Label(new Rect(0, y, UiScale.Width, 44f), $"새 기물: {pending.name}  {Colored(pending.keyword)} · {GradeColored(pending.grade)}", _big);
            GUI.Label(new Rect(0, y + 46f, UiScale.Width, 28f), $"<color=#cfcfcf>{why} · 버릴 기물을 고르면 그 자리에 놓여요 · Esc 포기</color>", _bossLabel);
            float row = y + 92f;
            for (int i = 0; i < items.Items.Count; i++)
            {
                var it = items.Items[i];
                string where = it.OnBoard ? $"경맥도 ({it.Cell.x + 1},{it.Cell.y + 1})" : "주머니";
                string text = $"<size=13>[{(i < 9 ? (i + 1).ToString() : "-")}]</size>  버리기 · <b>{it.Def.name}</b>  {Colored(it.Def.keyword)} · {GradeColored(it.Def.grade)} · {where}";
                if (GUI.Button(new Rect(x, row, w, rowH), text, _card)) arena.Discard(i);
                row += rowH + 4f;
            }
            if (GUI.Button(new Rect(x, row + 8f, w, rowH), "포기 · 새 기물을 받지 않아요 (Esc)", _card)) arena.DeclinePending();
        }

        // 기물 고르기 카드 3장 (시간이 멈춰 있어요). 1·2·3 또는 클릭, Esc 넘기기
        void DrawOffer()
        {
            if (arena == null || !arena.IsChoosing) return;
            EnsureStyles();
            var offer = arena.Offer;
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(0, 0, UiScale.Width, UiScale.Height), Texture2D.whiteTexture);
            GUI.color = old;

            const float cardW = 320f, cardH = 260f, gap = 20f;
            float total = offer.Count * cardW + (offer.Count - 1) * gap;
            float x = (UiScale.Width - total) * 0.5f, y = (UiScale.Height - cardH) * 0.5f;
            GUI.Label(new Rect(0, y - 56f, UiScale.Width, 40f), "기물 고르기 · 1 2 3 또는 클릭 · Esc 넘기기", _big);
            for (int i = 0; i < offer.Count; i++)
            {
                var def = offer[i];
                string aux = string.IsNullOrEmpty(def.aux) ? "" : $" · 보조 #{def.aux}";
                string text = $"<size=13>[{i + 1}]</size>  <b><size=19>{def.name}</size></b>\n" +
                              $"{Colored(def.keyword)} · {GradeColored(def.grade)}{aux}\n" +
                              $"기 {def.minQi}–{def.maxQi} · 경맥도 빈 칸에 왼쪽 위부터 놓여요 (가득 차면 주머니)\n\n" +
                              $"{def.effectText}\n\n" +
                              $"<color=#ff8a7a>주화입마 부작용: {def.sideText}</color>";
                if (GUI.Button(new Rect(x + i * (cardW + gap), y, cardW, cardH), text, _card)) arena.Choose(i);
            }
        }

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

        static string GradeColored(ItemGrade grade)
        {
            string color = grade switch
            {
                ItemGrade.Common => "#d8d8d8",
                ItemGrade.Uncommon => "#7fd67f",
                ItemGrade.Rare => "#6fa8ff",
                _ => "#ffb347",
            };
            return $"<color={color}>{ItemDefinition.GradeLabel(grade)}</color>";
        }

        // 상태이상 점 (상태이상 탭 표시): 화상 = 진한 주홍 점 (중첩 수만큼), 둔화 = 하늘색 점 3개, 감전 = 노란 점 5개, 빙결 = 글자.
        // below면 y 아래로, 아니면 y 위로 쌓아요.
        void DrawStatus(float centerX, float y, StatusEffects s, bool below)
        {
            if (s == null) return;
            float step = below ? 8f : -8f;
            float row = below ? y : y - 8f;
            if (s.BurnStacks > 0) { DrawDots(centerX, row, s.BurnStacks, s.BurnStacks, BurnDot); row += step; }
            if (s.SlowStacks > 0) { DrawDots(centerX, row, s.SlowStacks, s.SlowMax, SlowDot); row += step; }
            if (s.ShockStacks > 0) { DrawDots(centerX, row, s.ShockStacks, s.ShockMax, ShockDot); row += step; }
            if (s.IsFrozen) GUI.Label(new Rect(centerX - 40f, below ? row : row - 14f, 80f, 20f), "빙결", _statusLabel);
        }

        static void DrawDots(float centerX, float y, int filled, int slots, Color color)
        {
            const float size = 5f, gap = 2f;
            float x = centerX - (slots * (size + gap) - gap) * 0.5f;
            var old = GUI.color;
            for (int i = 0; i < slots; i++)
            {
                GUI.color = i < filled ? color : new Color(color.r, color.g, color.b, 0.3f);
                GUI.DrawTexture(new Rect(x + i * (size + gap), y, size, size), Texture2D.whiteTexture);
            }
            GUI.color = old;
        }

        static void DrawBar(Rect rect, float ratio, Color fill)
        {
            var old = GUI.color;
            GUI.color = BarBack;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = fill;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(ratio), rect.height), Texture2D.whiteTexture);
            GUI.color = old;
        }

        void EnsureStyles()
        {
            if (_label != null) return;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 16, richText = true };
            _label.normal.textColor = Color.white;
            _wrapLabel = new GUIStyle(_label) { wordWrap = true };
            _popup = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _dummy = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
            _dummy.normal.textColor = Color.black;
            _big = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _big.normal.textColor = Color.white;
            _bossLabel = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _bossLabel.normal.textColor = Color.white;
            _popupSmall = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
            _popupBig = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _statusLabel = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _hintLabel = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter, richText = true };
            _statusLabel.normal.textColor = SlowDot;
            _card = new GUIStyle(GUI.skin.button) { fontSize = 15, wordWrap = true, richText = true, alignment = TextAnchor.UpperLeft, padding = new RectOffset(14, 14, 12, 12) };
            _card.normal.textColor = _card.hover.textColor = Color.white;
            _itemLine = new GUIStyle(_label) { fontSize = 15, richText = true, wordWrap = true };
            _itemSmall = new GUIStyle(_label) { fontSize = 14, richText = true, wordWrap = false, margin = new RectOffset(4, 4, 0, 0), padding = new RectOffset(0, 0, 1, 1) };
        }
    }
}

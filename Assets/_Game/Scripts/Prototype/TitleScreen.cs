using System.IO;
using AThousandDawns.Core;
using AThousandDawns.Items;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace AThousandDawns.Prototype
{
    // 시작 화면 (기획 대화 시작 화면안, IMGUI). 배경·붓글씨 제목·낙관·붓 밑줄·프롤로그는 아트 세션 그림 (Art/UI/Title, 없으면 글자로).
    //   배경은 진행에 따라 (죽림 → 폐사찰 → 빙궁 → 천마총 → 노을) · 제목은 가로 68% 지점 세로쓰기 · 왼쪽 아래: 버튼 4개 · 오른쪽 아래: 버전
    //   이어하기 (저장이 있을 때, 처음에 선택) → 판 중간 저장이 있으면 그 방부터, 없으면 마을
    //   시작하기 (저장이 없을 때) → 프롤로그 (글 4장, 넘기기 가능) → 첫 판
    //   설정 (SettingsPanel, 일시정지 메뉴와 같은 창) · 크레딧 · 종료 (확인 없이)
    // 마우스·키보드(위아래·Enter·Esc)·게임패드(십자·A·B) 모두 돼요.
    public sealed class TitleScreen : MonoBehaviour
    {
        public const string RunScene = "Prototype_Run";

        public enum View { Menu, Prologue, Settings, Credits }
        public enum Item { Continue, Settings, Credits, Quit }

        static readonly Color Paper = new Color32(0xEF, 0xE6, 0xD2, 0xFF); // 색감 시트 마을 바탕
        static readonly Color Ink = new Color32(0x2B, 0x28, 0x25, 0xFF);
        static readonly Color Faint = new Color32(0x8A, 0x82, 0x78, 0xFF);
        static readonly Color Seal = new Color32(0xC8, 0x46, 0x3C, 0xFF);   // 매화 홍 (낙관)

        // 프롤로그 (기획 대화 스토리 배경 초안). 그림은 장마다 TitleArt/Prologue_1~4.
        public static readonly string[] Prologue =
        {
            "백 년 전, 마교 교주 천마가 천마총 깊은 곳에 봉인되었다.",
            "마교는 그를 되살릴 의식을 준비한다.\n의식에는 흩어진 천마의 내단 조각이 모두 필요하다.",
            "마교가 마을을 덮친 밤, 내 단전은 부서졌다.\n그리고 그 빈자리에, 마지막 조각이 깃들었다.",
            "마교는 마지막 조각을 품은 나를 쫓는다.\n그들보다 먼저 천마총에 닿아, 이 모든 것을 끝내야 한다.",
        };

        public View Current { get; private set; } = View.Menu;
        public int Selected { get; private set; }
        public int ProloguePage { get; private set; }
        public bool HasSave { get; private set; }

        [SerializeField] AudioClip music; // 시작 화면 BGM (VARCO Sound, Audio/BGM/Title.mp3)

        MetaSave _meta;
        int _underlineFor = -1;   // 붓 밑줄을 긋기 시작한 메뉴
        float _underlineAt;
        public SettingsPanel Settings { get; } = new SettingsPanel();
        [System.NonSerialized] GUIStyle _titleBig, _titleSmall, _subtitle, _hint, _menu, _menuOn, _info, _small, _body, _sealText;

        void Awake()
        {
            _run = RunSave.Load(); // 판 중간 저장 (있으면 이어하기는 그 방부터)
            HasSave = File.Exists(MetaSave.FilePath) || _run != null;
            _meta = MetaSave.Load();
            Selected = 0;
            Time.timeScale = 1f;
            PlayMusic();
        }

        // 음량 설정이 생기면 여기서 곱해요
        void PlayMusic()
        {
            if (music == null) return;
            if (FindAnyObjectByType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
            var source = gameObject.AddComponent<AudioSource>();
            source.clip = music;
            source.loop = true;
            source.Play();
        }

        public string ItemLabel(Item item) => item switch
        {
            Item.Continue => HasSave ? "이어하기" : "시작하기",
            Item.Settings => "설정",
            Item.Credits => "크레딧",
            _ => "종료",
        };

        // 이어하기 옆 진행 정보: 판 중간 저장이 있으면 위치, 없으면 회차와 기억
        public string ProgressText => !HasSave ? ""
            : _run != null ? $"제{_meta.runs}회 회귀 · {_run.WhereText}"
            : $"제{_meta.runs}회 회귀 · 회귀의 기억 {_meta.memory}{(_meta.bestFloors > 0 ? $" · 가장 깊이 {_meta.bestFloors}층" : "")}";

        public bool HasRunSave => _run != null;
        RunSave _run;

        void Update()
        {
            var k = Keyboard.current;
            var pad = Gamepad.current;
            bool up = (k != null && (k.upArrowKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame)) || (pad != null && pad.dpad.up.wasPressedThisFrame);
            bool down = (k != null && (k.downArrowKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame)) || (pad != null && pad.dpad.down.wasPressedThisFrame);
            bool ok = (k != null && (k.enterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame)) || (pad != null && pad.buttonSouth.wasPressedThisFrame);
            bool back = (k != null && k.escapeKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame);

            switch (Current)
            {
                case View.Menu:
                    if (up) { Selected = (Selected + 3) % 4; Sfx.Play(SfxId.UiMove); }
                    if (down) { Selected = (Selected + 1) % 4; Sfx.Play(SfxId.UiMove); }
                    if (ok) Choose((Item)Selected);
                    break;
                case View.Prologue:
                    if (ok || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)) NextPage();
                    if (back) StartRun(); // 넘기기
                    break;
                case View.Settings:
                    if (Settings.TickKeys()) Current = View.Menu;
                    break;
                default:
                    if (back || ok) Current = View.Menu;
                    break;
            }
        }

        public void Choose(Item item)
        {
            Sfx.Play(SfxId.UiConfirm);
            switch (item)
            {
                case Item.Continue:
                    if (HasSave) StartRun();
                    else
                    {
                        Current = View.Prologue;
                        ProloguePage = 0;
                    }
                    break;
                case Item.Settings: Current = View.Settings; break;
                case Item.Credits: Current = View.Credits; break;
                default:
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                    break;
            }
        }

        public void NextPage()
        {
            ProloguePage++;
            if (ProloguePage >= Prologue.Length) StartRun();
        }

        // 판 구조 씬으로 (회차가 있으면 마을, 없으면 첫 판)
        public void StartRun()
        {
            RoomRun.SkipVillage = false;
            RoomRun.ResumeRun = _run != null; // 이어하기: 저장한 판부터 (없으면 마을)
            SceneManager.LoadScene(RunScene);
        }

        void OnGUI()
        {
            UiScale.Apply();
            EnsureStyles();
            float w = UiScale.Width, h = UiScale.Height;
            var old = GUI.color;
            GUI.color = Paper;
            GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            GUI.color = old;

            if (Current == View.Prologue)
            {
                DrawPrologue(w, h);
                return;
            }

            // 배경 그림 (진행에 따라) · 날리는 잎 · 붓글씨 제목. 그림이 없으면 예전처럼 글자로
            var bg = ItemIcons.TitleArt(BackgroundName(_meta));
            if (bg != null)
            {
                GUI.DrawTexture(new Rect(0, 0, w, h), bg, ScaleMode.ScaleAndCrop);
                DrawLeaves(w, h, _meta.bossKills == 2 && _meta.finalBossKills == 0); // 설산 빙궁은 눈
            }
            var title = ItemIcons.TitleArt("Title");
            if (title != null) DrawArtTitle(w, h, title);
            else DrawTextTitle(w, old);

            if (Current == View.Menu) DrawMenu(h);
            else if (Current == View.Settings) { if (Settings.Draw(new Rect(80f, (h - 760f) * 0.5f, 900f, 760f))) Current = View.Menu; }
            else DrawPanel(w, h);

            GUI.Label(new Rect(w - 260f, h - 50f, 230f, 30f), $"v{Application.version} 시험판", _small);
        }

        // 시작 화면 배경: 지역 보스를 쓰러뜨린 만큼 다음 지역 풍경, 첫 천마 격파 뒤에는 붉은 노을
        public static string BackgroundName(MetaSave meta) =>
            meta.finalBossKills > 0 ? "Bg_TombSunset"
            : meta.bossKills >= 3 ? "Bg_Tomb"
            : meta.bossKills == 2 ? "Bg_IcePalace"
            : meta.bossKills == 1 ? "Bg_Temple"
            : "Bg_Bamboo";

        // 붓글씨 제목 (세로): 화면 높이의 45%, 가로 68% 지점 (오른쪽 절벽의 주인공과 안 겹치게). 옆에 한글 · 아래 낙관과 영문
        void DrawArtTitle(float w, float h, Texture2D title)
        {
            float th = h * 0.45f, tw = th * title.width / title.height;
            float cx = w * 0.68f, ty = h * 0.06f;
            var rect = new Rect(cx - tw * 0.5f, ty, tw, th);
            GUI.DrawTexture(rect, title, ScaleMode.ScaleToFit);
            const string small = "일조천회";
            for (int i = 0; i < small.Length; i++) GUI.Label(new Rect(rect.x - 52f, ty + 16f + i * 40f, 48f, 40f), small[i].ToString(), _titleSmall);
            var seal = ItemIcons.TitleArt("Seal");
            if (seal != null) GUI.DrawTexture(new Rect(rect.xMax - 10f, rect.yMax - 70f, 56f, 56f), seal, ScaleMode.ScaleToFit);
            GUI.Label(new Rect(cx - 200f, rect.yMax + 12f, 400f, 30f), "A THOUSAND DAWNS", _subtitle);
            GUI.Label(new Rect(cx - 200f, rect.yMax + 44f, 400f, 30f), "같은 아침을, 천 번.", _small);
        }

        // 날리는 대나무 잎 (설산이면 흰 눈): 시간으로만 위치를 정해 상태가 없어요
        void DrawLeaves(float w, float h, bool snow)
        {
            var leaf = ItemIcons.TitleArt("BambooLeaf");
            if (!snow && leaf == null) return;
            float t = Time.unscaledTime;
            var oldMatrix = GUI.matrix;
            var oldColor = GUI.color;
            for (int i = 0; i < 9; i++)
            {
                float seed = i * 0.618f % 1f, speed = 0.05f + seed * 0.04f;
                float p = (t * speed + seed) % 1f; // 0 → 1: 오른쪽 위에서 왼쪽 아래로
                float x = w * (1.05f - p * 1.1f) + Mathf.Sin(t * 0.9f + i) * 40f;
                float y = h * (-0.05f + p * 1.1f) + i * 37f % (h * 0.3f);
                if (snow)
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.8f);
                    GUI.DrawTexture(new Rect(x, y, 6f, 6f), Texture2D.whiteTexture);
                    continue;
                }
                float size = 22f + seed * 14f;
                GUIUtility.RotateAroundPivot(t * (40f + seed * 60f) + i * 40f, new Vector2(x, y));
                GUI.color = new Color(1f, 1f, 1f, 0.75f);
                GUI.DrawTexture(new Rect(x - size * 0.18f, y - size * 0.5f, size * 0.35f, size), leaf, ScaleMode.ScaleToFit);
                GUI.matrix = oldMatrix;
            }
            GUI.color = oldColor;
            GUI.matrix = oldMatrix;
        }

        // 그림이 없을 때: 오른쪽 위 세로쓰기 + 낙관 + 영문
        void DrawTextTitle(float w, Color old)
        {
            // 한 글자씩 칸을 나눠 놓아요 (줄 간격에 따라 마지막 글자가 잘리지 않게)
            float tx = w - 240f, ty = 70f;
            const string big = "一朝千回", small = "일조천회";
            for (int i = 0; i < big.Length; i++) GUI.Label(new Rect(tx, ty + i * 140f, 140f, 140f), big[i].ToString(), _titleBig);
            for (int i = 0; i < small.Length; i++) GUI.Label(new Rect(tx - 64f, ty + 12f + i * 40f, 48f, 40f), small[i].ToString(), _titleSmall);
            var seal = new Rect(tx + 38f, ty + 4 * 140f + 16f, 64f, 64f);
            GUI.color = Seal;
            GUI.DrawTexture(seal, Texture2D.whiteTexture);
            GUI.color = old;
            GUI.Label(seal, "回", _sealText);
            GUI.Label(new Rect(tx - 130f, ty + 660f, 400f, 30f), "A THOUSAND DAWNS", _subtitle); // 제목 줄 가운데
            GUI.Label(new Rect(tx - 130f, ty + 692f, 400f, 30f), "같은 아침을, 천 번.", _small);
        }

        void DrawMenu(float h)
        {
            float x = 120f, y = h - 360f;
            for (int i = 0; i < 4; i++)
            {
                var item = (Item)i;
                var rect = new Rect(x, y + i * 64f, 320f, 56f);
                if (rect.Contains(Event.current.mousePosition)) Selected = i; // 마우스를 올리면 선택
                bool on = Selected == i;
                GUI.Label(rect, (on ? "▸ " : "   ") + ItemLabel(item), on ? _menuOn : _menu);
                if (on)
                {
                    if (_underlineFor != i)
                    {
                        _underlineFor = i;
                        _underlineAt = Time.unscaledTime;
                    }
                    var brush = ItemIcons.TitleArt("BrushUnderline");
                    if (brush != null) // 붓 밑줄: 왼쪽부터 0.25초 동안 쓱 그어요
                    {
                        float t = Mathf.Clamp01((Time.unscaledTime - _underlineAt) / 0.25f);
                        var line = new Rect(x + 30f, rect.yMax - 14f, 220f * t, 30f);
                        GUI.DrawTextureWithTexCoords(line, brush, new Rect(0f, 0f, t, 1f));
                    }
                    else
                    {
                        var old = GUI.color;
                        GUI.color = Seal;
                        GUI.DrawTexture(new Rect(x + 34f, rect.yMax - 8f, 150f, 3f), Texture2D.whiteTexture);
                        GUI.color = old;
                    }
                }
                if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) Choose(item);
                if (i == 0 && HasSave) GUI.Label(new Rect(x + 260f, rect.y + 12f, 700f, 40f), ProgressText, _info);
            }
            GUI.Label(new Rect(x, y + 4 * 64f + 10f, 700f, 30f), "위아래 · Enter (패드: 십자 · A)", _hint);
        }

        void DrawPanel(float w, float h)
        {
            const string title = "크레딧"; // 설정은 SettingsPanel이 그려요
            const string body = "일조천회 (一朝千回) · A THOUSAND DAWNS\n\n기획·개발  (이름)\n\n시험판입니다. 그림·소리는 아직 임시예요.";
            var area = new Rect(120f, h - 460f, 820f, 380f);
            GUI.Label(new Rect(area.x, area.y, area.width, 50f), title, _menuOn);
            GUI.Label(new Rect(area.x, area.y + 70f, area.width, 240f), body, _body);
            GUI.Label(new Rect(area.x, area.yMax - 30f, area.width, 30f), "Esc · Enter 돌아가기", _hint);
            if (GUI.Button(area, GUIContent.none, GUIStyle.none)) Current = View.Menu;
        }

        // 프롤로그: 장마다 그림을 화면 가득, 글은 아래쪽 한지 띠에 (그림이 없으면 가운데에 글만)
        void DrawPrologue(float w, float h)
        {
            int page = Mathf.Clamp(ProloguePage, 0, Prologue.Length - 1);
            var art = ItemIcons.TitleArt($"Prologue_{page + 1}");
            if (art == null)
            {
                GUI.Label(new Rect(0, h * 0.5f - 80f, w, 160f), Prologue[page], _body);
                GUI.Label(new Rect(0, h - 90f, w, 30f), $"{page + 1} / {Prologue.Length}   ·   클릭·Enter 다음   ·   Esc 넘기기", _small);
                return;
            }
            GUI.DrawTexture(new Rect(0, 0, w, h), art, ScaleMode.ScaleAndCrop);
            var old = GUI.color;
            GUI.color = new Color(Paper.r, Paper.g, Paper.b, 0.88f);
            GUI.DrawTexture(new Rect(0, h - 250f, w, 250f), Texture2D.whiteTexture);
            GUI.color = old;
            GUI.Label(new Rect(0, h - 240f, w, 160f), Prologue[page], _body);
            GUI.Label(new Rect(0, h - 70f, w, 30f), $"{page + 1} / {Prologue.Length}   ·   클릭·Enter 다음   ·   Esc 넘기기", _small);
        }

        void EnsureStyles()
        {
            if (_menu != null) return;
            _titleBig = new GUIStyle(GUI.skin.label) { fontSize = 120, alignment = TextAnchor.MiddleCenter, normal = { textColor = Ink } };
            _titleSmall = new GUIStyle(GUI.skin.label) { fontSize = 30, alignment = TextAnchor.MiddleCenter, normal = { textColor = Faint } };
            _sealText = new GUIStyle(GUI.skin.label) { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Paper } };
            _menu = new GUIStyle(GUI.skin.label) { fontSize = 34, alignment = TextAnchor.MiddleLeft, normal = { textColor = Faint } };
            _menuOn = new GUIStyle(_menu) { fontStyle = FontStyle.Bold, normal = { textColor = Ink } };
            _info = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleLeft, normal = { textColor = Ink } };
            _subtitle = new GUIStyle(_info) { alignment = TextAnchor.MiddleCenter };
            _hint = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleLeft, normal = { textColor = Faint } };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter, normal = { textColor = Faint } };
            _body = new GUIStyle(GUI.skin.label) { fontSize = 34, alignment = TextAnchor.MiddleCenter, wordWrap = true, richText = true, normal = { textColor = Ink } };
            // 마우스를 올리거나 누를 때도 같은 색 (기본 스킨은 흰색으로 바뀌어 한지 바탕에서 안 보여요)
            foreach (var s in new[] { _titleBig, _titleSmall, _subtitle, _hint, _menu, _menuOn, _info, _small, _body, _sealText })
                s.hover.textColor = s.active.textColor = s.focused.textColor = s.onNormal.textColor = s.onHover.textColor = s.normal.textColor;
        }
    }
}

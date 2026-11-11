using System;
using System.IO;
using UnityEngine;

namespace AThousandDawns.Core
{
    // 설정 (기획 대화 설정창 "첫 버전 필수"). 진행 저장(meta.json)과 따로 settings.json에 둬요.
    // 진행을 초기화해도 설정은 남아요.
    [Serializable]
    public sealed class GameSettings
    {
        public enum AimMode { Mouse, Move }          // 조준: 마우스 방향 / 이동 방향
        public enum DashDir { Move, Aim }            // 대시: 이동 방향 / 조준 방향
        public enum EnemyBars { Always, Damaged, Off } // 적 체력바: 항상 / 피격 시만 / 끄기
        public enum QiNumbers { Always, Hover }      // 경맥도 기 숫자: 항상 / 마우스를 올렸을 때만
        public enum AspectMode { Auto, Wide16x9, Wide16x10, Ultra21x9 } // 화면 비율: 창에 맞춤 / 고정 (남는 곳은 검은 띠)

        // 소리 (0~100): 전체는 AudioListener, 음악은 RunMusic·시작 화면, 효과음은 Sfx가 곱해요
        public int masterVolume = 100, musicVolume = 80, sfxVolume = 100;
        public float MasterScale => Mathf.Clamp01(masterVolume / 100f);
        public float MusicScale => Mathf.Clamp01(musicVolume / 100f);
        public float SfxScale => Mathf.Clamp01(sfxVolume / 100f);
        // 화면 (0이면 지금 화면 그대로)
        public int width, height;
        public FullScreenMode screenMode = FullScreenMode.FullScreenWindow;
        public AspectMode aspect = AspectMode.Auto;
        // 조작
        public AimMode aim = AimMode.Mouse;
        public DashDir dash = DashDir.Move;
        // 게임플레이
        public bool damageNumbers = true;
        public bool damageNumbersSum;       // 피해 숫자 합산 표시: 같은 자리에 짧은 시간 안에 들어간 피해를 하나로 모아요
        public EnemyBars enemyBars = EnemyBars.Damaged;
        public QiNumbers qiNumbers = QiNumbers.Always;
        public bool confirmDialogs = true;  // 되돌릴 수 없는 행동 (대장간 녹이기, 단전 위치, 이혈) 전에 묻기
        // 접근성
        public int screenShake = 100;       // 0~100 (화면 흔들림이 생기면 곱해요)
        // 조작 키 (KeyBindings.Bind 순서, Input System 경로). 비어 있으면 기본값
        public string[] keys;

        public static string PathOverride; // 테스트에서 진짜 설정 파일을 건드리지 않게
        public static string FilePath => PathOverride ?? Path.Combine(Application.persistentDataPath, "settings.json");

        static GameSettings _current;
        public static GameSettings Current => _current ??= Load();

        public static event Action Changed;

        public static GameSettings Load()
        {
            try
            {
                if (File.Exists(FilePath)) return JsonUtility.FromJson<GameSettings>(File.ReadAllText(FilePath)) ?? new GameSettings();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GameSettings] 설정 파일을 읽지 못해 기본값으로 시작해요: {e.Message}");
            }
            return new GameSettings();
        }

        // 테스트·경로가 바뀌었을 때 다시 읽어요
        public static void Reload() => _current = Load();

        public void Save()
        {
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(this, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GameSettings] 설정을 저장하지 못했어요: {e.Message}");
            }
            Changed?.Invoke();
        }

        // 해상도·화면 모드를 화면에 적용 (에디터에서는 게임 뷰 크기를 못 바꿔서 화면 모드만 기록돼요)
        public void ApplyDisplay()
        {
            int w = width > 0 ? width : Screen.width, h = height > 0 ? height : Screen.height;
            if (w != Screen.width || h != Screen.height || Screen.fullScreenMode != screenMode)
                Screen.SetResolution(w, h, screenMode);
        }

        // 고정 비율 (가로 ÷ 세로). 자동이면 0
        public static float Ratio(AspectMode mode) => mode switch
        {
            AspectMode.Wide16x9 => 16f / 9f,
            AspectMode.Wide16x10 => 16f / 10f,
            AspectMode.Ultra21x9 => 21f / 9f,
            _ => 0f,
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void ApplyOnStart()
        {
            AudioListener.volume = Current.MasterScale; // 전체 음량 (저장할 때마다 다시)
            Changed += () => AudioListener.volume = Current.MasterScale;
            if (!Application.isEditor) Current.ApplyDisplay();
        }
    }
}

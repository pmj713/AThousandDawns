using UnityEngine;

namespace AThousandDawns.Art
{
    // 수묵 강도 조절 (한 곳에서): Assets/_Game/Art/Resources/InkLook.asset을 고르면 Inspector에 슬라이더 여섯이 나와요.
    // 메뉴 AThousandDawns → 아트 → 수묵 강도 조절로 바로 열 수 있어요. 플레이 중에 움직여도 바로 바뀌고, 값은 파일에 남아요.
    //   캐릭터·적 수묵 강도: 1 = 먹 3단계·가장자리 먹 그대로, 0 = 원래 색에 부드러운 명암만 (붓 외곽선은 남아요)
    //   캐릭터·적 최소 색:   지역이 깊어져 색이 빠져도 캐릭터·적·소품에 남길 색 (0 = 지역 따라 다 빠짐)
    //   캐릭터·적 외곽선:    1 = 붓 외곽선 그대로, 0 = 없음 (작게 보이는 캐릭터가 먹 덩어리로 뭉개지면 줄여요)
    //   캐릭터·적 밝기:      어두운 곳을 밝혀 짙은 옷 주름·얼굴이 보이게
    //   배경 옅게:           0 = 그대로, 1 = 배경이 한지색에 가깝게 (캐릭터가 도드라져요)
    //   화면 종이 효과:      1 = 한지 결·가장자리 바램·색 빼기 그대로, 0 = 끔
    // 셰이더(InkCharacter · InkTriplanar · InkPaperScreen)가 읽는 전역 값으로 넘겨요.
    [CreateAssetMenu(menuName = "AThousandDawns/수묵 강도 설정", fileName = "InkLook")]
    public class InkLookSettings : ScriptableObject
    {
        [Header("캐릭터·적")]
        [Tooltip("1 = 수묵 그대로, 0 = 원래 색과 부드러운 명암")]
        [Range(0f, 1f)] public float characterInk = 0.5f;
        [Tooltip("지역이 바래도 남길 최소 색 (0~1)")]
        [Range(0f, 1f)] public float characterColor = 0.5f;
        [Tooltip("1 = 붓 외곽선 그대로(3px), 0 = 없음. 작은 캐릭터가 먹 덩어리로 뭉개지면 줄여요")]
        [Range(0f, 1f)] public float characterOutline = 0.5f;
        [Tooltip("0 = 그대로, 1 = 어두운 곳을 크게 밝혀요 (짙은 옷 주름·얼굴이 보이게)")]
        [Range(0f, 1f)] public float characterBrightness = 0.3f;

        [Header("배경")]
        [Tooltip("0 = 그대로, 1 = 한지색에 가깝게")]
        [Range(0f, 1f)] public float backgroundFade = 0.2f;

        [Header("화면")]
        [Tooltip("1 = 한지 결·가장자리 바램·색 빼기 그대로, 0 = 끔")]
        [Range(0f, 1f)] public float screenPaper = 0.6f;

        static readonly int CharSoftenId = Shader.PropertyToID("_InkCharSoften");
        static readonly int CharColorFloorId = Shader.PropertyToID("_InkCharColorFloor");
        static readonly int EnvFadeId = Shader.PropertyToID("_InkEnvFade");
        static readonly int ScreenSoftenId = Shader.PropertyToID("_InkScreenSoften");
        static readonly int OutlineThinId = Shader.PropertyToID("_InkCharOutlineThin");
        static readonly int LiftId = Shader.PropertyToID("_InkCharLift");

        public const string ResourcePath = "InkLook";

        public void Apply()
        {
            Shader.SetGlobalFloat(CharSoftenId, 1f - characterInk);
            Shader.SetGlobalFloat(CharColorFloorId, characterColor);
            Shader.SetGlobalFloat(EnvFadeId, backgroundFade);
            Shader.SetGlobalFloat(ScreenSoftenId, 1f - screenPaper);
            Shader.SetGlobalFloat(OutlineThinId, 1f - characterOutline);
            Shader.SetGlobalFloat(LiftId, characterBrightness);
        }

        void OnEnable() => Apply();
        void OnValidate() => Apply(); // Inspector에서 움직이면 바로

        // 게임을 켜면 설정 파일 값을 넣어요 (파일이 없으면 기본값)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ApplyOnStart()
        {
            var settings = Resources.Load<InkLookSettings>(ResourcePath);
            if (settings == null) settings = CreateInstance<InkLookSettings>();
            settings.Apply();
        }
    }
}

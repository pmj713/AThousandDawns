using System.Reflection;
using AThousandDawns.Prototype;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 업보 등불: 마을 사당 앞 붉은 등불 여섯. 고른 업보 단계(RoomRun.Karma)만큼 불이 켜지고,
    // 열렸지만 고르지 않은 단계는 붉은 등만 어둑하게, 아직 열리지 않은 단계(RoomRun.KarmaMax 너머)는 검게 식어 있어요.
    // 게임 코드는 건드리지 않고 값을 읽기만 해요 (업보 코드가 들어오기 전에도 컴파일되게 이름으로 찾아요)
    public class InkKarmaLanterns : MonoBehaviour
    {
        [SerializeField] Renderer[] shades = new Renderer[0];
        [SerializeField] GameObject[] flames = new GameObject[0];

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly Color Lit = new Color(1f, 0.42f, 0.28f, 1f);
        static readonly Color Dim = new Color(0.5f, 0.16f, 0.13f, 1f);
        static readonly Color Locked = new Color(0.2f, 0.18f, 0.19f, 1f);

        static readonly PropertyInfo KarmaProp = typeof(RoomRun).GetProperty("Karma", BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
        static readonly PropertyInfo KarmaMaxProp = typeof(RoomRun).GetProperty("KarmaMax", BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);

        RoomRun _run;
        int _karma = -1, _max = -1;
        MaterialPropertyBlock _block;

        public void Setup(Renderer[] lanternShades, GameObject[] lanternFlames)
        {
            shades = lanternShades;
            flames = lanternFlames;
        }

        void Start() => Show(0, 0);

        void Update()
        {
            if (KarmaProp == null) return;
            if (_run == null) _run = FindFirstObjectByType<RoomRun>();
            int karma = Read(KarmaProp), max = KarmaMaxProp != null ? Read(KarmaMaxProp) : 6;
            if (karma != _karma || max != _max) Show(karma, max);
        }

        int Read(PropertyInfo prop)
        {
            bool isStatic = prop.GetGetMethod().IsStatic;
            if (!isStatic && _run == null) return 0;
            return System.Convert.ToInt32(prop.GetValue(isStatic ? null : _run));
        }

        void Show(int karma, int max)
        {
            _karma = karma;
            _max = max;
            _block ??= new MaterialPropertyBlock();
            for (int i = 0; i < shades.Length; i++)
            {
                bool lit = i < karma;
                if (shades[i] != null)
                {
                    shades[i].GetPropertyBlock(_block);
                    _block.SetColor(BaseColorId, lit ? Lit : i < max ? Dim : Locked);
                    shades[i].SetPropertyBlock(_block);
                }
                if (i < flames.Length && flames[i] != null) flames[i].SetActive(lit);
            }
        }
    }
}

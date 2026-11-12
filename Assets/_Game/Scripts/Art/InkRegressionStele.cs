using AThousandDawns.Core;
using AThousandDawns.Prototype;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 회귀 비석 (마을 매화나무 곁): 회차(MetaSave.runs)만큼 正자 셈 획을 새기고, 천마 격파(finalBossKills)만큼 붉은 낙관을 찍어요.
    // 획은 50(正 열 자)까지, 낙관은 자리 수까지만 보이고, 넘치면 머릿돌·낙관 아래에 숫자를 새겨요.
    // 마을에 들어올 때마다 한 번 읽어요 (씬을 열 때도). 획·낙관은 씬 빌더(PrototypeSceneBuilder.InkStele)가 미리 만들어 꺼 둬요
    public class InkRegressionStele : MonoBehaviour
    {
        [SerializeField] GameObject[] strokes = new GameObject[0];   // 正 획 순서대로 (한 자에 다섯 획)
        [SerializeField] GameObject[] seals = new GameObject[0];     // 붉은 낙관 자리
        [SerializeField] TextMesh runsLabel;                         // 머릿돌 회차 숫자 (획이 넘칠 때)
        [SerializeField] TextMesh sealLabel;                         // 낙관 아래 격파 수 (낙관이 넘칠 때)

        RoomRun _run;
        bool _wasInVillage;

        public void Setup(GameObject[] strokeMarks, GameObject[] sealMarks, TextMesh runs, TextMesh sealCount)
        {
            strokes = strokeMarks; seals = sealMarks; runsLabel = runs; sealLabel = sealCount;
        }

        void OnEnable() => Refresh();

        void Update()
        {
            if (_run == null) _run = FindFirstObjectByType<RoomRun>();
            bool inVillage = _run != null && _run.InVillage;
            if (inVillage && !_wasInVillage) Refresh();
            _wasInVillage = inVillage;
        }

        public void Refresh()
        {
            var meta = MetaSave.Load();
            Show(meta.runs, meta.finalBossKills);
        }

        public void Show(int runs, int kills)
        {
            for (int i = 0; i < strokes.Length; i++)
                if (strokes[i] != null) strokes[i].SetActive(i < runs);
            for (int i = 0; i < seals.Length; i++)
                if (seals[i] != null) seals[i].SetActive(i < kills);
            if (runsLabel != null)
            {
                runsLabel.gameObject.SetActive(runs > strokes.Length);
                runsLabel.text = runs.ToString();
            }
            if (sealLabel != null)
            {
                sealLabel.gameObject.SetActive(kills > seals.Length);
                sealLabel.text = kills.ToString();
            }
        }
    }
}

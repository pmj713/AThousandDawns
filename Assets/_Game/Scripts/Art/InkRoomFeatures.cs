using AThousandDawns.Prototype;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 방 구성(RoomRun.RoomFeatures)의 기본판 상자에 수묵 모델을 입혀요: 돌기둥 · 석불 · 무너진 담.
    // 상자의 충돌체는 그대로 두고 그림(Renderer)만 꺼서, 그 자리에 모델을 세워요 (상자가 치워지면 같이 사라져요).
    // 물(Water)은 다른 쪽이 그려요. 모델 프리팹은 씬 빌더(PrototypeSceneBuilder.InkFeatures)가 채워요
    public class InkRoomFeatures : MonoBehaviour
    {
        [SerializeField] GameObject[] pillars = new GameObject[0];   // 깨진 돌기둥 (지름 0.8, 높이 2.4) 변형들
        [SerializeField] GameObject statue;                          // 석불 (1.6 × 1.4, 높이 2.6), 앞이 -Z
        [SerializeField] GameObject wall;                            // 무너진 담 (길이 3, 두께 0.5, 높이 1.2), 길이가 X
        [SerializeField] GameObject coffin;                          // 천마총 무너진 관 (2 × 1, 높이 1), 길이가 X
        [SerializeField] GameObject miasmaPillar;                    // 천마총 마기 기둥 (1 × 1, 높이 2.6)
        [SerializeField] GameObject bloodPoolRim;                    // 천마총 혈지 가장자리 돌·뼈 (3.5 × 2.5, 핏물 표면은 아트 2)

        public void SetupTomb(GameObject coffinModel, GameObject miasmaModel, GameObject poolRimModel)
        {
            coffin = coffinModel; miasmaPillar = miasmaModel; bloodPoolRim = poolRimModel;
        }

        public void Setup(GameObject[] pillarModels, GameObject statueModel, GameObject wallModel)
        {
            pillars = pillarModels; statue = statueModel; wall = wallModel;
        }

        void OnEnable() => RoomRun.LayoutChanged += Dress;
        void OnDisable() => RoomRun.LayoutChanged -= Dress;

        void Dress(RoomRun run)
        {
            if (run == null) return;
            int i = 0;
            foreach (var f in run.RoomFeatures)
            {
                i++;
                if (f.Go == null || f.Go.GetComponent<InkFeatureLink>() != null) continue;
                GameObject model = f.Kind switch
                {
                    RoomRun.FeatureKind.Pillar => pillars.Length > 0 ? pillars[i % pillars.Length] : null,
                    RoomRun.FeatureKind.Statue => statue,
                    RoomRun.FeatureKind.Wall => wall,
                    RoomRun.FeatureKind.Coffin => coffin,
                    RoomRun.FeatureKind.MiasmaPillar => miasmaPillar,
                    RoomRun.FeatureKind.BloodPool => bloodPoolRim,
                    _ => null,
                };
                if (model == null) continue;
                var r = f.Go.GetComponent<Renderer>();
                if (r != null && f.Kind != RoomRun.FeatureKind.BloodPool) r.enabled = false;   // 혈지 바닥판은 아트 2가 핏물로 그려요
                // 상자는 크기를 늘린 물체라 그 자식으로 두면 돌렸을 때 찌그러져요. 따로 세우고 상자가 사라지면 같이 지워요
                float yaw;
                var scale = Vector3.one;
                if (f.Kind == RoomRun.FeatureKind.Wall || f.Kind == RoomRun.FeatureKind.Coffin || f.Kind == RoomRun.FeatureKind.BloodPool)
                {
                    yaw = (f.Size.y > f.Size.x ? 90f : 0f) + (i % 2) * 180f;   // 긴 쪽에 맞춰 돌리고, 무너진·밀려난 쪽이 저마다 달라 보이게
                    float baseLen = f.Kind == RoomRun.FeatureKind.Coffin ? 2f : f.Kind == RoomRun.FeatureKind.BloodPool ? 3.5f : 3f;
                    scale.x = Mathf.Max(f.Size.x, f.Size.y) / baseLen;          // 길이를 맞춰요
                    if (f.Kind == RoomRun.FeatureKind.BloodPool) scale.z = Mathf.Min(f.Size.x, f.Size.y) / 2.5f;
                }
                else if (f.Kind == RoomRun.FeatureKind.Statue)
                    yaw = f.Center.z >= 0f ? 0f : 180f;                         // 석불은 방 가운데 쪽을 봐요 (앞이 -Z)
                else
                    yaw = (f.Center.x * 37f + f.Center.z * 53f) % 360f;
                var inst = Instantiate(model, f.Center, Quaternion.Euler(0f, yaw, 0f));
                inst.name = $"{model.name} ({f.Go.name})";
                inst.transform.localScale = scale;
                f.Go.AddComponent<InkFeatureLink>().model = inst;
            }
        }
    }

    // 기본판 상자에 붙어, 상자가 치워지면(Destroy) 세운 모델도 같이 지워요
    public class InkFeatureLink : MonoBehaviour
    {
        public GameObject model;
        void OnDestroy() { if (model != null) Destroy(model); }
    }
}

using AThousandDawns.Prototype;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 방 구성(RoomRun.RoomFeatures)의 기본판 상자에 수묵 모델을 입혀요: 돌기둥 · 석불 · 무너진 담, 천마총 관 · 마기 기둥 · 혈지, 죽림 대숲 · 쓰러진 대나무 · 개울가, 장강 물가 · 웅덩이 둘레.
    // 상자의 충돌체는 그대로 두고 그림(Renderer)만 꺼서, 그 자리에 모델을 세워요 (상자가 치워지면 같이 사라져요).
    // 물(Water)과 혈지의 표면은 아트 2가 그려요. 죽림 개울엔 조약돌·풀, 장강 물엔 젖은 돌·갈대 물가만 둘러요. 모델 프리팹은 씬 빌더(PrototypeSceneBuilder.InkFeatures)가 채워요
    public class InkRoomFeatures : MonoBehaviour
    {
        [SerializeField] GameObject[] pillars = new GameObject[0];   // 깨진 돌기둥 (지름 0.8, 높이 2.4) 변형들
        [SerializeField] GameObject statue;                          // 석불 (1.6 × 1.4, 높이 2.6), 앞이 -Z
        [SerializeField] GameObject wall;                            // 무너진 담 (길이 3, 두께 0.5, 높이 1.2), 길이가 X
        [SerializeField] GameObject coffin;                          // 천마총 무너진 관 (2 × 1, 높이 1), 길이가 X
        [SerializeField] GameObject miasmaPillar;                    // 천마총 마기 기둥 (1 × 1, 높이 2.6)
        [SerializeField] GameObject bloodPoolRim;                    // 천마총 혈지 가장자리 돌·뼈 (3.5 × 2.5, 핏물 표면은 아트 2)
        [SerializeField] GameObject[] bambooClumps = new GameObject[0]; // 죽림 대숲 덤불 (지름 0.7, 높이 3) 변형들
        [SerializeField] GameObject fallenBamboo;                    // 죽림 쓰러진 대나무 (3.5 × 0.4, 높이 0.5), 길이가 X
        public GameObject FallenBambooModel => fallenBamboo;         // 아트 2 InkQingzhuFx: 청죽객이 쓰러뜨리는 대나무에도 같은 모델을 써요
        [SerializeField] GameObject streamBank;                      // 죽림 개울가 조약돌·풀 (폭 2 · X, 길이 12 · Z)

        [SerializeField] GameObject[] riverBanks = new GameObject[0];   // 장강 물띠 물가 토막 (길이 4 · Z, 물 폭 2.6 · X) 변형들
        [SerializeField] GameObject riverPoolRim;                    // 장강 웅덩이 둘레 (4 × 3)

        const int BambooRegion = 0;   // 죽림 개울엔 조약돌·풀
        const int RiverRegion = 2;    // 장강 수채 여울·물길엔 물가 토막, 웅덩이엔 둘레
        const float RiverSegment = 4f;     // 물가 토막 길이
        const float RiverEndClear = 1.5f;  // 물띠 양끝(문 앞)은 비워요

        public void Setup(GameObject[] pillarModels, GameObject statueModel, GameObject wallModel)
        {
            pillars = pillarModels; statue = statueModel; wall = wallModel;
        }

        public void SetupTomb(GameObject coffinModel, GameObject miasmaModel, GameObject poolRimModel)
        {
            coffin = coffinModel; miasmaPillar = miasmaModel; bloodPoolRim = poolRimModel;
        }

        public void SetupBamboo(GameObject[] clumpModels, GameObject fallenModel, GameObject bankModel)
        {
            bambooClumps = clumpModels; fallenBamboo = fallenModel; streamBank = bankModel;
        }

        public void SetupRiver(GameObject[] bankSegments, GameObject poolRim)
        {
            riverBanks = bankSegments; riverPoolRim = poolRim;
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
                // 죽림 종류(BambooClump · FallenBamboo)는 게임 코드가 들어오기 전에도 컴파일되게 이름으로 골라요
                string kind = f.Kind.ToString();
                bool clump = kind == "BambooClump", fallen = kind == "FallenBamboo";
                if (f.Kind == RoomRun.FeatureKind.Water && run.Region == RiverRegion)
                {
                    DressRiver(f, i);
                    continue;
                }
                bool bank = f.Kind == RoomRun.FeatureKind.Water && run.Region == BambooRegion;
                GameObject model = clump ? (bambooClumps.Length > 0 ? bambooClumps[i % bambooClumps.Length] : null)
                    : fallen ? fallenBamboo
                    : bank ? streamBank
                    : f.Kind switch
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
                // 혈지·물 바닥판은 아트 2가 표면으로 그려요 (가장자리만 둘러요)
                if (r != null && f.Kind != RoomRun.FeatureKind.BloodPool && f.Kind != RoomRun.FeatureKind.Water) r.enabled = false;
                // 상자는 크기를 늘린 물체라 그 자식으로 두면 돌렸을 때 찌그러져요. 따로 세우고 상자가 사라지면 같이 지워요
                float yaw;
                var scale = Vector3.one;
                if (bank)
                {
                    // 개울가 모델은 길이가 Z예요. 물길이 X로 길면 돌려요
                    yaw = (f.Size.x > f.Size.y ? 90f : 0f) + (i % 2) * 180f;
                    scale.x = Mathf.Min(f.Size.x, f.Size.y) / 2f;
                    scale.z = Mathf.Max(f.Size.x, f.Size.y) / 12f;
                }
                else if (f.Kind == RoomRun.FeatureKind.Wall || f.Kind == RoomRun.FeatureKind.Coffin || f.Kind == RoomRun.FeatureKind.BloodPool || fallen)
                {
                    yaw = (f.Size.y > f.Size.x ? 90f : 0f) + (i % 2) * 180f;   // 긴 쪽에 맞춰 돌리고, 무너진·밀려난 쪽이 저마다 달라 보이게
                    float baseLen = f.Kind == RoomRun.FeatureKind.Coffin ? 2f : f.Kind == RoomRun.FeatureKind.BloodPool || fallen ? 3.5f : 3f;
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

        // 장강 물: 고인 웅덩이는 둘레 모델을 크기에 맞춰 하나, 흐르는 물띠는 물가 토막을 길이만큼 이어 붙여요 (돌·갈대가 찌그러지지 않게).
        // 물 바닥판의 그림은 아트 2가 표면으로 쓰니 그대로 둬요
        void DressRiver(RoomRun.RoomFeature f, int i)
        {
            bool alongX = f.Size.x > f.Size.y;
            GameObject holder;
            if (f.Current == Vector3.zero)
            {
                if (riverPoolRim == null) return;
                holder = Instantiate(riverPoolRim, f.Center, Quaternion.Euler(0f, (alongX ? 0f : 90f) + (i % 2) * 180f, 0f));
                holder.name = $"{riverPoolRim.name} ({f.Go.name})";
                holder.transform.localScale = new Vector3(Mathf.Max(f.Size.x, f.Size.y) / 4f, 1f, Mathf.Min(f.Size.x, f.Size.y) / 3f);
            }
            else
            {
                if (riverBanks.Length == 0) return;
                // 토막은 길이가 Z예요. 물띠가 X로 길면 돌려요
                holder = new GameObject($"River Banks ({f.Go.name})");
                holder.transform.SetPositionAndRotation(f.Center, Quaternion.Euler(0f, alongX ? 90f : 0f, 0f));
                float length = Mathf.Max(f.Size.x, f.Size.y) - RiverEndClear * 2f;
                float width = Mathf.Min(f.Size.x, f.Size.y) / 2.6f;
                int count = Mathf.Max(1, Mathf.FloorToInt(length / RiverSegment));
                float start = -count * RiverSegment * 0.5f + RiverSegment * 0.5f;
                for (int k = 0; k < count; k++)
                {
                    var seg = Instantiate(riverBanks[(k + i) % riverBanks.Length], holder.transform);
                    seg.transform.localPosition = new Vector3(0f, 0f, start + k * RiverSegment);
                    seg.transform.localRotation = Quaternion.Euler(0f, (k % 2) * 180f, 0f);   // 거리가 양쪽 물가에 번갈아 놓이게
                    seg.transform.localScale = new Vector3(width, 1f, 1f);
                }
            }
            f.Go.AddComponent<InkFeatureLink>().model = holder;
        }
    }

    // 기본판 상자에 붙어, 상자가 치워지면(Destroy) 세운 모델도 같이 지워요
    public class InkFeatureLink : MonoBehaviour
    {
        public GameObject model;
        void OnDestroy() { if (model != null) Destroy(model); }
    }
}

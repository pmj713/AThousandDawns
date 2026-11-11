using AThousandDawns.Prototype;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 기연 방 · 숨은 석실 (아트 목록 4. 판 안의 소품). 방들은 전투장 하나를 같이 쓰니까, 지금 방이 기연 방(FloorRoomKind.Hidden)일 때만
    // 석실 꾸밈을 켜고 나가면 꺼요. 게임 코드는 건드리지 않아요 (RoomRun.Room만 읽어요).
    //   가운데: 두 단짜리 돌 제단 위에 앉은 채 숨을 거둔 노고수의 유해 (카메라를 보는 붓 그림)
    //   제단 앞: 옥병(영약)과 낡은 책(비급) — 이벤트 선택지와 같은 두 물건
    //   바닥: 이끼 번짐 여러 장과 무너진 돌 몇 개, 제단 둘레로 옅은 빛 웅덩이
    // 씬 빌더(PrototypeSceneBuilder.InkRunProps.cs)가 그림·재질·방 가운데를 넣어 줘요.
    public class InkHiddenChamber : MonoBehaviour
    {
        [SerializeField] Sprite remains, bottle, book, moss, glow;
        [SerializeField] Material stoneMat;
        [SerializeField] Mesh rockMesh;
        [SerializeField] Vector3 center;
        [SerializeField] Vector2 size = new Vector2(20f, 12f);

        RoomRun _run;
        GameObject _root;

        public InkHiddenChamber Setup(Sprite remains, Sprite bottle, Sprite book, Sprite moss, Sprite glow, Material stoneMat, Mesh rockMesh, Vector3 center, Vector2 size)
        {
            this.remains = remains; this.bottle = bottle; this.book = book; this.moss = moss; this.glow = glow;
            this.stoneMat = stoneMat; this.rockMesh = rockMesh; this.center = center; this.size = size;
            return this;
        }

        void Update()
        {
            if (_run == null) _run = FindAnyObjectByType<RoomRun>();
            bool hidden = _run != null && _run.Room != null && _run.Room.Kind == FloorRoomKind.Hidden;
            if (hidden && _root == null) Build();
            if (_root != null && _root.activeSelf != hidden) _root.SetActive(hidden);
        }

        void Build()
        {
            _root = new GameObject("Hidden Chamber (기연 석실)");
            _root.transform.position = center;
            var rng = new System.Random(7);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            // 바닥 이끼 (벽 쪽에 더 많이) · 무너진 돌
            if (moss != null)
                for (int i = 0; i < 14; i++)
                {
                    bool edge = i < 9;
                    float x = edge ? (rng.Next(2) == 0 ? -1f : 1f) * R(size.x * 0.32f, size.x * 0.46f) : R(-size.x * 0.3f, size.x * 0.3f);
                    float z = edge ? R(-size.y * 0.45f, size.y * 0.45f) : (rng.Next(2) == 0 ? -1f : 1f) * R(size.y * 0.22f, size.y * 0.42f);
                    if (i % 3 == 0 && edge) { (x, z) = (R(-size.x * 0.45f, size.x * 0.45f), (rng.Next(2) == 0 ? -1f : 1f) * R(size.y * 0.36f, size.y * 0.46f)); }
                    Flat(moss, new Vector3(x, 0.02f + i * 0.001f, z), R(1.6f, 3.2f), R(0f, 360f), new Color(1f, 1f, 1f, R(0.75f, 1f)));
                }
            if (rockMesh != null && stoneMat != null)
                for (int i = 0; i < 6; i++)
                {
                    var rock = new GameObject("Rubble");
                    rock.transform.SetParent(_root.transform, false);
                    float a = R(0f, Mathf.PI * 2f);
                    rock.transform.localPosition = new Vector3(Mathf.Cos(a) * R(2.4f, 4.5f), 0.05f, Mathf.Sin(a) * R(1.8f, 3.6f));
                    rock.transform.localRotation = Quaternion.Euler(R(0f, 30f), R(0f, 360f), R(0f, 30f));
                    rock.transform.localScale = Vector3.one * R(0.25f, 0.55f);
                    rock.AddComponent<MeshFilter>().sharedMesh = rockMesh;
                    rock.AddComponent<MeshRenderer>().sharedMaterial = stoneMat;
                }

            // 제단 둘레 옅은 빛 (위에서 새어 드는 빛이 바닥에 고인 듯)
            if (glow != null) Flat(glow, new Vector3(0f, 0.03f, 0f), 6.5f, 0f, new Color(0.96f, 0.93f, 0.82f, 0.35f));

            // 두 단 돌 제단
            Step(new Vector3(0f, 0.15f, 0f), new Vector3(2.8f, 0.15f, 2.8f), true);
            Step(new Vector3(0f, 0.38f, 0f), new Vector3(1.9f, 0.1f, 1.9f), false);

            // 유해 · 옥병 · 책 (카메라를 봐요)
            Upright(remains, new Vector3(0f, 0.48f, 0.05f), 1.9f);
            Upright(bottle, new Vector3(-0.55f, 0.48f, -0.75f), 0.45f);
            Upright(book, new Vector3(0.5f, 0.48f, -0.8f), 0.42f);
        }

        void Step(Vector3 pos, Vector3 scale, bool collide)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Altar";
            if (!collide) Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(_root.transform, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            if (stoneMat != null) go.GetComponent<MeshRenderer>().sharedMaterial = stoneMat;
        }

        // 바닥에 눕힌 그림 (가운데 기준)
        void Flat(Sprite sprite, Vector3 pos, float width, float yaw, Color color)
        {
            var go = new GameObject(sprite.name);
            go.transform.SetParent(_root.transform, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(90f, yaw, 0f);
            go.transform.localScale = Vector3.one * (width / Mathf.Max(0.01f, sprite.bounds.size.x));
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = -10;
        }

        // 세운 그림 (아래 가운데 기준, 높이 height)
        void Upright(Sprite sprite, Vector3 pos, float height)
        {
            if (sprite == null) return;
            var go = new GameObject(sprite.name);
            go.transform.SetParent(_root.transform, false);
            go.transform.localPosition = pos;
            go.transform.localScale = Vector3.one * (height / Mathf.Max(0.01f, sprite.bounds.size.y));
            go.AddComponent<SpriteRenderer>().sprite = sprite;
            go.AddComponent<InkFaceCamera>();
        }
    }
}

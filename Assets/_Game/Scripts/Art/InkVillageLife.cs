using UnityEngine;

namespace AThousandDawns.Art
{
    // 마을 생활 소품의 작은 움직임: 빨랫줄의 빨래가 바람에 저마다 흔들리고, 닭은 가끔 고개를 숙여 모이를 쪼고 몸을 돌려요.
    // 빨래는 줄(X축)에 매달린 윗변이 회전축, 닭은 발밑이 회전축이에요. 씬 빌더(PrototypeSceneBuilder.InkVillageLife)가 채워요
    public class InkVillageLife : MonoBehaviour
    {
        [SerializeField] Transform[] cloths = new Transform[0];
        [SerializeField] Transform[] chickens = new Transform[0];
        [SerializeField] float clothSway = 9f;

        float[] _chickenYaw;

        public void Setup(Transform[] clothPivots, Transform[] chickenRoots)
        {
            cloths = clothPivots; chickens = chickenRoots;
        }

        void Start()
        {
            _chickenYaw = new float[chickens.Length];
            for (int i = 0; i < chickens.Length; i++)
                if (chickens[i] != null) _chickenYaw[i] = chickens[i].localEulerAngles.y;
        }

        void Update()
        {
            float t = Time.time;
            for (int i = 0; i < cloths.Length; i++)
            {
                if (cloths[i] == null) continue;
                // 큰 바람(느린 물결)에 잔떨림을 얹어요. 빨래마다 박자가 달라요
                float a = Mathf.Sin(t * 1.1f + i * 1.7f) * clothSway + Mathf.Sin(t * 3.3f + i * 0.9f) * clothSway * 0.25f;
                cloths[i].localRotation = Quaternion.Euler(a, 0f, 0f);
            }
            if (_chickenYaw == null) return;
            for (int i = 0; i < chickens.Length; i++)
            {
                if (chickens[i] == null) continue;
                // 닭마다 2.6~3.6초 주기: 앞 0.5초 동안 두 번 쪼고, 가끔(셋째 주기마다) 몸을 홱 돌려요
                float period = 2.6f + i * 0.5f;
                float local = (t + i * 1.3f) % period;
                int cycle = Mathf.FloorToInt((t + i * 1.3f) / period);
                float peck = local < 0.5f ? Mathf.Abs(Mathf.Sin(local / 0.5f * Mathf.PI * 2f)) * 40f : 0f;
                float yaw = _chickenYaw[i] + (cycle % 3) * 55f * (i % 2 == 0 ? 1f : -1f);
                chickens[i].localRotation = Quaternion.Euler(peck, yaw, 0f);
            }
        }
    }
}

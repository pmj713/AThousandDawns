using AThousandDawns.Enemies;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 녹림왕 2페이즈 "불타는 죽림" 연출: 불길(BurningGrove)이 붙으면 보스 방 대나무 숲에 숨겨 둔 불꽃을
    // 차례로 켜 불이 번지는 것처럼 보여요. 불길이 사라지면(보스 처치·판 다시 시작) 다시 꺼요.
    // 게임 규칙은 건드리지 않고 BurningGrove의 상태만 읽어요.
    public class InkBurningGroveFX : MonoBehaviour
    {
        [SerializeField] GameObject[] flames;
        [SerializeField] float spreadTime = 3f;

        BurningGrove _grove;
        float _searchTimer, _burnTime;
        bool _burning;

        void Awake() => SetAll(false);

        void Update()
        {
            if (_grove == null)
            {
                _searchTimer -= Time.deltaTime;
                if (_searchTimer <= 0f)
                {
                    _searchTimer = 0.5f;
                    _grove = FindAnyObjectByType<BurningGrove>();
                }
            }

            bool burning = _grove != null && _grove.IsBurning;
            if (burning && !_burning) _burnTime = 0f;
            if (!burning && _burning) SetAll(false);
            _burning = burning;
            if (!burning || flames == null) return;

            _burnTime += Time.deltaTime;
            int lit = Mathf.Min(flames.Length, Mathf.CeilToInt(flames.Length * _burnTime / Mathf.Max(0.01f, spreadTime)));
            for (int i = 0; i < lit; i++)
                if (flames[i] != null && !flames[i].activeSelf) flames[i].SetActive(true);
        }

        void SetAll(bool on)
        {
            if (flames == null) return;
            foreach (var f in flames)
                if (f != null) f.SetActive(on);
        }
    }
}

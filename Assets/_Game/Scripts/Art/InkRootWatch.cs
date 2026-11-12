using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AThousandDawns.Art
{
    // 게임이 이름으로 만든 루트 오브젝트(쇠닻 · 등패 · 핏덩이 · 물길 · 물결 검기 …)를 찾는 이펙트용.
    //   씬 루트 목록은 한 프레임에 한 번만 받아 같이 쓰고 (배열을 새로 만들지 않아요),
    //   이름(go.name은 읽을 때마다 문자열이 생겨요)은 처음 보는 루트만 확인해요.
    public static class InkRootWatch
    {
        static readonly List<GameObject> Roots = new List<GameObject>();
        static int _frame = -1;

        static List<GameObject> All()
        {
            if (_frame == Time.frameCount) return Roots;
            _frame = Time.frameCount;
            Roots.Clear();
            SceneManager.GetActiveScene().GetRootGameObjects(Roots);
            return Roots;
        }

        // seen에 없던 루트만 돌려줘요 (본 것으로 표시). seen은 부르는 쪽이 들고 있다가 가끔 Prune으로 비워요
        public static IEnumerable<GameObject> New(HashSet<GameObject> seen)
        {
            var all = All();
            for (int i = 0; i < all.Count; i++)
            {
                var root = all[i];
                if (root != null && seen.Add(root)) yield return root;
            }
        }

        public static void Prune(HashSet<GameObject> seen) => seen.RemoveWhere(g => g == null);
    }
}

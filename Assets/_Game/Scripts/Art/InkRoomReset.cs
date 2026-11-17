using System;
using System.Collections.Generic;
using AThousandDawns.Enemies;
using UnityEngine;

namespace AThousandDawns.Art
{
    // 방 단위 연출 치우기 (플레이테스트 4차: 방을 옮기면 같은 크기 방은 같은 월드 자리를 다시 써서, 앞 방의 핏자국 · 바닥 자국 · 파편 · 잔상이 새 방에 남았어요).
    //   EnemyDirector.RoomReset (방을 옮길 때마다)이 오면, 전투 연출 호스트가 제 밑에 둔 연출(바닥 자국 · 장판 · 잔상 · 고리 …)을 모두 지워요.
    //   호스트는 매 프레임 Tick(_live, dt, transform)으로 연출을 돌리며 등록돼요 (방 꾸밈 호스트는 transform 없이 불러서 지우지 않아요).
    //   지워진 연출을 들고 있던 단계가 예외를 내면 그 단계만 빼요 (다른 연출은 계속 돌아요). 바닥 먹물 얼룩(InkHitSplatter)·파편(InkDebris)은 각자 RoomReset을 들어요.
    public static class InkRoomReset
    {
        static readonly HashSet<Transform> Hosts = new HashSet<Transform>();
        static readonly List<Transform> Scratch = new List<Transform>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            EnemyDirector.RoomReset -= Clear;
            EnemyDirector.RoomReset += Clear;
        }

        // 연출 단계를 한 프레임 돌려요. host를 주면 방을 옮길 때 그 밑의 연출을 지우는 호스트로 등록돼요.
        public static void Tick(List<Func<float, bool>> live, float dt, Transform host = null)
        {
            if (host != null) Hosts.Add(host);
            for (int i = live.Count - 1; i >= 0; i--)
            {
                if (i >= live.Count) continue;
                bool keep;
                try { keep = live[i](dt); }
                catch (MissingReferenceException) { keep = false; } // 방을 옮기며 지운 연출
                catch (NullReferenceException) { keep = false; }
                if (!keep && i < live.Count) live.RemoveAt(i);
            }
        }

        static void Clear()
        {
            Hosts.RemoveWhere(h => h == null);
            foreach (var host in Hosts)
            {
                Scratch.Clear();
                foreach (Transform child in host) Scratch.Add(child);
                foreach (var child in Scratch) UnityEngine.Object.Destroy(child.gameObject);
            }
        }
    }
}

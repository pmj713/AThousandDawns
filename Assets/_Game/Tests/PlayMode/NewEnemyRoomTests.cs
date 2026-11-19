using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AThousandDawns.Core;
using AThousandDawns.Enemies;
using AThousandDawns.Player;
using AThousandDawns.Prototype;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AThousandDawns.Tests
{
    // 플레이테스트 11차 새 공격 (일반 적 열한 종): 엄폐물이 사이에 있어도 오류 없이 싸우고, 공격이 들어오고,
    // 엄폐물 안으로 파고들지 않고, 방을 다시 시작하면 투사체 · 웅덩이가 남지 않아요
    public class NewEnemyRoomTests : InputTestFixture
    {
        static readonly EnemyKind[] Kinds =
        {
            EnemyKind.Grunt, EnemyKind.Skeleton, EnemyKind.Goblin, EnemyKind.Gargoyle, EnemyKind.GoblinRogue, EnemyKind.Werewolf,
            EnemyKind.Slime, EnemyKind.GoblinArcher, EnemyKind.Imp, EnemyKind.Orc, EnemyKind.GoblinShaman,
        };

        static readonly string[] Leftovers = { "점액", "LobbedArrow", "ImpFireball" };

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            MetaSave.PathOverride = System.IO.Path.Combine(Application.temporaryCachePath, $"meta_test_{Guid.NewGuid():N}.json");
        }

        public override void TearDown()
        {
            PrototypeArena.AutoSpawn = true;
            RoomRun.LayoutOverride = -1;
            RoomRun.RoomSizeOverride = -1;
            if (System.IO.File.Exists(MetaSave.PathOverride)) System.IO.File.Delete(MetaSave.PathOverride);
            MetaSave.PathOverride = null;
            Time.timeScale = 1f;
            base.TearDown();
        }

        static void PlacePlayer(PlayerController player, Vector3 flat)
        {
            var cc = player.GetComponent<CharacterController>();
            cc.enabled = false;
            player.transform.position = new Vector3(flat.x, player.transform.position.y, flat.z);
            cc.enabled = true;
        }

        static bool Inside(Bounds b, Vector3 p, float shrink) =>
            Mathf.Abs(p.x - b.center.x) < b.extents.x - shrink && Mathf.Abs(p.z - b.center.z) < b.extents.z - shrink;

        // 공격이 들어올 때까지 싸워요. 그동안 적 몸 가운데가 엄폐물 안에 들어가면 실패
        static IEnumerator Fight(EnemyBrain enemy, PlayerController player, List<Bounds> cover, float seconds, Action<string> fail)
        {
            Time.timeScale = 2f;
            float end = Time.time + seconds, hardEnd = Time.realtimeSinceStartup + seconds * 3f;
            while (Time.time < end && Time.realtimeSinceStartup < hardEnd && player.Health >= player.MaxHealth)
            {
                if (enemy != null && !enemy.IsDead)
                    foreach (var b in cover)
                        if (Inside(b, enemy.transform.position, 0.15f)) { fail($"{enemy.DisplayName}가 엄폐물 안에 들어갔어요 ({enemy.transform.position})"); yield break; }
                yield return null;
            }
            Time.timeScale = 1f;
            if (player.Health >= player.MaxHealth) fail($"{enemy.DisplayName}: 엄폐물 너머 플레이어에게 공격이 안 들어와요");
        }

        static int LeftoverCount() =>
            Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Count(t => t.gameObject.activeInHierarchy && Leftovers.Contains(t.name));

        [UnityTest]
        public IEnumerator 담을_사이에_두고_싸움([ValueSource(nameof(Kinds))] EnemyKind kind)
        {
            PrototypeArena.AutoSpawn = false;
            yield return SceneManager.LoadSceneAsync("Prototype_Combat", LoadSceneMode.Single);
            yield return null;
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            Vector3 home = player.transform.position;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = home + new Vector3(0f, 0.6f, 3f);
            wall.transform.localScale = new Vector3(4f, 1.2f, 0.5f);
            Physics.SyncTransforms();

            var enemy = director.Spawn(kind, home + new Vector3(0f, 0f, 6f));
            if (kind == EnemyKind.GoblinShaman) director.Spawn(EnemyKind.Goblin, home + new Vector3(2.5f, 0f, 6f)); // 치유할 동료
            string error = null;
            yield return Fight(enemy, player, new List<Bounds> { wall.GetComponent<Collider>().bounds }, 30f, m => error = m);
            Assert.IsNull(error, error);

            yield return new WaitForSeconds(0.2f);
            director.ResetRoom();
            yield return null;
            Assert.AreEqual(0, LeftoverCount(), "방을 다시 시작하면 점액 · 곡사 화살 · 화염구가 남지 않아요");
        }

        [UnityTest]
        public IEnumerator 폐사찰_방_구성_안에서_싸움([ValueSource(nameof(Kinds))] EnemyKind kind)
        {
            RoomRun.RoomSizeOverride = 0;
            RoomRun.LayoutOverride = 1; // 석불 · 돌기둥 여섯 (엄폐물이 가장 많아요)
            yield return SceneManager.LoadSceneAsync("Prototype_Run", LoadSceneMode.Single);
            yield return null;
            var run = Object.FindFirstObjectByType<RoomRun>();
            run.SetRegionForTest(1);
            run.ChooseFork(0);
            var room = run.Map.Rooms.First(r => r.HasFight && r.FixedWaves == null);
            run.Enter(room, 0);
            yield return null;
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            var cover = run.RoomFeatures.Where(f => f.Go != null && f.Go.GetComponent<Collider>() != null).Select(f => f.Go.GetComponent<Collider>().bounds).ToList();
            Assert.Greater(cover.Count, 0, "엄폐물이 있는 방 구성");

            // 플레이어는 방 가운데 아래쪽, 적은 엄폐물 하나를 사이에 두고 선 빈자리
            Vector3 c = run.RoomCenter;
            Vector3 me = c + new Vector3(0f, 0f, -3f);
            PlacePlayer(player, me);
            Vector3? spot = null;
            for (float x = -7f; x <= 7f && spot == null; x += 0.5f)
                for (float z = -1f; z <= 4.5f && spot == null; z += 0.5f)
                {
                    Vector3 p = c + new Vector3(x, 0f, z);
                    if (cover.Any(b => Inside(b, p, -0.8f))) continue;
                    Vector3 eye = Vector3.up * 0.6f;
                    if (Physics.Linecast(me + eye, p + eye, out var hit) && cover.Any(b => b.Contains(hit.point) || b.SqrDistance(hit.point) < 0.01f)) spot = p;
                }
            if (spot == null) Assert.Inconclusive("엄폐물 너머 빈자리가 없어요");

            var enemy = director.Spawn(kind, spot.Value);
            if (kind == EnemyKind.GoblinShaman) director.Spawn(EnemyKind.Goblin, spot.Value + new Vector3(0f, 0f, 1.5f));
            yield return null;
            foreach (var e in director.Enemies.ToList()) // 방에 원래 나온 적은 치워요 (새 적만 남게)
                if (e != null && !e.IsDead && e != enemy && !(kind == EnemyKind.GoblinShaman && e.Kind == EnemyKind.Goblin)) e.Damageable.ApplyHit(999999, false);
            player.Heal(99999f);
            yield return null;

            string error = null;
            yield return Fight(enemy, player, cover, 30f, m => error = m);
            Assert.IsNull(error, error);
        }
    }
}

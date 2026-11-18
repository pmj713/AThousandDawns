using System;
using System.Collections;
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
    // 플레이테스트 8차: 쫓아오는 근접 적은 기둥 · 상자 · 막다른 모서리에 걸려 서지 않고 트인 길로 돌아와요
    public class EnemySteeringTests : InputTestFixture
    {
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
            if (System.IO.File.Exists(MetaSave.PathOverride)) System.IO.File.Delete(MetaSave.PathOverride);
            MetaSave.PathOverride = null;
            Time.timeScale = 1f;
            base.TearDown();
        }

        static IEnumerator WaitUntil(Func<bool> condition, float timeout)
        {
            float end = Time.time + timeout, hardEnd = Time.realtimeSinceStartup + timeout * 5f;
            while (!condition() && Time.time < end && Time.realtimeSinceStartup < hardEnd) yield return null;
        }

        static GameObject Box(Vector3 at, Vector3 size)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.transform.position = at + Vector3.up * size.y * 0.5f;
            box.transform.localScale = size;
            Physics.SyncTransforms(); // 만든 프레임에 바로 장애물 검사가 되게
            return box;
        }

        IEnumerator LoadCombat()
        {
            PrototypeArena.AutoSpawn = false;
            yield return SceneManager.LoadSceneAsync("Prototype_Combat", LoadSceneMode.Single);
            yield return null;
        }

        static float Flat(Vector3 a, Vector3 b)
        {
            Vector3 d = a - b;
            d.y = 0f;
            return d.magnitude;
        }

        // 바로 앞을 막은 기둥: 돌아서 플레이어에게 와요 (플레이어 앞쪽엔 목인장이 있어서 뒤쪽에서 시험해요)
        [UnityTest]
        public IEnumerator 기둥_뒤의_근접_적이_돌아서_플레이어에게_와요()
        {
            yield return LoadCombat();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            Vector3 home = player.transform.position;
            Box(home + new Vector3(0f, 0f, -3f), new Vector3(1.4f, 2f, 1.4f));
            var grunt = director.Spawn(EnemyKind.Grunt, home + new Vector3(0f, 0f, -6.5f));
            bool detoured = false;
            yield return WaitUntil(() =>
            {
                player.Heal(9999f);
                detoured |= grunt.IsDetouring;
                return Flat(grunt.transform.position, player.transform.position) < 1.8f;
            }, 6f);
            Assert.IsTrue(detoured, "기둥 앞에서 돌아가요");
            Assert.Less(Flat(grunt.transform.position, player.transform.position), 1.8f, "기둥을 돌아 6초 안에 와요");
        }

        // 한쪽이 막다른 모서리 (긴 담 + 꺾인 벽): 그쪽으로 돌던 적도 다 막히면 반대쪽으로 바꿔서 와요.
        // 둘을 내보내면 우회 쪽이 번갈아 정해져서 하나는 꼭 막힌 쪽으로 먼저 가요.
        [UnityTest]
        public IEnumerator 막다른_모서리에_갇히지_않고_반대쪽으로_돌아와요()
        {
            yield return LoadCombat();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            Vector3 home = player.transform.position;
            Box(home + new Vector3(1.5f, 0f, -3f), new Vector3(6f, 2f, 0.5f));    // 긴 담: x -1.5 ~ 4.5
            Box(home + new Vector3(4.5f, 0f, -5.75f), new Vector3(0.5f, 2f, 5.5f)); // 오른쪽 끝에서 뒤로 꺾인 벽
            var a = director.Spawn(EnemyKind.Grunt, home + new Vector3(0.6f, 0f, -6f));
            var b = director.Spawn(EnemyKind.Grunt, home + new Vector3(1.4f, 0f, -6f));
            bool aIn = false, bIn = false;
            yield return WaitUntil(() =>
            {
                player.Heal(9999f);
                aIn |= a != null && Flat(a.transform.position, player.transform.position) < 1.8f;
                bIn |= b != null && Flat(b.transform.position, player.transform.position) < 1.8f;
                return aIn && bIn;
            }, 14f);
            Assert.IsTrue(aIn, "첫째 적이 담을 돌아 와요");
            Assert.IsTrue(bIn, "둘째 적도 모서리에 갇히지 않고 와요");
        }
    }
}

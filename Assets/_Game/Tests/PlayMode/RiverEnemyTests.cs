using System;
using System.Collections;
using System.Linq;
using AThousandDawns.Combat;
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
    // 장강 수채 일반 적 6종: 수적 졸개 두 번 베기 · 작살수 찌르기 끌어당김 · 쇠닻 거한 닻 던지기 · 등패수 원거리 막기·방패 굴리기 · 수귀 끌고 가기 · 깃발수 지휘
    public class RiverEnemyTests : InputTestFixture
    {
        PlayerController _player;
        EnemyDirector _director;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            PrototypeArena.AutoSpawn = false;
        }

        public override void TearDown()
        {
            PrototypeArena.AutoSpawn = true;
            Time.timeScale = 1f;
            base.TearDown();
        }

        IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("Prototype_Combat", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindFirstObjectByType<PlayerController>();
            _director = Object.FindFirstObjectByType<EnemyDirector>();
        }

        static IEnumerator WaitUntil(Func<bool> condition, float timeout)
        {
            float end = Time.time + timeout, hardEnd = Time.realtimeSinceStartup + timeout * 5f;
            while (!condition() && Time.time < end && Time.realtimeSinceStartup < hardEnd) yield return null;
        }

        EnemyBrain Spawn(EnemyKind kind, float distance) =>
            _director.Spawn(kind, _player.transform.position + _player.Facing * distance);

        [UnityTest]
        public IEnumerator 여섯_종이_이름과_체력대로_나옴()
        {
            yield return Load();
            var expect = new (EnemyKind kind, string name, float hp)[]
            {
                (EnemyKind.Pirate, "수적 졸개", 30f), (EnemyKind.Harpooner, "작살수", 25f), (EnemyKind.Anchor, "쇠닻 거한", 104f),
                (EnemyKind.Rattan, "등패수", 60f), (EnemyKind.WaterGhost, "수귀", 30f), (EnemyKind.FlagBearer, "깃발수", 40f),
            };
            foreach (var (kind, name, hp) in expect)
            {
                var e = _director.Spawn(kind, _player.transform.position + new Vector3(8f, 0f, 0f));
                Assert.AreEqual(kind, e.Kind);
                Assert.AreEqual(name, e.DisplayName);
                Assert.AreEqual(hp, e.Damageable.MaxHealth, 0.01f, $"{name} 체력 (지역 배율 전)");
                e.Damageable.ApplyHit(99999);
            }
            Assert.AreSame(RoomBudget.River, RoomBudget.ForRegion(2));
            Assert.AreEqual(2, RoomBudget.PackSize(EnemyKind.Pirate), "수적 졸개는 둘씩");
            Assert.IsFalse(EnemyBrain.Allows(EnemyKind.WaterGhost, EliteTrait.Blaze), "수귀는 정예가 아니에요");
        }

        [UnityTest]
        public IEnumerator 작살에_맞으면_끌려감()
        {
            yield return Load();
            var harpooner = Spawn(EnemyKind.Harpooner, 5f);
            float start = Vector3.Distance(_player.transform.position, harpooner.transform.position);
            float hp = _player.Health, closest = float.MaxValue;
            yield return WaitUntil(() =>
            {
                if (_player.Health < hp) closest = Mathf.Min(closest, Vector3.Distance(_player.transform.position, harpooner.transform.position));
                return _player.Health < hp && closest < start - 1f;
            }, 10f);
            Assert.Less(_player.Health, hp, "작살에 맞았어요");
            Assert.Less(closest, start - 1f, "작살수 쪽으로 끌려가요");
        }

        [UnityTest]
        public IEnumerator 등패수는_멀리서_온_공격을_정면에서_다_막음()
        {
            yield return Load();
            var rattan = Spawn(EnemyKind.Rattan, 6f);
            yield return new WaitForSeconds(0.3f); // 방패를 들고 나를 바라볼 때까지
            Vector3 toPlayer = _player.transform.position - rattan.transform.position;
            toPlayer.y = 0f;
            toPlayer.Normalize();
            Vector3 front = rattan.transform.position + toPlayer * 5f;
            Vector3 near = rattan.transform.position + toPlayer * 1.5f;
            Assert.AreEqual(0f, rattan.Damageable.ResolveSituational(front, HitTag.Basic), "5칸 밖 정면: 다 막아요");
            Assert.AreEqual(0.2f, rattan.Damageable.ResolveSituational(near, HitTag.Basic), 0.01f, "가까운 정면: 방패 산적처럼 20%");
        }

        [UnityTest]
        public IEnumerator 등패수는_방패를_굴려_던지고_그동안_정면이_열림()
        {
            yield return Load();
            var rattan = (RattanBrain)Spawn(EnemyKind.Rattan, 5f);
            yield return WaitUntil(() => rattan.ShieldThrown, 8f);
            Assert.IsTrue(rattan.ShieldThrown, "방패를 굴려 던져요");
            Assert.Greater(Object.FindObjectsByType<BeadThrow>(FindObjectsSortMode.None).Length, 0, "굴러가는 방패");
            Assert.AreEqual(1f, rattan.Damageable.ResolveSituational(rattan.transform.position + (_player.transform.position - rattan.transform.position).normalized * 5f, HitTag.Basic), "방패가 없으면 정면도 열려요");
        }

        [UnityTest]
        public IEnumerator 쇠닻_거한은_닻을_던져_날아오고_닻이_길을_막음()
        {
            yield return Load();
            var anchor = Spawn(EnemyKind.Anchor, 6f);
            float start = Vector3.Distance(_player.transform.position, anchor.transform.position);
            yield return WaitUntil(() => AnchorBrain.PostCount > 0, 6f);
            Vector3 thrownAt = anchor.transform.position;
            yield return null;
            yield return null;
            Assert.Less(Vector3.Distance(thrownAt, anchor.transform.position), 0.05f, "닻이 날아가 꽂히기 전엔 제자리 (던지고 나서 끌려가요)");
            Assert.Greater(AnchorBrain.PostCount, 0, "닻을 던져요");
            yield return WaitUntil(() => Vector3.Distance(_player.transform.position, anchor.transform.position) < start - 3f, 2f);
            Assert.Less(Vector3.Distance(_player.transform.position, anchor.transform.position), start - 3f, "꽂힌 자리로 날아와요");
        }

        [UnityTest]
        public IEnumerator 수귀는_숨어서_다가와_붙잡고_끌고_감()
        {
            yield return Load();
            var ghost = (WaterGhostBrain)Spawn(EnemyKind.WaterGhost, 5f);
            yield return null;
            Assert.IsTrue(ghost.Submerged, "처음엔 숨어 있어요");
            Assert.IsFalse(ghost.Damageable.Targetable, "숨은 동안은 맞힐 수 없어요");
            float hp = _player.Health;
            yield return WaitUntil(() => _player.Health < hp, 8f);
            Assert.Less(_player.Health, hp, "다가와 붙잡아요");
            Assert.IsFalse(ghost.Submerged, "튀어나와 드러나요");
            Assert.IsTrue(ghost.Dragging, "붙잡고 끌고 가요");
            Vector3 before = _player.transform.position;
            yield return new WaitForSeconds(0.6f);
            Assert.Greater(Vector3.Distance(before, _player.transform.position), 0.5f, "끌려가요");
            ghost.Damageable.ApplyHit(1);
            Assert.IsFalse(ghost.Dragging, "때리면 풀려나요");
        }

        [UnityTest]
        public IEnumerator 깃발수는_둘레_적을_지휘해_빠르게()
        {
            yield return Load();
            Spawn(EnemyKind.FlagBearer, 6f);
            var pirate = Spawn(EnemyKind.Pirate, 4f);
            yield return WaitUntil(() => pirate.IsHastened, 8f);
            Assert.IsTrue(pirate.IsHastened, "지휘받으면 빨라져요");
            Assert.AreEqual(0, _director.Count(EnemyKind.Pirate) - 1, "부르지는 않아요");
        }

        [UnityTest]
        public IEnumerator 수적_졸개는_두_번_벰()
        {
            yield return Load();
            var pirate = Spawn(EnemyKind.Pirate, 2.5f);
            bool second = false;
            yield return WaitUntil(() => (second |= pirate.AttackName == "두 번 베기" && pirate.AttackPhase == EnemyAttackTimeline.Phase.Active), 6f);
            Assert.IsTrue(second, "전진 베기가 나와요");
            Assert.AreEqual("두 번 베기", pirate.AttackName);
        }
    }
}

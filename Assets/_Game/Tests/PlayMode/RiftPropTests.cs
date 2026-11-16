using System;
using System.Collections;
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
    // 차원 겹침 소품: 무협 방에 이계 소품(성벽 조각 · 기사 석상 · 룬 기둥 · 마나 수정)이 섞여요. 길 · 가운데 · 다른 소품을 비키고, 마나 수정은 깨면 적에게만 터져요
    public class RiftPropTests : InputTestFixture
    {
        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            MetaSave.PathOverride = System.IO.Path.Combine(Application.temporaryCachePath, $"meta_test_{Guid.NewGuid():N}.json");
            RoomRun.RoomSizeOverride = 0;
        }

        public override void TearDown()
        {
            RoomRun.LayoutOverride = -1;
            RoomRun.RoomSizeOverride = -1;
            RoomRun.RiftOverride = 0;
            RoomRun.RiftKindOverride = -1;
            RoomRun.SeedOverride = 0;
            if (System.IO.File.Exists(MetaSave.PathOverride)) System.IO.File.Delete(MetaSave.PathOverride);
            MetaSave.PathOverride = null;
            base.TearDown();
        }

        static readonly Vector2[] Bamboo = { new Vector2(-5.5f, 3f), new Vector2(5.5f, -3f), new Vector2(-7f, -3.5f), new Vector2(7f, 3.5f), new Vector2(3.5f, 3f), new Vector2(-3.5f, -3f) };
        static readonly Vector2[] Bushes = { new Vector2(-7.5f, 4.3f), new Vector2(7.5f, -4.3f), new Vector2(-3f, 4.3f), new Vector2(3f, -4.3f) };

        static bool Overlaps(RoomRun.RoomFeature f, Vector3 origin, Vector2 p, float r) =>
            Mathf.Abs(origin.x + p.x - f.Center.x) < f.Size.x * 0.5f + r && Mathf.Abs(origin.z + p.y - f.Center.z) < f.Size.y * 0.5f + r;

        IEnumerator Load(int region)
        {
            yield return SceneManager.LoadSceneAsync("Prototype_Run", LoadSceneMode.Single);
            yield return null;
            var run = Object.FindFirstObjectByType<RoomRun>();
            run.SetRegionForTest(region);
            run.ChooseFork(0);
        }

        [UnityTest]
        public IEnumerator 이계_소품은_길_가운데_대나무_풀숲_다른_소품을_비켜요()
        {
            foreach (int seed in new[] { 11, 4242, 987654 }) // 판 시드마다 자리가 달라요
            for (int region = 1; region <= 3; region++)
            {
                RoomRun.SeedOverride = seed;
                yield return Load(region);
                var run = Object.FindFirstObjectByType<RoomRun>();
                var room = run.Map.Rooms.First(r => r.HasFight && r.FixedWaves == null);
                RoomRun.RiftOverride = 3;
                for (int layout = 0; layout < 4; layout++)
                {
                    RoomRun.LayoutOverride = layout;
                    run.Enter(room, 0);
                    var rift = run.RoomFeatures.Where(f => RoomRun.IsRift(f.Kind)).ToList();
                    Assert.GreaterOrEqual(rift.Count, 2, $"시드 {seed} 지역 {region} 구성 {layout}: 이계 소품이 섞여요");
                    Assert.AreEqual(rift.Count, run.RiftPropCount);
                    Vector3 c = run.RoomCenter;
                    foreach (var f in rift)
                    {
                        Assert.IsTrue(f.Go.GetComponent<Collider>().enabled, "엄폐물이에요");
                        Vector3 local = f.Center - c;
                        Assert.IsFalse(Mathf.Abs(local.x) < f.Size.x * 0.5f + 1.5f || Mathf.Abs(local.z) < f.Size.y * 0.5f + 1.5f, $"{f.Kind}: 문 사이 십자 길은 비워요");
                        foreach (var b in Bamboo) Assert.IsFalse(Overlaps(f, c, b, 0.5f), $"{f.Kind}가 대나무 기둥과 겹쳐요");
                        foreach (var b in Bushes) Assert.IsFalse(Overlaps(f, c, b, 0.5f), $"{f.Kind}가 풀숲과 겹쳐요");
                        foreach (var o in run.RoomFeatures)
                        {
                            if (o == f) continue;
                            bool hit = Mathf.Abs(o.Center.x - f.Center.x) < (o.Size.x + f.Size.x) * 0.5f + 0.5f && Mathf.Abs(o.Center.z - f.Center.z) < (o.Size.y + f.Size.y) * 0.5f + 0.5f;
                            Assert.IsFalse(hit, $"{f.Kind}가 {o.Kind}와 겹쳐요");
                        }
                    }
                    foreach (var jar in run.Jars)
                        foreach (var f in rift)
                            Assert.IsFalse(Overlaps(f, Vector3.zero, new Vector2(jar.transform.position.x, jar.transform.position.z), 0.2f), "항아리는 소품 밖에");
                    yield return null;
                }
            }
        }

        [UnityTest]
        public IEnumerator 마나_수정은_깨지면_둘레_적에게만_터져요()
        {
            yield return Load(2);
            var run = Object.FindFirstObjectByType<RoomRun>();
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            var room = run.Map.Rooms.First(r => r.HasFight && r.FixedWaves == null);
            RoomRun.RiftOverride = 1;
            RoomRun.RiftKindOverride = (int)RoomRun.FeatureKind.ManaCrystal;
            RoomRun.LayoutOverride = 0;
            run.Enter(room, 0);
            var crystal = run.RoomFeatures.FirstOrDefault(f => f.Kind == RoomRun.FeatureKind.ManaCrystal);
            Assert.IsNotNull(crystal, "마나 수정");
            Assert.IsNotNull(crystal.Go.GetComponent<AThousandDawns.Combat.Breakable>(), "공격으로 깨져요");

            director.DespawnAll();
            var near = director.Spawn(EnemyKind.Grunt, crystal.Center + new Vector3(1.2f, 0f, 0f));
            var far = director.Spawn(EnemyKind.Grunt, crystal.Center + new Vector3(0f, 0f, 6f));
            var cc = player.GetComponent<CharacterController>();
            cc.enabled = false;
            player.transform.position = crystal.Center + new Vector3(-1f, 0f, 0f);
            cc.enabled = true;
            yield return null;
            float nearHp = near.Damageable.Health, farHp = far.Damageable.Health, playerHp = player.Health;
            bool burst = false;
            int dealt = 0;
            near.Damageable.Damaged += (d, amount) => dealt += amount;
            Action<RoomRun, Vector3, float> onBurst = (r, at, radius) => burst = true;
            RoomRun.CrystalBurst += onBurst;
            try
            {
                AThousandDawns.Combat.Breakable.BreakNear(crystal.Center, 0.1f);
            }
            finally
            {
                RoomRun.CrystalBurst -= onBurst;
            }
            Assert.IsTrue(burst, "터져요");
            Assert.AreEqual(0, run.RiftPropCount, "수정은 사라져요");
            Assert.Greater(dealt, 0, "둘레 적은 맞아요");
            Assert.AreEqual(farHp, far.Damageable.Health, "먼 적은 그대로");
            Assert.AreEqual(playerHp, player.Health, "플레이어는 안 맞아요");
            float scale = 1f + 2 * GameConfig.GetOptional("rift.crystal_region_scale", 0.6f);
            Assert.AreEqual(Mathf.RoundToInt(GameConfig.GetOptional("rift.crystal_damage", 40f) * scale), dealt, "지역 배율 (장강 ×2.2)");
        }
    }
}

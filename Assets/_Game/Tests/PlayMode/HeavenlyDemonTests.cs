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
    // 천마총 최종 보스 천마: 3페이즈 · 마기 포박 · 해체대법 안전 반경 · 격파하면 천마 격파 (회귀의 기억 · 유품)
    public class HeavenlyDemonTests : InputTestFixture
    {
        string _savePath;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            _savePath = System.IO.Path.Combine(Application.temporaryCachePath, $"meta_test_{Guid.NewGuid():N}.json");
            MetaSave.PathOverride = _savePath;
        }

        public override void TearDown()
        {
            PrototypeArena.AutoSpawn = true;
            MetaSave.PathOverride = null;
            if (System.IO.File.Exists(_savePath)) System.IO.File.Delete(_savePath);
            Time.timeScale = 1f;
            base.TearDown();
        }

        static IEnumerator WaitUntil(Func<bool> condition, float timeout)
        {
            float end = Time.time + timeout, hardEnd = Time.realtimeSinceStartup + timeout * 5f;
            while (!condition() && Time.time < end && Time.realtimeSinceStartup < hardEnd) yield return null;
        }

        IEnumerator Spawn(Action<HeavenlyDemonBrain, PlayerController> use, float distance = 4f)
        {
            PrototypeArena.AutoSpawn = false;
            yield return SceneManager.LoadSceneAsync("Prototype_Combat", LoadSceneMode.Single);
            yield return null;
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            var boss = (HeavenlyDemonBrain)director.Spawn(EnemyKind.HeavenlyDemon, player.transform.position + new Vector3(0f, 0f, distance));
            boss.SetZone(player.transform.position, new Vector2(30f, 24f));
            use(boss, player);
        }

        [UnityTest]
        public IEnumerator 세_페이즈와_해체대법_안전_반경()
        {
            HeavenlyDemonBrain boss = null;
            PlayerController player = null;
            yield return Spawn((b, p) => { boss = b; player = p; });
            Assert.AreEqual("천마", boss.DisplayName);
            Assert.AreEqual(EnemyRank.Boss, boss.Rank);
            Assert.AreEqual(1, boss.Phase);

            boss.Damageable.ApplyHit(Mathf.CeilToInt(boss.Damageable.MaxHealth * 0.36f));
            Assert.IsTrue(boss.IsTransitioning, "66%에서 전환 (무적)");
            yield return WaitUntil(() => boss.Phase == 2 && !boss.IsTransitioning, 4f);
            Assert.AreEqual(2, boss.Phase, "마도");
            Assert.AreEqual(0f, boss.SafeRadius, "마기는 3페이즈부터");

            boss.Damageable.ApplyHit(Mathf.CeilToInt(boss.Damageable.MaxHealth * 0.34f));
            Assert.IsTrue(boss.IsTransitioning, "33%에서 전환");
            yield return WaitUntil(() => boss.Phase == 3 && !boss.IsTransitioning, 4f);
            Assert.AreEqual(3, boss.Phase, "마검 해체대법");
            Assert.AreEqual(11f, boss.SafeRadius, 0.01f, "안전 반경 11칸에서 시작");
            yield return WaitUntil(() => boss.SafeRadius < 11f, 4f);
            Assert.Less(boss.SafeRadius, 11f, "3초마다 좁아져요");

            var cc = player.GetComponent<CharacterController>();
            cc.enabled = false;
            player.transform.position = player.transform.position + new Vector3(13f, 0f, 0f);
            cc.enabled = true;
            yield return null;
            Assert.IsTrue(boss.PlayerInMiasma, "안전 반경 밖");
            float hp = player.Health;
            yield return WaitUntil(() => player.Health < hp, 2f);
            Assert.Less(player.Health, hp, "마기 속에 있으면 아파요");
        }

        // 마검사 천마: 2페이즈는 마법 (유성 · 차원 베기 · 마법진), 3페이즈는 마검 융합 (마검 천멸 · 마검비)
        [UnityTest]
        public IEnumerator 이페이즈는_마법_삼페이즈는_마검_융합()
        {
            HeavenlyDemonBrain boss = null;
            PlayerController player = null;
            yield return Spawn((b, p) => { boss = b; player = p; }, 5f);
            int kitBefore = EnemyBrain.BossKitOverride;
            EnemyBrain.BossKitOverride = 0;
            var seen = new System.Collections.Generic.HashSet<string>();
            int rifts = 0, meteors = 0, runes = 0, fires = 0;
            System.Action<HeavenlyDemonBrain, HeavenlyDemonBrain.FxCue> cue = (b, c) =>
            {
                if (c.Kind == HeavenlyDemonBrain.FxKind.RiftCut) rifts++;
                if (c.Kind == HeavenlyDemonBrain.FxKind.MarkFall && c.Style == HeavenlyDemonBrain.MarkStyle.Fireball) meteors++;
                if (c.Kind == HeavenlyDemonBrain.FxKind.MarkFall && c.Style == HeavenlyDemonBrain.MarkStyle.Rune) runes++;
                if (c.Kind == HeavenlyDemonBrain.FxKind.MarkFall && c.Style == HeavenlyDemonBrain.MarkStyle.FireSword) fires++;
            };
            HeavenlyDemonBrain.Cue += cue;
            try
            {
                boss.Damageable.ApplyHit(Mathf.CeilToInt(boss.Damageable.MaxHealth * 0.36f));
                yield return WaitUntil(() => boss.Phase == 2 && !boss.IsTransitioning, 4f);
                Time.timeScale = 3f;
                yield return WaitUntil(() =>
                {
                    player.Heal(9999f);
                    if (boss.AttackName != null) seen.Add(boss.AttackName);
                    return seen.Contains("유성") && seen.Contains("마법진") && seen.Contains("차원 베기") && rifts > 0 && meteors > 0 && runes > 0;
                }, 25f);
                Assert.Contains("유성", new System.Collections.Generic.List<string>(seen));
                Assert.Contains("마법진", new System.Collections.Generic.List<string>(seen));
                Assert.Contains("차원 베기", new System.Collections.Generic.List<string>(seen));
                Assert.Greater(rifts, 0, "차원 베기: 균열 사이가 베어져요");
                Assert.Greater(meteors, 0, "유성이 떨어져요");
                Assert.Greater(runes, 0, "마법진이 터져요");
                Assert.IsFalse(seen.Contains("화염구 비") || seen.Contains("순간이동 검격") || seen.Contains("마력 흡인"), "없어진 패턴");
                Assert.IsFalse(seen.Contains("마검 천멸"), "융합은 3페이즈부터");

                Time.timeScale = 1f;
                boss.Damageable.ApplyHit(Mathf.CeilToInt(boss.Damageable.MaxHealth * 0.34f));
                yield return WaitUntil(() => boss.Phase == 3 && !boss.IsTransitioning, 4f);
                Time.timeScale = 3f;
                yield return WaitUntil(() =>
                {
                    player.Heal(9999f);
                    if (boss.AttackName != null) seen.Add(boss.AttackName);
                    return seen.Contains("마검 천멸") && seen.Contains("마검비") && fires > 0;
                }, 25f);
                Assert.IsTrue(seen.Contains("마검 천멸"), "마검 천멸");
                Assert.IsTrue(seen.Contains("마검비"), "마검비");
                Assert.Greater(fires, 0, "불길 두른 검이 터져요");
            }
            finally
            {
                HeavenlyDemonBrain.Cue -= cue;
                EnemyBrain.BossKitOverride = kitBefore;
            }
        }

        // 10차: 마기 장판 세 곳은 깔리기 전에 그 자리 · 그 반경에 바닥 예고 원이 먼저 차올라요 (miasma_warn)
        [UnityTest]
        public IEnumerator 마기_장판은_세_곳에_먼저_바닥_예고()
        {
            HeavenlyDemonBrain boss = null;
            PlayerController player = null;
            yield return Spawn((b, p) => { boss = b; player = p; });
            int kitBefore = EnemyBrain.BossKitOverride;
            EnemyBrain.BossKitOverride = 0;
            var marks = new System.Collections.Generic.List<(Vector3 at, float time)>();
            System.Action<HeavenlyDemonBrain, HeavenlyDemonBrain.FxCue> cue = (b, c) =>
            {
                if (c.Kind == HeavenlyDemonBrain.FxKind.MiasmaMark) marks.Add((c.At, Time.time));
            };
            HeavenlyDemonBrain.Cue += cue;
            try
            {
                Time.timeScale = 2f;
                yield return WaitUntil(() => { player.Heal(9999f); return marks.Count >= 3; }, 15f);
                Assert.GreaterOrEqual(marks.Count, 3, "세 곳에 바닥 예고");
                Assert.AreEqual(0, Pools().Count, "예고가 차는 동안엔 아직 장판이 없어요");
                yield return WaitUntil(() => { player.Heal(9999f); return Pools().Count >= 3; }, 3f);
                var pools = Pools();
                Assert.GreaterOrEqual(pools.Count, 3, "예고가 다 차면 장판");
                float warn = AThousandDawns.Core.GameConfig.GetOptional("heavenlydemon.miasma_warn", 0.8f);
                Assert.GreaterOrEqual(Time.time - marks[0].time, warn - 0.05f, "miasma_warn만큼 기다려요");
                foreach (var m in marks.GetRange(0, 3))
                    Assert.IsTrue(pools.Exists(f => new Vector2(f.transform.position.x - m.at.x, f.transform.position.z - m.at.z).magnitude < 0.01f), "예고한 바로 그 자리에 깔려요");
            }
            finally
            {
                HeavenlyDemonBrain.Cue -= cue;
                EnemyBrain.BossKitOverride = kitBefore;
            }
        }

        static System.Collections.Generic.List<GhostFire> Pools() =>
            new System.Collections.Generic.List<GhostFire>(Object.FindObjectsByType<GhostFire>(FindObjectsSortMode.None)).FindAll(f => f.name == "마기 장판");

        // 멀리서만 싸우면 마기 포박: 느린 구슬이 날아와 맞으면 잠깐 묶여요 (끌어당기지 않아요)
        [UnityTest]
        public IEnumerator 멀리_있으면_마기_포박으로_묶음()
        {
            HeavenlyDemonBrain boss = null;
            PlayerController player = null;
            yield return Spawn((b, p) => { boss = b; player = p; }, 9f);
            int launched = 0, bound = 0;
            float boundSlow = 0f;
            System.Action<HeavenlyDemonBrain, HeavenlyDemonBrain.FxCue> cue = (b, c) =>
            {
                if (c.Kind == HeavenlyDemonBrain.FxKind.Bind) launched++;
                if (c.Kind == HeavenlyDemonBrain.FxKind.Bound)
                {
                    bound++;
                    boundSlow = player.SlowRatio;
                }
            };
            HeavenlyDemonBrain.Cue += cue;
            try
            {
                var cc = player.GetComponent<CharacterController>();
                yield return WaitUntil(() =>
                {
                    player.Heal(9999f);
                    if (launched == 0 && !player.IsInvulnerable) // 구슬이 날아오기 전까지 멀리 붙잡아 둬요
                    {
                        cc.enabled = false;
                        player.transform.position = boss.transform.position + new Vector3(0f, 0f, -9f);
                        cc.enabled = true;
                    }
                    return bound > 0;
                }, 15f);
                Assert.Greater(launched, 0, "멀리 있으면 마기 포박을 던져요");
                Assert.Greater(bound, 0, "구슬에 맞으면 묶여요");
                Assert.GreaterOrEqual(boundSlow, 0.95f, "묶이면 못 움직여요");
                Assert.IsFalse(boss.IsPulling, "끌어당기지 않아요");
            }
            finally
            {
                HeavenlyDemonBrain.Cue -= cue;
            }
        }

        // 천마총 보스를 쓰러뜨리면 판 끝 · 천마 격파 (결과 화면 · 회귀의 기억 · 유품 가지)
        [UnityTest]
        public IEnumerator 천마를_쓰러뜨리면_천마_격파()
        {
            yield return SceneManager.LoadSceneAsync("Prototype_Run", LoadSceneMode.Single);
            yield return null;
            var run = Object.FindFirstObjectByType<RoomRun>();
            var arena = Object.FindFirstObjectByType<PrototypeArena>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            run.SetRegionForTest(3);
            run.ChooseFork(0);
            run.EnterRest();
            var cc = player.GetComponent<CharacterController>();
            cc.enabled = false;
            player.transform.position = new Vector3(60f, 0f, 6.6f);
            cc.enabled = true;
            yield return WaitUntil(() => arena.Boss != null && arena.Boss.IsAwake, 3f);
            Assert.IsInstanceOf<HeavenlyDemonBrain>(arena.Boss, "천마총 보스는 천마");
            Assert.AreEqual(11700f, arena.Boss.Damageable.MaxHealth, 1f, "2600 × 4.5");
            arena.Boss.Damageable.ApplyHit(99999);
            yield return WaitUntil(() => run.IsDone, 4f);
            Assert.IsTrue(run.IsDone, "판 끝");
            run.ShowResult(true);
            Assert.AreEqual("천마 격파", run.Result.Where);
            Assert.AreEqual(1, MetaSave.Load().finalBossKills, "유품 가지가 열려요");
        }

        // 천마를 쓰러뜨리고 보상을 고르면 결과 화면 전에 엔딩: 첫 격파는 대사 4줄, 다음부터는 1줄
        [UnityTest]
        public IEnumerator 천마_격파_엔딩_첫_격파는_네_줄_다음부터는_한_줄()
        {
            for (int kills = 0; kills < 2; kills++)
            {
                new MetaSave { finalBossKills = kills }.Save();
                yield return SceneManager.LoadSceneAsync("Prototype_Run", LoadSceneMode.Single);
                yield return null;
                var run = Object.FindFirstObjectByType<RoomRun>();
                var arena = Object.FindFirstObjectByType<PrototypeArena>();
                var player = Object.FindFirstObjectByType<PlayerController>();
                run.SetRegionForTest(3);
                run.ChooseFork(0);
                run.EnterRest();
                var cc = player.GetComponent<CharacterController>();
                cc.enabled = false;
                player.transform.position = new Vector3(60f, 0f, 6.6f);
                cc.enabled = true;
                yield return WaitUntil(() => arena.Boss != null && arena.Boss.IsAwake, 3f);
                arena.Boss.Damageable.ApplyHit(999999);
                yield return WaitUntil(() =>
                {
                    if (arena.IsChoosing) arena.Choose(0);
                    return run.IsEnding;
                }, 6f);
                Assert.IsTrue(run.IsEnding, "보상을 고르면 엔딩");
                Assert.IsFalse(run.IsShowingResult, "결과 화면은 엔딩 뒤");
                Assert.AreEqual(kills == 0, run.EndingFirst);
                Assert.AreEqual(kills == 0 ? 3 : 1, run.EndingLineCount);
                StringAssert.Contains(kills == 0 ? "무릎" : "다시 쓰러진다", run.EndingLine);
                for (int i = 0; i < run.EndingLineCount; i++) run.AdvanceEnding();
                Assert.IsFalse(run.IsEnding);
                yield return WaitUntil(() => run.IsShowingResult, 2f);
                Assert.IsTrue(run.IsShowingResult, "엔딩이 끝나면 결과 화면");
                Assert.AreEqual("천마 격파", run.Result.Where);
            }
        }
    }
}

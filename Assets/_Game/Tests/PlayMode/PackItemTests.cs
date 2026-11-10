using System.Collections;
using AThousandDawns.Combat;
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
    // 기물 꾸러미 1·2 (회귀록 견문 가지): 한기 구슬 · 천리 신발 · 호신 부적 · 서리 갑옷 · 재 항아리
    public class PackItemTests : InputTestFixture
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

        // 기물을 받아 운기 상태가 되는 칸에 놓아요 (주화입마가 아니게)
        OwnedItem Give(string id)
        {
            var def = GameConfig.Items.Find(id);
            Assert.IsNotNull(def, $"{id}가 기물 에셋에 없어요 (시트 가져오기)");
            var item = _player.Items.Add(def);
            Assert.IsNotNull(item);
            var board = _player.Items.Board;
            _player.Gongryeok = 6; // 기 3~6 칸도 생기게
            if (board.Danjeons.Count == 0) _player.Items.AddDanjeon(new Vector2Int(board.Width / 2, board.Height / 2));
            for (int y = 0; y < board.Height; y++)
            for (int x = 0; x < board.Width; x++)
            {
                if (_player.Items.At(new Vector2Int(x, y)) != null && _player.Items.At(new Vector2Int(x, y)) != item) continue;
                _player.Items.Place(item, new Vector2Int(x, y));
                if (PlayerItems.StateOf(item) == QiState.Circulating) return item;
            }
            Assert.Fail($"{id}가 운기되는 칸이 없어요");
            return null;
        }

        Damageable Spawn(Vector3 offset)
        {
            var enemy = _director.Spawn(EnemyKind.Grunt, _player.transform.position + offset);
            enemy.Damageable.ScaleMaxHealth(50f);
            StatusEffects.For(enemy.Damageable).TryFreeze(10f); // 움직이지 않게
            return enemy.Damageable;
        }

        [UnityTest]
        public IEnumerator 한기_구슬은_둔화를_한_중첩_더()
        {
            yield return Load();
            Give("frost_bead");
            Assert.Greater(ElementalMods.Current.SlowExtraChance, 0.2f, "레벨 1이면 25%");

            var enemy = _director.Spawn(EnemyKind.Grunt, _player.transform.position + new Vector3(5f, 0f, 0f)).Damageable;
            ElementalMods.Current.SlowExtraChance = 1f; // 확률 대신 꼭
            StatusEffects.For(enemy).AddSlow(0f, false);
            Assert.AreEqual(2, StatusEffects.For(enemy).SlowStacks);
        }

        [UnityTest]
        public IEnumerator 천리_신발은_대시_한_번_더()
        {
            yield return Load();
            int before = _player.Dash.MaxCharges;
            var boots = GameConfig.Items.Find("cloud_boots");
            Give("cloud_boots");
            yield return null;
            Assert.AreEqual(before + 1, _player.Dash.MaxCharges);
            Assert.AreEqual(3, boots.minQi);
        }

        [UnityTest]
        public IEnumerator 호신_부적은_방을_비울_때마다_보호막이_쌓임()
        {
            yield return Load();
            _player.Shields.Clear();
            _player.OnRoomCleared();
            Assert.AreEqual(0, _player.Shields.Total, "없으면 아무것도");

            int level = PlayerItems.LevelOf(Give("guard_talisman"));
            _player.OnRoomCleared();
            Assert.AreEqual(Mathf.RoundToInt(_player.MaxHealth * 0.05f * level), _player.Shields.Total, "레벨마다 5%");
            for (int i = 0; i < 10; i++) _player.OnRoomCleared();
            Assert.AreEqual(Mathf.RoundToInt(_player.MaxHealth * 0.2f), _player.Shields.Total, "최대 20%");
        }

        [UnityTest]
        public IEnumerator 서리_갑옷은_때린_적을_얼림()
        {
            yield return Load();
            Give("frost_armor"); // 주화입마 기물이 없으면 (0개면) 둔화 대신 빙결
            var enemy = _director.Spawn(EnemyKind.Grunt, _player.transform.position + new Vector3(1f, 0f, 0f)).Damageable;
            yield return new WaitForSeconds(0.6f); // 피격 무적이 없게
            Assert.IsTrue(_player.TryReceiveHit(5f, enemy.transform.position, -1f, enemy));
            Assert.IsTrue(StatusEffects.For(enemy).IsFrozen);
        }

        [UnityTest]
        public IEnumerator 재_항아리는_화상을_옮겨붙임()
        {
            yield return Load();
            Give("ash_urn");
            var dead = Spawn(new Vector3(6f, 0f, 0f));
            var near = Spawn(new Vector3(7f, 0f, 0f));
            var far = Spawn(new Vector3(-6f, 0f, 0f));
            StatusEffects.For(dead).AddBurn(1f, 0, 3);
            Assert.AreEqual(3, StatusEffects.For(dead).BurnStacks);
            dead.ApplyHit(999999);
            yield return null;
            Assert.GreaterOrEqual(StatusEffects.For(near).BurnStacks, 1, "주변 2칸");
            Assert.AreEqual(0, StatusEffects.For(far).BurnStacks, "멀면 안 옮겨요");
        }
    }
}

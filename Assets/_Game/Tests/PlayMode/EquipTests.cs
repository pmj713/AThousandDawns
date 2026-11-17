using System;
using System.Collections;
using System.IO;
using AThousandDawns.Core;
using AThousandDawns.Items;
using AThousandDawns.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AThousandDawns.Tests
{
    // 장비 칸 (차원 겹침 개편): 몸의 혈자리 칸 10개, 기물은 제 부위에만, 칸의 기 = 기본 기 + 공력 + 기혈 (+ 이계 기물 뒤틀림), 보스를 잡으면 칸이 열려요
    public class EquipTests : InputTestFixture
    {
        string _savePath;
        PlayerController _player;
        PlayerItems _items;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            _savePath = Path.Combine(Application.temporaryCachePath, $"meta_test_{Guid.NewGuid():N}.json");
            MetaSave.PathOverride = _savePath;
            PlayerItems.ModeOverride = 1;
        }

        public override void TearDown()
        {
            PlayerItems.ModeOverride = 0;
            MetaSave.PathOverride = null;
            if (File.Exists(_savePath)) File.Delete(_savePath);
            base.TearDown();
        }

        IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("Prototype_Run", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindFirstObjectByType<PlayerController>();
            _items = _player.Items;
        }

        static ItemDefinition Def(string id, EquipSlot slot, int min = 1, int max = 6, bool otherworld = false) =>
            new ItemDefinition { id = id, name = id, keyword = Keyword.Ganggi, slot = slot, minQi = min, maxQi = max, otherworld = otherworld, effectText = "" };

        [UnityTest]
        public IEnumerator 기물은_제_부위의_열린_칸에만_끼워요()
        {
            yield return Load();
            Assert.IsTrue(_items.Board.EquipMode);
            Assert.AreEqual(EquipSlots.Count, _items.Board.Width);
            Assert.AreEqual(0, _items.StartDanjeons, "단전 고르기가 없어요");
            _items.BagCapacity = 4;

            var hat = _items.Add(Def("t_hat", EquipSlot.Head));
            Assert.IsTrue(hat.OnBoard);
            Assert.AreEqual(new Vector2Int(0, 0), hat.Cell, "머리 칸");

            var earring = _items.Add(Def("t_ear", EquipSlot.Earring));
            Assert.IsTrue(earring.OnBoard, "모든 칸이 처음부터 열려 있어요");
            Assert.AreEqual(new Vector2Int(4, 0), earring.Cell, "왼쪽 귀걸이 칸");
            _items.Place(earring, new Vector2Int(0, 0));
            Assert.AreEqual(new Vector2Int(4, 0), earring.Cell, "다른 부위 칸에는 못 끼워요");
            _items.Place(earring, new Vector2Int(5, 0));
            Assert.AreEqual(new Vector2Int(5, 0), earring.Cell, "오른쪽 귀걸이 칸");
        }

        [UnityTest]
        public IEnumerator 같은_키워드_이계_기물_2개_3개면_혼종_세트_단계()
        {
            yield return Load();
            _items.UnlockSlotsThrough(2); // 허리까지 열어요
            Def3 make = (id, slot) => { var d = Def(id, slot, 1, 9, otherworld: true); d.keyword = Keyword.Geumgang; return d; };
            _items.Add(make("t_helm", EquipSlot.Head));
            Assert.AreEqual(1, _items.OtherworldCount(Keyword.Geumgang));
            Assert.AreEqual(0, _items.HybridLevel(Keyword.Geumgang), "1개는 아직");
            _items.Add(make("t_mail", EquipSlot.Top));
            Assert.AreEqual(1, _items.HybridLevel(Keyword.Geumgang), "2개: 미스릴 수호 1단계");
            _items.Add(make("t_belt", EquipSlot.Waist));
            Assert.AreEqual(2, _items.HybridLevel(Keyword.Geumgang), "3개: 2단계");
            Assert.AreEqual(0, _items.HybridLevel(Keyword.Yeolyang));

            var plain = Def("t_plain", EquipSlot.Shoes);
            plain.keyword = Keyword.Geumgang;
            _items.Add(plain);
            Assert.AreEqual(3, _items.OtherworldCount(Keyword.Geumgang), "무협 기물은 세지 않아요");
        }

        delegate ItemDefinition Def3(string id, EquipSlot slot);

        // 회귀록 경맥 가지는 장비 칸에선 부품 대신: 통맥 = 시작 혈자리 +1 · 점혈 = 이계 기물 기 +1 · 한옥 = 모든 기물 최대 기 +1
        [UnityTest]
        public IEnumerator 회귀록_경맥_가지는_장비_칸에선_혈자리_이계_기_최대_기()
        {
            var meta = MetaSave.Load();
            meta.bloomed.AddRange(new[] { "tongmaek", "jeomhyeol", "hanok" });
            meta.Save();
            yield return Load();
            Assert.GreaterOrEqual(_player.Level.Pending, 1, "통맥: 시작 혈자리");
            Assert.AreEqual(1, _items.BookOtherworldQi, "점혈");
            Assert.AreEqual(1, _items.BookMaxQiBonus, "한옥");
            StringAssert.Contains("혈자리", AThousandDawns.Core.RegressionBook.Find("tongmaek").EffectNow);
            Assert.AreEqual("장비창", PlayerItems.ScreenName);
            var hat = _items.Add(Def("t_hat2", EquipSlot.Head, 1, 4));
            Assert.AreEqual(1, hat.MaxQiBonus, "한옥: 최대 기 +1");
        }

        [UnityTest]
        public IEnumerator 칸의_기는_기본_기와_공력_기혈_이계_뒤틀림()
        {
            yield return Load();
            int baseQi = EquipSlots.BaseQi(1); // 상의 (단중)
            var top = _items.Add(Def("t_top", EquipSlot.Top));
            Assert.AreEqual(new Vector2Int(1, 0), top.Cell);
            int power = _player.Gongryeok, start = _items.Board.PowerBase;
            Assert.AreEqual(baseQi + power - start, top.Qi);

            _player.Gongryeok += 1;
            Assert.AreEqual(baseQi + power + 1 - start, top.Qi, "공력이 오르면 모든 칸의 기가 올라요");

            _items.OpenAcupoint(new Vector2Int(1, 0), Acupoint.Gihyeol);
            Assert.AreEqual(baseQi + power + 1 - start + _items.Board.GihyeolQi, top.Qi, "기혈");

            var boots = _items.Add(Def("t_boots", EquipSlot.Shoes, otherworld: true));
            int otherworld = Mathf.RoundToInt(GameConfig.GetOptional("equip.otherworld_qi", 2f));
            Assert.AreEqual(EquipSlots.BaseQi(3) + power + 1 - start + otherworld, boots.Qi, "이계 기물은 혈자리의 기를 뒤틀어 올려요");
        }
    }
}

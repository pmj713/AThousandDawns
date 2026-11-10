using AThousandDawns.Core;

namespace AThousandDawns.Prototype
{
    // 회귀록을 판에 적용 (판을 시작할 때): 주머니 칸, 금창약 지참, 객잔 진열 +1.
    // 회귀록 화면은 결과 화면에서 B로 열어요 (RegressionBookScreen).
    public sealed partial class RoomRun
    {
        int _innStockBonus;
        RegressionBookScreen _book;

        public RegressionBookScreen Book => _book;

        void ApplyBook()
        {
            if (!TryGetComponent(out _book)) _book = gameObject.AddComponent<RegressionBookScreen>();
            var meta = MetaSave.Load();
            if (Player.Items != null) Player.Items.BagCapacity = RegressionBook.BagCapacity(meta);
            for (int i = RegressionBook.StartPotions(meta); i > 0; i--) Player.AddPotion();
            _innStockBonus = RegressionBook.InnStockBonus(meta);
            _unlockedParts = RegressionBook.UnlockedParts(meta);
            _forgeUnlocked = RegressionBook.ForgeUnlocked(meta);
            Player.SetWeapon(meta.weapon == 1 && RegressionBook.DaggerUnlocked(meta) ? AThousandDawns.Player.WeaponKind.Dagger : AThousandDawns.Player.WeaponKind.Sword); // 마을 무기 거치대
            if (meta.runs < UnityEngine.Mathf.RoundToInt(GameConfig.GetOptional("event.deja_runs", 3f))) _usedEvents.Add("deja"); // 기시감은 회귀 3회 이상부터
            ApplyMeditation(meta);
        }
    }
}

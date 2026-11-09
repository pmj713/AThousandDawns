namespace AThousandDawns.Player
{
    // 판 안에서 이벤트가 거는 보정 (이벤트 방). RoomRun이 정하고 층·판이 끝나면 되돌려요.
    public sealed partial class PlayerController
    {
        public float RunMoveBonus { get; set; }      // 산신당 축복: 이동속도 +10% (이번 지역)
        public float RunGaugeBonus { get; set; }     // 산신당 축복: 기세 충전 +15% (이번 지역)
        public float RunTakenIncrease { get; set; }  // 산신의 노여움: 받는 피해 +15% (이번 층)

        float GaugeGainMultiplier => 1f + RunGaugeBonus;

        // 시험판 K 키: 바로 쓰러지기 (결과 화면 확인용)
        public void KillForTest()
        {
            if (IsDead) return;
            Health = 0f;
            if (visual != null) visual.localScale = new UnityEngine.Vector3(1.2f, 0.25f, 1.2f);
        }

        // 버려진 약방 독약: 체력을 직접 깎아요 (쓰러지지는 않아요)
        public void TakeEventDamage(float amount)
        {
            if (IsDead || amount <= 0f) return;
            Health = UnityEngine.Mathf.Max(1f, Health - amount);
        }
    }
}

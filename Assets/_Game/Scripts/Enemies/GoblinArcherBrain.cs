using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 고블린 궁수: 산적 궁수처럼 거리를 두지만, 곧게 쏘지 않고 곡사로 떨어뜨려요 (플레이테스트 11차).
    //   당기는 동안 떨어질 자리 (플레이어 발밑, 움직이면 goblinarcher.lob_lead초만큼 앞, 최대 lob_lead_max칸)를 겨누다 고정하고 (바닥 예고는 없어요, 플레이테스트 11-27),
    //   쏘면 화살이 lob_flight초 동안 포물선으로 날아가며 그 자리에 원(lob_radius)이 차올라요. 떨어지는 순간 원 안의 플레이어만 맞아요 (공격 × lob_ratio).
    //   엄폐물 너머로도 쏴서 사선을 찾아 옮기지 않아요.
    public sealed class GoblinArcherBrain : KiteBrain
    {
        float _lobRadius, _lobFlight, _lobRatio, _lobLead, _lobLeadMax, _lobApex;
        Vector3 _lastPlayer, _playerVelocity;
        bool _tracked;

        public override EnemyKind Kind => EnemyKind.GoblinArcher;
        protected override string Key => "goblinarcher";
        public override string AttackName => "곡사";
        protected override bool NeedsClearLine => false;
        protected override bool RadialTelegraph => true;
        protected override bool WindupTelegraph => false; // 플레이테스트 11-27: 당기는 동안엔 바닥 예고 없이, 쏜 뒤 화살(LobbedArrow)이 떨어질 자리 원을 띄워요

        // 쏜 순간 (아트: 하늘로 당겨 쏘는 자세 · 화살 궤적): 누가, 어디서 (활 높이), 떨어질 자리, 날아가는 시간
        public static event System.Action<GoblinArcherBrain, Vector3, Vector3, float> Lobbed;
        // 떨어진 순간 (아트: 꽂히는 화살 · 흙먼지): 화살, 자리, 반경, 플레이어가 맞았나. 쏜 궁수가 쓰러졌어도 와요 (LobbedArrow.Owner)
        public static event System.Action<LobbedArrow, Vector3, float, bool> LobLanded;
        internal static void RaiseLanded(LobbedArrow arrow, Vector3 at, float radius, bool hit) => LobLanded?.Invoke(arrow, at, radius, hit);

        protected override void ReadShotTuning()
        {
            _lobRadius = Tuning("lob_radius", 1f);
            _lobFlight = Mathf.Max(0.1f, Tuning("lob_flight", 1f));
            _lobRatio = Tuning("lob_ratio", 0.9f);
            _lobLead = Tuning("lob_lead", 0.3f);
            _lobLeadMax = Tuning("lob_lead_max", 1.5f);
            _lobApex = Tuning("lob_apex", 2.5f);
        }

        // 플레이어가 움직이는 빠르기 (앞질러 쏘기용). 둔화와 상관없이 실제 시간으로 재요
        protected override void TickAlways(float dt)
        {
            if (Player == null) return;
            Vector3 p = Player.transform.position;
            float t = Time.deltaTime;
            if (_tracked && t > 1e-5f) _playerVelocity = Vector3.Lerp(_playerVelocity, (p - _lastPlayer) / t, Mathf.Clamp01(t * 10f));
            _lastPlayer = p;
            _tracked = true;
        }

        protected override Vector3 PickAimPoint()
        {
            Vector3 at = TargetPoint;
            if (Rival == null) // 플레이어를 노릴 때만 앞질러요
            {
                Vector3 lead = _playerVelocity * _lobLead;
                lead.y = 0f;
                at += Vector3.ClampMagnitude(lead, _lobLeadMax);
            }
            Vector3 off = at - transform.position;
            off.y = 0f;
            if (off.magnitude > AttackRange) at = transform.position + off.normalized * AttackRange; // 사거리 끝까지만
            return at;
        }

        // 예고: 떨어질 자리의 원 (가운데부터 차올라요)
        protected override System.Collections.Generic.List<Vector2> TelegraphShape() => TelegraphShapes.Circle(_lobRadius);

        protected override void UpdateTelegraph() => Telegraph.SetPose(AimPoint, Vector3.forward);

        protected override void Fire(Vector3 origin, Vector3 direction)
        {
            Vector3 to = new Vector3(AimPoint.x, 0f, AimPoint.z);
            LobbedArrow.Launch(Director, origin, to, _lobFlight, _lobRadius, AttackPower * _lobRatio, _lobApex, Body);
            Sfx.Play(SfxId.Arrow);
            Lobbed?.Invoke(this, origin, to, _lobFlight);
        }
    }

}

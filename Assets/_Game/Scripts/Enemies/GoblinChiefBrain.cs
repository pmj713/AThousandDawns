using UnityEngine;

namespace AThousandDawns.Enemies
{
    // 고블린 족장 (차원 겹침, 판타지 고수): 철권 마웅과 같은 규칙 (결투장, 50%에서 2페이즈, 같은 패턴 연속 2번까지). 패턴은 겹치지 않게 저만의 것:
    //   철퇴 휘돌리기 (가까울 때): 앞 220° 넓은 부채꼴 한 번, 맞으면 크게 밀려나요
    //   덫 던지기 (중간 거리): goblinchief.trap_interval마다 플레이어 둘레에 덫 셋. 떨어질 자리에 예고 원이 trap_fall초 차오른 뒤 덫 (밟으면 거의 못 움직여요)
    //   점프 덮치기 (멀 때): 내려앉을 자리 예고 원 → 그 자리로 뛰어 내려찍기
    //   함성: 2페이즈에 들어설 때 고블린을 goblinchief.summon_count만큼 불러 모아요 (부하는 지역 배율을 받아요)
    public sealed class GoblinChiefBrain : IronFistBrain
    {
        public override EnemyKind Kind => EnemyKind.GoblinChief;
        protected override string Key => "goblinchief";
        protected override string JabName => "철퇴 휘돌리기";
        protected override string FarLeapName => "점프 덮치기";
        protected override MeleeKind Melee => MeleeKind.Sweep;
        protected override SpecialKind Special => SpecialKind.None;
        protected override FarKind Far => FarKind.Leap;

        public static event System.Action<GoblinChiefBrain> WarCry;               // 함성 (아트: 붉은 음파 고리)
        public static event System.Action<GoblinChiefBrain, Vector3> TrapThrown; // 덫 하나가 떨어진 자리 (아트)
        public static event System.Action<GoblinChiefBrain, Vector3, GhostFire> TrapLanded; // 덫이 실제로 깔린 순간 (자리, 덫 장판) — 이름으로 찾지 않아도 돼요

        float _trapTimer = 2f;
        readonly System.Collections.Generic.List<(Vector3 at, float timer, Telegraph marker)> _falling = new System.Collections.Generic.List<(Vector3, float, Telegraph)>();

        protected override void TickMore(float dt)
        {
            if (_trapTimer > 0f) _trapTimer -= dt;
            float fall = Mathf.Max(0.01f, Tuning("trap_fall", 0.7f));
            for (int i = _falling.Count - 1; i >= 0; i--) // 덫이 떨어질 자리 예고 원 → 차오르면 덫
            {
                var (at, timer, marker) = _falling[i];
                timer -= dt;
                if (marker != null) marker.SetProgress(1f - Mathf.Clamp01(timer / fall));
                if (timer > 0f) { _falling[i] = (at, timer, marker); continue; }
                if (marker != null) Destroy(marker.gameObject);
                _falling.RemoveAt(i);
                float radius = Tuning("trap_radius", 0.9f), life = Tuning("trap_life", 6f), slow = Tuning("trap_slow", 0.8f), slowTime = Tuning("trap_slow_time", 1.2f);
                var trap = GhostFire.Create(at, radius, life, AttackPower * Tuning("trap_ratio", 0.3f), slow, slowTime, Player, Body);
                trap.name = "덫";
                TrapLanded?.Invoke(this, at, trap);
            }
        }

        protected override void TryMidRange()
        {
            if (_trapTimer > 0f || Attack.IsAttacking) return;
            _trapTimer = Tuning("trap_interval", 6f);
            int count = Mathf.Max(1, Mathf.RoundToInt(Tuning("trap_count", 3f)));
            float radius = Tuning("trap_radius", 0.9f), spread = Tuning("trap_spread", 2f), fall = Tuning("trap_fall", 0.7f);
            Vector3 p = Player.transform.position;
            float start = Random.Range(0f, 360f);
            for (int i = 0; i < count; i++)
            {
                Vector3 at = i == 0 ? p : p + Quaternion.Euler(0f, start + i * 360f / count, 0f) * Vector3.forward * spread;
                at = Clamp(at);
                var marker = Telegraph.Create(null, Director.Visuals.telegraphBase, Director.Visuals.telegraphFill, Director.Visuals.telegraphOutline);
                marker.Show(TelegraphShapes.Circle(radius), true); // 어디에 깔리는지 먼저 보여줘요
                marker.SetPose(at, Vector3.forward);
                marker.SetProgress(0f);
                _falling.Add((at, fall, marker));
                TrapThrown?.Invoke(this, at); // 던지는 순간 (떨어질 자리)
            }
        }

        protected override void OnDiedMore()
        {
            foreach (var f in _falling) if (f.marker != null) Destroy(f.marker.gameObject);
            _falling.Clear();
        }

        protected override void OnEnterPhase2()
        {
            WarCry?.Invoke(this);
            int count = Mathf.RoundToInt(Tuning("summon_count", 3f));
            for (int i = 0; i < count; i++)
            {
                var at = Director.FindFreeSpot(transform.position + Quaternion.Euler(0f, i * 360f / count, 0f) * Vector3.forward * 2.5f, 0.5f);
                Director.SpawnSummon(EnemyKind.Goblin, at);
            }
        }
    }
}

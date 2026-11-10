using AThousandDawns.Combat;
using AThousandDawns.Core;
using AThousandDawns.Player;
using UnityEngine;

namespace AThousandDawns.Prototype
{
    // 이미 있는 이벤트로 낼 수 있는 효과음 (게임 코드를 고치지 않고 듣기만 해요).
    //   적 피격(경직 있는 타격만) · 적 처치 · 빙결 · 휘두르기 · 플레이어 피격 · 호신강기 깨짐 · 레벨 업
    // 나머지(대시·화살·폭발·예고 등)는 그 코드에서 Sfx.Play를 바로 불러요.
    public sealed class SfxHooks : MonoBehaviour
    {
        PlayerController _player;

        void OnEnable()
        {
            Damageable.AnyDamaged += OnAnyDamaged;
            Damageable.AnyDied += OnAnyDied;
            StatusEffects.AnyFrozen += OnAnyFrozen;
        }

        void OnDisable()
        {
            Damageable.AnyDamaged -= OnAnyDamaged;
            Damageable.AnyDied -= OnAnyDied;
            StatusEffects.AnyFrozen -= OnAnyFrozen;
            Unhook();
        }

        bool _wasDead;

        // 플레이어는 씬이 시작된 뒤에 준비될 수 있어서 찾을 때까지 매 프레임 확인해요
        void Update()
        {
            if (_player != null)
            {
                // 쓰러짐: 프레임 끝에 쓰러져 있을 때만 (같은 프레임에 기사회생하면 안 나요)
                bool dead = _player.IsDead;
                if (dead && !_wasDead) Sfx.Play(SfxId.PlayerDeath, 0.45f);
                _wasDead = dead;
                return;
            }
            _player = FindAnyObjectByType<PlayerController>();
            if (_player == null || _player.Combo == null) { _player = null; return; }
            _player.Hurt += OnHurt;
            _player.Combo.SwingStarted += OnSwing;
            if (_player.Shields != null) _player.Shields.Broken += OnShieldBroken;
            if (_player.Level != null) _player.Level.LeveledUp += OnLeveledUp;
        }

        void Unhook()
        {
            if (_player == null) return;
            _player.Hurt -= OnHurt;
            if (_player.Combo != null) _player.Combo.SwingStarted -= OnSwing;
            if (_player.Shields != null) _player.Shields.Broken -= OnShieldBroken;
            if (_player.Level != null) _player.Level.LeveledUp -= OnLeveledUp;
            _player = null;
        }

        static void OnAnyDamaged(Damageable target, int amount)
        {
            if (target.LastHitReacts) Sfx.Play(SfxId.Hit); // 화상·번개 같은 파생 피해는 빼요
        }

        static void OnAnyDied(Damageable target) => Sfx.Play(SfxId.EnemyDeath);
        static void OnAnyFrozen(StatusEffects target) => Sfx.Play(SfxId.Freeze);
        static void OnSwing(int index) => Sfx.Play(SfxId.Swing, index); // 1·2·3타마다 다른 소리
        static void OnHurt(int dealt) { if (dealt > 0) Sfx.Play(SfxId.Hurt); }
        static void OnShieldBroken(int left) => Sfx.Play(SfxId.ShieldBreak);
        static void OnLeveledUp() => Sfx.Play(SfxId.LevelUp);
    }
}

using AThousandDawns.Core;
using UnityEngine;

namespace AThousandDawns.Player
{
    // 금창약 (조작 시트): R을 0.5초 누르고 있으면 최대 체력의 40% 회복. 누르는 동안 이동 50%, 평타·초식 불가.
    // 손을 떼거나 대시하거나 맞으면 취소되고 소모되지 않아요. 다 마신 뒤 1초 동안은 다시 못 마셔요.
    // 시작 0개, 최대 3개. 객잔과 이벤트(떠돌이 의원 · 버려진 약방)에서 얻어요.
    public sealed partial class PlayerController
    {
        bool _potionTuned;
        int _potionMax;
        float _potionHold, _potionHeal, _potionMove, _potionReuse;
        float _potionTimer = -1f, _potionCooldown;

        public int Potions { get; private set; }
        public int PotionMax { get { TunePotion(); return _potionMax; } }
        public bool IsDrinking => _potionTimer >= 0f;
        public float PotionProgress => IsDrinking ? Mathf.Clamp01(_potionTimer / Mathf.Max(0.01f, _potionHold)) : 0f;

        void TunePotion()
        {
            if (_potionTuned) return;
            _potionTuned = true;
            _potionMax = Mathf.RoundToInt(GameConfig.Get("potion.max", 3f));
            _potionHold = GameConfig.Get("potion.hold", 0.5f);
            _potionHeal = GameConfig.Get("potion.heal", 0.4f);
            _potionMove = GameConfig.Get("potion.move_ratio", 0.5f);
            _potionReuse = GameConfig.Get("potion.reuse", 1f);
            Potions = Mathf.Min(_potionMax, Mathf.RoundToInt(GameConfig.Get("potion.start", 0f)));
        }

        // 최대 소지면 false
        public bool AddPotion()
        {
            TunePotion();
            if (Potions >= _potionMax) return false;
            Potions++;
            return true;
        }

        void CancelPotion() => _potionTimer = -1f;

        // 마시는 중이면 true (이번 프레임의 다른 행동을 막아요). 대시를 누르면 취소하고 대시로 넘어가요.
        bool TickPotion(float dt)
        {
            TunePotion();
            if (_potionCooldown > 0f) _potionCooldown -= dt;

            if (!IsDrinking)
            {
                bool idle = !_combo.IsAttacking && !IsCastingSkill;
                if (!_input.PotionPressed || Potions <= 0 || _potionCooldown > 0f || !idle) return false;
                _potionTimer = 0f;
            }

            if (!_input.PotionHeld || _sinceDashPress <= _inputBuffer)
            {
                CancelPotion();
                return false;
            }

            _potionTimer += dt;
            UpdateMovement(dt);
            if (_potionTimer < _potionHold) return true;

            Potions--;
            Sfx.Play(SfxId.PotionDrink);
            Heal(MaxHealth * _potionHeal);
            CancelPotion();
            _potionCooldown = _potionReuse;
            return true;
        }
    }
}

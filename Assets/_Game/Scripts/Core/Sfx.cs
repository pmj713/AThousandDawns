using System;
using UnityEngine;

namespace AThousandDawns.Core
{
    // 새 소리는 맨 뒤에 더해요 (씬에 저장된 순서가 이 순서예요)
    public enum SfxId
    {
        Swing, Hit, Finisher, Dash, Hurt, EnemyDeath, Burn, Freeze, Zap, ShieldBreak,
        Explosion, Arrow, JarBreak, Coin, LevelUp, Telegraph, BossPhase, RoomClear, UiMove, UiConfirm,
        Spin,
        PerfectDodge, DashAttack, Ultimate, SwordLand, DaggerThrow, DaggerRecall, BambooFlash, Quake,
        BoltFire, BoltExplode, PlayerDeath, Revive, PotionDrink,
        HornBlow, BoarCharge, BoarCrash, HoundBark, HoundBite, BushRustle, SnakeStrike, FuseHiss, ShieldBlock,
        AssassinAppear, BossCharge, BossSummon, BossDeath, GroveIgnite, GroveShrink,
    }

    // 효과음 (VARCO Sound, Audio/SFX). 씬마다 하나 두고 어디서든 Sfx.Play(SfxId.X)로 불러요.
    // 한 소리에 여러 버전이 있으면 무작위로 하나 (평타는 몇 번째 타인지로 고를 수 있어요).
    // 같은 소리가 한꺼번에 여러 번 나면 시끄러워서 소리마다 최소 간격을 둬요. 소리가 없으면(테스트 등) 그냥 넘어가요.
    // 음량 설정이 생기면 Volume에 곱해요.
    public sealed class Sfx : MonoBehaviour
    {
        [Serializable]
        public sealed class Sound
        {
            public AudioClip[] clips;
        }

        [SerializeField] Sound[] sounds = new Sound[Enum.GetValues(typeof(SfxId)).Length]; // SfxId 순서
        [SerializeField] int voices = 8;
        [SerializeField] float minInterval = 0.05f;

        const float Volume = 0.8f;

        static Sfx _instance;
        AudioSource[] _sources;
        int _next;
        float[] _lastPlayed;

        void Awake()
        {
            _sources = new AudioSource[Mathf.Max(1, voices)];
            for (int i = 0; i < _sources.Length; i++)
            {
                _sources[i] = gameObject.AddComponent<AudioSource>();
                _sources[i].playOnAwake = false;
            }
            _lastPlayed = new float[sounds.Length];
            for (int i = 0; i < _lastPlayed.Length; i++) _lastPlayed[i] = -1f;
        }

        void OnEnable() => _instance = this;
        void OnDisable() { if (_instance == this) _instance = null; }

        public static void Play(SfxId id, float volume = 1f) => Play(id, -1, volume);

        // variant: 몇 번째 버전 (넘치면 처음부터 돌아요). 음수면 무작위
        public static void Play(SfxId id, int variant, float volume = 1f)
        {
            if (_instance != null) _instance.PlayOne(id, variant, volume, true);
        }

        // 최소 간격 없이 매번 (만검귀종 검처럼 하나하나 소리가 나야 할 때)
        public static void PlayEach(SfxId id, int variant, float volume = 1f)
        {
            if (_instance != null) _instance.PlayOne(id, variant, volume, false);
        }

        // 도중에 멈출 수 있는 소리 (나팔처럼 긴 소리). 돌려받은 핸들로 Stop을 불러요
        public static AudioSource PlayStoppable(SfxId id, float volume = 1f)
        {
            if (_instance == null) return null;
            var clip = _instance.Pick(id, -1);
            if (clip == null) return null;
            var source = _instance.gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.clip = clip;
            source.volume = volume * Volume;
            source.Play();
            Destroy(source, clip.length + 0.1f);
            return source;
        }

        public static void Stop(AudioSource handle)
        {
            if (handle != null) Destroy(handle);
        }

        public static int VariantCount(SfxId id)
        {
            if (_instance == null) return 0;
            int i = (int)id;
            return i < _instance.sounds.Length && _instance.sounds[i]?.clips != null ? _instance.sounds[i].clips.Length : 0;
        }

        AudioClip Pick(SfxId id, int variant)
        {
            int i = (int)id;
            var clips = i < sounds.Length ? sounds[i]?.clips : null;
            if (clips == null || clips.Length == 0) return null;
            return clips[variant >= 0 ? variant % clips.Length : UnityEngine.Random.Range(0, clips.Length)];
        }

        void PlayOne(SfxId id, int variant, float volume, bool throttle)
        {
            int i = (int)id;
            var clip = Pick(id, variant);
            if (clip == null) return;
            float now = Time.unscaledTime;
            if (throttle && _lastPlayed[i] >= 0f && now - _lastPlayed[i] < minInterval) return;
            _lastPlayed[i] = now;
            var source = _sources[_next];
            _next = (_next + 1) % _sources.Length;
            source.pitch = UnityEngine.Random.Range(0.95f, 1.05f); // 같은 소리가 반복돼도 덜 기계적으로
            source.PlayOneShot(clip, volume * Volume);
        }
    }
}

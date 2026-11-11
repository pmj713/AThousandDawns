using UnityEngine;

namespace AThousandDawns.Prototype
{
    // 판 BGM (VARCO Sound, Audio/BGM). RoomRun 단계를 보고 곡을 골라 짧게 엇갈려 바꿔요.
    //   마을 → village · 죽림(갈림길·층·고수) → bamboo · 객잔·정비실 → inn · 녹림왕(격파 후 포함) → boss · 결과 창 → result
    // 음량: 설정의 음악 비율 (Volume). 전체 음량은 AudioListener.
    [RequireComponent(typeof(RoomRun))]
    public sealed class RunMusic : MonoBehaviour
    {
        [SerializeField] AudioClip village, bamboo, inn, boss, result;
        [SerializeField] float fadeSeconds = 1.2f;

        static float Volume => AThousandDawns.Core.GameSettings.Current.MusicScale; // 설정 → 음악 (바꾸면 바로 따라가요)

        RoomRun _run;
        AudioSource _current, _previous;

        public AudioClip Playing => _current != null ? _current.clip : null;

        void Awake()
        {
            _run = GetComponent<RoomRun>();
            if (FindAnyObjectByType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
        }

        public AudioClip Pick()
        {
            if (_run.IsShowingResult) return result;
            return _run.CurrentPhase switch
            {
                RoomRun.Phase.Village => village,
                RoomRun.Phase.Fork or RoomRun.Phase.Floor or RoomRun.Phase.Master => bamboo,
                RoomRun.Phase.Inn or RoomRun.Phase.Rest => inn,
                RoomRun.Phase.Boss or RoomRun.Phase.Done => boss,
                _ => null,
            };
        }

        void Update()
        {
            var want = Pick();
            if (want != Playing) Switch(want);

            // 일시정지(timeScale 0)에도 페이드가 멈추지 않게 unscaled
            float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeSeconds);
            if (_current != null) _current.volume = Mathf.MoveTowards(_current.volume, Volume, step);
            if (_previous != null)
            {
                _previous.volume = Mathf.MoveTowards(_previous.volume, 0f, step);
                if (_previous.volume <= 0f) { Destroy(_previous); _previous = null; }
            }
        }

        void Switch(AudioClip clip)
        {
            if (_previous != null) Destroy(_previous);
            _previous = _current;
            _current = null;
            if (clip == null) return;
            _current = gameObject.AddComponent<AudioSource>();
            _current.clip = clip;
            _current.loop = true;
            _current.volume = 0f;
            _current.Play();
        }
    }
}

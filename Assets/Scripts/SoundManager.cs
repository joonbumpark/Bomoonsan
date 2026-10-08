using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace Mountains
{
    // 배경음(BGM)과 효과음(SFX)을 한 곳에서 재생하는 싱글턴.
    //
    // - BGM: AudioSource 두 개를 번갈아 써서 곡이 바뀔 때 크로스페이드한다.
    // - SFX: AudioSource 풀을 돌려 쓴다. 클립마다 AudioSource를 새로 만들지 않고, 동시에
    //   여러 소리가 겹쳐도 되며, 풀이 꽉 차면 가장 오래된 소리부터 끊고 재사용한다.
    // - 볼륨은 마스터 x (BGM 또는 SFX) x 곡/소리별 볼륨으로 곱해진다.
    //
    // 씬에 미리 배치해두면 인스펙터에서 볼륨을 조절할 수 있고, 배치하지 않아도 처음 쓰는
    // 순간 GetOrCreate()가 기본값으로 만든다(ToastUI와 같은 방식). 씬이 바뀌어도 살아남고,
    // 새 씬에 또 배치돼 있으면 나중 것이 스스로 사라져 하나만 남는다.
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance { get; private set; }

        const string MasterKey = "Sound.Master";
        const string BgmKey = "Sound.Bgm";
        const string SfxKey = "Sound.Sfx";

        [Header("볼륨")]
        [Range(0f, 1f)] public float masterVolume = 1f;
        [Range(0f, 1f)] public float bgmVolume = 0.7f;
        [Range(0f, 1f)] public float sfxVolume = 1f;

        [Header("효과음")]
        [Tooltip("동시에 낼 수 있는 효과음 개수. 넘치면 가장 오래된 소리부터 끊긴다.")]
        [Min(1)] public int sfxVoices = 8;

        [Header("수명")]
        [Tooltip("켜두면 씬을 넘어가도 BGM이 끊기지 않는다.")]
        public bool persistAcrossScenes = true;

        // BGM 한 트랙. fade는 크로스페이드용 0~1 배수이고, baseVolume은 곡마다 따로 주는 볼륨이다.
        class BgmTrack
        {
            public AudioSource source;
            public float baseVolume = 1f;
            public float fade;
            public Tween tween;
        }

        readonly BgmTrack[] _tracks = new BgmTrack[2];
        readonly List<AudioSource> _sfxPool = new List<AudioSource>();
        int _activeTrack = -1;
        int _nextSteal;
        bool _initialized;

        public bool IsBgmPlaying =>
            _activeTrack >= 0 && _tracks[_activeTrack].source != null && _tracks[_activeTrack].source.isPlaying;

        public AudioClip CurrentBgm => _activeTrack >= 0 ? _tracks[_activeTrack].source.clip : null;

        void Awake()
        {
            // 새 씬에 또 배치된 매니저는 기존 것에 자리를 양보한다.
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (persistAcrossScenes)
            {
                // DontDestroyOnLoad는 루트 오브젝트에만 먹는다.
                if (transform.parent != null)
                {
                    transform.SetParent(null);
                }
                DontDestroyOnLoad(gameObject);
            }

            LoadSavedVolumes();
            Initialize();
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public static SoundManager GetOrCreate()
        {
            if (Instance != null)
            {
                return Instance;
            }

            // AddComponent 안에서 Awake가 바로 돌아 Instance가 채워진다.
            return new GameObject("SoundManager").AddComponent<SoundManager>();
        }

        void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            for (int i = 0; i < _tracks.Length; i++)
            {
                var source = CreateSource($"Bgm{i}");
                source.loop = true;
                _tracks[i] = new BgmTrack { source = source };
            }

            for (int i = 0; i < Mathf.Max(1, sfxVoices); i++)
            {
                _sfxPool.Add(CreateSource($"Sfx{i}"));
            }

            _initialized = true;
        }

        AudioSource CreateSource(string sourceName)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(transform, false);

            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            return source;
        }

        // ───────────── BGM ─────────────

        // 곡을 재생한다. 이미 재생 중인 곡이 있으면 fadeSeconds 동안 크로스페이드한다.
        // 같은 곡이 이미 재생 중이면 처음부터 다시 틀지 않고 볼륨만 맞춘다.
        // 돌려주는 값은 페이드에 걸리는 시간(초)이다.
        public float PlayBgm(AudioClip clip, float volume = 1f, float fadeSeconds = 1f, bool loop = true)
        {
            if (clip == null)
            {
                return 0f;
            }

            Initialize();
            volume = Mathf.Clamp01(volume);
            fadeSeconds = Mathf.Max(0f, fadeSeconds);

            if (_activeTrack >= 0)
            {
                var current = _tracks[_activeTrack];
                if (current.source.clip == clip && current.source.isPlaying)
                {
                    current.baseVolume = volume;
                    current.source.loop = loop;
                    return 0f;
                }

                FadeTrack(current, 0f, fadeSeconds, stopWhenSilent: true);
            }

            int next = _activeTrack == 0 ? 1 : 0;
            var track = _tracks[next];

            track.tween?.Kill();
            track.source.Stop();
            track.source.clip = clip;
            track.source.loop = loop;
            track.baseVolume = volume;
            track.fade = 0f;
            track.source.volume = 0f;
            track.source.Play();

            FadeTrack(track, 1f, fadeSeconds, stopWhenSilent: false);
            _activeTrack = next;
            return fadeSeconds;
        }

        // 재생 중인 BGM을 fadeSeconds에 걸쳐 줄이고 멈춘다.
        public float StopBgm(float fadeSeconds = 1f)
        {
            if (_activeTrack < 0)
            {
                return 0f;
            }

            fadeSeconds = Mathf.Max(0f, fadeSeconds);
            FadeTrack(_tracks[_activeTrack], 0f, fadeSeconds, stopWhenSilent: true);
            _activeTrack = -1;
            return fadeSeconds;
        }

        void FadeTrack(BgmTrack track, float target, float seconds, bool stopWhenSilent)
        {
            track.tween?.Kill();

            if (seconds <= 0f)
            {
                track.fade = target;
                if (stopWhenSilent && target <= 0f)
                {
                    track.source.Stop();
                }
                return;
            }

            // SetUpdate(true): 일시정지(timeScale 0) 중에도 페이드가 흘러야 한다.
            track.tween = DOTween.To(() => track.fade, v => track.fade = v, target, seconds)
                .SetEase(Ease.Linear)
                .SetUpdate(true)
                .SetLink(gameObject)
                .OnComplete(() =>
                {
                    if (stopWhenSilent && target <= 0f)
                    {
                        track.source.Stop();
                    }
                });
        }

        // 슬라이더로 볼륨을 바꿔도 재생 중인 BGM에 바로 반영되도록 매 프레임 곱해 적용한다.
        void Update()
        {
            if (!_initialized)
            {
                return;
            }

            foreach (var track in _tracks)
            {
                if (track.source.isPlaying)
                {
                    track.source.volume = track.fade * track.baseVolume * bgmVolume * masterVolume;
                }
            }
        }

        // ───────────── SFX ─────────────

        // 효과음을 한 번 재생한다. position을 주면 그 자리에서 나는 3D 소리, 비우면 화면 전체에
        // 깔리는 2D 소리다. 재생에 쓴 AudioSource를 돌려주므로 끝나는 시점을 알고 싶으면 쓰면 된다.
        public AudioSource PlaySfx(AudioClip clip, float volume = 1f, float pitch = 1f, Vector3? position = null)
        {
            if (clip == null)
            {
                return null;
            }

            Initialize();

            var source = AcquireSfxSource();
            source.Stop();
            source.clip = clip;
            source.volume = Mathf.Clamp01(volume) * sfxVolume * masterVolume;
            source.pitch = Mathf.Max(0.01f, pitch);

            if (position.HasValue)
            {
                source.transform.position = position.Value;
                source.spatialBlend = 1f;
            }
            else
            {
                source.spatialBlend = 0f;
            }

            source.Play();
            return source;
        }

        public void StopAllSfx()
        {
            foreach (var source in _sfxPool)
            {
                source.Stop();
            }
        }

        AudioSource AcquireSfxSource()
        {
            foreach (var source in _sfxPool)
            {
                if (!source.isPlaying)
                {
                    return source;
                }
            }

            // 전부 쓰는 중이면 돌아가며 하나를 끊고 가져온다.
            var stolen = _sfxPool[_nextSteal];
            _nextSteal = (_nextSteal + 1) % _sfxPool.Count;
            return stolen;
        }

        // ───────────── 볼륨 ─────────────
        // 옵션 팝업의 슬라이더에 바로 연결할 수 있게 값을 저장까지 해준다.

        public void SetMasterVolume(float value)
        {
            masterVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MasterKey, masterVolume);
        }

        public void SetBgmVolume(float value)
        {
            bgmVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(BgmKey, bgmVolume);
        }

        public void SetSfxVolume(float value)
        {
            sfxVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(SfxKey, sfxVolume);
        }

        // 저장된 값이 있을 때만 덮어쓴다 — 처음 실행할 때는 인스펙터에 정해둔 값이 그대로 쓰인다.
        void LoadSavedVolumes()
        {
            if (PlayerPrefs.HasKey(MasterKey)) masterVolume = PlayerPrefs.GetFloat(MasterKey);
            if (PlayerPrefs.HasKey(BgmKey)) bgmVolume = PlayerPrefs.GetFloat(BgmKey);
            if (PlayerPrefs.HasKey(SfxKey)) sfxVolume = PlayerPrefs.GetFloat(SfxKey);
        }

        // 이 프로젝트는 Enter Play Mode Options에서 도메인 리로드를 꺼둬서 static 값이
        // Play 세션 사이에 남는다 — 다른 싱글턴들과 같은 이유로 초기화한다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            Instance = null;
        }
    }
}

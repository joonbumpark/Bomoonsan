using System.Collections.Generic;
using UnityEngine;

namespace Match3
{
    /// <summary>효과음 종류. 실제 파일은 Assets/Resources/Sounds/Sfx/&lt;FileName&gt;.ogg.</summary>
    public enum Sfx
    {
        Click,
        Match3Swap,
        Match3SwapFail,
        Match3Match,
        Match3Item,
        QuizCorrect,
        QuizWrong,
        JigsawSelect,
        JigsawSwap,
        JigsawComplete,
        RoundEnd,
    }

    /// <summary>배경음악 종류. 실제 파일은 Assets/Resources/Sounds/Bgm/&lt;FileName&gt;.ogg.</summary>
    public enum Bgm
    {
        Lobby,
        Match3,
        CountryQuiz,
        Jigsaw,
    }

    /// <summary>
    /// 미니게임 효과음/배경음악 재생. 씬에 미리 배치할 필요 없이 처음 부를 때 스스로
    /// 만들어진다 (InGameScene 안에서만 살고, 씬이 바뀌면 같이 사라진다).
    ///
    /// 소리를 바꾸고 싶으면 Assets/Resources/Sounds 아래 같은 이름의 파일만 교체하면 된다
    /// (출처/라이선스는 같은 폴더의 CREDITS.txt).
    /// </summary>
    public class SoundManager : MonoBehaviour
    {
        private const float SfxVolume = 0.8f;
        private const float BgmVolume = 0.45f;

        private static SoundManager instance;

        private AudioSource sfxSource;
        private AudioSource pitchedSfxSource; // 연쇄 콤보처럼 음 높이를 바꿔 내는 소리 전용
        private AudioSource bgmSource;
        private readonly Dictionary<string, AudioClip> clipCache = new Dictionary<string, AudioClip>();
        private Bgm? currentBgm;

        private static SoundManager Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("SoundManager");
                    instance = go.AddComponent<SoundManager>();
                }
                return instance;
            }
        }

        private void Awake()
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;

            pitchedSfxSource = gameObject.AddComponent<AudioSource>();
            pitchedSfxSource.playOnAwake = false;

            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.playOnAwake = false;
            bgmSource.loop = true;
            bgmSource.volume = BgmVolume;
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        /// <summary>효과음 한 번 재생. pitch로 연쇄 콤보처럼 같은 소리를 점점 높게 낼 수 있다.</summary>
        public static void Play(Sfx sfx, float pitch = 1f)
        {
            var self = Instance;
            var clip = self.Load("Sfx/" + FileName(sfx));
            if (clip == null)
                return;

            // PlayOneShot은 소스의 pitch를 따르므로, 기본 음높이 소리와 음높이를 바꾼 소리를
            // 다른 소스로 나눠서 서로의 pitch를 건드리지 않게 한다.
            if (Mathf.Approximately(pitch, 1f))
            {
                self.sfxSource.PlayOneShot(clip, SfxVolume);
            }
            else
            {
                self.pitchedSfxSource.pitch = pitch;
                self.pitchedSfxSource.PlayOneShot(clip, SfxVolume);
            }
        }

        /// <summary>배경음악을 바꾼다. 이미 같은 곡이 나오고 있으면 처음부터 다시 틀지 않는다.</summary>
        public static void PlayBgm(Bgm bgm)
        {
            var self = Instance;
            if (self.currentBgm == bgm && self.bgmSource.isPlaying)
                return;

            var clip = self.Load("Bgm/" + FileName(bgm));
            if (clip == null)
                return;

            self.currentBgm = bgm;
            self.bgmSource.clip = clip;
            self.bgmSource.Play();
        }

        public static void StopBgm()
        {
            if (instance == null)
                return;
            instance.currentBgm = null;
            instance.bgmSource.Stop();
        }

        private AudioClip Load(string path)
        {
            if (!clipCache.TryGetValue(path, out var clip))
            {
                clip = Resources.Load<AudioClip>("Sounds/" + path);
                if (clip == null)
                    Debug.LogWarning($"[SoundManager] 소리 파일이 없음: Resources/Sounds/{path}");
                clipCache[path] = clip;
            }
            return clip;
        }

        private static string FileName(Sfx sfx) => sfx switch
        {
            Sfx.Click => "click",
            Sfx.Match3Swap => "match3_swap",
            Sfx.Match3SwapFail => "match3_swap_fail",
            Sfx.Match3Match => "match3_match",
            Sfx.Match3Item => "match3_item",
            Sfx.QuizCorrect => "quiz_correct",
            Sfx.QuizWrong => "quiz_wrong",
            Sfx.JigsawSelect => "jigsaw_select",
            Sfx.JigsawSwap => "jigsaw_swap",
            Sfx.JigsawComplete => "jigsaw_complete",
            Sfx.RoundEnd => "round_end",
            _ => sfx.ToString(),
        };

        private static string FileName(Bgm bgm) => bgm switch
        {
            Bgm.Lobby => "lobby",
            Bgm.Match3 => "match3",
            Bgm.CountryQuiz => "country_quiz",
            Bgm.Jigsaw => "jigsaw",
            _ => bgm.ToString(),
        };
    }
}

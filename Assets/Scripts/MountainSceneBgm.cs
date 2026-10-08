using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mountains
{
    // Mountain 씬이 로드되면 sndBGM을 자동으로 튼다. 씬 파일을 건드리지 않고 동작하도록
    // 씬 이름으로 연결한다 — 곡/볼륨을 바꾸려면 아래 상수만 고치면 된다.
    // (이후 연출 중 곡을 바꾸고 싶으면 트리거에 PlaySoundAction(BgmPlay)을 쓰면 크로스페이드로 교체된다.)
    public static class MountainSceneBgm
    {
        const string ClipPath = "Sounds/Bgm/sndBGM"; // Assets/Resources/Sounds/Bgm/sndBGM.mp3
        const float Volume = 0.45f;
        const float FadeSeconds = 1.5f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Register()
        {
            SceneBootstrap.OnSceneReady(nameof(MountainSceneBgm), SceneBootstrap.MountainScene, Apply);
        }

        static void Apply(Scene scene)
        {
            var clip = Resources.Load<AudioClip>(ClipPath);
            if (clip == null)
            {
                Debug.LogWarning($"[MountainSceneBgm] BGM 파일이 없음: Resources/{ClipPath}");
                return;
            }

            var manager = SoundManager.GetOrCreate();
            manager.PlayBgm(clip, Volume, FadeSeconds);
            Debug.Log($"[MountainSceneBgm] '{clip.name}' ({clip.length:0.#}s, {clip.loadState}) 재생 요청 — " +
                $"master {manager.masterVolume}, bgm {manager.bgmVolume}, listener volume {AudioListener.volume}, " +
                $"playing {manager.IsBgmPlaying}");
        }
    }
}

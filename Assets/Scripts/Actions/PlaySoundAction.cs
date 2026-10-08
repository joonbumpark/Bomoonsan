using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Mountains
{
    // 사운드를 재생하는 액션. 효과음 한 번, 배경음 시작, 배경음 정지를 모드로 고른다.
    // 실제 재생은 SoundManager가 하고, 씬에 매니저가 없으면 처음 쓰는 순간 만들어진다.
    [Serializable]
    public class PlaySoundAction : TriggerAction
    {
        public enum Mode
        {
            [Tooltip("효과음을 한 번 재생한다.")] Sfx,
            [Tooltip("배경음을 틀거나 다른 곡으로 크로스페이드한다.")] BgmPlay,
            [Tooltip("재생 중인 배경음을 서서히 줄이며 멈춘다.")] BgmStop
        }

        public Mode mode = Mode.Sfx;

        [Tooltip("재생할 사운드. BgmStop에서는 쓰지 않는다.")]
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;

        [Header("효과음 (Sfx)")]
        [Tooltip("1이 원래 속도. 높으면 빠르고 높은 소리, 낮으면 느리고 낮은 소리.")]
        [Range(0.1f, 3f)] public float pitch = 1f;
        [Tooltip("재생할 때마다 pitch를 이 폭만큼 무작위로 흔든다. 같은 소리가 반복돼도 덜 단조롭다.")]
        [Range(0f, 0.5f)] public float pitchRandomness = 0f;
        [Tooltip("지정하면 그 자리에서 나는 3D 소리가 된다(멀어지면 작아진다). 비우면 화면 전체에 깔리는 2D 소리.")]
        public Transform at;

        [Header("배경음 (BgmPlay / BgmStop)")]
        public bool loop = true;
        [Tooltip("곡이 바뀌거나 멈출 때 페이드하는 시간(초). 0이면 즉시.")]
        [Min(0f)] public float fadeSeconds = 1f;

        [Header("진행")]
        [Tooltip("켜두면 Sfx는 소리가 끝날 때까지, BGM은 페이드가 끝날 때까지 다음 스텝으로 " +
            "넘어가지 않는다. 꺼두면 재생만 걸어두고 바로 다음으로 넘어간다.")]
        public bool waitUntilDone;

        public override UniTask ExecuteAsync(TriggerContext context, CancellationToken cancellationToken)
        {
            float waitSeconds;

            switch (mode)
            {
                case Mode.BgmStop:
                {
                    var manager = SoundManager.Instance;
                    // 매니저가 아직 없으면 멈출 것도 없다 — 굳이 만들지 않는다.
                    waitSeconds = manager != null ? manager.StopBgm(fadeSeconds) : 0f;
                    break;
                }

                case Mode.BgmPlay:
                {
                    if (!HasClip(context))
                    {
                        return UniTask.CompletedTask;
                    }

                    waitSeconds = SoundManager.GetOrCreate().PlayBgm(clip, volume, fadeSeconds, loop);
                    break;
                }

                default:
                {
                    if (!HasClip(context))
                    {
                        return UniTask.CompletedTask;
                    }

                    float finalPitch = pitch;
                    if (pitchRandomness > 0f)
                    {
                        finalPitch += UnityEngine.Random.Range(-pitchRandomness, pitchRandomness);
                    }
                    finalPitch = Mathf.Clamp(finalPitch, 0.1f, 3f);

                    Vector3? position = at != null ? at.position : (Vector3?)null;
                    SoundManager.GetOrCreate().PlaySfx(clip, volume, finalPitch, position);

                    // pitch가 빠르면 그만큼 일찍 끝난다.
                    waitSeconds = clip.length / finalPitch;
                    break;
                }
            }

            if (!waitUntilDone || waitSeconds <= 0f)
            {
                return UniTask.CompletedTask;
            }

            // 일시정지 중에도 소리는 계속 나오므로 시간 배율을 무시하고 기다린다.
            return UniTask.Delay(TimeSpan.FromSeconds(waitSeconds), ignoreTimeScale: true,
                cancellationToken: cancellationToken);
        }

        bool HasClip(TriggerContext context)
        {
            if (clip != null)
            {
                return true;
            }

            Debug.LogWarning($"[PlaySoundAction] clip이 비어 있어 건너뜁니다. ({context.Name})");
            return false;
        }
    }
}

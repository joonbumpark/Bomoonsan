using UnityEngine;

namespace Mountains
{
    // Mountain 씬에서 플레이어가 걸을 때 발소리(footstep_1)를 낸다. 씬 파일을 건드리지 않고
    // 동작하도록 씬이 로드되면 스스로 생겨서 Player를 찾아 따라다닌다.
    //
    // 입력이 아니라 "실제로 움직인 거리"를 기준으로 한다 — 조이스틱 이동은 물론 이벤트 연출의
    // 강제 이동(MovePlayerToPoint 등)에서도 걷는 동안 발소리가 나고, 벽에 막혀 제자리에 있으면
    // 나지 않는다. 한 걸음 거리(StrideLength)마다 한 번 재생한다.
    public class PlayerFootsteps : MonoBehaviour
    {
        const string ClipPath = "Sounds/Sfx/footstep_1";

        const float Volume = 0.6f;
        const float PitchJitter = 0.06f;      // 매번 음높이를 ±6% 흔들어 단조로움을 줄인다
        const float VolumeJitter = 0.15f;     // 볼륨도 ±15% 흔든다
        const float StrideLength = 1.7f;      // 한 걸음의 거리(m). 이동 속도 5m/s면 초당 약 3걸음
        const float FirstStepRatio = 0.6f;    // 멈췄다 걸을 때 첫 발소리를 한 걸음의 60% 지점에서 낸다
        const float TeleportDistance = 3f;    // 한 프레임에 이만큼 이상 움직이면 걸음이 아니라 순간이동으로 본다
        const float StillSpeed = 0.2f;        // 이 속도(m/s) 미만이면 멈춘 것으로 본다
        const float StillSeconds = 0.15f;
        const float FindInterval = 0.5f;

        AudioClip _clip;
        Transform _player;
        Vector3 _lastPosition;
        float _distance;
        float _stillTime;
        float _nextFindTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Register()
        {
            SceneBootstrap.Register<PlayerFootsteps>(SceneBootstrap.MountainScene);
        }

        void Start()
        {
            _clip = Resources.Load<AudioClip>(ClipPath);
            if (_clip == null)
            {
                Debug.LogWarning($"[PlayerFootsteps] 발소리 파일이 없음: Resources/{ClipPath}");
                enabled = false;
            }
        }

        void Update()
        {
            if (_player == null)
            {
                FindPlayer();
                return;
            }

            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
            {
                return;
            }

            Vector3 position = _player.position;
            Vector3 delta = position - _lastPosition;
            delta.y = 0f; // 오르막/내리막이나 지형 높이 보정은 걸음 수에 넣지 않는다
            _lastPosition = position;

            float moved = delta.magnitude;
            if (moved >= TeleportDistance)
            {
                ResetStride();
                return;
            }

            if (moved / deltaTime < StillSpeed)
            {
                _stillTime += deltaTime;
                if (_stillTime >= StillSeconds)
                {
                    ResetStride();
                }
                return;
            }

            _stillTime = 0f;
            _distance += moved;
            if (_distance >= StrideLength)
            {
                _distance -= StrideLength;
                PlayStep();
            }
        }

        void FindPlayer()
        {
            if (Time.unscaledTime < _nextFindTime)
            {
                return;
            }

            _nextFindTime = Time.unscaledTime + FindInterval;
            var player = FindFirstObjectByType<Player>();
            if (player != null)
            {
                _player = player.transform;
                _lastPosition = _player.position;
                ResetStride();
            }
        }

        void ResetStride()
        {
            _distance = StrideLength * FirstStepRatio;
            _stillTime = 0f;
        }

        void PlayStep()
        {
            float volume = Volume * (1f + Random.Range(-VolumeJitter, VolumeJitter));
            float pitch = 1f + Random.Range(-PitchJitter, PitchJitter);
            SoundManager.GetOrCreate().PlaySfx(_clip, volume, pitch);
        }
    }
}

using Cinemachine;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace Mountains
{
    // 트리거 콜라이더(Is Trigger 체크 필요)에 붙여서, 플레이어가 들어오면 지정한
    // 대화를 재생한다. CharacterController는 Rigidbody가 없어도 트리거 콜라이더에
    // 들어가면 OnTriggerEnter를 그대로 받으므로 별도 Rigidbody가 필요 없다.
    [RequireComponent(typeof(Collider))]
    public class DialogTrigger : MonoBehaviour
    {
        public DialogData dialogData;
        [Tooltip("켜두면 한 번 재생한 뒤로는 다시 트리거되지 않는다.")]
        public bool triggerOnce = true;

        [Header("이벤트 카메라")]
        [Tooltip("비워두면 카메라를 바꾸지 않고 대화만 재생한다. 지정하면 대화 중엔 이 " +
            "가상 카메라로 전환되고 대화가 끝나면 평상시 카메라(Main Virtual Camera)로 " +
            "돌아간다. 씬에 배치할 때 반드시 비활성 상태로 둘 것 — 켜진 채로 두면 Brain이 " +
            "활성화된 vcam을 바로 live로 잡기 때문에 트리거 전에도 카메라를 가로챈다.")]
        public CinemachineVirtualCamera eventCamera;
        [Tooltip("이벤트 동안 이 값으로 Priority를 올린다.")]
        public int eventCameraPriority = 20;
        [FormerlySerializedAs("eventCamaraDeactiveWhenEnd")]
        [Tooltip("꺼두면 대화가 끝나도 eventCamera를 비활성화하지 않는다 — 이어지는 다른 " +
            "연출(예: 스플라인 이동, 리워드 연출)이 같은 카메라를 계속 써야 할 때 쓴다. " +
            "그 경우 카메라 복귀는 직접 처리해야 한다.")]
        public bool deactivateEventCameraOnEnd = true;

        [Header("콜백")]
        [Tooltip("대화(및 카메라 복귀)가 끝난 뒤 호출된다. 예: 문 열기, 퀘스트 진행.")]
        public UnityEvent onEventComplete;

        bool _consumed;

        void OnTriggerEnter(Collider other)
        {
            if (_consumed && triggerOnce)
            {
                return;
            }
            if (!other.CompareTag("Player"))
            {
                return;
            }
            if (DialogUI.Instance == null)
            {
                return;
            }

            _consumed = true;
            BeginEvent();
        }

        void BeginEvent()
        {
            if (eventCamera != null)
            {
                eventCamera.Priority = eventCameraPriority;
                eventCamera.gameObject.SetActive(true);
            }

            DialogUI.Instance.Play(dialogData, EndEvent);
        }

        void EndEvent()
        {
            // 평상시 카메라(Main Virtual Camera)도 CinemachineVirtualCamera라 별도로
            // 꺼둘 필요가 없다 — eventCamera의 Priority가 항상 더 높게 잡혀 있어서
            // CinemachineBrain이 알아서 우선순위 높은 쪽으로 블렌드한다.
            // deactivateEventCameraOnEnd가 false일 땐(스플라인 이동 등 이어지는 연출이
            // 같은 카메라를 계속 써야 할 때) 끄는 것 자체를 미루고, 그 연출이 끝난 뒤
            // 호출 측에서 직접 처리하게 둔다.
            if (eventCamera != null && deactivateEventCameraOnEnd)
            {
                eventCamera.gameObject.SetActive(false);
            }

            onEventComplete?.Invoke();
        }
    }
}

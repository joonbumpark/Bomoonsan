using System.Collections;
using Cinemachine;
using UnityEngine;
using UnityEngine.Events;

namespace Mountains
{
    // DialogTrigger를 대체하지 않는 별도의 범용 이벤트 트리거. "대화 한 편 재생"이라는
    // 한 시나리오에 고정된 DialogTrigger와 달리, TriggerAction 컴포넌트를 자유롭게
    // 조합해서 원하는 순서(steps, 직렬)와 동시성(각 step 안의 actions, 병렬)으로 실행한다.
    // 카메라 스왑/복귀, triggerOnce, 태그 체크는 DialogTrigger와 동일한 방식을 그대로 쓴다.
    [RequireComponent(typeof(Collider))]
    public class EventTrigger : MonoBehaviour
    {
        [System.Serializable]
        public class ActionStep
        {
            [Tooltip("인스펙터 가독성용 — 동작에는 영향 없음.")]
            public string label;
            [Tooltip("이 스텝 안의 액션들은 전부 동시에(병렬) 실행되고, 모두 끝나야 다음 스텝으로 넘어간다.")]
            public TriggerAction[] actions = new TriggerAction[0];
        }

        [Tooltip("켜두면 한 번 발동한 뒤로는 다시 트리거되지 않는다.")]
        public bool triggerOnce = true;
        [Tooltip("triggerOnce가 꺼져 있을 때, 재발동까지 최소로 기다려야 하는 시간(초).")]
        public float cooldown = 1f;

        [Header("이벤트 카메라")]
        [Tooltip("비워두면 카메라를 바꾸지 않는다. 지정하면 시퀀스 동안 이 가상 카메라로 전환되고 " +
            "끝나면 평상시 카메라로 돌아간다. 씬에 배치할 때 반드시 비활성 상태로 둘 것.")]
        public CinemachineVirtualCamera eventCamera;
        [Tooltip("이벤트 동안 이 값으로 Priority를 올린다.")]
        public int eventCameraPriority = 20;
        [Tooltip("꺼두면 시퀀스가 끝나도 eventCamera를 비활성화하지 않는다.")]
        public bool deactivateEventCameraOnEnd = true;

        [Tooltip("켜두면 시퀀스가 도는 동안 플레이어 조작(조이스틱 이동/드래그 회전)을 막는다 — " +
            "강제 이동 같은 연출이 조작과 싸우지 않게 한다.")]
        public bool blockInputDuringEvent = true;

        [Tooltip("시퀀스 시작 즉시(카메라 전환과 같은 프레임) 호출된다 — steps와 병렬로 시작해야 " +
            "하는 연출을 여기 연결한다.")]
        public UnityEvent onEventBegin;
        [Tooltip("순서대로(직렬) 실행되는 스텝들. 각 스텝 내부의 액션들은 서로 병렬로 실행된다.")]
        public ActionStep[] steps = new ActionStep[0];
        [Tooltip("모든 스텝과 카메라 복귀가 끝난 뒤 호출된다.")]
        public UnityEvent onEventComplete;

        bool _consumed;
        float _cooldownEndTime;
        bool _inputBlocked;

        void OnTriggerEnter(Collider other)
        {
            if (triggerOnce && _consumed)
            {
                return;
            }
            if (!triggerOnce && Time.time < _cooldownEndTime)
            {
                return;
            }
            if (!other.CompareTag("Player"))
            {
                return;
            }

            _consumed = true;
            _cooldownEndTime = Time.time + cooldown;
            StartCoroutine(RunSequence());
        }

        IEnumerator RunSequence()
        {
            if (blockInputDuringEvent)
            {
                InputBlocker.Block();
                _inputBlocked = true;
            }

            onEventBegin?.Invoke();

            if (eventCamera != null)
            {
                eventCamera.Priority = eventCameraPriority;
                eventCamera.gameObject.SetActive(true);
            }

            foreach (var step in steps)
            {
                yield return RunStepInParallel(step);
            }

            if (eventCamera != null && deactivateEventCameraOnEnd)
            {
                eventCamera.gameObject.SetActive(false);
            }

            ReleaseInputBlock();
            onEventComplete?.Invoke();
        }

        // 시퀀스 도중에 이 오브젝트가 꺼지거나 파괴되면 코루틴이 중단되어 잠금이 남는다 —
        // 그러면 조작이 영영 막히므로 여기서도 반드시 풀어준다.
        void OnDisable()
        {
            ReleaseInputBlock();
        }

        void ReleaseInputBlock()
        {
            if (!_inputBlocked)
            {
                return;
            }

            InputBlocker.Unblock();
            _inputBlocked = false;
        }

        IEnumerator RunStepInParallel(ActionStep step)
        {
            if (step?.actions == null || step.actions.Length == 0)
            {
                yield break;
            }

            int remaining = step.actions.Length;
            foreach (var action in step.actions)
            {
                if (action == null)
                {
                    remaining--;
                    continue;
                }
                action.Execute(() => remaining--);
            }

            while (remaining > 0)
            {
                yield return null;
            }
        }

        void OnDrawGizmos()
        {
            var col = GetComponent<Collider>();
            if (col == null)
            {
                return;
            }

            // Is Trigger가 꺼져 있으면 OnTriggerEnter가 아예 안 불려서 이 트리거는 죽은
            // 오브젝트가 된다 — 씬 뷰에서 바로 알아볼 수 있게 빨간색으로 그린다.
            Gizmos.color = col.isTrigger ? new Color(1f, 0.9f, 0.2f, 1f) : Color.red;
            DrawColliderGizmo(col);

            if (eventCamera != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, eventCamera.transform.position);
#if UNITY_EDITOR
                UnityEditor.Handles.Label((transform.position + eventCamera.transform.position) * 0.5f,
                    $"→ {eventCamera.name}");
#endif
            }
        }

        static void DrawColliderGizmo(Collider col)
        {
            var t = col.transform;
            Matrix4x4 originalMatrix = Gizmos.matrix;

            if (col is BoxCollider box)
            {
                Gizmos.matrix = Matrix4x4.TRS(t.TransformPoint(box.center), t.rotation, t.lossyScale);
                Gizmos.DrawWireCube(Vector3.zero, box.size);
                Gizmos.matrix = originalMatrix;
                return;
            }

            if (col is SphereCollider sphere)
            {
                float maxScale = Mathf.Max(t.lossyScale.x, t.lossyScale.y, t.lossyScale.z);
                Gizmos.DrawWireSphere(t.TransformPoint(sphere.center), sphere.radius * maxScale);
                return;
            }

            // Capsule 등 그 외 콜라이더 타입은 정확한 모양 대신 bounds를 근사치로 그린다.
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }
    }
}

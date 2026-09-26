using System.Collections.Generic;
using System.Threading;
using Cinemachine;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

namespace Mountains
{
    // DialogTrigger를 대체하지 않는 별도의 범용 이벤트 트리거. TriggerAction을 자유롭게
    // 조합해서 원하는 순서(steps, 직렬)와 동시성(각 step 안의 actions, 병렬)으로 실행한다.
    // 카메라 스왑/복귀, triggerOnce, 태그 체크는 DialogTrigger와 동일한 방식을 그대로 쓴다.
    //
    // 액션은 [SerializeReference]로 이 컴포넌트 안에 직접 들어간다 — 액션마다 자식
    // GameObject를 만들 필요가 없다. 예전 방식(자식 오브젝트 + 컴포넌트)으로 설정해둔
    // 트리거를 위해 legacyActions 경로를 마이그레이션이 끝날 때까지 남겨둔다.
    [RequireComponent(typeof(Collider))]
    public class EventTrigger : MonoBehaviour
    {
        [System.Serializable]
        public class ActionStep
        {
            [Tooltip("인스펙터 가독성용 — 동작에는 영향 없음.")]
            public string label;

            [Tooltip("이 스텝 안의 액션들은 전부 동시에(병렬) 실행되고, 모두 끝나야 다음 스텝으로 넘어간다. " +
                "+로 칸을 늘린 뒤 오른쪽 드롭다운에서 액션 종류를 고른다.")]
            [SerializeReference, SubclassSelector] public TriggerAction[] newActions = new TriggerAction[0];

            [Tooltip("예전 방식(자식 오브젝트에 붙인 컴포넌트)으로 만든 액션들. 마이그레이션 " +
                "메뉴를 돌리면 위 목록으로 옮겨진다. 위 목록이 비어 있을 때만 실행된다.")]

            [HideInInspector]public LegacyTriggerAction[] actions = new LegacyTriggerAction[0];

            public bool HasNewActions
            {
                get
                {
                    if (newActions == null)
                    {
                        return false;
                    }
                    foreach (var action in newActions)
                    {
                        if (action != null)
                        {
                            return true;
                        }
                    }
                    return false;
                }
            }
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
            RunSequenceAsync().Forget();
        }

        async UniTaskVoid RunSequenceAsync()
        {
            // 트리거가 파괴되면 진행 중인 await가 전부 풀린다 — 예전 코루틴이 오브젝트와 함께
            // 중단되던 것과 같은 수명이다.
            var cancellationToken = this.GetCancellationTokenOnDestroy();
            var context = new TriggerContext(this);

            try
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
                    await RunStepAsync(step, context, cancellationToken);
                }

                if (eventCamera != null && deactivateEventCameraOnEnd)
                {
                    eventCamera.gameObject.SetActive(false);
                }

                ReleaseInputBlock();
                onEventComplete?.Invoke();
            }
            catch (System.OperationCanceledException)
            {
                // 트리거가 파괴되어 중단된 것이므로 조용히 끝낸다. 잠금은 아래 finally에서 푼다.
            }
            finally
            {
                ReleaseInputBlock();
            }
        }

        static UniTask RunStepAsync(ActionStep step, TriggerContext context, CancellationToken cancellationToken)
        {
            if (step == null)
            {
                return UniTask.CompletedTask;
            }

            // 마이그레이션 전 트리거는 예전 경로로 돈다. 둘 다 있으면 새 목록이 우선이다.
            if (!step.HasNewActions)
            {
                return RunLegacyStepAsync(step);
            }

            var tasks = new List<UniTask>(step.newActions.Length);
            foreach (var action in step.newActions)
            {
                if (action != null)
                {
                    tasks.Add(action.ExecuteAsync(context, cancellationToken));
                }
            }

            return UniTask.WhenAll(tasks);
        }

        // 예전 콜백 방식(LegacyTriggerAction.Execute)을 await할 수 있게 감싼다.
        static UniTask RunLegacyStepAsync(ActionStep step)
        {
            if (step.actions == null || step.actions.Length == 0)
            {
                return UniTask.CompletedTask;
            }

            var completion = new UniTaskCompletionSource();
            var counter = new CompletionCounter(step.actions.Length, () => completion.TrySetResult());
            foreach (var action in step.actions)
            {
                if (action == null)
                {
                    counter.Signal();
                    continue;
                }
                action.Execute(counter.Signal);
            }

            return completion.Task;
        }

        // 시퀀스 도중에 이 오브젝트가 꺼지거나 파괴되면 잠금이 남는다 — 그러면 조작이 영영
        // 막히므로 여기서도 반드시 풀어준다.
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

#if UNITY_EDITOR
            // 액션이 가리키는 씬 오브젝트 중 [GizmoTarget]이 붙은 것만 이름표를 단다.
            TriggerActionGizmos.Draw(transform, steps);
#endif

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

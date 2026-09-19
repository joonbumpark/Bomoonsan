using System;
using DG.Tweening;
using UnityEngine;

namespace Mountains
{
    // 스폰 액션이 만든 NPC를 등장(In)/퇴장(Out) 연출로 트윈하는 액션. 대상 전부의 연출이
    // 끝나야 onComplete를 부르므로, 퇴장 연출 다음 스텝에 DespawnNpcAction을 두면
    // "사라지는 연출 → 실제 제거" 순서가 그대로 나온다.
    //
    // 등장(In)은 SpawnNpcAction과 같은 스텝에, 스폰보다 뒤 순서에 두는 것이 중요하다 —
    // EventTrigger는 한 스텝 안의 액션을 배열 순서대로 연달아 Execute하므로, 그래야
    // 스폰된 NPC가 화면에 그려지기 전에 숨은 상태로 스냅된다. 스텝을 나누면 한 프레임
    // 동안 원래 크기로 보일 수 있다.
    public class TweenNpcAction : NpcTargetingAction
    {
        public enum Mode
        {
            In,
            Out
        }

        [Tooltip("In=숨은 상태에서 정상 상태로(등장), Out=정상 상태에서 숨은 상태로(퇴장).")]
        public Mode mode = Mode.In;

        [Header("연출")]
        public float duration = 0.5f;
        public Ease ease = Ease.OutBack;
        [Tooltip("켜두면 스케일이 0에서 원래 크기로(또는 그 반대로) 변한다.")]
        public bool tweenScale = true;
        [Tooltip("0이 아니면 이만큼 땅속에서 솟아오른다(퇴장이면 가라앉는다). 모델만 " +
            "움직이므로 NavMeshAgent와 충돌하지 않는다.")]
        public float sinkDistance = 0f;
        [Tooltip("켜두면 연출이 도는 동안 NPC의 AI(따라가기/경로 이동)를 멈춘다 — " +
            "등장 연출 중에 태연히 걸어가는 걸 막는다.")]
        public bool freezeAiDuringTween = true;

        public override void Execute(Action onComplete)
        {
            ResolveTargets();
            if (_targets.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            var counter = new CompletionCounter(_targets.Count, onComplete);
            bool appearing = mode == Mode.In;
            foreach (var npc in _targets)
            {
                NpcTweenUtility.Play(npc, appearing, duration, ease, sinkDistance, tweenScale,
                    freezeAiDuringTween, counter.Signal);
            }
        }
    }
}

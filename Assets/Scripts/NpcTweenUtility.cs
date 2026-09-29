using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace Mountains
{
    // NPC 등장/퇴장 연출(스케일, 솟아오르기/가라앉기)을 실제로 돌리는 공통 코드.
    // TriggerAction(TweenNpcAction)과 NPC 자신의 자연 소멸(NpcDespawner) 양쪽에서 쓴다.
    public static class NpcTweenUtility
    {
        static readonly List<MonoBehaviour> _brainBuffer = new List<MonoBehaviour>();

        // 연출은 몸통(NavMeshAgent/Collider가 붙은 루트)이 아니라 그 밑의 "Root"(모델이
        // 심기는 자리)를 움직인다 — 루트를 직접 움직이면 NavMeshAgent가 매 프레임
        // transform.position을 자기 값으로 덮어써서 가라앉기 연출이 아예 안 보이고,
        // 스케일을 건드리면 콜라이더까지 같이 줄어든다. CharacterManager가 모델을
        // 심는 위치와 같은 규칙이다.
        public static Transform ResolveVisualRoot(GameObject npc)
        {
            var root = npc.transform.Find("Root");
            return root != null ? root : npc.transform;
        }

        // appearing이 true면 "숨은 상태로 스냅 → 정상 상태로 복귀", false면 그 반대.
        // onFinished는 연출이 끝나거나 중간에 대상이 파괴돼 트윈이 죽어도 정확히 한 번
        // 호출된다 — EventTrigger가 이 콜백을 세기 때문에, 안 불리면 시퀀스가 멈춘다.
        public static void Play(GameObject npc, bool appearing, float duration, Ease ease,
            float sinkDistance, bool tweenScale, bool freezeAi, Action onFinished)
        {
            if (npc == null)
            {
                onFinished?.Invoke();
                return;
            }

            Transform visual = ResolveVisualRoot(npc);
            var state = NpcVisualState.CaptureOrGet(visual);

            Vector3 hiddenScale = tweenScale ? Vector3.zero : state.shownScale;
            Vector3 hiddenLocalPosition = state.shownLocalPosition - Vector3.up * sinkDistance;

            // 같은 대상에 새 연출이 들어오면 이전 트윈은 버린다 — 둘이 겹치면 서로 반대
            // 방향으로 값을 써서 어중간한 크기로 멈춘다.
            DOTween.Kill(visual);

            if (freezeAi)
            {
                SetBrainsEnabled(npc, false);
            }

            Vector3 startScale = appearing ? hiddenScale : state.shownScale;
            Vector3 startPosition = appearing ? hiddenLocalPosition : state.shownLocalPosition;
            Vector3 endScale = appearing ? state.shownScale : hiddenScale;
            Vector3 endPosition = appearing ? state.shownLocalPosition : hiddenLocalPosition;

            visual.localScale = startScale;
            visual.localPosition = startPosition;

            if (duration <= 0f)
            {
                visual.localScale = endScale;
                visual.localPosition = endPosition;
                Finish(npc, freezeAi, onFinished);
                return;
            }

            // ease는 시퀀스가 아니라 개별 트윈에 건다 — 시퀀스의 ease는 타임라인 진행
            // 자체를 휘게 해서, 기본 ease가 걸린 내부 트윈과 겹쳐 두 번 먹는다.
            // SetTarget은 위 DOTween.Kill(visual)로 이 시퀀스까지 함께 정리하기 위한 것이다
            // (시퀀스 안의 트윈은 개별로 죽일 수 없다).
            var sequence = DOTween.Sequence().SetTarget(visual);
            if (tweenScale)
            {
                sequence.Join(visual.DOScale(endScale, duration).SetEase(ease));
            }
            if (!Mathf.Approximately(sinkDistance, 0f))
            {
                sequence.Join(visual.DOLocalMove(endPosition, duration).SetEase(ease));
            }

            // OnComplete가 아니라 OnKill을 쓴다 — 연출 도중 NPC가 파괴되면 트윈은 완료가
            // 아니라 Kill로 끝나고, 그때 OnComplete만 걸어뒀으면 콜백이 영영 안 온다.
            bool finished = false;
            sequence.OnKill(() =>
            {
                if (finished)
                {
                    return;
                }
                finished = true;
                Finish(npc, freezeAi, onFinished);
            });
        }

        static void Finish(GameObject npc, bool freezeAi, Action onFinished)
        {
            if (freezeAi && npc != null)
            {
                SetBrainsEnabled(npc, true);
            }
            onFinished?.Invoke();
        }

        static void SetBrainsEnabled(GameObject npc, bool enabled)
        {
            npc.GetComponents(_brainBuffer);
            foreach (var component in _brainBuffer)
            {
                if (component is INpcBrain)
                {
                    component.enabled = enabled;
                }
            }
            _brainBuffer.Clear();
        }
    }
}

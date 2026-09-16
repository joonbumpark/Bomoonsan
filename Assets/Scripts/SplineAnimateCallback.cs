using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Splines;

namespace Mountains
{
    public class SplineAnimateCallback : MonoBehaviour
    {
        [SerializeField] private SplineAnimate splineAnimate;
        public UnityEvent onEventComplete;

        private void OnEnable()
        {
            if (splineAnimate != null)
            {
                // 애니메이션 완료 콜백 등록
                splineAnimate.Completed += OnSplineCompleted;

                // 매 프레임 업데이트 콜백 등록 (필요 시)
                splineAnimate.Updated += OnSplineUpdated;
            }
        }

        private void OnDisable()
        {
            if (splineAnimate != null)
            {
                // 메모리 누수 방지를 위한 구독 해제
                splineAnimate.Completed -= OnSplineCompleted;
                splineAnimate.Updated -= OnSplineUpdated;
            }
        }

        // 재생 완료 시 호출
        private void OnSplineCompleted()
        {
            Debug.Log("Spline 이동 완료!");
            // 예: 대화창 출력, 다음 카메라 전환 등 호출

            onEventComplete?.Invoke();
        }

        // 매 프레임 이동 진행률 받기 (0.0 ~ 1.0)
        private void OnSplineUpdated(Vector3 position, Quaternion rotation)
        {
            float progress = splineAnimate.NormalizedTime;

            // 예: 특정 구간(50% 지점) 지날 때 특정 연출 실행
            if (progress >= 0.5f)
            {
                // 50% 통과 처리
            }
        }
    }
}
using DG.Tweening;
using UnityEngine;

namespace Mountains
{
    // CanvasGroup의 알파를 왕복시켜 깜빡이게 한다. "터치하여 시작"처럼 입력을 기다리는
    // 안내에 쓴다.
    //
    // 텍스트나 이미지 색을 직접 건드리지 않고 CanvasGroup을 쓰는 이유: 자식이 여럿인
    // 안내(아이콘 + 글자)도 그대로 함께 깜빡이고, 원래 색을 망가뜨리지 않는다.
    [RequireComponent(typeof(CanvasGroup))]
    public class UiBlink : MonoBehaviour
    {
        [Range(0f, 1f)] public float minAlpha = 0.25f;
        [Range(0f, 1f)] public float maxAlpha = 1f;
        [Tooltip("한 방향으로 페이드하는 데 걸리는 시간(초). 왕복은 그 두 배다.")]
        [Min(0.01f)] public float duration = 0.8f;
        public Ease ease = Ease.InOutSine;

        CanvasGroup _canvasGroup;
        Tween _tween;

        void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        void OnEnable()
        {
            _canvasGroup.alpha = maxAlpha;
            _tween = _canvasGroup.DOFade(minAlpha, duration)
                .SetEase(ease)
                .SetLoops(-1, LoopType.Yoyo)
                // 타이틀에서 일시정지 상태로 들어와도 멈추지 않게 한다.
                .SetUpdate(true);
        }

        // 꺼질 때 반드시 정리한다 — 무한 반복 트윈이라 두면 오브젝트가 사라진 뒤에도
        // DOTween이 붙잡고 있고, 다시 켜질 때 트윈이 두 개가 된다.
        void OnDisable()
        {
            _tween?.Kill();
            _tween = null;

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = maxAlpha;
            }
        }
    }
}

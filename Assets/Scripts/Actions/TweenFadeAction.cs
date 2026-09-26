using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Mountains
{
    // 투명도를 트윈하는 액션. 대상에 붙어 있는 컴포넌트를 보고 알아서 고른다 —
    // CanvasGroup(자식 UI 전체), Graphic(Image/Text 하나), SpriteRenderer 순.
    [Serializable]
    public class TweenFadeAction : TweenActionBase
    {
        [Header("페이드")]
        [Tooltip("도달할 투명도. 0=완전 투명, 1=불투명.")]
        [Range(0f, 1f)] public float endAlpha = 1f;

        protected override Tween CreateTween(Transform target)
        {
            var canvasGroup = target.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                return canvasGroup.DOFade(endAlpha, duration);
            }

            var graphic = target.GetComponent<Graphic>();
            if (graphic != null)
            {
                return graphic.DOFade(endAlpha, duration);
            }

            var spriteRenderer = target.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                return spriteRenderer.DOFade(endAlpha, duration);
            }

            Debug.LogWarning($"[TweenFadeAction] {target.name}에 CanvasGroup/Graphic/SpriteRenderer가 " +
                "없어 페이드할 대상을 찾지 못했습니다.");
            return null;
        }
    }
}

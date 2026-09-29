using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Mountains
{
    // 투명도를 트윈하는 액션. 대상에 붙어 있는 컴포넌트를 보고 알아서 고른다 —
    // CanvasGroup(자식 UI 전체), Graphic(Image/Text 하나), SpriteRenderer 순.
    //
    // CanvasGroup이 우선인 이유: 팝업처럼 자식이 여러 개인 UI는 Graphic 하나만 페이드해도
    // 나머지가 그대로 보여서 의미가 없다. 반대로 한 요소만 페이드하고 싶으면 CanvasGroup이
    // 없는 오브젝트를 대상으로 지정하면 된다.
    // 새로 붙이지 못하게 Add Component 메뉴에서 감춘다 — 이미 붙어 있는
    // 컴포넌트는 그대로 동작하므로 마이그레이션에는 영향이 없다.
    [AddComponentMenu("")]
    public class LegacyTweenFadeAction : LegacyTweenActionBase
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

            Debug.LogWarning($"[LegacyTweenFadeAction] {target.name}에 CanvasGroup/Graphic/SpriteRenderer가 " +
                "없어 페이드할 대상을 찾지 못했습니다.");
            return null;
        }
    }
}

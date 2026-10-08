using UnityEngine;

namespace Mountains
{
    // 그림 안에 글자(로고 등)가 들어간 배경용 맞춤. AspectRatioFitter의 EnvelopeParent처럼
    // 화면을 꽉 채우려고 무작정 키우면, 화면 비율이 그림과 많이 다를 때(폴더블 안쪽 화면 등)
    // 로고가 화면 밖으로 잘려 나간다.
    //
    // 그래서 "그림 전체가 보이는 크기(Fit)"에서 최대 maxOverscale 배까지만 키우고, 그래도 남는
    // 빈 공간은 뒤에 깔린 채움용 이미지(같은 그림을 어둡게 크게 깐 것)가 보이게 둔다.
    // alignment로 어느 쪽을 기준으로 붙일지 정한다 - 기본값은 위쪽(로고가 위에 있으니
    // 잘리더라도 아래쪽이 잘리게).
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class CoverFitter : MonoBehaviour
    {
        [Tooltip("그림의 가로/세로 비율")]
        public float aspectRatio = 1f;

        [Tooltip("그림 전체가 보이는 크기에서 최대 몇 배까지 키워 빈 공간을 줄일지 (1이면 항상 전체가 보임)")]
        public float maxOverscale = 1.15f;

        [Tooltip("부모 안에서 붙일 기준점 (0.5, 1) = 가운데 위")]
        public Vector2 alignment = new Vector2(0.5f, 1f);

        Vector2 _lastParentSize = new Vector2(-1f, -1f);

        void OnEnable() => _lastParentSize = new Vector2(-1f, -1f);

        void LateUpdate()
        {
            var parent = transform.parent as RectTransform;
            if (parent == null || aspectRatio <= 0f)
                return;

            Vector2 size = parent.rect.size;
            if (size == _lastParentSize || size.x <= 0f || size.y <= 0f)
                return;
            _lastParentSize = size;

            float fitH = Mathf.Min(size.y, size.x / aspectRatio);
            float coverH = Mathf.Max(size.y, size.x / aspectRatio);
            float scale = Mathf.Min(coverH / fitH, Mathf.Max(1f, maxOverscale));
            float h = fitH * scale;
            float w = h * aspectRatio;

            // 화면보다 큰 축(잘리는 쪽)만 alignment 기준으로 붙이고, 남는 축은 가운데 둔다.
            var pivot = new Vector2(w > size.x ? alignment.x : 0.5f, h > size.y ? alignment.y : 0.5f);

            var rt = (RectTransform)transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = pivot;
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2((pivot.x - 0.5f) * size.x, (pivot.y - 0.5f) * size.y);
        }
    }
}

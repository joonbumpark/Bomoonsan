using UnityEngine;
using UnityEngine.UI;

namespace Match3
{
    /// <summary>
    /// 서버와 통신 중(연결 시도/응답 대기)일 때 화면 우상단에 표시하는 로딩 스피너.
    /// 프리팹/씬 배치 없이 Create()로 코드에서 바로 만든다. 아이콘은 임시로 한쪽이 비어
    /// 있는 고리(원의 270도만 채운 모양)를 돌려서 "로딩 중"처럼 보이게 했다 - 나중에 실제
    /// 아트로 바꾸려면 Icon.sprite만 갈아끼우면 된다.
    ///
    /// 여러 군데서 동시에 Show()를 부를 수 있어 참조 카운트로 관리한다 - Show()한 만큼
    /// Hide()가 불려야 실제로 꺼진다.
    /// </summary>
    public class NetworkIndicator : MonoBehaviour
    {
        private const float SpinDegreesPerSecond = 260f;

        private static Sprite arcSprite;
        private static Sprite ArcSprite => arcSprite != null ? arcSprite : (arcSprite = CreateArcSprite());

        public Image Icon { get; private set; }

        private int activeCount;

        /// <summary>overrideIcon을 주면(AppFlowManager 인스펙터의 networkIndicatorIcon
        /// 필드로 연결) 임시 도형 대신 그 스프라이트를 쓴다.</summary>
        public static NetworkIndicator Create(Transform parent, Sprite overrideIcon = null)
        {
            var go = new GameObject("NetworkIndicator", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(72f, 72f);
            rt.anchoredPosition = new Vector2(-32f, -32f);

            var image = go.AddComponent<Image>();
            image.sprite = overrideIcon != null ? overrideIcon : ArcSprite;
            image.color = Color.white;
            image.raycastTarget = false;

            var indicator = go.AddComponent<NetworkIndicator>();
            indicator.Icon = image;
            go.SetActive(false);
            return indicator;
        }

        /// <summary>가장자리만 남긴 원에서 270도만 채우고 나머지 90도는 비워, 돌아갈 때
        /// 방향이 눈에 보이는 "C자" 스피너 모양을 만든다.</summary>
        private static Sprite CreateArcSprite()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float outerRadius = size / 2f - 2f;
            float innerRadius = outerRadius * 0.55f;
            const float arcDegrees = 270f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - center;
                    float dist = p.magnitude;
                    float angle = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg;
                    if (angle < 0f) angle += 360f;

                    float ringEdge = Mathf.Min(outerRadius - dist, dist - innerRadius);
                    float alpha = (angle <= arcDegrees && ringEdge >= 0f) ? Mathf.Clamp01(ringEdge / 1.5f) : 0f;

                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        /// <summary>Show()를 부른 횟수만큼 Hide()가 불려야 실제로 꺼진다 - 여러 통신이
        /// 겹쳐도 먼저 끝난 쪽이 인디케이터를 꺼버리지 않게 하기 위함.</summary>
        public void Show()
        {
            activeCount++;
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            activeCount = Mathf.Max(0, activeCount - 1);
            if (activeCount == 0)
                gameObject.SetActive(false);
        }

        private void Update()
        {
            transform.Rotate(0f, 0f, -SpinDegreesPerSecond * Time.deltaTime);
        }
    }
}

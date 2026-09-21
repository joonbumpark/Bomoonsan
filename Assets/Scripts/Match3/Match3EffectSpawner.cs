using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Match3
{
    /// <summary>
    /// 블록이 터지거나 아이템이 발동될 때 나오는 파티클풍 이펙트. 보드가 전부 uGUI(Screen
    /// Space - Overlay)라 일반 ParticleSystem(카메라가 있어야 그려짐) 대신, 작은 UI
    /// Image 조각 여러 개를 던지고 회전/페이드시키는 방식으로 흉내 낸다. 아이템 발동은
    /// 추가로 원점에서 확 커졌다 사라지는 "플래시" 한 장을 더해서 타격감을 준다.
    /// 게임(Match3/Tetris) 무관하게 재사용하는 static 유틸리티다.
    /// </summary>
    public static class Match3EffectSpawner
    {
        private static Sprite dotSprite;
        private static Sprite DotSprite => dotSprite != null ? dotSprite : (dotSprite = CreateCircleSprite());

        /// <summary>UI/Skin/Knob.psd 같은 내장 리소스는 유니티 버전에 따라 없을 수 있어(이
        /// 프로젝트의 6000.0.82f1에서도 없음) 직접 원형 텍스처를 만들어 쓴다. 가장자리를
        /// 살짝 부드럽게 처리해 두어 확대돼도 각지지 않는다.</summary>
        private static Sprite CreateCircleSprite()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    float alpha = Mathf.Clamp01(radius - dist);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private static readonly Color[] RainbowColors =
        {
            new Color(0.91f, 0.30f, 0.24f), new Color(0.95f, 0.61f, 0.07f), new Color(0.95f, 0.87f, 0.20f),
            new Color(0.30f, 0.69f, 0.31f), new Color(0.20f, 0.60f, 0.86f), new Color(0.61f, 0.35f, 0.71f),
        };

        /// <summary>일반 매치로 지워지는 칸 - 작은 색 파편이 살짝 튀었다 사라진다.</summary>
        public static void SpawnPop(MonoBehaviour runner, Transform parent, Vector2 position, Color color) =>
            SpawnBurst(runner, parent, position, count: 10, colors: new[] { color, Color.white }, speed: 260f, spreadDeg: 360f, life: 0.4f, size: 16f);

        /// <summary>가로 줄삭제 아이템 발동 - 좌우로 길게 뻗는 스트릭 + 플래시.</summary>
        public static void SpawnLineHorizontal(MonoBehaviour runner, Transform parent, Vector2 position, Color color)
        {
            SpawnFlash(runner, parent, position, color, maxSize: 160f, life: 0.25f);
            SpawnBurst(runner, parent, position, count: 22, colors: new[] { color, Color.white }, speed: 1100f, spreadDeg: 16f, life: 0.45f, size: 18f, angleOffsetDeg: 0f, twoSided: true, streak: true);
        }

        /// <summary>세로 줄삭제 아이템 발동 - 위아래로 길게 뻗는 스트릭 + 플래시.</summary>
        public static void SpawnLineVertical(MonoBehaviour runner, Transform parent, Vector2 position, Color color)
        {
            SpawnFlash(runner, parent, position, color, maxSize: 160f, life: 0.25f);
            SpawnBurst(runner, parent, position, count: 22, colors: new[] { color, Color.white }, speed: 1100f, spreadDeg: 16f, life: 0.45f, size: 18f, angleOffsetDeg: 90f, twoSided: true, streak: true);
        }

        /// <summary>3x3 폭탄 아이템 발동 - 사방으로 둥글게 퍼지는 큼직한 폭발 + 플래시.</summary>
        public static void SpawnAreaBomb(MonoBehaviour runner, Transform parent, Vector2 position, Color color)
        {
            SpawnFlash(runner, parent, position, color, maxSize: 260f, life: 0.3f);
            SpawnBurst(runner, parent, position, count: 24, colors: new[] { color, new Color(1f, 0.6f, 0.2f), Color.white }, speed: 520f, spreadDeg: 360f, life: 0.55f, size: 24f, spin: true);
        }

        /// <summary>무지개(컬러 폭탄) 아이템 발동 - 화려한 다색 파티클 + 플래시.</summary>
        public static void SpawnColorBomb(MonoBehaviour runner, Transform parent, Vector2 position)
        {
            SpawnFlash(runner, parent, position, Color.white, maxSize: 220f, life: 0.3f);
            SpawnBurst(runner, parent, position, count: 32, colors: RainbowColors, speed: 480f, spreadDeg: 360f, life: 0.65f, size: 20f, spin: true);
        }

        /// <summary>큰 축하 이펙트 - 화면 중앙에서 화려한 다색 컨페티가 크고 넓게 퍼진다.
        /// 판정(매치/줄삭제)이 아니라 "완주 자체를 축하"하는 상황(직소 퍼즐 완성 등)에 쓴다.</summary>
        public static void SpawnCelebration(MonoBehaviour runner, Transform parent, Vector2 position)
        {
            SpawnFlash(runner, parent, position, Color.white, maxSize: 340f, life: 0.4f);
            SpawnBurst(runner, parent, position, count: 40, colors: RainbowColors, speed: 620f, spreadDeg: 360f, life: 0.9f, size: 22f, spin: true);
        }

        /// <summary>원점에서 확 커졌다 빠르게 사라지는 충격파 한 장. 이펙트에 타격감을 더한다.</summary>
        private static void SpawnFlash(MonoBehaviour runner, Transform parent, Vector2 position, Color color, float maxSize, float life)
        {
            if (runner == null || parent == null || !runner.gameObject.activeInHierarchy)
                return;

            runner.StartCoroutine(RunFlash(parent, position, color, maxSize, life));
        }

        private static IEnumerator RunFlash(Transform parent, Vector2 position, Color color, float maxSize, float life)
        {
            var go = new GameObject("ClearEffectFlash", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.SetAsLastSibling();

            var image = go.AddComponent<Image>();
            image.sprite = DotSprite;
            image.color = new Color(color.r, color.g, color.b, 0.9f);
            image.raycastTarget = false;

            float t = 0f;
            while (t < life)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / life);
                float eased = 1f - (1f - p) * (1f - p);
                rt.sizeDelta = Vector2.one * Mathf.Lerp(8f, maxSize, eased);
                image.color = new Color(color.r, color.g, color.b, Mathf.Lerp(0.9f, 0f, p * p));
                yield return null;
            }

            Object.Destroy(go);
        }

        private static void SpawnBurst(MonoBehaviour runner, Transform parent, Vector2 position, int count,
            Color[] colors, float speed, float spreadDeg, float life, float size, float angleOffsetDeg = 0f,
            bool twoSided = false, bool streak = false, bool spin = false)
        {
            if (runner == null || parent == null || !runner.gameObject.activeInHierarchy)
                return;

            for (int i = 0; i < count; i++)
            {
                // twoSided면(줄삭제) 절반은 반대 방향으로 던져서 양쪽으로 퍼지게 한다.
                float baseAngle = angleOffsetDeg + (twoSided && i % 2 == 1 ? 180f : 0f);
                float angle = baseAngle + Random.Range(-spreadDeg / 2f, spreadDeg / 2f);
                Color color = colors[Random.Range(0, colors.Length)];
                float particleSize = size * Random.Range(0.7f, 1.4f);
                float particleSpeed = speed * Random.Range(0.7f, 1.15f);
                float particleLife = life * Random.Range(0.85f, 1.15f);
                float spinSpeed = spin ? Random.Range(-720f, 720f) : 0f;
                runner.StartCoroutine(RunParticle(parent, position, angle, particleSpeed, particleLife, particleSize, color, streak, spinSpeed));
            }
        }

        private static IEnumerator RunParticle(Transform parent, Vector2 origin, float angleDeg, float speed, float life,
            float size, Color color, bool streak, float spinSpeed)
        {
            var go = new GameObject("ClearEffectParticle", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            // 스트릭(줄삭제)은 날아가는 방향으로 길쭉하게 늘려서 레이저처럼 보이게 한다.
            rt.sizeDelta = streak ? new Vector2(size * 2.6f, size * 0.55f) : new Vector2(size, size);
            rt.anchoredPosition = origin;
            rt.localRotation = streak ? Quaternion.Euler(0, 0, angleDeg) : Quaternion.identity;
            rt.SetAsLastSibling();

            var image = go.AddComponent<Image>();
            image.sprite = DotSprite;
            image.color = color;
            image.raycastTarget = false;

            Vector2 dir = new Vector2(Mathf.Cos(angleDeg * Mathf.Deg2Rad), Mathf.Sin(angleDeg * Mathf.Deg2Rad));

            float t = 0f;
            while (t < life)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / life);
                // 빠르게 튀어나갔다가 감속하며(EaseOutQuad), 끝에는 작아지고 투명해진다.
                float dist = speed * life * (1f - (1f - p) * (1f - p));
                rt.anchoredPosition = origin + dir * dist;
                rt.localScale = Vector3.one * Mathf.Lerp(1.15f, 0.15f, p);
                if (!streak && spinSpeed != 0f)
                    rt.localRotation = Quaternion.Euler(0, 0, spinSpeed * t);
                image.color = new Color(color.r, color.g, color.b, Mathf.Lerp(1f, 0f, p * p));
                yield return null;
            }

            Object.Destroy(go);
        }

        /// <summary>확 튀어나왔다(팝) 잠깐 머물고 위로 떠오르며 사라지는 텍스트. 테트리스
        /// 여러 줄 동시 삭제처럼 "이번 판정이 얼마나 대단한지"를 글자로 알려줄 때 쓴다.</summary>
        public static void SpawnPopupText(MonoBehaviour runner, Transform parent, Vector2 position, string text, Color color, float fontSize, float life = 1f)
        {
            if (runner == null || parent == null || !runner.gameObject.activeInHierarchy)
                return;

            runner.StartCoroutine(RunPopupText(parent, position, text, color, fontSize, life));
        }

        private static IEnumerator RunPopupText(Transform parent, Vector2 position, string text, Color color, float fontSize, float life)
        {
            var go = new GameObject("PopupText", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(700f, 180f);
            rt.anchoredPosition = position;
            rt.localScale = Vector3.one * 0.3f;
            rt.SetAsLastSibling();

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.raycastTarget = false;
            if (UIFactory.KoreanFont != null)
                tmp.font = UIFactory.KoreanFont;

            // 확 커지며 튀어나왔다가(팝) 살짝 가라앉아 자리 잡고, 잠시 머문 뒤 위로 떠오르며 페이드아웃.
            const float popTime = 0.18f;
            const float settleTime = 0.12f;
            const float fadeTime = 0.3f;
            float holdTime = Mathf.Max(0f, life - popTime - settleTime - fadeTime);

            float t = 0f;
            while (t < popTime)
            {
                t += Time.deltaTime;
                rt.localScale = Vector3.one * Mathf.LerpUnclamped(0.3f, 1.2f, EaseOutBack(Mathf.Clamp01(t / popTime)));
                yield return null;
            }

            t = 0f;
            while (t < settleTime)
            {
                t += Time.deltaTime;
                rt.localScale = Vector3.one * Mathf.Lerp(1.2f, 1f, Mathf.Clamp01(t / settleTime));
                yield return null;
            }

            yield return new WaitForSeconds(holdTime);

            Vector2 start = rt.anchoredPosition;
            t = 0f;
            while (t < fadeTime)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / fadeTime);
                rt.anchoredPosition = start + Vector2.up * (50f * p);
                tmp.color = new Color(color.r, color.g, color.b, Mathf.Lerp(1f, 0f, p));
                yield return null;
            }

            Object.Destroy(go);
        }

        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3) + c1 * Mathf.Pow(x - 1f, 2);
        }
    }
}

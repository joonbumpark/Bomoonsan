using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Match3
{
    /// <summary>
    /// 코드로 uGUI 요소를 만들 때 쓰는 공용 헬퍼. Match3GameManager/TetrisGameManager/
    /// JigsawGameManager/WhackGameManager/SimonGameManager와 (씬 UI를 미리 만들어주는)
    /// Assets/Editor의 SceneBuilder 툴들이 함께 쓴다.
    /// </summary>
    public static class UIFactory
    {
        /// <summary>
        /// "제한시간 라운드 게임" 하나가 쓰는 전체화면 캔버스(배경 포함)를 만든다.
        /// Whack/Simon 게임 매니저가 아직 이 위에 자기 판/HUD를 코드로 올린다 (매치3/
        /// 테트리스/직소는 GameHudSceneBuilder가 씬에 미리 만들어둔 캔버스를 쓴다).
        /// </summary>
        public static GameObject CreateGameCanvasRoot(Transform parent, string name)
        {
            var canvasRoot = new GameObject(name);
            canvasRoot.transform.SetParent(parent, false);

            var canvas = canvasRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            canvasRoot.AddComponent<GraphicRaycaster>();

            var background = CreateImage("Background", canvasRoot.transform, new Color(0.10f, 0.11f, 0.15f));
            StretchFull(background.rectTransform);

            return canvasRoot;
        }

        /// <summary>화면 위쪽에 점수/남은시간을 보여주는 고정 높이 바를 만든다.</summary>
        public static void CreateGameTopBar(Transform parent, float height, out TextMeshProUGUI scoreText, out TextMeshProUGUI timerText)
        {
            var topBar = CreateRect("TopBar", parent);
            topBar.anchorMin = new Vector2(0, 1);
            topBar.anchorMax = new Vector2(1, 1);
            topBar.pivot = new Vector2(0.5f, 1);
            topBar.sizeDelta = new Vector2(0, height);
            topBar.anchoredPosition = Vector2.zero;

            scoreText = CreateText("ScoreText", topBar, "점수: 0", 56, TextAnchor.MiddleLeft);
            var scoreRt = scoreText.rectTransform;
            scoreRt.anchorMin = new Vector2(0, 0);
            scoreRt.anchorMax = new Vector2(0.5f, 1);
            scoreRt.offsetMin = new Vector2(40, 0);
            scoreRt.offsetMax = Vector2.zero;

            timerText = CreateText("TimerText", topBar, "남은 시간: 1:30", 56, TextAnchor.MiddleRight);
            var timerRt = timerText.rectTransform;
            timerRt.anchorMin = new Vector2(0.5f, 0);
            timerRt.anchorMax = new Vector2(1, 1);
            timerRt.offsetMin = Vector2.zero;
            timerRt.offsetMax = new Vector2(-40, 0);
        }

        /// <summary>화면 아래쪽 고정 높이 안내문구 바를 만들고, 나중에 문구를 바꿀 수 있게 텍스트를 반환한다.</summary>
        public static TextMeshProUGUI CreateGameHint(Transform parent, float height, string message)
        {
            var hint = CreateText("HintText", parent, message, 40, TextAnchor.MiddleCenter);
            hint.color = new Color(1f, 1f, 1f, 0.6f);
            var rt = hint.rectTransform;
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.sizeDelta = new Vector2(0, height);
            rt.anchoredPosition = new Vector2(0, 40);
            return hint;
        }

        private static TMP_FontAsset koreanFont;

        // 유니티 기본 내장 폰트는 한글 글리프가 없어서 별도로 포함시킨 한글 폰트(나눔고딕,
        // OFL 라이선스)의 TextMeshPro SDF 버전을 쓴다. Dynamic Atlas라 실제 화면에 나온
        // 글자만 그때그때 아틀라스에 채워 넣는다 (Assets/Editor/TMPFontAssetBuilder.cs 참고).
        public static TMP_FontAsset KoreanFont
        {
            get
            {
                if (koreanFont == null)
                    koreanFont = Resources.Load<TMP_FontAsset>("Fonts/NanumGothic-Regular SDF");
                return koreanFont;
            }
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Image CreateImage(string name, Transform parent, Color color)
        {
            var rt = CreateRect(name, parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public static TextMeshProUGUI CreateText(string name, Transform parent, string content, int fontSize, TextAnchor anchor)
        {
            var rt = CreateRect(name, parent);
            var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (KoreanFont != null)
                text.font = KoreanFont;
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = ToTmpAlignment(anchor);
            text.color = Color.white;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }

        private static TextAlignmentOptions ToTmpAlignment(TextAnchor anchor) => anchor switch
        {
            TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
            TextAnchor.UpperCenter => TextAlignmentOptions.Top,
            TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
            TextAnchor.MiddleLeft => TextAlignmentOptions.MidlineLeft,
            TextAnchor.MiddleCenter => TextAlignmentOptions.Midline,
            TextAnchor.MiddleRight => TextAlignmentOptions.MidlineRight,
            TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft,
            TextAnchor.LowerCenter => TextAlignmentOptions.Bottom,
            TextAnchor.LowerRight => TextAlignmentOptions.BottomRight,
            _ => TextAlignmentOptions.Center,
        };

        /// <summary>
        /// 화면 중앙(부모 중심)에 복주머니 프레임 팝업 카드 배경을 깐다. 9-slice라서
        /// 모서리 매듭 장식은 그대로 유지된 채 가운데만 늘어난다. 반환된 RectTransform을
        /// 부모로 써서 그 안의 제목/버튼 등을 배치하면 카드와 함께 중앙 정렬된다.
        /// </summary>
        public static RectTransform CreatePopupCard(string name, Transform parent, Vector2 size)
        {
            var rt = CreateRect(name, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;

            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = UIArt.PopupPanel;
            image.type = Image.Type.Sliced;

            return rt;
        }

        public static Button CreateButton(string name, Transform parent, string label, Vector2 pixelOffset, Vector2? size = null)
        {
            var rt = CreateRect(name, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size ?? new Vector2(420, 140);
            rt.anchoredPosition = pixelOffset;

            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = UIArt.Button;
            image.type = Image.Type.Simple;
            image.color = new Color(0.20f, 0.60f, 0.86f);

            var button = rt.gameObject.AddComponent<Button>();

            var labelText = CreateText(name + "_Label", rt, label, 52, TextAnchor.MiddleCenter);
            StretchFull(labelText.rectTransform);

            return button;
        }
    }
}

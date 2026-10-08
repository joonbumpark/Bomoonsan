using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Match3.EditorTools
{
    /// <summary>
    /// 나라찾기 게임을 한 번에 세팅한다:
    ///   1. JigsawCanvas.prefab을 복사해 CountryQuizCanvas.prefab을 만든다 (배경/점수바/
    ///      안내문구 바는 그대로 쓰고, 완성 미리보기 바를 지운 뒤 국기 카드 + 보기 버튼 4개를 올린다).
    ///   2. InGameScene에 CountryQuizGameManager + 그 프리팹 인스턴스를 배치하고 UI 필드를 연결한다.
    ///   3. GameSelectPopup.prefab의 테트리스 버튼을 나라찾기(country_btn)로 바꾼다.
    ///
    /// 다시 실행하면 CountryQuizCanvas.prefab과 씬의 CountryQuizGameManager를 지우고 새로
    /// 만든다 - 프리팹/씬에서 위치나 아트를 직접 손댔다면 재실행 전에 기억해둘 것.
    ///
    /// 배치 모드 호출: -executeMethod Match3.EditorTools.CountryQuizBuilder.Build
    /// </summary>
    public static class CountryQuizBuilder
    {
        private const string SourcePrefabPath = "Assets/Resources/Prefabs/JigsawCanvas.prefab";
        private const string PrefabPath = "Assets/Resources/Prefabs/CountryQuizCanvas.prefab";
        private const string GameSelectPrefabPath = "Assets/Resources/Prefabs/GameSelectPopup.prefab";
        private const string ScenePath = "Assets/Scenes/InGameScene.unity";
        private const string ButtonSpritePath = "Assets/Resources/Sprites/UI/country_btn.png";
        private const string ManagerName = "CountryQuizGameManager";

        private static readonly Color CardColor = Color.white;
        private static readonly Color AnswerColor = new Color(1f, 0.97f, 0.88f);
        private static readonly Color AnswerTextColor = new Color(0.27f, 0.16f, 0.08f);

        [MenuItem("Bomoonsan/Build Country Quiz")]
        public static void Build()
        {
            bool success = TryBuild();
            if (Application.isBatchMode)
                EditorApplication.Exit(success ? 0 : 1);
        }

        private static bool TryBuild()
        {
            AssetDatabase.Refresh();

            if (!BuildCanvasPrefab())
                return false;
            if (!UpdateGameSelectPopup())
                return false;
            if (!PlaceInScene())
                return false;

            Debug.Log("[CountryQuizBuilder] 나라찾기 세팅 완료.");
            return true;
        }

        // ----------------------------------------------------------------
        // 1. CountryQuizCanvas 프리팹
        // ----------------------------------------------------------------

        private static bool BuildCanvasPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
                AssetDatabase.DeleteAsset(PrefabPath);
            if (!AssetDatabase.CopyAsset(SourcePrefabPath, PrefabPath))
            {
                Debug.LogError($"[CountryQuizBuilder] {SourcePrefabPath} 복사 실패");
                return false;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                root.name = "CountryQuizCanvas";

                var previewBar = FindDeep(root.transform, "PreviewBar");
                if (previewBar != null)
                    Object.DestroyImmediate(previewBar.gameObject);

                var hintText = FindDeep(root.transform, "HintText")?.GetComponent<TextMeshProUGUI>();
                if (hintText != null)
                    hintText.text = "1번째 문제";

                var uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

                // 질문 문구 - 점수바 바로 아래.
                var questionBg = UIFactory.CreateImage("QuestionBg", root.transform, new Color(0f, 0f, 0f, 0.6f));
                var qRt = questionBg.rectTransform;
                qRt.anchorMin = new Vector2(0f, 1f);
                qRt.anchorMax = new Vector2(1f, 1f);
                qRt.pivot = new Vector2(0.5f, 1f);
                qRt.sizeDelta = new Vector2(0f, 120f);
                qRt.anchoredPosition = new Vector2(0f, -240f);
                questionBg.raycastTarget = false;

                var question = UIFactory.CreateText("QuestionText", qRt, "이 국기는 어느 나라일까요?", 56, TextAnchor.MiddleCenter);
                question.fontStyle = FontStyles.Bold;
                question.raycastTarget = false;
                UIFactory.StretchFull(question.rectTransform);

                // 국기 카드 - 흰 카드 위에 국기(비율 유지).
                var card = UIFactory.CreateImage("FlagCard", root.transform, CardColor);
                card.sprite = uiSprite;
                card.type = Image.Type.Sliced;
                card.raycastTarget = false;
                var cardRt = card.rectTransform;
                cardRt.anchorMin = cardRt.anchorMax = new Vector2(0.5f, 0.5f);
                cardRt.pivot = new Vector2(0.5f, 0.5f);
                cardRt.sizeDelta = new Vector2(840f, 590f);
                cardRt.anchoredPosition = new Vector2(0f, 230f);
                var cardShadow = card.gameObject.AddComponent<Shadow>();
                cardShadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
                cardShadow.effectDistance = new Vector2(8f, -10f);

                var flag = UIFactory.CreateImage("FlagImage", cardRt, Color.white);
                flag.preserveAspect = true;
                flag.raycastTarget = false;
                var flagRt = flag.rectTransform;
                flagRt.anchorMin = flagRt.anchorMax = new Vector2(0.5f, 0.5f);
                flagRt.pivot = new Vector2(0.5f, 0.5f);
                flagRt.sizeDelta = new Vector2(780f, 530f);
                flagRt.anchoredPosition = Vector2.zero;

                // 보기 버튼 4개 (2x2).
                var answers = UIFactory.CreateRect("Answers", root.transform);
                answers.anchorMin = answers.anchorMax = new Vector2(0.5f, 0.5f);
                answers.pivot = new Vector2(0.5f, 0.5f);
                answers.sizeDelta = new Vector2(900f, 400f);
                answers.anchoredPosition = new Vector2(0f, -430f);

                Vector2[] positions =
                {
                    new Vector2(-230f, 100f), new Vector2(230f, 100f),
                    new Vector2(-230f, -100f), new Vector2(230f, -100f),
                };
                for (int i = 0; i < positions.Length; i++)
                    CreateAnswerButton(answers, i, positions[i], uiSprite);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void CreateAnswerButton(Transform parent, int index, Vector2 position, Sprite sprite)
        {
            var rt = UIFactory.CreateRect($"Answer{index + 1}", parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(440f, 170f);
            rt.anchoredPosition = position;

            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = AnswerColor;

            var shadow = rt.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.4f);
            shadow.effectDistance = new Vector2(5f, -7f);

            // 틀려서 잠긴 버튼(interactable = false)도 글자가 잘 보이게 반투명 대신 불투명 회색으로.
            var button = rt.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 1f);
            button.colors = colors;

            var label = UIFactory.CreateText("Label", rt, "나라 이름", 58, TextAnchor.MiddleCenter);
            label.color = AnswerTextColor;
            label.fontStyle = FontStyles.Bold;
            label.enableAutoSizing = true;
            label.fontSizeMin = 30f;
            label.fontSizeMax = 58f;
            label.raycastTarget = false;
            UIFactory.StretchFull(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(20f, 10f);
            label.rectTransform.offsetMax = new Vector2(-20f, -10f);
        }

        // ----------------------------------------------------------------
        // 2. 게임 선택 팝업 - 테트리스 버튼을 나라찾기로
        // ----------------------------------------------------------------

        private static bool UpdateGameSelectPopup()
        {
            var buttonSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ButtonSpritePath);
            if (buttonSprite == null)
            {
                Debug.LogError($"[CountryQuizBuilder] 버튼 이미지를 찾을 수 없음: {ButtonSpritePath}");
                return false;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(GameSelectPrefabPath);
            try
            {
                var popup = root.GetComponent<GameSelectPopup>();
                var so = new SerializedObject(popup);
                var entries = so.FindProperty("gameButtons");

                bool replaced = false;
                for (int i = 0; i < entries.arraySize; i++)
                {
                    var entry = entries.GetArrayElementAtIndex(i);
                    var kind = entry.FindPropertyRelative("kind");
                    if (kind.intValue != (int)GameKind.Tetris && kind.intValue != (int)GameKind.CountryQuiz)
                        continue;

                    kind.intValue = (int)GameKind.CountryQuiz;
                    var button = entry.FindPropertyRelative("button").objectReferenceValue as Button;
                    if (button != null)
                    {
                        button.image.sprite = buttonSprite;
                        button.gameObject.name = "CountryQuizButton";
                    }
                    replaced = true;
                }

                if (!replaced)
                {
                    Debug.LogError("[CountryQuizBuilder] GameSelectPopup에서 테트리스 버튼 항목을 찾을 수 없음");
                    return false;
                }

                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, GameSelectPrefabPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ----------------------------------------------------------------
        // 3. InGameScene 배치/연결
        // ----------------------------------------------------------------

        private static bool PlaceInScene()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var existing = GameObject.Find(ManagerName);
            if (existing != null)
                Object.DestroyImmediate(existing);

            var managerGo = new GameObject(ManagerName);
            var manager = managerGo.AddComponent<CountryQuizGameManager>();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var canvas = (GameObject)PrefabUtility.InstantiatePrefab(prefab, managerGo.transform);
            var t = canvas.transform;

            var so = new SerializedObject(manager);
            so.FindProperty("canvasRoot").objectReferenceValue = canvas;
            so.FindProperty("scoreText").objectReferenceValue = FindDeep(t, "ScoreText").GetComponent<TextMeshProUGUI>();
            so.FindProperty("timerText").objectReferenceValue = FindDeep(t, "TimerText").GetComponent<TextMeshProUGUI>();
            so.FindProperty("hintText").objectReferenceValue = FindDeep(t, "HintText").GetComponent<TextMeshProUGUI>();
            so.FindProperty("flagImage").objectReferenceValue = FindDeep(t, "FlagImage").GetComponent<Image>();

            var buttons = so.FindProperty("answerButtons");
            var labels = so.FindProperty("answerLabels");
            buttons.arraySize = 4;
            labels.arraySize = 4;
            for (int i = 0; i < 4; i++)
            {
                var answer = FindDeep(t, $"Answer{i + 1}");
                buttons.GetArrayElementAtIndex(i).objectReferenceValue = answer.GetComponent<Button>();
                labels.GetArrayElementAtIndex(i).objectReferenceValue = answer.Find("Label").GetComponent<TextMeshProUGUI>();
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            // 다른 게임 캔버스들처럼 꺼진 채로 저장한다 - 실제 표시는 AppFlowManager가 한다.
            canvas.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            return EditorSceneManager.SaveScene(scene);
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            if (parent.name == name)
                return parent;
            foreach (Transform child in parent)
            {
                var found = FindDeep(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }
    }
}

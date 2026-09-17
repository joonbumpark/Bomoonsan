using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Match3.EditorTools
{
    /// <summary>
    /// 매치3/테트리스/직소 퍼즐 게임 매니저가 예전엔 Awake()에서 코드로 매번 새로 만들던
    /// 인게임 HUD(캔버스, 점수/타이머 바, 테트리스 다음 블록 바/조작 버튼, 직소 완성본
    /// 미리보기 바+안내문구)를 씬에 한 번 미리 배치해준다. 실행하고 나면 각 게임 매니저의
    /// 씬 UI 필드가 자동으로 연결되므로, 위치/아트를 손보고 싶을 땐 씬에서 바로 수정하면
    /// 되고 이 툴을 다시 돌릴 필요는 없다.
    ///
    /// 나머지 게임(복주머니 잡기/순서 기억하기)은 지금 게임 선택 화면에 안 보이는
    /// 상태라 이 툴이 손대지 않는다.
    ///
    /// 실제 게임판(타일/블록/조각 하나하나)과 테트리스의 "다음 블록" 4x4 미리보기는
    /// 여전히 코드가 만든다 - 그리드 크기가 인스펙터 설정에 따라 달라지거나 매 조각/
    /// 라운드마다 다시 그려야 해서 씬에 미리 박아둘 수 있는 게 아니다. 씬에는 그 부모
    /// 자리(캔버스, 다음 블록 미리보기의 NextPreviewRoot)만 미리 잡아둔다.
    ///
    /// 다시 실행하면 각 게임의 기존 캔버스("Match3Canvas" 등)를 지우고 새로 만든다 -
    /// 씬에서 그 밑을 직접 손댄 내용이 있으면 재실행 전에 백업해둘 것. 게임 매니저
    /// 오브젝트 자체는 씬에 이미 있으면(AppFlowManager.CreateGame과 같은 규칙) 그걸
    /// 그대로 쓰고, 없으면 새로 만든다.
    /// </summary>
    public static class GameHudSceneBuilder
    {
        private const float TopBarHeight = 220f;

        // 게임별로 메뉴를 따로 둔 이유: 이 툴은 대상 게임의 캔버스를 통째로 지우고
        // 새로 만든다 - "전체"를 돌리면 세 게임 다 손댄 내용이 날아가므로, 한 게임만
        // 고치고 싶을 땐 그 게임 메뉴만 실행해서 나머지 두 게임은 건드리지 않는다.

        [MenuItem("Bomoonsan/Build Game HUDs In Scene/전체 (매치3+테트리스+직소)")]
        public static void BuildAll()
        {
            BuildMatch3();
            BuildTetris();
            BuildJigsaw();
            FinishAndLog("매치3/테트리스/직소");
        }

        [MenuItem("Bomoonsan/Build Game HUDs In Scene/매치3만")]
        public static void BuildMatch3Only()
        {
            BuildMatch3();
            FinishAndLog("매치3");
        }

        [MenuItem("Bomoonsan/Build Game HUDs In Scene/테트리스만")]
        public static void BuildTetrisOnly()
        {
            BuildTetris();
            FinishAndLog("테트리스");
        }

        [MenuItem("Bomoonsan/Build Game HUDs In Scene/직소만")]
        public static void BuildJigsawOnly()
        {
            BuildJigsaw();
            FinishAndLog("직소");
        }

        private static void FinishAndLog(string builtWhat)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[GameHudSceneBuilder] {builtWhat} HUD 생성 완료 - 인스펙터에서 연결을 확인하고 씬을 저장할 것 (Cmd+S).");
        }

        // ----------------------------------------------------------------
        // 공용
        // ----------------------------------------------------------------

        private static GameObject FindOrCreateGameManager<T>(string name) where T : Component
        {
            var existing = Object.FindFirstObjectByType<T>();
            if (existing != null)
                return existing.gameObject;

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.AddComponent<T>();
            return go;
        }

        /// <summary>parent 밑의 canvasName 오브젝트가 이미 있으면 지우고 새로 만든다.</summary>
        private static GameObject ReplaceCanvas(Transform parent, string canvasName)
        {
            var existing = parent.Find(canvasName);
            if (existing != null)
                Undo.DestroyObjectImmediate(existing.gameObject);

            var canvasGo = new GameObject(canvasName);
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create " + canvasName);
            canvasGo.transform.SetParent(parent, false);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            // 예전엔 단색 배경을 캔버스 전체에 늘렸는데(StretchFull), 지금은 실제 배경
            // 그림(bgMinigame, 1200x1920)을 원본 크기 그대로 중앙에 놓는다.
            var background = UIFactory.CreateImage("Background", canvasGo.transform, Color.white);
            background.sprite = UIArt.MinigameBackground;
            var backgroundRt = background.rectTransform;
            backgroundRt.anchorMin = backgroundRt.anchorMax = new Vector2(0.5f, 0.5f);
            backgroundRt.pivot = new Vector2(0.5f, 0.5f);
            backgroundRt.sizeDelta = new Vector2(1200, 1920);

            return canvasGo;
        }

        /// <summary>TopBar 밑에 점수/타이머 글자를 받쳐주는 흰색 스트립을 깐다. 세 게임
        /// 다 있지만 스프라이트 유무는 다르다(매치3/테트리스는 기본 알약 스프라이트,
        /// 직소는 색만) - 각 게임이 실제로 씬에 갖고 있는 모습 그대로 재현한다.</summary>
        private static void AddTopBarBackingStrip(Transform topBar, bool useBuiltinSprite)
        {
            var bg = UIFactory.CreateImage("bg", topBar, Color.white);
            var rt = bg.rectTransform;
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.sizeDelta = new Vector2(0, 100);
            rt.anchoredPosition = new Vector2(0, -110);
            rt.SetSiblingIndex(0); // 점수/타이머 글자보다 먼저(뒤에) 그려져야 글자를 안 가린다.

            if (useBuiltinSprite)
            {
                bg.sprite = Resources.GetBuiltinResource<Sprite>("UISprite.psd");
                bg.type = Image.Type.Sliced;
            }
        }

        private static void SetField(SerializedObject so, string field, Object value)
        {
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"[GameHudSceneBuilder] {so.targetObject.GetType().Name}에 '{field}' 필드가 없음 - 스크립트가 바뀌었는지 확인할 것.");
                return;
            }
            prop.objectReferenceValue = value;
        }

        // ----------------------------------------------------------------
        // 매치3
        // ----------------------------------------------------------------

        private static void BuildMatch3()
        {
            var managerGo = FindOrCreateGameManager<Match3GameManager>("Match3GameManager");
            var canvasGo = ReplaceCanvas(managerGo.transform, "Match3Canvas");

            UIFactory.CreateGameTopBar(canvasGo.transform, TopBarHeight, out var scoreText, out var timerText);
            AddTopBarBackingStrip(scoreText.transform.parent, useBuiltinSprite: true);
            UIFactory.CreateGameHint(canvasGo.transform, 100f, "드래그로 교환, 아이템 블록은 탭하거나 옮기면 발동!");

            canvasGo.SetActive(false);

            var so = new SerializedObject(managerGo.GetComponent<Match3GameManager>());
            SetField(so, "canvasRoot", canvasGo);
            SetField(so, "scoreText", scoreText);
            SetField(so, "timerText", timerText);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ----------------------------------------------------------------
        // 테트리스
        // ----------------------------------------------------------------

        // TetrisGameManager 기본 cellSize(56)로 만든 4x4 미리보기(224x224)가 위아래
        // 여유를 두고 들어갈 만큼. cellSize를 인스펙터에서 크게 바꾸면 이 값도 같이
        // 늘려야 미리보기가 바 밖으로 안 넘친다.
        private const float NextPanelHeight = 260f;
        private const float ControlBarHeight = 220f;

        private static void BuildTetris()
        {
            var managerGo = FindOrCreateGameManager<TetrisGameManager>("TetrisGameManager");
            var canvasGo = ReplaceCanvas(managerGo.transform, "TetrisCanvas");

            UIFactory.CreateGameTopBar(canvasGo.transform, TopBarHeight, out var scoreText, out var timerText);
            AddTopBarBackingStrip(scoreText.transform.parent, useBuiltinSprite: true);
            var nextPreviewRoot = BuildNextBar(canvasGo.transform);
            var (left, right, rotate, softDrop, hardDrop) = BuildControlBar(canvasGo.transform);

            canvasGo.SetActive(false);

            var so = new SerializedObject(managerGo.GetComponent<TetrisGameManager>());
            SetField(so, "canvasRoot", canvasGo);
            SetField(so, "scoreText", scoreText);
            SetField(so, "timerText", timerText);
            SetField(so, "nextPreviewRoot", nextPreviewRoot);
            SetField(so, "leftButton", left);
            SetField(so, "rightButton", right);
            SetField(so, "rotateButton", rotate);
            SetField(so, "softDropButton", softDrop);
            SetField(so, "hardDropButton", hardDrop);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>"다음 블록" 라벨 + 실제 미니 셀이 들어갈 빈 자리(NextPreviewRoot)까지만
        /// 만든다 - 미니 셀 16개 자체는 TetrisGameManager가 코드로 채운다.</summary>
        private static RectTransform BuildNextBar(Transform parent)
        {
            var bar = UIFactory.CreateRect("NextBar", parent);
            bar.anchorMin = new Vector2(0, 1);
            bar.anchorMax = new Vector2(1, 1);
            bar.pivot = new Vector2(0.5f, 1);
            bar.sizeDelta = new Vector2(0, NextPanelHeight);
            bar.anchoredPosition = new Vector2(0, -TopBarHeight);

            var label = UIFactory.CreateText("NextLabel", bar, "다음 블록", 44, TextAnchor.MiddleLeft);
            var labelRt = label.rectTransform;
            labelRt.anchorMin = new Vector2(0, 0);
            labelRt.anchorMax = new Vector2(0.5f, 1);
            labelRt.offsetMin = new Vector2(40, 0);
            labelRt.offsetMax = new Vector2(-30, -30); // 미리보기가 커진 자리를 내주려고 라벨 오른쪽/위를 줄여둠

            var previewRoot = UIFactory.CreateRect("NextPreviewRoot", bar);
            previewRoot.anchorMin = previewRoot.anchorMax = new Vector2(0.75f, 0.5f);
            previewRoot.pivot = new Vector2(0.5f, 0.5f);
            previewRoot.sizeDelta = new Vector2(224, 224); // TetrisGameManager 기본 cellSize(56, 실제 보드 블록과 같은 크기) * 4칸
            previewRoot.anchoredPosition = new Vector2(-150, 0);

            // 미리보기 4x4 셀 뒤에 깔리는 반투명 배경 패널.
            var previewBacking = previewRoot.gameObject.AddComponent<Image>();
            previewBacking.sprite = Resources.GetBuiltinResource<Sprite>("UISprite.psd");
            previewBacking.type = Image.Type.Sliced;
            previewBacking.color = new Color32(46, 139, 182, 100);

            return previewRoot;
        }

        /// <summary>실제 조작 버튼 5개(좌/우/회전/소프트드롭/하드드롭)를 만든다 - 클릭/누르고
        /// 있기 동작은 TetrisGameManager가 이 버튼들을 받아서 코드로 붙인다.</summary>
        private static (Button left, Button right, Button rotate, Button softDrop, Button hardDrop) BuildControlBar(Transform parent)
        {
            var bar = UIFactory.CreateRect("ControlBar", parent);
            bar.anchorMin = new Vector2(0, 0);
            bar.anchorMax = new Vector2(1, 0);
            bar.pivot = new Vector2(0.5f, 0);
            bar.sizeDelta = new Vector2(0, ControlBarHeight);
            bar.anchoredPosition = Vector2.zero;

            float btnW = 170f;
            float btnH = 150f;
            float spacing = 20f;
            string[] labels = { "◀", "▶", "회전", "▼", "드롭" };
            float totalW = labels.Length * btnW + (labels.Length - 1) * spacing;

            var buttons = new Button[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                float x = -totalW / 2f + btnW / 2f + i * (btnW + spacing);
                buttons[i] = UIFactory.CreateButton($"CtrlBtn_{i}", bar, labels[i], new Vector2(x, 0), new Vector2(btnW, btnH));

                // 커스텀 버튼 프레임(UIArt.Button) 대신 기본 알약 모양으로 바꿔둔 상태 -
                // 색(파랑)은 CreateButton 기본값 그대로 두고 프레임만 교체한다.
                var img = buttons[i].GetComponent<Image>();
                img.sprite = Resources.GetBuiltinResource<Sprite>("UISprite.psd");
                img.type = Image.Type.Sliced;
            }

            return (buttons[0], buttons[1], buttons[2], buttons[3], buttons[4]);
        }

        // ----------------------------------------------------------------
        // 직소 퍼즐
        // ----------------------------------------------------------------

        private const float PreviewBarHeight = 200f;

        private static void BuildJigsaw()
        {
            var managerGo = FindOrCreateGameManager<JigsawGameManager>("JigsawGameManager");
            var canvasGo = ReplaceCanvas(managerGo.transform, "JigsawCanvas");

            UIFactory.CreateGameTopBar(canvasGo.transform, TopBarHeight, out var scoreText, out var timerText);
            AddTopBarBackingStrip(scoreText.transform.parent, useBuiltinSprite: false);
            var previewImage = BuildPreviewBar(canvasGo.transform);
            var hintText = UIFactory.CreateGameHint(canvasGo.transform, 110f, "조각을 두 번 탭해 자리를 바꾸세요");

            canvasGo.SetActive(false);

            var so = new SerializedObject(managerGo.GetComponent<JigsawGameManager>());
            SetField(so, "canvasRoot", canvasGo);
            SetField(so, "scoreText", scoreText);
            SetField(so, "timerText", timerText);
            SetField(so, "hintText", hintText);
            SetField(so, "previewImage", previewImage);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Image BuildPreviewBar(Transform parent)
        {
            var bar = UIFactory.CreateRect("PreviewBar", parent);
            bar.anchorMin = new Vector2(0, 1);
            bar.anchorMax = new Vector2(1, 1);
            bar.pivot = new Vector2(0.5f, 1);
            bar.sizeDelta = new Vector2(0, PreviewBarHeight);
            bar.anchoredPosition = new Vector2(0, -TopBarHeight);

            var label = UIFactory.CreateText("PreviewLabel", bar, "완성 모습", 44, TextAnchor.MiddleLeft);
            var labelRt = label.rectTransform;
            labelRt.anchorMin = new Vector2(0, 0);
            labelRt.anchorMax = new Vector2(0.5f, 1);
            labelRt.offsetMin = new Vector2(40, 0);
            labelRt.offsetMax = new Vector2(-150, 0); // 썸네일이 커진/옮겨진 자리를 내주려고 오른쪽을 줄여둠

            var previewImage = UIFactory.CreateImage("PreviewThumb", bar, Color.white);
            var thumbRt = previewImage.rectTransform;
            thumbRt.anchorMin = thumbRt.anchorMax = new Vector2(0.72f, 0.5f);
            thumbRt.pivot = new Vector2(0.5f, 0.5f);
            thumbRt.sizeDelta = new Vector2(300, 300);
            thumbRt.anchoredPosition = new Vector2(-100, 0);
            previewImage.preserveAspect = true;

            return previewImage;
        }
    }
}

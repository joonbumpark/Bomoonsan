using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Match3.EditorTools
{
    /// <summary>
    /// AppFlowManager가 예전엔 Awake()에서 코드로 매번 새로 만들던 앱 셸(Canvas + 화면
    /// 패널)을 InGameScene에 한 번 미리 배치해준다. 실행하고 나면 AppFlowManager의
    /// 씬 UI 필드가 전부 자동으로 연결되므로, 위치/아트를 손보고 싶을 땐 씬에서 바로
    /// 수정하면 되고 이 툴을 다시 돌릴 필요는 없다.
    ///
    /// 화면 4개(GameSelectPopup/PlayModePopup/MatchmakingPopup/ResultPopup) 전부 직접
    /// 만들지 않고 이미 아트가 입혀진 Assets/Resources/Prefabs/*.prefab을 그대로
    /// 인스턴스화한다(프리팹 연결 유지) - 프리팹이 아직 없으면 에러 로그만 남기고 그
    /// 필드는 비워둔 채 계속 진행하니, 나중에 프리팹을 만들고 이 툴을 다시 돌리면 된다.
    ///
    /// 다시 실행하면 기존에 이 툴이 만든 "AppCanvas"를 지우고 새로 만든다 - 씬에서
    /// 그 밑을 직접 손댄 내용이 있으면 재실행 전에 백업해둘 것.
    /// </summary>
    public static class AppShellSceneBuilder
    {
        private const string CanvasName = "AppCanvas";
        private const string AppFlowManagerName = "AppFlowManager";
        private const string GameSelectPrefabPath = "Assets/Resources/Prefabs/GameSelectPopup.prefab";
        private const string PlayModePrefabPath = "Assets/Resources/Prefabs/PlayModePopup.prefab";
        private const string MatchmakingPrefabPath = "Assets/Resources/Prefabs/MatchmakingPopup.prefab";
        private const string ResultPrefabPath = "Assets/Resources/Prefabs/ResultPopup.prefab";

        [MenuItem("Bomoonsan/Build App Shell In Scene")]
        public static void Build()
        {
            var existingCanvas = GameObject.Find(CanvasName);
            if (existingCanvas != null)
                Undo.DestroyObjectImmediate(existingCanvas);

            EnsureEventSystem();

            var canvasGo = BuildCanvas();

            var gameSelectPopup = InstantiatePopup<GameSelectPopup>(GameSelectPrefabPath, canvasGo.transform);
            var playModePopup = InstantiatePopup<PlayModePopup>(PlayModePrefabPath, canvasGo.transform);
            var matchmakingPopup = InstantiatePopup<MatchmakingPopup>(MatchmakingPrefabPath, canvasGo.transform);
            var resultPopup = InstantiatePopup<ResultPopup>(ResultPrefabPath, canvasGo.transform);

            // 최초 상태는 게임 선택만 보이게 - 실제 화면 전환은 플레이 시작 시
            // AppFlowManager.Awake()가 ShowGameSelect()를 호출하면서 다시 정리하지만,
            // 에디터에서 보기 좋으라고 미리 정리해둔다.
            gameSelectPopup?.gameObject.SetActive(true);
            playModePopup?.gameObject.SetActive(false);
            matchmakingPopup?.gameObject.SetActive(false);
            resultPopup?.gameObject.SetActive(false);

            var appFlowManagerGo = GameObject.Find(AppFlowManagerName);
            AppFlowManager appFlowManager;
            if (appFlowManagerGo != null)
            {
                appFlowManager = appFlowManagerGo.GetComponent<AppFlowManager>();
            }
            else
            {
                appFlowManagerGo = new GameObject(AppFlowManagerName);
                Undo.RegisterCreatedObjectUndo(appFlowManagerGo, "Create " + AppFlowManagerName);
                appFlowManager = appFlowManagerGo.AddComponent<AppFlowManager>();
            }

            WireAppFlowManager(appFlowManager, canvasGo, gameSelectPopup, playModePopup, matchmakingPopup, resultPopup);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[AppShellSceneBuilder] 앱 셸 생성 완료 - AppFlowManager 인스펙터에서 연결을 확인하고 씬을 저장할 것 (Cmd+S).");
        }

        // ----------------------------------------------------------------
        // 구조 생성
        // ----------------------------------------------------------------

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
                return;

            var eventSystemGo = new GameObject("EventSystem");
            Undo.RegisterCreatedObjectUndo(eventSystemGo, "Create EventSystem");
            eventSystemGo.AddComponent<EventSystem>();
            // 이 프로젝트는 새 Input System만 사용하도록 설정되어 있으므로
            // 레거시 StandaloneInputModule 대신 InputSystemUIInputModule을 사용한다.
            var uiModule = eventSystemGo.AddComponent<InputSystemUIInputModule>();
            uiModule.AssignDefaultActions();
        }

        private static GameObject BuildCanvas()
        {
            var canvasGo = new GameObject(CanvasName);
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create " + CanvasName);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            var background = UIFactory.CreateImage("Background", canvasGo.transform, new Color(0.10f, 0.11f, 0.15f));
            UIFactory.StretchFull(background.rectTransform);

            return canvasGo;
        }

        /// <summary>이미 아트가 입혀진 팝업 프리팹(GameSelectPopup/PlayModePopup/MatchmakingPopup/
        /// ResultPopup)을 프리팹 연결을 유지한 채 캔버스 밑에 인스턴스화한다.</summary>
        private static T InstantiatePopup<T>(string prefabPath, Transform parent) where T : Component
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[AppShellSceneBuilder] 프리팹을 찾을 수 없음: {prefabPath}");
                return null;
            }

            var instanceGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(instanceGo, "Instantiate " + prefab.name);
            instanceGo.transform.SetParent(parent, false);

            var component = instanceGo.GetComponent<T>();
            if (component == null)
                Debug.LogError($"[AppShellSceneBuilder] {prefabPath}에 {typeof(T).Name} 컴포넌트가 없음");

            return component;
        }

        // ----------------------------------------------------------------
        // AppFlowManager 필드 연결
        // ----------------------------------------------------------------

        private static void WireAppFlowManager(AppFlowManager appFlowManager, GameObject canvasGo,
            GameSelectPopup gameSelectPopup, PlayModePopup playModePopup, MatchmakingPopup matchmakingPopup,
            ResultPopup resultPopup)
        {
            var so = new SerializedObject(appFlowManager);

            void Set(string field, Object value)
            {
                var prop = so.FindProperty(field);
                if (prop == null)
                {
                    Debug.LogError($"[AppShellSceneBuilder] AppFlowManager에 '{field}' 필드가 없음 - 스크립트가 바뀌었는지 확인할 것.");
                    return;
                }
                prop.objectReferenceValue = value;
            }

            Set("appCanvasRoot", canvasGo);
            Set("gameSelectPopup", gameSelectPopup);
            Set("playModePopup", playModePopup);
            Set("matchmakingPopup", matchmakingPopup);
            Set("resultPopup", resultPopup);

            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

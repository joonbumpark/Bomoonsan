using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Mountains
{
    // 기기 화면 비율 대응. 모든 UI는 1080x1920(9:16) 기준으로 배치돼 있는데, CanvasScaler를
    // MatchWidthOrHeight(0.5)로 두면 1:1에 가까운 폰(폴더블 안쪽 화면 등)에서 캔버스 높이가
    // 1440까지 줄어 위아래 UI가 잘리거나 겹친다.
    //
    // Expand 모드로 바꾸면 어떤 비율이든 1080x1920 기준 영역이 항상 화면 안에 통째로 들어오고,
    // 남는 공간(넓은 화면은 좌우, 긴 화면은 위아래)만 늘어난다. 그 남는 공간은 배경 이미지가
    // AspectRatioFitter(EnvelopeParent)로 비율을 유지한 채 꽉 채운다
    // (Assets/Editor/ResponsiveUiSetup.cs가 프리팹/씬에 미리 세팅).
    //
    // 프리팹/텍스트 씬은 에디터 도구로 이미 Expand로 바꿔뒀지만, 바이너리로 저장된 씬
    // (Mountain 5 - 팀원이 계속 작업 중이라 병합 충돌 때문에 직접 고치지 않는다)이나
    // 나중에 추가되는 캔버스도 빠짐없이 맞추려고 씬이 로드될 때마다 한 번 더 맞춘다.
    public static class ResponsiveCanvas
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            foreach (var scaler in Object.FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Apply(scaler);
        }

        // 코드로 캔버스를 만드는 곳(UIFactory, UiOverlayRoot)도 이걸 불러 같은 규칙을 쓴다.
        public static void Apply(CanvasScaler scaler)
        {
            if (scaler == null || scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
                return;

            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        }
    }
}

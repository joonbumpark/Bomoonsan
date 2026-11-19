using UnityEngine;

namespace Mountains
{
    // Windows(데스크톱) 실행 파일에서 창을 세로(9:16) 창 모드로 고정한다.
    // 빌드 설정의 기본 해상도만으로는 부족하다 — Unity는 마지막으로 쓴 창 크기를 저장해 두었다가 다음
    // 실행에 그대로 쓰기 때문에, 한 번이라도 가로로 바뀌면 계속 가로로 열린다. 그래서 시작할 때마다
    // 모니터 높이에 맞춰 세로 창을 다시 잡는다. 모바일/웹/에디터에서는 아무것도 하지 않는다.
    public static class PortraitWindow
    {
        const float Aspect = 9f / 16f;          // 가로 / 세로
        const float ScreenFill = 0.88f;         // 모니터 높이의 몇 %를 쓸지 (작업 표시줄 여유)
        const int MaxHeight = 1920;

#if UNITY_STANDALONE && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Apply()
        {
            int displayHeight = Display.main.systemHeight > 0 ? Display.main.systemHeight : Screen.currentResolution.height;
            int height = Mathf.Min(MaxHeight, Mathf.RoundToInt(displayHeight * ScreenFill));
            int width = Mathf.RoundToInt(height * Aspect);

            Screen.SetResolution(width, height, FullScreenMode.Windowed);
        }
#endif
    }
}

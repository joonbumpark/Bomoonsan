using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mountains
{
    // 씬 전환을 한 곳에서 처리한다.
    //
    // 넘어가기 전에 정리가 필요한 이유: 씬을 새로 로드해도 static 값은 초기화되지 않는다
    // ([RuntimeInitializeOnLoadMethod]는 앱 시작 때 한 번만 돈다). 특히 InputBlocker의
    // 잠금 카운트가 남으면 새 씬에서 조작이 영영 막힌 채로 시작한다 — 옵션 팝업이
    // 대화나 이벤트 도중에 열렸다면 실제로 그 상태가 된다.
    public static class SceneFlow
    {
        // 현재 씬을 처음부터 다시 시작한다.
        public static void ReloadCurrent()
        {
            var active = SceneManager.GetActiveScene();

            // buildIndex가 -1이면 Build Settings에 없는 씬이다(에디터에서 직접 열어둔 경우).
            // LoadScene은 목록에 있는 씬만 불러올 수 있어서 경로로 시도해도 똑같이 실패한다 —
            // 엉뚱한 에러 대신 이유를 알려주고 멈춘다.
            if (active.buildIndex < 0)
            {
                Debug.LogWarning($"[SceneFlow] '{active.name}'이 Build Settings에 없어 다시 시작할 수 " +
                    "없습니다. File > Build Settings 목록에 추가하세요.");
                return;
            }

            PrepareForLoad();
            SceneManager.LoadScene(active.buildIndex);
        }

        public static void Load(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogWarning("[SceneFlow] 이동할 씬 이름이 비어 있습니다.");
                return;
            }

            // Build Settings에 없는 씬을 그냥 LoadScene하면 "Scene couldn't be loaded"
            // 에러만 뜨고 왜 그런지 알기 어렵다 — 먼저 확인해서 이유를 알려준다.
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogWarning($"[SceneFlow] '{sceneName}' 씬을 불러올 수 없습니다. " +
                    "File > Build Settings의 목록에 그 씬이 등록돼 있는지 확인하세요.");
                return;
            }

            PrepareForLoad();
            SceneManager.LoadScene(sceneName);
        }

        static void PrepareForLoad()
        {
            // 팝업/대화 도중에 전환하면 잠금이 남는다.
            InputBlocker.ResetBlocks();
            // 일시정지 상태에서 전환하면 새 씬이 멈춘 채로 시작한다.
            Time.timeScale = 1f;
        }
    }
}

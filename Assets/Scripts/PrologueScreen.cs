using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mountains
{
    // 타이틀 화면에서 클릭하면 프롤로그 이미지를 한 장씩 보여주고, 마지막 장 다음 클릭에
    // 게임 씬으로 넘어간다. TitleScene의 빈 오브젝트에 붙여서 쓴다.
    //
    // 상태를 인덱스 하나로 표현한다: -1이면 아직 타이틀, 0 이상이면 그 번째 이미지.
    // "타이틀 클릭"과 "다음 이미지"가 결국 같은 동작(한 칸 전진)이라 분기를 나눌 필요가 없다.
    public class PrologueScreen : MonoBehaviour
    {
        [Header("데이터")]
        public PrologueData data;
        [Tooltip("프롤로그가 끝나면 이동할 씬. File > Build Settings 목록에 있어야 한다.")]
        public string nextSceneName = "Mountain 5";

        [Header("표시")]
        [Tooltip("이미지가 그려질 Image. 화면 전체를 덮도록 배치한다.")]
        public Image imageTarget;
        [Tooltip("프롤로그 패널 전체의 CanvasGroup. 타이틀 상태에서는 꺼둔다.")]
        public CanvasGroup canvasGroup;

        [Header("입력")]
        [Tooltip("씬이 열린 뒤 이 시간(초) 동안은 입력을 무시한다 — 이전 화면에서 누르던 " +
            "손가락이 이어져 타이틀이 한 프레임만 보이고 넘어가는 것을 막는다.")]
        public float inputDelay = 0.3f;
        [Tooltip("타이틀 상태에서 버튼 같은 UI 위를 터치했을 때는 시작하지 않는다. 타이틀에 " +
            "옵션 버튼 등을 놓을 때 필요하다(프롤로그 중에는 패널이 화면을 덮으므로 적용되지 않는다).")]
        public bool ignoreWhenOverUi = true;

        // -1 = 타이틀, 0 이상 = 그 번째 이미지를 보여주는 중
        int _index = -1;
        float _readyTime;
        bool _leaving;

        void OnEnable()
        {
            // timeScale과 무관하게 흘러야 한다 — 이전 씬에서 일시정지 상태로 넘어왔을 수 있다.
            _readyTime = Time.unscaledTime + inputDelay;
            _leaving = false;
            _index = -1;
            SetPanelVisible(false);
        }

        void Update()
        {
            if (_leaving || Time.unscaledTime < _readyTime)
            {
                return;
            }

            // 모바일 터치도 Unity가 마우스 0번 버튼으로 함께 넘겨준다(구 Input Manager).
            if (!Input.GetMouseButtonDown(0))
            {
                return;
            }

            // UI 위 무시는 타이틀 상태에서만 의미가 있다. 프롤로그가 뜨면 패널이 화면을
            // 덮으므로(레이캐스트도 막는다) 이 검사를 그대로 두면 모든 클릭이 씹힌다.
            if (ignoreWhenOverUi && _index < 0 && IsPointerOverUi())
            {
                return;
            }

            Advance();
        }

        void Advance()
        {
            _index++;

            if (data == null || data.images == null || _index >= data.images.Length)
            {
                Leave();
                return;
            }

            if (_index == 0)
            {
                SetPanelVisible(true);
            }

            ShowImage(data.images[_index]);
        }

        void ShowImage(Sprite sprite)
        {
            if (imageTarget == null)
            {
                Debug.LogWarning("[PrologueScreen] imageTarget이 없어 이미지를 표시하지 못합니다.");
                return;
            }

            imageTarget.sprite = sprite;
            imageTarget.enabled = sprite != null;

            float duration = data != null ? data.fadeDuration : 0f;
            if (duration <= 0f)
            {
                imageTarget.color = Color.white;
                return;
            }

            // 새 이미지가 투명에서 올라오게 한다. 이전 장과 겹쳐 보이지 않도록 이미지를
            // 갈아끼운 뒤 페이드하므로, 장면 사이에 잠깐 어두워지는 전환이 된다.
            imageTarget.DOKill();
            imageTarget.color = new Color(1f, 1f, 1f, 0f);
            imageTarget.DOFade(1f, duration);
        }

        void Leave()
        {
            _leaving = true;

            if (string.IsNullOrEmpty(nextSceneName))
            {
                Debug.LogWarning("[PrologueScreen] nextSceneName이 비어 있어 이동할 씬이 없습니다.");
                return;
            }

            SceneFlow.Load(nextSceneName);
        }

        void SetPanelVisible(bool visible)
        {
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = visible ? 1f : 0f;
            // 프롤로그 중에는 아래의 타이틀 UI(옵션 버튼 등)가 눌리지 않도록 막는다.
            canvasGroup.blocksRaycasts = visible;
            canvasGroup.interactable = visible;
        }

        static bool IsPointerOverUi()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            // 터치는 손가락 id로 물어봐야 정확하다 — 인자 없는 호출은 마우스 기준이라
            // 기기에서 항상 false가 나온다.
            if (Input.touchCount > 0)
            {
                return eventSystem.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
            }

            return eventSystem.IsPointerOverGameObject();
        }
    }
}

using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mountains
{
    // 화면 하단 대화창. 화면 전체를 덮는 투명 Image로 탭을 감지해서 대사를 넘기고,
    // 그 Image가 대화 중엔 최상단에서 탭을 가로채므로 조이스틱/화면 드래그 회전이
    // 같이 반응하지 않는다 — 대화 중 이동/카메라 회전을 막으려고 별도 처리를 할
    // 필요가 없다. CanvasGroup 하나로 표시 여부와 입력 차단을 함께 다룬다.
    public class DialogUI : MonoBehaviour, IPointerClickHandler
    {
        public static DialogUI Instance { get; private set; }

        [Header("References")]
        public CanvasGroup canvasGroup;
        [Tooltip("초상화 모델이 없는 캐릭터용 폴백. CharacterData.portrait(Sprite)를 표시한다.")]
        public Image portraitImage;
        [Tooltip("CharacterPortraitStage가 실시간으로 그린 3D 초상화가 표시되는 곳.")]
        public RawImage portraitRenderImage;
        public Image dialogBackground;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI bodyText;

        [Tooltip("character가 비어 있을 때 dialogBackground에 쓰이는 기본 색상.")]
        public Color defaultDialogColor = Color.black;

        [Header("Typing")]
        [Tooltip("초당 타이핑되는 글자 수.")]
        [Min(1f)] public float charsPerSecond = 30f;

        [Header("Debug")]
        [Tooltip("DialogTrigger 없이도, 아래 컴포넌트 우클릭 > 테스트 재생으로 Play 모드에서 " +
            "바로 확인할 수 있게 해주는 테스트용 데이터.")]
        public DialogData debugTestData;

        DialogData _data;
        int _lineIndex;
        Coroutine _typingRoutine;
        bool _isTyping;
        bool _warnedMissingPortraitTarget;
        System.Action _onFinished;

        void Awake()
        {
            Instance = this;
            SetVisible(false);
        }

        public bool IsShowing => _data != null;

        // onFinished는 이벤트 트리거가 카메라 복귀/게임 로직 콜백을 거는 데 쓴다.
        // 데이터가 비어 재생을 못 하는 경우에도 반드시 호출해야 한다 — 안 그러면
        // 트리거 쪽에서 카메라를 이벤트용으로 전환해둔 채 영영 돌아오지 못한다.
        public void Play(DialogData data, System.Action onFinished = null)
        {
            if (data == null || data.lines == null || data.lines.Length == 0)
            {
                Debug.LogWarning("[DialogUI] 재생할 대사가 없는 DialogData입니다.");
                onFinished?.Invoke();
                return;
            }

            _data = data;
            _lineIndex = 0;
            _onFinished = onFinished;
            SetVisible(true);
            // 대화 중 이동/회전 차단도 이벤트 연출과 같은 잠금을 쓴다 — 예전엔 조작하는
            // 쪽이 DialogUI.IsShowing을 직접 확인했는데, 이제 잠금을 거는 주체가 여럿이라
            // (EventTrigger 등) 한 곳으로 모은다. Close()의 Unblock과 반드시 쌍이 맞아야 한다.
            InputBlocker.Block();
            ShowLine();
        }

        void ShowLine()
        {
            var line = _data.lines[_lineIndex];
            var character = line.character;

            if (nameText != null)
            {
                nameText.text = character != null ? character.characterName : string.Empty;
            }
            UpdatePortrait(character);
            if (dialogBackground != null)
            {
                dialogBackground.color = character != null ? character.dialogColor : defaultDialogColor;
            }

            if (_typingRoutine != null)
            {
                StopCoroutine(_typingRoutine);
            }
            _typingRoutine = StartCoroutine(TypeText(line.text));
        }

        // 모델이 있는 캐릭터는 실시간으로 그린 3D 초상화를, 없는 캐릭터만 기존 Sprite를 쓴다.
        // 둘 중 하나만 켜두어야 서로 겹쳐 보이지 않는다.
        void UpdatePortrait(CharacterData character)
        {
            bool wantsModelPortrait = character != null && character.PortraitPrefab != null;
            RenderTexture portraitTexture = null;

            // portraitRenderImage가 비어 있으면 렌더해봐야 그릴 곳이 없다 — 그 경우엔 아예
            // 스테이지를 돌리지 않고 기존 Sprite로 폴백한다(초상화가 통째로 사라지는 것보다 낫다).
            if (wantsModelPortrait && portraitRenderImage != null)
            {
                portraitTexture = CharacterPortraitStage.GetOrCreate().Show(character);
            }
            else
            {
                // 초상화 모델을 안 쓰는 대화에서는 스테이지를 만들지도 않는다.
                CharacterPortraitStage.HideIfExists();
                if (wantsModelPortrait)
                {
                    WarnMissingPortraitTarget();
                }
            }

            if (portraitRenderImage != null)
            {
                portraitRenderImage.texture = portraitTexture;
                portraitRenderImage.enabled = portraitTexture != null;
            }

            if (portraitImage != null)
            {
                portraitImage.sprite = portraitTexture == null && character != null ? character.portrait : null;
                portraitImage.enabled = portraitImage.sprite != null;
            }
        }

        // 3D 초상화를 쓸 캐릭터인데 표시할 자리가 없으면 원인을 한 번만 알려준다 —
        // 대사 줄마다 찍으면 콘솔이 도배된다.
        void WarnMissingPortraitTarget()
        {
            if (_warnedMissingPortraitTarget)
            {
                return;
            }

            _warnedMissingPortraitTarget = true;
            Debug.LogWarning("[DialogUI] portraitRenderImage가 비어 있어 3D 초상화를 표시할 수 없습니다. " +
                "Mountains > Setup Dialog UI를 한 번 실행하세요.");
        }

        IEnumerator TypeText(string text)
        {
            _isTyping = true;
            bodyText.text = string.Empty;

            var builder = new StringBuilder();
            float delay = 1f / charsPerSecond;
            for (int i = 0; i < text.Length; i++)
            {
                builder.Append(text[i]);
                bodyText.text = builder.ToString();
                yield return new WaitForSeconds(delay);
            }

            _isTyping = false;
            _typingRoutine = null;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Advance();
        }

        void Advance()
        {
            if (_isTyping)
            {
                // 타이핑 도중 탭하면 그 탭으로는 넘기지 않고 남은 텍스트만 즉시 다 보여준다
                // — 흔한 VN 관례이자, 다음 탭에서 바로 다음 줄로 넘어가면 방금 나온 문장을
                // 놓치기 쉬운 문제를 막아준다.
                CompleteTyping();
                return;
            }

            _lineIndex++;
            if (_data == null || _lineIndex >= _data.lines.Length)
            {
                Close();
                return;
            }
            ShowLine();
        }

        void CompleteTyping()
        {
            if (_typingRoutine != null)
            {
                StopCoroutine(_typingRoutine);
                _typingRoutine = null;
            }
            _isTyping = false;
            bodyText.text = _data.lines[_lineIndex].text;
        }

        void Close()
        {
            SetVisible(false);
            _data = null;
            // 대화가 끝나면 초상화 카메라도 꺼서 매 프레임 렌더 비용을 0으로 되돌린다.
            CharacterPortraitStage.HideIfExists();
            InputBlocker.Unblock();

            var callback = _onFinished;
            _onFinished = null;
            callback?.Invoke();
        }

        void SetVisible(bool visible)
        {
            if (canvasGroup == null)
            {
                return;
            }
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        [ContextMenu("테스트 재생")]
        void PlayDebugTestData()
        {
            Play(debugTestData);
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Match3
{
    /// <summary>
    /// 나라찾기. 100개 나라(CountryQuizData) 중 하나의 국기가 나오고, 아래 4개 버튼 중
    /// 그 나라 이름을 고른다. 맞히면 다음 문제로 넘어가고, 틀리면 그 버튼만 잠근 채
    /// 맞힐 때까지 다시 고르게 한다 - 적게 틀릴수록 점수가 크다(pointsByAttempt).
    ///
    /// 한 라운드 안에서는 같은 나라가 다시 나오지 않는다(100개를 다 돌면 다시 섞는다).
    /// 다른 게임들처럼 제한시간이 다 되면 그때까지의 점수로 라운드가 끝난다.
    /// </summary>
    public class CountryQuizGameManager : MonoBehaviour, IRoundGame
    {
        private const int ChoiceCount = 4;

        [Header("라운드 설정")]
        public float roundDurationSeconds = 90f;
        [Tooltip("몇 번째 시도에 맞혔는지에 따른 점수 (1번째, 2번째, 3번째, 4번째)")]
        public int[] pointsByAttempt = { 100, 50, 20, 0 };

        [Header("연출")]
        public float correctDelaySeconds = 0.7f;

        private static readonly Color NormalTint = Color.white;
        private static readonly Color CorrectTint = new Color(0.55f, 0.9f, 0.5f);
        private static readonly Color WrongTint = new Color(0.85f, 0.85f, 0.85f);
        private static readonly Color CorrectTextColor = new Color(1f, 0.85f, 0.3f);

        public event Action<int> RoundEnded;
        public int CurrentScore => score;

        [Header("씬 UI (CountryQuizCanvas 프리팹 인스턴스)")]
        [SerializeField] private GameObject canvasRoot;
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private TextMeshProUGUI hintText;
        [SerializeField] private Image flagImage;
        [SerializeField] private Button[] answerButtons = new Button[ChoiceCount];
        [SerializeField] private TextMeshProUGUI[] answerLabels = new TextMeshProUGUI[ChoiceCount];

        private readonly Dictionary<string, Sprite> flagCache = new Dictionary<string, Sprite>();

        private int[] deck;       // 이번 라운드에 낼 나라 순서 (CountryQuizData.Countries 인덱스)
        private int deckPos;
        private readonly int[] choiceCountry = new int[ChoiceCount]; // 버튼 i -> 나라 인덱스
        private int correctChoice;
        private int wrongAttempts;
        private int solvedCount;

        private int score;
        private bool roundActive;
        private bool inputLocked;
        private float timeRemaining;
        private int lastDisplayedSeconds;
        private System.Random rng;

        private void Awake()
        {
            if (!ValidateSceneRefs())
                return;

            for (int i = 0; i < ChoiceCount; i++)
            {
                int captured = i;
                answerButtons[i].onClick.AddListener(() => OnAnswerClicked(captured));
            }
            SetVisible(false);
        }

        /// <summary>
        /// 인스펙터에서 연결이 빠진 씬 UI 필드가 있으면 NRE 대신 어떤 필드가 비었는지
        /// 한 번에 알려주고 멈춘다 - Bomoonsan > Build Country Quiz 메뉴를 다시 돌리거나
        /// 씬의 CountryQuizCanvas 인스턴스에서 인스펙터로 직접 연결하면 된다.
        /// </summary>
        private bool ValidateSceneRefs()
        {
            var missing = new List<string>();
            void Check(UnityEngine.Object obj, string fieldName)
            {
                if (obj == null)
                    missing.Add(fieldName);
            }

            Check(canvasRoot, nameof(canvasRoot));
            Check(scoreText, nameof(scoreText));
            Check(timerText, nameof(timerText));
            Check(hintText, nameof(hintText));
            Check(flagImage, nameof(flagImage));
            for (int i = 0; i < ChoiceCount; i++)
            {
                Check(answerButtons != null && answerButtons.Length > i ? answerButtons[i] : null, $"{nameof(answerButtons)}[{i}]");
                Check(answerLabels != null && answerLabels.Length > i ? answerLabels[i] : null, $"{nameof(answerLabels)}[{i}]");
            }

            if (missing.Count == 0)
                return true;

            Debug.LogError(
                $"[CountryQuizGameManager] 씬 UI 참조가 비어 있음: {string.Join(", ", missing)}\n" +
                "Bomoonsan > Build Country Quiz 메뉴로 다시 생성하거나 인스펙터에서 직접 연결할 것.",
                this);
            return false;
        }

        public void SetVisible(bool visible) => canvasRoot.SetActive(visible);

        public void BeginRound(int? seed = null)
        {
            StopAllCoroutines();

            score = 0;
            solvedCount = 0;
            roundActive = true;
            timeRemaining = roundDurationSeconds;
            lastDisplayedSeconds = -1;
            rng = new System.Random(seed ?? Environment.TickCount);

            deck = null;
            NextQuestion();
            UpdateHud();
            SetVisible(true);
        }

        /// <summary>진행 중인 라운드를 결과 없이 멈춘다(RoundEnded 안 보냄) - 설정 팝업의
        /// 다시하기/그만하기에서 AppFlowManager가 부른다.</summary>
        public void AbortRound()
        {
            StopAllCoroutines();
            roundActive = false;
        }

        private void Update()
        {
            if (!roundActive)
                return;

            timeRemaining -= Time.deltaTime;
            if (timeRemaining <= 0f)
            {
                timeRemaining = 0f;
                roundActive = false;
                StopAllCoroutines();
                UpdateHud();
                RoundEnded?.Invoke(score);
                return;
            }

            UpdateHud();
        }

        // ----------------------------------------------------------------
        // 문제 만들기
        // ----------------------------------------------------------------

        private void NextQuestion()
        {
            int total = CountryQuizData.Countries.Length;
            if (deck == null || deckPos >= total)
            {
                deck = new int[total];
                for (int i = 0; i < total; i++)
                    deck[i] = i;
                Shuffle(deck);
                deckPos = 0;
            }

            int answer = deck[deckPos++];

            // 정답 1개 + 정답이 아닌 나라 3개를 겹치지 않게 뽑아서 자리를 섞는다.
            var picked = new List<int> { answer };
            while (picked.Count < ChoiceCount)
            {
                int candidate = rng.Next(total);
                if (!picked.Contains(candidate))
                    picked.Add(candidate);
            }
            var order = picked.ToArray();
            Shuffle(order);

            for (int i = 0; i < ChoiceCount; i++)
            {
                choiceCountry[i] = order[i];
                if (order[i] == answer)
                    correctChoice = i;

                answerLabels[i].text = CountryQuizData.Countries[order[i]].Name;
                answerButtons[i].interactable = true;
                answerButtons[i].image.color = NormalTint;
            }

            flagImage.sprite = LoadFlag(CountryQuizData.Countries[answer].Code);
            flagImage.preserveAspect = true;

            wrongAttempts = 0;
            inputLocked = false;
            hintText.text = $"{solvedCount + 1}번째 문제";
        }

        private void Shuffle(int[] array)
        {
            for (int i = array.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (array[i], array[j]) = (array[j], array[i]);
            }
        }

        private Sprite LoadFlag(string code)
        {
            if (!flagCache.TryGetValue(code, out var sprite))
            {
                sprite = Resources.Load<Sprite>("Sprites/Flags/" + code);
                if (sprite == null)
                    Debug.LogWarning($"[CountryQuizGameManager] 국기 이미지가 없음: Sprites/Flags/{code}");
                flagCache[code] = sprite;
            }
            return sprite;
        }

        // ----------------------------------------------------------------
        // 답 고르기
        // ----------------------------------------------------------------

        private void OnAnswerClicked(int choice)
        {
            if (!roundActive || inputLocked)
                return;

            if (choice == correctChoice)
            {
                StartCoroutine(CorrectThenNext(choice));
                return;
            }

            // 틀린 버튼은 잠그고(다시 못 누름) 맞힐 때까지 남은 버튼으로 다시 고르게 한다.
            wrongAttempts++;
            answerButtons[choice].interactable = false;
            answerButtons[choice].image.color = WrongTint;
            hintText.text = "틀렸어요! 다시 골라보세요";

            var rt = (RectTransform)answerButtons[choice].transform;
            Match3EffectSpawner.SpawnPopupText(this, rt.parent, rt.anchoredPosition, "X", new Color(1f, 0.35f, 0.3f), 90f, 0.6f);
        }

        private IEnumerator CorrectThenNext(int choice)
        {
            inputLocked = true;
            solvedCount++;

            int points = pointsByAttempt.Length > 0
                ? pointsByAttempt[Mathf.Min(wrongAttempts, pointsByAttempt.Length - 1)]
                : 0;
            score += points;

            answerButtons[choice].image.color = CorrectTint;
            hintText.text = points > 0
                ? $"정답! {CountryQuizData.Countries[choiceCountry[choice]].Name} +{points}점"
                : $"정답! {CountryQuizData.Countries[choiceCountry[choice]].Name}";
            UpdateHud();

            var flagRt = flagImage.rectTransform;
            Match3EffectSpawner.SpawnCelebration(this, flagRt.parent, flagRt.anchoredPosition);
            Match3EffectSpawner.SpawnPopupText(this, flagRt.parent, flagRt.anchoredPosition, "정답!", CorrectTextColor, 120f);

            yield return new WaitForSeconds(correctDelaySeconds);

            if (roundActive)
                NextQuestion();
        }

        // ----------------------------------------------------------------
        // HUD
        // ----------------------------------------------------------------

        private void UpdateHud()
        {
            scoreText.text = score.ToString();

            int secondsLeft = Mathf.CeilToInt(timeRemaining);
            if (secondsLeft == lastDisplayedSeconds)
                return;

            lastDisplayedSeconds = secondsLeft;
            timerText.text = $"{secondsLeft / 60}:{secondsLeft % 60:00}";
        }
    }
}

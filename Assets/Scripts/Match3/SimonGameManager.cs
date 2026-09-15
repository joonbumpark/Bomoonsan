using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Match3
{
    /// <summary>
    /// 순서 기억하기(사이먼 게임 스타일). 복주머니 몇 개가 색깔별로 순서대로 반짝이면
    /// 그대로 따라 탭한다. 성공하면 순서가 하나씩 길어지고, 틀리면 이미 딴 점수는
    /// 그대로 두고 새 순서(길이 1)부터 다시 시작한다 - 제한시간 내내 계속 도전할 수 있게.
    /// 점수는 "성공한 탭 총합"이라, 실수해도 이미 딴 점수는 안 깎인다.
    ///
    /// match3/whack과 달리 다음 순서가 "직전 시도의 성공/실패"에 따라 갈라지므로,
    /// 같은 시드를 줘도 두 플레이어의 실제 순서는 서로 달라질 수 있다 - 완전히 동일한
    /// 문제를 푸는 게 아니라 같은 난이도 규칙으로 서로 겨루는 게임이다.
    /// </summary>
    public class SimonGameManager : MonoBehaviour, IRoundGame
    {
        [Header("패드 설정")]
        public float padSize = 220f;
        public float padSpacing = 24f;

        [Header("라운드 설정")]
        public float roundDurationSeconds = 45f;

        [Header("타이밍")]
        public float playbackHighlightSeconds = 0.45f;
        public float playbackGapSeconds = 0.2f;
        public float delayBeforeSequenceSeconds = 0.6f;

        private static readonly Color[] PadColors =
        {
            new Color(0.91f, 0.30f, 0.24f), // 빨강
            new Color(0.20f, 0.60f, 0.86f), // 파랑
            new Color(0.30f, 0.69f, 0.31f), // 초록
            new Color(0.95f, 0.87f, 0.20f), // 노랑
        };

        public event Action<int> RoundEnded;
        public int CurrentScore => score;

        private const float TopBarHeight = 220f;
        private const float HintBarHeight = 100f;

        private GameObject canvasRoot;
        private Image[] pads;
        private Text scoreText;
        private Text timerText;
        private Text statusText;

        private int score;
        private bool roundActive;
        private bool acceptingInput;
        private float timeRemaining;
        private int lastDisplayedSeconds;
        private System.Random rng;

        private readonly List<int> sequence = new List<int>();
        private int inputIndex;

        private void Awake()
        {
            BuildUI();
            SetVisible(false);
        }

        public void SetVisible(bool visible) => canvasRoot.SetActive(visible);

        public void BeginRound(int? seed = null)
        {
            StopAllCoroutines();

            score = 0;
            roundActive = true;
            timeRemaining = roundDurationSeconds;
            lastDisplayedSeconds = -1;
            rng = new System.Random(seed ?? Environment.TickCount);

            for (int i = 0; i < pads.Length; i++)
                SetPadDim(i);

            UpdateHud();
            SetVisible(true);
            StartNewSequence();
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
                acceptingInput = false;
                StopAllCoroutines();
                UpdateHud();
                RoundEnded?.Invoke(score);
                return;
            }

            UpdateHud();
        }

        // ----------------------------------------------------------------
        // 시퀀스 진행
        // ----------------------------------------------------------------

        private void StartNewSequence()
        {
            sequence.Clear();
            sequence.Add(rng.Next(PadColors.Length));
            inputIndex = 0;
            StartCoroutine(PlaybackThenAcceptInput());
        }

        private void ExtendSequence()
        {
            sequence.Add(rng.Next(PadColors.Length));
            inputIndex = 0;
            StartCoroutine(PlaybackThenAcceptInput());
        }

        private IEnumerator PlaybackThenAcceptInput()
        {
            acceptingInput = false;
            statusText.text = "잘 보세요...";
            yield return new WaitForSeconds(delayBeforeSequenceSeconds);

            foreach (int pad in sequence)
            {
                if (!roundActive)
                    yield break;
                yield return StartCoroutine(FlashPad(pad));
                yield return new WaitForSeconds(playbackGapSeconds);
            }

            if (!roundActive)
                yield break;

            statusText.text = "따라 탭하세요!";
            acceptingInput = true;
        }

        private IEnumerator FlashPad(int index)
        {
            SetPadBright(index);
            yield return new WaitForSeconds(playbackHighlightSeconds);
            if (roundActive)
                SetPadDim(index);
        }

        private void OnPadTapped(int index)
        {
            if (!roundActive || !acceptingInput)
                return;

            if (index == sequence[inputIndex])
            {
                score++;
                inputIndex++;
                UpdateHud();

                if (inputIndex >= sequence.Count)
                {
                    acceptingInput = false;
                    ExtendSequence();
                }
            }
            else
            {
                acceptingInput = false;
                StartCoroutine(WrongTapThenRestart());
            }
        }

        private IEnumerator WrongTapThenRestart()
        {
            statusText.text = "틀렸습니다! 새 순서로 다시...";
            yield return new WaitForSeconds(delayBeforeSequenceSeconds);
            if (roundActive)
                StartNewSequence();
        }

        private void SetPadBright(int index) => pads[index].color = PadColors[index];

        private void SetPadDim(int index) => pads[index].color = Color.Lerp(PadColors[index], Color.black, 0.35f);

        // ----------------------------------------------------------------
        // UI 생성
        // ----------------------------------------------------------------

        private void BuildUI()
        {
            canvasRoot = UIFactory.CreateGameCanvasRoot(transform, "SimonCanvas");
            UIFactory.CreateGameTopBar(canvasRoot.transform, TopBarHeight, out scoreText, out timerText);
            BuildPads(canvasRoot.transform);
            statusText = UIFactory.CreateGameHint(canvasRoot.transform, HintBarHeight, "잘 보세요...");
        }

        private void BuildPads(Transform parent)
        {
            int count = PadColors.Length;
            float totalW = count * padSize + (count - 1) * padSpacing;

            var root = UIFactory.CreateRect("PadRoot", parent);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(totalW, padSize);
            root.anchoredPosition = new Vector2(0, (HintBarHeight - TopBarHeight) / 2f);

            pads = new Image[count];

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject($"Pad_{i}", typeof(RectTransform));
                go.transform.SetParent(root, false);

                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(padSize, padSize);
                float x = -totalW / 2f + padSize / 2f + i * (padSize + padSpacing);
                rt.anchoredPosition = new Vector2(x, 0);

                var img = go.AddComponent<Image>();
                img.sprite = TileArt.Base;
                img.preserveAspect = true;

                var button = go.AddComponent<Button>();
                button.transition = Selectable.Transition.None; // 색은 직접 관리하므로 기본 트윈은 끈다.
                int idx = i;
                button.onClick.AddListener(() => OnPadTapped(idx));

                pads[i] = img;
                SetPadDim(i);
            }
        }

        // ----------------------------------------------------------------
        // HUD
        // ----------------------------------------------------------------

        private void UpdateHud()
        {
            scoreText.text = $"점수: {score}";

            int secondsLeft = Mathf.CeilToInt(timeRemaining);
            if (secondsLeft == lastDisplayedSeconds)
                return;

            lastDisplayedSeconds = secondsLeft;
            timerText.text = $"남은 시간: {secondsLeft / 60}:{secondsLeft % 60:00}";
        }
    }
}

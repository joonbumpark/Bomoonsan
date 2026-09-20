using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;

namespace Mountains
{
    // 보물상자 연출: 금화가 주변으로 튀었다가(DOJump) 잠깐 멈춘 뒤 플레이어에게 빨려들어가며
    // 작아져 사라진다. 코인이 런타임에 생성되므로 범용 Tween 액션(TweenJumpAction 등)으로는
    // 대상을 지정할 수 없어 전용 액션으로 만들었다.
    public class CoinBurstAction : TriggerAction
    {
        [Header("코인")]
        public GameObject coinPrefab;
        [Min(1)] public int count = 10;
        [Tooltip("코인이 튀어나올 지점. 비워두면 이 액션이 붙어있는 오브젝트 위치.")]
        public Transform origin;

        [Header("튀어나가기")]
        [Tooltip("코인이 흩어질 반경(월드 단위).")]
        public float scatterRadius = 3f;
        public float scatterDuration = 0.5f;
        [Tooltip("포물선의 높이. 클수록 높이 뜬다.")]
        public float jumpPower = 3f;
        [Tooltip("착지까지 몇 번 튕길지.")]
        [Min(1)] public int numJumps = 1;
        [Tooltip("코인 피벗이 바닥이 아닐 때 지면에서 띄울 높이.")]
        public float groundOffset = 0.5f;
        [Tooltip("튀어나간 시점부터 이 시간(초) 뒤에 빨려들어가기 시작한다.")]
        [Min(0f)] public float gatherStartTime = 0.3f;
        [Tooltip("튀어나갈 때 추가로 회전할 각도 (예: 360도 회전 시 Vector3.one * 360f)")]
        public Vector3 scatterAddRotation = new Vector3(0f, 360f, 0f);

        [Header("빨려들어가기")]
        [Tooltip("비워두면 Player 태그로 자동 탐색한다.")]
        public Transform player;
        [Tooltip("플레이어 기준 도착 지점. 발밑이 아니라 가슴 높이로 빨려들어가게 올려둔다.")]
        public Vector3 playerOffset = new Vector3(0f, 1f, 0f);
        public float gatherDuration = 0.45f;
        public Ease gatherEase = Ease.InQuad;
        [Tooltip("코인마다 출발을 이만큼 늦춘다 — 0이면 전부 동시에, 크면 줄줄이 빨려들어간다.")]
        public float perCoinDelay = 0.05f;
        [Tooltip("빨려들어갈 때 추가로 회전할 총 각도")]
        public Vector3 gatherAddRotation = new Vector3(0f, 720f, 0f);

        GameManager _gameManager;

        public override void Execute(Action onComplete)
        {
            if (coinPrefab == null)
            {
                Debug.LogWarning("[CoinBurstAction] coinPrefab이 비어 있어 연출을 건너뜁니다.");
                onComplete?.Invoke();
                return;
            }

            StartCoroutine(Run(onComplete));
        }

        IEnumerator Run(Action onComplete)
        {
            _gameManager = FindFirstObjectByType<GameManager>();
            Vector3 originPosition = origin != null ? origin.position : transform.position;

            var coins = new Transform[count];
            for (int i = 0; i < count; i++)
            {
                coins[i] = SpawnScatteredCoin(originPosition);
            }

            yield return new WaitForSeconds(gatherStartTime);

            var counter = new CompletionCounter(count, onComplete);
            for (int i = 0; i < count; i++)
            {
                StartCoroutine(GatherCoin(coins[i], i * perCoinDelay, counter.Signal));
            }

            while (!counter.IsDone)
            {
                yield return null;
            }
        }

        Transform SpawnScatteredCoin(Vector3 originPosition)
        {
            var coin = Instantiate(coinPrefab, originPosition, Quaternion.identity).transform;

            DisablePickup(coin.gameObject);

            Vector3 targetPos = RandomScatterPoint(originPosition);
            
            // 점프 이동과 함께 회전 연출 추가
            coin.DOJump(targetPos, jumpPower, numJumps, scatterDuration);
            
            if (scatterAddRotation != Vector3.zero)
            {
                coin.DORotate(coin.eulerAngles + scatterAddRotation, scatterDuration, RotateMode.FastBeyond360)
                    .SetEase(Ease.OutQuad);
            }

            return coin;
        }

        static void DisablePickup(GameObject coin)
        {
            var pickup = coin.GetComponent<Coin>();
            if (pickup != null)
            {
                pickup.enabled = false;
            }

            foreach (var collider in coin.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }
        }

        Vector3 RandomScatterPoint(Vector3 originPosition)
        {
            Vector2 circle = UnityEngine.Random.insideUnitCircle * scatterRadius;
            Vector3 point = originPosition + new Vector3(circle.x, 0f, circle.y);

            if (_gameManager != null)
            {
                return _gameManager.GetTerrainPosition(point.x, point.z, groundOffset);
            }

            point.y += groundOffset;
            return point;
        }

        IEnumerator GatherCoin(Transform coin, float delay, Action onFinished)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            if (coin == null)
            {
                onFinished?.Invoke();
                yield break;
            }

            coin.DOKill();

            Vector3 startPosition = coin.position;
            Vector3 startScale = coin.localScale;
            Quaternion startRotation = coin.rotation;
            
            float elapsed = 0f;
            float safeDuration = Mathf.Max(0.01f, gatherDuration);

            while (elapsed < safeDuration)
            {
                if (coin == null)
                {
                    onFinished?.Invoke();
                    yield break;
                }

                elapsed += Time.deltaTime;
                float eased = DOVirtual.EasedValue(0f, 1f, Mathf.Clamp01(elapsed / safeDuration), gatherEase);
                
                // 위치, 크기, 회전 동시 갱신 (유도 및 회전 동선 적용)
                coin.position = Vector3.Lerp(startPosition, ResolveTargetPosition(), eased);
                coin.localScale = startScale * (1f - eased);
                coin.rotation = startRotation * Quaternion.Euler(gatherAddRotation * eased);

                yield return null;
            }

            if (coin != null)
            {
                Destroy(coin.gameObject);
            }

            onFinished?.Invoke();
        }

        Vector3 ResolveTargetPosition()
        {
            if (player == null)
            {
                var found = GameObject.FindGameObjectWithTag("Player");
                if (found != null)
                {
                    player = found.transform;
                }
            }

            return player != null ? player.position + playerOffset : transform.position;
        }
    }
}
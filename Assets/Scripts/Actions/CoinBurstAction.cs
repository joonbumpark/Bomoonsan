using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace Mountains
{
    // 보물상자 연출: 금화가 주변으로 튀었다가(DOJump) 플레이어에게 빨려들어가며 작아져 사라진다.
    [Serializable]
    public class CoinBurstAction : TriggerAction
    {
        [Header("코인")]
        public GameObject coinPrefab;
        [Min(1)] public int count = 10;
        [Tooltip("코인이 튀어나올 지점. 비워두면 트리거 위치.")]
        [GizmoTarget("금화")] public Transform origin;

        [Header("튀어나가기")]
        public float scatterRadius = 3f;
        public float scatterDuration = 0.5f;
        [Tooltip("포물선의 높이.")]
        public float jumpPower = 3f;
        [Min(1)] public int numJumps = 1;
        [Tooltip("코인 피벗이 바닥이 아닐 때 지면에서 띄울 높이.")]
        public float groundOffset = 0.5f;
        [Tooltip("튀어나간 시점부터 이 시간(초) 뒤에 빨려들어가기 시작한다. scatterDuration보다 " +
            "작으면 아직 공중에 있을 때 끌려온다.")]
        [Min(0f)] public float gatherStartTime = 0.3f;

        [Header("빨려들어가기")]
        [Tooltip("비워두면 Player 태그로 자동 탐색한다.")]
        public Transform player;
        [Tooltip("플레이어 기준 도착 지점. 발밑이 아니라 가슴 높이로 올려둔다.")]
        public Vector3 playerOffset = new Vector3(0f, 1f, 0f);
        public float gatherDuration = 0.45f;
        public Ease gatherEase = Ease.InQuad;
        [Tooltip("코인마다 출발을 이만큼 늦춘다 — 0이면 전부 동시에.")]
        public float perCoinDelay = 0.05f;

        GameManager _gameManager;

        public override async UniTask ExecuteAsync(TriggerContext context, CancellationToken cancellationToken)
        {
            if (coinPrefab == null)
            {
                Debug.LogWarning("[CoinBurstAction] coinPrefab이 비어 있어 연출을 건너뜁니다.");
                return;
            }

            _gameManager = UnityEngine.Object.FindFirstObjectByType<GameManager>();
            Vector3 originPosition = origin != null ? origin.position : context.Transform.position;

            var coins = new Transform[count];
            for (int i = 0; i < count; i++)
            {
                coins[i] = SpawnScatteredCoin(originPosition);
            }

            await UniTask.Delay(TimeSpan.FromSeconds(gatherStartTime), cancellationToken: cancellationToken);

            var tasks = new UniTask[count];
            for (int i = 0; i < count; i++)
            {
                tasks[i] = GatherCoin(coins[i], i * perCoinDelay, cancellationToken);
            }

            await UniTask.WhenAll(tasks);
        }

        Transform SpawnScatteredCoin(Vector3 originPosition)
        {
            var coin = UnityEngine.Object.Instantiate(coinPrefab, originPosition, Quaternion.identity).transform;

            // 날아가는 동안 플레이어에 스칠 수 있는데, Coin이 그때 스스로 파괴되면 연출이
            // 중간에 끊긴다 — 줍기 판정을 끄고 도착 처리를 이 액션이 직접 한다.
            DisablePickup(coin.gameObject);

            coin.DOJump(RandomScatterPoint(originPosition), jumpPower, numJumps, scatterDuration);
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

            // 기울어진 사면에 상자를 놔도 코인이 뜨거나 파묻히지 않게 착지 지점의 지형 높이를 쓴다.
            if (_gameManager != null)
            {
                return _gameManager.GetTerrainPosition(point.x, point.z, groundOffset);
            }

            point.y += groundOffset;
            return point;
        }

        // DOMove로 플레이어 위치까지 트윈하면 목표가 고정돼서, 빨려들어가는 동안 플레이어가
        // 걸어가면 엉뚱한 허공으로 모인다 — 매 프레임 현재 위치를 다시 보는 유도로 처리하고,
        // 속도 곡선만 DOTween의 ease를 그대로 쓴다.
        async UniTask GatherCoin(Transform coin, float delay, CancellationToken cancellationToken)
        {
            if (delay > 0f)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: cancellationToken);
            }

            if (coin == null)
            {
                return;
            }

            // 아직 공중에 있는(=DOJump이 도는 중인) 코인을 끌어오려면 먼저 그 트윈을 죽여야
            // 한다 — 살려두면 매 프레임 위치를 덮어써서 유도가 전혀 먹히지 않는다.
            coin.DOKill();

            Vector3 startPosition = coin.position;
            Vector3 startScale = coin.localScale;
            float elapsed = 0f;
            float safeDuration = Mathf.Max(0.01f, gatherDuration);

            while (elapsed < safeDuration)
            {
                if (coin == null)
                {
                    return;
                }

                elapsed += Time.deltaTime;
                float eased = DOVirtual.EasedValue(0f, 1f, Mathf.Clamp01(elapsed / safeDuration), gatherEase);
                coin.position = Vector3.Lerp(startPosition, ResolveTargetPosition(coin), eased);
                coin.localScale = startScale * (1f - eased);
                await UniTask.Yield(cancellationToken);
            }

            if (coin != null)
            {
                UnityEngine.Object.Destroy(coin.gameObject);
            }
        }

        Vector3 ResolveTargetPosition(Transform fallback)
        {
            if (player == null)
            {
                player = PlayerLocator.Resolve(null);
            }

            return player != null ? player.position + playerOffset : fallback.position;
        }
    }
}

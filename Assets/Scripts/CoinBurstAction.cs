using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;

namespace Mountains
{
    // 보물상자 연출: 금화가 주변으로 튀었다가(DOJump) 잠깐 멈춘 뒤 플레이어에게 빨려들어가며
    // 작아져 사라진다. 코인이 런타임에 생성되므로 범용 Tween 액션(TweenJumpAction 등)으로는
    // 대상을 지정할 수 없어 전용 액션으로 만들었다.
    //
    // 기존 Reward.GenerateCoin은 "튀어나가기"까지만 하고 그대로 바닥에 남는다(플레이어가
    // 직접 주워야 함) — 씬에서 UnityEvent로 연결돼 동작 중이라 건드리지 않았다.
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
        [Tooltip("튀어나간 시점부터 이 시간(초) 뒤에 빨려들어가기 시작한다. scatterDuration보다 " +
            "작으면 아직 공중에 있을 때 끌려오고(정점 부근이 가장 자연스럽다), 크면 착지한 뒤 " +
            "그만큼 기다렸다 끌려온다. 0이면 흩어지지 않고 곧바로 빨려들어간다.")]
        [Min(0f)] public float gatherStartTime = 0.3f;

        [Header("빨려들어가기")]
        [Tooltip("비워두면 Player 태그로 자동 탐색한다.")]
        public Transform player;
        [Tooltip("플레이어 기준 도착 지점. 발밑이 아니라 가슴 높이로 빨려들어가게 올려둔다.")]
        public Vector3 playerOffset = new Vector3(0f, 1f, 0f);
        public float gatherDuration = 0.45f;
        public Ease gatherEase = Ease.InQuad;
        [Tooltip("코인마다 출발을 이만큼 늦춘다 — 0이면 전부 동시에, 크면 줄줄이 빨려들어간다.")]
        public float perCoinDelay = 0.05f;

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

            // 지형이 기울어진 곳에 상자를 놔도 코인이 공중에 뜨거나 파묻히지 않게, 착지
            // 지점의 실제 지형 높이를 구해서 쓴다(식생 배치·물 판정에 쓰는 것과 같은 API).
            if (_gameManager != null)
            {
                return _gameManager.GetTerrainPosition(point.x, point.z, groundOffset);
            }

            point.y += groundOffset;
            return point;
        }

        // DOMove로 플레이어 위치까지 트윈하면 목표가 고정돼서, 빨려들어가는 동안 플레이어가
        // 걸어가면 엉뚱한 허공으로 모인다 — 매 프레임 현재 위치를 다시 보는 유도(homing)로
        // 처리하고, 속도 곡선만 DOTween의 ease를 그대로 쓴다.
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

            // 아직 공중에 있는(=DOJump이 도는 중인) 코인을 끌어오려면 먼저 그 트윈을 죽여야
            // 한다 — 살려두면 DOJump이 매 프레임 위치를 자기 값으로 덮어써서 유도가 전혀
            // 먹히지 않는다. complete:false라 코인은 지금 있는 자리에서 그대로 출발한다.
            coin.DOKill();

            Vector3 startPosition = coin.position;
            Vector3 startScale = coin.localScale;
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
                coin.position = Vector3.Lerp(startPosition, ResolveTargetPosition(), eased);
                coin.localScale = startScale * (1f - eased);
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
                // 플레이어는 CharacterManager가 런타임에 만들기 때문에 인스펙터로 미리
                // 걸어둘 수 없다 — 다른 NPC 스크립트와 같은 방식으로 태그로 찾는다.
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

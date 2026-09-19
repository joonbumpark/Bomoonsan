using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mountains
{
    public class GameManager : MonoBehaviour
    {
        [Tooltip("비워두면 CharacterManager가 게임 시작 시 자동으로 만든다.")]
        public Player Player;
        [Tooltip("비워두면 씬에서 자동으로 찾는다.")]
        public ProceduralTerrainMesh Terrain;

        [Header("플레이어 스폰")]
        [Tooltip("Player가 비어 있으면 게임 시작 시 이 데이터로 CharacterManager가 만든다.")]
        public CharacterData playerCharacterData;
        [Tooltip("비워두면 지형 중앙에 스폰한다.")]
        public Transform playerSpawnPoint;

        [System.Serializable]
        public class NpcSpawnEntry
        {
            public CharacterData characterData;
            [Tooltip("비워두면 플레이어 스폰 위치에서 살짝 옆으로 띄워서 스폰한다.")]
            public Transform spawnPoint;
            [Tooltip("비워두면(0,0,0) 자동으로 플레이어 뒤 부채꼴 대형 자리를 배정한다. " +
                "직접 지정하면 그 값을 그대로 NpcFollower.followOffset으로 쓴다.")]
            public Vector3 followOffset;
        }

        [Header("NPC 스폰 (테스트용)")]
        [Tooltip("여러 개 등록해서 한 번에 여러 NPC를 스폰할 수 있다.")]
        public NpcSpawnEntry[] npcSpawns = new NpcSpawnEntry[0];

        void Awake()
        {

#if !UNITY_EDITOR && UNITY_ANDROID
            Application.targetFrameRate = 60;
#endif

            if (Terrain == null)
            {
                Terrain = FindFirstObjectByType<ProceduralTerrainMesh>();
            }
        }

        // Player/NPC 생성은 Start()에서 한다 — Unity는 같은 프레임의 모든 Awake()가 어떤
        // Start()보다도 먼저 끝나는 걸 보장하므로, CharacterManager.Instance가 스크립트
        // 실행 순서와 무관하게 이미 준비돼 있다.
        void Start()
        {
            Vector3 defaultSpawnPos = GetDefaultSpawnPosition();
            SpawnPlayerIfNeeded(defaultSpawnPos);
            SpawnTestNpcIfNeeded(defaultSpawnPos);
        }

        Vector3 GetDefaultSpawnPosition()
        {
            if (Terrain != null)
            {
                return Terrain.GetVertexWorldPosition(Terrain.width / 2, Terrain.length / 2);
            }
            return Vector3.zero;
        }

        void SpawnPlayerIfNeeded(Vector3 defaultSpawnPos)
        {
            if (Player != null)
            {
                return;
            }

            var existing = GameObject.FindGameObjectWithTag("Player");
            if (existing != null)
            {
                Player = existing.GetComponent<Player>();
                return;
            }

            Vector3 spawnPos = playerSpawnPoint != null ? playerSpawnPoint.position : defaultSpawnPos;
            if (Terrain == null && playerSpawnPoint == null)
            {
                Debug.LogWarning("[GameManager] terrain/playerSpawnPoint가 모두 없어 플레이어를 스폰할 위치를 정할 수 없습니다.");
                return;
            }

            var playerGo = CharacterManager.Instance.CreatePlayer(playerCharacterData, spawnPos);
            Player = playerGo.GetComponent<Player>();
        }

        void SpawnTestNpcIfNeeded(Vector3 defaultSpawnPos)
        {
            for (int i = 0; i < npcSpawns.Length; i++)
            {
                var entry = npcSpawns[i];
                if (entry == null || entry.characterData == null)
                {
                    continue;
                }

                // spawnPoint가 없으면 플레이어 스폰 위치에서 옆으로 띄운다 — 정확히 같은
                // 자리면 NavMeshAgent 로컬 회피가 서로 밀어내는 것처럼 보인다. 여러 개를
                // 한꺼번에 기본 위치로 스폰할 때 겹치지 않도록 순서대로 더 멀리 벌린다.
                Vector3 spawnPos = entry.spawnPoint != null
                    ? entry.spawnPoint.position
                    : playerSpawnPoint != null ? playerSpawnPoint.position
                    : defaultSpawnPos + Vector3.right * (5f + i * 2f);
                var npcGo = CharacterManager.Instance.CreateNpc(entry.characterData, spawnPos);

                var follower = npcGo != null ? npcGo.GetComponent<NpcFollower>() : null;
                if (follower != null)
                {
                    follower.followOffset = entry.followOffset != Vector3.zero
                        ? entry.followOffset
                        : DefaultFollowOffset(i);
                }
            }
        }

        // 플레이어 뒤쪽에 부채꼴로 자리를 나눠준다 — 여러 NPC가 한 점에 몰리지 않고
        // 각자 다른 슬롯을 따라가게 하는 기본값. followOffset을 직접 지정하지 않은
        // npcSpawns 항목에만 쓰인다.
        static Vector3 DefaultFollowOffset(int index)
        {
            float[] angles = { 0f, -35f, 35f, -60f, 60f };
            float angle = angles[index % angles.Length];
            float distance = 2.5f + (index / angles.Length) * 1.5f;
            return Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, 0f, -distance);
        }

        // 월드 XZ 좌표(x, z)의 지형 높이에 맞춘 월드 위치를 구한다. WorldToGrid/
        // GetWorldPositionAt은 이미 식생 배치·물 판정 등에 쓰던 지형 높이 조회 API를
        // 그대로 재사용한 것 — 지형이 기울어지거나(Transform 회전) 스케일이 달라도
        // 정확한 월드 위치가 나온다. yOffset으로 표면보다 살짝 띄우거나(오브젝트 피벗이
        // 바닥이 아닐 때) 파묻을 수 있다.
        public Vector3 GetTerrainPosition(float x, float z, float yOffset = 0f)
        {
            if (Terrain == null)
            {
                Debug.LogWarning("[GameManager] terrain이 없어 위치를 구할 수 없습니다.");
                return Vector3.zero;
            }

            Vector2 grid = Terrain.WorldToGrid(new Vector3(x, 0f, z));
            Vector3 pos = Terrain.GetWorldPositionAt(grid.x, grid.y);
            pos.y += yOffset;
            return pos;
        }

        public void PlaceOnTerrain(GameObject target, float x, float z, float yOffset = 0f)
        {
            if (target == null || Terrain == null)
            {
                Debug.LogWarning("[GameManager] target 또는 terrain이 없어 배치를 건너뜁니다.");
                return;
            }

            target.transform.position = GetTerrainPosition(x, z, yOffset);
        }
    }
}

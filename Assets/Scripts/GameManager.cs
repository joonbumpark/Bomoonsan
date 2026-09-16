using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mountains
{
    public class GameManager : MonoBehaviour
    {
        [Tooltip("비워두면 씬에서 자동으로 찾는다.")]
        public Player Player;
        [Tooltip("비워두면 씬에서 자동으로 찾는다.")]
        public ProceduralTerrainMesh Terrain;
        public int CoinAmount;

        public DialogData CoinFull;

        void Awake()
        {
            if (Player == null)
            {
                Player = GameObject.FindGameObjectWithTag("Player")?.GetComponent<Player>();
            }

            if (Terrain == null)
            {
                Terrain = FindObjectOfType<ProceduralTerrainMesh>();
            }
        }

        public void IncCoin()
        {
            CoinAmount++;

            if (CoinAmount >= 10)
            {
                DialogUI.Instance.Play(CoinFull);
            }
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

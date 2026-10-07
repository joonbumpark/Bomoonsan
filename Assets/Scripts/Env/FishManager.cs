using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mountains.Env
{
    // [예전 구현] 마리당 GameObject + MonoBehaviour로 돌리는 방식. 새 작업에는 FishSchool을
    // 쓴다 — 같은 보이드 규칙을 Burst 잡과 격자로 계산하고 GPU 인스턴싱으로 그린다.
    // 이미 이 컴포넌트로 맞춰둔 씬이 있어 남겨둔다.
    public class FishManager : MonoBehaviour
    {
        public GameObject fishPrefab;
        public int spawnCount = 80;

        [Header("Rect Bounds Settings")]
        public Vector2 boundsSize = new Vector2(40f, 25f); // (가로 X 크기, 세로 Z 크기)
        public Transform predatorTransform;

        private List<Fish> fishes = new List<Fish>();

        void Start()
        {
            for (int i = 0; i < spawnCount; i++)
            {
                // Rect 영역 안에서 랜덤 스폰
                float rx = Random.Range(-boundsSize.x * 0.4f, boundsSize.x * 0.4f);
                float rz = Random.Range(-boundsSize.y * 0.4f, boundsSize.y * 0.4f);
                Vector3 spawnPos = transform.position + new Vector3(rx, 0, rz);

                GameObject obj = Instantiate(fishPrefab, spawnPos, Quaternion.identity, transform);
                Fish fish = obj.GetComponent<Fish>();
                if (fish != null) fishes.Add(fish);
            }
        }

        void Update()
        {
            Vector3 predatorPos = predatorTransform != null ? predatorTransform.position : Vector3.one * 9999f;

            foreach (Fish fish in fishes)
            {
                fish.Flock(fishes, predatorPos, boundsSize, transform.position);
                fish.UpdatePhysics();
            }
        }

        // 에디터 씬 뷰에서 직사각형 영역 기즈모 그리기
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Vector3 size = new Vector3(boundsSize.x, 0.1f, boundsSize.y);
            Gizmos.DrawWireCube(transform.position, size);
        }
    }
}
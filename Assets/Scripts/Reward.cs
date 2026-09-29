using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace Mountains
{
    public class Reward : MonoBehaviour
    {
        public GameManager gameManager;
        public GameObject CoinPrefab;
        public float Radius;
        public float jumpPower;
        public float Duration;

        //[ContextMenu("Test")]
        public void GenerateCoin()
        {
            for (int i = 0; i < 10; i++)
            {
                var go = GameObject.Instantiate(CoinPrefab);

                var start = transform.position;
                var vector2 = GetRandomPosition2D(transform, Radius);
                var targetPos = gameManager.GetTerrainPosition(vector2.x, vector2.y, .5f);

                go.transform.position = start;
                go.transform.DOJump(targetPos, jumpPower: jumpPower, numJumps: 1, duration: Duration);
            }
        }

        public static Vector2 GetRandomPosition2D(Transform centerTransform, float radius)
        {
            // 반지름 1짜리 원 내부의 랜덤한 Vector2(x, y) 생성
            Vector2 randomCircle = Random.insideUnitCircle * radius;
            // Vector2의 y값을 3D의 z축으로 변환하여 Y축 높이 유지
            Vector2 randomPos = new Vector2(centerTransform.position.x, centerTransform.position.z) + randomCircle;
            return randomPos;
        }
    }
}
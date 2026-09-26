using UnityEngine;

namespace Mountains
{
    // 물 표면에 무언가 들어오면 닿은 지점에 물튀김 파티클을 띄운다. 물 오브젝트의
    // Collider(Is Trigger)에 붙여서 쓴다.
    public class WaterSurface : MonoBehaviour
    {
        [Tooltip("물에 닿을 때 띄울 파티클 프리팹.")]
        public GameObject splashEffectPrefab;
        [Tooltip("파티클을 이 시간 뒤에 파괴한다. 0 이하면 두지 않는다(스스로 정리하는 FX).")]
        public float splashLifetime = 2f;
        [Tooltip("생성할 때 적용할 회전. 파티클이 위로 솟게 만들어졌으면 (-90, 0, 0).")]
        public Vector3 splashRotation = new Vector3(-90f, 0f, 0f);

        void OnTriggerEnter(Collider other)
        {
            // 닿은 물체의 XZ 위치에, 높이는 물 표면에 맞춘다.
            Vector3 spawnPosition = other.transform.position;
            spawnPosition.y = transform.position.y;

            FxUtility.Spawn(splashEffectPrefab, spawnPosition, Quaternion.Euler(splashRotation),
                splashLifetime);
        }
    }
}

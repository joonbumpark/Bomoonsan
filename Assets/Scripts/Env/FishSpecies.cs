using System;
using UnityEngine;

namespace Mountains.Env
{
    // 물고기 한 종류. 프리팹에서 메시와 머티리얼만 뽑아 쓰고 GameObject는 만들지 않는다 —
    // 물고기 프리팹은 Animator 없는 정적 MeshRenderer라 GPU 인스턴싱으로 한 번에 그릴 수 있다.
    [Serializable]
    public class FishSpecies
    {
        public GameObject prefab;

        [Tooltip("이 종류가 뽑힐 상대 비중. 2면 1짜리보다 두 배 자주 나온다. 0이면 스폰하지 않는다.")]
        [Min(0f)] public float weight = 1f;

        [Tooltip("개체마다 이 범위에서 크기를 뽑는다(프리팹 스케일에 곱해진다).")]
        public Vector2 scaleRange = new Vector2(0.8f, 1.2f);

        [Tooltip("이 종류의 속도 배수. 작은 물고기를 빠르게 하고 싶을 때 쓴다.")]
        [Min(0.01f)] public float speedMultiplier = 1f;

        [Tooltip("모델이 +Z를 바라보지 않을 때 보정할 회전(도). 물고기마다 축이 달라 종류별로 둔다.")]
        public Vector3 modelRotationOffset;

        // 프리팹에서 한 번만 뽑아 두는 값들. 런타임에 GameObject를 만들지 않으므로
        // 여기 담아두고 인스턴싱 드로우에 그대로 쓴다.
        public Mesh Mesh { get; private set; }
        public Material Material { get; private set; }
        // 프리팹 루트 기준으로 메시가 어디에 어떻게 놓여 있는지(위치·회전·스케일).
        // 메시가 자식에 있고 FBX 축 변환 때문에 회전이 걸린 프리팹도 그대로 재현하려면
        // 이 행렬을 인스턴스 변환 뒤에 곱해야 한다 — 루트만 보면 몸이 비스듬해진다.
        public Matrix4x4 MeshToRoot { get; private set; } = Matrix4x4.identity;

        public bool IsUsable => Mesh != null && Material != null && weight > 0f;

        // 인스턴싱이 꺼져 있을 때 만든 복제본. 원본 에셋을 건드리지 않기 위한 것이라
        // 다 쓰면 반드시 파괴해야 한다.
        Material _runtimeMaterial;

        // RenderMeshInstanced는 머티리얼의 "Enable GPU Instancing"이 꺼져 있으면 예외를
        // 던진다. 원본 머티리얼의 그 값을 코드로 켜면 에디터에서 .mat 에셋이 dirty로
        // 표시돼 저장/빌드 때 디스크에 기록된다 — 그래서 켜진 복제본을 따로 만들어 쓴다.
        // 에셋에서 직접 체크해두면 복제 없이 원본을 그대로 쓴다.
        Material ResolveInstancedMaterial(Material source)
        {
            if (source == null || source.enableInstancing)
            {
                return source;
            }

            _runtimeMaterial = new Material(source)
            {
                enableInstancing = true,
                hideFlags = HideFlags.HideAndDontSave
            };
            Debug.Log($"[FishSpecies] '{source.name}'의 Enable GPU Instancing이 꺼져 있어 " +
                "인스턴싱용 복제본을 만들었습니다. 에셋에서 켜두면 복제 없이 그대로 씁니다.");
            return _runtimeMaterial;
        }

        public void ReleaseRuntimeMaterial()
        {
            if (_runtimeMaterial == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(_runtimeMaterial);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(_runtimeMaterial);
            }

            _runtimeMaterial = null;
            Material = null;
        }

        // 프리팹 계층 어디에 메시가 있든(루트든 자식이든) 찾아낸다.
        public bool Resolve()
        {
            if (prefab == null)
            {
                return false;
            }

            var filter = prefab.GetComponentInChildren<MeshFilter>();
            var renderer = prefab.GetComponentInChildren<MeshRenderer>();
            if (filter == null || renderer == null)
            {
                Debug.LogWarning($"[FishSpecies] {prefab.name}에 MeshFilter/MeshRenderer가 없어 건너뜁니다.");
                return false;
            }

            Mesh = filter.sharedMesh;
            Material = ResolveInstancedMaterial(renderer.sharedMaterial);
            MeshToRoot = prefab.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;

            return Mesh != null && Material != null;
        }
    }
}

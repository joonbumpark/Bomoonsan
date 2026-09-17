using System.Collections.Generic;
using UnityEngine;

namespace Mountains
{
    // 카메라와 플레이어 사이에 나무가 끼면 시야를 가리는 문제를 해결한다. 나무
    // 프리팹엔 Collider가 없어서 레이캐스트로는 감지가 안 되므로, 순수 기하 계산
    // (카메라-플레이어 선분과 나무 위치 사이의 거리)으로 "가리는지"를 판정하고,
    // 가려지는 나무만 런타임에 만든 반투명 머티리얼로 바꿔 알파를 부드럽게 낮춘다.
    // 위치 데이터만으로 판정하는 방식이라, 나중에 식생을 GPU 인스턴싱으로 옮겨도
    // 같은 방식(인스턴싱용 MaterialPropertyBlock)으로 이어갈 수 있다.
    public class TreeViewOcclusionFader : MonoBehaviour
    {
        public Transform player;
        public VegetationScatter vegetationScatter;

        [Tooltip("카메라-플레이어 선분에서 이 반경(월드 단위) 안에 있는 나무를 가리는 것으로 판정한다.")]
        public float occlusionRadius = 1.5f;
        [Tooltip("가려질 때 나무가 도달하는 최소 알파.")]
        [Range(0f, 1f)] public float fadedAlpha = 0.25f;
        [Tooltip("초당 알파 변화량 — 클수록 빨리 반투명해지고 돌아온다.")]
        public float fadeSpeed = 3f;

        class TreeEntry
        {
            public Transform transform;
            // 나무 프리팹에 LODGroup이 있어 렌더러가 LOD0/1/2로 나뉘어 있다. 하나만
            // 골라 페이드시키면 실제로 활성화된 LOD가 아닐 수 있어 겉보기엔 아무
            // 변화도 없는 것처럼 보인다 — 전부 잡아서 다 같이 페이드시킨다.
            public Renderer[] renderers;
            public Material[][] originalMaterials;
            public float currentAlpha = 1f;
            public bool faded;
        }

        readonly List<TreeEntry> _trees = new List<TreeEntry>();
        readonly Dictionary<Material, Material> _fadeMaterialCache = new Dictionary<Material, Material>();
        MaterialPropertyBlock _propertyBlock;
        Camera _camera;

        void Start()
        {
            _camera = GetComponent<Camera>();
            _propertyBlock = new MaterialPropertyBlock();

            // VegetationScatter가 런타임 중 다시 배치/비우기를 하면 지금 들고 있는
            // Transform들이 파괴되므로, 그때마다 알림을 받아 목록을 다시 수집한다 —
            // 이게 없으면 다음 LateUpdate에서 파괴된 Transform에 접근해
            // MissingReferenceException이 난다.
            if (vegetationScatter != null)
            {
                vegetationScatter.InstancesChanged += CollectTrees;
            }

            CollectTrees();
        }

        void OnDestroy()
        {
            if (vegetationScatter != null)
            {
                vegetationScatter.InstancesChanged -= CollectTrees;
            }
        }

        // VegetationScatter가 다시 배치하면 인스턴스가 전부 새로 생기므로, 필요하면
        // 밖에서(TerrainSceneSetup 등) 다시 호출해 목록을 갱신할 수 있게 public으로 둔다.
        public void CollectTrees()
        {
            _trees.Clear();
            if (vegetationScatter == null)
            {
                return;
            }

            // 어떤 그룹을 페이드할지는 VegetationScatter의 그룹 정의(fadeWhenOccludingPlayer)가
            // 단일 기준이다 — 여기에 라벨 목록을 따로 두면 그룹을 추가할 때마다 양쪽을 맞춰야
            // 하고, 한쪽을 빠뜨리면 조용히 동작하지 않는다(Tree2를 추가했더니 페이드가 안 걸리던 버그).
            foreach (var t in vegetationScatter.GetInstancesToFadeWhenOccluding())
            {
                var renderers = t.GetComponentsInChildren<Renderer>(true);
                if (renderers == null || renderers.Length == 0)
                {
                    continue;
                }

                var originalMaterials = new Material[renderers.Length][];
                for (int i = 0; i < renderers.Length; i++)
                {
                    originalMaterials[i] = renderers[i].sharedMaterials;
                }

                _trees.Add(new TreeEntry
                {
                    transform = t,
                    renderers = renderers,
                    originalMaterials = originalMaterials,
                    currentAlpha = 1f,
                    faded = false,
                });
            }
        }

        void LateUpdate()
        {
            if (player == null || _camera == null || _trees.Count == 0)
            {
                return;
            }

            Vector3 camPos = _camera.transform.position;
            Vector3 segment = player.position - camPos;
            float segLenSq = segment.sqrMagnitude;
            float maxDelta = fadeSpeed * Time.deltaTime;

            foreach (var tree in _trees)
            {
                // InstancesChanged 알림 타이밍과 무관하게 한 번 더 방어한다(Unity의
                // Transform == null 오버로드는 파괴된 네이티브 오브젝트도 true로 잡아낸다).
                if (tree.transform == null || tree.renderers == null || tree.renderers.Length == 0)
                {
                    continue;
                }

                bool occluding = false;
                if (segLenSq > 0.0001f)
                {
                    Vector3 toTree = tree.transform.position - camPos;
                    // 선분 위 어디쯤인지(0=카메라, 1=플레이어). 카메라/플레이어 바로 옆
                    // 나무까지 가려지는 걸로 치면 어색해서 양 끝 5%는 판정에서 뺀다.
                    float t = Vector3.Dot(toTree, segment) / segLenSq;
                    if (t > 0.05f && t < 0.95f)
                    {
                        Vector3 closest = camPos + segment * t;
                        occluding = Vector3.Distance(tree.transform.position, closest) < occlusionRadius;
                    }
                }

                float targetAlpha = occluding ? fadedAlpha : 1f;
                tree.currentAlpha = Mathf.MoveTowards(tree.currentAlpha, targetAlpha, maxDelta);
                ApplyAlpha(tree);
            }
        }

        void ApplyAlpha(TreeEntry tree)
        {
            bool shouldFade = tree.currentAlpha < 0.999f;

            if (shouldFade && !tree.faded)
            {
                for (int r = 0; r < tree.renderers.Length; r++)
                {
                    var original = tree.originalMaterials[r];
                    var fadeMats = new Material[original.Length];
                    for (int i = 0; i < original.Length; i++)
                    {
                        fadeMats[i] = GetOrCreateFadeMaterial(original[i]);
                    }
                    tree.renderers[r].sharedMaterials = fadeMats;
                }
                tree.faded = true;
            }
            else if (!shouldFade && tree.faded)
            {
                // 완전히 다시 불투명해지면 원본(바람 애니메이션 포함) 머티리얼로 되돌린다.
                for (int r = 0; r < tree.renderers.Length; r++)
                {
                    tree.renderers[r].sharedMaterials = tree.originalMaterials[r];
                }
                tree.faded = false;
            }

            if (tree.faded)
            {
                Color c = new Color(1f, 1f, 1f, tree.currentAlpha);
                for (int r = 0; r < tree.renderers.Length; r++)
                {
                    tree.renderers[r].GetPropertyBlock(_propertyBlock);
                    _propertyBlock.SetColor("_BaseColor", c);
                    tree.renderers[r].SetPropertyBlock(_propertyBlock);
                }
            }
        }

        // 나무의 원래 커스텀 셰이더(바람 애니메이션 포함)는 건드리지 않고, 전용
        // Mountains/TreeFade 셰이더로 된 반투명 버전을 원본 텍스처(_BaseTexture)만
        // 물려받아 새로 만든다. 같은 원본 머티리얼을 쓰는 나무는 이 결과를 공유한다.
        //
        // URP 기본 Lit로 만들었을 땐 메인 패스의 블렌드 알파와 그림자 캐스터의
        // 알파클립이 같은 값(텍스처알파 x _BaseColor.a)을 공유해서, 페이드 진행도가
        // _BaseColor.a에 실리는 순간 그림자까지 클립돼 사라지거나(컷오프를 낮게 잡아도
        // fadedAlpha 밑으로 내려가면 재발) 원본과 다른 뭉툭한 모양이 됐다. TreeFade는
        // 그림자 클립(텍스처 알파 vs _ShadowAlphaCutoff)과 메인 블렌드 알파를 완전히
        // 분리해서, 원본 셰이더처럼 그림자가 페이드 진행도와 무관하게 항상 같은 잎
        // 모양을 유지한다.
        Material GetOrCreateFadeMaterial(Material source)
        {
            if (source == null)
            {
                return null;
            }

            if (_fadeMaterialCache.TryGetValue(source, out var cached))
            {
                return cached;
            }

            var shader = Shader.Find("Mountains/TreeFade");
            var fadeMat = new Material(shader) { name = source.name + " (Fade)" };

            if (source.HasProperty("_BaseTexture"))
            {
                fadeMat.SetTexture("_BaseMap", source.GetTexture("_BaseTexture"));
            }

            // 원본(PT_Vegetation_Foliage_Shader)의 잎 컷아웃은 _LeavesThickness로
            // 계산된다(Alpha = step(texAlpha, 1 - _LeavesThickness)) — 그 경계를
            // 그대로 옮긴다. 줄기 등 이 프로퍼티가 없는 재질은 사실상 클립이 안 걸리는
            // 낮은 값을 기본으로 둔다.
            float shadowCutoff = source.HasProperty("_LeavesThickness")
                ? 1f - source.GetFloat("_LeavesThickness")
                : 0.01f;
            fadeMat.SetFloat("_ShadowAlphaCutoff", shadowCutoff);

            fadeMat.enableInstancing = true;

            _fadeMaterialCache[source] = fadeMat;
            return fadeMat;
        }
    }
}

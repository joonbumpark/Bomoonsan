using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mountains
{
    // 프리팹 하나가 실제로 그릴 (Mesh, Submesh, Material) 조합 하나. 멀티 렌더러
    // 프리팹(가지/잎이 따로인 경우 등)이면 프리팹 하나당 여러 개가 나올 수 있다.
    class RendererInfo
    {
        public Mesh mesh;
        public int submeshIndex;
        public Material material;
    }

    // Graphics.DrawMeshInstanced 한 번에 넘길 수 있는 매트릭스 묶음(최대 1023개)을
    // 미리 잘라 둔 것 — 매 프레임 다시 자르지 않도록 BuildChunks에서 한 번만 만든다.
    class DrawBatch
    {
        public RendererInfo info;
        public Matrix4x4[][] slices;
    }

    class Chunk
    {
        public Vector3 center;
        public float radius;
        public readonly List<DrawBatch> batches = new List<DrawBatch>();
    }

    // ScatterGroup 하나(gpuInstanced=true)의 인스턴싱 데이터. GameObject를 하나도
    // 만들지 않고 위치/회전/스케일만 누적했다가 Graphics.DrawMeshInstanced로 그린다.
    // VegetationScatter가 소유하고, Scatter()에서 채워서 Update()가 매 프레임 그린다.
    public class GpuInstancedGroup
    {
        const int MaxInstancesPerDraw = 1023;

        static readonly HashSet<Material> _instancingWarned = new HashSet<Material>();

        readonly RendererInfo[][] _rendererInfosByDrawIndex;
        readonly bool _castShadows;
        readonly int _variantCount;

        readonly List<int> _drawIndices = new List<int>();
        readonly List<Vector3> _positions = new List<Vector3>();
        readonly List<Matrix4x4> _matrices = new List<Matrix4x4>();

        List<Chunk> _chunks;

        // 그리는 단위는 (프리팹, 색 변이) 조합 하나다. 변이를 프리팹처럼 취급하면 인스턴싱
        // 배치가 자연히 변이별로 나뉘고(같은 머티리얼끼리만 묶임) 나머지 로직은 손댈 게 없다.
        // variantCount=1, resolver=null이면 예전과 완전히 동일하게 동작한다.
        public GpuInstancedGroup(GameObject[] prefabs, bool castShadows, int variantCount = 1,
            System.Func<Material, int, Material> materialVariantResolver = null)
        {
            _castShadows = castShadows;
            _variantCount = Mathf.Max(1, variantCount);
            _rendererInfosByDrawIndex = new RendererInfo[prefabs.Length * _variantCount][];

            for (int p = 0; p < prefabs.Length; p++)
            {
                var prefab = prefabs[p];
                if (prefab == null)
                {
                    for (int v = 0; v < _variantCount; v++)
                    {
                        _rendererInfosByDrawIndex[DrawIndex(p, v)] = System.Array.Empty<RendererInfo>();
                    }
                    continue;
                }

                var filters = prefab.GetComponentsInChildren<MeshFilter>(true);
                var infos = new List<RendererInfo>(filters.Length);
                foreach (var filter in filters)
                {
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (filter.sharedMesh == null || renderer == null)
                    {
                        continue;
                    }

                    var materials = renderer.sharedMaterials;
                    int submeshCount = Mathf.Min(filter.sharedMesh.subMeshCount, materials.Length);
                    for (int s = 0; s < submeshCount; s++)
                    {
                        var material = materials[s];
                        if (material == null)
                        {
                            continue;
                        }

                        EnsureInstancingEnabled(material);
                        infos.Add(new RendererInfo { mesh = filter.sharedMesh, submeshIndex = s, material = material });
                    }
                }

                for (int v = 0; v < _variantCount; v++)
                {
                    _rendererInfosByDrawIndex[DrawIndex(p, v)] = BuildVariant(infos, v, materialVariantResolver);
                }
            }
        }

        static RendererInfo[] BuildVariant(List<RendererInfo> infos, int variantIndex,
            System.Func<Material, int, Material> resolver)
        {
            if (resolver == null || variantIndex == 0)
            {
                return infos.ToArray();
            }

            var result = new RendererInfo[infos.Count];
            for (int i = 0; i < infos.Count; i++)
            {
                var material = resolver(infos[i].material, variantIndex) ?? infos[i].material;
                EnsureInstancingEnabled(material);
                result[i] = new RendererInfo
                {
                    mesh = infos[i].mesh,
                    submeshIndex = infos[i].submeshIndex,
                    material = material
                };
            }
            return result;
        }

        public int VariantCount => _variantCount;

        public int DrawIndex(int prefabIndex, int variantIndex)
        {
            return prefabIndex * _variantCount + variantIndex;
        }

        // Graphics.DrawMeshInstanced는 머티리얼의 "Enable GPU Instancing"이 꺼져 있으면
        // 그리지 못한다. 셰이더 자체는 인스턴싱을 지원해도 .mat 에셋에서 체크박스만 꺼둔
        // 경우가 있어서(예: PT_Grass_Mat), 외형에 영향 없는 런타임 설정이라 여기서 강제로
        // 켠다 — 에셋 파일은 안 건드리므로 디스크에 저장되지 않는다.
        static void EnsureInstancingEnabled(Material material)
        {
            if (material.enableInstancing)
            {
                return;
            }
            material.enableInstancing = true;
            if (_instancingWarned.Add(material))
            {
                Debug.Log($"[GpuInstancedGroup] '{material.name}' 머티리얼의 Enable GPU Instancing이 꺼져 있어 런타임에 켰습니다.");
            }
        }

        public void AddInstance(int drawIndex, Vector3 worldPosition, Matrix4x4 matrix)
        {
            _drawIndices.Add(drawIndex);
            _positions.Add(worldPosition);
            _matrices.Add(matrix);
        }

        // 스캐터가 끝난 뒤 한 번만 호출한다 — 월드 좌표를 chunkSize 격자 셀로 나누고,
        // 셀마다 렌더러별로 매트릭스를 1023개 단위로 미리 잘라둔다. 매 프레임 재계산/GC
        // 없이 바로 그릴 수 있게 하기 위해서다.
        public void BuildChunks(float chunkSize)
        {
            chunkSize = Mathf.Max(chunkSize, 1f);

            var byCell = new Dictionary<(int, int), List<int>>();
            for (int i = 0; i < _positions.Count; i++)
            {
                var pos = _positions[i];
                var cell = (Mathf.FloorToInt(pos.x / chunkSize), Mathf.FloorToInt(pos.z / chunkSize));
                if (!byCell.TryGetValue(cell, out var list))
                {
                    list = new List<int>();
                    byCell[cell] = list;
                }
                list.Add(i);
            }

            _chunks = new List<Chunk>(byCell.Count);
            foreach (var kv in byCell)
            {
                var indices = kv.Value;

                Vector3 min = _positions[indices[0]];
                Vector3 max = min;
                var matricesByInfo = new Dictionary<RendererInfo, List<Matrix4x4>>();

                foreach (int i in indices)
                {
                    Vector3 pos = _positions[i];
                    min = Vector3.Min(min, pos);
                    max = Vector3.Max(max, pos);

                    var infos = _rendererInfosByDrawIndex[_drawIndices[i]];
                    foreach (var info in infos)
                    {
                        if (!matricesByInfo.TryGetValue(info, out var list))
                        {
                            list = new List<Matrix4x4>();
                            matricesByInfo[info] = list;
                        }
                        list.Add(_matrices[i]);
                    }
                }

                var chunk = new Chunk
                {
                    center = (min + max) * 0.5f,
                    radius = Vector3.Distance(min, max) * 0.5f,
                };

                foreach (var kv2 in matricesByInfo)
                {
                    chunk.batches.Add(new DrawBatch { info = kv2.Key, slices = Slice(kv2.Value) });
                }

                _chunks.Add(chunk);
            }

            // 원본 목록은 청크로 다 옮겨졌으니 더 들고 있을 필요가 없다.
            _drawIndices.Clear();
            _positions.Clear();
            _matrices.Clear();
        }

        static Matrix4x4[][] Slice(List<Matrix4x4> matrices)
        {
            int sliceCount = Mathf.CeilToInt(matrices.Count / (float)MaxInstancesPerDraw);
            var slices = new Matrix4x4[sliceCount][];
            for (int s = 0; s < sliceCount; s++)
            {
                int start = s * MaxInstancesPerDraw;
                int count = Mathf.Min(MaxInstancesPerDraw, matrices.Count - start);
                var slice = new Matrix4x4[count];
                matrices.CopyTo(start, slice, 0, count);
                slices[s] = slice;
            }
            return slices;
        }

        // 셀 중심이 카메라에서 maxDrawDistance보다 멀면 그 셀은 통째로 스킵한다(거리
        // 기반 컬링 — 프러스텀 컬링은 하지 않는다, 카메라 뒤쪽 가까운 셀도 그려진다).
        public void Draw(Vector3 cameraPosition, float maxDrawDistance)
        {
            if (_chunks == null)
            {
                return;
            }

            var shadowMode = _castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;

            foreach (var chunk in _chunks)
            {
                if (Vector3.Distance(cameraPosition, chunk.center) > maxDrawDistance + chunk.radius)
                {
                    continue;
                }

                foreach (var batch in chunk.batches)
                {
                    foreach (var slice in batch.slices)
                    {
                        Graphics.DrawMeshInstanced(batch.info.mesh, batch.info.submeshIndex, batch.info.material,
                            slice, slice.Length, null, shadowMode, true);
                    }
                }
            }
        }
    }
}

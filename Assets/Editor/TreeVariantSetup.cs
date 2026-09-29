using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Mountains
{
    // 과일나무(PT_Fruit_Tree_01_*)의 잎 머티리얼만 바꾼 Prefab Variant를 만들고, 씬
    // VegetationScatter 설정에 그 변형들로 된 배치 그룹을 추가한다. 종류(단풍/꽃)마다 다른 건
    // 잎 머티리얼을 어떻게 바꾸는지와 어디에 심는지뿐이라 VariantPreset 하나로 묶는다.
    //
    // Tree 그룹은 GameObject로 심어서 런타임 색 변이(colorVariation)를 못 쓰므로 변형을 에셋으로
    // 만든다. 이미 있는 머티리얼/프리팹/그룹은 건드리지 않는다 — 다시 실행해도 인스펙터에서
    // 튜닝한 색과 배치 값이 초기화되지 않고, 빠진 것만 채워진다.
    static class TreeVariantSetup
    {
        const string SourceFoliagePath =
            "Assets/Polytope Studio/Lowpoly_Environments/Sources/Materials/PT_Fruit_Tree_Foliage_Mat.mat";
        const string FlowerTexturePath =
            "Assets/Polytope Studio/Lowpoly_Environments/Sources/Textures/PT_Fruit_Tree_Flowers_01.png";
        const string TreePrefabFolder = "Assets/Polytope Studio/Lowpoly_Environments/Prefabs/Trees/";
        const string SourceGroupLabel = "Tree";

        sealed class MaterialVariant
        {
            public string Name;
            // false를 돌려주면(필요한 에셋이 없는 등) 머티리얼을 만들지 않는다 — 잘못 만든 에셋이
            // 남으면 다시 실행해도 "이미 있음"으로 건너뛰어 고쳐지지 않는다.
            public System.Func<Material, bool> Configure;
        }

        sealed class VariantPreset
        {
            // 에셋 폴더/파일 이름에 들어간다(Assets/Materials/{FolderName}, ..._{FolderName}_{변형}).
            public string FolderName;
            public string[] BasePrefabNames;
            public MaterialVariant[] Materials;
            public string GroupLabel;
            // Tree 그룹을 복제한 뒤 이 종류에 맞게 고칠 값만 덮어쓴다.
            public System.Action<ScatterGroup> ConfigureGroup;
        }

        // 셰이더의 섞임 비율은 clamp((0.5 + 1.5 * 오브젝트 높이) * _Gradient, -1, 1)이다. 과일나무
        // 수관은 오브젝트 공간 높이 약 2.5~6.3(LODGroup 중심 y 3.1, 크기 7.8)이라 0.1이면 수관
        // 아래가 0.43, 꼭대기가 1.0쯤으로 수관 전체에 그라데이션이 걸친다(원본 값 0.123은
        // 수관 위쪽 절반이 꼭대기 색으로 포화된다).
        const float AutumnGradient = 0.1f;

        // 단풍: 잎 셰이더(PT_Vegetation_Foliage_Shader)의 CUSTOM COLORS TINTING을 켜서 잎 색을
        // 텍스처 대신 Ground Color(밑동) -> Top Color(꼭대기) 높이 그라데이션으로 칠한다. 원본 잎
        // 텍스처는 잎 픽셀의 97%가 같은 초록이라 텍스처를 버려도 잃는 디테일이 없고, 색마다 2048
        // 텍스처를 새로 만드는 것보다 가볍다. 한 숲 안에서 빨강/주황/노랑이 섞여야 단풍 숲처럼 보인다.
        static readonly VariantPreset Autumn = new VariantPreset
        {
            FolderName = "Autumn",
            BasePrefabNames = new[]
            {
                "PT_Fruit_Tree_01_green",
                "PT_Fruit_Tree_01_apples",
                "PT_Fruit_Tree_01_pears",
                "PT_Fruit_Tree_01_plums",
            },
            Materials = new[]
            {
                Tint("Red", new Color(0.30f, 0.03f, 0.02f), new Color(0.95f, 0.25f, 0.08f)),
                Tint("Orange", new Color(0.40f, 0.10f, 0.02f), new Color(1.00f, 0.55f, 0.10f)),
                Tint("Yellow", new Color(0.45f, 0.28f, 0.03f), new Color(1.00f, 0.85f, 0.25f)),
            },
            GroupLabel = "AutumnTree",
            // 높이/경사 범위는 Tree 그대로 두고, clusterSeed만 Tree(2)/Tree2(4)와 달리 해서
            // 초록 숲과 다른 자리에 단풍 숲이 모이게 한다.
            ConfigureGroup = group =>
            {
                group.count = 110;
                group.clusterAmount = 0.85f;
                group.clusterScale = 45f;
                group.clusterSeed = 9;
            },
        };

        // 꽃나무: 잎 텍스처를 꽃 텍스처(PT_Fruit_Tree_Flowers_01)로 바꾼다. 두 텍스처 모두 잎 카드
        // 한 장을 가득 쓰는 같은 구도이고, 꽃 텍스처에 잎 몇 장이 같이 그려져 있어 그대로 끼우면
        // 꽃이 핀 나무가 된다. 꽃이 필 땐 열매가 없으니 열매 달린 프리팹은 빼고 green만 쓴다.
        static readonly VariantPreset Blossom = new VariantPreset
        {
            FolderName = "Blossom",
            BasePrefabNames = new[] { "PT_Fruit_Tree_01_green" },
            Materials = new[]
            {
                new MaterialVariant { Name = "White", Configure = ApplyFlowerTexture },
            },
            GroupLabel = "BlossomTree",
            // 고지대(소나무 띠 윗부분 ~ 고산 초지)의 완만한 자리에 드문드문 모여 핀다. 고지대는
            // 평평한 자리가 적어 경사 허용을 Tree보다 넓히고, 군락도 약하게 걸어 개수가 덜 줄게 한다.
            ConfigureGroup = group =>
            {
                group.count = 60;
                group.minHeight = 0.55f;
                group.maxHeight = 0.85f;
                group.minSlope = 0f;
                group.maxSlope = 0.35f;
                group.clusterAmount = 0.6f;
                group.clusterScale = 35f;
                group.clusterSeed = 13;
                group.minDistance = 2.5f;
                // 바람 센 고지대라 저지대 나무보다 작게 자란다.
                group.uniformScaleRange = new Vector2(1.4f, 2.6f);
            },
        };

        [MenuItem("Mountains/단풍나무 변형 생성")]
        static void CreateAutumn() => Create(Autumn);

        [MenuItem("Mountains/꽃나무 변형 생성")]
        static void CreateBlossom() => Create(Blossom);

        static MaterialVariant Tint(string name, Color ground, Color top)
        {
            return new MaterialVariant
            {
                Name = name,
                Configure = material =>
                {
                    // 셰이더는 이 값을 키워드가 아니라 float 분기로 읽는다(_CUSTOMCOLORSTINTING ? 그라데이션 : 텍스처).
                    material.SetFloat("_CUSTOMCOLORSTINTING", 1f);
                    material.SetColor("_GroundColor", ground);
                    material.SetColor("_TopColor", top);
                    material.SetFloat("_Gradient", AutumnGradient);
                    return true;
                },
            };
        }

        static bool ApplyFlowerTexture(Material material)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(FlowerTexturePath);
            if (texture == null)
            {
                Debug.LogWarning($"[TreeVariantSetup] 꽃 텍스처를 찾지 못했습니다: {FlowerTexturePath}");
                return false;
            }

            material.SetTexture("_BaseTexture", texture);
            material.SetFloat("_CUSTOMCOLORSTINTING", 0f);
            return true;
        }

        static void Create(VariantPreset preset)
        {
            var source = AssetDatabase.LoadAssetAtPath<Material>(SourceFoliagePath);
            if (source == null)
            {
                Debug.LogWarning($"[TreeVariantSetup] 원본 잎 머티리얼을 찾지 못했습니다: {SourceFoliagePath}");
                return;
            }

            string materialFolder = $"Assets/Materials/{preset.FolderName}";
            string prefabFolder = $"Assets/Prefabs/{preset.FolderName}";
            EnsureFolder(materialFolder);
            EnsureFolder(prefabFolder);

            var variants = new List<GameObject>();
            var previewScene = EditorSceneManager.NewPreviewScene();
            try
            {
                foreach (var materialVariant in preset.Materials)
                {
                    string suffix = $"{preset.FolderName}_{materialVariant.Name}";
                    var material = FindOrCreateMaterial(source, materialVariant, $"{materialFolder}/PT_Fruit_Tree_Foliage_{suffix}.mat");
                    if (material == null)
                    {
                        continue;
                    }

                    foreach (var baseName in preset.BasePrefabNames)
                    {
                        var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{TreePrefabFolder}{baseName}.prefab");
                        if (basePrefab == null)
                        {
                            Debug.LogWarning($"[TreeVariantSetup] 나무 프리팹을 찾지 못했습니다: {baseName}");
                            continue;
                        }

                        var variant = FindOrCreateVariant(basePrefab, source, material,
                            $"{prefabFolder}/{baseName}_{suffix}.prefab", previewScene);
                        if (variant != null)
                        {
                            variants.Add(variant);
                        }
                    }
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }

            AssetDatabase.SaveAssets();

            string groupResult = RegisterScatterGroup(preset, variants.ToArray());
            Debug.Log($"[TreeVariantSetup] {preset.FolderName} 나무 변형 {variants.Count}개 준비 완료 ({prefabFolder}). {groupResult}");
        }

        static Material FindOrCreateMaterial(Material source, MaterialVariant variant, string path)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }

            // 바람/반투과 등 나머지 설정은 원본을 그대로 복제하고 종류별 값만 바꾼다.
            material = new Material(source) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
            if (!variant.Configure(material))
            {
                Object.DestroyImmediate(material);
                return null;
            }

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // 원본 프리팹 인스턴스를 저장하면 Prefab Variant가 된다. 원본 에셋 팩 프리팹은 그대로
        // 두고 Variant 쪽에 머티리얼 오버라이드만 남긴다. 씬을 더럽히지 않도록 프리뷰 씬에서 만든다.
        static GameObject FindOrCreateVariant(GameObject basePrefab, Material source, Material replacement,
            string path, UnityEngine.SceneManagement.Scene previewScene)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                return existing;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab, previewScene);
            try
            {
                // LOD마다 머티리얼 슬롯 순서가 다르다(LOD0은 잎이 1번, LOD1/2는 0번) —
                // 슬롯 번호가 아니라 원본 잎 머티리얼인지로 찾아 바꾼다.
                int replaced = 0;
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    var materials = renderer.sharedMaterials;
                    bool changed = false;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        if (materials[i] == source)
                        {
                            materials[i] = replacement;
                            changed = true;
                            replaced++;
                        }
                    }

                    if (changed)
                    {
                        renderer.sharedMaterials = materials;
                    }
                }

                if (replaced == 0)
                {
                    Debug.LogWarning($"[TreeVariantSetup] {basePrefab.name}에서 원본 잎 머티리얼을 찾지 못해 건너뜁니다.");
                    return null;
                }

                return PrefabUtility.SaveAsPrefabAsset(instance, path);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        static string RegisterScatterGroup(VariantPreset preset, GameObject[] variants)
        {
            var scatter = Object.FindFirstObjectByType<VegetationScatter>();
            var settings = scatter != null ? scatter.settings : null;
            if (settings == null)
            {
                return "씬에 VegetationScatter(settings)가 없어 배치 그룹은 추가하지 않았습니다.";
            }

            if (FindGroup(settings, preset.GroupLabel) != null)
            {
                return $"'{preset.GroupLabel}' 그룹은 이미 있어 그대로 뒀습니다.";
            }

            if (variants.Length == 0)
            {
                return "만든 변형이 없어 배치 그룹은 추가하지 않았습니다.";
            }

            // Tree 그룹 값을 복제해 시작한다 — 나무답게 충돌/페이드/스케일 등이 같아야 한다.
            var treeGroup = FindGroup(settings, SourceGroupLabel);
            var group = treeGroup != null
                ? JsonUtility.FromJson<ScatterGroup>(JsonUtility.ToJson(treeGroup))
                : new ScatterGroup();
            group.label = preset.GroupLabel;
            group.enabled = true;
            group.prefabs = variants;
            group.fadeWhenOccludingPlayer = true;
            group.blockPlayer = true;
            group.gpuInstanced = false;
            group.colorVariation = 0f;
            preset.ConfigureGroup(group);

            // 맨 뒤에 붙인다 — 배치는 그룹 순서대로 난수 하나를 공유하므로, 중간에 끼우면
            // 뒤따르는 기존 그룹들의 배치가 전부 바뀐다.
            Undo.RecordObject(settings, $"{preset.GroupLabel} 그룹 추가");
            var groups = new List<ScatterGroup>(settings.groups ?? System.Array.Empty<ScatterGroup>()) { group };
            settings.groups = groups.ToArray();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);

            return $"'{settings.name}'에 '{preset.GroupLabel}' 그룹을 추가했습니다 — VegetationScatter의 Scatter 버튼으로 다시 배치하세요.";
        }

        static ScatterGroup FindGroup(VegetationScatterSettings settings, string label)
        {
            if (settings.groups == null)
            {
                return null;
            }

            foreach (var group in settings.groups)
            {
                if (group != null && group.label == label)
                {
                    return group;
                }
            }
            return null;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}

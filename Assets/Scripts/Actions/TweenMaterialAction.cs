using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace Mountains
{
    // 머티리얼 프로퍼티(Emission 색, 알파, 커스텀 float 등)를 바꾸는 액션. duration이 0이면
    // 즉시 적용하고, 0보다 크면 현재 값에서 목표 값까지 트윈한다.
    //
    // renderer.materials(런타임 인스턴스)만 건드리고 sharedMaterial은 만지지 않는다 —
    // sharedMaterial은 에셋 그 자체라, 에디터에서 값을 쓰면 .mat 파일이 dirty로 표시돼
    // 저장/빌드 때 디스크에 기록된다.
    [Serializable]
    public class TweenMaterialAction : TweenActionBase
    {
        public enum PropertyType
        {
            Color,
            Float,
            Vector
        }

        [Header("대상 렌더러")]
        [Tooltip("비워두면 대상 Transform 아래에서 Renderer를 찾아 쓴다.")]
        public Renderer[] renderers = new Renderer[0];
        [Tooltip("renderers가 비어 있을 때, 자식의 Renderer까지 포함할지.")]
        public bool includeChildren = true;
        [Tooltip("-1이면 머티리얼 전부, 0 이상이면 그 슬롯 하나만.")]
        public int materialIndex = -1;

        [Header("프로퍼티")]
        [Tooltip("셰이더의 프로퍼티 이름. URP Lit의 발광색은 _EmissionColor, 기본색은 _BaseColor.")]
        public string propertyName = "_EmissionColor";
        public PropertyType propertyType = PropertyType.Color;
        [Tooltip("Emission은 1을 넘는 HDR 값을 줘야 눈에 띄게 빛난다.")]
        [ColorUsage(true, true)] public Color colorValue = Color.black;
        public float floatValue;
        public Vector4 vectorValue;

        [Tooltip("비워두지 않으면 값을 바꾸기 전에 이 셰이더 키워드를 켠다. URP Lit은 머티리얼에서 " +
            "Emission 체크를 꺼두면 _EMISSION 키워드가 없어 _EmissionColor를 바꿔도 변화가 없다.")]
        public string enableKeyword = "";

        // 여러 머티리얼을 한 Sequence로 묶으므로 ease는 개별 트윈에 직접 건다.
        protected override bool UsesEase => false;

        readonly List<Material> _materials = new List<Material>();

        protected override Tween CreateTween(Transform target)
        {
            CollectMaterials(target);
            if (_materials.Count == 0)
            {
                Debug.LogWarning($"[TweenMaterialAction] {target.name}: 대상 Renderer를 찾지 못했습니다.");
                return null;
            }

            Sequence sequence = null;
            foreach (var material in _materials)
            {
                if (!string.IsNullOrEmpty(enableKeyword))
                {
                    material.EnableKeyword(enableKeyword);
                }

                if (!material.HasProperty(propertyName))
                {
                    Debug.LogWarning($"[TweenMaterialAction] {material.name}에 {propertyName} " +
                        "프로퍼티가 없어 건너뜁니다.");
                    continue;
                }

                if (duration <= 0f)
                {
                    ApplyImmediately(material);
                    continue;
                }

                sequence ??= DOTween.Sequence().SetTarget(target);
                sequence.Join(CreatePropertyTween(material).SetEase(ease));
            }

            return sequence;
        }

        void ApplyImmediately(Material material)
        {
            switch (propertyType)
            {
                case PropertyType.Float:
                    material.SetFloat(propertyName, floatValue);
                    break;
                case PropertyType.Vector:
                    material.SetVector(propertyName, vectorValue);
                    break;
                default:
                    material.SetColor(propertyName, colorValue);
                    break;
            }
        }

        Tween CreatePropertyTween(Material material)
        {
            switch (propertyType)
            {
                case PropertyType.Float:
                    return material.DOFloat(floatValue, propertyName, duration);
                case PropertyType.Vector:
                    return material.DOVector(vectorValue, propertyName, duration);
                default:
                    return material.DOColor(colorValue, propertyName, duration);
            }
        }

        // renderer.materials는 접근하는 순간 이 렌더러 전용 복제본을 만든다 — 그래서 여기서
        // 모은 머티리얼을 바꿔도 다른 오브젝트나 에셋에 번지지 않는다.
        void CollectMaterials(Transform target)
        {
            _materials.Clear();

            var sources = renderers != null && renderers.Length > 0
                ? renderers
                : includeChildren
                    ? target.GetComponentsInChildren<Renderer>(true)
                    : target.GetComponents<Renderer>();

            foreach (var renderer in sources)
            {
                if (renderer == null)
                {
                    continue;
                }

                if (materialIndex < 0)
                {
                    _materials.AddRange(renderer.materials);
                    continue;
                }

                var slots = renderer.materials;
                if (materialIndex < slots.Length)
                {
                    _materials.Add(slots[materialIndex]);
                }
            }
        }
    }
}

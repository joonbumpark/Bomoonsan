using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace Mountains
{
    // 호수 물색(얕은 곳/깊은 곳, 선택적으로 주변광 세기)을 바꾸는 액션. duration이 0이면 즉시,
    // 0보다 크면 지금 색에서 목표 색으로 서서히 바꾼다. 저녁 노을, 오염, 마법 연출 등에 쓴다.
    //
    // TweenMaterialAction(renderer.materials 복제)으로 하지 않고 MaterialPropertyBlock을 쓰는
    // 이유: 모든 호수가 머티리얼 하나를 공유하고 호수 모양 마스크(_MaskTex)만 블록으로 개체별
    // 오버라이드하는 구조라, 같은 블록에 색만 얹으면 머티리얼 복제 없이 호수별로 바꿀 수 있다.
    // 블록은 직렬화되지 않아 Play가 끝나면 원래 색으로 돌아오고 .mat 에셋도 건드리지 않는다.
    [Serializable]
    public class ChangeWaterColorAction : TriggerAction
    {
        static readonly int ShallowColorId = Shader.PropertyToID("_ShallowColor");
        static readonly int DeepColorId = Shader.PropertyToID("_DeepColor");
        static readonly int AmbientStrengthId = Shader.PropertyToID("_AmbientStrength");

        [Tooltip("비워두면 씬에서 지형(ProceduralTerrainMesh)을 찾아 쓴다.")]
        public ProceduralTerrainMesh terrain;
        [Tooltip("바꿀 호수 번호(지형 Water Areas 순서 = Water_0, Water_1 ...). 비워두면 모든 호수.")]
        public int[] waterIndices = new int[0];

        [Header("색")]
        [Tooltip("물가 쪽(얕은 곳) 색. 알파가 낮을수록 투명해서 바닥이 비친다.")]
        public Color shallowColor = new Color(0.35f, 0.65f, 0.65f, 0.55f);
        [Tooltip("가운데(깊은 곳) 색. 수심에 따라 얕은 색과 섞인다.")]
        public Color deepColor = new Color(0.08f, 0.25f, 0.35f, 0.9f);

        [Header("주변광")]
        [Tooltip("켜두면 주변광(하늘빛) 세기도 함께 바꾼다. 물 색과 그림자 안쪽이 얼마나 밝은지를 " +
            "정한다 — 0이면 해가 직접 비추는 곳만 색이 나고, 올릴수록 그늘진 물도 밝아진다. " +
            "밤/동굴처럼 어둡게 가거나 한낮처럼 밝게 갈 때 색과 같이 바꾼다.")]
        public bool changeAmbient;
        [Range(0f, 2f)] public float ambientStrength = 1f;

        [Header("전환")]
        [Tooltip("0이면 즉시 바꾼다.")]
        [Min(0f)] public float duration = 2f;
        public Ease ease = Ease.InOutSine;
        [Tooltip("켜두면 색이 다 바뀔 때까지 다음 스텝으로 넘어가지 않는다.")]
        public bool waitForComplete = true;

        // 호수마다 시작 색이 다를 수 있어(이전 액션으로 한 곳만 바꿨다든지) 개별로 기억한다.
        struct WaterTarget
        {
            public Renderer Renderer;
            public Color Shallow;
            public Color Deep;
            public float Ambient;
        }

        public override UniTask ExecuteAsync(TriggerContext context, CancellationToken cancellationToken)
        {
            var resolvedTerrain = terrain != null ? terrain : UnityEngine.Object.FindFirstObjectByType<ProceduralTerrainMesh>();
            if (resolvedTerrain == null)
            {
                Debug.LogWarning("[ChangeWaterColorAction] 씬에서 ProceduralTerrainMesh를 찾지 못했습니다.");
                return UniTask.CompletedTask;
            }

            // 블록은 호출마다 새로 만든다 — 트리거가 재발동해 같은 액션이 겹쳐 돌 수 있다.
            var block = new MaterialPropertyBlock();
            var targets = CollectTargets(resolvedTerrain, block);
            if (targets.Count == 0)
            {
                Debug.LogWarning($"[ChangeWaterColorAction] {resolvedTerrain.name}: 바꿀 호수 표면을 찾지 못했습니다.");
                return UniTask.CompletedTask;
            }

            if (duration <= 0f)
            {
                Apply(targets, block, 1f);
                return UniTask.CompletedTask;
            }

            var tween = DOTween.To(() => 0f, t => Apply(targets, block, t), 1f, duration)
                .SetEase(ease)
                .SetTarget(resolvedTerrain);

            return waitForComplete ? tween.AwaitKill(cancellationToken) : UniTask.CompletedTask;
        }

        List<WaterTarget> CollectTargets(ProceduralTerrainMesh source, MaterialPropertyBlock block)
        {
            var targets = new List<WaterTarget>();
            int count = source.waterAreas != null ? source.waterAreas.Length : 0;

            if (waterIndices == null || waterIndices.Length == 0)
            {
                for (int i = 0; i < count; i++)
                {
                    AddTarget(targets, source.GetWaterSurfaceRenderer(i), block);
                }
                return targets;
            }

            foreach (int index in waterIndices)
            {
                var renderer = index >= 0 && index < count ? source.GetWaterSurfaceRenderer(index) : null;
                if (renderer == null)
                {
                    Debug.LogWarning($"[ChangeWaterColorAction] {index}번 호수 표면이 없어 건너뜁니다(호수 {count}개).");
                    continue;
                }
                AddTarget(targets, renderer, block);
            }
            return targets;
        }

        static void AddTarget(List<WaterTarget> targets, Renderer renderer, MaterialPropertyBlock block)
        {
            if (renderer == null || renderer.sharedMaterial == null)
            {
                return;
            }

            renderer.GetPropertyBlock(block);
            targets.Add(new WaterTarget
            {
                Renderer = renderer,
                Shallow = ReadColor(renderer, block, ShallowColorId),
                Deep = ReadColor(renderer, block, DeepColorId),
                Ambient = ReadFloat(renderer, block, AmbientStrengthId),
            });
        }

        // 이전 액션이 블록에 색을 얹어 뒀으면 그 색이, 아니면 머티리얼 색이 지금 보이는 색이다.
        static Color ReadColor(Renderer renderer, MaterialPropertyBlock block, int id)
        {
            return block.HasColor(id) ? block.GetColor(id) : renderer.sharedMaterial.GetColor(id);
        }

        static float ReadFloat(Renderer renderer, MaterialPropertyBlock block, int id)
        {
            return block.HasFloat(id) ? block.GetFloat(id) : renderer.sharedMaterial.GetFloat(id);
        }

        void Apply(List<WaterTarget> targets, MaterialPropertyBlock block, float t)
        {
            foreach (var target in targets)
            {
                if (target.Renderer == null)
                {
                    continue;
                }

                // 블록을 비우지 않고 먼저 읽어온다 — 호수 모양 마스크(_MaskTex)가 같은 블록에
                // 들어 있어서, 새 블록으로 덮으면 마스크가 사라져 물이 통째로 안 보인다.
                target.Renderer.GetPropertyBlock(block);
                block.SetColor(ShallowColorId, Color.Lerp(target.Shallow, shallowColor, t));
                block.SetColor(DeepColorId, Color.Lerp(target.Deep, deepColor, t));
                if (changeAmbient)
                {
                    block.SetFloat(AmbientStrengthId, Mathf.Lerp(target.Ambient, ambientStrength, t));
                }
                target.Renderer.SetPropertyBlock(block);
            }
        }
    }
}

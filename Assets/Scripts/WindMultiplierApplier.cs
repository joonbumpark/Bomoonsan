using UnityEngine;

namespace Mountains
{
    // 씬에 하나 놓고 쓰는 바람 세기 조절기. WindMultiplierSettings(모든 씬이 공유하는
    // baseline)에 multiplier를 곱해서 적용하되, 머티리얼 에셋 자체는 절대 저장하지
    // 않는다(EditorUtility.SetDirty/AssetDatabase.SaveAssets 호출 없음) — 씬마다 이
    // 컴포넌트의 multiplier 값만 다르게 두면, 그 씬이 열리거나(에디터) 로드될 때(빌드)
    // 항상 그 씬이 원하는 세기로 라이브 오버라이드된다.
    //
    // 머티리얼이 씬끼리 공유되는 에셋이라, 여러 씬을 동시에 additive로 띄우면 마지막에
    // Apply한 씬 값이 이긴다 — 이 프로젝트는 씬을 하나만 활성화해서 쓰는 구조라 문제 없다.
    [ExecuteAlways]
    public class WindMultiplierApplier : MonoBehaviour
    {
        public WindMultiplierSettings settings;
        [Range(0f, 3f)] public float multiplier = 1f;

        void OnEnable()
        {
            Apply();
        }

        void OnValidate()
        {
            Apply();
        }

#if UNITY_EDITOR
        // 에디터에서 머티리얼 에셋에 SetFloat을 하면 유니티가 그 에셋을 더티로 표시하고,
        // 프로젝트를 저장할 때 곱해진 값을 디스크에 그대로 써버린다(내가 SetDirty를
        // 호출하지 않아도 그렇다). 씬을 닫거나 이 컴포넌트를 끌 때 원본으로 되돌려서,
        // 머티리얼 파일에는 항상 baseline이 남게 한다. 저장 직전 복원은 에디터 쪽의
        // WindMaterialSaveGuard가 따로 맡는다.
        void OnDisable()
        {
            RestoreBaseline();
        }
#endif

        public void Apply()
        {
            ApplyMultiplier(multiplier);
        }

        // 배율 1 = baseline 그대로. 머티리얼 에셋을 원래 값으로 되돌릴 때 쓴다.
        public void RestoreBaseline()
        {
            ApplyMultiplier(1f);
        }

        void ApplyMultiplier(float value)
        {
            if (settings == null || settings.entries == null)
            {
                return;
            }

            foreach (var entry in settings.entries)
            {
                if (entry.material == null)
                {
                    continue;
                }
                entry.material.SetFloat("_WindStrength", entry.baseWindStrength * value);
                if (entry.hasSecondaryLayer)
                {
                    entry.material.SetFloat("_WindStrength1", entry.baseWindStrength1 * value);
                }
            }
        }
    }
}

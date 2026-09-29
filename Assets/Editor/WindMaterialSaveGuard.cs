using UnityEditor;
using UnityEngine;

namespace Mountains
{
    // WindMultiplierApplier는 씬마다 다른 바람 세기를 보여주려고 공유 머티리얼 에셋의
    // _WindStrength를 라이브로 덮어쓴다. 문제는 에디터에서 머티리얼 에셋을 스크립트로
    // 수정하면 유니티가 그 에셋을 더티로 표시하고, 프로젝트를 저장할 때 곱해진 값을
    // 그대로 디스크에 써버린다는 것이다 — 그래서 PT_*.mat들이 아무도 손대지 않았는데
    // 계속 변경 파일로 잡혔다.
    //
    // 저장 "직전"에 baseline으로 되돌려서 파일에는 항상 원본이 기록되게 하고, 저장이
    // 끝난 뒤 다시 적용해서 에디터 미리보기는 그대로 유지한다. 이렇게 하면 ExecuteAlways
    // 미리보기(씬 뷰에서 바람 세기가 바로 보이는 것)를 포기하지 않고도 머티리얼 파일이
    // 깨끗하게 유지된다.
    public class WindMaterialSaveGuard : AssetModificationProcessor
    {
        static string[] OnWillSaveAssets(string[] paths)
        {
            var appliers = Object.FindObjectsByType<WindMultiplierApplier>(FindObjectsSortMode.None);
            if (appliers.Length == 0)
            {
                return paths;
            }

            foreach (var applier in appliers)
            {
                applier.RestoreBaseline();
            }

            // 저장이 끝난 다음 프레임에 다시 적용한다 — 여기서 바로 적용하면 저장되는
            // 값이 도로 곱해진 값이 되어 의미가 없다.
            EditorApplication.delayCall += ReapplyAll;
            return paths;
        }

        static void ReapplyAll()
        {
            foreach (var applier in Object.FindObjectsByType<WindMultiplierApplier>(FindObjectsSortMode.None))
            {
                applier.Apply();
            }
        }
    }
}

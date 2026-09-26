using UnityEditor;
using UnityEngine;

namespace Mountains
{
    // 죽은 오브젝트를 붙잡고 있는 선택을 강제로 비운다.
    //
    // Play 중에 생성된 오브젝트(스폰된 NPC, 런타임 UI 등)를 선택한 채 Play를 끝내면,
    // 하이어라키 창이 그 인스턴스 ID를 창 상태에 저장해두고 도메인 리로드마다 복원한다.
    // 대상이 이미 없으므로 인스펙터가 에디터를 만들 때마다
    // SerializedObjectNotCreatableException / MissingReferenceException이 반복된다.
    // 하이어라키 빈 공간 클릭으로 안 지워질 때 쓰는 비상구다.
    static class ClearStaleSelection
    {
        [MenuItem("Mountains/선택 해제 (죽은 인스펙터 참조 정리)")]
        static void Clear()
        {
            Selection.objects = new Object[0];
            Selection.activeObject = null;

            // 인스펙터가 이미 만들어둔 에디터까지 버리게 강제로 다시 그린다.
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                window.Repaint();
            }

            Debug.Log("[ClearStaleSelection] 선택을 비웠습니다. 다음 컴파일부터 " +
                "인스펙터 예외가 사라지는지 확인하세요.");
        }
    }
}

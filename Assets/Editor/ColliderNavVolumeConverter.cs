using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Mountains
{
    // ProceduralTerrain/Colliders 밑에 손으로 배치한 BoxCollider(길막이)를 같은 크기의
    // NavMeshModifierVolume으로 바꾼다.
    //
    // 박스 콜라이더를 NavMeshSurface(Physics Colliders)에 지오메트리로 넣으면, 부모의
    // NavMeshModifier로 Not Walkable을 줘도 박스 윗면/옆면이 따로 구워지는 등 "물체"로
    // 취급된다. 볼륨은 지오메트리 없이 영역만 칠하므로 박스가 덮는 땅이 정확히 그 영역만큼
    // 막힌다. 플레이어는 NavMeshAgent.Move로만 움직여서 물리 충돌 없이 NavMesh만으로 막힌다.
    static class ColliderNavVolumeConverter
    {
        const string ContainerName = "Colliders";

        [MenuItem("Mountains/Colliders 박스를 NavMesh Volume으로 변환")]
        static void Convert()
        {
            var terrain = Object.FindFirstObjectByType<ProceduralTerrainMesh>();
            var container = terrain != null ? terrain.transform.Find(ContainerName) : null;
            if (container == null)
            {
                Debug.LogWarning($"[ColliderNavVolumeConverter] ProceduralTerrain 밑에서 '{ContainerName}' 오브젝트를 찾지 못했습니다.");
                return;
            }

            var boxes = container.GetComponentsInChildren<BoxCollider>(true);
            if (boxes.Length == 0)
            {
                Debug.Log($"[ColliderNavVolumeConverter] '{ContainerName}' 밑에 변환할 BoxCollider가 없습니다.");
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Colliders 박스를 NavMesh Volume으로 변환");

            foreach (var box in boxes)
            {
                ConvertOne(box);
            }

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(container.gameObject.scene);

            bool baked = RebakeNavMesh(terrain);
            Debug.Log($"[ColliderNavVolumeConverter] BoxCollider {boxes.Length}개를 NavMeshModifierVolume으로 바꿨습니다" +
                (baked ? " — NavMesh도 다시 구웠습니다." : " — NavMeshSurface가 없어 NavMesh는 굽지 않았습니다(Mountains/Bake NavMesh)."));
        }

        static void ConvertOne(BoxCollider box)
        {
            var go = box.gameObject;
            var volume = go.GetComponent<NavMeshModifierVolume>();
            if (volume == null)
            {
                volume = Undo.AddComponent<NavMeshModifierVolume>(go);
            }
            else
            {
                Undo.RecordObject(volume, "NavMesh Volume 갱신");
            }

            // 볼륨도 콜라이더처럼 Transform의 회전과 스케일(lossyScale)을 그대로 적용하므로
            // 로컬 center/size를 옮기기만 하면 같은 영역이 된다.
            volume.center = box.center;
            volume.size = box.size;
            volume.area = ResolveArea(go.transform);

            Undo.DestroyObjectImmediate(box);
        }

        // 지금까지 이 박스들에 걸려 있던 영역 설정(부모 NavMeshModifier의 Override Area)을
        // 그대로 이어받는다. 없으면 길막이 용도이므로 Not Walkable.
        static int ResolveArea(Transform t)
        {
            var modifier = t.GetComponentInParent<NavMeshModifier>(true);
            if (modifier != null && modifier.overrideArea
                && (modifier.transform == t || modifier.applyToChildren))
            {
                return modifier.area;
            }
            return NavMesh.GetAreaFromName("Not Walkable");
        }

        // Bake NavMesh 메뉴와 달리 지형을 재생성(Generate)하지 않는다 — 바뀐 건 이 볼륨뿐이라
        // 지형 쪽 데이터(물 볼륨 등)는 그대로 두고 NavMesh만 다시 굽는다.
        static bool RebakeNavMesh(ProceduralTerrainMesh terrain)
        {
            var surface = terrain.GetComponent<NavMeshSurface>();
            if (surface == null)
            {
                return false;
            }

            surface.BuildNavMesh();
            return true;
        }
    }
}

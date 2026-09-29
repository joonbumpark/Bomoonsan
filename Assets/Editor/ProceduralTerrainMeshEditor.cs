using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Mountains
{
    [CustomEditor(typeof(ProceduralTerrainMesh))]
    public class ProceduralTerrainMeshEditor : Editor
    {
        bool _addPointMode;
        int _activePathIndex = -1;
        bool _addWaterPointMode;
        int _activeWaterAreaIndex = -1;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var terrain = (ProceduralTerrainMesh)target;

            EditorGUILayout.Space();

            if (terrain.settings == null)
            {
                EditorGUILayout.HelpBox("settings가 비어있습니다. Mountains > Create Procedural Terrain을 " +
                    "한 번 실행하면 자동으로 만들어집니다.", MessageType.Info);
                return;
            }

            if (GUILayout.Button("Regenerate"))
            {
                terrain.Generate();
                EditorUtility.SetDirty(terrain);
            }

            if (GUILayout.Button("Randomize Seed"))
            {
                terrain.settings.seed = Random.Range(int.MinValue, int.MaxValue);
                EditorUtility.SetDirty(terrain.settings);
                terrain.Generate();
                EditorUtility.SetDirty(terrain);
            }

            EditorGUILayout.Space();
            DrawPathEditingGUI(terrain);

            EditorGUILayout.Space();
            DrawWaterAreaEditingGUI(terrain);
        }

        void DrawPathEditingGUI(ProceduralTerrainMesh terrain)
        {
            EditorGUILayout.LabelField("Path Editing", EditorStyles.boldLabel);

            ClampActiveIndex(terrain);

            // 편집 대상 길 선택 — 서로 안 이어지는 길을 여러 개 둘 수 있어서, "지금 어느
            // 길에 점을 추가/삭제할지"를 먼저 고르게 한다.
            if (terrain.paths != null && terrain.paths.Length > 0)
            {
                var labels = new string[terrain.paths.Length];
                for (int i = 0; i < terrain.paths.Length; i++)
                {
                    var p = terrain.paths[i];
                    int count = p?.waypoints?.Length ?? 0;
                    labels[i] = $"{i}: {p?.name} ({count}pt)";
                }
                _activePathIndex = EditorGUILayout.Popup("Active Path", _activePathIndex, labels);
            }
            else
            {
                EditorGUILayout.HelpBox("길이 아직 없습니다. 'New Path'로 새로 만드세요.", MessageType.None);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("New Path"))
            {
                AddNewPath(terrain);
                SceneView.RepaintAll();
            }

            using (new EditorGUI.DisabledScope(!HasActivePath(terrain)))
            {
                if (GUILayout.Button("Delete Path"))
                {
                    Undo.RecordObject(terrain, "Delete Path");
                    var list = new List<TerrainPath>(terrain.paths);
                    list.RemoveAt(_activePathIndex);
                    terrain.paths = list.ToArray();
                    _activePathIndex = Mathf.Clamp(_activePathIndex, -1, list.Count - 1);
                    EditorUtility.SetDirty(terrain);
                    SceneView.RepaintAll();
                }
            }
            EditorGUILayout.EndHorizontal();

            GUI.backgroundColor = _addPointMode ? Color.green : Color.white;
            string label = _addPointMode ? "Adding Points... (Scene 뷰 클릭, Esc로 종료)" : "Add Path Points";
            if (GUILayout.Button(label))
            {
                _addPointMode = !_addPointMode;
                if (_addPointMode)
                {
                    _addWaterPointMode = false; // 두 추가 모드가 동시에 클릭을 가로채지 않게 상호 배타적으로 둔다.
                    EnsureActivePath(terrain);
                }
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = Color.white;

            using (new EditorGUI.DisabledScope(!HasActivePath(terrain) || terrain.paths[_activePathIndex].waypoints.Length == 0))
            {
                if (GUILayout.Button("Clear Path"))
                {
                    Undo.RecordObject(terrain, "Clear Path Waypoints");
                    terrain.paths[_activePathIndex].waypoints = new Vector2[0];
                    EditorUtility.SetDirty(terrain);
                    SceneView.RepaintAll();
                }
            }

            EditorGUILayout.HelpBox("Add Path Points를 켜고 Scene 뷰에서 지형을 클릭하면 활성 길(Active Path)에 " +
                "웨이포인트가 추가된다. 새로 안 이어지는 길이 필요하면 New Path로 만들고 활성화한 뒤 다시 클릭. " +
                "점은 항상(모드와 무관) 드래그로 옮길 수 있다 — 활성 길은 노란색, 다른 길은 회색으로 표시된다.",
                MessageType.None);
        }

        bool HasActivePath(ProceduralTerrainMesh terrain)
        {
            return terrain.paths != null && _activePathIndex >= 0 && _activePathIndex < terrain.paths.Length;
        }

        void ClampActiveIndex(ProceduralTerrainMesh terrain)
        {
            int count = terrain.paths?.Length ?? 0;
            if (_activePathIndex >= count)
            {
                _activePathIndex = count - 1;
            }
        }

        void EnsureActivePath(ProceduralTerrainMesh terrain)
        {
            if (!HasActivePath(terrain))
            {
                AddNewPath(terrain);
            }
        }

        void AddNewPath(ProceduralTerrainMesh terrain)
        {
            Undo.RecordObject(terrain, "Add Path");
            var list = new List<TerrainPath>(terrain.paths ?? new TerrainPath[0]);
            list.Add(new TerrainPath { name = $"Path {list.Count}" });
            terrain.paths = list.ToArray();
            _activePathIndex = list.Count - 1;
            EditorUtility.SetDirty(terrain);
        }

        // 길 편집 UI(DrawPathEditingGUI 등)를 그대로 본뜬 호수 편집 UI. 유일한 차이는
        // 호수는 닫힌 폴리곤이라 최소 3점이 필요하다는 것뿐이다.
        void DrawWaterAreaEditingGUI(ProceduralTerrainMesh terrain)
        {
            EditorGUILayout.LabelField("Water Area Editing", EditorStyles.boldLabel);

            ClampActiveWaterIndex(terrain);

            if (terrain.waterAreas != null && terrain.waterAreas.Length > 0)
            {
                var labels = new string[terrain.waterAreas.Length];
                for (int i = 0; i < terrain.waterAreas.Length; i++)
                {
                    var a = terrain.waterAreas[i];
                    int count = a?.waypoints?.Length ?? 0;
                    labels[i] = $"{i}: {a?.name} ({count}pt)";
                }
                _activeWaterAreaIndex = EditorGUILayout.Popup("Active Water Area", _activeWaterAreaIndex, labels);
            }
            else
            {
                EditorGUILayout.HelpBox("호수가 아직 없습니다. 'New Water Area'로 새로 만드세요.", MessageType.None);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("New Water Area"))
            {
                AddNewWaterArea(terrain);
                SceneView.RepaintAll();
            }

            using (new EditorGUI.DisabledScope(!HasActiveWaterArea(terrain)))
            {
                if (GUILayout.Button("Delete Water Area"))
                {
                    Undo.RecordObject(terrain, "Delete Water Area");
                    var list = new List<WaterArea>(terrain.waterAreas);
                    list.RemoveAt(_activeWaterAreaIndex);
                    terrain.waterAreas = list.ToArray();
                    _activeWaterAreaIndex = Mathf.Clamp(_activeWaterAreaIndex, -1, list.Count - 1);
                    EditorUtility.SetDirty(terrain);
                    SceneView.RepaintAll();
                }
            }
            EditorGUILayout.EndHorizontal();

            GUI.backgroundColor = _addWaterPointMode ? Color.green : Color.white;
            string waterLabel = _addWaterPointMode ? "Adding Points... (Scene 뷰 클릭, Esc로 종료)" : "Add Water Area Points";
            if (GUILayout.Button(waterLabel))
            {
                _addWaterPointMode = !_addWaterPointMode;
                if (_addWaterPointMode)
                {
                    _addPointMode = false;
                    EnsureActiveWaterArea(terrain);
                }
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = Color.white;

            using (new EditorGUI.DisabledScope(!HasActiveWaterArea(terrain) || terrain.waterAreas[_activeWaterAreaIndex].waypoints.Length == 0))
            {
                if (GUILayout.Button("Clear Water Area"))
                {
                    Undo.RecordObject(terrain, "Clear Water Area Waypoints");
                    terrain.waterAreas[_activeWaterAreaIndex].waypoints = new Vector2[0];
                    EditorUtility.SetDirty(terrain);
                    SceneView.RepaintAll();
                }
            }

            EditorGUILayout.HelpBox("Add Water Area Points를 켜고 Scene 뷰에서 지형을 클릭하면 활성 호수(Active Water Area)에 " +
                "경계점이 추가된다. 최소 3점부터 폐곡선(호수 경계)으로 취급된다 — 마지막 점과 첫 점이 자동으로 이어진다. " +
                "점은 항상(모드와 무관) 드래그로 옮길 수 있다 — 활성 호수는 파란색, 다른 호수는 회색으로 표시된다.",
                MessageType.None);
        }

        bool HasActiveWaterArea(ProceduralTerrainMesh terrain)
        {
            return terrain.waterAreas != null && _activeWaterAreaIndex >= 0 && _activeWaterAreaIndex < terrain.waterAreas.Length;
        }

        void ClampActiveWaterIndex(ProceduralTerrainMesh terrain)
        {
            int count = terrain.waterAreas?.Length ?? 0;
            if (_activeWaterAreaIndex >= count)
            {
                _activeWaterAreaIndex = count - 1;
            }
        }

        void EnsureActiveWaterArea(ProceduralTerrainMesh terrain)
        {
            if (!HasActiveWaterArea(terrain))
            {
                AddNewWaterArea(terrain);
            }
        }

        void AddNewWaterArea(ProceduralTerrainMesh terrain)
        {
            Undo.RecordObject(terrain, "Add Water Area");
            var list = new List<WaterArea>(terrain.waterAreas ?? new WaterArea[0]);
            list.Add(new WaterArea { name = $"Lake {list.Count}" });
            terrain.waterAreas = list.ToArray();
            _activeWaterAreaIndex = list.Count - 1;
            EditorUtility.SetDirty(terrain);
        }

        void OnSceneGUI()
        {
            var terrain = (ProceduralTerrainMesh)target;

            DrawWaypointHandles(terrain);
            DrawWaterAreaHandles(terrain);

            if (_addPointMode)
            {
                HandleAddPointInput(terrain);
            }
            else if (_addWaterPointMode)
            {
                HandleAddWaterPointInput(terrain);
            }
        }

        void DrawWaypointHandles(ProceduralTerrainMesh terrain)
        {
            if (terrain.paths == null)
            {
                return;
            }

            for (int p = 0; p < terrain.paths.Length; p++)
            {
                var path = terrain.paths[p];
                if (path?.waypoints == null)
                {
                    continue;
                }

                bool isActive = p == _activePathIndex;
                Color handleColor = isActive ? Color.yellow : new Color(0.6f, 0.6f, 0.6f, 0.6f);
                float sizeScale = isActive ? 0.15f : 0.1f;

                for (int i = 0; i < path.waypoints.Length; i++)
                {
                    Vector3 worldPos = terrain.GetWorldPositionAt(path.waypoints[i].x, path.waypoints[i].y);
                    float size = HandleUtility.GetHandleSize(worldPos) * sizeScale;

                    Handles.color = handleColor;
                    EditorGUI.BeginChangeCheck();
                    Vector3 moved = Handles.FreeMoveHandle(worldPos, size, Vector3.zero, Handles.SphereHandleCap);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(terrain, "Move Path Waypoint");
                        path.waypoints[i] = WorldToGrid(terrain, moved);
                        EditorUtility.SetDirty(terrain);
                    }

                    if (isActive)
                    {
                        Handles.Label(worldPos + Vector3.up * (size * 2f), $"{p}-{i}");
                    }
                }
            }
        }

        // 클릭 한 번 = 활성 길에 웨이포인트 하나 추가. 지형의 MeshCollider에 직접
        // 레이캐스트해서(Physics.Raycast를 쓰면 씬의 다른 콜라이더까지 다 맞을 수 있어
        // 부정확하다) 클릭한 표면 위치를 정확히 얻는다. Esc를 누르면 모드가 꺼진다.
        void HandleAddPointInput(ProceduralTerrainMesh terrain)
        {
            Event e = Event.current;
            int controlID = GUIUtility.GetControlID(FocusType.Passive);
            //HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

            if (e.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(controlID);
            }

            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
            {
                EnsureActivePath(terrain);
                if (!HasActivePath(terrain))
                {
                    return;
                }

                var collider = terrain.GetComponent<MeshCollider>();
                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                if (collider != null && collider.Raycast(ray, out RaycastHit hit, 100000f))
                {
                    Undo.RecordObject(terrain, "Add Path Waypoint");
                    var path = terrain.paths[_activePathIndex];
                    var list = new List<Vector2>(path.waypoints ?? new Vector2[0]);
                    list.Add(WorldToGrid(terrain, hit.point));
                    path.waypoints = list.ToArray();
                    EditorUtility.SetDirty(terrain);
                    Repaint();
                    e.Use();
                }
            }
            else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                _addPointMode = false;
                e.Use();
                Repaint();
            }
        }

        // DrawWaypointHandles를 그대로 본뜬 호수 경계점 핸들. 활성 호수는 파란색으로
        // 표시해서 길(노란색)과 한눈에 구분되게 한다.
        void DrawWaterAreaHandles(ProceduralTerrainMesh terrain)
        {
            if (terrain.waterAreas == null)
            {
                return;
            }

            for (int a = 0; a < terrain.waterAreas.Length; a++)
            {
                var area = terrain.waterAreas[a];
                if (area?.waypoints == null)
                {
                    continue;
                }

                bool isActive = a == _activeWaterAreaIndex;
                Color handleColor = isActive ? Color.blue : new Color(0.6f, 0.6f, 0.6f, 0.6f);
                float sizeScale = isActive ? 0.15f : 0.1f;

                for (int i = 0; i < area.waypoints.Length; i++)
                {
                    Vector3 worldPos = terrain.GetWorldPositionAt(area.waypoints[i].x, area.waypoints[i].y);
                    float size = HandleUtility.GetHandleSize(worldPos) * sizeScale;

                    Handles.color = handleColor;
                    EditorGUI.BeginChangeCheck();
                    Vector3 moved = Handles.FreeMoveHandle(worldPos, size, Vector3.zero, Handles.SphereHandleCap);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(terrain, "Move Water Area Waypoint");
                        area.waypoints[i] = WorldToGrid(terrain, moved);
                        EditorUtility.SetDirty(terrain);
                    }

                    if (isActive)
                    {
                        Handles.Label(worldPos + Vector3.up * (size * 2f), $"W{a}-{i}");
                    }
                }
            }
        }

        // HandleAddPointInput을 그대로 본뜬 호수 경계점 추가 입력.
        void HandleAddWaterPointInput(ProceduralTerrainMesh terrain)
        {
            Event e = Event.current;
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
            {
                EnsureActiveWaterArea(terrain);
                if (!HasActiveWaterArea(terrain))
                {
                    return;
                }

                var collider = terrain.GetComponent<MeshCollider>();
                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                if (collider != null && collider.Raycast(ray, out RaycastHit hit, 100000f))
                {
                    Undo.RecordObject(terrain, "Add Water Area Waypoint");
                    var area = terrain.waterAreas[_activeWaterAreaIndex];
                    var list = new List<Vector2>(area.waypoints ?? new Vector2[0]);
                    list.Add(WorldToGrid(terrain, hit.point));
                    area.waypoints = list.ToArray();
                    EditorUtility.SetDirty(terrain);
                    Repaint();
                    e.Use();
                }
            }
            else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                _addWaterPointMode = false;
                e.Use();
                Repaint();
            }
        }

        // 런타임 쪽(CharacterMovement의 물 차단)에서도 같은 변환이 필요해져서
        // ProceduralTerrainMesh로 옮겼다. 여기서는 그대로 위임만 한다.
        static Vector2 WorldToGrid(ProceduralTerrainMesh terrain, Vector3 worldPos)
        {
            return terrain.WorldToGrid(worldPos);
        }
    }
}

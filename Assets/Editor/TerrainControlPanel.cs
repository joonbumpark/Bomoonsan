using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mountains
{
    // 지형(TerrainGenerationSettings)과 식생(VegetationScatterSettings) 설정이 서로 다른
    // 에셋에 나뉘어 있어서, 값 하나 고치고 다시 생성하려면 Project 창에서 에셋을 찾아 선택 →
    // 값 수정 → 다시 씬의 컴포넌트를 선택해 버튼 클릭이라는 왕복을 매번 반복해야 했다.
    // 이 창은 "지금 열려 있는 씬"의 지형/식생/바람 설정과 실행 버튼을 한 화면에 모아준다.
    //
    // 설정 필드를 여기에 다시 나열하지 않고 Editor.CreateEditor로 각 에셋의 기본 인스펙터를
    // 그대로 끼워 넣는다 — 나중에 설정 에셋에 필드가 추가돼도 이 창은 손댈 필요가 없고,
    // 두 곳의 UI가 서로 어긋날 일도 없다.
    public class TerrainControlPanel : EditorWindow
    {
        [MenuItem("Mountains/Terrain Control Panel %#t")]
        public static void Open()
        {
            var window = GetWindow<TerrainControlPanel>();
            window.titleContent = new GUIContent("Terrain Panel");
            window.minSize = new Vector2(340f, 420f);
            window.Refresh();
        }

        ProceduralTerrainMesh _terrain;
        VegetationScatter _scatter;
        WindMultiplierApplier _wind;

        Editor _terrainSettingsEditor;
        Editor _vegetationSettingsEditor;
        SerializedObject _windSerialized;

        Vector2 _scroll;
        bool _showTerrain = true;
        bool _showVegetation = true;
        bool _showWind = true;
        bool _showResolution;
        [SerializeField] float _targetCellSize = 2f;

        [SerializeField] bool _autoRegenerateTerrain;
        [SerializeField] bool _autoScatter;

        void OnEnable()
        {
            Refresh();
            // 씬을 바꾸거나 지형/식생 오브젝트를 새로 만들면 대상이 통째로 달라진다 —
            // 창을 닫았다 열지 않아도 따라가게 한다.
            EditorApplication.hierarchyChanged += Refresh;
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
        }

        void OnDisable()
        {
            EditorApplication.hierarchyChanged -= Refresh;
            EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
            DestroyEditors();
        }

        void OnActiveSceneChanged(Scene previous, Scene next)
        {
            Refresh();
        }

        void Refresh()
        {
            _terrain = Object.FindObjectOfType<ProceduralTerrainMesh>();
            // VegetationScatter는 보통 지형과 같은 오브젝트에 붙는다(TerrainSceneSetup이 그렇게
            // 만든다). 다른 데 붙어 있는 씬도 있을 수 있어 못 찾으면 씬 전체에서 한 번 더 찾는다.
            _scatter = _terrain != null ? _terrain.GetComponent<VegetationScatter>() : null;
            if (_scatter == null)
            {
                _scatter = Object.FindObjectOfType<VegetationScatter>();
            }
            _wind = Object.FindObjectOfType<WindMultiplierApplier>();

            SyncEditor(ref _terrainSettingsEditor, _terrain != null ? _terrain.settings : null);
            SyncEditor(ref _vegetationSettingsEditor, _scatter != null ? _scatter.settings : null);
            // 바람은 에셋이 아니라 씬 컴포넌트라 기본 인스펙터를 통째로 그리면 맨 위 Script
            // 참조 행까지 따라 그려지고(인스펙터 창 밖에서는 그 행이 스크립트를 못 찾아
            // "script can not be loaded" 경고를 띄운다), 여기선 그 행이 필요하지도 않다 —
            // 필요한 두 필드만 직접 그린다.
            _windSerialized = _wind != null ? new SerializedObject(_wind) : null;

            Repaint();
        }

        // 대상이 그대로면 에디터 인스턴스를 재사용한다 — hierarchyChanged마다 새로 만들면
        // 인스펙터의 펼침 상태가 매번 초기화되고 인스턴스도 계속 새어나간다.
        static void SyncEditor(ref Editor editor, Object target)
        {
            if (editor != null && editor.target == target)
            {
                return;
            }

            if (editor != null)
            {
                DestroyImmediate(editor);
                editor = null;
            }

            if (target != null)
            {
                editor = Editor.CreateEditor(target);
            }
        }

        void DestroyEditors()
        {
            SyncEditor(ref _terrainSettingsEditor, null);
            SyncEditor(ref _vegetationSettingsEditor, null);
            _windSerialized = null;
        }

        void OnGUI()
        {
            if (_terrain == null)
            {
                EditorGUILayout.HelpBox("현재 씬에 ProceduralTerrainMesh가 없습니다.", MessageType.Info);
                if (GUILayout.Button("지형 새로 만들기 (Create Procedural Terrain)"))
                {
                    TerrainSceneSetup.CreateProceduralTerrain();
                    Refresh();
                }
                if (GUILayout.Button("새로고침"))
                {
                    Refresh();
                }
                return;
            }

            DrawTargetHeader();
            DrawActions();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawSettingsSection(ref _showTerrain, ref _autoRegenerateTerrain, "지형 설정",
                _terrainSettingsEditor,
                "지형 settings가 비어있습니다. '전체 셋업'을 한 번 누르면 자동으로 만들어집니다.",
                RegenerateTerrain,
                "값을 바꿀 때마다 자동으로 지형을 재생성한다. 노이즈/높이를 눈으로 맞출 때 편하지만 " +
                "격자가 크면 느려진다.");

            DrawSettingsSection(ref _showVegetation, ref _autoScatter, "식생 설정",
                _vegetationSettingsEditor,
                _scatter == null
                    ? "씬에 VegetationScatter가 없습니다. '전체 셋업'을 누르면 추가됩니다."
                    : "식생 settings가 비어있습니다. '전체 셋업'을 한 번 누르면 자동으로 만들어집니다.",
                RunScatter,
                "값을 바꿀 때마다 자동으로 다시 뿌린다. 개수가 많은 그룹이 있으면 슬라이더를 " +
                "드래그하는 내내 재배치가 돌아 매우 느려질 수 있다.");

            DrawWindSection();
            DrawResolutionSection();
            EditorGUILayout.EndScrollView();
        }

        void DrawTargetHeader()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"씬: {_terrain.gameObject.scene.name}", EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField("지형", _terrain, typeof(ProceduralTerrainMesh), true);
                    EditorGUILayout.ObjectField("식생", _scatter, typeof(VegetationScatter), true);
                }
            }
        }

        void DrawActions()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("실행", EditorStyles.boldLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("지형 재생성",
                        "지형 메시/길/호수/가장자리벽만 다시 만든다. 식생은 건드리지 않는다."),
                        GUILayout.Height(24f)))
                    {
                        RegenerateTerrain();
                    }

                    using (new EditorGUI.DisabledScope(_scatter == null))
                    {
                        if (GUILayout.Button(new GUIContent("식생 재배치",
                            "지형은 그대로 두고 식생만 다시 뿌린다."), GUILayout.Height(24f)))
                        {
                            RunScatter();
                        }

                        if (GUILayout.Button(new GUIContent("지형+식생",
                            "지형을 재생성한 뒤 이어서 식생까지 다시 뿌린다. 지형 모양을 바꿨을 때 " +
                            "식생만 예전 지형 기준으로 남는 걸 막아준다."), GUILayout.Height(24f)))
                        {
                            RegenerateTerrain();
                            RunScatter();
                        }
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("전체 셋업",
                        "Mountains > Create Procedural Terrain과 동일 — 머티리얼/스카이박스/라이트/" +
                        "카메라/식생 컴포넌트까지 한 번에 보정한다.")))
                    {
                        TerrainSceneSetup.CreateProceduralTerrain();
                        Refresh();
                    }

                    if (GUILayout.Button(new GUIContent("NavMesh 굽기",
                        "지형을 한 번 재생성한 뒤 NavMesh를 다시 굽는다(물 제외 볼륨이 Generate 안에서만 " +
                        "갱신되기 때문).")))
                    {
                        TerrainSceneSetup.BakeNavMesh();
                    }

                    using (new EditorGUI.DisabledScope(_scatter == null))
                    {
                        if (GUILayout.Button(new GUIContent("식생 지우기",
                            "배치된 식생 인스턴스를 모두 제거한다.")))
                        {
                            _scatter.ClearInstances();
                            MarkSceneDirty();
                        }
                    }

                    if (GUILayout.Button(new GUIContent("재생성 (높이 유지)",
                        "지형을 재생성하되, 씬에 배치한 오브젝트들의 '지면 대비 높이'를 그대로 " +
                        "유지한다. 지형 파라미터를 바꿔도 이벤트 트리거가 묻히거나 뜨지 않는다.")))
                    {
                        RegenerateKeepingObjectHeights();
                    }

                    if (GUILayout.Button(new GUIContent("고도/경사 분포",
                        "지금 지형의 고도·경사 분포를 콘솔에 출력한다. 식생 그룹의 " +
                        "minHeight/maxHeight, minSlope/maxSlope를 감으로 찍지 않고 실제 지형에 " +
                        "맞춰 정할 때 쓴다.")))
                    {
                        LogTerrainDistribution();
                    }
                }
            }
        }

        // 지형/식생 섹션은 "설정 에셋 인스펙터 + 에셋 Ping + 시드 랜덤 + 값 바꾸면 자동 실행"
        // 이라는 완전히 같은 구성이라 한 메서드로 묶었다. run은 그 설정을 실제로 반영하는
        // 동작(지형 재생성 / 식생 재배치)이다.
        void DrawSettingsSection(ref bool show, ref bool autoRun, string title, Editor settingsEditor,
            string missingMessage, System.Action run, string autoRunTooltip)
        {
            show = EditorGUILayout.Foldout(show, title, true, EditorStyles.foldoutHeader);
            if (!show)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                // 컴포넌트가 없거나 settings가 비어 있으면 에디터도 못 만들어진다 —
                // 어느 쪽인지는 호출부가 아는 내용이라 메시지를 받아서 그대로 띄운다.
                if (settingsEditor == null)
                {
                    EditorGUILayout.HelpBox(missingMessage, MessageType.Warning);
                    return;
                }

                var settings = settingsEditor.target;

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(settings.name, EditorStyles.miniBoldLabel);
                    if (GUILayout.Button("에셋 선택", EditorStyles.miniButton, GUILayout.Width(70f)))
                    {
                        Selection.activeObject = settings;
                        EditorGUIUtility.PingObject(settings);
                    }
                    if (GUILayout.Button("시드 랜덤", EditorStyles.miniButton, GUILayout.Width(70f)))
                    {
                        RandomizeSeed(settingsEditor);
                        run();
                    }
                }

                autoRun = EditorGUILayout.ToggleLeft(new GUIContent("값 변경 시 자동 실행", autoRunTooltip), autoRun);

                EditorGUI.BeginChangeCheck();
                settingsEditor.OnInspectorGUI();
                if (EditorGUI.EndChangeCheck() && autoRun)
                {
                    run();
                }
            }
        }

        // 두 설정 에셋 모두 시드 필드 이름이 seed로 같아서 SerializedObject로 공통 처리한다 —
        // 타입별로 따로 쓰던 Undo.RecordObject/SetDirty 짝도 ApplyModifiedProperties가 대신해준다.
        static void RandomizeSeed(Editor settingsEditor)
        {
            var serialized = settingsEditor.serializedObject;
            serialized.Update();
            serialized.FindProperty("seed").intValue = Random.Range(int.MinValue, int.MaxValue);
            serialized.ApplyModifiedProperties();
        }

        // 해상도(cellSize)를 바꾸면 지형 정점 간격이 달라져서 식생 배치 조건(고도/경사)이
        // 평가되는 단위도 같이 정밀해진다. 다만 길/호수 웨이포인트가 격자 좌표라 그냥 바꾸면
        // 전부 엉뚱한 위치로 옮겨가고, 노이즈도 격자 좌표로 샘플링해서 산 배치가 통째로
        // 달라진다 — 셋을 한 번에 보정해주는 전용 명령이 필요하다.
        void DrawResolutionSection()
        {
            _showResolution = EditorGUILayout.Foldout(_showResolution, "해상도 변경", true, EditorStyles.foldoutHeader);
            if (!_showResolution || _terrain.settings == null)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                var settings = _terrain.settings;
                float currentExtentX = (settings.width - 1) * settings.cellSize;
                float currentExtentZ = (settings.length - 1) * settings.cellSize;

                EditorGUILayout.LabelField($"현재: {settings.width}x{settings.length} / cellSize {settings.cellSize}" +
                    $" → {currentExtentX:0}m x {currentExtentZ:0}m");

                _targetCellSize = EditorGUILayout.FloatField("목표 cellSize", _targetCellSize);
                _targetCellSize = Mathf.Max(0.1f, _targetCellSize);

                float ratio = settings.cellSize / _targetCellSize;
                int newWidth = Mathf.RoundToInt((settings.width - 1) * ratio) + 1;
                int newLength = Mathf.RoundToInt((settings.length - 1) * ratio) + 1;

                EditorGUILayout.LabelField($"변경 후: {newWidth}x{newLength} (정점 {newWidth * newLength:N0}개, 배율 {ratio:0.##}배)");

                EditorGUILayout.HelpBox("길/호수 웨이포인트와 노이즈 파라미터(noiseScale, 위상)를 같은 비율로 " +
                    "보정하므로 지형 모양과 길·호수 위치는 월드 기준으로 그대로 유지됩니다. " +
                    "다만 더 촘촘히 샘플링하면서 예전엔 안 보이던 굴곡이 드러나므로 표면 높이는 " +
                    "조금씩 달라집니다 — 씬에 직접 배치한 오브젝트는 Ctrl+Shift+G(Snap Selected To Terrain Y)로 " +
                    "다시 붙이세요.", MessageType.Info);

                using (new EditorGUI.DisabledScope(Mathf.Approximately(ratio, 1f)))
                {
                    if (GUILayout.Button("해상도 변경 + 웨이포인트 변환"))
                    {
                        ChangeResolution(ratio, newWidth, newLength);
                    }
                }
            }
        }

        void ChangeResolution(float ratio, int newWidth, int newLength)
        {
            TerrainTools.ChangeResolution(_terrain, _targetCellSize, newWidth, newLength, ratio);

            RegenerateTerrain();
            RunScatter();

            Debug.Log($"[TerrainControlPanel] 해상도를 {newWidth}x{newLength} (cellSize {_targetCellSize})로 바꾸고 " +
                $"길/호수 웨이포인트와 노이즈를 {ratio:0.##}배로 보정했습니다. 씬에 배치한 오브젝트는 " +
                "Ctrl+Shift+G로 지형 높이에 다시 붙이세요.");
        }

        void DrawWindSection()
        {
            _showWind = EditorGUILayout.Foldout(_showWind, "바람", true, EditorStyles.foldoutHeader);
            if (!_showWind)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                if (_wind == null || _windSerialized == null)
                {
                    EditorGUILayout.HelpBox("씬에 WindMultiplierApplier가 없습니다. 빈 오브젝트에 추가하면 " +
                        "이 씬의 바람 세기를 조절할 수 있습니다.", MessageType.Info);
                    return;
                }

                // ApplyModifiedProperties가 대상의 OnValidate를 불러주고, 거기서 곧바로 머티리얼에
                // 반영되므로(WindMultiplierApplier.Apply) 별도 실행 버튼이 필요 없다.
                _windSerialized.Update();
                EditorGUILayout.PropertyField(_windSerialized.FindProperty("settings"));
                EditorGUILayout.PropertyField(_windSerialized.FindProperty("multiplier"));
                _windSerialized.ApplyModifiedProperties();
            }
        }

        void LogTerrainDistribution()
        {
            Debug.Log(TerrainTools.DescribeDistribution(_terrain));
        }

        // 재생성 전에 배치물의 지면 대비 높이를 기록해두고, 재생성 후 그대로 복원한다 —
        // 지형 파라미터를 바꿔도 이벤트 트리거가 묻히거나 뜨지 않게.
        void RegenerateKeepingObjectHeights()
        {
            var snapshot = new TerrainTools.HeightOffsetSnapshot(_terrain);
            RegenerateTerrain();
            snapshot.Restore();

            Debug.Log($"[TerrainControlPanel] 지형을 재생성하고 배치물 {snapshot.Count}개의 지면 대비 높이를 " +
                "유지했습니다. NavMesh는 따로 다시 구워야 합니다.");
        }

        void RegenerateTerrain()
        {
            _terrain.Generate();
            EditorUtility.SetDirty(_terrain);

            // heightMultiplier를 바꾸면 지형의 높이 범위가 달라진다 — 머티리얼의 _MaxHeight를
            // 같이 갱신하지 않으면 고도별 색이 예전 범위 기준으로 남아 눈/바위 경계가 어긋난다.
            var renderer = _terrain.GetComponent<MeshRenderer>();
            if (renderer != null && renderer.sharedMaterial != null)
            {
                TerrainSceneSetup.ApplyHeightRangeToMaterial(renderer.sharedMaterial, _terrain);
            }

            MarkSceneDirty();
        }

        void RunScatter()
        {
            if (_scatter == null)
            {
                return;
            }

            _scatter.Scatter();
            EditorUtility.SetDirty(_scatter);
            MarkSceneDirty();
        }

        void MarkSceneDirty()
        {
            EditorSceneManager.MarkSceneDirty(_terrain.gameObject.scene);
        }
    }
}

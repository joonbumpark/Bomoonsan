using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Mountains
{
    /// <summary>
    /// 현재 씬에 배치된 EventTrigger 목록을 보여주고, 선택한 트리거 위치를
    /// GameManager.playerSpawnPoint 로 지정하는 디버그 윈도우.
    /// 메뉴: Tools/Debug/Spawn Point Changer
    ///
    /// - Edit 모드: playerSpawnPoint 를 바꾸고 씬을 dirty 로 표시 (저장하면 유지, 다음 Play 부터 적용).
    /// - Play 모드: GameManager 는 Start() 에서 한 번만 스폰하므로, 이미 스폰된 플레이어는
    ///   "Set + Teleport" 로 즉시 옮긴다. (Play 중 변경한 값은 Play 종료 시 되돌아간다.)
    /// </summary>
    public class SpawnPointDebugWindow : EditorWindow
    {
        GameManager _gameManager;
        readonly List<EventTrigger> _triggers = new List<EventTrigger>();
        Vector2 _scroll;
        string _filter = "";
        bool _includeInactive = true;
        bool _autoRefresh = true;

        [MenuItem("Tools/Debug/Spawn Point Changer")]
        public static void Open()
        {
            var window = GetWindow<SpawnPointDebugWindow>("Spawn Point");
            window.minSize = new Vector2(380, 280);
            window.Refresh();
        }

        void OnEnable()
        {
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            Refresh();
        }

        void OnDisable()
        {
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        void OnSceneOpened(Scene scene, OpenSceneMode mode) { Refresh(); Repaint(); }
        void OnPlayModeChanged(PlayModeStateChange state) { Refresh(); Repaint(); }
        void OnHierarchyChanged()
        {
            if (!_autoRefresh) return;
            Refresh();
            Repaint();
        }

        void Refresh()
        {
            _gameManager = FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);

            _triggers.Clear();
            var found = FindObjectsByType<EventTrigger>(
                _includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            _triggers.AddRange(found.OrderBy(t => DisplayName(t), StringComparer.OrdinalIgnoreCase));
        }

        // "부모 / 트리거" 형태. 부모가 없으면 트리거 이름만.
        static string DisplayName(EventTrigger trigger)
        {
            var parent = trigger.transform.parent;
            return parent != null ? $"{parent.name} / {trigger.name}" : trigger.name;
        }

        static string HierarchyPath(Transform t)
        {
            string path = t.name;
            for (var p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
            return path;
        }

        // ───────────── 적용 ─────────────

        void SetSpawn(EventTrigger trigger)
        {
            if (_gameManager == null)
            {
                ShowNotification(new GUIContent("씬에 GameManager 가 없습니다"));
                return;
            }

            Undo.RecordObject(_gameManager, "Change Player Spawn Point");
            _gameManager.playerSpawnPoint = trigger.transform;
            EditorUtility.SetDirty(_gameManager);
            if (!Application.isPlaying)
            {
                EditorSceneManager.MarkSceneDirty(_gameManager.gameObject.scene);
            }

            Debug.Log($"[SpawnPoint] playerSpawnPoint → '{DisplayName(trigger)}' {trigger.transform.position}", trigger);
            ShowNotification(new GUIContent($"스폰 지점: {DisplayName(trigger)}"));
        }

        void TeleportPlayer(EventTrigger trigger)
        {
            if (!Application.isPlaying) return;

            var player = _gameManager != null ? _gameManager.Player : null;
            if (player == null)
            {
                var go = GameObject.FindGameObjectWithTag("Player");
                if (go != null) player = go.GetComponent<Player>();
            }
            if (player == null)
            {
                ShowNotification(new GUIContent("아직 Player 가 스폰되지 않았습니다"));
                return;
            }

            // 지형 높이에 맞춰 배치 (Terrain 이 없으면 트리거 위치 그대로).
            var pos = trigger.transform.position;
            if (_gameManager != null && _gameManager.Terrain != null)
            {
                pos = _gameManager.GetTerrainPosition(pos.x, pos.z);
            }

            var cc = player.GetComponent<CharacterController>();
            var agent = player.GetComponent<NavMeshAgent>();

            if (cc != null) cc.enabled = false;
            if (agent != null && agent.isOnNavMesh) agent.Warp(pos);
            else player.transform.position = pos;
            if (cc != null) cc.enabled = true;
        }

        // ───────────── GUI ─────────────

        void OnGUI()
        {
            DrawToolbar();
            DrawStatus();

            _filter = EditorGUILayout.TextField("Filter", _filter);
            EditorGUILayout.LabelField($"Event Triggers ({_triggers.Count})", EditorStyles.boldLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var trigger in _triggers)
            {
                if (trigger == null) continue;
                if (!string.IsNullOrEmpty(_filter) &&
                    HierarchyPath(trigger.transform).IndexOf(_filter, StringComparison.OrdinalIgnoreCase) < 0) continue;

                DrawRow(trigger);
            }
            EditorGUILayout.EndScrollView();
        }

        void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label($"Scene: {SceneManager.GetActiveScene().name}", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

                EditorGUI.BeginChangeCheck();
                _includeInactive = GUILayout.Toggle(_includeInactive, "Inactive", EditorStyles.toolbarButton, GUILayout.Width(58));
                if (EditorGUI.EndChangeCheck()) Refresh();

                _autoRefresh = GUILayout.Toggle(_autoRefresh, "Auto", EditorStyles.toolbarButton, GUILayout.Width(44));
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60))) Refresh();
            }
        }

        void DrawStatus()
        {
            if (_gameManager == null)
            {
                EditorGUILayout.HelpBox("현재 씬에서 GameManager 를 찾지 못했습니다.", MessageType.Warning);
                return;
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("GameManager", _gameManager, typeof(GameManager), true);
                EditorGUILayout.ObjectField("Player Spawn Point", _gameManager.playerSpawnPoint, typeof(Transform), true);
            }

            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Play 중입니다. GameManager 는 Start() 에서 한 번만 스폰하므로, 이미 스폰된 플레이어는 " +
                    "'Set+TP' 로 옮기세요. Play 중 바꾼 스폰 지점은 종료 시 원래대로 돌아갑니다.",
                    MessageType.Info);
            }
        }

        void DrawRow(EventTrigger trigger)
        {
            bool isCurrent = _gameManager != null && _gameManager.playerSpawnPoint == trigger.transform;

            var prevBg = GUI.backgroundColor;
            if (isCurrent) GUI.backgroundColor = new Color(0.55f, 1f, 0.6f);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUI.backgroundColor = prevBg;

                string text = (isCurrent ? "★ " : "") + DisplayName(trigger) +
                              (trigger.gameObject.activeInHierarchy ? "" : " (inactive)");
                var label = new GUIContent(text, HierarchyPath(trigger.transform));
                if (GUILayout.Button(label, EditorStyles.label, GUILayout.MinWidth(120)))
                {
                    Selection.activeGameObject = trigger.gameObject;
                    EditorGUIUtility.PingObject(trigger.gameObject);
                }

                if (GUILayout.Button("Focus", GUILayout.Width(48)))
                {
                    Selection.activeGameObject = trigger.gameObject;
                    SceneView.lastActiveSceneView?.FrameSelected();
                }

                if (GUILayout.Button("Set", GUILayout.Width(40)))
                {
                    SetSpawn(trigger);
                }

                using (new EditorGUI.DisabledScope(!Application.isPlaying))
                {
                    if (GUILayout.Button("Set+TP", GUILayout.Width(56)))
                    {
                        SetSpawn(trigger);
                        TeleportPlayer(trigger);
                    }
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mountains
{
    // 특정 씬이 로드되면 "씬에 배치하지 않아도 알아서 동작하는" 도우미(BGM, 버튼 클릭음, 발소리 등)를
    // 깨우는 공통 진입점. 씬 파일을 건드리지 않고 기능을 붙이기 위해 쓴다.
    //
    // 쓰는 법: [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] 정적 메서드에서
    //   SceneBootstrap.Register<MyComponent>(SceneBootstrap.MountainScene);   // 컴포넌트를 씬에 하나 만든다
    //   SceneBootstrap.OnSceneReady(key, SceneBootstrap.MountainScene, scene => ...); // 직접 처리한다
    public static class SceneBootstrap
    {
        public const string MountainScene = "Mountain 5";

        class Entry
        {
            public string sceneName;
            public Action<Scene> callback;
        }

        static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>();

        // sceneName 씬이 로드될 때마다 callback을 부른다. 이미 그 씬에 들어와 있으면 즉시 한 번 부른다
        // (AfterSceneLoad는 첫 씬이 로드된 뒤에 불리므로 첫 씬은 직접 처리해야 한다).
        // 같은 key로 다시 등록하면 덮어쓴다 — 도메인 리로드를 꺼둔 프로젝트에서 Play를 다시 눌러도 중복되지 않는다.
        public static void OnSceneReady(string key, string sceneName, Action<Scene> callback)
        {
            var entry = new Entry { sceneName = sceneName, callback = callback };
            Entries[key] = entry;

            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;

            Invoke(entry, SceneManager.GetActiveScene());
        }

        // sceneName 씬에 T 컴포넌트를 가진 오브젝트를 하나 만든다(이미 있으면 만들지 않는다).
        // 오브젝트는 그 씬에 속해서 씬이 내려갈 때 같이 사라진다.
        public static void Register<T>(string sceneName) where T : Component
        {
            OnSceneReady(typeof(T).Name, sceneName, scene =>
            {
                if (UnityEngine.Object.FindFirstObjectByType<T>() != null)
                {
                    return;
                }

                var go = new GameObject(typeof(T).Name);
                SceneManager.MoveGameObjectToScene(go, scene);
                go.AddComponent<T>();
            });
        }

        static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            foreach (var entry in Entries.Values)
            {
                Invoke(entry, scene);
            }
        }

        static void Invoke(Entry entry, Scene scene)
        {
            if (scene.name == entry.sceneName)
            {
                entry.callback(scene);
            }
        }
    }
}

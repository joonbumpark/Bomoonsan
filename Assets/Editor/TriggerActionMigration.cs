using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Mountains
{
    // 예전 방식(자식 GameObject + MonoBehaviour)으로 만들어둔 액션을, 새 방식
    // ([SerializeReference]로 EventTrigger 안에 직접 들어가는 순수 클래스)으로 옮긴다.
    //
    // 클래스를 바로 갈아치우지 않고 Legacy*로 남겨둔 이유가 이것이다 — 옛 컴포넌트가 살아
    // 있어야 그 안의 값(어떤 DialogData인지, 어떤 CharacterData인지)을 읽어올 수 있다.
    // 이 메뉴를 돌린 뒤에야 Legacy 폴더를 지울 수 있다.
    static class TriggerActionMigration
    {
        // 키는 NpcKeyStore 한 곳에 문자열로 모인다(Tag Manager와 같은 방식).

        [MenuItem("Mountains/TriggerAction 마이그레이션 (Legacy -> SerializeReference)")]
        static void Migrate()
        {
            var triggers = UnityEngine.Object
                .FindObjectsByType<EventTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (triggers.Length == 0)
            {
                Debug.Log("[TriggerActionMigration] 씬에 EventTrigger가 없습니다.");
                return;
            }

            // 1차: 스폰 액션을 먼저 옮기면서 "옛 컴포넌트 -> 새 인스턴스" 지도를 만든다.
            // 제거/연출 액션이 스폰 액션을 참조하고 있어서, 그걸 키로 바꾸려면 먼저 필요하다.
            var keyMap = new Dictionary<LegacyTriggerAction, string>();
            var store = NpcKeyDrawer.FindOrCreateStore();
            int converted = 0, failed = 0;
            var log = new System.Text.StringBuilder();

            foreach (var trigger in triggers)
            {
                if (trigger.steps == null)
                {
                    continue;
                }

                Undo.RegisterCompleteObjectUndo(trigger, "Migrate TriggerActions");

                foreach (var step in trigger.steps)
                {
                    if (step?.actions == null || step.actions.Length == 0)
                    {
                        continue;
                    }

                    var newActions = new List<TriggerAction>(step.actions.Length);
                    foreach (var legacy in step.actions)
                    {
                        if (legacy == null)
                        {
                            continue;
                        }

                        var converted_action = Convert(legacy, store, keyMap, log);
                        if (converted_action == null)
                        {
                            failed++;
                            continue;
                        }

                        newActions.Add(converted_action);
                        converted++;
                    }

                    step.newActions = newActions.ToArray();
                }

                EditorUtility.SetDirty(trigger);
            }

            // 2차: 스폰 키 참조를 채운다(1차에서 아직 안 만들어진 스폰 액션을 가리켰을 수 있다).
            ResolvePendingKeys(triggers, store, keyMap, log);

            // 옛 컴포넌트 제거는 전부 옮긴 뒤에 한다 — 중간에 지우면 아직 읽어야 할 참조가 끊긴다.
            int removed = RemoveLegacyObjects(triggers);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Debug.Log($"[TriggerActionMigration] 액션 {converted}개 변환, 실패 {failed}개, " +
                $"옛 오브젝트 {removed}개 제거.\n{log}\n" +
                "씬을 저장한 뒤 Play로 확인하세요. 문제가 없으면 Assets/Scripts/Legacy를 지우면 됩니다.");
        }

        // Legacy<X> -> <X> 로 같은 이름의 새 타입을 찾아 필드를 이름 기준으로 복사한다.
        static TriggerAction Convert(LegacyTriggerAction legacy, NpcKeyStore store,
            Dictionary<LegacyTriggerAction, string> keyMap,
            System.Text.StringBuilder log)
        {
            string legacyName = legacy.GetType().Name;
            string newName = legacyName.StartsWith("Legacy") ? legacyName.Substring("Legacy".Length) : legacyName;

            // 이름이 그대로 이어지지 않는 액션들. 옛 MoveThenRotate는 DOMove로 직선 보간했고,
            // 지금 그 이름은 NavMesh로 걸어가는 쪽이 쓴다 — 동작을 유지하려면 트윈 쪽으로 옮겨야 한다.
            if (newName == "MoveThenRotatePlayerAction")
            {
                newName = "TweenMoveThenRotatePlayerAction";
            }

            var newType = typeof(TriggerAction).Assembly.GetType($"Mountains.{newName}");
            if (newType == null || newType.IsAbstract)
            {
                log.AppendLine($"  [실패] {legacyName}: 대응하는 새 타입 Mountains.{newName}을 찾지 못했습니다.");
                return null;
            }

            var instance = (TriggerAction)Activator.CreateInstance(newType);
            CopyFields(legacy, instance);
            ConvertLoopFlag(legacy, instance);

            // 스폰 액션이면 키를 붙여서 나중에 지목할 수 있게 한다.
            var keyField = newType.GetField("spawnKey");
            if (keyField != null && keyField.FieldType == typeof(string))
            {
                keyField.SetValue(instance, GetOrCreateKey(legacy, store, keyMap));
            }

            log.AppendLine($"  {legacy.gameObject.name} : {legacyName} -> {newName}");
            return instance;
        }

        // 옛 경로 이동 액션은 loop(bool)로 무한/한 바퀴를 표현했고, 지금은 loopCount 하나로
        // 합쳤다(-1=무한). 이름이 달라 일반 복사에 걸리지 않으므로 여기서 따로 옮긴다.
        static void ConvertLoopFlag(object legacy, object instance)
        {
            var loopField = legacy.GetType().GetField("loop");
            var countField = instance.GetType().GetField("loopCount");
            if (loopField == null || countField == null || loopField.FieldType != typeof(bool))
            {
                return;
            }

            countField.SetValue(instance, (bool)loopField.GetValue(legacy) ? -1 : 1);
        }

        // 이름과 타입이 맞는 public 필드만 그대로 옮긴다. enum은 타입이 달라도(Legacy쪽 중첩
        // enum) 값이 같은 순서로 선언돼 있으므로 정수 값으로 옮긴다.
        static void CopyFields(object source, object destination)
        {
            var sourceFields = source.GetType()
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .ToDictionary(f => f.Name, f => f);

            foreach (var target in destination.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!sourceFields.TryGetValue(target.Name, out var from))
                {
                    continue;
                }

                object value = from.GetValue(source);
                if (value == null)
                {
                    continue;
                }

                if (target.FieldType.IsEnum && from.FieldType.IsEnum)
                {
                    target.SetValue(destination, Enum.ToObject(target.FieldType, (int)value));
                    continue;
                }

                if (target.FieldType.IsAssignableFrom(from.FieldType))
                {
                    target.SetValue(destination, value);
                }
            }
        }

        // 제거/연출 액션의 spawnActions(옛 컴포넌트 참조 배열)를 spawnKeys로 바꾼다.
        static void ResolvePendingKeys(EventTrigger[] triggers, NpcKeyStore store,
            Dictionary<LegacyTriggerAction, string> keyMap, System.Text.StringBuilder log)
        {
            foreach (var trigger in triggers)
            {
                if (trigger.steps == null)
                {
                    continue;
                }

                foreach (var step in trigger.steps)
                {
                    if (step?.actions == null || step.newActions == null)
                    {
                        continue;
                    }

                    int count = Math.Min(step.actions.Length, step.newActions.Length);
                    for (int i = 0; i < count; i++)
                    {
                        var legacy = step.actions[i];
                        var target = step.newActions[i] as NpcTargetingAction;
                        if (legacy == null || target == null)
                        {
                            continue;
                        }

                        var field = legacy.GetType().GetField("spawnActions");
                        if (field?.GetValue(legacy) is not Array array)
                        {
                            continue;
                        }

                        var keys = new List<string>();
                        foreach (var item in array)
                        {
                            if (item is LegacyTriggerAction spawn)
                            {
                                keys.Add(GetOrCreateKey(spawn, store, keyMap));
                            }
                        }

                        target.spawnKeys = keys.ToArray();
                        if (keys.Count > 0)
                        {
                            log.AppendLine($"  {trigger.name}: 스폰 참조 {keys.Count}개를 키로 변환");
                        }
                    }
                }
            }
        }

        // 스폰 액션 하나당 키 문자열 하나. 같은 컴포넌트를 두 번 만나도 같은 키를 주고,
        // 저장소에 없으면 등록해서 드롭다운에 나타나게 한다. 이름은 그 액션이 붙어 있던
        // 오브젝트 이름을 쓴다 — 씬에서 무엇을 가리키는 키인지 바로 알아볼 수 있다.
        static string GetOrCreateKey(LegacyTriggerAction spawn, NpcKeyStore store,
            Dictionary<LegacyTriggerAction, string> keyMap)
        {
            if (keyMap.TryGetValue(spawn, out var existing))
            {
                return existing;
            }

            string key = spawn.gameObject.name.Replace("/", "_");
            if (store != null && store.Add(key))
            {
                EditorUtility.SetDirty(store);
            }

            keyMap[spawn] = key;
            return key;
        }

        // 옛 액션 컴포넌트를 지우고, 그것만 달려 있던 오브젝트는 통째로 지운다.
        static int RemoveLegacyObjects(EventTrigger[] triggers)
        {
            int removed = 0;
            foreach (var trigger in triggers)
            {
                if (trigger.steps == null)
                {
                    continue;
                }

                foreach (var step in trigger.steps)
                {
                    if (step?.actions == null)
                    {
                        continue;
                    }

                    foreach (var legacy in step.actions)
                    {
                        if (legacy == null)
                        {
                            continue;
                        }

                        var go = legacy.gameObject;
                        bool onlyAction = go != trigger.gameObject
                            && go.GetComponents<Component>().All(c => c is Transform || c is LegacyTriggerAction);

                        if (onlyAction)
                        {
                            Undo.DestroyObjectImmediate(go);
                        }
                        else
                        {
                            Undo.DestroyObjectImmediate(legacy);
                        }
                        removed++;
                    }

                    step.actions = new LegacyTriggerAction[0];
                }
            }

            return removed;
        }

    }
}

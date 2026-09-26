using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mountains
{
    // 지정한 위치에 FX 프리팹을 하나씩 터뜨리는 액션. 등장/퇴장 어느 쪽에나 같은 방식으로
    // 쓴다 — 등장이면 아직 NPC가 없을 수 있으니 at에 스폰 지점을 직접 물리고, 퇴장이면
    // spawnActions로 사라질 NPC들을 지목하면 각자 위치에 하나씩 나온다.
    // 새로 붙이지 못하게 Add Component 메뉴에서 감춘다 — 이미 붙어 있는
    // 컴포넌트는 그대로 동작하므로 마이그레이션에는 영향이 없다.
    [AddComponentMenu("")]
    public class LegacySpawnFxAction : LegacyNpcTargetingAction
    {
        public GameObject fxPrefab;

        [Header("위치")]
        [Tooltip("지정하면 여기 한 곳에만 생성한다. 비워두면 spawnActions/extraTargets가 " +
            "가리키는 대상들의 위치에 각각 생성하고, 그것마저 없으면 이 액션이 붙어있는 " +
            "오브젝트 위치에 생성한다.")]
        public Transform at;
        public Vector3 offset;

        [Header("수명")]
        [Tooltip("이 시간이 지나면 생성된 FX를 파괴한다. 0 이하면 스스로 정리하는 FX로 보고 두지 않는다.")]
        public float lifetime = 3f;
        [Tooltip("켜두면 lifetime이 끝날 때까지 다음 스텝으로 넘어가지 않는다. 꺼두면 즉시 " +
            "완료 처리돼서 같은 스텝의 제거/이동과 동시에 터진다.")]
        public bool waitForLifetime = false;

        public override void Execute(Action onComplete)
        {
            if (fxPrefab == null)
            {
                Debug.LogWarning("[LegacySpawnFxAction] fxPrefab이 비어 있어 FX를 생성하지 않습니다.");
                onComplete?.Invoke();
                return;
            }

            foreach (var position in CollectPositions())
            {
                FxUtility.Spawn(fxPrefab, position + offset, lifetime);
            }

            if (waitForLifetime && lifetime > 0f)
            {
                StartCoroutine(CompleteAfterLifetime(onComplete));
                return;
            }

            onComplete?.Invoke();
        }

        IEnumerable<Vector3> CollectPositions()
        {
            if (at != null)
            {
                yield return at.position;
                yield break;
            }

            ResolveTargets();
            if (_targets.Count == 0)
            {
                yield return transform.position;
                yield break;
            }

            foreach (var target in _targets)
            {
                yield return target.transform.position;
            }
        }

        IEnumerator CompleteAfterLifetime(Action onComplete)
        {
            yield return new WaitForSeconds(lifetime);
            onComplete?.Invoke();
        }
    }
}

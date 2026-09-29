using DG.Tweening;
using UnityEngine;

namespace Mountains
{
    // NPC 소멸 연출(FX + 트윈) 설정값 묶음. NPC 프리팹에 붙는 NpcDespawnEffect와, 트리거마다
    // 다른 연출을 주기 위한 스폰 액션의 오버라이드 필드가 같은 값을 다루므로 정의를 한 곳에만
    // 둔다 — 양쪽에 같은 필드를 복사해두면 나중에 항목을 추가할 때 한쪽만 고치기 쉽다.
    [System.Serializable]
    public class NpcDespawnEffectSettings
    {
        [Header("FX")]
        public GameObject fxPrefab;
        [Tooltip("생성된 FX를 이 시간 뒤에 파괴한다. 0 이하면 두지 않는다(스스로 정리하는 FX).")]
        public float fxLifetime = 3f;
        public Vector3 fxOffset;

        [Header("트윈")]
        [Tooltip("0이면 트윈 없이 FX만 터뜨리고 바로 파괴한다.")]
        public float tweenDuration = 0.35f;
        public Ease ease = Ease.InBack;
        public bool tweenScale = true;
        [Tooltip("0이 아니면 이만큼 땅속으로 가라앉으면서 사라진다.")]
        public float sinkDistance = 0f;

        // 스폰 액션이 들고 있는 설정을 NPC마다 그대로 넘겨주면 여러 NPC가 같은 인스턴스를
        // 공유하게 된다 — 값 복사본을 만들어 서로 영향을 주지 않게 한다.
        public NpcDespawnEffectSettings Clone()
        {
            return new NpcDespawnEffectSettings
            {
                fxPrefab = fxPrefab,
                fxLifetime = fxLifetime,
                fxOffset = fxOffset,
                tweenDuration = tweenDuration,
                ease = ease,
                tweenScale = tweenScale,
                sinkDistance = sinkDistance
            };
        }
    }
}

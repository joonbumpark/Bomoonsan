using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Mountains.Env
{
    // 물고기를 흩어지게 하는 "놀람 지점". 일정 시간 동안만 살아 있다가 스스로 사라진다.
    public struct FishScarePoint
    {
        public float3 position;
        public float radius;
        public float weight;
    }

    // 놀람 지점을 모아두는 곳. 트리거 액션(ScareFishAction)이 넣고 FishSchool이 매 프레임
    // 읽어간다.
    //
    // 포식자 Transform을 직접 참조하던 방식을 대신한다 — 상시 따라다니는 대상이 아니라
    // "돌을 던졌다", "발을 담갔다" 같은 순간 반응이라 지점과 수명만 있으면 된다. 정적
    // 레지스트리라 액션이 어느 무리를 가리킬지 연결할 필요도 없고, 무리가 여럿이면 모두
    // 같은 지점에 반응한다.
    public static class FishScare
    {
        // 동시에 살아 있을 수 있는 최대 개수. 잡에 넘길 배열 크기라 고정해둔다.
        public const int MaxActive = 8;

        struct Entry
        {
            public FishScarePoint point;
            public float endTime;
        }

        static readonly List<Entry> _entries = new List<Entry>(MaxActive);

        public static void Add(Vector3 position, float radius, float weight, float duration)
        {
            if (radius <= 0f || duration <= 0f)
            {
                return;
            }

            // 가득 찼으면 가장 먼저 끝나는 것을 밀어낸다 — 방금 일어난 일이 더 중요하다.
            if (_entries.Count >= MaxActive)
            {
                int oldest = 0;
                for (int i = 1; i < _entries.Count; i++)
                {
                    if (_entries[i].endTime < _entries[oldest].endTime)
                    {
                        oldest = i;
                    }
                }
                _entries.RemoveAt(oldest);
            }

            _entries.Add(new Entry
            {
                point = new FishScarePoint
                {
                    position = position,
                    radius = radius,
                    weight = weight
                },
                endTime = Time.time + duration
            });
        }

        // 살아 있는 지점만 배열에 채우고 개수를 돌려준다. 만료된 것은 이때 정리한다.
        public static int Collect(NativeArray<FishScarePoint> buffer)
        {
            float now = Time.time;
            int count = 0;

            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                if (_entries[i].endTime <= now)
                {
                    _entries.RemoveAt(i);
                    continue;
                }

                if (count < buffer.Length)
                {
                    buffer[count++] = _entries[i].point;
                }
            }

            return count;
        }

        // 도메인 리로드를 꺼둔 프로젝트라 static 값이 Play 세션 사이에 남는다 —
        // 지난 Play의 놀람 지점이 새 Play까지 이어지지 않게 초기화한다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear()
        {
            _entries.Clear();
        }
    }
}

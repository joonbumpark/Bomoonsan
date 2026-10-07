using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Mountains.Env
{
    // 새 떼 계산을 Burst 잡으로 나눈 것. FishFlockJobs와 같은 격자+Burst 구조를 쓰지만,
    // 물고기는 정해진 높이(WaterHeight)에 붙어 2D(XZ)로만 헤엄치는 반면 새는 고도가
    // 자유로운 진짜 3D 이동이라 — 이웃 탐색/조향/경계 회피를 전부 Y까지 포함해서 계산한다.

    // 각 개체를 자기가 속한 3D 셀에 등록한다.
    [BurstCompile]
    public struct BuildBirdGridJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> Positions;
        public float CellSize;
        public NativeParallelMultiHashMap<int, int>.ParallelWriter Grid;

        public void Execute(int index)
        {
            Grid.Add(BirdGrid.Hash(Positions[index], CellSize), index);
        }
    }

    [BurstCompile]
    public struct BirdFlockJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> Positions;
        [ReadOnly] public NativeArray<float3> Velocities;
        [ReadOnly] public NativeParallelMultiHashMap<int, int> Grid;

        public float CellSize;
        public float PerceptionRadius;
        public float SeparationRadius;
        public float CosHalfFov;
        public float MaxSpeed;
        public float MaxForce;

        public float AlignWeight;
        public float CohereWeight;
        public float SeparateWeight;
        public float BoundsWeight;

        // 수평 경계(X/Z)와 고도 밴드(Y)를 한 상자로 합쳐서 다룬다 — Center는 밴드
        // 중앙(최저/최고 고도의 평균), HalfBounds는 각 축의 절반 폭이다.
        public float3 Center;
        public float3 HalfBounds;

        [WriteOnly] public NativeArray<float3> Accelerations;

        public void Execute(int index)
        {
            float3 position = Positions[index];
            float3 velocity = Velocities[index];
            float3 forward = math.lengthsq(velocity) > 1e-6f ? math.normalize(velocity) : new float3(0f, 0f, 1f);

            float3 alignSum = float3.zero;
            float3 cohereSum = float3.zero;
            float3 separateSum = float3.zero;
            int alignCount = 0, separateCount = 0;

            float perceptionSq = PerceptionRadius * PerceptionRadius;
            float separationSq = SeparationRadius * SeparationRadius;

            int3 cell = BirdGrid.CellOf(position, CellSize);
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        int hash = BirdGrid.Hash(new int3(cell.x + dx, cell.y + dy, cell.z + dz));
                        if (!Grid.TryGetFirstValue(hash, out int other, out var iterator))
                        {
                            continue;
                        }

                        do
                        {
                            if (other == index)
                            {
                                continue;
                            }

                            float3 toOther = Positions[other] - position;
                            float distanceSq = math.lengthsq(toOther);
                            if (distanceSq > perceptionSq || distanceSq < 1e-6f)
                            {
                                continue;
                            }

                            float3 direction = toOther * math.rsqrt(distanceSq);
                            if (math.dot(forward, direction) < CosHalfFov)
                            {
                                continue;
                            }

                            alignSum += Velocities[other];
                            cohereSum += Positions[other];
                            alignCount++;

                            if (distanceSq < separationSq)
                            {
                                separateSum += -direction / distanceSq;
                                separateCount++;
                            }
                        }
                        while (Grid.TryGetNextValue(out other, ref iterator));
                    }
                }
            }

            float3 acceleration = float3.zero;

            if (alignCount > 0)
            {
                acceleration += Steer(alignSum / alignCount, velocity) * AlignWeight;
                acceleration += Steer(cohereSum / alignCount - position, velocity) * CohereWeight;
            }
            if (separateCount > 0)
            {
                acceleration += Steer(separateSum / separateCount, velocity) * SeparateWeight;
            }

            // 상자 경계 회피: 밖으로 나간 축만 안쪽으로 되돌린다(물고기의 사각 경계 회피를
            // Y까지 한 축 더 넣은 것).
            float3 local = position - Center;
            float3 boundsForce = float3.zero;
            if (math.abs(local.x) > HalfBounds.x)
            {
                boundsForce.x = math.sign(local.x) * HalfBounds.x * 0.8f - local.x;
            }
            if (math.abs(local.y) > HalfBounds.y)
            {
                boundsForce.y = math.sign(local.y) * HalfBounds.y * 0.8f - local.y;
            }
            if (math.abs(local.z) > HalfBounds.z)
            {
                boundsForce.z = math.sign(local.z) * HalfBounds.z * 0.8f - local.z;
            }
            if (math.lengthsq(boundsForce) > 1e-6f)
            {
                acceleration += Steer(boundsForce, velocity) * BoundsWeight;
            }

            Accelerations[index] = acceleration;
        }

        float3 Steer(float3 desiredDirection, float3 velocity)
        {
            if (math.lengthsq(desiredDirection) < 1e-6f)
            {
                return float3.zero;
            }

            float3 desired = math.normalize(desiredDirection) * MaxSpeed;
            return ClampLength(desired - velocity, MaxForce);
        }

        static float3 ClampLength(float3 value, float maxLength)
        {
            float lengthSq = math.lengthsq(value);
            if (lengthSq <= maxLength * maxLength || lengthSq < 1e-6f)
            {
                return value;
            }
            return value * (maxLength * math.rsqrt(lengthSq));
        }
    }

    [BurstCompile]
    public struct IntegrateBirdJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> Accelerations;
        [ReadOnly] public NativeArray<float> Scales;

        public NativeArray<float3> Positions;
        public NativeArray<float3> Velocities;
        public NativeArray<quaternion> Rotations;
        // RenderMeshInstanced는 objectToWorld 레이아웃(= Matrix4x4)만 받는다.
        [WriteOnly] public NativeArray<Matrix4x4> Matrices;

        // 모델 축 보정과 프리팹 안에서 메시가 놓인 상태 — 한 종류뿐이라 인스턴스별
        // 배열이 아니라 값 하나로 받는다.
        public quaternion ModelRotation;
        public float4x4 MeshToRoot;

        public float DeltaTime;
        public float MaxSpeed;
        public float MinSpeed;
        public float TurnSpeed;
        // 최대 피치각의 sin — 속력 대비 수직 속도 비율의 상한.
        public float SinMaxPitch;

        public void Execute(int index)
        {
            float3 velocity = Velocities[index] + Accelerations[index] * DeltaTime;
            float3 forward = math.mul(Rotations[index], new float3(0f, 0f, 1f));

            float speedSq = math.lengthsq(velocity);
            if (speedSq < MinSpeed * MinSpeed)
            {
                velocity = speedSq > 1e-6f
                    ? math.normalize(velocity) * MinSpeed
                    : forward * MinSpeed;
            }
            else if (speedSq > MaxSpeed * MaxSpeed)
            {
                velocity = velocity * (MaxSpeed * math.rsqrt(speedSq));
            }

            velocity = LimitPitch(velocity, forward);

            // 물고기와 달리 Y를 고정하지 않는다 — 고도는 BirdFlockJob의 경계 회피가
            // 잡아주고, 오르내리는 기울기만 LimitPitch로 제한한 채 3D 적분한다.
            float3 position = Positions[index] + velocity * DeltaTime;

            var targetRotation = quaternion.LookRotationSafe(velocity, math.up());
            var rotation = math.slerp(Rotations[index], targetRotation, math.saturate(TurnSpeed * DeltaTime));

            Positions[index] = position;
            Velocities[index] = velocity;
            Rotations[index] = rotation;

            // 모델 축 보정은 회전 뒤에, 메시 배치(MeshToRoot)는 마지막에 곱한다 — 순서가
            // 바뀌면 진행 방향이 어긋나거나 모델이 비스듬해진다(IntegrateJob과 같은 규칙).
            var trs = float4x4.TRS(position, math.mul(rotation, ModelRotation), new float3(Scales[index]));
            Matrices[index] = ToMatrix(math.mul(trs, MeshToRoot));
        }

        // 상승/하강 각도를 SinMaxPitch 이내로 자른다. 속력은 유지하고 수직 성분만 줄인
        // 만큼 수평으로 돌려준다 — 그냥 Y만 깎으면 경계에서 밀릴 때마다 속도가 떨어진다.
        // 회전도 이 속도를 따라가므로 기수가 가파르게 꺾이는 것도 같이 막힌다.
        float3 LimitPitch(float3 velocity, float3 forward)
        {
            float speed = math.length(velocity);
            float maxVertical = speed * SinMaxPitch;
            if (math.abs(velocity.y) <= maxVertical)
            {
                return velocity;
            }

            // 거의 수직이라 수평 방향이 없으면 현재 기수의 수평 방향을 쓴다.
            float2 horizontal = velocity.xz;
            if (math.lengthsq(horizontal) < 1e-6f)
            {
                horizontal = forward.xz;
                if (math.lengthsq(horizontal) < 1e-6f)
                {
                    horizontal = new float2(0f, 1f);
                }
            }

            float horizontalSpeed = speed * math.sqrt(1f - SinMaxPitch * SinMaxPitch);
            horizontal = math.normalize(horizontal) * horizontalSpeed;
            return new float3(horizontal.x, math.sign(velocity.y) * maxVertical, horizontal.y);
        }

        static Matrix4x4 ToMatrix(float4x4 m)
        {
            return new Matrix4x4(
                new Vector4(m.c0.x, m.c0.y, m.c0.z, m.c0.w),
                new Vector4(m.c1.x, m.c1.y, m.c1.z, m.c1.w),
                new Vector4(m.c2.x, m.c2.y, m.c2.z, m.c2.w),
                new Vector4(m.c3.x, m.c3.y, m.c3.z, m.c3.w));
        }
    }

    // 셀 좌표와 해시. FishGrid의 3D 버전 — 새는 고도까지 흩어져 있어 XZ만으로는
    // 이웃 셀 판정이 부정확하다.
    public static class BirdGrid
    {
        public static int3 CellOf(float3 position, float cellSize)
        {
            return new int3(
                (int)math.floor(position.x / cellSize),
                (int)math.floor(position.y / cellSize),
                (int)math.floor(position.z / cellSize));
        }

        public static int Hash(int3 cell)
        {
            return (int)math.hash(cell);
        }

        public static int Hash(float3 position, float cellSize)
        {
            return Hash(CellOf(position, cellSize));
        }
    }
}

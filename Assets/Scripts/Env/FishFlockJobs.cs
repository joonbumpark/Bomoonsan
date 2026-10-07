using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Mountains.Env
{
    // 물고기 무리 계산을 Burst 잡으로 나눈 것.
    //
    // 예전 구현의 진짜 비용은 렌더링이 아니라 이웃 탐색이었다. 모든 쌍을 비교해서(O(n²))
    // 80마리면 6,400쌍, 200마리면 40,000쌍이었고 쌍마다 Distance(sqrt)와 Angle(acos)을
    // 불렀다. 여기서는 (1) 인지 반경 크기의 격자로 나눠 이웃 셀만 보고, (2) 거리는 제곱으로,
    // 시야는 내적으로 비교해 sqrt/acos를 없앤다.

    // 각 물고기를 자기가 속한 셀에 등록한다. 셀 크기를 인지 반경으로 잡으면 주변 3x3 셀만
    // 봐도 반경 안의 이웃이 전부 들어온다.
    [BurstCompile]
    public struct BuildGridJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> Positions;
        public float CellSize;
        public NativeParallelMultiHashMap<int, int>.ParallelWriter Grid;

        public void Execute(int index)
        {
            Grid.Add(FishGrid.Hash(Positions[index], CellSize), index);
        }
    }

    // 이웃을 훑어 가속도를 만든다. 속도 배열은 읽기만 하고 결과를 따로 쓰므로,
    // 같은 프레임 안에서 계산 순서에 따라 결과가 달라지지 않는다.
    [BurstCompile]
    public struct FlockJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> Positions;
        [ReadOnly] public NativeArray<float3> Velocities;
        [ReadOnly] public NativeArray<float> SpeedMultipliers;
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

        public float3 Center;
        public float2 HalfBounds;

        // 일시적인 놀람 지점들. 상시 추적하는 포식자가 아니라 트리거가 만든 순간 반응이라
        // 여러 개가 동시에 살아 있을 수 있다.
        [ReadOnly] public NativeArray<FishScarePoint> Scares;
        public int ScareCount;

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

            int2 cell = FishGrid.CellOf(position, CellSize);
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    int hash = FishGrid.Hash(new int2(cell.x + dx, cell.y + dz));
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
                        toOther.y = 0f;
                        float distanceSq = math.lengthsq(toOther);
                        if (distanceSq > perceptionSq || distanceSq < 1e-6f)
                        {
                            continue;
                        }

                        // 시야 판정: 정규화한 방향과 전방의 내적이 cos(반각)보다 크면 시야 안이다.
                        // Angle(acos) 대신 쓰는 것이라 결과는 같고 비용만 줄어든다.
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
                            // 가까울수록 강하게 밀어낸다(거리 제곱에 반비례).
                            separateSum += -direction / distanceSq;
                            separateCount++;
                        }
                    }
                    while (Grid.TryGetNextValue(out other, ref iterator));
                }
            }

            float maxSpeed = MaxSpeed * SpeedMultipliers[index];
            float3 acceleration = float3.zero;

            if (alignCount > 0)
            {
                acceleration += Steer(alignSum / alignCount, velocity, maxSpeed) * AlignWeight;
                acceleration += Steer(cohereSum / alignCount - position, velocity, maxSpeed) * CohereWeight;
            }
            if (separateCount > 0)
            {
                acceleration += Steer(separateSum / separateCount, velocity, maxSpeed) * SeparateWeight;
            }

            // 사각 경계 회피: 밖으로 나간 축만 안쪽으로 되돌린다.
            float3 local = position - Center;
            float3 boundsForce = float3.zero;
            if (math.abs(local.x) > HalfBounds.x)
            {
                boundsForce.x = math.sign(local.x) * HalfBounds.x * 0.8f - local.x;
            }
            if (math.abs(local.z) > HalfBounds.y)
            {
                boundsForce.z = math.sign(local.z) * HalfBounds.y * 0.8f - local.z;
            }
            if (math.lengthsq(boundsForce) > 1e-6f)
            {
                acceleration += Steer(boundsForce, velocity, maxSpeed) * BoundsWeight;
            }

            // 놀람 지점 회피. 가까울수록 세게 밀리도록 반경 대비 거리로 강도를 준다 —
            // 경계에서 갑자기 반응이 끊기면 물고기가 선을 넘나들며 떨린다.
            for (int i = 0; i < ScareCount; i++)
            {
                var scare = Scares[i];
                float3 fromScare = position - scare.position;
                fromScare.y = 0f;

                float distanceSq = math.lengthsq(fromScare);
                float radiusSq = scare.radius * scare.radius;
                if (distanceSq >= radiusSq || distanceSq < 1e-6f)
                {
                    continue;
                }

                float falloff = 1f - math.sqrt(distanceSq / radiusSq);
                acceleration += Steer(fromScare, velocity, maxSpeed) * (scare.weight * falloff);
            }

            Accelerations[index] = acceleration;
        }

        float3 Steer(float3 desiredDirection, float3 velocity, float maxSpeed)
        {
            desiredDirection.y = 0f;
            if (math.lengthsq(desiredDirection) < 1e-6f)
            {
                return float3.zero;
            }

            float3 desired = math.normalize(desiredDirection) * maxSpeed;
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

    // 속도를 적분해 위치/회전을 갱신하고, 그리는 데 쓸 행렬까지 여기서 만든다 —
    // 메인 스레드에서 다시 훑지 않도록.
    [BurstCompile]
    public struct IntegrateJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> Accelerations;
        [ReadOnly] public NativeArray<float> Scales;
        [ReadOnly] public NativeArray<float> SpeedMultipliers;
        [ReadOnly] public NativeArray<quaternion> ModelRotations;
        [ReadOnly] public NativeArray<float4x4> MeshToRoot;

        public NativeArray<float3> Positions;
        public NativeArray<float3> Velocities;
        public NativeArray<quaternion> Rotations;
        // RenderMeshInstanced는 objectToWorld 레이아웃(= Matrix4x4)만 받는다 —
        // float4x4로 두면 "not a marshaled member" 예외가 난다.
        [WriteOnly] public NativeArray<Matrix4x4> Matrices;

        public float DeltaTime;
        public float MaxSpeed;
        public float MinSpeed;
        public float TurnSpeed;
        public float WaterHeight;

        public void Execute(int index)
        {
            float maxSpeed = MaxSpeed * SpeedMultipliers[index];
            float3 velocity = Velocities[index] + Accelerations[index] * DeltaTime;
            velocity.y = 0f;

            float speedSq = math.lengthsq(velocity);
            if (speedSq < MinSpeed * MinSpeed)
            {
                // 멈춰 서지 않도록 최소 속도를 유지한다. 완전히 0이면 방향을 잃으므로
                // 그때는 임의 방향 대신 현재 바라보는 쪽으로 밀어준다.
                float3 forward = math.mul(Rotations[index], new float3(0f, 0f, 1f));
                velocity = speedSq > 1e-6f
                    ? math.normalize(velocity) * MinSpeed
                    : forward * MinSpeed;
            }
            else if (speedSq > maxSpeed * maxSpeed)
            {
                velocity = velocity * (maxSpeed * math.rsqrt(speedSq));
            }

            float3 position = Positions[index] + velocity * DeltaTime;
            position.y = WaterHeight;

            var targetRotation = quaternion.LookRotationSafe(velocity, math.up());
            var rotation = math.slerp(Rotations[index], targetRotation, math.saturate(TurnSpeed * DeltaTime));

            Positions[index] = position;
            Velocities[index] = velocity;
            Rotations[index] = rotation;

            // 모델 축 보정은 회전 뒤에 곱한다 — 먼저 곱하면 진행 방향이 어긋난다.
            // 프리팹 안에서 메시가 놓인 상태(MeshToRoot)를 마지막에 곱한다 — 회전·오프셋·
            // 스케일이 그대로 재현되어, 프리팹을 씬에 놓았을 때와 같은 모양이 된다.
            var trs = float4x4.TRS(position, math.mul(rotation, ModelRotations[index]),
                new float3(Scales[index]));
            Matrices[index] = ToMatrix(math.mul(trs, MeshToRoot[index]));
        }

        // float4x4 -> Matrix4x4. 열을 그대로 옮기기만 하므로 Burst에서도 그대로 인라인된다.
        static Matrix4x4 ToMatrix(float4x4 m)
        {
            return new Matrix4x4(
                new Vector4(m.c0.x, m.c0.y, m.c0.z, m.c0.w),
                new Vector4(m.c1.x, m.c1.y, m.c1.z, m.c1.w),
                new Vector4(m.c2.x, m.c2.y, m.c2.z, m.c2.w),
                new Vector4(m.c3.x, m.c3.y, m.c3.z, m.c3.w));
        }
    }

    // 셀 좌표와 해시. 잡과 메인 스레드가 같은 규칙을 써야 하므로 한 곳에 둔다.
    public static class FishGrid
    {
        public static int2 CellOf(float3 position, float cellSize)
        {
            return new int2((int)math.floor(position.x / cellSize), (int)math.floor(position.z / cellSize));
        }

        public static int Hash(int2 cell)
        {
            // 좌표를 큰 소수와 섞어 셀마다 다른 값이 나오게 한다.
            return (int)math.hash(cell);
        }

        public static int Hash(float3 position, float cellSize)
        {
            return Hash(CellOf(position, cellSize));
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mountains.Env
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;

    // [예전 구현] FishManager가 마리당 하나씩 들고 도는 보이드. 새 작업에는 FishSchool을 쓴다.
    public class Fish : MonoBehaviour
    {
        [Header("Fish Settings")]
        public float maxSpeed = 5f;
        public float maxForce = 3f;
        public float perceptionRadius = 6f;
        public float fovAngle = 270f;

        [Header("Weights")]
        public float alignWeight = 1.0f;
        public float cohereWeight = 1.0f;
        public float separateWeight = 1.5f;
        public float boundsWeight = 4.0f; // 직사각형 경계 회피 가중치

        [HideInInspector] public Vector3 velocity;
        private Vector3 acceleration;

        void Start()
        {
            Vector2 randomDir = Random.insideUnitCircle.normalized;
            velocity = new Vector3(randomDir.x, 0f, randomDir.y) * Random.Range(2f, maxSpeed);
        }

        public void Flock(List<Fish> allFishes, Vector3 predatorPos, Vector2 boundsSize, Vector3 centerPos)
        {
            Vector3 alignSteering = Vector3.zero;
            Vector3 cohereSteering = Vector3.zero;
            Vector3 separateSteering = Vector3.zero;

            int alignCount = 0, cohereCount = 0, separateCount = 0;

            foreach (Fish other in allFishes)
            {
                if (other == this) continue;

                float distance = Vector3.Distance(
                    new Vector3(transform.position.x, 0, transform.position.z),
                    new Vector3(other.transform.position.x, 0, other.transform.position.z)
                );

                Vector3 dirToOther = (other.transform.position - transform.position).normalized;
                dirToOther.y = 0;

                float angle = Vector3.Angle(transform.forward, dirToOther);

                if (distance < perceptionRadius && angle < fovAngle * 0.5f)
                {
                    // 1. 정렬
                    alignSteering += other.velocity;
                    alignCount++;

                    // 2. 응집
                    cohereSteering += other.transform.position;
                    cohereCount++;

                    // 3. 분리
                    if (distance < perceptionRadius * 0.4f)
                    {
                        Vector3 diff = transform.position - other.transform.position;
                        diff.y = 0;
                        separateSteering += diff.normalized / (distance * distance);
                        separateCount++;
                    }
                }
            }

            Vector3 fAlign = alignCount > 0 ? SteerTo(alignSteering / alignCount) : Vector3.zero;
            Vector3 fCohere = cohereCount > 0 ? SteerTo((cohereSteering / cohereCount) - transform.position) : Vector3.zero;
            Vector3 fSeparate = separateCount > 0 ? SteerTo(separateSteering / separateCount) : Vector3.zero;

            // 4. 직사각형 경계(Rect Bounds) 회피
            Vector3 fBounds = Vector3.zero;
            float halfX = boundsSize.x * 0.5f;
            float halfZ = boundsSize.y * 0.5f; // Vector2의 y를 Z축 길이로 사용

            Vector3 localPos = transform.position - centerPos;

            // X축 경계 체크
            if (Mathf.Abs(localPos.x) > halfX)
            {
                float targetX = Mathf.Sign(localPos.x) * halfX;
                fBounds.x = (centerPos.x + targetX * 0.8f) - transform.position.x;
            }

            // Z축 경계 체크
            if (Mathf.Abs(localPos.z) > halfZ)
            {
                float targetZ = Mathf.Sign(localPos.z) * halfZ;
                fBounds.z = (centerPos.z + targetZ * 0.8f) - transform.position.z;
            }

            if (fBounds != Vector3.zero)
            {
                fBounds = SteerTo(fBounds);
            }

            // 5. 포식자 회피
            Vector3 fEvade = Vector3.zero;
            float predatorDist = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), new Vector3(predatorPos.x, 0, predatorPos.z));
            if (predatorDist < 8f)
            {
                Vector3 evadeDir = transform.position - predatorPos;
                evadeDir.y = 0;
                fEvade = SteerTo(evadeDir) * 3f;
            }

            // 힘 적용
            ApplyForce(fAlign * alignWeight);
            ApplyForce(fCohere * cohereWeight);
            ApplyForce(fSeparate * separateWeight);
            ApplyForce(fBounds * boundsWeight);
            ApplyForce(fEvade);
        }

        public void UpdatePhysics()
        {
            velocity += acceleration * Time.deltaTime;
            velocity.y = 0;

            if (velocity.magnitude < 1.5f)
            {
                velocity = velocity.normalized * 1.5f;
            }

            velocity = Vector3.ClampMagnitude(velocity, maxSpeed);
            transform.position += velocity * Time.deltaTime;

            if (velocity != Vector3.zero)
            {
                float targetAngle = Mathf.Atan2(velocity.x, velocity.z) * Mathf.Rad2Deg;
                Quaternion targetRotation = Quaternion.Euler(0, targetAngle, 0);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 6f);
            }

            acceleration = Vector3.zero;
        }

        private void ApplyForce(Vector3 force)
        {
            force.y = 0;
            acceleration += force;
        }

        private Vector3 SteerTo(Vector3 targetVelocity)
        {
            targetVelocity.y = 0;
            Vector3 desired = targetVelocity.normalized * maxSpeed;
            Vector3 steer = desired - velocity;
            steer.y = 0;
            return Vector3.ClampMagnitude(steer, maxForce);
        }
    }
}

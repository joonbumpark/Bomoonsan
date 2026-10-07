using UnityEngine;

namespace Mountains.Env
{
    // 나비/곤충 에셋이 없어서 코드로 만든 단순 날개 실루엣을 쓴다. 파티클 시스템의 Mesh
    // 렌더 모드에 물려서 쓴다(InsectSwarmSetup 참고) — 움직임/펄럭임은 셰이더가 아니라
    // 파티클 시스템 모듈(Size over Lifetime로 펄럭임, Noise로 흔들림)이 맡으므로 이 메시
    // 자체는 정적이고 아주 단순하다. 두께 없는 평면이라 머티리얼이 양면을 그린다.
    public static class InsectWingMesh
    {
        // 둥글고 넓은 연 모양 날개 두 장.
        public static Mesh BuildButterfly()
        {
            var vertices = new Vector3[]
            {
                new Vector3(0f, 0f, 0f),        // 0: 몸통 중심(날개 뿌리)
                new Vector3(-0.15f, 0f, 0.3f),   // 1: 왼쪽 날개 앞쪽 뿌리
                new Vector3(-0.55f, 0f, 0.05f),  // 2: 왼쪽 날개 끝
                new Vector3(-0.2f, 0f, -0.35f),  // 3: 왼쪽 날개 뒤쪽 뿌리
                new Vector3(0.15f, 0f, 0.3f),    // 4: 오른쪽 날개 앞쪽 뿌리
                new Vector3(0.55f, 0f, 0.05f),   // 5: 오른쪽 날개 끝
                new Vector3(0.2f, 0f, -0.35f),   // 6: 오른쪽 날개 뒤쪽 뿌리
            };

            var triangles = new[]
            {
                0, 1, 2, // 왼쪽 날개 앞
                0, 2, 3, // 왼쪽 날개 뒤
                0, 5, 4, // 오른쪽 날개 앞
                0, 6, 5, // 오른쪽 날개 뒤
            };

            return CreateMesh("InsectWing (Generated)", vertices, triangles);
        }

        // 날개 없이 길쭉한 몸통만. 아주 작게 그려서 "움직이는 어두운 점"으로 보이는 게
        // 목적이라 날개 형태는 필요 없다. 평면 한 장이면 옆에서 볼 때 선이 돼서 사라지므로,
        // 가로(XZ)와 세로(YZ) 마름모를 십자로 겹쳐 어느 각도에서도 면이 보이게 한다.
        public static Mesh BuildFly()
        {
            const float halfWidth = 0.15f;
            var vertices = new Vector3[]
            {
                new Vector3(0f, 0f, 0.35f),        // 0: 머리
                new Vector3(0f, 0f, -0.35f),       // 1: 꼬리
                new Vector3(-halfWidth, 0f, 0.05f), // 2: 가로 마름모 왼쪽
                new Vector3(halfWidth, 0f, 0.05f),  // 3: 가로 마름모 오른쪽
                new Vector3(0f, -halfWidth, 0.05f), // 4: 세로 마름모 아래
                new Vector3(0f, halfWidth, 0.05f),  // 5: 세로 마름모 위
            };

            var triangles = new[]
            {
                0, 2, 1, // 가로 왼쪽
                0, 1, 3, // 가로 오른쪽
                0, 4, 1, // 세로 아래
                0, 1, 5, // 세로 위
            };

            return CreateMesh("InsectFly (Generated)", vertices, triangles);
        }

        static Mesh CreateMesh(string name, Vector3[] vertices, int[] triangles)
        {
            // 정점 색을 흰색으로 채워둔다 — 파티클 셰이더가 메시 색과 파티클 색을 곱할 때
            // 흰색이면 파티클 색(시작 색 × Color over Lifetime)이 그대로 통과한다.
            var colors = new Color[vertices.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = Color.white;
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetColors(colors);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

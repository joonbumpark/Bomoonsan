using UnityEditor;
using UnityEngine;

namespace Mountains
{
    // 플레이어 카메라(Follow/FreeFly 모드를 오가는 Camera.main)가 지금 보고 있는 그대로
    // Scene 뷰를 맞춰준다. Scene 뷰는 눈 위치를 직접 못 정하고 pivot(주시점)+size(거리)로만
    // 카메라를 움직이므로, FOV 기준으로 pivot까지의 거리를 역산해서 실제 눈 위치가
    // Camera.main의 위치와 정확히 일치하게 만든다.
    public static class AlignSceneViewToCamera
    {
        [MenuItem("Mountains/Align Scene View to Camera %#g")]
        public static void Align()
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                Debug.LogWarning("[AlignSceneViewToCamera] 활성화된 Scene 뷰가 없습니다.");
                return;
            }

            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[AlignSceneViewToCamera] Camera.main을 찾을 수 없습니다.");
                return;
            }

            sceneView.rotation = cam.transform.rotation;

            float fov = sceneView.camera != null ? sceneView.camera.fieldOfView : cam.fieldOfView;
            float distance = sceneView.size / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            sceneView.pivot = cam.transform.position + cam.transform.forward * distance;

            sceneView.Repaint();
        }
    }
}

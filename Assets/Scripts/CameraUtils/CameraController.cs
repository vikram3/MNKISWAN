using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.BossRoom.CameraUtils
{
    public class CameraController : MonoBehaviour
    {
        const string k_CMCameraTag = "CMCamera";
        const float k_DefaultHorizontalAxis = 40f;
        const float k_DefaultVerticalAxis = 0.5f;
        const float k_AerialVerticalAxis = 0.82f;
        const float k_AerialFieldOfView = 50f;

        bool m_UseAerialView;

        public void UseAerialView()
        {
            m_UseAerialView = true;
        }

        void Start()
        {
            AttachCamera();
        }

        void AttachCamera()
        {
            var cinemachineCameraGameObject = GameObject.FindGameObjectWithTag(k_CMCameraTag);
            Assert.IsNotNull(cinemachineCameraGameObject);

            var cinemachineCamera = cinemachineCameraGameObject.GetComponent<CinemachineCamera>();
            Assert.IsNotNull(cinemachineCamera, "CameraController.AttachCamera: Couldn't find gameplay CinemachineCamera");

            if (cinemachineCamera != null)
            {
                // camera body / aim
                cinemachineCamera.Follow = transform;
                cinemachineCamera.LookAt = transform;

                if (m_UseAerialView)
                {
                    var lens = cinemachineCamera.Lens;
                    lens.FieldOfView = k_AerialFieldOfView;
                    cinemachineCamera.Lens = lens;
                }
            }

            var cinemachineOrbitalFollow = cinemachineCameraGameObject.GetComponent<CinemachineOrbitalFollow>();
            Assert.IsNotNull(cinemachineOrbitalFollow, "CameraController.AttachCamera: Couldn't find gameplay CinemachineOrbitalFollow");

            if (cinemachineOrbitalFollow != null)
            {
                // default rotation / zoom
                cinemachineOrbitalFollow.HorizontalAxis.Value = k_DefaultHorizontalAxis;
                cinemachineOrbitalFollow.VerticalAxis.Value = m_UseAerialView ? k_AerialVerticalAxis : k_DefaultVerticalAxis;
            }
        }
    }
}

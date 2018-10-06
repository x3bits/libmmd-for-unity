using LibMMD.Motion;
using LibMMD.Reader;
using UnityEngine;

namespace LibMMD.Unity3D
{
    public class MmdCameraObject : MonoBehaviour
    {
        private CameraMotion _cameraMotion = null; 
        public bool Playing = false;
        private double _playTime;
        private Camera _camera;

        public static GameObject CreateGameObject(string name = "MMDCameraObject")
        {
            var cameraObj = new GameObject("Camera");
            var cameraComponent = cameraObj.AddComponent<Camera>();
            var obj = new GameObject(name);
            cameraComponent.transform.SetParent(obj.transform);
            var mmdCameraObject = obj.AddComponent<MmdCameraObject>();
            mmdCameraObject._camera = cameraComponent;            
            return obj;
        }
        
        public void LoadCameraMotion(string path)
        {
            _cameraMotion = new VmdReader().ReadCameraMotion(path);
            ResetMotion();
        }

        public void ResetMotion()
        {
            _playTime = 0.0;
            Playing = false;
        }

        private void Update()
        {
            if (!Playing || _cameraMotion == null)
            {
                return;
            }
            var deltaTime = Time.deltaTime;
            _playTime += deltaTime;
            var cameraPose = _cameraMotion.GetCameraPose(_playTime);
            transform.position = cameraPose.Position;
            transform.rotation = Quaternion.Euler(-cameraPose.Rotation);
            _camera.transform.localPosition = new Vector3(0.0f,0.0f,cameraPose.FocalLength);
            _camera.fieldOfView = cameraPose.Fov;
            _camera.orthographic = cameraPose.Orthographic;
        }
        
    }
}
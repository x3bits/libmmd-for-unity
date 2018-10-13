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
            cameraComponent.backgroundColor = Color.black;
            cameraComponent.clearFlags = CameraClearFlags.Color;
            var obj = new GameObject(name);
            cameraComponent.transform.SetParent(obj.transform);
            var mmdCameraObject = obj.AddComponent<MmdCameraObject>();
            mmdCameraObject._camera = cameraComponent;            
            return obj;
        }
        
        public bool LoadCameraMotion(string path)
        {
            if (path == null)
            {
                _cameraMotion = null;
                return false;
            }
            _cameraMotion = new VmdReader().ReadCameraMotion(path);
            if (_cameraMotion.KeyFrames.Count == 0)
            {
                return false;
            }
            ResetMotion();
            return true;
        }

        public void ResetMotion()
        {
            _playTime = 0.0;
            Playing = false;
            Refresh();
        }

        public void SetPlayPos(double pos)
        {
            _playTime = pos;
            Refresh();
        }

        private void Update()
        {
            if (!Playing || _cameraMotion == null)
            {
                return;
            }
            var deltaTime = Time.deltaTime;
            _playTime += deltaTime;
            Refresh();
        }

        private void Refresh()
        {
            if (_cameraMotion == null)
            {
                return;
            }
            var cameraPose = _cameraMotion.GetCameraPose(_playTime);
            if (cameraPose == null)
            {
                return;
            }
            transform.position = cameraPose.Position;
            transform.rotation = Quaternion.Euler(- 180 / Mathf.PI * cameraPose.Rotation);
            _camera.transform.localPosition = new Vector3(0.0f, 0.0f, cameraPose.FocalLength);
            _camera.fieldOfView = cameraPose.Fov;
            _camera.orthographic = cameraPose.Orthographic;
        }
    }
}
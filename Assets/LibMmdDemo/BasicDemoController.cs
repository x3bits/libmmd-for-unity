using LibMMD.Unity3D;
using UnityEngine;

namespace LibMmdDemo
{
	public class BasicDemoController : MonoBehaviour
	{
		public string ModelPath;

		public string MotionPath;
		
		public string CameraPath;
		
		protected void Start ()
		{
			if (string.IsNullOrEmpty(ModelPath) || string.IsNullOrEmpty(MotionPath) || string.IsNullOrEmpty(CameraPath))
			{
				Debug.LogError("please fill your model, motion and camera file path");
			}
			var mmdObj = MmdGameObject.CreateGameObject("MmdGameObject");
			var mmdGameObject = mmdObj.GetComponent<MmdGameObject>();

			mmdGameObject.LoadModel(ModelPath);
			mmdGameObject.LoadMotion(MotionPath);
			
			//You can set model render options
			mmdGameObject.UpdateConfig(new MmdUnityConfig
			{
				EnableDrawSelfShadow = MmdConfigSwitch.ForceFalse,
				EnableCastShadow = MmdConfigSwitch.ForceFalse
			});	
			
			mmdGameObject.Playing = true;

			var mmdCamera = MmdCameraObject.CreateGameObject("MmdCameraObject").GetComponent<MmdCameraObject>();
			mmdCamera.LoadCameraMotion(CameraPath);
			mmdCamera.Playing = true;

		}

		
	}
}

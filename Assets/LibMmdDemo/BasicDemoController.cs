using LibMMD.Unity3D;
using UnityEngine;

namespace LibMmdDemo
{
	public class BasicDemoController : MonoBehaviour
	{
		public string ModelPath;

		public string MotionPath;
		
		protected void Start ()
		{
			if (string.IsNullOrEmpty(ModelPath) || string.IsNullOrEmpty(MotionPath))
			{
				Debug.LogError("please fill your model and motion file path to ");
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

		}

		
	}
}

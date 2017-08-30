using UnityEngine;

namespace LibMMDTest
{
	public class FpsDisplay : MonoBehaviour
	{
		float deltaTime = 0.0f;
	
		void Update()
		{
			deltaTime += (Time.deltaTime - deltaTime) * 0.1f;
		}
	
		void OnGUI()
		{
			int w = Screen.width;
		
			GUIStyle style = new GUIStyle();
		
			var fontSize = w * 5 / 100;
			Rect rect = new Rect (0, 0, w, fontSize);
			style.alignment = TextAnchor.UpperLeft;
			style.fontSize = fontSize;
			style.normal.textColor = new Color (0.0f, 0.0f, 0.5f, 1.0f);
			float msec = deltaTime * 1000.0f;
			float fps = 1.0f / deltaTime;
			string text = string.Format("{0:0.0} ms ({1:0.} fps)", msec, fps);
			GUI.Label(rect, text, style);
		}
	}
}
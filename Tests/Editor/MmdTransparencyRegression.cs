using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class MmdTransparencyRegression
{
    static MmdTransparencyRegression() { EditorApplication.update += Tick; }
    static void Tick()
    {
        if (!File.Exists("Logs/MmdTransparency.request") || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete("Logs/MmdTransparency.request");
        try { Run(); } catch (Exception e) { File.WriteAllText("Logs/MmdTransparency.result", e.ToString()); }
    }
    static void Run()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var mesh = new Mesh();
        var texture = new Texture2D(2, 1, TextureFormat.RGBA32, false);
        var target = new RenderTexture(64, 64, 24, RenderTextureFormat.ARGB32);
        var image = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        var material = new Material(Shader.Find("MMD/Transparent/PMDMaterial-NoCastShadow"));
        var previous = RenderTexture.active;
        var async = ShaderUtil.allowAsyncCompilation;
        ShaderUtil.allowAsyncCompilation = false;
        try
        {
            mesh.vertices = new[] {new Vector3(-1,-1,0),new Vector3(-1,1,0),new Vector3(1,1,0),new Vector3(1,-1,0),new Vector3(-1,-1,1),new Vector3(-1,1,1),new Vector3(1,1,1),new Vector3(1,-1,1)};
            mesh.normals = new[] {Vector3.back,Vector3.back,Vector3.back,Vector3.back,Vector3.forward,Vector3.forward,Vector3.forward,Vector3.forward};
            mesh.uv = new[] {new Vector2(.25f,.5f),new Vector2(.25f,.5f),new Vector2(.25f,.5f),new Vector2(.25f,.5f),new Vector2(.75f,.5f),new Vector2(.75f,.5f),new Vector2(.75f,.5f),new Vector2(.75f,.5f)};
            mesh.triangles = new[] {0,1,2,0,2,3,4,6,5,4,7,6};
            texture.SetPixels(new[] {Color.red, Color.blue}); texture.Apply(); texture.filterMode = FilterMode.Point;
            var obj = new GameObject("Layers", typeof(MeshFilter), typeof(MeshRenderer)); SceneManager.MoveGameObjectToScene(obj, scene);
            obj.GetComponent<MeshFilter>().sharedMesh = mesh; obj.GetComponent<MeshRenderer>().sharedMaterial = material;
            var cameraObj = new GameObject("Camera", typeof(Camera)); SceneManager.MoveGameObjectToScene(cameraObj, scene);
            var camera = cameraObj.GetComponent<Camera>(); camera.transform.position = new Vector3(0,0,-5); camera.transform.LookAt(Vector3.zero);
            camera.orthographic = true; camera.orthographicSize = 1.5f; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear; camera.targetTexture = target; camera.scene = scene;
            var lightObj = new GameObject("Light", typeof(Light)); SceneManager.MoveGameObjectToScene(lightObj, scene); lightObj.GetComponent<Light>().type = LightType.Directional;
            var report = "";
            foreach (var suffix in new[] {"", "-NoCastShadow"})
            foreach (var outline in new[] {"", "-with-Outline"})
            {
                material.shader = Shader.Find("MMD/Transparent/PMDMaterial" + outline + suffix);
                material.mainTexture = texture; material.SetFloat("_Opacity", .5f); material.SetColor("_Color", Color.white); material.SetColor("_AmbColor", Color.white); material.SetColor("_SpecularColor", Color.black);
                if (material.HasProperty("_OutlineWidth")) material.SetFloat("_OutlineWidth", 0);
                camera.Render(); RenderTexture.active = target; image.ReadPixels(new Rect(0,0,64,64),0,0); image.Apply();
                var pixel = image.GetPixel(32,32); report += material.shader.name + " " + pixel + "\n";
                // An occluded back-face outline must not darken the front surface.
                if (Mathf.Abs(pixel.a - .75f) > .02f ||
                    Mathf.Abs(pixel.r - .5f) > .02f || Mathf.Abs(pixel.b - .25f) > .02f)
                    throw new Exception(report);
            }
            var controllerObj = new GameObject("Material config test"); SceneManager.MoveGameObjectToScene(controllerObj, scene);
            var controller = controllerObj.AddComponent<LibMMD.Unity3D.MmdGameObject>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var source = new LibMMD.Material.MmdMaterial { DiffuseColor = new Color(1,1,1,.5f), EdgeColor = Color.white };
            typeof(LibMMD.Unity3D.MmdGameObject).GetField("_model", flags).SetValue(controller,
                new LibMMD.Model.MmdModel { Parts = new[] {new LibMMD.Model.Part {Material=source},new LibMMD.Model.Part {Material=source}} });
            typeof(LibMMD.Unity3D.MmdGameObject).GetField("_materialLoader", flags).SetValue(controller,
                new LibMMD.Unity3D.MaterialLoader(new LibMMD.Unity3D.TextureLoader("",1024)));
            var config = new LibMMD.Unity3D.MmdUnityConfig();
            var materials = controller.LoadMaterials(config);
            try
            {
                var before = materials[0].shader;
                config.EnableEdge = LibMMD.Unity3D.MmdConfigSwitch.ForceTrue;
                controller.UpdateMaterialsConfig(materials, config);
                if (materials[0].shader == before || materials[1].renderQueue <= materials[0].renderQueue) throw new Exception("Shader switch lost material order");
                config.EnableEdge = LibMMD.Unity3D.MmdConfigSwitch.ForceFalse;
                controller.UpdateMaterialsConfig(materials, config);
                if (materials[1].renderQueue <= materials[0].renderQueue) throw new Exception("Reverse switch lost material order");
                report += "PASS: public material load and outline toggles preserve material order\n";
            }
            finally { foreach (var m in materials) UnityEngine.Object.DestroyImmediate(m); }
            File.WriteAllText("Logs/MmdTransparency.result", report + "PASS\n");
        }
        finally
        {
            RenderTexture.active = previous; ShaderUtil.allowAsyncCompilation = async;
            EditorSceneManager.ClosePreviewScene(scene);
            UnityEngine.Object.DestroyImmediate(mesh); UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(material);
        }
    }
}

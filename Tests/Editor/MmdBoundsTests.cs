using System;
using System.Collections;
using System.IO;
using System.Reflection;
using LibMMD.Unity3D;
using UnityEditor;
using UnityEngine;

// Run in an isolated project: -executeMethod MmdBoundsTests.Run
[InitializeOnLoad]
public static class MmdBoundsTests
{
    const string Pending = "MmdBoundsTests.Pending";
    static IEnumerator running;
    static MmdBoundsTests() { EditorApplication.update += Tick; }
    public static void Run() { SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true; }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || Time.frameCount < 2) return;
        if (running == null)
        {
            if (!SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false);
            running = Verify();
        }
        try
        {
            if (running.MoveNext()) return;
            running = null;
            File.WriteAllText("Logs/MmdBounds.result", "PASS: main/part renderers, moved bones, offscreen return, scaled/rotated root and vertex deformation");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            (running as IDisposable)?.Dispose(); running = null;
            File.WriteAllText("Logs/MmdBounds.result", e.ToString());
            Debug.LogException(e); EditorApplication.Exit(1);
        }
    }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static IEnumerator Verify()
    {
        var root = MmdGameObject.CreateGameObject("Bounds test");
        var controller = root.GetComponent<MmdGameObject>(); controller.enabled = false;
        var skeleton = new GameObject("Skeleton"); skeleton.transform.SetParent(root.transform, false);
        var bone = new GameObject("Bone"); bone.transform.SetParent(skeleton.transform, false);
        var part = new GameObject("Part", typeof(SkinnedMeshRenderer)); part.transform.SetParent(root.transform, false);
        var cameraObject = new GameObject("Bounds camera", typeof(Camera));
        var camera = cameraObject.GetComponent<Camera>();
        camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 1;
        camera.cullingMask = 1 << 30; root.layer = part.layer = 30;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
        var target = new RenderTexture(32, 32, 24); camera.targetTexture = target;
        var pixels = new Texture2D(32, 32, TextureFormat.RGB24, false);
        var material = new Material(Shader.Find("Unlit/Color")); material.color = Color.red;
        var mesh = new Mesh();
        var vertices = new[] {new Vector3(-1,-1,0), new Vector3(-1,1,0), new Vector3(1,1,0), new Vector3(1,-1,0)};
        mesh.vertices = vertices; mesh.triangles = new[] {0,1,2,0,2,3};
        var weights = new BoneWeight[4];
        for (var i = 0; i < weights.Length; i++) weights[i] = new BoneWeight {boneIndex0 = 0, weight0 = 1};
        mesh.boneWeights = weights; mesh.RecalculateBounds();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(MmdGameObject).GetField("_bones", flags).SetValue(controller, new[] {bone});
        typeof(MmdGameObject).GetField("_boneRootGameObject", flags).SetValue(controller, skeleton);
        var bind = typeof(MmdGameObject).GetMethod("BuildBindpose", flags);
        var renderers = new[] {root.GetComponent<SkinnedMeshRenderer>(), part.GetComponent<SkinnedMeshRenderer>()};
        var previous = RenderTexture.active;
        try
        {
            foreach (var renderer in renderers)
            {
                bind.Invoke(controller, new object[] {mesh, renderer, false});
                renderer.sharedMaterial = material;
                Check(renderer.rootBone == skeleton.transform && renderer.updateWhenOffscreen, "Bounds setup missing");
                renderer.enabled = false;
            }
            root.transform.SetPositionAndRotation(new Vector3(4,2,0), Quaternion.Euler(0,25,0));
            root.transform.localScale = Vector3.one * .2f;
            foreach (var renderer in renderers)
            {
                renderer.enabled = true;
                foreach (var offset in new[] {0f, 30f, -30f, 0f})
                {
                    bone.transform.localPosition = new Vector3(offset,0,0);
                    var center = bone.transform.position;
                    camera.transform.position = center + Vector3.back * 5;
                    yield return null;
                    camera.Render();
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0,0,32,32),0,0); pixels.Apply();
                    Check(pixels.GetPixel(16,16).r > .8f, renderer.name + " disappeared at bone offset " + offset);
                    foreach (var vertex in vertices)
                    {
                        var world = bone.transform.TransformPoint(vertex);
                        Check(renderer.bounds.SqrDistance(world) < .0001f,
                            renderer.name + " bounds exclude vertex at " + world + "; bounds=" + renderer.bounds + "; offset=" + offset);
                    }
                    // Render a frame while the character is outside the view.
                    camera.transform.position = center + Vector3.right * 100 + Vector3.back * 5;
                    camera.Render();
                    yield return null;
                }
                renderer.enabled = false;
            }
            // Vertex morphs must be included as well as bone motion.
            for (var i = 0; i < vertices.Length; i++) vertices[i] += Vector3.up * 20;
            mesh.vertices = vertices;
            renderers[0].enabled = true;
            camera.transform.position = root.transform.TransformPoint(Vector3.up * 20) + Vector3.back * 5;
            yield return null;
            camera.Render(); RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0,0,32,32),0,0); pixels.Apply();
            Check(pixels.GetPixel(16,16).r > .8f, "Morphed mesh disappeared");
        }
        finally
        {
            RenderTexture.active = previous;
            UnityEngine.Object.Destroy(root); UnityEngine.Object.Destroy(cameraObject);
            UnityEngine.Object.Destroy(mesh);
            UnityEngine.Object.Destroy(material); UnityEngine.Object.Destroy(pixels); UnityEngine.Object.Destroy(target);
        }
    }
}

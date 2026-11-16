using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Batch entry point: -executeMethod PortalVideoRecorder.Run (without -quit).
[InitializeOnLoad]
public static class PortalVideoRecorder
{
    const string Pending = "PortalVideoRecorder.Pending";
    const int Fps = 30;
    const int Frames = 450;
    static readonly string Output = "../.utmp/portal-reference/video-frames";
    static RenderTexture target;
    static Texture2D pixels;
    static int recorded;
    static int previousFrame = -1;
    static int warmup;
    static bool failed;
    static double started;
    static Vector3 focus;

    static PortalVideoRecorder() { if (SessionState.GetBool(Pending, false)) Attach(); }

    [MenuItem("Tools/Portal/Record assignment video frames")]
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/New Scene.unity");
        Directory.CreateDirectory(Output);
        SessionState.SetBool(Pending, true);
        Attach();
        EditorApplication.isPlaying = true;
    }

    static void Attach()
    {
        started = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Application.logMessageReceived -= OnLog;
        Application.logMessageReceived += OnLog;
    }

    static void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) failed = true;
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Pending, false)) return;
        if (EditorApplication.timeSinceStartup - started > 600) { Finish(1, "Timed out"); return; }
        if (!EditorApplication.isPlaying || Time.frameCount == previousFrame) return;
        previousFrame = Time.frameCount;
        Time.captureFramerate = Fps;
        if (++warmup < 90) return;
        try
        {
            var camera = Camera.main;
            if (target == null)
            {
                target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGBHalf);
                pixels = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                var portal = UnityEngine.Object.FindFirstObjectByType<PortalReveal>();
                portal.Replay();
                focus = portal.transform.position + new Vector3(0, -0.09f, -0.40f);
            }
            float t = recorded / (float)(Frames - 1);
            // A restrained arc shows the pull direction and the depth of the portal mouth.
            float angle = Mathf.Lerp(8, 24, Mathf.SmoothStep(0, 1, t)) * Mathf.Deg2Rad;
            camera.transform.position = focus + new Vector3(Mathf.Sin(angle) * 2.8f, 0.32f, -Mathf.Cos(angle) * 2.8f);
            camera.transform.LookAt(focus);
            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(Path.Combine(Output, $"frame-{recorded:D4}.jpg"), pixels.EncodeToJPG(95));
            camera.targetTexture = oldTarget;
            RenderTexture.active = oldActive;
            if (++recorded >= Frames) Finish(failed ? 1 : 0, $"{recorded} frames, 1920x1080, {Fps} fps; runtime errors={failed}");
        }
        catch (Exception e) { Finish(1, e.ToString()); }
    }

    static void Finish(int code, string message)
    {
        SessionState.SetBool(Pending, false);
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        Time.captureFramerate = 0;
        if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
        if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
        File.WriteAllText("../.utmp/portal-reference/video-validation.txt", (code == 0 ? "PASS\n" : "FAIL\n") + message);
        Debug.Log("PORTAL_VIDEO: " + message);
        if (Application.isBatchMode) EditorApplication.Exit(code);
        else EditorApplication.isPlaying = false;
    }
}

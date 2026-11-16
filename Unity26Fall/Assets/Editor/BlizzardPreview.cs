using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

[InitializeOnLoad]
public static class BlizzardPreview
{
    const string Pending = "BlizzardPreview.Pending";
    const string Output = "../.utmp/blizzard/frames";
    static RenderTexture target;
    static Texture2D pixels;
    static int frame;
    static int lastFrame = -1;
    static int warmup;
    static bool failed;
    static double started;

    static BlizzardPreview() { if (SessionState.GetBool(Pending, false)) Attach(); }
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Blizzard.unity");
        Directory.CreateDirectory(Output);
        SessionState.SetBool(Pending, true);
        Attach();
        EditorApplication.isPlaying = true;
    }
    static void Attach()
    {
        started = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
    }
    static void Log(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) failed = true;
    }
    static void Tick()
    {
        if (EditorApplication.timeSinceStartup - started > 500) { Finish(1, "Timed out"); return; }
        if (!EditorApplication.isPlaying || lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        Time.captureFramerate = 30;
        if (++warmup < 90) return;
        try
        {
            if (target == null)
            {
                target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                pixels = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            }
            var camera = Camera.main;
            var previous = camera.targetTexture;
            var active = RenderTexture.active;
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); pixels.Apply();
            File.WriteAllBytes(Path.Combine(Output, $"frame-{frame:D4}.jpg"), pixels.EncodeToJPG(93));
            camera.targetTexture = previous; RenderTexture.active = active;
            if (++frame < 300) return;
            var root = GameObject.Find("Blizzard - Snow and Wind Mist");
            var systems = root.GetComponentsInChildren<ParticleSystem>();
            var renderers = root.GetComponentsInChildren<ParticleSystemRenderer>();
            var defaultMaterial = GraphicsSettings.currentRenderPipeline.defaultParticleMaterial;
            if (renderers.Length != 2 || renderers.Any(r => r.sharedMaterial != defaultMaterial)) throw new Exception("Expected two renderers sharing the default particle material.");
            if (systems.Any(ps => ps.particleCount == 0)) throw new Exception("Empty blizzard particle system.");
            var snow = systems.First(ps => ps.name.StartsWith("Snow"));
            var particles = new ParticleSystem.Particle[snow.main.maxParticles];
            int count = snow.GetParticles(particles);
            int correct = particles.Take(count).Count(p => p.totalVelocity.x > 0 && p.totalVelocity.y < 0);
            if (correct < count * 0.9f) throw new Exception("Snow direction is inconsistent.");
            Finish(failed ? 1 : 0, $"300 frames; snow={count}, mist={systems.First(ps => ps != snow).particleCount}; downwind snow={correct}/{count}; particle renderers=2; shared default material; runtime errors={failed}");
        }
        catch (Exception e) { Finish(1, e.ToString()); }
    }
    static void Finish(int code, string message)
    {
        SessionState.SetBool(Pending, false); EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        Time.captureFramerate = 0;
        if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
        if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
        File.WriteAllText("../.utmp/blizzard/validation.txt", (code == 0 ? "PASS\n" : "FAIL\n") + message);
        Debug.Log("BLIZZARD_PREVIEW: " + message); EditorApplication.Exit(code);
    }
}

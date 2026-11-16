using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Batch entry point: -executeMethod PortalPlayModeCheck.Run (without -quit).
// SessionState survives the domain reload when entering Play Mode.
[InitializeOnLoad]
public static class PortalPlayModeCheck
{
    const string Pending = "PortalPlayModeCheck.Pending";
    const string FirstFrame = "PortalPlayModeCheck.FirstFrame";
    static bool failed;
    static double started;

    static PortalPlayModeCheck()
    {
        if (SessionState.GetBool(Pending, false)) Attach();
    }

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/New Scene.unity");
        SessionState.SetBool(Pending, true);
        SessionState.SetBool(FirstFrame, false);
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
        if (EditorApplication.timeSinceStartup - started > 90) { Finish(1, "Play Mode timed out"); return; }
        if (!EditorApplication.isPlaying || Time.time < 2) return;
        try
        {
            if (!SessionState.GetBool(FirstFrame, false))
            {
                PortalWorkshop.CaptureTo("../.utmp/portal-reference/play-2s.png");
                SessionState.SetBool(FirstFrame, true);
            }
            if (Time.time < 5) return;
            PortalWorkshop.CaptureTo("../.utmp/portal-reference/play-5s.png");
            var leaves = GameObject.Find("FX_MOD_Portal/PS_Leaves").GetComponent<ParticleSystem>();
            var diagnostics = new System.Text.StringBuilder();
            var particles = new ParticleSystem.Particle[leaves.main.maxParticles];
            int particleCount = leaves.GetParticles(particles);
            for(int i=0;i<Mathf.Min(10,particleCount);i++)
            {
                var p = particles[i];
                diagnostics.AppendLine($"Petal: world={leaves.transform.TransformPoint(p.position)}, size={p.GetCurrentSize(leaves)}, color={p.GetCurrentColor(leaves)}, screen={Camera.main.WorldToScreenPoint(leaves.transform.TransformPoint(p.position))}");
            }
            var mesh = new Mesh();
            leaves.GetComponent<ParticleSystemRenderer>().BakeMesh(mesh, Camera.main, ParticleSystemBakeMeshOptions.BakeRotationAndScale);
            for(int i=0;i<Mathf.Min(20,mesh.vertexCount);i++) diagnostics.AppendLine($"baked uv={mesh.uv[i]}, vertex={mesh.vertices[i]}");
            UnityEngine.Object.DestroyImmediate(mesh);
            File.WriteAllText("../.utmp/portal-reference/petal-inspection.txt", diagnostics.ToString());
            if (leaves.particleCount == 0) throw new InvalidOperationException("Leaves did not emit in Play Mode.");
            var a = File.ReadAllBytes("../.utmp/portal-reference/play-2s.png");
            var b = File.ReadAllBytes("../.utmp/portal-reference/play-5s.png");
            bool changed = a.Length != b.Length;
            if (!changed) for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) { changed = true; break; }
            if (!changed) throw new InvalidOperationException("The animated portal rendered identical frames.");
            Finish(failed ? 1 : 0, $"Animated frames differ; live leaves={leaves.particleCount}; runtime errors={failed}");
        }
        catch (Exception e) { Finish(1, e.ToString()); }
    }

    static void Finish(int code, string message)
    {
        SessionState.SetBool(Pending, false);
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        File.WriteAllText("../.utmp/portal-reference/play-validation.txt", (code == 0 ? "PASS\n" : "FAIL\n") + message);
        Debug.Log("PORTAL_PLAY_CHECK: " + message);
        EditorApplication.Exit(code);
    }
}

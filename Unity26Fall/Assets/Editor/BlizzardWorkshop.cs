using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class BlizzardWorkshop
{
    [MenuItem("Tools/Blizzard/Create simple blizzard scene")]
    public static void Create()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/New Scene.unity");
        foreach (var root in scene.GetRootGameObjects())
            if (root.name == "FX_MOD_Portal" || root.name == "Portal Post Processing") UnityEngine.Object.DestroyImmediate(root);
        // Keep the environment, but exclude unrelated butterflies and other effects.
        foreach (var ps in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.playOnAwake = false;
            var emission = ps.emission; emission.enabled = false;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer != null) renderer.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(ps);
            if (renderer != null) PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }
        var camera = Camera.main;
        camera.transform.position = new Vector3(-20, 10, -80);
        camera.transform.LookAt(new Vector3(-8, 14, -38));
        camera.fieldOfView = 60;
        camera.nearClipPlane = 0.15f;
        camera.farClipPlane = 180;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.48f, 0.57f, 0.65f);
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        RenderSettings.skybox = null;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.023f;
        RenderSettings.fogColor = camera.backgroundColor;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.45f, 0.52f, 0.62f);
        foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            light.color = new Color(0.73f, 0.83f, 1);
            light.intensity = light.type == LightType.Directional ? 0.8f : 0;
        }

        var material = GraphicsSettings.currentRenderPipeline.defaultParticleMaterial;
        var blizzard = new GameObject("Blizzard - Snow and Wind Mist");
        blizzard.transform.position = camera.transform.position + camera.transform.forward * 10;
        var snow = NewSystem("Snow - Default Material", blizzard.transform, material, 2800, 3.2f, 800, 1701);
        snow.transform.localPosition = new Vector3(-11, 3, 0);
        var sm = snow.main;
        sm.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.065f);
        sm.startColor = new ParticleSystem.MinMaxGradient(new Color(1, 1, 1, 0.7f), Color.white);
        var ss = snow.shape;
        ss.scale = new Vector3(14, 14, 22);
        var sv = snow.velocityOverLifetime;
        sv.enabled = true; sv.space = ParticleSystemSimulationSpace.World;
        sv.x = new ParticleSystem.MinMaxCurve(7, 10);
        sv.y = new ParticleSystem.MinMaxCurve(-4, -2.5f);
        sv.z = new ParticleSystem.MinMaxCurve(1, 2);
        var sn = snow.noise;
        sn.enabled = true; sn.quality = ParticleSystemNoiseQuality.Low;
        sn.strength = 0.65f; sn.frequency = 0.22f; sn.scrollSpeed = 0.3f;
        sn.octaveCount = 1;
        var sr = snow.GetComponent<ParticleSystemRenderer>();
        sr.renderMode = ParticleSystemRenderMode.Stretch;
        sr.velocityScale = 0.012f; sr.lengthScale = 1.3f;
        sr.maxParticleSize = 0.025f;
        Fade(snow, 0.1f, 0.8f);

        var mist = NewSystem("Mist - Wind Gusts", blizzard.transform, material, 24, 7, 3, 1702);
        mist.transform.localPosition = new Vector3(-10, -1, 7);
        var mm = mist.main;
        mm.startSize = new ParticleSystem.MinMaxCurve(7, 13);
        mm.startColor = new ParticleSystem.MinMaxGradient(new Color(1, 1, 1, 0.03f), new Color(1, 1, 1, 0.07f));
        mm.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
        var ms = mist.shape; ms.scale = new Vector3(20, 7, 18);
        var mv = mist.velocityOverLifetime;
        mv.enabled = true; mv.space = ParticleSystemSimulationSpace.World;
        mv.x = new ParticleSystem.MinMaxCurve(3.5f, 5);
        mv.y = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
        mv.z = new ParticleSystem.MinMaxCurve(0.5f, 1);
        var mn = mist.noise;
        mn.enabled = true; mn.quality = ParticleSystemNoiseQuality.Low;
        mn.strength = 0.35f; mn.frequency = 0.09f; mn.scrollSpeed = 0.12f;
        var mr = mist.GetComponent<ParticleSystemRenderer>();
        mr.renderMode = ParticleSystemRenderMode.Billboard;
        mr.maxParticleSize = 0.6f;
        Fade(mist, 0.2f, 0.65f);
        PrefabUtility.SaveAsPrefabAsset(blizzard, "Assets/TutorialInfo/FX/Prefabs/FX_Blizzard.prefab");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Blizzard.unity");
        AssetDatabase.SaveAssets();
        Debug.Log("BLIZZARD_CREATED: two particle renderers, shared URP default particle material; 2800 snow / 24 mist cap.");
    }

    static ParticleSystem NewSystem(string name, Transform parent, Material material, int max, float lifetime, float rate, uint seed)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 8; main.loop = true; main.prewarm = true; main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.75f, lifetime);
        main.startSpeed = 0; main.maxParticles = max;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.cullingMode = ParticleSystemCullingMode.Automatic;
        ps.useAutoRandomSeed = false; ps.randomSeed = seed;
        var emission = ps.emission; emission.rateOverTime = rate;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
        r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return ps;
    }

    static void Fade(ParticleSystem ps, float peak, float fade)
    {
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, peak), new GradientAlphaKey(1, fade), new GradientAlphaKey(0, 1) });
        var color = ps.colorOverLifetime;
        color.enabled = true; color.color = gradient;
    }

    public static void Inspect()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/New Scene.unity");
        var report = new StringBuilder();
        foreach (var root in scene.GetRootGameObjects())
        {
            report.AppendLine("ROOT: " + root.name + " at " + root.transform.position);
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>().OrderByDescending(r => r.bounds.size.sqrMagnitude).Take(25))
                report.AppendLine(r.name + " bounds=" + r.bounds);
        }
        var material = GraphicsSettings.currentRenderPipeline.defaultParticleMaterial;
        report.AppendLine("Default particle material=" + material + "; path=" + AssetDatabase.GetAssetPath(material));
        Directory.CreateDirectory("../.utmp/blizzard");
        File.WriteAllText("../.utmp/blizzard/environment.txt", report.ToString());
    }
}

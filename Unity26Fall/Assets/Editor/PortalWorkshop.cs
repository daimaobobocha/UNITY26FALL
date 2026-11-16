using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class PortalWorkshop
{
    const string ScenePath = "Assets/Scenes/New Scene.unity";
    const string Fx = "Assets/TutorialInfo/FX/";

    [MenuItem("Tools/Portal/Complete tutorial scene")]
    public static void Complete()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var portal = scene.GetRootGameObjects().Single(g => g.name == "FX_MOD_Portal");
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(Fx + "Materials/FX_SHDR_PortalSwirls.shadergraph");
        if (shader == null || ShaderUtil.ShaderHasError(shader))
            throw new InvalidOperationException("The portal Shader Graph must compile before updating the scene.");

        var primary = ConfigureSwirlMaterial(Fx + "Materials/FX_SHDR_PortalSwirls.mat", shader, false);
        var secondary = ConfigureSwirlMaterial(Fx + "Materials/FX_MAT_PortalSwirl2.mat", shader, true);
        var swirl = portal.transform.Find("FX_MOD_PortalSwirl");
        var outer = portal.transform.Find("FX_MOD_PortalSwirl (1)");
        swirl.GetComponent<MeshRenderer>().sharedMaterial = primary;
        outer.gameObject.SetActive(false);
        outer.GetComponent<MeshRenderer>().sharedMaterial = secondary;
        outer.localPosition = swirl.localPosition + new Vector3(0, 0, -0.025f);
        outer.localRotation = Quaternion.Euler(0, 0, 68);
        outer.localScale = new Vector3(1.07f, 1.07f, 1.15f);
        foreach (var renderer in new[] { swirl.GetComponent<MeshRenderer>(), outer.GetComponent<MeshRenderer>() })
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // This was an empty particle system accidentally added to the imported model root.
        var emptySystem = portal.GetComponent<ParticleSystem>();
        if (emptySystem != null) UnityEngine.Object.DestroyImmediate(emptySystem);
        var emptyRenderer = portal.GetComponent<ParticleSystemRenderer>();
        if (emptyRenderer != null) UnityEngine.Object.DestroyImmediate(emptyRenderer);

        ConfigureLeaves(portal.transform);
        var camera = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>()).Single(c => c.CompareTag("MainCamera"));
        var focus = portal.transform.position + new Vector3(0, -0.09f, -0.40f);
        camera.transform.position = focus + new Vector3(0.95f, 0.42f, -2.65f);
        camera.transform.LookAt(focus);
        camera.fieldOfView = 42;
        camera.nearClipPlane = 0.03f;
        camera.farClipPlane = 400;
        camera.allowHDR = true;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;

        var volumeObject = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Portal Post Processing") ?? new GameObject("Portal Post Processing");
        var volume = volumeObject.GetComponent<Volume>();
        if (volume == null) volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        const string profilePath = "Assets/Settings/PortalVolumeProfile.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if (profile == null) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, profilePath); }
        if (!profile.TryGet<Bloom>(out var bloom))
        {
            bloom = profile.Add<Bloom>(true);
            AssetDatabase.AddObjectToAsset(bloom, profile);
        }
        bloom.threshold.Override(1);
        bloom.intensity.Override(0.25f);
        bloom.scatter.Override(0.65f);
        volume.sharedProfile = profile;
        EditorUtility.SetDirty(profile);
        EditorUtility.SetDirty(bloom);

        foreach (var renderer in portal.GetComponentsInChildren<Renderer>(true))
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        foreach (var t in portal.GetComponentsInChildren<Transform>(true))
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
            PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        PrefabUtility.SaveAsPrefabAsset(portal, Fx + "Prefabs/FX_Portal.prefab");
        // Keep the existing sample scene available, while making Play/build start in the completed work.
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }
            .Concat(EditorBuildSettings.scenes.Where(s => s.path != ScenePath)).ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("PORTAL_COMPLETION_SUCCESS");
        Validate();
        Capture();
    }

    static Material ConfigureSwirlMaterial(string path, Shader shader, bool secondary)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
        material.shader = shader;
        material.SetTexture("_MainTexture", AssetDatabase.LoadAssetAtPath<Texture2D>(Fx + "Textures/FX_TEX_Caustics.png"));
        material.SetTexture("_SecondaryTexture", AssetDatabase.LoadAssetAtPath<Texture2D>(Fx + "Textures/FX_TEX_MotionBlur.png"));
        material.SetColor("_MainColor", new Color(0, 0.8f, 0.9f, 1));
        material.SetColor("_SecondaryColor", Color.white);
        material.SetVector("_MainTiling", new Vector4(1, 1, 0, 0));
        material.SetVector("_SecondaryTiling", new Vector4(3, 0.5f, 0, 0));
        material.SetVector("_MainSpeed", new Vector4(0.05f, secondary ? -0.10f : 0.12f, 0, 0));
        material.SetVector("_SecondarySpeed", new Vector4(0.01f, secondary ? -0.3f : 0.3f, 0, 0));
        material.SetFloat("_MainEmission", secondary ? 2 : 3);
        material.SetFloat("_SecondaryEmission", secondary ? 3 : 5);
        material.SetColor("_MainEmissionColor", new Color(0.55f, 0.68f, 1, 1));
        material.SetColor("_SecondaryEmissionColor", new Color(0.65f, 0.55f, 1, 1));
        EditorUtility.SetDirty(material);
        return material;
    }

    static void ConfigureLeaves(Transform portal)
    {
        var leafTransform = portal.Find("PS_Leaves");
        var go = leafTransform == null ? new GameObject("PS_Leaves") : leafTransform.gameObject;
        go.transform.SetParent(portal, false);
        go.transform.localPosition = new Vector3(0, 0, -0.13f);
        go.transform.localRotation = Quaternion.Euler(0, 180, 180);
        go.transform.localScale = new Vector3(1, 1, 0.5f);
        var ps = go.GetComponent<ParticleSystem>();
        if (ps == null) ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 5; main.loop = true; main.prewarm = true; main.playOnAwake = true;
        main.startLifetime = 1.2f; main.startSpeed = -0.9f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.035f);
        main.startColor = Color.white;
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.maxParticles = 150;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        ps.useAutoRandomSeed = false; ps.randomSeed = 241113;
        var emission = ps.emission; emission.rateOverTime = 45;
        var shape = ps.shape;
        shape.enabled = true; shape.shapeType = ParticleSystemShapeType.ConeVolume;
        shape.angle = 21.86f; shape.radius = 0.49f; shape.radiusThickness = 0.2f; shape.length = 1.11f; shape.arc = 180;
        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.orbitalZ = 0.35f; velocity.radial = -0.12f;
        var size = ps.sizeOverLifetime;
        size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, 0, 1, 1));
        var rotation = ps.rotationOverLifetime;
        rotation.enabled = true; rotation.separateAxes = true;
        rotation.x = new ParticleSystem.MinMaxCurve(-100 * Mathf.Deg2Rad, 500 * Mathf.Deg2Rad);
        rotation.y = new ParticleSystem.MinMaxCurve(-100 * Mathf.Deg2Rad, 500 * Mathf.Deg2Rad);
        rotation.z = new ParticleSystem.MinMaxCurve(-100 * Mathf.Deg2Rad, 500 * Mathf.Deg2Rad);
        var sheet = ps.textureSheetAnimation;
        sheet.enabled = true; sheet.numTilesX = 2; sheet.numTilesY = 2;
        sheet.animation = ParticleSystemAnimationType.WholeSheet;
        sheet.frameOverTime = 0; sheet.startFrame = new ParticleSystem.MinMaxCurve(0, 4); sheet.cycleCount = 1;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Fx + "Materials/FX_MAT_Petals.mat");
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.Local;
        renderer.shadowCastingMode = ShadowCastingMode.Off;

        // The tutorial's test quad becomes an invisible collision plane at the portal mouth.
        var plane = portal.Find("Portal Leaf Collision");
        if (plane == null) { plane = new GameObject("Portal Leaf Collision").transform; plane.SetParent(portal, false); }
        plane.localPosition = new Vector3(0, 0, -0.11f);
        plane.localRotation = Quaternion.Euler(-90, 0, 0);
        var collision = ps.collision;
        collision.enabled = true; collision.type = ParticleSystemCollisionType.Planes;
        collision.SetPlane(0, plane); collision.lifetimeLoss = 1; collision.bounce = 0;
        collision.radiusScale = 0.1f;
    }

    [MenuItem("Tools/Portal/Validate tutorial scene")]
    public static void Validate()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var portal = scene.GetRootGameObjects().Single(g => g.name == "FX_MOD_Portal");
        var report = new StringBuilder("Portal tutorial validation\n");
        foreach (var renderer in portal.GetComponentsInChildren<Renderer>(true))
        foreach (var material in renderer.sharedMaterials)
        {
            if (material == null) throw new InvalidOperationException("Missing material: " + PathOf(renderer.transform));
            if (ShaderUtil.ShaderHasError(material.shader)) throw new InvalidOperationException("Shader error: " + material.shader.name);
        }
        foreach (var mf in portal.GetComponentsInChildren<MeshFilter>(true))
            if (mf.sharedMesh == null) throw new InvalidOperationException("Missing portal mesh: " + mf.name);
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(Fx + "Materials/FX_SHDR_PortalSwirls.shadergraph");
        report.AppendLine("Shader Graph: compiled; exposed properties=" + ShaderUtil.GetPropertyCount(shader));
        foreach (var ps in portal.GetComponentsInChildren<ParticleSystem>())
        {
            ps.Simulate(3, false, true);
            report.AppendLine(ps.name + ": live particles=" + ps.particleCount);
            if (ps.particleCount == 0) throw new InvalidOperationException("Particle system did not emit: " + ps.name);
        }
        var leaves = portal.transform.Find("PS_Leaves").GetComponent<ParticleSystem>();
        var particles = new ParticleSystem.Particle[leaves.main.maxParticles];
        int count = leaves.GetParticles(particles);
        var center = portal.transform.TransformPoint(new Vector3(0, 0, -0.11f));
        int approaching = 0;
        for (int i = 0; i < count; i++)
        {
            var position = leaves.transform.TransformPoint(particles[i].position);
            var velocity = leaves.transform.TransformVector(particles[i].velocity);
            if (Vector3.Dot(center - position, velocity) > 0) approaching++;
        }
        report.AppendLine($"Leaves moving toward portal: {approaching}/{count}; collision plane assigned={leaves.collision.GetPlane(0) != null}");
        if (count == 0 || approaching < count * 0.7f) throw new InvalidOperationException("Leaves are not moving into the portal.");
        var emission = leaves.emission;
        bool wasEmitting = emission.enabled;
        try
        {
            leaves.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            emission.enabled = false;
            var probe = new ParticleSystem.Particle
            {
                position = new Vector3(0, 0, 0.02f), velocity = new Vector3(0, 0, -0.9f),
                startLifetime = 1, remainingLifetime = 1, startSize = 0.01f, startColor = Color.white
            };
            leaves.SetParticles(new[] { probe }, 1);
            leaves.Simulate(0.2f, false, false);
            if (leaves.particleCount != 0) throw new InvalidOperationException("A leaf crossed the portal collision plane without being removed.");
            report.AppendLine("Collision probe: leaf removed at portal mouth (PASS)");
        }
        finally
        {
            emission.enabled = wasEmitting;
            leaves.Simulate(3, false, true);
        }
        report.AppendLine("PASS");
        Directory.CreateDirectory("../.utmp/portal-reference");
        File.WriteAllText("../.utmp/portal-reference/validation.txt", report.ToString());
        Debug.Log(report.ToString());
    }

    public static void Capture()
    {
        CaptureTo("../.utmp/portal-reference/portal-preview.png");
    }

    public static void CaptureTo(string path)
    {
        var camera = Camera.main;
        var target = new RenderTexture(1600, 1000, 24, RenderTextureFormat.ARGBHalf);
        var previous = camera.targetTexture;
        var active = RenderTexture.active;
        var image = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previous;
            RenderTexture.active = active;
            UnityEngine.Object.DestroyImmediate(image);
            target.Release(); UnityEngine.Object.DestroyImmediate(target);
        }
    }

    static string PathOf(Transform t) => t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;
}

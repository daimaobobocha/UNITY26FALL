using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class PortalAssignment
{
    const string Fx = "Assets/TutorialInfo/FX/";

    [MenuItem("Tools/Portal/Set forest background")]
    public static void Complete()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/New Scene.unity");
        var portal = scene.GetRootGameObjects().Single(g => g.name == "FX_MOD_Portal");
        var path = Fx + "Textures/FX_TEX_PortalForest.png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        var material = AssetDatabase.LoadAssetAtPath<Material>(Fx + "Materials/FX_MAT_Background.mat");
        material.SetTexture("_BaseMap", texture);
        material.SetTexture("_MainTex", texture);
        material.SetColor("_BaseColor", Color.white);
        material.SetTextureScale("_BaseMap", Vector2.one);
        material.SetTextureOffset("_BaseMap", Vector2.zero);
        EditorUtility.SetDirty(material);
        var background = portal.transform.Find("PS_Background").GetComponent<ParticleSystem>();
        background.transform.localRotation = Quaternion.identity;
        // URP's particle shader consumes mesh UVs directly. Crop the panorama to
        // its central square in a separate mesh, preserving the imported model.
        var sourceMesh = AssetDatabase.LoadAllAssetsAtPath(Fx + "Models/FX_MOD_HalfSphere.fbx").OfType<Mesh>().First();
        var meshPath = Fx + "Models/FX_MOD_PortalForestDome.asset";
        var dome = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (dome == null)
        {
            dome = Object.Instantiate(sourceMesh);
            dome.name = "Portal Forest Dome";
            var uv = dome.uv;
            for (int i = 0; i < uv.Length; i++) uv[i].x = 0.25f + uv[i].x * 0.5f;
            dome.uv = uv;
            AssetDatabase.CreateAsset(dome, meshPath);
        }
        background.GetComponent<ParticleSystemRenderer>().mesh = dome;
        var bgMain = background.main;
        bgMain.startRotation = 0;
        var bgRotation = background.rotationOverLifetime;
        bgRotation.enabled = false;
        var petals = AssetDatabase.LoadAssetAtPath<Material>(Fx + "Materials/FX_MAT_Petals.mat");
        petals.shader = material.shader;
        EditorUtility.SetDirty(petals);
        var leaves = portal.transform.Find("PS_Leaves").GetComponent<ParticleSystem>();
        var leafMain = leaves.main;
        leafMain.startSize = new ParticleSystem.MinMaxCurve(0.055f, 0.09f);
        var leafSize = leaves.sizeOverLifetime;
        leafSize.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, 0.6f, 1, 1));
        var trail = portal.transform.Find("PS_ParticleTrail").GetComponent<ParticleSystem>();
        var trailMain = trail.main;
        trailMain.startSize = 0.0002f;
        trail.GetComponent<ParticleSystemRenderer>().maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        var trailPath = Fx + "Materials/FX_MAT_PetalTrail.mat";
        var petalTrailMaterial = AssetDatabase.LoadAssetAtPath<Material>(trailPath);
        if (petalTrailMaterial == null)
        {
            petalTrailMaterial = new Material(material);
            petalTrailMaterial.name = "FX_MAT_PetalTrail";
            petalTrailMaterial.SetTexture("_BaseMap", null);
            petalTrailMaterial.SetTexture("_MainTex", null);
            petalTrailMaterial.SetColor("_BaseColor", new Color(1, 0.65f, 0.85f, 0.5f));
            AssetDatabase.CreateAsset(petalTrailMaterial, trailPath);
        }
        leaves.GetComponent<ParticleSystemRenderer>().trailMaterial = petalTrailMaterial;
        var petalTrails = leaves.trails;
        petalTrails.enabled = true;
        petalTrails.mode = ParticleSystemTrailMode.PerParticle;
        petalTrails.ratio = 0.65f;
        petalTrails.lifetime = 0.25f;
        petalTrails.minVertexDistance = 0.01f;
        petalTrails.sizeAffectsWidth = true;
        petalTrails.widthOverTrail = new ParticleSystem.MinMaxCurve(0.12f);
        petalTrails.dieWithParticles = true;
        var trailGradient = new Gradient();
        trailGradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(0.8f, 0), new GradientAlphaKey(0, 1) });
        petalTrails.colorOverTrail = new ParticleSystem.MinMaxGradient(trailGradient);
        var empty = portal.GetComponent<ParticleSystem>();
        if (empty != null && !empty.emission.enabled)
        {
            Object.DestroyImmediate(empty);
            Object.DestroyImmediate(portal.GetComponent<ParticleSystemRenderer>());
        }
        if (portal.GetComponent<PortalReveal>() == null) portal.AddComponent<PortalReveal>();
        var ringMain = portal.transform.Find("PS_Portal").GetComponent<ParticleSystem>().main;
        ringMain.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var outerMain = portal.transform.Find("PS_Portal/PS_OuterRing").GetComponent<ParticleSystem>().main;
        outerMain.scalingMode = ParticleSystemScalingMode.Hierarchy;
        PrefabUtility.ApplyPrefabInstance(portal, InteractionMode.AutomatedAction);
        if (!scene.GetRootGameObjects().Any(g => g.name == "Portal Post Processing"))
        {
            var volume = new GameObject("Portal Post Processing").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/PortalVolumeProfile.asset");
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        var report = new StringBuilder();
        foreach (var ps in portal.GetComponentsInChildren<ParticleSystem>())
        {
            var r = ps.GetComponent<ParticleSystemRenderer>();
            report.AppendLine($"{ps.name}: mode={r.renderMode}, mesh={r.mesh}, mask={r.maskInteraction}, pos={ps.transform.localPosition}, rotation={ps.transform.localEulerAngles}, scale={ps.transform.localScale}, size={ps.main.startSize.constant}, color={ps.main.startColor.color}");
            if (r.mesh != null && ps.name == "PS_Background")
            {
                var mesh = r.mesh;
                report.AppendLine("Mesh bounds=" + mesh.bounds);
                for (int i=0;i<mesh.vertexCount;i+=Mathf.Max(1,mesh.vertexCount/30)) report.AppendLine($"v={mesh.vertices[i]} uv={mesh.uv[i]}");
            }
            if (ps.name == "FX_MOD_Portal") report.AppendLine($"Root: emission={ps.emission.enabled}, rate={ps.emission.rateOverTime.constant}, material={r.sharedMaterial}");
        }
        AssetDatabase.SaveAssets();
        File.WriteAllText("../.utmp/portal-reference/background-inspection.txt", report.ToString());
        Debug.Log(report.ToString());
        PortalWorkshop.Validate();
    }
}

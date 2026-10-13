using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

// Run for every build, including the normal Build Profiles window.
public sealed class CroustyBuild : IPreprocessBuildWithReport, IProcessSceneWithReport
{
    public int callbackOrder => 100;

    public void OnPreprocessBuild(BuildReport report) => Validate();

    [MenuItem("Tools/Crousty/Build/Verifier les ressources du build")]
    public static void Validate()
    {
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        Require(scenes.Length > 0 && scenes[0] == "Assets/Scenes/CroustyMainMenu.unity",
            "Le menu principal doit etre la premiere scene du build.");
        Require(scenes.Contains("Assets/SceneBase.unity"), "SceneBase manque au build.");
        foreach (var scene in scenes) Require(File.Exists(scene), "Scene absente : " + scene);

        var graphics = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
        Require(graphics.FindProperty("m_FogStripping").intValue == 1 &&
            graphics.FindProperty("m_FogKeepLinear").boolValue,
            "Conserver la brume lineaire : Graphics > Fog Modes > Custom > Linear.");
        foreach (var path in new[] { "Crousty/ProceduralSurface", "Crousty/ProceduralSignal", "Crousty/Atmosphere", "Crousty/CityGlow", "Crousty/WorldMarker" })
        {
            var material = Resources.Load<Material>(path);
            Require(material != null && material.shader != null, "Materiau/shader absent : Resources/" + path);
        }
        Require(Resources.Load<CroustySoundBank>("Crousty/SoundBank") != null, "SoundBank manque au build.");
        Require(Resources.Load<GameObject>("Crousty/StopSign") != null, "StopSign manque au build.");
        foreach (var path in new[] { "portrait", "bandeau", "etiquette", "titre" })
            Require(Resources.Load<Sprite>("Gerant/" + path) != null, "Sprite du gerant absent : " + path);
        Debug.Log("Crousty build : scenes, brume et ressources dynamiques presentes.");
    }

    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (report == null) return;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) == 0,
                    "Script manquant dans " + scene.path + " : " + transform.name);
            foreach (var smoke in root.GetComponentsInChildren<SceneBaseDriftSmoke>(true))
                Require(smoke.smokeMaterial != null && smoke.smokeMaterial.shader != null,
                    "Materiau de fumee manquant dans " + scene.path);
        }
    }

    [MenuItem("Tools/Crousty/Build/Construire Windows")]
    public static void BuildWindows()
    {
        Validate();
        var output = Path.GetFullPath("Build/Crousty.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = output,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new BuildFailedException("Build Crousty : " + report.summary.result + " (" + report.summary.totalErrors + " erreurs).");
        Debug.Log("CROUSTY_BUILD_OK " + output + " bytes=" + report.summary.totalSize);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new BuildFailedException(message);
    }
}

using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SceneSmokeTest
{
    private static int _logErrorCount;

    // Entry point for: Unity -batchmode -executeMethod SceneSmokeTest.EntryPoint
    // Runs after the AssetDatabase refresh, so synchronous work is safe here.
    public static void EntryPoint()
    {
        RunSmokeTest();
    }

    private static void RunSmokeTest()
    {
        Application.logMessageReceived += OnLog;
        try
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            bool bonusTypeExists = TypeExists("ARProximityBonus");
            bool vfxTypeExists = TypeExists("ImpactVFX");
            bool swapTypeExists = TypeExists("CharacterSwapDemo");
            bool audioLoaded = Resources.Load<AudioClip>("Audio/attack_whoosh") != null;
            bool matLoaded = Resources.Load<Material>("VFX/PuffMaterial") != null;
            UnityChanSetupBuilder.BuildIfNeeded();
            bool ucPrefab = Resources.Load<GameObject>("UnityChan/UnityChanAR") != null;
            bool ucOverride = Resources.Load<RuntimeAnimatorController>("UnityChan/UnityChanOverride") != null;

            var report =
                "SMOKETEST " +
                "scene=" + (scene.IsValid() ? "OK" : "FAIL") +
                " bonusScript=" + (bonusTypeExists ? "OK" : "FAIL") +
                " vfxScript=" + (vfxTypeExists ? "OK" : "FAIL") +
                " swapScript=" + (swapTypeExists ? "OK" : "FAIL") +
                " whoosh=" + (audioLoaded ? "OK" : "FAIL") +
                " puffMat=" + (matLoaded ? "OK" : "FAIL") +
                " ucPrefab=" + (ucPrefab ? "OK" : "FAIL") +
                " ucOverride=" + (ucOverride ? "OK" : "FAIL") +
                " logErrors=" + _logErrorCount;
            File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "SmokeTestResult.txt"), report);
            Debug.Log(report);
        }
        catch (System.Exception ex)
        {
            File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "SmokeTestResult.txt"),
                "SMOKETEST EXCEPTION " + ex.Message);
        }
        finally
        {
            Application.logMessageReceived -= OnLog;
            EditorApplication.Exit(0);
        }
    }

    private static bool TypeExists(string typeName)
    {
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            if (asm.GetType(typeName, false) != null) return true;
        }
        return false;
    }

    private static void OnLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            _logErrorCount++;
    }
}

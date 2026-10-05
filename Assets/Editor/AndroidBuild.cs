using UnityEditor;
using UnityEngine;
using System.Linq;

// Headless Android build: Unity -batchmode -executeMethod AndroidBuild.BuildAndInstall
public static class AndroidBuild
{
    public static void BuildAndInstall()
    {
        string apkPath = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "imr-lab1.apk");

        var options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/SampleScene.unity" },
            locationPathName = apkPath,
            target = BuildTarget.Android,
            options = BuildOptions.Development | BuildOptions.AutoRunPlayer,
        };

        // Apply Android settings the user would otherwise set in Project Settings.
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.imrlabs.arlab");
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;

        var report = BuildPipeline.BuildPlayer(options);
        bool ok = report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
        Debug.Log("[AndroidBuild] result=" + report.summary.result +
                  " totalSize=" + report.summary.totalSize +
                  " outputPath=" + report.summary.outputPath +
                  " errors=" + report.summary.totalErrors);
        EditorApplication.Exit(ok ? 0 : 1);
    }
}

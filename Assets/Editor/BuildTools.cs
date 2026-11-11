#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Command-line and menu builds for Windows and WebGL.</summary>
public static class BuildTools
{
    [MenuItem("Dungeon Escape/Build Windows")]
    public static void BuildWindows()
    {
        string outDir = GetArg("-buildOut") ?? "Builds/Windows";
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        string only = GetArg("-onlyScene");
        if (!string.IsNullOrEmpty(only)) scenes = new[] { only };
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outDir + "/DungeonEscape.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        });
        Debug.Log("[BuildTools] Windows build: " + report.summary.result + " size=" + report.summary.totalSize / (1024 * 1024) + "MB errors=" + report.summary.totalErrors);
        if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }

    static string GetArg(string name)
    {
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
        return null;
    }
}
#endif

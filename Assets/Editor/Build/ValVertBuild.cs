using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Local player-build entry points used by the Unity CLI.</summary>
public static class ValVertBuild
{
    /// <summary>
    /// Development build for the project's Intel HD 3000-class target. Use D3D11 only:
    /// this GPU cannot create D3D12, and compiling both APIs needlessly doubles terrain variants.
    /// The editor's prior graphics API settings are restored after BuildPipeline finishes.
    /// </summary>
    public static void BuildWindowsDevD3D11()
    {
        const BuildTarget target = BuildTarget.StandaloneWindows64;
        bool oldUseDefault = PlayerSettings.GetUseDefaultGraphicsAPIs(target);
        GraphicsDeviceType[] oldApis = PlayerSettings.GetGraphicsAPIs(target);
        int exitCode = 1;

        try
        {
            string output = BuildOutputArgument();
            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
                throw new InvalidOperationException("No enabled scenes in EditorBuildSettings.");

            PlayerSettings.SetUseDefaultGraphicsAPIs(target, false);
            PlayerSettings.SetGraphicsAPIs(target, new[] { GraphicsDeviceType.Direct3D11 });

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = target,
                options = BuildOptions.Development
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[ValVertBuild] {report.summary.result}: {report.summary.totalSize} bytes -> {output}");
            exitCode = report.summary.result == BuildResult.Succeeded ? 0 : 1;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            exitCode = 1;
        }
        finally
        {
            PlayerSettings.SetUseDefaultGraphicsAPIs(target, false);
            PlayerSettings.SetGraphicsAPIs(target, oldApis);
            PlayerSettings.SetUseDefaultGraphicsAPIs(target, oldUseDefault);
            AssetDatabase.SaveAssets();
        }

        EditorApplication.Exit(exitCode);
    }

    static string BuildOutputArgument()
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "-buildOutput") return args[i + 1];
        throw new InvalidOperationException("Pass --output-path to unity build.");
    }
}

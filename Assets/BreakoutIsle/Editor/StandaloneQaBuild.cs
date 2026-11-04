using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BreakoutIsle.Editor
{
    public static class StandaloneQaBuild
    {
        private const string OutputDirectory = "Logs/BreakoutQA/Standalone";
        private const string OutputPath = OutputDirectory + "/BreakoutIsle-QA.exe";

        [MenuItem("Breakout Isle/QA/Build Non-Development Windows Probe")]
        public static void Build()
        {
            var scenes = new System.Collections.Generic.List<string>();
            foreach (var scene in EditorBuildSettings.scenes)
                if (scene.enabled)
                    scenes.Add(scene.path);

            if (scenes.Count == 0)
                throw new BuildFailedException("No enabled scenes are configured for the standalone QA build.");

            Directory.CreateDirectory(OutputDirectory);
            var options = new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = OutputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
                extraScriptingDefines = new[] { "BREAKOUT_QA" }
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException($"Standalone QA build failed: {report.summary.result}; errors={report.summary.totalErrors}.");

            Debug.Log($"Built the non-Development standalone QA probe to '{OutputPath}' ({report.summary.totalSize} bytes).");
        }
    }
}

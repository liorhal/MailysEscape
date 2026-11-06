using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Maily.EditorTools
{
    /// <summary>
    /// Builds the browser version (WebGL 2) into Builds/Web. Gzip with the loader's decompression fallback,
    /// so the folder runs from any static web host without special server headers.
    /// </summary>
    public static class WebBuild
    {
        public const string OutDir = "Builds/Web";

        [MenuItem("Maily/Build Web Version")]
        public static string Build()
        {
            PlayerSettings.companyName = "Lior Halachmi";
            PlayerSettings.productName = "Maily's Great Escape";
            PlayerSettings.WebGL.template = "PROJECT:Maily";               // Assets/WebGLTemplates/Maily: full-window page, Hebrew title
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;   // works on hosts that don't send Content-Encoding
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.nameFilesAsHashes = false;
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultWebScreenWidth = 1280; PlayerSettings.defaultWebScreenHeight = 720;
            PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.WebGL, ManagedStrippingLevel.Low);

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) scenes = new[] { GameSetup.ScenePath };
            var opts = new BuildPlayerOptions
            {
                scenes = scenes, locationPathName = OutDir, target = BuildTarget.WebGL, options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            var s = report.summary;
            string msg = $"Web build {s.result}: {s.totalSize / (1024f * 1024f):0.0} MB in {s.totalTime.TotalMinutes:0.0} min, {s.totalErrors} errors, {s.totalWarnings} warnings → {OutDir}";
            Debug.Log("[Maily] " + msg);
            if (s.result != BuildResult.Succeeded)
                foreach (var step in report.steps) foreach (var m in step.messages)
                    if (m.type == LogType.Error || m.type == LogType.Exception) msg += "\n" + m.content;
            return msg;
        }
    }
}

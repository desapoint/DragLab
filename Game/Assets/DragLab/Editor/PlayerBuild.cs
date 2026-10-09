using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DragLab.Editor
{
    public static class PlayerBuild
    {
        [MenuItem("DragLab/Build Windows Prototype")]
        public static void BuildWindows()
        {
            ProjectSetup.PrepareEngineBenchAssets();
            Verification.Run();
            SceneVerification.Run();
            BuildPlayerOnly();
        }
        public static void BuildPlayerOnly()
        {
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            string destination = "Builds/Windows/DragLab.exe";
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { "Assets/DragLab/Scenes/FirstSlice.unity", "Assets/DragLab/Scenes/EngineTest.unity", "Assets/DragLab/Scenes/DrivingTest.unity" },
                locationPathName = destination,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (result.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Player build failed: " + result.summary.result);
            Debug.Log("DRAGLAB BUILD SUCCESS: " + Path.GetFullPath(destination));
        }
    }
}

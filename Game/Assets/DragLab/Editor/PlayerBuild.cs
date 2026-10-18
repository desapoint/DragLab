using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
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
            NativeUiSetup.PrepareShell();
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
        [MenuItem("DragLab/Build Android Prototype")]
        public static void BuildAndroid()
        {
            if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android,BuildTarget.Android))throw new InvalidOperationException("Install Android Build Support, SDK/NDK and OpenJDK for this Editor.");
            NativeUiSetup.PrepareShell();
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
            EditorUserBuildSettings.buildAppBundle=false;EditorUserBuildSettings.exportAsGoogleAndroidProject=false;
            string destination="Builds/Android/DragLab.apk";Directory.CreateDirectory(Path.GetDirectoryName(destination));
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
                scenes=new[]{"Assets/DragLab/Scenes/FirstSlice.unity","Assets/DragLab/Scenes/EngineTest.unity","Assets/DragLab/Scenes/DrivingTest.unity"},
                locationPathName=destination,target=BuildTarget.Android,options=BuildOptions.Development
            });
            if(result.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Android player build failed: "+result.summary.result);
            Debug.Log("DRAGLAB ANDROID BUILD SUCCESS: "+Path.GetFullPath(destination));
        }
    }
}

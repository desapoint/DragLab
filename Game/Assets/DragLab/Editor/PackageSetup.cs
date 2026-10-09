using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace DragLab.Editor
{
    public static class PackageSetup
    {
        static AddRequest request;
        static double deadline;
        public static void InstallCapture()
        {
            request = Client.Add("com.unity.modules.screencapture@1.0.0");
            deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update += Poll;
        }
        static void Poll()
        {
            if (!request.IsCompleted && EditorApplication.timeSinceStartup < deadline) return;
            EditorApplication.update -= Poll;
            if (request.Status == StatusCode.Success) { Debug.Log("DRAGLAB PACKAGE: " + request.Result.name + "@" + request.Result.version); EditorApplication.Exit(0); }
            else { Debug.LogError(request.Error != null ? request.Error.message : "Package resolution timed out."); EditorApplication.Exit(1); }
        }
    }
}

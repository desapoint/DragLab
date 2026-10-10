using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.UIElements;

namespace DragLab.Editor
{
    // Explicit bootstrap only; package resolution needs the Editor update pump.
    public static class NativeUiSetup
    {
        static AddRequest request;
        static double started;
        public static void Install()
        {
            started = EditorApplication.timeSinceStartup;
            request = Client.Add("com.unity.modules.uielements@1.0.0");
            EditorApplication.update += Poll;
        }
        static void Poll()
        {
            if (!request.IsCompleted && EditorApplication.timeSinceStartup - started < 120) return;
            EditorApplication.update -= Poll;
            if (!request.IsCompleted || request.Status != StatusCode.Success)
            { Debug.LogError("Native UI dependency failed: " + request.Error?.message); EditorApplication.Exit(1); return; }
            Debug.Log("Native UI dependency ready: " + request.Result.name);
            EditorApplication.Exit(0);
        }
        public static void PrepareShell()
        {
            const string path = "Assets/DragLab/Resources/UI/NativePanel.asset";
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                panel.scaleMode = PanelScaleMode.ConstantPixelSize;
                panel.sortingOrder = 200;
                AssetDatabase.CreateAsset(panel, path);
            }
            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/DragLab/Resources/UI/NativeTheme.tss");
            EditorUtility.SetDirty(panel);
            AssetDatabase.SaveAssets();
            // Remove only the Editor-generated default theme if no other content lives there.
            const string generated = "Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss";
            if (AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(generated) != null &&
                AssetDatabase.FindAssets("", new[] { "Assets/UI Toolkit" }).Length == 2)
                AssetDatabase.DeleteAsset("Assets/UI Toolkit");
            Debug.Log("Native panel ready: " + path);
        }
    }
}

using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
namespace DragLab.Editor
{
    public sealed class SqlitePluginSetup : IPreprocessBuildWithReport
    {
        public int callbackOrder=>-100;
        public void OnPreprocessBuild(BuildReport report){Configure();if(report.summary.platform!=BuildTarget.StandaloneWindows64&&report.summary.platform!=BuildTarget.Android)throw new BuildFailedException("This checkpoint bundles SQLite for Windows x64 and Android ARM64. Add a native library before building another platform.");}
        public static void Configure()
        {
            foreach(string path in new[]{"Assets/DragLab/Plugins/SQLite/Windows/x86_64/draglab_sqlite3.dll","Assets/DragLab/Plugins/SQLite/Android/arm64-v8a/libdraglab_sqlite3.so"}){
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);var importer=AssetImporter.GetAtPath(path) as PluginImporter;if(importer==null)throw new BuildFailedException("SQLite native importer missing: "+path);
                bool win=path.EndsWith(".dll");importer.SetCompatibleWithAnyPlatform(false);importer.SetCompatibleWithEditor(win);if(win){importer.SetEditorData("OS","Windows");importer.SetEditorData("CPU","x86_64");}importer.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64,win);importer.SetCompatibleWithPlatform(BuildTarget.Android,!win);importer.SetPlatformData(win?BuildTarget.StandaloneWindows64:BuildTarget.Android,"CPU",win?"x86_64":"ARM64");importer.SaveAndReimport();
            }
        }
    }
}

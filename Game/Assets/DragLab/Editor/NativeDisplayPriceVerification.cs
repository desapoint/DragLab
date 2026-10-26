using System;
using System.IO;
using System.Text;
using DragLab.Runtime;
using UnityEditor;
using UnityEngine;

namespace DragLab.Editor
{
    // Independent entry point for the pure valuation component. It does not
    // activate, construct or depend on the pending native Parts browser UI.
    public static class NativeDisplayPriceVerification
    {
        public static void Run()
        {
            if (!Application.isBatchMode || EditorApplication.isPlaying)
                throw new InvalidOperationException("Use a separate batch Editor outside Play Mode.");
            var root = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../reports/save-refactor/S06H1/valuation-component"));
            Directory.CreateDirectory(root);
            Verify(root, "encoder", NativePartEncoderProbeChecks.Run);
            Verify(root, "prices", NativePartDisplayPriceChecks.Run);
        }

        static void Verify(string root, string name, Action<Action<bool,string>,string> run)
        {
            var report = new StringBuilder();
            int count = 0;
            try
            {
                run((ok, label) =>
                {
                    report.AppendLine((ok ? "PASS " : "FAIL ") + label);
                    if (!ok) throw new InvalidOperationException(label);
                    count++;
                }, Path.Combine(root, name + "-fixtures"));
                report.AppendLine("RESULT PASS count=" + count);
                Debug.Log("S06 valuation component " + name + " PASS " + count);
            }
            catch (Exception error)
            {
                report.AppendLine("RESULT FAIL count=" + count);
                report.AppendLine(error.ToString());
                throw;
            }
            finally
            {
                File.WriteAllText(Path.Combine(root, name + "-checks.txt"),
                    report.ToString(), new UTF8Encoding(false));
            }
        }
    }
}

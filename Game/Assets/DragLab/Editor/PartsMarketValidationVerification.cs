using System;
using System.IO;
using System.Text;
using DragLab.Runtime;
using UnityEditor;
using UnityEngine;

namespace DragLab.Editor
{
    // Standalone component runner; independent of pending Parts UI activation.
    public static class PartsMarketValidationVerification
    {
        public static void Run()
        {
            if (!Application.isBatchMode || EditorApplication.isPlaying)
                throw new InvalidOperationException("Use a separate batch Editor outside Play Mode.");
            string root = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../reports/save-refactor/S06H1/market-validation-component"));
            Directory.CreateDirectory(root);
            var report = new StringBuilder();
            int count = 0;
            try
            {
                PartsMarketValidationChecks.Run((ok, label) =>
                {
                    report.AppendLine((ok ? "PASS " : "FAIL ") + label);
                    if (!ok) throw new InvalidOperationException(label);
                    count++;
                }, Path.Combine(root, "fixtures-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff")));
                report.AppendLine("RESULT PASS count=" + count);
                Debug.Log("S06 market validation component PASS " + count);
            }
            catch (Exception error)
            {
                report.AppendLine("RESULT FAIL count=" + count);
                report.AppendLine(error.ToString());
                throw;
            }
            finally
            {
                File.WriteAllText(Path.Combine(root, "checks.txt"),
                    report.ToString(), new UTF8Encoding(false));
            }
        }
    }
}

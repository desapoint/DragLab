using System;
using System.IO;
using System.Text;
using DragLab.Runtime;
using UnityEditor;
using UnityEngine;

namespace DragLab.Editor
{
    // Editor-only frozen-reference characterization; never selects a game save.
    public static class CartPreviewCharacterizationVerification
    {
        public static void Run()=>RunCore(false,false);
        public static void RunMutationBoundary()=>RunCore(true,false);
        public static void RunCollectAll()=>RunCore(false,true);
        static void RunCore(bool mutationBoundary,bool collectAll)
        {
            if (!Application.isBatchMode || EditorApplication.isPlaying)
                throw new InvalidOperationException("Use a separate batch Editor outside Play Mode.");
            string root = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../reports/save-refactor/S06D2B1/" + (mutationBoundary?"boundary-":collectAll?"collect-":"run-") + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff")));
            Directory.CreateDirectory(root);
            var report = new StringBuilder("S06d2b step1 Editor-only legacy cart characterization\n");
            int count = 0, failures = 0;
            try
            {
                Action<bool,string> check=(ok, label) =>
                {
                    report.AppendLine((ok ? "PASS " : "FAIL ") + label);
                    if (!ok)
                    {
                        failures++;
                        if(!collectAll)throw new InvalidOperationException(label);
                    }
                    else count++;
                };
                if(mutationBoundary)CartPreviewCharacterizationChecks.RunMutationBoundary(check,root);
                else if(collectAll)CartPreviewCharacterizationChecks.RunCollectAll(check,root);
                else CartPreviewCharacterizationChecks.Run(check,root);
                if(failures>0)throw new InvalidOperationException("Characterization retains "+failures+" failed assertions; inspect the complete report.");
                report.AppendLine((mutationBoundary?"DIAGNOSTIC COMPLETE":"RESULT PASS")+" count="+count);
                Debug.Log("S06 cart characterization "+(mutationBoundary?"diagnostic complete ":"managed PASS ")+count + " folder=" + root);
            }
            catch (Exception error)
            {
                report.AppendLine("RESULT FAIL passed="+count+" failedAssertions="+failures);
                report.AppendLine(error.ToString());
                Debug.LogError("S06 cart characterization managed FAIL folder=" + root);
                throw;
            }
            finally
            {
                File.WriteAllText(Path.Combine(root, "checks.txt"), report.ToString(), new UTF8Encoding(false));
            }
        }
    }
}

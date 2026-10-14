using System;
using System.IO;
using System.Text;
using UnityEngine;
using DragLab.Data;
using DragLab.Runtime;

namespace DragLab.Editor
{
    public static class NativeSetupPreviewVerification
    {
        public static void Run()
        {
            var log=new StringBuilder();int checks=0;var vehicles=Resources.LoadAll<VehicleDefinition>("Data/Vehicles");
            NativeSetupPreviewChecks.Run(vehicles,(ok,label)=>{if(!ok)throw new InvalidOperationException(label);checks++;log.AppendLine("PASS: "+label);});
            NativeSetupPreviewChecks.LoadChecks(vehicles,(ok,label)=>{if(!ok)throw new InvalidOperationException(label);checks++;log.AppendLine("PASS: "+label);});
            var folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/setup-library"));Directory.CreateDirectory(folder);
            log.AppendLine("Cars: "+vehicles.Length+"; checks: "+checks+"; failures: 0");File.WriteAllText(Path.Combine(folder,"preview-verification.txt"),log.ToString(),new UTF8Encoding(false));
            Debug.Log("Setup preview verification passed: "+checks+" checks.");
        }
    }
}

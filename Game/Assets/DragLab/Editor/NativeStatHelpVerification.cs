using System;
using System.IO;
using System.Text;
using UnityEngine;
using DragLab.Data;
using DragLab.Runtime;

namespace DragLab.Editor
{
    public static class NativeStatHelpVerification
    {
        public static void Run()
        {
            var log=new StringBuilder();int checks=0;var vehicles=Resources.LoadAll<VehicleDefinition>("Data/Vehicles");
            NativeStatHelpChecks.Run(vehicles,(ok,label)=>{if(!ok)throw new InvalidOperationException(label);checks++;log.AppendLine("PASS: "+label);});
            var folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/stat-help"));Directory.CreateDirectory(folder);
            log.AppendLine("Cars: "+vehicles.Length+"; checks: "+checks+"; failures: 0");File.WriteAllText(Path.Combine(folder,"verification.txt"),log.ToString(),new UTF8Encoding(false));
            Debug.Log("Stat help verification passed: "+checks+" checks.");
        }
    }
}

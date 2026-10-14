using System;
using System.IO;
using System.Text;
using UnityEngine;
using DragLab.Data;
using DragLab.Runtime;
namespace DragLab.Editor
{
    public static class PartBrandVerification
    {
        public static void Run()
        {
            var log=new StringBuilder();int checks=0;PartBrandChecks.Run(Resources.LoadAll<VehicleDefinition>("Data/Vehicles"),(ok,label)=>{checks++;log.AppendLine((ok?"PASS ":"FAIL ")+label);if(!ok)throw new InvalidOperationException(label);});
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/part-brands"));Directory.CreateDirectory(folder);log.AppendLine("Checks: "+checks+" / failures: 0");File.WriteAllText(Path.Combine(folder,"verification.txt"),log.ToString(),new UTF8Encoding(false));Debug.Log("Part brand verification passed: "+checks);
        }
    }
}

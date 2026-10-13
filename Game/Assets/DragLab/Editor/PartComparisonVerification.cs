using System;
using System.IO;
using System.Text;
using UnityEngine;
using DragLab.Data;
using DragLab.Runtime;
namespace DragLab.Editor
{
    public static class PartComparisonVerification
    {
        public static void Run()
        {
            var log=new StringBuilder();int count=0,failures=0;
            NativePartComparisonChecks.Run(Resources.LoadAll<VehicleDefinition>("Data/Vehicles"),(ok,label)=>{count++;if(!ok)failures++;log.AppendLine((ok?"PASS: ":"FAIL: ")+label);});
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/part-comparisons"));Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"verification.txt"),log+"Checks: "+count+" / failures: "+failures+"\n",new UTF8Encoding(false));if(failures>0)throw new InvalidOperationException("Part comparison failures: "+failures);Debug.Log("Part comparison verification passed: "+count);
        }
    }
}

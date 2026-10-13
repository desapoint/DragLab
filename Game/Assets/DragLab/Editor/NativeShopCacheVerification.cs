using System;
using System.IO;
using System.Text;
using UnityEngine;
using DragLab.Data;
using DragLab.Runtime;

namespace DragLab.Editor
{
    public static class NativeShopCacheVerification
    {
        public static void Run()
        {
            var folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/shop-inventory"));
            Directory.CreateDirectory(folder);
            var log=new StringBuilder(); int checks=0;
            var vehicles=Resources.LoadAll<VehicleDefinition>("Data/Vehicles");
            NativeShopDomainChecks.Run(vehicles,(ok,label)=>{
                if(!ok)throw new Exception(label);
                checks++; log.AppendLine("PASS: "+label);
            });
            log.AppendLine("Cars: "+vehicles.Length+"; checks: "+checks+"; failures: 0");
            File.WriteAllText(Path.Combine(folder,"native-shop-cache-verification.txt"),log.ToString(),new UTF8Encoding(false));
            Debug.Log("Shop cache verification passed: "+checks+" checks across "+vehicles.Length+" cars.");
        }
    }
}

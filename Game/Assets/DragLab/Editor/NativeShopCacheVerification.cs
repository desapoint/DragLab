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
        public static void BrowserTimings()
        {
            string label=Environment.GetEnvironmentVariable("DRAGLAB_SHOP_TIMING_LABEL")??"after";
            if(label!="before"&&label!="after")throw new ArgumentException("Timing label must be before or after");
            string dir=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/shop-responsive-20261019"));Directory.CreateDirectory(dir);
            string result=NativeShopDomainChecks.BrowserTimings(Resources.LoadAll<VehicleDefinition>("Data/Vehicles"));
            File.WriteAllText(Path.Combine(dir,label+"-timings.txt"),result,new UTF8Encoding(false));Debug.Log(result);
        }
        public static void Responsiveness()
        {
            var vehicles=Resources.LoadAll<VehicleDefinition>("Data/Vehicles");int n=0;var log=new StringBuilder();
            void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);n++;log.AppendLine("PASS: "+label);if(n%100==0)Debug.Log("Shop regression progress: "+n);}
            var targets=System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Where(vehicles,v=>v.spec.id=="starter-hatch"||v.spec.id=="rally-sedan"||v.spec.id=="v8-muscle"||v.spec.id=="timber-diesel"));
            NativeShopDomainChecks.BrowserRegressions(vehicles,Check);NativeShopDomainChecks.Run(vehicles,Check,targets);
            NativeShopDomainChecks.UnitPreviews(vehicles,Check,targets);
            string dir=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/shop-responsive-20261019"));Directory.CreateDirectory(dir);
            log.AppendLine("PASS: "+n+" shop regression checks");File.WriteAllText(Path.Combine(dir,"domain-verification.txt"),log.ToString(),new UTF8Encoding(false));Debug.Log("Shop regression checks: "+n);
        }
        public static void CardWindows()
        {
            int checks=NativeShopDomainChecks.CardWindows();
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/mobile-clip-budget"));Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder,"layout-verification.txt"),"PASS: "+checks+" count/viewport/offset cases; bounded rows, exact full scroll extent, odd counts and large offsets.\n",new UTF8Encoding(false));
            Debug.Log("Card window checks passed: "+checks);
        }
        public static void UnitPreviews()
        {
            var folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/individual-part-preview"));Directory.CreateDirectory(folder);
            var log=new StringBuilder();int checks=0,failures=0;
            NativeShopDomainChecks.UnitPreviews(Resources.LoadAll<VehicleDefinition>("Data/Vehicles"),(ok,label)=>{checks++;if(!ok)failures++;log.AppendLine((ok?"PASS: ":"FAIL: ")+label);});
            log.AppendLine("Checks: "+checks+"; failures: "+failures);
            File.WriteAllText(Path.Combine(folder,"verification.txt"),log.ToString(),new UTF8Encoding(false));
            if(failures>0)throw new InvalidOperationException("Individual preview failures: "+failures);
            Debug.Log("Individual preview verification passed: "+checks);
        }
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

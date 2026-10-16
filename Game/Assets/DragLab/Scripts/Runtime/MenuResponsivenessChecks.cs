using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public static class MenuResponsivenessChecks
    {
        public static void Baseline()=>Run("before");
        public static void Optimized()=>Run("after");
        static void Run(string label)
        {
            var text=new StringBuilder("Menu workload timings / "+label+" / milliseconds\n");
            var vehicles=Resources.LoadAll<VehicleDefinition>("Data/Vehicles");MechanicalCatalog catalog=null;PlayerProfile p=null;NativePartBrowser browser=null;
            void Time(string name,Action work){var timer=Stopwatch.StartNew();work();timer.Stop();text.AppendLine(name+": "+timer.Elapsed.TotalMilliseconds.ToString("F2",System.Globalization.CultureInfo.InvariantCulture));}
            Time("catalogue",()=>catalog=new MechanicalCatalog(vehicles.Select(v=>v.spec)));
            Time("new profile / normalization",()=>{p=PlayerProfile.New(DragLab.Domain.RuntimeEntropy.UtcNow.Ticks);MechanicsOwnership.Ensure(p,catalog);p.cash=1000000;p.reputation=1000;});
            VehicleListings.Initialize(p,catalog,12345);
            text.AppendLine("definitions: "+catalog.parts.Count+" / new stock products: "+p.inventory.market.parts.Count+" / used stock: "+p.inventory.dealer.Count);
            Time("current profile normalization",()=>MechanicsOwnership.Ensure(p,catalog));
            Time("current profile validation",()=>{var error=MechanicsOwnership.Validate(p,catalog);if(error!=null)throw new InvalidOperationException(error);});
            Time("transaction validation / clone",()=>{if(!MechanicsTransactions.Try(p,catalog,p.revision,q=>{},q=>true,out _,out var error))throw new InvalidOperationException(error);});
            Time("three dealership static previews",()=>{foreach(var offer in p.vehicleStock.listings.Take(3))VehicleCollection.Preview(catalog,offer);});
            Time("stock refresh",()=>{for(int i=0;i<3;i++)VehicleListings.AfterRace(p,catalog);});
            var cache=new NativePartPreviewCache();
            Time("shop cold",()=>browser=new NativePartBrowser(p,Array.Empty<PartCartLine>(),false,"Shop",_=>100,(_,u)=>100,cache));
            Time("shop revisit",()=>browser=new NativePartBrowser(p,Array.Empty<PartCartLine>(),false,"Shop",_=>100,(_,u)=>100,cache));
            Time("10000 catalogue lookups",()=>{for(int i=0;i<10000;i++)catalog.Find(catalog.parts[i%catalog.parts.Count].id);});
            Time("100 stock quotes",()=>{foreach(var d in catalog.parts.Take(100))PartsMarket.Quote(p,d,1);});
            var head=browser.Entries.First(e=>e.Compatibility=="Compatible"&&e.Definition.category==PartCategory.HeadIntake);
            Time("part detail comparison",()=>browser.Evaluate(head));
            var line=new PartCartLine{definitionId=head.Definition.id,quantity=1,install=true};
            Time("planned shop",()=>browser=new NativePartBrowser(p,new[]{line},true,"Shop",_=>100,(_,u)=>100,cache));
            Time("planned shop revisit",()=>browser=new NativePartBrowser(p,new[]{line},true,"Shop",_=>100,(_,u)=>100,cache));
            Time("10 full profile serializations",()=>{for(int i=0;i<10;i++)JsonUtility.ToJson(p);});
#if UNITY_EDITOR
            string prior=ProfileStore.VerificationPath;try{ProfileStore.VerificationPath=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/player-fixtures/responsiveness-cpu/profile-v1.json"));ProfileStore.SaveMeasurement=(phase,ms)=>text.AppendLine(phase+": "+ms.ToString("F2",System.Globalization.CultureInfo.InvariantCulture));Time("durable profile save",()=>{if(!ProfileStore.TrySave(p))throw new InvalidOperationException(ProfileStore.Status);});}finally{ProfileStore.VerificationPath=prior;ProfileStore.SaveMeasurement=null;}
#endif
            text.AppendLine("profile bytes: "+Encoding.UTF8.GetByteCount(JsonUtility.ToJson(p)));
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/responsiveness"));Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"timings-"+label+".txt"),text.ToString(),new UTF8Encoding(false));UnityEngine.Debug.Log(text.ToString());
        }
    }
#endif
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Runtime
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public static class NativeStatHelpChecks
    {
        static IEnumerable<NativeStatDefinition> Rows(IEnumerable<NativeStatGroup> groups)
        {foreach(var group in groups){foreach(var row in group.Definitions)yield return row;foreach(var row in Rows(group.Children))yield return row;}}
        public static void Run(VehicleDefinition[] vehicles,Action<bool,string> check)
        {
            var catalog=new MechanicalCatalog(vehicles.Select(v=>v.spec));
            check(catalog.parts.Count>0&&catalog.parts.SelectMany(part=>PartSemantics.Stats(part).Select(metric=>"part:"+part.category+":rating:"+metric.key)).All(id=>NativeStatHelp.Find(id)!=null),"All catalogue part ratings have directional help, including non-OEM hardware");
            foreach(var vehicle in vehicles){
                var profile=PlayerProfile.New(DateTime.UtcNow.Ticks);MechanicsOwnership.Ensure(profile,catalog);profile.cash=1000000;profile.reputation=1000;
                VehicleCollection.Buy(profile,catalog,new VehicleOffer{model=vehicle.spec.id,price=1,reputation=0,stock=vehicle.CreateStockTune(),paint="#FFFFFF"});
                var inspection=NativePartBrowser.Inspect(InspectionBuild.Capture(profile,profile.SelectedCar));
                var current=new NativeStatsSnapshot(inspection);var proposed=new NativeStatsSnapshot(inspection);
                string before=JsonUtility.ToJson(profile);
                var rows=Rows(NativeStatCatalog.Groups(inspection)).Concat(NativeStatCatalog.TuningSummary().Definitions).ToArray();
                check(rows.Where(r=>r.Number!=null).All(r=>NativeStatHelp.Find(r.Id)!=null),"Every numeric stat, nested member and summary has authored direction help: "+vehicle.spec.id);
                check(rows.Where(r=>r.Number!=null).All(r=>r.InfoExplanation(current).StartsWith("More: ")&&r.InfoExplanation(current).Contains("\n\nLess: ")&&!r.InfoExplanation(current).Contains("Drawbacks in this comparison:")),"Single-car help has two directional paragraphs and no comparison claims: "+vehicle.spec.id);
                check(rows.Where(r=>r.Number!=null).All(r=>r.InfoExplanation(current,proposed).Contains("\n\nDrawbacks in this comparison: ")&&(r.Number(current).HasValue?r.InfoExplanation(current,proposed).Contains("unchanged"):r.InfoExplanation(current,proposed).Contains("unavailable"))),"Identical and unavailable values do not invent drawbacks: "+vehicle.spec.id);
                var ratio=new NativeStatDefinition{Id="finalDrive",Number=s=>ReferenceEquals(s,current)?3:4};
                check(ratio.InfoExplanation(current,proposed).Contains("higher.")&&ratio.InfoExplanation(current,proposed).Contains("more shifts"),"Shorter gearing explains the actual upward comparison's speed/shift penalty: "+vehicle.spec.id);
                ratio.Number=s=>ReferenceEquals(s,current)?4:3;
                check(ratio.InfoExplanation(current,proposed).Contains("lower.")&&ratio.InfoExplanation(current,proposed).Contains("struggle to pull"),"Taller gearing explains the downward comparison's launch penalty: "+vehicle.spec.id);
                var cooling=new NativeStatDefinition{Id="radiatorRejection",Number=s=>ReferenceEquals(s,current)?100:200};
                check(cooling.InfoExplanation(current,proposed).Contains("no direct drawback"),"Better heat rejection does not fabricate a cooling disadvantage: "+vehicle.spec.id);
                cooling.Number=s=>ReferenceEquals(s,current)?200:100;
                check(cooling.InfoExplanation(current,proposed).EndsWith("overheat the engine or oil."),"Reduced cooling explains sustained-load downside: "+vehicle.spec.id);
                cooling.Number=s=>ReferenceEquals(s,current)?(double?)null:0;
                check(cooling.InfoExplanation(current,proposed).Contains("unavailable")&&!cooling.InfoExplanation(current,proposed).Contains("value is higher"),"Missing hardware is not treated as a real zero: "+vehicle.spec.id);
                var layout=rows.First(r=>r.Id=="engineLayout");
                check(!layout.InfoExplanation(current,proposed).Contains("More:")&&layout.InfoExplanation(current,proposed)==layout.Help,"Categorical hardware descriptions do not invent a numeric ordering: "+vehicle.spec.id);
                check(NativeStatHelp.Find("part:Clutch:oilCondition")!=null&&NativeStatHelp.Find("exhaust:0:cutoff")!=null&&NativeStatHelp.Find("gearRatio:0")!=null,"Dynamic clutch, exhaust and gear IDs resolve shared help: "+vehicle.spec.id);
                var cam=new NativeStatDefinition{Id="part:Camshaft:rating:valvetrainrpm",Number=s=>ReferenceEquals(s,current)?7000:6000};
                var springs=new NativeStatDefinition{Id="part:ValveSprings:rating:valvetrainrpm",Number=cam.Number};
                check(cam.InfoExplanation(current,proposed).Contains("no direct drawback")&&springs.InfoExplanation(current,proposed).Contains("lose valve control"),"Lower cam requirement differs from reduced spring strength: "+vehicle.spec.id);
                check(before==JsonUtility.ToJson(profile),"Reading direction help never alters assets, wear, fuel or cash: "+vehicle.spec.id);
            }
        }
    }
#endif
}

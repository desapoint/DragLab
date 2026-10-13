using System;
using System.Linq;
using UnityEngine;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Runtime
{
    internal sealed class NativeSetupQuery
    {
        internal string Search="",Scope="All";
        internal static string ScopeName(SetupScope scope)=>new[]{"Engine + controls","Whole car","Tune only"}[(int)scope];
        internal bool Matches(SavedVehicleSetup setup,MechanicalCatalog catalog)
        {
            if(Scope!="All"&&Scope!=ScopeName(setup.scope))return false;
            string text=setup.name+" "+ScopeName(setup.scope)+" "+catalog.Spec(setup.vehicleDefinitionId).displayName;
            return text.IndexOf(Search.Trim(),StringComparison.OrdinalIgnoreCase)>=0;
        }
    }
    internal sealed class NativeSetupPreview
    {
        internal SetupPlan Plan;
        internal CarInspection Current,Proposed;
        internal string Unavailable;
        internal static NativeSetupPreview Capture(PlayerProfile profile,OwnedCar car,SavedVehicleSetup setup,NativePartPreviewCache cache=null)
        {
            var result=new NativeSetupPreview();
            if(car==null){result.Unavailable="Select a car in Garage to compare.";return result;}
            result.Plan=SavedSetups.Preview(profile,car.instanceId,setup.id);
            if(!string.IsNullOrEmpty(car.engineAssemblyId)){
                cache?.Bind(profile,car);
                result.Current=cache==null?NativePartBrowser.Inspect(InspectionBuild.Capture(profile,car)):cache.Inspection("current",()=>NativePartBrowser.Inspect(InspectionBuild.Capture(profile,car)));
            }
            if(result.Plan.resolution==null){result.Unavailable=string.Join("\n",result.Plan.errors);return result;}
            if(result.Current==null){result.Unavailable="Attach an engine to compare this setup.";return result;}
            if(result.Plan.resolution.errors.Count>0){result.Unavailable=string.Join("\n",result.Plan.resolution.errors);return result;}
            var copy=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(profile));copy.catalog=profile.catalog;
            var target=copy.cars.Find(c=>c.instanceId==car.instanceId);
            target.installed.slots.RemoveAll(s=>SavedSetups.InScope(setup.scope,AssemblyOperations.Parse(s.slot)));
            foreach(var line in result.Plan.lines){
                var item=line.purchase?null:copy.parts.Find(p=>p.instanceId==line.itemId);
                if(item==null&&!line.purchase)throw new InvalidOperationException("Owned setup component is unavailable.");
                if(line.purchase){
                    var definition=profile.catalog.Find(line.definitionId);
                    item=new PartInstance{instanceId="preview/setup/"+line.category,definitionId=line.definitionId,slot=line.category.ToString(),ownerId=target.instanceId,location=PartLocation.Vehicle};
                    EngineElements.Ensure(result.Plan.resolution.spec,definition,item);
                    if(line.category==PartCategory.Tires)TireDynamics.Ensure(item.condition);
                    copy.parts.Add(item);
                }
                item.slot=line.category.ToString();item.ownerId=target.instanceId;item.location=PartLocation.Vehicle;
                target.installed.slots.Add(new SlotBinding{slot=line.category.ToString(),partInstanceId=item.instanceId});
            }
            SavedTireFitments.Apply(copy,target,result.Plan,true);
            result.Proposed=NativePartBrowser.Inspect(InspectionBuild.Capture(copy,target,result.Plan.tune));
            return result;
        }
    }
}

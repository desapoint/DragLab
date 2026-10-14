using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
namespace DragLab.Domain.Mechanics
{
    // Main-thread callers capture only managed vehicle/condition data. Workers never access assets or profiles.
    public static class VehicleAssessmentCache
    {
        public struct Result { public bool engineRuns,drives; }
        [Serializable] sealed class Key
        {
            public VehicleSpec spec;public Tune tune;public FuelState fuel;public FluidState fluids;
            public ComponentCondition[] conditions;
        }
        static readonly Dictionary<string,Task<Result>> entries=new Dictionary<string,Task<Result>>(StringComparer.Ordinal);
        static readonly Queue<string> order=new Queue<string>();
        public static int Hits {get;private set;}
        public static int Misses {get;private set;}
        public static bool Pending=>entries.Values.Any(t=>!t.IsCompleted);
        public static Task<Result> Prepare(MechanicalCatalog catalog,VehicleOffer offer,BuildResolution prepared=null)
        {
            var build=prepared??VehicleInspection.Hardware(catalog,offer);
            var conditions=offer.parts.Select(p=>p.condition.Copy()).ToArray();
            // IDs distinguish assets, but do not change the physical inspection result.
            foreach(var c in conditions){foreach(var e in c.elements??Array.Empty<EngineElement>())e.id="";foreach(var t in c.tires??Array.Empty<TireCondition>())t.id="";}
            string key=JsonUtility.ToJson(new Key{spec=build.spec,tune=build.tune,fuel=offer.fuel,fluids=offer.fluids,conditions=conditions});
            if(entries.TryGetValue(key,out var cached)){Hits++;return cached;}
            // No catalogue construction, resource lookup or UnityEngine.Object is permitted in this job.
            if(!build.spec.hasInstalledParts)throw new InvalidOperationException("Inspection needs captured fitted hardware.");
            var spec=build.spec.Copy();var tune=build.tune.Copy();var parts=offer.parts.Select(p=>p.Copy()).ToArray();var fluids=offer.fluids.Copy();double fuel=offer.fuel.litres;
            var task=Task.Run(()=>{var car=new CarSimulation(spec,tune,new DriverSettings{wearMode=WearMode.None,throttleAssist=false},parts);car.SetFluids(fluids);car.SetMassInputs(tune.driverMassKg,tune.payloadMassKg,tune.payloadPositionM,tune.payloadHeightM,fuel);return Run(car);});Misses++;
            if(entries.Count>=64)entries.Remove(order.Dequeue());entries.Add(key,task);order.Enqueue(key);return task;
        }
        public static Result Run(CarSimulation car)
        {
            for(int i=0;i<30;i++)car.IndependentStep(DragMath.FixedStep,new DriverInput(),true,false);
            var result=new Result{engineRuns=!car.state.engineFailed&&!car.state.stalled&&car.state.engineSupplyAvailable&&car.state.rpm>0};
            if(!result.engineRuns)return result;
            for(int i=0;i<120;i++)car.Stage(DragMath.FixedStep,new DriverInput{throttle=.35});
            car.Launch(0);for(int i=0;i<240&&!car.state.engineFailed&&!car.state.drivetrainFailed;i++)car.Step(DragMath.FixedStep,new DriverInput{throttle=.5});
            result.drives=car.state.distance>.05&&!car.state.engineFailed&&!car.state.drivetrainFailed;return result;
        }
    }
}

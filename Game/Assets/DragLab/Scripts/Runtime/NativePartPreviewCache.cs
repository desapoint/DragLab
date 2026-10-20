using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Runtime
{
    internal sealed class NativePartPreviewCache
    {
        object profile,catalog;string context,currentPhysical;
        readonly Dictionary<string,CarInspection> inspections=new Dictionary<string,CarInspection>();
        readonly NativeSteadyOutputCache steadyCache=new NativeSteadyOutputCache();
        readonly Dictionary<string,PlayerProfile> plans=new Dictionary<string,PlayerProfile>();
        readonly Dictionary<string,NativePartEntry> comparisons=new Dictionary<string,NativePartEntry>();
        readonly LinkedList<string> recent=new LinkedList<string>();
        readonly Dictionary<string,LinkedListNode<string>> nodes=new Dictionary<string,LinkedListNode<string>>();
        internal const int ComparisonLimit=256;
        internal string Context=>context;
        internal int Hits,Misses;
        internal bool HasComparisons=>comparisons.Count>0;
        internal int SteadyHits=>steadyCache.Hits;
        internal int SteadyMisses=>steadyCache.Misses;
        // This cache is independent of the current car/profile revision. Only the exact
        // engine inputs participate in its key, so chassis and tire edits retain output.
        internal Func<double,InspectionSteadyPoint> SteadyOutput(InductionProfile induction)=>steadyCache.For(induction);
        internal static string Fingerprint(string text){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(text)));}
        // A vehicle's physical inspection should not expire when cash, another
        // car, market stock, selected-car ID or the global revision changes.
        // The key includes the car, catalog platform and fitted part instances
        // and their authored definitions (including individual tire/wheel sets).
        internal static string CarSignature(PlayerProfile p,OwnedCar car)
        {
            var state=new StringBuilder();
            // A car's presentation/history is not part of its physics input:
            // paint, rename, saved presets and pass records must never expire a
            // full race estimate. Capture only what InspectionBuild reads.
            state.Append(car.instanceId).Append('|').Append(car.vehicleId).Append('|')
                .Append(car.engineAssemblyId).Append('|')
                .Append(JsonUtility.ToJson(car.tune)).Append('|')
                .Append(car.fuel.litres.ToString("R",CultureInfo.InvariantCulture)).Append('|')
                .Append(JsonUtility.ToJson(car.fluids)).Append('|')
                .Append(JsonUtility.ToJson(p.catalog.Spec(car.vehicleId)));
            foreach(var slot in car.installed.slots){
                var part=p.parts.Find(i=>i.instanceId==slot.partInstanceId);
                state.Append('|').Append(slot.slot).Append('|').Append(slot.partInstanceId);
                if(part==null)continue;
                state.Append(JsonUtility.ToJson(part)).Append(JsonUtility.ToJson(p.catalog.Find(part.definitionId)));
                if(TireFitments.HasOverrides(part))
                    for(int i=0;i<4;i++)state.Append(JsonUtility.ToJson(p.catalog.Find(TireFitments.DefinitionId(part,i))));
                if(WheelFitments.HasOverrides(part))
                    for(int i=0;i<4;i++)state.Append(JsonUtility.ToJson(p.catalog.Find(WheelFitments.DefinitionId(part,i))));
            }
            return Fingerprint(state.ToString());
        }
        internal void Bind(PlayerProfile p,OwnedCar car)
        {
            // Shopping plans and comparisons depend on the wider inventory/
            // commerce context. The applied car's physical inspection does not.
            string physical=CarSignature(p,car);
            string key=p.revision+"/"+physical;
            if(ReferenceEquals(profile,p)&&ReferenceEquals(catalog,p.catalog)&&context==key)return;
            CarInspection retained=null;
            if(currentPhysical==physical)inspections.TryGetValue("current",out retained);
            profile=p;catalog=p.catalog;context=key;currentPhysical=physical;
            inspections.Clear();if(retained!=null)inspections["current"]=retained;
            plans.Clear();comparisons.Clear();recent.Clear();nodes.Clear();
        }
        internal CarInspection Inspection(string key,Func<CarInspection> calculate)
        {if(inspections.TryGetValue(key,out var value)){Hits++;return value;}Misses++;value=calculate();if(inspections.Count>=128)inspections.Clear();inspections[key]=value;return value;}
        internal PlayerProfile Plan(string key,Func<PlayerProfile> calculate)
        {if(plans.TryGetValue(key,out var value)){Hits++;return value;}Misses++;value=calculate();if(plans.Count>=4)plans.Clear();plans[key]=value;return value;}
        internal static string Key(string reference,NativePartEntry e)=>reference+"/"+JsonUtility.ToJson(e.Definition)+"/"+JsonUtility.ToJson(e.Part)+"/"+JsonUtility.ToJson(e.Unit)+"/"+e.Position;
        internal bool Restore(string key,NativePartEntry e)
        {
            if(!comparisons.TryGetValue(key,out var saved))return false;recent.Remove(nodes[key]);recent.AddLast(nodes[key]);Hits++;e.Ready=true;e.Proposed=saved.Proposed;e.Reason=saved.Reason;e.Compatibility=saved.Compatibility;e.Position=saved.Position;e.RequiresMatchingSet=saved.RequiresMatchingSet;e.Change=(double?[])saved.Change.Clone();return true;
        }
        internal void Store(string key,NativePartEntry e)
        {
            if(nodes.TryGetValue(key,out var node)){recent.Remove(node);recent.AddLast(node);}else{if(comparisons.Count>=ComparisonLimit){string oldest=recent.First.Value;recent.RemoveFirst();nodes.Remove(oldest);comparisons.Remove(oldest);}nodes[key]=recent.AddLast(key);}
            comparisons[key]=new NativePartEntry{Ready=true,Proposed=e.Proposed,Reason=e.Reason,Compatibility=e.Compatibility,Position=e.Position,RequiresMatchingSet=e.RequiresMatchingSet,Change=(double?[])e.Change.Clone()};
        }
    }

    // Stores only unconditioned steady induction points. Wear and failure state are
    // deliberately never cached: CarInspection still runs its mechanics preparation
    // at every RPM, so a worn piston, loss of fuel, etc. always takes effect.
    internal sealed class NativeSteadyOutputCache
    {
        internal const int Limit=32;
        readonly object gate=new object();
        readonly Dictionary<string,Dictionary<double,InspectionSteadyPoint>> values=new Dictionary<string,Dictionary<double,InspectionSteadyPoint>>(StringComparer.Ordinal);
        readonly LinkedList<string> recent=new LinkedList<string>();
        readonly Dictionary<string,LinkedListNode<string>> nodes=new Dictionary<string,LinkedListNode<string>>(StringComparer.Ordinal);
        int hits,misses;
        internal int Hits {get{lock(gate)return hits;}}
        internal int Misses {get{lock(gate)return misses;}}

        // Resolve the key on the Unity main thread (JsonUtility is not a worker
        // API). The returned pure-domain delegate may be read by another thread.
        internal Func<double,InspectionSteadyPoint> For(InductionProfile induction)
        {
            string key=Key(induction);
            Dictionary<double,InspectionSteadyPoint> points;
            lock(gate){
                if(!values.TryGetValue(key,out points)){
                    if(values.Count>=Limit){string oldest=recent.First.Value;recent.RemoveFirst();nodes.Remove(oldest);values.Remove(oldest);}
                    points=new Dictionary<double,InspectionSteadyPoint>();values.Add(key,points);nodes.Add(key,recent.AddLast(key));
                }else{recent.Remove(nodes[key]);recent.AddLast(nodes[key]);}
            }
            // Never share the mutable CarState from SteadyOutput. The lock also
            // protects in-flight delegates after their key has been evicted.
            return rpm=>{
                lock(gate){
                    if(points.TryGetValue(rpm,out var result)){hits++;return result;}
                    var evaluated=InductionDynamics.SteadyOutput(induction,rpm);
                    result=new InspectionSteadyPoint{engineTorqueNm=evaluated.engineTorqueNm,boostBar=evaluated.boostBar};
                    points.Add(rpm,result);misses++;return result;
                }
            };
        }

        static void Number(StringBuilder key,double number)=>key.Append(number.ToString("R",CultureInfo.InvariantCulture)).Append('|');
        internal static string Key(InductionProfile induction)
        {
            var spec=induction.vehicle;
            var key=new StringBuilder(1024);
            // Only the inputs read by SteadyOutput, BaseTorque, CamMultiplier,
            // CompressionMultiplier and Charge. Deliberately exclude tire,
            // chassis, braking, gearbox, fuel quantity and component wear data.
            key.Append(JsonUtility.ToJson(induction.data)).Append('|')
                .Append(JsonUtility.ToJson(spec.valvetrain)).Append('|')
                .Append(JsonUtility.ToJson(spec.combustion)).Append('|')
                .Append(JsonUtility.ToJson(spec.engineBehavior)).Append('|');
            Number(key,spec.redlineRpm);
            Number(key,spec.DisplacementLitres);
            Number(key,spec.fuelPowerCapacityHp);
            Number(key,spec.ignitionPowerCapacityHp);
            Number(key,induction.tune.PowerMultiplier);
            Number(key,(int)induction.type);
            Number(key,induction.turboCount);
            Number(key,induction.superchargerCount);
            Number(key,induction.size);
            Number(key,induction.boostSetting);
            Number(key,induction.pulley);
            // Keep the complete authored torque table, not just peak values:
            // two different engine curves can have identical peaks.
            foreach(var point in spec.torque){Number(key,point.rpm);Number(key,point.torqueNm);}
            return key.ToString();
        }
    }
}

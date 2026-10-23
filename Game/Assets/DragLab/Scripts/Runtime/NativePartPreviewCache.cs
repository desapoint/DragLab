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
            if(ReferenceEquals(catalog,p.catalog)&&currentPhysical==physical)
                inspections.TryGetValue("current",out retained);
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
        internal const int Limit = 32;
        internal const int PointLimit = 512;

        sealed class Entry
        {
            internal readonly SteadyOutputInputs Inputs;
            internal readonly Dictionary<double, InspectionSteadyPoint> Points = new Dictionary<double, InspectionSteadyPoint>();
            internal readonly Queue<double> Order = new Queue<double>();
            internal Entry(SteadyOutputInputs inputs) { Inputs = inputs; }
        }

        readonly object gate = new object();
        readonly Dictionary<SteadyOutputInputs, Entry> values = new Dictionary<SteadyOutputInputs, Entry>();
        readonly LinkedList<SteadyOutputInputs> recent = new LinkedList<SteadyOutputInputs>();
        readonly Dictionary<SteadyOutputInputs, LinkedListNode<SteadyOutputInputs>> nodes = new Dictionary<SteadyOutputInputs, LinkedListNode<SteadyOutputInputs>>();
        int hits, misses;
        internal int Hits { get { lock (gate) return hits; } }
        internal int Misses { get { lock (gate) return misses; } }
        internal int CachedEntries { get { lock (gate) return values.Count; } }
        internal int CachedPoints { get { lock (gate) return values.Values.Sum(entry => entry.Points.Count); } }

        // Capture on the input owner. The key owns only frozen engine dependencies;
        // no live profile, Unity object or mutable source escapes into the delegate.
        internal static SteadyOutputInputs Key(InductionProfile induction) => InductionDynamics.CaptureSteadyOutputInputs(induction);
        internal Func<double, InspectionSteadyPoint> For(InductionProfile induction)
        {
            var key = Key(induction);
            Entry entry;
            lock (gate)
            {
                if (!values.TryGetValue(key, out entry))
                {
                    if (values.Count >= Limit)
                    {
                        var oldest = recent.First.Value;
                        recent.RemoveFirst(); nodes.Remove(oldest); values.Remove(oldest);
                    }
                    entry = new Entry(key);
                    values.Add(key, entry); nodes.Add(key, recent.AddLast(key));
                }
                else
                {
                    var node = nodes[key]; recent.Remove(node); recent.AddLast(node);
                }
            }
            // Eviction does not invalidate an in-flight private evaluator. Its own
            // FIFO point table remains bounded and cannot reinsert an outer entry.
            return rpm =>
            {
                lock (gate)
                {
                    if (entry.Points.TryGetValue(rpm, out var result)) { hits++; return result; }
                    var evaluated = entry.Inputs.Evaluate(rpm);
                    result = new InspectionSteadyPoint { engineTorqueNm = evaluated.engineTorqueNm, boostBar = evaluated.boostBar };
                    if (entry.Points.Count >= PointLimit) entry.Points.Remove(entry.Order.Dequeue());
                    entry.Points.Add(rpm, result); entry.Order.Enqueue(rpm); misses++;
                    return result;
                }
            };
        }
    }
}

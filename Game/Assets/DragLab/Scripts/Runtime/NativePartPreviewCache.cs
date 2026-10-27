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
    internal sealed class NativePartsStaleInputsException : InvalidOperationException
    {
        internal NativePartsStaleInputsException(string message):base(message){}
    }

    internal sealed class NativePartPreviewCache
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static int legacyCarSignatureJsonCount,legacyEntryKeyJsonCount;
        internal static int LegacyCarSignatureJsonCount=>legacyCarSignatureJsonCount;
        internal static int LegacyEntryKeyJsonCount=>legacyEntryKeyJsonCount;
#else
        internal static int LegacyCarSignatureJsonCount=>0;
        internal static int LegacyEntryKeyJsonCount=>0;
#endif
        object profile,catalog;string context,currentPhysical;
        NativePartsBaseReadSet typedFacts;
        long typedGeneration;
        internal sealed class TypedBinding
        {
            readonly NativePartPreviewCache owner;
            readonly NativePartsBaseReadSet facts;
            internal long Generation{get;}
            internal TypedBinding(NativePartPreviewCache owner,NativePartsBaseReadSet facts,long generation){this.owner=owner;this.facts=facts;Generation=generation;}
            internal bool BelongsTo(NativePartPreviewCache cache)=>ReferenceEquals(owner,cache);
            internal bool Matches(NativePartPreviewCache cache,NativePartsBaseReadSet currentFacts,long generation)
                =>ReferenceEquals(owner,cache)&&ReferenceEquals(facts,currentFacts)&&Generation==generation;
        }
        sealed class TypedEntryKey : IEquatable<TypedEntryKey>
        {
            internal readonly long generation;internal readonly string source,id,asset,definition,position,role;
            internal TypedEntryKey(long g,NativePartEntry e){generation=g;source=e.Source??"";id=e.Id??"";asset=e.AssetId??"";definition=e.Definition?.id??"";position=e.Position??"";role=e.Unit?.element?.role??"";}
            public bool Equals(TypedEntryKey x)=>x!=null&&generation==x.generation&&source==x.source&&id==x.id&&asset==x.asset&&definition==x.definition&&position==x.position&&role==x.role;
            public override bool Equals(object obj)=>Equals(obj as TypedEntryKey);
            public override int GetHashCode(){unchecked{int h=generation.GetHashCode();h=h*31+StringComparer.Ordinal.GetHashCode(source);h=h*31+StringComparer.Ordinal.GetHashCode(id);h=h*31+StringComparer.Ordinal.GetHashCode(asset);h=h*31+StringComparer.Ordinal.GetHashCode(definition);h=h*31+StringComparer.Ordinal.GetHashCode(position);return h*31+StringComparer.Ordinal.GetHashCode(role);}}
        }
        sealed class TypedEntryValue
        {
            internal PartDefinition definition;internal PartInstance part;internal LooseEngineElement unit;
            internal NativePartEntry result;
            internal bool Matches(NativePartEntry e)=>MarketCatalogLayout.SameAuthoredDefinition(e.Definition,definition)&&NativePartsBaseReadSet.SamePartExact(e.Part,part)&&NativePartsBaseReadSet.SameUnitExact(e.Unit,unit);
        }
        readonly Dictionary<TypedEntryKey,TypedEntryValue> typedComparisons=new Dictionary<TypedEntryKey,TypedEntryValue>();
        readonly Dictionary<TypedEntryKey,LinkedListNode<string>> typedNodes=new Dictionary<TypedEntryKey,LinkedListNode<string>>();
        readonly Dictionary<string,TypedEntryKey> typedNodeKeys=new Dictionary<string,TypedEntryKey>(StringComparer.Ordinal);
        long typedNodeSerial;
        readonly Dictionary<string,CarInspection> inspections=new Dictionary<string,CarInspection>();
        readonly NativeSteadyOutputCache steadyCache=new NativeSteadyOutputCache();
        readonly Dictionary<string,PlayerProfile> plans=new Dictionary<string,PlayerProfile>();
        readonly Dictionary<string,NativePartEntry> comparisons=new Dictionary<string,NativePartEntry>();
        readonly LinkedList<string> recent=new LinkedList<string>();
        readonly Dictionary<string,LinkedListNode<string>> nodes=new Dictionary<string,LinkedListNode<string>>();
        internal const int ComparisonLimit=256;
        internal string Context=>context;
        internal int Hits,Misses;
        internal bool HasComparisons=>comparisons.Count+typedComparisons.Count>0;
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            legacyCarSignatureJsonCount++;
            RuntimeDataEncodingProbe.RecordJsonCall();
#endif
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
            plans.Clear();comparisons.Clear();typedComparisons.Clear();typedNodes.Clear();typedNodeKeys.Clear();typedFacts=null;typedGeneration=checked(typedGeneration+1);recent.Clear();nodes.Clear();
        }
        internal CarInspection Inspection(string key,Func<CarInspection> calculate)
        {if(inspections.TryGetValue(key,out var value)){Hits++;return value;}Misses++;value=calculate();if(inspections.Count>=128)inspections.Clear();inspections[key]=value;return value;}
        internal TypedBinding BindTyped(NativePartsBaseReadSet facts,NativePartsBaseReadInputs freshInputs)
        {
            NativePartsOwnerContext.AssertMainThread();if(facts==null||freshInputs==null)throw new ArgumentNullException(facts==null?nameof(facts):nameof(freshInputs));
            if(!facts.MatchesCurrent(freshInputs,out var reason))throw new NativePartsStaleInputsException(reason??"Typed cache binding requires a full current read-set fence.");
            bool sameWide=typedFacts!=null&&typedFacts.SameWideCacheContext(facts)&&typedFacts.MatchesCurrent(freshInputs,out _);
            bool samePhysical=typedFacts!=null&&typedFacts.SamePhysicalInspectionInputs(facts);
            if(!sameWide){typedGeneration=checked(typedGeneration+1);typedComparisons.Clear();typedNodes.Clear();typedNodeKeys.Clear();plans.Clear();comparisons.Clear();recent.Clear();nodes.Clear();}
            CarInspection current=null,healthy=null;
            if(samePhysical){inspections.TryGetValue("typed-current",out current);inspections.TryGetValue("typed-healthy-current",out healthy);}
            inspections.Clear();if(current!=null)inspections["typed-current"]=current;if(healthy!=null)inspections["typed-healthy-current"]=healthy;
            var typedProfile=facts.CapturedProfile;profile=typedProfile;catalog=typedProfile?.catalog;context=null;currentPhysical=null;typedFacts=facts;
            return new TypedBinding(this,facts,typedGeneration);
        }
        internal CarInspection TypedCurrent(TypedBinding binding,bool healthy,Func<CarInspection> calculate)
        {
            RequireBinding(binding);string key=healthy?"typed-healthy-current":"typed-current";
            return Inspection(key,calculate);
        }
        internal bool RestoreTyped(TypedBinding binding,NativePartEntry e)
        {
            if(e==null)throw new ArgumentNullException(nameof(e));RequireBinding(binding);var key=new TypedEntryKey(binding.Generation,e);
            if(!typedComparisons.TryGetValue(key,out var value)){Misses++;return false;}
            if(!value.Matches(e)){RemoveTyped(key);Misses++;return false;}
            TouchTyped(key);Hits++;CopyResult(value.result,e);return true;
        }
        internal void StoreTyped(TypedBinding binding,NativePartEntry e)
        {
            if(e==null)throw new ArgumentNullException(nameof(e));RequireBinding(binding);var key=new TypedEntryKey(binding.Generation,e);
            if(!typedComparisons.ContainsKey(key))EvictTypedIfFull();
            typedComparisons[key]=new TypedEntryValue{definition=MarketCatalogLayout.CopyAuthoredDefinition(e.Definition),part=NativePartsBaseReadSet.CopyPartExact(e.Part),unit=NativePartsBaseReadSet.CopyUnitExact(e.Unit),result=CopyResult(e)};
            TouchTyped(key);
        }
        void RequireBinding(TypedBinding binding)
        {NativePartsOwnerContext.AssertMainThread();if(binding==null||!binding.Matches(this,typedFacts,typedGeneration))throw new InvalidOperationException("Typed preview binding is stale or belongs to another cache.");}
        void TouchTyped(TypedEntryKey key)
        {if(typedNodes.TryGetValue(key,out var node)){recent.Remove(node);typedNodes[key]=recent.AddLast(node.Value);return;}string handle="T:"+checked(++typedNodeSerial).ToString(CultureInfo.InvariantCulture);typedNodes[key]=recent.AddLast(handle);typedNodeKeys[handle]=key;}
        void RemoveTyped(TypedEntryKey key)
        {typedComparisons.Remove(key);if(typedNodes.TryGetValue(key,out var node)){recent.Remove(node);typedNodes.Remove(key);typedNodeKeys.Remove(node.Value);}}
        void EvictTypedIfFull()
        {
            if(comparisons.Count+typedComparisons.Count<ComparisonLimit)return;
            while(recent.First!=null){string handle=recent.First.Value;recent.RemoveFirst();if(handle.StartsWith("T:",StringComparison.Ordinal)){if(typedNodeKeys.TryGetValue(handle,out var typedKey)){typedNodeKeys.Remove(handle);typedNodes.Remove(typedKey);typedComparisons.Remove(typedKey);return;}}else if(handle.StartsWith("L:",StringComparison.Ordinal)){string key=handle.Substring(2);if(nodes.ContainsKey(key)){nodes.Remove(key);comparisons.Remove(key);return;}}}
        }
        static NativePartEntry CopyResult(NativePartEntry e)=>new NativePartEntry{Ready=e.Ready,Proposed=e.Proposed,Reason=e.Reason,Compatibility=e.Compatibility,Position=e.Position,RequiresMatchingSet=e.RequiresMatchingSet,Change=(double?[])e.Change.Clone()};
        static void CopyResult(NativePartEntry from,NativePartEntry to){to.Ready=true;to.Proposed=from.Proposed;to.Reason=from.Reason;to.Compatibility=from.Compatibility;to.Position=from.Position;to.RequiresMatchingSet=from.RequiresMatchingSet;to.Change=(double?[])from.Change.Clone();}
        internal PlayerProfile Plan(string key,Func<PlayerProfile> calculate)
        {if(plans.TryGetValue(key,out var value)){Hits++;return value;}Misses++;value=calculate();if(plans.Count>=4)plans.Clear();plans[key]=value;return value;}
        internal static string Key(string reference,NativePartEntry e)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            legacyEntryKeyJsonCount++;
            RuntimeDataEncodingProbe.RecordJsonCall();
#endif
            return reference+"/"+JsonUtility.ToJson(e.Definition)+"/"+JsonUtility.ToJson(e.Part)+"/"+JsonUtility.ToJson(e.Unit)+"/"+e.Position;
        }
        internal bool Restore(string key,NativePartEntry e)
        {
            if(!comparisons.TryGetValue(key,out var saved))return false;recent.Remove(nodes[key]);nodes[key]=recent.AddLast("L:"+key);Hits++;e.Ready=true;e.Proposed=saved.Proposed;e.Reason=saved.Reason;e.Compatibility=saved.Compatibility;e.Position=saved.Position;e.RequiresMatchingSet=saved.RequiresMatchingSet;e.Change=(double?[])saved.Change.Clone();return true;
        }
        internal void Store(string key,NativePartEntry e)
        {
            if(nodes.TryGetValue(key,out var node)){recent.Remove(node);nodes[key]=recent.AddLast("L:"+key);}else{EvictTypedIfFull();nodes[key]=recent.AddLast("L:"+key);}
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

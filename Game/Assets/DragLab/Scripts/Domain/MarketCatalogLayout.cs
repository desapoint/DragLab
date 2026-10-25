using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine;
namespace DragLab.Domain.Mechanics
{
    // Authored product equivalence and maximum set sizes do not change with a stock epoch.
    internal static class MarketCatalogLayout
    {
        sealed class Product { public PartDefinition snapshot;public string key,json,jsonId; }
        internal sealed class Group {public PartDefinition definition;public List<string> ids;public int maximum;public bool deliverable;}
        // Original LINQ intentionally used TWO different comparers:
        // g.Min(d => d.id) uses Comparer<string>.Default for group order,
        // while g.OrderBy(d => d.id, StringComparer.Ordinal).First() chooses
        // the group representative. Do not conflate them: doing so changes
        // seeded stock and definition/quantity assignment.
        // Worker DTOs contain only copied managed data and precomputed string keys.
        // No Unity objects, catalog mutations or JsonUtility calls cross the boundary.
        sealed class DetachedInput {internal PartDefinition part;internal string key;}
        sealed class DetachedGroup {internal string representative;internal List<string> ids;internal int maximum;internal bool deliverable;}
        sealed class GroupDraft
        {
            internal PartDefinition definition;
            internal string orderingId;
            internal readonly List<string> ids=new List<string>();
            internal GroupDraft(PartDefinition first){definition=first;orderingId=first.id;}
        }
        internal sealed class Layout
        {
            public string[] ids,keys;public Group[] groups;
            readonly Dictionary<string,VehicleSpec> stockSpecs=new Dictionary<string,VehicleSpec>();
            readonly Dictionary<string,PartDefinition[][]> choices=new Dictionary<string,PartDefinition[][]>();
            public void Invalidate(){stockSpecs.Clear();choices.Clear();}
            public VehicleSpec Stock(MechanicalCatalog catalog,string model){if(!stockSpecs.TryGetValue(model,out var spec))stockSpecs[model]=spec=catalog.Resolve(model,catalog.Recipe(model),new Tune()).spec;return spec;}
            public PartDefinition[][] Choices(MechanicalCatalog catalog,string model){if(!choices.TryGetValue(model,out var rows)){var spec=Stock(catalog,model);choices[model]=rows=catalog.parts.Where(d=>catalog.Fits(spec,d)&&PartSemantics.Wears(d.category)).GroupBy(d=>d.category).OrderBy(g=>g.Key).Select(g=>g.OrderBy(d=>d.id,StringComparer.Ordinal).ToArray()).ToArray();}return rows;}
        }
        static readonly HashSet<string> fastFields=new HashSet<string>(new[]{"name","fitment","brand","quality","fuelKind","pistonCrown","crownVolumeCc","valveReliefMm","springMaxLiftMm","followerInterface","plugHeatRange","category","boreMm","power","grip","massScale","shiftScale","capacityScale","boostScale","centerRatioMin","centerRatioMax","rpmLimit","torqueLimitNm","massKg","inertiaKgM2","positionFromFrontAxleM","heightM","price","stage","reputationRequired","clutchEnvironment","clutchMaterial","clutchActuation","clutchPlateCount","clutchOilCapacityLitres","clutchEngagementRpm","tuning","turboSize","fixedBoost","boostMin","boostMax","fixedPulley","pulleyMin","pulleyMax","fixedFinalDrive","finalDriveMin","finalDriveMax","valve","turboUnits","superchargerUnits","fuelDelivery","supportsInjection","supportsDiesel","injectionControlMetadataVersion","controllerCylinderCapacity","injectionIdleControl","injectionMixtureVariation","camLiftMm","camDurationDegrees","camOverlapDegrees","camLobeSeparation","camTorqueBias","valvetrainRpm","camTimingAdjustable","fluidCapacityLitres","fluidMinimumLitres","baffling","pumpFlowScale","capPressureBar","induction","transmission","manualDownshiftInterlock","manualLayout","sequentialShiftScale","thermalLimitC","heatCapacityJPerC","coolingWattsPerC","wearEnergyJ","overloadSeconds","tireLifeDistanceM","tireOptimalC","tireHeatFraction","description","differential","torqueBiasRatio","preloadNm","engineHeatTransferScale","maxPowerHp","provenance"});
        static readonly FieldInfo[] fields=typeof(PartDefinition).GetFields().Where(f=>f.Name!="id"&&!fastFields.Contains(f.Name)).ToArray();
        static readonly ConditionalWeakTable<PartDefinition,Product> products=new ConditionalWeakTable<PartDefinition,Product>();
        static readonly ConditionalWeakTable<MechanicalCatalog,Layout> layouts=new ConditionalWeakTable<MechanicalCatalog,Layout>();
        // Canonical product-key strings depend on authored data, not the market
        // seed, stock, player wear, reservations or race. A completed snapshot
        // can be shared by subsequent epochs of this same catalog revision.
        sealed class KeySnapshot
        {
            internal int revision;
            internal PartDefinition[] definitions;
            internal Dictionary<string,string> keys;
            internal IReadOnlyList<ReadyProductGroup> shopGroups;
            internal IReadOnlyDictionary<string,ReadyProductGroup> shopGroupById;
        }
        internal sealed class ReadyProductGroup
        {
            internal IReadOnlyList<string> DefinitionIds { get; }
            internal ReadyProductGroup(string[] ids){DefinitionIds=Array.AsReadOnly(ids);}
        }
        static readonly ConditionalWeakTable<MechanicalCatalog,KeySnapshot> readyKeys=
            new ConditionalWeakTable<MechanicalCatalog,KeySnapshot>();
        internal static bool TryReadyProductKeys(MechanicalCatalog catalog,out Dictionary<string,string> keys)
        {
            keys=null;
            if(catalog==null||!readyKeys.TryGetValue(catalog,out var entry))return false;
            if(entry.revision!=catalog.AuthoringRevision||
                entry.definitions.Length!=catalog.parts.Count)
            {
                readyKeys.Remove(catalog);
                return false;
            }
            for(int i=0;i<entry.definitions.Length;i++)
                if(!ReferenceEquals(entry.definitions[i],catalog.parts[i]))
                {
                    readyKeys.Remove(catalog);
                    return false;
                }
            keys=entry.keys;return true;
        }
        static bool ValidateReadyProductKeys(MechanicalCatalog catalog,KeySnapshot entry)
        {
            for(int i=0;i<entry.definitions.Length;i++)
            {
                var definition=catalog.parts[i];
                if(entry.definitions[i].id!=definition.id||
                    !entry.keys.TryGetValue(definition.id,out var expected)||Key(definition)!=expected)
                {
                    readyKeys.Remove(catalog);layouts.Remove(catalog);return false;
                }
            }
            return true;
        }
        // Returns only source-ordered authored product IDs from a completed
        // warm snapshot. Semantic validation catches public in-place edits,
        // even when AuthoringRevision and list identity did not change.
        internal static bool TryReadyShopProductGroups(MechanicalCatalog catalog,
            out IReadOnlyList<ReadyProductGroup> groups)
            =>TryReadyShopProductGroups(catalog,out groups,out _);
        internal static bool TryReadyShopProductGroups(MechanicalCatalog catalog,
            out IReadOnlyList<ReadyProductGroup> groups,
            out IReadOnlyDictionary<string,ReadyProductGroup> groupById)
        {
            groups=null;groupById=null;
            if(!TryReadyProductKeys(catalog,out _)||
                !readyKeys.TryGetValue(catalog,out var entry)||entry.shopGroups==null||
                entry.shopGroupById==null||entry.shopGroupById.Count!=catalog.parts.Count||
                !ValidateReadyProductKeys(catalog,entry))
                return false;
            groups=entry.shopGroups;groupById=entry.shopGroupById;
            return true;
        }
        internal static bool PublishProductKeys(MechanicalCatalog catalog,
            PartDefinition[] definitions,int revision,Dictionary<string,string> keys)
        {
            if(catalog==null||definitions==null||keys==null||
                revision!=catalog.AuthoringRevision||
                definitions.Length!=catalog.parts.Count||definitions.Length!=keys.Count)
                return false;
            for(int i=0;i<definitions.Length;i++)
                if(!ReferenceEquals(definitions[i],catalog.parts[i])||
                    !keys.ContainsKey(definitions[i].id))return false;
            var complete=new KeySnapshot{
                revision=revision,definitions=definitions,keys=keys
            };
            readyKeys.Remove(catalog);
            readyKeys.Add(catalog,complete);
            return true;
        }
        static bool PublishShopProductGroups(MechanicalCatalog catalog,KeySnapshot expected,
            IReadOnlyList<ReadyProductGroup> groups,
            IReadOnlyDictionary<string,ReadyProductGroup> groupById,
            int revision,PartDefinition[] definitions)
        {
            if(catalog==null||groups==null||groupById==null||definitions==null||groupById.Count!=definitions.Length||
                !readyKeys.TryGetValue(catalog,out var entry)||!ReferenceEquals(entry,expected)||entry.revision!=revision||
                revision!=catalog.AuthoringRevision||definitions.Length!=catalog.parts.Count||
                !definitions.SequenceEqual(catalog.parts)||!ValidateReadyProductKeys(catalog,entry))return false;
            entry.shopGroups=groups;entry.shopGroupById=groupById;
            return true;
        }
        static bool Same(PartDefinition a,PartDefinition b)
        {
            if(!(a.name==b.name && a.fitment==b.fitment && a.brand==b.brand && a.quality==b.quality && a.fuelKind==b.fuelKind &&
                a.pistonCrown==b.pistonCrown && a.crownVolumeCc==b.crownVolumeCc && a.valveReliefMm==b.valveReliefMm && a.springMaxLiftMm==b.springMaxLiftMm && a.followerInterface==b.followerInterface &&
                a.plugHeatRange==b.plugHeatRange && a.category==b.category && a.boreMm==b.boreMm && a.power==b.power && a.grip==b.grip &&
                a.massScale==b.massScale && a.shiftScale==b.shiftScale && a.capacityScale==b.capacityScale && a.boostScale==b.boostScale && a.centerRatioMin==b.centerRatioMin &&
                a.centerRatioMax==b.centerRatioMax && a.rpmLimit==b.rpmLimit && a.torqueLimitNm==b.torqueLimitNm && a.massKg==b.massKg && a.inertiaKgM2==b.inertiaKgM2 &&
                a.positionFromFrontAxleM==b.positionFromFrontAxleM && a.heightM==b.heightM && a.price==b.price && a.stage==b.stage && a.reputationRequired==b.reputationRequired &&
                a.clutchEnvironment==b.clutchEnvironment && a.clutchMaterial==b.clutchMaterial && a.clutchActuation==b.clutchActuation && a.clutchPlateCount==b.clutchPlateCount && a.clutchOilCapacityLitres==b.clutchOilCapacityLitres &&
                a.clutchEngagementRpm==b.clutchEngagementRpm && a.tuning==b.tuning && a.turboSize==b.turboSize && a.fixedBoost==b.fixedBoost && a.boostMin==b.boostMin &&
                a.boostMax==b.boostMax && a.fixedPulley==b.fixedPulley && a.pulleyMin==b.pulleyMin && a.pulleyMax==b.pulleyMax && a.fixedFinalDrive==b.fixedFinalDrive &&
                a.finalDriveMin==b.finalDriveMin && a.finalDriveMax==b.finalDriveMax && a.valve==b.valve && a.turboUnits==b.turboUnits && a.superchargerUnits==b.superchargerUnits &&
                a.fuelDelivery==b.fuelDelivery && a.supportsInjection==b.supportsInjection && a.supportsDiesel==b.supportsDiesel && a.injectionControlMetadataVersion==b.injectionControlMetadataVersion && a.controllerCylinderCapacity==b.controllerCylinderCapacity &&
                a.injectionIdleControl==b.injectionIdleControl && a.injectionMixtureVariation==b.injectionMixtureVariation && a.camLiftMm==b.camLiftMm && a.camDurationDegrees==b.camDurationDegrees && a.camOverlapDegrees==b.camOverlapDegrees &&
                a.camLobeSeparation==b.camLobeSeparation && a.camTorqueBias==b.camTorqueBias && a.valvetrainRpm==b.valvetrainRpm && a.camTimingAdjustable==b.camTimingAdjustable && a.fluidCapacityLitres==b.fluidCapacityLitres &&
                a.fluidMinimumLitres==b.fluidMinimumLitres && a.baffling==b.baffling && a.pumpFlowScale==b.pumpFlowScale && a.capPressureBar==b.capPressureBar && a.induction==b.induction &&
                a.transmission==b.transmission && a.manualDownshiftInterlock==b.manualDownshiftInterlock && a.manualLayout==b.manualLayout && a.sequentialShiftScale==b.sequentialShiftScale && a.thermalLimitC==b.thermalLimitC &&
                a.heatCapacityJPerC==b.heatCapacityJPerC && a.coolingWattsPerC==b.coolingWattsPerC && a.wearEnergyJ==b.wearEnergyJ && a.overloadSeconds==b.overloadSeconds && a.tireLifeDistanceM==b.tireLifeDistanceM &&
                a.tireOptimalC==b.tireOptimalC && a.tireHeatFraction==b.tireHeatFraction && a.description==b.description && a.differential==b.differential && a.torqueBiasRatio==b.torqueBiasRatio &&
                a.preloadNm==b.preloadNm && a.engineHeatTransferScale==b.engineHeatTransferScale && a.maxPowerHp==b.maxPowerHp && a.provenance==b.provenance))return false;
            foreach(var f in fields){var x=f.GetValue(a);var y=f.GetValue(b);if(ReferenceEquals(x,y))continue;if(f.Name=="awdHardware"){if(!SameAwdHardware((AwdHardwareSpec)x,(AwdHardwareSpec)y))return false;}else if(f.FieldType.IsValueType||f.FieldType==typeof(string)){if(!Equals(x,y))return false;}else if(JsonUtility.ToJson(x)!=JsonUtility.ToJson(y))return false;}return true;
        }
        static bool SameAwdHardware(AwdHardwareSpec a,AwdHardwareSpec b)
        {
            if(ReferenceEquals(a,b))return true;if(a==null||b==null)return false;
            return a.version==b.version&&a.modes==b.modes&&a.integratedCenter==b.integratedCenter&&
                a.adjustments==b.adjustments&&a.responseMin==b.responseMin&&a.responseMax==b.responseMax&&
                SameAwdSpec(a.calibration,b.calibration);
        }
        static bool SameAwdSpec(AwdSpec a,AwdSpec b)
        {
            if(ReferenceEquals(a,b))return true;if(a==null||b==null)return false;
            return a.mode==b.mode&&a.centerSpeedRatio==b.centerSpeedRatio&&
                a.torqueBiasRatio==b.torqueBiasRatio&&a.centerPreloadNm==b.centerPreloadNm&&
                a.frontBias==b.frontBias&&a.minimumFront==b.minimumFront&&a.maximumFront==b.maximumFront&&
                a.primaryFront==b.primaryFront&&a.isolateFailedAxle==b.isolateFailedAxle&&
                a.maximumSecondary==b.maximumSecondary&&a.responseSeconds==b.responseSeconds&&
                a.slipThreshold==b.slipThreshold&&a.preload==b.preload&&
                a.freeShaftInertiaKgM2==b.freeShaftInertiaKgM2&&a.freeShaftDragNm==b.freeShaftDragNm&&
                a.freeShaftViscousDrag==b.freeShaftViscousDrag;
        }
        internal static string Key(PartDefinition d)
        {
            var entry=products.GetValue(d,_=>new Product());if(entry.snapshot!=null&&Same(d,entry.snapshot))return entry.key;
            entry.snapshot=d.Copy();entry.json=null;var copy=d.Copy();copy.id="";return entry.key=JsonUtility.ToJson(copy);
        }
        internal static string Json(PartDefinition d){Key(d);var entry=products.GetValue(d,_=>new Product());if(entry.json==null||entry.jsonId!=d.id){entry.jsonId=d.id;entry.json=JsonUtility.ToJson(d);}return entry.json;}
        static bool Matches(Layout entry,MechanicalCatalog catalog,Dictionary<string,string> keys)
            =>entry.groups!=null&&catalog.parts.Select(d=>d.id).SequenceEqual(entry.ids)&&
                catalog.parts.Select(d=>keys[d.id]).SequenceEqual(entry.keys);

        // Synchronous API retained for original callers and deterministic editor tests.
        internal static Layout Get(MechanicalCatalog catalog,Dictionary<string,string> keys,VehicleSpec[] specs)
        {
            Layout result=null;
            foreach(var ignored in GetSteps(catalog,keys,specs,value=>result=value)){}
            return result;
        }

        static DetachedGroup[] BuildDetachedGroups(DetachedInput[] inputs,VehicleSpec[] specs)
        {
            // Exactly the former LINQ semantics: default-comparer Min for the
            // group order, ordinal ID for the representative and alias list.
            var source=inputs.GroupBy(d=>d.key,StringComparer.Ordinal)
                .OrderBy(g=>g.Min(d=>d.part.id),StringComparer.Ordinal).ToArray();
            var groups=new DetachedGroup[source.Length];
            for(int i=0;i<source.Length;i++)
            {
                var sourceGroup=source[i];
                var definition=sourceGroup.OrderBy(d=>d.part.id,StringComparer.Ordinal).First().part;
                bool fitting=false;int maximum=1;
                foreach(var spec in specs)
                    if(MechanicalCatalog.FitsDetached(spec,definition))
                    {
                        fitting=true;
                        maximum=Math.Max(maximum,PartsMarket.Count(spec,definition));
                    }
                groups[i]=new DetachedGroup{
                    representative=definition.id,
                    ids=sourceGroup.Select(d=>d.part.id).OrderBy(id=>id,StringComparer.Ordinal).ToList(),
                    maximum=maximum,deliverable=fitting
                };
            }
            return groups;
        }
        internal static void ClearForVerification(MechanicalCatalog catalog)=>layouts.Remove(catalog);

        // A cold layout is built privately and only published once all groups
        // have been fitted. Synchronous callers keep the stepwise 2 ms soft
        // budget; incremental after-race callers offload copied pure data.
        internal static IEnumerable<object> GetSteps(MechanicalCatalog catalog,Dictionary<string,string> keys,
            VehicleSpec[] specs,Action<Layout> complete,bool offloadManaged=false)
        {
            var existing=layouts.GetValue(catalog,_=>new Layout());
            if(Matches(existing,catalog,keys)){complete(existing);yield break;}
            var candidate=new Layout{
                ids=catalog.parts.Select(d=>d.id).ToArray(),
                keys=catalog.parts.Select(d=>keys[d.id]).ToArray()
            };
            if(offloadManaged)
            {
                // Copy mutable authored definitions on Unity's main thread, then
                // give this complete detached snapshot exclusively to one worker.
                var inputs=new DetachedInput[catalog.parts.Count];
                int inputIndex=0;
                long captureStart=System.Diagnostics.Stopwatch.GetTimestamp();
                foreach(var authored in catalog.parts)
                {
                    inputs[inputIndex++]=new DetachedInput{
                        part=authored.Copy(),key=keys[authored.id]
                    };
                    if(inputIndex%32==0||
                        (System.Diagnostics.Stopwatch.GetTimestamp()-captureStart)*1000.0/
                            System.Diagnostics.Stopwatch.Frequency>=2.0)
                    {
                        yield return "parts/layout snapshot";
                        captureStart=System.Diagnostics.Stopwatch.GetTimestamp();
                    }
                }
                var workerSpecs=specs.Select(s=>s.Copy()).ToArray();
                var worker=Task.Run(()=>BuildDetachedGroups(inputs,workerSpecs));
                var wait=new MarketWorkerWait(worker);
                // Always surface the task to the caller so synchronous shutdown
                // boundaries can join the same worker safely.
                yield return wait;
                while(!worker.IsCompleted)yield return wait;
                var completed=worker.GetAwaiter().GetResult();
                var byId=catalog.parts.ToDictionary(d=>d.id,StringComparer.Ordinal);
                candidate.groups=completed.Select(g=>new Group{
                    definition=byId[g.representative],ids=g.ids,
                    maximum=g.maximum,deliverable=g.deliverable
                }).ToArray();
            }
            else
            {
            // GroupBy + OrderBy in one MoveNext caused the remaining 55 ms
            // player hitch. Construct equivalent buckets incrementally instead.
            // The entire draft is private until final atomic layout publication.
            var byKey=new Dictionary<string,GroupDraft>(StringComparer.Ordinal);
            var ordered=new SortedDictionary<string,GroupDraft>(StringComparer.Ordinal);
            long start=System.Diagnostics.Stopwatch.GetTimestamp();
            int rowsSinceYield=0;
            foreach(var definition in catalog.parts)
            {
                string key=keys[definition.id];
                if(!byKey.TryGetValue(key,out var group))
                {
                    group=new GroupDraft(definition);
                    byKey.Add(key,group);
                    ordered.Add(definition.id,group);
                }
                else
                {
                    // Ordinal representative and default-comparer minimum differ
                    // for some ID punctuation/case combinations. Preserve both.
                    if(StringComparer.Ordinal.Compare(definition.id,group.definition.id)<0)
                        group.definition=definition;
                    if(Comparer<string>.Default.Compare(definition.id,group.orderingId)<0)
                    {
                        ordered.Remove(group.orderingId);
                        group.orderingId=definition.id;
                        ordered.Add(group.orderingId,group);
                    }
                }
                group.ids.Add(definition.id);
                // The fixed batch cap makes both cold-start pacing and the
                // deterministic regression independent of machine speed.
                if(++rowsSinceYield>=32||
                    (System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/
                    System.Diagnostics.Stopwatch.Frequency>=2.0)
                {
                    yield return "parts/layout grouping";
                    rowsSinceYield=0;
                    start=System.Diagnostics.Stopwatch.GetTimestamp();
                }
            }
            // Never combine the last grouping batch with an expensive first fit.
            yield return "parts/layout grouping";
            var groups=new List<Group>(ordered.Count);
            start=System.Diagnostics.Stopwatch.GetTimestamp();
            foreach(var group in ordered.Values)
            {
                var definition=group.definition;
                var fitting=false;
                int maximum=1;
                // Fitting one entire product against all vehicle specifications used
                // to be an indivisible MoveNext; it caused a 54.6 ms player hitch.
                // Resume within that product with local state intact and no
                // partially built group visible to any caller.
                foreach(var spec in specs)
                {
                    if(catalog.Fits(spec,definition))
                    {
                        fitting=true;
                        maximum=Math.Max(maximum,PartsMarket.Count(spec,definition));
                    }
                    long fitNow=System.Diagnostics.Stopwatch.GetTimestamp();
                    if((fitNow-start)*1000.0/System.Diagnostics.Stopwatch.Frequency>=2.0)
                    {
                        yield return "parts/layout fitment";
                        start=System.Diagnostics.Stopwatch.GetTimestamp();
                    }
                }
                group.ids.Sort(StringComparer.Ordinal);
                groups.Add(new Group{
                    definition=definition,
                    ids=group.ids,
                    maximum=maximum,deliverable=fitting
                });
                long now=System.Diagnostics.Stopwatch.GetTimestamp();
                if((now-start)*1000.0/System.Diagnostics.Stopwatch.Frequency>=2.0)
                {
                    yield return "parts/layout group";
                    start=System.Diagnostics.Stopwatch.GetTimestamp();
                }
            }
            candidate.groups=groups.ToArray();
            }
            // A newer full layout may have been published during a suspended warmup.
            // Never overwrite that version with an older construction.
            var current=layouts.GetValue(catalog,_=>new Layout());
            if(Matches(current,catalog,keys)){complete(current);yield break;}
            current.Invalidate();
            layouts.Remove(catalog);
            layouts.Add(catalog,candidate);
            complete(candidate);
        }

        // Warm once on non-race frames, without making launch, reset or shop
        // navigation wait. The existing cold-path iterator is always available.
        internal static IEnumerable<object> WarmSteps(MechanicalCatalog catalog)
        {
            if(catalog==null)yield break;
            bool groupsReady=TryReadyShopProductGroups(catalog,out _);
            if(groupsReady&&TryReadyProductKeys(catalog,out var readyLayoutKeys)&&
                layouts.TryGetValue(catalog,out var readyLayout)&&Matches(readyLayout,catalog,readyLayoutKeys))yield break;
            var definitions=catalog.parts.ToArray();
            int revision=catalog.AuthoringRevision;
            Dictionary<string,string> keys;
            bool haveKeys=TryReadyProductKeys(catalog,out keys);
            if(haveKeys&&!groupsReady)
                haveKeys=readyKeys.TryGetValue(catalog,out var cached)&&ValidateReadyProductKeys(catalog,cached);
            if(!haveKeys)
            {
                keys=new Dictionary<string,string>(StringComparer.Ordinal);
                long start=System.Diagnostics.Stopwatch.GetTimestamp();
                foreach(var definition in definitions)
                {
                    keys.Add(definition.id,Key(definition));
                    long now=System.Diagnostics.Stopwatch.GetTimestamp();
                    if((now-start)*1000.0/System.Diagnostics.Stopwatch.Frequency>=2.0)
                    {
                        yield return "parts/layout warm keys";
                        start=System.Diagnostics.Stopwatch.GetTimestamp();
                    }
                }
                // A replaced authored catalog or override change cannot publish
                // keys derived from an earlier definition/revision.
                if(!PublishProductKeys(catalog,definitions,revision,keys))
                    yield break;
            }
            if(revision!=catalog.AuthoringRevision||
                !definitions.SequenceEqual(catalog.parts))yield break;
            if(!groupsReady)
            {
                if(!readyKeys.TryGetValue(catalog,out var groupingSnapshot)||
                    !ValidateReadyProductKeys(catalog,groupingSnapshot))yield break;
                var byKey=new Dictionary<string,List<string>>(StringComparer.Ordinal);
                var groupOrder=new List<string>();
                long groupStart=System.Diagnostics.Stopwatch.GetTimestamp();int groupedCount=0;
                foreach(var definition in definitions)
                {
                    string key=keys[definition.id];
                    if(!byKey.TryGetValue(key,out var ids)){ids=new List<string>();byKey.Add(key,ids);groupOrder.Add(key);}
                    ids.Add(definition.id);
                    if(++groupedCount>=512||(System.Diagnostics.Stopwatch.GetTimestamp()-groupStart)*1000.0/System.Diagnostics.Stopwatch.Frequency>=2.0)
                    {yield return "parts/shop grouping";groupedCount=0;groupStart=System.Diagnostics.Stopwatch.GetTimestamp();}
                }
                var shopGroups=new List<ReadyProductGroup>(groupOrder.Count);
                var groupById=new Dictionary<string,ReadyProductGroup>(StringComparer.Ordinal);
                groupStart=System.Diagnostics.Stopwatch.GetTimestamp();int publishedCount=0;
                foreach(var key in groupOrder)
                {
                    var product=new ReadyProductGroup(byKey[key].ToArray());shopGroups.Add(product);
                    foreach(var id in product.DefinitionIds)
                    {
                        groupById.Add(id,product);
                        if(++publishedCount>=512||(System.Diagnostics.Stopwatch.GetTimestamp()-groupStart)*1000.0/System.Diagnostics.Stopwatch.Frequency>=2.0)
                        {yield return "parts/shop group publish";publishedCount=0;groupStart=System.Diagnostics.Stopwatch.GetTimestamp();}
                    }
                }
                if(!ValidateReadyProductKeys(catalog,groupingSnapshot))yield break;
                var immutableGroupById=new ReadOnlyDictionary<string,ReadyProductGroup>(groupById);
                if(!PublishShopProductGroups(catalog,groupingSnapshot,Array.AsReadOnly(shopGroups.ToArray()),immutableGroupById,revision,definitions))yield break;
                yield return "parts/shop ready";
            }
            var models=catalog.parts.Where(d=>d.category==PartCategory.CylinderBlock)
                .Select(d=>d.id.Split('/')[0]).Distinct().OrderBy(s=>s,StringComparer.Ordinal).ToArray();
            var specs=models.Select(catalog.Spec).ToArray();
            foreach(var unused in GetSteps(catalog,keys,specs,_=>{}))yield return unused;
        }
    }
}

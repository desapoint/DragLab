using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
namespace DragLab.Domain.Mechanics
{
    // Authored product equivalence and maximum set sizes do not change with a stock epoch.
    internal static class MarketCatalogLayout
    {
        sealed class Product { public PartDefinition snapshot;public string key,json,jsonId; }
        internal sealed class Group {public PartDefinition definition;public List<string> ids;public int maximum;public bool deliverable;}
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
            foreach(var f in fields){var x=f.GetValue(a);var y=f.GetValue(b);if(ReferenceEquals(x,y))continue;if(f.FieldType.IsValueType||f.FieldType==typeof(string)){if(!Equals(x,y))return false;}else if(JsonUtility.ToJson(x)!=JsonUtility.ToJson(y))return false;}return true;
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

        // A cold layout is built privately and only published once all groups have
        // been fitted. Yield at a soft 2 ms budget to prevent the former ~300 ms
        // single-frame catalog layout spike. All work remains on Unity's main thread.
        internal static IEnumerable<object> GetSteps(MechanicalCatalog catalog,Dictionary<string,string> keys,
            VehicleSpec[] specs,Action<Layout> complete)
        {
            var existing=layouts.GetValue(catalog,_=>new Layout());
            if(Matches(existing,catalog,keys)){complete(existing);yield break;}
            var candidate=new Layout{
                ids=catalog.parts.Select(d=>d.id).ToArray(),
                keys=catalog.parts.Select(d=>keys[d.id]).ToArray()
            };
            // Preserve the authored equivalence grouping, ID ordering, and
            // per-platform maximum calculation, including every alias member.
            var source=catalog.parts.GroupBy(d=>keys[d.id],StringComparer.Ordinal)
                .OrderBy(g=>g.Min(d=>d.id),StringComparer.Ordinal).ToArray();
            // Keep the potentially expensive grouping separate from the first
            // fitting batch, especially on a cold player launch.
            yield return "parts/layout grouping";
            var groups=new List<Group>(source.Length);
            long start=System.Diagnostics.Stopwatch.GetTimestamp();
            foreach(var group in source)
            {
                var definition=group.OrderBy(d=>d.id,StringComparer.Ordinal).First();
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
                groups.Add(new Group{
                    definition=definition,
                    ids=group.Select(d=>d.id).OrderBy(id=>id,StringComparer.Ordinal).ToList(),
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
            var definitions=catalog.parts.ToArray();
            var keys=new Dictionary<string,string>(StringComparer.Ordinal);
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
            // An altered count/order must not publish a stale warmup. Any later
            // market refresh rebuilds using the current catalog and its own keys.
            if(!definitions.SequenceEqual(catalog.parts))yield break;
            var models=catalog.parts.Where(d=>d.category==PartCategory.CylinderBlock)
                .Select(d=>d.id.Split('/')[0]).Distinct().OrderBy(s=>s,StringComparer.Ordinal).ToArray();
            var specs=models.Select(catalog.Spec).ToArray();
            foreach(var unused in GetSteps(catalog,keys,specs,_=>{}))yield return unused;
        }
    }
}

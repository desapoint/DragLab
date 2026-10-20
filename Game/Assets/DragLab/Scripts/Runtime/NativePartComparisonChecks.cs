using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Runtime;
namespace DragLab.Runtime
{
    public static class NativePartComparisonChecks
    {
        static IEnumerable<NativeStatDefinition> Rows(IEnumerable<NativeStatGroup> groups)
        {foreach(var g in groups){foreach(var d in g.Definitions)yield return d;foreach(var d in Rows(g.Children))yield return d;}}
        public static void Run(VehicleDefinition[] vehicles,Action<bool,string> check)
        {
            var cat=new MechanicalCatalog(vehicles.Select(v=>v.spec));
            foreach(var category in cat.parts.Select(p=>p.category).Distinct()){
                var pair=(from candidate in cat.parts.Where(d=>d.category==category).OrderByDescending(d=>d.stage) from vehicle in vehicles where cat.Fits(cat.Spec(vehicle.spec.id),candidate) select new{part=candidate,v=vehicle}).FirstOrDefault();check(pair!=null,"Category has a real compatible test platform: "+category);var part=pair.part;var v=pair.v;
                var p=PlayerProfile.New(DragLab.Domain.RuntimeEntropy.UtcNow.Ticks);MechanicsOwnership.Ensure(p,cat);p.cash=1000000;p.reputation=1000;VehicleCollection.Buy(p,cat,new VehicleOffer{model=v.spec.id,price=1,stock=v.CreateStockTune(),paint="#FFFFFF"});
                var worn=p.componentsPart(category);if(worn!=null&&PartSemantics.Wears(category)){worn.condition.health=.35f;foreach(var e in worn.condition.elements)e.health=.35f;foreach(var tire in worn.condition.tires)tire.health=.35f;}
                string before=JsonUtility.ToJson(p);var a=NativePartBrowser.Inspect(InspectionBuild.Capture(p,p.SelectedCar));var b=NativePartBrowser.Inspect(InspectionBuild.Capture(p,p.SelectedCar,definitionId:part.id));
                var own=NativeStatCatalog.PartSpecific(a,b,new[]{category});var effects=NativeStatCatalog.PartEffects(a,b,new[]{category});var rows=Rows(effects).ToArray();
                check(own.Count==1&&own[0].Id=="PartChanges"&&own[0].Definitions.Any(d=>d.Id=="part:"+category+":hardware"),"Selected hardware has its own expanded change section: "+category);
                check(category==PartCategory.Wheels||category==PartCategory.Tires||!rows.Any(d=>d.Id=="wheelMass"||d.Id=="wheelInertia"||d.Id=="effectiveMass"||d.Id.StartsWith("axle:")),"Wheel-only specifications never leak into unrelated part comparisons: "+category);
                check(!effects.Any(g=>g.Id=="Components")&&rows.All(d=>!d.Id.StartsWith("part:")),"Unrelated component hardware is absent from general build effects: "+category);
                check(before==JsonUtility.ToJson(p),"Comparison selection and limit inspection are read-only: "+category);
                var domains=new Dictionary<string,PartCategory[]> {
                    {"brakeG",new[]{PartCategory.Brakes}},
                    {"flywheelInertia",new[]{PartCategory.Flywheel}},
                    {"crown",new[]{PartCategory.Pistons}},
                    {"compression",new[]{PartCategory.Pistons,PartCategory.CylinderBlock,PartCategory.HeadIntake}},
                    {"gearRatio:0",new[]{PartCategory.Gearbox}},
                    {"oilCapacity",new[]{PartCategory.OilPan,PartCategory.CylinderBlock}},
                    {"turboSize",new[]{PartCategory.Induction}},
                    {"camLift",new[]{PartCategory.Camshaft}},
                    {"springTravel",new[]{PartCategory.ValveSprings}},
                    {"tireCompound",new[]{PartCategory.Tires}},
                    {"coolingCap",new[]{PartCategory.CoolingCap}},
                    {"shiftLight",new[]{PartCategory.ShiftLight}}
                };
                foreach(var domain in domains)check(NativeStatCatalog.CanAffect(category,domain.Key)==domain.Value.Contains(category),"Explicit effect domain "+domain.Key+" belongs only to fitting part categories: "+category);
                foreach(var domain in domains.Where(kv=>kv.Value.Contains(category)))check(rows.Concat(Rows(own)).Any(d=>d.Id==domain.Key),"Relevant effect is visible for its part category: "+category+" / "+domain.Key);
                var all=Rows(NativeStatCatalog.Groups(a,b)).Where(d=>!d.Id.StartsWith("part:")&&!d.Id.StartsWith("warning:")&&!d.Id.StartsWith("component")&&d.Differs(new NativeStatsSnapshot(a),new NativeStatsSnapshot(b))).ToArray();
                foreach(var changed in all.Where(d=>d.Id!="effectiveMass"))check(NativeStatCatalog.CanAffect(category,changed.Id),"Actual build effect remains visible: "+category+" / "+changed.Id);

            }

            // The new independent induction cache must not change observable
            // vehicle inspections: worn and healthy curves, warnings and chassis
            // metrics are still computed from the current simulation state.
            if(vehicles.Length>0){
                var v=vehicles[0];
                var p=PlayerProfile.New(DragLab.Domain.RuntimeEntropy.UtcNow.Ticks);
                MechanicsOwnership.Ensure(p,cat);p.cash=1000000;p.reputation=1000;
                VehicleCollection.Buy(p,cat,new VehicleOffer{model=v.spec.id,price=1,stock=v.CreateStockTune(),paint="#FFFFFF"});
                var car=p.SelectedCar;var steady=new NativePartPreviewCache();
                string saved=JsonUtility.ToJson(p);
                void Equivalent(InspectionBuild source,string label){
                    var uncached=NativePartBrowser.Inspect(source);
                    var cached=NativePartBrowser.Inspect(source,steady);
                    check(uncached.curve.Count==cached.curve.Count&&uncached.curve.Zip(cached.curve,(a,b)=>a.rpm==b.rpm&&a.hp==b.hp&&a.torque==b.torque).All(equal=>equal)
                        &&uncached.peakHp==cached.peakHp&&uncached.peakTorque==cached.peakTorque&&uncached.boostBar==cached.boostBar
                        &&uncached.vehicleMassKg==cached.vehicleMassKg&&uncached.effectiveMassKg==cached.effectiveMassKg
                        &&uncached.tireGripFactor==cached.tireGripFactor&&uncached.brakeG==cached.brakeG
                        &&uncached.condition==cached.condition
                        &&uncached.warnings.Select(w=>w.title+"|"+w.explanation).SequenceEqual(cached.warnings.Select(w=>w.title+"|"+w.explanation)),
                        "Independent steady output is bit-identical to uncached inspection: "+label);
                }
                Equivalent(InspectionBuild.Capture(p,car),"applied");
                int hits=steady.SteadyHits;
                Equivalent(InspectionBuild.Capture(p,car),"repeated applied");
                check(steady.SteadyHits>hits,"Identical engine inputs reuse cached steady samples");
                var tire=p.componentsPart(PartCategory.Tires);
                if(tire!=null){
                    foreach(var wheel in tire.condition.tires)wheel.health=.43f;
                    hits=steady.SteadyHits;
                    Equivalent(InspectionBuild.Capture(p,car),"worn tires");
                    check(steady.SteadyHits>hits,"Tire condition changes reuse independent engine output");
                }
                var brakes=cat.parts.FirstOrDefault(part=>part.category==PartCategory.Brakes&&cat.Fits(cat.Spec(car.vehicleId),part));
                if(brakes!=null){
                    hits=steady.SteadyHits;
                    Equivalent(InspectionBuild.Capture(p,car,definitionId:brakes.id),"alternate brakes");
                    check(steady.SteadyHits>hits,"Brake hardware comparisons retain independent engine output");
                }
                var massTune=car.tune.Copy();massTune.driverMassKg+=20;massTune.tirePressurePsi+=1;
                hits=steady.SteadyHits;
                Equivalent(InspectionBuild.Capture(p,car,massTune),"driver mass and pressure");
                check(steady.SteadyHits>hits,"Mass and tire-pressure tuning reuse independent engine output");
                var altered=InspectionBuild.Capture(p,car);
                altered.build.spec.torque[0].torqueNm+=15;
                int misses=steady.SteadyMisses;
                Equivalent(altered,"authored engine torque changed");
                check(steady.SteadyMisses>misses,"Changing the engine torque map invalidates steady samples");
                check(JsonUtility.ToJson(p)==saved || tire!=null,"Preview calculations do not mutate an otherwise unchanged profile");
            }
            foreach(var v in vehicles){
                var p=PlayerProfile.New(DragLab.Domain.RuntimeEntropy.UtcNow.Ticks);MechanicsOwnership.Ensure(p,cat);p.cash=1000000;p.reputation=1000;VehicleCollection.Buy(p,cat,new VehicleOffer{model=v.spec.id,price=1,stock=v.CreateStockTune(),paint="#FFFFFF"});
                var car=p.SelectedCar;var map=MechanicsOwnership.Definitions(p,car);var pistons=cat.Find(map[PartCategory.Pistons]);var rods=cat.Find(map[PartCategory.Rods]);var crank=cat.Find(map[PartCategory.Crankshaft]);
                float pr=pistons.rpmLimit,rr=rods.rpmLimit,cr=crank.rpmLimit,pt=pistons.torqueLimitNm,rt=rods.torqueLimitNm,ct=crank.torqueLimitNm;
                try{
                    pistons.rpmLimit=20000;rods.rpmLimit=1000;crank.rpmLimit=15000;pistons.torqueLimitNm=700;rods.torqueLimitNm=450;crank.torqueLimitNm=620;
                    p.parts.First(i=>i.instanceId==car.installed.slots.First(s=>s.slot=="Rods").partInstanceId).condition.health=.5f;
                    var a=NativePartBrowser.Inspect(InspectionBuild.Capture(p,car));
                    check(a.safeOperatingRpm==1000&&Math.Abs(a.engineTorqueHeadroomNm-303.75)<.001,"Complete-engine limits use the minimum comparable internal ratings and actual condition: "+v.spec.id);
                    var spring=p.parts.First(i=>i.instanceId==car.installed.slots.First(s=>s.slot=="ValveSprings").partInstanceId);spring.condition.elements[0].health=.3f;spring.condition.elements[0].fault=ElementFault.WeakSpring;spring.condition.elements[0].severity=.6f;rods.rpmLimit=14000;
                    var b=NativePartBrowser.Inspect(InspectionBuild.Capture(p,car));var spec=b.source.build.spec;double expected=spec.valvetrain.springSafeRpm*.72*.61/Math.Max(1,spec.valvetrain.profileDemandRpm/spec.redlineRpm);
                    check(Math.Abs(b.safeOperatingRpm-expected)<.02&&b.rpmLimitPart==cat.Find(map[PartCategory.ValveSprings]).name,"Weak individual spring and installed cam demand can set the lowest safe RPM: "+v.spec.id);
                    var changed=NativePartBrowser.Inspect(InspectionBuild.Capture(p,car,definitionId:cat.Id(v.spec.id,PartCategory.Pistons,1)));
                    check(changed.safeOperatingRpm<=b.safeOperatingRpm+.02,"Upgrading a non-limiting internal does not falsely raise the full-engine RPM ceiling: "+v.spec.id);
                }finally{pistons.rpmLimit=pr;rods.rpmLimit=rr;crank.rpmLimit=cr;pistons.torqueLimitNm=pt;rods.torqueLimitNm=rt;crank.torqueLimitNm=ct;}
            }

        }
    }
}

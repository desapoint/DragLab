using System;
using System.Collections.Generic;
using System.Linq;
namespace DragLab.Domain.Mechanics
{
    public static class ServiceHardware
    {
        public static bool IsCategory(PartCategory c){return c>=PartCategory.Camshaft;}
        public static bool Optional(PartCategory c){return c==PartCategory.GlowPlugs||c==PartCategory.OilCooler||c==PartCategory.CoolingCap||c==PartCategory.CoolingHoses||c==PartCategory.CoolantPump;}
        public static bool Wears(PartCategory c){return IsCategory(c)&&c!=PartCategory.OilPan;}
        public static bool CoreFailure(PartCategory c){return c==PartCategory.Camshaft||c==PartCategory.Valves||c==PartCategory.TimingDrive||c==PartCategory.CylinderBlock||c==PartCategory.Bearings;}
        public static void Add(MechanicalCatalog catalog,VehicleSpec spec)
        {
            foreach(PartCategory c in Enum.GetValues(typeof(PartCategory)).Cast<PartCategory>().Where(IsCategory))for(int n=0;n<(c==PartCategory.Camshaft?3:2);n++) {
                bool upgraded=n>0;var p=new PartDefinition{id=catalog.Id(spec.id,c,n),category=c,stage=n,fitment=MechanicalCatalog.Interface(spec,c),quality=upgraded?"Aftermarket":"OEM",price=upgraded?700:200,massKg=1,positionFromFrontAxleM=spec.wheelbaseM*.15f,heightM=.4f,thermalLimitC=170,heatCapacityJPerC=10000,coolingWattsPerC=15,wearEnergyJ=3000000000f,overloadSeconds=60};
                p.fuelKind=spec.combustion.fuelKind;
                p.followerInterface=spec.engineBehavior.fueling==FuelSystem.Carburetor?"flat-tappet":"roller";
                if(CombustionHardware.Diesel(spec)&&c!=PartCategory.GlowPlugs)p.wearEnergyJ*=1.25f;
                string label="";
                switch(c){
                    case PartCategory.SparkPlugs:label=upgraded?"iridium spark-plug set":"copper spark-plug set";p.massKg=.25f;p.price=upgraded?120:35;p.wearEnergyJ=upgraded?9000000000f:3000000000f;p.description="Wearing spark plugs; replace a complete matched set or one cylinder through individual service. Wear/fouling weakens combustion and wastes fuel; coils remain separate.";break;
                    case PartCategory.GlowPlugs:label=upgraded?"fast-heating glow-plug set":"standard glow-plug set";p.massKg=.3f;p.price=upgraded?180:90;p.description="Diesel cold-start heaters; not spark ignition and not required to keep a warm engine firing. Individual heater wear affects cold operation.";break;
                    case PartCategory.Camshaft:
                        label=new[]{"factory camshaft set","street performance camshaft set","race camshaft set"}[n];p.camLiftMm=8+n*1.5f;p.camDurationDegrees=200+n*22;p.camOverlapDegrees=10+n*20;p.camLobeSeparation=114-n*3;p.camTorqueBias=n==0?0:n==1?.07f:.16f;p.valvetrainRpm=spec.redlineRpm*(1+n*.06f);p.massKg=4;
                        p.description="Complete cam set matched to this engine family. Lift/duration/overlap reshape cylinder filling: high-RPM gain trades low-RPM torque, idle quality and fuel efficiency. Requires appropriate springs and followers; no free universal power multiplier.";break;
                    case PartCategory.ValveSprings:label=upgraded?"performance valve spring/retainer set":"factory valve spring/retainer set";p.valvetrainRpm=spec.redlineRpm*(upgraded?1.30f:1.08f);p.description="Controls valve motion. Safe speed depends on cam demand and wear; insufficient spring control causes valve float, then potential valve damage. Strength adds no engine torque.";break;
                    case PartCategory.Valves:label=upgraded?"heat-resistant valve/guide set":"factory valve/guide set";p.massKg=2;p.description="Sealing loss reduces compression and efficiency. Damaged valves may require replacement; guides can be reconditioned within limits.";break;
                    case PartCategory.Followers:label=upgraded?"roller follower/lifter and rocker set":"factory follower/lifter and rocker set";p.description="Engine-family matched actuation set (pushrod or overhead-cam as applicable). Wear and clearance errors reduce effective valve lift; adjustable clearance can be serviced.";break;
                    case PartCategory.TimingDrive:label=upgraded?"adjustable timing drive/cam gear set":"factory fixed timing drive";p.camTimingAdjustable=upgraded;p.tuning=upgraded?PartTuningOptions.CamTiming:PartTuningOptions.None;p.massKg=2;p.description="Maintains valve timing. Adjustable gears unlock +/-6 crank degrees of cam timing. Excessive wear/timing error changes output; complete breakage can damage this modeled interference engine.";break;
                    case PartCategory.PistonRings:label=upgraded?"performance piston ring set":"factory piston ring set";p.description="Cylinder sealing and oil control. Worn rings reduce compression and increase oil burn. Re-ring service requires a serviceable cylinder bore; forged pistons do not repair worn rings.";break;
                    case PartCategory.CylinderBlock:
                        label=upgraded?"reinforced serviced cylinder block":"factory cylinder block/liners";p.massKg=55;p.price=upgraded?1800:1100;
                        // Conservative game calibration: factory blocks retain the
                        // previously supported forged-core envelope; reinforcement
                        // adds headroom, not power or restored condition.
                        p.rpmLimit=spec.redlineRpm*(upgraded?1.4f:1.2f);
                        p.torqueLimitNm=spec.torque.Max(t=>t.torqueNm)*(upgraded?3.5f:2.6f);
                        p.description="Bore geometry, sealing and torque/RPM strength. Wear reduces compression and load headroom; overload or overspeed damages the block. Reinforcement adds strength, not horsepower. Reconditioning is possible only above the service limit.";break;
                    case PartCategory.Bearings:label=upgraded?"performance crank/rod bearing set":"factory crank/rod bearing set";p.description="Worn clearances reduce oil pressure and increase friction. Oil starvation accelerates damage; a seized bearing cannot be healed by topping up.";break;
                    case PartCategory.ValveSeals:label=upgraded?"high-temperature valve stem seals":"factory valve stem seals";p.massKg=.1f;p.description="Controls oil entering through valve guides. Worn seals increase oil consumption without requiring replacement of the whole engine.";break;
                    case PartCategory.OilPan:label=upgraded?"baffled extended oil pan":"factory oil pan";p.massKg=upgraded?4.5f:3;p.fluidCapacityLitres=spec.fluids.oilCapacityLitres+(upgraded?2:0);p.fluidMinimumLitres=spec.fluids.oilMinimumLitres;p.baffling=upgraded?.9f:.3f;p.description="Capacity is total engine fill with this pan, including filter/circuit. Extra oil adds weight and thermal capacity; baffling protects pickup under drag acceleration. No automatic horsepower bonus.";break;
                    case PartCategory.OilPump:label=upgraded?"high-flow oil pump":"factory oil pump";p.pumpFlowScale=upgraded?1.3f:1;p.description="Provides oil circulation/pressure, subject to oil level, temperature and bearing leakage. More flow does not repair worn bearings.";break;
                    case PartCategory.OilFilter:label=upgraded?"extended-life oil filter":"factory oil filter";p.price=upgraded?80:25;p.description="Deposits progressively restrict oil flow. Cleaning/service restores restriction, not unrelated engine wear; oil/filter changes retain engine damage.";break;
                    case PartCategory.CoolantPump:label=upgraded?"high-flow coolant pump":"factory coolant pump";p.pumpFlowScale=upgraded?1.25f:1;p.description="Changes coolant circulation, which depends on level and pump condition. Removing it permits unsafe short operation with minimal circulation.";break;
                    case PartCategory.CoolingCap:label=upgraded?"high-pressure recovery-system cap":"factory pressure/recovery cap";p.capPressureBar=upgraded?1.4f:1.1f;p.price=upgraded?100:50;p.massKg=.1f;p.description="Controls circuit pressure/boiling resistance and coolant recovery. Excess pressure can worsen an existing leak; a higher rating does not fix the radiator.";break;
                    case PartCategory.CoolingHoses:label=upgraded?"reinforced cooling hose/seal set":"factory cooling hose/seal set";p.massKg=1.5f;p.description="Contains coolant. Wear/seal damage can leak under pressure or while parked. Resealing fixes a serviceable leak; topping up only replaces lost coolant.";break;
                    case PartCategory.OilCooler:label=upgraded?"competition oil cooler":"street oil cooler";p.coolingWattsPerC=upgraded?100:50;p.massKg=upgraded?3:2;p.description="Optional heat rejection from engine oil to ambient air. Improves hot-oil control, adds weight; it cannot repair low oil, leaks or existing engine damage.";break;
                }
                if(CombustionHardware.Diesel(spec)&&c==PartCategory.Camshaft){p.camLiftMm=8+n;p.camOverlapDegrees=5+n*10;p.camDurationDegrees=190+n*14;}
                if(c==PartCategory.ValveSprings)p.springMaxLiftMm=upgraded?14.5f:9.2f;
                if(upgraded){p.wearEnergyJ*=1.5f;p.thermalLimitC+=15;}
                p.name=spec.displayName+" / "+label;catalog.parts.Add(p);
            }
        }
        public static void Apply(PartDefinition p,VehicleSpec spec,Tune tune)
        {
            var v=spec.valvetrain;var f=spec.fluids;
            if(p.category==PartCategory.Camshaft){v.liftMm=p.camLiftMm;v.durationAt050Degrees=p.camDurationDegrees;v.overlapDegrees=p.camOverlapDegrees;v.lobeSeparationDegrees=p.camLobeSeparation;v.camTorqueBias=p.camTorqueBias;v.profileDemandRpm=p.valvetrainRpm;if(p.stage>0)spec.engineBehavior.camOverlap=Math.Min(1,Math.Max(spec.engineBehavior.camOverlap+p.camTorqueBias*.8f,p.camOverlapDegrees/90f));}
            if(p.category==PartCategory.ValveSprings){v.springSafeRpm=p.valvetrainRpm;v.springMaxLiftMm=p.springMaxLiftMm;}
            if(p.category==PartCategory.TimingDrive)v.timingAdjustable=p.camTimingAdjustable;
            if(p.category==PartCategory.OilPan){f.oilCapacityLitres=p.fluidCapacityLitres;f.oilMinimumLitres=p.fluidMinimumLitres;f.oilPanBaffling=p.baffling;}
            if(p.category==PartCategory.OilPump)f.oilPumpScale=p.pumpFlowScale;
            if(p.category==PartCategory.CoolantPump)f.pumpFlowScale=p.pumpFlowScale;
            if(p.category==PartCategory.CoolingCap)f.capPressureBar=p.capPressureBar;
            if(p.category==PartCategory.OilCooler)f.oilCoolerWattsPerC=p.coolingWattsPerC;
            if(p.category==PartCategory.Cooling && p.fluidCapacityLitres>0)f.coolantCapacityLitres=p.fluidCapacityLitres;
        }
        public static IEnumerable<PartStat> Stats(PartDefinition p)
        {
            if(p.category==PartCategory.ValveSprings)yield return new PartStat("springlift","Maximum spring travel",p.springMaxLiftMm,"mm",1,"Cam lift beyond this travel causes coil bind; spring RPM rating alone is insufficient.");
            if(p.category==PartCategory.OilCooler)yield return new PartStat("oilcooling","Oil heat rejection",p.coolingWattsPerC,"W/°C",1,"Extra cooling helps sustained load, with fluid quantity and airflow still required.");
            if(p.category==PartCategory.OilFilter)yield return new PartStat("filtercapacity","Contamination capacity",p.wearEnergyJ/3000000000,"×",1,"Slows deposit buildup; a used filter is retained when replaced.");
            if(ServiceHardware.Wears(p.category))yield return new PartStat("servicelife","Service-life resistance",p.wearEnergyJ/3000000000,"×",1,"Relative deterministic wear resistance under equal usage; heat/starvation can still damage it.");
            if(p.category==PartCategory.Camshaft){yield return new PartStat("camlift","Valve lift",p.camLiftMm,"mm",0,"More lift needs matched springs and valve clearance.");yield return new PartStat("duration","Duration at 0.050 inch lift",p.camDurationDegrees,"crank °",0,"Compare at the same measuring lift. Larger duration shifts useful RPM upward.");yield return new PartStat("overlap","Valve overlap",p.camOverlapDegrees,"crank °",0,"Changes idle, reversion and cylinder filling; larger is not always better.");yield return new PartStat("lsa","Lobe separation",p.camLobeSeparation,"cam °",0,"Physical cam profile, not a free tuning slider.");}
            if(p.valvetrainRpm>0)yield return new PartStat("valvetrainrpm",p.category==PartCategory.Camshaft?"Required spring control rating":"Valvetrain control rating",p.valvetrainRpm,"RPM",p.category==PartCategory.Camshaft?-1:1,"Spring rating is cam-dependent, separate from internal engine strength.");
            if(p.fluidCapacityLitres>0)yield return new PartStat("fluidcapacity","Total system fill capacity",p.fluidCapacityLitres,"L",0,"Larger fill adds fluid mass and thermal capacity; does not refill existing quantities.");
            if(p.fluidMinimumLitres>0)yield return new PartStat("fluidminimum","Minimum oil quantity",p.fluidMinimumLitres,"L",0,"Below this quantity, pickup and pressure become increasingly unreliable.");
            if(p.category==PartCategory.OilPan)yield return new PartStat("baffling","Oil pickup baffling",p.baffling*100,"%",1,"Reduces acceleration-induced starvation at low oil quantity.");
            if(p.category==PartCategory.OilPump||p.category==PartCategory.CoolantPump)yield return new PartStat("pumpflow","Pump flow capability",p.pumpFlowScale,"×",1,"Subject to wear, fluid quantity and restrictions.");
            if(p.category==PartCategory.CoolingCap)yield return new PartStat("cappressure","Cap pressure rating",p.capPressureBar,"bar",0,"Raises modeled boiling resistance, but can increase leakage.");
        }
    }
}

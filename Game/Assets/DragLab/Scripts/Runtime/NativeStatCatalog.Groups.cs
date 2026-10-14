using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Runtime
{
    internal static partial class NativeStatCatalog
    {
        static NativeStatGroup Group(string id,string title,params NativeStatDefinition[] rows)
            =>new NativeStatGroup(id,title,rows) {InitiallyCollapsed=true};
        static string Nice(object value)=>CultureInfo.CurrentCulture.TextInfo.ToTitleCase(PartSemantics.Friendly(value.ToString()));
        static string State(bool value)=>value?"On":"Off";
        static bool Friction(NativeStatsSnapshot s)=>s.Transmission.type==TransmissionType.Manual||s.Transmission.type==TransmissionType.DCT;
        static bool Converter(NativeStatsSnapshot s)=>!Friction(s);
        static string Differential(NativeStatsSnapshot s,bool front)
        {
            if(front&&s.Spec.drive==DriveType.RWD||!front&&s.Spec.drive==DriveType.FWD)return "—";
            var slot=front?PartCategory.FrontDifferential:PartCategory.RearDifferential;
            if(!s.Has(slot)&&!(s.Spec.drive!=DriveType.AWD&&s.Has(PartCategory.Differential)))return "Missing";
            return Nice((front?s.Spec.frontDifferential:s.Spec.rearDifferential).kind);
        }
        static string WheelDescription(NativeStatsSnapshot s){var parts=s.Spec.cornerWheelDefinitions;return parts!=null&&parts.Length==4?(parts.Select(p=>p.id).Distinct().Count()>1?"Mixed wheels":parts[0].name):s.Definition(PartCategory.Wheels)?.name??"Missing";}
        static string TireDescription(NativeStatsSnapshot s){var parts=s.Spec.cornerTireDefinitions;return parts!=null&&parts.Length==4?(parts.Select(p=>p.id).Distinct().Count()>1?"Mixed tires":parts[0].name):s.Definition(PartCategory.Tires)?.name??"Missing";}
        static readonly NativeStatGroup InductionGroup=Group("Induction","Induction / exhaust",
            T("inductionType","Induction",s=>Nice(s.Induction.type),"Effective installed induction topology. Twincharging combines turbo and supercharger operation; removing the kit returns naturally aspirated operation."),
            N("turboCount","Turbochargers",s=>s.Induction.turboCount,"","F0",0,2,"Number of installed turbo units, not the number of exhaust outlets."),
            N("superchargerCount","Superchargers",s=>s.Induction.superchargerCount,"","F0",0,2,"Number of installed mechanically driven compressor units."),
            N("boostBar","Settled boost",s=>s.Inspection.boostBar,"bar","F2",0,3,"Maximum modeled full-load gauge boost on the settled dyno. Launch preload, transient lag and cut events can produce different live boost."),
            N("turboSize","Turbo size / unit",s=>s.Induction.turboCount>0?(double?)s.Induction.size:null,"× reference","F2",.5,2,"Relative hardware size per turbo. Larger units can support airflow but take longer to spool; it is not a physical diameter."),
            N("turboSpool","Turbo response constant",s=>s.Induction.turboCount>0?(double?)s.Inspection.turboSpoolSeconds:null,"s","F2",0,4,"Modeled rotor response constant including hardware size and twin-turbo factor. Actual boost arrival also depends on RPM, throttle, exhaust energy and load.",-1),
            N("pulleyRatio","Blower drive ratio",s=>s.Induction.superchargerCount>0?(double?)s.Induction.pulley:null,"× reference","F2",.5,2,"Effective supercharger pulley ratio after installed tuning permissions. More boost also consumes engine power."),
            T("blowOffValve","Pressure release valve",s=>s.Induction.turboCount>0?Nice(s.Induction.valve):"—","Effective turbo release-valve configuration. Recirculation retains more rotor energy; atmospheric release changes the sound and pressure decay."),
            N("intercooler","Intercooler effectiveness",s=>s.Induction.turboCount+s.Induction.superchargerCount>0?(double?)(s.Spec.induction.intercoolerEffectiveness*100):null,"%","F0",0,100,"Fraction of modeled charge heat removed by the induction system; this is separate from engine cooling.",1),
            N("turboFlow","Turbo flow / unit",s=>s.Induction.turboCount>0?(double?)s.Spec.induction.turboUnitFlowKgS:null,"kg/s","F3",0,2,"Authored per-unit compressor airflow before the modeled hardware-size envelope."),
            N("turboEfficiency","Turbo compressor efficiency",s=>s.Induction.turboCount>0?(double?)(s.Spec.induction.turboEfficiency*100):null,"%","F0",0,100,"Authored compressor efficiency influencing charge heat and parasitic loss.",1),
            N("blowerFlow","Blower flow / unit",s=>s.Induction.superchargerCount>0?(double?)s.Spec.induction.superchargerUnitFlowKgS:null,"kg/s","F3",0,2,"Authored airflow per installed supercharger before pulley scaling."),
            N("blowerEfficiency","Blower compressor efficiency",s=>s.Induction.superchargerCount>0?(double?)(s.Spec.induction.superchargerEfficiency*100):null,"%","F0",0,100,"Authored mechanically driven compressor efficiency.",1),
            N("boostLimit","Hardware boost ceiling",s=>s.Induction.turboCount+s.Induction.superchargerCount>0?(double?)s.Spec.induction.maxBoostBar:null,"bar","F2",0,3,"Installed induction pressure ceiling; the applied tune and current operating state may produce less."),
            T("exhaustPath","Exhaust path",s=>!s.Has(PartCategory.Exhaust)?"Removed / open":s.Spec.sound.exhaustSystem.parts.Length==0?"Open":string.Join(" / ",s.Spec.sound.exhaustSystem.parts.Select(p=>Nice(p.kind)).Distinct()),"Installed acoustic path. Expand Exhaust sections for continuous silencer, catalyst and resonator settings; these are not boolean presence controls.")
        );
        static readonly NativeStatGroup TransmissionGroup=Group("Transmission","Transmission / drivetrain",
            T("transmission","Gearbox",s=>s.Inspection.transmissionName,"Effective installed gearbox including H-pattern versus sequential manual selection. Driver shifting assists do not change this hardware."),
            T("driveType","Driven axles",s=>s.Spec.drive.ToString(),"FWD drives the front axle, RWD the rear, and AWD follows its installed center/coupling topology."),
            T("downshiftProtection","Downshift protection",s=>State(s.Transmission.ProtectsDownshift),"Hardware protection against unsafe downshifts. A manual without an interlock permits a mechanical overrev even with an electronic limiter."),
            N("finalDrive","Final drive",s=>s.Tune.finalDrive,":1","F2",2,6,"Applied final-drive ratio. A higher number increases wheel leverage but reduces speed per engine revolution."),
            N("shiftSeconds","Shift handover",s=>s.Transmission.type!=TransmissionType.CVT?(double?)s.Inspection.shiftSeconds:null,"s","F3",0,1,"Calculated handover duration for this gearbox and tune, including sequential selector speed. A CVT changes ratio continuously.",-1),
            N("shiftRetention","Handover torque retention",s=>s.Transmission.type==TransmissionType.DCT||s.Transmission.type==TransmissionType.Automatic?(double?)(TransmissionDynamics.Retention(s.Transmission)*100):null,"%","F0",0,100,"Authored retention during DCT/automatic handover before capacity, slip and other live losses; not guaranteed tire torque."),
            T("clutchType","Clutch",s=>Friction(s)&&s.Definition(PartCategory.Clutch)!=null?Nice(s.Definition(PartCategory.Clutch).clutchEnvironment)+" / "+Nice(s.Definition(PartCategory.Clutch).clutchMaterial)+" / "+Nice(s.Definition(PartCategory.Clutch).clutchActuation):Friction(s)?"Missing":"Torque converter","Friction clutch construction applies to manual/DCT hardware. Automatic/CVT hydraulic coupling follows converter behavior instead."),
            N("clutchCapacity","Effective clutch capacity",s=>Friction(s)&&s.Has(PartCategory.Clutch)?(double?)s.Inspection.clutchCapacityNm:null,"Nm","F0",0,2000,"Installed friction-clutch capacity with captured condition/temperature. Slipping limits transmitted torque and converts lost work to heat.",1),
            N("converterStall","Converter stall rating",s=>Converter(s)?(double?)s.Transmission.converterStallRpm:null,"RPM","F0",0,7000,"Authored converter stall calibration; actual live stall is also limited by the effective engine limiter and load."),
            N("converterMultiply","Converter multiplication",s=>Converter(s)?(double?)s.Transmission.converterMultiplication:null,"×","F2",1,3,"Low-speed converter torque multiplication, which decreases as turbine speed approaches pump speed."),
            N("converterLock","Converter lock speed",s=>Converter(s)?(double?)s.Transmission.converterLockSpeedMps:null,"m/s","F1",0,50,"Authored lock-up speed; throttle, handover and hysteresis also govern live lock-up."),
            N("cvtMinimum","CVT tallest ratio",s=>s.Transmission.type==TransmissionType.CVT?(double?)s.Transmission.cvtMinRatio:null,":1","F2",.3,6,"Lowest CVT ratio gives the greatest theoretical speed per engine revolution."),
            N("cvtMaximum","CVT shortest ratio",s=>s.Transmission.type==TransmissionType.CVT?(double?)s.Transmission.cvtMaxRatio:null,":1","F2",.3,6,"Highest CVT ratio gives launch leverage. It does not represent a numbered gear."),
            N("cvtTarget","CVT target",s=>s.Transmission.type==TransmissionType.CVT?(double?)Math.Min(s.Tune.shiftRpm,s.Transmission.cvtTargetRpm):null,"RPM","F0",0,10000,"Effective CVT working target bounded by the saved shift target; road speed and ratio range still constrain actual RPM."),
            T("frontDifferential","Front differential",s=>Differential(s,true),"Open follows the weaker side's resistance; limited-slip adds bounded bias/preload; welded/spool locks the two wheel paths. Detailed ratings are in Components."),
            T("rearDifferential","Rear differential",s=>Differential(s,false),"Open, limited-slip, welded and spool hardware allocate wheel torque differently. This describes installed hardware, not a generic grip bonus."),
            T("awdMode","AWD coupling",s=>s.Spec.drive==DriveType.AWD?Nice(s.Awd.mode):"—","Effective fixed/open/locked/on-demand/active/limited-slip center configuration, including applied hardware permissions."),
            N("awdFrontBias","AWD front target",s=>s.Spec.drive==DriveType.AWD?(double?)(s.Awd.frontBias*100):null,"%","F0",0,100,"Configured front share. On-demand and active systems vary live allocation; an open center cannot force this share through a disconnected low-resistance branch."),
            N("centerSpeedRatio","Locked center speed ratio",s=>s.Spec.drive==DriveType.AWD&&s.Awd.mode==AwdMode.LockedCenter?(double?)s.Awd.LockedRatio:null,":1","F3",.5,2,"Front wheel speed / rear wheel speed, separate from torque bias. Match rear radius / front radius to avoid straight-line scrub."),
            T("awdPrimary","On-demand primary axle",s=>s.Spec.drive==DriveType.AWD&&s.Awd.mode==AwdMode.OnDemand?s.Awd.primaryFront?"Front":"Rear":"—","On-demand drive starts at the primary axle; secondary capacity and response determine engagement."),
            N("awdMinimumFront","Minimum front share",s=>s.Spec.drive==DriveType.AWD&&s.Awd.mode==AwdMode.ActiveBias?(double?)(s.Awd.minimumFront*100):null,"%","F0",0,100,"Lower front-allocation bound for active-bias AWD."),
            N("awdMaximumFront","Maximum front share",s=>s.Spec.drive==DriveType.AWD&&s.Awd.mode==AwdMode.ActiveBias?(double?)(s.Awd.maximumFront*100):null,"%","F0",0,100,"Upper front-allocation bound for active-bias AWD."),
            N("awdSecondary","Secondary-axle ceiling",s=>s.Spec.drive==DriveType.AWD&&s.Awd.mode==AwdMode.OnDemand?(double?)(s.Awd.maximumSecondary*100):null,"%","F0",0,100,"Maximum available secondary-axle torque share in on-demand operation."),
            N("awdResponse","AWD response constant",s=>s.Spec.drive==DriveType.AWD?(double?)s.Awd.responseSeconds:null,"s","F2",0,5,"Configured coupling response constant; only responsive coupling topologies use it dynamically."),
            N("awdSlipThreshold","AWD slip trigger",s=>s.Spec.drive==DriveType.AWD&&s.Awd.mode==AwdMode.OnDemand?(double?)(s.Awd.slipThreshold*100):null,"%","F1",0,50,"Primary axle slip threshold for on-demand engagement."),
            N("awdPreload","On-demand preload",s=>s.Spec.drive==DriveType.AWD&&s.Awd.mode==AwdMode.OnDemand?(double?)(s.Awd.preload*100):null,"%","F0",0,100,"Configured baseline engagement before the slip trigger."),
            N("centerBias","Center torque bias",s=>s.Spec.drive==DriveType.AWD&&s.Awd.mode==AwdMode.LimitedSlipCenter?(double?)s.Awd.torqueBiasRatio:null,":1","F2",1,10,"Limited-slip center's bounded torque-bias ratio. It cannot supply unlimited torque to a healthy axle if the other path has no resistance."),
            N("centerPreload","Center preload",s=>s.Spec.drive==DriveType.AWD&&s.Awd.mode==AwdMode.LimitedSlipCenter?(double?)s.Awd.centerPreloadNm:null,"Nm","F0",0,1000,"Limited-slip center coupling torque available at low branch resistance."),
            T("awdIsolation","Failed-axle isolation",s=>s.Spec.drive==DriveType.AWD?State(s.Awd.isolateFailedAxle):"—","Whether installed coupling hardware can isolate a failed branch. Open-center resistance and surviving-axle availability still determine drive."),
            N("brakeG","Effective brake rating",s=>s.Inspection.brakeG,"g","F2",0,2,"Captured stopping capacity before tire-grip limits. Brake failures do not automatically disable the engine.",1)
        );
        static readonly NativeStatGroup TiresGroup=Group("Tires","Tires / wheels",
            T("tireCompound","Tire compound",s=>TireDescription(s),"Installed tire assembly; individual corner specs appear in Components. Compound grip and rolling/sliding lifespan are independent properties."),
            N("wheelRadius","Driven rolling radius",s=>s.Has(PartCategory.Tires)&&s.Has(PartCategory.Wheels)?(double?)s.Inspection.wheelRadiusIn:null,"in","F2",8,24,"External tire radius, including sidewall. This is not the rim diameter. A taller tire changes gearing and rotating load."),
            N("wheelDiameter","Driven outer diameter",s=>s.Has(PartCategory.Tires)&&s.Has(PartCategory.Wheels)?(double?)s.Inspection.wheelDiameterIn:null,"in","F2",16,48,"Twice the external radius, not a wheel-rim size."),
            N("wheelWidth","Legacy all-wheel width",s=>s.Has(PartCategory.Tires)?(double?)s.Tune.WheelWidth:null,"× reference","F2",.5,2,"Applied width relative to this model's reference. The current model has no authored absolute section width, so millimeters are not invented."),
            N("tirePressure","Legacy all-wheel pressure",s=>s.Has(PartCategory.Tires)?(double?)(s.Tune.tirePressurePsi/DisplayUnits.PsiPerBar):null,"bar","F2",0,60/DisplayUnits.PsiPerBar,"Applied tire pressure in the selected pressure unit. Grip/life depend on pressure; higher is not always better."),
            N("wheelMass","Average assembly mass / wheel",s=>s.Has(PartCategory.Wheels)&&s.Has(PartCategory.Tires)?(double?)s.Inspection.wheelMassKg:null,"kg","F2",0,60,"Mass of one modeled wheel plus tire after geometry/material scaling. Four assemblies contribute to vehicle weight.",-1),
            N("wheelInertia","All-wheel inertia",s=>s.Inspection.wheelInertia,"kg m²","F2",0,30,"Total resistance to changing wheel rotation. Inertia affects acceleration and synchronization but is not an additional gravitational mass."),
            N("tireGrip","Lowest captured compound grip",s=>s.Has(PartCategory.Tires)?(double?)s.Inspection.tireGripFactor:null,"×","F2",0,3,"Installed compound friction factor after captured tire condition/temperature; live grip also depends on contact, load, pressure and width.",1),
            N("tireHealth","Lowest tire condition",s=>s.Has(PartCategory.Tires)?(double?)(s.Inspection.lowestTireHealth*100):null,"%","F0",0,100,"Worst individual tire condition, not average life. Expand Components → Tires for each corner's condition, heat and sliding work.",1),
            N("rollingLife","Lowest reference rolling life",s=>s.Has(PartCategory.Tires)?(double?)s.Inspection.tireRollingLifeKm:null,"km","F0",0,80000,"Authored life at reference load, not remaining mileage. Pressure, load and driving behavior alter wear.",1),
            N("slidingLife","Lowest sliding-work budget",s=>s.Has(PartCategory.Tires)?(double?)s.Inspection.tireSlidingLifeMj:null,"MJ","F1",0,1000,"Authored friction-work life per tire. A grippier compound does not necessarily have a larger life budget.",1)
        );
        static NativeStatGroup AxleWheels(bool front)
        {
            int i=front?0:2;string id="axle:"+(front?"Front":"Rear");
            return Group(front?"FrontWheels":"RearWheels",front?"Front wheels":"Rear wheels",
                N(id+":wheelRadius","Tire outer radius",s=>s.Has(PartCategory.Tires)&&s.Has(PartCategory.Wheels)?(double?)s.Inspection.wheelRadiiIn[i]:null,"in","F2",8,24,"Outer radius including rubber on this axle. Taller driven tires change gearing."),
                N(id+":wheelWidth","Width",s=>s.Has(PartCategory.Tires)?(double?)s.Tune.AxleWidth(front):null,"× reference","F2",.45,1.6,"Width on this axle relative to the authored reference; no absolute millimeters are invented."),
                N(id+":tirePressure","Pressure",s=>s.Has(PartCategory.Tires)?(double?)(s.Tune.AxlePressure(front)/DisplayUnits.PsiPerBar):null,"bar","F2",18/DisplayUnits.PsiPerBar,42/DisplayUnits.PsiPerBar,"Actual axle pressure, in the selected unit."),
                N(id+":wheelMass","Assembly mass / wheel",s=>s.Has(PartCategory.Wheels)?(double?)s.Inspection.wheelMassesKg[i]:null,"kg","F2",0,60,"Actual rotating assembly mass on this axle.",-1),
                N(id+":wheelInertia","Axle rotating inertia",s=>s.Inspection.wheelInertias[i]+s.Inspection.wheelInertias[i+1],"kg m²","F3",0,20,"Actual resistance to spinning both wheels on this axle."),
                N(id+":tireGrip","Lowest contact grip",s=>s.Has(PartCategory.Tires)?(double?)Math.Min(s.Inspection.wheelGripFactors[i],s.Inspection.wheelGripFactors[i+1]):null,"×","F2",0,4,"This axle's lower corner friction factor including compound, condition, pressure, width and captured temperature; surface and load still affect live grip.",1));
        }
        static readonly NativeStatGroup WeightGroup=Group("Weight","Weight / balance",
            N("vehicleMass","Translating mass",s=>s.Inspection.vehicleMassKg,"kg","F1",0,3500,"Current physical vehicle mass including driver, payload, actual fuel/fluids and installed hardware. Used for load and grip.",-1),
            N("effectiveMass","Acceleration-equivalent mass",s=>s.Inspection.effectiveMassKg,"kg","F1",0,4000,"Translating mass plus the equivalent wheel rotational load; this is not a scale weight.",-1),
            N("chassisMass","Chassis / body mass",s=>s.Inspection.mass.chassisKg,"kg","F1",0,2000,"Resolved structural body mass with its applied reduction. Removing optional trim is different from removing the required chassis.",-1),
            N("installedMass","Installed component mass",s=>s.Inspection.mass.installedComponentKg,"kg","F1",0,2500,"Mass breakdown's installed hardware/retained liquid contribution. Do not add fluid quantities again to the translating mass."),
            N("dryInstalledMass","Mass excluding tank / occupants",s=>s.Inspection.mass.dryInstalledKg,"kg","F1",0,3500,"Model mass excluding fuel, driver and payload. Current engine/coolant/clutch liquids are already accounted for; this is not fully dry shipping mass."),
            N("driverMass","Driver mass",s=>s.Inspection.mass.driverKg,"kg","F0",0,200,"Applied driver mass. This moves the center of gravity as well as adding weight."),
            N("payloadMass","Payload mass",s=>s.Inspection.mass.payloadKg,"kg","F0",0,1000,"Applied additional payload; its position affects axle loading."),
            N("fuelMass","Fuel mass",s=>s.Inspection.mass.fuelKg,"kg","F2",0,150,"Actual stored tank quantity times fuel density, independently of fuel-consumption assistance."),
            N("frontWeight","Static front weight",s=>s.Inspection.mass.frontWeightFraction*100,"%","F1",0,100,"Static share on the front axle. Acceleration shifts live load; more front weight is not always beneficial."),
            N("frontLoad","Static front axle load",s=>s.Inspection.mass.frontNormalLoadN,"N","F0",0,40000,"Front normal force from current mass/placement before acceleration load transfer."),
            N("rearLoad","Static rear axle load",s=>s.Inspection.mass.rearNormalLoadN,"N","F0",0,40000,"Rear normal force before acceleration load transfer; tire grip is load-sensitive."),
            N("cgPosition","CG from front axle",s=>s.Inspection.mass.centerOfGravityFromFrontM,"m","F2",0,4,"Longitudinal center of gravity calculated from components, occupants, payload and actual liquids."),
            N("cgHeight","CG height",s=>s.Inspection.mass.centerOfGravityHeightM,"m","F2",0,2,"Height affects acceleration weight transfer; changing it is a traction/stability tradeoff."),
            N("wheelbase","Wheelbase",s=>s.Spec.wheelbaseM,"m","F2",0,5,"Axle separation used for weight transfer and component placement."),
            N("dragArea","Aerodynamic drag area",s=>s.Spec.dragAreaM2,"m²","F2",0,2,"Effective drag coefficient times frontal area, not frontal area alone. Higher increases aerodynamic resistance.",-1),
            N("rollingResistance","Rolling resistance",s=>s.Spec.rollingResistance,"×","F3",0,.1,"Authored rolling-resistance coefficient; separate from aerodynamic drag and tire slipping.",-1),
            N("drivetrainEfficiency","Drivetrain efficiency",s=>s.Spec.drivetrainEfficiency*100,"%","F0",0,100,"Authored driveline efficiency before additional live clutch/converter slip and faults.",1),
            N("flywheelMass","Flywheel mass",s=>s.Inspection.flywheelMassKg,"kg","F2",0,25,"Applied installed flywheel mass. Heavier stores more energy and drops neutral RPM more slowly."),
            N("flywheelInertia","Flywheel inertia",s=>s.Inspection.flywheelInertia,"kg m²","F3",0,.8,"Calculated mass/geometry resistance to RPM changes; shared with neutral revving, shifts and clutch dynamics."),
            N("engineInertia","Total engine inertia",s=>s.Inspection.mass.engineInertiaKgM2,"kg m²","F3",0,1,"Engine/core plus flywheel inertia in the authoritative mass model; do not add the flywheel twice.")
        );
        static readonly NativeStatGroup CoolingGroup=Group("Cooling","Cooling / fluids",
            N("coolingTransfer","Effective engine heat transfer",s=>s.Inspection.coolingTransferPercent,"%","F0",0,300,"Captured heat-transfer factor after hardware and condition; more conductance moves heat out of the engine.",1),
            N("radiatorRejection","Effective radiator rejection",s=>s.Inspection.radiatorWattsPerC,"W/°C","F0",0,3000,"Captured radiator heat rejection per degree above ambient before live road-airflow effects. Failed/missing radiator contributes zero.",1),
            N("fuelLitres","Fuel quantity",s=>s.Inspection.source.startingFuel?.litres,"L","F2",0,100,"Actual retained fuel at inspection; viewing this does not refill or consume it."),
            N("tankCapacity","Fuel tank capacity",s=>s.Spec.fuel.capacityLitres,"L","F1",0,100,"Installed model's tank capacity; larger fills add physical mass."),
            N("fuelDensity","Fuel density",s=>s.Spec.fuel.densityKgPerLitre,"kg/L","F3",0,1,"Authored fuel density used for weight and consumption calculations."),
            N("idleFuelRate","Reference idle consumption",s=>s.Spec.fuel.IdleLitresPerHour,"L/h","F2",0,10,"Baseline warm-idle calibration before wear, engine behavior and consumption difficulty; not a live measured rate.",-1),
            N("fuelEfficiency","Reference specific consumption",s=>s.Spec.fuel.BsfcKgPerKwh,"kg/kWh","F3",0,1,"Base fuel mass per engine energy output. Operating state, fueling hardware, wear and difficulty change actual use.",-1),
            N("oilLitres","Oil quantity",s=>s.Inspection.source.startingFluids?.oilLitres,"L","F2",0,30,"Actual retained oil. Top-up does not restore its condition or repair a leak."),
            N("oilCapacity","Oil capacity",s=>s.Has(PartCategory.OilPan)?(double?)s.Spec.fluids.oilCapacityLitres:null,"L","F1",0,30,"Capacity of the installed pan; changing hardware never automatically refills the engine."),
            N("oilMinimum","Oil pickup minimum",s=>s.Has(PartCategory.OilPan)?(double?)s.Spec.fluids.oilMinimumLitres:null,"L","F2",0,15,"Modeled minimum pickup quantity. Acceleration/baffling and oil condition also affect lubrication."),
            N("oilCondition","Oil condition",s=>s.Inspection.source.startingFluids==null?(double?)null:s.Inspection.source.startingFluids.oilCondition*100,"%","F0",0,100,"Retained lubricant condition. Oil/filter changes renew it; topping up alone retains the aging oil.",1),
            N("oilTemperature","Stored oil temperature",s=>s.Inspection.source.startingFluids?.oilTemperatureC,"°C","F1",-50,250,"Temperature captured from the owned fluid state, not a live-running reading or an automatic race-start cooldown."),
            N("oilPressure","Stored oil pressure",s=>s.Inspection.source.startingFluids?.oilPressureBar,"bar","F2",0,10,"Last retained pressure state; a stopped inspection does not represent current running pressure."),
            N("coolantLitres","Coolant quantity",s=>s.Inspection.source.startingFluids?.coolantLitres,"L","F2",0,40,"Retained coolant quantity. Boiling can vent liquid without breaking the radiator."),
            N("coolantCapacity","Coolant capacity",s=>s.Spec.fluids.coolantCapacityLitres,"L","F1",0,40,"Installed cooling-circuit capacity; hardware swaps preserve current liquid."),
            N("coolingCap","Coolant cap rating",s=>s.Has(PartCategory.CoolingCap)?(double?)s.Spec.fluids.capPressureBar:null,"bar","F2",0,3,"Installed pressure-cap rating controlling boiling/venting; not a live coolant-pressure measurement."),
            N("oilBurned","Oil consumed",s=>s.Inspection.source.startingFluids?.oilConsumedLitres,"L","F2",0,30,"Retained cumulative burned oil; inspect rings, valve seals and forced induction for causes."),
            N("oilLost","Oil leaked",s=>s.Inspection.source.startingFluids?.oilLeakedLitres,"L","F2",0,30,"Retained cumulative external oil loss; resealing does not replenish the sump."),
            N("coolantLost","Coolant lost",s=>s.Inspection.source.startingFluids?.coolantLostLitres,"L","F2",0,40,"Retained cumulative coolant loss from leaks or venting; topping up does not repair damage.")
        );
        static readonly NativeStatDefinition[] SavedTuneRows={
            N("launchTarget","Launch target",s=>s.Tune.launchRpm,"RPM","F0",0,10000,"Applied driver's launch target. The effective two-step target can be fixed by installed hardware."),
            N("shiftTarget","Shift target",s=>s.Tune.shiftRpm,"RPM","F0",0,10000,"Saved shift target, not a guaranteed shift RPM; actual automatic shifting also uses engine/gear calculations."),
            T("twoStepEnabled","Two-step",s=>State(s.Engine.twoStep),"Effective installed launch controller state; an uninstalled module cannot be enabled through this read-only view."),
            N("twoStepTarget","Two-step target",s=>s.Engine.twoStep?(double?)s.Engine.launchRpm:null,"RPM","F0",0,10000,"Effective launch-cut target after fixed/programmable hardware permissions."),
            T("twoStepStyle","Two-step cut",s=>s.Engine.twoStep?Nice(s.Engine.launchLimiter)+" / "+Nice(s.Engine.launchCut):"—","Effective launch-cut behavior and fuel/ignition method; carburetors use ignition cut."),
            T("preloadEnabled","Launch preload",s=>s.Engine.turboEquipped?State(s.Engine.preload):"—","Effective turbo preload setting. Turbo-equipped hardware and controller support are required."),
            T("antiLagEnabled","Anti-lag",s=>s.Engine.turboEquipped?State(s.Engine.antiLag):"—","Effective controller anti-lag setting; it preserves exhaust energy with heat/wear consequences."),
            T("coldStart","Cold-start simulation",s=>State(s.Tune.coldStart),"Saved cold-start behavior; this does not override the selected race-start thermal policy."),
            T("shiftLight","Shift light",s=>s.Tune.partShiftLight?"Installed":"Not installed","Installed shift alert is separate from gearbox and engine performance. Its threshold does not command a shift."),
            N("shiftLightTarget","Shift-light threshold",s=>s.Tune.partShiftLight?(double?)s.Tune.ShiftLightRpm(s.Spec):null,"RPM","F0",0,10000,"Effective fixed/programmable shift-light threshold from the installed module."),
            N("boostSetting","Boost setting",s=>s.Induction.turboCount+s.Induction.superchargerCount>0?(double?)s.Tune.BoostSetting:null,"× reference","F2",0,2,"Applied tune multiplier after fixed/adjustable hardware permissions; actual gauge pressure is in Induction."),
            N("payloadPosition","Payload from front axle",s=>s.Tune.payloadPositionM,"m","F2",-.5,5,"Applied payload position, including when payload mass is zero. The Weight group shows its actual combined effect."),
            N("payloadHeight","Payload height",s=>s.Tune.payloadHeightM,"m","F2",0,2,"Applied height for the additional payload; matters when payload mass is nonzero."),
            N("frontLeftGrip","Front-left surface factor",s=>s.Tune.frontLeftGrip,"×","F2",0,2,"Applied per-wheel surface test factor; not a tire upgrade."),
            N("frontRightGrip","Front-right surface factor",s=>s.Tune.frontRightGrip,"×","F2",0,2,"Applied per-wheel surface factor used by differential/contact calculations."),
            N("rearLeftGrip","Rear-left surface factor",s=>s.Tune.rearLeftGrip,"×","F2",0,2,"Applied rear-left surface factor; a differential may limit drive on split grip."),
            N("rearRightGrip","Rear-right surface factor",s=>s.Tune.rearRightGrip,"×","F2",0,2,"Applied rear-right surface factor, independent of tire condition.")
        };
        static NativeStatGroup Gearing(CarInspection current,CarInspection proposed)
        {
            var rows=new List<NativeStatDefinition>();
            int Count(CarInspection s)=>s==null||s.transmission==TransmissionType.CVT?0:s.gearRedlineMps.Length;
            int count=Math.Max(Count(current),Count(proposed));
            for(int i=0;i<count;i++) {
                int gear=i;
                rows.Add(N("gearRatio:"+i,"Gear / ratio "+(i+1),s=>s.Transmission.type!=TransmissionType.CVT&&gear<s.Spec.gears.Length?(double?)s.Spec.gears[gear]:null,":1","F2",0,6,"Installed numbered gear ratio. CVT shortest/tallest ratios appear separately; changing wheel size changes road speed without changing this ratio."));
                rows.Add(N("gearSpeed:"+i,"Gear "+(i+1)+" redline speed",s=>s.Transmission.type!=TransmissionType.CVT&&gear<s.Inspection.gearRedlineMps.Length?(double?)s.Inspection.gearRedlineMps[gear]:null,"m/s","F1",0,120,"Theoretical speed at engine redline, not a drag-limited top speed or a prediction that the engine can pull this ratio."));
                if(i<count-1)rows.Add(N("shiftLanding:"+i,"Upshift "+(i+1)+" → "+(i+2),s=>s.Transmission.type!=TransmissionType.CVT&&gear<s.Inspection.rpmAfterUpshift.Length?(double?)s.Inspection.rpmAfterUpshift[gear]:null,"RPM","F0",0,10000,"Calculated engagement RPM using the automatic shift point and adjacent ratios. A manually chosen shift RPM can produce a different landing."));
            }
            if(current.transmission==TransmissionType.CVT||proposed?.transmission==TransmissionType.CVT)for(int i=0;i<2;i++) {
                int ratio=i;rows.Add(N("cvtSpeed:"+i,i==0?"CVT shortest-ratio speed":"CVT tallest-ratio speed",s=>s.Transmission.type==TransmissionType.CVT?(double?)s.Inspection.gearRedlineMps[ratio]:null,"m/s","F1",0,120,"Theoretical speed at engine redline at this CVT ratio endpoint; not a numbered gear or attainable top speed."));
            }
            return Group("Gearing","Gearing",rows.ToArray());
        }
        static NativeStatGroup Exhaust(CarInspection current,CarInspection proposed)
        {
            int count=Math.Max(current.source.build.spec.sound.exhaustSystem.parts.Length,proposed?.source.build.spec.sound.exhaustSystem.parts.Length??0);var children=new List<NativeStatGroup>();
            for(int i=0;i<count;i++) {
                int index=i;ExhaustPart Part(NativeStatsSnapshot s)=>index<s.Spec.sound.exhaustSystem.parts.Length?s.Spec.sound.exhaustSystem.parts[index]:null;
                string id="exhaust:"+i;
                children.Add(Group(id,"Exhaust section "+(i+1),
                    T(id+":type","Type",s=>Part(s)==null?"Absent":Nice(Part(s).kind),"Exact acoustic-path element; changing the sequence changes filtering. Removed sections are absent, not healed."),
                    N(id+":amount","Active amount",s=>Part(s)==null?(double?)null:Part(s).amount*100,"%","F0",0,100,"Continuous contribution of this acoustic element, rather than a silencer/catalyst boolean."),
                    N(id+":attenuation","Attenuation",s=>Part(s)?.attenuationDb,"dB","F1",0,30,"Authored attenuation before active-amount scaling. More reduces sound level, not engine power."),
                    N(id+":cutoff","Low-pass cutoff",s=>Part(s)?.lowPassHz,"Hz","F0",100,18000,"Acoustic high-frequency filtering; lower makes this section duller."),
                    N(id+":pulse","Pulse softening",s=>Part(s)==null?(double?)null:Part(s).pulseSoftening*100,"%","F0",0,100,"Continuous smoothing of combustion pulses."),
                    N(id+":resonance","Resonance frequency",s=>Part(s)?.resonanceHz,"Hz","F0",40,5000,"Center frequency of the section's acoustic resonance or notch."),
                    N(id+":gain","Resonance gain",s=>Part(s)?.resonanceGainDb,"dB","F1",-24,12,"Positive boosts and negative attenuates the resonance band; this is a sound-shaping tradeoff."),
                    N(id+":q","Resonance Q",s=>Part(s)?.resonanceQ,"","F2",.25,8,"Bandwidth/sharpness of the section resonance.")));
            }
            var group=Group("ExhaustSections","Exhaust sections");group.Children=children.AsReadOnly();return group;
        }
        static NativeStatGroup Components(CarInspection current,CarInspection proposed)
        {
            var children=new List<NativeStatGroup> {Group("SavedTuning","Saved tuning",SavedTuneRows)};
            var a=new NativeStatsSnapshot(current);var b=proposed==null?null:new NativeStatsSnapshot(proposed);
            foreach(var slot in Enum.GetValues(typeof(PartCategory)).Cast<PartCategory>().Where(slot=>a.Has(slot)||b!=null&&b.Has(slot))) {
                string id="part:"+slot;var rows=new List<NativeStatDefinition>();
                var hardware=T(id+":hardware","Hardware",s=>slot==PartCategory.Wheels&&s.Has(slot)?WheelDescription(s):slot==PartCategory.Tires&&s.Has(slot)?TireDescription(s):s.Definition(slot)?.name??"Not installed","Installed component, not a replacement recommendation.");
                hardware.Detail=s=>s.Definition(slot)==null?"No component installed in this slot.":s.Definition(slot).description+"\nDefinition: "+s.Definition(slot).id+"\nOwned instance: "+s.Part(slot).instanceId;rows.Add(hardware);
                rows.Add(T(id+":quality","Quality",s=>slot==PartCategory.Wheels&&s.Spec.cornerWheelDefinitions.Length==4?(s.Spec.cornerWheelDefinitions.Select(d=>d.quality).Distinct().Count()==1?s.Spec.cornerWheelDefinitions[0].quality:"Mixed"):slot==PartCategory.Tires&&s.Spec.cornerTireDefinitions.Length==4?(s.Spec.cornerTireDefinitions.Select(d=>d.quality).Distinct().Count()==1?s.Spec.cornerTireDefinitions[0].quality:"Mixed"):s.Definition(slot)?.quality??"—","Actual construction quality from the installed definition; quality is separate from remaining condition."));
                rows.Add(T(id+":status","Status",s=>s.Part(slot)==null?"Not installed":PrototypeGame.PartFailed(s.Part(slot))?"Failed":EngineElements.NeedsService(s.Part(slot).condition)?"Element fault":"Installed","A failed brake or drive branch does not imply a failed engine. Inspect the component's failure reason and the Performance warnings."));
                var reason=T(id+":fault","Failure reason",s=>s.Part(slot)==null?"—":string.IsNullOrEmpty(s.Part(slot).condition.failureReason)?"None":s.Part(slot).condition.failureReason,"Stored component failure explanation. Replacing or repairing requires an explicit maintenance action.");rows.Add(reason);
                rows.Add(T(id+":tuning","Hardware adjustment",s=>s.Definition(slot)==null?"—":s.Definition(slot).tuning==PartTuningOptions.None?"Fixed":Nice(s.Definition(slot).tuning),"Installed part's declared adjustment permissions; viewing stats does not unlock tuning. Effective applied settings are shown in their relevant groups."));
                rows.Add(N(id+":health","Condition",s=>s.Part(slot)!=null&&PartSemantics.Wears(slot)?(double?)(s.Part(slot).condition.health*100):null,"%","F0",0,100,"Retained component health; non-wearing parts have no artificial wear percentage.",1));
                rows.Add(N(id+":temperature","Stored temperature",s=>s.Part(slot)!=null&&PartSemantics.Wears(slot)?(double?)s.Part(slot).condition.temperatureC:null,"°C","F1",-50,300,"Owned component's retained temperature, before race-start assistance or elapsed-time cooldown is applied."));
                if(PartSemantics.Wears(slot)) {
                    if(slot==PartCategory.FuelSystem||slot==PartCategory.Ignition||slot==PartCategory.HeadIntake||slot==PartCategory.OilFilter)rows.Add(N(id+":deposits","Deposits",s=>s.Part(slot)==null?(double?)null:s.Part(slot).condition.deposits*100,"%","F0",0,100,"Retained metering/breathing/filter deposits. Cleaning applies only to supported categories; changing oil retains the removed filter.",-1));
                    if(slot==PartCategory.FuelSystem||slot==PartCategory.Induction||slot==PartCategory.Cooling||slot==PartCategory.CoolingHoses||slot==PartCategory.OilPan||slot==PartCategory.OilCooler)rows.Add(N(id+":leak","Leak severity",s=>s.Part(slot)==null?(double?)null:s.Part(slot).condition.leakSeverity*100,"%","F0",0,100,"Retained metering/fluid-leak severity; top-up does not reseal it.",-1));
                    if(slot==PartCategory.Followers||slot==PartCategory.TimingDrive)rows.Add(N(id+":adjustment","Adjustment error",s=>s.Part(slot)==null?(double?)null:s.Part(slot).condition.adjustmentError*100,"%","F0",0,100,"Retained clearance/timing error; a service adjustment differs from changing a saved tune.",-1));
                    if(slot==PartCategory.Clutch||slot==PartCategory.Tires)rows.Add(N(id+":work","Accumulated slip work",s=>s.Part(slot)==null?(double?)null:s.Part(slot).condition.slipEnergyJ/1e6,"MJ","F2",0,100,"Stored component friction work, separate from RPM slip and the authored life budget."));
                }
                if(a.Definition(slot)?.awdHardware?.version==1||b?.Definition(slot)?.awdHardware?.version==1){
                    rows.Add(T(id+":awdHardware","AWD hardware",s=>s.Definition(slot)?.awdHardware==null?"—":slot==PartCategory.TransferCase&&!s.Definition(slot).awdHardware.integratedCenter?"Pass-through transfer housing":string.Join(" / ",Enum.GetValues(typeof(AwdMode)).Cast<AwdMode>().Where(s.Definition(slot).awdHardware.Supports).Select(AwdHardware.Name)),"Modes supported by the actual center assembly. Engine ECU software cannot change its mechanical system."));
                    rows.Add(T(id+":awdIsolation","Failed-branch isolation",s=>s.Definition(slot)?.awdHardware==null?"—":s.Definition(slot).awdHardware.calibration.isolateFailedAxle?"Supported":"Not provided","Physical disconnect capability in this component. The complete build combines its center assembly and transfer housing."));
                    rows.Add(T(id+":awdAdjustment","AWD adjustments",s=>AwdHardware.Summary(s.Definition(slot)),"Only the installed hardware/controller's supported calibration is editable; primary axle, fixed geometry and failure-isolation hardware remain specifications."));
                }
                var metrics=slot==PartCategory.Tires||slot==PartCategory.Wheels?Enumerable.Empty<PartStat>():PartSemantics.Stats(a.Definition(slot)).Concat(PartSemantics.Stats(b?.Definition(slot))).GroupBy(m=>m.key).Select(g=>g.First());
                foreach(var metric in metrics) {
                    var key=metric.key;
                    rows.Add(N(id+":rating:"+key,metric.label,s=> {
                        var found=PartSemantics.Stats(s.Definition(slot)).Where(m=>m.key==key).ToArray();return found.Length==0?(double?)null:found[0].value;
                    },metric.unit,"0.##",Math.Min(0,PartSemantics.Stats(a.Definition(slot)).Concat(PartSemantics.Stats(b?.Definition(slot))).Where(m=>m.key==key).Min(m=>m.value)*1.25),Math.Max(1,PartSemantics.Stats(a.Definition(slot)).Concat(PartSemantics.Stats(b?.Definition(slot))).Where(m=>m.key==key).Max(m=>m.value)*1.25),metric.explanation,metric.direction));
                }
                if(slot==PartCategory.Clutch) {
                    rows.Add(N(id+":oil","Clutch oil quantity",s=>s.Definition(slot)?.clutchEnvironment==ClutchEnvironment.Wet?(double?)s.Part(slot).condition.clutchOilLitres:null,"L","F2",0,10,"Actual dedicated wet-clutch oil bath; dry clutches do not receive a fabricated fluid level."));
                    rows.Add(N(id+":oilCondition","Clutch oil condition",s=>s.Definition(slot)?.clutchEnvironment==ClutchEnvironment.Wet?(double?)(s.Part(slot).condition.clutchOilCondition*100):null,"%","F0",0,100,"Retained wet-clutch lubricant quality. Service renews the bath while retaining friction wear.",1));
                }
                var partGroup=Group(id,Nice(slot),rows.ToArray());partGroup.Children=ElementGroups(id,slot,a,b);children.Add(partGroup);
            }
            var group=Group("Components","Components / saved tuning",
                N("componentCount","Installed components",s=>s.Inspection.source.components.Count,"","F0",0,60,"Actual installed component packs, not loose inventory or assumed OEM parts."),
                N("componentCondition","Lowest part condition",s=>s.Inspection.condition*100,"%","F0",0,100,"Worst installed wearing-part condition, not a weighted car average.",1));group.Children=children.AsReadOnly();return group;
        }
        static IReadOnlyList<NativeStatGroup> ElementGroups(string prefix,PartCategory slot,NativeStatsSnapshot a,NativeStatsSnapshot b)
        {
            var groups=new List<NativeStatGroup>();
            var elements=(a.Part(slot)?.condition.elements??new EngineElement[0]).Concat(b?.Part(slot)?.condition.elements??new EngineElement[0]).GroupBy(e=>e.role+":"+e.cylinder+":"+e.valve).Select(g=>g.First()).ToArray();
            foreach(var element in elements) {
                string key=element.role+":"+element.cylinder+":"+element.valve,id=prefix+":element:"+key;
                EngineElement Find(NativeStatsSnapshot s)=>s.Part(slot)?.condition.elements.FirstOrDefault(e=>e.role+":"+e.cylinder+":"+e.valve==key);
                groups.Add(Group(id,slot==PartCategory.Wheels?new[]{"Front left wheel","Front right wheel","Rear left wheel","Rear right wheel"}[element.cylinder]:Nice(element.role)+(element.cylinder>=0?" · cylinder "+(element.cylinder+1):"")+(element.valve>=0?" · valve "+(element.valve+1):""),
                    N(id+":health","Condition",s=>Find(s)==null?(double?)null:Find(s).health*100,"%","F0",0,100,"Condition of this individual owned element, rather than the whole engine/set.",1),
                    T(id+":fault","Fault",s=>Find(s)==null?"Not installed":Nice(Find(s).fault),"Exact retained single-element fault; a failed cylinder/valve can coexist with other working cylinders."),
                    N(id+":severity","Fault severity",s=>Find(s)==null?(double?)null:Find(s).severity*100,"%","F0",0,100,"Stored severity for the individual retained fault.",-1),
                    T(id+":spec","Specification",s=>Find(s)==null?"Not installed":s.Spec.cornerWheelDefinitions.Length==4&&slot==PartCategory.Wheels?s.Spec.cornerWheelDefinitions[Find(s).cylinder].name:s.Definition(slot)?.name??"Not installed","Actual retained member specification.")));
            }
            if(slot==PartCategory.Wheels)for(int corner=0;corner<4;corner++){
                int index=corner;var member=groups.FirstOrDefault(g=>g.Id.Contains(":element:wheel:"+index+":"));if(member==null)continue;
                var rows=member.Definitions.ToList();rows.Add(N(prefix+":rimMass:"+index,"Actual rim mass",s=>s.Spec.cornerWheelDefinitions.Length==4?(double?)(s.Spec.cornerWheelDefinitions[index].massKg*.25*Math.Pow(s.Tune.AxleDiameter(index<2),3)*s.Tune.AxleWidth(index<2)):null,"kg","F2",0,60,"This fitted rim's actual mass with axle geometry; rubber is separate.",-1));
                groups[groups.IndexOf(member)]=Group(member.Id,member.Title,rows.ToArray());
            }
            int tires=Math.Max(a.Part(slot)?.condition.tires.Length??0,b?.Part(slot)?.condition.tires.Length??0);
            string[] corners={"Front left","Front right","Rear left","Rear right"};
            for(int i=0;i<tires;i++) {
                int corner=i;string id=prefix+":tire:"+i;TireCondition Find(NativeStatsSnapshot s)=>corner<(s.Part(slot)?.condition.tires.Length??0)?s.Part(slot).condition.tires[corner]:null;
                groups.Add(Group(id,corner<4?corners[corner]:"Tire "+(corner+1),
                    N(id+":heat","Heat tolerance",s=>Find(s)==null?(double?)null:(s.Spec.cornerTireDefinitions.Length==4?s.Spec.cornerTireDefinitions[corner]:s.Definition(PartCategory.Tires)).thermalLimitC,"°C","F0",0,200,"This corner compound's actual thermal limit.",1),
                    N(id+":tireoptimal","Best-grip temperature",s=>Find(s)==null?(double?)null:(s.Spec.cornerTireDefinitions.Length==4?s.Spec.cornerTireDefinitions[corner]:s.Definition(PartCategory.Tires)).tireOptimalC,"°C","F0",0,120,"Preferred operating temperature for this corner's actual compound."),
                    T(id+":hardware","Specification",s=>s.Spec.cornerTireDefinitions!=null&&s.Spec.cornerTireDefinitions.Length==4?s.Spec.cornerTireDefinitions[corner].name:s.Definition(PartCategory.Tires)?.name??"Not installed","Actual fitted tire specification at this corner; compatible compounds may differ between wheels."),
                    N(id+":health","Condition",s=>Find(s)==null?(double?)null:Find(s).health*100,"%","F0",0,100,"Retained condition for this tire corner.",1),
                    N(id+":grip","Contact grip",s=>Find(s)==null?(double?)null:s.Inspection.wheelGripFactors[corner],"×","F2",0,4,"Actual corner friction factor including compound, geometry, pressure and captured condition.",1),
                    N(id+":rollinglife","Reference rolling life",s=>Find(s)==null?(double?)null:s.Inspection.wheelRollingLifeKm[corner],"km","F0",0,80000,"This corner's compound reference life, not guaranteed remaining distance.",1),
                    N(id+":slidinglife","Sliding-work life",s=>Find(s)==null?(double?)null:s.Inspection.wheelSlidingLifeMj[corner],"MJ","F1",0,100,"This corner's own compound friction-work budget.",1),
                    T(id+":failed","Status",s=>Find(s)==null?"Not installed":Find(s).failed?"Failed":"Installed","A failed tire's condition is retained until an explicit repair/replacement transaction."),
                    N(id+":temperature","Stored temperature",s=>Find(s)?.temperatureC,"°C","F1",-50,200,"Captured corner temperature before race-start cooling or tire warm-up."),
                    N(id+":work","Sliding work",s=>Find(s)==null?(double?)null:Find(s).slipEnergyJ/1e6,"MJ","F2",0,100,"Accumulated friction work for this tire; not the whole axle."),
                    N(id+":distance","Rolling distance",s=>Find(s)==null?(double?)null:Find(s).distanceM/1000,"km","F1",0,80000,"Retained traveled distance for this owned tire.")));
            }
            return groups.AsReadOnly();
        }
        internal static NativeStatGroup Performance(CarInspection current,CarInspection proposed)
        {
            var rows=new List<NativeStatDefinition> {
                T("buildStatus","Build",s=>s.Driving.source.build.Drivable?"Hardware complete":"Blocked","Resolved required hardware/engine blockers. Separate brake or branch failures may still allow the engine to run; inspect warnings."),
                T("probeStatus","Driving estimate",s=>s.DrivingError!=null?"Unavailable":s.DrivingPending?"Calculating…":!s.Driving.probeComplete?"Not run":s.Driving.completed?"Completed":s.Driving.probeFailed?"Failed":"Incomplete","Standard copied-build automatic quarter-mile estimate with traction assist off and full wear, using the authoritative race simulation. Runs in the background on demand; never consumes player fuel, wears owned parts, changes cash or records a race."),
                N("quarterTime","Quarter-mile estimate",s=>s.Driving.probeComplete&&s.Driving.completed?(double?)s.Driving.elapsed:null,"s","F2",0,30,"Completed copied-build automatic quarter-mile result; unavailable without a completed probe.",-1),
                N("trapSpeed","Probe final speed",s=>s.Driving.probeComplete?(double?)s.Driving.trapMps:null,"m/s","F1",0,120,"Final speed of the supplied probe; only a completed pass reaches the trap distance."),
                N("maxWheelSlip","Probe peak wheel slip",s=>s.Driving.probeComplete&&s.Driving.source.build.Drivable?(double?)(s.Driving.maxSlip*100):null,"%","F0",0,100,"Measured tire/contact slip from the supplied standard probe, excluding very low speed. No run means unavailable, not zero.",-1),
                N("maxClutchSlip","Probe peak clutch slip",s=>s.Driving.probeComplete&&Friction(s)&&s.Driving.source.build.Drivable?(double?)s.Driving.maxClutchSlip:null,"RPM","F0",0,10000,"Measured engine/coupling speed mismatch from the supplied friction-clutch probe; it is not tire slip.",-1),
                N("warningCount","Warnings",s=>s.Driving.warnings.Count,"","F0",0,30,"Authoritative inspection warnings from hardware, captured condition, fluids and any supplied driving estimate.",-1)
            };
            foreach(string title in current.warnings.Select(w=>w.title).Concat(proposed?.warnings.Select(w=>w.title)??Enumerable.Empty<string>()).Distinct()) {
                var row=T("warning:"+rows.Count,title,s=>s.Driving.warnings.Any(w=>w.title==title)?s.Driving.warnings.First(w=>w.title==title).critical?"Critical":"Attention":"Clear","Inspection warning.");
                row.Detail=s=>string.Join("\n\n",s.Driving.warnings.Where(w=>w.title==title).Select(w=>w.explanation+"\n"+w.suggestion));rows.Add(row);
            }
            return Group("Performance","Performance / warnings",rows.ToArray());
        }
        internal static IReadOnlyList<NativeStatGroup> Groups(CarInspection current,CarInspection proposed=null)
        {
            var induction=Group(InductionGroup.Id,InductionGroup.Title,InductionGroup.Definitions.ToArray());induction.Children=Array.AsReadOnly(new[] {Exhaust(current,proposed)});
            var transmission=Group(TransmissionGroup.Id,TransmissionGroup.Title,TransmissionGroup.Definitions.ToArray());transmission.Children=Array.AsReadOnly(new[] {Gearing(current,proposed)});
            var tires=Group(TiresGroup.Id,TiresGroup.Title,TiresGroup.Definitions.Where(d=>d.Id!="wheelWidth"&&d.Id!="tirePressure").ToArray());tires.Children=Array.AsReadOnly(new[]{AxleWheels(true),AxleWheels(false)});
            return Array.AsReadOnly(new[] {Engine,induction,transmission,tires,WeightGroup,CoolingGroup,Components(current,proposed),Performance(current,proposed)});
        }
        internal static NativeStatGroup TuningSummary()=>new NativeStatGroup("TuneSummary","Changes",
            N("tuningHp","HP",s=>s.Inspection.peakHp,"HP","F0",0,500,"Estimated full-load power with the captured tune, installed hardware, wear and supplies.",1),
            N("tuningTorque","Torque",s=>s.Inspection.peakTorque,"Nm","F0",0,1000,"Engine torque before gearing. Higher torque can need stronger components and better traction.",1),
            N("tuningWeight","Weight",s=>s.Inspection.vehicleMassKg,"kg","F1",0,3500,"Physical vehicle mass, including driver, payload, fitted hardware and retained fuel/fluids.",-1),
            N("tuningCondition","Condition",s=>s.Inspection.condition*100,"%","F0",0,100,"Captured fitted-part condition. Previewing a tune does not repair components.",1));
    }
}

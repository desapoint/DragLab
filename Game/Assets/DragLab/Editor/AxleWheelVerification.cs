using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Editor
{
    public static class AxleWheelVerification
    {
        static void CheckWheelMembers(PlayerProfile p,Action<bool,string> check)
        {
            var car=p.SelectedCar;var root=p.parts.Find(i=>i.instanceId==car.installed.slots.First(s=>s.slot=="Wheels").partInstanceId);WheelFitments.Ensure(root);
            root.condition.elements[0].health=.35f;string oldFront=root.condition.elements[0].id,rear=JsonUtility.ToJson(root.condition.elements[2]);var before=MechanicsOwnership.Resolve(p,car,p.catalog);var baseWheels=new WheelProfile(before.spec,before.tune);
            string id=p.catalog.Id(car.vehicleId,PartCategory.Wheels,1);string unchanged=JsonUtility.ToJson(p);
            var lines=new[]{new PartCartLine{action=CartAction.NewUnits,definitionId=id,role="wheel",quantity=2,install=true,positions=new System.Collections.Generic.List<string>{"wheel:0","wheel:1"}}};
            var preview=PartCart.Preview(p,car,lines);var wheels=new WheelProfile(preview.build.spec,preview.build.tune);
            check(unchanged==JsonUtility.ToJson(p)&&wheels.rimMasses[0]<baseWheels.rimMasses[0]&&wheels.rimMasses[2]==baseWheels.rimMasses[2],"Front wheel pair preview lowers only actual front rim mass/inertia: "+car.vehicleId);
            long now=DateTime.UtcNow.Ticks;var quote=PartCart.Quote(p,car.instanceId,lines,now,60);PlayerProfile applied;string error;
            bool ok=MechanicsTransactions.Try(p,p.catalog,p.revision,c=>PartCart.Checkout(c,quote,now),c=>true,out applied,out error);check(ok,"Individual wheel pair uses one valid paid cart transaction: "+car.vehicleId+" / "+error);
            car=applied.SelectedCar;var rims=applied.parts.Find(i=>i.instanceId==root.instanceId);
            check(WheelFitments.DefinitionId(rims,0)==id&&WheelFitments.DefinitionId(rims,1)==id&&JsonUtility.ToJson(rims.condition.elements[2])==rear&&applied.looseEngineElements.Any(u=>u.element.id==oldFront&&u.element.health==.35f),"Wheel pair replacement retains rear identity and worn front originals: "+car.vehicleId);
            var saved=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(applied));MechanicsOwnership.Ensure(saved,p.catalog);check(MechanicsOwnership.Validate(saved,p.catalog)==null&&WheelFitments.DefinitionId(saved.parts.Find(i=>i.instanceId==root.instanceId),0)==id,"Mixed rims survive profile normalization without losing identities: "+car.vehicleId);
            SavedSetups.Save(applied,car.instanceId,"Mixed wheels",SetupScope.WholeCar);var setup=applied.savedSetups.Last();
            foreach(int corner in new[]{0,1}){var original=applied.looseEngineElements.First(u=>u.element.role=="wheel"&&u.definitionId==root.definitionId);InventoryUnits.Install(applied,car.instanceId,original.element.id,"wheel:"+corner);}
            var plan=SavedSetups.Preview(applied,car.instanceId,setup.id);check(plan.tires.Count(t=>t.wheel)==2&&plan.tires.Where(t=>t.wheel).All(t=>!t.purchase),"Whole-car setup locates exact owned wheel replacements for front pair: "+car.vehicleId);
            SavedSetups.Apply(applied,car.instanceId,setup.id,applied.revision,false,plan.cost);check(MechanicsOwnership.Validate(applied,p.catalog)==null&&WheelFitments.DefinitionId(rims,0)==id,"Existing blueprint transaction restores real mixed wheel members: "+car.vehicleId);
        }
        static void CheckCenterRatios(MechanicalCatalog catalog,Action<bool,string> check)
        {
            foreach(double q in new[]{.6,.8,1,1.2,1.5,1.8,2.0}){
                double[] inertia={.7,.9,1.2,1.5};double[] radii={.3,.3,.3*q,.3*q};
                var matched=new WheelContactState{speed=20,omega=radii.Select(r=>20/r).ToArray()};var mismatch=new WheelContactState{speed=20,omega=radii.Select(r=>20/r).ToArray()};
                var input=new WheelContactInput{radii=radii,inertias=inertia,lockCenter=true,centerSpeedRatio=q};
                for(int i=0;i<120;i++)WheelContactDynamics.Step(matched,input,DragMath.FixedStep);
                double initial=.5*input.massKg*400+Enumerable.Range(0,4).Sum(i=>.5*inertia[i]*Math.Pow(20/radii[i],2));
                check(Math.Abs(matched.speed-20)<1e-8&&matched.slipWorkJ.Sum()<1e-6&&Math.Abs(matched.KineticEnergy(input.massKg,inertia)+matched.contactWorkJ-initial)<1e-6,"Ratio-matched locked center rolls without forced slip and conserves energy: "+q);
                input.centerSpeedRatio=1;for(int i=0;i<120;i++)WheelContactDynamics.Step(mismatch,input,DragMath.FixedStep);
                check(q==1||mismatch.slipWorkJ.Sum()>matched.slipWorkJ.Sum()+1,"Mismatched locked ratio dissipates real tire sliding work: "+q);
                input.centerSpeedRatio=q;input.frontTorqueNm=500/q;input.rearTorqueNm=500;double before=matched.KineticEnergy(input.massKg,inertia),work=matched.driveWorkJ,loss=matched.contactWorkJ;
                for(int i=0;i<240;i++)WheelContactDynamics.Step(matched,input,DragMath.FixedStep);
                check(Math.Abs(matched.KineticEnergy(input.massKg,inertia)-before+matched.contactWorkJ-loss-(matched.driveWorkJ-work))<1e-5&&Math.Abs(matched.omega[0]+matched.omega[1]-q*(matched.omega[2]+matched.omega[3]))<1e-8,"Geared locked center preserves power/energy and actual angular constraint under torque: "+q);
            }
            var source=catalog.Spec(catalog.parts.First(p=>PartTuning.Allows(p,PartTuningOptions.CenterRatio)).id.Split('/')[0]);
            var driver=new DriverSettings();var mismatchTune=new Tune{frontWheelDiameter=.8f,rearWheelDiameter=1.2f,awdConfigured=true,awd=new AwdSpec{mode=AwdMode.LockedCenter},finalDrive=source.finalDrive};var matchedTune=mismatchTune.Copy();matchedTune.awd.centerSpeedRatio=1.5f;
            var coarse=new CarSimulation(source,mismatchTune,driver);var matchedCar=new CarSimulation(source,matchedTune,driver);coarse.state.speed=matchedCar.state.speed=20;
            for(int i=0;i<240;i++){coarse.IndependentStep(DragMath.FixedStep,new DriverInput(),true,false);matchedCar.IndependentStep(DragMath.FixedStep,new DriverInput(),true,false);}
            var coarseTires=coarse.mechanics.parts.First(p=>p.slot=="Tires");var matchedTires=matchedCar.mechanics.parts.First(p=>p.slot=="Tires");
            check(coarse.state.contact.slipWorkJ.Sum()>matchedCar.state.contact.slipWorkJ.Sum()+1&&coarseTires.condition.tires.Sum(t=>t.consumedLife)>matchedTires.condition.tires.Sum(t=>t.consumedLife)+1e-9,"Forced locked-center scrub causes extra actual tire deterioration in the authoritative car simulation");
            foreach(var offer in catalog.parts.Where(p=>PartTuning.Allows(p,PartTuningOptions.CenterRatio))){
                var spec=catalog.Spec(offer.id.Split('/')[0]);var stock=catalog.Recipe(spec.id);var t=new Tune{awdConfigured=true,awd=new AwdSpec{mode=AwdMode.LockedCenter,centerSpeedRatio=1.5f},frontWheelDiameter=.8f,rearWheelDiameter=1.2f};
                var ordinary=catalog.Resolve(spec.id,stock,t);check(AxleDriveDynamics.Configuration(ordinary.spec,ordinary.tune).LockedRatio==1,"Ordinary transfer case cannot acquire free ratio capability: "+spec.id);
                int standard=catalog.parts.Where(p=>p.category==PartCategory.TransferCase&&!PartTuning.Allows(p,PartTuningOptions.CenterRatio)&&catalog.Fits(spec,p)).Max(p=>p.price);stock[PartCategory.TransferCase]=offer.id;var built=catalog.Resolve(spec.id,stock,t);
                check(built.errors.Count==0&&offer.price>standard&&AxleDriveDynamics.Configuration(built.spec,built.tune).LockedRatio==1.5,"Paid specialized hardware preserves the requested ratio: "+spec.id+" / "+string.Join("; ",built.errors));
                var inspection=CarInspection.CalculatePrepared(new InspectionBuild{build=built,components=new System.Collections.Generic.List<PartInstance>()},new CarSimulation(built.spec,built.tune,new DriverSettings()),false);
                check(!inspection.warnings.Any(w=>w.title=="AWD tire rolling sizes differ"),"Matched geared AWD does not claim a rolling-size mismatch: "+spec.id);
                var wheels=new WheelProfile(built.spec,built.tune);check(Math.Abs(wheels.radius-wheels.radii[2])<1e-7,"Matched locked ratio gives the correct input rolling radius: "+spec.id);
            }
        }
        public static void Run()
        {
            var log=new StringBuilder();int count=0;
            Action<bool,string> check=(ok,label)=>{log.AppendLine(((ok?"PASS: ":"FAIL: ")+label).TrimEnd());if(!ok)throw new InvalidOperationException(label);count++;};
            var bath=new ComponentCondition();
            check(bath.clutch==null&&bath.clutchOilLitres==0&&bath.clutchOilCondition==1
                &&bath.clutchFluidVersion==0,"Non-clutch components have no serialized clutch oil payload");
            bath.clutchFluidVersion=1;bath.clutchOilLitres=1.75;bath.clutchOilCondition=.63;
            string clutchJson=JsonUtility.ToJson(bath);
            var clutchRestored=JsonUtility.FromJson<ComponentCondition>(clutchJson);
            check(clutchJson.Contains("\"clutch\"")&&!clutchJson.Contains("\"clutchOilLitres\"")
                &&clutchRestored.clutch!=null&&clutchRestored.clutchOilLitres==1.75
                &&clutchRestored.clutchOilCondition==.63&&clutchRestored.clutchFluidVersion==1
                &&clutchRestored.Validate()==null,"Clutch oil version, litres and condition survive a typed Unity JSON round trip");
            var clutchCopy=bath.Copy();clutchCopy.clutchOilCondition=.3;
            check(bath.clutchOilCondition==.63&&clutchCopy.clutchOilCondition==.3,
                "Clutch-specific oil payload copies are independent");
            // A stale/worn former tire-set condition must never contaminate healthy
            // fitted tires, and obsolete parent usage counters must not be retained.
            var independent=new ComponentCondition{health=.1f,failed=true,failureReason="old pack state",
                slipEnergyJ=800,distanceM=600,runningSeconds=10,
                tires=Enumerable.Range(0,4).Select(i=>new TireCondition{id="individual-"+i,health=1,temperatureC=20}).ToArray()};
            TireDynamics.Ensure(independent);
            check(independent.tires.All(t=>t.health==1&&!t.failed&&t.distanceM==0&&t.slipEnergyJ==0)
                &&independent.health==1&&!independent.failed&&independent.slipEnergyJ==0
                &&independent.distanceM==0,"Stale tire-set damage and history never flow back into independent tires");
            var oldCondition=independent.Copy();
            oldCondition.tires[0].slipEnergyJ=100;oldCondition.tires[0].distanceM=300;
            oldCondition.tires[0].health=.75f;oldCondition.tires[0].consumedLife=.25;
            var advanced=oldCondition.Copy();
            advanced.tires[0].slipEnergyJ+=50;advanced.tires[0].distanceM+=100;
            advanced.tires[0].health=.70f;advanced.tires[0].consumedLife=.30;
            check(RunPersistence.ConditionAdvances(oldCondition,advanced),"Per-tire journal accepts genuine usage growth");
            advanced.tires[0].slipEnergyJ=20;
            check(!RunPersistence.ConditionAdvances(oldCondition,advanced),"Per-tire journal rejects energy rewind");
            advanced=oldCondition.Copy();advanced.tires[0].id="different-tire";
            check(!RunPersistence.ConditionAdvances(oldCondition,advanced),"An active race cannot silently exchange tire identities");
            var typed=independent.Copy();
            typed.tires[2].health=.34f;typed.tires[2].consumedLife=.66;
            typed.tires[2].temperatureC=97;typed.tires[2].distanceM=1298;typed.tires[2].slipEnergyJ=33091;
            typed.tires[2].failed=true;typed.tires[2].failureReason="verified simulated puncture";
            string encoded=JsonUtility.ToJson(typed);
            var decoded=JsonUtility.FromJson<ComponentCondition>(encoded);
            check(!encoded.Contains("\"tires\"")
                &&decoded.elements.Length==4&&decoded.elements.All(e=>e is TireCondition)
                &&decoded.tires[2].id==typed.tires[2].id&&decoded.tires[2].health==typed.tires[2].health
                &&decoded.tires[2].consumedLife==typed.tires[2].consumedLife
                &&decoded.tires[2].temperatureC==typed.tires[2].temperatureC
                &&decoded.tires[2].distanceM==typed.tires[2].distanceM
                &&decoded.tires[2].slipEnergyJ==typed.tires[2].slipEnergyJ
                &&decoded.tires[2].failed&&decoded.tires[2].failureReason==typed.tires[2].failureReason,
                "SerializeReference stores the concrete tire member type and full worn history without a separate tire array");
            var typedCopy=decoded.Copy();typedCopy.tires[2].slipEnergyJ+=500;
            check(decoded.tires[2].slipEnergyJ!=typedCopy.tires[2].slipEnergyJ,
                "Typed member copies do not share independent tire history");
            var looseTire=new LooseEngineElement{
                definitionId="test-tire",tireUnit=true,
                element=new EngineElement{id="stored-test-tire",role="tire",cylinder=-1,valve=-1},
                tire=new TireCondition{health=.38f,consumedLife=.62,slipEnergyJ=444,temperatureC=76,distanceM=5000}
            };
            string looseJson=JsonUtility.ToJson(looseTire);
            var stored=JsonUtility.FromJson<LooseEngineElement>(looseJson);
            check(!looseJson.Contains("\"tire\"")
                &&stored.element is TireCondition
                &&stored.tire.id=="stored-test-tire"
                &&stored.tire.health==.38f&&stored.tire.consumedLife==.62
                &&stored.tire.slipEnergyJ==444&&stored.tire.temperatureC==76&&stored.tire.distanceM==5000,
                "Loose tires serialize once as specialized elements and retain stored wear history");
            var pistonElements=new ComponentCondition{
                elements=new EngineElement[]{
                    new PistonElement{id="test-piston",role="piston",cylinder=0,valve=-1},
                    new ValveElement{id="test-valve",role="intake valve",cylinder=0,valve=0}
                }
            };
            var engineRoundTrip=JsonUtility.FromJson<ComponentCondition>(JsonUtility.ToJson(pistonElements));
            check(engineRoundTrip.elements.Length==2
                &&engineRoundTrip.elements[0] is PistonElement
                &&engineRoundTrip.elements[1] is ValveElement,
                "Piston and valve component member types survive Unity JSON polymorphic serialization");
            var legacy=new ComponentCondition{health=.6f,slipEnergyJ=120,distanceM=800};
            TireDynamics.Ensure(legacy);
            check(legacy.tires.Length==4&&legacy.tires.All(t=>t.health==.6f&&t.distanceM==800&&t.slipEnergyJ==30)
                &&legacy.distanceM==0&&legacy.slipEnergyJ==0,"Legacy tire-set usage migrates once into per-tire histories");
            foreach(var vehicle in Resources.LoadAll<VehicleDefinition>("Data/Vehicles")){
                var spec=vehicle.spec.Copy();var tune=vehicle.CreateStockTune();tune.ClampTo(spec);
                var original=new WheelProfile(spec,tune);var inherited=tune.Copy();inherited.frontWheelDiameter=inherited.rearWheelDiameter=(float)tune.WheelDiameter;inherited.frontWheelWidth=inherited.rearWheelWidth=(float)tune.WheelWidth;inherited.frontTirePressurePsi=inherited.rearTirePressurePsi=tune.tirePressurePsi;
                var equal=new WheelProfile(spec,inherited);
                check(Math.Abs(original.vehicleMass-equal.vehicleMass)<1e-9&&Math.Abs(original.totalInertia-equal.totalInertia)<1e-9,"Legacy all-wheel settings equal explicit equal-axle settings: "+spec.id);
                var split=tune.Copy();split.frontWheelWidth=.5f;split.rearWheelWidth=1.4f;split.frontWheelDiameter=.9f;split.rearWheelDiameter=1.2f;split.frontTirePressurePsi=36;split.rearTirePressurePsi=23;
                var wheels=new WheelProfile(spec,split);
                check(wheels.assemblyMasses[0]<original.assemblyMasses[0]&&wheels.assemblyMasses[2]>original.assemblyMasses[2]&&wheels.radii[0]<wheels.radii[2],"Skinny front/wide tall rear changes actual independent mass and geometry: "+spec.id);
                check(Math.Abs(wheels.effectiveMass-wheels.vehicleMass-wheels.inertias.Select((v,i)=>v/(wheels.radii[i]*wheels.radii[i])).Sum())<1e-9,"Equivalent rolling mass sums each wheel's own radius/inertia once: "+spec.id);
                var onlyFront=tune.Copy();onlyFront.frontWheelWidth=.5f;var light=new WheelProfile(spec,onlyFront);
                check(light.vehicleMass<original.vehicleMass&&light.totalInertia<original.totalInertia&&light.radii[2]==original.radii[2],"Front-only reduction removes dead mass without changing rear gearing: "+spec.id);
                check(MassDynamics.Resolve(spec,onlyFront,light,spec.fuel.capacityLitres).frontWeightFraction<MassDynamics.Resolve(spec,tune,original,spec.fuel.capacityLitres).frontWeightFraction,"Removing front mass shifts real static weight distribution rearward: "+spec.id);
                if(spec.drive==DragLab.Domain.DriveType.RWD){var baseCar=new CarSimulation(spec,tune,new DriverSettings{wearMode=WearMode.None,tractionAssist=false,shifting=ShiftMode.Sequential});var lightCar=new CarSimulation(spec,onlyFront,new DriverSettings{wearMode=WearMode.None,tractionAssist=false,shifting=ShiftMode.Sequential});baseCar.Launch(0);lightCar.Launch(0);for(int i=0;i<120;i++){baseCar.Step(DragMath.FixedStep,new DriverInput{throttle=1});lightCar.Step(DragMath.FixedStep,new DriverInput{throttle=1});}check(lightCar.state.distance>baseCar.state.distance,"Lighter non-driven fronts improve a real RWD launch without extra HP: "+spec.id);}
                var copy=tune.Copy();copy.ApplyAdjustments(split);copy.ClampTo(spec);
                check(copy.AxlePressure(true)==36&&copy.AxlePressure(false)==23&&copy.frontWheelWidth==.5f&&copy.rearWheelDiameter==1.2f,"Apply/Copy/Clamp preserve separate axle settings: "+spec.id);
                var json=JsonUtility.FromJson<Tune>(JsonUtility.ToJson(copy));check(json.AxleDiameter(true)==copy.AxleDiameter(true)&&json.AxleWidth(false)==copy.AxleWidth(false),"Separate axle settings survive save JSON: "+spec.id);
                var driver=new DriverSettings{wearMode=WearMode.None,tractionAssist=false};var car=new CarSimulation(spec,split,driver);car.Launch(0);
                for(int i=0;i<240;i++)car.Step(DragMath.FixedStep,new DriverInput{throttle=1});
                check(car.state.contact.Validate()==null&&car.state.speed>0&&car.state.rpm>0,"Shared car simulation runs staggered axle geometry: "+spec.id);
                check(car.state.frontWheelRadiusM==car.wheels.radii[0]&&car.state.rearWheelRadiusM==car.wheels.radii[2],"Tire sound/smoke feedback receives actual axle radii: "+spec.id);
            }
            var catalogue=new MechanicalCatalog(Resources.LoadAll<VehicleDefinition>("Data/Vehicles").Select(v=>v.spec));
            var categoryTypes=new[]{
                PartCategory.Pistons,PartCategory.Rods,PartCategory.PistonRings,
                PartCategory.Bearings,PartCategory.Valves,PartCategory.ValveSprings,
                PartCategory.ValveSeals,PartCategory.Followers,PartCategory.Wheels,
                PartCategory.Ignition,PartCategory.SparkPlugs,PartCategory.GlowPlugs,
                PartCategory.FuelSystem
            };
            foreach(var category in categoryTypes)
            {
                var created=EngineElements.Create(category);
                created.id="type-test/"+category;created.definitionId="test-spec";
                created.role=category==PartCategory.Wheels?"wheel":"test member";
                created.cylinder=-1;created.valve=-1;created.health=.63f;
                created.severity=.24f;created.wearRemainder=.00000043;
                var beforeType=created.GetType();
                var payload=new ComponentCondition{elements=new[]{created}};
                var restored=JsonUtility.FromJson<ComponentCondition>(JsonUtility.ToJson(payload));
                var copied=payload.Copy();
                check(restored.elements.Length==1&&restored.elements[0].GetType()==beforeType
                    &&EngineElements.MatchesCategory(category,restored.elements[0])
                    &&restored.elements[0].id==created.id&&restored.elements[0].health==created.health
                    &&restored.elements[0].severity==created.severity
                    &&restored.elements[0].wearRemainder==created.wearRemainder
                    &&copied.elements[0].GetType()==beforeType&&!ReferenceEquals(copied.elements[0],created),
                    "Each individual component category survives typed Unity JSON and independent copying: "+category);
            }
            var mispositionedPiston=new PistonElement{id="invalid-piston",role="piston",cylinder=1,valve=2};
            var looseValveWithIndex=new ValveElement{id="invalid-valve",role="intake valve",cylinder=-1,valve=1};
            var mislabeledWheel=new WheelElement{id="invalid-wheel",role="injector",cylinder=0,valve=-1};
            check(mispositionedPiston.Validate()!=null&&looseValveWithIndex.Validate()!=null
                &&mislabeledWheel.Validate()!=null,
                "Category-specific topology rules reject valve indexes on pistons, positioned loose valves and wrong wheel roles");
            check(!EngineElements.MatchesCategory(PartCategory.Rods,EngineElements.Create(PartCategory.Pistons))
                &&!EngineElements.MatchesCategory(PartCategory.Tires,EngineElements.Create(PartCategory.Wheels)),
                "Physical subtype matching prevents cross-category insertion despite shared EngineElement ancestry");
            var donor=new IgnitionElement{id="split/ignition",role="spark plug / lead",cylinder=1,valve=-1,health=.37f,severity=.42f,wearRemainder=.00003,fault=ElementFault.Disabled};
            var convertedSparkPlug=EngineElements.CopyAsCategory(PartCategory.SparkPlugs,donor);
            check(convertedSparkPlug is SparkPlugElement&&convertedSparkPlug.id==donor.id&&convertedSparkPlug.health==donor.health
                &&convertedSparkPlug.fault==donor.fault&&convertedSparkPlug.severity==donor.severity
                &&convertedSparkPlug.wearRemainder==donor.wearRemainder
                &&!EngineElements.FaultAllowed(PartCategory.Ignition,convertedSparkPlug),
                "Ignition-kit split preserves physical spark plug wear and rejects wrong-category member types");
            foreach(var kind in new[]{PartCategory.Rods,PartCategory.ValveSprings,PartCategory.SparkPlugs})
            {
                var model=Resources.LoadAll<VehicleDefinition>("Data/Vehicles")
                    .FirstOrDefault(v=>catalogue.Recipe(v.spec.id).ContainsKey(kind));
                check(model!=null,"Integration fixture includes "+kind+" hardware");
                var test=PlayerProfile.New(DateTime.UtcNow.Ticks);
                MechanicsOwnership.Ensure(test,catalogue);
                test.cash=1000000;test.reputation=1000;
                VehicleCollection.Buy(test,catalogue,new VehicleOffer{
                    model=model.spec.id,price=1,reputation=0,stock=model.CreateStockTune(),paint="#FFFFFF"});
                var owned=test.SelectedCar;
                var bind=owned.installed.slots.Find(b=>b.slot==kind.ToString());
                var pack=bind==null?null:test.parts.Find(p=>p.instanceId==bind.partInstanceId);
                check(pack!=null&&pack.condition.elements.Length>0
                    &&pack.condition.elements.All(e=>EngineElements.MatchesCategory(kind,e)),
                    "Installed "+kind+" pack has only its dedicated element class");
                var removed=pack.condition.elements[0];
                removed.health=.58f;removed.wearRemainder=.000004;removed.severity=.15f;
                string removedId=removed.id;
                var originalState=JsonUtility.ToJson(removed);
                string initial=JsonUtility.ToJson(test);
                PlayerProfile result;string installError;int saves=0;
                bool applied=MechanicsTransactions.Try(test,catalogue,test.revision,editable=>{
                    var incoming=InventoryUnits.Buy(editable,owned.instanceId,pack.definitionId,removed.role);
                    if(!EngineElements.MatchesCategory(kind,incoming.element))
                        throw new InvalidOperationException("Bought unit was not category-specialized.");
                    InventoryUnits.Install(editable,owned.instanceId,incoming.element.id,removedId);
                },updated=>{saves++;return true;},out result,out installError);
                check(applied&&saves==1&&initial==JsonUtility.ToJson(test),
                    "Atomic "+kind+" member replacement leaves original profile untouched: "+installError);
                var fitted=result.parts.Find(p=>p.instanceId==pack.instanceId);
                var loose=result.looseEngineElements.Find(u=>u.element.id==removedId);
                check(loose!=null&&EngineElements.MatchesCategory(kind,loose.element)
                    &&JsonUtility.ToJson(loose.element)==originalState
                    &&fitted.condition.elements[0].id!=removedId
                    &&EngineElements.MatchesCategory(kind,fitted.condition.elements[0])
                    &&MechanicsOwnership.Validate(result,catalogue)==null,
                    "Installed "+kind+" replacement preserves removed physical member and saved wear");
            }
            foreach(var vehicle in Resources.LoadAll<VehicleDefinition>("Data/Vehicles")){
                var profile=PlayerProfile.New(DateTime.UtcNow.Ticks);MechanicsOwnership.Ensure(profile,catalogue);profile.cash=1000000;profile.reputation=1000;
                VehicleCollection.Buy(profile,catalogue,new VehicleOffer{model=vehicle.spec.id,price=1,reputation=0,stock=vehicle.CreateStockTune(),paint="#FFFFFF"});var owned=profile.SelectedCar;
                var pack=profile.parts.First(p=>p.instanceId==owned.installed.slots.First(s=>s.slot=="Tires").partInstanceId);TireDynamics.Ensure(pack.condition);
                foreach(var t in pack.condition.tires)t.id=MechanicsOwnership.NewId("tire");pack.condition.tires[2].health=.65f;pack.condition.tires[2].consumedLife=.35;pack.condition.health=.65f;
                string front=JsonUtility.ToJson(pack.condition.tires[0]),before=JsonUtility.ToJson(profile);int cash=profile.cash;string definition=catalogue.Id(owned.vehicleId,PartCategory.Tires,3);
                var lines=new[]{new PartCartLine{action=CartAction.NewUnits,definitionId=definition,role="tire",quantity=2,install=true,positions=new System.Collections.Generic.List<string>{"tire:2","tire:3"}}};
                var preview=PartCart.Preview(profile,owned,lines);var prepared=CarInspection.PrepareCar(preview);
                check(before==JsonUtility.ToJson(profile)&&prepared.mechanics.TireDefinition(0).id==pack.definitionId&&prepared.mechanics.TireDefinition(2).id==definition,"Mixed rear-compound cart previews actual corner definitions without changing owned assets: "+vehicle.spec.id);
                long now=DateTime.UtcNow.Ticks;var quote=PartCart.Quote(profile,owned.instanceId,lines,now,60);string packId=pack.instanceId;int writes=0;PlayerProfile next;string error;
                bool accepted=MechanicsTransactions.Try(profile,catalogue,profile.revision,p=>PartCart.Checkout(p,quote,now),p=>{writes++;return true;},out next,out error);check(accepted&&writes==1,"Mixed tire cart publishes one transaction: "+vehicle.spec.id+" / "+error);profile=next;owned=profile.SelectedCar;pack=profile.parts.Find(p=>p.instanceId==packId);
                check(profile.cash==cash-2*InventoryPricing.NewUnit(catalogue.Find(definition),4)&&front==JsonUtility.ToJson(pack.condition.tires[0])&&TireFitments.DefinitionId(pack,2)==definition&&profile.looseEngineElements.Any(u=>u.tireUnit&&u.definitionId==pack.definitionId&&u.tire.health==.65f),"Atomic mixed-compound checkout keeps exact fronts and removed worn originals: "+vehicle.spec.id);
                var actual=MechanicsOwnership.Resolve(profile,owned,catalogue);var car=new CarSimulation(actual.spec,actual.tune,new DriverSettings(),new[]{pack}.Concat(profile.parts.Where(p=>p.instanceId!=pack.instanceId&&owned.installed.slots.Any(s=>s.partInstanceId==p.instanceId))));
                check(car.mechanics.TireDefinition(0).grip<car.mechanics.TireDefinition(2).grip&&car.mechanics.TireDefinition(0).tireLifeDistanceM>car.mechanics.TireDefinition(2).tireLifeDistanceM,"Mixed compounds retain independent grip/life ratings in the shared simulation: "+vehicle.spec.id);
                car.ApplyRaceTemperatures(RaceTemperatureMode.Optimal,0,DateTime.UtcNow.Ticks);var simTires=car.mechanics.Part(PartCategory.Tires);check(simTires.condition.tires[0].temperatureC==car.mechanics.TireDefinition(0).tireOptimalC&&simTires.condition.tires[2].temperatureC==car.mechanics.TireDefinition(2).tireOptimalC,"Optimal starts use each actual compound's own temperature: "+vehicle.spec.id);
                car.state.optimalThermals=true;car.state.distance=1000;car.state.frontLoadN=car.state.rearLoadN=8000;for(int i=0;i<4;i++)car.state.contact.slipWorkJ[i]=12000;TireDynamics.Tick(car,1,0);
                check(simTires.condition.tires[2].health<simTires.condition.tires[0].health,"Equal controlled use wears rear drag compound sooner than front road compound: "+vehicle.spec.id);
                check(MechanicsOwnership.Validate(profile,catalogue)==null,"Mixed tire identities and condition remain valid: "+vehicle.spec.id+" / "+MechanicsOwnership.Validate(profile,catalogue));
                var restored=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(profile));MechanicsOwnership.Ensure(restored,catalogue);var restoredPack=restored.parts.Find(p=>p.instanceId==pack.instanceId);
                check(restoredPack.condition.tires[2].definitionId==definition&&restored.looseEngineElements.Any(u=>u.tireUnit&&u.tire.health==.65f),"Mixed compounds and removed wear survive actual save normalization: "+vehicle.spec.id);
                SavedSetups.Save(profile,owned.instanceId,"Staggered build",SetupScope.WholeCar);var setup=profile.savedSetups.Last();
                var originals=profile.looseEngineElements.Where(u=>u.tireUnit&&u.definitionId==pack.definitionId).ToArray();InventoryUnits.Install(profile,owned.instanceId,originals[0].element.id,"tire:2");InventoryUnits.Install(profile,owned.instanceId,originals[1].element.id,"tire:3");
                var plan=SavedSetups.Preview(profile,owned.instanceId,setup.id);check(plan.tires.Count==2&&plan.tires.All(t=>!t.purchase)&&plan.cost==0,"Whole-car blueprints find exact loose compounds for the saved corners: "+vehicle.spec.id);
                SavedSetups.Apply(profile,owned.instanceId,setup.id,profile.revision,false,plan.cost);check(TireFitments.DefinitionId(pack,2)==definition&&TireFitments.DefinitionId(pack,3)==definition&&MechanicsOwnership.Validate(profile,catalogue)==null,"Existing setup transaction restores mixed tires without granting replacements: "+vehicle.spec.id);
                var swapProfile=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(profile));MechanicsOwnership.Ensure(swapProfile,catalogue);
                var swapCar=swapProfile.SelectedCar;
                var swapPack=swapProfile.parts.Find(p=>p.instanceId==pack.instanceId);
                var swapSources=swapProfile.looseEngineElements.Where(u=>u.tireUnit).Take(2).ToArray();
                check(swapSources.Length==2,"Removed individual tires remain available for rear-axle swapping: "+vehicle.spec.id);
                string leftBefore=JsonUtility.ToJson(swapPack.condition.tires[0]),rightBefore=JsonUtility.ToJson(swapPack.condition.tires[1]);
                var outgoingRear=swapPack.condition.tires.Skip(2).Select(t=>t.id).ToArray();
                for(int i=0;i<2;i++){swapSources[i].tire.health=.4f+i*.1f;swapSources[i].tire.consumedLife=1-swapSources[i].tire.health;swapSources[i].tire.slipEnergyJ=18000+i*1000;swapSources[i].tire.distanceM=7200+i*100;swapSources[i].element.health=swapSources[i].tire.health;}
                var sourceHistory=swapSources.Select(u=>JsonUtility.ToJson(u.tire)).ToArray();
                var rearSwap=new[]{new TireSwapAssignment(swapSources[0].element.id,2),new TireSwapAssignment(swapSources[1].element.id,3)};
                int swapWrites=0;PlayerProfile swapped;string swapError;
                bool swapOk=MechanicsTransactions.Try(swapProfile,catalogue,swapProfile.revision,p=>InventoryUnits.SwapTires(p,swapCar.instanceId,rearSwap),p=>{swapWrites++;return true;},out swapped,out swapError);
                check(swapOk&&swapWrites==1,"Two worn rear tires are installed atomically: "+vehicle.spec.id+" / "+swapError);
                var fitted=swapped.parts.Find(p=>p.instanceId==swapPack.instanceId);
                check(JsonUtility.ToJson(fitted.condition.tires[0])==leftBefore&&JsonUtility.ToJson(fitted.condition.tires[1])==rightBefore
                    &&JsonUtility.ToJson(fitted.condition.tires[2])==sourceHistory[0]&&JsonUtility.ToJson(fitted.condition.tires[3])==sourceHistory[1]
                    &&outgoingRear.All(id=>swapped.looseEngineElements.Any(u=>u.tireUnit&&u.element.id==id)),
                    "Axle swap preserves untouched front tires, incoming worn histories, and both displaced originals: "+vehicle.spec.id);
                int refusedWrites=0;PlayerProfile refused;string refusedError;string oldJson=JsonUtility.ToJson(swapped);
                bool invalidSwap=MechanicsTransactions.Try(swapped,catalogue,swapped.revision,p=>InventoryUnits.SwapTires(p,swapCar.instanceId,
                    new[]{new TireSwapAssignment(outgoingRear[0],0),new TireSwapAssignment(outgoingRear[0],1)}),p=>{refusedWrites++;return true;},out refused,out refusedError);
                check(!invalidSwap&&refusedWrites==0&&oldJson==JsonUtility.ToJson(swapped),"Duplicate tire selection rolls back without publishing a partial installation: "+vehicle.spec.id);
                pack.condition.tires[0].health=.2f;pack.condition.tires[0].consumedLife=.8;pack.condition.health=.2f;string retained=JsonUtility.ToJson(pack.condition);string oldPack=pack.instanceId;
                var request=new PaymentRequest{kind=PaymentKind.Part,carId=owned.instanceId,targetId=pack.definitionId,install=true,preserveTireFitments=true};long repairNow=DateTime.UtcNow.Ticks;var policy=new OperatingPolicy();var payment=PaidEconomy.Quote(profile,catalogue,policy,ValuationPolicy.Defaults(),request,repairNow);
                int expected=Enumerable.Range(0,4).Sum(i=>InventoryPricing.NewUnit(catalogue.Find(TireFitments.DefinitionId(pack,i)),4));check(payment.Cost==expected,"Buy same quotes actual mixed constituent costs: "+vehicle.spec.id);
                int prior=profile.cash;PlayerProfile repaired;string repairError;bool repairedOk=MechanicsTransactions.Try(profile,catalogue,profile.revision,p=>PaidEconomy.Pay(p,catalogue,policy,ValuationPolicy.Defaults(),payment,repairNow),p=>true,out repaired,out repairError);
                var repairedPack=repaired.parts.Find(p=>p.instanceId==repaired.SelectedCar.installed.slots.First(b=>b.slot=="Tires").partInstanceId);
                check(repairedOk&&repaired.cash==prior-expected&&TireFitments.DefinitionId(repairedPack,2)==definition&&repairedPack.condition.tires.All(t=>t.health==1)&&repaired.parts.Find(p=>p.instanceId==oldPack).location==PartLocation.Inventory&&JsonUtility.ToJson(repaired.parts.Find(p=>p.instanceId==oldPack).condition)==retained,"Mixed Buy same preserves specifications and worn originals in one paid transaction: "+vehicle.spec.id+" / "+repairError);

                CheckWheelMembers(repaired,check);
            }

            CheckCenterRatios(catalogue,check);
            foreach(bool locked in new[]{false,true})foreach(double frontI in new[]{.2,1.2,4.0})foreach(double rearI in new[]{.4,2.0,8.0}){
                var input=new WheelContactInput{radii=new[]{.24,.24,.36,.36},inertias=new[]{frontI,frontI,rearI,rearI},lockCenter=locked,frontTorqueNm=100,rearTorqueNm=900};
                var state=new WheelContactState();for(int tick=0;tick<360;tick++){if(tick==180){input.frontTorqueNm=input.rearTorqueNm=0;input.brakeNm=new[]{600.0,600,600,600};}WheelContactDynamics.Step(state,input,DragMath.FixedStep);}
                double closure=state.KineticEnergy(input.massKg,input.inertias)+state.contactWorkJ+state.brakeWorkJ+state.resistanceWorkJ-state.driveWorkJ;
                check(state.Validate()==null&&Math.Abs(closure)<Math.Max(1e-6,Math.Abs(state.driveWorkJ)*1e-10),"Unequal-axle torque/braking energy closes: "+locked+" / "+frontI+" / "+rearI);
                if(locked)check(Math.Abs(state.omega[0]+state.omega[1]-state.omega[2]-state.omega[3])<1e-8,"Locked AWD constrains angular shaft speeds even with mismatched tire diameters");
            }
            var folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/axle-wheels"));Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"geometry-verification.txt"),log+"Checks: "+count+" / failures: 0\n",new UTF8Encoding(false));Debug.Log("Axle wheel geometry passed: "+count);
        }
    }
}

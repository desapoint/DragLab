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
        public static void Run()
        {
            var log=new StringBuilder();int count=0;
            Action<bool,string> check=(ok,label)=>{log.AppendLine(((ok?"PASS: ":"FAIL: ")+label).TrimEnd());if(!ok)throw new InvalidOperationException(label);count++;};
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
                pack.condition.tires[0].health=.2f;pack.condition.tires[0].consumedLife=.8;pack.condition.health=.2f;string retained=JsonUtility.ToJson(pack.condition);string oldPack=pack.instanceId;
                var request=new PaymentRequest{kind=PaymentKind.Part,carId=owned.instanceId,targetId=pack.definitionId,install=true,preserveTireFitments=true};long repairNow=DateTime.UtcNow.Ticks;var policy=new OperatingPolicy();var payment=PaidEconomy.Quote(profile,catalogue,policy,ValuationPolicy.Defaults(),request,repairNow);
                int expected=Enumerable.Range(0,4).Sum(i=>InventoryPricing.NewUnit(catalogue.Find(TireFitments.DefinitionId(pack,i)),4));check(payment.Cost==expected,"Buy same quotes actual mixed constituent costs: "+vehicle.spec.id);
                int prior=profile.cash;PlayerProfile repaired;string repairError;bool repairedOk=MechanicsTransactions.Try(profile,catalogue,profile.revision,p=>PaidEconomy.Pay(p,catalogue,policy,ValuationPolicy.Defaults(),payment,repairNow),p=>true,out repaired,out repairError);
                var repairedPack=repaired.parts.Find(p=>p.instanceId==repaired.SelectedCar.installed.slots.First(b=>b.slot=="Tires").partInstanceId);
                check(repairedOk&&repaired.cash==prior-expected&&TireFitments.DefinitionId(repairedPack,2)==definition&&repairedPack.condition.tires.All(t=>t.health==1)&&repaired.parts.Find(p=>p.instanceId==oldPack).location==PartLocation.Inventory&&JsonUtility.ToJson(repaired.parts.Find(p=>p.instanceId==oldPack).condition)==retained,"Mixed Buy same preserves specifications and worn originals in one paid transaction: "+vehicle.spec.id+" / "+repairError);

                CheckWheelMembers(repaired,check);
            }

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

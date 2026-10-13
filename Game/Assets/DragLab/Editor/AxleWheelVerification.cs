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
        public static void Run()
        {
            var log=new StringBuilder();int count=0;
            Action<bool,string> check=(ok,label)=>{log.AppendLine((ok?"PASS: ":"FAIL: ")+label);if(!ok)throw new InvalidOperationException(label);count++;};
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
                check(MechanicsOwnership.Validate(profile,catalogue)==null,"Mixed tire identities and condition remain valid: "+vehicle.spec.id+" / "+MechanicsOwnership.Validate(profile,catalogue));
                var restored=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(profile));MechanicsOwnership.Ensure(restored,catalogue);var restoredPack=restored.parts.Find(p=>p.instanceId==pack.instanceId);
                check(restoredPack.condition.tires[2].definitionId==definition&&restored.looseEngineElements.Any(u=>u.tireUnit&&u.tire.health==.65f),"Mixed compounds and removed wear survive actual save normalization: "+vehicle.spec.id);
                SavedSetups.Save(profile,owned.instanceId,"Staggered build",SetupScope.WholeCar);var setup=profile.savedSetups.Last();
                var originals=profile.looseEngineElements.Where(u=>u.tireUnit&&u.definitionId==pack.definitionId).ToArray();InventoryUnits.Install(profile,owned.instanceId,originals[0].element.id,"tire:2");InventoryUnits.Install(profile,owned.instanceId,originals[1].element.id,"tire:3");
                var plan=SavedSetups.Preview(profile,owned.instanceId,setup.id);check(plan.tires.Count==2&&plan.tires.All(t=>!t.purchase)&&plan.cost==0,"Whole-car blueprints find exact loose compounds for the saved corners: "+vehicle.spec.id);
                SavedSetups.Apply(profile,owned.instanceId,setup.id,profile.revision,false,plan.cost);check(TireFitments.DefinitionId(pack,2)==definition&&TireFitments.DefinitionId(pack,3)==definition&&MechanicsOwnership.Validate(profile,catalogue)==null,"Existing setup transaction restores mixed tires without granting replacements: "+vehicle.spec.id);
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

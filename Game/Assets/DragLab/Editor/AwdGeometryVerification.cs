using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
namespace DragLab.Editor
{
    public static class AwdGeometryVerification
    {
        static CarSimulation Car(AwdMode mode,double front=.8,double rear=1.2,double weight=.5,bool primary=true,double preload=.5,double grip=1)
        {
            var spec=new VehicleSpec{drive=DragLab.Domain.DriveType.AWD,frontWeightFraction=(float)weight,awd=new AwdSpec{mode=mode,primaryFront=primary,preload=(float)preload}};
            var tune=new Tune{frontWheelDiameter=(float)front,rearWheelDiameter=(float)rear,frontWheelWidth=.6f,rearWheelWidth=1.3f,frontTirePressurePsi=35,rearTirePressurePsi=23,frontLeftGrip=(float)grip};
            var driver=new DriverSettings{wearMode=WearMode.None,tractionAssist=false,throttleAssist=false,shifting=ShiftMode.Sequential,fuelUsage=FuelUsageMode.None,fluidDepletion=ConsumptionMode.None};
            var car=new CarSimulation(spec,tune,driver);car.Launch(0);RoadFixture.Seed(car,12,1,true);return car;
        }
        public static void Run()
        {
            var log=new StringBuilder();int checks=0,failures=0;
            Action<bool,string> check=(ok,label)=>{checks++;if(!ok)failures++;log.AppendLine((ok?"PASS: ":"FAIL: ")+label);};
            foreach(AwdMode mode in Enum.GetValues(typeof(AwdMode))){
                var car=Car(mode);for(int i=0;i<500;i++)AxleDriveDynamics.TorqueSplitFromCapacities(car,1000,3000,DragMath.FixedStep);
                check(mode==AwdMode.ActiveBias||mode==AwdMode.LockedCenter?Math.Abs(car.state.frontTorqueShare-.25)<.0001:mode==AwdMode.OnDemand||car.state.frontTorqueShare==car.awd.frontBias,"Hardware versus adaptive center-capacity distribution: "+mode);
                var coast=Car(mode,preload:0);for(int i=0;i<240;i++)coast.IndependentStep(DragMath.FixedStep,new DriverInput(),true,false);
                bool free=mode!=AwdMode.LockedCenter&&mode!=AwdMode.LimitedSlipCenter;
                check(coast.state.contact.Validate()==null&&(!free||Math.Abs(coast.FrontWheelOmega*coast.wheels.radii[0]-coast.RearWheelOmega*coast.wheels.radii[2])<.01),"Different rolling sizes retain their own physical angular speed while coasting: "+mode);
                check(!free||coast.state.contact.centerCouplingWorkJ==0,"A free/open/disengaged center does not invent coupling heat from angular-speed difference: "+mode);
            }
            foreach(bool primary in new[]{false,true}){
                var demand=Car(AwdMode.OnDemand,primary:primary,preload:0);demand.state.throttle=1;
                for(int i=0;i<240;i++)AxleDriveDynamics.TorqueSplitFromCapacities(demand,1000,3000,DragMath.FixedStep);
                check(demand.state.awdSecondaryShare==0,"Different tire RPM alone cannot engage reactive on-demand AWD: "+primary);
                if(primary)demand.state.frontSlip=.3;else demand.state.rearSlip=.3;
                AxleDriveDynamics.TorqueSplitFromCapacities(demand,1000,3000,DragMath.FixedStep);
                check(demand.state.awdSecondaryShare>0&&demand.state.awdSecondaryShare<.05,"On-demand engagement responds gradually to actual tire/road slip: "+primary);
                var loaded=Car(AwdMode.OnDemand,primary:primary);loaded.state.throttle=1;var light=Car(AwdMode.OnDemand,primary:primary);light.state.throttle=1;
                for(int i=0;i<240;i++){AxleDriveDynamics.TorqueSplitFromCapacities(loaded,primary?1000:3000,primary?3000:1000,DragMath.FixedStep);AxleDriveDynamics.TorqueSplitFromCapacities(light,primary?3000:1000,primary?1000:3000,DragMath.FixedStep);}
                check(loaded.state.awdSecondaryShare>light.state.awdSecondaryShare,"Preemptive on-demand share responds to secondary axle usable torque capacity: "+primary);
                demand.awd.maximumSecondary=0;for(int i=0;i<600;i++)AxleDriveDynamics.TorqueSplitFromCapacities(demand,1000,3000,DragMath.FixedStep);
                check(demand.state.awdSecondaryShare<1e-8,"Disabled on-demand secondary decays to disengaged: "+primary);
            }
            foreach(double front in new[]{.8,1.0,1.2})foreach(double rear in new[]{.8,1.0,1.2})foreach(bool primary in new[]{false,true}){
                var p=new WheelContactInput{radii=new[]{.3*front,.3*front,.3*rear,.3*rear},inertias=new[]{.7,.9,1.3,1.7},centerClutchCapacityNm=150};p.frontTorqueNm=primary?400:0;p.rearTorqueNm=primary?0:400;
                var s=new WheelContactState{speed=15,omega=p.radii.Select(r=>15/r).ToArray()};double initial=s.KineticEnergy(p.massKg,p.inertias);
                for(int i=0;i<300;i++)WheelContactDynamics.Step(s,p,DragMath.FixedStep);
                double closure=s.KineticEnergy(p.massKg,p.inertias)-initial+s.contactWorkJ+s.brakeWorkJ+s.resistanceWorkJ-s.driveWorkJ;
                check(Math.Abs(closure)<Math.Max(.00001,Math.Abs(s.driveWorkJ)*1e-9)&&s.centerCouplingWorkJ>=0,"Clutch transfer conserves signed work and dissipates real slip energy: "+front+" / "+rear+" / "+primary);
            }
            foreach(AwdMode mode in Enum.GetValues(typeof(AwdMode)))foreach(double front in new[]{.8,1.0,1.2})foreach(double weight in new[]{.3,.7})foreach(double grip in new[]{.15,1})foreach(bool primary in new[]{false,true}){
                var a=Car(mode,front,1,weight,primary,grip:grip);var b=Car(mode,front,1,weight,primary,grip:grip);double heat=0;
                for(int tick=0;tick<480;tick++){var input=new DriverInput{throttle=tick<300?1:0,shiftUp=tick==140||tick==260,brake=tick>=420?.2:0};a.Step(DragMath.FixedStep,input);b.Step(DragMath.FixedStep,input);heat+=a.state.awdCouplingLossPowerW*DragMath.FixedStep;}
                string id=mode+" / front "+front+" / load "+weight+" / grip "+grip+" / primary "+primary;
                check(a.state.contact.Validate()==null&&a.state.rpm>=a.spec.idleRpm&&a.state.frontLoadN>=0&&a.state.rearLoadN>=0&&Math.Abs(a.state.frontLoadN+a.state.rearLoadN-a.mass.TranslatingKg*DragMath.Gravity)<1e-6,"Shared powered/shift/lift/brake simulation remains finite with independent geometry and axle loads: "+id);
                check(JsonUtility.ToJson(a.CaptureDynamics())==JsonUtility.ToJson(b.CaptureDynamics()),"Identical varied AWD inputs repeat exactly: "+id);
                check(Math.Abs(heat-a.state.contact.centerCouplingWorkJ)<1e-6,"Per-tick AWD heat agrees with accumulated coupling work: "+id);
            }
            var shiftCoupling=Car(AwdMode.OnDemand);for(int i=0;i<120;i++)shiftCoupling.Step(DragMath.FixedStep,new DriverInput{throttle=1});double beforeWork=shiftCoupling.state.contact.centerCouplingWorkJ;
            for(int i=0;i<30;i++)shiftCoupling.IndependentStep(DragMath.FixedStep,new DriverInput{throttle=1},true,false);
            check(shiftCoupling.state.awdCouplingReferenceTorqueNm>0&&shiftCoupling.state.contact.centerCouplingWorkJ>beforeWork,"Engaged on-demand AWD preserves wheel momentum transfer through an open gearbox clutch");
            var frontLoaded=Car(AwdMode.ActiveBias,weight:.7);var rearLoaded=Car(AwdMode.ActiveBias,weight:.3);
            for(int i=0;i<300;i++){frontLoaded.IndependentStep(DragMath.FixedStep,new DriverInput(),true,false);rearLoaded.IndependentStep(DragMath.FixedStep,new DriverInput(),true,false);}
            check(frontLoaded.state.frontTorqueShare>rearLoaded.state.frontTorqueShare+.15,"Active AWD reacts to actual mass/placement-derived axle loads before power slip is required");
            var rimSpec=new VehicleSpec();var reference=new WheelProfile(rimSpec,new Tune());double rim=4*(rimSpec.wheels.assemblyMassKg-Math.Min(6,rimSpec.wheels.assemblyMassKg*.6));
            rimSpec.cornerWheelDefinitions=Enumerable.Range(0,4).Select(i=>new PartDefinition{category=PartCategory.Wheels,massKg=(float)rim}).ToArray();rimSpec.cornerWheelDefinitions[0].massKg*=.5f;
            var oneLight=new WheelProfile(rimSpec,new Tune());check(Math.Abs(reference.vehicleMass-oneLight.vehicleMass-rim*.125)<1e-5,"One lighter rim changes total/axle mass once, without cloning its mass to the opposite corner");
            foreach(AwdMode mode in new[]{AwdMode.OnDemand,AwdMode.LimitedSlipCenter}){
                var car=Car(mode);car.driver.wearMode=WearMode.Full;double start=car.mechanics.Part(PartCategory.TransferCase).condition.temperatureC;
                for(int i=0;i<240;i++)car.Step(DragMath.FixedStep,new DriverInput{throttle=1});
                check(car.state.contact.centerCouplingWorkJ>0&&car.mechanics.Part(PartCategory.TransferCase).condition.temperatureC>start,"Actual unequal-speed coupling slip heats the physical transfer case: "+mode);
            }
            foreach(AwdMode mode in Enum.GetValues(typeof(AwdMode))){
                var car=Car(mode);var source=new InspectionBuild{build=new BuildResolution{spec=car.spec,tune=car.tune},components=car.mechanics.parts.Select(p=>p.Copy()).ToList(),startingFuel=new FuelState{litres=car.state.fuelLitres},startingFluids=car.state.fluids.Copy()};
                var inspection=CarInspection.CalculatePrepared(source,car,false);bool warns=mode==AwdMode.OnDemand||mode==AwdMode.LimitedSlipCenter||mode==AwdMode.LockedCenter;
                check(inspection.warnings.Any(w=>w.title=="AWD tire rolling sizes differ")==warns,"Rolling mismatch warnings describe actual center hardware rather than treating all AWD as locked: "+mode);
            }
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/awd-geometry"));Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"verification.txt"),log+"Checks: "+checks+" / failures: "+failures+"\n",new UTF8Encoding(false));if(failures>0)throw new InvalidOperationException("AWD geometry failures: "+failures);Debug.Log("AWD GEOMETRY PASS: "+checks);
        }
    }
}

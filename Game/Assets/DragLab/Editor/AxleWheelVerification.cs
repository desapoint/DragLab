using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;

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

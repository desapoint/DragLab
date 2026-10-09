using System;
using System.IO;
using System.Text;
using UnityEngine;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
namespace DragLab.Editor {
 public static class AwdContactVerification {
  public static void RunStandalone(){Debug.Log("AWD CONTACT PASS: "+Run(Path.GetFullPath(Path.Combine(Application.dataPath,"../.."))));}
  static CarSimulation Car(AwdMode mode,double preload,bool broken=false){var spec=new VehicleSpec {drive=DriveType.AWD,awd=new AwdSpec {mode=mode,centerPreloadNm=(float)preload,torqueBiasRatio=3}};var tune=new Tune {frontLeftGrip=0,frontRightGrip=0};var car=new CarSimulation(spec,tune,new DriverSettings {shifting=ShiftMode.Sequential,throttleAssist=false,tractionAssist=false,wearMode=WearMode.None});if(broken){var p=car.mechanics.parts.Find(x=>x.slot=="FrontDriveshaft");p.condition.health=0;p.condition.failed=true;p.condition.failureReason="Fixture disconnect";}car.Launch(0);return car;}
  public static int Run(string root){int count=0;var log=new StringBuilder();Action<bool,string> check=(ok,label)=>{log.AppendLine((ok?"PASS: ":"FAIL: ")+label);Directory.CreateDirectory(Path.Combine(root,"reports/phases/2h"));File.WriteAllText(Path.Combine(root,"reports/phases/2h/awd-verification.txt"),log.ToString());if(!ok)throw new InvalidOperationException(label);count++;};
   foreach(bool broken in new[]{false,true}) {var open=Car(AwdMode.OpenCenter,0,broken);var preload=Car(AwdMode.LimitedSlipCenter,100,broken);double powerOpen=0,powerUseful=0;for(int tick=0;tick<960;tick++){open.Step(DragMath.FixedStep,new DriverInput {throttle=1});preload.Step(DragMath.FixedStep,new DriverInput {throttle=1});powerOpen+=Math.Max(0,open.state.frontShaftPowerW)*DragMath.FixedStep;powerUseful+=Math.Max(0,preload.state.rearShaftPowerW)*DragMath.FixedStep;}
    string id=broken?"disconnected front shaft":"zero-grip connected front axle";
    check(!open.state.engineFailed&&!preload.state.engineFailed&&open.state.contact.Validate()==null&&preload.state.contact.Validate()==null,"Finite healthy engine with "+id);
    check(preload.state.speed>open.state.speed+1,"Center LSD physical preload increases surviving axle propulsion: "+id+" / open "+open.state.speed+" / LSD "+preload.state.speed);
    check(powerOpen>0&&powerUseful>0,"Shaft work telemetry observes free and useful paths: "+id);
   }
   var locked=Car(AwdMode.LockedCenter,0);for(int tick=0;tick<360;tick++)locked.Step(DragMath.FixedStep,new DriverInput {throttle=1});check(Math.Abs(locked.FrontWheelOmega-locked.RearWheelOmega)<1e-8&&locked.state.speed>1,"Locked center equates axle means under split grip");
   var guarded=new CarSimulation(new VehicleSpec(),new Tune {partsResolved=true,transmissionUpgrade=TransmissionUpgrade.DCT},new DriverSettings {shifting=ShiftMode.Sequential});guarded.Launch(0);guarded.state.gear=2;RoadFixture.Seed(guarded,2);guarded.state.rpm=9000;check(guarded.Shift(-1,new DriverInput()),"Safe wheel-imposed downshift remains allowed despite harmless crank flare");
   guarded.state.shiftRemaining=0;guarded.state.gear=2;RoadFixture.Seed(guarded,30);guarded.state.rpm=900;check(!guarded.Shift(-1,new DriverInput()),"Dangerous wheel-imposed downshift blocked despite low crank RPM");
   File.AppendAllText(Path.Combine(root,"reports/phases/2h/awd-verification.txt"),"Checks: "+count);return count;
  }
 }
}

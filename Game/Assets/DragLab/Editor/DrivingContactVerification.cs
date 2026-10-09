using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
namespace DragLab.Editor {
 public static class DrivingContactVerification {
  public static void RunStandalone(){Debug.Log("DRIVING CONTACT PASS: "+Run(Path.GetFullPath(Path.Combine(Application.dataPath,"../.."))));}
  public static void LaunchProbe(){var spec=new VehicleSpec();var a=new CarSimulation(spec,new Tune{engineLevel=3,launchRpm=5000},new DriverSettings{tractionAssist=false,shifting=ShiftMode.Sequential});var b=new CarSimulation(spec,new Tune{engineLevel=3,tireLevel=3,launchRpm=5000},new DriverSettings{tractionAssist=false,shifting=ShiftMode.Sequential});a.Launch(0);b.Launch(0);for(int i=0;i<120;i++){a.Step(DragMath.FixedStep,new DriverInput());b.Step(DragMath.FixedStep,new DriverInput());}Debug.Log("LAUNCH PROBE: base distance "+a.state.distance+" slip "+a.state.slip+" work "+a.state.contact.contactWorkJ+" / grippy distance "+b.state.distance+" slip "+b.state.slip+" work "+b.state.contact.contactWorkJ);}
  public static int Run(string root){
   int count=0;var log=new StringBuilder();string folder=Path.Combine(root,"reports/phases/2h");Directory.CreateDirectory(folder);
   Action<bool,string> check=(ok,label)=>{log.AppendLine((ok?"PASS: ":"FAIL: ")+label);File.WriteAllText(Path.Combine(folder,"verification.txt"),log.ToString());if(!ok)throw new InvalidOperationException(label);count++;};
   foreach(var v in Resources.LoadAll<VehicleDefinition>("Data/Vehicles")) foreach(TransmissionUpgrade type in new[]{TransmissionUpgrade.Manual,TransmissionUpgrade.DCT,TransmissionUpgrade.Automatic,TransmissionUpgrade.CVT}) {
    var tune=v.CreateStockTune();tune.partsResolved=true;tune.transmissionUpgrade=type;tune.frontLeftGrip=tune.frontRightGrip=tune.rearLeftGrip=tune.rearRightGrip=.15f;
    var d=new DriverSettings {shifting=ShiftMode.Sequential,throttleAssist=false,tractionAssist=false,wearMode=WearMode.None};
    var run=new PhysicalDrivingSession(new RunSnapshot {vehicle=v.spec.Copy(),tune=tune,driving=d});
    for(int tick=0;tick<480;tick++)run.Step(new TickInput {tick=run.Tick,neutral=tick<120,staging=tick<120,input=new DriverInput {throttle=1}});
    var s=run.car.state;string id=v.spec.id+" / "+type;
    File.WriteAllText(Path.Combine(folder,v.spec.id+"-"+type+"-spin.csv"),run.Csv());
    check(s.contact.Validate()==null&&s.speed>0&&s.rpm>=v.spec.idleRpm&&!s.engineFailed,"Healthy finite powered contact "+id+" / speed "+s.speed+" / rpm "+s.rpm);
    check(s.contact.brakeWorkJ==0&&s.brakeFrictionPowerW==0,"Spinning without brakes generates no brake heat "+id);
    check(s.slip>.1&&s.contact.omega.Max()>s.speed/run.car.wheels.radius,"Independent wheel spin under power "+id+" / slip "+s.slip);
    double before=s.contact.omega.Max();double momentum=run.car.spec.RotationalInertia(run.car.tune)*s.rpm;
    if(type!=TransmissionUpgrade.CVT){run.Step(new TickInput {tick=run.Tick,input=new DriverInput {throttle=1,shiftUp=true}});check(s.gear==2&&s.rpm>0&&s.throttle==1,"Shift keeps rotating engine and held gas "+id);}
    run.Step(new TickInput {tick=run.Tick,neutral=true,input=new DriverInput {throttle=.3}});
    check(s.contact.omega.Max()>s.speed/run.car.wheels.radius&&s.contact.omega.Max()>before*.5,"Neutral preserves spinning wheel momentum "+id);
    var segment=run.ContinueRecording();for(int tick=0;tick<1440;tick++){var c=new TickInput {tick=run.Tick,neutral=true,input=new DriverInput()};run.Step(c);segment.Step(new TickInput {tick=segment.Tick,neutral=true,input=c.input});}
    check(s.slip<.01,"Contact regains grip without resetting spin "+id+" / slip "+s.slip);
    check(JsonUtility.ToJson(run.car.CaptureDynamics())==JsonUtility.ToJson(segment.car.CaptureDynamics()),"Wheel/engine checkpoint continuation is exact "+id);
    var decoded=PhysicalDrivingSession.Replay(JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(segment.snapshot)));File.WriteAllText(Path.Combine(folder,"original.csv"),segment.Csv());File.WriteAllText(Path.Combine(folder,"decoded.csv"),decoded.Csv());File.WriteAllText(Path.Combine(folder,"original.json"),JsonUtility.ToJson(segment.snapshot,true));File.WriteAllText(Path.Combine(folder,"decoded.json"),JsonUtility.ToJson(decoded.snapshot,true));
    check(segment.Csv()==decoded.Csv(),"Serialized contact trace replays every sample exactly "+id);
    double speed=s.speed;for(int tick=0;tick<480;tick++)run.Step(new TickInput {tick=run.Tick,neutral=true,input=new DriverInput {brake=1}});
    check(s.speed<speed*.01&&s.contact.brakeWorkJ>0&&!s.engineFailed,"Actual brake work stops body without killing engine "+id);
   }
   File.WriteAllText(Path.Combine(folder,"verification.txt"),log+"Checks: "+count);return count;
  }
 }
}

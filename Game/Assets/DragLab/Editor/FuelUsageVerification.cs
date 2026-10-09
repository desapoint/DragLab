using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Editor
{
    public static class FuelUsageVerification
    {
        public static int Run(string root)
        {
            int count=0; var log=new StringBuilder();
            Action<bool,string> check=(ok,label)=>{if(!ok)throw new InvalidOperationException(label);count++;log.AppendLine("PASS: "+label);};
            var old=JsonUtility.FromJson<DriverSettings>("{\"wearMode\":1,\"simulationDifficulty\":1}");
            check(old.fuelUsage==FuelUsageMode.Realistic && old.FuelUsageRate==1,"Older driver JSON keeps its pre-existing realistic fuel usage, including old Balanced captures.");
            foreach(var preset in new[]{SimulationDifficulty.Arcade,SimulationDifficulty.Balanced,SimulationDifficulty.Simulation}) {
                var driver=new DriverSettings{shifting=ShiftMode.ManualClutch,launch=LaunchMode.Manual}; driver.ApplySimulationPreset(preset);
                check(driver.FuelUsageRate==(preset==SimulationDifficulty.Arcade?0:preset==SimulationDifficulty.Balanced?.25:1),"Fuel usage follows newly applied difficulty preset: "+preset);
                check(driver.shifting==ShiftMode.ManualClutch && driver.launch==LaunchMode.Manual,"Fuel preset preserves chosen driver modes: "+preset);
            }
            foreach(var v in Resources.LoadAll<VehicleDefinition>("Data/Vehicles")) {
                var cars=new CarSimulation[3]; int index=0;
                foreach(var mode in new[]{FuelUsageMode.Realistic,FuelUsageMode.Reduced,FuelUsageMode.None}) {
                    var driver=new DriverSettings{fuelUsage=mode,wearMode=WearMode.None,throttleAssist=false};
                    var car=new CarSimulation(v.spec,v.CreateStockTune(),driver); cars[index++]=car;
                    car.state.rpm=4000;car.state.gear=1;car.state.throttle=.7;
                    double flow=FuelDynamics.Flow(car);var reference=new CarSimulation(v.spec,v.CreateStockTune(),new DriverSettings{throttleAssist=false}); reference.state.rpm=4000;reference.state.gear=1;reference.state.throttle=.7;
                    check(Math.Abs(flow-FuelDynamics.Flow(reference)*driver.FuelUsageRate)<1e-12,v.spec.id+" / "+mode+": loaded demand and actual flow scale exactly by the selected rate.");
                    car=new CarSimulation(v.spec,v.CreateStockTune(),driver); cars[index-1]=car; double initial=car.state.fuelLitres,mass=car.mass.TranslatingKg;
                    for(int i=0;i<2400;i++)car.IndependentStep(DragMath.FixedStep,new DriverInput{throttle=.35},true,false);
                    check(Math.Abs(initial-car.state.fuelLitres-car.state.fuelUsedLitres)<1e-8 && Math.Abs(mass-car.mass.TranslatingKg-car.state.fuelUsedLitres*car.spec.fuel.densityKgPerLitre-(car.state.fluids.oilConsumedLitres+car.state.fluids.oilLeakedLitres)*.86-car.state.fluids.coolantLostLitres*1.05)<1e-7,v.spec.id+" / "+mode+": burn telemetry, remaining quantity and fuel/service-fluid mass agree.");
                    check(mode!=FuelUsageMode.None || car.state.fuelUsedLitres==0 && car.state.fuelFlowLitresPerSecond==0 && car.state.fuelLitres==initial && car.mass.fuelKg==initial*car.spec.fuel.densityKgPerLitre && car.state.rpm>v.spec.idleRpm,v.spec.id+" / "+mode+": None runs the engine without consuming or deleting tank weight.");
                    var snapshot=new RunSnapshot{vehicle=v.spec.Copy(),tune=v.CreateStockTune(),driving=driver};var session=new PhysicalDrivingSession(snapshot);
                    for(int i=0;i<360;i++)session.Step(new TickInput{tick=session.Tick,input=new DriverInput{throttle=.6},neutral=i<60});
                    var parsed=JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(session.snapshot));
                    check(parsed.driving.fuelUsage==mode && PhysicalDrivingSession.Replay(parsed).Csv()==session.Csv(),v.spec.id+" / "+mode+": full fuel/mass/running trace replays exactly after JSON round trip.");
                    driver.fuelUsage=mode==FuelUsageMode.None?FuelUsageMode.Realistic:FuelUsageMode.None;
                    check(session.car.driver.fuelUsage==mode,v.spec.id+" / "+mode+": race/bench capture owns an independent copy of consumption settings.");
                }
                check(cars[0].state.fuelUsedLitres>0 && Math.Abs(cars[1].state.fuelUsedLitres/cars[0].state.fuelUsedLitres-.25)<1e-7,v.spec.id+": repeatable neutral revving consumes exactly 25% in Reduced.");
            }
            var profile=PlayerProfile.New(DateTime.UtcNow.Ticks);profile.driving.fuelUsage=FuelUsageMode.Reduced;
            var saved=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(profile));saved.Sanitize(DateTime.UtcNow.Ticks);
            check(saved.driving.fuelUsage==FuelUsageMode.Reduced && saved.RestartKeepingPreferences(DateTime.UtcNow.Ticks).driving.fuelUsage==FuelUsageMode.Reduced,"Fuel preference persists across JSON save/load and progression reset.");
            saved.driving.fuelUsage=(FuelUsageMode)99;saved.Sanitize(DateTime.UtcNow.Ticks);check(saved.driving.fuelUsage==FuelUsageMode.Realistic,"Unknown saved fuel mode sanitizes to the prior realistic default.");
            var invalid=new RunSnapshot{vehicle=new VehicleSpec(),tune=new Tune(),driving=new DriverSettings{fuelUsage=(FuelUsageMode)99}};
            check(PhysicalDrivingSession.Validate(invalid)!=null,"Replay rejects an unknown fuel mode rather than silently changing its consumption.");
            string folder=Path.Combine(root,"reports/fuel-usage");Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"verification.txt"),log+"Checks: "+count+"\n");return count;
        }
    }
}

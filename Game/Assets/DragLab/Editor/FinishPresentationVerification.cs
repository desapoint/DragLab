using System;
using System.IO;
using System.Linq;
using System.Text;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Presentation;
using UnityEngine;

namespace DragLab.Editor
{
    public static class FinishPresentationVerification
    {
        public static void RunSafety()
        {
            int checks=0;var report=new StringBuilder();
            void Check(bool ok,string text){checks++;if(!ok)throw new InvalidOperationException(text);report.AppendLine("PASS: "+text);}
            foreach(var vehicle in Resources.LoadAll<VehicleDefinition>("Data/Vehicles"))
            {
                var driver=new DriverSettings{launch=LaunchMode.Automatic,shifting=ShiftMode.Automatic,reactionSeconds=.1,wearMode=WearMode.None,fuelUsage=FuelUsageMode.None,optimalThermals=true,shutdownBrakeAssist=false};
                var rival=driver.Copy();rival.reactionSeconds=1.8;
                var race=RaceSessionFactory.Create(vehicle.spec,vehicle.CreateStockTune(),driver,vehicle.spec,vehicle.CreateStockTune(),rival,18.288);
                for(int tick=0;tick<120*50&&!race.player.state.finished;tick++)race.Step(new DriverInput{throttle=1});
                Check(race.player.state.finished,vehicle.spec.id+": safety fixture reaches a real finish");
                double et=race.player.state.elapsed;
                void Tick(DriverInput input){if(race.phase==RacePhase.Finished)race.Shutdown(input);else race.Step(input);}
                Tick(new DriverInput{brake=.23});Check(Math.Abs(race.player.state.brake-.23)<.00001,vehicle.spec.id+": manual partial braking is not overridden");
                Tick(new DriverInput());Check(race.player.state.brake==0,vehicle.spec.id+": manual coast has no automatic parking brake");
                race.player.driver.shutdownBrakeAssist=true;Tick(new DriverInput());
                Check(race.player.state.brake<=.65*DragMath.FixedStep+.00001,vehicle.spec.id+": automatic braking starts with a gradual first pressure step");
                var brakes=race.player.mechanics.Part(PartCategory.Brakes);brakes.condition.failed=true;brakes.condition.health=0;
                race.player.driver.shutdownBrakeAssist=true;
                for(int i=0;i<4;i++)Tick(new DriverInput());
                Check(race.player.state.brakesFailed&&race.player.state.brakeCapacityFactor==0,vehicle.spec.id+": actual broken brake hardware has no stopping capacity");
                Check(race.player.state.speed>.01&&!race.player.state.engineFailed,vehicle.spec.id+": failed brakes do not park or break the engine");
                Check(race.player.state.elapsed==et,vehicle.spec.id+": safety handling leaves the beam time fixed");
            }
            report.AppendLine("Checks: "+checks+" / failures: 0");Directory.CreateDirectory("../reports/shutdown-braking-20261016");File.WriteAllText("../reports/shutdown-braking-20261016/safety.txt",report.ToString(),new UTF8Encoding(false));Debug.Log("Finish safety: "+checks+" passed");
        }
        public static void Run()
        {
            int checks=0;var report=new StringBuilder();
            void Check(bool ok,string text){checks++;if(!ok)throw new InvalidOperationException(text);report.AppendLine("PASS: "+text);}
            foreach(var vehicle in Resources.LoadAll<VehicleDefinition>("Data/Vehicles"))
            {
                DriverSettings Driver(double reaction)=>new DriverSettings{launch=LaunchMode.Automatic,shifting=ShiftMode.Automatic,reactionSeconds=reaction,wearMode=WearMode.None,fuelUsage=FuelUsageMode.None,optimalThermals=true};
                foreach(bool rivalFirst in new[]{false,true})
                {
                    var race=RaceSessionFactory.Create(vehicle.spec,vehicle.CreateStockTune(),Driver(rivalFirst?1.8:.1),vehicle.spec,vehicle.CreateStockTune(),Driver(rivalFirst?.1:1.8),402.336);
                    Check(!new FinishReading(race.player.state).Finished&&new FinishReading(race.opponent.state).Elapsed=="",vehicle.spec.id+": fresh boards blank");
                    for(int tick=0;tick<120*100&&!race.player.state.finished&&!race.opponent.state.finished;tick++)race.Step(new DriverInput{throttle=1});
                    var first=rivalFirst?race.opponent.state:race.player.state;var second=rivalFirst?race.player.state:race.opponent.state;
                    Check(first.finished&&!second.finished,vehicle.spec.id+": expected independent first finish / rival "+rivalFirst);
                    var posted=new FinishReading(first);Check(posted.Elapsed!=""&&posted.Reaction!=""&&new FinishReading(second).Elapsed==""&&new FinishReading(second).Reaction=="",vehicle.spec.id+": only crossed beam posts ET and RT");
                    for(int tick=0;tick<120*100&&race.phase!=RacePhase.Finished;tick++)race.Step(new DriverInput{throttle=1});
                    Check(race.player.state.finished&&race.opponent.state.finished&&new FinishReading(first).Elapsed==posted.Elapsed,vehicle.spec.id+": later finish cannot rewrite first result");
                    double elapsed=race.player.state.elapsed,trap=race.player.state.trapSpeed,maxBrake=race.player.state.brake,maxRise=0;
                    for(int tick=0;tick<120*90&&race.player.state.speed>.01;tick++){double previous=race.player.state.brake;race.Shutdown(new DriverInput());maxBrake=Math.Max(maxBrake,race.player.state.brake);maxRise=Math.Max(maxRise,race.player.state.brake-previous);}
                    Check(race.player.state.speed<=.01&&maxBrake<=RaceSimulation.MaximumShutdownBrake+.00001,vehicle.spec.id+": shutdown stops physically with at most 90% brakes");
                    Check(maxRise<=.65/120+.00001,vehicle.spec.id+": pressure rises progressively, never snapping to the new limit");
                    Check(race.player.state.elapsed==elapsed&&race.player.state.trapSpeed==trap,vehicle.spec.id+": stopping leaves ET and trap untouched");
                    report.AppendLine("MEASURE: "+vehicle.spec.id+" / trap "+trap.ToString("F2")+" km/h / stop "+(race.player.state.distance-race.distance).ToString("F2")+" m / brake "+maxBrake.ToString("F3")+" / max tick rise "+maxRise.ToString("F5"));
                }
            }
            Check(FinishBillboard.BeyondFinishMetres>0&&FinishBillboard.BeyondFinishMetres<RaceSimulation.ShutdownStopMetres,"Billboard stands beyond finish, just before the shutdown stop target");
            report.AppendLine("Checks: "+checks+" / failures: 0");Directory.CreateDirectory("../reports/shutdown-braking-20261016");File.WriteAllText("../reports/shutdown-braking-20261016/domain.txt",report.ToString(),new UTF8Encoding(false));Debug.Log("Finish presentation: "+checks+" passed");
        }
    }
}

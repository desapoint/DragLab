using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Infrastructure;
using UnityEngine;

namespace DragLab.Editor
{
    public static class RaceTimingAudit
    {
        public static void Run()
        {
            int checks=0;var log=new StringBuilder();var csv=new StringBuilder("case,seed,delay,player_rt,player_et,rival_rt,rival_et,total_gap,won\n");
            void Check(bool ok,string label){checks++;if(!ok){Directory.CreateDirectory("../reports/race-timing-audit");File.WriteAllText("../reports/race-timing-audit/failed-diagnostic.txt",log+"\nFAIL: "+label+"\n"+csv,new UTF8Encoding(false));throw new InvalidOperationException(label);}log.AppendLine("PASS: "+label);}
            DriverSettings Driver(double reaction)=>new DriverSettings{reactionSeconds=reaction,wearMode=WearMode.None,fuelUsage=FuelUsageMode.None,fluidDepletion=DragLab.Domain.Mechanics.ConsumptionMode.None,fluidAging=DragLab.Domain.Mechanics.ConsumptionMode.None,optimalThermals=true};
            void RunRace(RaceSimulation race,string name,int seed,double delay,bool worker=false,bool allowRivalDnf=false)
            {
                Check(race.time==0&&race.phase==RacePhase.Countdown&&!race.player.state.launched&&!race.opponent.state.launched&&race.player.state.elapsed<0&&race.opponent.state.elapsed<0,name+": fresh timestamps/state");
                if(worker)race.EnableOpponentWorker();
                double first=-1,second=-1;bool firstPlayer=false;
                try {
                    for(int tick=0;tick<120*95&&race.phase!=RacePhase.Finished;tick++) {
                        bool a=race.player.state.finished,b=race.opponent.state.finished;
                        // Hold the automatic driver's same launch target until its
                        // reaction expires. Flooring it in neutral changes physical
                        // launch RPM/heat and would not isolate timer correctness.
                        race.Step(new DriverInput{throttle=race.time-RaceSimulation.GreenAt>=race.player.driver.reactionSeconds?1:0});double clock=race.time-RaceSimulation.GreenAt;
                        if(!a&&race.player.state.finished){Check(Math.Abs(clock-race.player.state.reaction-race.player.state.elapsed)<=DragMath.FixedStep+.000001,name+": player crossing matches independent green clock");if(first<0){first=clock;firstPlayer=true;}else second=clock;}
                        if(!b&&race.opponent.state.finished){Check(Math.Abs(clock-race.opponent.state.reaction-race.opponent.state.elapsed)<=DragMath.FixedStep+.000001,name+": rival crossing matches independent green clock");if(first<0){first=clock;firstPlayer=false;}else second=clock;}
                    }
                    Check(race.player.state.finished,name+": player physically finishes");
                    if(!race.opponent.state.finished) {
                        Check(allowRivalDnf&&(race.opponent.state.engineFailed||race.opponent.state.drivetrainFailed||race.timedOut),name+": missing rival time is a real DNF, never a previous-run ET");
                        Check(race.opponent.state.elapsed<0&&race.playerWon,name+": DNF has no fabricated elapsed time");return;
                    }
                    Check(race.opponent.state.finished,name+": rival physically finishes");
                    double aTotal=race.player.state.reaction+race.player.state.elapsed,bTotal=race.opponent.state.reaction+race.opponent.state.elapsed;
                    Check(race.playerWon==(aTotal<bTotal),name+": winner uses RT plus ET");
                    if(second-first>DragMath.FixedStep)Check(race.playerWon==firstPlayer,name+": outcome agrees with independently observed lane crossing order");
                    csv.AppendLine(string.Join(",",name,seed,delay.ToString("F3",CultureInfo.InvariantCulture),race.player.state.reaction.ToString("F3",CultureInfo.InvariantCulture),race.player.state.elapsed.ToString("F3",CultureInfo.InvariantCulture),race.opponent.state.reaction.ToString("F3",CultureInfo.InvariantCulture),race.opponent.state.elapsed.ToString("F3",CultureInfo.InvariantCulture),(bTotal-aTotal).ToString("F3",CultureInfo.InvariantCulture),race.playerWon));
                }finally{race.CloseOpponentWorker();}
            }
            var vehicles=Resources.LoadAll<VehicleDefinition>("Data/Vehicles");
            foreach(string id in new[]{"starter-hatch","v8-muscle","super-v8"}) {
                var v=vehicles.Single(d=>d.spec.id==id);double firstEt=-1;
                foreach(double delay in new[]{.18,3.18,6.18}) {
                    var race=RaceSessionFactory.Create(v.spec,v.CreateStockTune(),Driver(delay),v.spec,v.CreateStockTune(),Driver(.18),402.336);
                    RunRace(race,id,0,delay,delay>6);
                    if(firstEt<0)firstEt=race.player.state.elapsed;
                    Check(Math.Abs(race.player.state.elapsed-firstEt)<.15,id+": waiting changes reaction rather than adding idle seconds to ET / "+firstEt+" -> "+race.player.state.elapsed+" / delay "+delay);
                    if(delay>3)Check(!race.playerWon,id+": several seconds of waiting cannot become a close win against the same healthy car");
                }
            }
            var catalog=MechanicsTransactions.Catalog();
            var events=Resources.LoadAll<CareerEventDefinition>("Data/Events").Where(e=>e.opponent!=null).ToArray();Check(events.Length>0,"Authored career events are included in the audit");
            foreach(var e in events) {
                var v=vehicles.Single(d=>d.spec.id=="starter-hatch");
                foreach(int seed in new[]{117,491})foreach(double delay in new[]{.18,4.18}) {
                    var capture=CareerOpponents.Capture(e,catalog,1,seed);string hardware=JsonUtility.ToJson(capture.Entry());
                    var race=RaceSessionFactory.CreateCaptured(v.spec,v.CreateStockTune(),Driver(delay),capture,e.distanceM);
                    RunRace(race,e.stableId,seed,delay,allowRivalDnf:true);
                    var repeat=CareerOpponents.Capture(e,catalog,1,seed);
                    Check(JsonUtility.ToJson(repeat.Entry())==hardware,e.stableId+": retry capture has fresh behavior and identical authored hardware/seed");
                }
            }
            string folder="../reports/race-timing-audit";Directory.CreateDirectory(folder);
            log.AppendLine("Checks: "+checks+" / failures: 0");File.WriteAllText(folder+"/verification.txt",log.ToString(),new UTF8Encoding(false));File.WriteAllText(folder+"/timing-matrix.csv",csv.ToString(),new UTF8Encoding(false));Debug.Log("Race timing audit: "+checks+" passed.");
        }
    }
}

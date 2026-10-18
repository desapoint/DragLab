using System;
using System.Collections.Generic;
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
    public static class StartingVarietyVerification
    {
        public static void Run()
        {
            var log=new StringBuilder();int checks=0;
            void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);checks++;log.AppendLine("PASS "+text);}
            var catalog=MechanicsTransactions.Catalog();var cars=Resources.LoadAll<VehicleDefinition>("Data/Vehicles");
            Check(cars.Select(v=>v.spec.modelFamily).Distinct().Count()==25,"25 distinct model families");
            Check(cars.GroupBy(v=>v.spec.modelFamily).All(g=>g.Count()>=2),"Every model offers at least two trim configurations");
            var matrix=new StringBuilder("model,condition,price,reserve,quarter_seconds,rival_seconds,win\n");
            var opening=Resources.LoadAll<CareerEventDefinition>("Data/Events").Single(e=>e.stableId=="first-pass");
            foreach(var v in cars){var b=catalog.Resolve(v.spec.id,catalog.Recipe(v.spec.id),v.CreateStockTune());Check(b.Drivable&&b.spec.Validate()==null,v.spec.id+" complete compatible OEM build");}
            var seen=new HashSet<string>();var combinations=new HashSet<string>();
            for(int seed=1;seed<=32;seed++){
                var p=PlayerProfile.NewGame(638960000000000000L);VehicleListings.Initialize(p,catalog,seed);MechanicsOwnership.Ensure(p,catalog);
                Check(p.cash==6500&&p.cars.Count==0&&p.vehicleStock.listings.Count==3,"Seed "+seed+" funded three-choice start without gifted car");
                Check(p.vehicleStock.listings.Select(o=>catalog.Spec(o.model).modelFamily).Distinct().Count()==3,"Seed "+seed+" three different models");
                Check(p.vehicleStock.listings.All(o=>o.inspected&&o.firstStarter&&o.engineRuns&&o.drives&&o.parts.All(i=>!i.condition.failed)),"Seed "+seed+" disclosed serviceable starter hardware");
                Check(VehicleListings.Validate(p,catalog)==null,"Seed "+seed+" valid identities and per-corner tires");
                Check(p.vehicleStock.listings.All(o=>o.parts.Any(i=>i.condition.health<=.4||i.condition.elements.Any(e=>e.health<=.4))&&o.parts.Any(i=>i.condition.health>=.9)),"Seed "+seed+" has genuinely worn and fresh components in every starter");
                string stock=JsonUtility.ToJson(p.vehicleStock);MechanicsOwnership.Ensure(p,catalog);Check(stock==JsonUtility.ToJson(p.vehicleStock),"Seed "+seed+" browsing never rerolls");
                combinations.Add(string.Join("/",p.vehicleStock.listings.Select(o=>o.model)));
                foreach(var o in p.vehicleStock.listings){int cost=PaidEconomy.DealerPrice(catalog,VehicleListings.Costs,VehicleListings.Values,o);Check(cost<=p.cash,o.model+" affordable starter at seed "+seed+": "+cost);
                    if(!seen.Add(o.model))continue;
                    var b=catalog.Resolve(o.model,o.parts.ToDictionary(i=>catalog.Find(i.definitionId).category,i=>i.definitionId),o.stock);
                    var driver=StartingPreferences.Create(DrivingPreset.Automatic);driver.raceTemperatures=RaceTemperatureMode.Optimal;driver.throttleAssist=false;driver.stallPrevention=true;driver.tractionAssist=true;
                    var sim=new CarSimulation(b.spec,b.tune,driver,o.parts);sim.SetFluids(o.fluids);
                    var rival=CareerOpponents.Capture(opening,catalog,1,42);var race=new RaceSimulation(sim,rival.Create(),402.336,rival.behavior);
                    for(int tick=0;tick<10000&&race.phase!=RacePhase.Finished;tick++)race.Step(new DriverInput{throttle=1});
                    Check(sim.state.distance>=402.336&&!sim.state.engineFailed&&sim.mechanics.parts.All(i=>!i.condition.failed),o.model+" used starter survives full-simulation clean pass: distance="+sim.state.distance+" rpm="+sim.state.rpm+" speed="+sim.state.speed+" clutch="+sim.state.clutch+" torque="+sim.state.transmittedTorqueNm+" power="+sim.state.mechanicalPowerFactor+" supply="+sim.state.engineSupplyAvailable+" temperature="+sim.state.engineTemperature+" stalled="+sim.state.stalled+" failures="+string.Join(";",sim.mechanics.parts.Where(i=>i.condition.failed).Select(i=>i.definitionId+":"+i.condition.failureReason)));
                    matrix.AppendLine(o.model+","+o.reportedCondition.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+","+cost+","+(p.cash-cost)+","+sim.state.elapsed.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+","+race.opponent.state.elapsed.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+","+race.playerWon);
                    if(o.model=="sable")Check(race.playerWon&&p.cash-cost<200,"High-budget serviced starter can win the opening benchmark without upgrades; no result override");
                }
            }
            Check(seen.Count==15&&combinations.Count>20,"Seed matrix covers all 15 starter models and varied shortlists");
            var defaults=StartingPreferences.Create();Check(defaults.shifting==ShiftMode.Sequential&&defaults.simulationDifficulty==SimulationDifficulty.Simulation&&defaults.guaranteedFittingParts==5,"New-player defaults: shift myself / Simulation / Normal market");
            EconomyBalanceVerification.VerifyRaceRewards();
            Directory.CreateDirectory("../reports/starting-variety-20261018");
            File.WriteAllText("../reports/starting-variety-20261018/starter-matrix.csv",matrix.ToString(),new UTF8Encoding(false));
            File.WriteAllText("../reports/starting-variety-20261018/verification.txt",log+"Checks: "+checks+"\n",new UTF8Encoding(false));
            Debug.Log("Starting variety PASS: "+checks);
        }
    }
}

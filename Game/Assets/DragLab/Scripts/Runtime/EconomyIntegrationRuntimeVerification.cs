using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;
namespace DragLab.Runtime
{
    public sealed partial class RuntimeVerification
    {
        IEnumerator EconomyIntegrationRun()
        {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
            bool resume = Array.IndexOf(Environment.GetCommandLineArgs(), "--draglab-economy-resume") >= 0;
            var log = new StringBuilder("3H actual Windows combined job / paid career / pause / forced termination / reload.\n");
            Action<bool,string> check = (ok,label) => { Check(ok,label); log.AppendLine("PASS: " + label); File.WriteAllText(Path.Combine(output,"integration-progress.txt"),log.ToString(),Encoding.UTF8); };
            using (var mouse = new WindowsKeyboardProbe())
            {
                if (!resume)
                {
                    // Funded fixture isolates integration from the separately verified 3G earned route.
                    game.Profile.cash = 100000; game.Profile.reputation = 99;
                    game.settings.ApplyPreset(DrivingPreset.Automatic); game.settings.ApplySimulationPreset(SimulationDifficulty.Simulation);
                    string first = game.Profile.SelectedCar.instanceId; game.Profile.SelectedCar.fuel.litres = 7.25;
                    ProfileStore.Save(game.Profile);
                    game.OpenCollection(); yield return new WaitForSecondsRealtime(.2f); yield return ClickPart(mouse,"car-buy:starter-hatch");
                    string second = game.Profile.SelectedCar.instanceId;
                    check(second != first && game.Profile.cars.Count == 2,"Actual dealership buys a distinct same-model car.");
                    yield return ClickPart(mouse,"car-select:" + first);
                    game.OpenBusiness(); yield return new WaitForSecondsRealtime(.2f); yield return ClickPart(mouse,"job-assign:photo-session");
                    check(game.Profile.business.jobs.Single().carId == first,"Actual job reserves first car while duplicate remains available.");
                    game.OpenCollection(); yield return new WaitForSecondsRealtime(.2f); yield return ClickPart(mouse,"car-select:" + second);
                    game.OpenCareer(); yield return new WaitForSecondsRealtime(.2f); yield return ClickPart(mouse,"career-first-pass");
                    game.verificationInput = () => new DriverInput { throttle = 1 };
                    while (game.Race.time < 5) yield return null;
                    game.PauseRace(true); double time = game.Race.time, speed = game.Race.player.state.speed;
                    double fuel = game.Race.player.FuelSnapshot().litres;
                    yield return new WaitForSecondsRealtime(.6f);
                    check(game.Race.time == time && game.Race.player.state.speed == speed && game.Race.player.FuelSnapshot().litres == fuel,"Pause freezes motion, fuel and elapsed simulation while saving the checkpoint.");
                    game.PauseRace(false); while (game.Race.time < time + .5) yield return null;
                    check(game.Race.player.state.throttle > .9 && game.Race.time > time,"Resume restores continuing throttle and simulation.");
                    game.Race.player.mechanics.Part(PartCategory.Driveshaft).condition.health = .000001f;
                    float deadline = Time.realtimeSinceStartup + 10;
                    while (!game.Race.player.state.drivetrainFailed && Time.realtimeSinceStartup < deadline) yield return null;
                    check(game.Race.player.state.drivetrainFailed && !game.Race.player.state.engineFailed,"Transmitted torque fails shaft without stopping engine.");
                    game.PauseRace(true); var saved = ProfileStore.Load();
                    check(saved.careerRuns.Any(r=>!r.complete) && saved.runCheckpoints.Any(r=>!r.settled) && saved.business.jobs.Count == 1,"Career checkpoint and independent reservation are durable together.");
                    yield return Capture("combined-before-kill");
                    File.WriteAllText(Path.Combine(output,"expected-before-kill.json"),JsonUtility.ToJson(saved),Encoding.UTF8);
                    File.WriteAllText(Path.Combine(output,"stage-checks.txt"),log.ToString(),Encoding.UTF8);
                    File.WriteAllText(Path.Combine(output,"recovery-ready.txt"),"Own fixture process ready for termination.",Encoding.UTF8);
                    while (true) yield return null;
                }
                var expected = JsonUtility.FromJson<PlayerProfile>(File.ReadAllText(Path.Combine(output,"expected-before-kill.json"),Encoding.UTF8));
                log.Append(File.ReadAllText(Path.Combine(output,"stage-checks.txt"),Encoding.UTF8));
                var actual = game.Profile; var job = actual.business.jobs.Single(); string selected = actual.SelectedCar.instanceId;
                check(actual.cash == expected.cash && actual.reputation == expected.reputation && actual.cars.Select(c=>c.instanceId).SequenceEqual(expected.cars.Select(c=>c.instanceId)),"Forced restart retains both owned instances and matching currency.");
                check(actual.runCheckpoints.All(r=>r.settled) && actual.careerRuns.Single().outcome == "Interrupted / no payout" && actual.cars.All(c=>c.completedRuns == 0),"Interrupted career closes without result, record or duplicate payout.");
                check(Math.Abs(actual.SelectedCar.fuel.litres - expected.SelectedCar.fuel.litres) < 1e-10 && actual.cars.Find(c=>c.instanceId==job.carId).fuel.litres == 7.25,"Both independent partial tank quantities survive termination.");
                string shaft = actual.SelectedCar.installed.slots.Find(s=>s.slot == "Driveshaft").partInstanceId;
                check(actual.parts.Find(i=>i.instanceId==shaft).condition.failed && job.id == expected.business.jobs.Single().id && CareerProgression.EntryError(actual,actual.cars.Find(c=>c.instanceId==job.carId),null) != null,"Failed original and separate job reservation survive restart.");
                game.ShowRepairs(); yield return new WaitForSecondsRealtime(.2f); int cash = actual.cash;
                int price = actual.catalog.Find(actual.parts.Find(i=>i.instanceId==shaft).definitionId).price;
                yield return ClickPart(mouse,"repair-buy:" + shaft);
                check(game.Profile.cash == cash - price && game.Profile.parts.Find(i=>i.instanceId==shaft).condition.failed && game.Profile.parts.Find(i=>i.instanceId==shaft).location == PartLocation.Inventory,"Actual replacement charges once and retains failed original.");
                game.OpenBusiness(); yield return new WaitForSecondsRealtime(.2f);
                while (DragLab.Domain.RuntimeEntropy.UtcNow.Ticks < job.readyUtcTicks) yield return new WaitForSecondsRealtime(.1f);
                cash = game.Profile.cash; yield return ClickPart(mouse,"job-claim:" + job.id);
                check(game.Profile.cash == cash + job.reward && game.Profile.business.jobs.Count == 0,"Actual real-deadline claim releases offline job once.");
                game.OpenCareer(); yield return new WaitForSecondsRealtime(.2f); yield return ClickPart(mouse,"career-first-pass");
                game.verificationInput = () => new DriverInput { throttle = game.Race.player.state.launched ? 1 : 0 };
                yield return AwaitFinish(); game.ReturnToGarage();
                check(game.Profile.careerWins.Contains("first-pass") && game.Profile.SelectedCar.instanceId == selected && game.Profile.SelectedCar.completedRuns == 1,"Repaired interrupted career wins and records only the new pass.");
                var reloaded = ProfileStore.Load(true);
                check(reloaded.cash == game.Profile.cash && reloaded.careerRuns.Count == 2 && reloaded.runCheckpoints.All(r=>r.settled) && MechanicsOwnership.Validate(reloaded,reloaded.catalog) == null,"Completed retry reload retains one reward and exclusive ownership.");
                yield return Capture("combined-recovered-and-won");
            }
            check(errors == 0,"No runtime, GUI or audio errors.");
            File.WriteAllText(Path.Combine(output,"runtime-verification.txt"),log + "Runtime errors: " + errors + "\n",Encoding.UTF8);
#else
            yield return null; throw new InvalidOperationException("Requires Windows development player.");
#endif
        }
    }
}

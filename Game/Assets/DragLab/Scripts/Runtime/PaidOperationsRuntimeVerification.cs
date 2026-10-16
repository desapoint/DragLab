using System;
using System.IO;
using System.Linq;
using System.Collections;
using UnityEngine;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;
namespace DragLab.Runtime
{
    public sealed partial class RuntimeVerification
    {
        IEnumerator PaidOperationsRun()
        {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
            bool resume = Array.IndexOf(Environment.GetCommandLineArgs(), "--draglab-paid-resume") >= 0;
            var log = new System.Text.StringBuilder(resume ? "3D actual process restart / empty garage / recovery / replacement.\n" : "3D actual paid fuel / repairs / last incomplete chassis salvage.\n");
            Action<bool, string> check = (ok, label) => { Check(ok, label); log.AppendLine("PASS: " + label); File.WriteAllText(Path.Combine(output, resume ? "resume-progress.txt" : "verification-progress.txt"), log.ToString()); };
            using (var mouse = new WindowsKeyboardProbe()) {
                if (!resume) {
                    game.Profile.cash = 50000; game.Profile.reputation = 99; var car = game.Profile.SelectedCar; string owner = car.instanceId, engine = car.engineAssemblyId;
                    car.fuel.litres = game.Profile.catalog.Spec(car.vehicleId).fuel.capacityLitres - 2.5; ProfileStore.Save(game.Profile); game.ReturnToGarage(); yield return new WaitForSecondsRealtime(.2f);
                    yield return ClickPart(mouse, "fuel-quantity"); mouse.Key(0x11, true); mouse.Key(0x41, true); mouse.Key(0x41, false); mouse.Key(0x11, false); mouse.Key(0x39, true); mouse.Key(0x39, false); mouse.Key(0x39, true); mouse.Key(0x39, false); yield return new WaitForSecondsRealtime(.3f);
                    int cash = game.Profile.cash; yield return ClickPart(mouse, "fuel-refuel");
                    check(game.Profile.SelectedCar.fuel.litres == game.Profile.catalog.Spec(car.vehicleId).fuel.capacityLitres && game.Profile.cash == cash - (int)Math.Ceiling(2.5 * game.Costs.fuelPerLitre) && game.Profile.economyReceipts.Last().fuelAdded == 2.5, "Actual typed overfill is capped; paid fuel charges accepted litres exactly once."); yield return Capture("paid-fuel");
                    game.Profile.SelectedCar.fluids.oilLitres = 1; game.Profile.SelectedCar.fluids.coolantLitres = 1; game.Profile.SelectedCar.fluids.oilCondition = .4; game.settings.serviceAssist = ServiceAssist.AutoTopUp;
                    cash = game.Profile.cash; game.Profile.cash = 0; game.StartPractice();
                    check(game.ScreenName == "Garage" && game.Profile.SelectedCar.fluids.oilLitres == 1 && game.Profile.SelectedCar.fluids.coolantLitres == 1 && game.Profile.cash == 0, "Unaffordable automatic pre-race top-up blocks race and never supplies free fluids.");
                    game.Profile.cash = cash; game.settings.serviceAssist = ServiceAssist.Manual; ProfileStore.Save(game.Profile); yield return ClickPart(mouse, "fluids-fill");
                    check(game.Profile.cash < cash && game.Profile.SelectedCar.fluids.oilCondition == .4 && game.Profile.economyReceipts.Last().operation == "Fluids", "Actual paid manual top-up preserves oil aging and writes operating receipt.");
                    var cooling = game.Profile.parts.Find(i => i.ownerId == engine && game.Profile.catalog.Find(i.definitionId).category == PartCategory.Cooling); cooling.condition.leakSeverity = .6; cooling.condition.health = .7f; ProfileStore.Save(game.Profile);
                    game.ShowParts(PartCategory.Cooling); yield return new WaitForSecondsRealtime(.2f); cash = game.Profile.cash; yield return ClickPart(mouse, "service:Reseal:" + cooling.instanceId);
                    check(game.Profile.cash < cash && game.Profile.parts.Find(i => i.instanceId == cooling.instanceId).condition.leakSeverity == 0 && game.Profile.parts.Find(i => i.instanceId == cooling.instanceId).condition.health == .7f, "Actual priced reseal repairs leak while keeping structural wear.");
                    game.ShowParts(PartCategory.Gearbox); yield return new WaitForSecondsRealtime(.2f); yield return ClickPart(mouse, "remove");
                    string piston = game.Profile.SelectedCar.installed.slots.Find(s => s.slot == "Pistons").partInstanceId; game.Profile.parts.Find(i => i.instanceId == piston).condition.health = .6f; game.Profile.parts.Find(i => i.instanceId == piston).condition.elements[0].fault = ElementFault.CompressionLeak; game.Profile.parts.Find(i => i.instanceId == piston).condition.elements[0].severity = .5f;
                    game.Profile.SelectedCar.fluids.oilLitres = 2; ProfileStore.Save(game.Profile); game.OpenValuation(ValuationKind.Chassis, game.Profile.SelectedCar.chassisId); yield return new WaitForSecondsRealtime(.2f); var q = game.valuationQuote; cash = game.Profile.cash;
                    yield return ClickPart(mouse, "quote-sell-preview"); yield return ClickPart(mouse, "quote-sell-cancel");
                    check(game.Profile.cash == cash && game.Profile.cars.Count == 1, "Cancel actual disposal keeps cash and all ownership.");
                    yield return ClickPart(mouse, "quote-sell-preview"); yield return Capture("salvage-confirmation"); yield return ClickPart(mouse, "quote-sell-confirm");
                    check(game.Profile.cars.Count == 0 && game.Profile.SelectedCar == null && game.Profile.cash == cash + q.Total && game.Profile.engines.Find(e => e.instanceId == engine).fluids.oilLitres == 2, "Actual incomplete last-chassis salvage pays once and stores exact engine/fluid tree.");
                    check(game.Profile.parts.Find(i => i.instanceId == piston).condition.health == .6f && game.Profile.parts.Find(i => i.instanceId == piston).condition.elements[0].fault == ElementFault.CompressionLeak && game.Profile.economyReceipts.Last().retained.Contains(piston), "Retained damaged individual hardware is unchanged and documented in disposal receipt.");
                    check(!game.CommitParts(q.Revision, p => PaidEconomy.Sell(p, p.catalog, game.PricingPolicy, q, DragLab.Domain.RuntimeEntropy.UtcNow.Ticks)), "Repeated stale disposal cannot pay again.");
                    game.Profile.cash = 0; ProfileStore.Save(game.Profile); game.OpenCollection(); yield return new WaitForSecondsRealtime(.2f); yield return Capture("empty-garage");
                    File.WriteAllText(Path.Combine(output, "retained-engine.txt"), engine);
                    var loaded = ProfileStore.Load(); check(loaded.cars.Count == 0 && loaded.cash == 0 && loaded.engines.Any(e => e.instanceId == engine) && loaded.economyReceipts.Any(r => r.id == q.Id), "Disk roundtrip retains empty garage, retained engine and committed disposal; fixture explicitly stages zero cash for restart.");
                    yield return ClickPart(mouse, "collection-recovery"); yield return ClickPart(mouse, "recovery-start");
                    var savedJob = ProfileStore.Load().recoveryWork;
                    check(savedJob != null && savedJob.id == game.Profile.recoveryWork.id && savedJob.readyUtcTicks == game.Profile.recoveryWork.readyUtcTicks && game.Profile.cash == 0, "Actual work start saves captured deadline/reward before process exit without upfront cash.");
                    File.WriteAllText(Path.Combine(output, "recovery-job.txt"), savedJob.id); yield return Capture("recovery-before-exit");
                } else {
                    string engine = File.ReadAllText(Path.Combine(output, "retained-engine.txt"));
                    check(game.Profile.cars.Count == 0 && game.Profile.SelectedCar == null && game.ScreenName == "Collection" && game.Profile.engines.Any(e => e.instanceId == engine), "Actual second process opens empty collection without spawning an OEM replacement.");
                    yield return ClickPart(mouse, "collection-recovery"); var job = game.Profile.recoveryWork;
                    check(job != null && job.id == File.ReadAllText(Path.Combine(output, "recovery-job.txt")) && game.Profile.cash == 0, "Actual second process resumes the saved work deadline/reward without restarting it or paying early."); yield return Capture("recovery-work");
                    while (DragLab.Domain.RuntimeEntropy.UtcNow.Ticks < job.readyUtcTicks) yield return null;
                    yield return ClickPart(mouse, "recovery-claim"); check(game.Profile.cash == job.reward && game.Profile.recoveryWork == null, "Actual real-time deadline completes once and claim pays the saved reward.");
                    var model = game.vehicles.Where(v => v.reputationRequired == 0).OrderBy(v => PaidEconomy.DealerPrice(game.Profile.catalog, game.Costs, game.PricingPolicy, game.AuthoredOffer(v.spec.id))).First();
                    int price = PaidEconomy.DealerPrice(game.Profile.catalog, game.Costs, game.PricingPolicy, game.AuthoredOffer(model.spec.id)); int advances = 0;
                    while (game.Profile.cash < price) {
                        yield return ClickPart(mouse, "recovery-start");
                        // Explicit isolated-fixture time advance after verifying one real deadline.
                        game.Profile.recoveryWork.startedUtcTicks = DragLab.Domain.RuntimeEntropy.UtcNow.AddMinutes(-2).Ticks; game.Profile.recoveryWork.readyUtcTicks = DragLab.Domain.RuntimeEntropy.UtcNow.AddSeconds(-1).Ticks; ProfileStore.Save(game.Profile); yield return new WaitForSecondsRealtime(.1f); yield return ClickPart(mouse, "recovery-claim"); if (++advances > 40) throw new InvalidOperationException("Recovery cannot fund package.");
                    }
                    check(game.Profile.cars.Count == 0 && game.Profile.cash >= price && game.Profile.cash <= game.Costs.recoveryCashLimit && game.Profile.engines.Any(e => e.instanceId == engine), "Repeated bounded recovery buttons fund replacement while retaining assets; subsequent deadlines advanced only in fixture.");
                    yield return ClickPart(mouse, "recovery-collection"); int cash = game.Profile.cash; yield return ClickPart(mouse, "car-buy:" + model.spec.id);
                    check(game.Profile.cars.Count == 1 && game.Profile.SelectedCar.vehicleId == model.spec.id && game.Profile.cash == cash - price && MechanicsOwnership.Resolve(game.Profile, game.Profile.SelectedCar, game.Profile.catalog).Drivable && game.Profile.engines.Any(e => e.instanceId == engine && string.IsNullOrEmpty(e.parentVehicleId)), "Actual funded replacement purchase restores playable car without consuming retained engine."); yield return Capture("recovered-replacement");
                    var loaded = ProfileStore.Load(); check(loaded.cars.Count == 1 && loaded.cash == game.Profile.cash && loaded.economyReceipts.Count == game.Profile.economyReceipts.Count, "Recovered garage and operating receipts persist after purchase.");
                }
            }
            check(errors == 0, "No runtime/GUI/DSP errors."); File.WriteAllText(Path.Combine(output, resume ? "resume-verification.txt" : "stage-verification.txt"), log + "Runtime errors: " + errors + "\n"); File.WriteAllText(Path.Combine(output, "runtime-verification.txt"), log + "Runtime errors: " + errors + "\n");
#else
            yield return null; throw new InvalidOperationException("Requires Windows development player.");
#endif
        }
    }
}

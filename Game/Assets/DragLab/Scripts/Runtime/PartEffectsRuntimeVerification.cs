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
        IEnumerator PartEffectsRun()
        {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
            var log = new StringBuilder("Part effects / actual Windows shop mouse / isolated profile.\n");
            Action<bool, string> require = (ok, label) => { Check(ok, label); log.AppendLine("PASS: " + label); };
            game.Profile.cash = 100000; ProfileStore.Save(game.Profile);
            var catalog = game.Profile.catalog; string model = game.Profile.SelectedCar.vehicleId;
            using (var mouse = new WindowsKeyboardProbe()) {
                foreach (var module in new[] { PartCategory.RevLimiter, PartCategory.TwoStep, PartCategory.ShiftLight }) {
                    game.ShowParts(module); yield return new WaitForSecondsRealtime(.3f);
                    string definition = catalog.Id(model, module, 1); int cash = game.Profile.cash;
                    yield return ClickPart(mouse, "preview:" + definition);
                    require(game.ProposedInspection != null && game.Profile.cash == cash, "Actual mouse preview is isolated: " + module);
                    yield return ClickPart(mouse, "fit:" + definition);
                    require(game.Profile.cash == cash - catalog.Find(definition).price && MechanicsOwnership.Definitions(game.Profile, game.Profile.SelectedCar)[module] == definition, "Actual mouse buys/installs separate module: " + module);
                    yield return Capture("shop-" + module);
                }
                game.OpenTuning(); yield return new WaitForSecondsRealtime(.3f);
                string saved = File.ReadAllText(ProfileStore.FilePath);
                game.DraftValues.limiter = LimiterStyle.Soft; game.DraftValues.revLimitRpm = 4800;
                game.DraftValues.twoStep = EquipmentSetting.On; game.DraftValues.twoStepStyle = LimiterStyle.Hard; game.DraftValues.launchRpm = 2600;
                game.DraftValues.shiftLightRpm = 4200;
                yield return new WaitForSecondsRealtime(.25f);
                require(File.ReadAllText(ProfileStore.FilePath) == saved, "Equipment draft changes remain unsaved before Apply.");
                yield return Capture("equipment-tuning-draft");
                yield return ClickTune(mouse, "apply-top");
                var r = MechanicsOwnership.Resolve(game.Profile, game.Profile.SelectedCar, catalog); var controls = new EngineBehaviorProfile(r.spec, r.tune);
                require(controls.limiter == LimiterStyle.Soft && controls.limitRpm == 4800 && controls.twoStep && controls.launchRpm == 2600 && r.tune.ShiftLightRpm(r.spec) == 4200, "Actual Apply commits hardware-permitted threshold/behavior settings independently.");
                yield return Capture("equipment-tuning-applied");
                game.ShowParts(PartCategory.RevLimiter); yield return new WaitForSecondsRealtime(.25f); yield return ClickPart(mouse, "remove");
                r = MechanicsOwnership.Resolve(game.Profile, game.Profile.SelectedCar, catalog); controls = new EngineBehaviorProfile(r.spec, r.tune);
                require(controls.limiter == LimiterStyle.None && controls.twoStep && r.tune.partShiftLight, "Actual limiter removal removes its protection without deleting independent launch/light hardware.");
                game.ShowParts(PartCategory.Interior); yield return new WaitForSecondsRealtime(.25f);
                double mass = game.SavedInspection.vehicleMassKg;
                string oldId = game.Profile.SelectedCar.installed.slots.Find(s => s.slot == "Interior").partInstanceId;
                yield return ClickPart(mouse, "preview-remove");
                require(game.ProposedInspection.vehicleMassKg == mass - 60 && game.SavedInspection.vehicleMassKg == mass, "Actual strip preview shows 60 kg mass reduction without mutating car.");
                yield return Capture("interior-strip-preview"); yield return ClickPart(mouse, "remove");
                require(game.SavedInspection.vehicleMassKg == mass - 60 && MechanicsOwnership.Resolve(game.Profile, game.Profile.SelectedCar, catalog).Drivable && game.Profile.parts.Any(p => p.instanceId == oldId && p.location == PartLocation.Inventory), "Actual interior removal keeps car runnable and original reusable.");
                var reload = ProfileStore.Load(); require(!reload.SelectedCar.installed.slots.Any(s => s.slot == "Interior") && reload.SelectedCar.tune.shiftLightRpm == 4200, "Disk reload retains stripped slot and independent equipment tuning.");
                yield return ClickPart(mouse, "install:" + oldId);
                require(game.SavedInspection.vehicleMassKg == mass, "Actual reuse restores original interior mass without purchase.");
                game.ShowParts(PartCategory.Cooling); yield return new WaitForSecondsRealtime(.25f);
                yield return ClickPart(mouse, "preview:" + catalog.Id(model, PartCategory.Cooling, 1));
                require(game.ProposedInspection.coolingTransferPercent > game.SavedInspection.coolingTransferPercent && game.ProposedInspection.radiatorWattsPerC > game.SavedInspection.radiatorWattsPerC && game.ProposedInspection.peakHp == game.SavedInspection.peakHp, "Actual radiator preview improves heat-transfer/rejection stats without invented HP.");
                yield return Capture("cooling-preview");
                game.ShowParts(PartCategory.Differential); yield return new WaitForSecondsRealtime(.25f);
                yield return ClickPart(mouse, "preview:" + catalog.Id(model, PartCategory.Differential, 1));
                require(game.ProposedInspection.source.build.spec.frontDifferential.kind == DifferentialKind.LimitedSlip, "Actual differential preview resolves distinct limited-slip hardware.");
                game.ScrollInspectionTo(3600); yield return new WaitForSecondsRealtime(.25f); yield return Capture("differential-details");
            }
            require(errors == 0, "No runtime/GUI/DSP errors in part effects and equipment controls.");
            File.WriteAllText(Path.Combine(output, "runtime-verification.txt"), log + "Runtime errors: " + errors + "\n");
#else
            yield return null; throw new InvalidOperationException("Part effects UI verification requires Windows development player.");
#endif
        }
    }
}

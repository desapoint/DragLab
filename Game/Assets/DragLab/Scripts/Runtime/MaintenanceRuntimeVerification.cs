using System;
using System.IO;
using System.Linq;
using System.Collections;
using UnityEngine;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    public sealed partial class RuntimeVerification
    {
        IEnumerator MaintenanceRun()
        {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
            var log = new System.Text.StringBuilder("Component maintenance / actual Windows mouse / isolated saved inventory.\n");
            Action<bool, string> require = (ok, label) => { Check(ok, label); log.AppendLine("PASS: " + label); };
            var p = game.Profile; var car = p.SelectedCar; var catalog = p.catalog; p.cash = 100000;
            var piston = p.parts.Find(i => i.instanceId == car.installed.slots.Find(s => s.slot == "Pistons").partInstanceId);
            var brake = p.parts.Find(i => i.instanceId == car.installed.slots.Find(s => s.slot == "Brakes").partInstanceId);
            var clutch = p.parts.Find(i => i.instanceId == car.installed.slots.Find(s => s.slot == "Clutch").partInstanceId);
            piston.condition.health = 0; piston.condition.failed = true; piston.condition.failureReason = "Overrev fixture";
            brake.condition.health = .33f; brake.condition.failed = true; brake.condition.failureReason = "Brake thermal overload fixture";
            clutch.condition.health = .4f;
            string pistonId = piston.instanceId, brakeId = brake.instanceId, clutchId = clutch.instanceId;
            string spareId = PartOperations.Buy(p, catalog, car.instanceId, piston.definitionId, false);
            p.parts.Find(i => i.instanceId == spareId).condition.health = .72f;
            string brokenSpare = PartOperations.Buy(p, catalog, car.instanceId, piston.definitionId, false);
            var broken = p.parts.Find(i => i.instanceId == brokenSpare); broken.condition.failed = true; broken.condition.failureReason = "Broken loose spare fixture";
            // Compare the actual applied tune: fixed factory hardware replaces legacy zero
            // placeholders with unit multipliers when any fitted part is projected.
            ProfileStore.Save(p); string tune = JsonUtility.ToJson(MechanicsOwnership.Resolve(p, car, catalog).tune); File.WriteAllText(Path.Combine(output, "tune-before.json"), tune); int initialCash = p.cash;
            require(game.ServiceParts().Count == 3 && game.ServiceParts().Take(2).All(PrototypeGame.PartFailed), "Summary contains only installed failures/worn parts, puts failures first, and excludes loose broken inventory.");
            yield return new WaitForSecondsRealtime(.3f); yield return Capture("maintenance-garage-summary");
            using (var mouse = new WindowsKeyboardProbe())
            {
                yield return ClickPart(mouse, "repairs-open");
                yield return Capture("maintenance-failed-components");
                require(game.partRects.ContainsKey("repair-buy:" + brakeId) && game.partRects.ContainsKey("repair-spare:" + pistonId), "Actual Garage shortcut opens failed-component cards with purchase and usable inventory replacement actions.");
                yield return ClickPart(mouse, "repair-preview:" + pistonId);
                require(game.ProposedInspection != null && game.Profile.cash == initialCash && game.Profile.parts.Find(i => i.instanceId == pistonId).condition.failed, "New-part preview shows build/dyno comparison without buying or healing.");
                yield return ClickPart(mouse, "repair-spare:" + pistonId);
                require(game.Profile.SelectedCar.installed.slots.Find(s => s.slot == "Pistons").partInstanceId == spareId && game.Profile.parts.Find(i => i.instanceId == spareId).condition.health == .72f, "Actual Fit spare selects the healthiest usable compatible item and preserves its used condition.");
                require(game.Profile.cash == initialCash && game.Profile.parts.Find(i => i.instanceId == pistonId).location == PartLocation.Inventory && game.Profile.parts.Find(i => i.instanceId == pistonId).condition.failed, "Inventory replacement costs no purchase cash and retains the original failed instance.");
                require(game.Profile.parts.Find(i => i.instanceId == brokenSpare).location == PartLocation.Inventory && game.ServiceParts().Count(PrototypeGame.PartFailed) == 1, "A flagged failed spare at 100% health is not selected; installed failure count refreshes immediately.");
                game.Profile.cash = 0; ProfileStore.Save(game.Profile); yield return new WaitForSecondsRealtime(.15f);
                string bytes = File.ReadAllText(ProfileStore.FilePath);
                yield return ClickPart(mouse, "repair-buy:" + brakeId);
                require(game.Profile.cash == 0 && game.Profile.SelectedCar.installed.slots.Find(s => s.slot == "Brakes").partInstanceId == brakeId && File.ReadAllText(ProfileStore.FilePath) == bytes, "Unaffordable quick replacement stays disabled without changing cash, installation or disk.");
                yield return ClickPart(mouse, "repair-options:" + brakeId);
                require(game.partRects.ContainsKey("fit:" + brake.definitionId), "Choose alternatives opens the failed component's correct shop/inventory category.");
                yield return Capture("maintenance-brake-category");
                yield return ClickPart(mouse, "repairs-open");
                game.Profile.cash = 100000; ProfileStore.Save(game.Profile); yield return new WaitForSecondsRealtime(.15f);
                yield return ClickPart(mouse, "repair-buy:" + brakeId);
                var repaired = game.Profile.parts.Find(i => i.instanceId == game.Profile.SelectedCar.installed.slots.Find(s => s.slot == "Brakes").partInstanceId);
                require(repaired.instanceId != brakeId && repaired.definitionId == brake.definitionId && repaired.condition.health == 1 && !repaired.condition.failed && game.Profile.cash == 100000 - catalog.Find(brake.definitionId).price, "Actual Buy same + replace charges the shown price once and fits a new healthy instance of the same component.");
                require(game.Profile.parts.Find(i => i.instanceId == brakeId).location == PartLocation.Inventory && game.Profile.parts.Find(i => i.instanceId == brakeId).condition.health == .33f && game.Profile.parts.Find(i => i.instanceId == brakeId).condition.failureReason == "Brake thermal overload fixture", "Removed brakes retain partial health, failure flag and cause rather than becoming repaired stock.");
                string afterTune = JsonUtility.ToJson(MechanicsOwnership.Resolve(game.Profile, game.Profile.SelectedCar, catalog).tune);
                File.WriteAllText(Path.Combine(output, "tune-after.json"), afterTune);
                require(game.ServiceParts().Count == 1 && game.ServiceParts()[0].instanceId == clutchId && afterTune == tune, "Repairs remove resolved failures, keep unrelated wear visible, and preserve the actual applied tune.");
                yield return Capture("maintenance-replaced-worn-clutch");
                var loaded = ProfileStore.Load();
                require(loaded.SelectedCar.installed.slots.Find(s => s.slot == "Pistons").partInstanceId == spareId && loaded.parts.Find(i => i.instanceId == spareId).condition.health == .72f && loaded.parts.Find(i => i.instanceId == brakeId).condition.failed, "Replacement ownership, cash and retained conditions survive reload.");
            }
            require(errors == 0, "Maintenance UI has no runtime/GUI/DSP errors.");
            File.WriteAllText(Path.Combine(output, "runtime-verification.txt"), log + "Runtime errors: " + errors + "\n");
#else
            yield return null; throw new InvalidOperationException("Maintenance mouse probe requires Windows development player.");
#endif
        }
    }
}

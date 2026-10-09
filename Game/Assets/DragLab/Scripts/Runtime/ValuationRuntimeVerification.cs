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
        IEnumerator ValuationRun()
        {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
            var log = new System.Text.StringBuilder("3C actual Windows quote / buyer comparison / kept assets / stale state.\n");
            Action<bool, string> check = (ok, label) => { Check(ok, label); log.AppendLine("PASS: " + label); File.WriteAllText(Path.Combine(output, "verification-progress.txt"), log.ToString()); };
            game.Profile.cash = 100000; ProfileStore.Save(game.Profile); var car = game.Profile.SelectedCar; string engine = car.engineAssemblyId; string piston = car.installed.slots.Find(s => s.slot == "Pistons").partInstanceId; int cash = game.Profile.cash, count = game.Profile.parts.Count;
            using (var mouse = new WindowsKeyboardProbe()) {
                yield return ClickPart(mouse, "valuation-open");
                check(game.ScreenName == "Valuation" && game.valuationQuote.Kind == ValuationKind.Car && game.valuationStatus == null, "Garage button opens a valid exact-car quote.");
                yield return ClickPart(mouse, "quote-engine"); yield return ClickPart(mouse, "quote-buyer:builder"); var assembled = game.valuationQuote;
                check(assembled.Total > assembled.LoosePartTotal, "Engine-builder actual button shows assembly premium above loose components.");
                yield return ClickPart(mouse, "quote-buyer:wholesale"); check(game.valuationQuote.Total < assembled.LoosePartTotal, "Wholesaler actual button shows below loose-component total."); yield return Capture("engine-buyer-discount");
                yield return ClickPart(mouse, "quote-details"); yield return Capture("price-adjustments"); yield return ClickPart(mouse, "quote-details");
                yield return ClickPart(mouse, "quote-chassis"); check(!game.valuationQuote.IncludedIds.Contains(engine) && !game.valuationQuote.IncludedIds.Contains(piston), "Keep-engine chassis quote excludes exact engine and all its children."); yield return Capture("chassis-keep-engine");
                yield return ClickPart(mouse, "quote-car"); yield return ClickPart(mouse, "quote-retain-details"); yield return ClickPart(mouse, "quote-keep:" + piston);
                check(game.valuationKept.Contains(piston) && !game.valuationQuote.IncludedIds.Contains(piston) && !game.valuationQuote.Complete, "Actual keep-part control removes piston value and marks incomplete build."); yield return Capture("retained-piston");
                yield return ClickPart(mouse, "quote-keep:" + piston); yield return ClickPart(mouse, "quote-retain-details");
                check(game.Profile.cash == cash && game.Profile.parts.Count == count && game.Profile.SelectedCar.engineAssemblyId == engine, "All quote UI operations preserve money and physical assets.");
                var old = game.valuationQuote; game.Profile.parts.Find(i => i.instanceId == piston).condition.health = .5f; yield return ClickPart(mouse, "quote-validate");
                check(game.valuationStatus != null && game.valuationQuote == old, "Existing immutable quote becomes stale on same-revision wear; never silently replaced."); yield return Capture("quote-stale");
                yield return ClickPart(mouse, "quote-refresh"); check(game.valuationStatus == null && game.valuationQuote.Total < old.Total, "Explicit refresh prices new wear and restores validation.");
                yield return ClickPart(mouse, "quote-assemblies"); yield return ClickPart(mouse, "engine-detach"); yield return ClickPart(mouse, "engine-quote:" + engine);
                check(game.valuationQuote.Kind == ValuationKind.Engine && game.valuationQuote.AssetId == engine && game.valuationStatus == null, "Stored-engine actual quote button captures detached retained hardware."); yield return Capture("stored-engine-quote");
                game.ShowParts(PartCategory.Pistons); yield return new WaitForSecondsRealtime(.2f); // Stored engine removal uses its actual assembly controls.
                game.OpenAssemblies(); yield return new WaitForSecondsRealtime(.2f); yield return ClickPart(mouse, "engine-inspect:" + engine); yield return ClickPart(mouse, "engine-remove:" + piston);
                game.ShowParts(PartCategory.Pistons); yield return new WaitForSecondsRealtime(.2f); yield return ClickPart(mouse, "part-quote:" + piston);
                check(game.valuationQuote.Kind == ValuationKind.LoosePart && game.valuationQuote.IncludedIds.Contains(piston) && game.valuationQuote.Lines.Any(l => l.Label.Contains("Wear")), "Actual inventory quote button explains worn loose pack price."); yield return Capture("worn-loose-part-quote");
                game.OpenCollection(); yield return new WaitForSecondsRealtime(.2f); yield return ClickPart(mouse, "car-quote:" + car.instanceId);
                check(game.valuationQuote.Kind == ValuationKind.Car && !game.valuationQuote.Complete && !game.valuationQuote.IncludedIds.Contains(engine), "Collection exact-car quote detects missing engine without crediting stored assets.");
                check(game.Profile.cash == cash && game.Profile.parts.Count == count && MechanicsOwnership.Validate(game.Profile, game.Profile.catalog) == null, "Ownership and currency stay intact after UI preview / detach / removal.");
                var loaded = ProfileStore.Load(); check(loaded.engines.Any(e => e.instanceId == engine && string.IsNullOrEmpty(e.parentVehicleId)) && loaded.parts.Any(i => i.instanceId == piston && i.location == PartLocation.Inventory && i.condition.health == .5f), "Disk reload retains actual worn detached hardware; no quote grants assets.");
            }
            check(errors == 0, "No runtime / GUI / DSP errors."); File.WriteAllText(Path.Combine(output, "runtime-verification.txt"), log + "Runtime errors: " + errors + "\n");
#else
            yield return null; throw new InvalidOperationException("Requires Windows development player.");
#endif
        }
    }
}

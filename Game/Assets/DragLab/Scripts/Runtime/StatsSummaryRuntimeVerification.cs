using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    public sealed partial class RuntimeVerification
    {
        IEnumerator StatsSummaryRun()
        {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
            var log = new System.Text.StringBuilder("Pinned stats summary / actual Windows preview mouse / isolated save.\n");
            Action<bool, string> require = (ok, label) => { Check(ok, label); log.AppendLine("PASS: " + label); };
            ProfileStore.Save(game.Profile); string saved = File.ReadAllText(ProfileStore.FilePath);
            yield return new WaitForSecondsRealtime(.2f);
            require(game.StatsSummaryForVerification.Count == 4 && game.StatsSummaryForVerification.Any(s => s.StartsWith("Power:")) && game.StatsSummaryForVerification.Any(s => s.StartsWith("Vehicle weight:")) && game.StatsSummaryForVerification.All(s => !s.Contains("→")), "Saved-car summary shows core values without a comparison.");
            yield return Capture("summary-saved-car");
            string model = game.Profile.SelectedCar.vehicleId;
            using (var mouse = new WindowsKeyboardProbe()) {
                game.ShowParts(PartCategory.Cooling); yield return new WaitForSecondsRealtime(.2f);
                yield return ClickPart(mouse, "preview:" + game.Profile.catalog.Id(model, PartCategory.Cooling, 1));
                require(game.StatsSummaryForVerification.Any(s => s.StartsWith("Cooling / heat transfer:") && s.Contains("→")) && game.StatsSummaryForVerification.Any(s => s.StartsWith("Radiator cooling:") && s.Contains("→")), "Actual cooling preview puts concrete heat-transfer and radiator improvements in the pinned summary.");
                var rect = game.StatsSummaryRect; var text = string.Join("\n", game.StatsSummaryForVerification);
                require(rect.y >= 150 && rect.yMax < Screen.height - 80, "Summary fits above the details at 1280 × 720.");
                yield return Capture("summary-cooling-preview");
                game.ScrollInspectionTo(1200); yield return new WaitForSecondsRealtime(.2f);
                require(string.Join("\n", game.StatsSummaryForVerification) == text && Math.Abs(game.StatsSummaryRect.y - rect.y) < .01, "Scrolling detailed stats leaves the summary and comparison values pinned in place.");
                yield return Capture("summary-details-scrolled"); game.ScrollInspectionTo(0);
                game.ShowParts(PartCategory.Pistons); yield return new WaitForSecondsRealtime(.2f);
                yield return ClickPart(mouse, "preview:" + game.Profile.catalog.Id(model, PartCategory.Pistons, 1));
                require(game.ProposedInspection.peakHp == game.SavedInspection.peakHp && game.StatsSummaryForVerification.Any(s => s.StartsWith("Part / Torque strength:") && s.Contains("→")) && game.StatsSummaryForVerification.Any(s => s.StartsWith("Part / Internal safe RPM:") && s.Contains("→")), "Forged-internal preview summarizes stronger ratings while engine output remains unchanged.");
                yield return Capture("summary-forged-preview");
                game.ShowParts(PartCategory.Tires);yield return new WaitForSecondsRealtime(.2f);
                yield return ClickPart(mouse,"preview:"+game.Profile.catalog.Id(model,PartCategory.Tires,3));
                require(game.StatsSummaryForVerification.Any(s=>s.StartsWith("Tire condition grip:")&&s.Contains("→"))&&game.StatsSummaryForVerification.Any(s=>s.StartsWith("Tire rolling life:")&&s.Contains("→"))&&game.StatsSummaryForVerification.Any(s=>s.StartsWith("Tire sliding life:")&&s.Contains("→")),"Actual tire preview pins higher grip and separate shorter rolling/sliding life");yield return Capture("summary-tire-tradeoffs");
                game.OpenTuning(); game.DraftValues.wheelDiameter = 1.45f; game.DraftValues.tirePressurePsi = 34;
                SharedUnitSettings.Set(SpeedUnit.MilesPerHour, PressureUnit.Psi); yield return new WaitForSecondsRealtime(.2f);
                require(game.StatsSummaryForVerification.Any(s => s.StartsWith("Vehicle weight:") && s.Contains("→")) && game.StatsSummaryForVerification.Any(s => s.StartsWith("Tire outer radius:") && s.Contains("in")) && game.StatsSummaryForVerification.Any(s => s.StartsWith("Tire pressure:") && s.Contains("psi")), "Tuning summary shows wheel mass/radius effects and selected pressure units.");
                yield return Capture("summary-tuning-tradeoffs");
                game.ShowParts(PartCategory.Brakes); game.PreviewPart(remove: PartCategory.Pistons); yield return new WaitForSecondsRealtime(.2f);
                require(game.StatsSummaryForVerification.Any(s => s.StartsWith("¼ mile") && s.Contains("Unavailable")) && !game.ProposedInspection.completed, "Incomplete previews show unavailable quarter-mile estimates instead of zero seconds.");
                yield return Capture("summary-incomplete-preview");
                require(File.ReadAllText(ProfileStore.FilePath) == saved, "Summary previews and detail scrolling leave saved cash, tune, inventory and condition untouched.");
            }
            require(errors == 0, "Summary UI has no runtime/GUI/DSP errors.");
            File.WriteAllText(Path.Combine(output, "runtime-verification.txt"), log + "Runtime errors: " + errors + "\n");
#else
            yield return null; throw new InvalidOperationException("Summary probe requires Windows development player.");
#endif
        }
    }
}

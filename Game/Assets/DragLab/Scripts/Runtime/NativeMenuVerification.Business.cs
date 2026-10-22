using System;
using System.Collections;
using System.IO;
using System.Linq;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;
using UnityEngine;
using UnityEngine.UIElements;

namespace DragLab.Runtime
{
    internal sealed partial class NativeMenuVerification
    {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
        IEnumerator BusinessChecks(WindowsKeyboardProbe input)
        {
            if (!shell.Visible) yield return Key(input, 0x77);
            yield return Click(input, "navBusiness");
            Check(game.ScreenName == "Business" && !shell.Root.Q("businessPage").ClassListContains("hidden"), "Business opens the native page with persistent navigation");
            Check(game.CommitParts(game.Profile.revision, p => { p.cash = 5000; p.business.bank = 200.75; p.business.accruedThroughUtcTicks = RuntimeEntropy.UtcNow.Ticks; }), "Isolated fixture supplies existing accrued income, without healing parts");
            yield return new WaitForSecondsRealtime(.6f);
            string before = JsonUtility.ToJson(game.Profile); byte[] disk = File.ReadAllBytes(ProfileStore.FilePath);
            yield return Capture("business-overview");
            yield return Click(input, "businessTab:Display jobs");
            var offerButton = shell.Root.Q("businessAssign:photo-session");
            Check(offerButton.enabledInHierarchy, "Eligible exact car can take the existing photo-session contract");
            yield return Click(input, "businessChooseCar");
            Check(shell.Root.Q("businessCar:" + game.Profile.SelectedCar.instanceId) != null, "Job picker names actual owned instance identities");
            yield return Click(input, "businessCar:" + game.Profile.SelectedCar.instanceId);
            var firstContent = shell.Root.Q("businessContent").Children().First();
            yield return new WaitForSecondsRealtime(1.1f);
            Check(ReferenceEquals(firstContent, shell.Root.Q("businessContent").Children().First()), "Countdown ticks retain the same controls and layout rather than rebuilding the list");
            yield return Click(input, "businessTab:Recovery"); yield return Capture("business-recovery");
            yield return Click(input, "settingsButton"); yield return Click(input, "cancelSettings");
            Check(shell.Root.Q("businessTab:Recovery").ClassListContains("selected"), "Settings Cancel preserves the Business subpage");
            yield return Key(input, 0x77); Check(!shell.Visible, "Desktop F8 still exposes the existing Business interface");
            yield return Key(input, 0x77); Check(shell.Visible && shell.Root.Q("businessTab:Recovery").ClassListContains("selected"), "Returning with F8 preserves the new Business subpage");
            yield return Click(input, "navGarage"); yield return Click(input, "navBusiness");
            Check(shell.Root.Q("businessTab:Overview").ClassListContains("selected") && shell.Root.Q<ScrollView>("businessScroll").scrollOffset == Vector2.zero, "Fresh Business navigation resets Overview and scroll");
            Check(before == JsonUtility.ToJson(game.Profile) && disk.SequenceEqual(File.ReadAllBytes(ProfileStore.FilePath)), "Browsing jobs, car picker, recovery, Settings and F8 neither mutate nor save the profile");
            var policy = game.BusinessPolicy; string policyJson = JsonUtility.ToJson(policy), costsJson = JsonUtility.ToJson(game.Costs);
            Check(!game.CommitNativeBusiness(game.Profile.revision - 1, policyJson, costsJson, "Income", "", "") && before == JsonUtility.ToJson(game.Profile), "Stale Business review cannot publish cash");
            Check(!game.CommitNativeBusiness(game.Profile.revision, policyJson + " ", costsJson, "Income", "", "") && before == JsonUtility.ToJson(game.Profile), "Changed captured Business terms require a new review");
            yield return Click(input, "businessClaim"); DialogScrollBounds("Business income");
            yield return Click(input, "dialogCancel"); Check(before == JsonUtility.ToJson(game.Profile), "Cancelled income review leaves the captured contract intact");
            yield return Click(input, "businessClaim");
            using (var locked = new FileStream(ProfileStore.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                yield return Click(input, "dialogConfirm");
                Check(shell.ModalOpen && !string.IsNullOrEmpty(shell.Root.Q<Label>("businessCommitError").text) && before == JsonUtility.ToJson(game.Profile), "Failed durable income claim leaves cash and accrued bank unchanged and stays retryable");
            }
            int cash = game.Profile.cash; long revision = game.Profile.revision;
            yield return Click(input, "dialogConfirm");
            Check(!shell.ModalOpen && game.Profile.cash >= cash + 200 && game.Profile.business.bank < 1, "Confirmed income transfers actual accrued whole dollars and preserves the fractional remainder");
            Check(!game.CommitNativeBusiness(revision, policyJson, costsJson, "Income", "", "") && ProfileStore.Load().cash == game.Profile.cash, "Repeated old claim cannot pay twice and the successful claim survives SQLite reload");
            string carId = game.Profile.SelectedCar.instanceId, engineId = game.Profile.SelectedCar.engineAssemblyId;
            string physical = BusinessPhysicalState(); int level = game.Profile.garageLevel; cash = game.Profile.cash;
            yield return Click(input, "businessExpand"); yield return Capture("business-expand-review"); yield return Click(input, "dialogConfirm");
            Check(game.Profile.garageLevel == level + 1 && game.Profile.cash == cash - policy.upgradeCosts[level - 1], "Expansion pays the authored cost and settles existing accrued income first");
            Check(game.Profile.business.ratePerHour == policy.hourlyRates[level] && game.Profile.business.capHours == policy.capsHours[level], "New garage income and cap are captured from the expansion time");
            yield return Click(input, "businessTab:Display jobs");
            yield return Click(input, "businessAssign:photo-session"); yield return Capture("business-assign-review"); yield return Click(input, "dialogCancel");
            Check(game.Profile.business.jobs.Count == 0, "Cancelling assignment review does not reserve a build");
            yield return Click(input, "businessAssign:photo-session"); yield return Click(input, "dialogConfirm");
            var job = game.Profile.business.jobs.Single();
            Check(job.carId == carId && job.engineId == engineId && game.Profile.SelectedCar.garageJobId == job.id, "Assign reserves the exact chosen car, engine and fitted component identities");
            Check(CareerProgression.EntryError(game.Profile, game.Profile.SelectedCar, null) != null && !shell.Root.Q("businessAssign:photo-session").enabledInHierarchy, "Reserved car cannot enter races or accept another display contract");
            Check(!shell.Root.Q("businessJobClaim:" + job.id).enabledInHierarchy, "Unfinished display job cannot be claimed");
            Check(ProfileStore.Load().business.jobs.Single().readyUtcTicks == job.readyUtcTicks, "Captured job deadline and reservation survive durable reload");
            yield return Capture("business-active-jobs");
            foreach (int width in new[] { 1024, 1920 })
            {
                Screen.SetResolution(width, width == 1024 ? 768 : 1080, false); yield return new WaitForSecondsRealtime(.4f);
                var view = shell.Root.Q<ScrollView>("businessScroll");
                Check(view.worldBound.yMax < shell.Root.Q("footerBar").worldBound.yMin && view.worldBound.xMax <= shell.Root.worldBound.xMax, "Business scroll stays inside the frame at " + width);
                Check(shell.Root.Q("businessChooseCar").worldBound.xMax < shell.Root.worldBound.xMax, "Job car selector remains in bounds at " + width);
                yield return Capture("business-jobs-" + width);
            }
            var target = new RenderTexture(3840, 2160, 24); target.Create(); shell.Panel.targetTexture = target; shell.Refresh(); yield return new WaitForSecondsRealtime(.3f);
            Check(shell.Panel.scale == 2 && shell.Root.Q("businessScroll").worldBound.yMax < shell.Root.Q("footerBar").worldBound.yMin, "Business uses the shared crisp 4K scale and bounded viewport");
            var active = RenderTexture.active; RenderTexture.active = target; var image = new Texture2D(3840, 2160, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 3840, 2160), 0, 0); image.Apply(); File.WriteAllBytes(Path.Combine(folder, "business-4k.png"), image.EncodeToPNG()); RenderTexture.active = active; shell.Panel.targetTexture = null; target.Release(); Destroy(target); Destroy(image);
            Screen.SetResolution(1280, 720, false); yield return new WaitForSecondsRealtime(.4f);
            while (RuntimeEntropy.UtcNow.Ticks < job.readyUtcTicks) yield return new WaitForSecondsRealtime(.5f);
            yield return new WaitForSecondsRealtime(.6f);
            Check(shell.Root.Q("businessJobClaim:" + job.id).enabledInHierarchy && game.Profile.SelectedCar.garageJobId == job.id, "Real elapsed deadline enables Claim while retaining the car reservation");
            cash = game.Profile.cash; revision = game.Profile.revision;
            yield return Click(input, "businessJobClaim:" + job.id); yield return Click(input, "dialogConfirm");
            Check(game.Profile.cash == cash + job.reward && game.Profile.business.jobs.Count == 0 && string.IsNullOrEmpty(game.Profile.SelectedCar.garageJobId), "Claim pays the captured reward exactly once and releases the build");
            Check(!game.CommitNativeBusiness(revision, policyJson, costsJson, "Finish", job.id, "") && game.Profile.cash == cash + job.reward, "Replaying a completed display job cannot duplicate rewards");
            yield return Click(input, "businessAssign:photo-session"); yield return Click(input, "dialogConfirm"); var cancelled = game.Profile.business.jobs.Single(); cash = game.Profile.cash;
            yield return Click(input, "businessJobCancel:" + cancelled.id); yield return Click(input, "dialogConfirm");
            Check(game.Profile.cash == cash && game.Profile.business.jobs.Count == 0 && string.IsNullOrEmpty(game.Profile.SelectedCar.garageJobId), "Explicit cancellation releases the original build with no reward");
            Check(physical == BusinessPhysicalState(), "Income, expansion, real-time job completion and cancellation preserve all fuel, fluids, wear and component identities");
            Check(GarageBusiness.Validate(ProfileStore.Load()) == null, "Native Business actions leave a valid durable reservation ledger");
            // Sell the actual last car through the existing transaction, to exercise recovery without any car.
            var sale = InventoryPricing.Sell(game.Profile, ValuationKind.Car, carId, game.PricingPolicy, "enthusiast", RuntimeEntropy.UtcNow.Ticks);
            Check(game.CommitParts(game.Profile.revision, p => { PaidEconomy.Sell(p, p.catalog, game.PricingPolicy, sale, RuntimeEntropy.UtcNow.Ticks); p.cash = 0; }), "Isolated recovery fixture legally disposes the last car and starts with no cash");
            game.OpenRecovery(); yield return new WaitForSecondsRealtime(.6f);
            Check(shell.Visible && shell.Root.Q("businessTab:Recovery").ClassListContains("selected") && game.Profile.cars.Count == 0, "Empty-garage recovery route opens the native Business Recovery tab");
            yield return Click(input, "businessRecoveryStart"); yield return Click(input, "dialogConfirm");
            var recovery = game.Profile.recoveryWork;
            Check(recovery != null && recovery.reward == Math.Min(game.Costs.recoveryReward, game.Costs.recoveryCashLimit), "Recovery work captures the existing bounded reward without requiring a car");
            Check(!shell.Root.Q("businessRecoveryClaim").enabledInHierarchy, "Recovery claim stays disabled before its real deadline");
            NativeMobile.SimulatedTouch = true; shell.Refresh(); yield return new WaitForSecondsRealtime(.2f);
            InterfaceComparison.SetReplacement("Recovery", false);
            Check(shell.Visible && InterfaceComparison.IsReplacement("Recovery") && NativeMenuShell.Supports("Recovery"), "Touch builds keep Recovery in the new interface and cannot switch to legacy");
            Check(shell.Root.Query<Button>().ToList().All(b => b.tabIndex == -1), "Business buttons retain the shared no-Tab-navigation policy");
            yield return Capture("business-mobile-recovery"); NativeMobile.SimulatedTouch = false; InterfaceComparison.SetReplacement("Recovery", true);
            while (RuntimeEntropy.UtcNow.Ticks < recovery.readyUtcTicks) yield return new WaitForSecondsRealtime(.5f);
            yield return new WaitForSecondsRealtime(.6f);
            yield return Click(input, "businessRecoveryClaim"); yield return Click(input, "dialogConfirm");
            Check(game.Profile.recoveryWork == null && game.Profile.cash == recovery.reward && ProfileStore.Load().cash == recovery.reward, "Real recovery deadline pays its captured reward once and persists without an owned car");
            Check(game.Profile.economyReceipts.Count(r => r.id == recovery.id) == 1 && shell.Root.Q("businessContent").Query<Label>().ToList().Any(l => l.text == "Recovery work"), "Recovery receipt appears once in the native recent-operations list");
            yield return Capture("business-recovery-complete");
        }
        string BusinessPhysicalState()
        {
            var p = game.Profile;
            return string.Join("", p.cars.Select(c => c.instanceId + c.engineAssemblyId + JsonUtility.ToJson(c.fuel) + JsonUtility.ToJson(c.fluids) + JsonUtility.ToJson(c.tune) + c.paintHex)) + string.Join("", p.parts.Select(part => part.instanceId + JsonUtility.ToJson(part.condition)));
        }
#endif
    }
}

using System;
using DragLab.Domain;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    public sealed partial class PrototypeGame
    {
        TypedFluidRefillPilot manualRefillPilot;
        string manualRefillFacadeError, manualRefillSeenStatus;
        TypedFluidRefillResult manualRefillSeenResult;
        bool manualRefillSeenSuccess;
        long manualRefillCompletion;

        TypedFluidRefillPilot ManualRefillPilot => manualRefillPilot ??
            (manualRefillPilot = new TypedFluidRefillPilot(() => profile, () => Costs));

        // Pending is the UI's in-flight state. The wider mutation fence also includes
        // unknown and durable-but-unhandled operations, whose retry buttons stay usable.
        internal bool ManualRefillPending => manualRefillPilot != null && manualRefillPilot.HasPending &&
            !manualRefillPilot.CanRetry && !manualRefillPilot.NeedsRecovery;
        internal bool ManualRefillCanRetry => manualRefillPilot != null && manualRefillPilot.CanRetry;
        internal bool ManualRefillNeedsRecovery => manualRefillPilot != null && manualRefillPilot.NeedsRecovery;
        internal bool ManualRefillBlocksMutation => manualRefillPilot != null &&
            (manualRefillPilot.HasPending || manualRefillPilot.NeedsRecovery);
        internal bool ManualRefillLastSucceeded => manualRefillPilot != null && manualRefillPilot.LastSucceeded;
        internal string ManualRefillOperationId => manualRefillPilot?.OperationId;
        internal long ManualRefillCompletion => manualRefillCompletion;
        internal string ManualRefillStatus
        {
            get
            {
                if (!string.IsNullOrEmpty(manualRefillFacadeError)) return manualRefillFacadeError;
                if (manualRefillPilot == null) return "";
                if (!string.IsNullOrEmpty(manualRefillPilot.Error)) return manualRefillPilot.Error;
                if (ManualRefillCanRetry) return "Save status is unknown. Check the existing operation before trying again.";
                if (ManualRefillNeedsRecovery) return "The save needs to be reloaded before maintenance can continue.";
                if (ManualRefillPending) return "Saving refill… You can keep browsing.";
                return ManualRefillLastSucceeded ? "Refill saved." : "";
            }
        }

        internal bool BlockForManualRefill()
        {
            if (!ManualRefillBlocksMutation) return false;
            notice = ManualRefillStatus;
            return true;
        }

        bool CanRequestManualRefill(out string error)
        {
            error = null;
            if (profile == null || resetRequired) error = "A verified save is required for maintenance.";
            else if (ManualRefillBlocksMutation) error = ManualRefillStatus;
            else if (screen == "Race" || ResultSettlementPending || race != null && !liveRunClosed)
                error = "Return to the garage and finish saving the run before maintenance.";
            else if (pendingAction != null) error = "Finish the current action before maintenance.";
            else if (saveDelay > 0 && !drivingPreferencesDirty) error = "Save the current changes before maintenance.";
            return error == null;
        }

        internal bool TryQuoteManualRefill(string carId, double oil, double coolant,
            out TypedFluidRefillQuote quote, out string error)
        {
            quote = null;
            if (!CanRequestManualRefill(out error)) return false;
            return ManualRefillPilot.TryQuote(profile, carId, oil, coolant,
                DragLab.Domain.RuntimeEntropy.UtcNow.Ticks, out quote, out error);
        }

        internal bool BeginManualRefill(TypedFluidRefillQuote quote)
        {
            if (!CanRequestManualRefill(out var error))
            {
                notice = manualRefillFacadeError = error;
                return false;
            }
            if (!ManualRefillPilot.TryBegin(quote, DragLab.Domain.RuntimeEntropy.UtcNow.Ticks, out error))
            {
                notice = manualRefillFacadeError = error;
                return false;
            }
            manualRefillFacadeError = null;
            notice = ManualRefillStatus;
            nativeMenu?.UpdateTypedRefillDialog();
            return true;
        }

        internal bool RetryManualRefill()
        {
            if (manualRefillPilot == null)
            {
                notice = manualRefillFacadeError = "No refill is pending.";
                return false;
            }
            if (!manualRefillPilot.TryReconcile(out var error))
            {
                notice = manualRefillFacadeError = error;
                return false;
            }
            manualRefillFacadeError = null;
            notice = ManualRefillStatus;
            return true;
        }

        internal bool RecoverManualRefill()
        {
            if (manualRefillPilot == null) return false;
            // Preferences are a separate file; preserve the latest saved settings before
            // the exceptional validated-load boundary. Native settings drafts stay in the UI.
            if (drivingPreferencesDirty)
            {
                if (!ProfileStore.TrySavePreferences(profile, out var preferencesError))
                {
                    notice = manualRefillFacadeError = "Settings save failed: " + preferencesError;
                    return false;
                }
                drivingPreferencesDirty = false;
            }
            if (!manualRefillPilot.TryRecover(out var recovered, out var error))
            {
                notice = manualRefillFacadeError = error;
                return false;
            }
            profile = recovered;
            settings = profile.driving;
            manualRefillFacadeError = null;
            saveDelay = 0;
            lastBenchmarkKey = null;
            paymentCosts.Clear();
            UpdateManualRefill();
            return true;
        }

        void UpdateManualRefill()
        {
            if (manualRefillPilot != null)
            {
                var priorDraft = draft;
                long priorRevision = profile.revision;
                manualRefillPilot.Poll();
                if (manualRefillPilot.LastSucceeded && (!manualRefillSeenSuccess ||
                    !ReferenceEquals(manualRefillSeenResult, manualRefillPilot.Completion)))
                {
                    // A successful scoped refill changes fluids/economy only. Keep the
                    // unrelated owner-thread tuning edits while advancing their fence.
                    if (priorDraft != null && priorDraft.revision == priorRevision &&
                        profile.revision == priorRevision + 1 && Build?.instanceId == priorDraft.vehicleInstanceId)
                        draft = new TuneDraft(Build, profile.revision) { values = priorDraft.values, paint = priorDraft.paint };
                    manualRefillCompletion++;
                    lastBenchmarkKey = null;
                    paymentCosts.Clear();
                }
                manualRefillSeenSuccess = manualRefillPilot.LastSucceeded;
                manualRefillSeenResult = manualRefillPilot.Completion;
                string status = manualRefillPilot.Error ?? (manualRefillPilot.CanRetry ? "Checking save status is required." :
                    manualRefillPilot.NeedsRecovery ? "Reload the save to continue maintenance." :
                    manualRefillPilot.HasPending ? "Saving refill…" : manualRefillPilot.LastSucceeded ? "Refill saved." : "");
                if (status != manualRefillSeenStatus)
                {
                    manualRefillSeenStatus = status;
                    manualRefillFacadeError = null;
                    if (!string.IsNullOrEmpty(status)) notice = status;
                }
            }
            nativeMenu?.UpdateTypedRefillDialog();
        }
    }
}

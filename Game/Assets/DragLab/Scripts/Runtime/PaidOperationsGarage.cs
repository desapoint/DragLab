using System;
using System.Linq;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
namespace DragLab.Runtime
{
    public sealed partial class PrototypeGame
    {
        OperatingPolicyDefinition operatingPolicyAsset;
        internal OperatingPolicy Costs { get { if (operatingPolicyAsset == null) operatingPolicyAsset = Resources.Load<OperatingPolicyDefinition>("Data/Economy/OperatingPolicy"); return operatingPolicyAsset == null ? null : operatingPolicyAsset.policy; } }
        internal PaymentRequest Payment(PaymentKind kind, double oil = 0, double coolant = 0, string target = null, ServiceAction service = ServiceAction.Clean)
        { return new PaymentRequest { kind = kind, carId = Build?.instanceId, litres = oil, coolantLitres = coolant, targetId = target, service = service }; }
        internal VehicleOffer AuthoredOffer(string model)
        { var v = Array.Find(vehicles, a => a.spec.id == model); return v == null ? null : new VehicleOffer { model = model, price = v.purchasePrice, reputation = v.reputationRequired, stock = v.CreateStockTune(), paint = "#" + ColorUtility.ToHtmlStringRGB(v.defaultPaint) }; }
        internal int PaymentCost(PaymentRequest request)
        { try { return PaidEconomy.Preview(profile, profile.catalog, Costs, PricingPolicy, request, DateTime.UtcNow.Ticks).cost; } catch { return -1; } }
        internal bool PayNow(PaymentQuote quote)
        {
            if (quote == null) return false;
            var request = JsonUtility.FromJson<PaymentRequest>(quote.RequestJson);
            return CommitParts(quote.Revision, p => PaidEconomy.Pay(p, p.catalog, Costs, PricingPolicy, quote, DateTime.UtcNow.Ticks, request.kind == PaymentKind.Car ? AuthoredOffer(request.offer.model) : null));
        }
        void QueuePayment(PaymentRequest request)
        {
            try { var quote = PaidEconomy.Quote(profile, profile.catalog, Costs, PricingPolicy, request, DateTime.UtcNow.Ticks); pendingAction = () => PayNow(quote); }
            catch (Exception e) { notice = e.Message; }
        }
        void PaidButton(string id, string label, PaymentRequest request, bool actionable = true)
        {
            int cost = PaymentCost(request); bool previous = GUI.enabled; GUI.enabled = previous && actionable && cost >= 0 && profile.cash >= cost;
            if (PartButton(id, label + (cost < 0 ? " / unavailable" : " / $" + cost))) QueuePayment(request);
            GUI.enabled = previous;
            if (cost > profile.cash) GUILayout.Label("Need $" + (cost - profile.cash) + " more. Recovery work / idle claims / sell retained spares are available.");
        }
        internal void OpenRecovery() { DiscardPreviews(); screen = "Recovery"; scroll = Vector2.zero; partRects.Clear(); }
        void RecoveryGUI()
        {
            bool wrap = GUI.skin.label.wordWrap; GUI.skin.label.wordWrap = true;
            try { DrawRecovery(); } finally { GUI.skin.label.wordWrap = wrap; }
        }
        void DrawRecovery()
        {
            GUILayout.Label("RECOVERY / garage work / operating receipts", stat);
            GUILayout.Label("Keep your retained engines and spares. Earn a bounded payment for a real-time garage job, then buy fuel, a compatible repair or a replacement car. No parts are repaired or created by a claim.");
            if (Costs == null) { GUILayout.Label("Missing operating policy. Prepare it in Unity."); return; }
            long now = DateTime.UtcNow.Ticks; var job = profile.recoveryWork;
            if (job == null) {
                GUILayout.Label("Garage job: " + Costs.recoverySeconds + " seconds / up to $" + Costs.recoveryReward + " / available below $" + Costs.recoveryCashLimit + " cash. Continues offline.");
                GUI.enabled = profile.cash < Costs.recoveryCashLimit;
                if (PartButton("recovery-start", "Start recovery work")) QueuePartAction(p => PaidEconomy.StartRecovery(p, Costs, DateTime.UtcNow.Ticks)); GUI.enabled = true;
            } else {
                GUILayout.Label("Job " + job.id + " / $" + job.reward + " / " + (now >= job.readyUtcTicks ? "Ready" : Math.Ceiling((job.readyUtcTicks - now) / (double)TimeSpan.TicksPerSecond) + " seconds remaining"));
                GUI.enabled = now >= job.readyUtcTicks;
                if (PartButton("recovery-claim", "Claim finished job once")) { string id = job.id; QueuePartAction(p => PaidEconomy.ClaimRecovery(p, id, DateTime.UtcNow.Ticks)); } GUI.enabled = true;
            }
            int idle = profile.PendingIdle(now);
            GUI.enabled = idle > 0;
            if (PartButton("recovery-idle", "Claim existing idle earnings / $" + idle)) QueuePartAction(p => p.ClaimIdle(DateTime.UtcNow.Ticks)); GUI.enabled = true;
            if (PartButton("recovery-collection", "Dealership / owned cars")) pendingAction = OpenCollection;
            if (PartButton("recovery-assets", "Value / sell retained assets")) pendingAction = () => OpenValuation(ValuationKind.Car, Build?.instanceId ?? "");
            if (Build != null && PartButton("recovery-repairs", "Repair / replace selected car parts")) pendingAction = ShowRepairs;
            if (PartButton("recovery-garage", "Garage")) pendingAction = ReturnToGarage;
            GUILayout.Label("Recent committed operations (last 256 retained)");
            foreach (var r in profile.economyReceipts.AsEnumerable().Reverse().Take(20)) GUILayout.Label(r.operation + " / " + (r.cashAfter - r.cashBefore).ToString("+$0;-$0;$0") + " / cash $" + r.cashAfter + " / " + r.id.Substring(0, Math.Min(14, r.id.Length)));
        }
        string saleConfirmation;
        void QuoteSaleGUI(ValuationQuote quote)
        {
            bool previous = GUI.enabled; GUI.enabled = previous && valuationStatus == null && quote != null;
            string label = quote.Kind == ValuationKind.Chassis ? "Salvage chassis / keep engine tree" : "Sell captured " + quote.Kind;
            if (PartButton("quote-sell-preview", label + " / $" + quote.Total)) saleConfirmation = quote.Id;
            if (saleConfirmation == quote.Id) {
                GUILayout.Label("CONFIRM: dispose of " + quote.IncludedIds.Count + " captured identities for $" + quote.Total + ". " + quote.RetainedIds.Count + " selected kept packs go to inventory. " + (quote.Kind == ValuationKind.Chassis ? "Attached engine and fluids remain stored." : "Use Chassis / keep engine to retain the full engine."));
                if (PartButton("quote-sell-confirm", "Confirm disposal / save cash and ownership together")) {
                    long revision = quote.Revision; pendingAction = () => { if (CommitParts(revision, p => PaidEconomy.Sell(p, p.catalog, PricingPolicy, quote, DateTime.UtcNow.Ticks))) { saleConfirmation = null; valuationStatus = "Disposed once; request a quote for another asset."; } };
                }
                if (PartButton("quote-sell-cancel", "Cancel / keep all assets")) saleConfirmation = null;
            }
            GUI.enabled = previous;
        }
    }
}

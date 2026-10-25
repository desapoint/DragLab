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
        { var existing=VehicleListings.Find(profile,model);if(existing!=null)return existing;var v = Array.Find(vehicles, a => a.spec.id == model); return v == null ? null : new VehicleOffer { model = model, price = v.purchasePrice, reputation = v.reputationRequired, stock = v.CreateStockTune(), paint = "#" + ColorUtility.ToHtmlStringRGB(v.defaultPaint) }; }
        readonly System.Collections.Generic.Dictionary<string,int> paymentCosts=new System.Collections.Generic.Dictionary<string,int>();
        PlayerProfile paymentCostsProfile;long paymentCostsRevision=-1;
        internal int PaymentCost(PaymentRequest request)
        {
            if(request!=null&&request.kind==PaymentKind.Fluids)
                return TryQuoteManualRefill(request.carId,request.litres,request.coolantLitres,out var refill,out _)?refill.Cost:-1;
            if(paymentCostsProfile!=profile||paymentCostsRevision!=profile.revision){paymentCosts.Clear();paymentCostsProfile=profile;paymentCostsRevision=profile.revision;}
            string key=JsonUtility.ToJson(request)+"/"+JsonUtility.ToJson(Build?.fuel)+"/"+JsonUtility.ToJson(Build?.fluids);
            if(paymentCosts.TryGetValue(key,out var cost))return cost;
            try{cost=PaidEconomy.Preview(profile,profile.catalog,Costs,PricingPolicy,request,DragLab.Domain.RuntimeEntropy.UtcNow.Ticks).cost;}catch{cost=-1;}
            if(paymentCosts.Count>=256)paymentCosts.Clear();paymentCosts[key]=cost;return cost;
        }
        internal bool PayNow(PaymentQuote quote)
        {
            if(BlockForManualRefill())return false;
            if (quote == null) return false;
            var request = JsonUtility.FromJson<PaymentRequest>(quote.RequestJson);
            return CommitParts(quote.Revision, p => PaidEconomy.Pay(p, p.catalog, Costs, PricingPolicy, quote, DragLab.Domain.RuntimeEntropy.UtcNow.Ticks, (request.kind == PaymentKind.Car || request.kind==PaymentKind.VehicleInspection) ? string.IsNullOrEmpty(request.offer.listingId)?AuthoredOffer(request.offer.model):VehicleListings.Find(p,request.offer.listingId) : null));
        }
        internal bool InspectNativeCar(PaymentQuote quote)
        {
            try{if(screen!="Garage"||pendingAction!=null||quote==null||JsonUtility.FromJson<PaymentRequest>(quote.RequestJson)?.kind!=PaymentKind.VehicleInspection)throw new InvalidOperationException("Reopen the dealership inspection.");
                if(!PayNow(quote))return false;notice="Inspection saved.";return true;
            }catch(Exception error){notice=error.Message;return false;}
        }
        internal bool BuyNativeCar(PaymentQuote quote)
        {
            try {
                if(screen!="Garage"||pendingAction!=null)throw new InvalidOperationException("Garage is no longer available.");
                if(quote==null||JsonUtility.FromJson<PaymentRequest>(quote.RequestJson)?.kind!=PaymentKind.Car)throw new InvalidOperationException("Request a dealership quote first.");
                if(!PayNow(quote))return false;
                ShowGarageCar();notice="Car purchased and selected.";return true;
            }catch(Exception error){notice=error.Message;return false;}
        }
        internal bool CommitNativeMaintenance(PaymentRequest request, long revision, string snapshot, int shownCost, string replacedPartId = null)
        {
            try {
                if(BlockForManualRefill())return false;
                if(request!=null&&request.kind==PaymentKind.Fluids)throw new InvalidOperationException("Confirm a current typed refill quote instead.");
                if (screen != "Garage" || pendingAction != null) throw new InvalidOperationException("Garage is no longer available.");
                if (profile.revision != revision || !SnapshotEquality.Profile(snapshot,profile,profile.catalog)) throw new InvalidOperationException("Car details changed. Close and reopen maintenance.");
                if (request == null || !new[] {PaymentKind.Fuel,PaymentKind.Fluids,PaymentKind.OilChange,PaymentKind.CoolantFlush,PaymentKind.ClutchOil,PaymentKind.Repair,PaymentKind.Part,PaymentKind.Rebuild}.Contains(request.kind)) throw new InvalidOperationException("Unsupported maintenance action.");
                if (request.kind == PaymentKind.Part) {
                    var car = profile.cars.Find(c => c.instanceId == request.carId);
                    var old = profile.parts.Find(p => p.instanceId == replacedPartId);
                    if (car == null || old == null || !request.install || request.targetId != old.definitionId || !car.installed.slots.Any(s => s.partInstanceId == old.instanceId)) throw new InvalidOperationException("Installed component changed. Reopen maintenance.");
                }
                var quote=PaidEconomy.Quote(profile,profile.catalog,Costs,PricingPolicy,request,DragLab.Domain.RuntimeEntropy.UtcNow.Ticks);
                if (quote.Cost != shownCost) throw new InvalidOperationException("Maintenance price changed. Close and reopen.");
                if (!PayNow(quote)) return false;
                if (request.carId == profile.selectedOwnedVehicleId) ShowGarageCar();
                notice = "Maintenance saved."; return true;
            } catch (Exception error) { notice = error.Message; return false; }
        }
        internal bool CommitNativeService(string carId,long revision,string snapshot,string rebuildId=null)
        {
            try {
                if(BlockForManualRefill())return false;
                if(screen!="Garage"||pendingAction!=null||profile.revision!=revision||!SnapshotEquality.Profile(snapshot,profile,profile.catalog))throw new InvalidOperationException("Car details changed. Close and reopen maintenance.");
                if(!CommitParts(revision,p=> {
                    if(rebuildId!=null)EngineRebuilds.Collect(p,carId,rebuildId,DragLab.Domain.RuntimeEntropy.UtcNow.Ticks);
                    else {
                        var car=p.cars.Find(c=>c.instanceId==carId);EngineRebuilds.RequireAvailable(car);
                        if(string.IsNullOrEmpty(car.engineAssemblyId))throw new InvalidOperationException("No attached engine to service.");
                        double capacity=MechanicsOwnership.Resolve(p,car,p.catalog).spec.fluids.oilCapacityLitres;
                        if(car.fluids.oilLitres<=capacity)throw new InvalidOperationException("There is no excess oil to drain.");
                        car.fluids.oilLitres=capacity;
                    }
                }))return false;
                if(carId==profile.selectedOwnedVehicleId)ShowGarageCar();notice="Maintenance saved.";return true;
            }catch(Exception error){notice=error.Message;return false;}
        }
        void QueuePayment(PaymentRequest request, int? shownCost=null)
        {
            if(BlockForManualRefill())return;
            if(request!=null&&request.kind==PaymentKind.Fluids)
            {
                if(!TryQuoteManualRefill(request.carId,request.litres,request.coolantLitres,out var refill,out var error))
                {notice=error;return;}
                if(shownCost.HasValue&&refill.Cost!=shownCost.Value){notice="The refill terms changed. Review the amount and price again.";return;}
                pendingAction=()=>BeginManualRefill(refill);
                return;
            }
            try { var quote = PaidEconomy.Quote(profile, profile.catalog, Costs, PricingPolicy, request, DragLab.Domain.RuntimeEntropy.UtcNow.Ticks); pendingAction = () => PayNow(quote); }
            catch (Exception e) { notice = e.Message; }
        }
        void PaidButton(string id, string label, PaymentRequest request, bool actionable = true)
        {
            int cost = request!=null&&request.kind==PaymentKind.Fluids&&!actionable ? -1 : PaymentCost(request); bool previous = GUI.enabled; GUI.enabled = previous && !ManualRefillBlocksMutation && actionable && cost >= 0 && profile.cash >= cost;
            if (PartButton(id, label + (cost < 0 ? " / unavailable" : " / " + DisplayUnits.Money(cost)))) QueuePayment(request,cost);
            GUI.enabled = previous;
            if (cost > profile.cash) GUILayout.Label("Need " + DisplayUnits.Money((cost - profile.cash)) + " more. Recovery work / idle claims / sell retained spares are available.");
        }
        internal void OpenRecovery() { DiscardPreviews(); screen = "Recovery"; scroll = Vector2.zero; partRects.Clear(); nativeMenu?.ResetMenuEntry("Recovery"); }
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
            RecoveryAdviceGUI();
            long now = DragLab.Domain.RuntimeEntropy.UtcNow.Ticks; var job = profile.recoveryWork;
            if (job == null) {
                GUILayout.Label("Garage job: " + Costs.recoverySeconds + " seconds / up to " + DisplayUnits.Money(Costs.recoveryReward) + " / available below " + DisplayUnits.Money(Costs.recoveryCashLimit) + " cash. Continues offline.");
                GUI.enabled = profile.cash < Costs.recoveryCashLimit;
                if (PartButton("recovery-start", "Start recovery work")) QueuePartAction(p => PaidEconomy.StartRecovery(p, Costs, DragLab.Domain.RuntimeEntropy.UtcNow.Ticks)); GUI.enabled = true;
            } else {
                GUILayout.Label("Job " + job.id + " / " + DisplayUnits.Money(job.reward) + " / " + (now >= job.readyUtcTicks ? "Ready" : Math.Ceiling((job.readyUtcTicks - now) / (double)TimeSpan.TicksPerSecond) + " seconds remaining"));
                GUI.enabled = now >= job.readyUtcTicks;
                if (PartButton("recovery-claim", "Claim finished job once")) { string id = job.id; QueuePartAction(p => PaidEconomy.ClaimRecovery(p, id, DragLab.Domain.RuntimeEntropy.UtcNow.Ticks)); } GUI.enabled = true;
            }
            int idle = GarageBusiness.Pending(profile,BusinessPolicy,now);
            GUI.enabled = idle > 0;
            if (PartButton("recovery-idle", "Claim existing idle earnings / " + DisplayUnits.Money(idle))) QueuePartAction(p => GarageBusiness.Claim(p,BusinessPolicy,DragLab.Domain.RuntimeEntropy.UtcNow.Ticks)); GUI.enabled = true;
            if(PartButton("recovery-business","Business / reserved jobs / expansion"))pendingAction=OpenBusiness;
            if (PartButton("recovery-collection", "Dealership / owned cars")) pendingAction = OpenCollection;
            if (PartButton("recovery-assets", "Value / sell retained assets")) pendingAction = () => OpenValuation(ValuationKind.Car, Build?.instanceId ?? "");
            if (Build != null && PartButton("recovery-repairs", "Repair / replace selected car parts")) pendingAction = ShowRepairs;
            if (PartButton("recovery-garage", "Garage")) pendingAction = ReturnToGarage;
            GUILayout.Label("Recent committed operations (last 256 retained)");
            foreach (var r in profile.economyReceipts.AsEnumerable().Reverse().Take(20)) GUILayout.Label(r.operation + " / " + DisplayUnits.Money((long)r.cashAfter - r.cashBefore, signed: true) + " / cash " + DisplayUnits.Money(r.cashAfter) + " / " + r.id.Substring(0, Math.Min(14, r.id.Length)));
        }
        string saleConfirmation;
        void QuoteSaleGUI(ValuationQuote quote)
        {
            bool previous = GUI.enabled; GUI.enabled = previous && valuationStatus == null && quote != null;
            string label = quote.Kind == ValuationKind.Chassis ? "Salvage chassis / keep engine tree" : "Sell captured " + quote.Kind;
            if (PartButton("quote-sell-preview", label + " / " + DisplayUnits.Money(quote.Total))) saleConfirmation = quote.Id;
            if (saleConfirmation == quote.Id) {
                GUILayout.Label("CONFIRM: dispose of " + quote.IncludedIds.Count + " captured identities for " + DisplayUnits.Money(quote.Total) + ". " + quote.RetainedIds.Count + " selected kept packs go to inventory. " + (quote.Kind == ValuationKind.Chassis ? "Attached engine and fluids remain stored." : "Use Chassis / keep engine to retain the full engine."));
                if (PartButton("quote-sell-confirm", "Confirm disposal / save cash and ownership together")) {
                    long revision = quote.Revision; pendingAction = () => { if (CommitParts(revision, p => PaidEconomy.Sell(p, p.catalog, PricingPolicy, quote, DragLab.Domain.RuntimeEntropy.UtcNow.Ticks))) { saleConfirmation = null; valuationStatus = "Disposed once; request a quote for another asset."; } };
                }
                if (PartButton("quote-sell-cancel", "Cancel / keep all assets")) saleConfirmation = null;
            }
            GUI.enabled = previous;
        }
    }
}

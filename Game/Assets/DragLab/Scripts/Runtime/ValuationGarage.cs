using System;
using DragLab.Domain;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain.Mechanics;
namespace DragLab.Runtime
{
    public sealed partial class PrototypeGame
    {
        internal ValuationQuote valuationQuote;
        internal ValuationKind valuationKind = ValuationKind.Car;
        internal string valuationAsset, valuationBuyer = "enthusiast", valuationStatus;
        internal readonly HashSet<string> valuationKept = new HashSet<string>();
        ValuationPolicyDefinition valuationPolicyAsset;
        bool valuationDetails, valuationRetainDetails;
        float valuationCheckAt;
        internal ValuationPolicy PricingPolicy
        {
            get { if (valuationPolicyAsset == null) valuationPolicyAsset = Resources.Load<ValuationPolicyDefinition>("Data/Economy/ValuationPolicy"); return valuationPolicyAsset == null ? null : valuationPolicyAsset.policy; }
        }
        internal void OpenValuation(ValuationKind kind, string id)
        {
            DiscardPreviews(); screen = "Valuation"; scroll = Vector2.zero; partRects.Clear(); valuationKind = kind; valuationAsset = id; valuationKept.Clear(); RefreshValuation();
        }
        internal void RefreshValuation()
        {
            try { valuationQuote = InventoryPricing.Sell(profile, valuationKind, valuationAsset, PricingPolicy, valuationBuyer, DateTime.UtcNow.Ticks, valuationKept); CheckValuation(); }
            catch (Exception e) { valuationQuote = null; valuationStatus = e.Message; }
        }
        internal void CheckValuation() { valuationStatus = Valuations.Validate(valuationQuote, profile, profile.catalog, PricingPolicy, DateTime.UtcNow.Ticks); valuationCheckAt = Time.realtimeSinceStartup + 1; }
        void ValuationGUI()
        {
            bool previous = GUI.skin.label.wordWrap;
            GUI.skin.label.wordWrap = true;
            try { DrawValuation(); } finally { GUI.skin.label.wordWrap = previous; }
        }
        void DrawValuation()
        {
            GUILayout.Label("VALUATION / captured owned assets / priced sale & salvage", stat);
            GUILayout.Label("Compare offers first. Confirm disposal explicitly; cash and included ownership save together. Kept components remain owned.");
            var policy = PricingPolicy;
            if (policy == null) { GUILayout.Label("Missing valuation policy. Run DragLab / Prepare Valuation Policy in Unity."); return; }
            GUILayout.BeginHorizontal();
            foreach (var buyer in policy.buyers) { string id = buyer.id; if (PartButton("quote-buyer:" + id, (valuationBuyer == id ? "✓ " : "") + buyer.name)) pendingAction = () => { valuationBuyer = id; RefreshValuation(); }; }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUI.enabled = Build != null;
            if (PartButton("quote-car", "Selected car")) pendingAction = () => OpenValuation(ValuationKind.Car, Build.instanceId);
            if (PartButton("quote-chassis", "Chassis / keep engine")) pendingAction = () => OpenValuation(ValuationKind.Chassis, Build.chassisId);
            GUI.enabled = Build != null && !string.IsNullOrEmpty(Build.engineAssemblyId);
            if (PartButton("quote-engine", "Attached engine")) pendingAction = () => OpenValuation(ValuationKind.Engine, Build.engineAssemblyId);
            GUI.enabled = true; GUILayout.EndHorizontal();
            if (PartButton("quote-refresh", "Refresh quote / capture current assets")) pendingAction = RefreshValuation;
            if (valuationQuote != null) {
                if (Time.realtimeSinceStartup >= valuationCheckAt) CheckValuation();
                var q = valuationQuote;
                GUILayout.Label(q.Kind + " / " + q.BuyerName + " / " + DisplayUnits.Money(q.Total), stat);
                var quotedCar = profile.cars.Find(c => c.instanceId == q.AssetId || c.chassisId == q.AssetId);
                var quotedEngine = profile.engines.Find(e => e.instanceId == q.AssetId);
                var quotedPart = profile.parts.Find(i => i.instanceId == q.AssetId);
                string name = quotedCar != null ? VehicleCollection.Label(profile.catalog, quotedCar) : quotedEngine != null ? profile.catalog.Spec(quotedEngine.vehicleDefinitionId).displayName + " engine" : quotedPart != null ? profile.catalog.Find(quotedPart.definitionId).name : q.Kind.ToString();
                GUILayout.Label("Captured asset: " + name + " / " + q.AssetId);
                GUILayout.Label("Comparable loose parts " + DisplayUnits.Money(q.LoosePartTotal, 2) + " + bare chassis " + DisplayUnits.Money(q.ChassisCore, 2) + " → offer " + DisplayUnits.Money(q.Total));
                GUILayout.Label((q.Complete ? "Required hardware complete" : "Incomplete / non-running") + (q.Faulty ? " / fault risk" : "") + " / " + q.IncludedIds.Count + " actual identities captured / " + q.RetainedIds.Count + " kept parts");
                GUILayout.Label("Policy " + q.PolicyId + " v" + q.PolicyVersion + " / quote " + q.Id.Substring(0, 8) + " / expires " + new DateTime(q.ExpiresUtcTicks, DateTimeKind.Utc).ToLocalTime().ToString("HH:mm:ss"));
                GUILayout.Label(valuationStatus == null ? "CURRENT / valid inspection quote" : "STALE / BLOCKED: " + valuationStatus);
                if (PartButton("quote-validate", "Validate captured quote")) pendingAction = CheckValuation;
                QuoteSaleGUI(q);
                foreach (var warning in q.Warnings) GUILayout.Label("NOTE: " + warning);
                if (PartButton("quote-details", (valuationDetails ? "Hide" : "Show") + " item values and every price adjustment")) valuationDetails = !valuationDetails;
                if (valuationDetails) foreach (var line in q.Lines) {
                    GUILayout.Label(line.Label + " / " + DisplayUnits.Money(line.Before, 2) + " → " + DisplayUnits.Money(line.After, 2) + " (" + DisplayUnits.Money(line.Delta, 2, signed: true) + ")");
                    GUILayout.Label(line.Explanation);
                }
                if (PartButton("quote-retain-details", (valuationRetainDetails ? "Hide" : "Choose") + " individual fitted parts to keep / excluded from quote")) valuationRetainDetails = !valuationRetainDetails;
                if (valuationRetainDetails && (q.Kind == ValuationKind.Car || q.Kind == ValuationKind.Chassis || q.Kind == ValuationKind.Engine)) {
                    foreach (var item in profile.parts.Where(i => q.IncludedIds.Contains(i.instanceId) || valuationKept.Contains(i.instanceId)).ToArray()) {
                        string id = item.instanceId; bool kept = valuationKept.Contains(id);
                        if (PartButton("quote-keep:" + id, (kept ? "KEEP / " : "IN OFFER / ") + profile.catalog.Find(item.definitionId).name)) pendingAction = () => { if (kept) valuationKept.Remove(id); else valuationKept.Add(id); RefreshValuation(); };
                    }
                }
            } else GUILayout.Label("Quote unavailable: " + valuationStatus);
            GUILayout.Space(8); GUILayout.Label("OTHER OWNED ASSETS");
            foreach (var car in profile.cars.ToArray()) { string id = car.instanceId; if (PartButton("quote-owned:" + id, "Car / " + VehicleCollection.Label(profile.catalog, car))) pendingAction = () => OpenValuation(ValuationKind.Car, id); }
            foreach (var engine in profile.engines.Where(e => string.IsNullOrEmpty(e.parentVehicleId)).ToArray()) { string id = engine.instanceId; if (PartButton("quote-stored:" + id, "Stored engine / " + profile.catalog.Spec(engine.vehicleDefinitionId).displayName + " / " + id.Substring(id.Length - 6))) pendingAction = () => OpenValuation(ValuationKind.Engine, id); }
            foreach (var item in profile.parts.Where(i => i.location == PartLocation.Inventory).ToArray()) { string id = item.instanceId; if (PartButton("quote-loose:" + id, "Loose / " + profile.catalog.Find(item.definitionId).name + " / " + Condition(item))) pendingAction = () => OpenValuation(ValuationKind.LoosePart, id); }
            foreach (var element in profile.looseEngineElements.ToArray()) { string id = element.element.id; if (PartButton("quote-element:" + id, "Single " + element.element.role + " / " + element.element.health.ToString("P0"))) pendingAction = () => OpenValuation(ValuationKind.LooseElement, id); }
            GUI.enabled = Build != null;
            if (PartButton("quote-assemblies", "Assemblies / detach / transfer")) pendingAction = OpenAssemblies;
            GUI.enabled = true;
            if (PartButton("quote-recovery", "Recovery work / buy replacement")) pendingAction = OpenRecovery;
            if (PartButton("quote-garage", "Garage")) pendingAction = ReturnToGarage;
        }
    }
}

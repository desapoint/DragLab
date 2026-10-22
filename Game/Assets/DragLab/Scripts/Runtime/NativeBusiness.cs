using System;
using System.Collections.Generic;
using System.Linq;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using UnityEngine;
using UnityEngine.UIElements;

namespace DragLab.Runtime
{
    internal sealed partial class NativeMenuShell
    {
        VisualElement businessPage, businessContent;
        ScrollView businessScroll;
        string businessTab = "Overview", businessCarId, businessKey;
        float businessNextTick;
        readonly List<RacingSurface> businessSurfaces = new List<RacingSurface>();
        readonly List<Action<long>> businessTimers = new List<Action<long>>();

        void InitializeBusiness()
        {
            businessPage = new VisualElement { name = "businessPage" };
            businessPage.AddToClassList("car-details-page");
            businessPage.AddToClassList("hidden");
            root.Q(className: "workspace").Add(businessPage);
            var tabs = new VisualElement(); tabs.AddToClassList("choice-group");
            tabs.style.flexDirection = FlexDirection.Row; tabs.style.height = 32;
            tabs.style.flexShrink = 0; tabs.style.marginBottom = 12; businessPage.Add(tabs);
            PartChoices(tabs, "businessTab", new[] { "Overview", "Display jobs", "Recovery" }, () => businessTab,
                value => { businessTab = value; businessKey = null; businessScroll.scrollOffset = Vector2.zero; RefreshBusiness(game.ScreenName); });
            var frame = new VisualElement(); frame.AddToClassList("car-details-frame");
            businessPage.Add(frame); AddSurface(frame, RacingSurface.Kind.Panel);
            businessScroll = new ScrollView { name = "businessScroll", horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            businessScroll.AddToClassList("car-details-scroll"); frame.Add(businessScroll);
            businessContent = new VisualElement { name = "businessContent" };
            businessContent.AddToClassList("car-details-content"); businessScroll.Add(businessContent);
        }

        void ResetBusiness(string route)
        {
            businessTab = route == "Recovery" ? "Recovery" : "Overview";
            businessCarId = null; businessKey = null; businessNextTick = 0;
            businessScroll.scrollOffset = Vector2.zero;
            businessPage.Query<Button>(className: "choice-button").ForEach(button =>
                button.EnableInClassList("selected", button.name == "businessTab:" + businessTab));
        }

        void RefreshBusiness(string route)
        {
            bool show = route == "Business" || route == "Recovery";
            businessPage.EnableInClassList("hidden", !show); if (!show) return;
            if (Time.unscaledTime < businessNextTick && businessKey != null) return;
            businessNextTick = Time.unscaledTime + .5f;
            var p = game.Profile;
            if (!p.cars.Any(car => car.instanceId == businessCarId)) businessCarId = p.SelectedCar?.instanceId ?? p.cars.FirstOrDefault()?.instanceId;
            string key = p.revision + "/" + businessTab + "/" + businessCarId + "/" + JsonUtility.ToJson(game.BusinessPolicy) + "/" + JsonUtility.ToJson(game.Costs);
            if (businessKey != key)
            {
                businessKey = key;
                foreach (var surface in businessSurfaces) surfaces.Remove(surface);
                businessSurfaces.Clear(); businessTimers.Clear(); businessContent.Clear();
                if (businessTab == "Overview") BuildBusinessOverview();
                else if (businessTab == "Display jobs") BuildBusinessJobs();
                else BuildBusinessRecovery();
                ExcludeTabTraversal();
            }
            long now = RuntimeEntropy.UtcNow.Ticks;
            foreach (var timer in businessTimers) timer(now);
        }

        Label BusinessText(VisualElement parent, string text, string id = null, string style = null)
        {
            var label = new Label(text) { name = id, enableRichText = false };
            label.AddToClassList("business-text"); if (style != null) label.AddToClassList(style);
            parent.Add(label); return label;
        }
        VisualElement BusinessCard(VisualElement parent, string title)
        {
            var card = new VisualElement(); card.AddToClassList("owned-car-card"); card.AddToClassList("business-card");
            parent.Add(card); AddSurface(card, RacingSurface.Kind.Panel); businessSurfaces.Add(card.Q<RacingSurface>());
            BusinessText(card, title, style: "business-title"); return card;
        }
        Button BusinessButton(VisualElement parent, string id, string text, Action action, bool primary = false)
        {
            var button = new Button(action) { name = id, text = text, focusable = false, tabIndex = -1 };
            button.AddToClassList("action-button"); button.AddToClassList("business-button");
            if (primary) button.AddToClassList("primary-action"); parent.Add(button);
            AddSurface(button, primary ? RacingSurface.Kind.Action : RacingSurface.Kind.SecondaryAction);
            businessSurfaces.Add(button.Q<RacingSurface>()); return button;
        }
        VisualElement BusinessGrid() { var grid = new VisualElement(); grid.AddToClassList("owned-cars-grid"); businessContent.Add(grid); return grid; }
        static string BusinessDuration(long ticks)
        {
            long seconds = Math.Max(0, (long)Math.Ceiling(ticks / (double)TimeSpan.TicksPerSecond));
            return seconds >= 3600 ? (seconds / 3600) + "h " + ((seconds % 3600) / 60) + "m" : seconds >= 60 ? (seconds / 60) + "m " + (seconds % 60) + "s" : seconds + "s";
        }
        string BusinessClockError(long now)
        {
            var p = game.Profile;
            if (now < Math.Max(p.lastEconomyUtcTicks, p.business.accruedThroughUtcTicks)) return "Saved clock is ahead. Earnings and claims resume when real time catches up.";
            return p.runCheckpoints.Any(run => !run.settled) ? "Close the active run before changing business state." : null;
        }
        void BuildBusinessOverview()
        {
            var p = game.Profile; var policy = game.BusinessPolicy; var state = p.business;
            var grid = BusinessGrid(); var income = BusinessCard(grid, "Passive income");
            BusinessText(income, DisplayUnits.Money(state.ratePerHour) + " / hour · " + state.capHours + "h cap", "businessRate");
            var pending = BusinessText(income, "", "businessPending", "business-amount");
            BusinessText(income, "Earnings continue offline up to " + DisplayUnits.Money((long)state.ratePerHour * state.capHours) + ". Claim to make room for more.");
            if (state.legacyEightHourCap || state.ratePerHour != policy.hourlyRates[p.garageLevel - 1] || state.capHours != policy.capsHours[p.garageLevel - 1])
                BusinessText(income, "After your next claim: " + DisplayUnits.Money(policy.hourlyRates[p.garageLevel - 1]) + " / hour · " + policy.capsHours[p.garageLevel - 1] + "h cap.");
            var claim = BusinessButton(income, "businessClaim", "Claim income", () => ReviewBusiness("Claim income", "Claim " + DisplayUnits.Money(GarageBusiness.Pending(p, policy, RuntimeEntropy.UtcNow.Ticks)) + ". Income continues to accrue until confirmation.", "Income", "", ""), true);
            var upgrade = BusinessCard(grid, "Garage · level " + p.garageLevel + " / 4");
            BusinessText(upgrade, policy.jobSlots[p.garageLevel - 1] + " display job bays · " + state.jobs.Count + " occupied");
            BusinessText(upgrade, "Expansion improves income, its offline cap and display job capacity. Car storage is unrestricted.");
            Button expand = null;
            if (p.garageLevel < 4)
            {
                int level = p.garageLevel, cost = policy.upgradeCosts[level - 1];
                BusinessText(upgrade, "Next: " + DisplayUnits.Money(policy.hourlyRates[level]) + " / hour · " + policy.capsHours[level] + "h cap · " + policy.jobSlots[level] + " bays");
                expand = BusinessButton(upgrade, "businessExpand", "Expand · " + DisplayUnits.Money(cost), () => ReviewBusiness("Expand garage", "Level " + (level + 1) + " costs " + DisplayUnits.Money(cost) + ". Existing income is claimed first, then the expansion is paid. New income terms apply from confirmation.", "Upgrade", "", ""), true);
            }
            else BusinessText(upgrade, "Fully expanded", style: "car-ready");
            var warning = BusinessText(businessContent, "", "businessClock", "car-warning");
            businessTimers.Add(now => {
                int amount = GarageBusiness.Pending(p, policy, now); pending.text = DisplayUnits.Money(amount) + " ready to claim";
                string error = BusinessClockError(now); warning.text = error ?? "";
                claim.SetEnabled(amount > 0 && error == null);
                if (expand != null) expand.SetEnabled(error == null && (long)p.cash + amount >= policy.upgradeCosts[p.garageLevel - 1]);
            });
        }

        void BuildBusinessJobs()
        {
            var p = game.Profile; var policy = game.BusinessPolicy;
            BusinessButton(businessContent, "businessChooseCar", "Job car · " + BusinessCarName(businessCarId), OpenBusinessCarPicker);
            BusinessText(businessContent, "Display jobs reserve this exact car, engine and fitted parts until claimed or cancelled, including after the timer ends. They use no fuel and cause no wear.");
            var warning = BusinessText(businessContent, "", "businessClock", "car-warning");
            businessTimers.Add(now => warning.text = BusinessClockError(now) ?? "");
            BusinessText(businessContent, "Active jobs · " + p.business.jobs.Count + " / " + policy.jobSlots[p.garageLevel - 1] + " bays", style: "business-title");
            var active = BusinessGrid();
            if (p.business.jobs.Count == 0) BusinessText(businessContent, "No active display jobs.");
            foreach (var job in p.business.jobs)
            {
                string id = job.id; var card = BusinessCard(active, job.name);
                BusinessText(card, BusinessCarName(job.carId));
                BusinessText(card, "Captured reward · " + DisplayUnits.Money(job.reward));
                var timer = BusinessText(card, "", "businessJobTime:" + id);
                var actions = new VisualElement(); actions.AddToClassList("business-actions"); card.Add(actions);
                var claim = BusinessButton(actions, "businessJobClaim:" + id, "Claim", () => ReviewBusiness("Claim display job", job.name + " · " + BusinessCarName(job.carId) + "\nReceive " + DisplayUnits.Money(job.reward) + " and release this car.", "Finish", id, ""), true);
                BusinessButton(actions, "businessJobCancel:" + id, "Cancel job", () => ReviewBusiness("Cancel display job", "Release " + BusinessCarName(job.carId) + " immediately. The " + DisplayUnits.Money(job.reward) + " reward is forfeited.", "Cancel", id, ""));
                businessTimers.Add(now => { bool ready = now >= job.readyUtcTicks; timer.text = ready ? "Ready · car remains reserved until claimed" : BusinessDuration(job.readyUtcTicks - now) + " remaining"; timer.EnableInClassList("car-ready", ready); claim.SetEnabled(ready && BusinessClockError(now) == null); });
            }
            BusinessText(businessContent, "Available contracts", style: "business-title"); var offers = BusinessGrid();
            var car = p.cars.Find(c => c.instanceId == businessCarId);
            foreach (var offer in policy.offers)
            {
                string offerId = offer.id, carId = businessCarId; var card = BusinessCard(offers, offer.name);
                BusinessText(card, BusinessDuration(TimeSpan.TicksPerSecond * offer.seconds) + " · " + DisplayUnits.Money(offer.reward));
                BusinessText(card, "Garage " + offer.levelRequired + " · REP " + offer.reputationRequired + (offer.minimumHp > 0 ? " · " + offer.minimumHp + " HP minimum" : "") + (offer.restrictDrive ? " · " + offer.drive : ""));
                string reason = p.business.jobs.Count >= policy.jobSlots[p.garageLevel - 1] ? "All display bays are occupied." : GarageBusiness.Eligibility(p, car, offer);
                var eligibility = BusinessText(card, reason ?? "Available", "businessOfferState:" + offerId, reason == null ? "car-ready" : "car-warning");
                var assign = BusinessButton(card, "businessAssign:" + offerId, "Assign car", () => ReviewBusiness("Assign display job", BusinessCarName(carId) + "\n" + offer.name + " · " + BusinessDuration(TimeSpan.TicksPerSecond * offer.seconds) + " · " + DisplayUnits.Money(offer.reward) + "\nThis exact build becomes unavailable for driving, tuning, maintenance and sale until released.", "Assign", offerId, carId), true);
                businessTimers.Add(now => assign.SetEnabled(reason == null && BusinessClockError(now) == null));
            }
        }
        string BusinessCarName(string id)
        {
            var car = game.Profile.cars.Find(c => c.instanceId == id);
            return car == null ? "Choose an owned car" : VehicleCollection.DisplayName(game.Profile.catalog, car);
        }
        void OpenBusinessCarPicker()
        {
            OpenDialog("Choose job car", null, new[] { new DialogAction("Close", CloseDialog) }, populate: content => {
                foreach (var car in game.Profile.cars)
                {
                    string id = car.instanceId;
                    var button = new Button(() => { businessCarId = id; businessKey = null; CloseDialog(); Refresh(); }) { name = "businessCar:" + id, text = BusinessCarName(id) + (string.IsNullOrEmpty(car.garageJobId) ? "" : " · Reserved"), tooltip = id };
                    button.AddToClassList("action-button"); button.EnableInClassList("selected", id == businessCarId); content.Add(button);
                    AddSurface(button, RacingSurface.Kind.SecondaryAction); dialogSurfaces.Add(button.Q<RacingSurface>());
                }
                if (game.Profile.cars.Count == 0) content.Add(new Label("No owned cars. Recovery work does not require a car."));
            });
        }

        void BuildBusinessRecovery()
        {
            var p = game.Profile; var costs = game.Costs; var grid = BusinessGrid();
            var work = BusinessCard(grid, "Recovery work");
            BusinessText(work, "Earn a bounded payment without a car. Retained parts and engines remain yours; work does not repair or create components.");
            if (costs == null) BusinessText(work, "Operating policy unavailable.", style: "car-critical");
            else if (p.recoveryWork == null)
            {
                BusinessText(work, BusinessDuration(TimeSpan.TicksPerSecond * costs.recoverySeconds) + " · up to " + DisplayUnits.Money(costs.recoveryReward) + " · continues offline");
                BusinessText(work, "Available below " + DisplayUnits.Money(costs.recoveryCashLimit) + " cash. Reward is limited to the gap below that balance.");
                var start = BusinessButton(work, "businessRecoveryStart", "Start work", () => ReviewBusiness("Start recovery work", "Duration " + BusinessDuration(TimeSpan.TicksPerSecond * costs.recoverySeconds) + " · reward " + DisplayUnits.Money(Math.Min(costs.recoveryReward, costs.recoveryCashLimit - p.cash)) + ". No car is required.", "RecoveryStart", "", ""), true);
                businessTimers.Add(now => start.SetEnabled(p.cash < costs.recoveryCashLimit && BusinessClockError(now) == null));
            }
            else
            {
                var job = p.recoveryWork; BusinessText(work, "Captured reward · " + DisplayUnits.Money(job.reward));
                var timer = BusinessText(work, "", "businessRecoveryTime");
                var claim = BusinessButton(work, "businessRecoveryClaim", "Claim work", () => ReviewBusiness("Claim recovery work", "Receive " + DisplayUnits.Money(job.reward) + ". This completed job can only be claimed once.", "RecoveryClaim", job.id, ""), true);
                businessTimers.Add(now => { timer.text = now >= job.readyUtcTicks ? "Ready to claim" : BusinessDuration(job.readyUtcTicks - now) + " remaining"; claim.SetEnabled(now >= job.readyUtcTicks && BusinessClockError(now) == null); });
            }
            var next = BusinessCard(grid, "Get back on track");
            BusinessText(next, "Claim passive income in Overview, inspect and service owned cars in Garage, or sell spare parts and engines from Build.");
            BusinessButton(next, "businessToGarage", "Owned cars / dealership", () => Navigate("Garage"));
            BusinessButton(next, "businessToBuild", "Parts / stored engines", () => Navigate("Parts"));
            BusinessText(businessContent, "Recent operations", style: "business-title");
            foreach (var receipt in p.economyReceipts.AsEnumerable().Reverse().Take(20))
            {
                var row = new VisualElement(); row.AddToClassList("business-receipt"); businessContent.Add(row);
                BusinessText(row, receipt.operation);
                BusinessText(row, DisplayUnits.Money((long)receipt.cashAfter - receipt.cashBefore, signed: true) + " · balance " + DisplayUnits.Money(receipt.cashAfter));
            }
            if (p.economyReceipts.Count == 0) BusinessText(businessContent, "No completed operations yet.");
            var warning = BusinessText(businessContent, "", "businessClock", "car-warning");
            businessTimers.Add(now => warning.text = BusinessClockError(now) ?? "");
        }

        void ReviewBusiness(string title, string message, string operation, string target, string carId)
        {
            long revision = game.Profile.revision;
            string policy = JsonUtility.ToJson(game.BusinessPolicy), costs = JsonUtility.ToJson(game.Costs);
            var error = new Label { name = "businessCommitError" }; error.AddToClassList("car-critical");
            OpenDialog(title, message, new[] { new DialogAction("Cancel", CloseDialog), new DialogAction("Confirm", () => {
                if (!game.CommitNativeBusiness(revision, policy, costs, operation, target, carId)) { error.text = game.NativeNotice; return; }
                CloseDialog(); businessKey = null; Refresh();
            }, true) }, populate: content => content.Add(error));
        }
    }

    public sealed partial class PrototypeGame
    {
        internal bool CommitNativeBusiness(long revision, string policy, string costs, string operation, string target, string carId)
        {
            try
            {
                if ((screen != "Business" && screen != "Recovery") || pendingAction != null) throw new InvalidOperationException("Business page changed. Reopen the review.");
                if (policy != JsonUtility.ToJson(BusinessPolicy) || costs != JsonUtility.ToJson(Costs)) throw new InvalidOperationException("Business terms changed. Reopen the review.");
                bool ok = CommitParts(revision, p => {
                    long now = RuntimeEntropy.UtcNow.Ticks;
                    switch (operation)
                    {
                        case "Income": GarageBusiness.Claim(p, BusinessPolicy, now); break;
                        case "Upgrade": GarageBusiness.Upgrade(p, BusinessPolicy, now); break;
                        case "Assign": GarageBusiness.Assign(p, BusinessPolicy, carId, target, now); break;
                        case "Finish": GarageBusiness.Finish(p, target, now); break;
                        case "Cancel": GarageBusiness.Finish(p, target, now, true); break;
                        case "RecoveryStart": PaidEconomy.StartRecovery(p, Costs, now); break;
                        case "RecoveryClaim": PaidEconomy.ClaimRecovery(p, target, now); break;
                        default: throw new InvalidOperationException("Unknown business operation.");
                    }
                });
                if (ok) notice = "Business operation saved.";
                return ok;
            }
            catch (Exception exception) { notice = exception.Message; return false; }
        }
    }
}

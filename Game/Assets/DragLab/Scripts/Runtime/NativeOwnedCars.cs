using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Presentation;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    internal sealed partial class NativeMenuShell
    {
        ScrollView ownedScroll;
        VisualElement ownedGrid;
        PlayerProfile ownedProfile;
        long ownedRevision = -1;
        string ownedSelection;
        TorqueUnit ownedTorque;
        CancellationTokenSource ownedCancellation;

        sealed class OwnedRatingResult
        {
            internal string Context;
            internal BenchmarkResult Rating;
            internal CarInspection Driving;
        }
        readonly Dictionary<string,OwnedRatingResult> ownedRatingCache=new Dictionary<string,OwnedRatingResult>();
        readonly LinkedList<string> ownedRatingRecency=new LinkedList<string>();
        readonly Dictionary<string,LinkedListNode<string>> ownedRatingNodes=new Dictionary<string,LinkedListNode<string>>();
        Task<Dictionary<string,OwnedRatingResult>> ownedRatings;
        readonly Dictionary<string,string> ownedPendingContexts=new Dictionary<string,string>();
        string ownedDetailId;
        Vector2 ownedReturnScroll;
        long ownedDetailRevision;
        VisualElement ownedDetails, ownedDetailContent;
        Button ownedUse;
        internal bool OwnedDetailsOpen => !dealerMode && ownedDetailId != null && game.ScreenName == "Garage" && Visible;
        readonly List<RacingSurface> ownedSurfaces = new List<RacingSurface>();
        internal int OwnedGridBuildCount { get; private set; }
        internal void InvalidateOwnedGrid() => ownedRevision = -1;

        void InitializeOwnedCars()
        {
            ownedScroll = new ScrollView { name = "ownedCars", horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            ownedScroll.AddToClassList("owned-cars-scroll"); ownedScroll.AddToClassList("hidden");
            ownedGrid = new VisualElement { name = "ownedCarGrid" }; ownedGrid.AddToClassList("owned-cars-grid");
            ownedScroll.Add(ownedGrid); root.Q(className:"workspace").Add(ownedScroll);
            InitializeOwnedCarFilters();
            ownedDetails = new VisualElement { name = "ownedDetails" }; ownedDetails.AddToClassList("car-details-page"); ownedDetails.AddToClassList("hidden");
            root.Q(className:"workspace").Add(ownedDetails);
            var heading = new VisualElement(); heading.AddToClassList("car-details-heading"); ownedDetails.Add(heading);
            var back = new Button(BackToOwnedCars) { name = "ownedBack", tooltip = "Back to owned cars" }; back.AddToClassList("action-button"); back.AddToClassList("car-back-button");
            back.Add(new FilledMenuIcon("Back")); heading.Add(back); AddSurface(back,RacingSurface.Kind.SecondaryAction);
            var title = new Label { name = "ownedDetailTitle", enableRichText = false }; title.AddToClassList("car-detail-title"); heading.Add(title);
            var rename = new Button(OpenOwnedCarRename) { name = "renameOwnedCar", tooltip = "Rename car" }; rename.AddToClassList("action-button"); rename.AddToClassList("car-rename-button");
            rename.Add(new FilledMenuIcon("Pencil")); heading.Add(rename); AddSurface(rename,RacingSurface.Kind.SecondaryAction);
            var frame = new VisualElement(); frame.AddToClassList("car-details-frame"); ownedDetails.Add(frame); AddSurface(frame,RacingSurface.Kind.Panel);
            var detailScroll = new ScrollView { name = "ownedDetailScroll", horizontalScrollerVisibility = ScrollerVisibility.Hidden }; detailScroll.AddToClassList("car-details-scroll"); frame.Add(detailScroll);
            ownedDetailContent = new VisualElement { name = "ownedCarDetails" }; ownedDetailContent.AddToClassList("car-details-content"); detailScroll.Add(ownedDetailContent);
            var actions = new VisualElement(); actions.AddToClassList("car-details-actions"); ownedDetails.Add(actions);
            var error = new Label { name = "carSelectionError" }; error.AddToClassList("car-critical"); error.AddToClassList("car-selection-error"); actions.Add(error);
            ownedUse = new Button(() => {
                if (!game.SelectNativeCar(ownedDetailId,ownedDetailRevision)) { error.text = game.NativeNotice; return; }
                BackToOwnedCars();
            }) { name = "ownedUseCar", text = "Use this car" };
            ownedUse.AddToClassList("action-button"); ownedUse.AddToClassList("primary-action"); actions.Add(ownedUse); AddSurface(ownedUse,RacingSurface.Kind.Action);
            ownedCompare=CarCompareButton(actions,"ownedCompareCar",ToggleOwnedComparison);
        }
        void RefreshOwnedCars(string route)
        {
            ownedToolbar.EnableInClassList("hidden",route!="Garage"||(dealerMode?dealerDetailId!=null:ownedDetailId!=null));
            ownedScroll.EnableInClassList("hidden", route != "Garage" || dealerMode || ownedDetailId != null);
            ownedDetails.EnableInClassList("hidden", route != "Garage" || dealerMode || ownedDetailId == null || disposalCarId != null);
            if (route != "Garage") return;
            var profile = game.Profile;
            TorqueUnit units = SharedUnitSettings.Current.torque;
            if (!ReferenceEquals(ownedProfile,profile) || ownedRevision != profile.revision || ownedSelection != profile.selectedOwnedVehicleId || ownedTorque != units)
                BuildOwnedCars(profile,units);
            if (ownedRatings == null || !ownedRatings.IsCompleted) return;
            var completed = ownedRatings; ownedRatings = null;ownedPendingContexts.Clear();
            try {
                foreach (var rating in completed.Result) {
                    var label = ownedGrid.Q<Label>("carClass:"+rating.Key);
                    var value=rating.Value?.Rating;
                    string text=value==null?"Class unavailable · PP —":"Class "+value.performanceClass+" · PP "+value.points;
                    if (label != null) label.text = text;
                    if(rating.Value!=null)RememberOwnedRating(rating.Key,rating.Value);
                    if(ownedEntries.TryGetValue(rating.Key,out var entry)){entry.Rating=value;entry.RatingPending=false;}
                    if (ownedDetailId == rating.Key) {
                        var detail = ownedDetailContent.Q<Label>(className:"car-class");
                        if (detail != null) detail.text = text;
                    }
                }
                ApplyOwnedQuery();
                if(ownedDetailId!=null&&comparingOwnedId==ownedDetailId) {
                    var panel=ownedDetailContent.Q<NativeStatsPanel>("ownedCarStats");
                    if(panel?.Proposed!=null&&ownedEntries.TryGetValue(ownedDetailId,out var entry))ComparisonSummary(ownedDetailContent.Q(className:"car-summary"),panel.Current.Inspection,panel.Proposed.Inspection,entry);
                }
            } catch (Exception) {
                ownedGrid.Query<Label>(className:"car-class").ForEach(label => label.text = "Class unavailable · PP —");
                foreach(var entry in ownedEntries.Values)entry.RatingPending=false;ApplyOwnedQuery();
            }
        }
        bool TryGetOwnedRating(string id,out OwnedRatingResult value)
        {
            if(!ownedRatingCache.TryGetValue(id,out value))return false;
            var node=ownedRatingNodes[id];ownedRatingRecency.Remove(node);ownedRatingRecency.AddLast(node);
            return true;
        }
        void RememberOwnedRating(string id,OwnedRatingResult value)
        {
            if(ownedRatingNodes.TryGetValue(id,out var node))ownedRatingRecency.Remove(node);
            else {
                if(ownedRatingCache.Count>=128){
                    var oldest=ownedRatingRecency.First.Value;
                    ownedRatingRecency.RemoveFirst();ownedRatingNodes.Remove(oldest);ownedRatingCache.Remove(oldest);
                }
            }
            ownedRatingNodes[id]=ownedRatingRecency.AddLast(id);
            ownedRatingCache[id]=value;
        }
        void BuildOwnedCars(PlayerProfile profile,TorqueUnit units)
        {
            // A previous batch may have finished since the last UI refresh.
            // Retain its fully computed, signature-keyed results before replacing
            // the cards, instead of running the same quarter-mile probes again.
            var previousRatings=ownedRatings;
            if(previousRatings!=null&&previousRatings.Status==TaskStatus.RanToCompletion)
                foreach(var finished in previousRatings.Result)
                    if(finished.Value!=null)RememberOwnedRating(finished.Key,finished.Value);
            foreach (var surface in ownedSurfaces) surfaces.Remove(surface);
            ownedSurfaces.Clear();
            Vector2 scroll = ownedScroll.scrollOffset; ownedGrid.Clear();ownedEntries.Clear();
            ownedProfile = profile; ownedRevision = profile.revision; ownedSelection = profile.selectedOwnedVehicleId; ownedTorque = units;
            OwnedGridBuildCount++;ownedWarningSnapshots.Clear();warningProfile=profile;warningRevision=profile.revision;
            var probes = new List<Tuple<string,string,InspectionBuild,CarSimulation>>();
            foreach (var car in profile.cars) {
                string id = car.instanceId;
                var entry=new OwnedCarEntry {Id=id,Order=ownedEntries.Count,Name=VehicleCollection.DisplayName(profile.catalog,car),Model=profile.catalog.Spec(car.vehicleId).displayName,Drive=profile.catalog.Spec(car.vehicleId).drive.ToString(),Type=BodyName(profile.catalog.Spec(car.vehicleId).bodyType),Status="Unavailable",RatingPending=true};ownedEntries.Add(id,entry);
                var card = new Button(() => OpenOwnedCar(id)) { name = "ownedCar:"+id, tooltip = VehicleCollection.DisplayName(profile.catalog,car)+" · "+id };
                card.AddToClassList("owned-car-card"); card.AddToClassList("clickable-panel"); ownedGrid.Add(card);
                AddSurface(card,RacingSurface.Kind.Panel); ownedSurfaces.Add(surfaces[surfaces.Count-1]);
                var heading = new VisualElement(); heading.AddToClassList("car-card-heading"); card.Add(heading);
                var title = new Label(VehicleCollection.DisplayName(profile.catalog,car)) { enableRichText = false }; title.AddToClassList("car-card-title"); heading.Add(title);
                if (car.instanceId == profile.SelectedCar?.instanceId) { var current = new Label("CURRENT"); current.AddToClassList("car-current"); heading.Add(current); }
                var identity = new Label("#"+car.instanceId.Substring(Math.Max(0,car.instanceId.Length-6))); identity.AddToClassList("car-identity"); card.Add(identity);
                var body = new VisualElement(); body.AddToClassList("car-card-body"); card.Add(body);
                var visual = new VisualElement(); visual.AddToClassList("car-visual"); body.Add(visual);
                var info = new VisualElement { name = "carSummary" }; info.AddToClassList("car-summary"); body.Add(info);
                try {
                    var build = MechanicsOwnership.Resolve(profile,car,profile.catalog);
                    visual.Add(CarPreview(build,car.paintHex));
                    var inspected=InspectOwned(car);
                    var source=inspected==null?InspectionBuild.Capture(profile,car):inspected.Stats.source;
                    CarInspection stats;
                    if(inspected!=null)stats=inspected.Stats;
                    else {
                        var simulation=CarInspection.PrepareCar(source);
                        stats=CarInspection.CalculatePrepared(source,simulation,false,partPreviewCache.SteadyOutput(simulation.induction));
                    }
                    ownedWarningSnapshots[car.instanceId]=stats.warnings.ToArray();
                    entry.Hp=stats.peakHp;entry.Torque=stats.peakTorque;entry.Weight=stats.vehicleMassKg;entry.Condition=stats.condition*100;entry.Drive=build.spec.drive.ToString();entry.Type=BodyName(build.spec.bodyType);entry.Gearbox=OwnedCarEntry.GearboxName(TransmissionDynamics.Configuration(build.spec,build.tune));entry.Induction=InductionName(new InductionProfile(build.spec,build.tune).type);entry.LockedCenter=build.spec.drive==DriveType.AWD&&AxleDriveDynamics.Configuration(build.spec,build.tune).mode==AwdMode.LockedCenter;
                    var values = new VisualElement(); values.AddToClassList("car-card-stats"); info.Add(values);
                    CarFact(values,"HP",stats.peakHp.ToString("F0"));
                    CarFact(values,"Torque",DisplayUnits.Torque(stats.peakTorque,units).ToString("F0")+" "+DisplayUnits.TorqueLabel(units));
                    CarFact(values,"Weight",stats.vehicleMassKg.ToString("F0")+" kg");
                    string signature=inspected==null?NativePartPreviewCache.CarSignature(profile,car):inspected.Fingerprint;
                    bool reusedRating=TryGetOwnedRating(car.instanceId,out var prior)&&prior.Context==signature;
                    if(reusedRating){entry.Rating=prior.Rating;entry.RatingPending=false;}
                    string ratingText=reusedRating?(prior.Rating==null?"Class unavailable · PP —":"Class "+prior.Rating.performanceClass+" · PP "+prior.Rating.points):"Class …";
                    var rating = new Label(ratingText) { name = "carClass:"+car.instanceId }; rating.AddToClassList("car-class"); info.Add(rating);
                    var installed = car.installed.slots.Select(slot => profile.parts.Find(part => part.instanceId == slot.partInstanceId)).Where(part => part != null).ToArray();
                    int failed = installed.Sum(part => profile.catalog.Find(part.definitionId).category==PartCategory.Tires?part.condition.tires.Count(t=>t.failed||t.health<=0||t.consumedLife>=1):(part.condition.failed||part.condition.health<=0?1:0));
                    int worn = installed.Sum(part => profile.catalog.Find(part.definitionId).category==PartCategory.Tires?part.condition.tires.Count(t=>!t.failed&&t.health>0&&t.consumedLife<1&&(t.health<=.5f||t.consumedLife>=.5)):(PartSemantics.Wears(profile.catalog.Find(part.definitionId).category)&&!part.condition.failed&&part.condition.health>0&&(part.condition.health<=.5f||part.condition.deposits>=.05||part.condition.leakSeverity>=.01||part.condition.adjustmentError>=.05||EngineElements.NeedsService(part.condition))?1:0));
                    bool reserved = !string.IsNullOrEmpty(car.garageJobId) || car.engineRebuild != null;
                    string status = reserved ? "Reserved" : !build.Drivable ? "Unavailable" : car.fuel.litres <= 0 ? "No fuel" : failed > 0 || worn > 0 || build.warnings.Count > 0 ? "Needs service" : "Ready";
                    entry.Status=status;
                    var condition = new Label(status+(failed>0?" · "+failed+" failed":"")+(worn>0?" · "+worn+" worn":""));
                    condition.AddToClassList("car-condition"); condition.AddToClassList(failed>0 || !build.Drivable ? "car-critical" : status=="Ready"?"car-ready":"car-warning"); info.Add(condition);
                    var fuel = new Label(entry.DriveLabel+" · "+entry.Type+" · "+car.fuel.litres.ToString("F1")+" / "+build.spec.fuel.capacityLitres.ToString("F0")+" L"); fuel.AddToClassList("car-identity"); info.Add(fuel);
                    if (!build.Drivable || build.warnings.Count > 0) {
                        var reason = new Label(string.IsNullOrEmpty(car.engineAssemblyId)?"No engine attached":build.errors.Concat(build.warnings).FirstOrDefault()); reason.AddToClassList("car-reason"); info.Add(reason);
                    }
                    if(!reusedRating)probes.Add(Tuple.Create(car.instanceId,signature,source,CarInspection.PrepareCar(source)));
                } catch (Exception) {
                    entry.RatingPending=false;
                    var unavailable = new Label("Build data unavailable"); unavailable.AddToClassList("car-critical"); info.Add(unavailable);
                }
                var warningButton=WarningButton(heading,"ownedWarnings:"+id,CurrentOwnedWarnings(car),()=>OpenOwnedWarnings(id),(element,kind)=>{AddSurface(element,kind);ownedSurfaces.Add(element.Q<RacingSurface>());},true);
                card.Query<VisualElement>().ForEach(element => { if (element != card) element.pickingMode = PickingMode.Ignore; });
                warningButton.pickingMode=PickingMode.Position;
            }
            if (profile.cars.Count == 0) { var empty = new Label("No owned cars"); empty.AddToClassList("car-empty"); ownedGrid.Add(empty); }
            if(profile.cars.Count==0){var recovery=new Button(()=>game.OpenRecovery()){name="emptyGarageRecovery",text="Recovery work"};recovery.AddToClassList("action-button");ownedGrid.Add(recovery);AddSurface(recovery,RacingSurface.Kind.SecondaryAction);ownedSurfaces.Add(recovery.Q<RacingSurface>());}
            ApplyOwnedQuery();ownedScroll.scrollOffset = scroll; RefreshOwnedDetails(); ExcludeTabTraversal();
            // Rebuilding for unit/selection/cosmetic changes must not cancel an
            // identical in-flight physics batch and immediately schedule it again.
            if(previousRatings!=null&&!previousRatings.IsCompleted
                &&ownedCancellation!=null&&!ownedCancellation.IsCancellationRequested
                &&probes.Count==ownedPendingContexts.Count
                &&probes.All(p=>ownedPendingContexts.TryGetValue(p.Item1,out var prior)&&prior==p.Item2))
                return;
            ownedCancellation?.Cancel();ownedCancellation?.Dispose();
            ownedCancellation=null;ownedRatings=null;ownedPendingContexts.Clear();
            if(probes.Count>0) {
                ownedCancellation=new CancellationTokenSource();var token=ownedCancellation.Token;
                foreach(var probe in probes)ownedPendingContexts[probe.Item1]=probe.Item2;
                OwnedRatingRunCount++;
                ownedRatings = Task.Run(() => {
                    var result = new Dictionary<string,OwnedRatingResult>();
                    foreach (var probe in probes) {
                        if (token.IsCancellationRequested) break;
                        try {
                            var stats=CarInspection.CalculatePrepared(probe.Item3,probe.Item4);
                            result[probe.Item1]=new OwnedRatingResult{
                                Context=probe.Item2,
                                Driving=stats,
                                Rating=stats.completed?Benchmark.Rating(stats.elapsed,stats.trapMps*3.6):null
                            };
                        }catch(Exception){result[probe.Item1]=null;}
                    }
                    return result;
                });
            }
        }
        void OpenOwnedCar(string id)
        {
            if (ModalOpen || dealerMode || game.ScreenName != "Garage") return;
            if (!game.Profile.cars.Exists(car => car.instanceId == id)) return;
            ownedReturnScroll = ownedScroll.scrollOffset;
            ownedDetailId = id;
            RefreshOwnedDetails(); Refresh();
        }
        void OpenOwnedCarRename()
        {
            if (ModalOpen || !OwnedDetailsOpen) return;
            string id = ownedDetailId;
            var car = game.Profile.cars.Find(item => item.instanceId == id);
            if (car == null) return;
            long revision = game.Profile.revision;
            var input = new TextField { name = "ownedCarName", maxLength = VehicleCollection.NameLimit, value = car.customName ?? "", tooltip = "Blank restores the model name" };
            input.AddToClassList("setting-text");
            var error = new Label { name = "renameCarError", enableRichText = false }; error.AddToClassList("car-critical");
            OpenDialog("Rename car", null, new[] {
                new DialogAction("Cancel",CloseDialog),
                new DialogAction("Save",() => {
                    if (!game.RenameNativeCar(id,revision,input.value)) { error.text = game.NativeNotice; return; }
                    CloseDialog(); Refresh();
                },true)
            }, populate:content => {
                content.Add(input);
                var fallback = new Label("Blank: "+game.Profile.catalog.Spec(car.vehicleId).displayName) { name = "renameCarFallback", enableRichText = false };
                fallback.AddToClassList("car-name-fallback"); content.Add(fallback); content.Add(error);
            });
            var save = root.Q<Button>("dialogSave");
            void UpdateRename() {
                try { string name = VehicleCollection.NormalizeName(input.value); error.text = ""; save.SetEnabled(name != (car.customName ?? "")); }
                catch (InvalidOperationException invalid) { error.text = invalid.Message; save.SetEnabled(false); }
            }
            input.RegisterValueChangedCallback(_ => UpdateRename()); UpdateRename();
            input.Focus(); ExcludeTabTraversal();
        }
        void BackToOwnedCars()
        {
            comparingOwnedId=null;
            ownedDetailId = null; ClearOwnedDetailContent();
            root.Q<Label>("carSelectionError").text = "";
            Refresh();
            ownedScroll.schedule.Execute(() => ownedScroll.scrollOffset = ownedReturnScroll);
        }
        void RefreshOwnedDetails()
        {
            if (ownedDetailId == null) return;
            var profile = game.Profile;
            var car = profile.cars.Find(item => item.instanceId == ownedDetailId);
            var card = ownedGrid.Q("ownedCar:"+ownedDetailId);
            if (car == null || card == null) { ownedDetailId = null; ClearOwnedDetailContent(); ownedDetails.AddToClassList("hidden"); if(game.ScreenName=="Garage"&&!dealerMode)ownedScroll.RemoveFromClassList("hidden"); return; }
            ownedDetailRevision = profile.revision;
            root.Q<Label>("ownedDetailTitle").text = VehicleCollection.DisplayName(profile.catalog,car);
            root.Q<Label>("carSelectionError").text = "";
            ClearOwnedDetailContent(); ownedDetailContent.tooltip = car.instanceId;
            var identity = new Label("#"+car.instanceId.Substring(Math.Max(0,car.instanceId.Length-6))); identity.AddToClassList("car-identity"); ownedDetailContent.Add(identity);
            bool current=car.instanceId==profile.selectedOwnedVehicleId;
            var selected=InspectOwned(profile.SelectedCar)?.Stats;
            bool comparing=!current&&comparingOwnedId==ownedDetailId&&selected!=null&&InspectOwned(car)!=null;
            ownedCompare.EnableInClassList("hidden",current);ownedCompare.SetEnabled(!current&&selected!=null&&InspectOwned(car)!=null);ownedCompare.EnableInClassList("selected",comparing);
            ownedCompare.tooltip=ownedCompare.enabledSelf?"Compare with the selected car":"Comparison unavailable: engine or build data missing";
            if(comparing)ComparisonHeading(ownedDetailContent,VehicleCollection.DisplayName(profile.catalog,car));
            var body = new VisualElement { name = "ownedDetailsTopRow" }; body.AddToClassList("car-card-body"); body.AddToClassList("car-details-top"); ownedDetailContent.Add(body);
            var visual = new VisualElement(); visual.AddToClassList("car-visual"); body.Add(visual);
            visual.Add(CarPreview(MechanicsOwnership.Resolve(profile,car,profile.catalog),car.paintHex));
            body.Add(CopyCarSummary(card.Q("carSummary")));
            if(comparing)ComparisonSummary(body.Q(className:"car-summary"),selected,InspectOwned(car).Stats,ownedEntries[car.instanceId]);
            body.Add(OwnedCarDyno(car,comparing?selected:null));
            BuildOwnedStats(body,car,comparing?selected:null);
            var summary=body.Q(className:"car-summary");
            var primaryActions=new VisualElement{name="ownedPrimaryActions"};primaryActions.AddToClassList("car-detail-actions");summary.Add(primaryActions);primaryActions.Add(summary.Q<Button>("ownedStatsToggle"));
            var fitted=new Button(()=>OpenFittedParts(car.installed.slots.Select(slot=>profile.parts.Single(part=>part.instanceId==slot.partInstanceId)),profile.catalog)){name="ownedFittedParts",text="Fitted parts",tabIndex=-1,focusable=false};
            fitted.AddToClassList("action-button");fitted.AddToClassList("car-stats-button");fitted.AddToClassList("fitted-parts-button");fitted.AddToClassList("car-detail-action-last");fitted.Add(new FilledMenuIcon("Build"));primaryActions.Add(fitted);DecorateOwnedStats(fitted,RacingSurface.Kind.SecondaryAction);
            var secondaryActions=new VisualElement{name="ownedSecondaryActions"};secondaryActions.AddToClassList("car-detail-actions");summary.Add(secondaryActions);
            WarningButton(secondaryActions,"ownedDetailWarnings",CurrentOwnedWarnings(car),()=>OpenOwnedWarnings(car.instanceId),DecorateOwnedStats);
            BuildOwnedMaintenance(car);
            if(car.usedHistory.Count>0){var history=new Button(()=>OpenDialog("Previous history",string.Join("\n",car.usedHistory),new[]{new DialogAction("Close",CloseDialog)})){name="ownedHistory",text="History",tabIndex=-1,focusable=false};history.AddToClassList("action-button");history.AddToClassList("car-stats-button");history.AddToClassList("car-detail-action-last");history.Add(new FilledMenuIcon("Data"));secondaryActions.Add(history);DecorateOwnedStats(history,RacingSurface.Kind.SecondaryAction);}
            ownedUse.SetEnabled(!current); ownedUse.tooltip = current ? "Already the current car" : "Use this owned car";
            ExcludeTabTraversal();
        }
        static VisualElement CopyCarSummary(VisualElement original)
        {
            VisualElement copy = original is Label label ? new Label(label.text) : new VisualElement();
            foreach (string className in original.GetClasses()) copy.AddToClassList(className);
            foreach (var child in original.Children()) copy.Add(CopyCarSummary(child));
            copy.pickingMode = PickingMode.Ignore;
            return copy;
        }
        static void CarFact(VisualElement parent,string caption,string value)
        {
            var row = new VisualElement(); row.AddToClassList("car-fact"); parent.Add(row);
            var label = new Label(caption); label.AddToClassList("car-fact-label"); row.Add(label);
            var number = new Label(value); number.AddToClassList("car-fact-value"); row.Add(number);
        }
        static VisualElement CarPreview(BuildResolution build,string paintHex)
        {
            var preview = new VisualElement { name = "carPreview", pickingMode = PickingMode.Ignore }; preview.AddToClassList("car-preview");
            var stage = new GameObject("Owned car preview source") { hideFlags = HideFlags.HideAndDontSave };
            stage.transform.position = new Vector3(-10000,-10000,0);
            try {
                var view = stage.AddComponent<LayeredCarView>();
                Color paint; if (!ColorUtility.TryParseHtmlString(paintHex,out paint)) paint = Color.white;
                view.Build(build.spec,paint,0,false); view.ApplySetup(build.spec,build.tune);
                var layers = stage.GetComponentsInChildren<SpriteRenderer>().Where(layer => layer.enabled && layer.sprite != null).OrderBy(layer => layer.sortingOrder).ToArray();
                if (layers.Length == 0) return preview;
                Bounds bounds = layers[0].bounds; foreach (var layer in layers) bounds.Encapsulate(layer.bounds);
                foreach (var layer in layers) {
                    var box=layer.bounds;var center=layer.transform.TransformPoint(layer.sprite.bounds.center);var size=Vector3.Scale(layer.sprite.bounds.size,layer.transform.lossyScale);
                    var image = new Image { sprite = layer.sprite, tintColor = layer.color, scaleMode = ScaleMode.StretchToFill, pickingMode = PickingMode.Ignore };
                    image.style.position = Position.Absolute;
                    image.style.left = Length.Percent((center.x-size.x*.5f-bounds.min.x)/bounds.size.x*100);
                    image.style.top = Length.Percent((bounds.max.y-center.y-size.y*.5f)/bounds.size.y*100);
                    image.style.width = Length.Percent(size.x/bounds.size.x*100);
                    image.style.height = Length.Percent(size.y/bounds.size.y*100);
                    image.style.rotate=new Rotate(new Angle(-layer.transform.eulerAngles.z,AngleUnit.Degree));
                    image.name="carLayer:"+layer.gameObject.name;preview.Add(image);
                }
                float aspect = bounds.size.y/bounds.size.x;
                preview.RegisterCallback<GeometryChangedEvent>(evt => {
                    if (evt.newRect.width > 0 && !Mathf.Approximately(evt.newRect.width,evt.oldRect.width))
                        preview.style.height = evt.newRect.width*aspect;
                });
            } finally { stage.SetActive(false); Destroy(stage); }
            return preview;
        }
    }
}

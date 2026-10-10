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
        Task<Dictionary<string,string>> ownedRatings;
        string ownedDetailId;
        Vector2 ownedReturnScroll;
        long ownedDetailRevision;
        VisualElement ownedDetails, ownedDetailContent;
        Button ownedUse;
        internal bool OwnedDetailsOpen => ownedDetailId != null && game.ScreenName == "Garage" && Visible;
        readonly List<RacingSurface> ownedSurfaces = new List<RacingSurface>();
        internal int OwnedGridBuildCount { get; private set; }
        internal void InvalidateOwnedGrid() => ownedRevision = -1;

        void InitializeOwnedCars()
        {
            ownedScroll = new ScrollView { name = "ownedCars", horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            ownedScroll.AddToClassList("owned-cars-scroll"); ownedScroll.AddToClassList("hidden");
            ownedGrid = new VisualElement { name = "ownedCarGrid" }; ownedGrid.AddToClassList("owned-cars-grid");
            ownedScroll.Add(ownedGrid); root.Q(className:"workspace").Add(ownedScroll);
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
        }
        void RefreshOwnedCars(string route)
        {
            ownedScroll.EnableInClassList("hidden", route != "Garage" || ownedDetailId != null);
            ownedDetails.EnableInClassList("hidden", route != "Garage" || ownedDetailId == null);
            if (route != "Garage") return;
            var profile = game.Profile;
            TorqueUnit units = SharedUnitSettings.Current.torque;
            if (!ReferenceEquals(ownedProfile,profile) || ownedRevision != profile.revision || ownedSelection != profile.selectedOwnedVehicleId || ownedTorque != units)
                BuildOwnedCars(profile,units);
            if (ownedRatings == null || !ownedRatings.IsCompleted) return;
            var completed = ownedRatings; ownedRatings = null;
            try {
                foreach (var rating in completed.Result) {
                    var label = ownedGrid.Q<Label>("carClass:"+rating.Key);
                    if (label != null) label.text = rating.Value;
                    if (ownedDetailId == rating.Key) {
                        var detail = ownedDetailContent.Q<Label>(className:"car-class");
                        if (detail != null) detail.text = rating.Value;
                    }
                }
            } catch (Exception) {
                ownedGrid.Query<Label>(className:"car-class").ForEach(label => label.text = "Class unavailable");
            }
        }
        void BuildOwnedCars(PlayerProfile profile,TorqueUnit units)
        {
            ownedCancellation?.Cancel(); ownedCancellation?.Dispose();
            ownedCancellation = new CancellationTokenSource(); var token = ownedCancellation.Token;
            ownedRatings = null;
            foreach (var surface in ownedSurfaces) surfaces.Remove(surface);
            ownedSurfaces.Clear();
            Vector2 scroll = ownedScroll.scrollOffset; ownedGrid.Clear();
            ownedProfile = profile; ownedRevision = profile.revision; ownedSelection = profile.selectedOwnedVehicleId; ownedTorque = units;
            OwnedGridBuildCount++;
            var probes = new List<Tuple<string,InspectionBuild,CarSimulation>>();
            foreach (var car in profile.cars) {
                string id = car.instanceId;
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
                    var source = InspectionBuild.Capture(profile,car);
                    var simulation = CarInspection.PrepareCar(source);
                    var stats = CarInspection.CalculatePrepared(source,simulation,false);
                    var values = new VisualElement(); values.AddToClassList("car-card-stats"); info.Add(values);
                    CarFact(values,"HP",stats.peakHp.ToString("F0"));
                    CarFact(values,"Torque",DisplayUnits.Torque(stats.peakTorque,units).ToString("F0")+" "+DisplayUnits.TorqueLabel(units));
                    CarFact(values,"Weight",stats.vehicleMassKg.ToString("F0")+" kg");
                    var rating = new Label("Class …") { name = "carClass:"+car.instanceId }; rating.AddToClassList("car-class"); info.Add(rating);
                    var installed = car.installed.slots.Select(slot => profile.parts.Find(part => part.instanceId == slot.partInstanceId)).Where(part => part != null).ToArray();
                    int failed = installed.Count(part => part.condition.failed || part.condition.health <= 0);
                    int worn = installed.Count(part => PartSemantics.Wears(profile.catalog.Find(part.definitionId).category) && !part.condition.failed && part.condition.health > 0 && (part.condition.health <= .5f || part.condition.deposits >= .05 || part.condition.leakSeverity >= .01 || part.condition.adjustmentError >= .05 || EngineElements.NeedsService(part.condition) || part.condition.tires.Any(tire=>tire.failed || tire.health<=.5f)));
                    bool reserved = !string.IsNullOrEmpty(car.garageJobId) || car.engineRebuild != null;
                    string status = reserved ? "Reserved" : !build.Drivable ? "Unavailable" : car.fuel.litres <= 0 ? "No fuel" : failed > 0 || worn > 0 || build.warnings.Count > 0 ? "Needs service" : "Ready";
                    var condition = new Label(status+(failed>0?" · "+failed+" failed":"")+(worn>0?" · "+worn+" worn":""));
                    condition.AddToClassList("car-condition"); condition.AddToClassList(failed>0 || !build.Drivable ? "car-critical" : status=="Ready"?"car-ready":"car-warning"); info.Add(condition);
                    var fuel = new Label(build.spec.drive+"   ·   "+car.fuel.litres.ToString("F1")+" / "+build.spec.fuel.capacityLitres.ToString("F0")+" L"); fuel.AddToClassList("car-identity"); info.Add(fuel);
                    if (!build.Drivable || build.warnings.Count > 0) {
                        var reason = new Label(string.IsNullOrEmpty(car.engineAssemblyId)?"No engine attached":build.errors.Concat(build.warnings).FirstOrDefault()); reason.AddToClassList("car-reason"); info.Add(reason);
                    }
                    probes.Add(Tuple.Create(car.instanceId,source,CarInspection.PrepareCar(source)));
                } catch (Exception) {
                    var unavailable = new Label("Build data unavailable"); unavailable.AddToClassList("car-critical"); info.Add(unavailable);
                }
                card.Query<VisualElement>().ForEach(element => { if (element != card) element.pickingMode = PickingMode.Ignore; });
            }
            if (profile.cars.Count == 0) { var empty = new Label("No owned cars"); empty.AddToClassList("car-empty"); ownedGrid.Add(empty); }
            ownedScroll.scrollOffset = scroll; RefreshOwnedDetails(); ExcludeTabTraversal();
            ownedRatings = Task.Run(() => {
                var result = new Dictionary<string,string>();
                foreach (var probe in probes) {
                    if (token.IsCancellationRequested) break;
                    try { var stats = CarInspection.CalculatePrepared(probe.Item2,probe.Item3);
                        result[probe.Item1] = stats.completed ? "Class "+Benchmark.Rating(stats.elapsed,stats.trapMps*3.6).performanceClass : "Class unavailable";
                    } catch (Exception) { result[probe.Item1] = "Class unavailable"; }
                }
                return result;
            });
        }
        void OpenOwnedCar(string id)
        {
            if (ModalOpen || game.ScreenName != "Garage") return;
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
            if (car == null || card == null) { ownedDetailId = null; ClearOwnedDetailContent(); ownedDetails.AddToClassList("hidden"); if(game.ScreenName=="Garage")ownedScroll.RemoveFromClassList("hidden"); return; }
            ownedDetailRevision = profile.revision;
            root.Q<Label>("ownedDetailTitle").text = VehicleCollection.DisplayName(profile.catalog,car);
            root.Q<Label>("carSelectionError").text = "";
            ClearOwnedDetailContent(); ownedDetailContent.tooltip = car.instanceId;
            var identity = new Label("#"+car.instanceId.Substring(Math.Max(0,car.instanceId.Length-6))); identity.AddToClassList("car-identity"); ownedDetailContent.Add(identity);
            var body = new VisualElement { name = "ownedDetailsTopRow" }; body.AddToClassList("car-card-body"); body.AddToClassList("car-details-top"); ownedDetailContent.Add(body);
            var visual = new VisualElement(); visual.AddToClassList("car-visual"); body.Add(visual);
            visual.Add(CarPreview(MechanicsOwnership.Resolve(profile,car,profile.catalog),car.paintHex));
            body.Add(CopyCarSummary(card.Q("carSummary")));
            body.Add(OwnedCarDyno(car));
            BuildOwnedStats(body,car);
            BuildOwnedMaintenance(car);
            bool current = car.instanceId == profile.selectedOwnedVehicleId;
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
                view.Build(build.spec.artId,paint,0,false); view.ApplySetup(build.spec,build.tune);
                var layers = stage.GetComponentsInChildren<SpriteRenderer>().Where(layer => layer.enabled && layer.sprite != null).OrderBy(layer => layer.sortingOrder).ToArray();
                if (layers.Length == 0) return preview;
                Bounds bounds = layers[0].bounds; foreach (var layer in layers) bounds.Encapsulate(layer.bounds);
                foreach (var layer in layers) {
                    var box = layer.bounds;
                    var image = new Image { sprite = layer.sprite, tintColor = layer.color, scaleMode = ScaleMode.StretchToFill, pickingMode = PickingMode.Ignore };
                    image.style.position = Position.Absolute;
                    image.style.left = Length.Percent((box.min.x-bounds.min.x)/bounds.size.x*100);
                    image.style.top = Length.Percent((bounds.max.y-box.max.y)/bounds.size.y*100);
                    image.style.width = Length.Percent(box.size.x/bounds.size.x*100);
                    image.style.height = Length.Percent(box.size.y/bounds.size.y*100);
                    preview.Add(image);
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

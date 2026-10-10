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
        readonly List<RacingSurface> ownedSurfaces = new List<RacingSurface>();
        internal int OwnedGridBuildCount { get; private set; }
        internal void InvalidateOwnedGrid() => ownedRevision = -1;

        void InitializeOwnedCars()
        {
            ownedScroll = new ScrollView { name = "ownedCars", horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            ownedScroll.AddToClassList("owned-cars-scroll"); ownedScroll.AddToClassList("hidden");
            ownedGrid = new VisualElement { name = "ownedCarGrid" }; ownedGrid.AddToClassList("owned-cars-grid");
            ownedScroll.Add(ownedGrid); root.Q(className:"workspace").Add(ownedScroll);
        }
        void RefreshOwnedCars(string route)
        {
            ownedScroll.EnableInClassList("hidden", route != "Garage");
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
                var card = new VisualElement { name = "ownedCar:"+car.instanceId, tooltip = car.instanceId, pickingMode = PickingMode.Ignore };
                card.AddToClassList("owned-car-card"); card.AddToClassList("neutral-panel"); ownedGrid.Add(card);
                AddSurface(card,RacingSurface.Kind.Panel); ownedSurfaces.Add(surfaces[surfaces.Count-1]);
                var heading = new VisualElement(); heading.AddToClassList("car-card-heading"); card.Add(heading);
                var title = new Label(profile.catalog.Spec(car.vehicleId).displayName); title.AddToClassList("car-card-title"); heading.Add(title);
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
            }
            if (profile.cars.Count == 0) { var empty = new Label("No owned cars"); empty.AddToClassList("car-empty"); ownedGrid.Add(empty); }
            ownedScroll.scrollOffset = scroll; ExcludeTabTraversal();
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

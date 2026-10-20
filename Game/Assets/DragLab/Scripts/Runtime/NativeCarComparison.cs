using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine.UIElements;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    internal sealed partial class NativeMenuShell
    {
        sealed class OwnedInspection
        {
            internal CarInspection Stats, Healthy;
            internal Task<CarInspection> Driving;
        }
        readonly Dictionary<string,OwnedInspection> carInspections=new Dictionary<string,OwnedInspection>();
        PlayerProfile inspectionProfile;
        long inspectionRevision=-1;
        string comparingOwnedId,comparingDealerId;
        Button ownedCompare,dealerCompare;
        internal int InspectionCaptureCount {get;private set;}
        OwnedInspection InspectOwned(OwnedCar car)
        {
            if(inspectionProfile!=game.Profile||inspectionRevision!=game.Profile.revision) {
                carInspections.Clear();inspectionProfile=game.Profile;inspectionRevision=game.Profile.revision;
            }
            if(car==null||string.IsNullOrEmpty(car.engineAssemblyId))return null;
            if(carInspections.TryGetValue(car.instanceId,out var cached))return cached;
            try {
                var source=InspectionBuild.Capture(game.Profile,car);
                cached=new OwnedInspection {Stats=NativePartBrowser.Inspect(source,partPreviewCache)};
                InspectionCaptureCount++;
            }catch(Exception){cached=null;}
            carInspections[car.instanceId]=cached;return cached;
        }
        Task<CarInspection> OwnedDriving(OwnedCar car)
        {
            var cached=InspectOwned(car);
            if(cached==null)return Task.FromException<CarInspection>(new InvalidOperationException("Inspection unavailable"));
            if(cached.Driving==null) {
                var source=InspectionBuild.Capture(game.Profile,car);var prepared=CarInspection.PrepareCar(source);
                cached.Driving=Task.Run(()=>CarInspection.CalculatePrepared(source,prepared));
            }
            return cached.Driving;
        }
        CarInspection OwnedHealthy(OwnedCar car)
        {
            var cached=InspectOwned(car);if(cached==null)throw new InvalidOperationException("Inspection unavailable");
            if(cached.Healthy==null){var source=InspectionBuild.Capture(game.Profile,car,healthy:true);cached.Healthy=NativePartBrowser.Inspect(source,partPreviewCache);}
            return cached.Healthy;
        }
        Task<CarInspection> DealerDriving(DealerModel model)
        {
            if(model.Driving!=null)return Task.FromResult(model.Driving);
            if(dealerRatings==null)return Task.FromException<CarInspection>(new InvalidOperationException("Stock driving estimate unavailable"));
            return dealerRatings.ContinueWith(task=>task.GetAwaiter().GetResult().TryGetValue(model.Offer.listingId,out var value)&&value!=null?value:throw new InvalidOperationException("Stock driving estimate unavailable"));
        }
        Button CarCompareButton(VisualElement actions,string id,Action toggle)
        {
            var button=new Button(toggle) {name=id,text="Compare",tooltip="Compare with the selected car",tabIndex=-1,focusable=false};
            button.AddToClassList("action-button");actions.Insert(actions.childCount-1,button);AddSurface(button,RacingSurface.Kind.SecondaryAction);return button;
        }
        void ToggleOwnedComparison()
        {
            if(ModalOpen||!OwnedDetailsOpen||ownedDetailId==game.Profile.selectedOwnedVehicleId)return;
            comparingOwnedId=comparingOwnedId==ownedDetailId?null:ownedDetailId;
            if(comparingOwnedId!=null)ownedStatsShown.Add(ownedDetailId);
            RefreshOwnedDetails();
        }
        void ToggleDealerComparison()
        {
            if(ModalOpen||!DealerDetailsOpen)return;
            comparingDealerId=comparingDealerId==dealerDetailId?null:dealerDetailId;
            if(comparingDealerId!=null)dealerStatsShown.Add(dealerDetailId);
            RefreshDealerDetails();
        }
        void ComparisonHeading(VisualElement content,string viewed)
        {
            var heading=new Label("Selected: "+VehicleCollection.DisplayName(game.Profile.catalog,game.Profile.SelectedCar)+" → "+viewed) {name="carComparisonHeading",enableRichText=false};
            heading.AddToClassList("car-comparison-heading");content.Add(heading);
        }
        void ComparisonSummary(VisualElement summary,CarInspection selected,CarInspection viewed,OwnedCarEntry entry,double? viewedCondition=null)
        {
            var values=summary.Q(className:"car-card-stats");if(values==null)return;values.Clear();
            var units=SharedUnitSettings.Current;
            void Fact(string id,string label,double before,double after,string unit,int benefit) {
                string Format(double value)=>(unit=="Nm"?DisplayUnits.Torque(value,units.torque):value).ToString("F0")+(unit=="Nm"?" "+DisplayUnits.TorqueLabel(units.torque):string.IsNullOrEmpty(unit)?"":" "+unit);
                CarFact(values,label,Format(before)+" → "+Format(after));
                var number=values.Children().Last().Q<Label>(className:"car-fact-value");number.name="compareSummary:"+id;
                if(Math.Abs(after-before)>.00001)number.AddToClassList((after-before)*benefit>0?"car-ready":"car-critical");
            }
            Fact("HP","HP",selected.peakHp,viewed.peakHp,"",1);Fact("Torque","Torque",selected.peakTorque,viewed.peakTorque,"Nm",1);
            Fact("Weight","Weight",selected.vehicleMassKg,viewed.vehicleMassKg,"kg",-1);Fact("Condition","Condition",selected.condition*100,(viewedCondition??viewed.condition)*100,"%",1);
            if(ownedEntries.TryGetValue(game.Profile.selectedOwnedVehicleId,out var baseline)) {
                string Rating(OwnedCarEntry car)=>car.Rating==null?car.RatingPending?"…":"—":car.Rating.performanceClass+" / "+car.Rating.points;
                var rating=summary.Q<Label>(className:"car-class");if(rating!=null)rating.text="Class / PP "+Rating(baseline)+" → "+Rating(entry);
            }
        }
        NativeDynoPanel ComparisonDyno(CarInspection selected,CarInspection viewed,string plotName,string viewedLabel="Viewed")
            =>new NativeDynoPanel(new[] {
                new NativeDynoSeries("Selected","Selected",selected,NativeDynoStyle.Primary),
                new NativeDynoSeries("Viewed",viewedLabel,viewed,NativeDynoStyle.Alternative)
            },SharedUnitSettings.Current.torque,plotName);
    }
}

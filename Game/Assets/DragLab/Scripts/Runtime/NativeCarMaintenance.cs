using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Runtime
{
    internal sealed partial class NativeMenuShell
    {
        readonly List<RacingSurface> maintenanceSurfaces = new List<RacingSurface>();
        void ClearOwnedDetailContent()
        {
            foreach (var surface in maintenanceSurfaces) surfaces.Remove(surface);
            maintenanceSurfaces.Clear();
            foreach(var surface in ownedStatsSurfaces)surfaces.Remove(surface);ownedStatsSurfaces.Clear();
            ownedDetailContent.Clear();
        }
        void BuildOwnedMaintenance(OwnedCar car)
        {
            var profile=game.Profile; var catalog=profile.catalog;
            var section=new VisualElement {name="carMaintenance"}; section.AddToClassList("car-maintenance"); ownedDetailContent.Add(section);
            var build=MechanicsOwnership.Resolve(profile,car,catalog);
            var installed=profile.parts.Where(p=>car.installed.slots.Any(s=>s.partInstanceId==p.instanceId)).ToList();
            var service=installed.Where(p=>catalog.Find(p.definitionId).category!=PartCategory.Tires
                &&PartSemantics.Wears(catalog.Find(p.definitionId).category)
                &&(PrototypeGame.PartFailed(p)||p.condition.health<=.5f||p.condition.deposits>=.05
                    ||p.condition.leakSeverity>=.01||p.condition.adjustmentError>=.05||EngineElements.NeedsService(p.condition)))
                .OrderByDescending(PrototypeGame.PartFailed).ThenBy(p=>p.condition.health).ToList();
            var wornTires=installed.Where(p=>catalog.Find(p.definitionId).category==PartCategory.Tires)
                .SelectMany(p=>p.condition.tires.Select((t,i)=>new{Part=p,Tire=t,Corner=i}))
                .Where(x=>x.Tire.Failed||TireDynamics.RemainingHealth(x.Tire)<=.5f).ToList();
            int failed=service.Count(PrototypeGame.PartFailed)+wornTires.Count(x=>x.Tire.Failed);
            int needs=service.Count+wornTires.Count-failed;
            var summary=new Label("Maintenance · "+failed+" failed · "+needs+" need service") {name="maintenanceCondition"};
            summary.AddToClassList("maintenance-summary"); summary.AddToClassList(failed>0?"car-critical":needs>0?"car-warning":"car-ready"); section.Add(summary);
            bool available=true;
            try { EngineRebuilds.RequireAvailable(car); }
            catch (InvalidOperationException restriction) { var warning=new Label(restriction.Message); warning.AddToClassList("car-warning"); section.Add(warning); available=false; }
            ReplacementActions(section,car,available);
            var rows=new VisualElement {name="maintenanceFluids"}; rows.AddToClassList("maintenance-fluids"); section.Add(rows);
            var fuel=FluidRow(rows,"fuel","Fuel",car.fuel.litres,build.spec.fuel.capacityLitres);
            QuantityAction(fuel,"maintenanceFuel", "Refill",new PaymentRequest {kind=PaymentKind.Fuel,carId=car.instanceId},Math.Max(0,build.spec.fuel.capacityLitres-car.fuel.litres),available);
            if (!string.IsNullOrEmpty(car.engineAssemblyId)) {
                var oil=FluidRow(rows,"oil","Engine oil",car.fluids.oilLitres,build.spec.fluids.oilCapacityLitres,car.fluids.oilCondition);
                QuantityAction(oil,"maintenanceOil","Refill",new PaymentRequest {kind=PaymentKind.Fluids,carId=car.instanceId},Math.Max(0,build.spec.fluids.oilCapacityLitres-car.fluids.oilLitres),available);
                MaintenanceAction(oil,"maintenanceOilChange","Change oil",new PaymentRequest {kind=PaymentKind.OilChange,carId=car.instanceId},"Fresh oil, a new filter and labour. The old filter stays in inventory; leaks and other wear remain.",available);
                if(car.fluids.oilLitres>build.spec.fluids.oilCapacityLitres)
                    MaintenanceButton(oil,"maintenanceDrainOil","Drain excess",()=>OpenFreeMaintenance(car.instanceId,"Drain excess oil","Drain to the installed oil-pan capacity. Oil aging and component faults remain."),available);
                var coolant=FluidRow(rows,"coolant","Coolant",car.fluids.coolantLitres,build.spec.fluids.coolantCapacityLitres);
                QuantityAction(coolant,"maintenanceCoolant","Refill",new PaymentRequest {kind=PaymentKind.Fluids,carId=car.instanceId},Math.Max(0,build.spec.fluids.coolantCapacityLitres-car.fluids.coolantLitres),available,true);
                MaintenanceAction(coolant,"maintenanceFlush","Flush",new PaymentRequest {kind=PaymentKind.CoolantFlush,carId=car.instanceId},"Renew coolant and pay labour. Leaks and component wear remain.",available);
            } else {var missing=new Label("No engine attached · oil/coolant stay with the stored engine");missing.AddToClassList("car-warning");section.Add(missing);}
            foreach(var clutch in installed.Where(p=>catalog.Find(p.definitionId).category==PartCategory.Clutch && catalog.Find(p.definitionId).clutchEnvironment==ClutchEnvironment.Wet)) {
                var d=catalog.Find(clutch.definitionId);
                var row=FluidRow(rows,"clutch","Clutch oil",clutch.condition.clutchOilLitres,d.clutchOilCapacityLitres,clutch.condition.clutchOilCondition);
                MaintenanceAction(row,"maintenanceClutchOil","Service oil",new PaymentRequest {kind=PaymentKind.ClutchOil,carId=car.instanceId,targetId=clutch.instanceId},"Renew the wet-clutch oil bath and pay labour. Clutch wear remains.",available);
            }
            var parts=new VisualElement {name="maintenanceParts"}; section.Add(parts);
            foreach(var entry in wornTires){
                var row=new VisualElement{name="maintenanceTire:"+entry.Corner};row.AddToClassList("maintenance-part");parts.Add(row);
                var caption=new VisualElement();caption.AddToClassList("maintenance-part-caption");row.Add(caption);
                var spec=catalog.Find(TireFitments.DefinitionId(entry.Part,entry.Corner));
                string corner=new[]{"Front left","Front right","Rear left","Rear right"}[entry.Corner];
                var title=new Label(corner+" tire · "+spec.name){tooltip=entry.Tire.id,enableRichText=false};
                title.AddToClassList("maintenance-part-name");caption.Add(title);
                double life=Math.Min(entry.Tire.health,Math.Max(0,1-entry.Tire.consumedLife));
                bool tireFailed=entry.Tire.failed||life<=0;
                var state=new Label(tireFailed?"Failed":"Condition "+life.ToString("P0"));
                state.AddToClassList(tireFailed?"car-critical":ServiceConditionClass((float)life));caption.Add(state);
                var guidance=new Label("Select this corner in Replace parts to buy and fit an individual tire.");
                guidance.AddToClassList("maintenance-summary");row.Add(guidance);
            }
            foreach(var item in service) {
                var d=catalog.Find(item.definitionId);var row=new VisualElement {name="maintenancePart:"+item.instanceId};row.AddToClassList("maintenance-part");parts.Add(row);
                var caption=new VisualElement();caption.AddToClassList("maintenance-part-caption");row.Add(caption);
                string model=catalog.Spec(car.vehicleId).displayName+" / "; string name=d.name.StartsWith(model,StringComparison.Ordinal)?d.name.Substring(model.Length):d.name;
                var title=new Label(name) {tooltip=d.name+" · "+item.instanceId,enableRichText=false};title.AddToClassList("maintenance-part-name");caption.Add(title);
                int faults=item.condition.elements.Count(e=>e is TireCondition ? ((TireCondition)e).failed||e.health<=.5f : e.Failed||e.fault!=ElementFault.None||e.health<.5f);
                float memberHealth=d.category==PartCategory.Tires?TireDynamics.LowestHealth(item.condition):item.condition.health;
                string failureReason=d.category==PartCategory.Tires?TireDynamics.FailedCorners(item.condition):item.condition.failureReason;
                var state=new Label((PrototypeGame.PartFailed(item)?"Failed":"Condition "+memberHealth.ToString("P0"))+(faults>0?" · "+faults+" faulty":"")) {tooltip=failureReason};state.AddToClassList(PrototypeGame.PartFailed(item)?"car-critical":ServiceConditionClass(memberHealth));caption.Add(state);
                var actions=new VisualElement();actions.AddToClassList("maintenance-part-actions");row.Add(actions);
                foreach(var action in ServiceOperations.Available(d,item.condition,ServiceOperations.BoreHealth(profile,car)))
                    MaintenanceAction(actions,"maintenanceRepair:"+action+":"+item.instanceId,action.ToString(),new PaymentRequest {kind=PaymentKind.Repair,carId=car.instanceId,targetId=item.instanceId,service=action},ServiceOperations.Description(action),available);
                int count=Math.Max(item.condition.elements.Length,item.condition.tires.Length);
                MaintenanceAction(actions,"maintenanceReplace:"+item.instanceId,"Buy same",new PaymentRequest {kind=PaymentKind.Part,carId=car.instanceId,targetId=item.definitionId,install=true,preserveTireFitments=d.category==PartCategory.Tires},(d.category==PartCategory.Tires&&TireFitments.HasOverrides(item)?"Current mixed tire specifications":d.name)+(count>1?(d.category==PartCategory.Tires?" · set of ":" · matched set of ")+count:"")+"\nBuy and install the same specification. The removed part/set stays in inventory with its condition; saved tuning stays.",available,item.instanceId);
            }
            if(!string.IsNullOrEmpty(car.engineAssemblyId)) {
                var engine=new VisualElement();engine.AddToClassList("maintenance-engine");section.Add(engine);
                var job=car.engineRebuild;
                if(job!=null&&job.active) {
                    var status=new Label {name="maintenanceRebuildStatus"};status.AddToClassList("maintenance-part-name");engine.Add(status);
                    var collect=MaintenanceButton(engine,"maintenanceRebuildCollect","Collect",()=>OpenFreeMaintenance(car.instanceId,"Collect rebuilt engine","Complete the paid rebuild. Saved tuning stays; work applies only to the reserved engine.",job.id),false);
                    void UpdateJob(){long remaining=job.readyUtcTicks-DragLab.Domain.RuntimeEntropy.UtcNow.Ticks;status.text=remaining<=0?"Engine rebuild · ready":"Engine rebuild · "+TimeSpan.FromTicks(remaining).ToString(@"hh\:mm\:ss");collect.SetEnabled(remaining<=0);}
                    UpdateJob();status.schedule.Execute(UpdateJob).Every(1000);
                } else {
                    try {
                        long now=DragLab.Domain.RuntimeEntropy.UtcNow.Ticks;var quote=EngineRebuilds.Quote(profile,car,now,game.Costs);
                        string description="Recondition the engine core/valvetrain; replace unsalvageable sets, renew oil/filter. Other systems and saved tuning stay.\nShop time: "+TimeSpan.FromTicks(quote.readyUtcTicks-now).TotalMinutes.ToString("F0")+" minutes; engine reserved until collection. Offline time counts.";
                        MaintenanceAction(engine,"maintenanceRebuild","Rebuild engine",new PaymentRequest {kind=PaymentKind.Rebuild,carId=car.instanceId},description,available);
                    }catch(InvalidOperationException){ }
                }
            }
        }
        void OpenFreeMaintenance(string carId,string title,string description,string rebuildId=null)
        {
            if(ModalOpen||!OwnedDetailsOpen||carId!=ownedDetailId)return;
            long revision=game.Profile.revision;string snapshot=JsonUtility.ToJson(game.Profile);var error=new Label {name="maintenanceError"};error.AddToClassList("car-critical");
            OpenDialog(title,null,new[] {new DialogAction("Cancel",CloseDialog),new DialogAction("Confirm",()=> {
                if(!game.CommitNativeService(carId,revision,snapshot,rebuildId)){error.text=game.NativeNotice;return;}
                CloseDialog();Refresh();
            },true)},populate:content=> {content.Add(new Label(description));content.Add(error);});
        }
        VisualElement FluidRow(VisualElement parent,string id,string label,double quantity,double capacity,double condition=-1)
        {
            var row=new VisualElement {name="maintenanceFluid:"+id};row.AddToClassList("maintenance-fluid");parent.Add(row);
            var caption=new Label(label);caption.AddToClassList("maintenance-fluid-name");row.Add(caption);
            var meter=new ProgressBar {lowValue=0,highValue=1,value=(float)Math.Max(0,Math.Min(1,quantity/Math.Max(.001,capacity)))};meter.AddToClassList("maintenance-meter");meter.pickingMode=PickingMode.Ignore;row.Add(meter);
            var level=new VisualElement();level.AddToClassList("maintenance-fluid-level");row.Add(level);
            level.Add(new Label(quantity.ToString("F2")+" / "+capacity.ToString("F1")+" L") {name="maintenanceLevel:"+id});
            if(condition>=0) {
                var percentage=new Label(" · "+condition.ToString("P0")) {name="maintenanceCondition:"+id,tooltip="Remaining fluid condition"};
                percentage.AddToClassList(ServiceConditionClass(condition));level.Add(percentage);
            }
            return row;
        }
        static string ServiceConditionClass(double condition)=>condition<=.25?"car-critical":condition<=.5?"car-warning":"car-ready";
        Button MaintenanceButton(VisualElement parent,string id,string label,Action action,bool enabled)
        {
            var button=new Button(action) {name=id,text=label};button.AddToClassList("action-button");button.AddToClassList("maintenance-action");parent.Add(button);
            AddSurface(button,RacingSurface.Kind.SecondaryAction);maintenanceSurfaces.Add(button.Q<RacingSurface>());button.SetEnabled(enabled);return button;
        }
        void QuantityAction(VisualElement parent,string id,string label,PaymentRequest request,double free,bool available,bool coolant=false)
        {
            bool refillPilot=request.kind==PaymentKind.Fluids;
            bool checkSave=refillPilot&&game.ManualRefillCanRetry;
            bool recoverSave=refillPilot&&game.ManualRefillNeedsRecovery;
            string actionLabel=checkSave?"Check save":recoverSave?"Recover save":label;
            MaintenanceButton(parent,id,actionLabel,
                ()=>OpenMaintenance(checkSave?"Check refill save":recoverSave?"Recover refill save":
                    "Refill "+(request.kind==PaymentKind.Fuel?"fuel":coolant?"coolant":"engine oil"),
                    request,checkSave||recoverSave?"Resolve the retained refill operation before starting another service.":
                    "Added liquid increases weight. Existing faults and fluid aging remain.",free,coolant),
                checkSave||recoverSave||available&&free>.000001);
        }
        void MaintenanceAction(VisualElement parent,string id,string label,PaymentRequest request,string description,bool available,string replacedId=null)
        {
            int cost=-1;string reason=null;
            try { cost=PaidEconomy.Preview(game.Profile,game.Profile.catalog,game.Costs,game.PricingPolicy,request,DragLab.Domain.RuntimeEntropy.UtcNow.Ticks).cost; }
            catch(Exception error) {reason=error.Message;}
            var button=MaintenanceButton(parent,id,label+ (cost>=0?" · " + DisplayUnits.Money(cost):""),()=>OpenMaintenance(label,request,description,replacedId:replacedId),available&&cost>=0);
            button.tooltip=reason??description;
        }
        void OpenMaintenance(string title,PaymentRequest request,string description,double free=-1,bool coolant=false,string replacedId=null)
        {
            if(ModalOpen||!OwnedDetailsOpen||request.carId!=ownedDetailId)return;
            // The manual engine-fluid pilot is typed end-to-end. Return before the legacy
            // request clone and profile snapshot below; fuel and other services remain legacy.
            if(request.kind==PaymentKind.Fluids) {
                OpenTypedManualRefill(title,request.carId,free,coolant,description);
                return;
            }
            long revision=game.Profile.revision;string snapshot=JsonUtility.ToJson(game.Profile);
            var draft=JsonUtility.FromJson<PaymentRequest>(JsonUtility.ToJson(request));
            var cost=new Label {name="maintenanceCost"};var error=new Label {name="maintenanceError"};error.AddToClassList("car-critical");
            var quantity=free<0?null:new FloatField {name="maintenanceQuantity",value=(float)free};
            var slider=free<0?null:new Slider(0,(float)free) {name="maintenanceQuantitySlider",value=(float)free};
            int price=-1;
            OpenDialog(title,null,new[] {new DialogAction("Cancel",CloseDialog),new DialogAction("Confirm",()=> {
                if(price<0||!game.CommitNativeMaintenance(draft,revision,snapshot,price,replacedId)){error.text=game.NativeNotice;return;}
                CloseDialog();Refresh();
            },true)},populate:content=> {
                content.Add(new Label(description) {enableRichText=false});
                if(quantity!=null) {
                    var row=new VisualElement();row.AddToClassList("maintenance-quantity");content.Add(row);
                    slider.AddToClassList("setting-slider");var fill=new SliderFill(slider);slider.Q(className:"unity-base-slider__tracker").Add(fill);slider.RegisterValueChangedCallback(_=>fill.MarkDirtyRepaint());row.Add(slider);
                    quantity.AddToClassList("setting-number");row.Add(quantity);row.Add(new Label("L"));
                    content.Add(new Label("Free space: "+free.ToString("F2")+" L"));
                }
                content.Add(cost);content.Add(error);
            });
            var confirm=root.Q<Button>("dialogConfirm");
            void UpdateQuote() {
                try {
                    if(quantity!=null) {
                        if(float.IsNaN(quantity.value)||float.IsInfinity(quantity.value)||quantity.value<0||quantity.value>free+.00001)throw new InvalidOperationException("Enter 0–"+free.ToString("F2")+" L.");
                        slider.SetValueWithoutNotify(quantity.value);slider.Q<SliderFill>().MarkDirtyRepaint();
                        if(coolant)draft.coolantLitres=quantity.value;else draft.litres=quantity.value;
                    }
                    var preview=PaidEconomy.Preview(game.Profile,game.Profile.catalog,game.Costs,game.PricingPolicy,draft,DragLab.Domain.RuntimeEntropy.UtcNow.Ticks);
                    string delivery="";if(draft.kind==PaymentKind.Part){var d=game.Profile.catalog.Find(draft.targetId);var target=game.Profile.cars.Find(c=>c.instanceId==draft.carId);var supply=draft.preserveTireFitments&&draft.install&&d.category==PartCategory.Tires?TireFitments.ReplacementSupply(game.Profile,target,d.id):PartsMarket.Quote(game.Profile,d,InventoryUnits.Count(game.Profile,target,d));if(supply.fee>0)delivery="\nExpress delivery included · "+DisplayUnits.Money(supply.fee)+" (5–10%)";}
                    price=preview.cost;cost.text="Total " + DisplayUnits.Money(price)+" · Cash " + DisplayUnits.Money(game.Profile.cash)+delivery;
                    bool enough=game.Profile.cash>=price;error.text=enough?"":"Need " + DisplayUnits.Money((price-game.Profile.cash))+" more";
                    confirm.SetEnabled(enough && (quantity==null || quantity.value>0));
                }catch(Exception invalid) {price=-1;error.text=invalid.Message;confirm.SetEnabled(false);}
            }
            if(quantity!=null){quantity.RegisterValueChangedCallback(_=>UpdateQuote());slider.RegisterValueChangedCallback(_=>{quantity.SetValueWithoutNotify(slider.value);UpdateQuote();});}
            UpdateQuote();ExcludeTabTraversal();
        }
    }
}

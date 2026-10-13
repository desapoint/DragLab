using System;
using System.Collections.Generic;
using System.Linq;
using DragLab.Domain;
namespace DragLab.Domain.Mechanics
{
    public enum BlockPreparationMode { Reuse,Manual,OEM }
    public sealed class BlockPartChoice {public string Source,Id;public BlockPartChoice(string source,string id=null){Source=source;Id=id;}}
    public sealed class BlockPreparationPlan
    {
        public List<PartCartLine> Lines=new List<PartCartLine>();
        public Dictionary<PartCategory,BlockPartChoice> Choices=new Dictionary<PartCategory,BlockPartChoice>();
        public CarInspection Current,Proposed,Healthy;
        public int Total;public string Error,Owner;
        public bool Ready=>Error==null&&Proposed!=null;
    }
    public static class BlockPreparation
    {
        public static string Owner(PartCartLine line)=>"block/"+line.action+"/"+(line.assetId??line.definitionId);
        public static bool Installs(PartCartLine line,string carId)=>line.install||line.action==CartAction.Move&&line.destination==InventoryKind.Car&&line.destinationId==carId;
        public static BlockPreparationPlan Build(PlayerProfile p,string carId,PartCartLine source,IEnumerable<PartCartLine> cart,BlockPreparationMode mode,IDictionary<PartCategory,BlockPartChoice> manual=null,ValuationPolicy policy=null)
        {
            var plan=new BlockPreparationPlan{Owner=Owner(source)};
            try{
                var car=p.cars.Find(c=>c.instanceId==carId)??throw new InvalidOperationException("Car no longer exists.");if(string.IsNullOrEmpty(car.engineAssemblyId))throw new InvalidOperationException("Attach an owned engine before replacing its block.");
                var block=p.catalog.Find(source.definitionId);if(block?.category!=PartCategory.CylinderBlock)throw new InvalidOperationException("Choose a compatible engine block.");
                if(source.quantity<1||source.quantity>99)throw new InvalidOperationException("A block line holds 1–99 cores.");
                plan.Current=NativeInspect(p,car);
                plan.Lines=cart.Where(l=>l.preparationOwner!=plan.Owner&&Owner(l)!=plan.Owner).Select(l=>l.Copy()).ToList();
                var core=source.Copy();core.preparedBlock=true;core.preparationOwner=plan.Owner;if(core.action==CartAction.Move){core.install=false;core.destination=InventoryKind.Car;core.destinationId=carId;}else core.install=true;plan.Lines.Add(core);
                var reference=PartCart.PreviewProfile(p,car,plan.Lines);var target=reference.cars.Find(c=>c.instanceId==carId);var definitions=MechanicsOwnership.Definitions(reference,target);var spec=reference.catalog.Resolve(target.vehicleId,definitions,target.tune).spec;
                var recipe=p.catalog.Recipe(car.vehicleId);var categories=recipe.Keys.Where(c=>(MechanicalCatalog.EngineSlot(c)||c==PartCategory.Controls)&&c!=PartCategory.CylinderBlock&&(definitions.ContainsKey(c)||!PartSemantics.Optional(c,spec,definitions,p.catalog))).ToArray();
                var reserved=plan.Lines.Select(l=>l.assetId).Where(id=>id!=null).ToHashSet();
                foreach(var category in categories){
                    bool required=!PartSemantics.Optional(category,spec,definitions,p.catalog);var pending=plan.Lines.FirstOrDefault(l=>Installs(l,carId)&&p.catalog.Find(l.definitionId)?.category==category);var fitted=car.installed.slots.Find(s=>s.slot==category.ToString());var current=fitted==null?null:p.parts.Find(i=>i.instanceId==fitted.partInstanceId);
                    BlockPartChoice choice=null;
                    if(mode==BlockPreparationMode.Manual&&manual!=null)manual.TryGetValue(category,out choice);
                    if(choice==null){if(pending!=null)choice=new BlockPartChoice("Cart",pending.definitionId);else if(mode==BlockPreparationMode.OEM&&required)choice=new BlockPartChoice("New",recipe[category]);else if(current!=null&&p.catalog.Fits(spec,p.catalog.Find(current.definitionId)))choice=new BlockPartChoice("Keep",current.instanceId);else{var owned=p.parts.Where(i=>i.location==PartLocation.Inventory&&!reserved.Contains(i.instanceId)&&!i.condition.failed&&i.condition.health>0&&p.catalog.Find(i.definitionId)?.category==category&&p.catalog.Fits(spec,p.catalog.Find(i.definitionId))).OrderByDescending(i=>i.condition.health).ThenBy(i=>i.instanceId,StringComparer.Ordinal).FirstOrDefault();choice=new BlockPartChoice(owned==null?"Missing":"Owned",owned?.instanceId);}}
                    plan.Choices[category]=choice;
                    if(!new[]{"Keep","Cart","Owned","New","Missing"}.Contains(choice.Source))throw new InvalidOperationException("Unknown component source.");
                    if(choice.Source=="Keep"&&(current==null||current.instanceId!=choice.Id))throw new InvalidOperationException("Fitted component changed.");
                    if(choice.Source=="Cart"&&pending==null)throw new InvalidOperationException("Supporting cart row no longer exists.");
                    if(choice.Source=="Owned"&&(!p.parts.Any(i=>i.instanceId==choice.Id&&i.location==PartLocation.Inventory&&p.catalog.Find(i.definitionId)?.category==category)||reserved.Contains(choice.Id)))throw new InvalidOperationException("Owned supporting part is unavailable or already planned.");
                    if(choice.Source=="New"&&p.catalog.Find(choice.Id)?.category!=category)throw new InvalidOperationException("Supporting offer has the wrong component type.");
                    if(pending!=null&&choice.Source!="Cart")throw new InvalidOperationException("Another cart row installs "+PartSemantics.Friendly(category.ToString())+". Keep Cart or edit that row first.");
                    PartCartLine line=null;
                    if(choice.Source=="Owned")line=new PartCartLine{action=CartAction.Move,assetId=choice.Id,definitionId=p.parts.Find(i=>i.instanceId==choice.Id)?.definitionId,destination=InventoryKind.Car,destinationId=carId};
                    else if(choice.Source=="New")line=new PartCartLine{action=CartAction.NewSet,definitionId=choice.Id,install=true};
                    else if(choice.Source=="Missing"&&current!=null)line=new PartCartLine{action=CartAction.Move,assetId=current.instanceId,definitionId=current.definitionId,destination=InventoryKind.Storage,destinationId=SharedInventory.Garage};
                    if(line!=null){line.preparationOwner=plan.Owner;plan.Lines.Add(line);if(line.assetId!=null)reserved.Add(line.assetId);}
                }
                plan.Total=PartCart.Total(p,carId,plan.Lines,policy,validateBlocks:false);var preview=PartCart.PreviewProfile(p,car,plan.Lines);var proposed=preview.cars.Find(c=>c.instanceId==carId);plan.Proposed=NativeInspect(preview,proposed);var healthy=InspectionBuild.Capture(preview,proposed,healthy:true);plan.Healthy=CarInspection.CalculatePrepared(healthy,CarInspection.PrepareCar(healthy),false);
                var result=MechanicsOwnership.Resolve(preview,proposed,preview.catalog);if(!result.Drivable)plan.Error="Required components / faults: "+string.Join("; ",result.errors);
            }catch(Exception e){plan.Error=e.Message;}
            return plan;
        }
        static CarInspection NativeInspect(PlayerProfile p,OwnedCar car){var source=InspectionBuild.Capture(p,car);return CarInspection.CalculatePrepared(source,CarInspection.PrepareCar(source),false);}
    }
}

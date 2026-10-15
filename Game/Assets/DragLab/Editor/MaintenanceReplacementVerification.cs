using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Editor
{
    public static class MaintenanceReplacementVerification
    {
        static string Rows(System.Collections.Generic.IEnumerable<PartCartLine> lines)=>string.Join("\n",lines.Select(l=>JsonUtility.ToJson(l)));
        public static void Run()
        {
            var log=new StringBuilder();int count=0;Action<bool,string> check=(ok,label)=>{if(!ok)throw new InvalidOperationException(label);count++;log.AppendLine(("PASS "+label).TrimEnd());};
            var catalog=MechanicsTransactions.Catalog();long now=DateTime.UtcNow.Ticks;
            foreach(var v in Resources.LoadAll<VehicleDefinition>("Data/Vehicles").OrderBy(v=>v.spec.id)){
                var p=PlayerProfile.New(now);p.cars[0].vehicleId=v.spec.id;p.selectedVehicleId=v.spec.id;p.cash=10000000;p.reputation=1000;MechanicsOwnership.Ensure(p,catalog);string carId=p.SelectedCar.instanceId;
                p.cars.Add(new OwnedCar{vehicleId=v.spec.id});MechanicsOwnership.Ensure(p,catalog);p.selectedOwnedVehicleId=p.cars[1].instanceId;p.selectedVehicleId=v.spec.id;
                var car=p.cars[0];PartInstance Part(PartCategory category)=>p.parts.Find(i=>i.instanceId==car.installed.slots.First(s=>s.slot==category.ToString()).partInstanceId);
                var pistons=Part(PartCategory.Pistons);pistons.condition.elements[0].health=.1f;pistons.condition.elements[1].health=.15f;pistons.condition.elements[2].health=.9f;pistons.condition.elements[2].fault=ElementFault.Disabled;EngineElementService.RefreshAggregate(pistons.condition);
                var tires=Part(PartCategory.Tires);var rear=catalog.Find(catalog.Id(v.spec.id,PartCategory.Tires,2));for(int n=0;n<4;n++){var t=tires.condition.tires[n];t.id=tires.instanceId+"/tire/"+n;t.health=n==0?.12f:n==2?.1f:.8f;t.consumedLife=1-t.health;if(n>=2)t.definitionId=rear.id;}tires.condition.health=tires.condition.tires.Min(t=>t.health);
                var cooling=Part(PartCategory.Cooling);cooling.condition.health=.12f;cooling.condition.leakSeverity=.3;
                var brakes=Part(PartCategory.Brakes);brakes.condition.failed=true;brakes.condition.health=.7f;brakes.condition.failureReason="Replacement matrix";
                car.fuel.litres=2;car.fluids.oilLitres=1;car.fluids.coolantLitres=1;car.fluids.oilCondition=.4;p=EngineWorkshop.Copy(p);car=p.cars[0];pistons=Part(PartCategory.Pistons);tires=Part(PartCategory.Tires);cooling=Part(PartCategory.Cooling);brakes=Part(PartCategory.Brakes);
                var broken=MaintenanceReplacements.Candidates(p,carId,true);var candidates=MaintenanceReplacements.Candidates(p,carId,false);
                check(broken.Length==2&&broken.Any(t=>t.partId==brakes.instanceId)&&broken.Any(t=>t.position==pistons.condition.elements[2].id),v.spec.id+" broken mode targets a failed set and one disabled cylinder");
                check(candidates.Length==6&&!candidates.Any(t=>t.position==pistons.condition.elements[1].id),v.spec.id+" under 15% uses remaining per-member condition and excludes the 15% boundary");
                string before=JsonUtility.ToJson(p),tune=JsonUtility.ToJson(catalog.Resolve(car.vehicleId,MechanicsOwnership.Definitions(p,car),car.tune).tune),fluids=JsonUtility.ToJson(car.fluids),other=JsonUtility.ToJson(p.cars[1]);
                var plan=MaintenanceReplacements.Preview(p,carId,false);check(plan.lines.Sum(l=>l.quantity)==6&&plan.cost==PartCart.Total(p,carId,plan.lines),v.spec.id+" batch price equals authoritative set/unit checkout");
                check(before==JsonUtility.ToJson(p),v.spec.id+" threshold/selection/stock preview edits and saves nothing");
                string kept=pistons.condition.elements[1].id,old=pistons.condition.elements[0].id,failed=pistons.condition.elements[2].id;string unaffectedTires=JsonUtility.ToJson(tires.condition.tires[1])+JsonUtility.ToJson(tires.condition.tires[3]);
                var quote=PartCart.Quote(p,carId,plan.lines,now,300);PlayerProfile next;string error;int cash=p.cash;
                bool saved=MechanicsTransactions.Try(p,catalog,p.revision,q=>PartCart.Checkout(q,quote,now),q=>true,out next,out error);check(saved,v.spec.id+" one atomic replacement payment commits: "+error);
                var fitted=next.parts.Find(i=>i.instanceId==pistons.instanceId);check(fitted.condition.elements[0].health==1&&fitted.condition.elements[2].health==1&&fitted.condition.elements[1].id==kept&&fitted.condition.elements[1].health==.15f,v.spec.id+" only selected cylinders change, retaining boundary condition");
                check(next.looseEngineElements.Any(u=>u.element.id==old&&u.element.health==.1f)&&next.looseEngineElements.Any(u=>u.element.id==failed&&u.element.fault==ElementFault.Disabled),v.spec.id+" removed members retain exact identities, wear and faults");
                var fittedTires=next.parts.Find(i=>i.instanceId==tires.instanceId);check(unaffectedTires==JsonUtility.ToJson(fittedTires.condition.tires[1])+JsonUtility.ToJson(fittedTires.condition.tires[3])&&fittedTires.condition.tires[0].health==1&&fittedTires.condition.tires[2].health==1&&TireFitments.DefinitionId(fittedTires,2)==rear.id,v.spec.id+" exact mixed tire compounds replace selected corners only");
                check(next.parts.Find(i=>i.instanceId==brakes.instanceId).location==PartLocation.Inventory&&next.parts.Find(i=>i.instanceId==brakes.instanceId).condition.failed&&next.parts.Find(i=>i.instanceId==cooling.instanceId).condition.leakSeverity==.3,v.spec.id+" replaced full components keep original damage in inventory");
                check(next.cash==cash-plan.cost&&next.revision==p.revision+1&&next.economyReceipts.Last().target==carId&&next.selectedOwnedVehicleId==p.selectedOwnedVehicleId,v.spec.id+" one charge/receipt preserves the independently selected car");
                check(tune==JsonUtility.ToJson(catalog.Resolve(car.vehicleId,MechanicsOwnership.Definitions(next,next.cars[0]),next.cars[0].tune).tune)&&SnapshotEquality.Serialized(fluids,next.cars[0].fluids)&&SnapshotEquality.Serialized(other,next.cars[1])&&next.cars[0].fuel.litres==2,v.spec.id+" applied tuning, liquids and the other car are unchanged / fluids "+SnapshotEquality.Serialized(fluids,next.cars[0].fluids)+" / other "+SnapshotEquality.Serialized(other,next.cars[1]));
                check(MechanicsOwnership.Validate(next,catalog)==null&&before==JsonUtility.ToJson(p),v.spec.id+" final ownership validates and original transaction source is unchanged");
                // Buying fuel immediately after Quick Replace must not depend on the removed parts
                // or change the retained dealer stock; both operations settle in separate receipts.
                var operating=new OperatingPolicy();var prices=ValuationPolicy.Defaults();
                var fuelRequest=new PaymentRequest{kind=PaymentKind.Fuel,carId=carId,litres=1};
                var fuelQuote=PaidEconomy.Quote(next,catalog,operating,prices,fuelRequest,now);
                int afterReplacementCash=next.cash;string dealerSnapshot=JsonUtility.ToJson(next.inventory);
                PlayerProfile afterFuel;string fuelError;
                bool fuelCommitted=MechanicsTransactions.Try(next,catalog,next.revision,
                    state=>PaidEconomy.Pay(state,catalog,operating,prices,fuelQuote,now),
                    state=>true,out afterFuel,out fuelError);
                check(fuelCommitted&&afterFuel.cash==afterReplacementCash-fuelQuote.Cost
                    &&JsonUtility.ToJson(afterFuel.inventory)==dealerSnapshot
                    &&afterFuel.economyReceipts.Count==next.economyReceipts.Count+1,
                    v.spec.id+" quick replacement followed by liquid checkout does not alter dealer inventory or double charge: "+fuelError);
                foreach(var stock in p.inventory.market.parts)stock.units=0;var express=MaintenanceReplacements.Preview(p,carId,false);check(express.deliveryFee>0&&express.localUnits==0&&express.cost==PartCart.Total(p,carId,express.lines),v.spec.id+" absent stock adds only the shared express price");
                var single=MaintenanceReplacements.Preview(p,carId,false,15,new[]{candidates.First(t=>t.position==old).key});check(single.lines.Count==1&&single.lines[0].action==CartAction.NewUnits&&single.lines[0].quantity==1,v.spec.id+" manual deselection leaves one exact individual replacement");
                var basket=new[]{new PartCartLine{action=CartAction.NewSet,definitionId=brakes.definitionId,quantity=2},single.lines[0].Copy()};
                string basketBefore=Rows(basket);before=JsonUtility.ToJson(p);
                var merged=MaintenanceReplacements.AddToCart(p,carId,express,basket);
                check(merged.lines.Single(l=>l.definitionId==brakes.definitionId).quantity==2&&merged.lines.Single(l=>l.definitionId==pistons.definitionId).quantity==2&&merged.lines.All(l=>l.install),v.spec.id+" maintenance reuses the existing spare order and unions exact cylinder positions");
                check(before==JsonUtility.ToJson(p)&&basketBefore==Rows(basket),v.spec.id+" merging is read-only for both profile and existing cart");
                string mergedRows=Rows(merged.lines);
                check(mergedRows==Rows(MaintenanceReplacements.AddToCart(p,carId,express,merged.lines).lines),v.spec.id+" repeated Add to cart does not buy duplicate replacements");
                check(merged.deliveryFee>0&&merged.cost==PartCart.Total(p,carId,merged.lines)&&merged.supply.Values.Sum(q=>q.fee)==merged.deliveryFee,v.spec.id+" merged review and per-offer supply use the actual delivered basket price");
                var fullPistons=new ReplacementPlan();fullPistons.lines.Add(new PartCartLine{action=CartAction.NewSet,definitionId=pistons.definitionId,install=true});
                var covered=MaintenanceReplacements.AddToCart(p,carId,fullPistons,single.lines);
                check(covered.lines.Count==1&&covered.lines[0].action==CartAction.NewSet&&MaintenanceReplacements.AddToCart(p,carId,single,covered.lines).lines.Count==1,v.spec.id+" a matching complete set supersedes selected units and already covers repeated individual replacements");
                var spare=single.lines[0].Copy();spare.quantity=3;var withSpares=MaintenanceReplacements.AddToCart(p,carId,fullPistons,new[]{spare});
                check(withSpares.lines.Single(l=>l.action==CartAction.NewUnits).quantity==2&&!withSpares.lines.Single(l=>l.action==CartAction.NewUnits).install,v.spec.id+" promoting to a complete set retains intentionally ordered loose spares");
                var conflicts=new[]{new PartCartLine{action=CartAction.NewSet,definitionId=catalog.Id(v.spec.id,PartCategory.Pistons,1),install=true}};string conflictBefore=Rows(conflicts);bool conflict=false;
                try{MaintenanceReplacements.AddToCart(p,carId,express,conflicts);}catch(InvalidOperationException){conflict=true;}
                check(conflict&&conflictBefore==Rows(conflicts)&&before==JsonUtility.ToJson(p),v.spec.id+" conflicting full-set and individual installation rejects the entire merge without losing existing rows");
                check(MechanicsTransactions.Try(p,catalog,p.revision,q=>CartReservations.Capture(q,carId,merged.lines),q=>true,out var reserved,out error),v.spec.id+" Add saves the shared reservation without buying: "+error);
                check(reserved.cash==p.cash&&reserved.selectedOwnedVehicleId==p.selectedOwnedVehicleId&&p.parts.All(i=>SnapshotEquality.Serialized(JsonUtility.ToJson(i),reserved.parts.Single(r=>r.instanceId==i.instanceId)))&&reserved.looseEngineElements.Count==p.looseEngineElements.Count&&reserved.economyReceipts.Count==p.economyReceipts.Count,v.spec.id+" staging spends no money, installs nothing and preserves selected car");
                check(CartReservations.Validate(reserved)==null&&PartCart.Total(reserved,carId,reserved.inventory.cart.lines)==merged.cost,v.spec.id+" captured reservation retains the reviewed price and valid stock ownership");
                var stagedQuote=PartCart.Quote(reserved,carId,reserved.inventory.cart.lines,now,300);
                check(MechanicsTransactions.Try(reserved,catalog,reserved.revision,q=>PartCart.Checkout(q,stagedQuote,now),q=>true,out var checkedOut,out error)&&checkedOut.cash==reserved.cash-merged.cost&&checkedOut.inventory.cart==null,v.spec.id+" reserved maintenance basket installs through the normal atomic checkout: "+error);
                check(!MechanicsTransactions.Try(p,catalog,p.revision,q=>CartReservations.Capture(q,carId,merged.lines),q=>false,out next,out error)&&before==JsonUtility.ToJson(p),v.spec.id+" failed staging persistence publishes no partial cart or resource changes");
                var poorDraft=EngineWorkshop.Copy(p);poorDraft.cash=0;check(MaintenanceReplacements.AddToCart(poorDraft,carId,express,basket).cost==merged.cost,v.spec.id+" no money is required to stage replacements for later checkout");
                // Existing held stock must not gain a false express fee when more targets are added.
                var held=EngineWorkshop.Copy(p);var stockRow=held.inventory.market.parts.First(s=>s.definitions.Contains(pistons.definitionId));stockRow.units=stockRow.initialUnits=1;
                CartReservations.Capture(held,carId,single.lines);var withHold=MaintenanceReplacements.AddToCart(held,carId,express,held.inventory.cart.lines);CartReservations.Capture(held,carId,withHold.lines);
                check(withHold.cost==PartCart.Total(held,carId,held.inventory.cart.lines)&&withHold.localUnits==1,v.spec.id+" merging honors previously reserved supply instead of charging express twice");
                var rejected=PartCart.Quote(p,carId,express.lines,now,300);before=JsonUtility.ToJson(p);check(!MechanicsTransactions.Try(p,catalog,p.revision,q=>PartCart.Checkout(q,rejected,now),q=>false,out next,out error)&&before==JsonUtility.ToJson(p),v.spec.id+" failed persistence rolls back cash, stock and every replacement");
                var all=EngineWorkshop.Copy(p);var allPack=all.parts.Find(i=>i.instanceId==pistons.instanceId);foreach(var e in allPack.condition.elements){e.health=.1f;e.fault=ElementFault.None;}EngineElementService.RefreshAggregate(allPack.condition);var allTargets=MaintenanceReplacements.Candidates(all,carId,false).Where(t=>t.partId==allPack.instanceId).Select(t=>t.key);var full=MaintenanceReplacements.Preview(all,carId,false,15,allTargets);check(full.lines.Count==1&&full.lines[0].action==CartAction.NewSet,v.spec.id+" selecting every matching member buys a complete set");
                all.cash=0;var poor=PartCart.Quote(all,carId,full.lines,now,300);before=JsonUtility.ToJson(all);check(!MechanicsTransactions.Try(all,catalog,all.revision,q=>PartCart.Checkout(q,poor,now),q=>true,out next,out error)&&before==JsonUtility.ToJson(all),v.spec.id+" insufficient funds cannot partly replace components");
            }
            var folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/maintenance-batch"));Directory.CreateDirectory(folder);log.AppendLine("Checks: "+count+"; failures: 0");File.WriteAllText(Path.Combine(folder,"verification.txt"),log.ToString(),new UTF8Encoding(false));Debug.Log("Maintenance replacement verification passed: "+count);
        }
    }
}

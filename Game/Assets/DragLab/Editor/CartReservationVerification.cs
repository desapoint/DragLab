using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;
namespace DragLab.Editor
{
    public static class CartReservationVerification
    {
        public static void Run()
        {
            var report=new StringBuilder();int checks=0;void Check(bool ok,string label){checks++;if(!ok)throw new InvalidOperationException(label);report.AppendLine("PASS "+label);}
            var catalog=MechanicsTransactions.Catalog();long now=DateTime.UtcNow.Ticks;
            foreach(int seed in new[]{12345,8841,7337}){
                var p=PlayerProfile.New(now);MechanicsOwnership.Ensure(p,catalog);p.cash=1000000;p.reputation=1000;VehicleListings.Initialize(p,catalog,seed);var car=p.SelectedCar;
                var head=catalog.Find(catalog.Id(car.vehicleId,PartCategory.HeadIntake,1));var piston=catalog.Find(catalog.Id(car.vehicleId,PartCategory.Pistons));var tire=catalog.Find(catalog.Id(car.vehicleId,PartCategory.Tires));
                foreach(var stock in p.inventory.market.parts)stock.units=0;
                void Supply(PartDefinition d,int quantity){var row=p.inventory.market.parts.Find(s=>s.definitions.Contains(d.id));row.initialUnits=row.units=quantity;}
                Supply(head,2);Supply(piston,2);Supply(tire,4);
                var worn=p.parts.Find(i=>i.slot==PartCategory.Cooling.ToString()).Copy();worn.instanceId="reserved-used/"+seed;worn.ownerId=worn.slot="";worn.location=PartLocation.Inventory;worn.condition.health=.42f;worn.condition.leakSeverity=.3;p.inventory.dealer.Add(new DealerStock{part=worn,paidPrice=100});
                var lines=new[]{new PartCartLine{definitionId=head.id,quantity=1},new PartCartLine{action=CartAction.NewUnits,definitionId=piston.id,role=InventoryUnits.Roles(p,car,piston).First(),quantity=2},new PartCartLine{definitionId=tire.id,quantity=1},new PartCartLine{action=CartAction.BuyUsed,definitionId=worn.definitionId,assetId=worn.instanceId,quantity=1}};
                int price=PartCart.Total(p,car.instanceId,lines);string used=JsonUtility.ToJson(p.inventory.dealer.Find(s=>s.Id==worn.instanceId));int parts=p.parts.Count,cash=p.cash;
                CartReservations.Capture(p,car.instanceId,lines);Check(p.inventory.cart.expiresRun==9&&p.cash==cash&&p.parts.Count==parts,"Seed "+seed+" reserves nine eligible races without buying or installing");
                Check(PartsMarket.Available(p,head)==1&&PartCart.Total(p,car.instanceId,lines)==price&&PartCart.DeliveryFee(p,car.instanceId,lines)==0,"Seed "+seed+" reserved stock is separate from the shelf and still quoted once");
                var separate=EngineWorkshop.Copy(p);var otherLines=new[]{new PartCartLine{definitionId=head.id}};var otherPreview=PartCart.PreviewProfile(separate,separate.SelectedCar,otherLines);Check(CartReservations.Active(otherPreview)&&CartReservations.Held(otherPreview,head)==1,"Seed "+seed+" unrelated purchase preview retains the cart hold");
                PartCart.Checkout(separate,PartCart.Quote(separate,car.instanceId,otherLines,now,300),now);separate.revision++;Check(CartReservations.Active(separate)&&CartReservations.Held(separate,head)==1&&PartsMarket.Available(separate,head)==0&&MechanicsOwnership.Validate(separate,catalog)==null,"Seed "+seed+" unrelated maintenance checkout consumes free supply without releasing or spending the cart hold / "+MechanicsOwnership.Validate(separate,catalog));
                var released=EngineWorkshop.Copy(p);CartReservations.Clear(released);Check(released.inventory.cart==null&&PartsMarket.Available(released,head)==2,"Seed "+seed+" clearing a same-epoch cart releases shelf quantities without purchasing");
                for(int race=1;race<=9;race++){VehicleListings.AfterRace(p,catalog);if(race%3==0){foreach(var stock in p.inventory.market.parts)stock.units=0;Check(CartReservations.Active(p)&&p.inventory.dealer.Any(s=>s.Id==worn.instanceId)&&PartCart.Total(p,car.instanceId,lines)==price,"Seed "+seed+" retains exact cart through refresh "+race/3);}}
                Check(used==JsonUtility.ToJson(p.inventory.dealer.Find(s=>s.Id==worn.instanceId)),"Seed "+seed+" reserved used identity, faults and condition are unchanged");
                p=EngineWorkshop.Copy(p);car=p.SelectedCar;Check(CartReservations.Active(p)&&p.inventory.cart.lines.Count==4&&PartCart.Total(p,car.instanceId,lines)==price,"Seed "+seed+" save roundtrip retains the complete cart and supply");
                var expired=EngineWorkshop.Copy(p);VehicleListings.AfterRace(expired,catalog);Check(!CartReservations.Active(expired)&&!expired.inventory.dealer.Any(s=>s.Id==worn.instanceId),"Seed "+seed+" next race expires the hold and removes old-epoch used stock");CartReservations.Capture(expired,car.instanceId,lines);Check(!CartReservations.Active(expired),"Seed "+seed+" an untouched expired cart does not automatically renew");
                var quote=PartCart.Quote(p,car.instanceId,lines,now,300);string before=JsonUtility.ToJson(p);PlayerProfile next;string error;
                Check(!MechanicsTransactions.Try(p,catalog,p.revision,q=>PartCart.Checkout(q,quote,now),q=>false,out next,out error)&&before==JsonUtility.ToJson(p),"Seed "+seed+" failed persistence preserves the reservation, stock and money");
                Check(MechanicsTransactions.Try(p,catalog,p.revision,q=>PartCart.Checkout(q,quote,now),q=>true,out next,out error),"Seed "+seed+" reserved mixed cart checkout succeeds: "+(error??"none"));
                Check(next.cash==cash-price&&next.inventory.cart==null&&next.parts.Any(i=>i.instanceId==worn.instanceId&&i.condition.health==.42f&&i.condition.leakSeverity==.3)&&MechanicsOwnership.Validate(next,catalog)==null,"Seed "+seed+" one charge transfers the retained item and releases the lease");
                var migration=EngineWorkshop.Copy(p);migration.saveVersion=8;migration.driving.guaranteedFittingParts=13;MechanicsOwnership.Ensure(migration,catalog);Check(migration.saveVersion==9&&migration.driving.guaranteedFittingParts==13,"Seed "+seed+" schema-8 migration retains custom market difficulty");
            }
            report.AppendLine("Checks: "+checks+"; failures: 0");string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/cart-reservations"));Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"verification.txt"),report.ToString(),new UTF8Encoding(false));Debug.Log("Cart reservations passed: "+checks);
        }
    }
}

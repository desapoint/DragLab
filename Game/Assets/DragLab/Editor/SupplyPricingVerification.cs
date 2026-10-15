using System;
using System.Collections.Generic;
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
    public static class SupplyPricingVerification
    {
        // Deliberately independent of PartsMarket.Calculate/Quote: test the
        // published 5–10% missing-supply fee, including per-purchase rounding.
        static int Expected(int price,int needed,int available,int percent)
        {
            long denominator=checked(100L*needed),numerator=checked((long)price*Math.Max(0,needed-available)*percent);
            return checked(price+(int)((numerator+denominator-1)/denominator));
        }
        static NewPartStock Pool(PlayerProfile p,PartDefinition d,int units,int percent)
        {
            string key=PartsMarket.ProductKey(d);
            var row=p.inventory.market.parts.First(s=>s.definitions.Any(id=>PartsMarket.ProductKey(p.catalog.Find(id))==key));
            row.units=units;row.initialUnits=Math.Max(row.initialUnits,units);row.deliveryPercent=percent;return row;
        }
        public static void OemDiagnostic()
        {
            var vehicles=Resources.LoadAll<VehicleDefinition>("Data/Vehicles");var catalog=MechanicsTransactions.Catalog();var log=new StringBuilder();
            foreach(var v in vehicles){
                var r=catalog.Resolve(v.spec.id,catalog.Recipe(v.spec.id),v.CreateStockTune());
                var raw=Benchmark.QuarterMile(v.spec,v.CreateStockTune());var resolved=Benchmark.QuarterMile(r.spec,r.tune);
                log.AppendLine(v.spec.id+" drivable="+r.Drivable+" raw ET="+raw.elapsed.ToString("R")+" resolved ET="+resolved.elapsed.ToString("R")+" raw trap="+raw.trapKmh.ToString("R")+" resolved trap="+resolved.trapKmh.ToString("R"));
                foreach(var field in typeof(VehicleSpec).GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance)){
                    var a=field.GetValue(v.spec);var b=field.GetValue(r.spec);string Describe(object value)=>value==null?"null":value.GetType().IsPrimitive||value.GetType().IsEnum||value is string?value.ToString():JsonUtility.ToJson(value);
                    string left=Describe(a),right=Describe(b);if(left!=right)log.AppendLine("  "+field.Name+": "+left+" -> "+right);
                }
                log.AppendLine("  raw tune="+JsonUtility.ToJson(v.CreateStockTune()));log.AppendLine("  resolved tune="+JsonUtility.ToJson(r.tune));
            }
            File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/branch-audit/pricing-oem-diagnostic.txt")),log.ToString(),new UTF8Encoding(false));
        }
        public static void VerifyRecordedPricing()
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/branch-audit/pricing"));
            var catalog=MechanicsTransactions.Catalog();var log=new StringBuilder();int count=0;
            try{
                foreach(string line in File.ReadAllLines(Path.Combine(folder,"pricing-matrix.csv"),Encoding.UTF8).Skip(1)){
                    var f=line.Split(',');string model=f[0],operation=f[3];int percent=int.Parse(f[1]),available=int.Parse(f[2]);var recipe=catalog.Recipe(model);
                    var piston=catalog.Find(recipe[PartCategory.Pistons]);int members=catalog.Spec(model).sound.cylinders,unit=Math.Max(1,(int)Math.Ceiling(piston.price/(double)members)),expected;
                    if(operation=="single-piston")expected=Expected(unit,1,available,percent);
                    else if(operation=="piston-set")expected=Expected(piston.price,members,available,percent);
                    else if(operation=="two-unit-cart")expected=Expected(unit,1,available,percent)+Expected(unit,1,Math.Max(0,available-1),percent);
                    else if(operation=="failed-shaft-service")expected=Expected(catalog.Find(recipe[PartCategory.Driveshaft]).price,1,available,percent);
                    else if(operation=="mixed-tire-service"){
                        expected=0;var remaining=new Dictionary<string,int>();
                        for(int corner=0;corner<4;corner++){
                            var d=catalog.Find(corner<2?recipe[PartCategory.Tires]:catalog.Id(model,PartCategory.Tires,3));string key=PartsMarket.ProductKey(d);
                            if(!remaining.ContainsKey(key))remaining[key]=available;
                            expected+=Expected(Math.Max(1,(int)Math.Ceiling(d.price/4.0)),1,remaining[key],percent);remaining[key]=Math.Max(0,remaining[key]-1);
                        }
                    }else throw new InvalidOperationException("Unknown pricing case: "+operation);
                    bool ok=int.Parse(f[5])==expected&&int.Parse(f[6])==expected&&int.Parse(f[7])==expected;
                    log.AppendLine((ok?"PASS: ":"FAIL: ")+model+" / "+operation+" / fee="+percent+" local="+available+" exact integer total="+expected);
                    if(!ok)throw new InvalidOperationException("Recorded quote/charge disagrees with exact integer fee arithmetic: "+line);count++;
                }
                if(count!=1170)throw new InvalidOperationException("Incomplete pricing matrix: "+count);
            }finally{File.WriteAllText(Path.Combine(folder,"exact-recorded-verification.txt"),log+"Checks: "+count+"\n",new UTF8Encoding(false));}
            Debug.Log("EXACT RECORDED PRICING PASS: "+count);
        }
        public static void Run()
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/branch-audit/pricing"));Directory.CreateDirectory(folder);
            var log=new StringBuilder();var matrix=new StringBuilder("model,fee_percent,local_units,operation,base,expected,quoted,charged\n");int checks=0;
            void Check(bool ok,string label){log.AppendLine((ok?"PASS: ":"FAIL: ")+label);if(!ok)throw new InvalidOperationException(label);checks++;}
            var catalog=MechanicsTransactions.Catalog();var costs=new OperatingPolicy();var values=ValuationPolicy.Defaults();
            try
            {
                foreach(var vehicle in Resources.LoadAll<VehicleDefinition>("Data/Vehicles"))
                {
                    long now=DateTime.UtcNow.Ticks;var basis=PlayerProfile.New(now);MechanicsOwnership.Ensure(basis,catalog);basis.cash=1000000;basis.reputation=1000;
                    VehicleCollection.Buy(basis,catalog,new VehicleOffer{model=vehicle.spec.id,price=1,reputation=0,stock=vehicle.CreateStockTune(),paint="#FFFFFF"});
                    string carId=basis.SelectedCar.instanceId;
                    PartInstance Part(PlayerProfile p,PartCategory c)=>p.parts.Single(i=>i.instanceId==p.SelectedCar.installed.slots.First(s=>s.slot==c.ToString()).partInstanceId);
                    var piston=catalog.Find(Part(basis,PartCategory.Pistons).definitionId);int count=Part(basis,PartCategory.Pistons).condition.elements.Length;
                    foreach(int percent in Enumerable.Range(5,6))foreach(int available in new[]{0,1,count})
                    {
                        string label=vehicle.spec.id+" fee="+percent+" local="+available+" / ";
                        var p=EngineWorkshop.Copy(basis);var pack=Part(p,PartCategory.Pistons);Pool(p,piston,available,percent);
                        pack.condition.elements[0].health=.1f;EngineElementService.RefreshAggregate(pack.condition);
                        string oldId=pack.condition.elements[0].id;string[] neighbours=pack.condition.elements.Skip(1).Select(e=>JsonUtility.ToJson(e)).ToArray();
                        int baseUnit=Math.Max(1,(int)Math.Ceiling(piston.price/(double)count)),expected=Expected(baseUnit,1,available,percent),cash=p.cash;
                        var request=new PaymentRequest{kind=PaymentKind.ElementNew,carId=carId,targetId=pack.instanceId,auxiliaryId=oldId};
                        var preview=PaidEconomy.Preview(p,catalog,costs,values,request,now);var quote=PaidEconomy.Quote(p,catalog,costs,values,request,now);
                        Check(preview.cost==expected&&quote.Cost==expected,label+"individual displayed/payment quote matches independent fee calculation");
                        PlayerProfile paid;string error;int writes=0;
                        bool ok=MechanicsTransactions.Try(p,catalog,p.revision,q=>PaidEconomy.Pay(q,catalog,costs,values,quote,now),q=>{writes++;return true;},out paid,out error);
                        Check(ok&&writes==1&&paid.cash==cash-expected,label+"individual confirmation charges exactly once / "+error);
                        var fitted=Part(paid,PartCategory.Pistons);
                        Check(fitted.condition.elements[0].health==1&&fitted.condition.elements[0].id!=oldId&&neighbours.SequenceEqual(fitted.condition.elements.Skip(1).Select(e=>JsonUtility.ToJson(e)))&&paid.looseEngineElements.Any(u=>u.element.id==oldId&&u.element.health==.1f),label+"physical member replacement preserves neighbours and removed worn identity");
                        matrix.AppendLine(vehicle.spec.id+","+percent+","+available+",single-piston,"+baseUnit+","+expected+","+quote.Cost+","+(cash-paid.cash));

                        p=EngineWorkshop.Copy(basis);Pool(p,piston,available,percent);cash=p.cash;
                        expected=Expected(piston.price,count,available,percent);var supply=PartsMarket.Quote(p,piston,count);
                        string bought=PartOperations.Buy(p,catalog,carId,piston.id,false);
                        Check(supply.cost==expected&&p.cash==cash-expected&&p.parts.Any(i=>i.instanceId==bought&&i.location==PartLocation.Inventory),label+"complete set quotes and charges missing fraction only");
                        matrix.AppendLine(vehicle.spec.id+","+percent+","+available+",piston-set,"+piston.price+","+expected+","+supply.cost+","+(cash-p.cash));

                        p=EngineWorkshop.Copy(basis);pack=Part(p,PartCategory.Pistons);Pool(p,piston,available,percent);cash=p.cash;
                        expected=Expected(baseUnit,1,available,percent)+Expected(baseUnit,1,Math.Max(0,available-1),percent);
                        var lines=new[]{new PartCartLine{action=CartAction.NewUnits,definitionId=piston.id,role=pack.condition.elements[0].role,quantity=2,install=true,positions=pack.condition.elements.Take(2).Select(e=>e.id).ToList()}};
                        var cart=PartCart.Quote(p,carId,lines,now,300);writes=0;
                        ok=MechanicsTransactions.Try(p,catalog,p.revision,q=>PartCart.Checkout(q,cart,now),q=>{writes++;return true;},out paid,out error);
                        Check(cart.total==expected&&ok&&writes==1&&paid.cash==cash-expected,label+"multi-unit cart depletes shared stock sequentially and charges rounded fees once / "+error);
                        matrix.AppendLine(vehicle.spec.id+","+percent+","+available+",two-unit-cart,"+(2*baseUnit)+","+expected+","+cart.total+","+(cash-paid.cash));

                        p=EngineWorkshop.Copy(basis);var shaft=Part(p,PartCategory.Driveshaft);var shaftDefinition=catalog.Find(shaft.definitionId);Pool(p,shaftDefinition,available,percent);
                        shaft.condition.failed=true;shaft.condition.failureReason="Pricing fixture";cash=p.cash;string shaftId=shaft.instanceId;
                        expected=Expected(shaftDefinition.price,1,available,percent);
                        request=new PaymentRequest{kind=PaymentKind.Part,carId=carId,targetId=shaftDefinition.id,install=true};quote=PaidEconomy.Quote(p,catalog,costs,values,request,now);
                        ok=MechanicsTransactions.Try(p,catalog,p.revision,q=>PaidEconomy.Pay(q,catalog,costs,values,quote,now),q=>true,out paid,out error);
                        Check(ok&&quote.Cost==expected&&paid.cash==cash-expected,label+"failed shaft Buy same charges the independent single-part supply total / "+error);
                        Check(Part(paid,PartCategory.Driveshaft).instanceId!=shaftId&&!Part(paid,PartCategory.Driveshaft).condition.failed&&paid.parts.Single(i=>i.instanceId==shaftId).condition.failed&&paid.parts.Single(i=>i.instanceId==shaftId).location==PartLocation.Inventory,label+"repair checkout fits a working shaft and retains the failed original");
                        matrix.AppendLine(vehicle.spec.id+","+percent+","+available+",failed-shaft-service,"+shaftDefinition.price+","+expected+","+quote.Cost+","+(cash-paid.cash));

                        p=EngineWorkshop.Copy(basis);var tires=Part(p,PartCategory.Tires);var front=catalog.Find(tires.definitionId);var rear=catalog.Find(catalog.Id(vehicle.spec.id,PartCategory.Tires,3));
                        for(int corner=2;corner<4;corner++)tires.condition.tires[corner].definitionId=rear.id;
                        Pool(p,front,available,percent);Pool(p,rear,available,percent);cash=p.cash;
                        var remaining=new Dictionary<NewPartStock,int>();expected=0;
                        for(int corner=0;corner<4;corner++){
                            var d=corner<2?front:rear;var pool=p.inventory.market.parts.First(s=>s.definitions.Contains(d.id));
                            if(!remaining.ContainsKey(pool))remaining[pool]=pool.units;
                            expected+=Expected(Math.Max(1,(int)Math.Ceiling(d.price/4.0)),1,remaining[pool],percent);remaining[pool]=Math.Max(0,remaining[pool]-1);
                        }
                        request=new PaymentRequest{kind=PaymentKind.Part,carId=carId,targetId=tires.definitionId,install=true,preserveTireFitments=true};
                        quote=PaidEconomy.Quote(p,catalog,costs,values,request,now);string oldTires=tires.instanceId,oldState=JsonUtility.ToJson(tires.condition);
                        ok=MechanicsTransactions.Try(p,catalog,p.revision,q=>PaidEconomy.Pay(q,catalog,costs,values,quote,now),q=>true,out paid,out error);
                        Check(ok&&quote.Cost==expected&&paid.cash==cash-expected,label+"mixed Buy same agrees with independent corner-by-corner price / "+error);
                        var newTires=Part(paid,PartCategory.Tires);
                        Check(Enumerable.Range(0,4).All(i=>TireFitments.DefinitionId(newTires,i)==(i<2?front.id:rear.id))&&paid.parts.Single(i=>i.instanceId==oldTires).location==PartLocation.Inventory&&JsonUtility.ToJson(paid.parts.Single(i=>i.instanceId==oldTires).condition)==oldState,label+"mixed Buy same retains exact compounds and removed condition");
                        matrix.AppendLine(vehicle.spec.id+","+percent+","+available+",mixed-tire-service,0,"+expected+","+quote.Cost+","+(cash-paid.cash));
                    }
                }
            }
            finally
            {
                File.WriteAllText(Path.Combine(folder,"verification.txt"),log+"Checks: "+checks+"\n",new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(folder,"pricing-matrix.csv"),matrix.ToString(),new UTF8Encoding(false));
            }
            Debug.Log("SUPPLY PRICING PASS: "+checks);
        }
    }
}

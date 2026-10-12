using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DragLab.Data;
namespace DragLab.Domain.Mechanics {
 [Serializable] public sealed class VehicleStockState {public int version=1,seed,generation,purchases,completedRuns;public List<VehicleOffer> listings=new List<VehicleOffer>();}
 public static class VehicleListings {
 static readonly string[] starters={"starter-hatch","juniper-economy","juniper-club"};
 public static OperatingPolicy Costs=>Resources.Load<OperatingPolicyDefinition>("Data/Economy/OperatingPolicy")?.policy??new OperatingPolicy();
 public static ValuationPolicy Values=>Resources.Load<ValuationPolicyDefinition>("Data/Economy/ValuationPolicy")?.policy??ValuationPolicy.Defaults();
 public static void Ensure(PlayerProfile p,MechanicalCatalog c){if(p.vehicleStock!=null)return;Initialize(p,c,Guid.NewGuid().GetHashCode());}
 public static void Initialize(PlayerProfile p,MechanicalCatalog c,int seed){p.vehicleStock=new VehicleStockState{seed=seed==0?1:seed};Generate(p,c);}
 public static VehicleOffer Find(PlayerProfile p,string id)=>p.vehicleStock?.listings.FirstOrDefault(o=>o.listingId==id);
 public static void RequireCurrent(PlayerProfile p,VehicleOffer offer){if(offer==null)throw new InvalidOperationException("Missing offer.");if(string.IsNullOrEmpty(offer.listingId)){if(p.firstPurchasePending)throw new InvalidOperationException("Choose one of the fixed starter listings first.");return;}var actual=Find(p,offer.listingId);if(actual==null||!SnapshotEquality.Serialized(JsonUtility.ToJson(actual),offer))throw new InvalidOperationException("Listing sold or changed. Reopen the dealership.");}
 public static void Consume(PlayerProfile p,MechanicalCatalog c,VehicleOffer offer){
 if(string.IsNullOrEmpty(offer.listingId))return;
 var stock=p.vehicleStock;stock.listings.RemoveAll(o=>o.listingId==offer.listingId);stock.purchases++;
 if(p.firstPurchasePending){p.firstPurchasePending=false;stock.generation++;Generate(p,c);}
 else if(!offer.used){var v=Resources.LoadAll<VehicleDefinition>("Data/Vehicles").Single(a=>a.spec.id==offer.model);uint seed=1;stock.listings.Add(Create(c,v,offer.model,false,1,0,stock.generation,stock.purchases,stock.seed,ref seed));}
 }
 public static void AfterRace(PlayerProfile p,MechanicalCatalog c){if(p.firstPurchasePending)return;Ensure(p,c);p.vehicleStock.completedRuns++;if(p.vehicleStock.completedRuns%3==0){p.vehicleStock.generation++;Generate(p,c);}}
 static double Next(ref uint seed){seed^=seed<<13;seed^=seed>>17;seed^=seed<<5;return seed/(double)uint.MaxValue;}
 static void Generate(PlayerProfile p,MechanicalCatalog c){
 var stock=p.vehicleStock;stock.listings.Clear();var cars=Resources.LoadAll<VehicleDefinition>("Data/Vehicles").OrderBy(v=>v.spec.id,StringComparer.Ordinal).ToArray();uint seed=(uint)stock.seed^(uint)(stock.generation+1)*2654435761u;if(seed==0)seed=1;
 if(p.firstPurchasePending){for(int i=0;i<starters.Length;i++){var v=cars.Single(a=>a.spec.id==starters[i]);uint fixedSeed=(uint)(101+i);var offer=Create(c,v,"starter:"+v.spec.id,true,new[]{.96,.97,.96}[i],new[]{55000.0,32000,76000}[i],0,0,stock.seed,ref fixedSeed,true);offer.reputation=0;stock.listings.Add(offer);}return;}
 foreach(var v in cars)stock.listings.Add(Create(c,v,v.spec.id,false,1,0,stock.generation,stock.purchases,stock.seed,ref seed));
 for(int i=0;i<7;i++){var v=cars[(int)(Next(ref seed)*cars.Length)%cars.Length];double health=.58+Next(ref seed)*.39;double km=10000+Next(ref seed)*180000;stock.listings.Add(Create(c,v,"used:"+stock.generation+":"+i,true,health,km,stock.generation,stock.purchases,stock.seed,ref seed));}
 }
 static VehicleOffer Create(MechanicalCatalog c,VehicleDefinition v,string id,bool used,double health,double km,int generation,int purchases,int marketSeed,ref uint seed,bool starter=false){
 var map=c.Recipe(v.spec.id);if(used&&!starter&&Next(ref seed)>.55){map[PartCategory.HeadIntake]=c.Id(v.spec.id,PartCategory.HeadIntake,1);map[PartCategory.Exhaust]=c.Id(v.spec.id,PartCategory.Exhaust,1);}if(used&&!starter&&Next(ref seed)>.75)map.Remove(PartCategory.Interior);
 var build=c.Resolve(v.spec.id,map,v.CreateStockTune());string identity=marketSeed+"/"+id+"/"+generation+"/"+purchases;
 var offer=new VehicleOffer{listingId=id,model=v.spec.id,stock=build.tune.Copy(),paint="#"+ColorUtility.ToHtmlStringRGB(v.defaultPaint),used=used,mileageKm=Math.Round(km),price=Math.Max(1,v.purchasePrice),reputation=v.reputationRequired,parts=new List<PartInstance>(),fuel=new FuelState{litres=build.spec.fuel.capacityLitres*(used?.35:1)},fluids=FluidState.Full(build.spec.fluids)};
 if(used)offer.fluids.oilCondition=starter?.9:.55+Next(ref seed)*.4;
 foreach(var entry in map){var d=c.Find(entry.Value);double h=PartSemantics.Wears(d.category)?DragMath.Clamp(health+(used&&!starter?(Next(ref seed)-.5)*.15:0),.4,1):1;var part=new PartInstance{instanceId=identity+"/"+entry.Key,definitionId=d.id,slot=entry.Key.ToString(),condition=new ComponentCondition{health=(float)h,distanceM=km*1000,runningSeconds=km/45*3600}};EngineElements.Ensure(build.spec,d,part);ClutchHardware.Ensure(d,part.condition);if(d.category==PartCategory.Tires){TireDynamics.Ensure(part.condition);foreach(var tire in part.condition.tires){tire.health=(float)h;tire.consumedLife=1-h;}}offer.parts.Add(part);}
 // Exact condition/history floor prevents instant part-out profit. Deal spread varies above it.
 offer.price=1;int floor=PaidEconomy.DealerPrice(c,Costs,Values,offer);double spread=starter?1:used?1.02+Next(ref seed)*.24:1;offer.price=(int)Math.Ceiling(Math.Max(floor,v.purchasePrice*(used?.72:1))*spread);return offer;
 }
 public static string Validate(PlayerProfile p,MechanicalCatalog c){var stock=p.vehicleStock;if(stock==null||stock.version!=1||stock.seed==0||stock.generation<0||stock.purchases<0||stock.completedRuns<0||stock.listings==null||stock.listings.Count>100)return "Invalid vehicle market.";var ids=new HashSet<string>();var components=new HashSet<string>(p.parts.Select(i=>i.instanceId));
 foreach(var o in stock.listings){if(o==null||string.IsNullOrEmpty(o.listingId)||!ids.Add(o.listingId)||o.price<=0||o.reputation<0||o.stock==null||o.parts==null||o.fuel==null||o.fluids==null||!MechanicsSchema.Finite(o.mileageKm)||o.mileageKm<0)return "Invalid vehicle listing.";var spec=c.Spec(o.model);var map=new Dictionary<PartCategory,string>();foreach(var part in o.parts){var d=c.Find(part.definitionId);if(d==null||!c.Fits(spec,d)||!components.Add(part.instanceId)||part.condition.Validate()!=null||map.ContainsKey(d.category))return "Invalid listing component identity/fitment/condition.";map.Add(d.category,d.id);}var build=c.Resolve(o.model,map,o.stock);if(!build.Drivable||o.fuel.Validate(build.spec.fuel)!=null||o.fluids.Validate(build.spec.fluids)!=null)return "Invalid listing build/resources.";}
 return null;}
 }
}

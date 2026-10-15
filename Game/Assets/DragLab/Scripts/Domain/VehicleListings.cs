using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DragLab.Data;
namespace DragLab.Domain.Mechanics {
 [Serializable] public sealed class VehicleStockState {public int version=1,seed,generation,purchases,completedRuns,knowledgeRevision;public List<VehicleOffer> listings=new List<VehicleOffer>();}
 public static class VehicleListings {
 static readonly string[] starters={"starter-hatch","juniper-economy","juniper-club"};
 static readonly System.Runtime.CompilerServices.ConditionalWeakTable<MechanicalCatalog,HashSet<string>> warmed=new System.Runtime.CompilerServices.ConditionalWeakTable<MechanicalCatalog,HashSet<string>>();
 public static OperatingPolicy Costs=>Resources.Load<OperatingPolicyDefinition>("Data/Economy/OperatingPolicy")?.policy??new OperatingPolicy();
 public static ValuationPolicy Values=>Resources.Load<ValuationPolicyDefinition>("Data/Economy/ValuationPolicy")?.policy??ValuationPolicy.Defaults();
 public static void Ensure(PlayerProfile p,MechanicalCatalog c){if(p.vehicleStock!=null){var offers=p.vehicleStock.listings??new List<VehicleOffer>();foreach(var offer in offers)VehicleInspection.Migrate(c,offer,p.firstPurchasePending);
 // Add price metadata to old new-car listings without rerolling asks, contents or identities.
 if(offers.Any(o=>!o.used&&o.cataloguePrice==0)){var prices=Resources.LoadAll<VehicleDefinition>("Data/Vehicles").ToDictionary(v=>v.spec.id,v=>v.purchasePrice);foreach(var offer in offers.Where(o=>!o.used&&o.cataloguePrice==0))offer.cataloguePrice=prices.TryGetValue(offer.model,out var price)?Math.Max(1,price):offer.price;}
 return;}Initialize(p,c,Guid.NewGuid().GetHashCode());}
 public static void Initialize(PlayerProfile p,MechanicalCatalog c,int seed){p.vehicleStock=new VehicleStockState{seed=seed==0?1:seed};Generate(p,c);}
 public static VehicleOffer Find(PlayerProfile p,string id)=>p.vehicleStock?.listings.FirstOrDefault(o=>o.listingId==id);
 public static void RequireCurrent(PlayerProfile p,VehicleOffer offer){if(offer==null)throw new InvalidOperationException("Missing offer.");if(string.IsNullOrEmpty(offer.listingId)){if(p.firstPurchasePending)throw new InvalidOperationException("Choose one of the fixed starter listings first.");return;}var actual=Find(p,offer.listingId);if(actual==null||!SnapshotEquality.Serialized(JsonUtility.ToJson(actual),offer))throw new InvalidOperationException("Listing sold or changed. Reopen the dealership.");}
 public static void Consume(PlayerProfile p,MechanicalCatalog c,VehicleOffer offer){
 if(string.IsNullOrEmpty(offer.listingId))return;
 var stock=p.vehicleStock;stock.listings.RemoveAll(o=>o.listingId==offer.listingId);stock.purchases++;
 if(p.firstPurchasePending){p.firstPurchasePending=false;stock.generation++;Generate(p,c);}
 else if(!offer.used){var v=Resources.LoadAll<VehicleDefinition>("Data/Vehicles").Single(a=>a.spec.id==offer.model);uint seed=1;stock.listings.Add(Create(c,v,offer.model,false,1,0,stock.generation,stock.purchases,stock.seed,ref seed));}
 }
 public static void AfterRace(PlayerProfile p,MechanicalCatalog c){if(p.firstPurchasePending)return;Ensure(p,c);p.vehicleStock.completedRuns++;CartReservations.Expire(p);if(p.vehicleStock.completedRuns%3==0){p.vehicleStock.generation++;Generate(p,c);}}
 public static System.Collections.IEnumerable WarmNext(PlayerProfile p){
 var cars=Resources.LoadAll<VehicleDefinition>("Data/Vehicles").OrderBy(v=>v.spec.id,StringComparer.Ordinal).ToArray();int generation=p.vehicleStock.generation+1,purchases=p.vehicleStock.purchases+(p.firstPurchasePending?1:0),marketSeed=p.vehicleStock.seed;var catalog=p.catalog;uint seed=(uint)marketSeed^(uint)(generation+1)*2654435761u;if(seed==0)seed=1;
 foreach(var unused in WarmGeneration(catalog,cars,generation,purchases,marketSeed,seed))yield return null;
 }
 static System.Collections.IEnumerable WarmGeneration(MechanicalCatalog c,VehicleDefinition[] cars,int generation,int purchases,int marketSeed,uint seed){
 var scheduled=warmed.GetValue(c,_=>new HashSet<string>());string key=marketSeed+"/"+generation+"/"+seed;if(scheduled.Contains(key))yield break;
 for(int i=0;i<7;i++){var v=cars[(int)(Next(ref seed)*cars.Length)%cars.Length];double health=.58+Next(ref seed)*.39;double km=10000+Next(ref seed)*180000;Create(c,v,"used:"+generation+":"+i,true,health,km,generation,purchases,marketSeed,ref seed,false,true);yield return null;}
 if(scheduled.Count>=8)scheduled.Clear();scheduled.Add(key);
 }
 static double Next(ref uint seed){seed^=seed<<13;seed^=seed>>17;seed^=seed<<5;return seed/(double)uint.MaxValue;}
 static void Generate(PlayerProfile p,MechanicalCatalog c){
 p.catalog=c;PartsMarket.Refresh(p);
 var stock=p.vehicleStock;stock.listings.Clear();var cars=Resources.LoadAll<VehicleDefinition>("Data/Vehicles").OrderBy(v=>v.spec.id,StringComparer.Ordinal).ToArray();uint seed=(uint)stock.seed^(uint)(stock.generation+1)*2654435761u;if(seed==0)seed=1;
 if(p.firstPurchasePending){for(int i=0;i<starters.Length;i++){var v=cars.Single(a=>a.spec.id==starters[i]);uint fixedSeed=(uint)(101+i);var offer=Create(c,v,"starter:"+v.spec.id,true,new[]{.96,.97,.96}[i],new[]{55000.0,32000,76000}[i],0,0,stock.seed,ref fixedSeed,true);offer.reputation=0;stock.listings.Add(offer);}return;}
 foreach(var v in cars)stock.listings.Add(Create(c,v,v.spec.id,false,1,0,stock.generation,stock.purchases,stock.seed,ref seed));
 // On a cold miss, launch the seven independent inspections before awaiting any result.
 foreach(var unused in WarmGeneration(c,cars,stock.generation,stock.purchases,stock.seed,seed)){}
 for(int i=0;i<7;i++){var v=cars[(int)(Next(ref seed)*cars.Length)%cars.Length];double health=.58+Next(ref seed)*.39;double km=10000+Next(ref seed)*180000;stock.listings.Add(Create(c,v,"used:"+stock.generation+":"+i,true,health,km,stock.generation,stock.purchases,stock.seed,ref seed));}
 }
 static VehicleOffer Create(MechanicalCatalog c,VehicleDefinition v,string id,bool used,double health,double km,int generation,int purchases,int marketSeed,ref uint seed,bool starter=false,bool warm=false){
 var map=c.Recipe(v.spec.id);if(used&&!starter&&Next(ref seed)>.55){map[PartCategory.HeadIntake]=c.Id(v.spec.id,PartCategory.HeadIntake,1);map[PartCategory.Exhaust]=c.Id(v.spec.id,PartCategory.Exhaust,1);}if(used&&!starter&&Next(ref seed)>.75)map.Remove(PartCategory.Interior);
 var build=c.Resolve(v.spec.id,map,v.CreateStockTune());string identity=marketSeed+"/"+id+"/"+generation+"/"+purchases;
 var offer=new VehicleOffer{listingId=id,model=v.spec.id,stock=build.tune.Copy(),paint="#"+ColorUtility.ToHtmlStringRGB(v.defaultPaint),used=used,disclosureVersion=1,inspected=!used||starter,firstStarter=starter,estimateSeed=(int)seed,mileageKm=Math.Round(km),price=Math.Max(1,v.purchasePrice),cataloguePrice=Math.Max(1,v.purchasePrice),reputation=v.reputationRequired,parts=new List<PartInstance>(),fuel=new FuelState{litres=build.spec.fuel.capacityLitres*(used?.35:1)},fluids=FluidState.Full(build.spec.fluids)};
 if(used)offer.fluids.oilCondition=starter?.9:.55+Next(ref seed)*.4;
 foreach(var entry in map){var d=c.Find(entry.Value);double h=PartSemantics.Wears(d.category)?DragMath.Clamp(health+(used&&!starter?(Next(ref seed)-.5)*.15:0),.4,1):1;var part=new PartInstance{instanceId=identity+"/"+entry.Key,definitionId=d.id,slot=entry.Key.ToString(),condition=new ComponentCondition{health=(float)h,distanceM=km*1000,runningSeconds=km/45*3600}};EngineElements.Ensure(build.spec,d,part);ClutchHardware.Ensure(d,part.condition);if(d.category==PartCategory.Tires){TireDynamics.SeedNew(part.condition);for(int i=0;i<4;i++){var tire=part.condition.tires[i];tire.id=part.instanceId+"/tire/"+i;tire.definitionId=d.id;}}offer.parts.Add(part);}
 if(used&&!starter){
 double roll=Next(ref seed);offer.historyNotes.Add(roll<.28?"Repeated high-load use":roll<.55?"Neglected maintenance":roll<.75?"Previous overheating":"Routine road use");
 offer.fuel.litres=build.spec.fuel.capacityLitres*(.03+Next(ref seed)*.82);
 offer.fluids.oilLitres=build.spec.fluids.oilCapacityLitres*(.25+Next(ref seed)*.75);
 offer.fluids.coolantLitres=build.spec.fluids.coolantCapacityLitres*(.25+Next(ref seed)*.75);
 if(roll<.28){var categories=new[]{PartCategory.Brakes,PartCategory.Driveshaft,PartCategory.Clutch,PartCategory.OilPump,PartCategory.Pistons};var category=categories[(int)(Next(ref seed)*categories.Length)%categories.Length];var damaged=offer.parts.Single(p=>p.slot==category.ToString());damaged.condition.failed=true;damaged.condition.health=.15f;damaged.condition.failureReason=category==PartCategory.Pistons?"Previous engine seizure":"Unrepaired "+PartSemantics.Friendly(category.ToString())+" failure";damaged.condition.stressExposure=1500;foreach(var e in damaged.condition.elements){e.health=0;if(category==PartCategory.Pistons){e.fault=ElementFault.Seized;e.severity=1;}}}
 else if(roll<.55){var cooling=offer.parts.Single(p=>p.slot=="Cooling");cooling.condition.leakSeverity=.35+Next(ref seed)*.45;cooling.condition.lastService="Leak left unrepaired";}
 else if(roll<.75){var rings=offer.parts.Single(p=>p.slot=="PistonRings");rings.condition.elements[0].fault=ElementFault.CompressionLeak;rings.condition.elements[0].severity=.55f;rings.condition.stressExposure=1000;}
 }
 if(starter)offer.historyNotes.Add("Documented starter service");
 offer.reportedCondition=VehicleInspection.Overall(c,offer);
 TireFitments.Capture(build.spec,offer.parts,c);
 if(!used||starter)offer.engineRuns=offer.drives=true;else if(warm)VehicleAssessmentCache.Prepare(c,offer,build);else VehicleInspection.Assess(c,offer,build);
 // Exact condition/history floor prevents instant part-out profit. Deal spread varies above it.
 double spread=starter?1:used?1.02+Next(ref seed)*.24:1;if(warm)return offer;offer.price=1;int floor=PaidEconomy.DealerPrice(c,Costs,Values,offer);offer.price=(int)Math.Ceiling(Math.Max(floor,v.purchasePrice*(used?(offer.drives?.72:.35):1))*spread);return offer;
 }
 public static string Validate(PlayerProfile p,MechanicalCatalog c){var stock=p.vehicleStock;if(stock==null||stock.version!=1||stock.seed==0||stock.generation<0||stock.purchases<0||stock.completedRuns<0||stock.knowledgeRevision<0||stock.listings==null||stock.listings.Count>100)return "Invalid vehicle market.";if(p.firstPurchasePending&&(stock.listings.Count!=3||stock.listings.Any(o=>o==null||!o.firstStarter)))return "Initial market must contain the three fully disclosed starter choices.";var ids=new HashSet<string>();var components=new HashSet<string>(p.parts.Select(i=>i.instanceId));
 foreach(var o in stock.listings){if(o==null||string.IsNullOrEmpty(o.listingId)||!ids.Add(o.listingId)||o.price<=0||o.cataloguePrice<0||o.reputation<0||o.stock==null||o.parts==null||o.fuel==null||o.fluids==null||!MechanicsSchema.Finite(o.mileageKm)||o.mileageKm<0)return "Invalid vehicle listing.";if(VehicleInspection.Validate(o)!=null)return VehicleInspection.Validate(o);var spec=c.Spec(o.model);var map=new Dictionary<PartCategory,string>();foreach(var part in o.parts){var d=c.Find(part.definitionId);if(d==null||!c.Fits(spec,d)||!components.Add(part.instanceId)||part.condition.Validate()!=null||map.ContainsKey(d.category))return "Invalid listing component identity/fitment/condition.";map.Add(d.category,d.id);}var build=c.Resolve(o.model,map,o.stock);if(!build.Drivable||o.fuel.Validate(build.spec.fuel)!=null||o.fluids.Validate(build.spec.fluids)!=null)return "Invalid listing build/resources.";}
 return null;}
 }
}

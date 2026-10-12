using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;
namespace DragLab.Editor
{
    public static class UsedVehicleInspectionVerification
    {
        public static void Prepare()
        {
            var asset=Resources.Load<OperatingPolicyDefinition>("Data/Economy/OperatingPolicy");
            if(asset.policy.version<3){asset.policy.vehicleInspectionFee=50;asset.policy.version=3;EditorUtility.SetDirty(asset);AssetDatabase.SaveAssets();}
        }
        static PlayerProfile Market(MechanicalCatalog cat,long now,int seed)
        {
            var p=PlayerProfile.NewGame(now);VehicleListings.Initialize(p,cat,seed);MechanicsOwnership.Ensure(p,cat);
            VehicleCollection.Buy(p,cat,p.vehicleStock.listings[0]);p.cash=100000;p.reputation=1000;return p;
        }
        public static void RunStandalone()
        {
            Prepare();var cat=MechanicsTransactions.Catalog();var policy=VehicleListings.Costs;var values=VehicleListings.Values;long now=DateTime.UtcNow.Ticks;
            int count=0;var log=new StringBuilder();Action<bool,string> check=(ok,label)=>{if(!ok)throw new InvalidOperationException(label);count++;log.AppendLine("PASS "+label);};
            var initial=PlayerProfile.NewGame(now);MechanicsOwnership.Ensure(initial,cat);string untouched=JsonUtility.ToJson(initial);
            foreach(var o in initial.vehicleStock.listings){check(o.firstStarter&&VehicleInspection.Known(o)&&o.engineRuns&&o.drives&&o.parts.All(p=>!p.condition.failed&&p.condition.health>=.9),o.model+" starter is safe and fully disclosed without a fee");bool rejected=false;try{PaidEconomy.Quote(initial,cat,policy,values,new PaymentRequest{kind=PaymentKind.VehicleInspection,offer=o},now);}catch(InvalidOperationException){rejected=true;}check(rejected,"Starter inspection cannot charge");}
            check(untouched==JsonUtility.ToJson(initial),"Rejected starter inspection changes no assets or cash");
            int broken=0;var fuel=new System.Collections.Generic.HashSet<double>();var oil=new System.Collections.Generic.HashSet<double>();var coolant=new System.Collections.Generic.HashSet<double>();
            for(int seed=1;seed<=8;seed++){
                var p=Market(cat,now,seed);check(MechanicsOwnership.Validate(p,cat)==null,"Seeded stock validates with hidden damage / "+seed);
                check(p.vehicleStock.listings.Where(o=>!o.used).All(o=>VehicleInspection.Known(o)&&o.parts.All(i=>!i.condition.failed&&i.condition.health==1)),"New vehicles remain exact and undamaged / "+seed);
                foreach(var o in p.vehicleStock.listings.Where(o=>o.used)){
                    if(o.parts.Any(i=>i.condition.failed))broken++;fuel.Add(o.fuel.litres);oil.Add(o.fluids.oilLitres);coolant.Add(o.fluids.coolantLitres);
                    string before=JsonUtility.ToJson(o);var view=VehicleInspection.PublicOffer(cat,o);var again=VehicleInspection.PublicOffer(cat,o);
                    check(!VehicleInspection.Known(o)&&view.parts.All(i=>!i.condition.failed&&i.condition.leakSeverity==0&&i.condition.elements.All(e=>e.fault==ElementFault.None))&&view.historyNotes.Count==0,"Public estimates contain no actual part faults/history / "+o.listingId);
                    check(JsonUtility.ToJson(view)==JsonUtility.ToJson(again)&&before==JsonUtility.ToJson(o),"Viewing cannot reroll estimates, modify truth or consume liquids / "+o.listingId);
                }
            }
            check(broken>0&&fuel.Count>20&&oil.Count>20&&coolant.Count>20,"Used stock spans broken parts and varied fuel/oil/coolant levels");
            var stripped=Market(cat,now,19);var strippedOffer=stripped.vehicleStock.listings.First(o=>!o.used);strippedOffer.parts.RemoveAll(p=>p.slot=="Interior"||p.slot=="Accessories");
            var exactPreview=VehicleCollection.Preview(cat,strippedOffer);
            check(exactPreview.parts.Count==strippedOffer.parts.Count&&exactPreview.parts.All(p=>strippedOffer.parts.Any(o=>o.instanceId==p.instanceId)),"Known preview preserves missing optional hardware and all exact identities");
            var preSource=InspectionBuild.Capture(exactPreview,exactPreview.SelectedCar);var preStats=CarInspection.CalculatePrepared(preSource,CarInspection.PrepareCar(preSource),false);
            VehicleCollection.Buy(stripped,cat,strippedOffer);var postSource=InspectionBuild.Capture(stripped,stripped.SelectedCar);var postStats=CarInspection.CalculatePrepared(postSource,CarInspection.PrepareCar(postSource),false);
            check(Math.Abs(preStats.vehicleMassKg-postStats.vehicleMassKg)<.001&&Math.Abs(preStats.peakHp-postStats.peakHp)<.001,"Known stripped-car inspection and purchase retain identical weight and output");
            var malformed=PlayerProfile.NewGame(now);MechanicsOwnership.Ensure(malformed,cat);malformed.vehicleStock.listings[0].firstStarter=false;
            check(MechanicsOwnership.Validate(malformed,cat)!=null,"First-car disclosure protection cannot be removed by malformed stock metadata");
            var insufficient=Market(cat,now,13);var lowOffer=insufficient.vehicleStock.listings.First(o=>o.used);insufficient.cash=49;var lowQuote=PaidEconomy.Quote(insufficient,cat,policy,values,new PaymentRequest{kind=PaymentKind.VehicleInspection,offer=lowOffer},now);string lowBefore=JsonUtility.ToJson(insufficient);bool noFunds=false;try{PaidEconomy.Pay(insufficient,cat,policy,values,lowQuote,now,lowOffer);}catch(InvalidOperationException){noFunds=true;}check(noFunds&&lowBefore==JsonUtility.ToJson(insufficient),"Insufficient funds neither disclose nor charge");
            foreach(var category in new[]{PartCategory.Brakes,PartCategory.Driveshaft}){
                var status=Market(cat,now,15);var testOffer=status.vehicleStock.listings.First(o=>!o.used);var part=testOffer.parts.Single(i=>i.slot==category.ToString());part.condition.failed=true;part.condition.failureReason="Assigned status check";part.condition.health=0;VehicleInspection.Assess(cat,testOffer);
                check(testOffer.engineRuns&&(category==PartCategory.Brakes?testOffer.drives:!testOffer.drives),category+" failure keeps actual independent engine/drive behavior");
            }
            var market=Market(cat,now,17);var listing=market.vehicleStock.listings.First(o=>o.used);
            var piston=listing.parts.Single(p=>p.slot=="Pistons");piston.condition.failed=true;piston.condition.health=.15f;piston.condition.failureReason="Test retained seizure";foreach(var e in piston.condition.elements){e.health=0;e.fault=ElementFault.Seized;e.severity=1;}listing.reportedCondition=VehicleInspection.Overall(cat,listing);VehicleInspection.Assess(cat,listing);
            check(!listing.engineRuns&&!listing.drives,"Real seized engine is advertised as non-running/non-driving without exposing its cause");
            var publicProfile=VehicleCollection.Preview(cat,VehicleInspection.PublicOffer(cat,listing));var guess=CarInspection.CalculatePrepared(InspectionBuild.Capture(publicProfile,publicProfile.SelectedCar),CarInspection.PrepareCar(InspectionBuild.Capture(publicProfile,publicProfile.SelectedCar)),false);
            var actualProfile=VehicleCollection.Preview(cat,listing);var truth=CarInspection.CalculatePrepared(InspectionBuild.Capture(actualProfile,actualProfile.SelectedCar),CarInspection.PrepareCar(InspectionBuild.Capture(actualProfile,actualProfile.SelectedCar)),false);
            check(guess.peakHp>0&&truth.peakHp==0,"Estimated brochure output can differ from the actual failed engine");
            string components=JsonUtility.ToJson(listing.parts),liquids=JsonUtility.ToJson(listing.fluids),gas=JsonUtility.ToJson(listing.fuel),ownerParts=JsonUtility.ToJson(market.parts),profileBefore=JsonUtility.ToJson(market);int cash=market.cash;
            var request=new PaymentRequest{kind=PaymentKind.VehicleInspection,offer=listing};var quote=PaidEconomy.Quote(market,cat,policy,values,request,now);check(quote.Cost==50,"Inspection fee comes from configurable operating policy");
            PlayerProfile committed;string error;
            bool paid=MechanicsTransactions.Try(market,cat,market.revision,p=>PaidEconomy.Pay(p,cat,policy,values,quote,now,VehicleListings.Find(p,listing.listingId)),p=>false,out committed,out error);
            check(!paid&&ReferenceEquals(committed,market)&&profileBefore==JsonUtility.ToJson(market),"Failed persistence cannot charge or reveal information");
            paid=MechanicsTransactions.Try(market,cat,market.revision,p=>PaidEconomy.Pay(p,cat,policy,values,quote,now,VehicleListings.Find(p,listing.listingId)),p=>true,out committed,out error);check(paid,"Inspection commits atomically / "+error);market=committed;listing=VehicleListings.Find(market,listing.listingId);
            check(VehicleInspection.Known(listing)&&market.cash==cash-50&&market.vehicleStock.knowledgeRevision==1&&market.economyReceipts.Last().operation=="VehicleInspection","Inspection charges once and persists listing-specific knowledge");
            check(SnapshotEquality.Serialized(components,listing.parts)&&SnapshotEquality.Serialized(liquids,listing.fluids)&&SnapshotEquality.Serialized(gas,listing.fuel)&&SnapshotEquality.Serialized(ownerParts,market.parts),"Inspection neither repairs/refills seller assets nor changes player hardware");
            bool duplicate=false;try{PaidEconomy.Pay(market,cat,policy,values,quote,now,listing);}catch(InvalidOperationException){duplicate=true;}check(duplicate&&market.cash==cash-50,"Duplicate payment cannot charge again");
            var reloaded=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(market));MechanicsOwnership.Ensure(reloaded,cat);check(VehicleInspection.Known(VehicleListings.Find(reloaded,listing.listingId))&&MechanicsOwnership.Validate(reloaded,cat)==null,"Inspection and hidden truth survive reload");
            var buying=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(market));MechanicsOwnership.Ensure(buying,cat);var sold=VehicleListings.Find(buying,listing.listingId);VehicleCollection.Buy(buying,cat,sold);
            check(buying.SelectedCar.usedHistory.SequenceEqual(listing.historyNotes)&&SnapshotEquality.Serialized(liquids,buying.SelectedCar.fluids)&&SnapshotEquality.Serialized(gas,buying.SelectedCar.fuel)&&buying.parts.Single(p=>p.instanceId==piston.instanceId).condition.failed,"Buying transfers exact damage, history and liquids without healing");
            var uninspected=Market(cat,now,29);var raw=uninspected.vehicleStock.listings.First(o=>o.used);string rawParts=JsonUtility.ToJson(raw.parts);VehicleCollection.Buy(uninspected,cat,raw);check(raw.parts.All(p=>uninspected.parts.Any(q=>q.instanceId==p.instanceId&&q.condition.failed==p.condition.failed&&q.condition.health==p.condition.health)),"Uninspected purchase transfers actual contents, not the public estimate");
            var stale=Market(cat,now,37);var offer=stale.vehicleStock.listings.First(o=>o.used);var old=PaidEconomy.Quote(stale,cat,policy,values,new PaymentRequest{kind=PaymentKind.VehicleInspection,offer=offer},now);int held=stale.cash;for(int i=0;i<3;i++)VehicleListings.AfterRace(stale,cat);bool blocked=false;try{PaidEconomy.Pay(stale,cat,policy,values,old,now,VehicleListings.Find(stale,offer.listingId));}catch(InvalidOperationException){blocked=true;}check(blocked&&stale.cash==held,"Expired listing/stock refresh rejects inspection without charging");
            var legacy=Market(cat,now,41);var prior=legacy.vehicleStock.listings.First(o=>o.used);prior.disclosureVersion=0;string preserved=JsonUtility.ToJson(prior.parts);MechanicsOwnership.Ensure(legacy,cat);check(VehicleInspection.Known(prior)&&preserved==JsonUtility.ToJson(prior.parts),"Previously disclosed saved listings migrate without retroactive hidden damage or healing");
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/variety"));File.WriteAllText(Path.Combine(folder,"inspection-verification.txt"),log+"TOTAL "+count,new UTF8Encoding(false));Debug.Log("USED VEHICLE INSPECTION PASS: "+count);
        }
    }
}

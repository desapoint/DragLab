using System;
using DragLab.Domain;
using System.Linq;
using UnityEngine;
using DragLab.Domain.Mechanics;
namespace DragLab.Runtime
{
    public sealed partial class PrototypeGame
    {
        void RecoveryAdviceGUI()
        {
            GUILayout.Label("RECOVERY OPTIONS",stat);
            if(Build!=null){
                if(!string.IsNullOrEmpty(Build.garageJobId))GUILayout.Label("This car is assigned to a job. Business > Claim or Cancel releases it without replacing parts.");
                if(Build.engineRebuild!=null)GUILayout.Label("An engine rebuild is already paid. Collect it in Parts when its deadline is reached.");
                if(string.IsNullOrEmpty(Build.engineAssemblyId))GUILayout.Label("Engine missing: attach a compatible stored engine in Assemblies, or buy a replacement car. Fuel stays with this chassis.");
                if(Build.fuel.litres<1){double quantity=Math.Min(1,profile.catalog.Spec(Build.vehicleId).fuel.capacityLitres-Build.fuel.litres);int cost=PaymentCost(Payment(PaymentKind.Fuel,quantity));GUILayout.Label("Low fuel: add "+quantity.ToString("F2")+" L / "+(cost<0?"release reservation first":DisplayUnits.Money(cost)+" at current fuel price")+". Larger fills add weight; free/reduced fuel difficulty retains physical tank mass.");if(cost>=0)PaidButton("recovery-fuel","Buy a small usable fuel reserve",Payment(PaymentKind.Fuel,quantity));}
                var failed=profile.parts.Where(i=>Build.installed.slots.Any(s=>s.partInstanceId==i.instanceId)&&(i.condition.failed||i.condition.health<=0)).ToArray();
                if(failed.Length>0)GUILayout.Label("Failed packs: "+string.Join("; ",failed.Select(i=>profile.catalog.Find(i.definitionId).name))+". Reuse compatible inventory first, replace individual elements where applicable, or repair/rebuild supported faults. Replacement prices are shown in Parts; failures do not all disable the engine.");
                var b=MechanicsOwnership.Resolve(profile,Build,profile.catalog);if(!b.Drivable)GUILayout.Label("Current blockers: "+string.Join("; ",b.errors));
            }else GUILayout.Label("Empty garage: retained engines/parts can still be sold. Recovery and capped idle income need no car; dealership purchases provide a new paid package.");
            var offers=profile.vehicleStock.listings.Where(o=>o.reputation<=profile.reputation).Select(o=>new{v=vehicles.First(v=>v.spec.id==o.model),price=PaidEconomy.DealerPrice(profile.catalog,Costs,PricingPolicy,o)}).OrderBy(v=>v.price).ToArray();
            if(offers.Length>0){var cheapest=offers[0];int shortfall=Math.Max(0,cheapest.price-profile.cash);int jobs=(int)Math.Ceiling(shortfall/(double)Costs.recoveryReward);GUILayout.Label("Cheapest unlocked replacement: "+cheapest.v.spec.displayName+" / " + DisplayUnits.Money(cheapest.price)+" / shortfall " + DisplayUnits.Money(shortfall)+" / about "+jobs+" recovery claims (before idle income or asset sales). Work requires cash below " + DisplayUnits.Money(Costs.recoveryCashLimit)+".");}
        }
    }
}

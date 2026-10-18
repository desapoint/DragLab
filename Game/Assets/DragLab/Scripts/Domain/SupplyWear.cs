using System;
using System.Collections.Generic;
using System.Linq;

namespace DragLab.Domain.Mechanics
{
    internal static class SupplyWear
    {
        internal sealed class Candidate
        {
            internal NewPartSupply.Product product;
            internal int required;
            internal Dictionary<string,int> roles;
        }
        internal sealed class Need
        {
            internal PlayerProfile owner;
            internal string component;
            internal double health;
            internal bool failed;
            internal bool mixable;
            internal Candidate[] candidates;
            internal double usedMultiplier;
            internal double Severity=>failed?1:1-health;
            internal double Probability()
            {
                int complete=0;
                if(mixable)complete=candidates.Sum(c=>PartsMarket.Available(owner,c.product.definition))/candidates[0].required;
                else foreach(var c in candidates){complete+=PartsMarket.Available(owner,c.product.definition)/c.required;if(complete>=2)return 0;}
                return Base(health,failed)*NewMultiplier(complete)*usedMultiplier;
            }
        }
        internal static double Base(double condition,bool failed)=>failed||condition<=.1f?.4:condition<=.25f?.3:condition<=.5f?.2:condition<=.6f?.1:0;
        internal static double NewMultiplier(int complete)=>complete==0?1:complete==1?.5:0;
        internal static double UsedMultiplier(int complete,bool partial)=>complete>=2?.25:complete==1?.5:partial?.75:1;
        internal static bool Usable(EngineElement e,PartCategory category)
        {
            if(e==null||!EngineElements.MatchesCategory(category,e)||e.Failed||e.fault!=ElementFault.None||e.health<=.6f)return false;
            return !(e is TireCondition t)||!t.failed&&Math.Min(t.health,Math.Max(0,1-t.consumedLife))>.6f;
        }
        static string Role(PartCategory c,EngineElement e)=>c==PartCategory.Tires?"tire":c==PartCategory.Wheels?"wheel":e.role;
        static Dictionary<string,int> Roles(VehicleSpec spec,PartDefinition d)
        {
            if(d.category==PartCategory.Tires)return new Dictionary<string,int>{{"tire",4}};
            var template=new PartInstance{instanceId="supply-topology",definitionId=d.id};
            // Match the same topology used by individual purchases, without touching owned components.
            if(d.category==PartCategory.Ignition&&!CombustionHardware.Diesel(spec)){spec=spec.Copy();spec.combustion.separateSparkPlugs=true;}
            if(d.category==PartCategory.FuelSystem&&d.fuelDelivery!=FuelDeliveryHardware.Factory){spec=spec.Copy();FuelingHardware.Apply(spec,d);}
            EngineElements.Ensure(spec,d,template);
            return template.condition.elements.Length>0?template.condition.elements.GroupBy(e=>Role(d.category,e)).ToDictionary(g=>g.Key,g=>g.Count(),StringComparer.Ordinal):new Dictionary<string,int>{{"part",PartsMarket.Count(spec,d)}};
        }
        internal static Need[] Build(PlayerProfile p,NewPartSupply.Product[] products)=>Steps(p,products).ToArray();
        internal static IEnumerable<Need> Steps(PlayerProfile p,NewPartSupply.Product[] products)
        {
            var byId=new Dictionary<string,NewPartSupply.Product>(StringComparer.Ordinal);
            foreach(var product in products)foreach(var id in product.stock.definitions)byId[id]=product;
            var used=new Dictionary<NewPartSupply.Product,Dictionary<string,Dictionary<string,int>>>();
            void Add(string id,string role,int count){
                if(!byId.TryGetValue(id,out var product))return;
                if(!used.TryGetValue(product,out var definitions))used[product]=definitions=new Dictionary<string,Dictionary<string,int>>(StringComparer.Ordinal);
                if(!definitions.TryGetValue(id,out var roles))definitions[id]=roles=new Dictionary<string,int>(StringComparer.Ordinal);
                roles[role]=roles.TryGetValue(role,out int n)?n+count:count;
            }
            foreach(var row in p.inventory.dealer){
                if(CartReservations.HoldsUsed(p,row.Id))continue;
                if(row.IsUnit){var d=p.catalog.Find(row.DefinitionId);if(d!=null&&Usable(row.unit.element,d.category))Add(d.id,Role(d.category,row.unit.element),1);continue;}
                if(!row.IsPart)continue;var pack=row.part;var definition=p.catalog.Find(pack.definitionId);var c=pack.condition;
                if(definition==null||c.failed)continue;
                if(c.elements.Length==0){if(c.health>.6f)Add(definition.id,"part",definition.category==PartCategory.Brakes?4:1);continue;}
                for(int i=0;i<c.elements.Length;i++){
                    string id=definition.category==PartCategory.Tires?TireFitments.DefinitionId(pack,i):definition.category==PartCategory.Wheels?WheelFitments.DefinitionId(pack,i):definition.id;
                    var d=p.catalog.Find(id);var e=c.elements[i];if(d!=null&&Usable(e,d.category))Add(id,Role(d.category,e),1);
                }
            }
            // Persisted acquisition/member order is stable across a seeded replay;
            // sorting by newly generated GUIDs would introduce unseeded choice order.
            foreach(var car in p.cars){
                // Legacy/new-game initialization may precede the ownership import.
                // Prioritize only actual installed identities; never synthesize OEM assets here.
                if(car.installed==null||car.installed.slots==null)continue;
                var spec=p.catalog.Resolve(car.vehicleId,MechanicsOwnership.Definitions(p,car),car.tune).spec;
                // Cached per car/category product, rather than resolving every candidate for every cylinder.
                var topology=new Dictionary<NewPartSupply.Product,Dictionary<string,int>>();
                var targets=MaintenanceReplacements.Candidates(p,car.instanceId,false,100).Where(t=>Base(t.condition,t.broken)>0).ToArray();
                foreach(var target in targets){
                    bool mixable=target.category==PartCategory.Tires||target.category==PartCategory.Wheels;
                    var repair=target.complete?null:targets.Where(t=>t.partId==target.partId&&!t.complete).GroupBy(t=>t.role).ToDictionary(g=>g.Key,g=>g.Count(),StringComparer.Ordinal);
                    var choices=new List<Candidate>();byId.TryGetValue(target.definitionId,out var same);
                    foreach(var product in products){
                        var d=product.definition;if(d.category!=target.category||!p.catalog.Fits(spec,d))continue;
                        if(!topology.TryGetValue(product,out var roles))topology[product]=roles=Roles(spec,d);
                        Dictionary<string,int> required=roles;
                        if(!target.complete&&(mixable||ReferenceEquals(product,same))){
                            if(repair.Any(r=>!roles.TryGetValue(r.Key,out int count)||count<r.Value))continue;required=repair;
                        }
                        choices.Add(new Candidate{product=product,roles=required,required=required.Values.Sum()});
                    }
                    if(choices.Count==0)continue;
                    int complete=0,mixedUnits=0;bool partial=false;
                    foreach(var candidate in choices){
                        if(!used.TryGetValue(candidate.product,out var variants))continue;
                        foreach(var variant in variants){
                            if(mixable){foreach(var role in candidate.roles)if(variant.Value.TryGetValue(role.Key,out int count))mixedUnits+=count;continue;}
                            // New alias stock can be ordered as the installed definition. A physical
                            // used engine member must satisfy the service's exact matched-ID rule;
                            // a different alias requires its own complete replacement set.
                            var required=candidate.roles;
                            if(!target.complete&&target.category!=PartCategory.Tires&&target.category!=PartCategory.Wheels&&variant.Key!=target.definitionId)required=topology[candidate.product];
                            int sets=int.MaxValue;bool any=false;
                            foreach(var role in required){int n=variant.Value.TryGetValue(role.Key,out int units)?units:0;sets=Math.Min(sets,n/role.Value);any|=n>0;}
                            complete+=sets;partial|=any;
                        }
                    }
                    if(mixable){complete=mixedUnits/choices[0].required;partial=mixedUnits>0;}
                    yield return new Need{owner=p,component=target.key,health=target.condition,failed=target.broken,mixable=mixable,candidates=choices.ToArray(),usedMultiplier=UsedMultiplier(complete,partial)};
                }
            }
        }
    }
}

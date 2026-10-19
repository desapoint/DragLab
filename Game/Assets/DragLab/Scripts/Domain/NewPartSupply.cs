using System;
using System.Collections.Generic;
using System.Linq;

namespace DragLab.Domain.Mechanics
{
    // Selection and delivery rules apply only to new products. Used rows are read-only coverage.
    internal static class NewPartSupply
    {
        internal sealed class Product
        {
            internal PartDefinition definition;
            internal NewPartStock stock;
            internal int maximum;
            internal bool owned,individual;
            internal readonly Dictionary<Branch,bool> categories=new Dictionary<Branch,bool>();
        }
        internal sealed class Branch
        {
            internal string name;
            internal double weight;
            internal Func<Product,bool> filter;
            internal Branch[] children;
            internal bool Accepts(Product p)
            {
                if(!p.categories.TryGetValue(this,out bool valid)){valid=filter!=null?filter(p):children.Any(c=>c.weight>0&&c.Accepts(p));p.categories[this]=valid;}
                return valid;
            }
        }
        static Branch Leaf(string name,double weight,Func<Product,bool> filter)=>new Branch{name=name,weight=weight,filter=filter};
        static Branch Part(PartCategory category,double weight=1)=>Leaf(category.ToString(),weight,p=>p.definition.category==category);
        static Branch Group(string name,double weight,params Branch[] children)=>new Branch{name=name,weight=weight,children=children};
        static Branch Parts(string name,double weight,params PartCategory[] types)=>Group(name,weight,types.Select(c=>Part(c)).ToArray());
        static Branch Induction(EngineUpgradeInduction kind)=>Leaf(kind.ToString(),1,p=>p.definition.category==PartCategory.Induction&&p.definition.induction==kind);
        // Legacy numeric compounds remain deliverable in their nearest existing street family.
        internal static string TireFamily(PartDefinition d)
        {
            string key=d.id.Substring(d.id.LastIndexOf('/')+1);
            if(new[]{"StreetSemiSlick","EnduranceRadial","ProRadial","SlickMedium","SlickSoft","QualifyingSlick"}.Contains(key))return key;
            return d.stage==0?"OEM":d.stage==1?"Touring":"StreetSemiSlick";
        }
        static Branch Tire(string key)=>Leaf(key,1,p=>p.definition.category==PartCategory.Tires&&TireFamily(p.definition)==key);
        internal static readonly Branch Categories=Group("Categories",1,
            Group("Induction",3,Group("Turbo family",65,Induction(EngineUpgradeInduction.Turbo),Induction(EngineUpgradeInduction.TwinTurbo),Induction(EngineUpgradeInduction.Twincharged)),Group("Supercharger family",25,Induction(EngineUpgradeInduction.Supercharger),Induction(EngineUpgradeInduction.TwinSupercharger)),Leaf("Factory induction",10,p=>p.definition.category==PartCategory.Induction&&p.definition.induction==EngineUpgradeInduction.Factory)),
            Group("Tires",3,Group("Street",20,Tire("OEM"),Tire("Touring"),Tire("StreetSemiSlick")),Group("Drag radial",30,Tire("EnduranceRadial"),Tire("ProRadial")),Group("Competition slick",50,Tire("SlickMedium"),Tire("SlickSoft"),Tire("QualifyingSlick"))),
            Group("Intake / fuel / exhaust",2.5,Part(PartCategory.HeadIntake,40),Part(PartCategory.FuelSystem,30),Part(PartCategory.Exhaust,30)),
            Group("Drivetrain",2,Part(PartCategory.Gearbox,25),Parts("Clutch / flywheel",20,PartCategory.Clutch,PartCategory.Flywheel),Parts("Differential family",20,PartCategory.Differential,PartCategory.FrontDifferential,PartCategory.RearDifferential),Parts("Driveshaft family",15,PartCategory.Driveshaft,PartCategory.FrontDriveshaft,PartCategory.RearDriveshaft),Parts("Axle / transfer case",20,PartCategory.FrontAxle,PartCategory.RearAxle,PartCategory.TransferCase)),
            Group("Engine / electronics",2,Parts("Core engine",35,PartCategory.Pistons,PartCategory.Rods,PartCategory.Crankshaft,PartCategory.CylinderBlock,PartCategory.PistonRings,PartCategory.Bearings),Parts("Valvetrain",30,PartCategory.Camshaft,PartCategory.ValveSprings,PartCategory.Valves,PartCategory.Followers,PartCategory.TimingDrive,PartCategory.ValveSeals),Parts("Ignition / controls",20,PartCategory.Ignition,PartCategory.SparkPlugs,PartCategory.GlowPlugs,PartCategory.Controls),Parts("Electronic accessories",15,PartCategory.RevLimiter,PartCategory.ShiftLight,PartCategory.TwoStep)),
            Group("Wheels / cooling / lubrication",1.75,Part(PartCategory.Wheels,35),Parts("Cooling parts",40,PartCategory.Cooling,PartCategory.CoolantPump,PartCategory.CoolingCap,PartCategory.CoolingHoses,PartCategory.OilCooler),Parts("Lubrication parts",25,PartCategory.OilPan,PartCategory.OilPump,PartCategory.OilFilter)),
            Part(PartCategory.Brakes,1.5),Group("Body / interior / accessories",1,Part(PartCategory.Body,50),Part(PartCategory.Interior,25),Part(PartCategory.Accessories,25)));

        internal static double Next(ref uint s){s^=s<<13;s^=s>>17;s^=s<<5;return s/4294967296.0;}
        internal static int Roll(double[] weights,double roll)
        {
            double sum=weights.Sum();if(sum<=0)return -1;double value=Math.Min(.9999999999999999,Math.Max(0,roll))*sum;
            for(int i=0;i<weights.Length;i++){value-=weights[i];if(weights[i]>0&&value<0)return i;}
            return Array.FindLastIndex(weights,w=>w>0);
        }
        internal static List<Product> Filter(List<Product> pool,Func<Product,bool>[] filters,double[] weights,ref uint random)
        {
            var children=filters.Select(f=>pool.Where(f).ToList()).ToArray();
            var valid=weights.Select((w,i)=>children[i].Count>0?w:0).ToArray();int only=Array.FindIndex(valid,w=>w>0);
            if(only<0)return new List<Product>();
            int pick=valid.Count(w=>w>0)==1?only:Roll(valid,Next(ref random));return children[pick];
        }
        internal static Product PickCategory(List<Product> pool,Branch branch,ref uint random)
        {
            if(pool.Count==0)return null;if(pool.Count==1)return pool[0];
            if(branch.children==null)return pool[(int)(Next(ref random)*pool.Count)];
            // Partition once, in the original product order. This replaces one LINQ scan
            // and temporary list per child without changing the weighted RNG stream.
            var children=branch.children;
            var groups=new List<Product>[children.Length];
            foreach(var product in pool){
                for(int i=0;i<children.Length;i++){
                    if(!children[i].Accepts(product))continue;
                    if(groups[i]==null)groups[i]=new List<Product>();
                    groups[i].Add(product);
                }
            }
            var weights=new double[children.Length];
            int first=-1,valid=0;
            for(int i=0;i<children.Length;i++){
                if(groups[i]==null||children[i].weight<=0)continue;
                weights[i]=children[i].weight;
                if(first<0)first=i;
                valid++;
            }
            if(first<0)return null;
            int pick=valid==1?first:Roll(weights,Next(ref random));
            var selected=groups[pick];
            // Preserve the original first-accepting-child rule even for overlapping fixtures.
            var child=children.First(b=>b.weight>0&&b.Accepts(selected[0]));
            return PickCategory(selected,child,ref random);
        }
        internal static double DiscoveryProbability(int unknown,int total)=>unknown==0||total==0?0:Math.Max(.1,unknown/(double)total);
        internal static int Quantity(Product p,bool first,double mode,double quantity)
        {
            if(p.maximum<=1)return first?(quantity<.8?1:quantity<.97?2:3):1;
            if(p.individual&&mode>=.6)return 1;
            return checked(p.maximum*(first?(quantity<.5?1:quantity<.85?2:3):1));
        }
        // Disabled outside an explicit PartsMarket.Measurement subscription.
        // Reset the clock after invoking the observer so logging time is not charged to the next stage.
        static void Sample(Action<string,double> measure,string stage,ref long stamp)
        {
            if(measure==null)return;
            long end=System.Diagnostics.Stopwatch.GetTimestamp();
            measure(stage,(end-stamp)*1000.0/System.Diagnostics.Stopwatch.Frequency);
            stamp=System.Diagnostics.Stopwatch.GetTimestamp();
        }
        // The regular 24-44 opportunities use one immutable eligible membership snapshot.
        // Stock counts/discovery change during delivery, but category and max-set fit do not.
        internal sealed class DeliveryPool
        {
            internal readonly List<Product> eligible;
            internal DeliveryPool(List<Product> eligibleProducts){eligible=eligibleProducts;}
        }
        internal static DeliveryPool PrepareDeliveryPool(List<Product> all)
        {
            var measure=PartsMarket.Measurement;
            long stamp=measure==null?0:System.Diagnostics.Stopwatch.GetTimestamp();
            var prepared=new DeliveryPool(all.Where(p=>p.maximum>0&&Categories.Accepts(p)).ToList());
            Sample(measure,"delivery/pool preparation",ref stamp);
            return prepared;
        }
        internal static int Deliver(List<Product> all,SupplyWear.Need[] needs,ref uint selection,ref uint quantity)
            =>DeliverCore(all,false,needs,ref selection,ref quantity);
        internal static int Deliver(DeliveryPool prepared,SupplyWear.Need[] needs,ref uint selection,ref uint quantity)
            =>DeliverCore(prepared.eligible,true,needs,ref selection,ref quantity);
        static int DeliverCore(List<Product> all,bool alreadyEligible,SupplyWear.Need[] needs,ref uint selection,ref uint quantity)
        {
            var measure=PartsMarket.Measurement;
            long stamp=measure==null?0:System.Diagnostics.Stopwatch.GetTimestamp();
            // Exact same eligibility predicate as the uncached entry path; only the
            // regular refresh reuses the result across its own delivery opportunities.
            var pool=alreadyEligible?all:all.Where(p=>p.maximum>0&&Categories.Accepts(p)).ToList();
            Sample(measure,"delivery/eligible pool",ref stamp);
            if(pool.Count==0)return 0;
            if(pool.Count>1)pool=Filter(pool,new Func<Product,bool>[] {p=>p.owned,p=>!p.owned},new[]{.76,.24},ref selection);
            if(pool.Count>1){double chance=DiscoveryProbability(pool.Count(p=>!p.stock.discovered),pool.Count);pool=Filter(pool,new Func<Product,bool>[] {p=>!p.stock.discovered,p=>p.stock.discovered},new[]{chance,1-chance},ref selection);}
            Sample(measure,"delivery/ownership and discovery",ref stamp);
            Product chosen;
            if(pool.Count==1)chosen=pool[0];
            else{
                var available=new HashSet<Product>(pool);var eligible=needs.Where(n=>n.candidates.Any(c=>available.Contains(c.product))).ToArray();
                Sample(measure,"delivery/wear candidate match",ref stamp);
                var probabilities=eligible.Select(n=>n.Probability()).ToArray();double priority=probabilities.DefaultIfEmpty(0).Max();
                Sample(measure,"delivery/wear probabilities",ref stamp);
                if(priority>0&&Next(ref selection)<priority){
                    int pick=Roll(eligible.Select((n,i)=>probabilities[i]*n.Severity).ToArray(),Next(ref selection));
                    var replacements=eligible[pick].candidates.Where(c=>available.Contains(c.product)).Select(c=>c.product).ToArray();
                    chosen=replacements.Length==1?replacements[0]:replacements[(int)(Next(ref selection)*replacements.Length)];
                }else chosen=PickCategory(pool,Categories,ref selection);
            }
            Sample(measure,"delivery/product choice",ref stamp);
            if(chosen==null)return 0;
            bool first=!chosen.stock.discovered;double mode=chosen.maximum>1&&chosen.individual?Next(ref quantity):0;
            bool full=chosen.maximum>1&&(!chosen.individual||mode<.6);
            double amount=first&&(chosen.maximum<=1||full)?Next(ref quantity):0;
            int units=Quantity(chosen,first,mode,amount);chosen.stock.units=checked(chosen.stock.units+units);chosen.stock.initialUnits=checked(chosen.stock.initialUnits+units);chosen.stock.discovered=true;
            Sample(measure,"delivery/quantity and publication",ref stamp);
            return units;
        }
        internal static Product[] Prepare(PlayerProfile p,MarketCatalogLayout.Layout layout,PartsStockState previous)
        {
            var old=new Dictionary<string,NewPartStock>(StringComparer.Ordinal);
            if(previous!=null)foreach(var row in previous.parts)foreach(string id in row.definitions)old[id]=row;
            var owned=p.cars.Select(car=>p.catalog.Spec(car.vehicleId)).ToArray();var result=new List<Product>();
            var rows=p.inventory.market.parts.ToDictionary(s=>s.definitions[0],StringComparer.Ordinal);
            var carried=new HashSet<NewPartStock>();
            foreach(var group in layout.groups){
                var row=rows[group.ids[0]];var prior=group.ids.Select(id=>old.TryGetValue(id,out var s)?s:null).FirstOrDefault(s=>s!=null);
                int held=prior==null||!CartReservations.Active(p)?0:p.inventory.cart.supply.Where(s=>s.generation==previous.generation&&prior.definitions.Contains(s.definitionId)).Sum(s=>s.units);
                // If an authored alias group splits, carry its physical units once, never once per alias.
                row.units=row.initialUnits=prior==null||!carried.Add(prior)?0:Math.Max(0,prior.units-held);row.discovered=prior!=null&&(prior.discovered||prior.initialUnits>0);
                if(prior!=null)row.deliveryPercent=prior.deliveryPercent;
                var d=group.definition;result.Add(new Product{definition=d,stock=row,maximum=group.deliverable?group.maximum:0,owned=owned.Any(s=>p.catalog.Fits(s,d)),individual=d.category==PartCategory.Tires||EngineElements.Supported(d.category)});
            }
            p.inventory.market.index=null;return result.ToArray();
        }
    }
}

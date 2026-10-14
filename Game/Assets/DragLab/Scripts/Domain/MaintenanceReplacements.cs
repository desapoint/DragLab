using System;
using System.Collections.Generic;
using System.Linq;

namespace DragLab.Domain.Mechanics
{
    public sealed class ReplacementTarget
    {
        public string key, partId, definitionId, position, role;
        public bool complete, broken;
        public double condition;
        public int count;
        public PartCategory category;
    }
    public sealed class ReplacementPlan
    {
        public readonly List<ReplacementTarget> targets=new List<ReplacementTarget>();
        public readonly List<PartCartLine> lines=new List<PartCartLine>();
        public int cost, deliveryFee, localUnits, expressUnits;
    }
    // Selection is read-only. Checkout uses the same matched sets, exact units and stock ledger as Build.
    public static class MaintenanceReplacements
    {
        static bool Broken(ComponentCondition c)=>c.failed||c.health<=0;
        static bool Select(bool broken,double health,bool brokenOnly,double threshold)=>broken||!brokenOnly&&health<threshold/100;
        public static ReplacementTarget[] Candidates(PlayerProfile p,string carId,bool brokenOnly,double belowPercent=15)
        {
            if(double.IsNaN(belowPercent)||double.IsInfinity(belowPercent)||belowPercent<0||belowPercent>100)throw new InvalidOperationException("Condition must be 0–100%.");
            var car=p.cars.Find(c=>c.instanceId==carId)??throw new InvalidOperationException("Car no longer exists.");
            var result=new List<ReplacementTarget>();
            foreach(var binding in car.installed.slots.OrderBy(s=>s.slot,StringComparer.Ordinal)){
                var part=p.parts.Find(i=>i.instanceId==binding.partInstanceId);if(part==null)continue;
                var d=p.catalog.Find(part.definitionId);var c=part.condition;bool broken=Broken(c);int members=c.tires.Length==4?4:c.elements.Length;
                bool everyMemberFailed=c.tires.Length==4?c.tires.All(t=>t.failed||t.health<=0||t.consumedLife>=1):c.elements.Length>0&&c.elements.All(e=>e.Failed);
                // An independent set-level failure cannot be repaired by replacing one healthy member.
                if(members==0||broken&&!everyMemberFailed){
                    if(Select(broken,c.health,brokenOnly,belowPercent)&&(broken||PartSemantics.Wears(d.category)))result.Add(new ReplacementTarget{key=part.instanceId,partId=part.instanceId,definitionId=d.id,complete=true,condition=c.health,broken=broken,category=d.category,count=Math.Max(members,d.category==PartCategory.Brakes?4:1)});
                    continue;
                }
                for(int n=0;n<members;n++){
                    bool tire=d.category==PartCategory.Tires;var e=tire?null:c.elements[n];var t=tire?c.tires[n]:null;
                    double health=tire?Math.Min(t.health,Math.Max(0,1-t.consumedLife)):e.health;
                    bool failed=tire?t.failed||health<=0:e.Failed||e.fault!=ElementFault.None;
                    if(!Select(failed,health,brokenOnly,belowPercent))continue;
                    string def=tire?TireFitments.DefinitionId(part,n):d.category==PartCategory.Wheels?WheelFitments.DefinitionId(part,n):d.id;
                    string position=tire?"tire:"+n:d.category==PartCategory.Wheels?"wheel:"+n:e.id;
                    result.Add(new ReplacementTarget{key=part.instanceId+"/"+position,partId=part.instanceId,definitionId=def,position=position,role=tire?"tire":e.role,condition=health,broken=failed,category=d.category,count=1});
                }
            }
            return result.ToArray();
        }
        public static ReplacementPlan Preview(PlayerProfile p,string carId,bool brokenOnly,double belowPercent=15,IEnumerable<string> selected=null)
        {
            var car=p.cars.Find(c=>c.instanceId==carId)??throw new InvalidOperationException("Car no longer exists.");
            EngineRebuilds.RequireAvailable(car);if(p.runCheckpoints.Any(r=>!r.settled))throw new InvalidOperationException("Close the active run first.");
            var candidates=Candidates(p,carId,brokenOnly,belowPercent);var keys=new HashSet<string>(selected??candidates.Select(t=>t.key),StringComparer.Ordinal);
            if(keys.Any(key=>!candidates.Any(t=>t.key==key)))throw new InvalidOperationException("Replacement targets changed; reopen maintenance.");
            var plan=new ReplacementPlan();plan.targets.AddRange(candidates.Where(t=>keys.Contains(t.key)));
            void AddSet(string id){plan.lines.Add(new PartCartLine{definitionId=id,action=CartAction.NewSet,quantity=1,install=true});}
            void AddUnit(string id,string role,string position){var line=plan.lines.Find(l=>l.action==CartAction.NewUnits&&l.definitionId==id&&l.role==role);if(line==null){line=new PartCartLine{action=CartAction.NewUnits,definitionId=id,role=role,quantity=0,install=true};plan.lines.Add(line);}line.quantity++;line.positions.Add(position);}
            foreach(var group in plan.targets.GroupBy(t=>t.partId)){
                var part=p.parts.Find(i=>i.instanceId==group.Key);var d=p.catalog.Find(part.definitionId);var targets=group.ToArray();
                if(targets[0].complete){
                    if(d.category==PartCategory.Tires&&TireFitments.HasOverrides(part))for(int n=0;n<4;n++)AddUnit(TireFitments.DefinitionId(part,n),"tire","tire:"+n);
                    else if(d.category==PartCategory.Wheels&&WheelFitments.HasOverrides(part))for(int n=0;n<4;n++)AddUnit(WheelFitments.DefinitionId(part,n),"wheel","wheel:"+n);
                    else AddSet(d.id);
                }else if(targets.Length==(d.category==PartCategory.Tires?4:part.condition.elements.Length)&&targets.Select(t=>t.definitionId).Distinct().Count()==1)AddSet(targets[0].definitionId);
                else foreach(var target in targets)AddUnit(target.definitionId,target.role,target.position);
            }
            var supply=new PartsMarket.Ledger(p);
            foreach(var line in plan.lines){var d=p.catalog.Find(line.definitionId);if(p.reputation<d.reputationRequired)throw new InvalidOperationException(d.name+" requires REP "+d.reputationRequired+". Deselect it to replace other parts.");int count=PartsMarket.Count(p,car,d);bool unit=line.action==CartAction.NewUnits;var q=supply.Take(d,unit?1:count,unit?InventoryPricing.NewUnit(d,count):d.price,line.quantity);plan.cost=checked(plan.cost+q.cost);plan.deliveryFee=checked(plan.deliveryFee+q.fee);plan.localUnits+=q.localUnits;plan.expressUnits+=q.expressUnits;}
            return plan;
        }
    }
}

using System;
using System.Linq;
using UnityEngine;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
namespace DragLab.Runtime
{
    public sealed partial class PrototypeGame
    {
        int serviceCylinder;
        void IndividualEngineService(PartInstance item)
        {
            var d=profile.catalog.Find(item.definitionId);var elements=item.condition.elements;
            if(d.category==PartCategory.Clutch&&d.clutchEnvironment==ClutchEnvironment.Wet){GUILayout.Label("Dedicated clutch oil "+item.condition.clutchOilLitres.ToString("F2")+" / "+d.clutchOilCapacityLitres.ToString("F1")+" L / condition "+item.condition.clutchOilCondition.ToString("P0"));PaidButton("clutch-oil:"+item.instanceId,"Service clutch oil + labour",Payment(PaymentKind.ClutchOil,target:item.instanceId));}
            if(elements==null||elements.Length==0||d.category==PartCategory.Tires)return;
            GUILayout.Label("INDIVIDUAL COMPONENTS / "+elements.Count(e=>e.Failed||e.fault!=ElementFault.None||e.health<.5)+" need attention. Replacement keeps all other components' wear.");
            int n=Selected.spec.sound.cylinders;serviceCylinder=Math.Min(serviceCylinder,n-1);serviceCylinder=GUILayout.SelectionGrid(serviceCylinder,Enumerable.Range(1,n).Select(i=>"Cylinder "+i).ToArray(),4);
            foreach(var e in elements.Where(e=>e.cylinder==serviceCylinder||e.cylinder<0)){
                GUILayout.Label(e.role+(e.valve>=0?" "+(e.valve+1):"")+" / "+e.health.ToString("P0")+" / "+e.fault+(e.fault!=ElementFault.None?" "+e.severity.ToString("P0"):""));
                string car=Build.instanceId,part=item.instanceId,element=e.id;int cost=PartsMarket.Quote(profile,d.category==PartCategory.Wheels?profile.catalog.Find(WheelFitments.DefinitionId(item,Array.IndexOf(elements,e))):d,elements.Length,true).cost;bool enabled=GUI.enabled;GUI.enabled=enabled&&profile.cash>=cost&&Build.engineRebuild==null;
                if(PartButton("element-new:"+element,"Replace this individual component / " + DisplayUnits.Money(cost)+" provisional"))QueuePayment(new PaymentRequest{kind=PaymentKind.ElementNew,carId=car,targetId=part,auxiliaryId=element});GUI.enabled=enabled;
                foreach(var spare in profile.looseEngineElements.Where(l=>l.definitionId==item.definitionId&&l.element.role==e.role&&!l.element.Failed)) {string id=spare.element.id;if(PartButton("element-spare:"+element+":"+id,"Reuse individual spare / "+spare.element.health.ToString("P0")))QueuePartAction(p=>EngineElementService.Replace(p,car,part,element,id));}
            }
            foreach(var loose in profile.looseEngineElements.Where(l=>l.definitionId==item.definitionId)){string id=loose.element.id;GUILayout.Label("Loose "+loose.element.role+" / "+loose.element.health.ToString("P0")+" / "+loose.element.fault);if(PartButton("element-sell:"+id,"Sell individual spare / preview buyer quote"))pendingAction=()=>OpenValuation(ValuationKind.LooseElement,id);}
        }
        void EngineRebuildGUI()
        {
            if(Build==null)return;long now=DateTime.UtcNow.Ticks;var job=Build.engineRebuild;string car=Build.instanceId;
            GUILayout.Label("ENGINE REBUILD SHOP",stat);
            if(job!=null){bool ready=now>=job.readyUtcTicks;GUILayout.Label("Paid " + DisplayUnits.Money(job.paidPrice)+" / "+(ready?"Ready to collect":TimeSpan.FromTicks(job.readyUtcTicks-now).ToString(@"hh\:mm\:ss")+" remaining")+" / engine reserved until collection. Time continues while offline.");bool enabled=GUI.enabled;GUI.enabled=enabled&&ready;if(PartButton("engine-rebuild-collect","Collect rebuilt engine")){string id=job.id;QueuePartAction(p=>EngineRebuilds.Collect(p,car,id,DateTime.UtcNow.Ticks));}GUI.enabled=enabled;return;}
            EngineRebuildJob quote;try{quote=EngineRebuilds.Quote(profile,Build,now,Costs);}catch(InvalidOperationException e){GUILayout.Label(e.Message);return;}
            GUILayout.Label("Recondition intact core/valvetrain; replace failed/unsalvageable sets with matching hardware, renew engine oil/filter. Cooling, turbo, gearbox and clutch remain separate. Tune retained.");
            GUILayout.Label("Quote " + DisplayUnits.Money(PaymentCost(Payment(PaymentKind.Rebuild)))+" including renewal oil / "+TimeSpan.FromTicks(quote.readyUtcTicks-now).TotalMinutes.ToString("F0")+" real minutes. Provisional service price/time; final economy balance in Slice 3.");
            PaidButton("engine-rebuild-start","Pay quote + send engine to shop",Payment(PaymentKind.Rebuild));
        }
    }
    public sealed partial class PhysicalDrivingBench
    {
        int testElement;
        void IndividualEngineControls(PartInstance setup,PartDefinition d,PartInstance live)
        {
            if(live==null)return;EngineElements.Ensure(Session.car.spec,d,setup);
            var elements=setup.condition.elements;
            bool enabled=GUI.enabled;GUI.enabled=enabled&&(Paused||!Session.car.state.launched);
            if(d.category==PartCategory.Clutch&&d.clutchEnvironment==ClutchEnvironment.Wet){float oil=(float)setup.condition.clutchOilLitres,condition=(float)setup.condition.clutchOilCondition;bool edit=MassSlider("Starting dedicated clutch oil (L)",ref oil,0,d.clutchOilCapacityLitres,"F2",d.clutchOilCapacityLitres);edit|=MassSlider("Starting clutch oil condition",ref condition,0,1,"F2",1);if(edit){setup.condition.clutchOilLitres=oil;setup.condition.clutchOilCondition=condition;ResetBench(true);}}
            if(elements==null||elements.Length==0){GUI.enabled=enabled;return;}
            GUILayout.Label("INDIVIDUAL CYLINDER / VALVE FAULTS (deterministic)");testElement=Math.Min(testElement,elements.Length-1);
            testElement=GUILayout.SelectionGrid(testElement,elements.Select(e=>(e.cylinder<0?"Shared":"C"+(e.cylinder+1))+" / "+e.role+(e.valve>=0?" "+(e.valve+1):"")).ToArray(),2);
            var selected=elements[testElement];float health=selected.health,severity=selected.severity;
            bool change=MassSlider("Individual structural health",ref health,0,1,"F2",1);change|=MassSlider("Fault severity",ref severity,0,1,"F2",1);
            var available=IndividualFaults(d.category);int current=Math.Max(0,Array.IndexOf(available,selected.fault));int chosen=GUILayout.SelectionGrid(current,available.Select(f=>PartSemantics.Friendly(f.ToString())).ToArray(),3);
            if(Event.current.type==EventType.Repaint)massRects["Individual fault choices"]=RuntimeGuiRect.ToClient(GUILayoutUtility.GetLastRect());
            change|=current!=chosen;
            if(change){selected.health=health;selected.severity=severity;selected.fault=available[chosen];ResetBench(true);}
            GUI.enabled=enabled;
            var actual=live.condition.elements.FirstOrDefault(e=>e.cylinder==selected.cylinder&&e.valve==selected.valve&&e.role==selected.role);
            if(actual!=null){GUILayout.Label("Live "+actual.role+" / "+actual.health.ToString("P0")+" / "+actual.fault);if(Button("element-replace","Replace this individual test component (free / isolated)")){Archive();actual.health=1;actual.fault=ElementFault.None;actual.severity=0;EngineElementService.RefreshAggregate(live.condition);Session.car.state.engineFailed=false;Session.car.state.engineDamage=0;Session=Session.ContinueRecording(Recording);notice="Selected test element replaced; other wear retained. Restart with Enter if stalled.";}}
            var s=Session.car.state;GUILayout.Label("Cylinder contributions: "+string.Join(" / ",s.cylinderContribution.Select((v,i)=>"C"+(i+1)+" "+v.ToString("P0"))));GUILayout.Label("Individual valve float "+s.individualValveFloat.ToString("P0")+" / delivered fuel fraction "+s.cylinderFuelFactor.ToString("P0"));
        }
        static ElementFault[] IndividualFaults(PartCategory c){return c==PartCategory.ValveSprings?new[]{ElementFault.None,ElementFault.WeakSpring,ElementFault.Disabled}:c==PartCategory.Valves?new[]{ElementFault.None,ElementFault.CompressionLeak,ElementFault.StuckOpen,ElementFault.DroppedValve}:c==PartCategory.Pistons?new[]{ElementFault.None,ElementFault.CompressionLeak,ElementFault.Seized}:c==PartCategory.PistonRings?new[]{ElementFault.None,ElementFault.CompressionLeak,ElementFault.Disabled}:c==PartCategory.Rods||c==PartCategory.Bearings?new[]{ElementFault.None,ElementFault.Disabled,ElementFault.Seized}:new[]{ElementFault.None,ElementFault.Disabled};}
    }
}

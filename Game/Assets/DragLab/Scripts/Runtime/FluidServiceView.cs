using System;
using System.Linq;
using UnityEngine;
using DragLab.Domain;
using DragLab.Infrastructure;
using DragLab.Domain.Mechanics;
namespace DragLab.Runtime
{
    public sealed partial class PrototypeGame
    {
        float oilAdd=1,coolantAdd=1;
        void FluidGarage()
        {
            if(string.IsNullOrEmpty(Build.engineAssemblyId)){GUILayout.Label("No attached engine: its oil/coolant remain in the stored assembly.");return;}
            EngineRebuildGUI();
            var f=Build.fluids;var spec=MechanicsOwnership.Resolve(profile,Build,profile.catalog).spec.fluids;
            GUILayout.Label("FLUIDS / paid quantities and service / faults retained",stat);
            GUILayout.Label("Oil "+f.oilLitres.ToString("F2")+" / "+spec.oilCapacityLitres.ToString("F1")+" L / minimum "+spec.oilMinimumLitres.ToString("F1")+" L / condition "+f.oilCondition.ToString("P0")+" / "+f.oilTemperatureC.ToString("F0")+" °C");
            GUILayout.Label("Coolant "+f.coolantLitres.ToString("F2")+" / "+spec.coolantCapacityLitres.ToString("F1")+" L / recovery reserve "+spec.recoveryCapacityLitres.ToString("F2")+" L");
            GUILayout.Label("Fluid mass: oil "+(f.oilLitres*.86).ToString("F2")+" kg / coolant "+(f.coolantLitres*1.05).ToString("F2")+" kg. Included once in vehicle mass.");
            GUILayout.Label("Oil consumed "+f.oilConsumedLitres.ToString("F3")+" L / leaked "+f.oilLeakedLitres.ToString("F3")+" L / coolant lost "+f.coolantLostLitres.ToString("F3")+" L. Returning from a race simulates 60 seconds of engine-off cooldown.");
            oilAdd=FluidQuantity("Add oil (L)",oilAdd,Math.Max(0,spec.oilCapacityLitres-f.oilLitres));
            string id=Build.instanceId;
            PaidButton("oil-topup","Top up oil / selected litres",Payment(PaymentKind.Fluids,oilAdd),oilAdd>0);
            coolantAdd=FluidQuantity("Add coolant (L)",coolantAdd,Math.Max(0,spec.coolantCapacityLitres-f.coolantLitres));
            PaidButton("coolant-topup","Top up coolant / selected litres",Payment(PaymentKind.Fluids,0,coolantAdd),coolantAdd>0);
            PaidButton("fluids-fill","Top up both to installed capacities",Payment(PaymentKind.Fluids,Math.Max(0,spec.oilCapacityLitres-f.oilLitres),Math.Max(0,spec.coolantCapacityLitres-f.coolantLitres)),f.oilLitres<spec.oilCapacityLitres||f.coolantLitres<spec.coolantCapacityLitres);
            PaidButton("oil-change","Change oil + replace filter + labour",Payment(PaymentKind.OilChange));
            PaidButton("coolant-flush","Flush / renew coolant / does not repair leaks",Payment(PaymentKind.CoolantFlush));
            if(f.oilLitres>spec.oilCapacityLitres&&PartButton("oil-excess","Drain oil excess after smaller pan installation"))QueuePartAction(p=>p.cars.Find(c=>c.instanceId==id).fluids.oilLitres=spec.oilCapacityLitres);
            if(PartButton("service-open","Inspect faults / repair / replace"))pendingAction=ShowRepairs;
            GUILayout.Label("Topping up never repairs wear, seals or a leak. Oil/filter change retains old filter. Automatic pre-race fluid assistance pays the same quoted cost and blocks when unaffordable; test benches remain free.");
        }
        float FluidQuantity(string label,float value,double maximum){GUILayout.Label(label+": "+Math.Min(value,maximum).ToString("F2"));value=(float)Math.Min(value,maximum);TuningSlider.Draw(ref value,0,(float)maximum,Math.Min(1,(float)maximum));return value;}
        void InstalledService(PartInstance item)
        {
            var d=profile.catalog.Find(item.definitionId);var c=item.condition;
            IndividualEngineService(item);
            GUILayout.Label("Deposits "+c.deposits.ToString("P0")+" / leak "+c.leakSeverity.ToString("P0")+" / clearance error "+c.adjustmentError.ToString("P0")+" / services "+c.serviceCount);
            if(!string.IsNullOrEmpty(c.lastService))GUILayout.Label("Last service: "+c.lastService);
            foreach(var action in ServiceOperations.Available(d,c,ServiceOperations.BoreHealth(profile,Build))) {
                string car=Build.instanceId,part=item.instanceId;var service=action;
                PaidButton("service:"+action+":"+part,ServiceOperations.Description(action),Payment(PaymentKind.Repair,target:part,service:service));
            }
        }
        bool AutoTopUp()
        {
            if(settings.serviceAssist!=ServiceAssist.AutoTopUp)return true;
            var spec=MechanicsOwnership.Resolve(profile,Build,profile.catalog).spec.fluids;
            double oil=Math.Max(0,spec.oilCapacityLitres-Build.fluids.oilLitres),coolant=Math.Max(0,spec.coolantCapacityLitres-Build.fluids.coolantLitres);
            if(oil+coolant<=0)return true;
            try { return PayNow(PaidEconomy.Quote(profile,profile.catalog,Costs,PricingPolicy,Payment(PaymentKind.Fluids,oil,coolant),DragLab.Domain.RuntimeEntropy.UtcNow.Ticks)); }
            catch(Exception e){notice=e.Message;return false;}
        }
    }
    public sealed partial class PhysicalDrivingBench
    {
        internal void SetTestFluids(FluidState fluids){Archive();Session.car.SetFluids(fluids);config.startingFluids=fluids.Copy();Session=Session.ContinueRecording(Recording);notice="Isolated fluids changed. Component wear/faults and driving input ownership retained.";}
        void FluidControls()
        {
            var car=Session.car;var f=car.state.fluids;var spec=car.spec.fluids;
            GUILayout.Label("OIL / "+f.oilLitres.ToString("F2")+" L / "+f.oilTemperatureC.ToString("F1")+" °C / "+SharedUnitSettings.Pressure(f.oilPressureBar)+" / condition "+f.oilCondition.ToString("P0"));
            GUILayout.Label("COOLANT / "+f.coolantLitres.ToString("F2")+" L / pressure "+SharedUnitSettings.Pressure(f.coolantPressureBar)+" / circulation "+car.state.coolantCirculation.ToString("P0"));
            GUILayout.Label("Oil burn "+(f.oilBurnLps*3600).ToString("F3")+" L/h / oil leak "+(f.oilLeakLps*3600).ToString("F3")+" L/h / coolant leak "+(f.coolantLeakLps*60).ToString("F3")+" L/min / vent "+(f.coolantVentLps*60).ToString("F3")+" L/min");
            var changed=f.Copy();float oil=(float)f.oilLitres,coolant=(float)f.coolantLitres,condition=(float)f.oilCondition,temp=(float)f.oilTemperatureC;
            bool edit=MassSlider("Test oil quantity (L)",ref oil,0,spec.oilCapacityLitres,"F2",spec.oilCapacityLitres);
            edit|=MassSlider("Test coolant quantity (L)",ref coolant,0,spec.coolantCapacityLitres,"F2",spec.coolantCapacityLitres);
            edit|=MassSlider("Test oil condition",ref condition,0,1,"F2",1);
            edit|=MassSlider("Test oil temperature (°C)",ref temp,20,200,"F1",80);
            if(edit){changed.oilLitres=oil;changed.coolantLitres=coolant;changed.oilCondition=condition;changed.oilTemperatureC=temp;SetTestFluids(changed);}
            if(Button("oil-fill","Top up test oil")){Archive();FluidDynamics.Add(car,true,30);Session=Session.ContinueRecording(Recording);}
            if(Button("coolant-fill","Top up test coolant")){Archive();FluidDynamics.Add(car,false,40);Session=Session.ContinueRecording(Recording);}
            if(Button("fluid-cooldown","Simulate 60 s parked cooldown / leaks")){Archive();car.CoolDown(60);Session=Session.ContinueRecording(Recording);}
            GUILayout.Label("Wear reset leaves fluid quantities unchanged. All quantities/faults/temperatures are captured in physical replays.");
        }
        void ServiceFaultControls(PartInstance setup,PartDefinition definition,PartInstance live)
        {
            IndividualEngineControls(setup,definition,live);
            if(live==null)return;
            GUILayout.Label("Live deposits "+live.condition.deposits.ToString("P0")+" / leak "+live.condition.leakSeverity.ToString("P0")+" / clearance "+live.condition.adjustmentError.ToString("P0"));
            bool enabled=GUI.enabled;GUI.enabled=enabled&&(Paused||!Session.car.state.launched);
            float deposits=(float)setup.condition.deposits,leak=(float)setup.condition.leakSeverity,clearance=(float)setup.condition.adjustmentError;
            var cat=definition.category;bool changed=false;
            if(cat==PartCategory.FuelSystem||cat==PartCategory.Ignition||cat==PartCategory.HeadIntake||cat==PartCategory.OilFilter)changed|=MassSlider("Starting deposits",ref deposits,0,1,"F2",0);
            if(cat==PartCategory.Cooling||cat==PartCategory.CoolingHoses||cat==PartCategory.OilPan||cat==PartCategory.OilCooler||cat==PartCategory.Induction||cat==PartCategory.FuelSystem)changed|=MassSlider("Starting leak severity",ref leak,0,1,"F2",0);
            if(cat==PartCategory.Followers||cat==PartCategory.TimingDrive)changed|=MassSlider("Starting clearance error",ref clearance,0,1,"F2",0);
            if(changed){setup.condition.deposits=deposits;setup.condition.leakSeverity=leak;setup.condition.adjustmentError=clearance;ResetBench(true);}
            GUI.enabled=enabled;
            foreach(var action in ServiceOperations.Available(definition,live.condition))if(GUILayout.Button("Test service: "+ServiceOperations.Description(action))){Archive();ServiceOperations.Apply(definition,live.condition,action,ServiceDynamics.Health(Session.car,PartCategory.CylinderBlock));Session=Session.ContinueRecording(Recording);}
        }
    }
}

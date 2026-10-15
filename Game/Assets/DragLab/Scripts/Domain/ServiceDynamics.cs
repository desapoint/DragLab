using System;
using System.Linq;
using DragLab.Domain.Mechanics;
namespace DragLab.Domain
{
    public static class ServiceDynamics
    {
        public static double Health(CarSimulation car,PartCategory slot){var p=car.mechanics.Part(slot);return p==null?0:p.condition.failed?0:DragMath.Clamp(p.condition.health,0,1);}
        static double HeaterHealth(CarSimulation car)
        {
            var p=car.mechanics.Part(PartCategory.GlowPlugs);
            if(p==null||p.condition.failed)return 0;
            var elements=p.condition.elements;
            if(elements==null||elements.Length==0)return Health(car,PartCategory.GlowPlugs);
            double total=0;foreach(var e in elements)total+=e.Failed?0:DragMath.Clamp(e.health,0,1);
            return total/elements.Length;
        }
        static double Loss(CarSimulation car,PartCategory slot){return (1-Health(car,slot))*car.driver.ConditionSeverity;}
        public static void Prepare(CarSimulation car)
        {
            var s=car.state;var v=car.spec.valvetrain;var f=car.spec.fluids;
            foreach(var required in new[]{PartCategory.Camshaft,PartCategory.Valves,PartCategory.Followers,PartCategory.ValveSprings,PartCategory.TimingDrive,PartCategory.CylinderBlock,PartCategory.Bearings,PartCategory.OilPan,PartCategory.OilPump})if(car.mechanics.Part(required)==null||(required==PartCategory.ValveSprings||required==PartCategory.Followers)&&Health(car,required)<=0){s.engineSupplyAvailable=false;s.stalled=true;}
            double sealing=DragMath.Clamp(1-.22*Loss(car,PartCategory.PistonRings)-.18*Loss(car,PartCategory.CylinderBlock)-.12*Loss(car,PartCategory.Valves),.4,1);
            var ignition=car.mechanics.Part(PartCategory.Ignition);double deposits=ignition==null?0:ignition.condition.deposits;
            var plugs=car.mechanics.Part(PartCategory.SparkPlugs);
            double spark=CombustionHardware.Diesel(car.spec)?1:1-.12*Loss(car,PartCategory.Ignition)-.1*deposits*car.driver.ConditionSeverity-(plugs==null?0:(1-plugs.condition.health)*.1*car.driver.ConditionSeverity+plugs.condition.deposits*.1*car.driver.ConditionSeverity);
            s.conditionCombustionEfficiency=sealing*spark;
            var fuel=car.mechanics.Part(PartCategory.FuelSystem);
            s.fuelMeteringWaste=(fuel==null?0:fuel.condition.deposits*.12+fuel.condition.leakSeverity*.25)*car.driver.ConditionSeverity;
            double camWear=Loss(car,PartCategory.Camshaft),followerWear=Loss(car,PartCategory.Followers);var follower=car.mechanics.Part(PartCategory.Followers);var timing=car.mechanics.Part(PartCategory.TimingDrive);
            double adjustment=(follower==null?0:follower.condition.adjustmentError)+(timing==null?0:timing.condition.adjustmentError+(1-Health(car,PartCategory.TimingDrive))*.4);
            double springHealth=1-Loss(car,PartCategory.ValveSprings)*.4;
            double safe=v.springSafeRpm*springHealth/Math.Max(1,v.profileDemandRpm/car.spec.redlineRpm);
            s.valveFloat=DragMath.Clamp((s.rpm/Math.Max(1000,safe)-1)*4,0,.75);
            var head=car.mechanics.Part(PartCategory.HeadIntake);double breathing=head==null?1:1-head.condition.deposits*.1*car.driver.ConditionSeverity;
            s.mechanicalPowerFactor*=breathing*s.conditionCombustionEfficiency*(1-camWear*.18-followerWear*.1)*(1-adjustment*.1*car.driver.ConditionSeverity)*(1-s.valveFloat);
            s.extraEngineFrictionNm=Loss(car,PartCategory.Bearings)*(4+s.rpm*.0015);
            EngineElements.Prepare(car);
            CombustionHardware.Prepare(car);
            if(CombustionHardware.Diesel(car.spec)){double heaters=HeaterHealth(car);double compression=DragMath.Clamp((car.spec.combustion.compressionRatio-12)/5,0,1);s.dieselColdFactor=s.engineTemperature+(1-s.engineTemperature)*(.55+.35*heaters)*compression;}else s.dieselColdFactor=1;
            double oilFraction=s.fluids.oilLitres/f.oilMinimumLitres;
            double pickup=DragMath.Clamp(oilFraction,0,1);
            // Excess oil after fitting a smaller pan can aerate under crank windage.
            pickup*=DragMath.Clamp(1-Math.Max(0,s.fluids.oilLitres/f.oilCapacityLitres-1)*.8,.2,1);
            if(oilFraction<1.25)pickup*=DragMath.Clamp(1-Math.Abs(s.acceleration)/DragMath.Gravity*(1-f.oilPanBaffling)*Math.Max(0,1.25-oilFraction),.1,1);
            var filter=car.mechanics.Part(PartCategory.OilFilter);double restriction=filter==null?.2:(1-filter.condition.deposits*.5)*(.4+.6*Health(car,PartCategory.OilFilter));
            double viscosity=DragMath.Clamp(1-Math.Max(0,s.fluids.oilTemperatureC-110)*.006,.3,1);
            s.fluids.oilPressureBar=Math.Max(0,Math.Min(5,.7+s.rpm/1800)*f.oilPumpScale*Health(car,PartCategory.OilPump)*pickup*restriction*viscosity*(.4+.6*Health(car,PartCategory.Bearings)));
            s.lubricationFactor=DragMath.Clamp(s.fluids.oilPressureBar/.55,0,1)*(.5+.5*s.fluids.oilCondition);
            double circuit=Math.Max(.1,f.coolantCapacityLitres-f.recoveryCapacityLitres);
            s.coolantCirculation=DragMath.Clamp(s.fluids.coolantLitres/(circuit*.65),0,1)*f.pumpFlowScale*Health(car,PartCategory.CoolantPump);
            s.engineCoolingFactor*=Math.Max(.05,s.coolantCirculation);
            foreach(var p in car.mechanics.parts)if(ServiceHardware.CoreFailure(car.mechanics.Definition(p).category)&&(p.condition.failed||p.condition.health<=0))s.engineFailed=true;
        }
        public static double CamMultiplier(VehicleSpec spec,double rpm)
        {
            var v=spec.valvetrain;if(v==null||v.camTorqueBias==0&&v.camTimingDegrees==0)return 1;
            double range=DragMath.Clamp((rpm+v.camTimingDegrees*65)/spec.redlineRpm,0,1);
            double profile=v.camTorqueBias*(range*1.8-.75);
            double timing=v.camTimingDegrees*.003*(1-range*2);
            return DragMath.Clamp(1+profile+timing,.65,1.3);
        }
        public static void Tick(CarSimulation car,double dt)
        {
            var s=car.state;if(car.driver.WearRate<=0)return;
            CombustionHardware.Tick(car,dt);
            bool running=!s.stalled&&!s.engineFailed&&s.rpm>car.spec.idleRpm*.4;
            foreach(var p in car.mechanics.parts) {
                var d=car.mechanics.Definition(p);var c=p.condition;if(c.failed)continue;
                if(ServiceHardware.Wears(d.category)&&running) {
                    double rpmStress=Math.Pow(Math.Max(0,s.rpm/car.spec.redlineRpm),2);
                    double rate=(.00000002+rpmStress*s.throttle*.00000006)*car.driver.WearRate;
                    if(d.category==PartCategory.Valves)rate+=s.valveFloat*s.valveFloat*.025*car.driver.WearRate;
                    if(d.category==PartCategory.Bearings||d.category==PartCategory.PistonRings||d.category==PartCategory.Camshaft||d.category==PartCategory.Followers)rate+=(1-s.lubricationFactor)*(1-s.lubricationFactor)*(.01+s.throttle*.03)*car.driver.WearRate;
                    bool waterPart=d.category==PartCategory.CoolantPump||d.category==PartCategory.CoolingCap||d.category==PartCategory.CoolingHoses;
                    c.temperatureC=waterPart?(car.mechanics.Part(PartCategory.Cooling)==null?20:car.mechanics.Part(PartCategory.Cooling).condition.temperatureC):s.fluids.oilTemperatureC;
                    rate+=Math.Pow(Math.Max(0,c.temperatureC/d.thermalLimitC-1),2)/d.overloadSeconds*car.driver.WearRate;
                    if(d.category==PartCategory.GlowPlugs)rate=.00000008*(1-s.engineTemperature)*car.driver.WearRate;
                    rate*=3000000000/Math.Max(1,d.wearEnergyJ);
                    EngineElements.Wear(p,rate*dt);c.serviceWearRemainder+=rate*dt; if(c.serviceWearRemainder>=.000001){c.health=(float)Math.Max(0,c.health-c.serviceWearRemainder);c.serviceWearRemainder=0;} c.runningSeconds+=dt;
                    if(c.health<=0){c.failed=true;c.failureReason=s.lubricationFactor<.4?"Oil starvation damage":s.valveFloat>.05?"Valve float / valve contact":"Mechanical wear depleted";}
                }
                if(running&&(d.category==PartCategory.FuelSystem||d.category==PartCategory.Ignition||d.category==PartCategory.SparkPlugs||d.category==PartCategory.HeadIntake||d.category==PartCategory.OilFilter))c.deposits=DragMath.Clamp(c.deposits+dt*(d.category==PartCategory.OilFilter?.0000007/Math.Max(1,d.wearEnergyJ/3000000000):.0000001)*car.driver.WearRate,0,1);
                if(running&&(d.category==PartCategory.Followers||d.category==PartCategory.TimingDrive))c.adjustmentError=DragMath.Clamp(c.adjustmentError+dt*.00000008*car.driver.WearRate,0,1);
            }
        }
    }
    public static class FluidDynamics
    {
        static double Leak(CarSimulation car,PartCategory c){var p=car.mechanics.Part(c);if(p==null)return c==PartCategory.CoolingHoses?.5:0;return Math.Max(p.condition.leakSeverity,p.condition.failed?.8:p.condition.health<.3?(.3-p.condition.health)*.5:0);}
        public static double Add(CarSimulation car,bool oil,double requested)
        {
            if(!MechanicsSchema.Finite(requested)||requested<0)throw new ArgumentException("Fluid addition must be finite and nonnegative.");
            var f=car.state.fluids;double capacity=oil?car.spec.fluids.oilCapacityLitres:car.spec.fluids.coolantCapacityLitres;
            double actual=Math.Min(requested,Math.Max(0,capacity-(oil?f.oilLitres:f.coolantLitres)));
            if(oil)f.oilLitres+=actual;else f.coolantLitres+=actual;car.RefreshFluidMass();return actual;
        }
        public static void Tick(CarSimulation car,double dt,bool parked=false)
        {
            var s=car.state;var f=s.fluids;var spec=car.spec.fluids;var cooler=car.mechanics.Part(PartCategory.Cooling);
            double coolantC=cooler==null?20:cooler.condition.temperatureC;
            bool running=!parked&&!s.stalled&&!s.engineFailed&&s.rpm>car.spec.idleRpm*.4;
            double pressureSeal=1-DragMath.Clamp(Leak(car,PartCategory.Cooling)+Leak(car,PartCategory.CoolingHoses),0,.98);
            double capHealth=ServiceDynamics.Health(car,PartCategory.CoolingCap);
            double target=DragMath.Clamp((coolantC-60)/55,0,1)*spec.capPressureBar*capHealth*pressureSeal;
            f.coolantPressureBar+= (target-f.coolantPressureBar)*(1-Math.Exp(-dt/(parked?30:3)));
            double cooling=(20+spec.oilCoolerWattsPerC*ServiceDynamics.Health(car,PartCategory.OilCooler))*(1+(parked?0:s.speed)*.02)*(f.oilTemperatureC-20);
            double heating=running?Math.Abs(s.engineHorsepower)*745.7*.015+1000:0;
            if(s.optimalThermals&&!parked)f.oilTemperatureC=90;else if(parked)f.oilTemperatureC=20+(f.oilTemperatureC-20)*Math.Exp(-dt*(20+spec.oilCoolerWattsPerC*ServiceDynamics.Health(car,PartCategory.OilCooler))/Math.Max(1000,f.oilLitres*.86*2000));else f.oilTemperatureC=DragMath.Clamp(f.oilTemperatureC+(heating-cooling)/Math.Max(1000,f.oilLitres*.86*2000)*dt,20,400);
            f.oilBurnLps=running?(.000005*car.spec.DisplacementLitres*(.2+s.throttle)*(.5+s.rpm/car.spec.redlineRpm))*(1+12*Math.Max(1-ServiceDynamics.Health(car,PartCategory.PistonRings),EngineElements.LocalLoss(car,PartCategory.PistonRings))+8*Math.Max(1-ServiceDynamics.Health(car,PartCategory.ValveSeals),EngineElements.LocalLoss(car,PartCategory.ValveSeals))+6*(car.induction.turboCount>0?Leak(car,PartCategory.Induction):0)):0;
            f.oilLeakLps=(Leak(car,PartCategory.OilPan)+Leak(car,PartCategory.OilCooler))*(.0002+(running?f.oilPressureBar*.0005:0));
            f.coolantLeakLps=(Leak(car,PartCategory.Cooling)+Leak(car,PartCategory.CoolingHoses))*(.0005+f.coolantPressureBar*.004);
            double boil=108+f.coolantPressureBar*20;
            // Recovery reservoir retains ordinary expansion. Only boiling beyond its buffer vents liquid.
            double expansion=Math.Max(0,coolantC-20)*.00045*Math.Max(0,spec.coolantCapacityLitres-spec.recoveryCapacityLitres);
            double expansionOverflow=Math.Max(0,expansion-spec.recoveryCapacityLitres-Math.Max(0,spec.coolantCapacityLitres-f.coolantLitres));
            f.coolantVentLps=s.optimalThermals?0:Math.Max(0,coolantC-boil-3)*.00015+expansionOverflow*.002;
            double depletion=car.driver.FluidDepletionRate;
            f.oilBurnLps*=depletion;f.oilLeakLps*=depletion;f.coolantLeakLps*=depletion;f.coolantVentLps*=depletion;
            double burn=Math.Min(f.oilLitres,f.oilBurnLps*dt);f.oilLitres-=burn;f.oilConsumedLitres+=burn;
            double oilLeak=Math.Min(f.oilLitres,f.oilLeakLps*dt);f.oilLitres-=oilLeak;f.oilLeakedLitres+=oilLeak;
            double coolantLeak=Math.Min(f.coolantLitres,(f.coolantLeakLps+f.coolantVentLps)*dt);f.coolantLitres-=coolantLeak;f.coolantLostLitres+=coolantLeak;
            if(running)f.oilCondition=DragMath.Clamp(f.oilCondition-dt*(.0000002+Math.Max(0,f.oilTemperatureC-120)*.00000008)*car.driver.FluidAgingRate,0,1);
            if(parked){f.serviceElapsedSeconds+=dt;if(cooler!=null)cooler.condition.temperatureC=20+(coolantC-20)*Math.Exp(-dt/120);}
            ServiceDynamics.Tick(car,parked?0:dt);EngineElements.TickLocal(car,parked?0:dt);if(!parked){car.mechanics.Prepare(s);ServiceDynamics.Prepare(car);}if(parked)f.oilPressureBar=0;if(s.engineFailed)s.engineTorqueNm=s.engineHorsepower=s.transmittedTorqueNm=0;car.RefreshFluidMass();
        }
    }
    public sealed partial class CarSimulation
    {
        public void RefreshFluidMass(){UpdateMass();}
        public void SetFluids(FluidState fluids){if(fluids==null||fluids.Validate(spec.fluids)!=null)throw new ArgumentException("Invalid fluid state.");state.fluids=fluids.Copy();RefreshFluidMass();}
        public void ApplyRaceTemperatures(RaceTemperatureMode mode,long previousUtcTicks,long nowUtcTicks)
        {
            if(!Enum.IsDefined(typeof(RaceTemperatureMode),mode)||previousUtcTicks<0||nowUtcTicks<0||previousUtcTicks>DateTime.MaxValue.Ticks||nowUtcTicks>DateTime.MaxValue.Ticks)throw new ArgumentException("Invalid race temperature mode/clock.");
            // Thermal difficulty must not bypass the independent parked fluid/leak rules.
            if(previousUtcTicks>0&&nowUtcTicks>previousUtcTicks)CoolDown((nowUtcTicks-previousUtcTicks)/(double)TimeSpan.TicksPerSecond);
            if(driver.optimalThermals)mode=RaceTemperatureMode.Optimal;
            if(mode==RaceTemperatureMode.Live)return;
            bool optimal=mode==RaceTemperatureMode.Optimal;
            foreach(var part in mechanics.parts) {
                var d=mechanics.Definition(part);
                if(d.category==PartCategory.Tires&&part.condition.tires!=null){for(int i=0;i<part.condition.tires.Length;i++)part.condition.tires[i].temperatureC=optimal?mechanics.TireDefinition(i).tireOptimalC:20;TireDynamics.SyncSummary(part.condition);}
                else part.condition.temperatureC=optimal?MechanicalStress.OptimalTemperature(d.category):20;
            }
            state.fluids.oilTemperatureC=optimal?90:20;state.fluids.oilPressureBar=state.fluids.coolantPressureBar=0;state.engineTemperature=optimal?1:0;state.exhaustHeat=0;
        }
        public void CoolDown(double seconds)
        {
            if(!MechanicsSchema.Finite(seconds)||seconds<0||seconds>DateTime.MaxValue.Ticks/(double)TimeSpan.TicksPerSecond)throw new ArgumentException("Invalid elapsed cooldown time.");
            // Resolve the hot transient each second; after an hour the remaining parked
            // tail uses the same exponential cooling and retained leak rates in one step.
            for(double time=0;time<seconds;) {
                double dt=time<3600?Math.Min(1,seconds-time):seconds-time;
                foreach(var p in mechanics.parts) {
                    var d=mechanics.Definition(p);
                    // Cooling's liquid/pressure circuit is advanced by FluidDynamics below.
                    if(d.category!=PartCategory.Cooling&&d.category!=PartCategory.Tires)p.condition.temperatureC=20+(p.condition.temperatureC-20)*Math.Exp(-dt*d.coolingWattsPerC/Math.Max(1,d.heatCapacityJPerC));
                    if(d.category==PartCategory.Tires&&p.condition.tires!=null) {
                        double capacity=d.heatCapacityJPerC*Math.Max(.1,wheels.tireMassKg/Math.Max(.1,wheels.referenceTireMassKg));
                        for(int i=0;i<p.condition.tires.Length;i++){var tire=p.condition.tires[i];var tireSpec=mechanics.TireDefinition(i);double localCapacity=tireSpec.heatCapacityJPerC*Math.Max(.1,wheels.tireMasses[i]/Math.Max(.1,wheels.referenceTireMassKg));tire.temperatureC=20+(tire.temperatureC-20)*Math.Exp(-dt*tireSpec.coolingWattsPerC/localCapacity);}TireDynamics.SyncSummary(p.condition);
                    }
                }
                FluidDynamics.Tick(this,dt,true);
                time+=dt;
            }
        }
    }
}

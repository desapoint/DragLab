using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain.Mechanics;
namespace DragLab.Domain {
 public enum RivalStyle { Novice, Smooth, Aggressive, Conservative, Confident }
 [Serializable] public sealed class RivalPart {
 public PartCategory category; public string definitionId; public float health=1;
 }
 [Serializable] public sealed class RivalSetup {
 public int authoringVersion;public bool authored; public RivalStyle style; public int seed=1;
 public float health=.97f, mileageKm=25000, reactionSeconds=.25f, throttle=1, shiftFraction=1;
 public float hesitationInterval, hesitationSeconds, hesitationThrottle=.3f;
 public List<RivalPart> parts=new List<RivalPart>();
 public string driverId;public float limiterHoldSeconds;public bool riskWhenBehind,flatShiftWhenRisking;public float riskBehindMeters=30,riskLimiterHoldSeconds=.6f;
 public bool confidenceFromBuild;public float confidenceRatio=1.75f,openingThrottle=.8f,openingUntilMeters=110,openingEmergencyGap=35,confidenceReactionDelay=.35f;
 public float reactionJitterSeconds=.1f,shiftJitterRpm=100,shiftDelayMin=.02f,shiftDelayMax=.12f,throttleWander=.018f;
 }
 [Serializable] public sealed class RivalBehavior {
 public RivalStyle style;public int seed;public double throttle=1,interval,duration,hesitationThrottle;
 public string driverId;public double limiterHoldSeconds,riskBehindMeters=30,riskLimiterHoldSeconds=.6;public bool riskWhenBehind,flatShiftWhenRisking;
 public bool confidenceFromBuild,confidenceActivated;public double confidenceRatio=1.75,openingThrottle=.8,openingUntilMeters=110,openingEmergencyGap=35,confidenceReactionDelay=.35,powerAdvantage,torqueAdvantage,powerToWeightAdvantage;
 [NonSerialized] public bool OpeningPhaseEnded;
 public double shiftJitterRpm=100,shiftDelayMin=.02,shiftDelayMax=.12,throttleWander=.018;public bool planningComplete;
 [NonSerialized] double decisionRemaining=-1;[NonSerialized] double gearJitter;
 [NonSerialized] public bool RiskActive;[NonSerialized] public int RiskEngagements;[NonSerialized] public double HeldLimiterSeconds;[NonSerialized] double limiterTime;[NonSerialized] int holdGear;
 public static bool Valid(RivalBehavior b)=>b!=null&&Enum.IsDefined(typeof(RivalStyle),b.style)&&new[]{b.throttle,b.interval,b.duration,b.hesitationThrottle,b.limiterHoldSeconds,b.riskBehindMeters,b.riskLimiterHoldSeconds,b.confidenceRatio,b.openingThrottle,b.openingUntilMeters,b.openingEmergencyGap,b.confidenceReactionDelay,b.powerAdvantage,b.torqueAdvantage,b.powerToWeightAdvantage,b.shiftJitterRpm,b.shiftDelayMin,b.shiftDelayMax,b.throttleWander}.All(MechanicsSchema.Finite)&&b.throttle>0&&b.throttle<=1&&b.interval>=0&&b.duration>=0&&b.duration<=b.interval&&b.hesitationThrottle>=0&&b.hesitationThrottle<=1&&b.limiterHoldSeconds>=0&&b.limiterHoldSeconds<=3&&b.riskBehindMeters>0&&b.riskLimiterHoldSeconds>=0&&b.riskLimiterHoldSeconds<=3&&b.confidenceRatio>=1&&b.confidenceRatio<=4&&b.openingThrottle>0&&b.openingThrottle<=1&&b.openingUntilMeters>0&&b.openingUntilMeters<402&&b.openingEmergencyGap>0&&b.confidenceReactionDelay>=0&&b.confidenceReactionDelay<=3&&b.powerAdvantage>=0&&b.torqueAdvantage>=0&&b.powerToWeightAdvantage>=0&&b.shiftJitterRpm>=0&&b.shiftJitterRpm<=1000&&b.shiftDelayMin>=0&&b.shiftDelayMax>=b.shiftDelayMin&&b.shiftDelayMax<=1&&b.throttleWander>=0&&b.throttleWander<=.3;
 public static double Noise(int seed,int index){unchecked{uint x=(uint)seed+(uint)index*0x9e3779b9u;x^=x>>16;x*=0x7feb352du;x^=x>>15;x*=0x846ca68bu;x^=x>>16;return x/(double)uint.MaxValue;}}
 static (double hp,double torque) EstimatedOutput(CarSimulation car){double hp=0,torque=0;for(double rpm=car.spec.idleRpm;rpm<=car.spec.redlineRpm;rpm+=100){double t=Math.Max(0,InductionDynamics.SteadyOutput(car.induction,rpm).engineTorqueNm*car.state.mechanicalPowerFactor-car.state.extraEngineFrictionNm);if(car.state.deliveryPowerCapacityHp>0)t=Math.Min(t,car.state.deliveryPowerCapacityHp*7127/rpm);if(car.state.engineFailed||!car.state.engineSupplyAvailable)t=0;torque=Math.Max(torque,t);hp=Math.Max(hp,t*rpm/7127);}return(hp,torque);}
 public void PlanAgainst(CarSimulation rival,CarSimulation player,bool refresh=false){if(planningComplete&&!refresh)return;if(planningComplete&&confidenceActivated)rival.driver.reactionSeconds-=confidenceReactionDelay;planningComplete=true;if(!confidenceFromBuild)return;var mine=EstimatedOutput(rival);var other=EstimatedOutput(player);powerAdvantage=mine.hp/Math.Max(1,other.hp);torqueAdvantage=mine.torque/Math.Max(1,other.torque);powerToWeightAdvantage=powerAdvantage*player.wheels.vehicleMass/Math.Max(1,rival.wheels.vehicleMass);confidenceActivated=powerAdvantage>=confidenceRatio&&torqueAdvantage>=confidenceRatio&&powerToWeightAdvantage>=1.35;if(confidenceActivated)rival.driver.reactionSeconds+=confidenceReactionDelay;}
 public DriverInput DriveInput(CarSimulation car,double greenTime,double playerLeadMeters,double dt){
 var s=car.state;if(s.gear!=holdGear){holdGear=s.gear;limiterTime=0;decisionRemaining=-1;gearJitter=(Noise(seed,100+s.gear)*2-1)*shiftJitterRpm;}
 bool wasRisking=RiskActive;if(riskWhenBehind&&s.launched&&!s.finished){if(playerLeadMeters>=riskBehindMeters)RiskActive=true;else if(playerLeadMeters<riskBehindMeters*.5)RiskActive=false;}else RiskActive=false;if(RiskActive&&!wasRisking)RiskEngagements++;
 var input=Input(s,greenTime);if(confidenceActivated&&!OpeningPhaseEnded){if(s.distance>=openingUntilMeters||playerLeadMeters>=openingEmergencyGap||RiskActive)OpeningPhaseEnded=true;else input.throttle*=openingThrottle;}if(RiskActive)input.throttle=1;
 bool custom=limiterHoldSeconds>0||riskWhenBehind||shiftJitterRpm>0||shiftDelayMax>0;car.driver.shifting=custom?ShiftMode.Sequential:ShiftMode.Automatic;
 if(!custom||car.transmission.type==TransmissionType.CVT)return input;
 double hold=limiterHoldSeconds+(RiskActive?riskLimiterHoldSeconds:0),threshold=Math.Max(car.spec.idleRpm*1.3,Math.Min(car.tune.shiftRpm,car.ShiftPoint(s.gear))+gearJitter);
 if(hold>0)threshold=car.engine.limiter==LimiterStyle.None?car.spec.redlineRpm:car.engine.limitRpm-(car.engine.limiter==LimiterStyle.Soft?car.engine.data.softBandRpm:100);
 if(s.rpm>=threshold&&s.shiftRemaining<=0){limiterTime+=dt;if(hold>0)HeldLimiterSeconds+=dt;}
 if(decisionRemaining<0&&s.rpm>=threshold)decisionRemaining=shiftDelayMin+(shiftDelayMax-shiftDelayMin)*Noise(seed,200+s.gear);else if(decisionRemaining>=0)decisionRemaining=Math.Max(0,decisionRemaining-dt);
 bool shift=s.launched&&s.gear<car.spec.gears.Length&&s.speed>1&&s.clutch<.2&&(s.clutchSlipRpm<250||car.transmission.type==TransmissionType.Automatic)&&s.shiftRemaining<=0&&s.rpm>=threshold&&limiterTime>=hold&&decisionRemaining==0;
 input.shiftUp=shift;
 if(car.transmission.type==TransmissionType.Manual&&(shift||s.shiftRemaining>0)&&!(RiskActive&&flatShiftWhenRisking))input.throttle=0;
 return input;
 }
 public DriverInput Input(CarState state,double greenTime){
 double pedal=throttle;
 double phase=Math.Max(0,greenTime)/.65;int sample=(int)phase;double blend=phase-sample;double wandering=Noise(seed,300+sample)*(1-blend)+Noise(seed,301+sample)*blend;pedal*=1-throttleWander*wandering;
 if(interval>0&&greenTime>=0&&(greenTime+Math.Abs(seed%100)*.01)%interval<duration)pedal*=hesitationThrottle;
 return new DriverInput{throttle=pedal};
 }
 }
 [Serializable] public sealed class RivalEntry {
 public string model;public Tune tune;public DriverSettings driver;public RivalBehavior behavior;
 public float mileageKm;public List<RivalPart> hardware;
 }
 public sealed class RivalCapture {
 public VehicleSpec spec;public Tune tune;public DriverSettings driver;public List<PartInstance> components;public FluidState fluids;public RivalBehavior behavior;public float mileageKm;
 public CarSimulation Create(){var car=new CarSimulation(spec.Copy(),tune.Copy(),driver.Copy(),components);car.SetFluids(fluids);return car;}
 public RivalEntry Entry(RaceSimulation race=null)=>new RivalEntry{model=spec.id,tune=tune.Copy(),driver=(race?.opponent.driver??driver).Copy(),behavior=JsonUtility.FromJson<RivalBehavior>(JsonUtility.ToJson(race?.opponentBehavior??behavior)),mileageKm=mileageKm,hardware=components.Select(p=>new RivalPart{category=(PartCategory)Enum.Parse(typeof(PartCategory),p.slot),definitionId=p.definitionId,health=p.condition.health}).ToList()};
 }
 public static class CareerOpponents {
 public static string ValidateEntry(RivalEntry r){if(r==null)return null;if(string.IsNullOrEmpty(r.model)||r.tune==null||r.driver==null||r.behavior==null||r.hardware==null||r.hardware.Count==0||!MechanicsSchema.Finite(r.mileageKm)||r.mileageKm<0||!MechanicsSchema.Finite(r.driver.reactionSeconds)||r.driver.reactionSeconds<0)return "Invalid captured rival.";if(!RivalBehavior.Valid(r.behavior))return "Invalid captured rival behavior.";var categories=new HashSet<PartCategory>();foreach(var p in r.hardware)if(p==null||!Enum.IsDefined(typeof(PartCategory),p.category)||!categories.Add(p.category)||string.IsNullOrEmpty(p.definitionId)||!MechanicsSchema.Finite(p.health)||p.health<=0||p.health>1)return "Invalid captured rival hardware.";return null;}
 public static RivalCapture Capture(CareerEventDefinition e,MechanicalCatalog catalog,double difficulty=1,int? attemptSeed=null){
 if(e==null||e.opponent==null)throw new InvalidOperationException("Missing rival vehicle.");
 var setup=e.rival??new RivalSetup();var tune=e.opponentTune.Copy();var map=catalog.Recipe(e.opponent.spec.id,tune);
 if(setup.authored){
 if(!Enum.IsDefined(typeof(RivalStyle),setup.style)||new[]{setup.health,setup.reactionSeconds,setup.throttle,setup.shiftFraction,setup.mileageKm,setup.hesitationInterval,setup.hesitationSeconds,setup.hesitationThrottle}.Any(v=>float.IsNaN(v)||float.IsInfinity(v))||setup.seed==0||setup.health<=0||setup.health>1||setup.reactionSeconds<0||setup.throttle<=0||setup.throttle>1||setup.shiftFraction<.5||setup.shiftFraction>1.1||setup.mileageKm<0||setup.hesitationInterval<0||setup.hesitationSeconds<0||setup.hesitationSeconds>setup.hesitationInterval||setup.hesitationThrottle<0||setup.hesitationThrottle>1)throw new InvalidOperationException("Invalid authored rival behavior.");
 if(setup.parts==null)throw new InvalidOperationException("Missing authored rival hardware.");var seen=new HashSet<PartCategory>();
 foreach(var part in setup.parts){if(part==null||!seen.Add(part.category)||!Enum.IsDefined(typeof(PartCategory),part.category)||!MechanicsSchema.Finite(part.health)||part.health<=0||part.health>1)throw new InvalidOperationException("Invalid rival part fitment/condition.");var id=string.IsNullOrEmpty(part.definitionId)?map[part.category]:part.definitionId;var definition=catalog.Find(id);if(definition==null||definition.category!=part.category||!catalog.Fits(e.opponent.spec,definition))throw new InvalidOperationException("Invalid rival part fitment/condition.");map[part.category]=id;}
 tune.shiftRpm*=setup.shiftFraction;
 }
 var build=catalog.Resolve(e.opponent.spec.id,map,tune);if(!build.Drivable)throw new InvalidOperationException(string.Join("; ",build.errors));
 var components=new List<PartInstance>();
 foreach(var pair in map){var d=catalog.Find(pair.Value);var assigned=setup.parts?.FirstOrDefault(p=>p.category==pair.Key);double h=!setup.authored||!PartSemantics.Wears(pair.Key)?1:assigned?.health??DragMath.Clamp(setup.health+((int)pair.Key%5-2)*.003,.4,1);
 var p=new PartInstance{instanceId="rival/"+e.stableId+"/"+setup.seed+"/"+pair.Key,definitionId=d.id,slot=pair.Key.ToString(),condition=new ComponentCondition{health=(float)h,distanceM=setup.authored?setup.mileageKm*1000:0}};
 EngineElements.Ensure(build.spec,d,p);ClutchHardware.Ensure(d,p.condition);if(pair.Key==PartCategory.Tires){TireDynamics.SeedNew(p.condition);for(int i=0;i<4;i++){var tire=p.condition.tires[i];tire.id=p.instanceId+"/tire/"+i;tire.definitionId=d.id;}}components.Add(p);
 }
 double tier=difficulty<.5?.2:difficulty>1.5?-.12:0;
 int drivingSeed=attemptSeed??setup.seed;if(!MechanicsSchema.Finite(setup.reactionJitterSeconds)||setup.reactionJitterSeconds<0||setup.reactionJitterSeconds>.5)throw new InvalidOperationException("Invalid reaction variance.");double jitter=setup.authored?(RivalBehavior.Noise(drivingSeed,1)*2-1)*setup.reactionJitterSeconds:0;
 var driver=new DriverSettings{reactionSeconds=Math.Max(.06,(setup.authored?setup.reactionSeconds:.22)+tier+jitter),throttleAssist=false,tractionAssist=setup.style!=RivalStyle.Aggressive,wearMode=WearMode.Full};
 var behavior=new RivalBehavior{driverId=setup.driverId,style=setup.style,seed=drivingSeed,throttle=setup.authored?setup.throttle:1,interval=setup.authored?setup.hesitationInterval:0,duration=setup.hesitationSeconds,hesitationThrottle=setup.hesitationThrottle,limiterHoldSeconds=setup.limiterHoldSeconds,riskWhenBehind=setup.riskWhenBehind,riskBehindMeters=setup.riskBehindMeters,riskLimiterHoldSeconds=setup.riskLimiterHoldSeconds,flatShiftWhenRisking=setup.flatShiftWhenRisking,confidenceFromBuild=setup.confidenceFromBuild,confidenceRatio=setup.confidenceRatio,openingThrottle=setup.openingThrottle,openingUntilMeters=setup.openingUntilMeters,openingEmergencyGap=setup.openingEmergencyGap,confidenceReactionDelay=setup.confidenceReactionDelay,shiftJitterRpm=setup.shiftJitterRpm,shiftDelayMin=setup.shiftDelayMin,shiftDelayMax=setup.shiftDelayMax,throttleWander=setup.throttleWander};
 if(!RivalBehavior.Valid(behavior))throw new InvalidOperationException("Invalid authored rival habits/risk settings.");
 return new RivalCapture{spec=build.spec.Copy(),tune=build.tune.Copy(),driver=driver,components=components,fluids=FluidState.Full(build.spec.fluids),behavior=behavior,mileageKm=setup.mileageKm};
 }
 }
}

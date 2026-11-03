using System;
using System.Collections.Generic;
using System.Linq;

namespace DragLab.Domain.Mechanics
{
    // Result nodes are explicit value DTOs. Identity-bearing strings are represented
    // by CartApplyIdentityRef so candidate-private symbols never escape Evaluate.
    internal sealed class CartApplyRowEffect
    {
        readonly CartApplySupplyDelta[] supplyDeltas;
        internal readonly int RowIndex, QuantityOrdinal;
        internal readonly CartAction RequestedAction;
        internal readonly CartApplyBranch Branch;
        internal readonly string DefinitionId,AssetId,InstallTargetId;
        internal readonly CartApplyIdentityRef? Identity;
        internal readonly int? PaidAmount;
        internal readonly CartApplyQuote Quote;
        internal CartApplySupplyDelta[] SupplyDeltas => Clone(supplyDeltas);
        internal CartApplyRowEffect(int rowIndex,int quantityOrdinal,CartAction action,CartApplyBranch branch,
            string definitionId=null,string assetId=null,CartApplyIdentityRef? identity=null,string installTargetId=null,int? paidAmount=null,
            CartApplyQuote quote=null,IEnumerable<CartApplySupplyDelta> supplyDeltas=null)
        {RowIndex=rowIndex;QuantityOrdinal=quantityOrdinal;RequestedAction=action;Branch=branch;DefinitionId=definitionId;AssetId=assetId;InstallTargetId=installTargetId;Identity=identity;PaidAmount=paidAmount;Quote=quote;this.supplyDeltas=Copy(supplyDeltas);}
        static CartApplySupplyDelta[] Copy(IEnumerable<CartApplySupplyDelta> values)=>values==null?Array.Empty<CartApplySupplyDelta>():values.ToArray();
        static CartApplySupplyDelta[] Clone(CartApplySupplyDelta[] values)=>(CartApplySupplyDelta[])values.Clone();
    }

    internal abstract class CartApplyRecordSnapshot
    {
        internal readonly CartApplyIdentityRef Identity;
        internal readonly bool Deleted;
        internal readonly int SourceOrdinal, InsertionOrdinal;
        protected CartApplyRecordSnapshot(CartApplyIdentityRef identity,bool deleted,int sourceOrdinal,int insertionOrdinal)
        {Identity=identity;Deleted=deleted;SourceOrdinal=sourceOrdinal;InsertionOrdinal=insertionOrdinal;}
    }
    internal sealed class CartApplyElementSnapshot
    {
        internal readonly string TypeName, Role, DefinitionId, FailureReason;
        internal readonly CartApplyIdentityRef Id;
        internal readonly float Health, Severity;
        internal readonly int Cylinder, Valve;
        internal readonly double WearRemainder, TemperatureC, SlipEnergyJ, DistanceM, LastContactWorkJ, ConsumedLife, RunningSeconds;
        internal readonly ElementFault Fault;
        internal readonly bool TireFailed;
        internal readonly int ObjectOrdinal;
        internal CartApplyElementSnapshot(EngineElement value,Func<string,CartApplyIdentityRef> identity,CartApplyCaptureContext capture)
        {
            ObjectOrdinal=value==null?-1:capture.Ordinal(value);
            TypeName=value?.GetType().FullName;Id=value==null?CartApplyIdentityRef.Existing(null):identity(value.id);
            DefinitionId=value?.definitionId;Health=value?.health??0;Role=value?.role;Cylinder=value?.cylinder??0;Valve=value?.valve??0;
            Severity=value?.severity??0;WearRemainder=value?.wearRemainder??0;Fault=value?.fault??ElementFault.None;
            var tire=value as TireCondition;TireFailed=tire!=null&&tire.failed;FailureReason=tire?.failureReason;
            TemperatureC=tire?.temperatureC??0;SlipEnergyJ=tire?.slipEnergyJ??0;DistanceM=tire?.distanceM??0;
            LastContactWorkJ=tire?.lastContactWorkJ??0;ConsumedLife=tire?.consumedLife??0;RunningSeconds=tire?.runningSeconds??0;
        }
    }
    internal sealed class CartApplyCaptureContext
    {
        readonly Dictionary<object,int> ordinals=new Dictionary<object,int>(ReferenceComparer<object>.Instance);int next;
        internal int Count=>ordinals.Count;
        internal int Ordinal(object value){if(value==null)return -1;if(ordinals.TryGetValue(value,out var ordinal))return ordinal;ordinals[value]=ordinal=next++;return ordinal;}
        internal void Clear()=>ordinals.Clear();
    }
    internal sealed class CartApplyClutchSnapshot
    {
        internal readonly int Version; internal readonly double OilLitres,OilCondition;
        internal CartApplyClutchSnapshot(ClutchConditionState value){Version=value.version;OilLitres=value.oilLitres;OilCondition=value.oilCondition;}
    }
    internal sealed class CartApplyConditionSnapshot
    {
        readonly CartApplyElementSnapshot[] elements;readonly CartApplyElementSnapshot[] tires;
        internal readonly float Health;internal readonly bool Failed;internal readonly string FailureReason,LastService;
        internal readonly double RunningSeconds,DistanceM,StressExposure,TemperatureC,SlipEnergyJ,Deposits,LeakSeverity,AdjustmentError,ServiceWearRemainder;
        internal readonly int ServiceCount,ReconditionCount;internal readonly CartApplyClutchSnapshot Clutch;
        internal CartApplyElementSnapshot[] Elements=>elements==null?null:(CartApplyElementSnapshot[])elements.Clone();
        internal CartApplyElementSnapshot[] Tires=>tires==null?null:(CartApplyElementSnapshot[])tires.Clone();
        internal CartApplyConditionSnapshot(ComponentCondition value,Func<string,CartApplyIdentityRef> identity,CartApplyCaptureContext capture)
        {Health=value.health;Failed=value.failed;FailureReason=value.failureReason;RunningSeconds=value.runningSeconds;DistanceM=value.distanceM;StressExposure=value.stressExposure;TemperatureC=value.temperatureC;SlipEnergyJ=value.slipEnergyJ;Deposits=value.deposits;LeakSeverity=value.leakSeverity;AdjustmentError=value.adjustmentError;ServiceWearRemainder=value.serviceWearRemainder;ServiceCount=value.serviceCount;ReconditionCount=value.reconditionCount;LastService=value.lastService;Clutch=value.clutch==null?null:new CartApplyClutchSnapshot(value.clutch);elements=value.elements==null?null:value.elements.Select(e=>e==null?null:new CartApplyElementSnapshot(e,identity,capture)).ToArray();var sourceElements=value.elements;EngineElement[] sourceTires=null;if(sourceElements==null||sourceElements.Length==0||!(sourceElements[0] is TireCondition))sourceTires=Array.Empty<EngineElement>();else if(sourceElements.Length==4&&sourceElements.All(e=>e is TireCondition))sourceTires=sourceElements;tires=sourceTires==null?null:sourceTires.Select(e=>e==null?null:new CartApplyElementSnapshot(e,identity,capture)).ToArray();}
    }
    internal sealed class CartApplyPartSnapshot : CartApplyRecordSnapshot
    {
        internal readonly int Version;internal readonly CartApplyIdentityRef InstanceId;internal readonly string DefinitionId,StorageId;
        internal readonly PartLocation Location;internal readonly CartApplyIdentityRef OwnerId;internal readonly string Slot;
        internal readonly CartApplyConditionSnapshot Condition;
        internal CartApplyPartSnapshot(CartApplyIdentityRef identity,bool deleted,int sourceOrdinal,int insertionOrdinal,PartInstance value,Func<string,CartApplyIdentityRef> map,CartApplyCaptureContext capture)
            :base(identity,deleted,sourceOrdinal,insertionOrdinal)
        {if(value==null)return;Version=value.version;InstanceId=map(value.instanceId);DefinitionId=value.definitionId;StorageId=value.storageId;Location=value.location;OwnerId=map(value.ownerId);Slot=value.slot;Condition=value.condition==null?null:new CartApplyConditionSnapshot(value.condition,map,capture);}
    }
    internal sealed class CartApplyBindingSnapshot
    {internal readonly string Slot;internal readonly CartApplyIdentityRef PartId;internal CartApplyBindingSnapshot(SlotBinding value,Func<string,CartApplyIdentityRef> map){Slot=value?.slot;PartId=value==null?CartApplyIdentityRef.Existing(null):map(value.partInstanceId);}}
    internal sealed class CartApplyFluidSnapshot
    {
        internal readonly double OilLitres,CoolantLitres,OilTemperatureC,OilPressureBar,CoolantPressureBar,OilCondition,OilConsumedLitres,OilLeakedLitres,CoolantLostLitres,ServiceElapsedSeconds,OilBurnLps,OilLeakLps,CoolantLeakLps,CoolantVentLps;
        internal CartApplyFluidSnapshot(FluidState v){OilLitres=v.oilLitres;CoolantLitres=v.coolantLitres;OilTemperatureC=v.oilTemperatureC;OilPressureBar=v.oilPressureBar;CoolantPressureBar=v.coolantPressureBar;OilCondition=v.oilCondition;OilConsumedLitres=v.oilConsumedLitres;OilLeakedLitres=v.oilLeakedLitres;CoolantLostLitres=v.coolantLostLitres;ServiceElapsedSeconds=v.serviceElapsedSeconds;OilBurnLps=v.oilBurnLps;OilLeakLps=v.oilLeakLps;CoolantLeakLps=v.coolantLeakLps;CoolantVentLps=v.coolantVentLps;}
    }
    internal sealed class CartApplyFuelSnapshot
    {internal readonly double Litres,CumulativeUsedLitres,FlowLitresPerSecond;internal CartApplyFuelSnapshot(FuelState v){Litres=v.litres;CumulativeUsedLitres=v.cumulativeUsedLitres;FlowLitresPerSecond=v.flowLitresPerSecond;}}
    internal sealed class CartApplyDriverSnapshot
    {
        internal readonly ShiftMode Shifting;internal readonly LaunchMode Launch;internal readonly bool ThrottleAssist,TractionAssist,RevMatchAssist,ShutdownBrakeAssist,OptimalThermals,ClutchAssist,StallPrevention,WarmupBeforeRace;
        internal readonly double ReactionSeconds;internal readonly float ClutchReleaseSeconds,SoundVolume,WearMultiplier;internal readonly WearMode WearMode;internal readonly FuelUsageMode FuelUsage;internal readonly int GuaranteedFittingParts;
        internal readonly ConsumptionMode FluidDepletion,FluidAging;internal readonly ConditionEffectMode ConditionEffects;internal readonly ServiceAssist ServiceAssist;internal readonly RaceTemperatureMode RaceTemperatures;internal readonly SimulationDifficulty SimulationDifficulty;
        internal CartApplyDriverSnapshot(DriverSettings v){Shifting=v.shifting;Launch=v.launch;ThrottleAssist=v.throttleAssist;TractionAssist=v.tractionAssist;RevMatchAssist=v.revMatchAssist;ReactionSeconds=v.reactionSeconds;ClutchReleaseSeconds=v.clutchReleaseSeconds;SoundVolume=v.soundVolume;ShutdownBrakeAssist=v.shutdownBrakeAssist;WearMode=v.wearMode;FuelUsage=v.fuelUsage;GuaranteedFittingParts=v.guaranteedFittingParts;FluidDepletion=v.fluidDepletion;FluidAging=v.fluidAging;ConditionEffects=v.conditionEffects;ServiceAssist=v.serviceAssist;WearMultiplier=v.wearMultiplier;OptimalThermals=v.optimalThermals;ClutchAssist=v.clutchAssist;StallPrevention=v.stallPrevention;RaceTemperatures=v.raceTemperatures;WarmupBeforeRace=v.warmupBeforeRace;SimulationDifficulty=v.simulationDifficulty;}
    }
    internal sealed class CartApplyPassSnapshot
    {
        internal readonly double DistanceM,ElapsedSeconds,ReactionSeconds,TrapKmh,SixtyFootSeconds,EighthSeconds;internal readonly CartApplyTuneSnapshot Tune;internal readonly CartApplyDriverSnapshot Driving;
        internal CartApplyPassSnapshot(PassRecord v){DistanceM=v.distanceM;ElapsedSeconds=v.elapsedSeconds;ReactionSeconds=v.reactionSeconds;TrapKmh=v.trapKmh;SixtyFootSeconds=v.sixtyFootSeconds;EighthSeconds=v.eighthSeconds;Tune=v.tune==null?null:new CartApplyTuneSnapshot(v.tune);Driving=v.driving==null?null:new CartApplyDriverSnapshot(v.driving);}
    }
    internal sealed class CartApplyTuneSnapshot
    {
        internal readonly float CamTimingDegrees,DriverMassKg,PayloadMassKg,PayloadPositionM,PayloadHeightM,LaunchRpm,FinalDrive,TirePressurePsi,ShiftRpm,TurboSize,BoostSetting,SuperchargerPulley,RevLimitRpm,WheelDiameter,WheelWidth,WheelMass,FrontWheelDiameter,RearWheelDiameter,FrontWheelWidth,RearWheelWidth,FrontTirePressurePsi,RearTirePressurePsi,FlywheelMass,FrontLeftGrip,FrontRightGrip,RearLeftGrip,RearRightGrip,PartTurboSize,PartFixedBoost,PartBoostMin,PartBoostMax,PartFixedPulley,PartPulleyMin,PartPulleyMax,PartFixedFinalDrive,PartFinalDriveMin,PartFinalDriveMax,ShiftLightRpm,PartGearCapacity,PartPower,PartGrip,PartMass,PartShift,PartCapacity,PartBoost;
        internal readonly int EngineLevel,TireLevel,WeightLevel,GearboxLevel,ExhaustLevel,ControlLevel;
        internal readonly EngineUpgradeInduction InductionUpgrade;internal readonly BlowOffType BlowOffValve,PartFixedValve;
        internal readonly EquipmentSetting TwoStep,LaunchPreload,AntiLag;internal readonly LimiterStyle Limiter,TwoStepStyle;internal readonly CutMethod LimiterCut,TwoStepCut;internal readonly TransmissionUpgrade TransmissionUpgrade;
        internal readonly bool ColdStart,AwdConfigured,PartsResolved,PartLimiterAdjustable,PartTwoStepAdjustable,PartShiftLight,PartShiftLightAdjustable,PartTuningConfigured,PartBoostAdjustable,PartPulleyAdjustable,PartValveAdjustable,PartFinalDriveAdjustable;
        internal readonly CartApplyAwdSnapshot Awd;
        internal CartApplyTuneSnapshot(Tune v){CamTimingDegrees=v.camTimingDegrees;DriverMassKg=v.driverMassKg;PayloadMassKg=v.payloadMassKg;PayloadPositionM=v.payloadPositionM;PayloadHeightM=v.payloadHeightM;LaunchRpm=v.launchRpm;FinalDrive=v.finalDrive;TirePressurePsi=v.tirePressurePsi;ShiftRpm=v.shiftRpm;EngineLevel=v.engineLevel;TireLevel=v.tireLevel;WeightLevel=v.weightLevel;GearboxLevel=v.gearboxLevel;ExhaustLevel=v.exhaustLevel;InductionUpgrade=v.inductionUpgrade;BlowOffValve=v.blowOffValve;TurboSize=v.turboSize;BoostSetting=v.boostSetting;SuperchargerPulley=v.superchargerPulley;ControlLevel=v.controlLevel;TwoStep=v.twoStep;LaunchPreload=v.launchPreload;AntiLag=v.antiLag;Limiter=v.limiter;TwoStepStyle=v.twoStepStyle;LimiterCut=v.limiterCut;TwoStepCut=v.twoStepCut;RevLimitRpm=v.revLimitRpm;WheelDiameter=v.wheelDiameter;WheelWidth=v.wheelWidth;WheelMass=v.wheelMass;FrontWheelDiameter=v.frontWheelDiameter;RearWheelDiameter=v.rearWheelDiameter;FrontWheelWidth=v.frontWheelWidth;RearWheelWidth=v.rearWheelWidth;FrontTirePressurePsi=v.frontTirePressurePsi;RearTirePressurePsi=v.rearTirePressurePsi;ColdStart=v.coldStart;TransmissionUpgrade=v.transmissionUpgrade;FlywheelMass=v.flywheelMass;AwdConfigured=v.awdConfigured;Awd=v.awd==null?null:new CartApplyAwdSnapshot(v.awd);FrontLeftGrip=v.frontLeftGrip;FrontRightGrip=v.frontRightGrip;RearLeftGrip=v.rearLeftGrip;RearRightGrip=v.rearRightGrip;PartsResolved=v.partsResolved;PartLimiterAdjustable=v.partLimiterAdjustable;PartTwoStepAdjustable=v.partTwoStepAdjustable;PartShiftLight=v.partShiftLight;PartShiftLightAdjustable=v.partShiftLightAdjustable;PartTuningConfigured=v.partTuningConfigured;PartBoostAdjustable=v.partBoostAdjustable;PartPulleyAdjustable=v.partPulleyAdjustable;PartValveAdjustable=v.partValveAdjustable;PartFinalDriveAdjustable=v.partFinalDriveAdjustable;PartTurboSize=v.partTurboSize;PartFixedBoost=v.partFixedBoost;PartBoostMin=v.partBoostMin;PartBoostMax=v.partBoostMax;PartFixedPulley=v.partFixedPulley;PartPulleyMin=v.partPulleyMin;PartPulleyMax=v.partPulleyMax;PartFixedFinalDrive=v.partFixedFinalDrive;PartFinalDriveMin=v.partFinalDriveMin;PartFinalDriveMax=v.partFinalDriveMax;PartFixedValve=v.partFixedValve;ShiftLightRpm=v.shiftLightRpm;PartGearCapacity=v.partGearCapacity;PartPower=v.partPower;PartGrip=v.partGrip;PartMass=v.partMass;PartShift=v.partShift;PartCapacity=v.partCapacity;PartBoost=v.partBoost;}
    }
    internal sealed class CartApplyAwdSnapshot
    {internal readonly AwdMode Mode;internal readonly float CenterSpeedRatio,TorqueBiasRatio,CenterPreloadNm,FrontBias,MinimumFront,MaximumFront,MaximumSecondary,ResponseSeconds,SlipThreshold,Preload,FreeShaftInertiaKgM2,FreeShaftDragNm,FreeShaftViscousDrag;internal readonly bool PrimaryFront,IsolateFailedAxle;internal CartApplyAwdSnapshot(AwdSpec v){Mode=v.mode;CenterSpeedRatio=v.centerSpeedRatio;TorqueBiasRatio=v.torqueBiasRatio;CenterPreloadNm=v.centerPreloadNm;FrontBias=v.frontBias;MinimumFront=v.minimumFront;MaximumFront=v.maximumFront;PrimaryFront=v.primaryFront;IsolateFailedAxle=v.isolateFailedAxle;MaximumSecondary=v.maximumSecondary;ResponseSeconds=v.responseSeconds;SlipThreshold=v.slipThreshold;Preload=v.preload;FreeShaftInertiaKgM2=v.freeShaftInertiaKgM2;FreeShaftDragNm=v.freeShaftDragNm;FreeShaftViscousDrag=v.freeShaftViscousDrag;}}
    internal sealed class CartApplyEngineSnapshot : CartApplyRecordSnapshot
    {
        readonly CartApplyBindingSnapshot[] slots;internal readonly CartApplyIdentityRef InstanceId,ParentVehicleId;internal readonly string VehicleDefinitionId,StorageId,CustomName;internal readonly CartApplyFluidSnapshot Fluids;internal readonly CartApplyTuneSnapshot Tune;
        internal CartApplyBindingSnapshot[] Slots=>slots==null?null:(CartApplyBindingSnapshot[])slots.Clone();
        internal CartApplyEngineSnapshot(CartApplyIdentityRef identity,bool deleted,int sourceOrdinal,int insertionOrdinal,EngineAssemblyInstance value,Func<string,CartApplyIdentityRef> map):base(identity,deleted,sourceOrdinal,insertionOrdinal)
        {if(value==null)return;InstanceId=map(value.instanceId);ParentVehicleId=map(value.parentVehicleId);VehicleDefinitionId=value.vehicleDefinitionId;StorageId=value.storageId;CustomName=value.customName;slots=value.slots==null?null:value.slots.Select(v=>v==null?null:new CartApplyBindingSnapshot(v,map)).ToArray();Fluids=value.fluids==null?null:new CartApplyFluidSnapshot(value.fluids);Tune=value.tune==null?null:new CartApplyTuneSnapshot(value.tune);}
    }
    internal sealed class CartApplyUnitSnapshot : CartApplyRecordSnapshot
    {internal readonly string DefinitionId,StorageId;internal readonly int UnitPrice;internal readonly bool TireUnit;internal readonly CartApplyElementSnapshot Element;internal CartApplyUnitSnapshot(CartApplyIdentityRef identity,bool deleted,int sourceOrdinal,int insertionOrdinal,LooseEngineElement v,Func<string,CartApplyIdentityRef> map,CartApplyCaptureContext capture):base(identity,deleted,sourceOrdinal,insertionOrdinal){if(v==null)return;DefinitionId=v.definitionId;StorageId=v.storageId;UnitPrice=v.unitPrice;TireUnit=v.tireUnit;Element=v.element==null?null:new CartApplyElementSnapshot(v.element,map,capture);}}
    internal sealed class CartApplyDealerSnapshot : CartApplyRecordSnapshot
    {internal readonly bool IsUnit;internal readonly int PaidPrice;internal readonly CartApplyPartSnapshot Part;internal readonly CartApplyUnitSnapshot Unit;internal CartApplyDealerSnapshot(CartApplyIdentityRef identity,bool deleted,int sourceOrdinal,int insertionOrdinal,DealerStock v,Func<string,CartApplyIdentityRef> map,CartApplyCaptureContext capture):base(identity,deleted,sourceOrdinal,insertionOrdinal){if(v==null)return;IsUnit=v.isUnit;PaidPrice=v.paidPrice;Part=v.part==null?null:new CartApplyPartSnapshot(map(v.part.instanceId),false,sourceOrdinal,insertionOrdinal,v.part,map,capture);Unit=v.unit==null?null:new CartApplyUnitSnapshot(map(v.unit.element?.id),false,sourceOrdinal,insertionOrdinal,v.unit,map,capture);}}
    internal sealed class CartApplyEngineRebuildTargetSnapshot
    {internal readonly CartApplyIdentityRef PartId;internal readonly string DefinitionId;internal readonly bool Replace;internal CartApplyEngineRebuildTargetSnapshot(EngineRebuildTarget v,Func<string,CartApplyIdentityRef> map){PartId=map(v.partId);DefinitionId=v.definitionId;Replace=v.replace;}}
    internal sealed class CartApplyEngineRebuildSnapshot
    {readonly CartApplyEngineRebuildTargetSnapshot[] targets;internal readonly bool Active;internal readonly CartApplyIdentityRef Id,EngineId;internal readonly long StartedUtcTicks,ReadyUtcTicks;internal readonly int PaidPrice;internal CartApplyEngineRebuildTargetSnapshot[] Targets=>targets==null?null:(CartApplyEngineRebuildTargetSnapshot[])targets.Clone();internal CartApplyEngineRebuildSnapshot(EngineRebuildJob v,Func<string,CartApplyIdentityRef> map){Active=v.active;Id=map(v.id);EngineId=map(v.engineId);StartedUtcTicks=v.startedUtcTicks;ReadyUtcTicks=v.readyUtcTicks;PaidPrice=v.paidPrice;targets=v.targets==null?null:v.targets.Select(t=>t==null?null:new CartApplyEngineRebuildTargetSnapshot(t,map)).ToArray();}}
    internal sealed class CartApplyCarSnapshot : CartApplyRecordSnapshot
    {
        readonly CartApplyBindingSnapshot[] bindings;readonly CartApplyIdentityRef[] usedHistory;readonly CartApplyTuneSnapshot[] presets;
        internal readonly CartApplyIdentityRef InstanceId,ChassisId,EngineAssemblyId,GarageJobId;internal readonly int InstalledVersion,ServiceHardwareVersion,MassVersion,CompletedRuns;
        internal readonly CartApplyIdentityRef InstalledIdentity;internal readonly string InstalledVehicleDefinitionId,VehicleId,CustomName,PaintHex;
        internal readonly bool HasInstalledBuild,FuelInitialized;internal readonly CartApplyFuelSnapshot Fuel;internal readonly CartApplyFluidSnapshot Fluids;internal readonly CartApplyTuneSnapshot Tune;internal readonly CartApplyEngineRebuildSnapshot EngineRebuild;internal readonly CartApplyPassSnapshot LastPass;
        internal readonly long ThermalUtcTicks;internal readonly double BestQuarterSeconds,BestSixtyFootSeconds,BestEighthSeconds,BestTrapKmh,BestReactionSeconds,MileageKm;
        internal CartApplyBindingSnapshot[] Bindings=>bindings==null?null:(CartApplyBindingSnapshot[])bindings.Clone();internal CartApplyIdentityRef[] UsedHistory=>usedHistory==null?null:(CartApplyIdentityRef[])usedHistory.Clone();internal CartApplyTuneSnapshot[] Presets=>presets==null?null:(CartApplyTuneSnapshot[])presets.Clone();
        internal CartApplyCarSnapshot(CartApplyIdentityRef identity,bool deleted,int sourceOrdinal,int insertionOrdinal,OwnedCar v,Func<string,CartApplyIdentityRef> map):base(identity,deleted,sourceOrdinal,insertionOrdinal)
        {if(v==null)return;InstanceId=map(v.instanceId);ChassisId=map(v.chassisId);EngineAssemblyId=map(v.engineAssemblyId);GarageJobId=map(v.garageJobId);HasInstalledBuild=v.installed!=null;InstalledVersion=v.installed?.version??0;InstalledIdentity=map(v.installed?.instanceId);InstalledVehicleDefinitionId=v.installed?.vehicleDefinitionId;bindings=v.installed==null||v.installed.slots==null?null:v.installed.slots.Select(s=>s==null?null:new CartApplyBindingSnapshot(s,map)).ToArray();FuelInitialized=v.fuelInitialized;Fuel=v.fuel==null?null:new CartApplyFuelSnapshot(v.fuel);Fluids=v.fluids==null?null:new CartApplyFluidSnapshot(v.fluids);ServiceHardwareVersion=v.serviceHardwareVersion;ThermalUtcTicks=v.thermalUtcTicks;EngineRebuild=v.engineRebuild==null?null:new CartApplyEngineRebuildSnapshot(v.engineRebuild,map);MassVersion=v.massVersion;VehicleId=v.vehicleId;CustomName=v.customName;usedHistory=v.usedHistory?.Select(map).ToArray();Tune=v.tune==null?null:new CartApplyTuneSnapshot(v.tune);PaintHex=v.paintHex;presets=v.presets?.Select(t=>t==null?null:new CartApplyTuneSnapshot(t)).ToArray();CompletedRuns=v.completedRuns;MileageKm=v.mileageKm;BestQuarterSeconds=v.bestQuarterSeconds;BestSixtyFootSeconds=v.bestSixtyFootSeconds;BestEighthSeconds=v.bestEighthSeconds;BestTrapKmh=v.bestTrapKmh;BestReactionSeconds=v.bestReactionSeconds;LastPass=v.lastPass==null?null:new CartApplyPassSnapshot(v.lastPass);}
    }
    internal sealed class CartApplyOwnerSnapshot : CartApplyRecordSnapshot
    {readonly CartApplyIdentityRef?[] partIds;internal readonly string Kind;internal CartApplyIdentityRef?[] PartIds=>partIds==null?null:(CartApplyIdentityRef?[])partIds.Clone();internal CartApplyOwnerSnapshot(CartApplyIdentityRef identity,bool deleted,int sourceOrdinal,int insertionOrdinal,string kind,IEnumerable<CartApplyIdentityRef?> values):base(identity,deleted,sourceOrdinal,insertionOrdinal){Kind=kind;partIds=values==null?null:values.ToArray();}}

    internal sealed class OrderedCartApplyResult
    {
        readonly CartApplyRowEffect[] effects;readonly CartApplyPartSnapshot[] parts;readonly CartApplyEngineSnapshot[] engines;readonly CartApplyUnitSnapshot[] units;readonly CartApplyDealerSnapshot[] dealer;readonly CartApplyCarSnapshot[] cars;readonly CartApplyOwnerSnapshot[] owners;readonly CartApplySupplyDelta[] supplyDeltas;readonly CartApplyIdentity[] identities;
        internal readonly bool Succeeded,HasCashDelta;internal readonly int? CashBefore,CashAfter,CashDelta;internal readonly string ErrorType,ErrorMessage,ErrorParameterName;internal readonly int ErrorIndex;
        internal CartApplyRowEffect[] Effects=>Clone(effects);internal CartApplyPartSnapshot[] Parts=>Clone(parts);internal CartApplyEngineSnapshot[] Engines=>Clone(engines);internal CartApplyUnitSnapshot[] Units=>Clone(units);internal CartApplyDealerSnapshot[] Dealer=>Clone(dealer);internal CartApplyCarSnapshot[] Cars=>Clone(cars);internal CartApplyOwnerSnapshot[] Owners=>Clone(owners);internal CartApplySupplyDelta[] SupplyDeltas=>Clone(supplyDeltas);internal CartApplyIdentity[] Identities=>Clone(identities);
        internal OrderedCartApplyResult(bool success,bool hasCash,int? before,int? after,int? delta,IEnumerable<CartApplyRowEffect> effects=null,IEnumerable<CartApplyPartSnapshot> parts=null,IEnumerable<CartApplyEngineSnapshot> engines=null,IEnumerable<CartApplyUnitSnapshot> units=null,IEnumerable<CartApplyDealerSnapshot> dealer=null,IEnumerable<CartApplyCarSnapshot> cars=null,IEnumerable<CartApplyOwnerSnapshot> owners=null,IEnumerable<CartApplySupplyDelta> supply=null,IEnumerable<CartApplyIdentity> identities=null,string errorType=null,string errorMessage=null,string errorParameterName=null,int errorIndex=-1)
        {Succeeded=success;HasCashDelta=hasCash;CashBefore=before;CashAfter=after;CashDelta=delta;this.effects=Copy(effects);this.parts=Copy(parts);this.engines=Copy(engines);this.units=Copy(units);this.dealer=Copy(dealer);this.cars=Copy(cars);this.owners=Copy(owners);supplyDeltas=Copy(supply);this.identities=Copy(identities);ErrorType=errorType;ErrorMessage=errorMessage;ErrorParameterName=errorParameterName;ErrorIndex=errorIndex;}
        internal static OrderedCartApplyResult Failure(Exception error,int index){var arg=error as ArgumentException;return new OrderedCartApplyResult(false,false,null,null,null,errorType:error?.GetType().FullName,errorMessage:error?.Message,errorParameterName:arg?.ParamName,errorIndex:index);}
        static T[] Copy<T>(IEnumerable<T> values)=>values==null?null:values.ToArray();static T[] Clone<T>(T[] values)=>values==null?null:(T[])values.Clone();
    }
}

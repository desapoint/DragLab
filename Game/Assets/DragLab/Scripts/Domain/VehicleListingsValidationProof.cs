using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using DragLab.Domain;

namespace DragLab.Domain.Mechanics
{
    // Positive certificate for the VehicleListings.Validate leaf. The profile
    // reference selects a weak bucket; every hit still proves all leaf inputs.
    internal static class VehicleListingsValidationProof
    {
        sealed class Bucket { internal Certificate current; }
        sealed class Certificate
        {
            internal readonly MechanicalCatalog catalog;
            internal readonly MarketCatalogLayout.ReadyContentToken authored;
            internal readonly bool firstPurchasePending;
            internal readonly int version,seed,generation,purchases,completedRuns,knowledgeRevision;
            internal readonly VehicleOffer[] listings;
            internal readonly string[] ownedPartIds;

            internal Certificate(PlayerProfile profile,MechanicalCatalog source,MarketCatalogLayout.ReadyContentToken token,
                VehicleOffer[] offers,string[] partIds)
            {
                catalog=source;authored=token;firstPurchasePending=profile.firstPurchasePending;
                var stock=profile.vehicleStock;version=stock.version;seed=stock.seed;generation=stock.generation;
                purchases=stock.purchases;completedRuns=stock.completedRuns;knowledgeRevision=stock.knowledgeRevision;
                listings=offers;ownedPartIds=partIds;
            }

            internal bool Matches(PlayerProfile profile,MechanicalCatalog current)
            {
                try
                {
                    if(profile==null||!ReferenceEquals(current,catalog)||!authored.MatchesCurrent(current)||
                        profile.firstPurchasePending!=firstPurchasePending||profile.vehicleStock==null||
                        profile.vehicleStock.version!=version||profile.vehicleStock.seed!=seed||
                        profile.vehicleStock.generation!=generation||profile.vehicleStock.purchases!=purchases||
                        profile.vehicleStock.completedRuns!=completedRuns||profile.vehicleStock.knowledgeRevision!=knowledgeRevision||
                        profile.vehicleStock.listings==null||profile.vehicleStock.listings.Count!=listings.Length||
                        profile.parts==null||profile.parts.Count!=ownedPartIds.Length)return false;
                    for(int i=0;i<ownedPartIds.Length;i++)
                    {
                        var part=profile.parts[i];if(part==null||part.instanceId!=ownedPartIds[i])return false;
                    }
                    for(int i=0;i<listings.Length;i++)if(!SameOffer(listings[i],profile.vehicleStock.listings[i]))return false;
                    return true;
                }
                catch(Exception){return false;}
            }
        }

        static readonly ConditionalWeakTable<PlayerProfile,Bucket> buckets=new ConditionalWeakTable<PlayerProfile,Bucket>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static long proofHits,coreRuns;
        internal static long ProofHitsForChecks=>Interlocked.Read(ref proofHits);
        internal static long CoreRunsForChecks=>Interlocked.Read(ref coreRuns);
#else
        internal static long ProofHitsForChecks=>0;
        internal static long CoreRunsForChecks=>0;
#endif
        internal static void RecordCoreRun()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Interlocked.Increment(ref coreRuns);
#endif
        }

        internal static bool TryValidate(PlayerProfile profile,MechanicalCatalog catalog,out string result)
        {
            result=null;
            if(profile==null||!buckets.TryGetValue(profile,out var bucket))return false;
            var certificate=Volatile.Read(ref bucket.current);
            if(certificate==null)return false;
            if(certificate.Matches(profile,catalog))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Interlocked.Increment(ref proofHits);
#endif
                return true;
            }
            Interlocked.CompareExchange(ref bucket.current,null,certificate);
            return false;
        }

        internal static void StoreValid(PlayerProfile profile,MechanicalCatalog catalog)
        {
            if(profile==null||catalog==null)return;
            try
            {
                if(!MarketCatalogLayout.TryCaptureReadyContentToken(catalog,out var token)||token==null)return;
                var stock=profile.vehicleStock;if(stock==null||stock.listings==null||profile.parts==null)return;
                var listings=new VehicleOffer[stock.listings.Count];
                for(int i=0;i<listings.Length;i++)if(!TryCopyOffer(stock.listings[i],out listings[i]))return;
                var partIds=new string[profile.parts.Count];
                for(int i=0;i<partIds.Length;i++){var part=profile.parts[i];if(part==null)return;partIds[i]=part.instanceId;}
                var certificate=new Certificate(profile,catalog,token,listings,partIds);
                if(!certificate.Matches(profile,catalog))return;
                var bucket=buckets.GetValue(profile,_=>new Bucket());Volatile.Write(ref bucket.current,certificate);
                if(!certificate.Matches(profile,catalog))Interlocked.CompareExchange(ref bucket.current,null,certificate);
            }
            catch(Exception)
            {
                // This optimization is optional; failures to capture it cannot alter
                // the already successful legacy validation result.
            }
        }

        internal static bool DirectMatchesForChecks(PlayerProfile profile,MechanicalCatalog catalog)
        {
            if(profile==null||!buckets.TryGetValue(profile,out var bucket))return false;
            var certificate=Volatile.Read(ref bucket.current);
            return certificate!=null&&certificate.Matches(profile,catalog);
        }

        static bool TryCopyOffer(VehicleOffer source,out VehicleOffer copy)
        {
            copy=null;
            if(source==null||source.stock==null||source.fuel==null||source.fluids==null||source.parts==null||source.historyNotes==null)return false;
            var parts=new List<PartInstance>(source.parts.Count);
            foreach(var part in source.parts)
            {
                if(part==null||part.condition==null)return false;
                var elementArray=part.condition.elements;
                if(elementArray!=null&&elementArray.GetType()!=typeof(EngineElement[])&&elementArray.GetType()!=typeof(TireCondition[]))return false;
                var partCopy=part.Copy();
                if(elementArray is TireCondition[] tires)
                {
                    var tireCopies=new TireCondition[tires.Length];
                    for(int i=0;i<tires.Length;i++)tireCopies[i]=tires[i]==null?null:tires[i].Copy();
                    partCopy.condition.elements=tireCopies;
                }
                parts.Add(partCopy);
            }
            var stockCopy=source.stock.Copy();
            stockCopy.awd=source.stock.awd==null?null:source.stock.awd.Copy();
            copy=new VehicleOffer{
                model=source.model,paint=source.paint,listingId=source.listingId,used=source.used,mileageKm=source.mileageKm,
                disclosureVersion=source.disclosureVersion,estimateSeed=source.estimateSeed,inspected=source.inspected,
                firstStarter=source.firstStarter,engineRuns=source.engineRuns,drives=source.drives,reportedCondition=source.reportedCondition,
                historyNotes=new List<string>(source.historyNotes),parts=parts,fuel=source.fuel.Copy(),fluids=source.fluids.Copy(),
                price=source.price,reputation=source.reputation,cataloguePrice=source.cataloguePrice,stock=stockCopy
            };
            return true;
        }

        static bool SameOffer(VehicleOffer a,VehicleOffer b)
        {
            if(a==null||b==null)return a==b;
            if(a.model!=b.model||a.paint!=b.paint||a.listingId!=b.listingId||a.used!=b.used||!F64(a.mileageKm,b.mileageKm)||
                a.disclosureVersion!=b.disclosureVersion||a.estimateSeed!=b.estimateSeed||a.inspected!=b.inspected||
                a.firstStarter!=b.firstStarter||a.engineRuns!=b.engineRuns||a.drives!=b.drives||!F64(a.reportedCondition,b.reportedCondition)||
                a.price!=b.price||a.reputation!=b.reputation||a.cataloguePrice!=b.cataloguePrice||!SameTune(a.stock,b.stock)||
                !SameFuel(a.fuel,b.fuel)||!SameFluids(a.fluids,b.fluids)||!SameStrings(a.historyNotes,b.historyNotes))return false;
            if(a.parts==null||b.parts==null)return a.parts==b.parts;
            if(a.parts.Count!=b.parts.Count)return false;
            for(int i=0;i<a.parts.Count;i++)if(!SamePart(a.parts[i],b.parts[i]))return false;
            return true;
        }

        static bool SamePart(PartInstance a,PartInstance b)
        {
            if(a==null||b==null)return a==b;
            return a.version==b.version&&a.instanceId==b.instanceId&&a.definitionId==b.definitionId&&a.storageId==b.storageId&&
                a.location==b.location&&a.ownerId==b.ownerId&&a.slot==b.slot&&SameCondition(a.condition,b.condition);
        }
        static bool SameCondition(ComponentCondition a,ComponentCondition b)
        {
            if(a==null||b==null)return a==b;
            if(!F32(a.health,b.health)||a.failed!=b.failed||a.failureReason!=b.failureReason||!F64(a.runningSeconds,b.runningSeconds)||
                !F64(a.distanceM,b.distanceM)||!F64(a.stressExposure,b.stressExposure)||!F64(a.temperatureC,b.temperatureC)||
                !F64(a.slipEnergyJ,b.slipEnergyJ)||!F64(a.deposits,b.deposits)||!F64(a.leakSeverity,b.leakSeverity)||
                !F64(a.adjustmentError,b.adjustmentError)||!F64(a.serviceWearRemainder,b.serviceWearRemainder)||
                a.serviceCount!=b.serviceCount||a.reconditionCount!=b.reconditionCount||a.lastService!=b.lastService)return false;
            if(a.clutch==null||b.clutch==null){if(a.clutch!=b.clutch)return false;}
            else if(a.clutch.version!=b.clutch.version||!F64(a.clutch.oilLitres,b.clutch.oilLitres)||!F64(a.clutch.oilCondition,b.clutch.oilCondition))return false;
            if(a.elements==null||b.elements==null)return a.elements==b.elements;
            if(a.elements.GetType()!=b.elements.GetType())return false;
            if(a.elements.Length!=b.elements.Length)return false;
            for(int i=0;i<a.elements.Length;i++)if(!SameElement(a.elements[i],b.elements[i]))return false;
            return true;
        }
        static bool SameElement(EngineElement a,EngineElement b)
        {
            if(a==null||b==null)return a==b;
            if(a.GetType()!=b.GetType()||a.id!=b.id||a.definitionId!=b.definitionId||!F32(a.health,b.health)||a.role!=b.role||
                a.cylinder!=b.cylinder||a.valve!=b.valve||!F32(a.severity,b.severity)||!F64(a.wearRemainder,b.wearRemainder)||a.fault!=b.fault)return false;
            if(a is TireCondition x&&b is TireCondition y)
                return x.failed==y.failed&&F64(x.temperatureC,y.temperatureC)&&F64(x.slipEnergyJ,y.slipEnergyJ)&&
                    F64(x.distanceM,y.distanceM)&&F64(x.lastContactWorkJ,y.lastContactWorkJ)&&F64(x.consumedLife,y.consumedLife)&&
                    F64(x.runningSeconds,y.runningSeconds)&&x.failureReason==y.failureReason;
            return true;
        }
        static bool SameFuel(FuelState a,FuelState b)
            =>a==null?b==null:b!=null&&F64(a.litres,b.litres)&&F64(a.cumulativeUsedLitres,b.cumulativeUsedLitres)&&F64(a.flowLitresPerSecond,b.flowLitresPerSecond);
        static bool SameFluids(FluidState a,FluidState b)
            =>a==null?b==null:b!=null&&F64(a.oilLitres,b.oilLitres)&&F64(a.coolantLitres,b.coolantLitres)&&F64(a.oilTemperatureC,b.oilTemperatureC)&&
                F64(a.oilPressureBar,b.oilPressureBar)&&F64(a.coolantPressureBar,b.coolantPressureBar)&&F64(a.oilCondition,b.oilCondition)&&
                F64(a.oilConsumedLitres,b.oilConsumedLitres)&&F64(a.oilLeakedLitres,b.oilLeakedLitres)&&F64(a.coolantLostLitres,b.coolantLostLitres)&&
                F64(a.serviceElapsedSeconds,b.serviceElapsedSeconds)&&F64(a.oilBurnLps,b.oilBurnLps)&&F64(a.oilLeakLps,b.oilLeakLps)&&
                F64(a.coolantLeakLps,b.coolantLeakLps)&&F64(a.coolantVentLps,b.coolantVentLps);
        static bool SameStrings(IList<string> a,IList<string> b)
        {
            if(a==null||b==null)return a==b;if(a.Count!=b.Count)return false;
            for(int i=0;i<a.Count;i++)if(a[i]!=b[i])return false;
            return true;
        }
        static bool SameTune(Tune a,Tune b)
        {
            if(a==null||b==null)return a==b;
            if(a.GetType()!=b.GetType())return false;
            return F32(a.camTimingDegrees,b.camTimingDegrees)&&F32(a.driverMassKg,b.driverMassKg)&&F32(a.payloadMassKg,b.payloadMassKg)&&
                F32(a.payloadPositionM,b.payloadPositionM)&&F32(a.payloadHeightM,b.payloadHeightM)&&F32(a.launchRpm,b.launchRpm)&&
                F32(a.finalDrive,b.finalDrive)&&F32(a.tirePressurePsi,b.tirePressurePsi)&&F32(a.shiftRpm,b.shiftRpm)&&
                a.engineLevel==b.engineLevel&&a.tireLevel==b.tireLevel&&a.weightLevel==b.weightLevel&&a.gearboxLevel==b.gearboxLevel&&a.exhaustLevel==b.exhaustLevel&&
                a.inductionUpgrade==b.inductionUpgrade&&a.blowOffValve==b.blowOffValve&&F32(a.turboSize,b.turboSize)&&F32(a.boostSetting,b.boostSetting)&&
                F32(a.superchargerPulley,b.superchargerPulley)&&a.controlLevel==b.controlLevel&&a.twoStep==b.twoStep&&a.launchPreload==b.launchPreload&&a.antiLag==b.antiLag&&
                a.limiter==b.limiter&&a.twoStepStyle==b.twoStepStyle&&a.limiterCut==b.limiterCut&&a.twoStepCut==b.twoStepCut&&F32(a.revLimitRpm,b.revLimitRpm)&&
                F32(a.wheelDiameter,b.wheelDiameter)&&F32(a.wheelWidth,b.wheelWidth)&&F32(a.wheelMass,b.wheelMass)&&F32(a.frontWheelDiameter,b.frontWheelDiameter)&&
                F32(a.rearWheelDiameter,b.rearWheelDiameter)&&F32(a.frontWheelWidth,b.frontWheelWidth)&&F32(a.rearWheelWidth,b.rearWheelWidth)&&
                F32(a.frontTirePressurePsi,b.frontTirePressurePsi)&&F32(a.rearTirePressurePsi,b.rearTirePressurePsi)&&a.coldStart==b.coldStart&&
                a.transmissionUpgrade==b.transmissionUpgrade&&F32(a.flywheelMass,b.flywheelMass)&&SameAwd(a.awd,b.awd)&&a.awdConfigured==b.awdConfigured&&
                F32(a.frontLeftGrip,b.frontLeftGrip)&&F32(a.frontRightGrip,b.frontRightGrip)&&F32(a.rearLeftGrip,b.rearLeftGrip)&&F32(a.rearRightGrip,b.rearRightGrip)&&
                a.partsResolved==b.partsResolved&&a.partLimiterAdjustable==b.partLimiterAdjustable&&a.partTwoStepAdjustable==b.partTwoStepAdjustable&&
                a.partShiftLight==b.partShiftLight&&a.partShiftLightAdjustable==b.partShiftLightAdjustable&&a.partTuningConfigured==b.partTuningConfigured&&
                a.partBoostAdjustable==b.partBoostAdjustable&&a.partPulleyAdjustable==b.partPulleyAdjustable&&a.partValveAdjustable==b.partValveAdjustable&&
                a.partFinalDriveAdjustable==b.partFinalDriveAdjustable&&F32(a.partTurboSize,b.partTurboSize)&&F32(a.partFixedBoost,b.partFixedBoost)&&
                F32(a.partBoostMin,b.partBoostMin)&&F32(a.partBoostMax,b.partBoostMax)&&F32(a.partFixedPulley,b.partFixedPulley)&&F32(a.partPulleyMin,b.partPulleyMin)&&
                F32(a.partPulleyMax,b.partPulleyMax)&&F32(a.partFixedFinalDrive,b.partFixedFinalDrive)&&F32(a.partFinalDriveMin,b.partFinalDriveMin)&&
                F32(a.partFinalDriveMax,b.partFinalDriveMax)&&a.partFixedValve==b.partFixedValve&&F32(a.shiftLightRpm,b.shiftLightRpm)&&
                F32(a.partGearCapacity,b.partGearCapacity)&&F32(a.partPower,b.partPower)&&F32(a.partGrip,b.partGrip)&&F32(a.partMass,b.partMass)&&
                F32(a.partShift,b.partShift)&&F32(a.partCapacity,b.partCapacity)&&F32(a.partBoost,b.partBoost);
        }
        static bool SameAwd(AwdSpec a,AwdSpec b)
            =>a==null?b==null:b!=null&&a.mode==b.mode&&F32(a.centerSpeedRatio,b.centerSpeedRatio)&&F32(a.torqueBiasRatio,b.torqueBiasRatio)&&
                F32(a.centerPreloadNm,b.centerPreloadNm)&&F32(a.frontBias,b.frontBias)&&F32(a.minimumFront,b.minimumFront)&&F32(a.maximumFront,b.maximumFront)&&
                a.primaryFront==b.primaryFront&&a.isolateFailedAxle==b.isolateFailedAxle&&F32(a.maximumSecondary,b.maximumSecondary)&&
                F32(a.responseSeconds,b.responseSeconds)&&F32(a.slipThreshold,b.slipThreshold)&&F32(a.preload,b.preload)&&
                F32(a.freeShaftInertiaKgM2,b.freeShaftInertiaKgM2)&&F32(a.freeShaftDragNm,b.freeShaftDragNm)&&F32(a.freeShaftViscousDrag,b.freeShaftViscousDrag);

        [StructLayout(LayoutKind.Explicit)] struct FloatBits { [FieldOffset(0)] internal float value;[FieldOffset(0)] internal int bits; }
        static bool F32(float a,float b){var x=new FloatBits{value=a};var y=new FloatBits{value=b};return x.bits==y.bits;}
        static bool F64(double a,double b)=>BitConverter.DoubleToInt64Bits(a)==BitConverter.DoubleToInt64Bits(b);
    }
}

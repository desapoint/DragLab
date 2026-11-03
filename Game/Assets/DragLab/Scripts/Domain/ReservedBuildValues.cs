using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace DragLab.Domain.Mechanics
{
    internal enum ReservedBuildComparisonMode { ExactHashBaseline, LegacyJsonFallback }
    internal enum ReservedBuildImportKind { CreatedTyped, HashMatchedAtImport, HashMismatchAtImport }
    internal enum ReservedBuildFallbackKind { Absent, Values, Error }
    internal enum ReservedBuildMatchKind
    {
        HashError, CaptureError, HashMatched, StalePayload, JsonTextMatched,
        JsonExpectedMatched, FallbackAbsent, FallbackError, Mismatch
    }

    internal sealed class ReservedBuildMatchOutcome
    {
        internal ReservedBuildMatchKind Kind { get; private set; }
        internal string ErrorType { get; private set; }
        internal string ErrorMessage { get; private set; }
        internal string ErrorParameterName { get; private set; }
        internal ReservedBuildMatchOutcome(ReservedBuildMatchKind kind, string type = null,
            string message = null, string parameterName = null)
        { Kind = kind; ErrorType = type; ErrorMessage = message; ErrorParameterName = parameterName; }
    }

    internal sealed class ReservedBuildValues
    {
        readonly FuelState fuel;
        readonly FluidState fluids;
        readonly Tune tune;
        readonly string paint;
        readonly PartInstance[] parts;

        ReservedBuildValues(FuelState fuelValue, FluidState fluidValue, Tune tuneValue,
            string paintValue, PartInstance[] partValues)
        {
            fuel = fuelValue == null ? null : fuelValue.Copy();
            fluids = fluidValue == null ? null : fluidValue.Copy();
            tune = tuneValue == null ? null : tuneValue.Copy();
            paint = paintValue;
            parts = CopyParts(partValues);
        }

        internal static ReservedBuildValues FromSnapshot(ReservedBuildSnapshot snapshot)
        {
            if (snapshot == null) return null;
            return new ReservedBuildValues(snapshot.fuel, snapshot.fluids, snapshot.tune,
                snapshot.paint, snapshot.parts);
        }

        internal static ReservedBuildValues CaptureCreated(PlayerProfile owner, OwnedCar car)
        {
            var ordered = car.installed.slots.OrderBy(slot => slot.partInstanceId, StringComparer.Ordinal);
            var parts = ordered.Select(slot => owner.parts.Find(part => part.instanceId == slot.partInstanceId)).ToArray();
            return new ReservedBuildValues(car.fuel, car.fluids, car.tune, car.paintHex, parts);
        }

        internal static ReservedBuildValues Capture(PlayerProfile owner, OwnedCar car)
        { return CaptureCreated(owner, car); }

        internal FuelState FuelCopy() { return fuel == null ? null : fuel.Copy(); }
        internal FluidState FluidsCopy() { return fluids == null ? null : fluids.Copy(); }
        internal Tune TuneCopy() { return tune == null ? null : tune.Copy(); }
        internal string Paint { get { return paint; } }
        internal PartInstance[] PartsCopy() { return CopyParts(parts); }

        internal bool MatchesCurrent(ReservedBuildValues current, ReservedBuildComparisonMode mode)
        {
            if (current == null) return false;
            bool exact = mode == ReservedBuildComparisonMode.ExactHashBaseline;
            return FuelSame(fuel, current.fuel, exact) && FluidsSame(fluids, current.fluids, exact)
                && TuneSame(tune, current.tune, exact) && StringSame(paint, current.paint)
                && PartsSame(parts, current.parts, exact);
        }

        internal ReservedBuildValues DefensiveCopy()
        { return new ReservedBuildValues(fuel, fluids, tune, paint, parts); }

        static PartInstance[] CopyParts(PartInstance[] source)
        { return source == null ? null : Array.ConvertAll(source, CopyPart); }

        static PartInstance CopyPart(PartInstance source)
        {
            if (source == null) return null;
            return new PartInstance
            {
                version = source.version,
                instanceId = source.instanceId,
                definitionId = source.definitionId,
                storageId = source.storageId,
                location = source.location,
                ownerId = source.ownerId,
                slot = source.slot,
                condition = CopyCondition(source.condition)
            };
        }

        static ComponentCondition CopyCondition(ComponentCondition source)
        {
            if (source == null) return null;
            EngineElement[] elements = null;
            if (source.elements != null)
            {
                elements = (EngineElement[])source.elements.Clone();
                for (int i = 0; i < elements.Length; i++)
                    elements[i] = elements[i] == null ? null : elements[i].Copy();
            }
            return new ComponentCondition
            {
                health=source.health, failed=source.failed, failureReason=source.failureReason,
                runningSeconds=source.runningSeconds, distanceM=source.distanceM, stressExposure=source.stressExposure,
                temperatureC=source.temperatureC, slipEnergyJ=source.slipEnergyJ, deposits=source.deposits,
                leakSeverity=source.leakSeverity, adjustmentError=source.adjustmentError,
                serviceWearRemainder=source.serviceWearRemainder, serviceCount=source.serviceCount,
                reconditionCount=source.reconditionCount, lastService=source.lastService,
                elements=elements, clutch=source.clutch == null ? null : source.clutch.Copy()
            };
        }

        static bool StringSame(string a, string b) { return String.Equals(a, b, StringComparison.Ordinal); }
        static bool FloatSame(float a, float b, bool exact)
        {
            if (!exact) return a.Equals(b);
            return BitConverter.SingleToInt32Bits(a) == BitConverter.SingleToInt32Bits(b);
        }
        static bool DoubleSame(double a, double b, bool exact, bool health = false)
        {
            if (exact) return BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);
            if (a == b) return true;
            if (health || Double.IsNaN(a) || Double.IsInfinity(a) || Double.IsNaN(b) || Double.IsInfinity(b)
                || Math.Sign(a) != Math.Sign(b)) return false;
            ulong ua = OrderedBits(a), ub = OrderedBits(b);
            ulong distance = ua >= ub ? ua - ub : ub - ua;
            return distance <= 4;
        }
        static ulong OrderedBits(double value)
        {
            ulong bits = unchecked((ulong)BitConverter.DoubleToInt64Bits(value));
            return (bits & 0x8000000000000000UL) != 0 ? ~bits : bits | 0x8000000000000000UL;
        }
        static bool IntSame<T>(T a, T b) where T : struct { return EqualityComparer<T>.Default.Equals(a, b); }

        static bool FuelSame(FuelState a, FuelState b, bool exact)
        {
            if (a == null || b == null) return a == b; if (exact && ReferenceEquals(a, b)) return true;
            return DoubleSame(a.litres,b.litres,exact) && DoubleSame(a.cumulativeUsedLitres,b.cumulativeUsedLitres,exact)
                && DoubleSame(a.flowLitresPerSecond,b.flowLitresPerSecond,exact);
        }
        static bool FluidsSame(FluidState a, FluidState b, bool exact)
        {
            if (a == null || b == null) return a == b; if (exact && ReferenceEquals(a, b)) return true;
            return DoubleSame(a.oilLitres,b.oilLitres,exact) && DoubleSame(a.coolantLitres,b.coolantLitres,exact)
                && DoubleSame(a.oilTemperatureC,b.oilTemperatureC,exact) && DoubleSame(a.oilPressureBar,b.oilPressureBar,exact)
                && DoubleSame(a.coolantPressureBar,b.coolantPressureBar,exact) && DoubleSame(a.oilCondition,b.oilCondition,exact)
                && DoubleSame(a.oilConsumedLitres,b.oilConsumedLitres,exact) && DoubleSame(a.oilLeakedLitres,b.oilLeakedLitres,exact)
                && DoubleSame(a.coolantLostLitres,b.coolantLostLitres,exact) && DoubleSame(a.serviceElapsedSeconds,b.serviceElapsedSeconds,exact)
                && DoubleSame(a.oilBurnLps,b.oilBurnLps,exact) && DoubleSame(a.oilLeakLps,b.oilLeakLps,exact)
                && DoubleSame(a.coolantLeakLps,b.coolantLeakLps,exact) && DoubleSame(a.coolantVentLps,b.coolantVentLps,exact);
        }
        static bool TuneSame(Tune a, Tune b, bool x)
        {
            if (a==null||b==null) return a==b; if (x && ReferenceEquals(a,b)) return true;
            return FloatSame(a.camTimingDegrees,b.camTimingDegrees,x)&&FloatSame(a.driverMassKg,b.driverMassKg,x)
                &&FloatSame(a.payloadMassKg,b.payloadMassKg,x)&&FloatSame(a.payloadPositionM,b.payloadPositionM,x)&&FloatSame(a.payloadHeightM,b.payloadHeightM,x)
                &&FloatSame(a.launchRpm,b.launchRpm,x)&&FloatSame(a.finalDrive,b.finalDrive,x)&&FloatSame(a.tirePressurePsi,b.tirePressurePsi,x)
                &&FloatSame(a.shiftRpm,b.shiftRpm,x)&&a.engineLevel==b.engineLevel&&a.tireLevel==b.tireLevel&&a.weightLevel==b.weightLevel
                &&a.gearboxLevel==b.gearboxLevel&&a.exhaustLevel==b.exhaustLevel&&a.inductionUpgrade==b.inductionUpgrade&&a.blowOffValve==b.blowOffValve
                &&FloatSame(a.turboSize,b.turboSize,x)&&FloatSame(a.boostSetting,b.boostSetting,x)&&FloatSame(a.superchargerPulley,b.superchargerPulley,x)
                &&a.controlLevel==b.controlLevel&&a.twoStep==b.twoStep&&a.launchPreload==b.launchPreload&&a.antiLag==b.antiLag
                &&a.limiter==b.limiter&&a.twoStepStyle==b.twoStepStyle&&a.limiterCut==b.limiterCut&&a.twoStepCut==b.twoStepCut
                &&FloatSame(a.revLimitRpm,b.revLimitRpm,x)&&FloatSame(a.wheelDiameter,b.wheelDiameter,x)&&FloatSame(a.wheelWidth,b.wheelWidth,x)
                &&FloatSame(a.wheelMass,b.wheelMass,x)&&FloatSame(a.frontWheelDiameter,b.frontWheelDiameter,x)&&FloatSame(a.rearWheelDiameter,b.rearWheelDiameter,x)
                &&FloatSame(a.frontWheelWidth,b.frontWheelWidth,x)&&FloatSame(a.rearWheelWidth,b.rearWheelWidth,x)
                &&FloatSame(a.frontTirePressurePsi,b.frontTirePressurePsi,x)&&FloatSame(a.rearTirePressurePsi,b.rearTirePressurePsi,x)
                &&a.coldStart==b.coldStart&&a.transmissionUpgrade==b.transmissionUpgrade&&FloatSame(a.flywheelMass,b.flywheelMass,x)
                &&AwdSame(a.awd,b.awd,x)&&a.awdConfigured==b.awdConfigured&&FloatSame(a.frontLeftGrip,b.frontLeftGrip,x)
                &&FloatSame(a.frontRightGrip,b.frontRightGrip,x)&&FloatSame(a.rearLeftGrip,b.rearLeftGrip,x)&&FloatSame(a.rearRightGrip,b.rearRightGrip,x)
                &&a.partsResolved==b.partsResolved&&a.partLimiterAdjustable==b.partLimiterAdjustable&&a.partTwoStepAdjustable==b.partTwoStepAdjustable
                &&a.partShiftLight==b.partShiftLight&&a.partShiftLightAdjustable==b.partShiftLightAdjustable&&a.partTuningConfigured==b.partTuningConfigured
                &&a.partBoostAdjustable==b.partBoostAdjustable&&a.partPulleyAdjustable==b.partPulleyAdjustable&&a.partValveAdjustable==b.partValveAdjustable
                &&a.partFinalDriveAdjustable==b.partFinalDriveAdjustable&&FloatSame(a.partTurboSize,b.partTurboSize,x)
                &&FloatSame(a.partFixedBoost,b.partFixedBoost,x)&&FloatSame(a.partBoostMin,b.partBoostMin,x)&&FloatSame(a.partBoostMax,b.partBoostMax,x)
                &&FloatSame(a.partFixedPulley,b.partFixedPulley,x)&&FloatSame(a.partPulleyMin,b.partPulleyMin,x)&&FloatSame(a.partPulleyMax,b.partPulleyMax,x)
                &&FloatSame(a.partFixedFinalDrive,b.partFixedFinalDrive,x)&&FloatSame(a.partFinalDriveMin,b.partFinalDriveMin,x)
                &&FloatSame(a.partFinalDriveMax,b.partFinalDriveMax,x)&&a.partFixedValve==b.partFixedValve&&FloatSame(a.shiftLightRpm,b.shiftLightRpm,x)
                &&FloatSame(a.partGearCapacity,b.partGearCapacity,x)&&FloatSame(a.partPower,b.partPower,x)&&FloatSame(a.partGrip,b.partGrip,x)
                &&FloatSame(a.partMass,b.partMass,x)&&FloatSame(a.partShift,b.partShift,x)&&FloatSame(a.partCapacity,b.partCapacity,x)&&FloatSame(a.partBoost,b.partBoost,x);
        }
        static bool AwdSame(AwdSpec a, AwdSpec b, bool x)
        {
            if(a==null||b==null)return a==b;if(x&&ReferenceEquals(a,b))return true;
            return a.mode==b.mode&&FloatSame(a.centerSpeedRatio,b.centerSpeedRatio,x)&&FloatSame(a.torqueBiasRatio,b.torqueBiasRatio,x)
                &&FloatSame(a.centerPreloadNm,b.centerPreloadNm,x)&&FloatSame(a.frontBias,b.frontBias,x)&&FloatSame(a.minimumFront,b.minimumFront,x)
                &&FloatSame(a.maximumFront,b.maximumFront,x)&&a.primaryFront==b.primaryFront&&a.isolateFailedAxle==b.isolateFailedAxle
                &&FloatSame(a.maximumSecondary,b.maximumSecondary,x)&&FloatSame(a.responseSeconds,b.responseSeconds,x)
                &&FloatSame(a.slipThreshold,b.slipThreshold,x)&&FloatSame(a.preload,b.preload,x)
                &&FloatSame(a.freeShaftInertiaKgM2,b.freeShaftInertiaKgM2,x)&&FloatSame(a.freeShaftDragNm,b.freeShaftDragNm,x)
                &&FloatSame(a.freeShaftViscousDrag,b.freeShaftViscousDrag,x);
        }
        static bool PartsSame(PartInstance[] a, PartInstance[] b, bool x)
        {
            if(a==null||b==null)return a==b;if(a.Length!=b.Length)return false;if(x&&ReferenceEquals(a,b))return true;
            for(int i=0;i<a.Length;i++)if(!PartSame(a[i],b[i],x))return false;return true;
        }
        static bool PartSame(PartInstance a, PartInstance b, bool x)
        {
            if(a==null||b==null)return a==b;if(x&&ReferenceEquals(a,b))return true;
            return a.version==b.version&&StringSame(a.instanceId,b.instanceId)&&StringSame(a.definitionId,b.definitionId)
                &&StringSame(a.storageId,b.storageId)&&a.location==b.location&&StringSame(a.ownerId,b.ownerId)
                &&StringSame(a.slot,b.slot)&&ConditionSame(a.condition,b.condition,x);
        }
        static bool ConditionSame(ComponentCondition a, ComponentCondition b, bool x)
        {
            if(a==null||b==null)return a==b;if(x&&ReferenceEquals(a,b))return true;
            return FloatSame(a.health,b.health,x)&&a.failed==b.failed&&StringSame(a.failureReason,b.failureReason)
                &&DoubleSame(a.runningSeconds,b.runningSeconds,x)&&DoubleSame(a.distanceM,b.distanceM,x)
                &&DoubleSame(a.stressExposure,b.stressExposure,x)&&DoubleSame(a.temperatureC,b.temperatureC,x)
                &&DoubleSame(a.slipEnergyJ,b.slipEnergyJ,x)&&DoubleSame(a.deposits,b.deposits,x)
                &&DoubleSame(a.leakSeverity,b.leakSeverity,x)&&DoubleSame(a.adjustmentError,b.adjustmentError,x)
                &&DoubleSame(a.serviceWearRemainder,b.serviceWearRemainder,x)&&a.serviceCount==b.serviceCount
                &&a.reconditionCount==b.reconditionCount&&StringSame(a.lastService,b.lastService)
                &&ElementsSame(a.elements,b.elements,x)&&ClutchSame(a.clutch,b.clutch,x);
        }
        static bool ClutchSame(ClutchConditionState a, ClutchConditionState b, bool x)
        { if(a==null||b==null)return a==b;if(x&&ReferenceEquals(a,b))return true;return a.version==b.version&&DoubleSame(a.oilLitres,b.oilLitres,x)&&DoubleSame(a.oilCondition,b.oilCondition,x); }
        static bool ElementsSame(EngineElement[] a, EngineElement[] b, bool x)
        {
            if(a==null||b==null)return a==b;if(a.GetType()!=b.GetType()||a.Length!=b.Length)return false;if(x&&ReferenceEquals(a,b))return true;
            for(int i=0;i<a.Length;i++)if(!ElementSame(a[i],b[i],x))return false;return true;
        }
        static bool ElementSame(EngineElement a, EngineElement b, bool x)
        {
            if(a==null||b==null)return a==b;
            if(a.GetType()!=b.GetType())return false;
            Type kind = a.GetType();
            if (kind != typeof(EngineElement) && kind != typeof(PistonElement)
                && kind != typeof(ConnectingRodElement) && kind != typeof(PistonRingElement)
                && kind != typeof(BearingElement) && kind != typeof(ValveElement)
                && kind != typeof(ValveSpringElement) && kind != typeof(ValveSealElement)
                && kind != typeof(FollowerElement) && kind != typeof(WheelElement)
                && kind != typeof(IgnitionElement) && kind != typeof(SparkPlugElement)
                && kind != typeof(GlowPlugElement) && kind != typeof(FuelSystemElement)
                && kind != typeof(TireCondition)) return false;
            if (x && ReferenceEquals(a,b)) return true;
            if(!StringSame(a.id,b.id)||!StringSame(a.definitionId,b.definitionId)||!FloatSame(a.health,b.health,x)
                ||!StringSame(a.role,b.role)||a.cylinder!=b.cylinder||a.valve!=b.valve||!FloatSame(a.severity,b.severity,x)
                ||!DoubleSame(a.wearRemainder,b.wearRemainder,x)||a.fault!=b.fault)return false;
            if(a is TireCondition at && b is TireCondition bt)
                return at.failed==bt.failed&&DoubleSame(at.temperatureC,bt.temperatureC,x)&&DoubleSame(at.slipEnergyJ,bt.slipEnergyJ,x)
                    &&DoubleSame(at.distanceM,bt.distanceM,x)&&DoubleSame(at.lastContactWorkJ,bt.lastContactWorkJ,x)
                    &&DoubleSame(at.consumedLife,bt.consumedLife,x)&&DoubleSame(at.runningSeconds,bt.runningSeconds,x)
                    &&StringSame(at.failureReason,bt.failureReason);
            return true;
        }
    }

    internal sealed class ReservedBuildHashBaseline
    {
        readonly ReservedBuildValues values;
        readonly string[] orderedSlotLabels;
        internal ReservedBuildHashBaseline(ReservedBuildValues value, string[] labels)
        { values = value; orderedSlotLabels = labels == null ? null : (string[])labels.Clone(); }
        internal ReservedBuildValues ValuesCopy() { return values == null ? null : values.DefensiveCopy(); }
        internal ReservedBuildValues Values { get { return ValuesCopy(); } }
        internal string[] OrderedSlotLabelsCopy() { return orderedSlotLabels == null ? null : (string[])orderedSlotLabels.Clone(); }
        internal bool MatchesCurrent(ReservedBuildValues current, string[] labels)
        {
            if (values == null || !values.MatchesCurrent(current, ReservedBuildComparisonMode.ExactHashBaseline)) return false;
            if (orderedSlotLabels == null || labels == null || orderedSlotLabels.Length != labels.Length) return orderedSlotLabels == null && labels == null;
            for (int i=0;i<labels.Length;i++) if (!String.Equals(orderedSlotLabels[i], labels[i], StringComparison.Ordinal)) return false;
            return true;
        }
    }

    internal sealed class ReservedBuildContext
    {
        readonly ReservedBuildImportResult[] results;
        ReservedBuildContext(ReservedBuildImportResult[] values) { results = values ?? Array.Empty<ReservedBuildImportResult>(); }
        internal static ReservedBuildContext FromImportedResults(IEnumerable<ReservedBuildImportResult> orderedResults)
        { return new ReservedBuildContext(orderedResults == null ? Array.Empty<ReservedBuildImportResult>() : orderedResults.ToArray()); }
        internal static ReservedBuildContext FromCreatedJobs(PlayerProfile owner, IEnumerable<AssignedGarageJob> createdJobs)
        {
            var list = new List<ReservedBuildImportResult>();
            foreach (var job in createdJobs)
            {
                var car = owner.cars.Find(item => item.instanceId == job.carId);
                var values = ReservedBuildValues.CaptureCreated(owner, car);
                string[] slots = car.installed.slots.OrderBy(slot => slot.partInstanceId, StringComparer.Ordinal).Select(slot => slot.slot).ToArray();
                string digest = ReservedBuildImportResult.Digest(job.snapshotJson);
                var hash = new ReservedBuildHashBaseline(values, slots);
                list.Add(new ReservedBuildImportResult(job.id, job.carId, job.snapshot, digest, job.snapshotJson == null,
                    ReservedBuildImportKind.CreatedTyped, values, values, hash, ReservedBuildFallbackKind.Values,
                    true, null, null, null, null, null, null, null, null, null));
            }
            return new ReservedBuildContext(list.ToArray());
        }
        internal bool TryGet(string jobId, string carId, string snapshotHash, out ReservedBuildImportResult result)
        {
            foreach (var item in results)
                if (item != null && String.Equals(item.JobId,jobId,StringComparison.Ordinal)
                    && String.Equals(item.CarId,carId,StringComparison.Ordinal)
                    && String.Equals(item.SnapshotHash,snapshotHash,StringComparison.Ordinal)) { result=item; return true; }
            result=null; return false;
        }
    }

    internal sealed class ReservedBuildImportResult
    {
        internal string JobId { get; private set; }
        internal string CarId { get; private set; }
        internal string SnapshotHash { get; private set; }
        internal string SnapshotJsonDigest { get; private set; }
        internal bool SnapshotJsonWasNull { get; private set; }
        internal ReservedBuildImportKind Kind { get; private set; }
        readonly ReservedBuildValues jsonTextBaseline, jsonExpected;
        readonly ReservedBuildHashBaseline hashBaseline;
        internal ReservedBuildValues JsonTextBaselineCopy() { return jsonTextBaseline == null ? null : jsonTextBaseline.DefensiveCopy(); }
        internal ReservedBuildValues JsonExpectedCopy() { return jsonExpected == null ? null : jsonExpected.DefensiveCopy(); }
        internal ReservedBuildValues JsonTextBaseline { get { return JsonTextBaselineCopy(); } }
        internal ReservedBuildValues JsonExpected { get { return JsonExpectedCopy(); } }
        internal ReservedBuildHashBaseline HashBaseline { get { return hashBaseline; } }
        internal ReservedBuildFallbackKind FallbackKind { get; private set; }
        internal bool FallbackMatchedAtImport { get; private set; }
        readonly string hashErrorType, hashErrorMessage, hashErrorParameterName;
        readonly string captureErrorType, captureErrorMessage, captureErrorParameterName;
        readonly string errorType, errorMessage, errorParameterName;
        internal string HashErrorType { get { return hashErrorType; } }
        internal string HashErrorMessage { get { return hashErrorMessage; } }
        internal string HashErrorParameterName { get { return hashErrorParameterName; } }
        internal string CaptureErrorType { get { return captureErrorType; } }
        internal string CaptureErrorMessage { get { return captureErrorMessage; } }
        internal string CaptureErrorParameterName { get { return captureErrorParameterName; } }
        internal string ErrorType { get { return errorType; } }
        internal string ErrorMessage { get { return errorMessage; } }
        internal string ErrorParameterName { get { return errorParameterName; } }

        internal ReservedBuildImportResult(string jobId, string carId, string snapshotHash, string digest, bool snapshotJsonWasNull,
            ReservedBuildImportKind kind, ReservedBuildValues textBaseline, ReservedBuildValues expected,
            ReservedBuildHashBaseline baseline, ReservedBuildFallbackKind fallbackKind, bool fallbackMatched,
            string hashType, string hashMessage, string hashParam, string captureType, string captureMessage,
            string captureParam, string type, string message, string param)
        {
            JobId=jobId; CarId=carId; SnapshotHash=snapshotHash; SnapshotJsonDigest=digest;
            SnapshotJsonWasNull=snapshotJsonWasNull; Kind=kind;
            jsonTextBaseline=textBaseline;
            jsonExpected=expected; hashBaseline=baseline;
            FallbackKind=fallbackKind; FallbackMatchedAtImport=fallbackMatched;
            hashErrorType=hashType;hashErrorMessage=hashMessage;hashErrorParameterName=hashParam;
            captureErrorType=captureType;captureErrorMessage=captureMessage;captureErrorParameterName=captureParam;
            errorType=type;errorMessage=message;errorParameterName=param;
        }
        internal static string Digest(string text)
        {
            using (var sha=SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(text == null ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(text)));
        }
        internal bool FallbackBindingMatches(string snapshotJson)
        { return SnapshotJsonWasNull == (snapshotJson == null)
            && String.Equals(SnapshotJsonDigest,Digest(snapshotJson),StringComparison.Ordinal); }
        internal ReservedBuildMatchOutcome MatchCurrent(ReservedBuildValues current, string[] slots, string snapshotJson)
        {
            if (hashErrorType != null) return new ReservedBuildMatchOutcome(ReservedBuildMatchKind.HashError,hashErrorType,hashErrorMessage,hashErrorParameterName);
            if (hashBaseline != null && hashBaseline.MatchesCurrent(current,slots)) return new ReservedBuildMatchOutcome(ReservedBuildMatchKind.HashMatched);
            if (captureErrorType != null) return new ReservedBuildMatchOutcome(ReservedBuildMatchKind.CaptureError,captureErrorType,captureErrorMessage,captureErrorParameterName);
            if (!FallbackBindingMatches(snapshotJson)) return new ReservedBuildMatchOutcome(ReservedBuildMatchKind.StalePayload);
            if (jsonTextBaseline != null && jsonTextBaseline.MatchesCurrent(current,ReservedBuildComparisonMode.ExactHashBaseline))
                return new ReservedBuildMatchOutcome(ReservedBuildMatchKind.JsonTextMatched);
            if (FallbackKind == ReservedBuildFallbackKind.Absent) return new ReservedBuildMatchOutcome(ReservedBuildMatchKind.FallbackAbsent);
            if (FallbackKind == ReservedBuildFallbackKind.Error) return new ReservedBuildMatchOutcome(ReservedBuildMatchKind.FallbackError,errorType,errorMessage,errorParameterName);
            return jsonExpected != null && jsonExpected.MatchesCurrent(current,ReservedBuildComparisonMode.LegacyJsonFallback)
                ? new ReservedBuildMatchOutcome(ReservedBuildMatchKind.JsonExpectedMatched)
                : new ReservedBuildMatchOutcome(ReservedBuildMatchKind.Mismatch);
        }
    }
}

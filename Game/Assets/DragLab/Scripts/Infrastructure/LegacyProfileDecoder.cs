using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using UnityEngine;

namespace DragLab.Infrastructure
{
    /// <summary>
    /// The only production JSON decoder used while migrating the supported v12
    /// profile boundary. It removes only source-audited Unity inline-null ghosts.
    /// </summary>
    internal static class LegacyProfileDecoder
    {
        internal static PlayerProfile DecodeAndValidate(string json, MechanicalCatalog catalog)
        {
            if (string.IsNullOrEmpty(json)) throw new InvalidDataException("Legacy profile payload is empty.");
            ValidateRawTypedSource(json);
            var profile = JsonUtility.FromJson<PlayerProfile>(DrivingPreferences.AddToJson(json));
            if (profile == null) throw new InvalidDataException("Legacy profile could not be decoded.");
            ValidateLoadedV12(profile, catalog);
            return profile;
        }

        internal static void ValidateLoadedV12(PlayerProfile profile, MechanicalCatalog catalog)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            profile.catalog = catalog;
            if (profile.saveVersion != MechanicsOwnership.SaveVersion || profile.mechanicsVersion != MechanicsSchema.Version)
                throw new NotSupportedException("Only the current v12/mechanics-1 profile contract can be migrated.");
            if (profile.drivelineVersion != 2 || profile.equipmentVersion != 1 || profile.ignitionHardwareVersion != 1)
                throw new NotSupportedException("Current profile normalization versions are incomplete; reset or an explicit logical migration is required.");

            Require(profile.cars != null && profile.engines != null && profile.parts != null,
                "Current profile is missing an authoritative asset collection.");

            foreach (var car in profile.cars)
                if (car == null || string.IsNullOrEmpty(car.instanceId) || string.IsNullOrEmpty(car.chassisId)
                    || car.installed == null)
                    throw new InvalidDataException("Current car is missing an authoritative identity or installed build.");
            foreach (var engine in profile.engines)
                if (engine == null || string.IsNullOrEmpty(engine.instanceId))
                    throw new InvalidDataException("Current engine assembly is missing its authoritative identity.");

            // Verify the binding before discarding Unity's inline-null fluid object.
            // Attached engines have no independent fluid authority; their car does.
            foreach (var engine in profile.engines)
            {
                if (engine == null) throw new InvalidDataException("Null engine assembly in current profile.");
                if (string.IsNullOrEmpty(engine.parentVehicleId)) continue;
                var parents = profile.cars.Where(car => car != null && car.instanceId == engine.parentVehicleId).ToArray();
                var bindings = profile.cars.Where(car => car != null && car.engineAssemblyId == engine.instanceId).ToArray();
                if (parents.Length != 1 || bindings.Length != 1 || !ReferenceEquals(parents[0], bindings[0])
                    || parents[0].vehicleId != engine.vehicleDefinitionId)
                    throw new InvalidDataException("Attached engine fluid placeholder has no unique verified car binding.");
                engine.fluids = null;
            }

            foreach (var car in profile.cars)
            {
                if (car.serviceHardwareVersion != 1 || car.fluids == null)
                    throw new InvalidDataException("Current car is missing authoritative fluid state.");

                var spec = catalog.Spec(car.vehicleId);
                if (spec == null || car.fluids.Validate(spec.fluids) != null)
                    throw new InvalidDataException("Current car has invalid authoritative fluid state.");

                if (car.engineRebuild != null && !car.engineRebuild.active)
                {
                    var job = car.engineRebuild;
                    if (string.IsNullOrEmpty(job.id) && string.IsNullOrEmpty(job.engineId)
                        && job.startedUtcTicks == 0 && job.readyUtcTicks == 0 && job.paidPrice == 0
                        && (job.targets == null || job.targets.Count == 0))
                        car.engineRebuild = null;
                    else
                        throw new InvalidDataException("Inactive rebuild record contains data and cannot be normalized.");
                }
            }

            if (profile.inventory == null)
                throw new InvalidDataException("Current profile is missing inventory state.");
            if (profile.inventory.cart != null)
            {
                var cart = profile.inventory.cart;
                bool emptyGhost = cart.version == 1 && cart.createdRun == 0 && cart.expiresRun == 0
                    && cart.generation == 0 && string.IsNullOrEmpty(cart.carId)
                    && cart.lines != null && cart.lines.Count == 0
                    && cart.supply != null && cart.supply.Count == 0;
                if (emptyGhost) profile.inventory.cart = null;
                else if (string.IsNullOrEmpty(cart.carId))
                    throw new InvalidDataException("Empty-owner cart contains data and cannot be normalized.");
            }

            var work = profile.recoveryWork;
            if (work != null && string.IsNullOrEmpty(work.id) && work.startedUtcTicks == 0
                && work.readyUtcTicks == 0 && work.reward == 0)
                profile.recoveryWork = null;

            if (profile.inventory.dealer == null)
                throw new InvalidDataException("Current profile is missing dealer stock state.");
            foreach (var stock in profile.inventory.dealer)
            {
                if (stock == null) continue;
                bool hasPart = stock.HasPart;
                bool hasUnit = stock.HasUnit;
                if (hasPart == hasUnit) continue;
                if (hasPart && !EmptyUnitWrapper(stock.unit)
                    || hasUnit && !EmptyPartWrapper(stock.part))
                    throw new InvalidDataException("Dealer stock has a partial inactive payload and cannot be normalized.");
                stock.isUnit = hasUnit;
                if (hasUnit) stock.part = null;
                else stock.unit = null;
            }

            string invalid;
            try { invalid = MechanicsOwnership.Validate(profile, catalog); }
            catch (Exception error)
            {
                throw new InvalidDataException("Current profile validation could not inspect the original state.", error);
            }
            if (invalid != null) throw new InvalidDataException("Current profile validation failed: " + invalid);
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidDataException(message);
        }

        static void ValidateRawTypedSource(string json)
        {
            var root = StateRows.Fields(json);
            RequireNumber(root, "cash");
            RequireNumber(root, "revision");
            RequireNumber(root, "lastEconomyUtcTicks");
            var receipts = Find(root, "economyReceipts");
            Require(receipts != null && receipts.StartsWith("[", StringComparison.Ordinal),
                "Legacy profile is missing its economy receipt array.");

            var cars = Find(root, "cars");
            Require(cars != null && cars.StartsWith("[", StringComparison.Ordinal),
                "Legacy profile is missing its car array.");
            foreach (var car in StateRows.Items(cars))
            {
                Require(car.StartsWith("{", StringComparison.Ordinal), "Legacy car row is not an object.");
                var fields = StateRows.Fields(car);
                RequireFluidState(fields, "car");
            }

            var engines = Find(root, "engines");
            Require(engines != null && engines.StartsWith("[", StringComparison.Ordinal),
                "Legacy profile is missing its engine array.");
            foreach (var engine in StateRows.Items(engines))
            {
                Require(engine.StartsWith("{", StringComparison.Ordinal), "Legacy engine row is not an object.");
                var fields = StateRows.Fields(engine);
                var parent = Find(fields, "parentVehicleId");
                bool detached = string.IsNullOrEmpty(parent) || parent == "null" || parent == "\"\"";
                if (detached) RequireFluidState(fields, "detached engine");
            }
        }

        static void RequireFluidState(List<KeyValuePair<string, string>> owner, string label)
        {
            var raw = Find(owner, "fluids");
            Require(raw != null && raw.StartsWith("{", StringComparison.Ordinal),
                "Legacy " + label + " is missing authoritative fluid state.");
            var fields = StateRows.Fields(raw);
            foreach (string name in new[] {
                "oilLitres", "coolantLitres", "oilTemperatureC", "oilPressureBar", "coolantPressureBar",
                "oilCondition", "oilConsumedLitres", "oilLeakedLitres", "coolantLostLitres",
                "serviceElapsedSeconds", "oilBurnLps", "oilLeakLps", "coolantLeakLps", "coolantVentLps"
            })
            {
                var value = Find(fields, name);
                Require(IsFiniteJsonNumber(value), "Legacy " + label + " is missing or has malformed fluid field " + name + ".");
            }
        }

        static void RequireNumber(List<KeyValuePair<string, string>> fields, string name)
        {
            string value = Find(fields, name);
            bool valid = !string.IsNullOrEmpty(value) && !value.StartsWith("\"", StringComparison.Ordinal)
                && long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _);
            if (name == "cash") valid = valid && int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _);
            Require(valid, "Legacy profile is missing or has malformed typed field " + name + ".");
        }

        static string Find(List<KeyValuePair<string, string>> fields, string name)
        {
            var matches = fields.Where(field => field.Key == name).ToArray();
            if (matches.Length > 1) throw new InvalidDataException("Legacy profile repeats field " + name + ".");
            return matches.Length == 0 ? null : matches[0].Value;
        }

        static bool IsFiniteJsonNumber(string value)
            => !string.IsNullOrEmpty(value) && !string.Equals(value, "null", StringComparison.Ordinal)
                && !value.StartsWith("\"", StringComparison.Ordinal)
                && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
                && MechanicsSchema.Finite(parsed);

        static bool EmptyUnitWrapper(LooseEngineElement unit)
            => unit == null || string.IsNullOrEmpty(unit.definitionId) && string.IsNullOrEmpty(unit.storageId)
                && unit.unitPrice == 0 && !unit.tireUnit && unit.element == null;

        static bool EmptyPartWrapper(PartInstance part)
            => part == null || part.version == MechanicsSchema.Version && string.IsNullOrEmpty(part.instanceId)
                && string.IsNullOrEmpty(part.definitionId) && string.IsNullOrEmpty(part.storageId)
                && part.location == PartLocation.Inventory && string.IsNullOrEmpty(part.ownerId)
                && string.IsNullOrEmpty(part.slot) && EmptyCondition(part.condition);

        static bool EmptyCondition(ComponentCondition condition)
            => condition == null || condition.health == 1 && !condition.failed
                && string.IsNullOrEmpty(condition.failureReason) && condition.runningSeconds == 0
                && condition.distanceM == 0 && condition.stressExposure == 0
                && condition.temperatureC == 20 && condition.slipEnergyJ == 0
                && condition.deposits == 0 && condition.leakSeverity == 0
                && condition.adjustmentError == 0 && condition.serviceWearRemainder == 0
                && condition.serviceCount == 0 && condition.reconditionCount == 0
                && string.IsNullOrEmpty(condition.lastService)
                && (condition.elements == null || condition.elements.Length == 0) && condition.clutch == null;
    }
}

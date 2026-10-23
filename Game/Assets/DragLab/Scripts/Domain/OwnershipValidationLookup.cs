using System;
using System.Collections.Generic;

namespace DragLab.Domain.Mechanics
{
    // First-match lookup scope for MechanicsOwnership.Validate's read-only validation stage.
    // It preserves existing malformed-input scan behavior and is not an ownership validator.
    internal sealed class OwnershipValidationLookup : IDisposable
    {
        readonly Dictionary<string, OwnedCar> carsById = new Dictionary<string, OwnedCar>(StringComparer.Ordinal);
        readonly Dictionary<string, EngineAssemblyInstance> enginesById = new Dictionary<string, EngineAssemblyInstance>(StringComparer.Ordinal);
        readonly Dictionary<string, PartInstance> partsById = new Dictionary<string, PartInstance>(StringComparer.Ordinal);
        IList<OwnedCar> cars;
        IList<EngineAssemblyInstance> engines;
        IList<PartInstance> parts;
        OwnedCar nullCarId;
        EngineAssemblyInstance nullEngineId;
        PartInstance nullPartId;
        bool hasNullCarId, hasNullEngineId, hasNullPartId;
        bool fallbackCars, fallbackEngines, fallbackParts;
        bool carPartRelationBuilt, fallbackCarPartRelation;
        readonly Dictionary<string, OwnedCar> carByPartId = new Dictionary<string, OwnedCar>(StringComparer.Ordinal);
        OwnedCar nullPartOwner;
        bool hasNullPartOwner;
        bool disposed;

        OwnershipValidationLookup(IList<OwnedCar> cars, IList<EngineAssemblyInstance> engines,
            IList<PartInstance> parts)
        {
            this.cars = cars;
            this.engines = engines;
            this.parts = parts;
            CaptureCars();
            CaptureEngines();
            CaptureParts();
        }

        internal static OwnershipValidationLookup Capture(IList<OwnedCar> cars,
            IList<EngineAssemblyInstance> engines, IList<PartInstance> parts)
        {
            return new OwnershipValidationLookup(cars, engines, parts);
        }

        void CaptureCars()
        {
            if (cars == null) { fallbackCars = true; return; }
            for (int i = 0; i < cars.Count; i++)
            {
                var car = cars[i];
                if (car == null) { fallbackCars = true; continue; }
                if (car.instanceId == null)
                {
                    if (!hasNullCarId) { hasNullCarId = true; nullCarId = car; }
                }
                else if (!carsById.ContainsKey(car.instanceId)) carsById.Add(car.instanceId, car);
            }
        }

        void CaptureEngines()
        {
            if (engines == null) { fallbackEngines = true; return; }
            for (int i = 0; i < engines.Count; i++)
            {
                var engine = engines[i];
                if (engine == null) { fallbackEngines = true; continue; }
                if (engine.instanceId == null)
                {
                    if (!hasNullEngineId) { hasNullEngineId = true; nullEngineId = engine; }
                }
                else if (!enginesById.ContainsKey(engine.instanceId)) enginesById.Add(engine.instanceId, engine);
            }
        }

        void CaptureParts()
        {
            if (parts == null) { fallbackParts = true; return; }
            for (int i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                if (part == null) { fallbackParts = true; continue; }
                if (part.instanceId == null)
                {
                    if (!hasNullPartId) { hasNullPartId = true; nullPartId = part; }
                }
                else if (!partsById.ContainsKey(part.instanceId)) partsById.Add(part.instanceId, part);
            }
        }

        internal OwnedCar FindCar(string id)
        {
            ThrowIfDisposed();
            if (fallbackCars) return ScanCar(id);
            if (id == null) return hasNullCarId ? nullCarId : null;
            return carsById.TryGetValue(id, out var car) ? car : null;
        }

        OwnedCar ScanCar(string id)
        {
            for (int i = 0; i < cars.Count; i++)
            {
                var car = cars[i];
                if (car.instanceId == id) return car;
            }
            return null;
        }

        internal EngineAssemblyInstance FindEngine(string id)
        {
            ThrowIfDisposed();
            if (fallbackEngines) return ScanEngine(id);
            if (id == null) return hasNullEngineId ? nullEngineId : null;
            return enginesById.TryGetValue(id, out var engine) ? engine : null;
        }

        EngineAssemblyInstance ScanEngine(string id)
        {
            for (int i = 0; i < engines.Count; i++)
            {
                var engine = engines[i];
                if (engine.instanceId == id) return engine;
            }
            return null;
        }

        internal PartInstance FindPart(string id)
        {
            ThrowIfDisposed();
            if (fallbackParts) return ScanPart(id);
            if (id == null) return hasNullPartId ? nullPartId : null;
            return partsById.TryGetValue(id, out var part) ? part : null;
        }

        PartInstance ScanPart(string id)
        {
            for (int i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                if (part.instanceId == id) return part;
            }
            return null;
        }

        internal bool HasCarID(string id)
        {
            ThrowIfDisposed();
            if (fallbackCars)
            {
                for (int i = 0; i < cars.Count; i++)
                    if (cars[i].instanceId == id) return true;
                return false;
            }
            return id == null ? hasNullCarId : carsById.ContainsKey(id);
        }

        internal OwnedCar CarContainingPart(string partId)
        {
            ThrowIfDisposed();
            EnsureCarPartRelation();
            if (fallbackCarPartRelation) return ScanCarContainingPart(partId);
            if (partId == null) return hasNullPartOwner ? nullPartOwner : null;
            return carByPartId.TryGetValue(partId, out var car) ? car : null;
        }

        void EnsureCarPartRelation()
        {
            if (carPartRelationBuilt) return;
            carPartRelationBuilt = true;
            if (cars == null || fallbackCars) { fallbackCarPartRelation = true; return; }
            for (int i = 0; i < cars.Count; i++)
            {
                var car = cars[i];
                if (car == null || car.installed == null || car.installed.slots == null)
                { fallbackCarPartRelation = true; break; }
                foreach (var binding in car.installed.slots)
                {
                    if (binding == null) { fallbackCarPartRelation = true; break; }
                    string id = binding.partInstanceId;
                    if (id == null)
                    {
                        if (!hasNullPartOwner) { hasNullPartOwner = true; nullPartOwner = car; }
                    }
                    else if (!carByPartId.ContainsKey(id)) carByPartId.Add(id, car);
                }
                if (fallbackCarPartRelation) break;
            }
            if (fallbackCarPartRelation)
            {
                carByPartId.Clear();
                nullPartOwner = null;
                hasNullPartOwner = false;
            }
        }

        OwnedCar ScanCarContainingPart(string partId)
        {
            for (int i = 0; i < cars.Count; i++)
            {
                var car = cars[i];
                foreach (var binding in car.installed.slots)
                    if (binding.partInstanceId == partId) return car;
            }
            return null;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            carsById.Clear(); enginesById.Clear(); partsById.Clear(); carByPartId.Clear();
            cars = null; engines = null; parts = null;
            nullCarId = null; nullEngineId = null; nullPartId = null; nullPartOwner = null;
        }

        void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(OwnershipValidationLookup));
        }
    }
}

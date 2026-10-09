using System.Linq;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Infrastructure
{
    public static class PhysicalBenchTransfer
    {
        static RunSnapshot pending;
        public static RunSnapshot Capture(PlayerProfile profile, OwnedCar car, int seed = 1234)
        {
            var resolved = MechanicsOwnership.Resolve(profile, car, profile.catalog);
            return PhysicalDrivingSession.Copy(new RunSnapshot { seed = seed, vehicle = resolved.spec.Copy(), tune = resolved.tune.Copy(), driving = profile.driving.Copy(),
                installed = car.installed, startingFuel = car.fuel, startingFluids=car.fluids, components = profile.parts.Where(p => p.ownerId == car.instanceId || p.ownerId == car.engineAssemblyId).Select(p => p.Copy()).ToList() });
        }
        public static void Put(RunSnapshot snapshot) { pending = PhysicalDrivingSession.Copy(snapshot); }
        public static RunSnapshot Take() { var result = pending; pending = null; return result; }
    }
}

using System;

namespace DragLab.Domain
{
    public sealed class TuneDraft
    {
        public readonly string vehicleInstanceId;
        public readonly long revision;
        public readonly Tune saved;
        public readonly string savedPaint;
        public Tune values;
        public string paint;
        public TuneDraft(OwnedCar car, long revision)
        {
            vehicleInstanceId = car.instanceId; this.revision = revision; saved = car.tune.Copy(); values = saved.Copy(); savedPaint = paint = car.paintHex;
        }
        public void ApplyTo(PlayerProfile profile)
        {
            if (profile.revision != revision) throw new InvalidOperationException("The installed build changed. Reopen tuning before applying.");
            var car = profile.cars.Find(c => c.instanceId == vehicleInstanceId);
            if (car == null) throw new InvalidOperationException("The tuned car no longer exists.");
            if (values.awd != null && values.awd.Validate() != null) throw new InvalidOperationException(values.awd.Validate());
            foreach (var field in typeof(Tune).GetFields()) if (field.FieldType == typeof(float))
            { float v = (float)field.GetValue(values); if (float.IsNaN(v) || float.IsInfinity(v)) throw new InvalidOperationException("Tune values must be finite."); }
            foreach (float grip in new[] { values.frontLeftGrip, values.frontRightGrip, values.rearLeftGrip, values.rearRightGrip }) if (grip < 0 || grip > 1.5f) throw new InvalidOperationException("Split-grip inputs must be 0–1.5.");
            var resolved = DragLab.Domain.Mechanics.MechanicsOwnership.Definitions(profile, car);
            var effective = profile.catalog.Resolve(car.vehicleId, resolved, values);
            car.tune.ApplyAdjustments(effective.tune); car.tune.ClampTo(effective.spec); car.paintHex = paint;
        }
    }
}

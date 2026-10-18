using System;

namespace DragLab.Domain.Mechanics
{
    // Fictional calibrated alternatives: fitment and IDs remain stable; no rank prerequisites.
    public static class CatalogVariants
    {
        static PartDefinition Variant(MechanicalCatalog catalog, VehicleSpec spec, PartCategory slot, string key, string label, int price, int stage = 1)
        {
            var part = catalog.Find(catalog.Id(spec.id, slot, stage)).Copy();
            part.id = spec.id + "/" + slot + "/" + key; part.name = spec.displayName + " / " + label; part.price = price; part.quality = "Aftermarket";
            catalog.parts.Add(part); return part;
        }
        public static void Add(MechanicalCatalog catalog, VehicleSpec spec)
        {
            var sizes = new[] { .75f, 1f, 1.4f }; var names = new[] { "Compact", "Street", "Large" };
            foreach (var topology in new[] { EngineUpgradeInduction.Turbo, EngineUpgradeInduction.TwinTurbo, EngineUpgradeInduction.Twincharged })
                for (int size = 0; size < sizes.Length; size++) foreach (bool adjustable in new[] { false, true }) {
                    string key = topology + names[size] + (adjustable ? "Adjustable" : "Fixed");
                    var p = Variant(catalog, spec, PartCategory.Induction, key, PartSemantics.Friendly(topology.ToString()) + " / " + names[size].ToLowerInvariant() + " / " + (adjustable ? "adjustable controller" : "fixed wastegate spring"), 1500 + size * 650 + (adjustable ? 500 : 0) + (topology == EngineUpgradeInduction.Turbo ? 0 : 1000));
                    p.induction = topology; p.turboSize = sizes[size]; p.power = p.boostScale = 1;
                    p.turboUnits = topology == EngineUpgradeInduction.TwinTurbo ? 2 : 1; p.superchargerUnits = topology == EngineUpgradeInduction.Twincharged ? 1 : 0;
                    p.fixedBoost = .8f; p.boostMin = .8f; p.boostMax = 1.2f;
                    p.tuning = adjustable ? PartTuningOptions.Boost | PartTuningOptions.Valve : PartTuningOptions.None;
                    if (adjustable && topology == EngineUpgradeInduction.Twincharged) p.tuning |= PartTuningOptions.Pulley;
                    p.valve = BlowOffType.Recirculated;
                    p.massKg = (topology == EngineUpgradeInduction.TwinTurbo ? 24 : topology == EngineUpgradeInduction.Twincharged ? 30 : 14) * (.85f + .15f * sizes[size]);
                    p.description = "Physical size " + sizes[size].ToString("F2") + "× reference per turbo; unit flow ceiling " + (spec.induction.turboUnitFlowKgS * sizes[size]).ToString("F3") + " kg/s. " +
                        (size == 0 ? "Earlier boost and faster response, but airflow limits top-end output. " : size == 2 ? "More airflow headroom, but later boost and slower response; peak HP is not guaranteed. " : "Balanced response and flow headroom. ") +
                        (adjustable ? "Included controller adjusts above the 0.80× spring floor, up to 1.20×. " : "Fixed 0.80× wastegate calibration and recirculation valve; no boost adjustment. ") +
                        "Size requires a hardware swap. Heat, fuel capacity, engine strength and traction still limit usable output.";
                }
            foreach (var topology in new[] { EngineUpgradeInduction.Supercharger, EngineUpgradeInduction.TwinSupercharger }) foreach (bool adjustable in new[] { false, true }) {
                var p = Variant(catalog, spec, PartCategory.Induction, topology + (adjustable ? "PulleySet" : "FixedDrive"), PartSemantics.Friendly(topology.ToString()) + " / " + (adjustable ? "interchangeable pulley set" : "fixed street drive"), adjustable ? 2900 : 2200);
                p.induction = topology; p.power = p.boostScale = 1; p.fixedBoost = 1; p.fixedPulley = adjustable ? 1 : .9f;
                p.turboUnits = 0; p.superchargerUnits = topology == EngineUpgradeInduction.TwinSupercharger ? 2 : 1;
                p.tuning = adjustable ? PartTuningOptions.Pulley : PartTuningOptions.None;
                p.massKg = topology == EngineUpgradeInduction.TwinSupercharger ? 28 : 18;
                p.description = adjustable ? "Included pulley set adjusts drive ratio 0.85–1.20×. More blower speed raises boost, parasitic drive load and charge heat; boost is not a separate electronic knob." : "Fixed 0.90× street pulley and boost calibration. Immediate belt-driven response with parasitic load; swap hardware to change drive ratio.";
            }
            var heavy = Variant(catalog, spec, PartCategory.Flywheel, "HeavyStreet", "heavy street flywheel/flexplate", 380);
            heavy.massScale = 1.25f; heavy.massKg = spec.flywheel.massKg * heavy.massScale; heavy.inertiaKgM2 = (float)(spec.flywheel.Inertia * heavy.massScale);
            heavy.description = "Fixed heavier rotating assembly: holds more energy through engagement and slows neutral RPM decay; slower rev response. No tuning knob or free power.";
            var medium = Variant(catalog, spec, PartCategory.Flywheel, "BalancedStreet", "balanced street flywheel/flexplate", 650);
            medium.massScale = .8f; medium.massKg = spec.flywheel.massKg * medium.massScale; medium.inertiaKgM2 = (float)(spec.flywheel.Inertia * medium.massScale);
            medium.description = "Fixed 80% factory rotating mass: moderate rev-response improvement with more stored energy than the lightweight racing part.";
            var endurance = Variant(catalog, spec, PartCategory.Cooling, "Endurance", "endurance radiator and pump", 1450);
            endurance.massKg = 29; endurance.engineHeatTransferScale = 2.8f; endurance.coolingWattsPerC = 900; endurance.heatCapacityJPerC = 38000;
            endurance.description = "Fixed high heat-rejection and coolant capacity for sustained loaded runs, at extra mass. No tune access or RPM/torque-strength bonus.";
            var compact = Variant(catalog, spec, PartCategory.Cooling, "LightSport", "lightweight sport radiator and pump", 850);
            compact.massKg = 15; compact.engineHeatTransferScale = 1.5f; compact.coolingWattsPerC = 460; compact.heatCapacityJPerC = 14000;
            compact.description = "Fixed lighter cooling kit, between stock and high-flow heat rejection. Smaller thermal buffer heats up sooner under prolonged load.";
            var organic = Variant(catalog, spec, PartCategory.Clutch, "StreetOrganic", "street organic friction clutch", 750);
            organic.capacityScale = 1.3f; organic.massKg = 8.5f; organic.wearEnergyJ = 90000000; organic.thermalLimitC = 280;
            organic.description = "Fixed moderate torque capacity with a larger friction-work life budget. Still slips above its rated transferable torque; no pedal/calibration unlock.";
            var race = Variant(catalog, spec, PartCategory.Clutch, "RaceFriction", "race friction clutch", 1500);
            race.capacityScale = 2.1f; race.massKg = 11; race.wearEnergyJ = 45000000; race.thermalLimitC = 340;
            race.description = "Fixed high transferable torque and heat tolerance, with less sliding-work life than the street organic kit. More capacity does not mean longer life.";
            var tires = Variant(catalog, spec, PartCategory.Tires, "Touring", "endurance touring compound", 500, 0);
            tires.grip = .95f; tires.tireLifeDistanceM = 250000; tires.wearEnergyJ = 60000000;
            tires.description = "Fixed lower-friction compound with greater rolling and sliding lifetime. Pressure and fitment remain service adjustments; compound properties require replacing tires.";
            var brakes = Variant(catalog, spec, PartCategory.Brakes, "Endurance", "endurance brake kit", 1600);
            brakes.capacityScale = 1.12f; brakes.massKg = 37; brakes.heatCapacityJPerC = 20000; brakes.wearEnergyJ = 9000000000f; brakes.coolingWattsPerC = 85;
            brakes.description = "Fixed larger thermal buffer, heat rejection and friction-work life; heavier than sport brakes. Grip still limits deceleration; no heat is generated unless braking.";
            var fixedBox = Variant(catalog, spec, PartCategory.Gearbox, "ShortFixed", "short fixed final-drive sport gearbox", 1900);
            fixedBox.tuning = PartTuningOptions.None; fixedBox.fixedFinalDrive = Math.Min(6, spec.finalDrive * 1.15f);
            fixedBox.description = "Fixed 15% shorter final-drive gearing (capped at 6:1). Higher wheel torque and lower speed per RPM; ratios cannot be adjusted without replacing the gearset.";
            var adjustableBox = Variant(catalog, spec, PartCategory.Gearbox, "Interchangeable", "sport gearbox with interchangeable final-drive set", 2700);
            adjustableBox.tuning = PartTuningOptions.FinalDrive; adjustableBox.finalDriveMin = 2.5f; adjustableBox.finalDriveMax = 5.5f;
            adjustableBox.description = "Included interchangeable final-drive gearsets unlock ratio 2.50–5.50. Selection is a garage hardware setup, not a real-time ratio change; Apply the draft before racing.";
        }
    }
}

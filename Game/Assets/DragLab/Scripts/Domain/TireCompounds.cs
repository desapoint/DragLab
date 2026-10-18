using System;
namespace DragLab.Domain.Mechanics
{
    // Fictional drag-game calibration, not road-mileage or manufacturer claims.
    // All offers retain ordinary fitment, per-corner ownership and seeded supply.
    public static class TireCompounds
    {
        static void Add(MechanicalCatalog catalog,VehicleSpec spec,string key,string name,int price,int stage,float grip,float distance,float sliding,float optimum,string tradeoff)
        {
            var p=catalog.Find(catalog.Id(spec.id,PartCategory.Tires,stage)).Copy();
            p.id=spec.id+"/Tires/"+key;p.name=name;p.quality=name;p.price=price;p.stage=stage;
            p.grip=grip;p.tireLifeDistanceM=distance;p.wearEnergyJ=sliding;p.tireOptimalC=optimum;
            p.heatCapacityJPerC=8000;p.coolingWattsPerC=30;p.thermalLimitC=170;p.tireHeatFraction=.4f;
            if(key=="StreetSemiSlick")p.thermalLimitC=135;
            p.tuning=PartTuningOptions.None;
            p.description=tradeoff+" Compound grip and life are fixed hardware properties. Pressure and front/rear size remain adjustable through the existing wheel setup. More power can still exceed traction; warm-up, load, differential, wheel size and throttle matter. Each tire retains its own wear and can be replaced individually.";
            catalog.parts.Add(p);
        }
        public static void Add(MechanicalCatalog catalog,VehicleSpec spec)
        {
            Add(catalog,spec,"StreetSemiSlick","street semi-slick compound",1900,2,1.55f,50000,8000000,50,"Strong cold traction and longer rolling life, but less sliding-work and heat tolerance than a club slick; sustained burnouts consume it quickly.");
            Add(catalog,spec,"EnduranceRadial","endurance drag radial compound",2400,2,1.75f,40000,16000000,65,"A durable drag radial: less peak grip than the softer slicks, with greater rolling and sliding-work life.");
            Add(catalog,spec,"ProRadial","pro drag radial compound",2800,3,2.1f,32000,10000000,75,"More traction for boosted road-based builds. Clean passes target roughly 40–60 runs at Full wear; wheelspin and abuse consume extra life.");
            Add(catalog,spec,"SlickMedium","medium competition slick compound",3400,3,2.45f,30000,8000000,85,"High-output drag traction with a balanced race lifespan. Clean passes target roughly 40–60 runs at Full wear; sustained spinning wears it faster.");
            Add(catalog,spec,"SlickSoft","soft competition slick compound",4100,3,2.9f,27000,5000000,95,"Higher grip with less life and greater sensitivity to sliding abuse. A softer compound is not a durability upgrade.");
            Add(catalog,spec,"QualifyingSlick","qualifying super-soft slick compound",5200,3,3.35f,16000,3000000,100,"Maximum available compound grip for extreme builds. Short-life qualifying choice; expect fewer clean passes than the normal 40–60-run performance target and much faster wear in burnouts.");
        }
    }
}

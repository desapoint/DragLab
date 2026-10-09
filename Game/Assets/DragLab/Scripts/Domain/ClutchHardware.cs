using System;
using System.Linq;
namespace DragLab.Domain.Mechanics
{
    public enum ClutchEnvironment { Dry, Wet }
    public enum ClutchMaterial { Organic, Ceramic, SinteredMetal }
    public enum ClutchActuation { Conventional, Centrifugal }
    public static class ClutchHardware
    {
        public static void Ensure(PartDefinition d,ComponentCondition c){if(d.category==PartCategory.Clutch&&d.clutchEnvironment==ClutchEnvironment.Wet&&c.clutchFluidVersion==0){c.clutchOilLitres=d.clutchOilCapacityLitres;c.clutchOilCondition=1;c.clutchFluidVersion=1;}}
        public static void Add(MechanicalCatalog catalog,VehicleSpec spec)
        {
            foreach(var p in catalog.parts.Where(p=>p.category==PartCategory.Clutch&&p.id.StartsWith(spec.id+"/"))){p.clutchPlateCount=1;if(spec.transmission.type==TransmissionType.DCT&&p.stage==0){p.clutchEnvironment=ClutchEnvironment.Wet;p.clutchPlateCount=6;p.clutchOilCapacityLitres=2;p.coolingWattsPerC=65;p.thermalLimitC=180;p.heatCapacityJPerC=16000;}if(p.id.EndsWith("RaceFriction"))p.clutchMaterial=ClutchMaterial.Ceramic;p.name+=" / "+p.clutchEnvironment.ToString().ToLower()+" "+p.clutchPlateCount+"-plate "+PartSemantics.Friendly(p.clutchMaterial.ToString());p.description+=" Environment: "+p.clutchEnvironment+" / lining: "+p.clutchMaterial+" / plate count: "+p.clutchPlateCount+". Wet packs use a separate clutch-oil bath; materials alter engagement and sliding-work life.";}
            AddVariant(catalog,spec,"DryTwinOrganic",ClutchEnvironment.Dry,ClutchMaterial.Organic,2,1.8f,1200);
            AddVariant(catalog,spec,"DryTwinCeramic",ClutchEnvironment.Dry,ClutchMaterial.Ceramic,2,2.2f,1600);
            AddVariant(catalog,spec,"DryTripleSintered",ClutchEnvironment.Dry,ClutchMaterial.SinteredMetal,3,2.8f,2400);
            AddVariant(catalog,spec,"WetMultiOrganic",ClutchEnvironment.Wet,ClutchMaterial.Organic,6,1.8f,1900);
            AddVariant(catalog,spec,"WetMultiRace",ClutchEnvironment.Wet,ClutchMaterial.SinteredMetal,8,2.5f,2800);
            var centrifugal=AddVariant(catalog,spec,"DryCentrifugal",ClutchEnvironment.Dry,ClutchMaterial.Ceramic,2,2.1f,2100);centrifugal.clutchActuation=ClutchActuation.Centrifugal;centrifugal.clutchEngagementRpm=1800;centrifugal.description+=" RPM-actuated clamp begins at 1800 RPM and ramps over 1200 RPM. Suitable for drag launch testing; shift disengagement remains available.";
        }
        static PartDefinition AddVariant(MechanicalCatalog catalog,VehicleSpec spec,string key,ClutchEnvironment environment,ClutchMaterial material,int plates,float capacity,int price)
        {
            var p=catalog.Find(catalog.Id(spec.id,PartCategory.Clutch,1)).Copy();p.id=spec.id+"/Clutch/"+key;p.name=spec.displayName+" / "+environment.ToString().ToLower()+" "+plates+"-plate "+PartSemantics.Friendly(material.ToString())+" clutch";p.clutchEnvironment=environment;p.clutchMaterial=material;p.clutchPlateCount=plates;p.price=price;p.tuning=PartTuningOptions.None;p.capacityScale=capacity;p.massKg=8+plates*.5f;
            bool wet=environment==ClutchEnvironment.Wet;p.clutchOilCapacityLitres=wet?2:0;p.coolingWattsPerC=wet?85:10;p.heatCapacityJPerC=wet?18000:12000;p.thermalLimitC=wet?180:material==ClutchMaterial.Organic?260:380;p.wearEnergyJ=material==ClutchMaterial.Organic?85000000:material==ClutchMaterial.Ceramic?50000000:35000000;
            p.description="Matched "+environment.ToString().ToLower()+" clutch assembly with "+plates+" friction plates; "+PartSemantics.Friendly(material.ToString())+" lining. "+(wet?"Includes dedicated oil bath/pump/housing conversion. Better heat rejection; oil drag, fluid level and condition affect coupling. Clutch oil is separate from engine oil.":"Air-cooled friction surfaces; no clutch oil bath. ")+(material==ClutchMaterial.Organic?"Progressive engagement and greater sliding-work life.":"Sharper engagement and higher heat tolerance; shorter sliding-work life.")+" Manual/DCT friction coupling only; converter/CVT behavior is unchanged. Fixed hardware; no tuning unlock.";catalog.parts.Add(p);return p;
        }
        public static double OilFactor(PartDefinition p,ComponentCondition c){return p.clutchEnvironment==ClutchEnvironment.Dry?1:DragMath.Clamp(c.clutchOilLitres/Math.Max(.1,p.clutchOilCapacityLitres),0,1);}
        public static double Capacity(PartDefinition p,ComponentCondition c){return p.clutchEnvironment==ClutchEnvironment.Dry?1:(.25+.75*OilFactor(p,c))*(.65+.35*c.clutchOilCondition);}
        public static double Engagement(PartDefinition p,double pedal,double rpm){double engagement=DragMath.Clamp((.85-pedal)/.55,0,1);engagement=Math.Pow(engagement,p.clutchMaterial==ClutchMaterial.Organic?1:p.clutchMaterial==ClutchMaterial.Ceramic?.7:.5);if(p.clutchActuation==ClutchActuation.Centrifugal)engagement*=DragMath.Clamp((rpm-p.clutchEngagementRpm)/1200,0,1);return engagement;}
        public static double Drag(PartDefinition p,ComponentCondition c,double mismatch){return p.clutchEnvironment==ClutchEnvironment.Wet?Math.Min(5,Math.Abs(mismatch)*.0008)*OilFactor(p,c):0;}
    }
}

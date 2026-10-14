using System;
using System.Collections.Generic;
using System.Linq;
namespace DragLab.Domain.Mechanics
{
    public sealed class PartBrandProfile
    {
        public readonly string name,focus,description;
        public PartBrandProfile(string name,string focus,string description){this.name=name;this.focus=focus;this.description=description;}
    }
    public static class PartBrands
    {
        static readonly PartBrandProfile[] profiles={
            new PartBrandProfile("Atlas OE","OEM","Factory-spec replacements. A factory part can already be forged or high performance; OEM does not mean weak."),
            new PartBrandProfile("Relay Components","OEM+","Replacement and long-service parts for stock-style builds. Check the actual ratings: an inexpensive cast replacement is not a forged upgrade."),
            new PartBrandProfile("Redline Engineering","Street","Mild road upgrades, with modest changes to everyday builds. Check supporting parts before fitting."),
            new PartBrandProfile("IronTrail","Street","Road and work-vehicle upgrades for pickups and SUVs."),
            new PartBrandProfile("Trackline","Street","Road-focused tire upgrades. More grip does not guarantee longer life."),
            new PartBrandProfile("Stratos Performance","Tuning","More substantial performance changes that may need supporting upgrades."),
            new PartBrandProfile("Vortex Dynamics","Tuning","Turbocharger, supercharger and induction conversions. Check boost response, cooling and strength."),
            new PartBrandProfile("IonWorks","Tuning","Fueling, ignition and electronic controls. Only the installed part's listed adjustments are unlocked."),
            new PartBrandProfile("LobeWorks","Tuning","Camshaft and valvetrain upgrades. Lift, springs and companion hardware must suit each other."),
            new PartBrandProfile("IronTrail Diesel","Tuning","Diesel engine and fueling upgrades. Diesel hardware and compatibility remain distinct."),
            new PartBrandProfile("Trackline Circuit","Tuning","Track-oriented tires. Check temperature targets, grip and lifespan together."),
            new PartBrandProfile("Apex Forge","Competition","High-strength forged engine internals. Extra strength is headroom, not an automatic horsepower increase."),
            new PartBrandProfile("Vector Motorsport","Competition","Specialist center systems, ratio gearsets and sequential gearboxes. Adjustments depend on the actual hardware."),
            new PartBrandProfile("Trackline Racing","Competition","Race-focused tires. Extra grip can come with heat requirements and shorter life."),
            new PartBrandProfile("Apex Competition","Competition","Competition-focused hardware for more demanding builds. Check its actual benefits and tradeoffs.")
        };
        static readonly string[] namePrefixes=profiles.Select(p=>p.name).OrderByDescending(n=>n.Length).ToArray();
        static readonly Dictionary<string,PartBrandProfile> byName=profiles.ToDictionary(p=>p.name,StringComparer.Ordinal);
        public static IReadOnlyList<PartBrandProfile> Profiles=>Array.AsReadOnly(profiles);
        public static string Focus(string brand)=>brand!=null&&byName.TryGetValue(brand,out var profile)?profile.focus:"Custom";
        public static string Description(string brand)=>brand!=null&&byName.TryGetValue(brand,out var profile)?profile.description:"Custom supplier. Check the part's actual specifications and compatibility.";
        public static string[] FocusChoices=>new[]{"OEM","OEM+","Street","Tuning","Competition"};
        static string Supplier(MechanicalCatalog catalog,PartDefinition p)
        {
            string variant=p.id.Substring(p.id.LastIndexOf('/')+1);var spec=catalog.Spec(p.id.Split('/')[0]);
            if(variant=="0")return "Atlas OE";
            if(variant=="cast"||variant=="ServiceLife")return "Relay Components";
            if(p.awdHardware!=null&&(p.awdHardware.integratedCenter||p.awdHardware.adjustments!=AwdAdjustments.None||(p.awdHardware.modes&(p.awdHardware.modes-1))!=0)||p.manualLayout==ManualGearboxLayout.Sequential)return "Vector Motorsport";
            if(p.category==PartCategory.Tires)return p.stage>=3?"Trackline Racing":p.stage>=2?"Trackline Circuit":"Trackline";
            if(p.stage>=3)return "Apex Competition";
            if(p.category==PartCategory.Pistons||p.category==PartCategory.Rods||p.category==PartCategory.Crankshaft)return "Apex Forge";
            if(p.category==PartCategory.Induction)return "Vortex Dynamics";
            if(spec.combustion.fuelKind==FuelKind.Diesel&&MechanicalCatalog.EngineSlot(p.category))return "IronTrail Diesel";
            if(p.category==PartCategory.FuelSystem||p.category==PartCategory.Controls||p.category==PartCategory.Ignition||p.category==PartCategory.SparkPlugs)return "IonWorks";
            if(p.category==PartCategory.Camshaft||p.category==PartCategory.ValveSprings||p.category==PartCategory.Followers)return "LobeWorks";
            if(p.stage>=2||p.stage==0)return "Stratos Performance";
            return spec.bodyType==CarBodyType.Pickup||spec.bodyType==CarBodyType.SUV?"IronTrail":"Redline Engineering";
        }
        public static void Apply(MechanicalCatalog catalog)
        {
            foreach(var p in catalog.parts){
                var spec=catalog.Spec(p.id.Split('/')[0]);string brand=Supplier(catalog,p);
                string label=(p.name??p.category.ToString()).Replace(spec.displayName+" / ","").Replace(spec.displayName+"/","").Trim();
                foreach(string old in namePrefixes)if(label.StartsWith(old+" ",StringComparison.Ordinal)){label=label.Substring(old.Length+1);break;}
                label=label.Replace(" / OEM"," Touring").Replace(" / option 1"," Street").Replace(" / option 2"," Sport").Replace(" / option 3"," Competition").Replace("factory ","Touring ");
                p.brand=brand;p.name=brand+" "+label;
                string description=p.description??"Fictional compatible replacement hardware.";int supplier=description.IndexOf(" Supplier: ",StringComparison.Ordinal);if(supplier>=0)description=description.Substring(0,supplier);
                p.description=description+" Supplier: "+brand+". Fitment: "+p.fitment+"; compatibility and matched companion hardware still apply.";
            }
        }
    }
}

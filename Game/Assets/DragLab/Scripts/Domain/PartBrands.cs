using System;
using System.Linq;
namespace DragLab.Domain.Mechanics
{
    public static class PartBrands
    {
        public static void Apply(MechanicalCatalog catalog)
        {
            foreach(var p in catalog.parts) {
                var spec=catalog.Spec(p.id.Split('/')[0]);
                string brand=p.stage==0&&p.quality=="OEM"?"Atlas OE":p.category==PartCategory.Pistons||p.category==PartCategory.Rods||p.category==PartCategory.Crankshaft?"Apex Forge":p.category==PartCategory.Camshaft||p.category==PartCategory.ValveSprings||p.category==PartCategory.Followers?"LobeWorks":p.category==PartCategory.Induction?"Vortex Dynamics":p.category==PartCategory.Tires?"Trackline":p.category==PartCategory.FuelSystem||p.category==PartCategory.Controls||p.category==PartCategory.Ignition||p.category==PartCategory.SparkPlugs?"IonWorks":spec.bodyType==CarBodyType.Pickup||spec.bodyType==CarBodyType.SUV?"IronTrail":"Redline Engineering";
                p.brand=brand;
                string label=(p.name??p.category.ToString()).Replace(spec.displayName+" / ","").Replace(spec.displayName+"/","");
                label=label.Replace(" / OEM", " Touring").Replace(" / option 1", " Street").Replace(" / option 2", " Sport").Replace(" / option 3", " Competition").Replace("factory ", "Touring ");
                p.name=brand+" "+label;
                if(p.description==null)p.description="Fictional compatible replacement hardware.";
                p.description+=" Supplier: "+brand+". Fitment: "+p.fitment+"; compatibility and matched companion hardware still apply.";
            }
        }
    }
}

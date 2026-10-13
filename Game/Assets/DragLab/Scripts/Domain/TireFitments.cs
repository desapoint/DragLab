using System;
using System.Collections.Generic;
using System.Linq;
using DragLab.Domain.Mechanics;

namespace DragLab.Domain
{
    public static class TireFitments
    {
        public static string DefinitionId(PartInstance pack,int corner)=>string.IsNullOrEmpty(pack.condition.tires[corner].definitionId)?pack.definitionId:pack.condition.tires[corner].definitionId;
        public static void Capture(VehicleSpec spec,IEnumerable<PartInstance> parts,MechanicalCatalog catalog)
        {
            var pack=parts.FirstOrDefault(p=>p.slot==PartCategory.Tires.ToString());spec.cornerTireDefinitions=new PartDefinition[0];if(pack==null)return;
            // Empty old condition arrays retain the pack's authored definition; never create player assets here.
            if(pack.condition.tires==null||pack.condition.tires.Length==0)return;
            spec.cornerTireDefinitions=Enumerable.Range(0,4).Select(i=>{
                var d=catalog.Find(DefinitionId(pack,i));
                if(d==null||d.category!=PartCategory.Tires||!catalog.Fits(spec,d))throw new InvalidOperationException("Incompatible fitted tire at corner "+(i+1));
                return d.Copy();
            }).ToArray();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using DragLab.Domain.Mechanics;

namespace DragLab.Domain
{
    // Detached facts for one concrete installed build. Catalog is deliberately supplied
    // per evaluation and is never retained by this value.
    internal sealed class ConcreteInstalledBuildInput
    {
        const string MissingEngineResolve="No engine attached. Attach a stored engine in Assemblies.";
        const string RebuildResolve="Engine is reserved at the rebuild shop. Collect it after the quoted time.";
        const string GarageJobResolve="Car and installed components are reserved by a garage job. Claim or cancel to release.";
        const string MissingEngineInspection="No engine attached: stored engines retain their own hardware and liquids.";

        readonly string carId,vehicleId,engineAssemblyId,garageJobId;
        readonly bool rebuildReserved,hasFuel;
        readonly double fuelLitres;
        readonly Tune tune;
        readonly Dictionary<PartCategory,string> definitions;
        readonly InstalledBuildComponent[] components;
        readonly FluidState fluids;

        internal int BindingCount=>components.Length;
        internal int ResolvedPartCount=>components.Count(component=>component.Part!=null);
        internal int MissingBindingCount=>components.Length-ResolvedPartCount;

        ConcreteInstalledBuildInput(string carId,string vehicleId,string engineAssemblyId,string garageJobId,
            bool rebuildReserved,Tune tune,Dictionary<PartCategory,string> definitions,
            InstalledBuildComponent[] components,bool hasFuel,double fuelLitres,FluidState fluids)
        {
            this.carId=carId;this.vehicleId=vehicleId;this.engineAssemblyId=engineAssemblyId;this.garageJobId=garageJobId;
            this.rebuildReserved=rebuildReserved;this.tune=tune;this.definitions=definitions;this.components=components;
            this.hasFuel=hasFuel;this.fuelLitres=fuelLitres;this.fluids=fluids;
        }

        internal static ConcreteInstalledBuildInput Capture(PlayerProfile profile,OwnedCar car)
        {
            Dictionary<PartCategory,string> definitions;
            InstalledBuildComponent[] components;
            using(var installed=InstalledPartLookup.Capture(profile.parts,car.installed.slots))
            {
                definitions=installed.CopyDefinitions();
                components=new InstalledBuildComponent[installed.Count];
                for(int i=0;i<installed.Count;i++)
                {
                    var entry=installed.EntryAt(i);
                    string bindingId=car.installed.slots[i].partInstanceId;
                    components[i]=new InstalledBuildComponent(entry.Slot,entry.Part?.Copy(),bindingId);
                }
            }
            return new ConcreteInstalledBuildInput(car.instanceId,car.vehicleId,car.engineAssemblyId,car.garageJobId,
                car.engineRebuild!=null,car.tune?.Copy(),definitions,components,car.fuel!=null,
                car.fuel==null?0:car.fuel.litres,car.fluids?.Copy());
        }

        internal static ConcreteInstalledBuildInput FromTransitionFacts(string carId,string vehicleId,string engineAssemblyId,
            string garageJobId,bool rebuildReserved,Tune tune,IDictionary<PartCategory,string> definitions,
            IEnumerable<InstalledBuildComponent> components,FuelState fuel,FluidState fluids)
        {
            var ownedComponents=(components??Enumerable.Empty<InstalledBuildComponent>())
                .Select(component=>new InstalledBuildComponent(component.Slot,component.Part?.Copy(),component.BindingId)).ToArray();
            return new ConcreteInstalledBuildInput(carId,vehicleId,engineAssemblyId,garageJobId,rebuildReserved,tune?.Copy(),
                new Dictionary<PartCategory,string>(definitions),ownedComponents,fuel!=null,fuel==null?0:fuel.litres,fluids?.Copy());
        }

        internal BuildResolution Resolve(MechanicalCatalog catalog)
        {
            return InstalledBuildResolution.Resolve(catalog,vehicleId,new Dictionary<PartCategory,string>(definitions),tune?.Copy(),
                components, !string.IsNullOrEmpty(engineAssemblyId),rebuildReserved,!string.IsNullOrEmpty(garageJobId),
                MissingEngineResolve,RebuildResolve,GarageJobResolve,false);
        }

        internal InspectionBuild Inspection(MechanicalCatalog catalog)
        {
            var ownedComponents=components.Where(component=>component.Part!=null)
                .Select(component=>new InstalledBuildComponent(component.Part.slot,component.Part.Copy())).ToArray();
            var result=InstalledBuildResolution.Resolve(catalog,vehicleId,new Dictionary<PartCategory,string>(definitions),tune?.Copy(),
                ownedComponents,!string.IsNullOrEmpty(engineAssemblyId),false,false,
                MissingEngineInspection,null,null,true);
            if(fluids==null)throw new InvalidOperationException("Inspection requires captured fluid state.");
            if(!hasFuel)throw new InvalidOperationException("Inspection requires captured fuel state.");
            var startingFluids=fluids.Copy();
            var startingFuel=new FuelState{litres=fuelLitres};
            return new InspectionBuild
            {
                build=result,
                components=ownedComponents.Select(component=>component.Part).ToList(),
                startingFluids=startingFluids,
                startingFuel=startingFuel
            };
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;
using DragLab.Runtime;

namespace DragLab.Editor
{
    public static class ComponentFaultVerification
    {
        static ElementFault[] Expected(PartCategory category)
        {
            switch(category){
                case PartCategory.Tires:return new[]{ElementFault.None};
                case PartCategory.Pistons:return new[]{ElementFault.None,ElementFault.Disabled,ElementFault.CompressionLeak,ElementFault.Seized};
                case PartCategory.Rods:case PartCategory.Bearings:return new[]{ElementFault.None,ElementFault.Disabled,ElementFault.Seized};
                case PartCategory.PistonRings:return new[]{ElementFault.None,ElementFault.Disabled,ElementFault.CompressionLeak};
                case PartCategory.Valves:return new[]{ElementFault.None,ElementFault.Disabled,ElementFault.CompressionLeak,ElementFault.StuckOpen,ElementFault.DroppedValve};
                case PartCategory.ValveSprings:return new[]{ElementFault.None,ElementFault.Disabled,ElementFault.WeakSpring};
                case PartCategory.Wheels:case PartCategory.ValveSeals:case PartCategory.Followers:case PartCategory.Ignition:case PartCategory.SparkPlugs:case PartCategory.GlowPlugs:case PartCategory.FuelSystem:return new[]{ElementFault.None,ElementFault.Disabled};
                default:return Array.Empty<ElementFault>();
            }
        }
        static EngineElement Member(PartCategory category)
        {
            var member=EngineElements.Create(category);member.id="fault-matrix/"+category;member.role=category==PartCategory.Tires?"tire":category==PartCategory.Wheels?"wheel":"test member";member.cylinder=0;member.valve=-1;return member;
        }
        public static void Run()
        {
            var report=new StringBuilder();int checks=0;
            void Check(bool ok,string label){checks++;if(!ok)throw new InvalidOperationException(label);report.AppendLine("PASS "+label);}
            var categories=Enum.GetValues(typeof(PartCategory)).Cast<PartCategory>().ToArray();
            var faults=Enum.GetValues(typeof(ElementFault)).Cast<ElementFault>().Concat(new[]{(ElementFault)(-1),(ElementFault)7,(ElementFault)999}).ToArray();
            var bench=typeof(PhysicalDrivingBench).GetMethod("IndividualFaults",BindingFlags.Static|BindingFlags.NonPublic);
            foreach(var category in categories){
                var expected=Expected(category);var member=Member(category);
                Check(EngineElements.Faults(category).SequenceEqual(expected),category+" exposes only its physically supported faults");
                Check(((ElementFault[])bench.Invoke(null,new object[]{category})).SequenceEqual(expected),category+" bench choices use the same authoritative permissions");
                foreach(var fault in faults){member.fault=fault;bool allowed=expected.Contains(fault);
                    Check(EngineElements.FaultAllowed(category,member)==allowed&&(member.Validate()==null)==allowed,category+" / "+fault+" is consistently accepted or rejected at the member and category boundaries");
                }
                member.fault=ElementFault.None;
                foreach(var other in categories.Where(c=>c!=category))Check(!EngineElements.FaultAllowed(other,member),category+" member cannot enter "+other+" under another fault policy");
            }
            var failedTire=new TireCondition{id="real-tire-failure",failed=true,failureReason="Puncture",health=.8f};
            Check(failedTire.Validate()==null&&failedTire.Failed&&EngineElements.FaultAllowed(PartCategory.Tires,failedTire),"Real tire failure remains valid through its own failure flag");
            var catalog=MechanicsTransactions.Catalog();var vehicles=Resources.LoadAll<VehicleDefinition>("Data/Vehicles");
            foreach(var vehicle in vehicles){
                var p=PlayerProfile.New(DateTime.UtcNow.Ticks);p.SelectedCar.vehicleId=p.selectedVehicleId=vehicle.spec.id;MechanicsOwnership.Ensure(p,catalog);p.cash=1000000;p.reputation=1000;
                Check(MechanicsOwnership.Validate(p,catalog)==null,vehicle.spec.id+" stock profile and both markets contain valid typed faults");
                foreach(var part in p.parts.Where(i=>i.condition.elements.Length>0)){
                    var category=catalog.Find(part.definitionId).category;var member=part.condition.elements[0];var original=member.fault;
                    member.fault=faults.First(f=>Enum.IsDefined(typeof(ElementFault),f)&&!Expected(category).Contains(f));
                    Check(MechanicsOwnership.Validate(p,catalog)!=null,vehicle.spec.id+" rejects an inappropriate fault on owned "+category);member.fault=original;
                }
                var tirePack=p.parts.Single(i=>catalog.Find(i.definitionId).category==PartCategory.Tires);var tire=tirePack.condition.tires[0];tire.fault=ElementFault.Disabled;string invalid=JsonUtility.ToJson(p);
                Check(!MechanicsTransactions.Try(p,catalog,p.revision,q=>q.cash--,q=>true,out _,out _)&&invalid==JsonUtility.ToJson(p),vehicle.spec.id+" an invalid tire fault cannot be paid/saved or silently healed by normalization");tire.fault=ElementFault.None;
                var piston=p.parts.Single(i=>i.ownerId==p.SelectedCar.engineAssemblyId&&catalog.Find(i.definitionId).category==PartCategory.Pistons);
                var loose=piston.condition.elements[0].Copy();loose.id="loose/"+vehicle.spec.id;loose.cylinder=loose.valve=-1;loose.fault=ElementFault.WeakSpring;
                p.looseEngineElements.Add(new LooseEngineElement{definitionId=piston.definitionId,element=loose});Check(MechanicsOwnership.Validate(p,catalog)!=null,vehicle.spec.id+" invalid faults cannot hide in loose inventory");p.looseEngineElements.Clear();
                var dealer=tirePack.Copy();dealer.instanceId="dealer/"+vehicle.spec.id;dealer.location=PartLocation.Inventory;dealer.ownerId=dealer.slot="";for(int i=0;i<4;i++)dealer.condition.tires[i].id=dealer.instanceId+"/tire/"+i;dealer.condition.tires[0].fault=ElementFault.Disabled;
                var stock=new DealerStock{part=dealer};p.inventory.dealer.Add(stock);Check(MechanicsOwnership.Validate(p,catalog)!=null,vehicle.spec.id+" invalid faults cannot hide in used-part stock");p.inventory.dealer.Remove(stock);
                foreach(int seed in new[]{381,772}){VehicleListings.Initialize(p,catalog,seed);Check(MechanicsOwnership.Validate(p,catalog)==null,vehicle.spec.id+" seeded car/part/engine markets generate only valid faults / "+seed);}
                var offer=p.vehicleStock.listings.First();var offered=offer.parts.First(i=>i.condition.elements.Length>0);var saved=offered.condition.elements;offered.condition.elements=new[]{Member(catalog.Find(offered.definitionId).category==PartCategory.Pistons?PartCategory.ValveSprings:PartCategory.Pistons)};
                Check(VehicleListings.Validate(p,catalog)!=null,vehicle.spec.id+" dealer cars reject a valid fault-bearing type installed in the wrong component");offered.condition.elements=saved;
                var session=new PhysicalDrivingSession(new RunSnapshot{vehicle=vehicle.spec.Copy(),tune=vehicle.CreateStockTune(),driving=new DriverSettings()});
                session.snapshot.components.AddRange(session.car.mechanics.parts.Select(i=>i.Copy()));
                Check(PhysicalDrivingSession.Validate(session.snapshot)==null,vehicle.spec.id+" captured stock components validate before the negative replay case");
                var captured=session.snapshot.components.Single(i=>i.slot==PartCategory.Pistons.ToString());var originalMember=captured.condition.elements[0];captured.condition.elements[0]=Member(PartCategory.ValveSprings);
                Check(PhysicalDrivingSession.Validate(session.snapshot)!=null,vehicle.spec.id+" bench/replay snapshots reject cross-component member types");
                captured.condition.elements[0]=originalMember;var continued=session.ContinueRecording().snapshot;
                continued.startingDynamics.components.Single(i=>i.slot==PartCategory.Pistons.ToString()).condition.elements[0].fault=ElementFault.WeakSpring;
                Check(PhysicalDrivingSession.Validate(continued)!=null,vehicle.spec.id+" exact continuation checkpoints reject a fault belonging to another component");
            }
            report.AppendLine("Checks: "+checks+"; failures: 0");string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../reports/branch-audit"));Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"fault-types-verification.txt"),report.ToString(),new UTF8Encoding(false));Debug.Log("Component fault audit passed: "+checks);
        }
    }
}

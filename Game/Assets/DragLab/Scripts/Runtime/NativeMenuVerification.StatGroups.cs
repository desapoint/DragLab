using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Runtime
{
    internal sealed partial class NativeMenuVerification
    {
#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
        static IEnumerable<NativeStatDefinition> StatDefinitions(IEnumerable<NativeStatGroup> groups)
        {foreach(var group in groups){foreach(var row in group.Definitions)yield return row;foreach(var row in StatDefinitions(group.Children))yield return row;}}
        IEnumerator StatsClick(WindowsKeyboardProbe input,string id)
        {
            shell.Root.Q<ScrollView>("ownedDetailScroll").ScrollTo(shell.Root.Q(id));yield return new WaitForSecondsRealtime(.1f);yield return Click(input,id);
        }
        IEnumerator ViewStats(string id)
        {
            var scroll=shell.Root.Q<ScrollView>("ownedDetailScroll");
            scroll.scrollOffset=new Vector2(0,Mathf.Max(0,shell.Root.Q("statsGroup:"+id).worldBound.yMin-scroll.contentContainer.worldBound.yMin-8));
            yield return new WaitForSecondsRealtime(.1f);
        }
        IEnumerator RemainingStatsChecks(WindowsKeyboardProbe input,CarInspection inspection)
        {
            var panel=shell.Root.Q<NativeStatsPanel>("ownedCarStats");
            string[] ids={"Induction","Transmission","Tires","Weight","Cooling","Components","Performance"};
            Check(ids.All(id=>panel.Q("statsGroup:"+id)!=null&&panel.Q("statsBody:"+id).ClassListContains("hidden")),"All seven remaining stat groups start collapsed below the accepted Engine group");
            Check(panel.Q("statsBody:Components").childCount==0,"Collapsed component contents are lazy-built, without thousands of dormant widgets at details creation");
            yield return StatsClick(input,"statsGroup:Engine");
            yield return Capture("stat-groups-overview");
            foreach(string id in ids) {
                yield return StatsClick(input,"statsGroup:"+id);
                yield return ViewStats(id);
                Check(!panel.Q("statsBody:"+id).ClassListContains("hidden")&&panel.Q("statsBody:"+id).Query(className:"stat-row").ToList().Count>0,id+" opens shared read-only stat rows");
                Check(panel.Q("statsBody:"+id).Query<Button>().ToList().All(b=>b.tabIndex==-1)&&panel.Query<TextField>().ToList().Count==0,"Lazy "+id+" controls preserve no Tab traversal and introduce no editable tuning fields");
                if(id=="Induction") {
                    Check(panel.Q<Label>("statValue:boostBar").text==DisplayUnits.FormatPressure(inspection.boostBar,DragLab.Infrastructure.SharedUnitSettings.Current),"Induction boost uses authoritative settled pressure and selected units");
                    yield return Capture("stats-induction");
                }
                if(id=="Tires")Check(panel.Q<Label>("statValue:wheelRadius").text==inspection.wheelRadiusIn.ToString("F2")+" in"&&panel.Q<Label>("statValue:wheelMass").text==inspection.wheelMassKg.ToString("F2")+" kg","Tires show actual external radius in inches and computed assembly mass");
                if(id=="Weight") {Check(panel.Q<Label>("statValue:fuelMass").text==inspection.mass.fuelKg.ToString("F2")+" kg","Weight includes retained fuel without adding it twice");yield return Capture("stats-weight");}
                if(id=="Cooling")Check(panel.Q<Label>("statValue:oilLitres").text==inspection.source.startingFluids.oilLitres.ToString("F2")+" L","Cooling displays retained oil rather than fabricating a refill");
                if(id=="Performance") {
                    float deadline=Time.realtimeSinceStartup+20;
                    while(panel.Current.DrivingPending&&Time.realtimeSinceStartup<deadline)yield return null;
                    Check(panel.Current.DrivingInspection?.probeComplete==true&&panel.Current.DrivingError==null&&panel.Q<Label>("statValue:probeStatus").text!="Not run","Background copied-build driving estimate populates without a race or recording");
                    Check(panel.Q<Label>("statValue:quarterTime").text==(panel.Current.Driving.completed?panel.Current.Driving.elapsed.ToString("F2")+" s":"—")&&panel.Q<Label>("statValue:maxWheelSlip").text==(panel.Current.Driving.maxSlip*100).ToString("F0")+" %","Completed estimate uses measured time and slip, with unavailable ET for an incomplete pass");
                    var warning=panel.Q("statsBody:Performance").Query<Button>().ToList().FirstOrDefault(b=>b.name.StartsWith("statInfo:warning:"));
                    if(warning!=null) {yield return StatsClick(input,warning.name);Check(shell.ModalOpen&&shell.Root.Q("modalContent").Query<Label>().ToList().Any(l=>l.text.Contains(panel.Current.Driving.warnings[0].suggestion)),"Warning info exposes the authoritative suggestion in the shared modal");yield return Key(input,0x1B);}
                    yield return Capture("stats-performance");
                    var cached=panel.Current.DrivingInspection;
                    yield return StatsClick(input,"statsGroup:Performance");yield return StatsClick(input,"statsGroup:Performance");
                    Check(ReferenceEquals(cached,panel.Current.DrivingInspection)&&!panel.Current.DrivingPending,"Reopening the Performance group retains its completed estimate without another simulation");
                }
                yield return StatsClick(input,"statsGroup:"+id);
            }
            yield return StatsClick(input,"statsGroup:Components");yield return StatsClick(input,"statsGroup:SavedTuning");
            yield return ViewStats("SavedTuning");
            Check(panel.Q<Label>("statValue:launchTarget").text==inspection.source.build.tune.launchRpm.ToString("F0")+" RPM","Saved tuning shows applied targets separately from component service condition");
            yield return Capture("stats-saved-tuning");yield return StatsClick(input,"statsGroup:SavedTuning");
            yield return StatsClick(input,"statsGroup:part:Pistons");
            var piston=panel.Current.Part(PartCategory.Pistons);
            Check(panel.Q<Label>("statValue:part:Pistons:hardware").text==panel.Current.Definition(PartCategory.Pistons).name&&panel.Q<Label>("statValue:part:Pistons:health").text==(piston.condition.health*100).ToString("F0")+" %","Component details use exact installed hardware and retained wear");
            var individual=panel.Q("statsBody:part:Pistons").Query<Button>().ToList().First(b=>b.name.StartsWith("statsGroup:part:Pistons:element:"));
            yield return StatsClick(input,individual.name);
            yield return ViewStats(individual.name.Substring("statsGroup:".Length));
            var memberBody=panel.Q("statsBody:"+individual.name.Substring("statsGroup:".Length));string memberPrefix=individual.name.Substring("statsGroup:".Length);
            Check(new[]{"health","fault","severity","spec"}.All(field=>memberBody.Q("stat:"+memberPrefix+":"+field)!=null),"Individual piston details expose condition, fault, severity and actual specification without replacing the whole set");
            yield return Capture("stats-component-elements");yield return StatsClick(input,"statInfo:part:Pistons:hardware");
            Check(shell.ModalOpen&&shell.Root.Q("modalContent").Query<Label>().ToList().Any(l=>l.text.Contains(piston.instanceId)),"Component info retains the owned instance identity and definition description");yield return Key(input,0x1B);
            yield return StatsClick(input,"statsGroup:Components");
            yield return StatsClick(input,"statsGroup:Weight");yield return Click(input,"settingsButton");yield return Click(input,"cancelSettings");
            Check(!shell.Root.Q("statsBody:Weight").ClassListContains("hidden")&&shell.Root.Q("statsBody:Components").ClassListContains("hidden"),"Settings return retains independent group collapse state");
            yield return StatsClick(input,"statsGroup:Weight");yield return StatsClick(input,"statsGroup:Engine");
            shell.Root.Q<ScrollView>("ownedDetailScroll").scrollOffset=Vector2.zero;
            var units=new UnitPreferences {pressure=PressureUnit.Psi,speed=SpeedUnit.MilesPerHour,torque=TorqueUnit.PoundFeet};
            var catalog=NativeStatCatalog.Groups(inspection);var definitions=StatDefinitions(catalog).ToArray();
            var snapshot=panel.Current;
            Check(definitions.Select(d=>d.Id).Distinct().Count()==definitions.Length,"Nested stat definitions have unique comparison identities");
            Check(definitions.First(d=>d.Id=="boostBar").Value(snapshot,units)==DisplayUnits.Pressure(inspection.boostBar,PressureUnit.Psi).ToString("F2")+" psi"&&definitions.First(d=>d.Id=="gearSpeed:0").Value(snapshot,units).Contains("mph"),"Shared definitions convert pressure and gearing speed without changing reference values");
            foreach(var car in game.Profile.cars) {
                var source=InspectionBuild.Capture(game.Profile,car);var stats=CarInspection.CalculatePrepared(source,CarInspection.PrepareCar(source),false);var state=new NativeStatsSnapshot(stats);
                var rows=StatDefinitions(NativeStatCatalog.Groups(stats)).ToArray();
                Check(rows.All(row=>!string.IsNullOrEmpty(row.Value(state,units))&&(row.Number==null||!row.Number(state).HasValue||MechanicsSchema.Finite(row.Number(state).Value))),"All stat groups have finite or explicitly unavailable values: "+car.vehicleId);
            }
            foreach(TransmissionUpgrade type in new[] {TransmissionUpgrade.Manual,TransmissionUpgrade.DCT,TransmissionUpgrade.Automatic,TransmissionUpgrade.CVT}) {
                var source=InspectionBuild.Capture(game.Profile,game.Profile.cars.First(c=>!string.IsNullOrEmpty(c.engineAssemblyId)));source.build.tune.transmissionUpgrade=type;
                var stats=CarInspection.CalculatePrepared(source,CarInspection.PrepareCar(source),false);var state=new NativeStatsSnapshot(stats);var rows=StatDefinitions(NativeStatCatalog.Groups(stats)).ToDictionary(d=>d.Id);
                bool friction=type==TransmissionUpgrade.Manual||type==TransmissionUpgrade.DCT;
                Check(rows["clutchCapacity"].Number(state).HasValue==friction&&rows["converterStall"].Number(state).HasValue==!friction&&rows["cvtMinimum"].Number(state).HasValue==(type==TransmissionUpgrade.CVT),"Coupling/CVT fields respect actual transmission applicability: "+type);
                if(type==TransmissionUpgrade.CVT)Check(!rows.ContainsKey("gearSpeed:0")&&rows.ContainsKey("cvtSpeed:0")&&rows.ContainsKey("cvtSpeed:1"),"CVT gearing shows two ratio endpoints without invented numbered gears");
            }
            var missingSource=InspectionBuild.Capture(game.Profile,game.Profile.cars.First(c=>!string.IsNullOrEmpty(c.engineAssemblyId)),remove:PartCategory.Tires);
            var missing=CarInspection.CalculatePrepared(missingSource,CarInspection.PrepareCar(missingSource),false);var missingState=new NativeStatsSnapshot(missing);var missingRows=StatDefinitions(NativeStatCatalog.Groups(inspection,missing)).ToDictionary(d=>d.Id);
            Check(missingRows["tireHealth"].Value(missingState,units)=="—"&&missingRows["part:Tires:hardware"].Value(missingState,units)=="Not installed","Comparison union shows removed hardware without inventing condition or OEM replacement");
            var degraded=InspectionBuild.Capture(game.Profile,game.Profile.cars.First(c=>!string.IsNullOrEmpty(c.engineAssemblyId)));var radiator=degraded.components.First(p=>p.slot==PartCategory.Cooling.ToString());radiator.condition.failed=true;
            var worn=CarInspection.CalculatePrepared(degraded,CarInspection.PrepareCar(degraded),false);var wornState=new NativeStatsSnapshot(worn);var wornRows=StatDefinitions(NativeStatCatalog.Groups(worn)).ToDictionary(d=>d.Id);
            Check(wornRows["radiatorRejection"].Number(wornState)==0&&wornRows["part:Cooling:status"].Value(wornState,units)=="Failed","Failed radiator shows zero effective heat rejection and its own failed state");
            Check(!wornRows.ContainsKey("part:Pistons:deposits")&&!wornRows.ContainsKey("part:Interior:rating:torque")&&wornRows["part:Interior:health"].Number(wornState)==null,"Components omit unrelated deposits/strength and non-wearing interior condition");
            foreach(AwdMode mode in Enum.GetValues(typeof(AwdMode))) {
                var source=InspectionBuild.Capture(game.Profile,game.Profile.cars.First(c=>game.Profile.catalog.Spec(c.vehicleId).drive==DriveType.AWD));
                source.build.tune.awdConfigured=true;source.build.tune.awd=new AwdSpec {mode=mode,frontBias=.35f};
                // This tests row applicability, so give the copied fixture matching hardware.
                // A tuning request can no longer turn an installed center into another system.
                source.build.spec.fittedParts=source.build.spec.fittedParts.Select(p=>p.Copy()).ToArray();
                var center=AwdHardware.Installed(source.build.spec);
                center.awdHardware=new AwdHardwareSpec {modes=1<<(int)mode,calibration=source.build.tune.awd.Copy()};
                var stats=CarInspection.CalculatePrepared(source,CarInspection.PrepareCar(source),false);var state=new NativeStatsSnapshot(stats);var rows=StatDefinitions(NativeStatCatalog.Groups(stats)).ToDictionary(d=>d.Id);
                Check(Math.Abs(rows["awdFrontBias"].Number(state).Value-35)<.001&&rows["awdSecondary"].Number(state).HasValue==(mode==AwdMode.OnDemand)&&rows["centerBias"].Number(state).HasValue==(mode==AwdMode.LimitedSlipCenter)&&rows["awdMinimumFront"].Number(state).HasValue==(mode==AwdMode.ActiveBias),"AWD rows honor configured topology and applicability: "+mode);
            }
            var faultSource=InspectionBuild.Capture(game.Profile,game.Profile.cars.First(c=>!string.IsNullOrEmpty(c.engineAssemblyId)));
            var pistonPart=faultSource.components.First(p=>p.slot==PartCategory.Pistons.ToString());
            EngineElements.Ensure(faultSource.build.spec,game.Profile.catalog.Find(pistonPart.definitionId),pistonPart);
            var faulty=pistonPart.condition.elements[0];faulty.health=.4f;faulty.fault=ElementFault.CompressionLeak;faulty.severity=.8f;
            var tirePart=faultSource.components.First(p=>p.slot==PartCategory.Tires.ToString());TireDynamics.Ensure(tirePart.condition);
            var tire=tirePart.condition.tires[2];tire.health=.15f;tire.temperatureC=62;
            var faults=CarInspection.CalculatePrepared(faultSource,CarInspection.PrepareCar(faultSource),false);var faultState=new NativeStatsSnapshot(faults);var faultRows=StatDefinitions(NativeStatCatalog.Groups(faults)).ToDictionary(d=>d.Id);
            string elementId="part:Pistons:element:"+faulty.role+":"+faulty.cylinder+":"+faulty.valve;
            Check(faultRows[elementId+":fault"].Value(faultState,units)=="Compression Leak"&&Math.Abs(faultRows[elementId+":health"].Number(faultState).Value-40)<.001&&Math.Abs(faultRows[elementId+":severity"].Number(faultState).Value-80)<.001,"Single piston compression fault retains independent health and severity");
            Check(Math.Abs(faultRows["part:Tires:tire:2:health"].Number(faultState).Value-15)<.001&&faultRows["part:Tires:tire:2:temperature"].Number(faultState)==62,"Rear-left tire details retain its own wear and stored heat");
        }
        IEnumerator StatGroupLayoutChecks(WindowsKeyboardProbe input,int width)
        {
            yield return StatsClick(input,"statsGroup:Induction");yield return ViewStats("Induction");
            var scroll=shell.Root.Q<ScrollView>("ownedDetailScroll");var bounds=scroll.contentViewport.worldBound;
            var panel=shell.Root.Q<NativeStatsPanel>("ownedCarStats");
            Check(panel.Q("statsBody:Induction").Query<Label>(className:"stat-value").ToList().All(l=>l.worldBound.xMin>=bounds.xMin-1&&l.worldBound.xMax<=bounds.xMax+1),"New unit/text rows fit the native viewport at "+width);
            yield return Capture("stat-groups-"+width);yield return StatsClick(input,"statsGroup:Induction");
            yield return StatsClick(input,"statsGroup:Components");
            if(panel.Q("statsBody:part:Pistons").ClassListContains("hidden"))yield return StatsClick(input,"statsGroup:part:Pistons");
            yield return ViewStats("part:Pistons");
            Check(!panel.Q("statsBody:part:Pistons").ClassListContains("hidden")&&panel.Q("statsBody:part:Pistons").Query<Label>(className:"stat-value").ToList().All(l=>l.worldBound.xMax<=bounds.xMax+1),"Long hardware descriptions wrap within the component grid at "+width);
            yield return Capture("stat-components-"+width);yield return StatsClick(input,"statsGroup:Components");
        }
#endif
    }
}

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Runtime
{
    /// <summary>Editor-only characterization of the dormant concrete installed-build input.</summary>
    public static partial class CartPreviewCharacterizationChecks
    {
        const uint InstalledBuildSeed=20261029u;

        public static void RunInstalledBuildProjectionContracts(Action<bool,string> check,string folder,string subset=null)
        {
            if(check==null)throw new ArgumentNullException(nameof(check));
            if(string.IsNullOrWhiteSpace(folder))throw new ArgumentException("An isolated report folder is required.",nameof(folder));
            Directory.CreateDirectory(folder);
            string scope=string.IsNullOrWhiteSpace(subset)?"all":subset;
            var log=new StringBuilder();int failures=0,assertions=0;
            Action<bool,string> record=(ok,label)=>{assertions++;if(!ok)failures++;check(ok,"installed-build/"+label);log.AppendLine("ASSERT|"+(ok?"PASS":"FAIL")+"|"+label);};
            log.AppendLine("S06D2B1H_INSTALLED_BUILD_PROJECTION");
            log.AppendLine("scope="+scope+";fixtureSeed="+InstalledBuildSeed+";nowTicks="+CartPreviewCharacterizationFixtures.FixedNowTicks);
            try
            {
                if(scope=="all"||scope=="normalized")InstalledBuildNormalized(record,log);
                if(scope=="all"||scope=="replacement")InstalledBuildReplacement(record,log);
                if(scope=="all"||scope=="tires")InstalledBuildTireAndWheel(record,log);
                if(scope=="all"||scope=="detached")InstalledBuildDetached(record,log);
                if(scope=="all"||scope=="bindings")InstalledBuildBindings(record,log);
                if(scope!="all"&&scope!="normalized"&&scope!="replacement"&&scope!="tires"&&scope!="detached"&&scope!="bindings")
                    record(false,"unknown subset="+scope);
            }
            catch(Exception error)
            {
                record(false,"runner exception="+error.GetType().FullName+" | "+error.Message);
                log.AppendLine("RUNNER_EXCEPTION|"+error.GetType().FullName+"|"+error.Message);
            }
            finally
            {
                log.AppendLine("SUMMARY|assertions="+assertions+";failures="+failures);
                File.WriteAllText(Path.Combine(folder,"S06D2B1H_INSTALLED_BUILD_PROJECTION.txt"),log.ToString(),new UTF8Encoding(false));
            }
        }

        static void InstalledBuildNormalized(Action<bool,string> check,StringBuilder log)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var profile,out var carId,InstalledBuildSeed);
            var car=profile.cars.Single(value=>value.instanceId==carId);
            string sourceBefore=SourceSnapshot(profile);
            long jsonBefore=RuntimeDataEncodingProbe.CurrentThreadJsonCallCount;
            bool entropyModeBefore=RuntimeEntropy.Capture;DateTime entropyBefore=RuntimeEntropy.UtcNow;
            var identity=RuntimeEntropy.BeginIdentityObservation(32);
            UnityEngine.Random.State randomBefore=UnityEngine.Random.state;
            ConcreteInstalledBuildInput captured;BuildResolution probeResolution;InspectionBuild probeInspection;
            try
            {
                using(RuntimeDataEncodingProbe.Measure("s06d2b1h/normalized-capture-evaluation"))
                {
                    captured=ConcreteInstalledBuildInput.Capture(profile,car);
                    probeResolution=captured.Resolve(profile.catalog);
                    probeInspection=captured.Inspection(profile.catalog);
                }
            }
            finally{identity.Dispose();}
            check(identity.Closed&&!identity.Overflowed&&identity.Count==0,"capture/resolve/inspection emit no physical identities");
            check(jsonBefore==RuntimeDataEncodingProbe.CurrentThreadJsonCallCount,"capture/resolve/inspection make no instrumented JSON calls");
            check(randomBefore.Equals(UnityEngine.Random.state),"capture/resolve/inspection preserve Unity RNG");
            bool entropyModeAfter=RuntimeEntropy.Capture;DateTime entropyAfter=RuntimeEntropy.UtcNow;
            check(entropyModeBefore==entropyModeAfter,"capture/resolve/inspection preserve RuntimeEntropy mode");
            if(entropyModeBefore)check(entropyBefore.Ticks==entropyAfter.Ticks&&entropyBefore.Kind==entropyAfter.Kind,"deterministic RuntimeEntropy clock remains exact under capture/evaluation");
            log.AppendLine("ENTROPY|normalized|capture="+entropyModeBefore+";utcBefore="+entropyBefore.Ticks+"/"+entropyBefore.Kind+";utcAfter="+entropyAfter.Ticks+"/"+entropyAfter.Kind+";same="+(entropyBefore.Ticks==entropyAfter.Ticks));
            check(probeResolution!=null&&probeInspection!=null,"capture and both evaluators return concrete outputs");
            AuditInstalledBuildSource(check);
            check(sourceBefore==SourceSnapshot(profile),"normalized capture preserves complete source graph");
            log.AppendLine("CASE|normalized|bindings="+captured.BindingCount+";resolved="+captured.ResolvedPartCount+";missing="+captured.MissingBindingCount);
            check(captured.BindingCount==car.installed.slots.Count&&captured.ResolvedPartCount+captured.MissingBindingCount==captured.BindingCount,
                "normalized copied binding/member counts are internally consistent");
            CheckForbiddenRetainedRoots(check,captured,"normalized");
            CheckInstalledParity(check,log,"normalized",profile,car,captured);
            RunInstalledProjectionCosts(check,log,profile,carId);

            var firstResolve=captured.Resolve(profile.catalog);var firstInspection=captured.Inspection(profile.catalog);
            string resolveFacts=TypedCanonical(firstResolve,new Dictionary<string,string>());
            string inspectionFacts=TypedCanonical(firstInspection,new Dictionary<string,string>());
            string sourceFacts=SourceSnapshot(profile);
            bool hadClutch=firstInspection.components.Any(part=>part?.condition?.clutch!=null);
            bool hadAwd=firstInspection.build?.tune?.awd!=null;
            MutateReturnedBuild(firstResolve);
            string deepMutations=MutateReturnedInspection(firstInspection,out bool memberMutated,out bool clutchMutated,out bool awdMutated);
            check(memberMutated,"deep output isolation probe changed an actual returned member health");
            check(clutchMutated==hadClutch,"deep output isolation probe changed clutch payload when present; fieldsPresent="+hadClutch);
            check(awdMutated==hadAwd,"deep output isolation probe changed nested AWD tune when present; fieldsPresent="+hadAwd);
            log.AppendLine("OUTPUT_MUTATION|member="+memberMutated+";clutch="+clutchMutated+"/present="+hadClutch+";awd="+awdMutated+"/present="+hadAwd+";paths="+deepMutations);
            check(sourceFacts==SourceSnapshot(profile),"mutating returned resolution and inspection leaves source graph unchanged");
            check(resolveFacts!=TypedCanonical(firstResolve,new Dictionary<string,string>())&&
                inspectionFacts!=TypedCanonical(firstInspection,new Dictionary<string,string>()),
                "returned objects are mutable detached outputs for caller ownership");
            var secondResolve=captured.Resolve(profile.catalog);var secondInspection=captured.Inspection(profile.catalog);
            check(resolveFacts==TypedCanonical(secondResolve,new Dictionary<string,string>()),"mutated resolution output cannot alter later evaluation");
            check(inspectionFacts==TypedCanonical(secondInspection,new Dictionary<string,string>()),"mutated inspection output cannot alter later evaluation");

            // Change source only after capture. The input must continue to represent its captured tune/fuel/fluid facts.
            string beforeMutation=TypedCanonical(captured.Resolve(profile.catalog),new Dictionary<string,string>());
            string beforeMutationInspection=TypedCanonical(captured.Inspection(profile.catalog),new Dictionary<string,string>());
            float oldFinal=car.tune.finalDrive;car.tune.finalDrive=oldFinal+.03125f;
            if(car.tune.awd!=null)car.tune.awd.frontBias=UnityEngine.Mathf.Clamp01(car.tune.awd.frontBias+.015f);
            if(car.fuel!=null)car.fuel.litres+=1.25;
            if(car.fluids!=null)car.fluids.oilLitres=Math.Max(0,car.fluids.oilLitres-.25);
            var fittedBinding=car.installed.slots.FirstOrDefault(slot=>slot!=null&&profile.parts.Any(part=>part.instanceId==slot.partInstanceId));
            var fittedPart=fittedBinding==null?null:profile.parts.FirstOrDefault(part=>part.instanceId==fittedBinding.partInstanceId);
            if(fittedPart?.condition!=null)
            {
                fittedPart.condition.health=UnityEngine.Mathf.Clamp01(fittedPart.condition.health-.07f);
                if(fittedPart.condition.elements!=null&&fittedPart.condition.elements.Length>0&&fittedPart.condition.elements[0]!=null)
                    fittedPart.condition.elements[0].health=UnityEngine.Mathf.Clamp01(fittedPart.condition.elements[0].health-.09f);
            }
            check(beforeMutation==TypedCanonical(captured.Resolve(profile.catalog),new Dictionary<string,string>()),
                "post-capture source tune/fuel/fluid mutation cannot alter the captured facts");
            check(beforeMutationInspection==TypedCanonical(captured.Inspection(profile.catalog),new Dictionary<string,string>()),
                "post-capture source tune/AWD/fuel/fluid/part/member mutation cannot alter later inspection output");
            check(sourceBefore!=SourceSnapshot(profile),"post-capture source mutation probe changed only its disposable profile");
        }

        static void InstalledBuildReplacement(Action<bool,string> check,StringBuilder log)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var profile,out var carId,InstalledBuildSeed+1);
            var car=profile.cars.Single(value=>value.instanceId==carId);var catalog=profile.catalog;
            var binding=car.installed.slots.FirstOrDefault(slot=>slot!=null&&slot.slot==PartCategory.Pistons.ToString());
            var old=profile.parts.FirstOrDefault(part=>binding!=null&&part.instanceId==binding.partInstanceId);
            PartDefinition definition=null;int quote=-1;var quoteErrors=new List<string>();
            if(old!=null)
                foreach(var candidate in catalog.parts.Where(item=>item!=null&&item.category==PartCategory.Pistons&&item.id!=old.definitionId&&
                        catalog.Fits(catalog.Spec(car.vehicleId),item)&&profile.reputation>=item.reputationRequired&&PartsMarket.FitsAny(profile,item))
                    .OrderBy(item=>item.id,StringComparer.Ordinal))
                {
                    try
                    {
                        int count=InventoryUnits.Count(profile,car,candidate);
                        int cost=PartsMarket.Quote(profile,candidate,count,false,1).cost;
                        if(cost>=0&&cost<=profile.cash){definition=candidate;quote=cost;break;}
                    }
                    catch(Exception error){quoteErrors.Add(candidate.id+":"+error.GetType().Name+"/"+error.Message);}
                }
            if(old==null||definition==null)
            {
                string rejected=string.Join(";",quoteErrors.Take(3));
                check(false,"replacement fixture precondition: eligible actual alternate Pistons offer required; quote errors="+rejected);
                log.AppendLine("OPEN|replacement|no eligible actual alternate definition/offer;quoteErrors="+rejected);return;
            }
            try
            {
                string looseId=PartOperations.Buy(profile,catalog,carId,definition.id,false);
                var loose=profile.parts.Single(part=>part.instanceId==looseId);
                PartOperations.Install(profile,catalog,carId,loose.instanceId);
                var fitted=profile.parts.Single(part=>part.instanceId==loose.instanceId);
                var member=fitted.condition?.elements?.FirstOrDefault(element=>element!=null);
                if(member==null)throw new InvalidOperationException("Installed Pistons definition has no actual member element to fault.");
                member.fault=ElementFault.CompressionLeak;member.health=.55f;member.severity=.08f;
                check(profile.parts.Any(part=>part.instanceId==old.instanceId&&part.location==PartLocation.Inventory),"replacement preserves displaced original physical identity in inventory");
                var capture=CaptureInstalledInputWithoutSideEffects(check,log,"replacement",profile,car);
                if(capture.ExceptionType!=null){check(false,"replacement typed capture failed: "+capture.ExceptionType+" | "+capture.ExceptionMessage);return;}
                var input=(ConcreteInstalledBuildInput)capture.Value;
                log.AppendLine("CASE|replacement|definition="+definition.id+";old="+old.instanceId+";new="+fitted.instanceId+";member="+member.id+";quote="+quote);
                CheckInstalledParity(check,log,"replacement",profile,car,input);
            }
            catch(Exception error){check(false,"replacement fixture actual domain operation failed: "+error.GetType().FullName+" | "+error.Message);}
        }

        static void InstalledBuildTireAndWheel(Action<bool,string> check,StringBuilder log)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var profile,out var carId,InstalledBuildSeed+2);
            var car=profile.cars.Single(value=>value.instanceId==carId);var catalog=profile.catalog;
            var tireBinding=car.installed.slots.FirstOrDefault(slot=>slot!=null&&slot.slot==PartCategory.Tires.ToString());
            var tire=tireBinding==null?null:profile.parts.FirstOrDefault(part=>part.instanceId==tireBinding.partInstanceId);
            var wheelBinding=car.installed.slots.FirstOrDefault(slot=>slot!=null&&slot.slot==PartCategory.Wheels.ToString());
            var wheel=wheelBinding==null?null:profile.parts.FirstOrDefault(part=>part.instanceId==wheelBinding.partInstanceId);
            if(tire?.condition?.tires==null||tire.condition.tires.Length!=4||wheel?.condition?.elements==null||wheel.condition.elements.Length<4)
            {check(false,"tires fixture precondition: actual installed four-corner TireCondition and WheelElement arrays required");log.AppendLine("OPEN|tires|actual per-corner installed tire/wheel arrays unavailable");return;}
            // Mutate existing corner state only; retain authored definitions and physical IDs.
            tire.condition.tires[1].consumedLife=.27;tire.condition.tires[3].health=.61f;
            wheel.condition.elements[0].health=.73f;wheel.condition.elements[2].severity=.04f;
            var capture=CaptureInstalledInputWithoutSideEffects(check,log,"tires-wheels",profile,car);
            if(capture.ExceptionType!=null){check(false,"tire/wheel typed capture failed: "+capture.ExceptionType+" | "+capture.ExceptionMessage);return;}
            var input=(ConcreteInstalledBuildInput)capture.Value;
            check(input.ResolvedPartCount>=2,"tire/wheel fixture resolves both actual fitted physical records");
            log.AppendLine("CASE|tires-wheels|tire="+tire.instanceId+";wheel="+wheel.instanceId+";tireCorners="+tire.condition.tires.Length+";wheelElements="+wheel.condition.elements.Length);
            CheckInstalledParity(check,log,"tires-wheels",profile,car,input);
        }

        static void InstalledBuildDetached(Action<bool,string> check,StringBuilder log)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var profile,out var carId,InstalledBuildSeed+3);
            var car=profile.cars.Single(value=>value.instanceId==carId);
            var engine=CartPreviewCharacterizationFixtures.DetachOwnedEngineToStorage(profile,carId);
            car.fuel.litres=17.375;car.fluids.oilLitres=2.625;car.fluids.oilCondition=.43;
            var capture=CaptureInstalledInputWithoutSideEffects(check,log,"detached",profile,car);
            if(capture.ExceptionType!=null){check(false,"detached typed capture failed: "+capture.ExceptionType+" | "+capture.ExceptionMessage);return;}
            var input=(ConcreteInstalledBuildInput)capture.Value;
            check(string.IsNullOrEmpty(car.engineAssemblyId)&&engine.storageId==SharedInventory.Storage,"detached case uses actual engine moved to Storage");
            log.AppendLine("CASE|detached|engine="+engine.instanceId+";storage="+engine.storageId+";fuel="+car.fuel.litres.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+";oil="+car.fluids.oilLitres.ToString("R",System.Globalization.CultureInfo.InvariantCulture));
            CheckInstalledParity(check,log,"detached",profile,car,input);
            check(input.Inspection(profile.catalog).startingFuel.litres==17.375&&input.Inspection(profile.catalog).startingFluids.oilLitres==2.625,
                "detached inspection preserves exact car fuel and fluids independently of stored engine");
            InstalledBuildContextReservation(check,log,"rebuild");
            InstalledBuildContextReservation(check,log,"garage-job");
            InstalledBuildMissingInspectionInput(check,log,"fluids-only");
            InstalledBuildMissingInspectionInput(check,log,"fuel-only");
            InstalledBuildMissingInspectionInput(check,log,"both-null");
        }

        static void InstalledBuildContextReservation(Action<bool,string> check,StringBuilder log,string kind)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var profile,out var carId,InstalledBuildSeed+40u+(uint)(kind=="rebuild"?1:2));
            var car=profile.cars.Single(value=>value.instanceId==carId);
            try
            {
                if(kind=="rebuild")EngineRebuilds.Start(profile,carId,CartPreviewCharacterizationFixtures.FixedNowTicks);
                else GarageBusiness.Assign(profile,GarageBusiness.DefaultPolicy(),carId,"photo-session",CartPreviewCharacterizationFixtures.FixedNowTicks);
                check(kind=="rebuild"?car.engineRebuild!=null&&!string.IsNullOrEmpty(car.engineRebuild.id):!string.IsNullOrEmpty(car.garageJobId),
                    kind+" fixture is an actual domain-created reservation");
                var capture=CaptureInstalledInputWithoutSideEffects(check,log,kind+"-context",profile,car);
                if(capture.ExceptionType!=null)throw new InvalidOperationException("Typed capture failed: "+capture.ExceptionType+" | "+capture.ExceptionMessage);
                var input=(ConcreteInstalledBuildInput)capture.Value;
                CheckInstalledParity(check,log,kind+"-context",profile,car,input);
                log.AppendLine("CASE|context-"+kind+"|job="+car.garageJobId+";rebuild="+(car.engineRebuild?.id??"none"));
            }
            catch(Exception error)
            {
                check(false,kind+" actual reservation fixture operation failed: "+error.GetType().FullName+" | "+error.Message);
                log.AppendLine("OPEN|context-"+kind+"|"+error.GetType().FullName+"|"+error.Message);
            }
        }

        static void InstalledBuildMissingInspectionInput(Action<bool,string> check,StringBuilder log,string variant)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var profile,out var carId,InstalledBuildSeed+30u+(uint)(variant=="fluids-only"?1:variant=="fuel-only"?2:3));
            var car=profile.cars.Single(value=>value.instanceId==carId);
            if(variant=="fluids-only"||variant=="both-null")car.fluids=null;
            if(variant=="fuel-only"||variant=="both-null")car.fuel=null;
            string sourceBefore=SourceSnapshot(profile);
            var frozen=CaptureInstalledOutcome(()=>LegacyInstalledBuildReference.Capture(profile,car));
            var capture=CaptureInstalledInputWithoutSideEffects(check,log,"missing-inspection-"+variant,profile,car);
            if(capture.ExceptionType!=null)throw new InvalidOperationException("Typed capture failed: "+capture.ExceptionType+" | "+capture.ExceptionMessage);
            var input=(ConcreteInstalledBuildInput)capture.Value;
            var resolveOld=CaptureInstalledOutcome(()=>LegacyInstalledBuildReference.Resolve(profile,car,profile.catalog));
            var resolveNew=CaptureTypedWithoutSideEffects(check,log,"missing inspection "+variant+" typed resolve",()=>input.Resolve(profile.catalog));
            var inspection=CaptureTypedWithoutSideEffects(check,log,"missing inspection "+variant+" typed inspection",()=>input.Inspection(profile.catalog));
            string message=variant=="fuel-only"?"Inspection requires captured fuel state.":"Inspection requires captured fluid state.";
            check(frozen.ExceptionType==typeof(NullReferenceException).FullName,
                "missing inspection input "+variant+" records legacy first exception type="+frozen.ExceptionType+" message="+frozen.ExceptionMessage);
            check(inspection.ExceptionType==typeof(InvalidOperationException).FullName&&inspection.ExceptionMessage==message,
                "typed inspection "+variant+" explicitly refuses absence with exact first error="+inspection.ExceptionType+" | "+inspection.ExceptionMessage);
            CheckInstalledOutcomeParity(check,"missing inspection "+variant+" resolve remains usable",resolveOld,resolveNew);
            check(sourceBefore==SourceSnapshot(profile),"missing inspection input "+variant+" preserves its source facts");
            log.AppendLine("MISSING_INSPECTION_INPUT|"+variant+"|legacy="+frozen.Text+";typed="+inspection.Text+";typedErrorExpected="+message);
        }

        static void InstalledBuildBindings(Action<bool,string> check,StringBuilder log)
        {
            string[] variants={"ordered", "blank", "dangling", "null-row", "duplicate-slot", "owner-null"};
            int ordinal=0;
            foreach(string variant in variants)
            {
                CartPreviewCharacterizationFixtures.CreateNormalized(out var profile,out var carId,InstalledBuildSeed+10u+(uint)ordinal++);
                var car=profile.cars.Single(value=>value.instanceId==carId);
                string injectedFact="none";
                if(variant=="ordered")car.installed.slots=car.installed.slots.AsEnumerable().Reverse().ToList();
                else if(variant=="blank")car.installed.slots[0].partInstanceId="";
                else if(variant=="dangling")
                {
                    string retainedBindingId=car.installed.slots[0].partInstanceId;
                    profile.parts.RemoveAll(part=>part.instanceId==retainedBindingId);
                    injectedFact="removed physical record, retained actual binding target="+retainedBindingId;
                }
                else if(variant=="null-row")car.installed.slots[0]=null;
                else if(variant=="owner-null")
                {
                    var ownedBinding=car.installed.slots.First(slot=>slot!=null&&profile.parts.Any(part=>part.instanceId==slot.partInstanceId));
                    profile.parts.Single(part=>part.instanceId==ownedBinding.partInstanceId).ownerId=null;
                }
                else
                {
                    var first=car.installed.slots.First(slot=>slot!=null);
                    var duplicate=new SlotBinding{slot=first.slot,partInstanceId=first.partInstanceId};
                    duplicate.slot=car.installed.slots.First(slot=>slot!=null&&slot.slot!=duplicate.slot).slot;
                    car.installed.slots.Add(duplicate);
                }
                string before=SourceSnapshot(profile);
                var oldResolve=CaptureInstalledOutcome(()=>LegacyInstalledBuildReference.Resolve(profile,car,profile.catalog));
                var oldInspection=CaptureInstalledOutcome(()=>LegacyInstalledBuildReference.Capture(profile,car));
                var oldInput=CaptureInstalledInputWithoutSideEffects(check,log,"binding-"+variant,profile,car);
                string captureException=oldInput.ExceptionType==null?"none":oldInput.ExceptionType+" | "+oldInput.ExceptionMessage;
                log.AppendLine("CASE|binding-"+variant+"|injection="+injectedFact+";capture="+captureException+";legacyResolve="+oldResolve.Text+";legacyInspection="+oldInspection.Text);
                if(oldInput.ExceptionType!=null)
                {
                    check(oldInput.ExceptionType==oldResolve.ExceptionType&&oldInput.ExceptionMessage==oldResolve.ExceptionMessage,
                        "binding-"+variant+" capture refusal matches authoritative resolver first error");
                    check(oldInput.ExceptionType==oldInspection.ExceptionType&&oldInput.ExceptionMessage==oldInspection.ExceptionMessage,
                        "binding-"+variant+" capture refusal matches authoritative inspection first error");
                }
                else
                {
                    var input=(ConcreteInstalledBuildInput)oldInput.Value;
                    CheckInstalledOutcomeParity(check,"binding-"+variant+" resolve",oldResolve,CaptureTypedWithoutSideEffects(check,log,"binding-"+variant+" typed resolve",()=>input.Resolve(profile.catalog)));
                    CheckInstalledOutcomeParity(check,"binding-"+variant+" inspection",oldInspection,CaptureTypedWithoutSideEffects(check,log,"binding-"+variant+" typed inspection",()=>input.Inspection(profile.catalog)));
                    check(input.BindingCount==car.installed.slots.Count&&input.ResolvedPartCount+input.MissingBindingCount==input.BindingCount,
                        "binding-"+variant+" preserves row count including unresolved IDs");
                }
                check(before==SourceSnapshot(profile),"binding-"+variant+" read-only capture/evaluation preserves malformed source exactly");
            }
        }

        static void CheckInstalledParity(Action<bool,string> check,StringBuilder log,string name,PlayerProfile profile,OwnedCar car,ConcreteInstalledBuildInput input)
        {
            string before=SourceSnapshot(profile);var random=UnityEngine.Random.state;
            var frozenResolve=CaptureInstalledOutcome(()=>LegacyInstalledBuildReference.Resolve(profile,car,profile.catalog));
            var currentResolve=CaptureInstalledOutcome(()=>MechanicsOwnership.Resolve(profile,car,profile.catalog));
            var newResolve=CaptureTypedWithoutSideEffects(check,log,name+" typed resolve",()=>input.Resolve(profile.catalog));
            var frozenInspection=CaptureInstalledOutcome(()=>LegacyInstalledBuildReference.Capture(profile,car));
            var currentInspection=CaptureInstalledOutcome(()=>InspectionBuild.Capture(profile,car));
            var newInspection=CaptureTypedWithoutSideEffects(check,log,name+" typed inspection",()=>input.Inspection(profile.catalog));
            CheckInstalledOutcomeParity(check,name+" current Resolve vs frozen",frozenResolve,currentResolve);
            CheckInstalledOutcomeParity(check,name+" typed Resolve vs frozen",frozenResolve,newResolve);
            CheckInstalledOutcomeParity(check,name+" current Inspection vs frozen",frozenInspection,currentInspection);
            CheckInstalledOutcomeParity(check,name+" typed Inspection vs frozen",frozenInspection,newInspection);
            bool sourceStable=before==SourceSnapshot(profile);
            check(sourceStable&&random.Equals(UnityEngine.Random.state),name+" public and typed evaluation preserve source and Unity RNG");
            log.AppendLine("PARITY|"+name+"|resolve="+frozenResolve.Text+";inspection="+frozenInspection.Text+";sourceStable="+sourceStable+";rngStable="+random.Equals(UnityEngine.Random.state));
            if(frozenInspection.Value is InspectionBuild reference&&newInspection.Value is InspectionBuild projected)
            {
                var oldInspectionView=NativePartBrowser.Inspect(reference);var newInspectionView=NativePartBrowser.Inspect(projected);
                check(TypedCanonical(oldInspectionView,new Dictionary<string,string>())==TypedCanonical(newInspectionView,new Dictionary<string,string>()),
                    name+" authoritative inspection view (curves/stats/warnings/components) matches frozen capture");
            }
            CheckNoOutputAliases(check,name,profile,car,newInspection.Value as InspectionBuild);
        }

        static void CheckNoOutputAliases(Action<bool,string> check,string name,PlayerProfile profile,OwnedCar car,InspectionBuild result)
        {
            if(result==null)return;
            var sourceParts=new HashSet<PartInstance>(profile.parts,ReferenceComparer<PartInstance>.Instance);
            bool componentsDetached=result.components!=null&&result.components.All(part=>part!=null&&!sourceParts.Contains(part));
            bool buildDetached=result.build!=null&&result.build.tune!=null&&!ReferenceEquals(result.build.tune,car.tune);
            check(componentsDetached&&buildDetached,name+" returned component/tune records do not alias profile facts");
        }

        sealed class ReferenceComparer<T>:IEqualityComparer<T> where T:class
        {
            public static readonly ReferenceComparer<T> Instance=new ReferenceComparer<T>();
            public bool Equals(T left,T right)=>ReferenceEquals(left,right);
            public int GetHashCode(T value)=>System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
        }

        static void CheckForbiddenRetainedRoots(Action<bool,string> check,ConcreteInstalledBuildInput input,string name)
        {
            bool safe=true;var forbidden=new[]{typeof(PlayerProfile),typeof(OwnedCar),typeof(MechanicalCatalog),typeof(Delegate),typeof(UnityEngine.Object)};
            foreach(var field in typeof(ConcreteInstalledBuildInput).GetFields(BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public))
                if(forbidden.Any(type=>type.IsAssignableFrom(field.FieldType)))safe=false;
            check(safe,name+" input fields retain no borrowed profile/car/catalogue/delegate/Unity-object root");
            check(!ReferenceEquals(input,null),name+" capture returned an owning input value");
        }

        static InstalledOutcome CaptureInstalledOutcome(Func<object> call)
        {
            try{return new InstalledOutcome{Value=call()};}
            catch(Exception error){return new InstalledOutcome{ExceptionType=error.GetType().FullName,ExceptionMessage=error.Message};}
        }

        sealed class InstalledOutcome
        {
            public object Value;public string ExceptionType,ExceptionMessage;
            public string Text=>ExceptionType==null?(Value==null?"null":Value.GetType().Name):ExceptionType+" | "+ExceptionMessage;
        }

        static void CheckInstalledOutcomeParity(Action<bool,string> check,string label,InstalledOutcome expected,InstalledOutcome actual)
        {
            bool same=expected.ExceptionType==actual.ExceptionType&&expected.ExceptionMessage==actual.ExceptionMessage;
            if(same&&expected.ExceptionType==null)
                same=TypedCanonical(expected.Value,new Dictionary<string,string>())==TypedCanonical(actual.Value,new Dictionary<string,string>());
            check(same,label+" exact typed output or first exception");
        }

        static void MutateReturnedBuild(BuildResolution build)
        {
            if(build==null)return;
            if(build.errors!=null)build.errors.Add("diagnostic output mutation");
            if(build.tune!=null)build.tune.finalDrive+=.125f;
            if(build.spec!=null)build.spec.massKg+=1;
        }

        static string MutateReturnedInspection(InspectionBuild inspection,out bool memberMutated,out bool clutchMutated,out bool awdMutated)
        {
            memberMutated=false;clutchMutated=false;awdMutated=false;
            if(inspection==null)return "none";
            var paths=new List<string>();
            var clutchPart=inspection.components?.FirstOrDefault(part=>part?.condition?.clutch!=null);
            var memberPart=inspection.components?.FirstOrDefault(part=>part?.condition?.elements!=null&&part.condition.elements.Length>0&&part.condition.elements[0]!=null);
            if(memberPart!=null){memberPart.condition.elements[0].health=.234f;memberMutated=true;paths.Add("components/member.health");}
            if(clutchPart!=null)
            {
                clutchPart.condition.clutch.oilLitres=Math.Max(0,clutchPart.condition.clutch.oilLitres-.1);
                clutchPart.condition.clutch.oilCondition=Math.Max(0,clutchPart.condition.clutch.oilCondition-.05);
                clutchMutated=true;paths.Add("components/clutch.oilLitres+oilCondition");
            }
            if(inspection.components!=null&&inspection.components.Count>0&&inspection.components[0]!=null)
            {
                inspection.components[0].condition.health=.123f;
                inspection.components.RemoveAt(0);
                paths.Add("components[0].condition.health+remove");
            }
            if(inspection.startingFuel!=null){inspection.startingFuel.litres+=.5;paths.Add("startingFuel.litres");}
            if(inspection.startingFluids!=null){inspection.startingFluids.oilLitres+=.5;paths.Add("startingFluids.oilLitres");}
            if(inspection.build?.tune?.awd!=null)
            {inspection.build.tune.awd.frontBias=UnityEngine.Mathf.Clamp01(inspection.build.tune.awd.frontBias+.05f);awdMutated=true;paths.Add("build.tune.awd.frontBias");}
            MutateReturnedBuild(inspection.build);
            return string.Join(",",paths);
        }

        static InstalledOutcome CaptureTypedWithoutSideEffects(Action<bool,string> check,StringBuilder log,string label,Func<object> call)
        {
            long jsonBefore=RuntimeDataEncodingProbe.CurrentThreadJsonCallCount;
            var randomBefore=UnityEngine.Random.state;
            bool entropyModeBefore=RuntimeEntropy.Capture;DateTime entropyBefore=RuntimeEntropy.UtcNow;
            var identity=RuntimeEntropy.BeginIdentityObservation(32);
            InstalledOutcome outcome;
            try
            {
                using(RuntimeDataEncodingProbe.Measure("s06d2b1h/"+label))outcome=CaptureInstalledOutcome(call);
            }
            finally{identity.Dispose();}
            check(identity.Closed&&!identity.Overflowed&&identity.Count==0,label+" emits no physical identities");
            check(jsonBefore==RuntimeDataEncodingProbe.CurrentThreadJsonCallCount,label+" makes no instrumented JSON calls");
            check(randomBefore.Equals(UnityEngine.Random.state),label+" preserves Unity RNG");
            bool entropyModeAfter=RuntimeEntropy.Capture;DateTime entropyAfter=RuntimeEntropy.UtcNow;
            check(entropyModeBefore==entropyModeAfter,label+" preserves RuntimeEntropy mode");
            if(entropyModeBefore)check(entropyBefore.Ticks==entropyAfter.Ticks&&entropyBefore.Kind==entropyAfter.Kind,label+" preserves deterministic RuntimeEntropy clock");
            log.AppendLine("ENTROPY|"+label+"|capture="+entropyModeBefore+"/"+entropyModeAfter+";utc="+entropyBefore.Ticks+"/"+entropyBefore.Kind+
                "->"+entropyAfter.Ticks+"/"+entropyAfter.Kind+";same="+(entropyBefore.Ticks==entropyAfter.Ticks));
            return outcome;
        }

        static InstalledOutcome CaptureInstalledInputWithoutSideEffects(Action<bool,string> check,StringBuilder log,string label,PlayerProfile profile,OwnedCar car)
        {
            string sourceBefore=SourceSnapshot(profile);
            long jsonBefore=RuntimeDataEncodingProbe.CurrentThreadJsonCallCount;
            var randomBefore=UnityEngine.Random.state;
            bool entropyModeBefore=RuntimeEntropy.Capture;DateTime entropyBefore=RuntimeEntropy.UtcNow;
            var identity=RuntimeEntropy.BeginIdentityObservation(32);
            InstalledOutcome outcome;
            try
            {
                using(RuntimeDataEncodingProbe.Measure("s06d2b1h/capture-"+label))
                    outcome=CaptureInstalledOutcome(()=>ConcreteInstalledBuildInput.Capture(profile,car));
            }
            finally{identity.Dispose();}
            bool sourceStable=sourceBefore==SourceSnapshot(profile);
            bool jsonStable=jsonBefore==RuntimeDataEncodingProbe.CurrentThreadJsonCallCount;
            bool randomStable=randomBefore.Equals(UnityEngine.Random.state);
            bool identityStable=identity.Closed&&!identity.Overflowed&&identity.Count==0;
            bool entropyModeAfter=RuntimeEntropy.Capture;DateTime entropyAfter=RuntimeEntropy.UtcNow;
            bool entropyModeStable=entropyModeBefore==entropyModeAfter;
            bool deterministicClockStable=!entropyModeBefore||entropyBefore.Ticks==entropyAfter.Ticks&&entropyBefore.Kind==entropyAfter.Kind;
            check(sourceStable,label+" capture preserves exact source graph");
            check(jsonStable,label+" capture makes no instrumented JSON calls");
            check(randomStable,label+" capture preserves Unity RNG");
            check(identityStable,label+" capture emits no physical identities");
            check(entropyModeStable,label+" capture preserves RuntimeEntropy mode");
            if(entropyModeBefore)check(deterministicClockStable,label+" capture preserves deterministic RuntimeEntropy clock");
            log.AppendLine("CAPTURE_EFFECTS|"+label+"|source="+sourceStable+";json="+jsonStable+";rng="+randomStable+";identity="+identity.Count+
                "/closed="+identity.Closed+"/overflow="+identity.Overflowed+";entropyMode="+entropyModeBefore+"/"+entropyModeAfter+
                ";utc="+entropyBefore.Ticks+"/"+entropyBefore.Kind+"->"+entropyAfter.Ticks+"/"+entropyAfter.Kind+"/same="+(entropyBefore.Ticks==entropyAfter.Ticks)+
                ";outcome="+outcome.Text);
            return outcome;
        }

        static void AuditInstalledBuildSource(Action<bool,string> check)
        {
            string root=Path.Combine(UnityEngine.Application.dataPath,"DragLab","Scripts","Domain");
            bool filesExist=true,noForbiddenTokens=true;
            foreach(string relative in new[]{"ConcreteInstalledBuildInput.cs","InstalledBuildResolution.cs"})
            {
                string path=Path.Combine(root,relative);
                if(!File.Exists(path)){filesExist=false;continue;}
                string source=File.ReadAllText(path,Encoding.UTF8);
                foreach(string token in new[]{"JsonUtility", "ToJson(", "FromJson", "Ensure(", "NewId(", "Guid.NewGuid"})
                    if(source.IndexOf(token,StringComparison.Ordinal)>=0)noForbiddenTokens=false;
            }
            check(filesExist,"source audit found both concrete input and shared resolution files");
            check(filesExist&&noForbiddenTokens,"source audit found no JSON materialization, Ensure, or identity allocation in the seam");
        }

        sealed class ProjectionCostFixture
        {
            public PlayerProfile Profile;public OwnedCar Car;public ConcreteInstalledBuildInput Input;public object Result;
        }

        static void RunInstalledProjectionCosts(Action<bool,string> check,StringBuilder log,PlayerProfile source,string carId)
        {
            bool allocationAvailable=CostAllocationCalibration();
            log.AppendLine("COST_PROTOCOL|warmups=5;measured=31;p95=nearest-rank-30;setup=exact literal profile copy and optional typed input before timer;"+
                "checksum=typed canonical output after timer;calibratedAllocation="+(allocationAvailable?"same-thread 8192-byte probe":"NA")+";maxPreparedCopies=1");
            Action<string,Action<ProjectionCostFixture>> run=(label,operation)=>RunInstalledProjectionCostSeries(check,log,label,source,carId,operation,allocationAvailable);
            run("capture",fixture=>fixture.Result=ConcreteInstalledBuildInput.Capture(fixture.Profile,fixture.Car));
            run("legacyResolve",fixture=>fixture.Result=LegacyInstalledBuildReference.Resolve(fixture.Profile,fixture.Car,fixture.Profile.catalog));
            run("typedResolve",fixture=>fixture.Result=fixture.Input.Resolve(fixture.Profile.catalog));
            run("legacyInspectionCapture",fixture=>fixture.Result=LegacyInstalledBuildReference.Capture(fixture.Profile,fixture.Car));
            run("typedInspection",fixture=>fixture.Result=fixture.Input.Inspection(fixture.Profile.catalog));
        }

        static void RunInstalledProjectionCostSeries(Action<bool,string> check,StringBuilder log,string label,PlayerProfile source,string carId,
            Action<ProjectionCostFixture> operation,bool allocationAvailable)
        {
            const int warmups=5,samples=31;
            var ticks=new long[samples];var allocated=new long[samples];var gc0=new int[samples];var gc1=new int[samples];var gc2=new int[samples];
            string sourceBefore=SourceSnapshot(source);int errors=0;long calibrationBytes=-1;
            if(allocationAvailable)
            {
                try{long before=GC.GetAllocatedBytesForCurrentThread();var known=new byte[8192];known[0]=1;GC.KeepAlive(known);calibrationBytes=GC.GetAllocatedBytesForCurrentThread()-before;}
                catch{allocationAvailable=false;}
            }
            for(int index=0;index<warmups+samples;index++)
            {
                ProjectionCostFixture fixture=null;long elapsed=0,bytes=-1;int d0=0,d1=0,d2=0;string error=null,checksum="";
                try
                {
                    var copy=CartPreviewLiteralTestClone.Copy(source);
                    var car=copy.cars.Single(candidate=>candidate.instanceId==carId);
                    fixture=new ProjectionCostFixture{Profile=copy,Car=car};
                    if(label=="typedResolve"||label=="typedInspection")fixture.Input=ConcreteInstalledBuildInput.Capture(copy,car);
                    int c0=GC.CollectionCount(0),c1=GC.CollectionCount(1),c2=GC.CollectionCount(2);
                    long beforeBytes=allocationAvailable?GC.GetAllocatedBytesForCurrentThread():-1;
                    long start=Stopwatch.GetTimestamp();
                    try{operation(fixture);}
                    finally{elapsed=Stopwatch.GetTimestamp()-start;}
                    d0=GC.CollectionCount(0)-c0;d1=GC.CollectionCount(1)-c1;d2=GC.CollectionCount(2)-c2;
                    if(beforeBytes>=0)bytes=GC.GetAllocatedBytesForCurrentThread()-beforeBytes;
                    checksum=InstalledProjectionCostChecksum(fixture.Result);
                }
                catch(Exception exception){error=exception.GetType().FullName+" | "+exception.Message;errors++;}
                if(index>=warmups)
                {
                    int sample=index-warmups;ticks[sample]=elapsed;allocated[sample]=bytes;gc0[sample]=d0;gc1[sample]=d1;gc2[sample]=d2;
                    log.AppendLine("COST_SAMPLE|"+label+"|i="+sample+";ticks="+elapsed+";allocated="+(allocationAvailable?bytes.ToString(System.Globalization.CultureInfo.InvariantCulture):"NA")+
                        ";gc="+d0+"/"+d1+"/"+d2+";checksum="+checksum+";error="+(error??"none"));
                }
                else if(error!=null)log.AppendLine("COST_WARMUP_ERROR|"+label+"|i="+index+";"+error);
                fixture=null;
            }
            long[] sortedTicks=ticks.OrderBy(value=>value).ToArray();
            int p95=29;
            string allocatedSummary="NA";
            if(allocationAvailable)
            {
                long[] sortedBytes=allocated.OrderBy(value=>value).ToArray();
                allocatedSummary="median/p95/max="+sortedBytes[15]+"/"+sortedBytes[p95]+"/"+sortedBytes[30]+";calibration="+calibrationBytes;
            }
            int totalGc0=gc0.Sum(),totalGc1=gc1.Sum(),totalGc2=gc2.Sum();
            log.AppendLine("COST_SERIES|"+label+"|n="+samples+";medianTicks="+sortedTicks[15]+";p95rank=30/31:"+sortedTicks[p95]+
                ";maxTicks="+sortedTicks[30]+";medianMs="+(sortedTicks[15]*1000.0/Stopwatch.Frequency).ToString("F4",System.Globalization.CultureInfo.InvariantCulture)+
                ";p95Ms="+(sortedTicks[p95]*1000.0/Stopwatch.Frequency).ToString("F4",System.Globalization.CultureInfo.InvariantCulture)+
                ";maxMs="+(sortedTicks[30]*1000.0/Stopwatch.Frequency).ToString("F4",System.Globalization.CultureInfo.InvariantCulture)+
                ";allocatedBytes="+allocatedSummary+";gc0/1/2="+totalGc0+"/"+totalGc1+"/"+totalGc2+";errors="+errors);
            check(errors==0,label+" bounded timing series completed without route exceptions; failures="+errors);
            check(sourceBefore==SourceSnapshot(source),label+" setup and measured read routes preserve the shared baseline source");
        }

        static string InstalledProjectionCostChecksum(object result)
        {
            if(result==null)return "null";
            if(result is ConcreteInstalledBuildInput input)
                return input.BindingCount+"/"+input.ResolvedPartCount+"/"+input.MissingBindingCount;
            string canonical=TypedCanonical(result,new Dictionary<string,string>());
            unchecked
            {
                uint hash=2166136261;
                foreach(char value in canonical)hash=(hash^value)*16777619;
                return canonical.Length+"/"+hash.ToString("x8",System.Globalization.CultureInfo.InvariantCulture);
            }
        }
    }
}
#endif

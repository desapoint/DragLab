#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using UnityEngine;

namespace DragLab.Runtime
{
    /// <summary>Editor-only S06d2b legacy characterization; never a player fallback.</summary>
    public static partial class CartPreviewCharacterizationChecks
    {
        const uint FixtureSeed=16102026u;
        const string TraceName="S06D2B1_LEGACY_TRACE_V3.txt";
        const string TimingName="S06D2B1_METHOD_TIMINGS_V3.txt";

        [Serializable] sealed class TypedProbe
        { public float scalar; public double precise; public string id; public string partInstanceId; public string note; public List<int> order; }
        sealed class Outcome
        {
            public object Value;
            public string ExceptionType,ExceptionMessage;
            public long ValueTicks,AllocatedBytes;
            public bool Threw=>ExceptionType!=null;
            public string Text=>Threw?"THREW "+ExceptionType+" | "+ExceptionMessage:Convert.ToString(Value,CultureInfo.InvariantCulture);
        }
        sealed class InputPair
        {
            public PlayerProfile Actual,Legacy;
            public PartCartLine[] ActualRows,LegacyRows;
            public string ProfileBefore,CatalogBefore,RowsBefore;
        }
        sealed class ObjectReferenceComparer : IEqualityComparer<object>
        {
            public new bool Equals(object left,object right)=>ReferenceEquals(left,right);
            public int GetHashCode(object value)=>RuntimeHelpers.GetHashCode(value);
        }
        sealed class Trace
        {
            readonly Action<bool,string> check;
            readonly string folder;
            readonly bool collectAll;
            public int Assertions,Failures;
            public readonly StringBuilder Text=new StringBuilder();
            public readonly StringBuilder TimingText=new StringBuilder();
            public Trace(Action<bool,string> callback,string reportFolder,bool collect=false){check=callback;folder=reportFolder;collectAll=collect;}
            public void SourceDifference(string label,string before,string actual,string legacy)
            {
                string safe=new string(label.Select(c=>Path.GetInvalidFileNameChars().Contains(c)?'_':c).ToArray());
                string prefix=Path.Combine(folder,"source-diff-"+safe);
                File.WriteAllText(prefix+"-before.txt",before,new UTF8Encoding(false));
                File.WriteAllText(prefix+"-actual.txt",actual,new UTF8Encoding(false));
                File.WriteAllText(prefix+"-legacy.txt",legacy,new UTF8Encoding(false));
                int different=0,common=Math.Min(before.Length,actual.Length);
                while(different<common&&before[different]==actual[different])different++;
                int start=Math.Max(0,different-140);
                string a=before.Substring(start,Math.Min(500,before.Length-start));
                string b=actual.Substring(start,Math.Min(500,actual.Length-start));
                File.WriteAllText(prefix+"-first.txt","First differing character="+different+"\nBEFORE: "+a+"\nACTUAL: "+b+"\n",new UTF8Encoding(false));
            }
            public void Assert(bool value,string label)
            {
                Assertions++;if(!value)Failures++;
                string record="ASSERT|"+(value?"PASS":"FAIL")+"|"+label;
                Text.AppendLine(record);Progress(record);
                if(collectAll){try{check(value,label);}catch(Exception error){Text.AppendLine("CHECK_CALLBACK_EXCEPTION|"+error.GetType().FullName+"|"+error.Message);}}
                else check(value,label);
            }
            public void Add(string value)
            {if(value.StartsWith("METHOD|",StringComparison.Ordinal))TimingText.AppendLine(value);else Text.AppendLine(value);Progress(value);}
            // Retain incomplete diagnostic evidence on timeout/crash. This is
            // explicitly separate from the final complete stable trace.
            void Progress(string value)=>File.AppendAllText(Path.Combine(folder,"progress-v3.txt"),value.Replace("\r\n","\n")+"\n",new UTF8Encoding(false));
        }
        static readonly HashSet<string> IdentityFieldNames=new HashSet<string>(StringComparer.Ordinal)
        {"id","instanceId","assetId","ownerId","parentVehicleId","vehicleInstanceId","engineInstanceId",
         "selectedCarId","selectedOwnedVehicleId","partInstanceId","engineAssemblyId","carId","engineId","unitId",
         "targetId","auxiliaryId","spareId"};
        static readonly Dictionary<PlayerProfile,string> FixtureBaselines=new Dictionary<PlayerProfile,string>();

        /// <summary>Run deterministic, isolated legacy reference and contract probes into the supplied folder.</summary>
        public static void Run(Action<bool,string> check,string reportFolder)
        {
            if(check==null)throw new ArgumentNullException(nameof(check));
            if(string.IsNullOrWhiteSpace(reportFolder))throw new ArgumentException("An isolated Editor report folder is required.",nameof(reportFolder));
            Directory.CreateDirectory(reportFolder);
            FixtureBaselines.Clear();
            var trace=new Trace(check,reportFolder);trace.Add("S06D2B1_LEGACY_TRACE_V3");
            trace.Add("seed="+FixtureSeed+";nowTicks="+CartPreviewCharacterizationFixtures.FixedNowTicks+";oracle=LegacyCartPreviewOracle;candidate=PartCart;typedCandidate=NONE");
            try
            {
                RecordSourceHashes(trace);
                RunTypedWalkerSelfTests(trace);
                RunNormalizedFixtureChecks(trace);
                RunMixedLegacyTrace(trace);
                RunManualErrorVectors(trace);
                RunCoreRowMatrix(trace);
                RunIdentityMatrix(trace);
                RunTopologyMatrix(trace);
                RunHundredLineBoundary(trace);
                RunReservationLifetime(trace);
                RunEnsureHealingTrace(trace);
                trace.Add("NATIVE_CALLBACK_TRACE|OPEN|NativeInventoryMoves.OpenInventoryMove is not exercised by this Editor-only checker.");
                trace.Add("GATES|OPEN|Full supported row/topology matrix, input-row/action/quantity identity mapping, native callback trace, native frame evidence and full S06 gate remain parent-owned.");
            }
            catch(Exception error)
            {
                trace.Assert(false,"Characterization runner caught "+error.GetType().FullName+" | "+error.Message);
                trace.Add("RUNNER_EXCEPTION|"+error.GetType().FullName+"|"+error.Message);
            }
            finally
            {
                string stableText=trace.Text.ToString().Replace("\r\n","\n");
                File.WriteAllText(Path.Combine(reportFolder,TraceName),stableText,new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(reportFolder,TraceName+".sha256"),HashFile(Path.Combine(reportFolder,TraceName))+"  "+TraceName+"\n",new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(reportFolder,TimingName),trace.TimingText.ToString(),new UTF8Encoding(false));
                FixtureBaselines.Clear();
            }
        }

        /// <summary>Run all independent characterization groups, preserving every failure and explicit skip.</summary>
        public static void RunCollectAll(Action<bool,string> check,string reportFolder)
        {
            if(check==null)throw new ArgumentNullException(nameof(check));
            if(string.IsNullOrWhiteSpace(reportFolder))throw new ArgumentException("An isolated Editor report folder is required.",nameof(reportFolder));
            Directory.CreateDirectory(reportFolder);
            FixtureBaselines.Clear();
            var trace=new Trace(check,reportFolder,true);
            trace.Add("S06D2B1_LEGACY_TRACE_V3|mode=collect-all;nonzero-on-any-false-assertion");
            trace.Add("seed="+FixtureSeed+";nowTicks="+CartPreviewCharacterizationFixtures.FixedNowTicks+";oracle=LegacyCartPreviewOracle;candidate=PartCart;typedCandidate=NONE");
            try
            {
                RunIndependentGroup(trace,"source-provenance-and-typed-comparator",()=>{RecordSourceHashes(trace);RunTypedWalkerSelfTests(trace);});
                RunIndependentGroup(trace,"valid-Ensure-and-Resolve",()=>RunNormalizedFixtureChecks(trace));
                RunIndependentGroup(trace,"mixed-cart-and-legacy-previews",()=>RunMixedLegacyTrace(trace));
                RunIndependentGroup(trace,"malformed-cart-first-errors",()=>RunManualErrorVectors(trace));
                RunIndependentGroup(trace,"core-row-contracts",()=>RunCoreRowMatrix(trace));
                RunIndependentGroup(trace,"row-key-physical-identity",()=>RunIdentityMatrix(trace));
                RunIndependentGroup(trace,"authored-topology-and-owner-graph",()=>RunTopologyMatrix(trace));
                RunIndependentGroup(trace,"100-row-and-quantity-boundaries",()=>RunHundredLineBoundary(trace));
                RunIndependentGroup(trace,"reservation-lifetime",()=>RunReservationLifetime(trace));
                RunIndependentGroup(trace,"legacy-Ensure-healing",()=>RunEnsureHealingTrace(trace));
                RunIndependentGroup(trace,"JSON-and-preview-mutation-boundary",()=>RunMutationBoundaryCore(trace));
                trace.Add("NATIVE_CALLBACK_TRACE|OPEN|NativeInventoryMoves.OpenInventoryMove is not exercised by this Editor-only checker.");
                trace.Add("GATES|OPEN|Full supported row/topology matrix, input-row/action/quantity identity mapping, native callback trace, native frame evidence and full S06 gate remain parent-owned.");
            }
            finally
            {
                trace.Add("RESULT|"+(trace.Failures==0?"PASS":"FAIL")+"|assertions="+trace.Assertions+";failures="+trace.Failures);
                WriteReports(trace,reportFolder);
                FixtureBaselines.Clear();
            }
        }

        static void RunIndependentGroup(Trace trace,string name,Action group)
        {
            FixtureBaselines.Clear();
            try{group();trace.Add("GROUP|DONE|"+name);}
            catch(Exception error)
            {
                trace.Add("GROUP_EXCEPTION|"+name+"|"+error.GetType().FullName+"|"+error.Message);
                trace.Assert(false,"Independent group threw / "+name+" / "+error.GetType().FullName+" | "+error.Message);
                trace.Add("GROUP|CONTINUE|"+name);
            }
            finally{FixtureBaselines.Clear();}
        }

        /// <summary>Targeted expansion diagnostics, not a substitute for the full collector.</summary>
        public static void RunExpansion(Action<bool,string> check,string reportFolder,bool topology,string subset=null,bool identity=false)
        {
            if(check==null)throw new ArgumentNullException(nameof(check));
            if(topology&&identity)throw new ArgumentException("Select one diagnostic expansion.");
            if(string.IsNullOrWhiteSpace(reportFolder))throw new ArgumentException("An isolated Editor report folder is required.",nameof(reportFolder));
            Directory.CreateDirectory(reportFolder);FixtureBaselines.Clear();
            var trace=new Trace(check,reportFolder,true);
            trace.Add("S06D2B1_LEGACY_TRACE_V3|mode="+(identity?"identity-only":topology?"topology-only":"core-rows-only")+";subset="+(subset??"all")+";nonzero-on-any-false-assertion");
            trace.Add("seed="+FixtureSeed+";nowTicks="+CartPreviewCharacterizationFixtures.FixedNowTicks+";oracle=LegacyCartPreviewOracle;candidate=PartCart;typedCandidate=NONE");
            try
            {
                RunIndependentGroup(trace,"source-provenance-and-typed-comparator",()=>{RecordSourceHashes(trace);RunTypedWalkerSelfTests(trace);});
                if(identity)RunIndependentGroup(trace,"row-key-physical-identity",()=>RunIdentityMatrix(trace));
                else if(topology)RunIndependentGroup(trace,"authored-topology-and-owner-graph",()=>RunTopologyMatrix(trace,subset));
                else RunIndependentGroup(trace,"core-row-contracts",()=>RunCoreRowMatrix(trace,subset));
                trace.Add("GATES|OPEN|Targeted diagnostic subset only; full collector, native callback and identity stream remain required.");
            }
            finally
            {
                trace.Add("RESULT|"+(trace.Failures==0?"PASS":"FAIL")+"|assertions="+trace.Assertions+";failures="+trace.Failures);
                WriteReports(trace,reportFolder);FixtureBaselines.Clear();
            }
        }

        static void WriteReports(Trace trace,string reportFolder)
        {
            string stableText=trace.Text.ToString().Replace("\r\n","\n");
            File.WriteAllText(Path.Combine(reportFolder,TraceName),stableText,new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(reportFolder,TraceName+".sha256"),HashFile(Path.Combine(reportFolder,TraceName))+"  "+TraceName+"\n",new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(reportFolder,TimingName),trace.TimingText.ToString(),new UTF8Encoding(false));
        }

        // Diagnostic localization only. A successful probe run means the
        // operations were observed, never that source immutability passed.
        public static void RunMutationBoundary(Action<bool,string> check,string reportFolder)
        {
            Directory.CreateDirectory(reportFolder);
            var trace=new Trace(check,reportFolder,true);
            RunMutationBoundaryCore(trace);
            string text=trace.Text.ToString().Replace("\r\n","\n");
            File.WriteAllText(Path.Combine(reportFolder,"mutation-boundary.txt"),text,new UTF8Encoding(false));
        }

        static void RunMutationBoundaryCore(Trace trace)
        {
            string preparedJson=null;
            foreach(string kind in new[]{"ToJson-only","ToJson-FromJson","FromJson-only","Total-no-move","Total-move","Preview-no-move"})
            {
                PlayerProfile p;string carId;
                CartPreviewCharacterizationFixtures.CreateNormalized(out p,out carId,FixtureSeed);
                var car=p.SelectedCar;
                var offer=FindOffer(p,car,PartCategory.HeadIntake,false);
                if(kind=="Total-move")PartOperations.Buy(p,p.catalog,carId,offer.id,false);
                var owned=p.parts.LastOrDefault(part=>part.location==PartLocation.Inventory&&part.definitionId==offer.id);
                var buy=new[]{new PartCartLine{action=CartAction.NewSet,definitionId=offer.id,quantity=1}};
                string before=SourceSnapshot(p),value=null;
                Exception failure=null;
                try
                {
                    if(kind=="ToJson-only"){preparedJson=JsonUtility.ToJson(p);value="chars="+preparedJson.Length;}
                    else if(kind=="ToJson-FromJson"){var copy=JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(p));value="cars="+copy.cars.Count;}
                    else if(kind=="FromJson-only"){var copy=JsonUtility.FromJson<PlayerProfile>(preparedJson);value="cars="+copy.cars.Count;}
                    else if(kind=="Total-no-move")value=PartCart.Total(p,carId,buy,now:CartPreviewCharacterizationFixtures.FixedNowTicks).ToString(CultureInfo.InvariantCulture);
                    else if(kind=="Total-move")value=PartCart.Total(p,carId,new[]{new PartCartLine{action=CartAction.Move,definitionId=owned.definitionId,
                        assetId=owned.instanceId,destination=InventoryKind.Storage,destinationId=SharedInventory.Storage}},
                        now:CartPreviewCharacterizationFixtures.FixedNowTicks).ToString(CultureInfo.InvariantCulture);
                    else {var copy=PartCart.PreviewProfile(p,car,buy);value="parts="+copy.parts.Count;}
                }
                catch(Exception error){failure=error;}
                string after=SourceSnapshot(p);bool changed=before!=after;
                if(changed)trace.SourceDifference("boundary-"+kind,before,after,after);
                trace.Add("MUTATION_BOUNDARY|"+kind+"|sourceChanged="+changed+"|result="+(failure==null?value:failure.GetType().FullName+" | "+failure.Message));
                trace.Assert(failure==null,"Diagnostic operation completed / "+kind+" / sourceChanged="+changed);
                trace.Assert(!changed,"Source graph remains unchanged / "+kind);
            }
            trace.Add("GATES|FAIL|Diagnostic completion is not source-immutability acceptance; the strict mixed-cart failure remains open.");
        }

        static void RunNormalizedFixtureChecks(Trace trace)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var p,out var carId,FixtureSeed);
            var car=p.cars.Find(item=>item.instanceId==carId);
            trace.Assert(car!=null&&ReferenceEquals(p.SelectedCar,car)&&MechanicsOwnership.Validate(p,p.catalog)==null,
                "fixture/current-schema selected car and full ownership validator");
            if(car==null)return;
            string before=SourceSnapshot(p);var random=UnityEngine.Random.state;
            var stopwatch=Stopwatch.StartNew();long allocatedBefore=GC.GetAllocatedBytesForCurrentThread();
            string ensureError=null;try{MechanicsOwnership.Ensure(p,p.catalog);}catch(Exception error){ensureError=error.GetType().FullName+" | "+error.Message;}
            stopwatch.Stop();long allocated=GC.GetAllocatedBytesForCurrentThread()-allocatedBefore;
            string after=SourceSnapshot(p);
            trace.Assert(ensureError==null&&before==after&&UnityEngine.Random.state.Equals(random),
                "valid normalized Ensure is a serialized graph/random-state no-op / "+(ensureError??"unchanged"));
            trace.Add("METHOD|Ensure.valid-noop|ticks="+stopwatch.ElapsedTicks+";allocatedBytes="+allocated+
                ";sourceUnchanged="+(before==after));
            trace.Add("FIXTURE|normalized|car="+StableCarLabel(p,carId)+";cars="+p.cars.Count+
                ";parts="+p.parts.Count+";units="+p.looseEngineElements.Count+";dealer="+p.inventory.dealer.Count+
                ";catalogDefinitions="+p.catalog.parts.Count);
            var resolve=Measure(()=>MechanicsOwnership.Resolve(p,car,p.catalog).Drivable?"drivable":"not-drivable");
            trace.Add("METHOD|Resolve.normalized-selected-car|ticks="+resolve.ValueTicks+";allocatedBytes="+resolve.AllocatedBytes+";result="+resolve.Text);
        }

        static void RunMixedLegacyTrace(Trace trace)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var p,out var carId,FixtureSeed);
            var car=p.cars.Find(item=>item.instanceId==carId);if(car==null)return;
            uint supplySeed=FixtureSeed^0x5f3759dfu;if(supplySeed==0)supplySeed=1;
            var usedPart=CartPreviewCharacterizationFixtures.AddUsedPart(p,"s06d2b/mixed/used-part",ref supplySeed);
            var usedUnit=CartPreviewCharacterizationFixtures.AddUsedUnit(p,"s06d2b/mixed/used-unit",ref supplySeed);
            var newSet=FindOffer(p,car,PartCategory.HeadIntake,unit:false);
            var multiDefinition=p.catalog.Find(usedUnit.DefinitionId);
            string unitRole=usedUnit.unit.element.role;
            var unitPositions=InventoryUnits.Positions(p,car,multiDefinition.id,unitRole);
            trace.Assert(newSet!=null&&usedPart!=null&&usedUnit?.unit?.element!=null&&
                InventoryUnits.Roles(p,car,multiDefinition).Contains(unitRole),
                "mixed fixture derives real set, dealer part, dealer unit and modeled unit role from the catalog");
            if(newSet==null||usedPart==null||usedUnit?.unit?.element==null)return;

            PartOperations.Buy(p,p.catalog,carId,newSet.id,false);
            var sale=newSet==null?null:p.parts.LastOrDefault(item=>item.definitionId==newSet.id&&item.location==PartLocation.Inventory);
            PartOperations.Buy(p,p.catalog,carId,newSet.id,false);
            var move=p.parts.LastOrDefault(item=>item.definitionId==newSet.id&&item.location==PartLocation.Inventory&&item.instanceId!=sale?.instanceId);
            trace.Assert(sale!=null&&move!=null&&MechanicsOwnership.Validate(p,p.catalog)==null,
                "mixed fixture obtains two actual loose parts through authoritative purchase and remains globally valid");
            if(sale==null||move==null)return;

            var rows=new List<PartCartLine>
            {
                new PartCartLine{action=CartAction.SellPart,assetId=sale.instanceId,definitionId=sale.definitionId},
                new PartCartLine{action=CartAction.NewSet,definitionId=newSet.id,quantity=1},
                new PartCartLine{action=CartAction.NewUnits,definitionId=multiDefinition.id,quantity=1,role=unitRole},
                new PartCartLine{action=CartAction.BuyUsed,definitionId=usedPart.DefinitionId,quantity=1,assetId=usedPart.Id},
                new PartCartLine{action=CartAction.BuyUsed,definitionId=usedUnit.DefinitionId,quantity=1,assetId=usedUnit.Id,role=usedUnit.unit.element.role},
                new PartCartLine{action=CartAction.Move,definitionId=move.definitionId,quantity=1,assetId=move.instanceId,
                    destination=InventoryKind.Storage,destinationId=SharedInventory.Storage}
            };
            trace.Add("INPUT|mixed|car=selected;nowTicks="+CartPreviewCharacterizationFixtures.FixedNowTicks+
                ";order="+DescribeRows(rows,p));
            var policy=ValuationPolicy.Defaults();long now=CartPreviewCharacterizationFixtures.FixedNowTicks;
            if(unitPositions.Length>0)
            {
                var installLine=new PartCartLine{action=CartAction.NewUnits,definitionId=multiDefinition.id,quantity=1,role=unitRole,
                    install=true,positions=new List<string>{unitPositions[0]}};
                CompareTotal(trace,"new-unit-install-position",p,carId,new[]{installLine},policy,now);
            }
            CompareTotal(trace,"mixed-row-order",p,carId,rows,policy,now);
            CompareFee(trace,"mixed-delivery-fee",p,carId,rows);
            ComparePreview(trace,"mixed-preview-and-apply",p,car,rows);
            trace.Add("MANUAL_CONTRACT|mixed|order=sale,new-set,new-units,used-part,used-unit,move;PreviewProfile.Apply skips sale;move is last;sale quote uses fixed wholesale clock;existing source IDs remain literal;newphysicalIDs=role-bijective.");
            trace.Add("CATALOG_SHAPE|set="+newSet.id+";multi="+multiDefinition.id+";role="+unitRole+
                ";positions="+string.Join(",",unitPositions)+";stock="+StockShape(p,newSet)+
                ";dealerPart="+usedPart.Id+";dealerUnit="+usedUnit.Id+";sale="+PhysicalRole(p,sale.instanceId)+";move="+PhysicalRole(p,move.instanceId));
        }

        static void RunManualErrorVectors(Trace trace)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var p,out var carId,FixtureSeed+1);
            var car=p.cars.Find(item=>item.instanceId==carId);if(car==null)return;
            var newSet=FindOffer(p,car,PartCategory.HeadIntake,unit:false);if(newSet==null){trace.Assert(false,"error vectors have an authored compatible HeadIntake offer");return;}
            long now=CartPreviewCharacterizationFixtures.FixedNowTicks;var policy=ValuationPolicy.Defaults();
            CompareTotal(trace,"null-source",p,carId,null,policy,now,typeof(ArgumentNullException).FullName,null);
            CompareTotal(trace,"empty-rows",p,carId,new PartCartLine[0],policy,now,typeof(InvalidOperationException).FullName,"Cart needs 1–100 distinct offers.");
            CompareTotal(trace,"null-row",p,carId,new PartCartLine[]{null},policy,now,typeof(InvalidOperationException).FullName,"Invalid cart action or quantity.");
            CompareTotal(trace,"quantity-zero",p,carId,new[]{new PartCartLine{action=CartAction.NewSet,definitionId=newSet.id,quantity=0}},policy,now,
                typeof(InvalidOperationException).FullName,"Invalid cart action or quantity.");
            CompareTotal(trace,"quantity-100",p,carId,new[]{new PartCartLine{action=CartAction.NewSet,definitionId=newSet.id,quantity=100}},policy,now,
                typeof(InvalidOperationException).FullName,"Invalid cart action or quantity.");
            var repeated=new[]{new PartCartLine{action=CartAction.NewSet,definitionId=newSet.id},new PartCartLine{action=CartAction.NewSet,definitionId=newSet.id}};
            CompareTotal(trace,"duplicate-new-offer",p,carId,repeated,policy,now,typeof(InvalidOperationException).FullName,"Duplicate new offer.");
            var quantityFirst=new[]{new PartCartLine{action=CartAction.NewSet,definitionId=newSet.id,quantity=100},repeated[0]};
            CompareTotal(trace,"ordered-first-quantity-before-later-duplicate",p,carId,quantityFirst,policy,now,
                typeof(InvalidOperationException).FullName,"Invalid cart action or quantity.");
            var unknownCar=CompareTotal(trace,"target-availability-precedes-row-source",p,"missing-car",null,policy,now,
                typeof(InvalidOperationException).FullName,"Service vehicle no longer exists.");
            trace.Assert(unknownCar.ExceptionMessage=="Service vehicle no longer exists.","manual first-error order: missing target car rejects before null row-source materialization");
            trace.Add("MANUAL_ERROR_VECTORS|expected=target-before-materialization;line-order;quantity-before-later-duplicate;exact-message-captured;oracle-copy-is-not-the-only-expectation.");
        }

        static void RunHundredLineBoundary(Trace trace)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var p,out var carId,FixtureSeed+2);
            var car=p.cars.Find(item=>item.instanceId==carId);if(car==null)return;
            var eligible=p.catalog.parts.Where(definition=>definition!=null&&definition.price>=0)
                .OrderBy(definition=>definition.price).ThenBy(definition=>definition.id,StringComparer.Ordinal)
                .GroupBy(definition=>definition.id,StringComparer.Ordinal).Select(group=>group.First()).Take(100).ToArray();
            trace.Assert(eligible.Length==100,"authored fixture provides 100 distinct valid offer IDs without shrinking the maximum-line matrix / found="+eligible.Length);
            if(eligible.Length!=100)return;
            var rows=eligible.Select(definition=>new PartCartLine{action=CartAction.NewSet,definitionId=definition.id,quantity=1}).ToArray();
            var hundred=CompareTotal(trace,"100-distinct-row-boundary",p,carId,rows,ValuationPolicy.Defaults(),CartPreviewCharacterizationFixtures.FixedNowTicks);
            trace.Assert(!hundred.Threw,"manual maximum-row contract: 100 distinct valid rows are accepted / "+hundred.Text);
            trace.Add("LINE_BOUNDARY|rows=100;distinctOffers="+eligible.Length+";first="+eligible[0].id+";last="+eligible[eligible.Length-1].id);
            var newSet=FindOffer(p,car,PartCategory.HeadIntake,unit:false);
            if(newSet!=null)
            {
                var quantity99=CompareTotal(trace,"quantity-99-boundary",p,carId,new[]{new PartCartLine{action=CartAction.NewSet,definitionId=newSet.id,quantity=99}},
                    ValuationPolicy.Defaults(),CartPreviewCharacterizationFixtures.FixedNowTicks);
                trace.Assert(!quantity99.Threw,"manual row contract: quantity 99 is within the supported quantity range / "+quantity99.Text);
            }
        }

        static void RunReservationLifetime(Trace trace)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var p,out var carId,FixtureSeed+3);
            var car=p.cars.Find(item=>item.instanceId==carId);var offer=car==null?null:FindOffer(p,car,PartCategory.HeadIntake,false);
            if(car==null||offer==null){trace.Assert(false,"reservation fixture has an actual new set offer");return;}
            var line=new PartCartLine{action=CartAction.NewSet,definitionId=offer.id,quantity=1};
            CartReservations.Capture(p,carId,new[]{line});
            int expires=p.inventory.cart.expiresRun;bool throughNine=true;
            for(int i=1;i<=CartReservations.LifetimeRaces;i++)
            {
                VehicleListings.AfterRace(p,p.catalog);
                bool active=CartReservations.Active(p);
                trace.Add("RESERVATION_BOUNDARY|completedRuns="+p.vehicleStock.completedRuns+";generation="+
                    p.inventory.market.generation+";active="+active+";expiresRun="+expires+";supply="+
                    string.Join(",",p.inventory.cart?.supply?.Select(item=>item.definitionId+":"+item.units+"@"+item.generation)??Enumerable.Empty<string>()));
                if(!active)throughNine=false;
            }
            trace.Assert(throughNine&&CartReservations.Active(p)&&p.vehicleStock.completedRuns==expires,
                "cart reservation remains active through its ninth completed race and third market refresh");
            VehicleListings.AfterRace(p,p.catalog);
            trace.Assert(!CartReservations.Active(p),"cart reservation expires only after the completion following race nine");
        }

        static void RunEnsureHealingTrace(Trace trace)
        {
            CartPreviewCharacterizationFixtures.CreateNormalized(out var p,out var carId,FixtureSeed+4);
            var car=p.cars.Find(item=>item.instanceId==carId);if(car==null){trace.Assert(false,"Ensure divergence fixture has its selected car");return;}
            var d=FindOffer(p,car,PartCategory.HeadIntake,false);if(d==null){trace.Assert(false,"Ensure divergence fixture has an actual compatible set offer");return;}
            var row=new PartCartLine{action=CartAction.NewSet,definitionId=d.id};
            car.fluids=null;
            string malformedBefore=SourceSnapshot(p);var random=UnityEngine.Random.state;
            Outcome output=Observe(()=>LegacyCartPreviewOracle.PreviewProfile(p,car,new[]{row}));
            string sourceAfter=SourceSnapshot(p);
            var healed=output.Value as PlayerProfile;var healedCar=healed?.cars?.Find(item=>item.instanceId==carId);
            trace.Assert(healedCar!=null&&healedCar.fluids!=null,"legacy preview Ensure heals null car fluids on its private copy / "+output.Text);
            trace.Assert(sourceAfter==malformedBefore&&UnityEngine.Random.state.Equals(random),
                "legacy preview isolation preserves deliberately incomplete null-fluid source graph");
            if(healed!=null)
                trace.Add("ENSURE_HEAL_DIVERGENCE|nullCarFluids=true;resultFluids="+(healedCar.fluids==null?"null":"materialized")+
                    ";resultFuel="+(healedCar.fuel==null?"null":"present")+
                    ";sourceUnchanged="+(sourceAfter==malformedBefore));
            else trace.Add("ENSURE_HEAL_DIVERGENCE|outcome="+output.Text+";sourceUnchanged="+(sourceAfter==malformedBefore));
            trace.Add("ENSURE_HEAL_DIVERGENCE|OPEN|Only null fluids are mutated here; full legacy-healing divergence set remains pending.");
        }

        static bool TryPreparePair(Trace trace,string name,PlayerProfile source,IEnumerable<PartCartLine> rows,out InputPair pair)
        {
            pair=null;string profileBefore,catalogBefore;
            try{profileBefore=SourceSnapshot(source);catalogBefore=CatalogSnapshot(source);}
            catch(Exception error)
            {trace.Assert(false,"Literal source snapshot completed / "+name+" / "+error.GetType().FullName+" | "+error.Message);trace.Add("PAIR_SKIP|"+name+"|source snapshot unsupported");return false;}
            if(FixtureBaselines.TryGetValue(source,out string expectedBaseline)&&expectedBaseline!=profileBefore)
            {
                trace.Assert(false,"Independent fixture remains at its original pre-call state / "+name);
                trace.Add("PAIR_SKIP|"+name+"|source fixture changed in an earlier operation; dependent comparison skipped");return false;
            }
            if(!FixtureBaselines.ContainsKey(source))FixtureBaselines.Add(source,profileBefore);
            PartCartLine[] template,actualRows,legacyRows;PlayerProfile actual,legacy;
            var random=UnityEngine.Random.state;
            try
            {
                template=CopyRows(rows);actualRows=CopyRows(template);legacyRows=CopyRows(template);
                actual=CartPreviewLiteralTestClone.Copy(source);legacy=CartPreviewLiteralTestClone.Copy(source);
            }
            catch(Exception error)
            {
                trace.Assert(false,"Literal clone preflight completed / "+name+" / "+error.GetType().FullName+" | "+error.Message);
                AssertOriginalAfterClone(trace,name,source,profileBefore,catalogBefore,random,"clone exception");
                trace.Add("PAIR_SKIP|"+name+"|unsupported or failed literal copy");return false;
            }
            bool sourceUntouched=AssertOriginalAfterClone(trace,name,source,profileBefore,catalogBefore,random,"clone success");
            if(!sourceUntouched){trace.Add("PAIR_SKIP|"+name+"|literal cloning changed source profile, catalog, or random state");return false;}
            string actualSnapshot,legacySnapshot,rowsBefore;bool valid;
            try
            {
                actualSnapshot=SourceSnapshot(actual);legacySnapshot=SourceSnapshot(legacy);rowsBefore=RowsSnapshot(template);
                bool rowArraysDistinct=template==null?actualRows==null&&legacyRows==null:
                    actualRows!=null&&legacyRows!=null&&!ReferenceEquals(actualRows,legacyRows);
                valid=!ReferenceEquals(source,actual)&&!ReferenceEquals(source,legacy)&&!ReferenceEquals(actual,legacy)&&
                    ReferenceEquals(actual.catalog,source.catalog)&&ReferenceEquals(legacy.catalog,source.catalog)&&
                    profileBefore==actualSnapshot&&profileBefore==legacySnapshot&&catalogBefore==CatalogSnapshot(actual)&&
                    catalogBefore==CatalogSnapshot(legacy)&&
                    !ReferenceEquals(actual.parts,source.parts)&&!ReferenceEquals(legacy.parts,source.parts)&&
                    !ReferenceEquals(actual.inventory,source.inventory)&&!ReferenceEquals(legacy.inventory,source.inventory)&&
                    !ReferenceEquals(actual.cars,source.cars)&&!ReferenceEquals(legacy.cars,source.cars)&&
                    rowArraysDistinct&&RowsSnapshot(actualRows)==rowsBefore&&RowsSnapshot(legacyRows)==rowsBefore&&
                    random.Equals(UnityEngine.Random.state)&&CloneRelationshipsMatch(source,actual)&&CloneRelationshipsMatch(source,legacy);
            }
            catch(Exception error)
            {trace.Assert(false,"Literal clone value/topology preflight completed / "+name+" / "+error.GetType().FullName+" | "+error.Message);AssertOriginalAfterClone(trace,name,source,profileBefore,catalogBefore,random,"preflight exception");trace.Add("PAIR_SKIP|"+name+"|literal clone comparison unsupported");return false;}
            bool originalStillUntouched=AssertOriginalAfterClone(trace,name,source,profileBefore,catalogBefore,random,"preflight completion");
            valid=valid&&originalStillUntouched;
            trace.Assert(valid,"literal-preserving independent actual/oracle input copies and rows / "+name);
            if(!valid){trace.Add("PAIR_SKIP|"+name+"|clone values, aliases, shared-catalog contract, row values, or graph separation did not match");return false;}
            pair=new InputPair{Actual=actual,Legacy=legacy,ActualRows=actualRows,LegacyRows=legacyRows,
                ProfileBefore=profileBefore,CatalogBefore=catalogBefore,RowsBefore=rowsBefore};
            return true;
        }

        static bool AssertOriginalAfterClone(Trace trace,string name,PlayerProfile source,string profileBefore,string catalogBefore,
            UnityEngine.Random.State random,string stage)
        {
            try
            {
                bool unchanged=profileBefore==SourceSnapshot(source)&&catalogBefore==CatalogSnapshot(source)&&random.Equals(UnityEngine.Random.state);
                trace.Assert(unchanged,"Literal copy leaves original profile/catalog/random unchanged / "+name+" / "+stage);
                return unchanged;
            }
            catch(Exception error)
            {
                trace.Assert(false,"Original state could be rechecked after literal copy / "+name+" / "+stage+" / "+error.GetType().FullName+" | "+error.Message);
                return false;
            }
        }

        static bool CloneRelationshipsMatch(PlayerProfile source,PlayerProfile copy)
        {
            if(!ReferenceEquals(source.catalog,copy.catalog))return false;
            return CloneTopologyMatches(source,copy,new Dictionary<object,object>(new ObjectReferenceComparer()),
                new Dictionary<object,object>(new ObjectReferenceComparer()));
        }

        static bool CloneTopologyMatches(object source,object copy,Dictionary<object,object> forward,Dictionary<object,object> reverse)
        {
            if(ReferenceEquals(source,null)||ReferenceEquals(copy,null))return ReferenceEquals(source,copy);
            var type=source.GetType();if(copy.GetType()!=type)return false;
            if(type.IsPrimitive||type.IsEnum||type==typeof(string)||type==typeof(decimal)||type==typeof(DateTime)||type==typeof(DateTimeOffset)||type==typeof(TimeSpan)||type==typeof(Guid))
                return Equals(source,copy);
            if(source is MechanicalCatalog)return ReferenceEquals(source,copy);
            if(source is UnityEngine.Object)return ReferenceEquals(source,copy);
            if(type.IsValueType)
            {
                foreach(var field in SerializedFields(type))if(!CloneTopologyMatches(field.GetValue(source),field.GetValue(copy),forward,reverse))return false;
                return true;
            }
            if(ReferenceEquals(source,copy))return false;
            if(forward.TryGetValue(source,out var known))return ReferenceEquals(known,copy);
            if(reverse.TryGetValue(copy,out var prior)&&!ReferenceEquals(prior,source))return false;
            forward.Add(source,copy);reverse[copy]=source;
            if(source is Array sourceArray&&copy is Array copyArray)
            {
                if(sourceArray.Rank!=copyArray.Rank||sourceArray.Length!=copyArray.Length)return false;
                for(int dimension=0;dimension<sourceArray.Rank;dimension++)
                    if(sourceArray.GetLength(dimension)!=copyArray.GetLength(dimension)||sourceArray.GetLowerBound(dimension)!=copyArray.GetLowerBound(dimension))return false;
                var left=sourceArray.GetEnumerator();var right=copyArray.GetEnumerator();
                while(left.MoveNext())if(!right.MoveNext()||!CloneTopologyMatches(left.Current,right.Current,forward,reverse))return false;
                return !right.MoveNext();
            }
            if(source is System.Collections.IList sourceList&&copy is System.Collections.IList copyList)
            {
                if(sourceList.Count!=copyList.Count)return false;
                for(int i=0;i<sourceList.Count;i++)if(!CloneTopologyMatches(sourceList[i],copyList[i],forward,reverse))return false;
                return true;
            }
            foreach(var field in SerializedFields(type))if(!CloneTopologyMatches(field.GetValue(source),field.GetValue(copy),forward,reverse))return false;
            return true;
        }

        static PartCartLine[] CopyRows(IEnumerable<PartCartLine> rows)
        {
            if(rows==null)return null;
            var source=rows.ToList();var copy=new PartCartLine[source.Count];
            for(int i=0;i<source.Count;i++)
            {
                var line=source[i];copy[i]=line==null?null:new PartCartLine{action=line.action,definitionId=line.definitionId,quantity=line.quantity,
                    install=line.install,preparedBlock=line.preparedBlock,preparationOwner=line.preparationOwner,assetId=line.assetId,role=line.role,
                    destinationId=line.destinationId,destination=line.destination,positions=line.positions==null?null:new List<string>(line.positions)};
            }
            return copy;
        }

        static string RowsSnapshot(PartCartLine[] rows)=>TypedCanonical(rows,new Dictionary<string,string>());

        static Outcome CompareTotal(Trace trace,string name,PlayerProfile p,string carId,IEnumerable<PartCartLine> rows,
            ValuationPolicy policy,long now,string expectedType=null,string expectedMessage=null)
        {
            if(!TryPreparePair(trace,name,p,rows,out var pair))return new Outcome{ExceptionType=typeof(InvalidOperationException).FullName,ExceptionMessage="Literal input copies unavailable; see PAIR_SKIP."};
            var random=UnityEngine.Random.state;
            var actual=Measure(()=>PartCart.Total(pair.Actual,carId,pair.ActualRows,policy,now));
            string actualAfter=SourceSnapshot(pair.Actual),actualCatalogAfter=CatalogSnapshot(pair.Actual),baselineAfterActual=SourceSnapshot(p);
            bool actualChanged=pair.ProfileBefore!=actualAfter,actualCatalogChanged=pair.CatalogBefore!=actualCatalogAfter;
            bool actualRandomChanged=!random.Equals(UnityEngine.Random.state);
            trace.Assert(!actualChanged,"Production Total input graph remains unchanged / "+name+" changed="+actualChanged);
            trace.Assert(!actualRandomChanged,"Production Total preserves Unity random state / "+name);
            if(actualChanged)trace.SourceDifference(name+"-actual",pair.ProfileBefore,actualAfter,actualAfter);
            if(actualCatalogChanged||pair.ProfileBefore!=baselineAfterActual)
            {
                trace.Assert(false,"Production Total leaves the shared catalog and baseline fixture unchanged / "+name);
                trace.Add("PAIR_SKIP|Total|"+name+"|legacy skipped because production changed shared catalog or baseline fixture");
                return actual;
            }
            if(pair.ProfileBefore!=SourceSnapshot(pair.Legacy)||pair.CatalogBefore!=CatalogSnapshot(pair.Legacy))
            {
                trace.Assert(false,"Frozen Total input remains isolated before its call / "+name);
                trace.Add("PAIR_SKIP|Total|"+name+"|legacy skipped because its literal baseline changed before call");
                return actual;
            }
            UnityEngine.Random.state=random;
            var legacy=Measure(()=>LegacyCartPreviewOracle.Total(pair.Legacy,carId,pair.LegacyRows,policy,now));
            string legacyAfter=SourceSnapshot(pair.Legacy),legacyCatalogAfter=CatalogSnapshot(pair.Legacy),baselineAfterLegacy=SourceSnapshot(p);
            bool legacyChanged=pair.ProfileBefore!=legacyAfter,legacyCatalogChanged=pair.CatalogBefore!=legacyCatalogAfter;
            bool legacyRandomChanged=!random.Equals(UnityEngine.Random.state);
            UnityEngine.Random.state=random;
            trace.Assert(!legacyChanged,"Frozen Total input graph remains unchanged / "+name+" changed="+legacyChanged);
            trace.Assert(!legacyRandomChanged,"Frozen Total preserves Unity random state / "+name);
            if(legacyChanged)trace.SourceDifference(name+"-legacy",pair.ProfileBefore,legacyAfter,legacyAfter);
            bool rowsChanged=pair.RowsBefore!=RowsSnapshot(pair.ActualRows)||pair.RowsBefore!=RowsSnapshot(pair.LegacyRows);
            trace.Assert(!rowsChanged,"Total input rows remain unchanged on both independent calls / "+name);
            bool same=actual.Text==legacy.Text;
            bool invariant=!actualChanged&&!legacyChanged&&!actualCatalogChanged&&!legacyCatalogChanged&&!actualRandomChanged&&!legacyRandomChanged&&
                !rowsChanged&&pair.ProfileBefore==baselineAfterActual&&pair.ProfileBefore==baselineAfterLegacy;
            if(actualCatalogChanged||legacyCatalogChanged||pair.ProfileBefore!=baselineAfterLegacy)
                trace.Assert(false,"Total comparison leaves shared catalog and baseline fixture unchanged / "+name);
            bool expected=expectedType==null||actual.ExceptionType==expectedType&&(expectedMessage==null||actual.ExceptionMessage==expectedMessage);
            trace.Assert(same&&invariant&&expected,"Total bridge/manual parity / "+name+" actual="+actual.Text+" oracle="+legacy.Text+
                " sourceUnchanged="+invariant+(expectedType==null?"":" expected="+expectedType+" | "+expectedMessage));
            trace.Add("TOTAL|"+name+"|actual="+actual.Text+"|legacy="+legacy.Text+"|same="+same+
                "|sourceUnchanged="+invariant+"|rows="+DescribeRows(rows,p));
            trace.Add("METHOD|Total."+name+"|ticks="+actual.ValueTicks+";allocatedBytes="+actual.AllocatedBytes);
            return actual;
        }

        static Outcome CompareFee(Trace trace,string name,PlayerProfile p,string carId,IEnumerable<PartCartLine> rows)
        {
            if(!TryPreparePair(trace,name,p,rows,out var pair))return new Outcome{ExceptionType=typeof(InvalidOperationException).FullName,ExceptionMessage="Literal input copies unavailable; see PAIR_SKIP."};
            var random=UnityEngine.Random.state;
            var actual=Measure(()=>PartCart.DeliveryFee(pair.Actual,carId,pair.ActualRows));
            string after=SourceSnapshot(pair.Actual);bool actualChanged=pair.ProfileBefore!=after;
            bool actualRandomChanged=!random.Equals(UnityEngine.Random.state);
            trace.Assert(!actualChanged,"Production DeliveryFee input graph remains unchanged / "+name);
            trace.Assert(!actualRandomChanged,"Production DeliveryFee preserves Unity random state / "+name);
            if(actualChanged)trace.SourceDifference(name+"-actual-fee",pair.ProfileBefore,after,after);
            if(pair.CatalogBefore!=CatalogSnapshot(pair.Actual)||pair.ProfileBefore!=SourceSnapshot(p))
            {trace.Assert(false,"Production DeliveryFee leaves shared catalog and baseline fixture unchanged / "+name);trace.Add("PAIR_SKIP|DeliveryFee|"+name+"|legacy skipped after shared-state mutation");return actual;}
            UnityEngine.Random.state=random;
            var legacy=Measure(()=>LegacyCartPreviewOracle.DeliveryFee(pair.Legacy,carId,pair.LegacyRows));
            string legacyAfter=SourceSnapshot(pair.Legacy);bool legacyChanged=pair.ProfileBefore!=legacyAfter;
            bool legacyRandomChanged=!random.Equals(UnityEngine.Random.state);UnityEngine.Random.state=random;
            trace.Assert(!legacyChanged,"Frozen DeliveryFee input graph remains unchanged / "+name);
            if(legacyChanged)trace.SourceDifference(name+"-legacy-fee",pair.ProfileBefore,legacyAfter,legacyAfter);
            bool legacyCatalogChanged=pair.CatalogBefore!=CatalogSnapshot(pair.Legacy);
            trace.Assert(!legacyCatalogChanged&&pair.ProfileBefore==SourceSnapshot(p),"Frozen DeliveryFee leaves shared catalog and baseline fixture unchanged / "+name);
            bool rowsChanged=pair.RowsBefore!=RowsSnapshot(pair.ActualRows)||pair.RowsBefore!=RowsSnapshot(pair.LegacyRows);
            trace.Assert(!rowsChanged,"DeliveryFee input rows remain unchanged on both independent calls / "+name);
            bool same=actual.Text==legacy.Text;bool invariant=!actualChanged&&!legacyChanged&&!legacyCatalogChanged&&!actualRandomChanged&&!legacyRandomChanged&&!rowsChanged&&pair.ProfileBefore==SourceSnapshot(p);
            trace.Assert(same&&invariant,"DeliveryFee bridge parity/source immutability / "+name+" actual="+actual.Text+" oracle="+legacy.Text);
            trace.Add("DELIVERY_FEE|"+name+"|actual="+actual.Text+"|legacy="+legacy.Text+"|sourceUnchanged="+invariant);
            trace.Add("METHOD|DeliveryFee."+name+"|ticks="+actual.ValueTicks+";allocatedBytes="+actual.AllocatedBytes);
            return actual;
        }

        static void ComparePreview(Trace trace,string name,PlayerProfile p,OwnedCar car,IEnumerable<PartCartLine> rows)
        {
            if(!TryPreparePair(trace,name,p,rows,out var pair))return;
            var random=UnityEngine.Random.state;
            var actualCar=pair.Actual.cars.Find(item=>item.instanceId==car.instanceId);
            var legacyCar=pair.Legacy.cars.Find(item=>item.instanceId==car.instanceId);
            if(actualCar==null||legacyCar==null){trace.Assert(false,"Literal preview copies retain the requested car / "+name);trace.Add("PAIR_SKIP|PreviewProfile|"+name+"|car lookup failed");return;}
            var actual=Measure(()=>PartCart.PreviewProfile(pair.Actual,actualCar,pair.ActualRows));
            string actualInputAfter=SourceSnapshot(pair.Actual);bool actualChanged=pair.ProfileBefore!=actualInputAfter;
            bool actualRandomChanged=!random.Equals(UnityEngine.Random.state);
            trace.Assert(!actualChanged,"Production PreviewProfile input graph remains unchanged / "+name);
            trace.Assert(!actualRandomChanged,"Production PreviewProfile preserves Unity random state / "+name);
            if(actualChanged)trace.SourceDifference(name+"-actual-preview",pair.ProfileBefore,actualInputAfter,actualInputAfter);
            if(pair.CatalogBefore!=CatalogSnapshot(pair.Actual)||pair.ProfileBefore!=SourceSnapshot(p))
            {trace.Assert(false,"Production PreviewProfile leaves shared catalog and baseline fixture unchanged / "+name);trace.Add("PAIR_SKIP|PreviewProfile|"+name+"|legacy skipped after shared-state mutation");return;}
            UnityEngine.Random.state=random;
            var legacy=Measure(()=>LegacyCartPreviewOracle.PreviewProfile(pair.Legacy,legacyCar,pair.LegacyRows));
            string legacyInputAfter=SourceSnapshot(pair.Legacy);bool legacyChanged=pair.ProfileBefore!=legacyInputAfter;
            bool legacyRandomChanged=!random.Equals(UnityEngine.Random.state);UnityEngine.Random.state=random;
            trace.Assert(!legacyChanged,"Frozen PreviewProfile input graph remains unchanged / "+name);
            if(legacyChanged)trace.SourceDifference(name+"-legacy-preview",pair.ProfileBefore,legacyInputAfter,legacyInputAfter);
            bool legacyCatalogChanged=pair.CatalogBefore!=CatalogSnapshot(pair.Legacy);
            trace.Assert(!legacyCatalogChanged&&pair.ProfileBefore==SourceSnapshot(p),"Frozen PreviewProfile leaves shared catalog and baseline fixture unchanged / "+name);
            bool rowsChanged=pair.RowsBefore!=RowsSnapshot(pair.ActualRows)||pair.RowsBefore!=RowsSnapshot(pair.LegacyRows);
            trace.Assert(!rowsChanged,"PreviewProfile input rows remain unchanged on both independent calls / "+name);
            bool unchanged=!actualChanged&&!legacyChanged&&!legacyCatalogChanged&&!actualRandomChanged&&!legacyRandomChanged&&!rowsChanged&&pair.ProfileBefore==SourceSnapshot(p);
            bool equivalent=actual.Threw==legacy.Threw&&
                (actual.Threw?actual.ExceptionType==legacy.ExceptionType&&actual.ExceptionMessage==legacy.ExceptionMessage:
                    CanonicalGraphEquivalent((PlayerProfile)actual.Value,(PlayerProfile)legacy.Value,p,trace,name));
            trace.Assert(unchanged,"preview source graph/catalogue/random state remains byte-for-byte unchanged / "+name);
            trace.Assert(equivalent,"PreviewProfile frozen oracle parity with operation/member-role ID bijection / "+name+
                " actual="+actual.Text+" oracle="+legacy.Text);
            trace.Add("PREVIEW|"+name+"|actual="+actual.Text+"|legacy="+legacy.Text+"|normalizedGraph="+equivalent+
                "|sourceUnchanged="+unchanged);
            trace.Add("METHOD|PreviewProfile."+name+"|ticks="+actual.ValueTicks+";allocatedBytes="+actual.AllocatedBytes);

            var actualApply=LegacyApplyPair(p,car.instanceId,rows,trace,name);
            trace.Assert(actualApply,"ordered Apply output parity/source immutability / "+name);
        }

        static bool LegacyApplyPair(PlayerProfile source,string carId,IEnumerable<PartCartLine> rows,Trace trace,string name)
        {
            if(!TryPreparePair(trace,name+".apply",source,rows,out var pair))return false;
            var random=UnityEngine.Random.state;
            var actual=Measure(()=>{PartCart.Apply(pair.Actual,carId,pair.ActualRows,true,ValuationPolicy.Defaults());return "applied";});
            bool actualRandomChanged=!random.Equals(UnityEngine.Random.state);
            trace.Assert(!actualRandomChanged,"Production Apply preserves Unity random state / "+name);
            if(pair.CatalogBefore!=CatalogSnapshot(pair.Actual)||pair.ProfileBefore!=SourceSnapshot(source))
            {trace.Assert(false,"Production Apply leaves shared catalog and baseline fixture unchanged / "+name);trace.Add("PAIR_SKIP|Apply|"+name+"|legacy skipped after shared-state mutation");return false;}
            UnityEngine.Random.state=random;
            var legacy=Measure(()=>{LegacyCartPreviewOracle.Apply(pair.Legacy,carId,pair.LegacyRows,true,ValuationPolicy.Defaults());return "applied";});
            bool legacyRandomChanged=!random.Equals(UnityEngine.Random.state);UnityEngine.Random.state=random;
            trace.Assert(!legacyRandomChanged,"Frozen Apply preserves Unity random state / "+name);
            bool catalogChanged=pair.CatalogBefore!=CatalogSnapshot(pair.Legacy);
            trace.Assert(!catalogChanged&&pair.ProfileBefore==SourceSnapshot(source),"Frozen Apply leaves shared catalog and baseline fixture unchanged / "+name);
            bool rowsChanged=pair.RowsBefore!=RowsSnapshot(pair.ActualRows)||pair.RowsBefore!=RowsSnapshot(pair.LegacyRows);
            trace.Assert(!rowsChanged,"Apply input rows remain unchanged on both independent calls / "+name);
            bool same=!actual.Threw&&!legacy.Threw&&CanonicalGraphEquivalent(pair.Actual,pair.Legacy,source,trace,name+".apply");
            trace.Add("APPLY|"+name+"|actual="+actual.Text+"|legacy="+legacy.Text+"|same="+same+
                "|sourceUnchanged="+(pair.ProfileBefore==SourceSnapshot(source)));
            trace.Add("METHOD|Apply."+name+"|ticks="+actual.ValueTicks+";allocatedBytes="+actual.AllocatedBytes);
            return same&&!actualRandomChanged&&!legacyRandomChanged&&!catalogChanged&&!rowsChanged&&pair.ProfileBefore==SourceSnapshot(source);
        }

        static bool CanonicalGraphEquivalent(PlayerProfile actual,PlayerProfile legacy,PlayerProfile source,Trace trace,string label)
        {
            var sourceIds=PhysicalIds(source);var left=PhysicalIdPaths(actual);var right=PhysicalIdPaths(legacy);
            if(left.Count!=right.Count||left.Keys.Except(right.Keys,StringComparer.Ordinal).Any())return false;
            var leftAliases=new Dictionary<string,string>(StringComparer.Ordinal);var rightAliases=new Dictionary<string,string>(StringComparer.Ordinal);
            var aliasRows=new List<string>();var seenLeft=new HashSet<string>(StringComparer.Ordinal);var seenRight=new HashSet<string>(StringComparer.Ordinal);
            foreach(var pair in left)
            {
                string other=right[pair.Key];bool existing=sourceIds.Contains(pair.Value);
                if(existing)
                {
                    if(pair.Value!=other||!sourceIds.Contains(other))return false;
                    continue;
                }
                if(sourceIds.Contains(other)||!seenLeft.Add(pair.Value)||!seenRight.Add(other))return false;
                string alias="<new:"+pair.Key+">";leftAliases[pair.Value]=alias;rightAliases[other]=alias;
                aliasRows.Add(pair.Key+"="+alias);
            }
            string leftTyped=TypedCanonical(actual,leftAliases),rightTyped=TypedCanonical(legacy,rightAliases);
            bool same=leftTyped==rightTyped;
            trace.Add("ID_BIJECTION|"+label+"|new="+string.Join(",",aliasRows)+"|bijective="+same+
                "|existingIDsLiteral="+left.Count(path=>sourceIds.Contains(path.Value))+"|typedWalker=true");
            return same;
        }

        static void RunTypedWalkerSelfTests(Trace trace)
        {
            double oneDoubleUlp=BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(1d)+1);
            trace.Assert(TypedCanonical(new TypedProbe{precise=1d},new Dictionary<string,string>())!=
                TypedCanonical(new TypedProbe{precise=oneDoubleUlp},new Dictionary<string,string>()),
                "Typed walker preserves a one-ULP double difference");
            var positiveZero=new TypedProbe{scalar=0f,id="guid-like",partInstanceId="guid-like",note="guid-like",order=new List<int>{1,2}};
            var negativeZero=new TypedProbe{scalar=BitConverter.ToSingle(BitConverter.GetBytes(unchecked((int)0x80000000)),0),id="guid-like",partInstanceId="guid-like",note="guid-like",order=new List<int>{1,2}};
            var nextFloat=BitConverter.ToSingle(BitConverter.GetBytes(BitConverter.ToInt32(BitConverter.GetBytes(1f),0)+1),0);
            var one=new TypedProbe{scalar=1f,id="guid-like",partInstanceId="guid-like",note="guid-like",order=new List<int>{1,2}};
            var oneUlp=new TypedProbe{scalar=nextFloat,id="guid-like",partInstanceId="guid-like",note="guid-like",order=new List<int>{1,2}};
            bool signedZero=TypedCanonical(positiveZero,new Dictionary<string,string>(),new List<object>(),"$")!=
                TypedCanonical(negativeZero,new Dictionary<string,string>(),new List<object>(),"$");
            bool adjacentFloats=TypedCanonical(one,new Dictionary<string,string>(),new List<object>(),"$")!=
                TypedCanonical(oneUlp,new Dictionary<string,string>(),new List<object>(),"$");
            string aliased=TypedCanonical(positiveZero,new Dictionary<string,string>{{"guid-like","<physical:test>"}},new List<object>(),"$");
            bool aliasScope=aliased!=TypedCanonical(new TypedProbe{scalar=0f,id="guid-like",partInstanceId="guid-like",note="guid-like",order=new List<int>{1,2}},
                    new Dictionary<string,string>(),new List<object>(),"$")&&aliased.Contains("note<System.String>=System.String{9:guid-like}")&&
                aliased.Contains("id<System.String>=System.String{15:<physical:test>}")&&
                aliased.Contains("partInstanceId<System.String>=System.String{15:<physical:test>}");
            bool orderAndNull=TypedCanonical(new TypedProbe{scalar=0f,id="x",partInstanceId="x",note=null,order=new List<int>{1,2}},new Dictionary<string,string>(),new List<object>(),"$")!=
                TypedCanonical(new TypedProbe{scalar=0f,id="x",partInstanceId="x",note=null,order=new List<int>{2,1}},new Dictionary<string,string>(),new List<object>(),"$");
            bool nullPreserved=TypedCanonical(new TypedProbe{scalar=0f,id="x",partInstanceId="x",note=null,order=null},new Dictionary<string,string>(),new List<object>(),"$")!=
                TypedCanonical(new TypedProbe{scalar=0f,id="x",partInstanceId="x",note=string.Empty,order=new List<int>()},new Dictionary<string,string>(),new List<object>(),"$");
            trace.Assert(signedZero&&adjacentFloats&&aliasScope&&orderAndNull&&nullPreserved,
                "typed canonical self-check distinguishes signed zero/one-ULP/order and only aliases identity-named string fields");
        }

        static string TypedCanonical(object value,Dictionary<string,string> aliases,List<object> ancestors,string fieldName)
        {var output=new StringBuilder();AppendTyped(output,value,aliases,ancestors,fieldName);return output.ToString();}

        static string TypedCanonical(object value,Dictionary<string,string> aliases)
        {return TypedCanonical(value,aliases,new List<object>(),"$");}

        static void AppendTyped(StringBuilder output,object value,Dictionary<string,string> aliases,List<object> ancestors,string fieldName)
        {
            if(value==null){output.Append("null;");return;}
            var type=value.GetType();output.Append(type.FullName).Append('{');
            if(type==typeof(string))
            {
                string text=(string)value;
                if(IdentityFieldNames.Contains(fieldName)&&aliases.TryGetValue(text,out string alias))text=alias;
                output.Append(text.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(text).Append("};");return;
            }
            if(type==typeof(float)){output.Append(BitConverter.ToInt32(BitConverter.GetBytes((float)value),0).ToString("X8",CultureInfo.InvariantCulture)).Append("};");return;}
            if(type==typeof(double)){output.Append(BitConverter.ToInt64(BitConverter.GetBytes((double)value),0).ToString("X16",CultureInfo.InvariantCulture)).Append("};");return;}
            if(type.IsEnum){output.Append(Convert.ToInt64(value,CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)).Append("};");return;}
            if(type.IsPrimitive||type==typeof(decimal))
            {output.Append(Convert.ToString(value,CultureInfo.InvariantCulture)).Append("};");return;}
            if(value is System.Collections.IList list)
            {
                output.Append("count=").Append(list.Count).Append(';');
                for(int i=0;i<list.Count;i++){output.Append('[').Append(i).Append("]=");AppendTyped(output,list[i],aliases,ancestors,i.ToString(CultureInfo.InvariantCulture));}
                output.Append("};");return;
            }
            if(ancestors.Any(item=>ReferenceEquals(item,value))){output.Append("cycle};");return;}
            ancestors.Add(value);
            foreach(var field in SerializedFields(type))
            {
                output.Append(field.Name).Append('<').Append(field.FieldType.FullName).Append(">=");
                AppendTyped(output,field.GetValue(value),aliases,ancestors,field.Name);
            }
            ancestors.RemoveAt(ancestors.Count-1);output.Append("};");
        }

        // Test metadata only: Type/FieldInfo descriptors are immutable in this
        // batch domain. Cache the same ordered field list, never player values.
        static readonly Dictionary<Type,FieldInfo[]> SerializedFieldDescriptors=new Dictionary<Type,FieldInfo[]>();
        static IEnumerable<FieldInfo> SerializedFields(Type type)
        {
            if(SerializedFieldDescriptors.TryGetValue(type,out var cached))return cached;
            var fields=new List<FieldInfo>();
            var hierarchy=new Stack<Type>();for(var current=type;current!=null&&current!=typeof(object);current=current.BaseType)hierarchy.Push(current);
            while(hierarchy.Count>0)
                foreach(var field in hierarchy.Pop().GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly)
                    .OrderBy(item=>item.MetadataToken))
                {
                    if(field.IsStatic||field.IsInitOnly||field.IsNotSerialized)continue;
                    if(!field.IsPublic&&!Attribute.IsDefined(field,typeof(SerializeField))&&!Attribute.IsDefined(field,typeof(SerializeReference)))continue;
                    fields.Add(field);
                }
            var result=fields.ToArray();SerializedFieldDescriptors.Add(type,result);return result;
        }

        static Dictionary<string,string> PhysicalIdPaths(PlayerProfile p)
        {
            var result=new Dictionary<string,string>(StringComparer.Ordinal);
            var partOccurrences=new Dictionary<string,int>(StringComparer.Ordinal);
            for(int i=0;i<(p.parts?.Count??0);i++)
            {
                var part=p.parts[i];if(part==null)continue;string definition=part.definitionId??"<null>";
                partOccurrences.TryGetValue(definition,out int occurrence);partOccurrences[definition]=occurrence+1;
                string role="part/"+definition+"/occ"+occurrence.ToString(CultureInfo.InvariantCulture);
                AddId(result,role+"/instance",part.instanceId);
                var members=part.condition?.elements;
                if(members==null)continue;
                for(int j=0;j<members.Length;j++)if(members[j]!=null)
                    AddId(result,role+"/member/"+j.ToString(CultureInfo.InvariantCulture)+"/"+
                        members[j].GetType().Name+"/"+(members[j].role??"<null>"),members[j].id);
            }
            var unitOccurrences=new Dictionary<string,int>(StringComparer.Ordinal);
            for(int i=0;i<(p.looseEngineElements?.Count??0);i++)
            {
                var unit=p.looseEngineElements[i];if(unit?.element==null)continue;
                string role=(unit.definitionId??"<null>")+"/"+unit.element.GetType().Name+"/"+(unit.element.role??"<null>");
                unitOccurrences.TryGetValue(role,out int occurrence);unitOccurrences[role]=occurrence+1;
                AddId(result,"unit/"+role+"/occ"+occurrence.ToString(CultureInfo.InvariantCulture),unit.element.id);
            }
            var engineOccurrences=new Dictionary<string,int>(StringComparer.Ordinal);
            for(int i=0;i<(p.engines?.Count??0);i++)
            {
                var engine=p.engines[i];if(engine==null)continue;
                string role=(engine.vehicleDefinitionId??"<null>")+"/"+(engine.parentVehicleId??"detached");
                engineOccurrences.TryGetValue(role,out int occurrence);engineOccurrences[role]=occurrence+1;
                AddId(result,"engine/"+role+"/occ"+occurrence.ToString(CultureInfo.InvariantCulture),engine.instanceId);
            }
            for(int i=0;i<(p.inventory?.dealer?.Count??0);i++)
            {
                var stock=p.inventory.dealer[i];if(stock==null)continue;
                string role="dealer/row"+i.ToString(CultureInfo.InvariantCulture)+"/"+(stock.DefinitionId??"<null>");
                AddId(result,role+"/stock",stock.Id);
                if(stock.part!=null)
                {
                    AddId(result,role+"/part",stock.part.instanceId);
                    var members=stock.part.condition?.elements;
                    if(members!=null)for(int j=0;j<members.Length;j++)if(members[j]!=null)
                        AddId(result,role+"/member/"+j.ToString(CultureInfo.InvariantCulture)+"/"+members[j].GetType().Name+"/"+(members[j].role??"<null>"),members[j].id);
                }
                if(stock.unit?.element!=null)AddId(result,role+"/unit/"+stock.unit.element.GetType().Name+"/"+(stock.unit.element.role??"<null>"),stock.unit.element.id);
            }
            return result;
        }

        static HashSet<string> PhysicalIds(PlayerProfile p)=>new HashSet<string>(PhysicalIdPaths(p).Values.Where(id=>!string.IsNullOrEmpty(id)),StringComparer.Ordinal);
        static void AddId(Dictionary<string,string> values,string path,string id){if(!string.IsNullOrEmpty(id))values[path]=id;}

        static Outcome Measure(Func<int> action)
        {
            long bytes=GC.GetAllocatedBytesForCurrentThread();var watch=Stopwatch.StartNew();
            try{return new Outcome{Value=action(),ValueTicks=watch.ElapsedTicks,AllocatedBytes=GC.GetAllocatedBytesForCurrentThread()-bytes};}
            catch(Exception error){return new Outcome{ExceptionType=error.GetType().FullName,ExceptionMessage=error.Message,
                ValueTicks=watch.ElapsedTicks,AllocatedBytes=GC.GetAllocatedBytesForCurrentThread()-bytes};}
        }
        static Outcome Measure(Func<string> action)
        {
            long bytes=GC.GetAllocatedBytesForCurrentThread();var watch=Stopwatch.StartNew();
            try{return new Outcome{Value=action(),ValueTicks=watch.ElapsedTicks,AllocatedBytes=GC.GetAllocatedBytesForCurrentThread()-bytes};}
            catch(Exception error){return new Outcome{ExceptionType=error.GetType().FullName,ExceptionMessage=error.Message,
                ValueTicks=watch.ElapsedTicks,AllocatedBytes=GC.GetAllocatedBytesForCurrentThread()-bytes};}
        }
        static Outcome Measure(Func<PlayerProfile> action)
        {
            long bytes=GC.GetAllocatedBytesForCurrentThread();var watch=Stopwatch.StartNew();
            try{return new Outcome{Value=action(),ValueTicks=watch.ElapsedTicks,AllocatedBytes=GC.GetAllocatedBytesForCurrentThread()-bytes};}
            catch(Exception error){return new Outcome{ExceptionType=error.GetType().FullName,ExceptionMessage=error.Message,
                ValueTicks=watch.ElapsedTicks,AllocatedBytes=GC.GetAllocatedBytesForCurrentThread()-bytes};}
        }
        static Outcome Observe(Func<PlayerProfile> action)
        {try{return new Outcome{Value=action()};}catch(Exception error){return new Outcome{ExceptionType=error.GetType().FullName,ExceptionMessage=error.Message};}}

        static string SourceSnapshot(PlayerProfile p)
            =>TypedCanonical(p,new Dictionary<string,string>())+"\nCATALOG\n"+CatalogSnapshot(p)+
              "\nRAW_SELECTION_IDS\n"+(p.selectedOwnedVehicleId??"<null>")+"/"+(p.selectedVehicleId??"<null>");

        static string CatalogSnapshot(PlayerProfile p)
        {
            var emptyAliases=new Dictionary<string,string>();
            var output=new StringBuilder(TypedCanonical(p.catalog?.parts,emptyAliases));
            if(p.catalog==null)return output.ToString();
            output.Append("\nAUTHORING_REVISION\n").Append(p.catalog.AuthoringRevision.ToString(CultureInfo.InvariantCulture));
            // Spec() returns a legacy copy; read the original authored values so
            // exact float changes in this shared catalog cannot be hidden.
            var field=typeof(MechanicalCatalog).GetField("specs",BindingFlags.NonPublic|BindingFlags.Instance);
            var specs=field?.GetValue(p.catalog) as System.Collections.IDictionary;
            if(specs==null)throw new InvalidOperationException("Characterization cannot observe the authored spec set.");
            foreach(string id in specs.Keys.Cast<string>().OrderBy(value=>value,StringComparer.Ordinal))
                output.Append("\nSPEC:").Append(id.Length).Append(':').Append(id).Append('=')
                    .Append(TypedCanonical(specs[id],emptyAliases));
            return output.ToString();
        }
        static void RecordSourceHashes(Trace trace)
        {
            string root=Path.Combine(Application.dataPath,"DragLab","Scripts");
            foreach(string relative in new[]{
                "Domain/AssemblyOperations.cs",
                "Domain/CartReservations.cs",
                "Domain/EngineElementService.cs",
                "Domain/EngineElements.cs",
                "Domain/InventoryUnits.cs",
                "Domain/MechanicalCatalog.cs",
                "Domain/MechanicsOwnership.cs",
                "Domain/PartCart.cs",
                "Domain/PartOperations.cs",
                "Domain/PartsMarket.cs",
                "Domain/RuntimeDataEncodingProbe.cs",
                "Domain/RuntimeEntropy.cs",
                "Domain/RuntimeEntropy.Diagnostics.cs",
                "Domain/SharedInventory.cs",
                "Domain/Valuation.cs",
                "Domain/VehicleCollection.cs",
                "Domain/VehicleListings.cs",
                "Runtime/CartPreviewCharacterizationFixtures.cs",
                "Runtime/LegacyCartPreviewOracle.cs",
                "Runtime/CartPreviewLiteralTestClone.cs",
                "Runtime/CartPreviewCharacterizationChecks.cs",
                "Runtime/CartPreviewCharacterizationCoreRows.cs",
                "Runtime/CartPreviewCharacterizationIdentity.cs",
                "Runtime/CartPreviewCharacterizationTopology.cs",
                "../Editor/CartPreviewCharacterizationVerification.cs"})
            {
                string path=Path.Combine(root,relative);
                bool exists=File.Exists(path);
                trace.Assert(exists,"Pinned source provenance exists / "+relative);
                trace.Add("SOURCE_SHA256|"+relative+"|"+(exists?HashFile(path):"MISSING"));
            }
        }
        static string HashFile(string path)
        {using(var sha=SHA256.Create())using(var input=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(input)).Replace("-",string.Empty).ToLowerInvariant();}

        static PartDefinition FindOffer(PlayerProfile p,OwnedCar car,PartCategory category,bool unit)
        {
            var spec=p.catalog.Spec(car.vehicleId);
            return p.catalog.parts.Where(definition=>definition!=null&&definition.category==category&&definition.price>=0&&p.catalog.Fits(spec,definition))
                .OrderBy(definition=>definition.price).ThenBy(definition=>definition.id,StringComparer.Ordinal)
                .FirstOrDefault(definition=>unit?InventoryUnits.Roles(p,car,definition).Any():p.inventory.market.parts.Any(stock=>stock.definitions.Contains(definition.id)));
        }
        static string StockShape(PlayerProfile p,PartDefinition d)
        {var row=p.inventory.market.parts.FirstOrDefault(item=>item.definitions.Contains(d.id));return row==null?"missing":
            "units="+row.units+"/"+row.initialUnits+";percent="+row.deliveryPercent+";marketGeneration="+p.inventory.market.generation+";definitions="+string.Join(",",row.definitions);}
        static string DescribeRows(IEnumerable<PartCartLine> rows,PlayerProfile p)
        {
            if(rows==null)return "<null>";
            return string.Join(";",rows.Select((line,index)=>line==null?index+":<null>":index+":"+line.action+"/"+
                (line.definitionId??"-")+"/q"+line.quantity+"/install="+line.install+"/role="+(line.role??"-")+"/asset="+
                PhysicalRole(p,line.assetId)+"/to="+line.destination+":"+(line.destinationId==p.SelectedCar?.instanceId?"selected-car":line.destinationId??"-")));
        }
        static string PhysicalRole(PlayerProfile p,string id)
        {
            if(string.IsNullOrEmpty(id))return "-";
            for(int i=0;i<(p.parts?.Count??0);i++)if(p.parts[i]?.instanceId==id)return "part/"+p.parts[i].definitionId+"/row"+i;
            for(int i=0;i<(p.looseEngineElements?.Count??0);i++)if(p.looseEngineElements[i]?.element?.id==id)return "unit/"+p.looseEngineElements[i].definitionId+"/row"+i;
            for(int i=0;i<(p.inventory?.dealer?.Count??0);i++)if(p.inventory.dealer[i]?.Id==id)return "dealer/row"+i+"/"+p.inventory.dealer[i].DefinitionId;
            for(int i=0;i<(p.engines?.Count??0);i++)if(p.engines[i]?.instanceId==id)return "engine/row"+i+"/"+p.engines[i].vehicleDefinitionId;
            return "external-or-unknown";
        }
        static string StableCarLabel(PlayerProfile p,string id)=>p.cars.FindIndex(car=>car.instanceId==id)==0?"selected-starter":"car-index-"+p.cars.FindIndex(car=>car.instanceId==id);
    }
}
#endif

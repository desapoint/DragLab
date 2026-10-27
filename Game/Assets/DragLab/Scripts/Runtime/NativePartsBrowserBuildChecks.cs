using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;
using UnityEngine;

namespace DragLab.Runtime
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>Managed parity and lifecycle checks for bounded Parts browser builds.</summary>
    public static class NativePartsBrowserBuildChecks
    {
        sealed class FixtureState
        {
            internal PlayerProfile profile;
            internal MechanicalCatalog catalog;
            internal VehicleDefinition vehicle;
            internal string pistonUnitId,tireUnitId,ownedPartId,usedPartId,usedUnitId,usedTireId;
        }

        sealed class ReferenceComparer:IEqualityComparer<object>
        {
            internal static readonly ReferenceComparer Instance=new ReferenceComparer();
            public new bool Equals(object a,object b)=>ReferenceEquals(a,b);
            public int GetHashCode(object value)=>RuntimeHelpers.GetHashCode(value);
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
        struct FloatBits
        {
            [System.Runtime.InteropServices.FieldOffset(0)] internal float value;
            [System.Runtime.InteropServices.FieldOffset(0)] internal int bits;
        }

        readonly struct EncoderCounts
        {
            internal readonly long canonical,keyRequests;
            internal readonly int plan,ownedCar,cartPreview,cacheCar,cacheEntry;
            internal EncoderCounts(bool _)
            {
                canonical=MarketCatalogLayout.CanonicalJsonEncodeCount;
                keyRequests=MarketCatalogLayout.CanonicalKeyRequestCount;
                plan=NativePartBrowser.LegacyPlanJsonCount;
                ownedCar=NativePartBrowser.LegacyOwnedCarJsonCount;
                cartPreview=NativePartBrowser.LegacyCartPreviewCount;
                cacheCar=NativePartPreviewCache.LegacyCarSignatureJsonCount;
                cacheEntry=NativePartPreviewCache.LegacyEntryKeyJsonCount;
            }
            internal bool Same(EncoderCounts other)=>canonical==other.canonical&&keyRequests==other.keyRequests&&plan==other.plan&&
                ownedCar==other.ownedCar&&cartPreview==other.cartPreview&&cacheCar==other.cacheCar&&cacheEntry==other.cacheEntry;
            public override string ToString()=>$"canonical={canonical}, keyRequests={keyRequests}, plan={plan}, ownedCar={ownedCar}, cart={cartPreview}, cacheCar={cacheCar}, cacheEntry={cacheEntry}";
        }

        static EncoderCounts Encoders()=>new EncoderCounts(true);

        static void Check(Action<bool,string> check,bool value,string label)
        {check(value,label);if(!value)throw new InvalidOperationException(label);}

        static FixtureState Fixture()
        {
            var vehicles=Resources.LoadAll<VehicleDefinition>("Data/Vehicles");
            var vehicle=vehicles.Single(v=>v.spec.id=="starter-hatch");
            var catalog=new MechanicalCatalog(vehicles.Select(v=>v.spec));
            var profile=PlayerProfile.New(25102026);
            profile.cars.Clear();profile.selectedOwnedVehicleId="";profile.selectedVehicleId="";
            // Keep vehicle offers isolated from Resources enumeration; this fixture
            // may also be reused with intentionally restricted authored catalogs.
            profile.vehicleStock=new VehicleStockState{version=1,seed=25102026,listings=new List<VehicleOffer>()};
            MechanicsOwnership.Ensure(profile,catalog);
            profile.cash=1000000;profile.reputation=1000;
            VehicleCollection.Buy(profile,catalog,new VehicleOffer{model=vehicle.spec.id,price=1,reputation=0,
                stock=vehicle.CreateStockTune(),paint="#FFFFFF"});
            var car=profile.SelectedCar;

            // A real fitted-spec loose member exercises Inventory's unit path.
            var pistonPack=profile.componentsPart(PartCategory.Pistons);
            string pistonRole=InventoryUnits.Roles(profile,car,catalog.Find(pistonPack.definitionId))[0];
            var pistonUnit=InventoryUnits.Buy(profile,car.instanceId,pistonPack.definitionId,pistonRole);

            // Preserve an actual tire condition and identity shape as a separate
            // used inventory tire; no fresh/healed member is synthesized.
            var tirePack=profile.componentsPart(PartCategory.Tires);
            var tire= tirePack.condition.tires[0].Copy();
            string tireDefinition=TireFitments.DefinitionId(tirePack,0);
            tire.id=MechanicsOwnership.NewId("tire");tire.definitionId=tireDefinition;
            tire.health=.78f;tire.consumedLife=.17;tire.temperatureC=64;tire.distanceM=12800;
            tire.slipEnergyJ=2400;tire.lastContactWorkJ=510;tire.runningSeconds=3300;
            var tireUnit=new LooseEngineElement{definitionId=tireDefinition,storageId=SharedInventory.Garage,
                unitPrice=InventoryPricing.NewUnit(catalog.Find(tireDefinition),4),tireUnit=true,
                element=tire.Copy(),tire=tire};
            profile.looseEngineElements.Add(tireUnit);
            var usedTire=tire.Copy();usedTire.id=MechanicsOwnership.NewId("tire");
            usedTire.health=.63f;usedTire.consumedLife=.29;usedTire.temperatureC=48;usedTire.distanceM=35400;
            usedTire.slipEnergyJ=7100;usedTire.lastContactWorkJ=1820;usedTire.runningSeconds=9420;
            var usedTireUnit=new LooseEngineElement{definitionId=tireDefinition,storageId=SharedInventory.Garage,
                unitPrice=InventoryPricing.NewUnit(catalog.Find(tireDefinition),4),tireUnit=true,
                element=usedTire.Copy(),tire=usedTire};
            profile.inventory.dealer.Add(new DealerStock{isUnit=true,unit=usedTireUnit,paidPrice=58});

            // A used uninstalled part and second used member exercise the Shop
            // used-offer rows independently from the player's owned inventory.
            var fitted=profile.componentsPart(PartCategory.HeadIntake);
            var ownedPart=fitted.Copy();ownedPart.instanceId=MechanicsOwnership.NewId("part");
            ownedPart.location=PartLocation.Inventory;ownedPart.ownerId="";ownedPart.slot="";
            ownedPart.storageId=SharedInventory.Garage;ownedPart.condition.health=.82f;
            ownedPart.condition.lastService="retained owned fixture history";
            foreach(var member in ownedPart.condition.elements??Array.Empty<EngineElement>())member.id=MechanicsOwnership.NewId("element");
            profile.parts.Add(ownedPart);
            var usedPart=fitted.Copy();usedPart.instanceId=MechanicsOwnership.NewId("part");
            usedPart.location=PartLocation.Inventory;usedPart.ownerId="";usedPart.slot="";
            usedPart.storageId=SharedInventory.Garage;usedPart.condition.health=.73f;
            usedPart.condition.lastService="retained used fixture history";
            foreach(var member in usedPart.condition.elements??Array.Empty<EngineElement>())member.id=MechanicsOwnership.NewId("element");
            profile.inventory.dealer.Add(new DealerStock{part=usedPart,paidPrice=73});
            var pistonDefinition=catalog.Find(pistonPack.definitionId);
            var usedMember=EngineElements.Create(pistonDefinition.category);
            usedMember.id=MechanicsOwnership.NewId("element");usedMember.definitionId=pistonPack.definitionId;
            usedMember.role=pistonRole;usedMember.health=.61f;usedMember.cylinder=-1;usedMember.valve=-1;
            var usedUnit=new LooseEngineElement{definitionId=pistonPack.definitionId,storageId=SharedInventory.Garage,
                unitPrice=InventoryPricing.NewUnit(catalog.Find(pistonPack.definitionId),
                    InventoryUnits.Count(profile,car,catalog.Find(pistonPack.definitionId))),element=usedMember};
            profile.inventory.dealer.Add(new DealerStock{isUnit=true,unit=usedUnit,paidPrice=41});

            string validation=MechanicsOwnership.Validate(profile,catalog);
            if(validation!=null)throw new InvalidOperationException("Browser fixture failed mechanics ownership validation: "+validation);
            return new FixtureState{profile=profile,catalog=catalog,vehicle=vehicle,ownedPartId=ownedPart.instanceId,
                pistonUnitId=pistonUnit.element.id,tireUnitId=tireUnit.element.id,
                usedPartId=usedPart.instanceId,usedUnitId=usedMember.id,usedTireId=usedTire.id};
        }

        static NativePartsOwnerContext Owner()=>NativePartsOwnerContext.Capture();

        static int UsedPrice(DealerStock row)=>Math.Max(1,row.paidPrice+19);
        static int SalePrice(string id,bool unit)=>Math.Max(1,(unit?37:59)+Math.Abs((id??"").GetHashCode()%17));

        static NativePartsPriceFact[] Prices(PlayerProfile profile,string page)
        {
            if(page=="Shop")return profile.inventory.dealer.Where(d=>d!=null&&(d.IsPart||d.IsUnit))
                .Select(d=>new NativePartsPriceFact("Used",d.Id,UsedPrice(d))).ToArray();
            if(page!="Inventory")return Array.Empty<NativePartsPriceFact>();
            var car=profile.SelectedCar;var fitted=new HashSet<string>(car.installed.slots.Select(s=>s.partInstanceId),StringComparer.Ordinal);
            var prices=new List<NativePartsPriceFact>();
            foreach(var part in profile.parts.Where(p=>p!=null&&!fitted.Contains(p.instanceId)))
                prices.Add(new NativePartsPriceFact("OwnedPart",part.instanceId,SalePrice(part.instanceId,false)));
            foreach(var unit in profile.looseEngineElements.Where(u=>u?.element!=null))
                prices.Add(new NativePartsPriceFact("OwnedUnit",unit.element.id,SalePrice(unit.element.id,true)));
            return prices.ToArray();
        }

        static NativePartsBaseReadInputs Inputs(PlayerProfile profile,string page,bool planned=false,
            IEnumerable<PartCartLine> lines=null,UnitPreferences units=null,NativePartsOwnerContext owner=null,
            IEnumerable<NativePartsPriceFact> prices=null)
            =>new NativePartsBaseReadInputs(profile,page,planned,lines,units??new UnitPreferences(),
                owner??Owner(),prices??Prices(profile,page));

        static void AppendValue(StringBuilder output,object value,HashSet<object> active)
        {
            if(value==null){output.Append("NULL;");return;}
            var type=value.GetType();
            if(value is string text){output.Append('S').Append(text.Length).Append(':').Append(text).Append(';');return;}
            if(type.IsEnum||type.IsPrimitive||type==typeof(decimal))
            {
                if(type==typeof(float)){var bits=new FloatBits{value=(float)value};output.Append("F32:").Append(bits.bits).Append(';');}
                else if(type==typeof(double))output.Append("F64:").Append(BitConverter.DoubleToInt64Bits((double)value)).Append(';');
                else if(type==typeof(decimal))foreach(int bits in decimal.GetBits((decimal)value))output.Append("D:").Append(bits).Append(';');
                else output.Append(type.FullName).Append(':').Append(Convert.ToString(value,System.Globalization.CultureInfo.InvariantCulture)).Append(';');
                return;
            }
            bool reference=!type.IsValueType;
            if(reference&&!active.Add(value)){output.Append("CYCLE:").Append(type.FullName).Append(';');return;}
            if(value is IList list)
            {
                output.Append("LIST:").Append(type.FullName).Append(':').Append(list.Count).Append('{');
                for(int i=0;i<list.Count;i++)AppendValue(output,list[i],active);
                output.Append("}ENDLIST;");
            }
            else if(typeof(UnityEngine.Object).IsAssignableFrom(type))output.Append("UNITY:").Append(type.FullName).Append(';');
            else
            {
                var fields=new List<FieldInfo>();
                for(var current=type;current!=null&&current!=typeof(object);current=current.BaseType)
                    fields.AddRange(current.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly)
                        .Where(f=>!f.IsNotSerialized));
                output.Append("OBJ:").Append(type.FullName).Append('{');
                foreach(var field in fields.OrderBy(f=>f.DeclaringType.FullName,StringComparer.Ordinal).ThenBy(f=>f.Name,StringComparer.Ordinal))
                {output.Append(field.DeclaringType.FullName).Append('.').Append(field.Name).Append('=');AppendValue(output,field.GetValue(value),active);}
                output.Append("}ENDOBJ;");
            }
            if(reference)active.Remove(value);
        }

        static string TypedOutput(object value)
        {var output=new StringBuilder(2048);AppendValue(output,value,new HashSet<object>(ReferenceComparer.Instance));return output.ToString();}

        static void CompareEntry(NativePartEntry expected,NativePartEntry actual,Action<bool,string> check,string label)
        {
            Check(check,expected!=null&&actual!=null,"Entry exists / "+label);
            Check(check,expected.Id==actual.Id&&expected.AssetId==actual.AssetId&&expected.Source==actual.Source&&
                expected.Location==actual.Location&&expected.Compatibility==actual.Compatibility&&expected.Reason==actual.Reason&&
                expected.Position==actual.Position&&expected.DisplayName==actual.DisplayName&&
                expected.SearchExtra==actual.SearchExtra&&expected.Owned==actual.Owned&&expected.Ready==actual.Ready&&
                expected.RequiresMatchingSet==actual.RequiresMatchingSet&&expected.Price==actual.Price&&
                expected.StockUnits==actual.StockUnits&&expected.RequiresExpress==actual.RequiresExpress,
                "Entry scalar and status parity / "+label);
            Check(check,ReferenceEquals(expected.Definition,actual.Definition)&&ReferenceEquals(expected.Part,actual.Part)&&
                ReferenceEquals(expected.Unit,actual.Unit),"Entry preserves live source identities / "+label);
            Check(check,TypedOutput(expected.FittedBrands)==TypedOutput(actual.FittedBrands)&&
                TypedOutput(expected.Change)==TypedOutput(actual.Change)&&TypedOutput(expected.Proposed)==TypedOutput(actual.Proposed),
                "Entry brands, exact seven deltas and full inspection/curve/warning payload parity / "+label);
        }

        static void CompareModels(NativePartBrowser expected,NativePartBrowser actual,Action<bool,string> check,string page)
        {
            Check(check,expected.Entries.Count==actual.Entries.Count,"Entry count parity / "+page);
            for(int i=0;i<expected.Entries.Count;i++)CompareEntry(expected.Entries[i],actual.Entries[i],check,page+" index="+i);
            Check(check,TypedOutput(expected.Current)==TypedOutput(actual.Current)&&
                TypedOutput(expected.Reference)==TypedOutput(actual.Reference)&&TypedOutput(expected.Plan)==TypedOutput(actual.Plan)&&
                expected.PlanError==actual.PlanError,"Current/reference/plan inspection and warning parity / "+page);
        }

        static void EvaluateAndCompare(NativePartBrowser expected,NativePartBrowser actual,string id,
            Action<bool,string> check,string label)
        {
            var left=expected.Entries.FirstOrDefault(e=>e.Id==id);
            var right=actual.Entries.FirstOrDefault(e=>e.Id==id);
            Check(check,left!=null&&right!=null,"Comparison fixture entry exists / "+label);
            expected.Evaluate(left);
            var encodersBefore=Encoders();
            actual.Evaluate(right);
            var encodersAfter=Encoders();
            Check(check,encodersBefore.Same(encodersAfter),"Converted browser entry evaluation uses no legacy JSON encoder / "+label+
                " / before="+encodersBefore+" after="+encodersAfter);
            CompareEntry(left,right,check,label);
        }

        static NativePartBrowser SynchronousReference(PlayerProfile profile,string page,PartCartLine[] lines,
            bool planned,NativePartPreviewCache cache)
            =>new NativePartBrowser(profile,lines,planned,page,UsedPrice,SalePrice,cache);

        // Test-only independent full fixture copy. Reflection is confined to this
        // opt-in verifier; neither this copier nor a whole profile reaches gameplay
        // previews/saves. JSON is unsuitable here: it materializes null Tune.awd.
        static object CopyOracleFixture(object source,Dictionary<object,object> seen)
        {
            if(source==null)return null;
            var type=source.GetType();
            if(type.IsPrimitive||type.IsEnum||source is string||source is decimal||source is UnityEngine.Object)return source;
            if(seen.TryGetValue(source,out var known))return known;
            if(source is Array array)
            {
                if(array.Rank!=1)throw new InvalidOperationException("Oracle fixture requires an explicit multidimensional array copier.");
                var result=Array.CreateInstance(type.GetElementType(),array.Length);seen.Add(source,result);
                for(int i=0;i<array.Length;i++)result.SetValue(CopyOracleFixture(array.GetValue(i),seen),i);
                return result;
            }
            if(source is IDictionary dictionary)
            {
                var result=(IDictionary)Activator.CreateInstance(type,true);seen.Add(source,result);
                foreach(DictionaryEntry pair in dictionary)result.Add(CopyOracleFixture(pair.Key,seen),CopyOracleFixture(pair.Value,seen));
                return result;
            }
            if(source is IList list)
            {
                var result=(IList)Activator.CreateInstance(type,true);seen.Add(source,result);
                foreach(var item in list)result.Add(CopyOracleFixture(item,seen));
                return result;
            }
            var copy=Activator.CreateInstance(type,true);seen.Add(source,copy);
            for(var current=type;current!=null&&current!=typeof(object);current=current.BaseType)
                foreach(var field in current.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly))
                    if(!field.IsNotSerialized)field.SetValue(copy,CopyOracleFixture(field.GetValue(source),seen));
            return copy;
        }

        // The independent complete input is never a production projection.
        static void CompareUnitWithFullFixtureOracle(PlayerProfile profile,NativePartBrowser model,string entryId,
            Action<bool,string> check,string label)
        {
            var entry=model.Entries.Single(e=>e.Id==entryId);
            Check(check,entry.Unit!=null,"Independent unit oracle has a physical incoming unit / "+label);
            string originalState=TypedOutput(profile);
            var copy=(PlayerProfile)CopyOracleFixture(profile,new Dictionary<object,object>(ReferenceComparer.Instance));
            copy.catalog=profile.catalog;
            Check(check,TypedOutput(copy)==originalState&&(copy.SelectedCar.tune.awd==null)==(profile.SelectedCar.tune.awd==null)&&
                !ReferenceEquals(copy.SelectedCar,profile.SelectedCar)&&!ReferenceEquals(copy.parts[0],profile.parts[0]),
                "Independent full fixture copy preserves exact state, nested nulls and detached owned records / "+label);
            var unit=copy.looseEngineElements.FirstOrDefault(u=>u.element.id==entry.Unit.element.id);
            if(unit==null)
            {
                unit=new LooseEngineElement{definitionId=entry.Unit.definitionId,storageId=entry.Unit.storageId,
                    unitPrice=entry.Unit.unitPrice,tireUnit=entry.Unit.tireUnit,element=entry.Unit.element.Copy()};
                copy.looseEngineElements.Add(unit);
            }
            var before=Encoders();
            entry.Ready=false;entry.Proposed=null;entry.Change=new double?[7];
            model.Evaluate(entry);
            Check(check,before.Same(Encoders()),"Independent unit oracle comparison retains the converted zero-encoder path / "+label);
            Check(check,!entry.RequiresMatchingSet&&entry.Proposed!=null&&!string.IsNullOrEmpty(entry.Position),
                "Independent unit oracle exercises a fitted physical position / "+label);
            InventoryUnits.Install(copy,copy.SelectedCar.instanceId,unit.element.id,entry.Position);
            var expected=NativePartBrowser.Inspect(InspectionBuild.Capture(copy,copy.SelectedCar));
            string expectedOutput=TypedOutput(expected),actualOutput=TypedOutput(entry.Proposed);
            if(expectedOutput!=actualOutput)
            {
                int first=0;while(first<expectedOutput.Length&&first<actualOutput.Length&&expectedOutput[first]==actualOutput[first])first++;
                int start=Math.Max(0,first-240);
                UnityEngine.Debug.Log("UNIT ORACLE FIRST DIFFERENCE "+label+" at "+first+
                    "\nEXPECTED "+expectedOutput.Substring(start,Math.Min(650,expectedOutput.Length-start))+
                    "\nACTUAL "+actualOutput.Substring(start,Math.Min(650,actualOutput.Length-start)));
            }
            Check(check,expectedOutput==actualOutput,
                "Scoped unit projection equals full-fixture authoritative install: complete inspection, curves and warnings / "+label);

            var category=entry.Definition.category;
            var prior=profile.componentsPart(category);
            var priorDefinition=prior==null?null:profile.catalog.Find(prior.definitionId);
            double incoming=entry.Unit.element.health;
            double outgoing=prior==null?0:category==PartCategory.Tires?TireDynamics.LowestHealth(prior.condition):prior.condition.health;
            if(prior!=null)
            {
                if(entry.Unit.tireUnit&&entry.Position.StartsWith("tire:",StringComparison.Ordinal))
                    outgoing=TireDynamics.RemainingHealth(prior.condition.tires[int.Parse(entry.Position.Substring(5))]);
                else outgoing=prior.condition.elements.FirstOrDefault(e=>e.id==entry.Position)?.health??outgoing;
            }
            var changes=new double?[7];
            changes[0]=expected.peakHp-model.Reference.peakHp;
            changes[1]=expected.peakTorque-model.Reference.peakTorque;
            changes[2]=expected.vehicleMassKg-model.Reference.vehicleMassKg;
            if(PartSemantics.Wears(category))changes[3]=(incoming-outgoing)*100;
            if(PartSemantics.TorqueRated(category)&&priorDefinition!=null)
                changes[4]=entry.Definition.torqueLimitNm-priorDefinition.torqueLimitNm;
            if(PartSemantics.EngineStrengthRated(category)&&priorDefinition!=null)
                changes[5]=entry.Definition.rpmLimit-priorDefinition.rpmLimit;
            changes[6]=expected.radiatorWattsPerC-model.Reference.radiatorWattsPerC;
            Check(check,TypedOutput(changes)==TypedOutput(entry.Change),
                "All seven changes equal the full-fixture authoritative unit oracle / "+label);
            Check(check,TypedOutput(profile)==originalState,
                "Unit preview and independent oracle leave every live fixture field unchanged / "+label);
        }

        static NativePartBrowserBuild CompleteBuild(PlayerProfile profile,string page,PartCartLine[] lines,
            bool planned,Action<bool,string> check,string label,out double elapsed,out int steps,out int largestStep,int itemCap=32)
        {
            UnityEngine.Debug.Log("Bounded browser check: "+label+" / item cap "+itemCap);
            var owner=Owner();var facts=Prices(profile,page);
            var encodersBefore=Encoders();
            var builder=NativePartBrowserBuild.Begin(Inputs(profile,page,planned,lines,new UnitPreferences(),owner,facts),
                new NativePartPreviewCache());
            var clock=Stopwatch.StartNew();steps=0;largestStep=0;int prior=builder.PreparationItems;
            while(!builder.Complete&&!builder.Failed)
            {
                if(++steps>100000)throw new InvalidOperationException("Browser build failed to terminate: "+label);
                bool capturing=builder.State.ToString()=="Capturing";
                int captureBefore=builder.CaptureItems;
                int before=builder.PreparationItems;
                builder.Advance(Inputs(profile,page,planned,lines,new UnitPreferences(),owner,facts),itemCap,2.0);
                int delta=builder.PreparationItems-before;
                if(delta<0||delta>itemCap)throw new InvalidOperationException("Browser item cap exceeded: "+label+" / "+delta);
                int captured=builder.CaptureItems-captureBefore;
                if(capturing&&(captured<0||captured>itemCap))throw new InvalidOperationException("Browser capture item cap exceeded: "+label+" / "+captured);
                if(builder.PreparationItems<prior)throw new InvalidOperationException("Browser progress moved backwards: "+label);
                prior=builder.PreparationItems;largestStep=Math.Max(largestStep,delta);
                Check(check,builder.Complete||builder.Model==null,"Pending browser work has no published model / "+label);
                if(builder.Failed)break;
            }
            clock.Stop();elapsed=clock.Elapsed.TotalMilliseconds;
            Check(check,builder.Complete&&!builder.Failed&&builder.Model!=null,
                "Fresh bounded browser build completes with a model / "+label+" / "+builder.FailureReason);
            Check(check,builder.ItemsPrepared>0||steps==0,"Completed build reports bounded work / "+label);
            var encodersAfter=Encoders();
            Check(check,encodersBefore.Same(encodersAfter),"Converted bounded browser build uses no legacy JSON encoder / "+label+
                " / before="+encodersBefore+" after="+encodersAfter);
            return builder;
        }

        static NativePartBrowserBuild BeginShop(PlayerProfile profile)
            =>NativePartBrowserBuild.Begin(Inputs(profile,"Shop",false,Array.Empty<PartCartLine>(),
                new UnitPreferences(),Owner(),Prices(profile,"Shop")),new NativePartPreviewCache());

        static NativePartBrowserBuild HoldUntilPrepared(PlayerProfile profile,Action<bool,string> check,string label)
        {
            var builder=BeginShop(profile);int calls=0;
            while((builder.State.ToString()=="Capturing"||builder.ItemsPrepared==0)&&!builder.Failed)
            {
                if(++calls>100000)throw new InvalidOperationException("Browser never reached a real held preparation step: "+label);
                builder.Advance(Inputs(profile,"Shop",false,Array.Empty<PartCartLine>(),
                    new UnitPreferences(),Owner(),Prices(profile,"Shop")),1,2.0);
                Check(check,!builder.Complete||builder.Model!=null,
                    "A completed model is visible only after its final fence / "+label);
                Check(check,builder.Complete||builder.Model==null,
                    "A held or pending builder does not expose a model / "+label);
            }
            Check(check,!builder.Failed&&builder.State.ToString()=="Preparing"&&builder.ItemsPrepared>0&&builder.Model==null,
                "Stopped between calls after actual model preparation starts / "+label);
            return builder;
        }

        static NativePartBrowserBuild HoldAtState(PlayerProfile profile,NativePartBrowserBuildState target,Action<bool,string> check)
        {
            var builder=BeginShop(profile);int calls=0;
            while(builder.State!=target&&!builder.Failed&&!builder.Complete)
            {
                if(++calls>100000)throw new InvalidOperationException("Browser never reached phase "+target+".");
                builder.Advance(Inputs(profile,"Shop",false,Array.Empty<PartCartLine>(),new UnitPreferences(),Owner(),Prices(profile,"Shop")),8,2.0);
            }
            Check(check,!builder.Failed&&!builder.Complete&&builder.State==target&&builder.Model==null&&
                builder.CompletedReadSet==null&&builder.CacheBinding==null,
                "Requested pending phase keeps all outputs private / "+target);
            return builder;
        }

        static void SetupPhaseFences(PlayerProfile profile,Action<bool,string> check)
        {
            var sequence=BeginShop(profile);int captureCalls=0;
            while(sequence.State==NativePartBrowserBuildState.Capturing&&!sequence.Failed)
            {
                if(++captureCalls>100000)throw new InvalidOperationException("Capture did not yield into typed binding.");
                sequence.Advance(Inputs(profile,"Shop",false,Array.Empty<PartCartLine>(),new UnitPreferences(),Owner(),Prices(profile,"Shop")),32,2.0);
            }
            Check(check,sequence.State==NativePartBrowserBuildState.Binding&&sequence.Model==null,"Capture completion yields exactly into Binding");
            sequence.Advance(Inputs(profile,"Shop",false,Array.Empty<PartCartLine>(),new UnitPreferences(),Owner(),Prices(profile,"Shop")),32,2.0);
            Check(check,sequence.State==NativePartBrowserBuildState.SettingUp&&sequence.Model==null,"Typed binding yields exactly into SettingUp");
            sequence.Advance(Inputs(profile,"Shop",false,Array.Empty<PartCartLine>(),new UnitPreferences(),Owner(),Prices(profile,"Shop")),32,2.0);
            Check(check,sequence.State==NativePartBrowserBuildState.ValidatingSetup&&sequence.Model==null,"Constructor work yields exactly into ValidatingSetup");
            sequence.Advance(Inputs(profile,"Shop",false,Array.Empty<PartCartLine>(),new UnitPreferences(),Owner(),Prices(profile,"Shop")),32,2.0);
            Check(check,sequence.State==NativePartBrowserBuildState.Preparing&&sequence.Model==null,"Post-setup fence yields exactly into Preparing");
            int prepareCalls=0;
            while(sequence.State==NativePartBrowserBuildState.Preparing&&!sequence.Failed)
            {
                if(++prepareCalls>100000)throw new InvalidOperationException("Bounded preparation did not reach Publishing.");
                sequence.Advance(Inputs(profile,"Shop",false,Array.Empty<PartCartLine>(),new UnitPreferences(),Owner(),Prices(profile,"Shop")),64,2.0);
                Check(check,sequence.Complete||sequence.Model==null,"Preparation output stays private through its yielded chunks");
            }
            Check(check,sequence.State==NativePartBrowserBuildState.Publishing&&sequence.Model==null,"Finished preparation yields exactly into Publishing");
            sequence.Advance(Inputs(profile,"Shop",false,Array.Empty<PartCartLine>(),new UnitPreferences(),Owner(),Prices(profile,"Shop")),64,2.0);
            Check(check,sequence.Complete&&sequence.Model!=null,"Publication fence is the only transition that exposes the model");
            var exactReference=SynchronousReference(profile,"Shop",Array.Empty<PartCartLine>(),false,new NativePartPreviewCache());
            CompareModels(exactReference,sequence.Model,check,"six-phase Shop build source parity");

            int boundaryIndex=0;var fuel=profile.SelectedCar.fuel;
            foreach(var boundary in new[]{NativePartBrowserBuildState.Binding,NativePartBrowserBuildState.SettingUp,
                NativePartBrowserBuildState.ValidatingSetup,NativePartBrowserBuildState.Preparing,NativePartBrowserBuildState.Publishing})
            {
                bool editCash=(boundaryIndex++%2)==0;int originalCash=profile.cash;double originalFuel=fuel.litres;
                var builder=HoldAtState(profile,boundary,check);int priorPrepared=builder.ItemsPrepared;
                try
                {
                    if(editCash)profile.cash=checked(originalCash+1);else fuel.litres=originalFuel+.5;
                    builder.Advance(Inputs(profile,"Shop",false,Array.Empty<PartCartLine>(),new UnitPreferences(),Owner(),Prices(profile,"Shop")),1,2.0);
                    Check(check,builder.Failed&&builder.InputsChanged&&builder.State==NativePartBrowserBuildState.Failed&&builder.Model==null&&
                        builder.CompletedReadSet==null&&builder.CacheBinding==null&&builder.ItemsPrepared==priorPrepared,
                        "Same-revision A-to-B "+(editCash?"cash":"physical fuel")+" mutation at "+boundary+" is rejected at its next full fence before further work or publication");
                }
                finally{profile.cash=originalCash;fuel.litres=originalFuel;}
                builder.Advance(Inputs(profile,"Shop",false,Array.Empty<PartCartLine>(),new UnitPreferences(),Owner(),Prices(profile,"Shop")),1,2.0);
                Check(check,builder.Failed&&builder.Model==null,"Restoring A after "+boundary+" failure cannot revive private work");
            }
        }

        static void ObservedMutationRejectsAndRetry(PlayerProfile profile,Action<bool,string> check,string label,
            Action mutate,Action restore)
        {
            var builder=HoldUntilPrepared(profile,check,label);
            mutate();
            try
            {
                builder.Advance(Inputs(profile,"Shop",false,Array.Empty<PartCartLine>(),
                    new UnitPreferences(),Owner(),Prices(profile,"Shop")),1,2.0);
                Check(check,builder.Failed&&builder.InputsChanged&&builder.State.ToString()=="Failed"&&builder.Model==null&&
                    !string.IsNullOrEmpty(builder.FailureReason),
                    "Observed same-revision change permanently rejects held private work / "+label);
            }
            finally{restore();}
            builder.Advance(Inputs(profile,"Shop",false,Array.Empty<PartCartLine>(),
                new UnitPreferences(),Owner(),Prices(profile,"Shop")),1,2.0);
            Check(check,builder.Failed&&builder.Model==null,
                "Restoring A after observing B cannot revive a failed builder / "+label);
            var retry=CompleteBuild(profile,"Shop",Array.Empty<PartCartLine>(),false,check,"fresh retry after "+label,
                out _,out _,out _);
            Check(check,retry.Complete&&retry.Model!=null,"Fresh builder recovers latest state after "+label);
        }

        static void CapturedBThenRestoreARejects(PlayerProfile profile,Action<bool,string> check)
        {
            var fuel=profile.SelectedCar.fuel;double original=fuel.litres;fuel.litres=original+.75;
            NativePartBrowserBuild builder;
            try{builder=BeginShop(profile);}
            finally{fuel.litres=original;}
            int calls=0;
            while(!builder.Complete&&!builder.Failed)
            {
                if(++calls>100000)throw new InvalidOperationException("Capture of B snapshot failed to terminate.");
                builder.Advance(Inputs(profile,"Shop",false,Array.Empty<PartCartLine>(),
                    new UnitPreferences(),Owner(),Prices(profile,"Shop")),8,2.0);
                Check(check,builder.Complete||builder.Model==null,"B snapshot remains private until finish fence");
            }
            Check(check,builder.Failed&&builder.InputsChanged&&builder.Model==null&&!string.IsNullOrEmpty(builder.FailureReason),
                "Facts captured while B was present reject after A is restored before final publication");
            var retry=CompleteBuild(profile,"Shop",Array.Empty<PartCartLine>(),false,check,
                "capture-B restore-A retry",out _,out _,out _);
            Check(check,retry.Complete,"A fresh capture after the B mismatch succeeds");
        }

        static string BuildTiming(NativePartBrowserBuild builder,string page,int steps,double wall)
            =>$"page={page} state={builder.State} captureItems={builder.CaptureItems}/{builder.CaptureTotal} visibleEntries={builder.ItemsPrepared} preparationItems={builder.PreparationItems} calls={steps} capture={builder.CaptureMilliseconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)}ms captureMax={builder.LargestCaptureStepMilliseconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)}ms validation={builder.ValidationMilliseconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)}ms validationMax={builder.LargestValidationMilliseconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)}ms modelSetup={builder.ModelSetupMilliseconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)}ms prep={builder.PreparationMilliseconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)}ms prepMax={builder.LargestPreparationStepMilliseconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)}ms publishValidation={builder.PublishValidationMilliseconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)}ms wall={wall.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)}ms; descriptive only, wall includes check callbacks, no threshold";

        static NewPartStock FindHeldAliasFixture(PlayerProfile profile,Action<bool,string> check,out string alias)
        {
            var row=profile.inventory.market.parts.FirstOrDefault(s=>s!=null&&s.units>1&&s.definitions!=null&&s.definitions.Count>1);
            Check(check,row!=null,"Fixture has a stocked multi-alias market row for held-supply coverage");
            alias=row.definitions.First(id=>profile.catalog.Find(id)!=null);
            return row;
        }

        static void SetHeldAliasFixture(PlayerProfile profile,string alias,Action<bool,string> check)
        {
            profile.inventory.cart=new CartReservationState{version=1,createdRun=profile.vehicleStock.completedRuns,
                expiresRun=profile.vehicleStock.completedRuns+CartReservations.LifetimeRaces,
                generation=profile.inventory.market.generation,carId=profile.SelectedCar.instanceId,
                lines=new List<PartCartLine>{new PartCartLine{action=CartAction.NewUnits,definitionId=alias,quantity=1}},
                supply=new List<ReservedPartSupply>{new ReservedPartSupply{definitionId=alias,units=1,
                    generation=profile.inventory.market.generation}}};
            profile.inventory.usingCartReservation=false;
            Check(check,CartReservations.Active(profile)&&CartReservations.Held(profile,profile.catalog.Find(alias))==1,
                "Alias reservation is live and held by the selected product group");
        }

        public static void RunUnitProjectionOracle(Action<bool,string> check,string fixtureDirectory)
        {
            var fixture=Fixture();
            foreach(var unused in MarketCatalogLayout.WarmSteps(fixture.catalog)){}
            var builder=CompleteBuild(fixture.profile,"Shop",Array.Empty<PartCartLine>(),false,check,
                "focused independent unit oracle",out _,out _,out _);
            CompareUnitWithFullFixtureOracle(fixture.profile,builder.Model,"used:"+fixture.usedUnitId,check,"focused dealer engine member");
            CompareUnitWithFullFixtureOracle(fixture.profile,builder.Model,"used:"+fixture.usedTireId,check,"focused dealer tire");
        }

        public static void RunReadyConstructorOracle(Action<bool,string> check,string fixtureDirectory)
        {
            var fixture=Fixture();var profile=fixture.profile;
            foreach(var unused in MarketCatalogLayout.WarmSteps(fixture.catalog)){}
            Check(check,MarketCatalogLayout.TryCaptureReadyContentToken(fixture.catalog,out var token)&&token.MatchesCurrent(fixture.catalog),
                "Ready constructor oracle borrows validated authored groups");
            var group=token.ShopGroups.First(g=>g.DefinitionIds.Count>=3);
            var a=fixture.catalog.Find(group.DefinitionIds[0]);
            var b=fixture.catalog.Find(group.DefinitionIds[1]);
            var c=fixture.catalog.Find(group.DefinitionIds[2]);
            var absent=fixture.catalog.Find(token.ShopGroups.First(g=>!ReferenceEquals(g,group)).DefinitionIds[0]);
            var definitions=new[]{a,b,c,absent};
            var priorMarket=profile.inventory.market;var priorCart=profile.inventory.cart;
            int priorGeneration=profile.vehicleStock.generation,priorRuns=profile.vehicleStock.completedRuns;
            string beforeProfile=TypedOutput(profile);
            try
            {
                profile.vehicleStock.generation=1;profile.vehicleStock.completedRuns=3;
                var first=new NewPartStock{definitions=new List<string>{a.id},units=5,initialUnits=5,deliveryPercent=5};
                var direct=new NewPartStock{definitions=new List<string>{b.id},units=7,initialUnits=7,deliveryPercent=10};
                profile.inventory.market=new PartsStockState{version=2,seed=priorMarket.seed,generation=1,
                    guaranteeAtRefresh=priorMarket.guaranteeAtRefresh,parts=new List<NewPartStock>{first,direct}};
                profile.inventory.cart=new CartReservationState{version=1,carId=profile.SelectedCar.instanceId,
                    createdRun=3,expiresRun=3+CartReservations.LifetimeRaces,generation=1,
                    lines=new List<PartCartLine>{new PartCartLine{action=CartAction.NewUnits,definitionId=b.id,quantity=3}},
                    supply=new List<ReservedPartSupply>{new ReservedPartSupply{definitionId=a.id,units=1,generation=0},
                        new ReservedPartSupply{definitionId=b.id,units=2,generation=1}}};
                Check(check,CartReservations.Active(profile),"Ready constructor oracle has an actual active mixed-generation hold");
                foreach(bool includeReserved in new[]{false,true})
                {
                    // The independent public constructor may warm its legacy index;
                    // reset it before the exact constructor under test and counters.
                    profile.inventory.market.index=null;
                    var legacy=new PartsMarket.Ledger(profile,includeReserved);
                    var legacyAvailable=definitions.Select(legacy.Available).ToArray();
                    var legacyQuotes=definitions.Select(d=>legacy.Quote(d,3,125,2)).ToArray();
                    profile.inventory.market.index=null;
                    string heldState=TypedOutput(profile.inventory.cart);
                    var encoders=Encoders();
                    var ready=new PartsMarket.Ledger(profile,includeReserved,token.ShopGroups,token.ShopGroupById);
                    var available=definitions.Select(ready.Available).ToArray();
                    var quotes=definitions.Select(d=>ready.Quote(d,3,125,2)).ToArray();
                    var currentHeld=definitions.Select(d=>ready.Held(d,true)).ToArray();
                    var allHeld=definitions.Select(d=>ready.Held(d)).ToArray();
                    Check(check,encoders.Same(Encoders())&&profile.inventory.market.index==null,
                        "Ready constructor and every lookup keep a cold market index and use zero key/encoder calls / reserved="+includeReserved);
                    int[] expectedAvailable=includeReserved?new[]{6,7,6,0}:new[]{5,5,5,0};
                    int[] expectedCost=includeReserved?new[]{250,250,250,276}:new[]{253,255,253,276};
                    int[] expectedLocal=includeReserved?new[]{6,6,6,0}:new[]{5,5,5,0};
                    for(int i=0;i<definitions.Length;i++)
                    {
                        var actual=quotes[i];var expected=legacyQuotes[i];
                        Check(check,available[i]==legacyAvailable[i]&&available[i]==expectedAvailable[i],
                            "Ready-before-holds exact quantity, direct-ID precedence, first-alias fallback or missing stock / reserved="+includeReserved+" index="+i);
                        Check(check,actual.cost==expected.cost&&actual.fee==expected.fee&&actual.localUnits==expected.localUnits&&
                            actual.expressUnits==expected.expressUnits&&actual.cost==expectedCost[i]&&
                            actual.fee==expectedCost[i]-250&&actual.localUnits==expectedLocal[i]&&actual.expressUnits==6-expectedLocal[i],
                            "Ready-before-holds quote equals legacy and independent fixed arithmetic / reserved="+includeReserved+" index="+i);
                    }
                    Check(check,TypedOutput(currentHeld)==TypedOutput(new[]{0,2,0,0})&&TypedOutput(allHeld)==TypedOutput(new[]{1,2,1,0}),
                        "Ready constructor filters current holds and retains prior-generation holds exactly / reserved="+includeReserved);
                    Check(check,first.units==5&&direct.units==7&&TypedOutput(profile.inventory.cart)==heldState,
                        "Ready constructor quotes never consume stock or reservations / reserved="+includeReserved);
                }
            }
            finally
            {
                profile.inventory.market=priorMarket;profile.inventory.cart=priorCart;
                profile.vehicleStock.generation=priorGeneration;profile.vehicleStock.completedRuns=priorRuns;
            }
            Check(check,TypedOutput(profile)==beforeProfile,"Ready constructor oracle restores the exact isolated fixture");
            int cash=profile.cash;
            ObservedMutationRejectsAndRetry(profile,check,"focused one-item held preparation",
                ()=>profile.cash=cash+1,()=>profile.cash=cash);
        }

        public static void Run(Action<bool,string> check,string fixtureDirectory)
        {
            if(check==null)throw new ArgumentNullException(nameof(check));
            if(string.IsNullOrWhiteSpace(fixtureDirectory))throw new ArgumentException("Isolated fixture directory is required.",nameof(fixtureDirectory));
            var fixture=Fixture();var profile=fixture.profile;var catalog=fixture.catalog;
            foreach(var unused in MarketCatalogLayout.WarmSteps(catalog)){}
            foreach(string page in new[]{"Shop","Inventory","Installed"})
            {
                var lines=Array.Empty<PartCartLine>();
                var oracleCache=new NativePartPreviewCache();
                var expected=SynchronousReference(profile,page,lines,false,oracleCache);
                var builder=CompleteBuild(profile,page,lines,false,check,"empty cart / "+page,
                    out double elapsed,out int steps,out int largestStep,page=="Shop"?1:32);
                CompareModels(expected,builder.Model,check,page);
                if(page=="Shop")
                {
                    EvaluateAndCompare(expected,builder.Model,"used:"+fixture.usedUnitId,check,"used engine member comparison / Shop");
                    EvaluateAndCompare(expected,builder.Model,"used:"+fixture.usedTireId,check,"used tire comparison / Shop");
                    CompareUnitWithFullFixtureOracle(profile,builder.Model,"used:"+fixture.usedUnitId,check,"dealer engine member");
                    CompareUnitWithFullFixtureOracle(profile,builder.Model,"used:"+fixture.usedTireId,check,"dealer retained-condition tire");
                    var newPiston=expected.Entries.First(e=>e.Source=="New"&&e.Definition.category==PartCategory.Pistons);
                    EvaluateAndCompare(expected,builder.Model,newPiston.Id,check,"new fitted-spec comparison / Shop");
                }
                else if(page=="Inventory")
                {
                    EvaluateAndCompare(expected,builder.Model,fixture.pistonUnitId,check,"owned engine member comparison / Inventory");
                    EvaluateAndCompare(expected,builder.Model,fixture.tireUnitId,check,"owned used tire comparison / Inventory");
                    EvaluateAndCompare(expected,builder.Model,fixture.ownedPartId,check,"owned part sale comparison / Inventory");
                    CompareUnitWithFullFixtureOracle(profile,builder.Model,fixture.pistonUnitId,check,"owned engine member");
                    CompareUnitWithFullFixtureOracle(profile,builder.Model,fixture.tireUnitId,check,"owned retained-condition tire");
                }
                else
                {
                    var fitted=expected.Entries.First(e=>e.Source=="Installed"&&e.Definition.category==PartCategory.Pistons);
                    EvaluateAndCompare(expected,builder.Model,fitted.Id,check,"installed physical part comparison / Installed");
                }
                check(true,"Bounded build timing / "+BuildTiming(builder,page,steps,elapsed)+
                    " entries="+builder.Model.Entries.Count+" max-visible-entries-per-call="+largestStep);
            }

            var plannedReference=SynchronousReference(profile,"Shop",Array.Empty<PartCartLine>(),true,new NativePartPreviewCache());
            var plannedBuild=CompleteBuild(profile,"Shop",Array.Empty<PartCartLine>(),true,check,
                "empty cart planned reference / Shop",out _,out _,out _);
            CompareModels(plannedReference,plannedBuild.Model,check,"Shop empty cart planned reference");
            var modeFence=HoldUntilPrepared(profile,check,"planned reference mode");
            modeFence.Advance(Inputs(profile,"Shop",true,Array.Empty<PartCartLine>(),
                new UnitPreferences(),Owner(),Prices(profile,"Shop")),1,2.0);
            Check(check,modeFence.Failed&&modeFence.Model==null,
                "Changing planned/current mode rejects held browser preparation");

            SetupPhaseFences(profile,check);

            int cash=profile.cash;
            ObservedMutationRejectsAndRetry(profile,check,"same-revision cash A-B-A",
                ()=>profile.cash=cash+1,()=>profile.cash=cash);
            var marketRow=profile.inventory.market.parts.First(row=>row!=null);
            int marketUnits=marketRow.units;
            ObservedMutationRejectsAndRetry(profile,check,"direct market units A-B-A",
                ()=>marketRow.units=marketUnits+1,()=>marketRow.units=marketUnits);
            var authored=catalog.parts[0];int authoredPrice=authored.price;
            ObservedMutationRejectsAndRetry(profile,check,"direct authored price A-B-A",
                ()=>authored.price=authoredPrice+1,()=>authored.price=authoredPrice);
            CapturedBThenRestoreARejects(profile,check);

            var checkpoint=new RunCheckpoint{settled=false};profile.runCheckpoints.Add(checkpoint);
            var unsettledReference=SynchronousReference(profile,"Inventory",Array.Empty<PartCartLine>(),false,new NativePartPreviewCache());
            var unsettledBuild=CompleteBuild(profile,"Inventory",Array.Empty<PartCartLine>(),false,check,
                "unsettled run blocks unit preview / Inventory",out _,out _,out _);
            CompareModels(unsettledReference,unsettledBuild.Model,check,"Inventory with unsettled run");
            string runBlockedProfile=TypedOutput(profile);
            EvaluateAndCompare(unsettledReference,unsettledBuild.Model,fixture.pistonUnitId,check,
                "unsettled run blocks unit preview / Inventory");
            var blockedUnit=unsettledBuild.Model.Entries.First(e=>e.Id==fixture.pistonUnitId);
            Check(check,blockedUnit.Proposed==null&&blockedUnit.Reason.Contains("Close the active run")&&
                TypedOutput(profile)==runBlockedProfile,
                "Unsettled run forbids typed used-unit preview without healing or changing any live profile data");
            var staleCheckpoint=HoldUntilPrepared(profile,check,"run checkpoint settled scope");
            // This is a same-revision mutation of the only run-checkpoint field
            // consumed by the exact typed unit-install preview guard.
            checkpoint.settled=true;
            try
            {
                staleCheckpoint.Advance(Inputs(profile,"Shop",false,Array.Empty<PartCartLine>(),
                    new UnitPreferences(),Owner(),Prices(profile,"Shop")),1,2.0);
                Check(check,staleCheckpoint.Failed&&staleCheckpoint.Model==null,
                    "Changing the held-run checkpoint settled value rejects browser work");
            }
            finally{checkpoint.settled=false;profile.runCheckpoints.Remove(checkpoint);}

            var holdRow=FindHeldAliasFixture(profile,check,out _);
            int availableBeforeHold=holdRow.units;
            var noHoldReference=SynchronousReference(profile,"Shop",Array.Empty<PartCartLine>(),false,new NativePartPreviewCache());
            var noHoldBuild=CompleteBuild(profile,"Shop",Array.Empty<PartCartLine>(),false,check,
                "empty cart with independent active hold / Shop",out _,out _,out _);
            CompareModels(noHoldReference,noHoldBuild.Model,check,"Shop active alias hold");
            var beforeHoldEntry=noHoldBuild.Model.Entries.FirstOrDefault(e=>e.Source=="New"&&holdRow.definitions.Contains(e.Definition.id));
            Check(check,beforeHoldEntry!=null&&holdRow.definitions.Any(id=>id!=beforeHoldEntry.Definition.id),
                "Held-stock fixture has a distinct alias from the live product representative");
            string heldAlias=holdRow.definitions.First(id=>id!=beforeHoldEntry.Definition.id);
            var priorHold=profile.inventory.cart;bool priorUsingReservation=profile.inventory.usingCartReservation;
            IReadOnlyList<MarketCatalogLayout.ReadyProductGroup> readyGroups;
            IReadOnlyDictionary<string,MarketCatalogLayout.ReadyProductGroup> readyGroupById;
            Check(check,MarketCatalogLayout.TryReadyShopProductGroups(catalog,out readyGroups,out readyGroupById),
                "Fixture has a complete immutable ready Shop alias snapshot");
            var invalidBindProbe=new PartsMarket.Ledger(profile,false);
            try
            {
                SetHeldAliasFixture(profile,heldAlias,check);
                var holdReference=SynchronousReference(profile,"Shop",Array.Empty<PartCartLine>(),false,new NativePartPreviewCache());
                var savedMarketIndex=profile.inventory.market.index;
                try
                {
                    // The public held-stock oracle above may have warmed this
                    // legacy index. Clear it only during the converted path so
                    // the ready-ledger test proves it needs no fallback lookup.
                    profile.inventory.market.index=null;
                    var noKeyBefore=Encoders();bool rejectedPartialReady=false;
                    try{_ = new PartsMarket.Ledger(profile,false,readyGroups,null);}
                    catch(InvalidOperationException){rejectedPartialReady=true;}
                    bool rejectedInvalidBind=!invalidBindProbe.BindReadyProductGroups(null,readyGroupById);
                    var noKeyAfter=Encoders();
                    Check(check,rejectedPartialReady&&rejectedInvalidBind&&noKeyBefore.Same(noKeyAfter)&&profile.inventory.market.index==null,
                        "Partial ready-ledger arguments and invalid binding fail closed without key lookup, encoding or market-index mutation");
                    var holdBuild=CompleteBuild(profile,"Shop",Array.Empty<PartCartLine>(),false,check,
                        "empty cart with active alias hold / cold legacy index / Shop",out _,out _,out _);
                    Check(check,profile.inventory.market.index==null,
                        "Ready alias binding resolves active holds without warming the legacy market index");
                    CompareModels(holdReference,holdBuild.Model,check,"Shop active alias hold");
                    var heldEntry=holdBuild.Model.Entries.FirstOrDefault(e=>e.Source=="New"&&holdRow.definitions.Contains(e.Definition.id));
                    Check(check,beforeHoldEntry!=null&&heldEntry!=null&&beforeHoldEntry.StockUnits==availableBeforeHold&&
                        heldEntry.StockUnits==Math.Max(0,availableBeforeHold-1),
                        "An active alias hold reduces the matching empty-cart browser offer exactly once");
                }
                finally{profile.inventory.market.index=savedMarketIndex;}
            }
            finally{profile.inventory.cart=priorHold;profile.inventory.usingCartReservation=priorUsingReservation;}

            // Legacy nonempty-cart parity stays explicit and outside any bounded claim.
            var legacyLines=new[]{new PartCartLine{definitionId=catalog.Id(profile.SelectedCar.vehicleId,PartCategory.HeadIntake,1),install=true}};
            var legacy=SynchronousReference(profile,"Shop",legacyLines,true,new NativePartPreviewCache());
            Check(check,legacy.Entries.Count>0,"Nonempty-cart legacy synchronous path remains available and labeled separately");

            var availableTire=profile.looseEngineElements.First(u=>u.element.id==fixture.tireUnitId);
            var availablePiston=profile.looseEngineElements.First(u=>u.element.id==fixture.pistonUnitId);
            Check(check,availableTire.tire!=null&&availableTire.tireUnit&&availablePiston.element!=null,
                "Fixture has real used tire condition and fitted-spec engine member unit");

            // The multi-position new-unit dialog is intentionally still legacy.
            // Exercise it separately so its JSON count cannot be mistaken for a
            // converted empty-cart build or fitted loose-unit evaluation.
            var pistonDefinition=catalog.Find(availablePiston.definitionId);
            var availablePositions=InventoryUnits.Positions(profile,profile.SelectedCar,pistonDefinition.id,availablePiston.element.role);
            if(availablePositions.Length>1)
            {
                var legacyModel=SynchronousReference(profile,"Shop",Array.Empty<PartCartLine>(),false,new NativePartPreviewCache());
                var offer=legacyModel.Entries.First(e=>e.Source=="New"&&e.Definition.id==pistonDefinition.id);
                int before=NativePartBrowser.LegacyCartPreviewCount;
                var preview=legacyModel.PreviewNewUnit(offer,availablePiston.element.role,availablePositions.Take(2));
                Check(check,preview.Proposed!=null&&NativePartBrowser.LegacyCartPreviewCount==before+1,
                    "Multi-position PreviewNewUnit remains explicitly measured legacy behavior outside the converted-path encoder gate");
            }
            else check(true,"Multi-position PreviewNewUnit legacy case unavailable for this authored vehicle; converted-path encoder checks remain independent");
        }
    }
#endif
}

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
    /// <summary>Managed correctness checks for the dormant typed Parts base read set.</summary>
    public static class NativePartsReadSetChecks
    {
        static void Check(Action<bool,string> check,bool value,string label)
        {
            check(value,label);
            if(!value)throw new InvalidOperationException(label);
        }

        static bool RejectsWithoutThrow(Func<bool> matches)
        {
            try{return !matches();}
            catch{return false;}
        }

        static bool SameReferences<T>(IList<T> current,T[] before) where T:class
        {
            if(current==null||before==null||current.Count!=before.Length)return current==null&&before==null;
            for(int i=0;i<before.Length;i++)if(!ReferenceEquals(current[i],before[i]))return false;
            return true;
        }

        static bool SameStringSequence(List<string> current,string[] before)
        {
            if(current==null||before==null)return current==null&&before==null;
            return current.SequenceEqual(before,StringComparer.Ordinal);
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

        static string TypedGraphStamp(params object[] roots)
        {
            var text=new StringBuilder(4096);
            var seen=new HashSet<object>(ReferenceComparer.Instance);
            for(int i=0;i<roots.Length;i++){text.Append("ROOT").Append(i).Append(':');AppendTyped(text,roots[i],seen);}
            return text.ToString();
        }

        static void AppendTyped(StringBuilder text,object value,HashSet<object> seen)
        {
            if(value==null){text.Append("NULL;");return;}
            Type type=value.GetType();
            if(value is string str){text.Append("S").Append(str.Length).Append(':').Append(str).Append(';');return;}
            if(type.IsEnum){text.Append("E").Append(type.FullName).Append(':').Append(Convert.ToString(value,System.Globalization.CultureInfo.InvariantCulture)).Append(';');return;}
            if(type==typeof(bool)||type==typeof(byte)||type==typeof(sbyte)||type==typeof(short)||type==typeof(ushort)||
                type==typeof(int)||type==typeof(uint)||type==typeof(long)||type==typeof(ulong)||type==typeof(char))
            {text.Append(type.FullName).Append(':').Append(Convert.ToString(value,System.Globalization.CultureInfo.InvariantCulture)).Append(';');return;}
            if(type==typeof(float)){var bits=new FloatBits{value=(float)value};text.Append("F32:").Append(bits.bits).Append(';');return;}
            if(type==typeof(double)){text.Append("F64:").Append(BitConverter.DoubleToInt64Bits((double)value)).Append(';');return;}
            if(type==typeof(decimal)){foreach(int part in decimal.GetBits((decimal)value))text.Append("DEC:").Append(part).Append(';');return;}
            if(!type.IsValueType)
            {
                text.Append("REF:").Append(type.FullName).Append(':').Append(RuntimeHelpers.GetHashCode(value)).Append(';');
                if(!seen.Add(value)){text.Append("REPEAT;");return;}
            }
            if(value is IList list)
            {
                text.Append("LIST:").Append(type.FullName).Append(':').Append(list.Count).Append('{');
                for(int i=0;i<list.Count;i++)AppendTyped(text,list[i],seen);
                text.Append("}ENDLIST;");return;
            }
            if(typeof(UnityEngine.Object).IsAssignableFrom(type))
            {text.Append("UNITY:").Append(type.FullName).Append(';');return;}
            var fields=new List<FieldInfo>();
            for(Type current=type;current!=null&&current!=typeof(object);current=current.BaseType)
                fields.AddRange(current.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly)
                    .Where(f=>!f.IsNotSerialized&&(f.IsPublic||Attribute.IsDefined(f,typeof(SerializeField))||
                        Attribute.IsDefined(f,typeof(SerializeReference)))));
            if(fields.Count==0)
            {
                text.Append("VALUE:").Append(type.FullName).Append(':')
                    .Append(Convert.ToString(value,System.Globalization.CultureInfo.InvariantCulture)).Append(';');return;
            }
            text.Append("OBJECT:").Append(type.FullName).Append('{');
            foreach(var field in fields.OrderBy(f=>f.DeclaringType.FullName,StringComparer.Ordinal).ThenBy(f=>f.Name,StringComparer.Ordinal))
            {text.Append(field.DeclaringType.FullName).Append('.').Append(field.Name).Append('=');AppendTyped(text,field.GetValue(value),seen);}
            text.Append("}ENDOBJECT;");
        }

        static PlayerProfile Fixture(MechanicalCatalog catalog,VehicleDefinition vehicle,int carCount=1,
            VehicleDefinition[] authoredVehicles=null)
        {
            var profile=PlayerProfile.New(25102026);
            profile.cars.Clear();
            profile.selectedOwnedVehicleId="";
            profile.selectedVehicleId="";
            // Keep the fixture's vehicle market self-contained. A null stock state
            // makes Ensure generate offers for every Resources vehicle, which is
            // invalid for the deliberately small one-vehicle catalog case.
            profile.vehicleStock=new VehicleStockState{version=1,seed=25102026,
                listings=new List<VehicleOffer>()};
            MechanicsOwnership.Ensure(profile,catalog);
            profile.cash=1000000;
            profile.reputation=1000;
            VehicleCollection.Buy(profile,catalog,new VehicleOffer{model=vehicle.spec.id,price=1,
                reputation=0,stock=vehicle.CreateStockTune(),paint="#FFFFFF"});
            string selectedId=profile.SelectedCar.instanceId;
            var choices=(authoredVehicles??new[]{vehicle});
            for(int i=1;i<carCount;i++)
            {
                var additional=choices[(i-1)%choices.Length];
                VehicleCollection.Buy(profile,catalog,new VehicleOffer{model=additional.spec.id,price=1,
                    reputation=0,stock=additional.CreateStockTune(),paint="#FFFFFF"});
            }
            if(carCount>1)VehicleCollection.Select(profile,selectedId);
            return profile;
        }

        static NativePartsBaseReadInputs Inputs(PlayerProfile profile,string page="Shop",bool planned=false,
            IEnumerable<PartCartLine> lines=null,UnitPreferences units=null,NativePartsOwnerContext owner=null,
            IEnumerable<NativePartsPriceFact> prices=null)
            =>new NativePartsBaseReadInputs(profile,page,planned,lines,units??new UnitPreferences(),
                owner??NativePartsOwnerContext.Capture(),prices);

        static NativePartsBaseReadSet Capture(NativePartsBaseReadInputs inputs,Action<bool,string> check,string label)
            =>Capture(inputs,check,label,7,out _);

        static NativePartsBaseReadSet Capture(NativePartsBaseReadInputs inputs,Action<bool,string> check,string label,
            int itemCap,out int yields)
        {
            yields=0;
            var session=NativePartsBaseReadSet.BeginCapture(inputs);
            int prior=0;
            bool itemCapRespected=true,progressMonotonic=true;
            while(!session.Complete)
            {
                if(++yields>100000)throw new InvalidOperationException("Capture failed to terminate: "+label);
                int before=session.ItemsCaptured;
                session.Advance(itemCap,2.0);
                int delta=session.ItemsCaptured-before;
                itemCapRespected&=delta>=0&&delta<=itemCap;
                progressMonotonic&=session.ItemsCaptured>=prior;
                prior=session.ItemsCaptured;
            }
            Check(check,itemCapRespected,"All capture slices respect the item cap: "+label);
            Check(check,progressMonotonic,"Capture progress is monotonic across slices: "+label);
            if(!session.TryFinish(inputs,out var readSet,out var reason))
                throw new InvalidOperationException("Matching read set did not finish: "+label+" / "+reason);
            return readSet;
        }

        static void Stale(PlayerProfile profile,string page,Action mutate,Action restore,
            Action<bool,string> check,string label,PartCartLine[] lines=null,
            NativePartsPriceFact[] prices=null,UnitPreferences units=null)
        {
            var owner=NativePartsOwnerContext.Capture();
            var original=Inputs(profile,page,false,lines,units,owner,prices);
            var set=Capture(original,check,label);
            mutate();
            try
            {
                var current=Inputs(profile,page,false,lines,units,owner,prices);
                Check(check,!set.MatchesCurrent(current,out _),"Changed typed fact invalidates read set: "+label);
            }
            finally{restore();}
            Check(check,set.MatchesCurrent(Inputs(profile,page,false,lines,units,owner,prices),out _),
                "Restoring typed fact restores exact match: "+label);
        }

        static bool MutateScalar(FieldInfo field,object target)
        {
            object old=field.GetValue(target);Type type=field.FieldType;
            if(type==typeof(string))field.SetValue(target,string.IsNullOrEmpty((string)old)?"s06d":"s06d-"+old);
            else if(type==typeof(bool))field.SetValue(target,!(bool)old);
            else if(type==typeof(int))field.SetValue(target,checked((int)old+1));
            else if(type==typeof(long))field.SetValue(target,checked((long)old+1));
            else if(type==typeof(float)){float v=(float)old,d=Math.Max(.125f,Math.Abs(v)*.01f),n=v+d;if(n==v)n=v-d;field.SetValue(target,n);}
            else if(type==typeof(double)){double v=(double)old,d=Math.Max(.125d,Math.Abs(v)*.01d),n=v+d;if(n==v)n=v-d;field.SetValue(target,n);}
            else if(type.IsEnum){var values=Enum.GetValues(type);int at=Array.IndexOf(values,old);field.SetValue(target,values.GetValue((at+1)%values.Length));}
            else return false;
            return !Equals(old,field.GetValue(target));
        }

        static void SignedZero(PlayerProfile profile,string page,object target,string fieldName,
            Action<bool,string> check,string label)
        {
            var field=target.GetType().GetField(fieldName,BindingFlags.Instance|BindingFlags.Public);
            if(field==null||(field.FieldType!=typeof(float)&&field.FieldType!=typeof(double)))return;
            object old=field.GetValue(target);
            field.SetValue(target,field.FieldType==typeof(float)?(object)0f:0d);
            var set=Capture(Inputs(profile,page),check,label+" signed-zero baseline");
            object negative=field.FieldType==typeof(float)?(object)(-0f):(-0d);
            field.SetValue(target,negative);
            bool hasSign=field.FieldType==typeof(float)
                ?BitConverter.ToInt32(BitConverter.GetBytes((float)field.GetValue(target)),0)==unchecked((int)0x80000000)
                :BitConverter.ToInt64(BitConverter.GetBytes((double)field.GetValue(target)),0)==unchecked((long)0x8000000000000000UL);
            Check(check,hasSign,"Test represents negative zero exactly: "+label);
            Check(check,!set.MatchesCurrent(Inputs(profile,page),out _),"Signed-zero change invalidates exact read set: "+label);
            field.SetValue(target,old);
        }

        static void SchemaCoverage(Action<bool,string> check)
        {
            var types=new Dictionary<string,Type>(StringComparer.Ordinal)
            {
                [nameof(Tune)]=typeof(Tune),[nameof(FuelState)]=typeof(FuelState),
                [nameof(FluidState)]=typeof(FluidState),[nameof(ComponentCondition)]=typeof(ComponentCondition),
                [nameof(PartInstance)]=typeof(PartInstance),[nameof(EngineElement)]=typeof(EngineElement),
                [nameof(TireCondition)]=typeof(TireCondition),[nameof(PartCartLine)]=typeof(PartCartLine),
                [nameof(UnitPreferences)]=typeof(UnitPreferences),[nameof(AwdSpec)]=typeof(AwdSpec),
                [nameof(ClutchConditionState)]=typeof(ClutchConditionState),
                [nameof(InstalledBuild)]=typeof(InstalledBuild),[nameof(SlotBinding)]=typeof(SlotBinding),
                [nameof(EngineRebuildJob)]=typeof(EngineRebuildJob),[nameof(EngineRebuildTarget)]=typeof(EngineRebuildTarget),
                [nameof(LooseEngineElement)]=typeof(LooseEngineElement),[nameof(DealerStock)]=typeof(DealerStock),
                [nameof(NewPartStock)]=typeof(NewPartStock),[nameof(CartReservationState)]=typeof(CartReservationState),
                [nameof(ReservedPartSupply)]=typeof(ReservedPartSupply),["VehicleStockState scoped"]=typeof(VehicleStockState)
            };
            var declared=NativePartsBaseReadSet.ComparedSerializedFields();
            foreach(var pair in types)
            {
                Check(check,declared.TryGetValue(pair.Key,out var expected),"Comparator schema explicitly lists "+pair.Key);
                var actual=pair.Value.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)
                    .Where(field=>!field.IsStatic&&!field.IsInitOnly&&!field.IsNotSerialized&&
                        (field.IsPublic||field.GetCustomAttributes(typeof(SerializeField),true).Length>0||
                         field.GetCustomAttributes(typeof(SerializeReference),true).Length>0))
                    .Select(field=>field.Name).ToHashSet(StringComparer.Ordinal);
                var expectedSet=new HashSet<string>(expected,StringComparer.Ordinal);
                if(pair.Key==nameof(TireCondition))
                    foreach(string inherited in declared[nameof(EngineElement)])expectedSet.Add(inherited);
                bool completeSchema=pair.Key=="VehicleStockState scoped"
                    ?expectedSet.IsSubsetOf(actual):actual.SetEquals(expectedSet);
                Check(check,completeSchema,"Comparator field list covers Unity serialized schema for "+pair.Key+
                    " (missing: "+string.Join(",",actual.Except(expectedSet))+"; extra: "+string.Join(",",expectedSet.Except(actual))+" )");
            }
            var authoredTypes=new Dictionary<string,Type>(StringComparer.Ordinal)
            {
                [nameof(PartDefinition)]=typeof(PartDefinition),[nameof(AwdHardwareSpec)]=typeof(AwdHardwareSpec),
                [nameof(AwdSpec)]=typeof(AwdSpec)
            };
            var authored=MarketCatalogLayout.AuthoredComparedFields();
            foreach(var pair in authoredTypes)
            {
                Check(check,authored.TryGetValue(pair.Key,out var expected),"Authored comparator schema explicitly lists "+pair.Key);
                var actual=pair.Value.GetFields(BindingFlags.Instance|BindingFlags.Public)
                    .Where(field=>!field.IsStatic&&!field.IsInitOnly&&!field.IsNotSerialized)
                    .Select(field=>field.Name).ToHashSet(StringComparer.Ordinal);
                Check(check,actual.SetEquals(new HashSet<string>(expected,StringComparer.Ordinal)),
                    "Authored comparator covers all public serialized fields for "+pair.Key);
            }
        }

        public static void Run(Action<bool,string> check,string fixtureDirectory)
        {
            if(check==null)throw new ArgumentNullException(nameof(check));
            if(string.IsNullOrWhiteSpace(fixtureDirectory))throw new ArgumentException("An isolated fixture directory is required.",nameof(fixtureDirectory));
            var vehicles=Resources.LoadAll<VehicleDefinition>("Data/Vehicles");
            Check(check,vehicles!=null&&vehicles.Length>0,"read-set vehicle definitions load");
            var vehicle=vehicles.First(v=>v.spec.id=="starter-hatch");
            var catalog=new MechanicalCatalog(vehicles.Select(v=>v.spec));
            var profile=Fixture(catalog,vehicle);
            foreach(var _ in MarketCatalogLayout.WarmSteps(catalog)){}
            var owner=NativePartsOwnerContext.Capture();
            foreach(string unsupportedPage in new string[]{null,"","shop","Bench"})
            {
                bool rejected=false;
                try{NativePartsBaseReadSet.BeginCapture(Inputs(profile,unsupportedPage));}
                catch(ArgumentException){rejected=true;}
                Check(check,rejected,"Unsupported, empty, null or wrong-case page fails closed: "+(unsupportedPage??"<null>"));
            }
            SchemaCoverage(check);
            var upperPath=new NativePartsOwnerContext("path-test","C:\\S06D\\Owner.sqlite3",4);
            var lowerPath=new NativePartsOwnerContext("path-test","c:\\s06d\\owner.SQLITE3",4);
            bool windowsPathSemantics=System.IO.Path.DirectorySeparatorChar=='\\';
            Check(check,upperPath.Same(lowerPath)==windowsPathSemantics,
                "Owner path equality matches platform semantics while Windows paths ignore case");

            var tokenCatalog=new MechanicalCatalog(vehicles.Select(v=>v.spec));
            bool sawShopReady=false,afterShopReady=false;
            foreach(var step in MarketCatalogLayout.WarmSteps(tokenCatalog))
            {
                bool atReady=Equals(step,"parts/shop ready");
                bool hasToken=MarketCatalogLayout.TryCaptureReadyContentToken(tokenCatalog,out _);
                Check(check,hasToken==atReady||sawShopReady,
                    "Opaque content token publishes only at complete Shop-ready boundary: "+step);
                if(atReady)sawShopReady=true;
                else if(sawShopReady&&step!=null)afterShopReady=true;
            }
            MarketCatalogLayout.ReadyContentToken readyToken=null;
            bool gotReadyToken=MarketCatalogLayout.TryCaptureReadyContentToken(tokenCatalog,out readyToken);
            Check(check,sawShopReady&&afterShopReady&&gotReadyToken,
                "Ready content token remains available while later market-fit layout steps yield");
            var tokenDefinition=tokenCatalog.parts[0];string tokenDefinitionId=tokenDefinition.id;
            long encodedBeforeMismatch=MarketCatalogLayout.CanonicalJsonEncodeCount;
            int authoredPrice=tokenDefinition.price;
            tokenDefinition.price=authoredPrice+1;
            Check(check,!readyToken.MatchesCurrent(tokenCatalog),
                "Same-count direct semantic edit invalidates the retained authored-content token");
            Check(check,MarketCatalogLayout.CanonicalJsonEncodeCount==encodedBeforeMismatch,
                "Rejected semantic token comparison performs no canonical JSON encoding");
            tokenDefinition.price=authoredPrice;
            Check(check,readyToken.MatchesCurrent(tokenCatalog),"Restoring semantic authored data restores token parity");
            string existingDefinitionId=tokenCatalog.parts[1].id;
            tokenDefinition.id=existingDefinitionId;
            Check(check,!readyToken.MatchesCurrent(tokenCatalog),
                "Same-reference same-count rename to another existing definition ID invalidates ready token");
            Check(check,MarketCatalogLayout.CanonicalJsonEncodeCount==encodedBeforeMismatch,
                "Rejected live token comparison performs no canonical JSON encoding");
            tokenDefinition.id=tokenDefinitionId;
            Check(check,readyToken.MatchesCurrent(tokenCatalog),"Restoring exact authored ID restores token parity");
            int tokenPartIndex=tokenCatalog.parts.IndexOf(tokenDefinition);
            var sameContentReplacement=tokenDefinition.Copy();
            tokenCatalog.parts[tokenPartIndex]=sameContentReplacement;
            Check(check,!readyToken.MatchesCurrent(tokenCatalog),
                "Replacing an authored definition with a same-ID same-content object invalidates the retained token");
            Check(check,MarketCatalogLayout.CanonicalJsonEncodeCount==encodedBeforeMismatch,
                "Same-content object-identity rejection performs no canonical JSON encoding");
            tokenCatalog.parts[tokenPartIndex]=tokenDefinition;
            Check(check,readyToken.MatchesCurrent(tokenCatalog),
                "Restoring the exact authored definition object restores token parity");
            tokenDefinition.id=tokenDefinitionId+"-legitimate-revision";
            tokenCatalog.RebuildIndexes();
            foreach(var _ in MarketCatalogLayout.WarmSteps(tokenCatalog)){}
            Check(check,MarketCatalogLayout.TryCaptureReadyContentToken(tokenCatalog,out var revisedToken)&&
                revisedToken.MatchesCurrent(tokenCatalog),"Legitimate ID authoring change rebuilds and publishes a current token");

            foreach(string page in new[]{"Shop","Inventory","Installed"})
            {
                var suppliedUnits=new UnitPreferences{version=5,speed=SpeedUnit.MilesPerHour,
                    pressure=PressureUnit.Psi,torque=TorqueUnit.PoundFeet};
                var suppliedPrices=new[]{new NativePartsPriceFact("new","starter-hatch",123),
                    new NativePartsPriceFact("used","asset-1",null)};
                string sideEffectBefore=TypedGraphStamp(profile,catalog.parts,suppliedUnits,suppliedPrices);
                var ownerBefore=NativePartsOwnerContext.Capture();
                int authoredRevisionBefore=catalog.AuthoringRevision;
                var authoredListBefore=catalog.parts;
                long encodesBefore=MarketCatalogLayout.CanonicalJsonEncodeCount;
                int beforeCash=profile.cash,beforeReputation=profile.reputation;
                long beforeRevision=profile.revision,beforeStorage=profile.storageCommit;
                var beforeCars=profile.cars.ToArray();var beforeParts=profile.parts.ToArray();
                var beforeLoose=profile.looseEngineElements.ToArray();
                var inventoryBefore=profile.inventory;var marketBefore=profile.inventory.market;
                var stockRowsBefore=marketBefore.parts;var stockSourcesBefore=stockRowsBefore.ToArray();
                var stockUnitsBefore=stockSourcesBefore.Select(x=>x?.units??0).ToArray();
                var stockInitialBefore=stockSourcesBefore.Select(x=>x?.initialUnits??0).ToArray();
                var stockDeliveryBefore=stockSourcesBefore.Select(x=>x?.deliveryPercent??0).ToArray();
                var stockDiscoveredBefore=stockSourcesBefore.Select(x=>x!=null&&x.discovered).ToArray();
                var stockDefinitionsBefore=stockSourcesBefore.Select(x=>x?.definitions?.ToArray()).ToArray();
                var dealerBefore=profile.inventory.dealer;var dealerSourcesBefore=dealerBefore.ToArray();
                var reservationBefore=profile.inventory.cart;
                var selectedBefore=profile.SelectedCar;var tuneBefore=selectedBefore.tune;
                var fuelBefore=selectedBefore.fuel;var fluidsBefore=selectedBefore.fluids;
                int unitsVersion=suppliedUnits.version;SpeedUnit speedBefore=suppliedUnits.speed;
                PressureUnit pressureBefore=suppliedUnits.pressure;TorqueUnit torqueBefore=suppliedUnits.torque;
                var priceReferences=suppliedPrices.ToArray();
                var inputs=Inputs(profile,page,false,null,suppliedUnits,owner,suppliedPrices);
                var timer=Stopwatch.StartNew();
                var set=Capture(inputs,check,"page baseline / "+page,5,out int yields);
                timer.Stop();
                Check(check,set.MatchesCurrent(inputs,out _),"Fresh exact facts match / "+page);
                bool stockUnchanged=ReferenceEquals(profile.inventory.market,marketBefore)&&
                    ReferenceEquals(profile.inventory.market.parts,stockRowsBefore)&&
                    SameReferences(profile.inventory.market.parts,stockSourcesBefore);
                if(stockUnchanged)for(int i=0;i<stockSourcesBefore.Length;i++)
                {
                    var row=stockSourcesBefore[i];
                    if(row!=null&&(row.units!=stockUnitsBefore[i]||row.initialUnits!=stockInitialBefore[i]||
                        row.deliveryPercent!=stockDeliveryBefore[i]||row.discovered!=stockDiscoveredBefore[i]||
                        !SameStringSequence(row.definitions,stockDefinitionsBefore[i])))stockUnchanged=false;
                }
                bool pricesUnchanged=suppliedPrices.Length==priceReferences.Length;
                for(int i=0;i<suppliedPrices.Length;i++)pricesUnchanged&=ReferenceEquals(suppliedPrices[i],priceReferences[i])&&
                    suppliedPrices[i].Source== (i==0?"new":"used")&&suppliedPrices[i].AssetKey==(i==0?"starter-hatch":"asset-1")&&
                    suppliedPrices[i].Price==(i==0?(int?)123:null);
                Check(check,profile.cash==beforeCash&&profile.reputation==beforeReputation&&profile.revision==beforeRevision&&
                    profile.storageCommit==beforeStorage&&SameReferences(profile.cars,beforeCars)&&
                    SameReferences(profile.parts,beforeParts)&&SameReferences(profile.looseEngineElements,beforeLoose)&&
                    ReferenceEquals(profile.inventory,inventoryBefore)&&stockUnchanged&&
                    ReferenceEquals(profile.inventory.dealer,dealerBefore)&&SameReferences(profile.inventory.dealer,dealerSourcesBefore)&&
                    ReferenceEquals(profile.inventory.cart,reservationBefore)&&ReferenceEquals(profile.SelectedCar,selectedBefore)&&
                    ReferenceEquals(selectedBefore.tune,tuneBefore)&&ReferenceEquals(selectedBefore.fuel,fuelBefore)&&
                    ReferenceEquals(selectedBefore.fluids,fluidsBefore),
                    "Capture/compare leaves profile, resources, stock, holds and selected state unchanged / "+page);
                Check(check,suppliedUnits.version==unitsVersion&&suppliedUnits.speed==speedBefore&&
                    suppliedUnits.pressure==pressureBefore&&suppliedUnits.torque==torqueBefore&&pricesUnchanged,
                    "Capture/compare leaves supplied settings and price objects unchanged / "+page);
                var ownerAfter=NativePartsOwnerContext.Capture();
                Check(check,sideEffectBefore==TypedGraphStamp(profile,catalog.parts,suppliedUnits,suppliedPrices)&&
                    ReferenceEquals(catalog.parts,authoredListBefore)&&catalog.AuthoringRevision==authoredRevisionBefore&&
                    ownerBefore.Slot==ownerAfter.Slot&&ownerBefore.CanonicalPath==ownerAfter.CanonicalPath&&
                    ownerBefore.Epoch==ownerAfter.Epoch&&encodesBefore==MarketCatalogLayout.CanonicalJsonEncodeCount,
                    "Typed serialized profile/catalog, settings, prices, owner context and canonical-encoding state are unchanged / "+page);
                Check(check,yields>0||set.CapturedItemCount==0,"Empty scope can finish without synthetic work / "+page);
                check(true,"Read-set descriptive timing / "+page+" catalogDefinitions="+catalog.parts.Count+
                    " yields="+yields+" captured="+set.CapturedItemCount+" pure-capture="+
                    set.CaptureMilliseconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+
                    "ms max-step="+set.LargestCaptureStepMilliseconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+
                    "ms final-compare="+set.FinalCompareMilliseconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+
                    "ms wall-with-check-callbacks="+timer.Elapsed.TotalMilliseconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+
                    "ms; wall includes verifier callback overhead, no timing threshold");
            }

            var smallCatalog=new MechanicalCatalog(new[]{vehicle.spec});
            var smallProfile=Fixture(smallCatalog,vehicle);
            foreach(var _ in MarketCatalogLayout.WarmSteps(smallCatalog)){}
            Check(check,smallCatalog.parts.Count<catalog.parts.Count,
                "Small one-vehicle authored catalog is smaller than the full authored catalog");
            var smallInputs=Inputs(smallProfile,"Shop",false,null,null,owner,null);
            var smallClock=Stopwatch.StartNew();
            var smallSet=Capture(smallInputs,check,"small-catalog Shop read set",7,out int smallYields);
            smallClock.Stop();
            Check(check,smallSet.MatchesCurrent(smallInputs,out _),"Small catalog read set matches exact current facts");
            check(true,"Read-set catalog boundary timing / catalogDefinitions="+smallCatalog.parts.Count+
                " ownedParts="+smallProfile.parts.Count+" engines="+smallProfile.engines.Count+
                " members="+smallProfile.parts.Sum(p=>p?.condition?.elements?.Length??0)+
                " yields="+smallYields+" pure-capture="+
                smallSet.CaptureMilliseconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+
                "ms max-step="+smallSet.LargestCaptureStepMilliseconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+
                "ms final-compare="+smallSet.FinalCompareMilliseconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+
                "ms wall-with-check-callbacks="+smallClock.Elapsed.TotalMilliseconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+
                "ms; no timing threshold");
            var priceRows=new[]{new NativePartsPriceFact("new","price-a",10),new NativePartsPriceFact("used","price-b",null)};
            var priceSet=Capture(Inputs(profile,"Shop",false,null,new UnitPreferences(),owner,priceRows),check,"ordered nullable price facts");
            Check(check,!priceSet.MatchesCurrent(Inputs(profile,"Shop",false,null,new UnitPreferences(),owner,
                priceRows.Reverse()),out _),"Supplied price source order is preserved");
            Check(check,!priceSet.MatchesCurrent(Inputs(profile,"Shop",false,null,new UnitPreferences(),owner,
                new[]{new NativePartsPriceFact("used","price-b",0),priceRows[0]}),out _),
                "Null and concrete supplied prices remain distinct");

            var identityInputs=Inputs(profile,"Shop",false,null,null,owner,null);
            var identitySet=Capture(identityInputs,check,"identity fences");
            var priorPartsCollection=profile.parts;
            try
            {
                profile.parts=null;
                Check(check,RejectsWithoutThrow(()=>identitySet.MatchesCurrent(identityInputs,out _)),
                    "Null parts collection safely rejects final comparison");
            }
            finally{profile.parts=priorPartsCollection;}
            var priorLooseCollection=profile.looseEngineElements;
            try
            {
                profile.looseEngineElements=null;
                Check(check,RejectsWithoutThrow(()=>identitySet.MatchesCurrent(identityInputs,out _)),
                    "Null loose-unit collection safely rejects final comparison");
            }
            finally{profile.looseEngineElements=priorLooseCollection;}
            var priorCarsCollection=profile.cars;
            try
            {
                profile.cars=null;
                Check(check,RejectsWithoutThrow(()=>identitySet.MatchesCurrent(identityInputs,out _)),
                    "Null selected-car collection safely rejects final comparison");
            }
            finally{profile.cars=priorCarsCollection;}
            string priorSelectedOwned=profile.selectedOwnedVehicleId,priorSelectedVehicle=profile.selectedVehicleId;
            try
            {
                profile.selectedOwnedVehicleId="";
                profile.selectedVehicleId="";
                Check(check,RejectsWithoutThrow(()=>identitySet.MatchesCurrent(identityInputs,out _)),
                    "Cleared selected-car identity safely rejects final comparison");
            }
            finally{profile.selectedOwnedVehicleId=priorSelectedOwned;profile.selectedVehicleId=priorSelectedVehicle;}
            Check(check,!identitySet.MatchesCurrent(Inputs(profile,"Shop",false,null,null,
                new NativePartsOwnerContext(owner.Slot+"-other",owner.CanonicalPath,owner.Epoch),null),out _),
                "Supplied owner slot changes reject");
            Check(check,!identitySet.MatchesCurrent(Inputs(profile,"Shop",false,null,null,
                new NativePartsOwnerContext(owner.Slot,owner.CanonicalPath+".other",owner.Epoch),null),out _),
                "Supplied owner path changes reject");
            Check(check,!identitySet.MatchesCurrent(Inputs(profile,"Shop",false,null,null,
                new NativePartsOwnerContext(owner.Slot,owner.CanonicalPath,owner.Epoch+1),null),out _),
                "Supplied owner epoch changes reject");
            var distinctProfile=Fixture(catalog,vehicle);
            Check(check,!identitySet.MatchesCurrent(Inputs(distinctProfile,"Shop",false,null,null,owner,null),out _),
                "A different profile object with matching data is rejected");
            var originalCatalog=profile.catalog;
            profile.catalog=new MechanicalCatalog(vehicles.Select(v=>v.spec));
            Check(check,!identitySet.MatchesCurrent(identityInputs,out _),"Catalog object replacement is rejected");
            profile.catalog=originalCatalog;
            var originalCar=profile.SelectedCar;
            int selectedIndex=profile.cars.IndexOf(originalCar);
            profile.cars[selectedIndex]=new OwnedCar{instanceId=originalCar.instanceId,vehicleId=originalCar.vehicleId,
                engineAssemblyId=originalCar.engineAssemblyId,tune=originalCar.tune,fuel=originalCar.fuel,
                fluids=originalCar.fluids,installed=originalCar.installed};
            Check(check,!identitySet.MatchesCurrent(identityInputs,out _),"Selected-car object replacement reusing its ID is rejected");
            profile.cars[selectedIndex]=originalCar;

            var incompleteInputs=Inputs(profile);
            var incomplete=NativePartsBaseReadSet.BeginCapture(incompleteInputs);
            Check(check,!incomplete.TryFinish(incompleteInputs,out var premature,out _)&&premature==null,
                "Incomplete source capture cannot publish a partial read set");

            var interruptedInputs=Inputs(profile,"Shop",false,null,null,owner,null);
            var interrupted=NativePartsBaseReadSet.BeginCapture(interruptedInputs);
            interrupted.Advance(1,2.0);
            profile.parts.Add(null);
            Check(check,!interrupted.Advance(1,2.0)&&interrupted.Failed&&
                !string.IsNullOrEmpty(interrupted.FailureReason),
                "Source-shape mutation produces a visible terminal capture failure");
            Check(check,!interrupted.Advance(1,2.0)&&
                !interrupted.TryFinish(interruptedInputs,out var interruptedSet,out var interruptedReason)&&
                interruptedSet==null&&!string.IsNullOrEmpty(interruptedReason),
                "Failed capture cannot spin, resume or publish partial facts");
            profile.parts.RemoveAt(profile.parts.Count-1);
            var afterInterrupted=Capture(Inputs(profile,"Shop",false,null,null,owner,null),check,"capture after stale abort");
            Check(check,afterInterrupted.MatchesCurrent(Inputs(profile,"Shop",false,null,null,owner,null),out _),
                "A fresh bounded capture succeeds after stale capture is discarded");

            int priorParts=-1,priorEngines=-1,priorMembers=-1,priorCaptured=-1;
            foreach(int carCount in new[]{1,5,20})
            {
                var scoped=Fixture(catalog,vehicle,carCount,vehicles);
                int members=scoped.parts.Sum(p=>p?.condition?.elements?.Length??0);
                Check(check,scoped.cars.Count==carCount&&scoped.cars.Select(c=>c.instanceId).Distinct(StringComparer.Ordinal).Count()==carCount,
                    "Roster contains the requested number of uniquely owned cars bought through VehicleCollection / "+carCount);
                Check(check,scoped.engines.Count>=carCount&&scoped.engines.Select(e=>e.instanceId).Distinct(StringComparer.Ordinal).Count()==scoped.engines.Count,
                    "Bought roster has real unique engine assemblies / "+carCount);
                Check(check,scoped.parts.Count>0&&scoped.parts.Select(p=>p.instanceId).Distinct(StringComparer.Ordinal).Count()==scoped.parts.Count,
                    "Bought roster has real unique part instances / "+carCount);
                Check(check,members>0,"Bought roster has typed engine/tire member rows / "+carCount);
                if(priorParts>=0)Check(check,scoped.parts.Count>priorParts&&scoped.engines.Count>priorEngines&&members>priorMembers,
                    "Actual part, engine and member rows grow with the bought car roster / "+carCount);
                var scopeInputs=Inputs(scoped,"Shop",false,null,null,owner,null);
                var clock=Stopwatch.StartNew();
                var scopedSet=Capture(scopeInputs,check,"Shop roster scope / "+carCount+" cars",1,out int countYields);
                clock.Stop();
                Check(check,scopedSet.MatchesCurrent(scopeInputs,out _),"Roster-size read-set parity / "+carCount+" cars");
                Check(check,priorCaptured<0||scopedSet.CapturedItemCount>priorCaptured,
                    "Captured source rows grow with the actual bought roster / "+carCount);
                check(true,"Read-set scope timing / cars="+carCount+" catalogDefinitions="+catalog.parts.Count+
                    " ownedParts="+scoped.parts.Count+" engines="+scoped.engines.Count+" members="+members+
                    " looseUnits="+scoped.looseEngineElements.Count+" yields="+countYields+" captured="+scopedSet.CapturedItemCount+
                    " pure-capture="+scopedSet.CaptureMilliseconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+
                    "ms max-step="+scopedSet.LargestCaptureStepMilliseconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+
                    "ms final-compare="+scopedSet.FinalCompareMilliseconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+
                    "ms wall-with-check-callbacks="+clock.Elapsed.TotalMilliseconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+
                    "ms; wall includes verifier callback overhead, no timing threshold");
                priorParts=scoped.parts.Count;priorEngines=scoped.engines.Count;priorMembers=members;priorCaptured=scopedSet.CapturedItemCount;
            }

            var tune=profile.SelectedCar.tune;
            SignedZero(profile,"Shop",tune,"camTimingDegrees",check,"Tune.camTimingDegrees");
            foreach(var field in typeof(Tune).GetFields(BindingFlags.Instance|BindingFlags.Public))
            {
                if(field.Name=="awd")continue;
                object old=field.GetValue(tune);if(!MutateScalar(field,tune))continue;
                object changed=field.GetValue(tune);field.SetValue(tune,old);
                Stale(profile,"Shop",()=>field.SetValue(tune,changed),()=>field.SetValue(tune,old),check,"Tune."+field.Name);
            }
            var oldAwd=tune.awd;bool oldAwdConfigured=tune.awdConfigured;
            tune.awd=new AwdSpec();tune.awdConfigured=true;
            foreach(var field in typeof(AwdSpec).GetFields(BindingFlags.Instance|BindingFlags.Public))
            {
                object old=field.GetValue(tune.awd);if(!MutateScalar(field,tune.awd))continue;
                object changed=field.GetValue(tune.awd);field.SetValue(tune.awd,old);
                Stale(profile,"Shop",()=>field.SetValue(tune.awd,changed),()=>field.SetValue(tune.awd,old),check,"Tune.awd."+field.Name);
            }
            Stale(profile,"Shop",()=>tune.awd=null,()=>tune.awd=new AwdSpec(),check,"Tune AWD object present versus null");
            tune.awd=oldAwd;tune.awdConfigured=oldAwdConfigured;

            var installed=profile.SelectedCar.installed;
            foreach(var field in typeof(InstalledBuild).GetFields(BindingFlags.Instance|BindingFlags.Public))
            {
                if(field.Name=="slots")continue;
                object old=field.GetValue(installed);if(!MutateScalar(field,installed))continue;object changed=field.GetValue(installed);field.SetValue(installed,old);
                Stale(profile,"Shop",()=>field.SetValue(installed,changed),()=>field.SetValue(installed,old),check,
                    "InstalledBuild."+field.Name);
            }
            if(installed.slots!=null&&installed.slots.Count>0)
            {
                var slot=installed.slots[0];
                foreach(var field in typeof(SlotBinding).GetFields(BindingFlags.Instance|BindingFlags.Public))
                {
                    object old=field.GetValue(slot);if(!MutateScalar(field,slot))continue;object changed=field.GetValue(slot);field.SetValue(slot,old);
                    Stale(profile,"Shop",()=>field.SetValue(slot,changed),()=>field.SetValue(slot,old),check,
                        "SlotBinding."+field.Name);
                }
                if(installed.slots.Count>1)
                {
                    var priorOrder=installed.slots.ToArray();
                    Stale(profile,"Shop",()=>installed.slots.Reverse(),
                        ()=>{installed.slots.Clear();installed.slots.AddRange(priorOrder);},check,"InstalledBuild slot order");
                }
            }

            var fuel=profile.SelectedCar.fuel;
            SignedZero(profile,"Shop",fuel,"cumulativeUsedLitres",check,"FuelState.cumulativeUsedLitres");
            foreach(var field in typeof(FuelState).GetFields(BindingFlags.Instance|BindingFlags.Public))
            {
                object old=field.GetValue(fuel);MutateScalar(field,fuel);object changed=field.GetValue(fuel);field.SetValue(fuel,old);
                Stale(profile,"Shop",()=>field.SetValue(fuel,changed),()=>field.SetValue(fuel,old),check,"FuelState."+field.Name);
            }
            var fluids=profile.SelectedCar.fluids??(profile.SelectedCar.fluids=new FluidState());
            var priorFluids=profile.SelectedCar.fluids;
            Stale(profile,"Shop",()=>profile.SelectedCar.fluids=null,()=>profile.SelectedCar.fluids=priorFluids,check,
                "Selected-car fluids present versus null");
            SignedZero(profile,"Shop",fluids,"oilConsumedLitres",check,"FluidState.oilConsumedLitres");
            foreach(var field in typeof(FluidState).GetFields(BindingFlags.Instance|BindingFlags.Public))
            {
                object old=field.GetValue(fluids);MutateScalar(field,fluids);object changed=field.GetValue(fluids);field.SetValue(fluids,old);
                Stale(profile,"Shop",()=>field.SetValue(fluids,changed),()=>field.SetValue(fluids,old),check,"FluidState."+field.Name);
            }
            var engineAssemblyId=profile.SelectedCar.engineAssemblyId;
            Stale(profile,"Shop",()=>profile.SelectedCar.engineAssemblyId=engineAssemblyId+"-changed",
                ()=>profile.SelectedCar.engineAssemblyId=engineAssemblyId,check,"selected engine assembly identity");
            bool fuelInitialized=profile.SelectedCar.fuelInitialized;
            Stale(profile,"Shop",()=>profile.SelectedCar.fuelInitialized=!fuelInitialized,
                ()=>profile.SelectedCar.fuelInitialized=fuelInitialized,check,"selected fuel initialization state");
            var garageJobId=profile.SelectedCar.garageJobId;
            Stale(profile,"Shop",()=>profile.SelectedCar.garageJobId="s06d-garage-job",
                ()=>profile.SelectedCar.garageJobId=garageJobId,check,"selected garage job identity");
            var priorRebuild=profile.SelectedCar.engineRebuild;
            var targetA=new EngineRebuildTarget{partId="part-a",definitionId="piston-a",replace=true};
            var targetB=new EngineRebuildTarget{partId="part-b",definitionId="piston-b",replace=false};
            var rebuild=new EngineRebuildJob{active=true,id="s06d-rebuild",engineId="engine-a",
                startedUtcTicks=100,readyUtcTicks=200,paidPrice=300,
                targets=new List<EngineRebuildTarget>{targetA,targetB}};
            var absentRebuildSet=Capture(Inputs(profile,"Shop"),check,"absent engine rebuild");
            profile.SelectedCar.engineRebuild=rebuild;
            Check(check,!absentRebuildSet.MatchesCurrent(Inputs(profile,"Shop"),out _),
                "Engine rebuild present versus null invalidates lock facts");
            foreach(var field in typeof(EngineRebuildJob).GetFields(BindingFlags.Instance|BindingFlags.Public))
            {
                if(field.Name=="targets")continue;
                object old=field.GetValue(rebuild);if(!MutateScalar(field,rebuild))continue;object changed=field.GetValue(rebuild);field.SetValue(rebuild,old);
                Stale(profile,"Shop",()=>field.SetValue(rebuild,changed),()=>field.SetValue(rebuild,old),check,
                    "EngineRebuildJob."+field.Name);
            }
            foreach(var field in typeof(EngineRebuildTarget).GetFields(BindingFlags.Instance|BindingFlags.Public))
            {
                object old=field.GetValue(rebuild.targets[0]);if(!MutateScalar(field,rebuild.targets[0]))continue;
                object changed=field.GetValue(rebuild.targets[0]);field.SetValue(rebuild.targets[0],old);
                Stale(profile,"Shop",()=>field.SetValue(rebuild.targets[0],changed),()=>field.SetValue(rebuild.targets[0],old),check,
                    "EngineRebuildTarget."+field.Name);
            }
            var rebuildInputs=Inputs(profile,"Shop");
            var rebuildSet=Capture(rebuildInputs,check,"rebuild target identity and order");
            rebuild.targets[0]=new EngineRebuildTarget{partId="part-a",definitionId="piston-a",replace=true};
            Check(check,!rebuildSet.MatchesCurrent(Inputs(profile,"Shop"),out _),
                "Rebuild target replacement reusing values is rejected");
            rebuild.targets[0]=new EngineRebuildTarget{partId="part-a",definitionId="piston-a",replace=true};
            Check(check,!rebuildSet.MatchesCurrent(Inputs(profile,"Shop"),out _),
                "Rebuild target source identity remains fenced after a copied replacement");
            rebuild.targets[0]=targetA;
            Stale(profile,"Shop",()=>rebuild.targets.Reverse(),()=>{rebuild.targets[0]=targetA;rebuild.targets[1]=targetB;},
                check,"EngineRebuildTarget source order");
            profile.SelectedCar.engineRebuild=priorRebuild;

            var part=profile.parts.First(p=>p!=null&&p.condition!=null);
            var tirePack=profile.parts.FirstOrDefault(p=>p!=null&&p.condition?.tires?.Length==4&&
                catalog.Find(p.definitionId)?.category==PartCategory.Tires);
            if(tirePack!=null)
            {
                var spareTirePack=tirePack.Copy();
                spareTirePack.instanceId="s06d-spare-tire-pack";
                spareTirePack.location=PartLocation.Inventory;
                for(int i=0;i<spareTirePack.condition.tires.Length;i++)
                    spareTirePack.condition.tires[i].id="s06d-spare-tire-"+i;
                profile.parts.Add(spareTirePack);
                var corner=spareTirePack.condition.tires[0];
                string priorDefinition=corner.definitionId;
                string alternative=catalog.parts.First(d=>d.category==PartCategory.Tires&&d.id!=spareTirePack.definitionId).id;
                var tireOwner=Inputs(profile,"Shop",false,null,null,owner,null);
                var tireSet=Capture(tireOwner,check,"Shop tire corner identity");
                corner.definitionId=alternative;
                Check(check,!tireSet.MatchesCurrent(Inputs(profile,"Shop",false,null,null,owner,null),out _),
                    "Shop ownership ID fence observes same-count per-corner tire definition override");
                corner.definitionId=priorDefinition;
                profile.parts.Remove(spareTirePack);
            }
            foreach(var field in typeof(PartInstance).GetFields(BindingFlags.Instance|BindingFlags.Public))
            {
                if(field.FieldType.IsClass&&field.FieldType!=typeof(string))continue;
                object old=field.GetValue(part);if(!MutateScalar(field,part))continue;object changed=field.GetValue(part);field.SetValue(part,old);
                Stale(profile,"Inventory",()=>field.SetValue(part,changed),()=>field.SetValue(part,old),check,"PartInstance."+field.Name);
            }
            int partIndex=profile.parts.IndexOf(part);
            var sameIdentityPart=part.Copy();
            var partSet=Capture(Inputs(profile,"Inventory"),check,"part source object identity");
            profile.parts[partIndex]=sameIdentityPart;
            Check(check,!partSet.MatchesCurrent(Inputs(profile,"Inventory"),out _),
                "Owned part replacement reusing exact values and instance ID is rejected");
            profile.parts[partIndex]=part;
            var originalPartCondition=part.condition;
            Stale(profile,"Inventory",()=>part.condition=null,()=>part.condition=originalPartCondition,check,
                "Owned PartInstance condition present versus null");
            var condition=part.condition;
            SignedZero(profile,"Inventory",condition,"slipEnergyJ",check,"ComponentCondition.slipEnergyJ");
            foreach(var field in typeof(ComponentCondition).GetFields(BindingFlags.Instance|BindingFlags.Public))
            {
                if(field.Name=="elements"||field.Name=="clutch")continue;
                object old=field.GetValue(condition);if(!MutateScalar(field,condition))continue;object changed=field.GetValue(condition);field.SetValue(condition,old);
                Stale(profile,"Inventory",()=>field.SetValue(condition,changed),()=>field.SetValue(condition,old),check,"ComponentCondition."+field.Name);
            }
            var oldClutch=condition.clutch;condition.clutch=new ClutchConditionState();
            SignedZero(profile,"Inventory",condition.clutch,"oilLitres",check,"ClutchConditionState.oilLitres");
            foreach(var field in typeof(ClutchConditionState).GetFields(BindingFlags.Instance|BindingFlags.Public))
            {
                object old=field.GetValue(condition.clutch);MutateScalar(field,condition.clutch);object changed=field.GetValue(condition.clutch);field.SetValue(condition.clutch,old);
                Stale(profile,"Inventory",()=>field.SetValue(condition.clutch,changed),()=>field.SetValue(condition.clutch,old),check,"ClutchConditionState."+field.Name);
            }
            condition.clutch=oldClutch;

            var element=profile.parts.SelectMany(p=>p?.condition?.elements??Array.Empty<EngineElement>()).FirstOrDefault(e=>e!=null);
            if(element!=null)
            {
                SignedZero(profile,"Inventory",element,"severity",check,"EngineElement.severity");
                foreach(var field in typeof(EngineElement).GetFields(BindingFlags.Instance|BindingFlags.Public))
                {
                    object old=field.GetValue(element);if(!MutateScalar(field,element))continue;object changed=field.GetValue(element);field.SetValue(element,old);
                    Stale(profile,"Inventory",()=>field.SetValue(element,changed),()=>field.SetValue(element,old),check,"EngineElement."+field.Name);
                }
                if(condition.elements!=null&&condition.elements.Length>0)
                {
                    var originalRows=condition.elements;
                    var replacement=new EngineElement{ id=element.id,definitionId=element.definitionId,health=element.health,
                        role=element.role,cylinder=element.cylinder,valve=element.valve,severity=element.severity,
                        wearRemainder=element.wearRemainder,fault=element.fault };
                    Stale(profile,"Inventory",()=>condition.elements=originalRows.Select(x=>ReferenceEquals(x,element)?replacement:x).ToArray(),
                        ()=>condition.elements=originalRows,check,"EngineElement concrete runtime type with equal base fields");
                    if(originalRows.Length>1)
                    {
                        var reordered=originalRows.Reverse().ToArray();
                        Stale(profile,"Inventory",()=>condition.elements=reordered,()=>condition.elements=originalRows,check,
                            "ComponentCondition element order");
                    }
                }
            }
            var tire=profile.parts.SelectMany(p=>p?.condition?.elements??Array.Empty<EngineElement>()).OfType<TireCondition>().FirstOrDefault();
            if(tire!=null)foreach(var field in typeof(TireCondition).GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.DeclaredOnly))
            {
                object old=field.GetValue(tire);if(!MutateScalar(field,tire))continue;object changed=field.GetValue(tire);field.SetValue(tire,old);
                Stale(profile,"Inventory",()=>field.SetValue(tire,changed),()=>field.SetValue(tire,old),check,"TireCondition."+field.Name);
            }
            if(tire!=null)SignedZero(profile,"Inventory",tire,"slipEnergyJ",check,"TireCondition.slipEnergyJ");

            var looseDefinition=catalog.parts.First(d=>d.category==PartCategory.Pistons);
            var looseSource=element?.Copy()??new EngineElement{id="s06d-loose-member",definitionId=looseDefinition.id,role="piston",cylinder=-1,valve=-1};
            var loose=new LooseEngineElement{definitionId=looseDefinition.id,storageId="s06d-loose-storage",
                unitPrice=42,element=looseSource};
            profile.looseEngineElements.Add(loose);
            foreach(var field in typeof(LooseEngineElement).GetFields(BindingFlags.Instance|BindingFlags.Public))
            {
                if(field.Name=="element")continue;
                object old=field.GetValue(loose);if(!MutateScalar(field,loose))continue;object changed=field.GetValue(loose);field.SetValue(loose,old);
                Stale(profile,"Inventory",()=>field.SetValue(loose,changed),()=>field.SetValue(loose,old),check,
                    "LooseEngineElement."+field.Name);
            }
            foreach(var field in typeof(EngineElement).GetFields(BindingFlags.Instance|BindingFlags.Public))
            {
                object old=field.GetValue(loose.element);if(!MutateScalar(field,loose.element))continue;
                object changed=field.GetValue(loose.element);field.SetValue(loose.element,old);
                Stale(profile,"Inventory",()=>field.SetValue(loose.element,changed),()=>field.SetValue(loose.element,old),check,
                    "LooseEngineElement.element."+field.Name);
            }
            int looseIndex=profile.looseEngineElements.IndexOf(loose);
            var sameValueReplacement=new LooseEngineElement{definitionId=loose.definitionId,storageId=loose.storageId,
                unitPrice=loose.unitPrice,tireUnit=loose.tireUnit,element=loose.element.Copy()};
            var looseInputs=Inputs(profile,"Inventory");
            var looseSet=Capture(looseInputs,check,"loose member source identity");
            profile.looseEngineElements[looseIndex]=sameValueReplacement;
            Check(check,!looseSet.MatchesCurrent(Inputs(profile,"Inventory"),out _),
                "Loose member replacement reusing exact values and identity is rejected");
            profile.looseEngineElements[looseIndex]=loose;
            profile.looseEngineElements.Remove(loose);

            var originalElements=condition.elements;
            if(originalElements==null)condition.elements=Array.Empty<EngineElement>();
            var elementsForNullTest=condition.elements;
            Stale(profile,"Inventory",()=>condition.elements=null,()=>condition.elements=elementsForNullTest,check,
                "ComponentCondition.elements null versus present");
            condition.elements=originalElements;
            var originalClutchForNullTest=condition.clutch;
            condition.clutch=new ClutchConditionState();
            Stale(profile,"Inventory",()=>condition.clutch=null,()=>condition.clutch=new ClutchConditionState(),check,
                "ComponentCondition.clutch present versus null");
            condition.clutch=originalClutchForNullTest;

            var line=new PartCartLine{definitionId="cart-a",quantity=2,install=true,preparedBlock=true,
                preparationOwner="owner-a",action=CartAction.Move,assetId="asset-a",role="piston",destinationId="car-a",
                destination=InventoryKind.Car,positions=new List<string>{"left","right"}};
            var nullPositionsLine=new PartCartLine{definitionId="cart-null-positions",quantity=1,positions=null};
            var nullPositionsInputs=Inputs(profile,"Shop",false,new[]{nullPositionsLine},null,owner,null);
            var nullPositionsSet=Capture(nullPositionsInputs,check,"null cart positions baseline");
            Check(check,nullPositionsSet.MatchesCurrent(nullPositionsInputs,out _),
                "A cart line with null positions remains null and matches its captured facts");
            var priorNullPositions=nullPositionsLine.positions;
            try
            {
                nullPositionsLine.positions=new List<string>();
                Check(check,!nullPositionsSet.MatchesCurrent(Inputs(profile,"Shop",false,
                    new[]{nullPositionsLine},null,owner,null),out _),
                    "Changing cart positions from null to empty rejects exact facts");
            }
            finally{nullPositionsLine.positions=priorNullPositions;}
            Check(check,nullPositionsSet.MatchesCurrent(Inputs(profile,"Shop",false,
                new[]{nullPositionsLine},null,owner,null),out _),
                "Restoring null cart positions restores exact parity");
            var lines=new[]{line,new PartCartLine{definitionId="cart-b",quantity=1,action=CartAction.NewUnits,positions=new List<string>()}};
            var original=Inputs(profile,"Shop",true,lines,new UnitPreferences{version=3,speed=SpeedUnit.MilesPerHour,
                pressure=PressureUnit.Psi,torque=TorqueUnit.PoundFeet},owner,new[]{new NativePartsPriceFact("shop","asset-a",15)});
            var setCart=Capture(original,check,"ordered nonempty cart");
            Check(check,setCart.RequiresTypedPreview,"Nonempty typed cart explicitly requires a separate typed preview");
            Check(check,setCart.MatchesCurrent(original,out _),"Nonempty cart base facts do not claim a planned preview");
            Check(check,!setCart.MatchesCurrent(Inputs(profile,"Inventory",true,lines,new UnitPreferences{version=3,
                speed=SpeedUnit.MilesPerHour,pressure=PressureUnit.Psi,torque=TorqueUnit.PoundFeet},owner,
                new[]{new NativePartsPriceFact("shop","asset-a",15)}),out _),"Page identity fences captured facts");
            Check(check,!setCart.MatchesCurrent(Inputs(profile,"Shop",false,lines,new UnitPreferences{version=3,
                speed=SpeedUnit.MilesPerHour,pressure=PressureUnit.Psi,torque=TorqueUnit.PoundFeet},owner,
                new[]{new NativePartsPriceFact("shop","asset-a",15)}),out _),"Planned mode identity fences captured facts");
            string inputPosition=line.positions[0];
            line.positions[0]="mutated-after-input-copy";
            Check(check,setCart.MatchesCurrent(original,out _),"Input wrapper owns copied cart line and ordered positions");
            line.positions[0]=inputPosition;
            foreach(var field in typeof(PartCartLine).GetFields(BindingFlags.Instance|BindingFlags.Public))
            {
                if(field.Name=="positions")
                {
                    var positions=line.positions.ToArray();
                    if(line.positions.Count>0)line.positions[0]+="-changed";else line.positions.Add("changed");
                    Check(check,!setCart.MatchesCurrent(Inputs(profile,"Shop",true,lines,new UnitPreferences{version=3,
                        speed=SpeedUnit.MilesPerHour,pressure=PressureUnit.Psi,torque=TorqueUnit.PoundFeet},owner,
                        new[]{new NativePartsPriceFact("shop","asset-a",15)}),out _),"Cart field change invalidates exact facts: positions");
                    line.positions.Clear();line.positions.AddRange(positions);
                    Check(check,setCart.MatchesCurrent(Inputs(profile,"Shop",true,lines,new UnitPreferences{version=3,
                        speed=SpeedUnit.MilesPerHour,pressure=PressureUnit.Psi,torque=TorqueUnit.PoundFeet},owner,
                        new[]{new NativePartsPriceFact("shop","asset-a",15)}),out _),"Restoring cart position order restores exact facts");
                }
                else
                {
                    object old=field.GetValue(line);
                    if(!MutateScalar(field,line))continue;
                    object changed=field.GetValue(line);
                    Check(check,!setCart.MatchesCurrent(Inputs(profile,"Shop",true,lines,new UnitPreferences{version=3,
                        speed=SpeedUnit.MilesPerHour,pressure=PressureUnit.Psi,torque=TorqueUnit.PoundFeet},owner,
                        new[]{new NativePartsPriceFact("shop","asset-a",15)}),out _),
                        "Cart field change invalidates exact facts: "+field.Name);
                    field.SetValue(line,old);
                    Check(check,setCart.MatchesCurrent(Inputs(profile,"Shop",true,lines,new UnitPreferences{version=3,
                        speed=SpeedUnit.MilesPerHour,pressure=PressureUnit.Psi,torque=TorqueUnit.PoundFeet},owner,
                        new[]{new NativePartsPriceFact("shop","asset-a",15)}),out _),
                        "Restoring cart field restores exact facts: "+field.Name);
                }
            }
            var changedLines=new[]{lines[0].Copy(),lines[1].Copy()};changedLines[1].quantity++;
            Check(check,!setCart.MatchesCurrent(Inputs(profile,"Shop",true,changedLines,new UnitPreferences{version=3,
                speed=SpeedUnit.MilesPerHour,pressure=PressureUnit.Psi,torque=TorqueUnit.PoundFeet},owner,
                new[]{new NativePartsPriceFact("shop","asset-a",15)}),out _),"Cart quantity/order fields are compared from fresh typed input");
            var reversedLines=lines.Reverse().Select(l=>l.Copy()).ToArray();
            Check(check,!setCart.MatchesCurrent(Inputs(profile,"Shop",true,reversedLines,new UnitPreferences{version=3,
                speed=SpeedUnit.MilesPerHour,pressure=PressureUnit.Psi,torque=TorqueUnit.PoundFeet},owner,
                new[]{new NativePartsPriceFact("shop","asset-a",15)}),out _),"Cart line source ordering is part of exact read facts");
            Check(check,!setCart.MatchesCurrent(Inputs(profile,"Shop",true,lines,new UnitPreferences{version=3,
                speed=SpeedUnit.MilesPerHour,pressure=PressureUnit.Psi,torque=TorqueUnit.PoundFeet},owner,
                new[]{new NativePartsPriceFact("shop","asset-a",16)}),out _),"Supplied exact price fact invalidates read set");
            Check(check,!setCart.MatchesCurrent(Inputs(profile,"Shop",true,lines,new UnitPreferences{version=3,
                speed=SpeedUnit.MilesPerHour,pressure=PressureUnit.Psi,torque=TorqueUnit.PoundFeet},owner,
                new[]{new NativePartsPriceFact("shop","asset-b",15)}),out _),"Supplied price identity invalidates read set");

            var prefs=new UnitPreferences{version=3,speed=SpeedUnit.MilesPerHour,pressure=PressureUnit.Psi,torque=TorqueUnit.PoundFeet};
            foreach(var field in typeof(UnitPreferences).GetFields(BindingFlags.Instance|BindingFlags.Public))
            {
                object old=field.GetValue(prefs);MutateScalar(field,prefs);object changed=field.GetValue(prefs);field.SetValue(prefs,old);
                var baseline=Capture(Inputs(profile,"Shop",false,null,prefs,owner,null),check,"unit preference baseline / "+field.Name);
                field.SetValue(prefs,changed);
                Check(check,!baseline.MatchesCurrent(Inputs(profile,"Shop",false,null,prefs,owner,null),out _),"Unit preference invalidates read set: "+field.Name);
                field.SetValue(prefs,old);
            }

            long revisions=profile.revision;Stale(profile,"Shop",()=>profile.revision++,()=>profile.revision=revisions,check,"profile revision");
            long storage=profile.storageCommit;Stale(profile,"Shop",()=>profile.storageCommit++,()=>profile.storageCommit=storage,check,"profile storage commit");
            int cash=profile.cash;Stale(profile,"Shop",()=>profile.cash++,()=>profile.cash=cash,check,"profile cash");
            int stockIndex=profile.inventory.market.parts.FindIndex(row=>row!=null);
            if(stockIndex>=0)
            {
                var row=profile.inventory.market.parts[stockIndex];int units= row.units;
                var stockInputs=Inputs(profile,"Shop",false,null,null,owner,null);
                var stockSet=Capture(stockInputs,check,"market stock source identity");
                var sameStock=new NewPartStock{definitions=row.definitions==null?null:new List<string>(row.definitions),
                    units=row.units,initialUnits=row.initialUnits,deliveryPercent=row.deliveryPercent,discovered=row.discovered};
                profile.inventory.market.parts[stockIndex]=sameStock;
                Check(check,!stockSet.MatchesCurrent(Inputs(profile,"Shop",false,null,null,owner,null),out _),
                    "Market stock row replacement with same values is rejected");
                profile.inventory.market.parts[stockIndex]=row;
                foreach(var field in typeof(NewPartStock).GetFields(BindingFlags.Instance|BindingFlags.Public))
                {
                    if(field.Name=="definitions")continue;
                    object old=field.GetValue(row);if(!MutateScalar(field,row))continue;object changed=field.GetValue(row);field.SetValue(row,old);
                    Stale(profile,"Shop",()=>field.SetValue(row,changed),()=>field.SetValue(row,old),check,
                        "NewPartStock."+field.Name);
                }
                Stale(profile,"Shop",()=>row.units=units+1,()=>row.units=units,check,"market live stock units");
                int delivery=row.deliveryPercent;
                Stale(profile,"Shop",()=>row.deliveryPercent=delivery+1,()=>row.deliveryPercent=delivery,check,"market delivery percentage");
                var originalDefinitions=row.definitions==null?null:new List<string>(row.definitions);
                Stale(profile,"Shop",()=>row.definitions=row.definitions==null?new List<string>{"s06d-alias"}:
                    new List<string>(row.definitions.Concat(new[]{"s06d-alias"})),
                    ()=>row.definitions=originalDefinitions,check,"market stock definition aliases");
            }
            if(profile.inventory.market.parts.Count>1)
            {
                var priorStockOrder=profile.inventory.market.parts.ToArray();
                Stale(profile,"Shop",()=>profile.inventory.market.parts.Reverse(),
                    ()=>{profile.inventory.market.parts.Clear();profile.inventory.market.parts.AddRange(priorStockOrder);},
                    check,"market stock source order");
            }
            int marketGeneration=profile.inventory.market.generation;
            Stale(profile,"Shop",()=>profile.inventory.market.generation++,()=>profile.inventory.market.generation=marketGeneration,check,"market generation");
            var marketVersion=profile.inventory.market.version;
            Stale(profile,"Shop",()=>profile.inventory.market.version++,()=>profile.inventory.market.version=marketVersion,check,"market version");
            var marketSeed=profile.inventory.market.seed;
            Stale(profile,"Shop",()=>profile.inventory.market.seed++,()=>profile.inventory.market.seed=marketSeed,check,"market seed");
            var guarantee=profile.inventory.market.guaranteeAtRefresh;
            Stale(profile,"Shop",()=>profile.inventory.market.guaranteeAtRefresh++,()=>profile.inventory.market.guaranteeAtRefresh=guarantee,check,"market fitting guarantee");
            var priorHold=profile.inventory.cart;
            profile.inventory.cart=new CartReservationState{version=1,createdRun=3,expiresRun=12,generation=4,
                carId=profile.SelectedCar.instanceId,lines=new List<PartCartLine>{line.Copy(),
                    new PartCartLine{definitionId="hold-second",quantity=1,action=CartAction.NewUnits}},
                supply=new List<ReservedPartSupply>{new ReservedPartSupply{definitionId=part.definitionId,units=2,generation=4}}};
            var heldInputs=Inputs(profile,"Shop",false,null,new UnitPreferences(),owner,null);
            var held=Capture(heldInputs,check,"active cart reservation");
            var reservationPositions=profile.inventory.cart.lines[0].positions;
            try
            {
                profile.inventory.cart.lines[0].positions=null;
                var nullReservationInputs=Inputs(profile,"Shop",false,null,new UnitPreferences(),owner,null);
                var nullReservationSet=Capture(nullReservationInputs,check,"null reservation line positions");
                Check(check,nullReservationSet.MatchesCurrent(nullReservationInputs,out _),
                    "A reservation line with null positions remains null and matches captured facts");
                profile.inventory.cart.lines[0].positions=new List<string>();
                Check(check,RejectsWithoutThrow(()=>nullReservationSet.MatchesCurrent(
                    Inputs(profile,"Shop",false,null,new UnitPreferences(),owner,null),out _)),
                    "Reservation positions null-to-empty change safely rejects exact facts");
            }
            finally{profile.inventory.cart.lines[0].positions=reservationPositions;}
            profile.inventory.cart.createdRun++;
            Check(check,!held.MatchesCurrent(Inputs(profile,"Shop",false,null,new UnitPreferences(),owner,null),out _),"Active hold clock invalidates read set");
            profile.inventory.cart.createdRun=3;
            profile.inventory.cart.version++;
            Check(check,!held.MatchesCurrent(Inputs(profile,"Shop",false,null,new UnitPreferences(),owner,null),out _),"Active hold version invalidates read set");
            profile.inventory.cart.version=1;
            profile.inventory.cart.expiresRun++;
            Check(check,!held.MatchesCurrent(Inputs(profile,"Shop",false,null,new UnitPreferences(),owner,null),out _),"Active hold expiry invalidates read set");
            profile.inventory.cart.expiresRun=12;
            profile.inventory.cart.generation++;
            Check(check,!held.MatchesCurrent(Inputs(profile,"Shop",false,null,new UnitPreferences(),owner,null),out _),"Active hold stock generation invalidates read set");
            profile.inventory.cart.generation=4;
            profile.inventory.cart.carId="changed-car";
            Check(check,!held.MatchesCurrent(Inputs(profile,"Shop",false,null,new UnitPreferences(),owner,null),out _),"Active hold vehicle identity invalidates read set");
            profile.inventory.cart.carId=profile.SelectedCar.instanceId;
            profile.inventory.cart.supply[0].units++;
            Check(check,!held.MatchesCurrent(Inputs(profile,"Shop",false,null,new UnitPreferences(),owner,null),out _),"Active held supply quantity invalidates read set");
            profile.inventory.cart.supply[0].units--;
            var reservedSupply=profile.inventory.cart.supply[0];
            var reservedDefinition=reservedSupply.definitionId;
            reservedSupply.definitionId=reservedDefinition+"-changed";
            Check(check,!held.MatchesCurrent(Inputs(profile,"Shop",false,null,new UnitPreferences(),owner,null),out _),"Active held supply definition invalidates read set");
            reservedSupply.definitionId=reservedDefinition;
            int reservedGeneration=reservedSupply.generation;
            reservedSupply.generation++;
            Check(check,!held.MatchesCurrent(Inputs(profile,"Shop",false,null,new UnitPreferences(),owner,null),out _),"Active held supply generation invalidates read set");
            reservedSupply.generation=reservedGeneration;
            profile.inventory.cart.lines[0].positions.Add("changed-order");
            Check(check,!held.MatchesCurrent(Inputs(profile,"Shop",false,null,new UnitPreferences(),owner,null),out _),"Active hold line ordered positions invalidate read set");
            profile.inventory.cart.lines[0].positions.RemoveAt(profile.inventory.cart.lines[0].positions.Count-1);
            profile.inventory.cart.lines.Reverse();
            Check(check,!held.MatchesCurrent(Inputs(profile,"Shop",false,null,new UnitPreferences(),owner,null),out _),"Active hold line order invalidates read set");
            profile.inventory.cart.lines.Reverse();
            profile.inventory.cart=priorHold;

            int dealerCount=profile.inventory.dealer.Count;
            var dealerPart=new PartInstance{instanceId="s06d-dealer-part",definitionId=part.definitionId,
                condition=new ComponentCondition()};
            var dealerRow=new DealerStock{isUnit=false,part=dealerPart,paidPrice=90};
            profile.inventory.dealer.Add(dealerRow);
            int dealerPaid=dealerRow.paidPrice;
            Stale(profile,"Shop",()=>dealerRow.paidPrice=dealerPaid+1,()=>dealerRow.paidPrice=dealerPaid,check,
                "dealer paid price");
            string dealerDefinition=dealerRow.part.definitionId;
            Stale(profile,"Shop",()=>dealerRow.part.definitionId=dealerDefinition+"-changed",
                ()=>dealerRow.part.definitionId=dealerDefinition,check,"dealer part definition identity");
            float dealerHealth=dealerRow.part.condition.health;
            Stale(profile,"Shop",()=>dealerRow.part.condition.health=dealerHealth-.125f,
                ()=>dealerRow.part.condition.health=dealerHealth,check,"dealer part condition");
            var dealerSourceSet=Capture(Inputs(profile,"Shop"),check,"dealer source identity");
            var dealerCopy=new DealerStock{isUnit=dealerRow.isUnit,part=dealerRow.part.Copy(),paidPrice=dealerRow.paidPrice};
            profile.inventory.dealer[dealerCount]=dealerCopy;
            Check(check,!dealerSourceSet.MatchesCurrent(Inputs(profile,"Shop"),out _),
                "Dealer row replacement with equivalent values is rejected");
            profile.inventory.dealer[dealerCount]=dealerRow;
            profile.inventory.dealer.RemoveRange(dealerCount,profile.inventory.dealer.Count-dealerCount);

            string selectedSlot=ProfileStore.SelectedSlot;
            var liveOwner=NativePartsOwnerContext.Capture();
            var liveInputs=Inputs(profile,"Shop",false,null,null,liveOwner,null);
            var liveSet=Capture(liveInputs,check,"live owner context");
            var liveSession=NativePartsBaseReadSet.BeginCapture(liveInputs);
            liveSession.Advance(1,2.0);
            string temporarySlot="s06d-readset-"+Guid.NewGuid().ToString("N").Substring(0,12);
            try
            {
                ProfileStore.UseSlot(temporarySlot);
                Check(check,!liveSet.MatchesCurrent(liveInputs,out _),"Live selected owner slot/epoch change rejects even the original stale input wrapper");
                Check(check,!liveSession.Advance(1,2.0)&&liveSession.Failed&&
                    !string.IsNullOrEmpty(liveSession.FailureReason),
                    "Live owner change exposes a terminal capture failure without owner-thread waiting");
                Check(check,!liveSession.TryFinish(liveInputs,out var ownerInterruptedSet,out var ownerInterruptedReason)&&
                    ownerInterruptedSet==null&&!string.IsNullOrEmpty(ownerInterruptedReason),
                    "Live-owner interrupted capture cannot publish a partial read set");
            }
            finally{ProfileStore.UseSlot(selectedSlot);}
            owner=NativePartsOwnerContext.Capture();

            // Scope check: Shop captures the selected inspection car and owned IDs, but not wear on a different car.
            var otherVehicle=vehicles.FirstOrDefault(v=>v.spec.id!=vehicle.spec.id);
            if(otherVehicle!=null)
            {
                VehicleCollection.Buy(profile,catalog,new VehicleOffer{model=otherVehicle.spec.id,price=1,
                    reputation=0,stock=otherVehicle.CreateStockTune(),paint="#FFFFFF"});
                var other=profile.cars.First(c=>c.vehicleId==otherVehicle.spec.id);
                string selectedId=profile.selectedOwnedVehicleId;string selectedModel=profile.selectedVehicleId;
                profile.selectedOwnedVehicleId=profile.cars.First(c=>c.vehicleId==vehicle.spec.id).instanceId;
                profile.selectedVehicleId=vehicle.spec.id;
                var scopeInputs=Inputs(profile,"Shop",false,null,null,owner,null);
                var scopeSet=Capture(scopeInputs,check,"Shop unrelated-car scope");
                var unrelated=profile.parts.FirstOrDefault(p=>p.ownerId==other.instanceId);
                if(unrelated!=null&&unrelated.condition!=null)
                {
                    float health=unrelated.condition.health;unrelated.condition.health=health-.125f;
                    Check(check,scopeSet.MatchesCurrent(Inputs(profile,"Shop",false,null,null,owner,null),out _),
                        "Shop read-set ignores same-revision wear on an unrelated car");
                    unrelated.condition.health=health;
                }
                else
                {
                    float mass=other.tune.driverMassKg;
                    other.tune.driverMassKg=mass+1;
                    Check(check,scopeSet.MatchesCurrent(Inputs(profile,"Shop",false,null,null,owner,null),out _),
                        "Shop read-set ignores same-revision tune changes on an unrelated car");
                    other.tune.driverMassKg=mass;
                }
                profile.selectedOwnedVehicleId=selectedId;profile.selectedVehicleId=selectedModel;
            }
        }
    }
#endif
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DragLab.Data;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DragLab.Runtime
{
#if UNITY_EDITOR
    /// <summary>Exact old-path parity and descriptive timing for fresh vehicle-listing validation.</summary>
    public static class VehicleListingValidationChecks
    {
        sealed class Outcome
        {
            public string Error, ExceptionType, ExceptionMessage;
        }

        sealed class StageInput
        {
            public VehicleOffer Offer;
            public Dictionary<PartCategory,string> Map;
            public BuildResolution Build;
        }

        static Outcome Invoke(Func<string> action)
        {
            try { return new Outcome { Error = action() }; }
            catch (Exception error) { return new Outcome { ExceptionType = error.GetType().FullName, ExceptionMessage = error.Message }; }
        }

        // Frozen pre-optimization VehicleListings.Validate body. Keep ordering and the
        // repeated VehicleInspection.Validate call intact as the compatibility oracle.
        static string LegacyValidate(PlayerProfile p, MechanicalCatalog c)
        {
            var stock=p.vehicleStock;if(stock==null||stock.version!=1||stock.seed==0||stock.generation<0||stock.purchases<0||stock.completedRuns<0||stock.knowledgeRevision<0||stock.listings==null||stock.listings.Count>100)return "Invalid vehicle market.";if(p.firstPurchasePending&&(stock.listings.Count!=3||stock.listings.Any(o=>o==null||!o.firstStarter)))return "Initial market must contain the three fully disclosed starter choices.";var ids=new HashSet<string>();var components=new HashSet<string>(p.parts.Select(i=>i.instanceId));
            foreach(var o in stock.listings){if(o==null||string.IsNullOrEmpty(o.listingId)||!ids.Add(o.listingId)||o.price<=0||o.cataloguePrice<0||o.reputation<0||o.stock==null||o.parts==null||o.fuel==null||o.fluids==null||!MechanicsSchema.Finite(o.mileageKm)||o.mileageKm<0)return "Invalid vehicle listing.";if(VehicleInspection.Validate(o)!=null)return VehicleInspection.Validate(o);var spec=c.Spec(o.model);var map=new Dictionary<PartCategory,string>();foreach(var part in o.parts){var d=c.Find(part.definitionId);if(d==null||!c.Fits(spec,d)||!components.Add(part.instanceId)||part.condition.Validate()!=null||(part.condition.elements??Array.Empty<EngineElement>()).Any(e=>!EngineElements.FaultAllowed(d.category,e))||map.ContainsKey(d.category))return "Invalid listing component identity/fitment/condition.";map.Add(d.category,d.id);}var build=c.Resolve(o.model,map,o.stock);if(!build.Drivable||o.fuel.Validate(build.spec.fuel)!=null||o.fluids.Validate(build.spec.fluids)!=null)return "Invalid listing build/resources.";}
            return null;
        }

        static void Check(Action<bool,string> check, bool condition, string label)
        {
            check(condition,label);
            if(!condition)throw new InvalidOperationException("Vehicle listing validation parity failed: "+label);
        }

        static void Compare(Action<bool,string> check, PlayerProfile profile, MechanicalCatalog catalog, string label)
        {
            var expected=Invoke(()=>LegacyValidate(profile,catalog));
            var actual=Invoke(()=>VehicleListings.Validate(profile,catalog));
            Check(check,expected.ExceptionType==actual.ExceptionType,label+" exact exception type");
            Check(check,expected.ExceptionMessage==actual.ExceptionMessage,label+" exact exception message");
            Check(check,expected.Error==actual.Error,label+" exact first error / acceptance");
        }

        static void ProofChecks(Action<bool,string> check,PlayerProfile profile,MechanicalCatalog catalog,string fixtureDirectory)
        {
            // Establish a ready authored token outside the operation being measured.
            foreach(var unused in MarketCatalogLayout.WarmSteps(catalog)){}
            Check(check,MarketCatalogLayout.TryCaptureReadyContentToken(catalog,out _),"proof fixture has a ready authored-content token");
            Check(check,profile!=null&&profile.vehicleStock.listings.Count>3&&profile.cars.Count==2,
                "proof field fixture uses the actual full-roster post-purchase market");
            Check(check,VehicleListings.ValidateWithoutProofForChecks(profile,catalog)==null,"proof fixture passes unchanged core");
            // Retire any proof made by the earlier parity/timing checks, then seed
            // from the original full-roster state through normal validation.
            var seedOffer=profile.vehicleStock.listings[0];int seedPrice=seedOffer.price;
            seedOffer.price=seedPrice+1;
            var coreBefore=VehicleListings.ValidationCoreRunsForChecks;
            var hitBefore=VehicleListings.ValidationProofHitsForChecks;
            Check(check,VehicleListings.Validate(profile,catalog)==null,"modified full-roster seed remains accepted");
            seedOffer.price=seedPrice;
            Check(check,!VehicleListings.DirectProofMatchesForChecks(profile,catalog),"restored full-roster state requires fresh proof");
            coreBefore=VehicleListings.ValidationCoreRunsForChecks;hitBefore=VehicleListings.ValidationProofHitsForChecks;
            using(RuntimeDataEncodingProbe.Measure("vehicle-listing-validation-proof-seed"))
                Check(check,VehicleListings.Validate(profile,catalog)==null,"proof seed remains accepted");
            Check(check,VehicleListings.ValidationCoreRunsForChecks==coreBefore+1,"first call after oracle performs full core");
            Check(check,VehicleListings.ValidationProofHitsForChecks==hitBefore,"first call after oracle does not claim a proof hit");
            Check(check,VehicleListings.DirectProofMatchesForChecks(profile,catalog),"successful validation publishes matching positive proof");

            long json=RuntimeDataEncodingProbe.PartConsumerJsonCallCount,key=RuntimeDataEncodingProbe.PartConsumerLegacyKeyCallCount;
            coreBefore=VehicleListings.ValidationCoreRunsForChecks;hitBefore=VehicleListings.ValidationProofHitsForChecks;
            string result;
            using(RuntimeDataEncodingProbe.Measure("vehicle-listing-validation-proof-hit")) result=VehicleListings.Validate(profile,catalog);
            Check(check,result==null,"unchanged proof hit preserves acceptance");
            Check(check,VehicleListings.ValidationCoreRunsForChecks==coreBefore,"unchanged proof hit skips full core");
            Check(check,VehicleListings.ValidationProofHitsForChecks==hitBefore+1,"unchanged fresh invocation records one proof hit");
            Check(check,RuntimeDataEncodingProbe.PartConsumerJsonCallCount==json&&RuntimeDataEncodingProbe.PartConsumerLegacyKeyCallCount==key,
                "proof hit performs no scoped JSON or legacy-key encoding");
            Check(check,VehicleListings.DirectProofMatchesForChecks(profile,catalog),"direct proof inspection is non-destructive");
            ProbePublicFields(check,profile,catalog,fixtureDirectory);

            var rows=profile.vehicleStock.listings;
            var row0=rows[0];rows[0]=rows[1];rows[1]=row0;
            Check(check,!VehicleListings.DirectProofMatchesForChecks(profile,catalog),"stock listing order is part of the exact proof");
            rows[1]=rows[0];rows[0]=row0;
            Check(check,VehicleListings.DirectProofMatchesForChecks(profile,catalog),"stock order restoration preserves pure proof");
            var parts=profile.vehicleStock.listings[0].parts;
            if(parts.Count>1)
            {
                var part0=parts[0];parts[0]=parts[1];parts[1]=part0;
                Check(check,!VehicleListings.DirectProofMatchesForChecks(profile,catalog),"offer part order is part of the exact proof");
                parts[1]=parts[0];parts[0]=part0;
                Check(check,VehicleListings.DirectProofMatchesForChecks(profile,catalog),"offer part order restoration preserves pure proof");
            }
            var memberArray=profile.vehicleStock.listings.SelectMany(offer=>offer.parts)
                .Select(part=>part.condition.elements).FirstOrDefault(array=>array!=null&&array.Length>0&&array[0]!=null);
            if(memberArray!=null)
            {
                var alternate=profile.vehicleStock.listings.SelectMany(offer=>offer.parts)
                    .SelectMany(part=>part.condition.elements??Array.Empty<EngineElement>())
                    .FirstOrDefault(element=>element!=null&&element.GetType()!=memberArray[0].GetType());
                if(alternate!=null)
                {
                    var original=memberArray[0];memberArray[0]=alternate;
                    Check(check,!VehicleListings.DirectProofMatchesForChecks(profile,catalog),"engine member subtype replacement invalidates proof");
                    memberArray[0]=original;
                    Check(check,VehicleListings.DirectProofMatchesForChecks(profile,catalog),"engine member subtype restoration preserves pure proof");
                }
                var originalMember=memberArray[0];memberArray[0]=null;
                Check(check,!VehicleListings.DirectProofMatchesForChecks(profile,catalog),"null engine member invalidates proof");
                memberArray[0]=originalMember;
                Check(check,VehicleListings.DirectProofMatchesForChecks(profile,catalog),"engine member restoration preserves pure proof");
            }
            var notes=profile.vehicleStock.listings[0].historyNotes;
            if(notes.Count>1)
            {
                string note0=notes[0];notes[0]=notes[1];notes[1]=note0;
                Check(check,!VehicleListings.DirectProofMatchesForChecks(profile,catalog),"ordered disclosure history is part of the exact proof");
                notes[1]=notes[0];notes[0]=note0;
                Check(check,VehicleListings.DirectProofMatchesForChecks(profile,catalog),"history order restoration preserves pure proof");
            }
            bool pending=profile.firstPurchasePending;profile.firstPurchasePending=!pending;
            Check(check,!VehicleListings.DirectProofMatchesForChecks(profile,catalog),"first-purchase state is part of the proof");
            profile.firstPurchasePending=pending;
            Check(check,VehicleListings.DirectProofMatchesForChecks(profile,catalog),"first-purchase restoration preserves pure proof");
            var nanOffer=profile.vehicleStock.listings[0];double mileage=nanOffer.mileageKm;nanOffer.mileageKm=double.NaN;
            Check(check,!VehicleListings.DirectProofMatchesForChecks(profile,catalog),"NaN mileage bits differ from the certified finite value");
            nanOffer.mileageKm=mileage;
            Check(check,VehicleListings.DirectProofMatchesForChecks(profile,catalog),"mileage restoration preserves pure proof");
            float rpm=nanOffer.stock.launchRpm;nanOffer.stock.launchRpm=float.NaN;
            Check(check,!VehicleListings.DirectProofMatchesForChecks(profile,catalog),"NaN tune bits differ from the certified finite value");
            nanOffer.stock.launchRpm=rpm;
            Check(check,VehicleListings.DirectProofMatchesForChecks(profile,catalog),"tune restoration preserves pure proof");

            // A same-revision B value must retire A even after the leaf returns to its
            // original value; the first restored call runs the full oracle path again.
            var offer=profile.vehicleStock.listings[0];int oldPrice=offer.price;
            offer.price=0;
            Check(check,!VehicleListings.DirectProofMatchesForChecks(profile,catalog),"same-revision listing edit invalidates exact proof");
            Check(check,Invoke(()=>VehicleListings.Validate(profile,catalog)).Error=="Invalid vehicle listing.","edited listing returns legacy first error");
            Check(check,!VehicleListings.DirectProofMatchesForChecks(profile,catalog),"invalid edited state is not certified");
            offer.price=oldPrice;
            Check(check,!VehicleListings.DirectProofMatchesForChecks(profile,catalog),"restoring A does not resurrect retired proof");
            coreBefore=VehicleListings.ValidationCoreRunsForChecks;hitBefore=VehicleListings.ValidationProofHitsForChecks;
            Compare(check,profile,catalog,"restored A after proof retirement");
            Check(check,VehicleListings.ValidationCoreRunsForChecks==coreBefore+1&&VehicleListings.ValidationProofHitsForChecks==hitBefore,
                "restored A is fully revalidated before a fresh certificate");
            Check(check,VehicleListings.DirectProofMatchesForChecks(profile,catalog),"restored valid A receives fresh proof");

            var replacement=PlayerProfile.NewGame(638000000000000000L);
            replacement.vehicleStock=profile.vehicleStock;replacement.parts=profile.parts;replacement.firstPurchasePending=profile.firstPurchasePending;
            Check(check,!VehicleListings.DirectProofMatchesForChecks(replacement,catalog),"different profile identity cannot borrow a certificate");
            coreBefore=VehicleListings.ValidationCoreRunsForChecks;
            Compare(check,replacement,catalog,"replacement profile with identical leaf references");
            Check(check,VehicleListings.ValidationCoreRunsForChecks==coreBefore+1,"replacement profile executes full core");

            var sameContentCatalog=new MechanicalCatalog(Resources.LoadAll<VehicleDefinition>("Data/Vehicles").Select(vehicle=>vehicle.spec));
            foreach(var unused in MarketCatalogLayout.WarmSteps(sameContentCatalog)){}
            Check(check,!VehicleListings.DirectProofMatchesForChecks(profile,sameContentCatalog),"catalog object identity is part of the proof bucket");
            coreBefore=VehicleListings.ValidationCoreRunsForChecks;
            Compare(check,profile,sameContentCatalog,"same-content replacement catalog");
            Check(check,VehicleListings.ValidationCoreRunsForChecks==coreBefore+1,"replacement catalog executes full core");

            // A cold catalog cannot be synchronously warmed by Validate. The normal
            // full validation still accepts it, and the next call remains a core run.
            var coldCatalog=new MechanicalCatalog(Resources.LoadAll<VehicleDefinition>("Data/Vehicles").Select(vehicle=>vehicle.spec));
            var cold=PlayerProfile.NewGame(638000000000000000L);
            VehicleListings.Initialize(cold,coldCatalog,103);MechanicsOwnership.Ensure(cold,coldCatalog);
            Check(check,!MarketCatalogLayout.TryCaptureReadyContentToken(coldCatalog,out _),"cold catalog starts without ready token");
            coreBefore=VehicleListings.ValidationCoreRunsForChecks;
            Check(check,VehicleListings.Validate(cold,coldCatalog)==null,"cold no-token validation retains full acceptance");
            Check(check,VehicleListings.ValidationCoreRunsForChecks==coreBefore+1,"cold no-token validation runs full core");
            Check(check,!MarketCatalogLayout.TryCaptureReadyContentToken(coldCatalog,out _),"cold validation does not warm catalog token");
            coreBefore=VehicleListings.ValidationCoreRunsForChecks;
            Check(check,VehicleListings.Validate(cold,coldCatalog)==null&&VehicleListings.ValidationCoreRunsForChecks==coreBefore+1,
                "no-token validation never captures a reusable proof");
            var zeroListings=PlayerProfile.NewGame(638000000000000000L);
            zeroListings.vehicleStock=new VehicleStockState{version=1,seed=1,listings=new List<VehicleOffer>()};
            zeroListings.firstPurchasePending=false;
            Compare(check,zeroListings,null,"zero listings preserve legacy null-catalog acceptance");
            Compare(check,null,null,"null profile and catalog preserve legacy exception");
        }

        static double Median(double[] values) => values.OrderBy(value=>value).ElementAt(values.Length/2);

        static string MedianAndMax(Action action,int repeats)
        {
            action();var samples=new double[repeats];
            for(int i=0;i<repeats;i++){var watch=Stopwatch.StartNew();action();watch.Stop();samples[i]=watch.Elapsed.TotalMilliseconds;}
            return "median_ms="+Median(samples).ToString("R",CultureInfo.InvariantCulture)+" max_ms="+samples.Max().ToString("R",CultureInfo.InvariantCulture)+" samples="+repeats.ToString(CultureInfo.InvariantCulture);
        }

        static bool TryFlip(FieldInfo field,object owner,out object oldValue)
        {
            oldValue=field.GetValue(owner);var type=field.FieldType;
            if(type==typeof(bool)){field.SetValue(owner,!(bool)oldValue);return true;}
            if(type==typeof(string)){field.SetValue(owner,(string)oldValue+"/validation-probe");return true;}
            if(type==typeof(int)){field.SetValue(owner,unchecked((int)oldValue+1));return true;}
            if(type==typeof(long)){field.SetValue(owner,unchecked((long)oldValue+1));return true;}
            if(type==typeof(float))
            {
                float current=(float)oldValue;int bits=BitConverter.ToInt32(BitConverter.GetBytes(current),0)^1;
                float changed=BitConverter.ToSingle(BitConverter.GetBytes(bits),0);
                field.SetValue(owner,changed);return BitConverter.ToInt32(BitConverter.GetBytes(changed),0)!=BitConverter.ToInt32(BitConverter.GetBytes(current),0);
            }
            if(type==typeof(double))
            {
                double current=(double)oldValue;long bits=BitConverter.ToInt64(BitConverter.GetBytes(current),0)^1L;
                double changed=BitConverter.ToDouble(BitConverter.GetBytes(bits),0);
                field.SetValue(owner,changed);return BitConverter.DoubleToInt64Bits(changed)!=BitConverter.DoubleToInt64Bits(current);
            }
            if(type.IsEnum){field.SetValue(owner,Enum.ToObject(type,Convert.ToInt64(oldValue)+1));return true;}
            if(!type.IsValueType)
            {
                if(oldValue!=null){field.SetValue(owner,null);return true;}
                try
                {
                    object replacement=type.IsArray?Array.CreateInstance(type.GetElementType(),0):
                        type.IsAbstract||type.IsInterface?null:Activator.CreateInstance(type,true);
                    if(replacement!=null){field.SetValue(owner,replacement);return true;}
                }
                catch{ }
            }
            return false;
        }

        static void ProbePublicFields(Action<bool,string> check,PlayerProfile profile,MechanicalCatalog catalog,string fixtureDirectory)
        {
            var owners=new List<object>{profile.vehicleStock};
            foreach(var offer in profile.vehicleStock.listings)
            {
                owners.Add(offer);owners.Add(offer.stock);owners.Add(offer.fuel);owners.Add(offer.fluids);
                if(offer.stock.awd!=null)owners.Add(offer.stock.awd);
                foreach(var part in offer.parts)
                {
                    owners.Add(part);owners.Add(part.condition);
                    if(part.condition.clutch!=null)owners.Add(part.condition.clutch);
                    foreach(var element in part.condition.elements??Array.Empty<EngineElement>())if(element!=null)owners.Add(element);
                }
            }
            var uniqueOwners=owners.Where(value=>value!=null).GroupBy(value=>value.GetType()).Select(group=>group.First()).ToArray();
            var ownerTypes=new HashSet<Type>(uniqueOwners.Select(owner=>owner.GetType()));
            var requiredTypes=new[]{typeof(VehicleStockState),typeof(VehicleOffer),typeof(Tune),typeof(FuelState),typeof(FluidState),
                typeof(PartInstance),typeof(ComponentCondition),typeof(ClutchConditionState),typeof(AwdSpec),typeof(TireCondition),
                typeof(PistonElement),typeof(ConnectingRodElement),typeof(PistonRingElement),typeof(BearingElement),typeof(ValveElement),
                typeof(ValveSpringElement),typeof(ValveSealElement),typeof(FollowerElement),typeof(IgnitionElement),typeof(SparkPlugElement),
                typeof(GlowPlugElement),typeof(FuelSystemElement),typeof(WheelElement)};
            foreach(var required in requiredTypes)
                Check(check,ownerTypes.Contains(required),"actual full-market proof fixture contains required type "+required.Name);

            int probed=0;
            foreach(var owner in uniqueOwners)
            for(var type=owner.GetType();type!=null&&type!=typeof(object);type=type.BaseType)
            foreach(var field in type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.DeclaredOnly))
            {
                if(field.IsNotSerialized)continue;
                Check(check,!field.IsInitOnly,"proof leaf public field is mutable for reflection probe "+type.Name+"."+field.Name);
                Check(check,TryFlip(field,owner,out var oldValue),"proof leaf reflection probe supports "+type.Name+"."+field.Name);
                bool rejected;
                try{rejected=!VehicleListings.DirectProofMatchesForChecks(profile,catalog);}
                finally{field.SetValue(owner,oldValue);}
                Check(check,rejected,"exact proof detects field mutation "+owner.GetType().Name+"."+field.Name);
                Check(check,VehicleListings.DirectProofMatchesForChecks(profile,catalog),"exact proof remains pure after restoring "+owner.GetType().Name+"."+field.Name);
                probed++;
            }
            var compared=MarketCatalogLayout.AuthoredComparedFields();int authoredProbed=0;
            foreach(var pair in compared)
            {
                var definition=catalog.parts.FirstOrDefault(part=>part!=null&&
                    (pair.Key=="PartDefinition"||part.awdHardware!=null));
                object owner=pair.Key=="PartDefinition"?definition:
                    pair.Key=="AwdHardwareSpec"?definition?.awdHardware:
                    pair.Key=="AwdSpec"?definition?.awdHardware?.calibration:null;
                if(owner==null)continue;
                foreach(var name in pair.Value)
                {
                    var field=owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.Public);
                    Check(check,field!=null,"authored-token field allowlist resolves "+pair.Key+"."+name);
                    Check(check,TryFlip(field,owner,out var oldValue),"authored token reflection probe supports "+pair.Key+"."+name);
                    bool rejected;
                    try{rejected=!VehicleListings.DirectProofMatchesForChecks(profile,catalog);}
                    finally{field.SetValue(owner,oldValue);}
                    Check(check,rejected,"ready authored token detects same-revision edit "+pair.Key+"."+name);
                    Check(check,VehicleListings.DirectProofMatchesForChecks(profile,catalog),"authored token proof matches again after restore "+pair.Key+"."+name);
                    authoredProbed++;
                }
            }
            Check(check,probed>=80,"reflection field matrix exercised broad validation leaf coverage (fields="+probed.ToString(CultureInfo.InvariantCulture)+")");
            Check(check,authoredProbed>=compared.Values.Sum(fields=>fields.Length),"authored token matrix exercised all allowlisted fields (fields="+authoredProbed.ToString(CultureInfo.InvariantCulture)+")");
            var owned=profile.parts.FirstOrDefault(part=>part!=null);
            if(owned!=null)
            {
                string id=owned.instanceId;owned.instanceId=id+"/validation-probe";
                Check(check,!VehicleListings.DirectProofMatchesForChecks(profile,catalog),"ordered owned part identity is compared");
                owned.instanceId=id;
                Check(check,VehicleListings.DirectProofMatchesForChecks(profile,catalog),"owned part ID restoration preserves pure certificate");
            }

            Check(check,LegacyValidate(profile,catalog)==null&&VehicleListings.ValidateWithoutProofForChecks(profile,catalog)==null,
                "actual full-roster fixture remains valid under frozen oracle and unchanged core");
            var clutchPopulated=profile.vehicleStock.listings.SelectMany(offer=>offer.parts)
                .Select(part=>part.condition).FirstOrDefault(condition=>condition.clutch!=null);
            var clutchEmpty=profile.vehicleStock.listings.SelectMany(offer=>offer.parts)
                .Select(part=>part.condition).FirstOrDefault(condition=>condition.clutch==null);
            Check(check,clutchPopulated!=null&&clutchEmpty!=null,"full market contains both populated and null clutch-state shapes");
            var clutchState=clutchPopulated.clutch;clutchPopulated.clutch=null;
            Check(check,!VehicleListings.DirectProofMatchesForChecks(profile,catalog),"populated clutch to null invalidates exact proof");
            clutchPopulated.clutch=clutchState;
            Check(check,VehicleListings.DirectProofMatchesForChecks(profile,catalog),"populated clutch restoration preserves exact proof");
            clutchEmpty.clutch=new ClutchConditionState();
            Check(check,!VehicleListings.DirectProofMatchesForChecks(profile,catalog),"null clutch to populated invalidates exact proof");
            clutchEmpty.clutch=null;
            Check(check,VehicleListings.DirectProofMatchesForChecks(profile,catalog),"null clutch restoration preserves exact proof");

            var awdTarget=profile.vehicleStock.listings.FirstOrDefault(offer=>catalog.Spec(offer.model).drive!=DragLab.Domain.DriveType.AWD);
            Check(check,awdTarget!=null,"full market contains a non-AWD offer for the optional AWD calibration case");
            var targetTune=awdTarget.stock;var originalAwd=targetTune.awd;
            var alternateAwd=originalAwd==null?new AwdSpec():null;
            targetTune.awd=alternateAwd;
            Check(check,!VehicleListings.DirectProofMatchesForChecks(profile,catalog),"Tune.awd populated/null shape transition invalidates exact proof");
            Check(check,LegacyValidate(profile,catalog)==null&&VehicleListings.ValidateWithoutProofForChecks(profile,catalog)==null,
                "temporary Tune.awd null or populated shape remains valid for a non-AWD offer");
            Compare(check,profile,catalog,"valid temporary Tune.awd null/populated shape");
            Check(check,VehicleListings.DirectProofMatchesForChecks(profile,catalog),"temporary Tune.awd shape receives exact proof");
            targetTune.awd=originalAwd;
            Check(check,!VehicleListings.DirectProofMatchesForChecks(profile,catalog),"restoring original Tune.awd requires a fresh proof");
            Check(check,LegacyValidate(profile,catalog)==null&&VehicleListings.ValidateWithoutProofForChecks(profile,catalog)==null,
                "restored original Tune.awd remains core-valid");
            Compare(check,profile,catalog,"restored original Tune.awd shape");
            Check(check,VehicleListings.DirectProofMatchesForChecks(profile,catalog),"restored Tune.awd shape is certified after core validation");
            targetTune.awd=alternateAwd;
            Check(check,!VehicleListings.DirectProofMatchesForChecks(profile,catalog),"reverse Tune.awd transition invalidates exact proof");
            targetTune.awd=originalAwd;
            Check(check,VehicleListings.DirectProofMatchesForChecks(profile,catalog),"original Tune.awd exact proof remains available after pure restoration");

            var tirePart=profile.vehicleStock.listings.SelectMany(offer=>offer.parts)
                .FirstOrDefault(part=>part.condition.elements is TireCondition[]);
            var ordinaryArrayPart=profile.vehicleStock.listings.SelectMany(offer=>offer.parts)
                .FirstOrDefault(part=>part.condition.elements!=null&&part.condition.elements.GetType()==typeof(EngineElement[]));
            Check(check,tirePart!=null&&ordinaryArrayPart!=null,"full market contains both TireCondition[] and EngineElement[] runtime array shapes");
            Check(check,LegacyValidate(profile,catalog)==null&&VehicleListings.ValidateWithoutProofForChecks(profile,catalog)==null,
                "both member-array shapes coexist in an accepted full-roster core state");
            var typedTires=(TireCondition[])tirePart.condition.elements;
            var ordinaryTires=typedTires.Cast<EngineElement>().ToArray();
            tirePart.condition.elements=ordinaryTires;
            Check(check,!VehicleListings.DirectProofMatchesForChecks(profile,catalog),"TireCondition[] to ordinary EngineElement[] invalidates exact proof");
            Check(check,LegacyValidate(profile,catalog)==null&&VehicleListings.ValidateWithoutProofForChecks(profile,catalog)==null,
                "ordinary EngineElement[] copy of valid tire members remains core-valid");
            Compare(check,profile,catalog,"valid tire member values in ordinary EngineElement[]");
            tirePart.condition.elements=typedTires;
            Check(check,!VehicleListings.DirectProofMatchesForChecks(profile,catalog),"restoring TireCondition[] requires fresh proof after normal validation");
            Compare(check,profile,catalog,"restored TireCondition[] runtime shape");
            Check(check,VehicleListings.DirectProofMatchesForChecks(profile,catalog),"restored typed tire array receives a fresh exact proof");

            const int timingRepeats=10;
            var timedOffer=profile.vehicleStock.listings[0];int timedPrice=timedOffer.price;
            timedOffer.price=timedPrice+1;var fillWatch=Stopwatch.StartNew();
            if(VehicleListings.Validate(profile,catalog)!=null)throw new InvalidOperationException("Proof timing fill fixture became invalid.");
            fillWatch.Stop();double changedFillMs=fillWatch.Elapsed.TotalMilliseconds;
            timedOffer.price=timedPrice;fillWatch=Stopwatch.StartNew();
            if(VehicleListings.Validate(profile,catalog)!=null)throw new InvalidOperationException("Proof timing restored fill fixture became invalid.");
            fillWatch.Stop();double restoredFillMs=fillWatch.Elapsed.TotalMilliseconds;
            Check(check,VehicleListings.DirectProofMatchesForChecks(profile,catalog),"timing fixture has a fresh full-market proof");
            string coreTiming=MedianAndMax(()=>{if(VehicleListings.ValidateWithoutProofForChecks(profile,catalog)!=null)throw new InvalidOperationException("Unchanged full core rejected timing fixture.");},timingRepeats);
            long hitsBefore=VehicleListings.ValidationProofHitsForChecks,coresBefore=VehicleListings.ValidationCoreRunsForChecks;
            long jsonBefore=RuntimeDataEncodingProbe.PartConsumerJsonCallCount,keyBefore=RuntimeDataEncodingProbe.PartConsumerLegacyKeyCallCount;
            string hitTiming=MedianAndMax(()=>{using(RuntimeDataEncodingProbe.Measure("vehicle-listing-validation-proof-timing"))if(VehicleListings.Validate(profile,catalog)!=null)throw new InvalidOperationException("Proof hit rejected timing fixture.");},timingRepeats);
            Check(check,VehicleListings.ValidationProofHitsForChecks==hitsBefore+timingRepeats+1,"timed warmup and ten proof samples all hit");
            Check(check,VehicleListings.ValidationCoreRunsForChecks==coresBefore,"timed proof samples perform no full core runs");
            Check(check,RuntimeDataEncodingProbe.PartConsumerJsonCallCount==jsonBefore&&RuntimeDataEncodingProbe.PartConsumerLegacyKeyCallCount==keyBefore,
                "timed proof samples perform no scoped JSON or legacy-key encoding");
            string proofTiming="definitions="+catalog.parts.Count.ToString(CultureInfo.InvariantCulture)+" cars="+profile.cars.Count.ToString(CultureInfo.InvariantCulture)+
                " profile_parts="+profile.parts.Count.ToString(CultureInfo.InvariantCulture)+" listings="+profile.vehicleStock.listings.Count.ToString(CultureInfo.InvariantCulture)+
                " listing_parts="+profile.vehicleStock.listings.Sum(offer=>offer.parts.Count).ToString(CultureInfo.InvariantCulture)+" repeats="+timingRepeats.ToString(CultureInfo.InvariantCulture)+
                " core_bypass="+coreTiming+" proof_hit="+hitTiming+" one_time_core_miss_store_ms="+changedFillMs.ToString("R",CultureInfo.InvariantCulture)+
                " one_time_restored_core_store_ms="+restoredFillMs.ToString("R",CultureInfo.InvariantCulture);
            if(!string.IsNullOrEmpty(fixtureDirectory)){Directory.CreateDirectory(fixtureDirectory);File.WriteAllText(Path.Combine(fixtureDirectory,"vehicle-listing-proof-timing.txt"),proofTiming+Environment.NewLine,new UTF8Encoding(false));}
            check(true,proofTiming);
        }

        static string Measure(Action action,int repeats)
        {
            action();var samples=new double[repeats];
            for(int i=0;i<repeats;i++){var watch=Stopwatch.StartNew();action();watch.Stop();samples[i]=watch.Elapsed.TotalMilliseconds;}
            return Median(samples).ToString("R",CultureInfo.InvariantCulture);
        }

        static string ProfileStages(PlayerProfile profile,MechanicalCatalog catalog,int repeats)
        {
            var offers=profile.vehicleStock.listings;
            var inputs=new List<StageInput>(offers.Count);
            foreach(var offer in offers)
            {
                var input=new StageInput{Offer=offer,Map=new Dictionary<PartCategory,string>()};
                foreach(var part in offer.parts){var definition=catalog.Find(part.definitionId);if(definition!=null&&!input.Map.ContainsKey(definition.category))input.Map.Add(definition.category,definition.id);}
                inputs.Add(input);
            }
            foreach(var input in inputs)input.Build=catalog.Resolve(input.Offer.model,input.Map,input.Offer.stock);
            var sink=0;
            string inventory=Measure(()=>{var ids=new HashSet<string>(profile.parts.Select(part=>part.instanceId));sink=unchecked(sink+ids.Count);},repeats);
            string inspect=Measure(()=>{foreach(var offer in offers)sink=unchecked(sink+(VehicleInspection.Validate(offer)==null?1:0));},repeats);
            string specs=Measure(()=>{foreach(var offer in offers)sink=unchecked(sink+catalog.Spec(offer.model).id.Length);},repeats);
            string finds=Measure(()=>{foreach(var offer in offers)foreach(var part in offer.parts)sink=unchecked(sink+(catalog.Find(part.definitionId)==null?0:1));},repeats);
            string maps=Measure(()=>{foreach(var offer in offers){var map=new Dictionary<PartCategory,string>();foreach(var part in offer.parts){var definition=catalog.Find(part.definitionId);if(definition!=null)map.Add(definition.category,definition.id);}sink=unchecked(sink+map.Count);}},repeats);
            string fits=Measure(()=>{foreach(var offer in offers){var spec=catalog.Spec(offer.model);foreach(var part in offer.parts){var definition=catalog.Find(part.definitionId);if(definition!=null&&catalog.Fits(spec,definition))sink++;}}},repeats);
            string conditions=Measure(()=>{foreach(var offer in offers)foreach(var part in offer.parts)sink=unchecked(sink+(part.condition.Validate()==null?1:0));},repeats);
            string fault=Measure(()=>{foreach(var offer in offers)foreach(var part in offer.parts){var definition=catalog.Find(part.definitionId);if(definition!=null)foreach(var element in part.condition.elements??Array.Empty<EngineElement>())if(EngineElements.FaultAllowed(definition.category,element))sink++;}},repeats);
            string resolve=Measure(()=>{foreach(var input in inputs)sink=unchecked(sink+(catalog.Resolve(input.Offer.model,input.Map,input.Offer.stock).Drivable?1:0));},repeats);
            string resources=Measure(()=>{foreach(var input in inputs)sink=unchecked(sink+(input.Offer.fuel.Validate(input.Build.spec.fuel)==null&&input.Offer.fluids.Validate(input.Build.spec.fluids)==null?1:0));},repeats);
            string complete=Measure(()=>sink=unchecked(sink+(VehicleListings.Validate(profile,catalog)==null?1:0)),repeats);
            return "stage_medians_ms(repeats="+repeats.ToString(CultureInfo.InvariantCulture)+"): profile_part_set="+inventory+"; inspection="+inspect+"; Spec_copies="+specs+"; Find_calls="+finds+"; per_offer_category_maps="+maps+"; Spec+Find+Fits="+fits+"; ComponentCondition.Validate="+conditions+"; FaultAllowed="+fault+"; Resolve="+resolve+"; Fuel+Fluid_validation="+resources+"; complete_VehicleListings.Validate="+complete+"; sink="+sink.ToString(CultureInfo.InvariantCulture);
        }

        static VehicleOffer CopyOffer(VehicleOffer source,string listingId,string partPrefix)
        {
            var copy=new VehicleOffer{model=source.model,paint=source.paint,listingId=listingId,used=source.used,mileageKm=source.mileageKm,
                disclosureVersion=source.disclosureVersion,estimateSeed=source.estimateSeed,inspected=source.inspected,firstStarter=source.firstStarter,
                engineRuns=source.engineRuns,drives=source.drives,reportedCondition=source.reportedCondition,historyNotes=new List<string>(source.historyNotes),
                parts=new List<PartInstance>(),fuel=source.fuel.Copy(),fluids=source.fluids.Copy(),price=source.price,reputation=source.reputation,
                cataloguePrice=source.cataloguePrice,stock=source.stock.Copy()};
            int index=0;
            foreach(var original in source.parts){var part=original.Copy();part.instanceId=partPrefix+index++;copy.parts.Add(part);}
            return copy;
        }

        /// <summary>Run from the existing isolated Editor verification entry point.</summary>
        public static void Run(Action<bool,string> check,string fixtureDirectory)
        {
            var authored=Resources.LoadAll<VehicleDefinition>("Data/Vehicles");
            if(authored==null||authored.Length==0)throw new InvalidOperationException("Vehicle listing validation checks require authored vehicle definitions.");
            var catalog=new MechanicalCatalog(authored.Select(vehicle=>vehicle.spec));
            var profile=PlayerProfile.NewGame(638000000000000000L);
            VehicleListings.Initialize(profile,catalog,103);
            MechanicsOwnership.Ensure(profile,catalog);
            if(VehicleListings.Validate(profile,catalog)!=null)throw new InvalidOperationException("Vehicle listing validation fixture is invalid.");

            var stock=profile.vehicleStock;
            var listings=stock.listings;
            var first=listings[0];
            int revisionBefore=profile.vehicleStock.generation;
            long profileRevision=profile.revision;
            Compare(check,profile,catalog,"generated three-listing starter profile");
            Check(check,ReferenceEquals(profile.vehicleStock,stock)&&ReferenceEquals(stock.listings,listings)&&ReferenceEquals(listings[0],first),"valid validation retains profile, market, list and offer references");

            int savedVersion=stock.version;stock.version=0;Compare(check,profile,catalog,"invalid market version preserves market error order");stock.version=savedVersion;
            bool savedFirst=profile.firstPurchasePending;profile.firstPurchasePending=false;
            int savedPrice=first.price;first.price=0;Compare(check,profile,catalog,"invalid listing scalar rejected before inspection");first.price=savedPrice;profile.firstPurchasePending=savedFirst;

            var savedHistory=first.historyNotes;first.historyNotes=null;Compare(check,profile,catalog,"null disclosure history retains inspection rejection");first.historyNotes=savedHistory;
            var savedFuel=first.fuel;first.fuel=null;Compare(check,profile,catalog,"null fuel retains listing scalar rejection");first.fuel=savedFuel;
            var savedParts=first.parts;first.parts=null;Compare(check,profile,catalog,"null offer part list retains listing scalar rejection");first.parts=savedParts;

            string savedDefinition=first.parts[0].definitionId;first.parts[0].definitionId="vehicle-validation/missing-definition";Compare(check,profile,catalog,"unknown component definition retains component rejection");first.parts[0].definitionId=savedDefinition;
            var authoredPart=catalog.Find(savedDefinition);string savedFitment=authoredPart.fitment;authoredPart.fitment="vehicle-validation/incompatible";Compare(check,profile,catalog,"same-revision authored fitment edit is observed by fresh validation");authoredPart.fitment=savedFitment;
            var nullPart=first.parts[0];first.parts[0]=null;Compare(check,profile,catalog,"null listing component preserves legacy exception behavior");first.parts[0]=nullPart;
            double savedLitres=first.fuel.litres;first.fuel.litres=double.NaN;Compare(check,profile,catalog,"non-finite fuel retains build/resource rejection");first.fuel.litres=savedLitres;

            // Same-revision public edits must be observed by each fresh invocation;
            // no result survives this call, even when the value is restored to A.
            first.price=0;Compare(check,profile,catalog,"same-revision state B is rejected");first.price=savedPrice;Compare(check,profile,catalog,"restored state A is accepted afresh");
            Check(check,profile.revision==profileRevision&&stock.generation==revisionBefore,"validation and same-revision edit checks do not alter profile or market revisions");

            var nullRow=stock.listings[0];stock.listings[0]=null;Compare(check,profile,catalog,"null first starter row preserves initial-market error");stock.listings[0]=nullRow;
            var savedProfileParts=profile.parts;profile.parts=null;Compare(check,profile,catalog,"null profile part list preserves legacy exception behavior");profile.parts=savedProfileParts;
            Compare(check,null,catalog,"null profile preserves legacy exception behavior");

            // Exercise multiple listings sharing a model in one validation call.
            var originalSecond=listings[1];bool originalPending=profile.firstPurchasePending;
            listings[1]=CopyOffer(first,"vehicle-validation/repeated-model","vehicle-validation/part/");profile.firstPurchasePending=false;
            Compare(check,profile,catalog,"repeated-model listings with distinct component identities remain fully validated");
            listings[1]=originalSecond;profile.firstPurchasePending=originalPending;

            // Build the actual post-purchase market from a real generated starter,
            // then acquire a second car from the full live roster. This reproduces
            // the native seed and path rather than timing only the 3-offer opening list.
            var nativeLike=PlayerProfile.NewGame(638000000000000000L);nativeLike.cash=100000000;nativeLike.reputation=10000;
            VehicleListings.Initialize(nativeLike,catalog,16102026);MechanicsOwnership.Ensure(nativeLike,catalog);
            var purchasedStarter=nativeLike.vehicleStock.listings[0];VehicleCollection.Buy(nativeLike,catalog,purchasedStarter);
            var secondOffer=nativeLike.vehicleStock.listings.FirstOrDefault(offer=>!offer.used&&offer.model!=purchasedStarter.model);
            if(secondOffer==null)throw new InvalidOperationException("Post-purchase roster has no second distinct new-car offer.");
            VehicleCollection.Buy(nativeLike,catalog,secondOffer);MechanicsOwnership.Ensure(nativeLike,catalog);
            if(nativeLike.firstPurchasePending||nativeLike.vehicleStock.listings.Count<=3||nativeLike.cars.Count!=2)
                throw new InvalidOperationException("Expected the real first-starter purchase, full market roster and two owned cars.");
            var partTemplate=nativeLike.parts.FirstOrDefault(part=>part.location==PartLocation.Inventory)??nativeLike.parts[0];
            while(nativeLike.parts.Count<1081){var filler=partTemplate.Copy();filler.instanceId="vehicle-validation/filler/"+nativeLike.parts.Count.ToString("D4",CultureInfo.InvariantCulture);filler.location=PartLocation.Inventory;filler.ownerId="";filler.slot="";filler.storageId=SharedInventory.Garage;foreach(var element in filler.condition.elements??Array.Empty<EngineElement>())element.id=filler.instanceId+"/member/"+element.cylinder.ToString(CultureInfo.InvariantCulture);nativeLike.parts.Add(filler);}
            if(nativeLike.parts.Count!=1081)throw new InvalidOperationException("Expected exactly 1,081 part rows for the native-like validation workload.");
            Compare(check,nativeLike,catalog,"actual seed-16102026 starter purchase, full market, two cars and 1,081 part rows");
            const int repeats=5;
            string stages=ProfileStages(nativeLike,catalog,repeats);
            string timing="definitions="+catalog.parts.Count.ToString(CultureInfo.InvariantCulture)+" native_seed=16102026 cars="+nativeLike.cars.Count.ToString(CultureInfo.InvariantCulture)+" profile_parts="+nativeLike.parts.Count.ToString(CultureInfo.InvariantCulture)+" listings="+nativeLike.vehicleStock.listings.Count.ToString(CultureInfo.InvariantCulture)+" listing_parts="+nativeLike.vehicleStock.listings.Sum(offer=>offer.parts.Count).ToString(CultureInfo.InvariantCulture)+" purchased_starter="+purchasedStarter.model+" second_car="+secondOffer.model+Environment.NewLine+stages;
            if(!string.IsNullOrEmpty(fixtureDirectory)){Directory.CreateDirectory(fixtureDirectory);File.WriteAllText(Path.Combine(fixtureDirectory,"vehicle-listing-validation-timing.txt"),timing+Environment.NewLine,new UTF8Encoding(false));}
            check(true,timing);
            ProofChecks(check,nativeLike,catalog,fixtureDirectory);
        }
    }
#endif
}

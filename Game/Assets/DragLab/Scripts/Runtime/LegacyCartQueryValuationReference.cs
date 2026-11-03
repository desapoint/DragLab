#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Runtime
{
    /// <summary>
    /// Editor-only copy of the loose-price view arithmetic reached by the frozen
    /// PartCart.Total sale branch. It is for disposable authored fixtures only.
    /// </summary>
    internal static class LegacyCartQueryValuationReference
    {
        static double Clamp(double x,double low,double high)=>Math.Max(low,Math.Min(high,x));
        static double Scale(double value,double factor)=>value*factor;
        static double ElementHealth(EngineElement e)=>e.Failed?0:e.health*(e.fault==ElementFault.None?1:1-.7*Clamp(e.severity,0,1));
        static double Health(ComponentCondition c)
        {
            double h=c.health;
            if(c.elements.Length>0)h=Math.Min(h,c.elements.Average(ElementHealth));
            if(c.tires.Length>0)h=Math.Min(h,c.tires.Average(t=>t.failed?0:Math.Min(t.health,Clamp(1-t.consumedLife,0,1))));
            return Clamp(h*(1-.25*Clamp(c.deposits,0,1))*(1-.25*Clamp(c.leakSeverity,0,1))*(1-.15*Clamp(c.adjustmentError,0,1)),0,1);
        }
        static bool Fault(ComponentCondition c)=>c.tires!=null&&c.tires.Length==4?c.tires.Any(t=>t.Failed):c.failed||c.health<=0||c.elements.Any(e=>e.Failed||e.fault!=ElementFault.None);
        static BuyerPolicy Buyer(ValuationPolicy p,string id){for(int i=0;i<p.buyers.Count;i++)if(p.buyers[i].id==id)return p.buyers[i];return null;}
        static QualityValue Quality(ValuationPolicy p,string id){for(int i=0;i<p.qualities.Count;i++)if(p.qualities[i].quality==id)return p.qualities[i];return null;}
        static PartInstance LoosePart(PlayerProfile p,string id){for(int i=0;i<p.parts.Count;i++){var item=p.parts[i];if(item.instanceId==id&&item.location==PartLocation.Inventory)return item;}return null;}
        static LooseEngineElement LooseElement(PlayerProfile p,string id){for(int i=0;i<p.looseEngineElements.Count;i++){var item=p.looseEngineElements[i];if(item.element.id==id)return item;}return null;}

        internal static Func<string,bool,int> LoosePriceView(PlayerProfile profile,ValuationPolicy policy,string buyerId,long nowUtcTicks)
        {
            policy.Validate();
            string invalid=LegacyValidateForQuote(profile,profile.catalog);
            if(invalid!=null)throw new InvalidOperationException(invalid);
            long revision=profile.revision;
            return (id,unit)=>
            {
                if(profile.revision!=revision)throw new InvalidOperationException("Price view changed; refresh inventory.");
                return DisplayLooseTotal(profile,profile.catalog,policy,unit?ValuationKind.LooseElement:ValuationKind.LoosePart,id,buyerId,nowUtcTicks);
            };
        }

        internal static int DisplayLooseTotal(PlayerProfile profile,MechanicalCatalog catalog,ValuationPolicy policy,ValuationKind kind,string assetId,string buyerId,long nowUtcTicks)
        {
            if(profile==null||catalog==null||policy==null||kind!=ValuationKind.LoosePart&&kind!=ValuationKind.LooseElement||nowUtcTicks<=0||nowUtcTicks>DateTime.MaxValue.Ticks-TimeSpan.FromDays(1).Ticks)throw new InvalidOperationException("Invalid valuation request.");
            var buyer=Buyer(policy,buyerId);if(buyer==null)throw new InvalidOperationException("Unknown buyer.");
            double total;
            if(kind==ValuationKind.LoosePart)
            {
                var item=LoosePart(profile,assetId);
                if(item==null)throw new InvalidOperationException("Quote requires an owned loose part without retained descendants.");
                var definition=LegacyMechanicalCatalogReference.Find(catalog,item.definitionId);
                if(definition.price<0)throw new InvalidOperationException("Invalid catalogue price.");
                total=PartValue(item,definition,policy,buyer,catalog);
            }
            else
            {
                var element=LooseElement(profile,assetId);
                if(element==null)throw new InvalidOperationException("Quote requires an owned loose engine element without retained descendants.");
                total=LooseElementValue(element,catalog,policy,buyer);
            }
            GarageBusiness.Reserved(profile,assetId);
            profile.runCheckpoints.Any(r=>!r.settled);
            if(!MechanicsSchema.Finite(total)||total<0||total>int.MaxValue)throw new InvalidOperationException("Quote total outside supported currency range.");
            return checked((int)Math.Round(total,MidpointRounding.AwayFromZero));
        }

        static double PartValue(PartInstance part,PartDefinition def,ValuationPolicy policy,BuyerPolicy buyer,MechanicalCatalog catalog)
        {
            var c=part.condition;
            if(def.category==PartCategory.Tires&&c.tires!=null&&c.tires.Length==4)
            {
                double total=0;
                for(int i=0;i<4;i++)
                {
                    var t=c.tires[i];var spec=LegacyMechanicalCatalogReference.Find(catalog,TireFitments.DefinitionId(part,i))??def;
                    double baseValue=spec.price*.25*policy.usedPartFraction;var grade=Quality(policy,spec.quality);
                    double tread=t.failed?0:Clamp(Math.Min(t.health,1-t.consumedLife),0,1);
                    double history=Clamp(t.distanceM/1000*policy.distanceDiscountPerKm+t.runningSeconds/3600*policy.hourDiscount,0,policy.historyDiscountLimit);
                    total+=baseValue*(grade==null?1:grade.factor)*(policy.minimumWearFactor+(1-policy.minimumWearFactor)*tread)*(t.failed||tread<=0?policy.scrapFraction:1)*(1-history);
                }
                return Scale(total,buyer.parts);
            }
            double cataloguePrice=def.price;
            if(def.category==PartCategory.Tires&&c.tires.Length==4&&catalog!=null){cataloguePrice=0;for(int i=0;i<4;i++)cataloguePrice+=LegacyMechanicalCatalogReference.Find(catalog,TireFitments.DefinitionId(part,i)).price*.25;}
            if(def.category==PartCategory.Wheels&&WheelFitments.HasOverrides(part)&&catalog!=null){cataloguePrice=0;for(int i=0;i<4;i++)cataloguePrice+=LegacyMechanicalCatalogReference.Find(catalog,WheelFitments.DefinitionId(part,i)).price*.25;}
            double value=cataloguePrice*policy.usedPartFraction;var quality=Quality(policy,def.quality);value=Scale(value,quality==null?1:quality.factor);
            value=Scale(value,policy.minimumWearFactor+(1-policy.minimumWearFactor)*Health(c));
            if(Fault(c))value=Scale(value,c.failed||c.health<=0?policy.scrapFraction:.65);
            double historyFactor=1-Clamp(c.distanceM/1000*policy.distanceDiscountPerKm+c.runningSeconds/3600*policy.hourDiscount+c.stressExposure*policy.stressDiscountPerSecond+c.reconditionCount*policy.reconditionDiscount,0,policy.historyDiscountLimit);
            return Scale(Scale(value,historyFactor),buyer.parts);
        }

        static double LooseElementValue(LooseEngineElement element,MechanicalCatalog catalog,ValuationPolicy policy,BuyerPolicy buyer)
        {
            double loose=element.unitPrice*policy.usedPartFraction;
            if(element.tireUnit&&element.tire!=null)
            {
                var t=element.tire;bool faulty=t.failed||t.health<=0||t.consumedLife>=1;
                var tireDefinition=LegacyMechanicalCatalogReference.Find(catalog,element.definitionId);string ignoredName=tireDefinition.name;
                double remaining=faulty?0:Clamp(Math.Min(t.health,1-t.consumedLife),0,1);
                loose=Scale(loose,policy.minimumWearFactor+(1-policy.minimumWearFactor)*remaining);
                if(faulty)loose=Scale(loose,policy.scrapFraction);
                double history=Clamp(t.distanceM/1000*policy.distanceDiscountPerKm+t.runningSeconds/3600*policy.hourDiscount,0,policy.historyDiscountLimit);
                loose=Scale(loose,1-history);
            }
            else
            {
                bool faulty=element.element.Failed||element.element.fault!=ElementFault.None;
                double wearFault=(policy.minimumWearFactor+(1-policy.minimumWearFactor)*ElementHealth(element.element))*(faulty?.65:1);
                loose=Scale(loose,wearFault);
            }
            return Scale(loose,buyer.parts);
        }

        // The pre-query ownership oracle copied Validate, while current quote validation
        // adds dealer-discriminator tolerance. Keep that distinction visible to reviewers.
        static string LegacyValidateForQuote(PlayerProfile profile,MechanicalCatalog catalog)
        {
            string invalid=LegacyValidateCore(profile,catalog,false);
            if(invalid==null||invalid.IndexOf("dealer",StringComparison.OrdinalIgnoreCase)<0)return invalid;
            return LegacyValidateCore(profile,catalog,true);
        }

        // Literal pre-edit MechanicsOwnership.ValidateCore copy; catalog index reads are routed to the frozen C1 reference.

                static string LegacyValidateCore(PlayerProfile profile, MechanicalCatalog catalog, bool tolerateDealerDiscriminator)
                {
                    if (profile.saveVersion != MechanicsOwnership.SaveVersion || profile.mechanicsVersion != MechanicsSchema.Version || profile.revision < 0 || profile.cash < 0) return "Invalid profile version/revision/cash.";
                    if(profile.cars.Count == 0 ? !string.IsNullOrEmpty(profile.selectedOwnedVehicleId) || !string.IsNullOrEmpty(profile.selectedVehicleId) : !profile.cars.Any(c=>c.instanceId==profile.selectedOwnedVehicleId&&c.vehicleId==profile.selectedVehicleId))return "Invalid selected owned vehicle reference.";
                    string stockError=LegacyValidateVehicleListings(profile,catalog);if(stockError!=null)return stockError;
                    string economyError = PaidEconomy.ValidateState(profile); if (economyError != null) return economyError;
                    string careerError = CareerProgression.Validate(profile); if (careerError != null) return careerError;
                    string businessError=GarageBusiness.Validate(profile);if(businessError!=null)return businessError;
                    using (var lookup = OwnershipValidationLookup.Capture(profile.cars, profile.engines, profile.parts))
                    {
                        var identities = new HashSet<string>(); var bindings = new HashSet<string>();
                        Func<string, bool> unique = id => !string.IsNullOrEmpty(id) && identities.Add(id);
                        foreach (var car in profile.cars)
                        {
                            if(car.usedHistory==null||car.usedHistory.Count>16||car.usedHistory.Any(n=>string.IsNullOrWhiteSpace(n)||n.Length>160||n.Any(char.IsControl)))return "Invalid owned history.";
                            if(!MechanicsSchema.Finite(car.mileageKm)||car.mileageKm<0)return "Invalid owned mileage.";
                            if (MassDynamics.ValidateInputs(LegacyMechanicalCatalogReference.Spec(catalog,car.vehicleId),car.tune) != null) return "Invalid owned mass/placement inputs.";
                            if(car.serviceHardwareVersion!=1||car.fluids==null||car.fluids.Validate(LegacyMechanicalCatalogReference.Spec(catalog,car.vehicleId).fluids)!=null)return "Invalid owned fluids/service migration.";
                            if(car.thermalUtcTicks<0||car.thermalUtcTicks>DateTime.MaxValue.Ticks)return "Invalid owned thermal clock.";
                            if (car.fuel == null || car.fuel.Validate(LegacyMechanicalCatalogReference.Spec(catalog,car.vehicleId).fuel) != null || !car.fuelInitialized || car.massVersion != 1) return "Invalid fuel/mass state.";
                            if (!unique(car.instanceId) || !unique(car.chassisId) || car.installed == null || car.installed.version != MechanicsSchema.Version || car.installed.instanceId != car.instanceId || car.installed.vehicleDefinitionId != car.vehicleId) return "Invalid vehicle/chassis/build identity.";
                            var engine = lookup.FindEngine(car.engineAssemblyId);
                            if (string.IsNullOrEmpty(car.engineAssemblyId)) {
                                if(car.engineRebuild!=null||car.installed.slots.Any(b=>MechanicalCatalog.EngineSlot(AssemblyOperations.Parse(b.slot)))||car.fluids.oilLitres!=0||car.fluids.coolantLitres!=0)return "Engine-less chassis has engine descendants/resources.";
                            } else if (engine == null || engine.parentVehicleId != car.instanceId || engine.vehicleDefinitionId != car.vehicleId || engine.slots.Count != 0) return "Invalid engine parent.";
                            var slots = new HashSet<string>();
                            foreach (var binding in car.installed.slots)
                            {
                                PartCategory category;
                                if (!Enum.TryParse(binding.slot, out category) || !Enum.IsDefined(typeof(PartCategory), category) || !slots.Add(binding.slot) || !bindings.Add(binding.partInstanceId)) return "Duplicate/invalid slot binding.";
                                var item = lookup.FindPart(binding.partInstanceId);
                                var definition = item == null ? null : LegacyMechanicalCatalogReference.Find(catalog,item.definitionId);
                                var location = MechanicalCatalog.EngineSlot(category) ? PartLocation.Assembly : PartLocation.Vehicle;
                                string owner = location == PartLocation.Assembly ? car.engineAssemblyId : car.instanceId;
                                if (item == null || definition == null || definition.category != category || item.location != location || item.ownerId != owner || item.slot != binding.slot || !catalog.Fits(LegacyMechanicalCatalogReference.Spec(catalog,car.vehicleId), definition)) return "Part parent/fitment does not match its slot.";
                            }
                        }
                        foreach (var engine in profile.engines) {
                            if(engine.customName!=null&&(engine.customName.Length>VehicleCollection.NameLimit||engine.customName.Any(char.IsControl)))return "Invalid engine name.";
                            if(!unique(engine.instanceId)||engine.slots==null||engine.tune==null)return "Invalid/duplicate engine assembly.";
                            if(!string.IsNullOrEmpty(engine.parentVehicleId)) { var parent=lookup.FindCar(engine.parentVehicleId);if(parent==null||parent.engineAssemblyId!=engine.instanceId)return "Orphan attached engine assembly."; continue; }
                            if(engine.fluids==null||engine.fluids.Validate(LegacyMechanicalCatalogReference.Spec(catalog,engine.vehicleDefinitionId).fluids)!=null)return "Invalid stored engine fluids.";
                            var slots=new HashSet<string>();
                            foreach(var binding in engine.slots){
                                PartCategory category;
                                if(!Enum.TryParse(binding.slot,out category)||!MechanicalCatalog.EngineSlot(category)||!slots.Add(binding.slot)||!bindings.Add(binding.partInstanceId))return "Invalid/duplicate stored engine slot.";
                                var item=lookup.FindPart(binding.partInstanceId);var d=item==null?null:LegacyMechanicalCatalogReference.Find(catalog,item.definitionId);
                                if(d==null||d.category!=category||item.location!=PartLocation.Assembly||item.ownerId!=engine.instanceId||item.slot!=binding.slot||!catalog.Fits(LegacyMechanicalCatalogReference.Spec(catalog,engine.vehicleDefinitionId),d))return "Invalid stored engine descendant/fitment.";
                            }
                        }
                        foreach(var setup in profile.savedSetups)if(LegacyValidateSetup(setup,catalog)!=null||!unique(setup.id))return "Invalid/duplicate saved setup.";
                        foreach (var item in profile.parts)
                            if (!unique(item.instanceId) || item.Validate() != null || LegacyMechanicalCatalogReference.Find(catalog,item.definitionId) == null || (item.location != PartLocation.Inventory) != bindings.Contains(item.instanceId)) return "Duplicate/invalid/orphan part instance.";
                        foreach(var loose in profile.looseEngineElements)if(loose==null||loose.element==null||loose.element.Validate()!=null||LegacyMechanicalCatalogReference.Find(catalog,loose.definitionId)==null||loose.unitPrice<0||!unique(loose.element.id))return "Invalid loose engine element.";
                        foreach(var part in profile.parts)
                        {
                            var definition=LegacyMechanicalCatalogReference.Find(catalog,part.definitionId);
                            if(definition!=null&&definition.category!=PartCategory.Clutch&&part.condition.clutch!=null)
                                return "Non-clutch components cannot own clutch-fluid state.";
                            foreach(var element in part.condition.elements??new EngineElement[0])
                                if(!EngineElements.FaultAllowed(definition.category,element)||!unique(element.id))
                                    return "Invalid/duplicate typed element identity, category or fault.";
                        }
                        foreach(var part in profile.parts.Where(p=>LegacyMechanicalCatalogReference.Find(catalog,p.definitionId).category==PartCategory.Tires))
                            if(part.condition.elements==null||part.condition.elements.Length!=4||part.condition.elements.Any(e=>!(e is TireCondition)))return "Tire sets must contain four typed tire elements.";
                        if(profile.driving==null||profile.driving.guaranteedFittingParts<0||profile.driving.guaranteedFittingParts>20)return "Invalid market guarantee difficulty.";
                        // Validate dealer identities first, before market descendant enumeration.
                        string inventoryError=LegacyValidateInventory(profile,catalog,identities,tolerateDealerDiscriminator);if(inventoryError!=null)return inventoryError;
                        string marketError=LegacyValidateMarket(profile,catalog);if(marketError!=null)return marketError;
                        foreach(var item in profile.parts)foreach(var tire in item.condition.tires??new TireCondition[0])if(!string.IsNullOrEmpty(tire.definitionId)){
                            var d=LegacyMechanicalCatalogReference.Find(catalog,tire.definitionId);if(d==null||d.category!=PartCategory.Tires)return "Invalid tire specification.";
                            var owner=lookup.CarContainingPart(item.instanceId);
                            if(owner!=null&&!catalog.Fits(LegacyMechanicalCatalogReference.Spec(catalog,owner.vehicleId),d))return "Incompatible individual tire.";
                        }
                        foreach(var item in profile.parts.Where(i=>LegacyMechanicalCatalogReference.Find(catalog,i.definitionId).category==PartCategory.Wheels)){if(item.condition.elements.Length!=4)return "Four wheel members required.";foreach(var e in item.condition.elements){var d=LegacyMechanicalCatalogReference.Find(catalog,string.IsNullOrEmpty(e.definitionId)?item.definitionId:e.definitionId);if(d==null||d.category!=PartCategory.Wheels||e.role!="wheel"||e.cylinder<0||e.cylinder>3)return "Invalid individual wheel specification.";var owner=lookup.CarContainingPart(item.instanceId);if(owner!=null&&!catalog.Fits(LegacyMechanicalCatalogReference.Spec(catalog,owner.vehicleId),d))return "Individual wheel does not fit.";}if(item.condition.elements.Select(e=>e.cylinder).Distinct().Count()!=4)return "Duplicate wheel positions.";}
                        foreach(var car in profile.cars){if(car.engineRebuild!=null&&EngineRebuilds.Validate(profile,car)!=null)return "Invalid engine rebuild reservation.";}
                        var runs = new HashSet<string>();
                        foreach (var run in profile.runCheckpoints)
                            if (run.version != MechanicsSchema.Version || string.IsNullOrEmpty(run.runId) || !runs.Add(run.runId) || !lookup.HasCarID(run.ownedVehicleId) || run.tick < 0 || run.revision > profile.revision) return "Invalid run checkpoint.";
                        return null;
                    }
                }


        // Exact pre-edit copies for dealer-tolerant ownership, inventory and market validation.
                static string LegacyValidateInventory(PlayerProfile p,MechanicalCatalog catalog,HashSet<string> identities,bool tolerateDealerDiscriminator)
                {
                    if(p.inventory==null||p.inventory.storage==null||p.inventory.dealer==null)return "Missing inventory state.";
                    var containers=new HashSet<string>();foreach(var s in p.inventory.storage)if(s==null||s.kind!=InventoryKind.Storage||string.IsNullOrWhiteSpace(s.id)||string.IsNullOrWhiteSpace(s.name)||!containers.Add(s.id))return "Invalid/duplicate storage container.";
                    if(!containers.Contains(SharedInventory.Garage))return "Garage storage is required.";
                    foreach(var item in p.parts.Where(i=>i.location==PartLocation.Inventory))if(!containers.Contains(SharedInventory.StorageOf(item.storageId)))return "Unknown part storage.";
                    foreach(var unit in p.looseEngineElements)if(!containers.Contains(SharedInventory.StorageOf(unit.storageId))||!LegacyValidUnit(p,catalog,unit))return "Invalid unit storage/condition.";
                    foreach(var e in p.engines.Where(e=>string.IsNullOrEmpty(e.parentVehicleId)))if(!containers.Contains(SharedInventory.StorageOf(e.storageId)))return "Unknown engine storage.";
                    for(int index=0;index<p.inventory.dealer.Count;index++){
                        var stock=p.inventory.dealer[index];
                        if(stock==null)return "Invalid dealer stock at row "+index+": missing listing.";
                        if(stock.paidPrice<0)return "Invalid dealer stock at row "+index+": negative purchase price.";
                        bool hasPart=stock.HasPart;
                        bool hasUnit=stock.HasUnit;
                        if(hasPart==hasUnit)return "Invalid dealer stock at row "+index
                            +(hasPart?": conflicting part and unit payloads.":": missing part/unit identity.");
                        if(!tolerateDealerDiscriminator&&stock.isUnit!=hasUnit)return "Invalid dealer stock at row "+index+": wrong part/unit discriminator.";
                        if(hasPart){
                            var i=stock.part;
                            string id=i.instanceId;
                            if(i.location!=PartLocation.Inventory)return "Invalid dealer part at row "+index+" ("+id+"): stored part is marked "+i.location+".";
                            string conditionError=i.Validate();
                            if(conditionError!=null)return "Invalid dealer part at row "+index+" ("+id+"): "+conditionError;
                            var definition=LegacyMechanicalCatalogReference.Find(catalog,i.definitionId);
                            if(definition==null)return "Invalid dealer part at row "+index+" ("+id+"): unknown definition "+i.definitionId+".";
                            if(!identities.Add(id))return "Duplicate dealer part identity at row "+index+": "+id+".";
                            if(definition.category!=PartCategory.Clutch&&i.condition.clutch!=null)return "Dealer part at row "+index+" has unrelated clutch state: "+id+".";
                            foreach(var e in i.condition.elements){
                                if(!EngineElements.FaultAllowed(definition.category,e))return "Invalid dealer descendant at row "+index+" ("+id+"): mismatched type or fault "+e.id+".";
                                if(!identities.Add(e.id))return "Duplicate dealer descendant at row "+index+" ("+id+"): "+e.id+".";
                                if(!string.IsNullOrEmpty(e.definitionId)&&(LegacyMechanicalCatalogReference.Find(catalog,e.definitionId)==null||LegacyMechanicalCatalogReference.Find(catalog,e.definitionId).category!=definition.category))
                                    return "Invalid dealer descendant specification at row "+index+" ("+id+"): "+e.definitionId+".";
                            }
                            foreach(var t in i.condition.tires)
                                if(!string.IsNullOrEmpty(t.definitionId)&&(LegacyMechanicalCatalogReference.Find(catalog,t.definitionId)==null||LegacyMechanicalCatalogReference.Find(catalog,t.definitionId).category!=PartCategory.Tires))
                                    return "Invalid dealer tire specification at row "+index+" ("+id+"): "+t.definitionId+".";
                        }
                        else {var u=stock.unit;if(!LegacyValidUnit(p,catalog,u)||!identities.Add(u.element.id))return "Invalid/duplicate dealer unit.";}
                    }
                    return null;
                }

                static bool LegacyValidUnit(PlayerProfile p,MechanicalCatalog catalog,LooseEngineElement u)
                {
                    var d=u==null?null:LegacyMechanicalCatalogReference.Find(catalog,u.definitionId);if(d==null||u.element==null||u.element.Validate()!=null||u.unitPrice<0)return false;
                    if(u.tireUnit)return d.category==PartCategory.Tires&&u.element is TireCondition&&u.tire.Validate()==null&&(string.IsNullOrEmpty(u.tire.definitionId)||u.tire.definitionId==u.definitionId);
                    return EngineElements.Supported(d.category)&&EngineElements.FaultAllowed(d.category,u.element)&&u.tire==null&&(string.IsNullOrEmpty(u.element.definitionId)||u.element.definitionId==u.definitionId);
                }

                static string LegacyValidateMarket(PlayerProfile p,MechanicalCatalog catalog)
                {
                    var m=p.inventory.market;
                    if(m==null||m.version!=2||m.seed!=p.vehicleStock.seed||m.generation!=p.vehicleStock.generation||m.guaranteeAtRefresh<0||m.guaranteeAtRefresh>20||m.parts==null||m.engines==null)return "Invalid parts market epoch.";
                    var ids=new HashSet<string>();foreach(var s in m.parts)if(s==null||s.definitions==null||s.definitions.Count==0||s.units<0||s.units>s.initialUnits||s.initialUnits<0||s.deliveryPercent<5||s.deliveryPercent>10||s.definitions.Any(id=>LegacyMechanicalCatalogReference.Find(catalog,id)==null||!ids.Add(id)))return "Invalid new-parts stock.";
                    string reservationError=LegacyValidateReservations(p,catalog);if(reservationError!=null)return reservationError;
                    ids=LegacyExistingMarketIdentitySet(p);foreach(var o in m.engines){if(o==null||string.IsNullOrWhiteSpace(o.id)||!ids.Add(o.id)||!ids.Add(o.engine?.instanceId??"")||o.quantity<0||o.quantity>3||o.used&&o.quantity>1||o.price<1||o.engine==null||o.engine.vehicleDefinitionId!=o.model||!string.IsNullOrEmpty(o.engine.parentVehicleId)||o.parts==null||o.parts.Count==0||o.engine.slots==null||o.engine.slots.Count!=o.parts.Count)return "Invalid engine stock.";
                        var map=new Dictionary<PartCategory,string>();foreach(var i in o.parts){var d=LegacyMechanicalCatalogReference.Find(catalog,i.definitionId);if(d==null||!MechanicalCatalog.EngineSlot(d.category)||!catalog.Fits(LegacyMechanicalCatalogReference.Spec(catalog,o.model),d)||i.Validate()!=null||!ids.Add(i.instanceId)||i.ownerId!=o.engine.instanceId||i.location!=PartLocation.Assembly||map.ContainsKey(d.category)||!o.engine.slots.Any(s=>s.partInstanceId==i.instanceId&&s.slot==d.category.ToString()))return "Invalid offered engine component.";map.Add(d.category,d.id);foreach(var e in i.condition.elements)if(!ids.Add(e.id)||!EngineElements.FaultAllowed(d.category,e))return "Invalid offered engine descendant.";}
                        if(catalog.Recipe(o.model).Any(v=>MechanicalCatalog.EngineSlot(v.Key)&&!map.ContainsKey(v.Key))||o.engine.fluids==null||o.engine.fluids.Validate(LegacyMechanicalCatalogReference.Spec(catalog,o.model).fluids)!=null||o.engine.tune==null)return "Incomplete offered engine.";
                    }return null;
                }

                static HashSet<string> LegacyExistingMarketIdentitySet(PlayerProfile p)
                {
                    // The source LINQ expression validates all source arguments before the
                    // HashSet starts enumerating; preserve that exception order without
                    // allocating a SelectMany/Concat/ItemIds iterator for each part.
                    var parts=p.parts;if(parts==null)throw new ArgumentNullException("source");
                    var engines=p.engines;if(engines==null)throw new ArgumentNullException("source");
                    var looseUnits=p.looseEngineElements;if(looseUnits==null)throw new ArgumentNullException("source");
                    var dealer=p.inventory.dealer;if(dealer==null)throw new ArgumentNullException("source");
                    var listings=p.vehicleStock.listings;if(listings==null)throw new ArgumentNullException("source");

                    var ids=new HashSet<string>();
                    foreach(var part in parts)LegacyAddPartIds(ids,part);
                    foreach(var engine in engines)ids.Add(engine.instanceId);
                    foreach(var unit in looseUnits)ids.Add(unit.element.id);
                    foreach(var stock in dealer)
                    {
                        if(stock.IsPart)LegacyAddPartIds(ids,stock.part);
                        else if(stock.IsUnit)ids.Add(stock.unit.element.id);
                    }
                    foreach(var listing in listings)
                    {
                        var listingParts=listing.parts;if(listingParts==null)throw new ArgumentNullException("source");
                        foreach(var part in listingParts)LegacyAddPartIds(ids,part);
                    }
                    return ids;
                }

                static void LegacyAddPartIds(HashSet<string> ids,PartInstance part)
                {
                    ids.Add(part.instanceId);
                    var elements=part.condition.elements;
                    if(elements==null)return;
                    for(int i=0;i<elements.Length;i++)
                    {
                        var element=elements[i];
                        if(element!=null&&!string.IsNullOrEmpty(element.id))ids.Add(element.id);
                    }
                }

                static string LegacyValidateReservations(PlayerProfile p,MechanicalCatalog catalog)
                {
                    var state=p.inventory.cart;if(state==null)return null;
                    if(state.version!=1||string.IsNullOrEmpty(state.carId)||state.lines==null||state.supply==null||state.lines.Count<1||state.lines.Count>100||state.createdRun<0||state.createdRun>p.vehicleStock.completedRuns||state.expiresRun!=(long)state.createdRun+CartReservations.LifetimeRaces||state.generation<0||state.generation>p.inventory.market.generation)return "Invalid cart reservation clock or owner.";
                    if(state.lines.Any(l=>l==null||!Enum.IsDefined(typeof(CartAction),l.action)||l.quantity<1||l.quantity>99||l.positions==null||l.definitionId!=null&&LegacyMechanicalCatalogReference.Find(catalog,l.definitionId)==null)||state.supply.Any(s=>s==null||s.units<0||s.units>25344||s.generation<0||s.generation>p.inventory.market.generation||LegacyMechanicalCatalogReference.Find(catalog,s.definitionId)==null))return "Invalid reserved stock or cart lines.";
                    if(LegacyCartQueryReference.Active(p))foreach(var row in p.inventory.market.parts){int held=state.supply.Where(s=>s.generation==p.inventory.market.generation&&row.definitions.Contains(s.definitionId)).Sum(s=>s.units);if(held>row.units)return "Reserved stock exceeds its original supply.";}
                    return null;
                }

                static string LegacyValidateSetup(SavedVehicleSetup setup,MechanicalCatalog catalog)
                {
                    if(setup==null||string.IsNullOrWhiteSpace(setup.id)||string.IsNullOrWhiteSpace(setup.name)||setup.name.Length>64||setup.tune==null||setup.parts==null||!Enum.IsDefined(typeof(SetupScope),setup.scope))return "Invalid saved setup.";
                    var spec=LegacyMechanicalCatalogReference.Spec(catalog,setup.vehicleDefinitionId);var slots=new HashSet<string>();
                    foreach(var part in setup.parts)
                    {
                        PartCategory category;
                        if(part==null||!Enum.TryParse(part.slot,out category)||!SavedSetups.InScope(setup.scope,category)||!slots.Add(part.slot))return "Invalid saved setup slot.";
                        if(part.wheelDefinitions!=null&&part.wheelDefinitions.Length>0&&(category!=PartCategory.Wheels||part.wheelDefinitions.Length!=4||part.wheelDefinitions.Any(id=>LegacyMechanicalCatalogReference.Find(catalog,id)==null||LegacyMechanicalCatalogReference.Find(catalog,id).category!=PartCategory.Wheels||!catalog.Fits(spec,LegacyMechanicalCatalogReference.Find(catalog,id)))))return "Invalid saved wheel fitment.";
                        if(part.tireDefinitions!=null&&part.tireDefinitions.Length>0&&(category!=PartCategory.Tires||part.tireDefinitions.Length!=4||part.tireDefinitions.Any(id=>LegacyMechanicalCatalogReference.Find(catalog,id)==null||LegacyMechanicalCatalogReference.Find(catalog,id).category!=PartCategory.Tires||!catalog.Fits(spec,LegacyMechanicalCatalogReference.Find(catalog,id)))))return "Invalid saved tire fitment.";
                        var definition=LegacyMechanicalCatalogReference.Find(catalog,part.definitionId);if(definition==null||definition.category!=category||!catalog.Fits(spec,definition))return "Invalid saved setup part fitment.";
                    }
                    foreach(var field in typeof(Tune).GetFields())if(field.FieldType==typeof(float)&&!MechanicsSchema.Finite((float)field.GetValue(setup.tune)))return "Nonfinite saved tuning.";
                    if(setup.tune.awdConfigured&&(setup.tune.awd==null||setup.tune.awd.Validate()!=null))return "Invalid saved AWD tuning.";
                    return null;
                }


        // Literal pre-edit vehicle-listing validation core, routed through the frozen catalog reader.
         static string LegacyValidateVehicleListings(PlayerProfile p,MechanicalCatalog c){var stock=p.vehicleStock;if(stock==null||stock.version!=1||stock.seed==0||stock.generation<0||stock.purchases<0||stock.completedRuns<0||stock.knowledgeRevision<0||stock.listings==null||stock.listings.Count>100)return "Invalid vehicle market.";if(p.firstPurchasePending&&(stock.listings.Count!=3||stock.listings.Any(o=>o==null||!o.firstStarter)))return "Initial market must contain the three fully disclosed starter choices.";var ids=new HashSet<string>();var components=new HashSet<string>(p.parts.Select(i=>i.instanceId));
         foreach(var o in stock.listings){if(o==null||string.IsNullOrEmpty(o.listingId)||!ids.Add(o.listingId)||o.price<=0||o.cataloguePrice<0||o.reputation<0||o.stock==null||o.parts==null||o.fuel==null||o.fluids==null||!MechanicsSchema.Finite(o.mileageKm)||o.mileageKm<0)return "Invalid vehicle listing.";if(VehicleInspection.Validate(o)!=null)return VehicleInspection.Validate(o);var spec=LegacyMechanicalCatalogReference.Spec(c,o.model);var map=new Dictionary<PartCategory,string>();foreach(var part in o.parts){var d=LegacyMechanicalCatalogReference.Find(c,part.definitionId);if(d==null||!c.Fits(spec,d)||!components.Add(part.instanceId)||part.condition.Validate()!=null||(part.condition.elements??Array.Empty<EngineElement>()).Any(e=>!EngineElements.FaultAllowed(d.category,e))||map.ContainsKey(d.category))return "Invalid listing component identity/fitment/condition.";map.Add(d.category,d.id);}var build=LegacyMechanicalCatalogReference.Resolve(c,o.model,map,o.stock);if(!build.Drivable||o.fuel.Validate(build.spec.fuel)!=null||o.fluids.Validate(build.spec.fluids)!=null)return "Invalid listing build/resources.";}
         return null;}

    }
}
#endif

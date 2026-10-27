#if UNITY_EDITOR
using System;
using System.Linq;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    /// <summary>
    /// Isolated, deterministic source-profile factories for S06D2B step-1.
    /// These methods never load or write a player profile and never call Ensure
    /// after returning a fixture to a caller.
    /// </summary>
    public static class CartPreviewCharacterizationFixtures
    {
        public const long FixedNowTicks = 638000000000000000L;

        public static void CreateNormalized(out PlayerProfile profile,out string carId,uint seed)
        {
            if(seed==0||seed>int.MaxValue)throw new ArgumentOutOfRangeException(nameof(seed),"Use a nonzero fixture seed that fits the authored market seed field.");
            var catalog=MechanicsTransactions.Catalog();
            if(catalog==null||catalog.parts==null||catalog.parts.Count==0)throw new InvalidOperationException("Cart characterization requires the authored mechanics catalogue.");
            profile=PlayerProfile.New(FixedNowTicks);
            profile.cash=100000000;
            profile.reputation=10000;
            profile.catalog=catalog;
            VehicleListings.Initialize(profile,catalog,(int)seed);
            MechanicsOwnership.Ensure(profile,catalog);
            var car=profile.SelectedCar;
            if(car==null||string.IsNullOrWhiteSpace(car.instanceId)||car.vehicleId!="starter-hatch"||profile.SelectedCar.instanceId!=car.instanceId)
                throw new InvalidOperationException("The normalized cart fixture did not create and select its real starter vehicle.");
            carId=car.instanceId;

            uint usedSeed=seed^0x5f3759dfu;if(usedSeed==0)usedSeed=1;
            AddUsedPart(profile,"cart-preview/used-part/seed-"+seed.ToString(System.Globalization.CultureInfo.InvariantCulture),ref usedSeed);
            var validation=MechanicsOwnership.Validate(profile,catalog);
            if(validation!=null)throw new InvalidOperationException("Normalized cart fixture failed global validation: "+validation);
        }

        /// <summary>Adds and selects an actual distinct new vehicle listing on this disposable fixture.</summary>
        public static OwnedCar AddSecondOwnedCar(PlayerProfile profile)
        {
            if(profile==null)throw new ArgumentNullException(nameof(profile));
            var current=profile.SelectedCar??throw new InvalidOperationException("CreateNormalized must run before adding a second car.");
            var offer=profile.vehicleStock?.listings?.FirstOrDefault(candidate=>candidate!=null&&!candidate.used&&candidate.model!=current.vehicleId&&candidate.stock!=null);
            if(offer==null)throw new InvalidOperationException("The seeded market has no second distinct actual new-car offer.");
            string id=VehicleCollection.Buy(profile,profile.catalog,offer);
            var result=profile.cars.Find(car=>car.instanceId==id);
            if(result==null||result.engineAssemblyId==null)throw new InvalidOperationException("The actual vehicle purchase did not create its owned engine.");
            return result;
        }

        /// <summary>Detaches an owned car's current real engine through the existing inventory operation.</summary>
        public static EngineAssemblyInstance DetachOwnedEngineToStorage(PlayerProfile profile,string carId)
        {
            if(profile==null)throw new ArgumentNullException(nameof(profile));
            var car=profile.cars.Find(candidate=>candidate.instanceId==carId)??throw new InvalidOperationException("The fixture car to detach does not exist.");
            var engine=profile.engines.Find(candidate=>candidate.instanceId==car.engineAssemblyId&&candidate.parentVehicleId==car.instanceId)
                ??throw new InvalidOperationException("The fixture car has no attached real engine to detach.");
            SharedInventory.Move(profile,engine.instanceId,InventoryKind.Storage,SharedInventory.Storage);
            if(!string.IsNullOrEmpty(engine.parentVehicleId)||engine.storageId!=SharedInventory.Storage)
                throw new InvalidOperationException("The real engine did not move into the existing Storage container.");
            return engine;
        }

        /// <summary>Creates one actual used dealer part from authored parts-market supply.</summary>
        public static DealerStock AddUsedPart(PlayerProfile profile,string idPrefix,ref uint seed)
        {
            return AddUsedSupplyWithShape(profile,idPrefix,ref seed,false);
        }

        /// <summary>Creates actual used dealer unit rows and returns one existing modeled unit.</summary>
        public static DealerStock AddUsedUnit(PlayerProfile profile,string idPrefix,ref uint seed)
        {
            return AddUsedSupplyWithShape(profile,idPrefix,ref seed,true);
        }

        static DealerStock AddUsedSupplyWithShape(PlayerProfile profile,string idPrefix,ref uint seed,bool wantUnit)
        {
            if(profile==null)throw new ArgumentNullException(nameof(profile));
            if(string.IsNullOrWhiteSpace(idPrefix))throw new ArgumentException("A stable fixture identity prefix is required.",nameof(idPrefix));
            if(seed==0)throw new ArgumentOutOfRangeException(nameof(seed),"Use a nonzero local used-supply seed.");
            var car=profile.SelectedCar??throw new InvalidOperationException("CreateNormalized must run before adding used stock.");
            var spec=profile.catalog.Spec(car.vehicleId);
            var candidates=profile.catalog.parts.OrderBy(definition=>definition.id,StringComparer.Ordinal)
                .Where(definition=>definition!=null&&profile.catalog.Fits(spec,definition))
                .Where(definition=>{
                    int count=EngineElements.Count(spec,definition);
                    if(count<=0)count=definition.category==PartCategory.Tires?4:1;
                    return wantUnit?count>1:count<=1;
                }).ToArray();
            if(candidates.Length==0)throw new InvalidOperationException(wantUnit
                ?"The selected authored vehicle has no compatible multi-member used-unit definition."
                :"The selected authored vehicle has no compatible used-part definition with set-only supply.");

            const int attemptsPerDefinition=128;
            int attemptIndex=0;
            foreach(var definition in candidates)
            {
                for(int attempt=0;attempt<attemptsPerDefinition;attempt++,attemptIndex++)
                {
                    uint localSeed=unchecked(seed+(uint)attemptIndex);if(localSeed==0)localSeed=1;
                    uint random=localSeed;
                    string id=idPrefix+"/"+definition.id+"/"+attempt.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    int firstNew=profile.inventory.dealer.Count;
                    PartsMarket.AddUsedSupply(profile,spec,definition,id,ref random);
                    var rows=profile.inventory.dealer.Skip(firstNew).ToArray();
                    var match=rows.FirstOrDefault(row=>row!=null&&(wantUnit?row.IsUnit:row.IsPart));
                    if(match!=null)
                    {
                        seed=random;
                        if(wantUnit&&(match.unit==null||match.unit.element==null||match.unit.element.Validate()!=null||string.IsNullOrWhiteSpace(match.unit.element.id)))
                            throw new InvalidOperationException("Used supply did not create a valid actual modeled loose unit.");
                        if(!wantUnit&&(match.part==null||match.part.Validate()!=null||string.IsNullOrWhiteSpace(match.part.instanceId)))
                            throw new InvalidOperationException("Used supply did not create a valid actual physical part.");
                        return match;
                    }
                    profile.inventory.dealer.RemoveRange(firstNew,profile.inventory.dealer.Count-firstNew);
                }
            }
            throw new InvalidOperationException("The bounded authored used-supply search did not produce the requested real dealer row shape.");
        }
    }
}
#endif

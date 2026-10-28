#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Runtime
{
    /// <summary>Editor-only cart row-order and checkout characterization.</summary>
    public static partial class CartPreviewCharacterizationChecks
    {
        const uint OrderFixtureSeed=20261029u;
        const int OrderPreviewCash=int.MaxValue/2;

        static void RunOrderSupplement(Trace trace,string subset)
        {
            if(trace==null)throw new ArgumentNullException(nameof(trace));
            if(subset!="order"&&subset!="order-sales"&&subset!="order-chain")
                throw new ArgumentException("Expected order, order-sales, or order-chain.",nameof(subset));
            trace.Add("ORDER_SUPPLEMENT|subset="+subset+";candidate=PartCart+actual-domain;legacy-oracle=Total/Preview/Apply;typed-policy=unapproved");
            if(subset=="order"||subset=="order-sales")OrderGroup(trace,"cash-constrained-sale-purchase-move",OrderSalePurchaseMove);
            if(subset=="order"||subset=="order-chain")OrderGroup(trace,"occupied-slot-displacement-move-chain",OrderOccupiedSlotMoveChain);
            trace.Add("ORDER_SUPPLEMENT_DISPOSITION|observations-only;legacy-json/source-differences-stay-strict;no-S07-conversion");
        }

        static void OrderGroup(Trace trace,string name,Action<Trace> action)
        {
            try{action(trace);}
            catch(Exception error)
            {
                trace.Assert(false,"Order fixture completed / "+name+" / "+error.GetType().FullName+" | "+error.Message);
                trace.Add("ORDER_OPEN|"+name+"|fixture exception retained; see exact local failure output");
            }
        }

        static void OrderOpen(Trace trace,string name,string reason)
        {
            trace.Add("ORDER_OPEN|"+name+"|"+OrderSafe(reason));
            trace.Assert(false,"Required actual authored order fixture exists / "+name+" / "+OrderSafe(reason));
        }

        static void OrderSalePurchaseMove(Trace trace)
        {
            const string name="sale-first-checkout-and-supported-row-permutation";
            CartPreviewCharacterizationFixtures.CreateNormalized(out var profile,out string carId,OrderFixtureSeed);
            var car=profile.cars.Find(item=>item.instanceId==carId);
            if(car==null){OrderOpen(trace,name,"normalized authored selected car is missing");return;}

            var purchase=profile.catalog.parts.Where(definition=>definition!=null&&definition.price>0&&profile.reputation>=definition.reputationRequired&&
                    profile.catalog.Fits(profile.catalog.Spec(car.vehicleId),definition)&&InventoryUnits.Count(profile,car,definition)>1&&
                    PartsMarket.Stock(profile,definition)!=null)
                .OrderBy(definition=>definition.id,StringComparer.Ordinal).FirstOrDefault();
            if(purchase==null){OrderOpen(trace,name,"no authored stocked compatible multi-member set for a real partial-local/express quote");return;}
            string purchaseProduct=PartsMarket.ProductKey(purchase);
            var singleOffers=profile.catalog.parts.Where(definition=>definition!=null&&definition.price>0&&
                    profile.reputation>=definition.reputationRequired&&definition.id!=purchase.id&&
                    profile.catalog.Fits(profile.catalog.Spec(car.vehicleId),definition)&&InventoryUnits.Count(profile,car,definition)==1&&
                    PartsMarket.Stock(profile,definition)!=null&&PartsMarket.ProductKey(definition)!=purchaseProduct)
                .Select(definition=>new{Definition=definition,Product=PartsMarket.ProductKey(definition),
                    Cost=PartsMarket.Quote(profile,definition,1,false,1).cost})
                .OrderBy(candidate=>candidate.Cost).ThenBy(candidate=>candidate.Definition.id,StringComparer.Ordinal).ToArray();
            var saleCandidate=singleOffers.FirstOrDefault();
            var moveCandidate=saleCandidate==null?null:singleOffers.FirstOrDefault(candidate=>candidate.Product!=saleCandidate.Product&&
                candidate.Cost<=profile.cash-saleCandidate.Cost);
            if(saleCandidate==null||moveCandidate==null)
            {OrderOpen(trace,name,"authored catalogue has no two distinct single-part product groups whose real sequential quotes fit setup cash");return;}

            string saleId,moveId;
            try
            {
                saleId=PartOperations.Buy(profile,profile.catalog,carId,saleCandidate.Definition.id,false);
                moveId=PartOperations.Buy(profile,profile.catalog,carId,moveCandidate.Definition.id,false);
            }
            catch(Exception error)
            {OrderOpen(trace,name,"actual setup PartOperations.Buy rejected selected authored offers: "+error.GetType().FullName+" | "+error.Message);return;}
            var saleDefinition=saleCandidate.Definition;var moveDefinition=moveCandidate.Definition;
            var sold=profile.parts.Find(item=>item.instanceId==saleId&&item.location==PartLocation.Inventory);
            var moved=profile.parts.Find(item=>item.instanceId==moveId&&item.location==PartLocation.Inventory);
            if(sold==null||moved==null){OrderOpen(trace,name,"actual authored PartOperations.Buy did not create both loose physical part IDs");return;}

            int count=InventoryUnits.Count(profile,car,purchase);
            var stock=PartsMarket.Stock(profile,purchase);
            if(count<2||stock==null){OrderOpen(trace,name,"selected authored purchase has no multi-member stock row");return;}
            stock.units=count-1;stock.initialUnits=count-1;stock.deliveryPercent=5;
            string stockValidation=MechanicsOwnership.Validate(profile,profile.catalog);
            if(stockValidation!=null){OrderOpen(trace,name,"controlled real shelf quantity rejected by authoritative profile validation: "+stockValidation);return;}

            var sale=new PartCartLine{action=CartAction.SellPart,definitionId=sold.definitionId,assetId=saleId,quantity=1};
            var buy=new PartCartLine{action=CartAction.NewSet,definitionId=purchase.id,quantity=1};
            var move=new PartCartLine{action=CartAction.Move,definitionId=moved.definitionId,assetId=moveId,
                destination=InventoryKind.Storage,destinationId=SharedInventory.Storage,quantity=1};
            var saleFirst=new[]{sale,buy,move};
            var purchaseFirst=new[]{buy,move,sale};
            var policy=ValuationPolicy.Defaults();long now=checked(CartPreviewCharacterizationFixtures.FixedNowTicks+1);
            int units=count;
            var supplyBeforeHold=PartsMarket.Quote(profile,purchase,units,false,1);
            int salePrice=Valuations.LoosePriceView(profile,policy,"wholesale",now)(saleId,false);
            int gross=supplyBeforeHold.cost;
            int expectedNet=checked(gross-salePrice);
            profile.cash=Math.Max(0,expectedNet);
            int cashBefore=profile.cash;
            CartReservations.Capture(profile,carId,saleFirst);
            int held=CartReservations.Held(profile,purchase,true);
            int express=Math.Max(0,units-Math.Min(units,held));
            string valid=MechanicsOwnership.Validate(profile,profile.catalog);
            trace.Assert(valid==null,"cash-limited sale/purchase fixture and exact hold validate without normalization / "+OrderSafe(valid??"valid"));
            trace.Assert(CartReservations.Matches(profile,carId,saleFirst)&&held==count-1&&express==1&&stock.units==count-1,
                "sale-first basket holds the real current shelf remainder and exposes one express member / held="+held+";express="+express);
            trace.Assert(salePrice>0&&cashBefore<gross&&checked(cashBefore+salePrice)>=gross&&cashBefore>=Math.Max(0,expectedNet),
                "actual sale credit is required to fund the controlled purchase / cash="+cashBefore+";gross="+gross+";sale="+salePrice);
            if(valid!=null)return;

            var inverseProfile=CartPreviewLiteralTestClone.Copy(profile);
            CartReservations.Capture(inverseProfile,carId,purchaseFirst);
            string inverseValidation=MechanicsOwnership.Validate(inverseProfile,inverseProfile.catalog);
            trace.Assert(inverseValidation==null&&CartReservations.Matches(inverseProfile,carId,purchaseFirst)&&
                CartReservations.Held(inverseProfile,purchase,true)==held,
                "inverse sale-row order gets its own equivalent current-generation hold / "+OrderSafe(inverseValidation??"valid"));
            if(inverseValidation!=null)return;

            var firstTotal=CompareTotal(trace,name+"-sale-first",profile,carId,saleFirst,policy,now);
            var inverseTotal=CompareTotal(trace,name+"-sale-last",inverseProfile,carId,purchaseFirst,policy,now);
            trace.Assert(!firstTotal.Threw&&!inverseTotal.Threw&&
                Convert.ToInt32(firstTotal.Value,CultureInfo.InvariantCulture)==expectedNet&&
                Convert.ToInt32(inverseTotal.Value,CultureInfo.InvariantCulture)==expectedNet,
                "Total preserves sale credit and identical non-sale row order under sale-row permutation / first="+firstTotal.Text+";inverse="+inverseTotal.Text+";expected="+expectedNet);
            var firstFee=CompareFee(trace,name+"-sale-first",profile,carId,saleFirst);
            var inverseFee=CompareFee(trace,name+"-sale-last",inverseProfile,carId,purchaseFirst);
            trace.Assert(!firstFee.Threw&&!inverseFee.Threw&&Convert.ToInt32(firstFee.Value,CultureInfo.InvariantCulture)==supplyBeforeHold.fee&&
                Convert.ToInt32(inverseFee.Value,CultureInfo.InvariantCulture)==supplyBeforeHold.fee,
                "DeliveryFee retains the exact one-member express ceiling / expected="+supplyBeforeHold.fee);
            ComparePreview(trace,name+"-sale-first",profile,car,saleFirst,typeof(InvalidOperationException).FullName,"Insufficient cash.");
            ComparePreview(trace,name+"-sale-last",inverseProfile,inverseProfile.cars.Find(item=>item.instanceId==carId),purchaseFirst,
                typeof(InvalidOperationException).FullName,"Insufficient cash.");

            OrderPreviewManual(trace,name+"-sale-first",profile,carId,saleFirst,saleId,moveId,purchase.id,gross,cashBefore,stock.units);
            OrderPreviewManual(trace,name+"-sale-last",inverseProfile,carId,purchaseFirst,saleId,moveId,purchase.id,gross,cashBefore,stock.units);
            var checkedFirst=OrderQuoteCheckout(trace,name+"-sale-first",profile,carId,saleFirst,policy,now,saleId,moveId,purchase.id,
                sold,gross,salePrice,expectedNet,cashBefore,stock.units);
            var checkedInverse=OrderQuoteCheckout(trace,name+"-sale-last",inverseProfile,carId,purchaseFirst,policy,now,saleId,moveId,purchase.id,
                sold,gross,salePrice,expectedNet,cashBefore,stock.units);
            if(checkedFirst!=null&&checkedInverse!=null)
            {
                trace.Assert(checkedFirst.SalePaidPrice==checkedInverse.SalePaidPrice&&checkedFirst.Total==checkedInverse.Total&&
                    checkedFirst.CashAfter==checkedInverse.CashAfter&&checkedFirst.StockAfter==checkedInverse.StockAfter&&
                    checkedFirst.SaleRetained==checkedInverse.SaleRetained&&checkedFirst.MoveStored==checkedInverse.MoveStored&&
                    checkedFirst.PurchaseDefinition==checkedInverse.PurchaseDefinition,
                    "sale-row permutation produces the same checkout cash/stock/ownership receipt semantics; generated IDs remain per-checkout values");
            }
            trace.Add("ORDER_SALE_CHECKOUT|sale="+saleId+";move="+moveId+";purchase="+purchase.id+";rows=3;cash="+cashBefore+
                ";gross="+gross+";saleCredit="+salePrice+";net="+expectedNet+";held="+held+";express="+express+";fixedTicks="+now);
        }

        sealed class OrderCheckoutFacts
        {
            public int SalePaidPrice,Total,CashAfter,StockAfter;
            public bool SaleRetained,MoveStored;
            public string PurchaseDefinition;
        }

        static void OrderPreviewManual(Trace trace,string name,PlayerProfile source,string carId,PartCartLine[] rows,
            string saleId,string moveId,string purchaseDefinition,int gross,int cashBefore,int stockBefore)
        {
            var input=CartPreviewLiteralTestClone.Copy(source);
            var before=FlattenNormalization(input);var refs=NormalizationReferenceTopology(input);
            var sourceBefore=SourceSnapshot(source);var random=UnityEngine.Random.state;
            PlayerProfile preview=null;string errorType=null,errorMessage=null;
            try{preview=PartCart.PreviewProfile(input,input.cars.Find(item=>item.instanceId==carId),CopyRows(rows));}
            catch(Exception error){errorType=error.GetType().FullName;errorMessage=error.Message;}
            var inputDiff=NormalizationDiff(before,FlattenNormalization(input));
            var inputTopologyDiff=NormalizationDiff(refs,NormalizationReferenceTopology(input));
            WriteNormalizationDiff(trace,name+"-manual-preview-input",inputDiff,inputTopologyDiff);
            bool originalStable=sourceBefore==SourceSnapshot(source);
            bool randomStable=random.Equals(UnityEngine.Random.state);
            trace.Assert(originalStable&&randomStable,"manual PreviewProfile probe leaves baseline source and Unity RNG unchanged / "+name+
                ";rngChanged="+(!randomStable));
            trace.Assert(inputDiff.Count==0&&inputTopologyDiff.Count==0,
                "manual PreviewProfile leaves isolated typed input values and reference topology unchanged / "+name+
                ";fieldPaths="+inputDiff.Count+";topologyPaths="+inputTopologyDiff.Count);
            if(errorType!=null)
            {
                trace.Add("ORDER_PREVIEW|"+name+"|threw="+OrderSafe(errorType+" | "+errorMessage)+";inputDiffPaths="+inputDiff.Count+
                    ";inputTopologyDiffPaths="+inputTopologyDiff.Count);
                trace.Assert(false,"manual PreviewProfile produced a result for supported sale/purchase/move rows / "+name+" / "+OrderSafe(errorType+" | "+errorMessage));
                return;
            }
            var salePart=preview.parts.Find(item=>item.instanceId==saleId);
            var saleDealer=preview.inventory.dealer.Find(item=>item!=null&&item.Id==saleId);
            var moved=preview.parts.Find(item=>item.instanceId==moveId);
            var bought=preview.parts.Where(item=>!source.parts.Any(original=>original.instanceId==item.instanceId)&&item.definitionId==purchaseDefinition).ToArray();
            var purchaseStock=PartsMarket.Stock(preview,preview.catalog.Find(purchaseDefinition));
            // The display preview writes a large cash sentinel, applies only non-sale rows,
            // and clears an owned reservation. Use the captured stock row to pin its actual debit.
            int sourceStock=PartsMarket.Stock(source,source.catalog.Find(purchaseDefinition))?.units??-1;
            int previewStock=purchaseStock?.units??-1;
            var expectedMoveFields=FlattenNormalization(source.parts.Find(item=>item.instanceId==moveId));
            expectedMoveFields["$.storageId"]=TypedCanonical(SharedInventory.Storage,new Dictionary<string,string>(),new List<object>(),"storageId");
            var moveFieldDiff=moved==null?new List<string>{"$ | before=<expected> | after=<missing>"}:
                NormalizationDiff(expectedMoveFields,FlattenNormalization(moved));
            WriteNormalizationDiff(trace,name+"-move-output",moveFieldDiff,new List<string>());
            bool ok=preview.cash==checked(OrderPreviewCash-gross)&&salePart!=null&&saleDealer==null&&moved!=null&&
                moved.location==PartLocation.Inventory&&moved.storageId==SharedInventory.Storage&&moveFieldDiff.Count==0&&bought.Length==1&&
                previewStock==Math.Max(0,sourceStock-(stockBefore));
            trace.Assert(ok,"PreviewProfile skips sale transfer/credit, applies purchase and move with sentinel cash / "+name+
                ";cash="+preview.cash+";saleOwned="+(salePart!=null)+";saleDealer="+(saleDealer!=null)+";purchaseRoots="+bought.Length+
                ";stock="+sourceStock+"->"+previewStock);
            trace.Assert(preview.economyReceipts.Count==source.economyReceipts.Count,
                "PreviewProfile does not publish an economy receipt / "+name);
            trace.Add("ORDER_PREVIEW|"+name+"|saleApplied=false;cash="+preview.cash+";sentinel="+OrderPreviewCash+
                ";purchaseGross="+gross+";saleStillOwned="+(salePart!=null)+";moveStorage="+(moved?.storageId??"<missing>")+
                ";purchaseRoots="+bought.Length+";stock="+sourceStock+"->"+previewStock+";inputDiffPaths="+inputDiff.Count+
                ";inputTopologyDiffPaths="+inputTopologyDiff.Count+";moveFieldDiff="+moveFieldDiff.Count);
        }

        static OrderCheckoutFacts OrderQuoteCheckout(Trace trace,string name,PlayerProfile source,string carId,PartCartLine[] rows,
            ValuationPolicy policy,long now,string saleId,string moveId,string purchaseDefinition,PartInstance expectedSold,
            int gross,int salePrice,int expectedNet,int cashBefore,int stockBefore)
        {
            var candidate=CartPreviewLiteralTestClone.Copy(source);
            var candidateRows=CopyRows(rows);
            var nonSaleOrder=candidateRows.Where(row=>row.action!=CartAction.SellPart&&row.action!=CartAction.SellUnit)
                .Select(row=>row.action.ToString()).ToArray();
            bool orderedNonSaleApply=nonSaleOrder.SequenceEqual(new[]{CartAction.NewSet.ToString(),CartAction.Move.ToString()},StringComparer.Ordinal);
            trace.Assert(orderedNonSaleApply,"checkout passes non-sale rows to Apply in captured NewSet then Move order / "+name+
                ";order="+string.Join(",",nonSaleOrder));
            string sourceBefore=SourceSnapshot(source),rowsBefore=RowsSnapshot(candidateRows);
            var candidateFields=FlattenNormalization(candidate);var candidateRefs=NormalizationReferenceTopology(candidate);
            string priorReceiptFacts=TypedCanonical(source.economyReceipts,new Dictionary<string,string>());
            var random=UnityEngine.Random.state;
            PartCartQuote quote=null;string stage="Quote",errorType=null,errorMessage=null;
            RuntimeEntropy.IdentityObservation identities=null;
            try
            {
                using(identities=RuntimeEntropy.BeginIdentityObservation())
                {
                    quote=PartCart.Quote(candidate,carId,candidateRows,now,policy.quoteSeconds,policy);
                    var quoteFieldDiff=NormalizationDiff(candidateFields,FlattenNormalization(candidate));
                    var quoteTopologyDiff=NormalizationDiff(candidateRefs,NormalizationReferenceTopology(candidate));
                    WriteNormalizationDiff(trace,name+"-quote-input",quoteFieldDiff,quoteTopologyDiff);
                    trace.Assert(quoteFieldDiff.Count==0&&quoteTopologyDiff.Count==0,
                        "PartCart.Quote leaves its exact profile fields and reference topology unchanged / "+name+
                        ";fieldPaths="+quoteFieldDiff.Count+";topologyPaths="+quoteTopologyDiff.Count);
                    var checkoutFieldsBefore=FlattenNormalization(candidate);
                    var checkoutRefsBefore=NormalizationReferenceTopology(candidate);
                    stage="Checkout";
                    PartCart.Checkout(candidate,quote,now,policy);
                    var checkoutFieldDiff=NormalizationDiff(checkoutFieldsBefore,FlattenNormalization(candidate));
                    var checkoutTopologyDiff=NormalizationDiff(checkoutRefsBefore,NormalizationReferenceTopology(candidate));
                    WriteNormalizationDiff(trace,name+"-checkout-input-output",checkoutFieldDiff,checkoutTopologyDiff);
                }
            }
            catch(Exception error){errorType=error.GetType().FullName;errorMessage=error.Message;}
            var candidateAfterFields=FlattenNormalization(candidate);
            var outputFields=NormalizationDiff(candidateFields,candidateAfterFields);
            var outputTopology=NormalizationDiff(candidateRefs,NormalizationReferenceTopology(candidate));
            WriteNormalizationDiff(trace,name+"-quote-checkout-output",outputFields,outputTopology);
            bool sourceStable=sourceBefore==SourceSnapshot(source);
            bool rowsStable=rowsBefore==RowsSnapshot(candidateRows);
            bool randomStable=random.Equals(UnityEngine.Random.state);
            trace.Assert(sourceStable&&rowsStable&&randomStable,"isolated Quote/Checkout leaves original fixture, cart row values and Unity RNG unchanged / "+name+
                ";rowsStable="+rowsStable+";rngChanged="+(!randomStable));
            if(errorType!=null)
            {
                trace.Add("ORDER_CHECKOUT_OPEN|"+name+"|stage="+stage+";exception="+OrderSafe(errorType+" | "+errorMessage)+
                    ";candidateChangedFields="+outputFields.Count+";candidateChangedTopology="+outputTopology.Count+
                    ";identityEvents="+OrderIdentitySummary(identities));
                trace.Assert(false,"authoritative PartCart.Quote+Checkout accepts controlled supported basket / "+name+
                    " / "+stage+" / "+OrderSafe(errorType+" | "+errorMessage));
                return null;
            }

            var saleInDealer=candidate.inventory.dealer.Find(item=>item!=null&&item.Id==saleId);
            var move=candidate.parts.Find(item=>item.instanceId==moveId);
            var purchaseRoots=candidate.parts.Where(item=>!source.parts.Any(original=>original.instanceId==item.instanceId)&&
                item.definitionId==purchaseDefinition).ToArray();
            var matchingReceipts=candidate.economyReceipts.Where(item=>item!=null&&item.id==quote.id).ToArray();
            var receipt=matchingReceipts.Length==1?matchingReceipts[0]:null;
            string retainedReceiptFacts=TypedCanonical(candidate.economyReceipts.Where(item=>item!=null&&item.id!=quote.id).ToList(),new Dictionary<string,string>());
            bool priorReceiptsStable=priorReceiptFacts==retainedReceiptFacts;
            var afterStock=PartsMarket.Stock(candidate,candidate.catalog.Find(purchaseDefinition));
            var soldFactsBefore=TypedCanonical(expectedSold,new Dictionary<string,string>());
            var soldFactsAfter=saleInDealer?.part==null?"<missing>":TypedCanonical(saleInDealer.part,new Dictionary<string,string>());
            var saleFieldDiff=NormalizationDiff(FlattenNormalization(expectedSold),saleInDealer?.part==null?
                new SortedDictionary<string,string>(StringComparer.Ordinal):FlattenNormalization(saleInDealer.part));
            WriteNormalizationDiff(trace,name+"-sold-part-payload",saleFieldDiff,new List<string>());
            var expectedMoveFields=FlattenNormalization(source.parts.Find(item=>item.instanceId==moveId));
            expectedMoveFields["$.storageId"]=TypedCanonical(SharedInventory.Storage,new Dictionary<string,string>(),new List<object>(),"storageId");
            var moveFieldDiff=move==null?new List<string>{"$ | before=<expected> | after=<missing>"}:
                NormalizationDiff(expectedMoveFields,FlattenNormalization(move));
            WriteNormalizationDiff(trace,name+"-moved-part-payload",moveFieldDiff,new List<string>());
            int actualStock=afterStock?.units??-1;
            bool saleMoved=saleInDealer!=null&&saleInDealer.IsPart&&saleInDealer.paidPrice==salePrice&&
                !candidate.parts.Any(item=>item.instanceId==saleId)&&soldFactsBefore==soldFactsAfter&&saleFieldDiff.Count==0;
            bool moveStored=move!=null&&move.location==PartLocation.Inventory&&move.storageId==SharedInventory.Storage&&moveFieldDiff.Count==0;
            bool purchasePresent=purchaseRoots.Length==1&&purchaseRoots[0].location==PartLocation.Inventory;
            bool receiptExact=receipt!=null&&priorReceiptsStable&&candidate.economyReceipts.Count==source.economyReceipts.Count+1&&receipt.operation=="Parts cart"&&
                receipt.target==carId&&receipt.cashBefore==cashBefore&&
                receipt.cashAfter==checked(cashBefore-expectedNet)&&receipt.utcTicks==now&&receipt.disposed!=null&&receipt.retained!=null&&
                receipt.disposed.Contains(saleId)&&receipt.retained.Contains(moveId);
            bool result=saleMoved&&moveStored&&purchasePresent&&candidate.cash==checked(cashBefore-expectedNet)&&
                actualStock==0&&receiptExact&&quote.total==expectedNet;
            trace.Assert(result,"Checkout sale-first credits exact sale, applies ordered non-sale rows, consumes local held stock and records receipt / "+name+
                ";cash="+candidate.cash+";quoteTotal="+quote.total+";stock="+actualStock+";sale="+saleMoved+
                ";move="+moveStored+";purchase="+purchasePresent+";receipt="+receiptExact+";saleFieldDiff="+saleFieldDiff.Count+
                ";moveFieldDiff="+moveFieldDiff.Count);
            trace.Assert(cashBefore<gross&&checked(cashBefore+salePrice)>=gross,
                "checkout payment is cash-constrained and sale-first credit covers purchase / "+name);
            trace.Add("ORDER_CHECKOUT|"+name+"|quote="+OrderSafe(quote.id)+";total="+quote.total+";cash="+cashBefore+"->"+candidate.cash+
                ";salePaid="+(saleInDealer?.paidPrice??-1)+";saleRetained="+saleMoved+";moveStored="+moveStored+
                ";purchaseId="+(purchaseRoots.Length==1?OrderSafe(purchaseRoots[0].instanceId):"<missing>")+";purchase="+purchasePresent+
                ";stock="+stockBefore+"->"+actualStock+";receipt="+receiptExact+";receiptCount="+candidate.economyReceipts.Count+
                ";priorReceiptsStable="+priorReceiptsStable+";nonSaleApplyOrder="+string.Join(",",nonSaleOrder)+";outputFieldDiff="+outputFields.Count+
                ";outputTopologyDiff="+outputTopology.Count+";identity="+OrderIdentitySummary(identities));
            return new OrderCheckoutFacts{SalePaidPrice=saleInDealer?.paidPrice??-1,Total=quote.total,CashAfter=candidate.cash,
                StockAfter=actualStock,SaleRetained=saleMoved,MoveStored=moveStored,PurchaseDefinition=purchaseRoots.Length==1?purchaseRoots[0].definitionId:null};
        }

        static void OrderOccupiedSlotMoveChain(Trace trace)
        {
            const string name="occupied-installed-slot-displacement-then-storage";
            CartPreviewCharacterizationFixtures.CreateNormalized(out var profile,out string carId,OrderFixtureSeed+1);
            var car=profile.cars.Find(item=>item.instanceId==carId);
            var original=car==null?null:car.installed.slots.Select(binding=>profile.parts.Find(part=>part.instanceId==binding.partInstanceId))
                .Where(part=>part!=null).Select(part=>new{Part=part,Definition=profile.catalog.Find(part.definitionId)})
                .Where(item=>item.Definition!=null&&MechanicalCatalog.EngineSlot(item.Definition.category)&&item.Definition.category!=PartCategory.CylinderBlock)
                .OrderBy(item=>item.Definition.category).ThenBy(item=>item.Part.instanceId,StringComparer.Ordinal).FirstOrDefault();
            if(original==null){OrderOpen(trace,name,"normalized actual car has no installed authored non-block engine-slot part");return;}
            var replacement=profile.catalog.parts.Where(definition=>definition!=null&&definition.id!=original.Definition.id&&
                    definition.category==original.Definition.category&&definition.price>=0&&profile.reputation>=definition.reputationRequired&&
                    profile.catalog.Fits(profile.catalog.Spec(car.vehicleId),definition)&&
                    PartsMarket.Quote(profile,definition,InventoryUnits.Count(profile,car,definition),false,1).cost<=profile.cash)
                .OrderBy(definition=>definition.id,StringComparer.Ordinal).FirstOrDefault();
            if(replacement==null){OrderOpen(trace,name,"authored catalogue has no distinct compatible replacement in actual installed category "+original.Definition.category);return;}
            string newId;
            try{newId=PartOperations.Buy(profile,profile.catalog,carId,replacement.id,false);}
            catch(Exception error){OrderOpen(trace,name,"actual setup PartOperations.Buy rejected the authored replacement: "+error.GetType().FullName+" | "+error.Message);return;}
            var newPart=profile.parts.Find(item=>item.instanceId==newId&&item.location==PartLocation.Inventory);
            if(newPart==null){OrderOpen(trace,name,"actual PartOperations.Buy did not create the compatible loose replacement part");return;}
            string oldId=original.Part.instanceId;
            string oldInitialStorage=original.Part.storageId;
            var install=new PartCartLine{action=CartAction.Move,assetId=newId,definitionId=replacement.id,
                destination=InventoryKind.Car,destinationId=carId,quantity=1};
            var storeOld=new PartCartLine{action=CartAction.Move,assetId=oldId,definitionId=original.Definition.id,
                destination=InventoryKind.Storage,destinationId=SharedInventory.Storage,quantity=1};
            var installThenStore=new[]{install,storeOld};
            var storeThenInstall=new[]{storeOld,install};
            string validation=MechanicsOwnership.Validate(profile,profile.catalog);
            trace.Assert(validation==null,"occupied-slot setup uses actual bought and installed physical parts / "+OrderSafe(validation??"valid"));
            if(validation!=null)return;
            var installTotal=CompareTotal(trace,name+"-install-then-store",profile,carId,installThenStore,ValuationPolicy.Defaults(),CartPreviewCharacterizationFixtures.FixedNowTicks);
            var reverseTotal=CompareTotal(trace,name+"-store-then-install",profile,carId,storeThenInstall,ValuationPolicy.Defaults(),CartPreviewCharacterizationFixtures.FixedNowTicks);
            trace.Assert(!installTotal.Threw&&!reverseTotal.Threw,"both real displacement row orders are supported / install="+installTotal.Text+";reverse="+reverseTotal.Text);
            ComparePreview(trace,name+"-install-then-store",profile,car,installThenStore);
            ComparePreview(trace,name+"-store-then-install",profile,car,storeThenInstall);
            var manualInstall=OrderRunMoveRows(trace,name+"-install-then-store",profile,carId,oldId,newId,original.Definition.category,original.Part,newPart,oldInitialStorage,false);
            var manualReverse=OrderRunMoveRows(trace,name+"-store-then-install",profile,carId,oldId,newId,original.Definition.category,original.Part,newPart,oldInitialStorage,true);
            trace.Add("ORDER_MOVE_CHAIN|car="+carId+";old="+oldId+";new="+newId+";category="+original.Definition.category+
                ";orders=install/store,store/install;bothTotalSupported="+(!installTotal.Threw&&!reverseTotal.Threw)+
                ";manualBoth="+(manualInstall&&manualReverse));
        }

        static bool OrderRunMoveRows(Trace trace,string name,PlayerProfile source,string carId,
            string oldId,string newId,PartCategory category,PartInstance oldSource,PartInstance newSource,string oldInitialStorage,bool reverse)
        {
            var state=CartPreviewLiteralTestClone.Copy(source);
            string originalCondition=TypedCanonical(oldSource.condition,new Dictionary<string,string>());
            string replacementCondition=TypedCanonical(newSource.condition,new Dictionary<string,string>());
            var originalOwner=state.cars.Find(item=>item.instanceId==carId);
            string tuneBefore=TypedCanonical(originalOwner.tune,new Dictionary<string,string>());
            string carFluidsBefore=TypedCanonical(originalOwner.fluids,new Dictionary<string,string>());
            var initialEngine=state.engines.Find(item=>item.instanceId==originalOwner.engineAssemblyId);
            string engineFluidsBefore=initialEngine==null?"<missing>":TypedCanonical(initialEngine.fluids,new Dictionary<string,string>());
            var baseline=FlattenNormalization(state);
            string[] sequence=reverse?new[]{oldId,newId}:new[]{newId,oldId};
            for(int index=0;index<sequence.Length;index++)
            {
                string asset=sequence[index];
                var before=FlattenNormalization(state);
                try
                {
                    if(reverse&&index==0||!reverse&&index==1)
                        SharedInventory.Move(state,asset,InventoryKind.Storage,SharedInventory.Storage);
                    else SharedInventory.Move(state,asset,InventoryKind.Car,carId);
                }
                catch(Exception error)
                {
                    trace.Add("ORDER_MOVE_STEP_OPEN|"+name+"|index="+index+";asset="+OrderSafe(asset)+
                        ";error="+OrderSafe(error.GetType().FullName+" | "+error.Message));
                    trace.Assert(false,"actual ordered SharedInventory.Move step succeeds / "+name+" / "+error.GetType().FullName+" | "+error.Message);
                    return false;
                }
                var after=FlattenNormalization(state);
                var changed=NormalizationDiff(before,after);
                WriteNormalizationDiff(trace,name+"-step-"+(index+1).ToString(CultureInfo.InvariantCulture),changed,new List<string>());
                var ownerAfter=state.cars.Find(item=>item.instanceId==carId);
                var oldAfter=state.parts.Find(item=>item.instanceId==oldId);
                var newAfter=state.parts.Find(item=>item.instanceId==newId);
                var resolution=MechanicsOwnership.Resolve(state,ownerAfter,state.catalog);
                var inspection=InspectionBuild.Capture(state,ownerAfter);
                var expectedInstalled=index==0&&!reverse||index==1&&reverse;
                bool newInstalled=newAfter!=null&&newAfter.location==PartLocation.Assembly&&newAfter.ownerId==ownerAfter.engineAssemblyId&&newAfter.slot==category.ToString()&&
                    ownerAfter.installed.slots.Any(row=>row.slot==category.ToString()&&row.partInstanceId==newId);
                bool oldCleared=oldAfter!=null&&oldAfter.location==PartLocation.Inventory&&string.IsNullOrEmpty(oldAfter.ownerId)&&
                    string.IsNullOrEmpty(oldAfter.slot)&&oldAfter.storageId==((reverse||index==1)?SharedInventory.Storage:oldInitialStorage)&&
                    !ownerAfter.installed.slots.Any(row=>row.partInstanceId==oldId);
                bool stageState=reverse&&index==0
                    ? oldCleared&&oldAfter.storageId==SharedInventory.Storage&&newAfter!=null&&newAfter.location==PartLocation.Inventory&&
                      string.IsNullOrEmpty(newAfter.ownerId)&&string.IsNullOrEmpty(newAfter.slot)&&
                      !ownerAfter.installed.slots.Any(row=>row.partInstanceId==newId)
                    : newInstalled&&oldCleared;
                bool binding=stageState;
                string oldConditionAfter=TypedCanonical(oldAfter?.condition,new Dictionary<string,string>());
                string newConditionAfter=TypedCanonical(newAfter?.condition,new Dictionary<string,string>());
                bool conditionExact=oldConditionAfter==originalCondition&&newConditionAfter==replacementCondition;
                bool fluidsExact=TypedCanonical(ownerAfter.fluids,new Dictionary<string,string>())==carFluidsBefore&&
                    (state.engines.Find(item=>item.instanceId==ownerAfter.engineAssemblyId)==null?"<missing>":
                     TypedCanonical(state.engines.Find(item=>item.instanceId==ownerAfter.engineAssemblyId).fluids,new Dictionary<string,string>()))==engineFluidsBefore;
                trace.Assert(stageState&&binding,"ordered move step retains exact old/new owner and slot state / "+name+";step="+(index+1)+
                    ";installedFirst="+expectedInstalled+";oldStorage="+OrderSafe(oldAfter?.storageId));
                trace.Assert(conditionExact&&fluidsExact,"ordered move step preserves both physical conditions and car/engine fluids / "+name+";step="+(index+1));
                trace.Add("ORDER_MOVE_STEP|"+name+"|step="+(index+1)+";asset="+OrderSafe(asset)+";old="+
                    (oldAfter==null?"<missing>":oldAfter.location+"/"+oldAfter.ownerId+"/"+oldAfter.slot+"/"+oldAfter.storageId)+
                    ";new="+(newAfter==null?"<missing>":newAfter.location+"/"+newAfter.ownerId+"/"+newAfter.slot+"/"+newAfter.storageId)+
                    ";ready="+resolution.Drivable+";errors="+OrderSafe(string.Join(";",resolution.errors))+";inspectionSpec="+
                    OrderSafe(inspection.build.spec.id)+";changedPaths="+changed.Count);
            }
            var finalCar=state.cars.Find(item=>item.instanceId==carId);
            var finalOld=state.parts.Find(item=>item.instanceId==oldId);
            var finalNew=state.parts.Find(item=>item.instanceId==newId);
            var finalResolution=MechanicsOwnership.Resolve(state,finalCar,state.catalog);
            var finalInspection=InspectionBuild.Capture(state,finalCar);
            var finalDiff=NormalizationDiff(baseline,FlattenNormalization(state));
            WriteNormalizationDiff(trace,name+"-final",finalDiff,new List<string>());
            string finalTune=TypedCanonical(finalCar.tune,new Dictionary<string,string>());
            bool finalState=finalOld!=null&&finalOld.location==PartLocation.Inventory&&finalOld.storageId==SharedInventory.Storage&&
                string.IsNullOrEmpty(finalOld.ownerId)&&string.IsNullOrEmpty(finalOld.slot)&&finalNew!=null&&
                finalNew.location==PartLocation.Assembly&&finalNew.ownerId==finalCar.engineAssemblyId&&finalNew.slot==category.ToString()&&
                finalCar.installed.slots.Any(row=>row.slot==category.ToString()&&row.partInstanceId==newId)&&
                !finalCar.installed.slots.Any(row=>row.partInstanceId==oldId);
            bool finalConditions=finalOld!=null&&finalNew!=null&&
                TypedCanonical(finalOld.condition,new Dictionary<string,string>())==originalCondition&&
                TypedCanonical(finalNew.condition,new Dictionary<string,string>())==replacementCondition;
            bool finalFluids=TypedCanonical(finalCar.fluids,new Dictionary<string,string>())==carFluidsBefore&&
                TypedCanonical(state.engines.Find(item=>item.instanceId==finalCar.engineAssemblyId).fluids,new Dictionary<string,string>())==engineFluidsBefore;
            trace.Assert(finalState&&finalConditions&&finalFluids,"completed move chain preserves displaced identity/condition, replacement binding, fluids, and exact owners / "+name);
            trace.Assert(finalResolution.Drivable==finalInspection.build.Drivable&&
                finalResolution.errors.SequenceEqual(finalInspection.build.errors),
                "final authoritative Resolve and InspectionBuild agree on result/error order / "+name+
                ";resolve="+OrderSafe(string.Join(";",finalResolution.errors))+";inspection="+OrderSafe(string.Join(";",finalInspection.build.errors)));
            trace.Add("ORDER_MOVE_FINAL|"+name+"|car="+carId+";old="+oldId+";new="+newId+";oldStorage="+
                OrderSafe(finalOld.storageId)+";newOwner="+OrderSafe(finalNew.ownerId)+";newSlot="+finalNew.slot+
                ";tuneChanged="+(tuneBefore!=finalTune)+";carFluidsExact="+(TypedCanonical(finalCar.fluids,new Dictionary<string,string>())==carFluidsBefore)+
                ";engineFluidsExact="+finalFluids+";resolveReady="+finalResolution.Drivable+";resolveErrors="+
                OrderSafe(string.Join(";",finalResolution.errors))+";inspectionSpec="+OrderSafe(finalInspection.build.spec.id)+
                ";fullGraphDiffPaths="+finalDiff.Count);
            return finalState&&finalConditions&&finalFluids&&finalResolution.Drivable==finalInspection.build.Drivable;
        }

        static string OrderIdentitySummary(RuntimeEntropy.IdentityObservation observation)
        {
            if(observation==null)return "<missing>";
            return "closed="+observation.Closed+";overflow="+observation.Overflowed+";count="+observation.Count+
                ";scopes="+string.Join(",",Enumerable.Range(0,Math.Min(observation.Count,8)).Select(index=>observation.EventAt(index).Scope))+
                ";omitted="+Math.Max(0,observation.Count-8);
        }

        static string OrderSafe(string value)
        {
            if(value==null)return "<null>";
            value=value.Replace('\r',' ').Replace('\n',' ');
            return value.Length<=360?value:value.Substring(0,357)+"...";
        }
    }
}
#endif

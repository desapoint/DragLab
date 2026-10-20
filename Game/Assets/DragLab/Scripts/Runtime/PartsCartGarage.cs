using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DragLab.Domain;
using DragLab.Domain.Mechanics;

namespace DragLab.Runtime
{
    public sealed partial class PrototypeGame
    {
        string partsPage="Shop",cartOwner,inventoryStorage=SharedInventory.Garage;
        readonly List<PartCartLine> cart=new List<PartCartLine>();
        readonly Dictionary<string,string> unitOffers=new Dictionary<string,string>();
        readonly Dictionary<string,PartInstance> offerTemplates=new Dictionary<string,PartInstance>();
        readonly Dictionary<PartCartLine,int> cartSalePrices=new Dictionary<PartCartLine,int>();
        string offerTemplateKey,cartPriceKey,cartError;
        int cartTotal,moveDestination;
        bool previewCart,inventoryAllCategories,inventoryCompatibleOnly;
        internal string PartsPage=>partsPage;
        internal int CartCount=>cart.Sum(l=>l.quantity);
        internal IReadOnlyList<PartCartLine> CartLines=>cart;
        internal void SelectPartsPage(string page){partsPage=page;scroll=Vector2.zero;partRects.Clear();PreviewPart();previewCart=cart.Count>0;statsKey=null;}
        void EnsureCartOwner(){if(cartOwner==Build.instanceId)return;cartOwner=Build.instanceId;cart.Clear();var saved=profile.inventory.cart;if(saved!=null&&saved.carId==cartOwner)cart.AddRange(saved.lines.Select(l=>l.Copy()));unitOffers.Clear();inventoryStorage=SharedInventory.Garage;moveDestination=0;previewCart=false;statsKey=null;cartPriceKey=null;}
        PartInstance OfferTemplate(PartDefinition d){string key=Build.instanceId+"/"+profile.revision;if(offerTemplateKey!=key){offerTemplates.Clear();offerTemplateKey=key;}if(!offerTemplates.TryGetValue(d.id,out var pack)){pack=InventoryUnits.Template(profile,Build,d);offerTemplates[d.id]=pack;}return pack;}
        int OfferCount(PartDefinition d){var pack=OfferTemplate(d);return pack.condition.elements.Length>0?pack.condition.elements.Length:d.category==PartCategory.Tires||d.category==PartCategory.Wheels||d.category==PartCategory.Brakes?4:1;}
        internal void AddToCart(string id,bool install=false)
        {
            EnsureCartOwner();var d=profile.catalog.Find(id);
            if(d==null||install&&!profile.catalog.Fits(Selected.spec,d)||profile.reputation<d.reputationRequired){notice="Offer incompatible or locked by reputation.";return;}
            string role=unitOffers.TryGetValue(id,out var chosen)?chosen:null;var action=role==null?CartAction.NewSet:CartAction.NewUnits;
            var line=cart.Find(l=>l.definitionId==id&&l.action==action&&(action==CartAction.NewSet||l.role==role));
            if(line==null){if(cart.Count>=100){notice="Cart holds at most 100 distinct lines.";return;}line=new PartCartLine{definitionId=id,action=action,role=role};cart.Add(line);}else line.quantity=Math.Min(99,line.quantity+1);
            if(install)SetCartInstallation(line,true);CartChanged();
        }
        internal void StageInventory(PartCartLine line){EnsureCartOwner();if(cart.Count>=100&&!cart.Any(l=>l.assetId==line.assetId)){notice="Cart holds at most 100 lines.";return;}cart.RemoveAll(l=>!string.IsNullOrEmpty(line.assetId)&&l.assetId==line.assetId);cart.Add(line);CartChanged();}
        void PersistNativeCartReservation(){if(cartOwner!=null)CartReservations.Capture(profile,cartOwner,cart);}
        void CartChanged(){if(profile.inventory.cart!=null&&cartOwner!=null)PersistNativeCartReservation();previewCart=true;previewDefinitionId=previewInstanceId=null;previewRemoval=null;statsKey=null;cartPriceKey=null;}
        bool IsUnit(PartCartLine line)=>line.action==CartAction.NewUnits||line.action==CartAction.BuyUsed&&profile.inventory.dealer.Any(s=>s.Id==line.assetId&&s.IsUnit);
        void SetCartInstallation(PartCartLine line,bool value)
        {
            if(value&&!IsUnit(line)){var slot=profile.catalog.Find(line.definitionId).category;foreach(var other in cart)if(other!=line&&other.definitionId!=null&&profile.catalog.Find(other.definitionId).category==slot)other.install=false;}
            if(value&&IsUnit(line)&&line.positions.Count==0){var positions=InventoryUnits.Positions(profile,Build,line.definitionId,line.role);if(positions.Length>0)line.positions.Add(positions[0]);}
            line.install=value;CartChanged();
        }
        void RefreshCartPrice()
        {
            string key=profile.revision+"/"+JsonUtility.ToJson(new CartInspectionKey{lines=cart})+"/"+JsonUtility.ToJson(PricingPolicy);
            if(cartPriceKey==key)return;cartPriceKey=key;cartError=null;cartTotal=0;cartSalePrices.Clear();if(cart.Count==0)return;
            try{cartTotal=PartCart.Total(profile,Build.instanceId,cart,PricingPolicy);foreach(var l in cart.Where(l=>l.action==CartAction.SellPart||l.action==CartAction.SellUnit))cartSalePrices[l]=-InventoryPricing.Sell(profile,l.assetId,l.action==CartAction.SellUnit,PricingPolicy,"wholesale",DragLab.Domain.RuntimeEntropy.UtcNow.Ticks).Total;}catch(Exception e){cartError=e.Message;}
        }
        internal bool CheckoutCartNow()
        {
            try{var quote=PartCart.Quote(profile,Build.instanceId,cart,DragLab.Domain.RuntimeEntropy.UtcNow.Ticks,Costs.quoteSeconds,PricingPolicy);if(!CommitParts(quote.revision,p=>PartCart.Checkout(p,quote,DragLab.Domain.RuntimeEntropy.UtcNow.Ticks,PricingPolicy)))return false;cart.Clear();previewCart=false;statsKey=null;cartPriceKey=null;notice="Complete basket saved once: purchases, sales and moves. Original condition and identities retained.";return true;}catch(Exception e){notice=e.Message;return false;}
        }
        string CartKey(PartCartLine line)=>line.action==CartAction.NewSet?line.definitionId:line.action+"/"+(line.assetId??line.definitionId)+"/"+line.role;
        void CartGUI()
        {
            RefreshCartPrice();GUILayout.BeginVertical(GUI.skin.box);GUILayout.Label("TRANSACTION CART / "+CartCount+" entries / preview only",stat);
            foreach(var line in cart){
                var captured=line;string key=CartKey(line);var d=profile.catalog.Find(line.definitionId);string detail=d==null?line.assetId:d.name;
                if(line.action==CartAction.NewSet)detail+=" / complete set of "+OfferCount(d)+" units";
                if(IsUnit(line))detail+=" / individual "+line.role;
                if(line.action==CartAction.Move)detail+=" → "+line.destination+" / "+line.destinationId;
                long price=NativeCartLinePrice(line);
                detail+=" / "+(price<0?"credit " + DisplayUnits.Money((-price)):"charge " + DisplayUnits.Money(price));
                GUILayout.Label(line.action+" / "+detail+" × "+line.quantity,partProse);GUILayout.BeginHorizontal();
                if(line.action==CartAction.NewSet||line.action==CartAction.NewUnits||line.action==CartAction.BuyUsed){bool install=GUILayout.Toggle(line.install,IsUnit(line)?"Install selected units":"Install one complete set");if(install!=line.install)SetCartInstallation(line,install);}
                if(line.action==CartAction.NewSet||line.action==CartAction.NewUnits){if(PartButton("cart-minus:"+key,"−")&&line.quantity>1){line.quantity--;if(line.positions.Count>line.quantity)line.positions.RemoveRange(line.quantity,line.positions.Count-line.quantity);CartChanged();}if(PartButton("cart-plus:"+key,"+")&&line.quantity<99){line.quantity++;CartChanged();}}
                if(PartButton("cart-remove:"+key,"Remove"))pendingAction=()=>{cart.Remove(captured);CartChanged();};GUILayout.EndHorizontal();
                if(IsUnit(line)&&line.install){foreach(var position in InventoryUnits.Positions(profile,Build,line.definitionId,line.role)){bool was=line.positions.Contains(position),chosen=GUILayout.Toggle(was,InventoryUnits.PositionLabel(profile,Build,line.definitionId,position));if(Event.current.type==EventType.Repaint)partRects["cart-position:"+position]=RuntimeGuiRect.ToClient(GUILayoutUtility.GetLastRect());if(chosen!=was){if(chosen)line.positions.Add(position);else line.positions.Remove(position);CartChanged();}}GUILayout.Label(line.positions.Count+" installed / "+Math.Max(0,line.quantity-line.positions.Count)+" loose. Match the fitted specification; full sets change specification.",partProse);}
            }
            if(cart.Count>0){
                GUILayout.Label(cartError??("Net "+(cartTotal<0?"receive " + DisplayUnits.Money((-(long)cartTotal)):"pay " + DisplayUnits.Money(cartTotal))+" / cash after " + DisplayUnits.Money(((long)profile.cash-cartTotal))));
                GUILayout.Label("Combined stats include checked installations and moves affecting this car. Sales use the wholesaler's condition-based quote. Confirm saves the whole basket once.",partProse);
                GUILayout.BeginHorizontal();if(PartButton("cart-preview","Preview combined changes"))CartChanged();bool enabled=GUI.enabled;GUI.enabled=enabled&&cartError==null&&profile.cash>=cartTotal;if(PartButton("cart-checkout","Confirm all / buy, sell, move"))pendingAction=()=>CheckoutCartNow();GUI.enabled=enabled;if(PartButton("cart-clear","Clear cart"))pendingAction=()=>{cart.Clear();CartChanged();};GUILayout.EndHorizontal();
            }else GUILayout.Label("Add complete sets (default), individual units, retained items or dealer stock. Nothing changes until confirmation.",partProse);
            GUILayout.EndVertical();
        }
        void OfferModeGUI(PartDefinition d)
        {
            var pack=OfferTemplate(d);int count=OfferCount(d);GUILayout.Label("Price buys "+(count>1?"a complete set of "+count+" units":"one component")+" / " + DisplayUnits.Money(PartsMarket.Quote(profile,d,count).cost));
            var roles=d.category==PartCategory.Tires?new[]{"tire"}:pack.condition.elements.Select(e=>e.role).Distinct().ToArray();if(roles.Length==0)return;
            GUILayout.BeginHorizontal();if(PartButton("offer-set:"+d.id,unitOffers.ContainsKey(d.id)?"Complete set / default":"[Complete set / "+count+" units]"))unitOffers.Remove(d.id);
            foreach(var role in roles){bool selected=unitOffers.TryGetValue(d.id,out var value)&&value==role;if(PartButton("offer-unit:"+d.id+":"+role,(selected?"[Individual ":"Individual ")+role+" / " + DisplayUnits.Money(PartsMarket.Quote(profile,d,count,true).cost)+(selected?"]":"")))unitOffers[d.id]=role;}GUILayout.EndHorizontal();
        }
        InventoryContainer[] MoveTargets()=>SharedInventory.Containers(profile).Where(c=>c.kind==InventoryKind.Storage||c.kind==InventoryKind.Car||c.kind==InventoryKind.Engine&&profile.engines.Any(e=>e.instanceId==c.id&&string.IsNullOrEmpty(e.parentVehicleId))).ToArray();
        void InventoryDestinationGUI(){GUILayout.BeginHorizontal();foreach(var s in profile.inventory.storage)if(PartButton("inventory-storage:"+s.id,(inventoryStorage==s.id?"[":"")+s.name+(inventoryStorage==s.id?"]":"")))inventoryStorage=s.id;GUILayout.EndHorizontal();var choices=MoveTargets();moveDestination=Math.Min(moveDestination,choices.Length-1);GUILayout.Label("Move / install destination");for(int i=0;i<choices.Length;i++){if(i%2==0)GUILayout.BeginHorizontal();if(PartButton("move-target:"+choices[i].kind+":"+choices[i].id,(moveDestination==i?"[":"")+choices[i].name+(moveDestination==i?"]":"")))moveDestination=i;if(i%2==1||i==choices.Length-1)GUILayout.EndHorizontal();}}
        void StageMoveButton(string id,string definitionId){if(!PartButton("stage-move:"+id,"Add move / install to cart"))return;var choices=MoveTargets();var target=choices[Math.Min(moveDestination,choices.Length-1)];StageInventory(new PartCartLine{action=CartAction.Move,assetId=id,definitionId=definitionId,destination=target.kind,destinationId=target.id});}
        void DealerGUI()
        {
            GUILayout.Label("DEALER USED STOCK / retained condition / buyback adds "+PricingPolicy.dealerResaleMarkup.ToString("P0")+" margin + " + DisplayUnits.Money(PricingPolicy.dealerHandlingFee),stat);
            foreach(var stock in profile.inventory.dealer.Where(s=>(s.IsPart||s.IsUnit)&&profile.catalog.Find(s.DefinitionId)?.category==partCategory)){var d=profile.catalog.Find(stock.DefinitionId);GUILayout.Label(d.name+" / "+(stock.IsPart?Condition(stock.part):stock.unit.element.role+" / "+stock.unit.element.health.ToString("P0"))+" / " + DisplayUnits.Money(InventoryPricing.BuyUsed(stock,PricingPolicy)),partProse);bool enabled=GUI.enabled;GUI.enabled=enabled&&profile.catalog.Fits(Selected.spec,d)&&profile.reputation>=d.reputationRequired;
                if(stock.IsPart&&PartButton("dealer-preview:"+stock.Id,"Inspect used hardware / dyno"))PreviewPart(instanceId:stock.Id);
                if(PartButton("dealer-cart:"+stock.Id,"Add used item to cart"))StageInventory(new PartCartLine{action=CartAction.BuyUsed,assetId=stock.Id,definitionId=d.id,role=stock.IsUnit?stock.unit.element.role:null});GUI.enabled=enabled;}
        }
        void InventoryUnitsGUI()
        {
            foreach(var entry in SharedInventory.Entries(profile,InventoryKind.Storage,inventoryStorage).Where(e=>e.unit!=null&&(inventoryAllCategories||profile.catalog.Find(e.definitionId).category==partCategory)&&(!inventoryCompatibleOnly||profile.catalog.Fits(Selected.spec,profile.catalog.Find(e.definitionId))))) {
                var unit=entry.unit;var d=profile.catalog.Find(unit.definitionId);GUILayout.Label(d.name+" / individual "+unit.element.role+" / "+unit.element.health.ToString("P0")+" / "+unit.element.fault+" / "+unit.element.id,partProse);
                StageMoveButton(entry.id,d.id);
                if(PartButton("cart-sell-unit:"+entry.id,"Add individual sale to cart"))StageInventory(new PartCartLine{action=CartAction.SellUnit,assetId=entry.id,definitionId=d.id});
                foreach(var position in InventoryUnits.Positions(profile,Build,d.id,unit.element.role)){string target=position,id=entry.id;if(PartButton("reuse-unit:"+id+":"+position,"Reuse in "+InventoryUnits.PositionLabel(profile,Build,d.id,position)))QueuePartAction(p=>InventoryUnits.Install(p,Build.instanceId,id,target));}
            }
            foreach(var entry in SharedInventory.Entries(profile,InventoryKind.Storage,inventoryStorage).Where(e=>e.engine!=null)){GUILayout.Label(entry.label+" / "+entry.engine.slots.Count+" attached component sets / "+entry.id,partProse);StageMoveButton(entry.id,null);if(PartButton("inspect-engine:"+entry.id,"Inspect engine tree / assembly valuation"))pendingAction=OpenAssemblies;}
        }
    }
}

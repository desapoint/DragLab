using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    internal sealed partial class NativeMenuShell
    {
        bool cartPageOpen;
        string cartPageKey,cartPageOwner;
        PlayerProfile cartPageProfile;
        VisualElement cartPage,cartRows,cartPreview;
        ScrollView cartScroll,cartPreviewScroll;
        Label cartError,cartEmpty;
        Button cartCheckout,cartClear,cartWarnings;
        SetupWarning[] cartCurrentWarnings=Array.Empty<SetupWarning>(),cartPlannedWarnings;
        string cartWarningUnavailable;
        readonly Dictionary<string,Dictionary<string,bool>> cartStatGroups=new Dictionary<string,Dictionary<string,bool>>();
        readonly Dictionary<string,bool> cartStatDifferences=new Dictionary<string,bool>();
        readonly List<RacingSurface> cartPageSurfaces=new List<RacingSurface>();
        internal bool CartPageOpen=>cartPageOpen;
        internal int CartPreviewBuildCount {get;private set;}
        internal static string NativeCartRowKey(PartCartLine line)=>line.action+"/"+(line.assetId??line.definitionId)+"/"+line.role;
        void CartSurface(VisualElement owner,RacingSurface.Kind kind){AddSurface(owner,kind);cartPageSurfaces.Add(owner.Q<RacingSurface>());}
        Button CartButton(VisualElement parent,string id,string text,Action action)
        {
            var b=new Button(action){name=id,text=text,focusable=false,tabIndex=-1};b.AddToClassList("action-button");parent.Add(b);CartSurface(b,RacingSurface.Kind.SecondaryAction);return b;
        }
        void InitializeNativeCart()
        {
            cartPage=new VisualElement{name="cartPage"};cartPage.AddToClassList("cart-page");cartPage.AddToClassList("hidden");root.Q(className:"workspace").Add(cartPage);
            var heading=new VisualElement();heading.AddToClassList("car-details-heading");cartPage.Add(heading);
            var back=new Button(BackFromNativeCart){name="cartBack",focusable=false,tabIndex=-1};back.AddToClassList("action-button");back.AddToClassList("car-back-button");heading.Add(back);AddSurface(back,RacingSurface.Kind.SecondaryAction);back.Add(new FilledMenuIcon("Back"));back.tooltip="Back to parts";
            var title=new Label("Cart");title.AddToClassList("car-detail-title");heading.Add(title);
            cartWarnings=WarningButton(heading,"cartWarnings",cartCurrentWarnings,()=>OpenCombinedWarnings(cartCurrentWarnings,cartPlannedWarnings,true,"Warnings",cartWarningUnavailable),(element,kind)=>AddSurface(element,kind));
            cartWarnings.AddToClassList("warning-header-button");
            var columns=new VisualElement{name="cartColumns"};columns.AddToClassList("cart-columns");cartPage.Add(columns);
            var list=new VisualElement();list.AddToClassList("cart-list-frame");columns.Add(list);AddSurface(list,RacingSurface.Kind.Panel);
            cartScroll=new ScrollView{name="cartScroll",horizontalScrollerVisibility=ScrollerVisibility.Hidden};cartScroll.AddToClassList("cart-list-scroll");list.Add(cartScroll);
            cartRows=new VisualElement{name="cartRows"};cartScroll.Add(cartRows);cartEmpty=new Label("Cart is empty");cartEmpty.AddToClassList("car-empty");cartScroll.Add(cartEmpty);
            var right=new VisualElement();right.AddToClassList("cart-preview-frame");columns.Add(right);AddSurface(right,RacingSurface.Kind.Panel);
            cartPreviewScroll=new ScrollView{name="cartPreviewScroll",horizontalScrollerVisibility=ScrollerVisibility.Hidden};cartPreviewScroll.AddToClassList("cart-preview-scroll");right.Add(cartPreviewScroll);cartPreview=new VisualElement();cartPreviewScroll.Add(cartPreview);
            var footer=new VisualElement{name="cartFooter"};footer.AddToClassList("cart-footer");cartPage.Add(footer);
            var totals=new VisualElement{name="cartTotals"};totals.AddToClassList("cart-totals");footer.Add(totals);
            foreach(string id in new[]{"Purchases","Credits","Total","Balance"}){var cell=new VisualElement();cell.AddToClassList("cart-total-cell");totals.Add(cell);cell.Add(new Label(id=="Balance"?"Balance after":id));var value=new Label{name="cart"+id};value.AddToClassList("cart-total-value");cell.Add(value);}
            cartError=new Label{name="cartError",enableRichText=false};cartError.AddToClassList("car-critical");footer.Add(cartError);
            var actions=new VisualElement{name="cartActions"};actions.AddToClassList("car-details-actions");footer.Add(actions);
            cartClear=new Button(()=>OpenDialog("Clear cart?",null,new[]{new DialogAction("Cancel",CloseDialog),new DialogAction("Clear",()=>{game.ClearNativeCart();CloseDialog();partsStateKey=null;cartPageKey=null;RefreshNativeCart("Parts");},true)})){name="cartClear",text="Clear",focusable=false,tabIndex=-1};cartClear.AddToClassList("action-button");actions.Add(cartClear);AddSurface(cartClear,RacingSurface.Kind.SecondaryAction);
            cartCheckout=new Button(ConfirmNativeCart){name="cartCheckout",text="Checkout",focusable=false,tabIndex=-1};cartCheckout.AddToClassList("action-button");cartCheckout.AddToClassList("primary-action");actions.Add(cartCheckout);AddSurface(cartCheckout,RacingSurface.Kind.Action);
        }
        void OpenNativeCart()
        {
            if(ModalOpen)CloseDialog();game.PrepareNativeCart();cartPageOwner=game.Profile.SelectedCar?.instanceId;cartPageOpen=true;RefreshNativeCart("Parts");partsPage.AddToClassList("hidden");
        }
        void BackFromNativeCart()
        {
            cartPageOpen=false;cartPage.AddToClassList("hidden");RefreshParts("Parts");
        }
        void EditCartPurchase(PartCartLine line)
        {
            var d=game.Profile.catalog.Find(line.definitionId);if(d==null){cartError.text="Offer unavailable.";return;}
            var stock=line.action==CartAction.BuyUsed?game.Profile.inventory.dealer.Find(s=>s.Id==line.assetId):null;
            if(line.action==CartAction.BuyUsed&&stock==null){cartError.text="Used stock unavailable.";return;}
            OpenPartPurchase(new NativePartEntry{Id=stock==null?"new:"+d.id:"used:"+stock.Id,Source=stock==null?"New":"Used",Definition=d,AssetId=stock?.Id,Part=stock?.part,Unit=stock?.unit,Price=stock==null?InventoryPricing.NewSet(d):game.NativeUsedPrice(stock)},line);
        }
        void RefreshNativeCart(string route)
        {
            if(route!="Parts"&&route!="Settings")cartPageOpen=false;
            if(cartPageOpen&&cartPageOwner!=game.Profile.SelectedCar?.instanceId)cartPageOpen=false;
            cartPage.EnableInClassList("hidden",route!="Parts"||!cartPageOpen);if(route!="Parts"||!cartPageOpen)return;
            game.PrepareNativeCart();var p=game.Profile;var car=p.SelectedCar;
            string key=p.revision+"/"+car?.instanceId+"/"+p.cash+"/"+JsonUtility.ToJson(SharedUnitSettings.Current)+"/"+JsonUtility.ToJson(new PartCart.Lines{entries=game.CartLines.Select(l=>l.Copy()).ToList()});
            if(cartPageProfile==p&&cartPageKey==key)return;cartPageProfile=p;cartPageKey=key;
            var offset=cartScroll.scrollOffset;var previewOffset=cartPreviewScroll.scrollOffset;
            foreach(var s in cartPageSurfaces)surfaces.Remove(s);cartPageSurfaces.Clear();cartRows.Clear();cartPreview.Clear();
            string problem=null;int total=0;long purchases=0,credits=0;
            try{total=game.NativeCartTotal();}catch(Exception e){problem=e.Message;}
            foreach(var line in game.CartLines.ToArray()){
                string id=NativeCartRowKey(line);var d=p.catalog.Find(line.definitionId);int? amount=null;try{amount=game.NativeCartLinePrice(line);if(amount>=0)purchases+=amount.Value;else credits-=amount.Value;}catch(Exception e){problem=problem??e.Message;}
                var row=new VisualElement{name="cartRow:"+id};row.AddToClassList("cart-row");cartRows.Add(row);
                var top=new VisualElement();top.AddToClassList("cart-row-heading");row.Add(top);var name=new Label(d?.name??line.assetId){enableRichText=false};name.AddToClassList("cart-row-name");top.Add(name);
                var price=new Label(amount.HasValue?DisplayUnits.Money(amount.Value):"Unavailable"){name="cartPrice:"+id};price.AddToClassList("cart-row-price");top.Add(price);
                bool buying=line.action==CartAction.NewSet||line.action==CartAction.NewUnits||line.action==CartAction.BuyUsed,fresh=line.action==CartAction.NewSet||line.action==CartAction.NewUnits;
                var stock=line.action==CartAction.BuyUsed?p.inventory.dealer.Find(s=>s.Id==line.assetId):null;bool individual=line.action==CartAction.NewUnits||stock?.unit!=null;
                var summary=new Label(line.action==CartAction.Move?"Move → "+InventoryLocation(line.destinationId):line.action==CartAction.SellPart||line.action==CartAction.SellUnit?"Sale":(individual?"Individual · "+line.role:d!=null&&InventoryUnits.Count(p,car,d)>1?"Complete set · "+InventoryUnits.Count(p,car,d)+" units":"Part")+" · "+(line.install?"Install":"Store")){enableRichText=false};summary.AddToClassList("cart-row-summary");row.Add(summary);
                if(line.preparationOwner!=null){var prepared=new Label(line.preparedBlock?"Prepared block installation":"Block companion · "+(line.action==CartAction.Move?"Reuse":"Purchase"));prepared.AddToClassList("cart-row-summary");row.Add(prepared);}
                if(buying&&amount.HasValue){var each=new Label(DisplayUnits.Money(amount.Value/line.quantity)+" each");each.AddToClassList("cart-row-summary");row.Add(each);}
                if(individual&&line.install){var target=new Label(string.Join(" · ",line.positions.Select(pos=>InventoryUnits.PositionLabel(p,car,line.definitionId,pos)))+" · "+(line.quantity-line.positions.Count)+" in storage"){name="cartTargets:"+id,enableRichText=false};target.AddToClassList("cart-row-summary");row.Add(target);}
                else if(buying&&line.install&&line.quantity>1){var extras=new Label((line.quantity-1)+" sets in storage");extras.AddToClassList("cart-row-summary");row.Add(extras);}
                var controls=new VisualElement();controls.AddToClassList("cart-row-controls");row.Add(controls);
                if(buying){var number=new FloatField{name="cartQuantity:"+id,isDelayed=true,formatString="0"};number.AddToClassList("setting-number");number.SetValueWithoutNotify(line.quantity);number.SetEnabled(fresh);controls.Add(number);
                    number.RegisterValueChangedCallback(e=>{float value=e.newValue;if(float.IsNaN(value)||float.IsInfinity(value)||value!=Mathf.Floor(value)||value<1||value>99){number.SetValueWithoutNotify(line.quantity);cartError.text="Enter a whole quantity from 1 to 99.";return;}
                        if(!game.EditNativeCartPurchase(line,JsonUtility.ToJson(line),car.instanceId,individual,line.role,(int)value,line.install,line.positions)){number.SetValueWithoutNotify(line.quantity);cartError.text=game.NativeNotice;return;}partsStateKey=null;cartPageKey=null;});
                    CartButton(controls,"cartEdit:"+id,"Edit",()=>EditCartPurchase(line)).Insert(1,new FilledMenuIcon("Pencil"));}
                CartButton(controls,"cartRemove:"+id,"Remove",()=>{game.RemoveNativeCartLine(line);partsStateKey=null;cartPageKey=null;});
            }
            cartEmpty.EnableInClassList("hidden",game.CartCount>0);cartClear.SetEnabled(game.CartCount>0);
            root.Q<Label>("cartPurchases").text=DisplayUnits.Money(purchases);root.Q<Label>("cartCredits").text=DisplayUnits.Money(credits);
            root.Q<Label>("cartTotal").text=problem==null?DisplayUnits.Money(total):"Unavailable";root.Q<Label>("cartBalance").text=problem==null?DisplayUnits.Money((long)p.cash-total):"—";
            cartError.text=problem??(total>p.cash?"Insufficient cash.":"");cartCheckout.SetEnabled(problem==null&&game.CartCount>0&&total<=p.cash);
            cartWarnings.SetEnabled(car!=null);
            if(car==null){cartCurrentWarnings=Array.Empty<SetupWarning>();cartPlannedWarnings=null;cartWarningUnavailable="No selected car";SetWarningButton(cartWarnings,cartCurrentWarnings);cartWarnings.tooltip=cartWarningUnavailable;}
            if(car!=null){var model=new NativePartBrowser(p,game.CartLines,false,"Cart",game.NativeUsedPrice,game.NativeSalePrice,partPreviewCache);CartPreviewBuildCount++;
                var currentWarnings=model.Current?.warnings.ToArray()??CurrentOwnedWarnings(car);var nextWarnings=model.Plan?.warnings.ToArray()??(game.CartCount==0?currentWarnings:null);
                cartCurrentWarnings=currentWarnings;cartPlannedWarnings=nextWarnings;cartWarningUnavailable=nextWarnings==null?"Planned inspection unavailable · "+(model.PlanError??"Check the installation plan."):null;SetWarningButton(cartWarnings,nextWarnings??currentWarnings);
                var combined=NativeWarningOverview.Combine(currentWarnings,nextWarnings,nextWarnings!=null);cartWarnings.Q<Label>().text="Warnings · "+combined.Length;
                cartWarnings.tooltip=combined.Length==0?"No warnings reported":string.Join(" · ",combined.GroupBy(w=>w.State).Select(g=>g.Count()+" "+g.Key.ToLowerInvariant()));
                if(model.Current!=null){var planned=model.Plan??(game.CartCount==0?model.Current:null);
                    if(planned!=null){cartPreview.Add(new NativeDynoPanel(new[]{new NativeDynoSeries("Current","Current",model.Current,NativeDynoStyle.Primary),new NativeDynoSeries("Plan","Planned",planned,NativeDynoStyle.Alternative),new NativeDynoSeries("NoWear","No wear",OwnedHealthy(car),NativeDynoStyle.Reference)},SharedUnitSettings.Current.torque,"cartDyno"));
                        NativeStatsPanel stats=null;stats=new NativeStatsPanel(model.Current,SharedUnitSettings.Current,new[]{NativeStatCatalog.TuningSummary()},new Dictionary<string,bool>(),d=>OpenDialog(d.Label,d.InfoExplanation(stats.Current,stats.Proposed),new[]{new DialogAction("Close",CloseDialog)}),CartSurface,planned){name="cartQuickStats"};stats.AddToClassList("part-quick-stats");stats.Q<Button>("statsDifferencesOnly").name="cartQuickDifferences";cartPreview.Add(stats);
                        if(!cartStatGroups.TryGetValue(car.instanceId,out var collapsed)){collapsed=new Dictionary<string,bool>();cartStatGroups[car.instanceId]=collapsed;}
                        var groups=NativeStatCatalog.Groups(model.Current,planned).Select(g=>new NativeStatGroup(g.Id,g.Title,g.Definitions.ToArray()){InitiallyCollapsed=true,Children=g.Children});
                        NativeStatsPanel details=null;details=new NativeStatsPanel(model.Current,SharedUnitSettings.Current,groups,collapsed,d=>OpenDialog(d.Label,d.InfoExplanation(details.Current,details.Proposed),new[]{new DialogAction("Close",CloseDialog)}),CartSurface,planned,cartStatDifferences.TryGetValue(car.instanceId,out bool only)&&only){name="cartStats"};
                        details.AddToClassList("cart-detailed-stats");details.Q<Button>("statsDifferencesOnly").name="cartDifferencesOnly";
                        details.DifferencesOnlyChanged=value=>cartStatDifferences[car.instanceId]=value;details.GroupOpened=_=>ExcludeTabTraversal();cartPreview.Add(details);
                    }else cartPreview.Add(new Label(model.PlanError??"Preview unavailable."){enableRichText=false});
                }else cartPreview.Add(new Label("Engine preview unavailable."));
            }
            ExcludeTabTraversal();cartScroll.schedule.Execute(()=>cartScroll.scrollOffset=offset);cartPreviewScroll.schedule.Execute(()=>cartPreviewScroll.scrollOffset=previewOffset);
        }
    }
}

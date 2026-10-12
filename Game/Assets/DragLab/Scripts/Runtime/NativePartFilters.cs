using System;
using System.Globalization;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.UIElements;
using DragLab.Domain;
using DragLab.Domain.Mechanics;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    internal sealed partial class NativeMenuShell
    {
        readonly List<SwitchSurface> partCartSwitches=new List<SwitchSurface>();
        void ClearPartCartSwitches(){foreach(var s in partCartSwitches)switchSurfaces.Remove(s);partCartSwitches.Clear();}
        void OpenPartFilters(NativePartQuery initial=null)
        {
            var query=(initial??PartsQuery).Copy();bool planned=partsPlanned;var error=new Label{name="partFilterError"};error.AddToClassList("car-critical");var fields=new TextField[7];
            bool Validate(){for(int i=0;i<fields.Length;i++){
                string value=fields[i]?.value??"";if(value.Trim()==""){query.Minimum[i]=null;continue;}
                if(!double.TryParse(value,NumberStyles.Float,CultureInfo.CurrentCulture,out double number)||double.IsNaN(number)||double.IsInfinity(number)||number<0){error.text="Enter a finite, non-negative minimum change.";root.Q<Button>("dialogApply")?.SetEnabled(false);return false;}
                query.Minimum[i]=i==1||i==4?DisplayUnits.ToNewtonMetres(number,SharedUnitSettings.Current.torque):number;
            }error.text="";root.Q<Button>("dialogApply")?.SetEnabled(true);return true;}
            OpenDialog("Filter parts",null,new[]{new DialogAction("Reset",()=>OpenPartFilters(new NativePartQuery())),new DialogAction("Cancel",CloseDialog),new DialogAction("Apply",()=>{if(!Validate())return;partQueries[game.PartsPage+"/"+partsSection]=query;if(partsPlanned!=planned){partsPlanned=planned;partsStateKey=null;}CloseDialog();ApplyPartQuery();})},populate:content=>{
                QueryChoices(content,"partReference","Compatibility / performance reference",new[]{"Current build","Planned build"},()=>planned?"Planned build":"Current build",v=>planned=v=="Planned build");
                QueryChoices(content,"partCompatibility","Compatibility",new[]{"All","Compatible","Requires supporting changes","Incompatible","Unavailable"},()=>query.Compatibility,v=>query.Compatibility=v,2);
                QueryChoices(content,"partAvailability","Availability",new[]{"All","Owned","Affordable","Unaffordable"},()=>query.Availability,v=>query.Availability=v);
                if(game.PartsPage=="Shop")QueryChoices(content,"partCondition","Shop stock",new[]{"All","New","Used"},()=>query.Condition,v=>query.Condition=v);
                QueryChoices(content,"partTuning","Tuning",new[]{"All","Adjustable","Fixed"},()=>query.Tuning,v=>query.Tuning=v);
                var categories=Enum.GetValues(typeof(PartCategory)).Cast<PartCategory>().Where(c=>NativePartBrowser.Section(c)==partsSection).ToArray();
                var block=new VisualElement();block.AddToClassList("owned-query-group");content.Add(block);block.Add(new Label("Subcategories"));
                for(int offset=0;offset<categories.Length;offset+=3){var row=new VisualElement();row.AddToClassList("owned-query-choices");row.AddToClassList("choice-group");block.Add(row);int count=Math.Min(3,categories.Length-offset);
                    for(int j=0;j<count;j++){var category=categories[offset+j];var b=new Button{name="partCategory:"+category,text=PartSemantics.Friendly(category.ToString()),focusable=false,tabIndex=-1};b.AddToClassList("choice-button");b.AddToClassList(j==0?"choice-first":j==count-1?"choice-last":"choice-middle");b.EnableInClassList("selected",query.Categories.Contains(category));b.clicked+=()=>{if(!query.Categories.Add(category))query.Categories.Remove(category);b.EnableInClassList("selected",query.Categories.Contains(category));};row.Add(b);AddSurface(b,j==0?RacingSurface.Kind.ChoiceFirst:j==count-1?RacingSurface.Kind.ChoiceLast:RacingSurface.Kind.ChoiceMiddle);dialogSurfaces.Add(b.Q<RacingSurface>());}
                }
                block.Add(new Label("No selection: all subcategories."));
                for(int i=0;i<7;i++){int index=i;
                    QueryChoices(content,"partDirection"+i,NativePartBrowser.MetricNames[i],new[]{"Any","Increase","Decrease","Unchanged"},()=>query.Direction[index],v=>query.Direction[index]=v);
                    var row=new VisualElement();row.AddToClassList("owned-filter-range");row.Add(new Label("Minimum magnitude"));double scale=i==1||i==4?DisplayUnits.Torque(1,SharedUnitSettings.Current.torque):1;
                    var field=new TextField{name="partMinimum"+i,value=query.Minimum[i].HasValue?(query.Minimum[i]*scale).Value.ToString("0.##"):"",maxLength=16};field.AddToClassList("setting-number");field.RegisterValueChangedCallback(_=>Validate());fields[i]=field;row.Add(field);
                    row.Add(new Label(i==0?"HP":i==1||i==4?SharedUnitSettings.TorqueLabel:i==2?"kg":i==3?"pp":i==5?"RPM":"W/°C"));content.Add(row);
                }
                content.Add(error);
            });Validate();
        }
        void OpenPartSort()
        {
            var query=PartsQuery.Copy();OpenDialog("Sort parts",null,new[]{new DialogAction("Cancel",CloseDialog),new DialogAction("Apply",()=>{PartsQuery.Sort=query.Sort;PartsQuery.Descending=query.Descending;CloseDialog();ApplyPartQuery();})},populate:content=>{
                QueryChoices(content,"partSortBy","Sort by",new[]{"Name","Price"}.Concat(NativePartBrowser.MetricNames).ToArray(),()=>query.Sort,v=>query.Sort=v,2);
                QueryChoices(content,"partSortDirection","Order",new[]{"Ascending","Descending"},()=>query.Descending?"Descending":"Ascending",v=>query.Descending=v=="Descending");
            });
        }
        void OpenNativeCart()
        {
            ClearPartCartSwitches();RefreshParts("Parts");
            string problem=null;int total=0;try{total=game.NativeCartTotal();if(game.CartLines.Any(l=>l.install&&game.NativeBlockPreparationPending(game.Profile.catalog.Find(l.definitionId))))problem="Prepare a complete engine installation before installing this block.";}catch(Exception error){problem=error.Message;}
            var actions=game.CartCount==0?new[]{new DialogAction("Close",CloseDialog)}:new[]{new DialogAction("Clear",()=>{game.ClearNativeCart();partsStateKey=null;OpenNativeCart();}),new DialogAction("Close",CloseDialog),new DialogAction("Checkout",ConfirmNativeCart,true)};
            OpenDialog("Cart",(problem??("Total "+DisplayUnits.Money(total)+" · Cash "+DisplayUnits.Money(game.Profile.cash))),actions,populate:content=>{
                foreach(var line in game.CartLines.ToArray()){
                    var d=game.Profile.catalog.Find(line.definitionId);var block=new VisualElement();block.AddToClassList("owned-query-group");content.Add(block);block.Add(new Label((d?.name??line.assetId)+" × "+line.quantity){enableRichText=false});
                    block.Add(new Label(line.action==CartAction.Move?"Move → "+line.destination:line.action==CartAction.SellPart||line.action==CartAction.SellUnit?"Sale":line.role==null?"Complete set":"Individual "+line.role));
                    if(line.action==CartAction.NewSet||line.action==CartAction.NewUnits||line.action==CartAction.BuyUsed){var row=new VisualElement();row.AddToClassList("cart-install");row.Add(new Label("Install"));block.Add(row);var toggle=new Toggle{value=line.install};toggle.AddToClassList("setting-toggle");toggle.SetEnabled(!game.NativeBlockPreparationPending(d)||line.install);row.Add(toggle);var s=new SwitchSurface(toggle);toggle.Q(className:"unity-toggle__input").Add(s);switchSurfaces.Add(s);partCartSwitches.Add(s);s.SetValue(line.install);toggle.RegisterValueChangedCallback(e=>{game.SetNativeCartInstall(line,e.newValue);partsStateKey=null;OpenNativeCart();});}
                    var remove=new Button(()=>{game.RemoveNativeCartLine(line);partsStateKey=null;OpenNativeCart();}){text="Remove",name="cartRemove:"+(line.assetId??line.definitionId),focusable=false,tabIndex=-1};remove.AddToClassList("action-button");block.Add(remove);AddSurface(remove,RacingSurface.Kind.SecondaryAction);dialogSurfaces.Add(remove.Q<RacingSurface>());
                }
                if(partsBrowser?.Plan!=null&&partsBrowser.Current!=null)content.Add(new NativeDynoPanel(new[]{new NativeDynoSeries("Current","Current",partsBrowser.Current,NativeDynoStyle.Primary),new NativeDynoSeries("Plan","Planned",partsBrowser.Plan,NativeDynoStyle.Alternative),new NativeDynoSeries("NoWear","No wear",OwnedHealthy(game.Profile.SelectedCar),NativeDynoStyle.Reference)},SharedUnitSettings.Current.torque,"cartDyno"));
            });root.Q<Button>("dialogCheckout")?.SetEnabled(problem==null&&game.CartCount>0&&total<=game.Profile.cash);
        }
        void ConfirmNativeCart()
        {
            PartCartQuote quote;try{quote=game.QuoteNativeCart();}catch(Exception error){partsNotice.text=error.Message;return;}
            string signature=UnityEngine.JsonUtility.ToJson(new PartCart.Lines{entries=game.CartLines.Select(l=>l.Copy()).ToList()});
            OpenDialog("Checkout?","Total "+DisplayUnits.Money(quote.total)+" · Cash "+DisplayUnits.Money(game.Profile.cash),new[]{new DialogAction("Cancel",OpenNativeCart),new DialogAction("Confirm",()=>{
                if(signature!=UnityEngine.JsonUtility.ToJson(new PartCart.Lines{entries=game.CartLines.Select(l=>l.Copy()).ToList()})){root.Q<ScrollView>("modalContent").Add(new Label("Cart changed; review it again."));return;}
                if(game.CommitNativeCart(quote)){CloseDialog();partsStateKey=null;partsNotice.text="";}else root.Q<ScrollView>("modalContent").Add(new Label(game.NativeNotice){enableRichText=false});
            },true)});
        }
    }
}

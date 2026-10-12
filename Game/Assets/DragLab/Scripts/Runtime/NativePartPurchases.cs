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
        sealed class PartPurchaseDraft
        {
            internal bool Individual;
            internal string Role;
            internal int Quantity=1;
            internal List<string> Positions=new List<string>();
            internal NativePartBrowser PreviewModel;
            internal string PreviewKey;
            internal NativePartEntry Preview;
        }
        readonly Dictionary<string,PartPurchaseDraft> partPurchases=new Dictionary<string,PartPurchaseDraft>();
        readonly List<RacingSurface> partPurchaseSurfaces=new List<RacingSurface>();
        VisualElement partPurchaseControls;
        string partPurchaseControlsKey;
        PartPurchaseDraft PurchaseDraft(NativePartEntry offer)
        {
            string key=game.Profile.SelectedCar.instanceId+"/"+offer.Id;
            if(!partPurchases.TryGetValue(key,out var draft)){draft=new PartPurchaseDraft();partPurchases[key]=draft;}
            var roles=InventoryUnits.Roles(game.Profile,game.Profile.SelectedCar,offer.Definition);
            if(!roles.Contains(draft.Role)){draft.Role=roles.FirstOrDefault();draft.Positions.Clear();}
            if(roles.Length==0)draft.Individual=false;
            else {var positions=InventoryUnits.Positions(game.Profile,game.Profile.SelectedCar,offer.Definition.id,draft.Role);draft.Positions=draft.Positions.Where(positions.Contains).ToList();}
            return draft;
        }
        NativePartEntry PurchasePreview(NativePartEntry offer)
        {
            if(offer.Source!="New")return offer;var draft=PurchaseDraft(offer);if(!draft.Individual)return offer;
            string key=draft.Role+"/"+string.Join("/",draft.Positions);
            if(draft.PreviewModel!=partsBrowser||draft.PreviewKey!=key){draft.Preview=partsBrowser.PreviewNewUnit(offer,draft.Role,draft.Positions);draft.PreviewKey=key;draft.PreviewModel=partsBrowser;}
            return draft.Preview;
        }
        void ClearPurchaseControls()
        {
            partPurchaseControls?.RemoveFromHierarchy();partPurchaseControls=null;partPurchaseControlsKey=null;
            foreach(var s in partPurchaseSurfaces)surfaces.Remove(s);partPurchaseSurfaces.Clear();
        }
        void PurchaseSurface(VisualElement element,RacingSurface.Kind kind){AddSurface(element,kind);partPurchaseSurfaces.Add(element.Q<RacingSurface>());}
        void PurchaseChoices(VisualElement parent,string id,string[] labels,Func<string> get,Action<string> set)
        {
            var group=new VisualElement();group.AddToClassList("choice-group");group.AddToClassList("part-purchase-choice");parent.Add(group);
            for(int i=0;i<labels.Length;i++){
                string value=labels[i];
                var b=new Button(()=>{set(value);OpenPartDetail(partsDetailId,false);}){name=id+":"+value,text=value,focusable=false,tabIndex=-1};
                b.AddToClassList("choice-button");b.AddToClassList(i==0?"choice-first":i==labels.Length-1?"choice-last":"choice-middle");group.Add(b);
                PurchaseSurface(b,i==0?RacingSurface.Kind.ChoiceFirst:i==labels.Length-1?RacingSurface.Kind.ChoiceLast:RacingSurface.Kind.ChoiceMiddle);
                b.userData=(Action)(()=>b.EnableInClassList("selected",get()==value));
            }
        }
        void BuildPurchaseControls(NativePartEntry offer)
        {
            if(offer.Source!="New"){ClearPurchaseControls();return;}
            var draft=PurchaseDraft(offer);var roles=InventoryUnits.Roles(game.Profile,game.Profile.SelectedCar,offer.Definition);
            int count=InventoryUnits.Count(game.Profile,game.Profile.SelectedCar,offer.Definition);
            string key=game.Profile.SelectedCar.instanceId+"/"+offer.Id+"/"+count+"/"+string.Join("/",roles);
            if(partPurchaseControlsKey!=key){
                ClearPurchaseControls();partPurchaseControlsKey=key;partPurchaseControls=new VisualElement{name="partPurchase"};partPurchaseControls.AddToClassList("part-purchase");
                var row=new VisualElement();row.AddToClassList("part-purchase-row");partPurchaseControls.Add(row);
                if(roles.Length>0)PurchaseChoices(row,"partPurchaseMode",new[]{"Complete set","Individual"},()=>draft.Individual?"Individual":"Complete set",v=>{draft.Individual=v=="Individual";draft.Positions.Clear();});
                var quantity=new VisualElement();quantity.AddToClassList("part-purchase-quantity");row.Add(quantity);
                quantity.Add(new Label{name="partQuantityLabel"});
                Action<int> assign=value=>{draft.Quantity=Mathf.Clamp(value,1,99);if(draft.Positions.Count>draft.Quantity){draft.Positions=draft.Positions.Take(draft.Quantity).ToList();OpenPartDetail(partsDetailId,false);}RefreshPurchaseControls(offer);};
                var minus=new Button(()=>assign(draft.Quantity-1)){name="partQuantityMinus",text="−",focusable=false,tabIndex=-1};minus.AddToClassList("action-button");minus.AddToClassList("part-quantity-step");quantity.Add(minus);PurchaseSurface(minus,RacingSurface.Kind.SecondaryAction);
                var number=new FloatField{name="partQuantity",isDelayed=true,formatString="0"};number.AddToClassList("setting-number");quantity.Add(number);
                number.RegisterValueChangedCallback(e=>{float value=e.newValue;if(float.IsNaN(value)||float.IsInfinity(value)||value!=Mathf.Floor(value)){number.SetValueWithoutNotify(draft.Quantity);partPurchaseControls.Q<Label>("partPurchaseError").text="Enter a whole quantity.";return;}assign((int)Mathf.Clamp(value,1,99));});
                var plus=new Button(()=>assign(draft.Quantity+1)){name="partQuantityPlus",text="+",focusable=false,tabIndex=-1};plus.AddToClassList("action-button");plus.AddToClassList("part-quantity-step");quantity.Add(plus);PurchaseSurface(plus,RacingSurface.Kind.SecondaryAction);
                var roleRow=new VisualElement{name="partPurchaseRoles"};roleRow.AddToClassList("part-purchase-row");roleRow.AddToClassList("part-purchase-roles");partPurchaseControls.Add(roleRow);
                if(roles.Length>1){roleRow.Add(new Label("Type"));PurchaseChoices(roleRow,"partPurchaseRole",roles,()=>draft.Role,v=>{draft.Role=v;draft.Positions.Clear();});}
                var error=new Label{name="partPurchaseError",enableRichText=false};error.AddToClassList("car-critical");error.AddToClassList("part-purchase-error");partPurchaseControls.Add(error);
            }
            partsDetail.Insert(1,partPurchaseControls);RefreshPurchaseControls(offer);
        }
        void RefreshPurchaseControls(NativePartEntry offer)
        {
            if(partPurchaseControls==null)return;var draft=PurchaseDraft(offer);
            foreach(var b in partPurchaseControls.Query<Button>(className:"choice-button").ToList())(b.userData as Action)?.Invoke();
            partPurchaseControls.Q("partPurchaseRoles").EnableInClassList("hidden",!draft.Individual||InventoryUnits.Roles(game.Profile,game.Profile.SelectedCar,offer.Definition).Length<2);
            partPurchaseControls.Q<Label>("partQuantityLabel").text=draft.Individual?"Units":InventoryUnits.Count(game.Profile,game.Profile.SelectedCar,offer.Definition)>1?"Sets":"Quantity";
            partPurchaseControls.Q<FloatField>("partQuantity").SetValueWithoutNotify(draft.Quantity);
            partPurchaseControls.Q<Button>("partQuantityMinus").SetEnabled(draft.Quantity>1);partPurchaseControls.Q<Button>("partQuantityPlus").SetEnabled(draft.Quantity<99);
            partPurchaseControls.Q<Label>("partPurchaseError").text="";RefreshPurchasePrice(offer);
        }
        void RefreshPurchasePrice(NativePartEntry offer)
        {
            var draft=PurchaseDraft(offer);int each=draft.Individual?InventoryPricing.NewUnit(offer.Definition,InventoryUnits.Count(game.Profile,game.Profile.SelectedCar,offer.Definition)):InventoryPricing.NewSet(offer.Definition);
            var price=partsDetail.Q<Label>("partDetailPrice");if(price!=null)price.text=DisplayUnits.Money((long)each*draft.Quantity);
            var label=partsDetail.Q<Label>("partQueued");if(label!=null){int queued=game.CartLines.Where(l=>l.definitionId==offer.Definition.id&&l.action==(draft.Individual?CartAction.NewUnits:CartAction.NewSet)&&l.role==(draft.Individual?draft.Role:null)).Sum(l=>l.quantity);label.text=queued>0?"Queued × "+queued:"";}
        }
        void StagePurchase(NativePartEntry offer,bool install)
        {
            if(offer.Source!="New"){game.StageNativePart(offer,install,offer.Position);partsStateKey=null;return;}
            var draft=PurchaseDraft(offer);
            if(install&&draft.Individual){ChoosePurchasePositions(offer,draft);return;}
            if(install&&InventoryUnits.Roles(game.Profile,game.Profile.SelectedCar,offer.Definition).Length>0){ChooseSetReplacement(offer,draft);return;}
            if(game.StageNativePurchase(offer.Definition.id,draft.Individual?draft.Role:null,draft.Quantity,install))partsStateKey=null;
            else partPurchaseControls.Q<Label>("partPurchaseError").text=game.NativeNotice;
        }
        void ChooseSetReplacement(NativePartEntry offer,PartPurchaseDraft draft)
        {
            var d=offer.Definition;var car=game.Profile.SelectedCar;var binding=car.installed.slots.Find(s=>s.slot==d.category.ToString());
            var fitted=binding==null?null:game.Profile.parts.Find(p=>p.instanceId==binding.partInstanceId);
            var roles=InventoryUnits.Roles(game.Profile,car,d);string role=draft.Role??roles[0];bool individual=false;
            bool matches=fitted?.definitionId==d.id;
            string[] all=fitted==null?new string[0]:d.category==PartCategory.Tires?Enumerable.Range(0,4).Select(i=>"tire:"+i).ToArray():fitted.condition.elements.Select(e=>e.id).ToArray();
            var selected=new HashSet<string>();var rows=new List<Tuple<Button,string>>();Label summary=null;Action update=null;
            OpenDialog("Replace components",null,new[]{new DialogAction("Cancel",CloseDialog),new DialogAction("Add",()=>{
                var targets=all.Where(selected.Contains).ToList();int quantity=individual?targets.Count:draft.Quantity;
                if(!game.StageNativePurchase(d.id,individual?role:null,quantity,true,individual?targets:null)){root.Q<Label>("setReplacementError").text=game.NativeNotice;return;}
                if(individual){draft.Individual=true;draft.Role=role;draft.Quantity=quantity;draft.Positions=targets;}
                CloseDialog();partsStateKey=null;
            },true)},populate:content=>{
                content.Add(new Label(d.name){enableRichText=false});
                QueryChoices(content,"setReplacementMode","Replace",new[]{"Complete set","Individual"},()=>individual?"Individual":"Complete set",value=>{individual=value=="Individual";selected.Clear();update();},2);
                content.Q<Button>("setReplacementMode:Complete set").parent.parent.Q<Label>().RemoveFromHierarchy();
                QueryChoices(content,"setReplacementRole","Type",roles,()=>role,value=>{role=value;selected.Clear();update();},2);
                var roleGroup=content.Q<Button>("setReplacementRole:"+roles[0]).parent.parent;roleGroup.name="setReplacementRoles";
                summary=new Label{name="setReplacementSummary"};content.Add(summary);
                if(!matches){var notice=new Label("Individual replacements require the fitted specification.");notice.AddToClassList("car-warning");content.Add(notice);}
                foreach(string position in all){string target=position;var row=ReplacementPositionRow(content,d,target,"setReplacementPosition:"+target,()=>{if(!matches)return;if(!individual){individual=true;selected.Clear();}if(!selected.Remove(target))selected.Add(target);update();});rows.Add(Tuple.Create(row,target));}
                if(all.Length==0)content.Add(new Label("No fitted units"));
                var error=new Label{name="setReplacementError"};error.AddToClassList("car-critical");content.Add(error);
                update=()=>{
                    var available=InventoryUnits.Positions(game.Profile,car,d.id,role);selected.RemoveWhere(p=>!available.Contains(p));
                    roleGroup.EnableInClassList("hidden",!individual||roles.Length<2);
                    foreach(var button in content.Query<Button>().ToList().Where(b=>b.name.StartsWith("setReplacementMode:")))button.EnableInClassList("selected",button.name=="setReplacementMode:"+(individual?"Individual":"Complete set"));
                    content.Q<Button>("setReplacementMode:Individual").SetEnabled(matches);
                    foreach(var pair in rows){bool valid=available.Contains(pair.Item2);pair.Item1.EnableInClassList("hidden",individual&&!valid);pair.Item1.EnableInClassList("selected",!individual||selected.Contains(pair.Item2));pair.Item1.SetEnabled(matches&&(!individual||valid));}
                    long price=individual?(long)InventoryPricing.NewUnit(d,InventoryUnits.Count(game.Profile,car,d))*selected.Count:(long)InventoryPricing.NewSet(d)*draft.Quantity;
                    summary.text=(individual?selected.Count+" individual "+(selected.Count==1?"replacement":"replacements"):draft.Quantity+" complete "+(draft.Quantity==1?"set":"sets")+" · "+InventoryUnits.Count(game.Profile,car,d)+" units per set")+" · "+DisplayUnits.Money(price)+(individual||draft.Quantity<=1?"":" · "+(draft.Quantity-1)+" sets in storage");
                    root.Q<Button>("dialogAdd")?.SetEnabled(!individual||selected.Count>0);
                };
            });update();
        }
        void ChoosePurchasePositions(NativePartEntry offer,PartPurchaseDraft draft)
        {
            OpenPurchasePositions(offer.Definition,draft.Role,draft.Quantity,draft.Positions,selected=>{
                if(!game.StageNativePurchase(offer.Definition.id,draft.Role,draft.Quantity,true,selected))return false;
                draft.Positions=selected.ToList();return true;
            },()=>partsStateKey=null);
        }
        void ChooseCartUnitPositions(PartCartLine line)
        {
            OpenPurchasePositions(game.Profile.catalog.Find(line.definitionId),line.role,line.quantity,line.positions,
                selected=>game.SetNativeCartUnitPositions(line,selected),()=>{partsStateKey=null;OpenNativeCart();});
        }
        Button ReplacementPositionRow(VisualElement parent,PartDefinition offer,string position,string name,Action clicked)
        {
            var car=game.Profile.SelectedCar;var binding=car.installed.slots.Find(s=>s.slot==offer.category.ToString());
            var part=binding==null?null:game.Profile.parts.Find(p=>p.instanceId==binding.partInstanceId);var fitted=part==null?null:game.Profile.catalog.Find(part.definitionId);
            var element=part?.condition.elements.FirstOrDefault(e=>e.id==position);
            TireCondition tire=position.StartsWith("tire:",StringComparison.Ordinal)&&part?.condition.tires!=null?part.condition.tires[int.Parse(position.Substring(5))]:null;
            double? health=element?.health??tire?.health;bool failed=element?.Failed??tire?.failed??false;
            string title=InventoryUnits.PositionLabel(game.Profile,car,offer.id,position);
            if(element!=null&&offer.category==PartCategory.Pistons)title="Piston "+(element.cylinder+1);
            string type=element?.role??"Tire";
            if(fitted!=null&&offer.category==PartCategory.Pistons)type=PartSemantics.Friendly(fitted.pistonCrown.ToString())+(fitted.name.IndexOf(fitted.quality,StringComparison.OrdinalIgnoreCase)>=0?"":" · "+fitted.quality)+" · "+fitted.boreMm.ToString("0.##")+" mm";
            else if(fitted!=null)type+=" · "+fitted.quality;
            var button=new Button(clicked){name=name,focusable=false,tabIndex=-1};button.AddToClassList("action-button");button.AddToClassList("part-position-choice");parent.Add(button);
            var heading=new VisualElement();heading.AddToClassList("position-heading");button.Add(heading);
            var label=new Label(title){name="positionTitle:"+position,enableRichText=false,pickingMode=PickingMode.Ignore};label.AddToClassList("position-title");heading.Add(label);
            var wear=new Label(health.HasValue?(failed?"Failed · ":"")+"Wear "+((1-health.Value)*100).ToString("0.#")+"%":"Condition unavailable"){name="positionWear:"+position,pickingMode=PickingMode.Ignore};wear.AddToClassList("position-wear");wear.AddToClassList(failed||health<=.25?"car-critical":!health.HasValue||health<=.5?"car-warning":"car-ready");heading.Add(wear);
            var spec=new Label((fitted?.name??"Missing specification")+" · "+type){name="positionSpec:"+position,enableRichText=false,pickingMode=PickingMode.Ignore};spec.AddToClassList("position-spec");button.Add(spec);
            string fault=element!=null&&element.fault!=ElementFault.None?PartSemantics.Friendly(element.fault.ToString())+" · "+(element.severity*100).ToString("0.#")+"%":tire?.failed==true?"Failed tire":element?.Failed==true?"Worn out":"";
            if(fault.Length>0){var reason=new Label(fault){name="positionFault:"+position,enableRichText=false,pickingMode=PickingMode.Ignore};reason.AddToClassList("position-fault");button.Add(reason);}
            button.tooltip=(fitted?.name??"")+"\n"+title+"\n"+type+"\nCondition "+(health.HasValue?health.Value.ToString("P0"):"unavailable")+(fault.Length>0?"\n"+fault:"");
            AddSurface(button,RacingSurface.Kind.SecondaryAction);dialogSurfaces.Add(button.Q<RacingSurface>());return button;
        }
        void OpenPurchasePositions(PartDefinition definition,string role,int quantity,IEnumerable<string> previous,Func<IEnumerable<string>,bool> apply,Action completed)
        {
            var positions=InventoryUnits.Positions(game.Profile,game.Profile.SelectedCar,definition.id,role);
            var selected=new HashSet<string>(previous.Where(positions.Contains));Action update=null;
            OpenDialog("Replacement positions",null,new[]{new DialogAction("Cancel",CloseDialog),new DialogAction("Add",()=>{
                if(!apply(positions.Where(selected.Contains))){root.Q<Label>("purchasePositionsError").text=game.NativeNotice;return;}
                CloseDialog();completed();
            },true)},populate:content=>{
                var buttons=new List<Tuple<Button,string>>();
                content.Add(new Label("Match the fitted specification. Extras go to storage."){enableRichText=false});
                var summary=new Label{name="purchasePositionsSummary"};content.Add(summary);
                update=()=>{summary.text=selected.Count+" installed · "+Math.Max(0,quantity-selected.Count)+" in storage";foreach(var pair in buttons){pair.Item1.EnableInClassList("selected",selected.Contains(pair.Item2));pair.Item1.SetEnabled(selected.Contains(pair.Item2)||selected.Count<quantity);}root.Q<Button>("dialogAdd")?.SetEnabled(selected.Count>0&&selected.Count<=quantity);};
                foreach(var position in positions){string target=position;var b=ReplacementPositionRow(content,definition,target,"purchasePosition:"+target,()=>{if(!selected.Remove(target))selected.Add(target);update();});buttons.Add(Tuple.Create(b,target));}
                var error=new Label{name="purchasePositionsError"};error.AddToClassList("car-critical");content.Add(error);
            });update();
        }
    }
}
